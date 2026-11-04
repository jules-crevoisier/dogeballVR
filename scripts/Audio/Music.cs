using Godot;
using DogeballVR.UI;

namespace DogeballVR.Audio;

/// <summary>
/// Musique et ambiance : une seule instance (nœud « MusicDirector » sous la racine, ProcessMode Always : la pause ne la coupe pas),
/// fondus enchaînés entre pistes sur le bus « Music », ambiance de toit en boucle sur le bus « SFX » pendant le match.
/// PlayIntensity(true) = « dernier carré » : le tempo et la hauteur montent de 6 % (effet chrono qui presse), retour en douceur avec false.
/// Stinger(Victory/Defeat) coupe la musique et joue le jingle (avec la foule pour la victoire) ; Sfx.Play(Victory/Defeat) n'en est
/// que la version courte : appeler l'un ou l'autre, pas les deux.
/// En headless (CI) rien n'est chargé ni joué.
/// </summary>
public static class Music
{
    const string Dir = "res://assets/audio/";
    const float MenuDb = -1f, MatchDb = -2f, AmbienceDb = -4f, StingerDb = -1f, Silent = -50f;
    const float HighPitch = 1.06f, HighBoostDb = 1.5f;

    static Node? director;
    static AudioStreamPlayer? current, ambience;
    static string currentTrack = "";
    static bool high;

    static bool Disabled => DisplayServer.GetName() == "headless";

    static Node? Director()
    {
        if (Disabled) return null;
        if (director != null && GodotObject.IsInstanceValid(director) && director.IsInsideTree()) return director;
        if (Engine.GetMainLoop() is not SceneTree tree) return null;
        AudioBuses.Ensure();
        director = new Node { Name = "MusicDirector", ProcessMode = Node.ProcessModeEnum.Always };
        tree.Root.CallDeferred(Node.MethodName.AddChild, director);   // sûr même pendant un _Ready
        current = ambience = null;
        currentTrack = "";
        return director;
    }

    static AudioStream? Load(string file, bool loop)
    {
        string path = Dir + file;
        if (!ResourceLoader.Exists(path)) { GD.PrintErr($"Musique introuvable : {path}"); return null; }
        var s = GD.Load<AudioStream>(path);
        if (s is AudioStreamOggVorbis ogg) ogg.Loop = loop;   // boucle échantillon-exacte (pistes coupées sur la mesure)
        return s;
    }

    static AudioStreamPlayer? Start(string file, string bus, float db, bool loop, float fadeIn)
    {
        var d = Director();
        var stream = d == null ? null : Load(file, loop);
        if (d == null || stream == null) return null;
        var p = new AudioStreamPlayer { Stream = stream, Bus = bus, VolumeDb = fadeIn > 0 ? Silent : db, ProcessMode = Node.ProcessModeEnum.Always };
        if (!loop) p.Finished += p.QueueFree;
        d.AddChild(p);
        void Go()
        {
            p.Play();
            if (fadeIn > 0) p.CreateTween().TweenProperty(p, "volume_db", db, fadeIn).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        }
        if (p.IsInsideTree()) Go(); else p.Ready += Go;   // directeur ajouté en différé : on attend l'entrée dans l'arbre
        return p;
    }

    static void FadeOutAndFree(AudioStreamPlayer? p, float fade)
    {
        if (p == null || !GodotObject.IsInstanceValid(p)) return;
        if (fade <= 0 || !p.IsInsideTree()) { p.QueueFree(); return; }
        var tw = p.CreateTween();
        tw.TweenProperty(p, "volume_db", Silent, fade).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        tw.TweenCallback(Callable.From(p.QueueFree));
    }

    static void CrossfadeTo(string file, float db, float fade)
    {
        if (Disabled) return;
        if (currentTrack == file && current != null && GodotObject.IsInstanceValid(current)) return;   // déjà en cours : on ne relance pas
        FadeOutAndFree(current, fade);
        current = Start(file, "Music", db, true, fade);
        currentTrack = current == null ? "" : file;
        high = false;
    }

    /// <summary>Boucle du menu principal (funk groovy).</summary>
    public static void PlayMenu(float fade = 1.2f)
    {
        CrossfadeTo("music/menu.ogg", MenuDb, fade);
        SetAmbience(false, fade);
    }

    /// <summary>Boucle de match (plus énergique) et ambiance de toit en fond.</summary>
    public static void PlayMatch(float fade = 1.5f)
    {
        CrossfadeTo("music/match.ogg", MatchDb, fade);
        SetAmbience(true, fade * 2f);
    }

    /// <summary>Dernière manche, dernier joueur… : true presse la musique (tempo +6 %, +1,5 dB), false revient à la normale.</summary>
    public static void PlayIntensity(bool on, float fade = 1.5f)
    {
        if (Disabled || on == high || current == null || !GodotObject.IsInstanceValid(current) || !current.IsInsideTree()) return;
        high = on;
        float baseDb = currentTrack == "music/menu.ogg" ? MenuDb : MatchDb;
        var tw = current.CreateTween().SetParallel();
        tw.TweenProperty(current, "pitch_scale", on ? HighPitch : 1f, fade).SetTrans(Tween.TransitionType.Sine);
        tw.TweenProperty(current, "volume_db", baseDb + (on ? HighBoostDb : 0f), fade);
    }

    /// <summary>Fin de match : la musique s'efface vite et le jingle part (victoire : avec la foule). Autre type : ignoré.</summary>
    public static void Stinger(SfxKind kind)
    {
        if (Disabled || kind is not (SfxKind.Victory or SfxKind.Defeat)) return;
        FadeOutAndFree(current, 0.35f);
        current = null;
        currentTrack = "";
        high = false;
        SetAmbience(false, 2.5f);
        Start(kind == SfxKind.Victory ? "music/victory.ogg" : "music/defeat.ogg", "Music", StingerDb, false, 0f);
        if (kind == SfxKind.Victory && Sfx.Variants("crowd") is { Length: > 0 } crowd)
            Start("sfx/crowd_1.ogg", "SFX", -8f, false, 0.2f);
    }

    /// <summary>Coupe musique et ambiance en fondu.</summary>
    public static void Stop(float fade = 1f)
    {
        if (Disabled) return;
        FadeOutAndFree(current, fade);
        current = null;
        currentTrack = "";
        high = false;
        SetAmbience(false, fade);
    }

    static void SetAmbience(bool on, float fade)
    {
        bool playing = ambience != null && GodotObject.IsInstanceValid(ambience);
        if (on == playing) return;
        if (on) ambience = Start("ambience/rooftop.ogg", "SFX", AmbienceDb, true, fade);
        else { FadeOutAndFree(ambience, fade); ambience = null; }
    }
}
