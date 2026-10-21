using System;
using Godot;
using DogeballVR.Maps;

namespace DogeballVR.Visual;

/// <summary>
/// Panneau mural de score au bord arrière d'une zone, à 2,5 m de haut, hors de l'espace de marche, tourné vers la zone.
/// Lettres de 0,3 m : lisible du fond de la zone (15 m). Texte = celui du HUD (manche, score, KO, chrono).
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
            Name = $"ScorePanel{zone.Id}", Source = source, FontSize = 96, PixelSize = 0.0042f, Modulate = Palette.Lines,
            OutlineModulate = Palette.Panel, OutlineSize = 24, Position = zone.Center + back + new Vector3(0, 2.5f, 0),
            Rotation = new Vector3(0, Mathf.DegToRad(zone.FacingDegrees + 180f), 0),
        };
        return panel;
    }

    public override void _Process(double delta)
    {
        var t = Source();
        if (t != Text) Text = t;
    }
}
