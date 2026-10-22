using System;
using System.Collections.Generic;
using Godot;

namespace DogeballVR.Audio;

public enum SfxKind
{
    Throw, Catch, Perfect, Tier, Ko, Alert, Parry, Bomb, Hit, Click,
    UiHover, UiClick, UiBack, Countdown, Go, Whistle, Dash, Jump, Land, ChargeTick, KoConfirm, Victory, Defeat, HurtSelf,
    Bounce, Crowd,
}

/// <summary>
/// Sons du jeu : fichiers Ogg (packs CC0, voir assets/audio/CREDITS.md) rangés en res://assets/audio/sfx/&lt;kind&gt;_N.ogg.
/// Une variante au hasard à chaque appel, hauteur légèrement aléatoire, gain de base par type, nombre de voix limité par type
/// (la plus ancienne est coupée), couche grave ajoutée sous les gros impacts. Si un type n'a aucun fichier, repli sur un son
/// synthétisé (balayage de fréquence enveloppé). Spatialisés (AudioStreamPlayer3D) quand une position est donnée.
/// </summary>
public static class Sfx
{
    const string Dir = "res://assets/audio/sfx/";
    const int Rate = 22050;

    /// <summary>Réglages par type : gain de base (dB), voix simultanées max, écart de hauteur aléatoire, couche grave (dB, null = aucune),
    /// rayon d'audibilité 3D (UnitSize).</summary>
    readonly record struct Def(float Db, int Voices, float Jitter = 0.05f, float? LowDb = null, float Unit = 6f);

    static readonly Dictionary<SfxKind, Def> Defs = new()
    {
        [SfxKind.Throw] = new(-3f, 3),
        [SfxKind.Catch] = new(-1f, 3),
        [SfxKind.Perfect] = new(0f, 2, 0.02f, -8f),
        [SfxKind.Tier] = new(-6f, 2, 0.015f),
        [SfxKind.Ko] = new(0f, 2, 0.04f, -3f, 10f),
        [SfxKind.Alert] = new(-3f, 2, 0.02f),
        [SfxKind.Parry] = new(-2f, 3),
        [SfxKind.Bomb] = new(0f, 2, 0.05f, -4f, 12f),
        [SfxKind.Hit] = new(0f, 4, 0.05f, -5f, 8f),
        [SfxKind.Click] = new(-7f, 2),
        [SfxKind.UiHover] = new(-12f, 2, 0.03f),
        [SfxKind.UiClick] = new(-6f, 2, 0.03f),
        [SfxKind.UiBack] = new(-6f, 2, 0.03f),
        [SfxKind.Countdown] = new(-3f, 2, 0f),
        [SfxKind.Go] = new(-1f, 1, 0f, null, 12f),
        [SfxKind.Whistle] = new(-6f, 1, 0.01f, null, 14f),
        [SfxKind.Dash] = new(-5f, 2),
        [SfxKind.Jump] = new(-7f, 2),
        [SfxKind.Land] = new(-3f, 2),
        [SfxKind.ChargeTick] = new(-5f, 2, 0f),
        [SfxKind.KoConfirm] = new(-3f, 2, 0.01f),
        [SfxKind.Victory] = new(-2f, 1, 0f),
        [SfxKind.Defeat] = new(-2f, 1, 0f),
        [SfxKind.HurtSelf] = new(0f, 2, 0.04f, -2f),
        [SfxKind.Bounce] = new(-6f, 3, 0.07f),
        [SfxKind.Crowd] = new(-9f, 2, 0.04f, null, 20f),
    };

    static readonly Dictionary<string, AudioStream[]> files = new();
    static readonly Dictionary<SfxKind, AudioStreamWav> synth = new();
    static readonly Dictionary<SfxKind, List<Node>> voices = new();
    static readonly Dictionary<SfxKind, ulong> lastPlayMs = new();
    static readonly RandomNumberGenerator rng = new();
    static readonly Dictionary<SfxKind, int> lastVariant = new();

    /// <summary>Variantes chargées pour un nom de fichier (« hit » → hit_1.ogg, hit_2.ogg…). Tableau vide si aucune.</summary>
    internal static AudioStream[] Variants(string name)
    {
        if (files.TryGetValue(name, out var list)) return list;
        var found = new List<AudioStream>();
        for (int i = 1; i <= 8; i++)
        {
            string path = $"{Dir}{name}_{i}.ogg";
            if (!ResourceLoader.Exists(path)) break;
            if (GD.Load<AudioStream>(path) is { } s) found.Add(s);
        }
        return files[name] = found.ToArray();
    }

    static string FileName(SfxKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>
    /// Charge tous les sons (fichiers, ou synthèse de repli) pendant le chargement de la partie : sinon le premier KO, la première
    /// touche… lisent le disque ou synthétisent sur l'image même, et la partie saute (mesuré : jusqu'à 180 ms au premier KO).
    /// </summary>
    public static void Preload()
    {
        foreach (SfxKind kind in Enum.GetValues(typeof(SfxKind)))
            if (Variants(FileName(kind)).Length == 0 && !synth.ContainsKey(kind)) synth[kind] = Make(kind);
    }

    /// <summary>Le son à jouer : une variante au hasard (jamais deux fois la même de suite), sinon la synthèse de repli.</summary>
    static AudioStream Pick(SfxKind kind)
    {
        var list = Variants(FileName(kind));
        if (list.Length == 0)
        {
            if (!synth.TryGetValue(kind, out var w)) synth[kind] = w = Make(kind);
            return w;
        }
        int i = list.Length == 1 ? 0 : rng.RandiRange(0, list.Length - 1);
        if (list.Length > 1 && lastVariant.TryGetValue(kind, out var last) && last == i) i = (i + 1) % list.Length;
        lastVariant[kind] = i;
        return list[i];
    }

    /// <summary>Vrai si le type a au moins un fichier (sinon il sonnera en synthèse).</summary>
    public static bool HasFile(SfxKind kind) => Variants(FileName(kind)).Length > 0;

    static string BusOf(SfxKind kind) => kind is SfxKind.UiHover or SfxKind.UiClick or SfxKind.UiBack ? "UI" : "SFX";

    /// <summary>Chaque son joué (type, position s'il est spatialisé) : indicateurs visuels des sons (accessibilité).</summary>
    public static event System.Action<SfxKind, Vector3?>? Played;

    /// <summary>Joue un son ; pitch 1 = normal (les paliers de charge montent la hauteur), volumeDb s'ajoute au gain de base du type.</summary>
    public static void Play(Node context, SfxKind kind, Vector3? position = null, float pitch = 1f, float volumeDb = 0f)
    {
        if (!GodotObject.IsInstanceValid(context) || !context.IsInsideTree()) return;
        var parent = Holder(context.GetTree());
        var def = Defs.TryGetValue(kind, out var d) ? d : new Def(0f, 3);

        // même type relancé dans la même trame (4 balles lancées ensemble) : un seul son, pas de « flam »
        ulong now = Time.GetTicksMsec();
        if (lastPlayMs.TryGetValue(kind, out var prev) && now - prev < 25) return;
        lastPlayMs[kind] = now;
        Played?.Invoke(kind, position);

        if (!voices.TryGetValue(kind, out var active)) voices[kind] = active = new List<Node>();
        active.RemoveAll(n => !GodotObject.IsInstanceValid(n) || n.IsQueuedForDeletion());
        while (active.Count >= def.Voices) { FadeOut(active[0]); active.RemoveAt(0); }   // vol de voix : la plus ancienne se tait (fondu court, sans clic)

        float p = pitch * (1f + rng.RandfRange(-def.Jitter, def.Jitter));
        active.Add(Spawn(parent, Pick(kind), position, p, def.Db + volumeDb, BusOf(kind), def.Unit));
        if (def.LowDb is float low && Variants("low") is { Length: > 0 } lows)   // couche grave : le corps de l'impact
            Spawn(parent, lows[rng.RandiRange(0, lows.Length - 1)], position, p * rng.RandfRange(0.95f, 1.05f), def.Db + volumeDb + low, BusOf(kind), def.Unit * 1.5f);
        if (kind == SfxKind.Ko) Play(context, SfxKind.Crowd, null, 1f, -3f);   // la foule réagit au KO
    }

    static Node3D? holder;

    /// <summary>Lâche les sons en cache à la sortie (une ressource gardée en static fait planter la fermeture).</summary>
    public static void ClearCache()
    {
        files.Clear(); synth.Clear(); voices.Clear(); holder = null;
    }

    /// <summary>Parent commun des voix, à l'origine, sous la racine : jamais « occupé » (un Play depuis un _Ready ne casse rien).
    /// Ajouté en différé ; les voix créées avant son entrée dans l'arbre démarrent à cette entrée (Autoplay).</summary>
    static Node3D Holder(SceneTree tree)
    {
        if (holder != null && GodotObject.IsInstanceValid(holder) && !holder.IsQueuedForDeletion()) return holder;
        holder = new Node3D { Name = "SfxVoices" };
        tree.Root.CallDeferred(Node.MethodName.AddChild, holder);
        return holder;
    }

    static void FadeOut(Node n)
    {
        if (!n.IsInsideTree()) { n.QueueFree(); return; }
        var tw = n.CreateTween();
        tw.TweenProperty(n, "volume_db", -60f, 0.06f);
        tw.TweenCallback(Callable.From(n.QueueFree));
    }

    static Node Spawn(Node3D parent, AudioStream stream, Vector3? position, float pitch, float db, string bus, float unit)
    {
        bool live = parent.IsInsideTree();
        if (position is Vector3 pos)
        {
            var p3 = new AudioStreamPlayer3D
            {
                Stream = stream, PitchScale = pitch, UnitSize = unit, MaxDistance = 45f, VolumeDb = db, Bus = bus,
                AttenuationFilterCutoffHz = 9000f, AttenuationFilterDb = -12f, Position = pos, Autoplay = !live,   // parent à l'origine : position = position globale
            };
            p3.Finished += p3.QueueFree;
            parent.AddChild(p3);
            if (live) p3.Play();
            return p3;
        }
        var p2 = new AudioStreamPlayer { Stream = stream, PitchScale = pitch, VolumeDb = db, Bus = bus, ProcessMode = Node.ProcessModeEnum.Always, Autoplay = !live };
        p2.Finished += p2.QueueFree;
        parent.AddChild(p2);
        if (live) p2.Play();
        return p2;
    }

    // ---------- repli : synthèse (si un fichier manque) ----------

    // fréquence de départ, d'arrivée (Hz), durée (s), volume, forme : 0 sinus, 1 carré doux, 2 bruit filtré, 3 choc
    static readonly Dictionary<SfxKind, (float f0, float f1, float dur, float gain, int wave)> SynthDefs = new()
    {
        [SfxKind.Throw] = (320, 140, 0.18f, 0.45f, 2),
        [SfxKind.Catch] = (220, 330, 0.12f, 0.6f, 1),
        [SfxKind.Perfect] = (440, 990, 0.35f, 0.6f, 0),
        [SfxKind.Tier] = (520, 780, 0.15f, 0.5f, 0),
        [SfxKind.Ko] = (330, 70, 0.55f, 0.7f, 1),
        [SfxKind.Alert] = (880, 880, 0.09f, 0.55f, 0),
        [SfxKind.Parry] = (700, 250, 0.1f, 0.5f, 1),
        [SfxKind.Bomb] = (140, 40, 0.6f, 0.8f, 2),
        [SfxKind.Hit] = (150, 45, 0.32f, 0.9f, 3),
        [SfxKind.Click] = (900, 600, 0.05f, 0.4f, 1),
        [SfxKind.UiHover] = (1200, 1400, 0.03f, 0.25f, 0),
        [SfxKind.UiClick] = (700, 1100, 0.07f, 0.45f, 1),
        [SfxKind.UiBack] = (700, 400, 0.08f, 0.4f, 1),
        [SfxKind.Countdown] = (660, 660, 0.12f, 0.5f, 0),
        [SfxKind.Go] = (880, 1320, 0.35f, 0.6f, 1),
        [SfxKind.Whistle] = (2100, 2300, 0.5f, 0.4f, 0),
        [SfxKind.Dash] = (500, 200, 0.2f, 0.5f, 2),
        [SfxKind.Jump] = (300, 520, 0.1f, 0.35f, 0),
        [SfxKind.Land] = (160, 80, 0.1f, 0.5f, 2),
        [SfxKind.ChargeTick] = (900, 1000, 0.05f, 0.35f, 0),
        [SfxKind.KoConfirm] = (520, 1040, 0.3f, 0.6f, 1),
        [SfxKind.Victory] = (440, 880, 1.0f, 0.6f, 1),
        [SfxKind.Defeat] = (330, 160, 1.0f, 0.6f, 1),
        [SfxKind.HurtSelf] = (200, 60, 0.35f, 0.9f, 3),
        [SfxKind.Bounce] = (180, 110, 0.08f, 0.5f, 3),
        [SfxKind.Crowd] = (300, 300, 1.2f, 0.25f, 2),
    };

    static AudioStreamWav Make(SfxKind kind)
    {
        var (f0, f1, dur, gain, wave) = SynthDefs.TryGetValue(kind, out var s0) ? s0 : (440f, 440f, 0.1f, 0.4f, 0);
        int n = (int)(Rate * dur);
        var data = new byte[n * 2];
        var r = new Random(7 + (int)kind);
        double phase = 0, lp = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / n;
            double f = f0 + (f1 - f0) * t;
            phase += 2 * Math.PI * f / Rate;
            double v = wave switch
            {
                0 => Math.Sin(phase),
                1 => Math.Tanh(2.5 * Math.Sin(phase)),
                2 => (lp += 0.25 * ((r.NextDouble() * 2 - 1) - lp)) * 3,
                _ => Math.Sin(phase) * 1.1 + (r.NextDouble() * 2 - 1) * Math.Exp(-t * 18) * 0.9 + Math.Tanh(3 * Math.Sin(phase * 0.5)) * 0.3,
            };
            double env = Math.Min(1.0, i / (Rate * 0.004)) * Math.Pow(1 - t, 1.6);
            short s = (short)(Math.Clamp(v * env * gain, -1, 1) * short.MaxValue);
            data[i * 2] = (byte)(s & 0xff);
            data[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }
        return new AudioStreamWav { Data = data, Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false };
    }
}
