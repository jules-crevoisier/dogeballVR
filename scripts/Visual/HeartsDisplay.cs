using System;
using Godot;

namespace DogeballVR.Visual;

/// <summary>Cœurs restants d'un joueur : au-dessus de l'avatar (suit une hurtbox) ou au dos de la main gauche (VR). ♥ plein, ♡ perdu.</summary>
public partial class HeartsDisplay : Label3D
{
    public Func<int>? Lives { get; set; }
    public int Max { get; set; } = 2;
    public Node3D? Follow { get; set; }
    public Vector3 Offset { get; set; } = new(0, 1.25f, 0);
    string last = "";

    public static HeartsDisplay Create(Func<int> lives, int max, Color color, float pixelSize, bool billboard) => new()
    {
        Lives = lives, Max = max, Modulate = color, FontSize = 96, PixelSize = pixelSize, OutlineModulate = Palette.Panel, OutlineSize = 16,
        Billboard = billboard ? BaseMaterial3D.BillboardModeEnum.Enabled : BaseMaterial3D.BillboardModeEnum.Disabled, NoDepthTest = billboard,
    };

    public override void _Process(double delta)
    {
        if (Follow != null)
        {
            if (!IsInstanceValid(Follow)) { QueueFree(); return; }
            GlobalPosition = Follow.GlobalPosition + Offset;
        }
        int n = Mathf.Clamp(Lives?.Invoke() ?? 0, 0, Max);
        var t = new string('♥', n) + new string('♡', Max - n);
        if (t != last) { Text = t; last = t; }
    }
}
