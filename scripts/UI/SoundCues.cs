using System.Collections.Generic;
using Godot;
using DogeballVR.Audio;
using DogeballVR.Gameplay;

namespace DogeballVR.UI;

/// <summary>
/// Option d'accessibilité « Sons importants à l'écran » : chaque son de jeu qui compte (lancer, explosion, attrape parfaite,
/// touche, KO) entendu à moins de 30 m s'affiche autour du viseur, dans sa direction : une flèche et un mot, qui s'effacent
/// en 1,5 s. Deux sons de la même direction s'empilent vers l'extérieur. Les sons joués sur le joueur lui-même ne sont pas
/// montrés (il les voit déjà) ; les rebonds et attrapes ordinaires non plus, trop fréquents.
/// </summary>
public partial class SoundCues : CanvasLayer
{
    public DesktopPlayer Player { get; set; } = null!;

    const float Range = 30f, Life = 1.5f, Radius = 170f;

    sealed class Cue { public string Text = ""; public Vector3 At; public float Age; public Color Color; }

    readonly List<Cue> cues = new();
    Control canvas = null!;

    static (string text, Color color)? Describe(SfxKind kind) => kind switch
    {
        SfxKind.Throw => ("Lancer", UiKit.Orange),
        SfxKind.Bomb => ("Explosion", UiKit.Pink),
        SfxKind.Perfect => ("Attrape parfaite", UiKit.Yellow),
        SfxKind.Hit => ("Touché", UiKit.Cream),
        SfxKind.Ko => ("KO", UiKit.Yellow),
        _ => null,
    };

    public override void _Ready()
    {
        Layer = 29;   // sous la carte de l'entraînement et les aides
        canvas = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        canvas.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        canvas.Draw += DrawCues;
        AddChild(canvas);
        Sfx.Played += OnPlayed;
    }

    public override void _ExitTree() => Sfx.Played -= OnPlayed;

    void OnPlayed(SfxKind kind, Vector3? position)
    {
        if (!GameSettings.SoundCues || position is not Vector3 at || Describe(kind) is not { } desc) return;
        var (text, color) = desc;
        if (Player == null || !IsInstanceValid(Player)) return;
        float d = at.DistanceTo(Player.GlobalPosition);
        if (d > Range || d < 1.6f) return;
        // Même son au même endroit (rebonds en rafale) : on rafraîchit l'indication au lieu d'en empiler.
        foreach (var c in cues)
            if (c.Text == text && c.At.DistanceTo(at) < 3f) { c.At = at; c.Age = 0; return; }
        if (cues.Count >= 5) cues.RemoveAt(0);
        cues.Add(new Cue { Text = text, At = at, Color = color });
    }

    public override void _Process(double delta)
    {
        if (cues.Count == 0) return;
        foreach (var c in cues) c.Age += (float)delta;
        cues.RemoveAll(c => c.Age > Life);
        canvas.QueueRedraw();
    }

    void DrawCues()
    {
        var cam = GetViewport().GetCamera3D();
        if (cam == null) return;
        var font = UiKit.Theme.DefaultFont;
        var center = canvas.Size * 0.5f;
        var basis = cam.GlobalTransform.Basis;
        var fwd = new Vector2(-basis.Z.X, -basis.Z.Z).Normalized();
        var right = new Vector2(basis.X.X, basis.X.Z).Normalized();
        var placed = new List<(Vector2 dir, float radius)>();
        for (int i = cues.Count - 1; i >= 0; i--)   // le plus récent au plus près du viseur
        {
            var c = cues[i];
            // Direction vue de dessus, par rapport au regard : devant = en haut de l'écran, derrière = en bas.
            var rel = new Vector2(c.At.X - cam.GlobalPosition.X, c.At.Z - cam.GlobalPosition.Z);
            var dir = new Vector2(rel.Dot(right), -rel.Dot(fwd));
            if (dir.LengthSquared() < 1e-4f) continue;
            dir = dir.Normalized();
            float radius = Radius;
            foreach (var (pd, pr) in placed)
                if (pd.Dot(dir) > 0.9f && pr >= radius) radius = pr + 38f;   // même direction (à 25° près) : un cran plus loin
            placed.Add((dir, radius));
            float a = 1f - Mathf.Clamp((c.Age - Life * 0.6f) / (Life * 0.4f), 0f, 1f);
            const int size = 24;
            var w = font.GetStringSize(c.Text, HorizontalAlignment.Left, -1, size).X;
            // La flèche se pose au-delà du mot, côté extérieur : demi-largeur du texte projetée sur la direction.
            float ext = Mathf.Abs(dir.X) * w * 0.5f + Mathf.Abs(dir.Y) * size * 0.5f;
            var tip = center + dir * (radius + ext + 22f);
            var perp = new Vector2(-dir.Y, dir.X);
            canvas.DrawColoredPolygon(new[] { tip + dir * 6f, tip - dir * 16f + perp * 15f, tip - dir * 16f - perp * 15f }, new Color(UiKit.Navy, 0.9f * a));
            canvas.DrawColoredPolygon(new[] { tip, tip - dir * 12f + perp * 10f, tip - dir * 12f - perp * 10f }, new Color(c.Color, a));
            var at = center + dir * radius - new Vector2(w * 0.5f, -size * 0.35f);
            canvas.DrawStringOutline(font, at, c.Text, HorizontalAlignment.Left, -1, size, 6, new Color(UiKit.Navy, a));
            canvas.DrawString(font, at, c.Text, HorizontalAlignment.Left, -1, size, new Color(c.Color, a));
        }
    }
}
