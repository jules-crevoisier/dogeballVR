using System;
using System.Collections.Generic;
using Godot;

namespace DogeballVR.Audio;

public enum SfxKind { Throw, Catch, Perfect, Tier, Ko, Alert, Parry, Bomb, Hit, Click }

/// <summary>
/// Sons du jeu, synthétisés au démarrage (aucun fichier audio dans le dépôt) : un balayage de fréquence enveloppé par sample.
/// Spatialisés (AudioStreamPlayer3D) quand une position est donnée. À remplacer par de vrais sons sans toucher aux appels.
/// </summary>
public static class Sfx
{
    const int Rate = 22050;
    static readonly Dictionary<SfxKind, AudioStreamWav> cache = new();

    // fréquence de départ, d'arrivée (Hz), durée (s), volume, forme : 0 sinus, 1 carré doux, 2 bruit filtré
    static readonly Dictionary<SfxKind, (float f0, float f1, float dur, float gain, int wave)> Defs = new()
    {
        [SfxKind.Throw] = (320, 140, 0.18f, 0.45f, 2),
        [SfxKind.Catch] = (220, 330, 0.12f, 0.6f, 1),
        [SfxKind.Perfect] = (440, 990, 0.35f, 0.6f, 0),
        [SfxKind.Tier] = (520, 780, 0.15f, 0.5f, 0),
        [SfxKind.Ko] = (330, 70, 0.55f, 0.7f, 1),
        [SfxKind.Alert] = (880, 880, 0.09f, 0.55f, 0),
        [SfxKind.Parry] = (700, 250, 0.1f, 0.5f, 1),
        [SfxKind.Bomb] = (140, 40, 0.6f, 0.8f, 2),
        [SfxKind.Hit] = (150, 45, 0.32f, 0.9f, 3),     // choc : claquement de bruit + grave qui plonge
        [SfxKind.Click] = (900, 600, 0.05f, 0.4f, 1),
    };

    static AudioStreamWav Make(SfxKind kind)
    {
        var (f0, f1, dur, gain, wave) = Defs[kind];
        int n = (int)(Rate * dur);
        var data = new byte[n * 2];
        var rng = new Random(7 + (int)kind);
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
                2 => (lp += 0.25 * ((rng.NextDouble() * 2 - 1) - lp)) * 3,
                _ => Math.Sin(phase) * 1.1 + (rng.NextDouble() * 2 - 1) * Math.Exp(-t * 18) * 0.9 + Math.Tanh(3 * Math.Sin(phase * 0.5)) * 0.3,
            };
            double env = Math.Min(1.0, i / (Rate * 0.004)) * Math.Pow(1 - t, 1.6);
            short s = (short)(Math.Clamp(v * env * gain, -1, 1) * short.MaxValue);
            data[i * 2] = (byte)(s & 0xff);
            data[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }
        return new AudioStreamWav { Data = data, Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false };
    }

    /// <summary>Joue un son ; pitch 1 = normal (les paliers de charge montent la hauteur).</summary>
    public static void Play(Node context, SfxKind kind, Vector3? position = null, float pitch = 1f)
    {
        if (!GodotObject.IsInstanceValid(context) || !context.IsInsideTree()) return;
        if (!cache.TryGetValue(kind, out var stream)) cache[kind] = stream = Make(kind);
        Node player;
        if (position is Vector3 p)
        {
            var p3 = new AudioStreamPlayer3D { Stream = stream, PitchScale = pitch, UnitSize = 6f, MaxDistance = 40f };
            p3.Finished += p3.QueueFree;
            context.GetTree().CurrentScene.AddChild(p3);
            p3.GlobalPosition = p;
            p3.Play();
            player = p3;
        }
        else
        {
            var p2 = new AudioStreamPlayer { Stream = stream, PitchScale = pitch };
            p2.Finished += p2.QueueFree;
            context.GetTree().CurrentScene.AddChild(p2);
            p2.Play();
            player = p2;
        }
    }
}
