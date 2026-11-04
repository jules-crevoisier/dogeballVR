using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Vitrine de capture (tools/vitrine/impact.tscn) : Riko touché à gauche, Nova mise KO à droite, sous la lumière de Croix Néon ;
/// déclenche ImpactFx à la 30e image puis écrit --out=PREFIXE_1..4.png à 1, 3, 7 et 14 images après. Ne sert qu'aux images avant/après.
/// </summary>
public partial class ImpactShowcase : Node3D
{
    static readonly int[] Shots = { 31, 33, 37, 44 };
    int frames;
    string outPrefix = "user://vitrine_impact";
    Avatar left = null!, right = null!;

    public override void _Ready()
    {
        foreach (var a in OS.GetCmdlineUserArgs()) if (a.StartsWith("--out=")) outPrefix = a.Substring(6);
        Maps.ArenaBuilder.AddKcEnvironment(this, true, "jour");
        var floor = new StaticBody3D();
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(30, 0.2f, 30) }, Position = Vector3.Down * 0.1f });
        floor.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(30, 30) }, MaterialOverride = Materials.Flat(new Color("3A4057")) });
        AddChild(floor);
        left = Avatar.Create(0, false);
        AddChild(left);
        left.Pose(new Vector3(-1.4f, 1.6f, 0), Mathf.DegToRad(200), new Vector3(-1.75f, 1.0f, 0.2f), new Vector3(-1.05f, 1.0f, 0.2f));
        right = Avatar.Create(1, false);
        AddChild(right);
        right.Pose(new Vector3(1.4f, 1.6f, 0), Mathf.DegToRad(160), new Vector3(1.05f, 1.0f, 0.2f), new Vector3(1.75f, 1.0f, 0.2f));
        var cam = new Camera3D { Fov = 50, Current = true };
        AddChild(cam);
        cam.LookAtFromPosition(new Vector3(0, 1.7f, 5.2f), new Vector3(0, 1.3f, 0));
    }

    public override void _Process(double delta)
    {
        frames++;
        if (frames == 30)
        {
            ImpactFx.Hit(this, new Vector3(-1.4f, 1.25f, 0.25f), Palette.Team(1));
            ImpactFx.Ko(this, new Vector3(1.4f, 1.25f, 0.25f), Palette.Team(0));
            left.PlayAction("Touche", 0.55);
            right.PlayAction("KO", 2);
        }
        int i = System.Array.IndexOf(Shots, frames);
        if (i < 0) return;
        GetViewport().GetTexture().GetImage().SavePng($"{outPrefix}_{i + 1}.png");
        if (i == Shots.Length - 1) GetTree().Quit();
    }
}
