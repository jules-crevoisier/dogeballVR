using System;
using Godot;
using DogeballVR.Maps;

namespace DogeballVR.Visual;

/// <summary>
/// Panneau LED 3D de stade au bord arrière d'une zone VR (2,5 m de haut, hors de l'espace de marche, tourné vers la zone).
/// Remplace le texte flottant nu par un vrai châssis sombre avec barres LED d'équipes (Orange / Bleue) et affichage net à 15 m.
/// </summary>
public partial class ScorePanel : Label3D
{
    public Func<string> Source { get; set; } = () => "";

    public static ScorePanel Create(ZoneDef zone, MapData map, Func<string> source)
    {
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(zone.FacingDegrees));
        var back = basis * new Vector3(0, 0, map.ZoneDepth * 0.5f + 0.9f);   // derrière le joueur (il regarde vers -Z)
        var panel = new ScorePanel
        {
            Name = $"ScorePanel{zone.Id}", Source = source,
            Font = UI.UiKit.Display, FontSize = 96, PixelSize = 0.0042f,
            Modulate = UI.UiKit.Cream, OutlineModulate = new Color("04070f"), OutlineSize = 14,
            Position = zone.Center + back + new Vector3(0, 2.5f, 0),
            Rotation = new Vector3(0, Mathf.DegToRad(zone.FacingDegrees + 180f), 0),
        };
        // Châssis LED sombre derrière le texte (-Z local = dos du panneau)
        var frameMat = new StandardMaterial3D { AlbedoColor = new Color("080d1a"), Roughness = 0.45f, Metallic = 0.35f };
        panel.AddChild(new MeshInstance3D
        {
            Name = "Chassis",
            Mesh = new BoxMesh { Size = new Vector3(4.6f, 1.35f, 0.14f) },
            Position = new Vector3(0, 0, -0.10f),
            MaterialOverride = frameMat,
        });
        // Barres LED Orange (gauche) et Bleue (droite) + filet Volt en haut
        panel.AddChild(new MeshInstance3D
        {
            Name = "LedOrange",
            Mesh = new BoxMesh { Size = new Vector3(0.08f, 1.25f, 0.16f) },
            Position = new Vector3(-2.26f, 0, -0.09f),
            MaterialOverride = Materials.Glow(UI.UiKit.Orange, 1.6f),
        });
        panel.AddChild(new MeshInstance3D
        {
            Name = "LedBlue",
            Mesh = new BoxMesh { Size = new Vector3(0.08f, 1.25f, 0.16f) },
            Position = new Vector3(2.26f, 0, -0.09f),
            MaterialOverride = Materials.Glow(UI.UiKit.Blue, 1.6f),
        });
        panel.AddChild(new MeshInstance3D
        {
            Name = "LedTop",
            Mesh = new BoxMesh { Size = new Vector3(4.44f, 0.05f, 0.16f) },
            Position = new Vector3(0, 0.65f, -0.09f),
            MaterialOverride = Materials.Glow(UI.UiKit.Yellow, 1.4f),
        });
        return panel;
    }

    public override void _Process(double delta)
    {
        var t = Source();
        if (t != Text) Text = t;
    }
}
