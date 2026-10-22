using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Vitrine de capture (tools/vitrine/balle.tscn) des effets de BallFx, cinq balles en rang sous la lumière de Croix Néon :
/// --vitrine=lancers : tir, tir au palier max, cloche, passe, montée de palier ;
/// --vitrine=prises : réception d'un tir, attrape parfaite, passe reçue, ramassage au sol (deux fois, orange et bleu).
/// Déclenche les effets à la 30e image puis écrit --out=PREFIXE_1..3.png 2, 5 et 10 images après. Ne sert qu'aux images avant/après.
/// </summary>
public partial class BallFxShowcase : Node3D
{
    static readonly int[] Shots = { 32, 35, 40 };
    int frames;
    string outPrefix = "user://vitrine_balle", which = "lancers";
    readonly Gameplay.Ball[] balls = new Gameplay.Ball[5];
    static readonly float[] Xs = { -3.6f, -1.8f, 0f, 1.8f, 3.6f };

    public override void _Ready()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--out=")) outPrefix = a.Substring(6);
            if (a.StartsWith("--vitrine=")) which = a.Substring(10);
        }
        Maps.ArenaBuilder.AddKcEnvironment(this, true, "jour");
        var floor = new StaticBody3D();
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(30, 0.2f, 30) }, Position = Vector3.Down * 0.1f });
        floor.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(30, 30) }, MaterialOverride = Materials.Flat(new Color("3A4057")) });
        AddChild(floor);
        bool lancers = which == "lancers";
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i] = Gameplay.Ball.CreatePuppet(1000 + i);
            AddChild(balls[i]);
            int team = i % 2;
            int charge = lancers ? (i == 1 ? 3 : i == 4 ? 2 : 0) : 0;
            float y = !lancers && i >= 3 ? 0.12f : 1.4f;
            balls[i].ApplyNet(new Vector3(Xs[i], y, 0), Vector3.Zero, team, i, charge, false);
        }
        var cam = new Camera3D { Fov = 42, Current = true };
        AddChild(cam);
        cam.LookAtFromPosition(new Vector3(0, 2.2f, 7.4f), new Vector3(0, 0.95f, 0));
    }

    public override void _Process(double delta)
    {
        frames++;
        if (frames == 30) Fire();
        int i = System.Array.IndexOf(Shots, frames);
        if (i < 0) return;
        GetViewport().GetTexture().GetImage().SavePng($"{outPrefix}_{i + 1}.png");
        if (i == Shots.Length - 1) GetTree().Quit();
    }

    void Fire()
    {
        var shot = new Vector3(0.75f, 0.12f, 0.65f).Normalized() * 18f;
        Vector3 P(int i) => balls[i].GlobalPosition;
        if (which == "lancers")
        {
            BallFx.Thrown(this, P(0), shot, 0, 0, 0);
            BallFx.Thrown(this, P(1), shot, 1, 3, 0);
            BallFx.Thrown(this, P(2), new Vector3(0.5f, 0.8f, 0.3f) * 9f, 0, 0, Gameplay.Ball.ModeLob);
            BallFx.Thrown(this, P(3), new Vector3(0.6f, 0.5f, 0.3f) * 8f, 1, 0, Gameplay.Ball.ModePass);
            BallFx.ChargeUp(this, P(4), Palette.Team(0), 2);
            return;
        }
        var incoming = new Vector3(-0.8f, -0.05f, 0.55f) * 16f;
        BallFx.Caught(this, P(0), incoming, 0, BallFx.Catch.Steal, false);
        BallFx.Caught(this, P(1), incoming, 1, BallFx.Catch.Steal, true);
        BallFx.Caught(this, P(2), new Vector3(0, -3f, 4f), 0, BallFx.Catch.Pass, false);
        BallFx.Caught(this, P(3), Vector3.Zero, 1, BallFx.Catch.Pickup, false);
        BallFx.Caught(this, P(4), Vector3.Zero, 0, BallFx.Catch.Pickup, false);
    }
}
