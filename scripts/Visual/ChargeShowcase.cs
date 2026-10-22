using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Vitrine de capture (tools/vitrine/charge.tscn) : quatre balles posées côte à côte aux paliers 0 à 3, sous la lumière de Croix Néon,
/// caméra proche ; écrit --out=FICHIER.png après une seconde puis quitte. Ne sert qu'aux images avant/après des effets de balle.
/// </summary>
public partial class ChargeShowcase : Node3D
{
    int frames;
    string outPath = "user://vitrine_charge.png";

    public override void _Ready()
    {
        foreach (var a in OS.GetCmdlineUserArgs()) if (a.StartsWith("--out=")) outPath = a.Substring(6);
        Maps.ArenaBuilder.AddKcEnvironment(this, true, "jour");
        AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(30, 30) }, MaterialOverride = Materials.Flat(new Color("3A4057")) });
        for (int i = 0; i < 4; i++)
        {
            var b = Gameplay.Ball.Create();
            b.Freeze = true;
            AddChild(b);
            b.ApplyNet(new Vector3(-1.05f + i * 0.7f, 0.9f, 0), Vector3.Zero, i % 2, -1, i, false);
        }
        var cam = new Camera3D { Fov = 38, Current = true };
        AddChild(cam);
        cam.LookAtFromPosition(new Vector3(0, 1.25f, 2.6f), new Vector3(0, 0.9f, 0));
    }

    public override void _Process(double delta)
    {
        if (++frames != 60) return;
        GetViewport().GetTexture().GetImage().SavePng(outPath);
        GD.Print($"[Vitrine] {outPath}");
        GetTree().Quit();
    }
}
