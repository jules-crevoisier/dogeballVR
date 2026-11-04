using System;
using Godot;

namespace DogeballVR.Audio;

/// <summary>
/// Test headless des sons : chaque SfxKind a au moins un fichier qui se charge et se joue (2D et 3D), les pistes de musique
/// et l'ambiance se chargent et bouclent. Quitte avec le code 0 si tout passe, 1 sinon. Lancement : ajouter ce nœud à l'arbre
/// (ex. godot --headless -s un_script.gd qui fait root.add_child(load("res://scripts/Audio/AudioSelfTest.cs").new())).
/// </summary>
public partial class AudioSelfTest : Node3D
{
    public override void _Ready()
    {
        bool ok = true;
        void Check(string name, bool pass, string detail) { GD.Print($"[AudioTest] {(pass ? "OK  " : "FAIL")} {name} : {detail}"); ok &= pass; }

        foreach (SfxKind kind in Enum.GetValues(typeof(SfxKind)))
        {
            var list = Sfx.Variants(kind.ToString().ToLowerInvariant());
            double len = list.Length > 0 ? list[0].GetLength() : 0;
            Check($"sfx {kind}", list.Length > 0 && len > 0.01, $"{list.Length} variante(s), {len:0.00} s");
            Sfx.Play(this, kind);
            Sfx.Play(this, kind, Vector3.Zero, 1.1f, -3f);
        }
        Check("couche grave", Sfx.Variants("low").Length > 0, $"{Sfx.Variants("low").Length} variante(s)");
        foreach (var (file, loop) in new[] { ("music/menu.ogg", true), ("music/match.ogg", true), ("music/victory.ogg", false), ("music/defeat.ogg", false), ("ambience/rooftop.ogg", true) })
        {
            string path = "res://assets/audio/" + file;
            var s = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
            if (s is AudioStreamOggVorbis ogg) ogg.Loop = loop;
            Check(file, s is AudioStreamOggVorbis && s.GetLength() > 0.5, s == null ? "introuvable" : $"{s.GetLength():0.0} s{(loop ? ", boucle" : "")}");
        }
        // en headless Music ne fait rien : les appels ne doivent pas lever d'exception
        Music.PlayMenu(); Music.PlayMatch(); Music.PlayIntensity(true); Music.Stinger(SfxKind.Victory); Music.Stop();
        GD.Print($"[AudioTest] {(ok ? "TOUT OK" : "ÉCHEC")}");
        GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit(ok ? 0 : 1);
    }
}
