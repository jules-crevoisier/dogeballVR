using System.Collections.Generic;
using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Test (--avatar-test) de l'IK des avatars, sans réseau ni casque : une rangée de personnages riggés reçoit des poses fixes
/// comme celles d'un joueur VR distant (armé au-dessus de l'épaule, parade accroupie, accroupi profond, main levée « PASSE ! »),
/// d'un enfant debout, d'un joueur PC accroupi sur une terrasse et d'un bot balle en main. Après 1,5 s : poignets sur les mains
/// réseau, chevilles jamais sous le sol, tête du modèle près de la vraie tête, bras armé levé. « [AvatarTest] TOUT OK » sinon la liste.
/// --ik-off rejoue la même scène sans IK (témoin : le test doit échouer). --avatar-shot=chemin.png enregistre une capture (hors headless).
/// </summary>
public partial class AvatarSelfTest : Node3D
{
    record struct Case(string Name, Vector3 Head, Vector3 Left, Vector3 Right, float Floor, bool Vr, bool HoldsBall);

    // Lacet 0 : le personnage regarde vers -Z, la caméra est devant lui. Positions dans le repère de chaque personnage.
    static readonly Case[] Cases =
    {
        new("armé VR",          new(0, 1.65f, 0), new(-0.25f, 1.25f, -0.45f), new(0.32f, 1.75f, 0.18f), 0f, true, false),
        new("parade accroupie", new(0, 1.15f, 0), new(-0.22f, 1.05f, -0.5f), new(0.22f, 1.1f, -0.52f), 0f, true, false),
        new("accroupi profond", new(0, 0.85f, 0), new(-0.3f, 0.55f, -0.25f), new(0.3f, 0.55f, -0.25f), 0f, true, false),
        new("main levée",       new(0, 1.65f, 0), new(-0.3f, 1.0f, -0.1f), new(0.25f, 1.95f, -0.1f), 0f, true, false),
        new("enfant debout",    new(0, 1.3f, 0), new(-0.25f, 0.85f, -0.2f), new(0.25f, 0.85f, -0.2f), 0f, true, false),
        new("PC accroupi, terrasse", new(0, 0.5f + 0.9f, 0), Vector3.Zero, Vector3.Zero, 0.5f, false, false),
        new("bot balle en main", new(0, 1.6f, 0), Vector3.Zero, Vector3.Zero, 0f, false, true),
    };
    const float Spacing = 1.25f;

    readonly List<Avatar> avatars = new();
    Gameplay.Ball? ball;
    float yaw;   // --avatar-side : tout le monde de profil (genoux, buste penché de l'accroupi)
    double clock;
    bool done;

    public override void _Ready()
    {
        Gameplay.Assist.VrRules = true;   // joueurs humains = casques : leurs mains réseau pilotent les bras
        if (Tools.DevFlags.Has("--ik-off")) Avatar.IkEnabled = false;
        if (Tools.DevFlags.Has("--avatar-side")) yaw = -Mathf.Pi / 2f;
        float x0 = -(Cases.Length - 1) * Spacing / 2f;
        for (int i = 0; i < Cases.Length; i++)
        {
            var c = Cases[i];
            var avatar = Avatar.Create(i % 2, bot: !c.Vr && c.HoldsBall, playerId: 100 + i);
            AddChild(avatar);
            avatars.Add(avatar);
            if (c.HoldsBall) { avatar.Position = new Vector3(x0 + i * Spacing, 0, 0); avatar.Rotation = new Vector3(0, yaw, 0); }   // un bot déplace son nœud, sans Pose
            if (c.Floor > 0f) AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1.1f, c.Floor, 1.1f) }, Position = new Vector3(x0 + i * Spacing, c.Floor / 2f, 0),
                MaterialOverride = Materials.Flat(new Color(0.42f, 0.44f, 0.5f)),
            });
            if (c.HoldsBall)
            {
                // comme Bot ; témoin : l'ancien point de tenue, au milieu de la poitrine
                var hold = new Node3D { Name = "HoldPoint", Position = Avatar.IkEnabled ? Avatar.ArmedBall : new Vector3(0, 1.4f, 0) };
                avatar.AddChild(hold);
                ball = Gameplay.Ball.Create();
                AddChild(ball);
                ball.Hold(hold, 100 + i, i % 2);
            }
        }
        if (DisplayServer.GetName() == "headless") return;
        AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(14, 8) }, MaterialOverride = Materials.Flat(new Color(0.3f, 0.32f, 0.38f)) });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, -30, 0), ShadowEnabled = true });
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.12f, 0.13f, 0.17f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.75f, 0.78f, 0.9f), AmbientLightEnergy = 0.7f,
            },
        });
        for (int i = 0; i < Cases.Length; i++)
            AddChild(new Label3D
            {
                Text = Cases[i].Name, Position = new Vector3(x0 + i * Spacing, 2.25f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = 0.0022f, FontSize = 48, OutlineSize = 10, Modulate = Colors.White,
            });
        var cam = new Camera3D { Current = true, Fov = 40 };
        AddChild(cam);
        // Face (de trois quarts) ou profil : le profil montre les genoux et le buste de l'accroupi.
        cam.LookAtFromPosition(new Vector3(-1.4f, 1.9f, -10.5f), new Vector3(0, 1.15f, 0));
    }

    public override void _Process(double delta)
    {
        if (done) return;
        clock += delta;
        float x0 = -(Cases.Length - 1) * Spacing / 2f;
        for (int i = 0; i < Cases.Length; i++)
        {
            var c = Cases[i];
            var at = new Vector3(x0 + i * Spacing, 0, 0);
            if (c.HoldsBall) continue;   // bot : pas de pose, comme en jeu (il se déplace comme un nœud)
            // Les mains de « PC accroupi » sont celles d'un joueur PC (non suivies) : on envoie des positions quelconques.
            var left = c.Vr ? c.Left : c.Head + new Vector3(-0.3f, -0.25f, -0.6f);
            var right = c.Vr ? c.Right : c.Head + new Vector3(0.3f, -0.3f, -0.9f);
            var turn = new Basis(Vector3.Up, yaw);
            avatars[i].Pose(at + c.Head, yaw, at + c.Head + turn * (left - c.Head), at + c.Head + turn * (right - c.Head), c.Floor);
        }
        if (clock < 1.6) return;
        done = true;
        Report(x0);
        var shot = Tools.DevFlags.Value("--avatar-shot=");
        if (shot != null && DisplayServer.GetName() != "headless")
        {
            GetTree().CreateTimer(0.2).Timeout += () =>
            {
                var err = GetViewport().GetTexture().GetImage().SavePng(shot);
                GD.Print($"[AvatarTest] capture {shot} : {err}");
                GetTree().Quit();
            };
        }
        else GetTree().Quit();
    }

    void Report(float x0)
    {
        bool ok = true;
        void Check(string name, bool pass, string detail) { GD.Print($"[AvatarTest] {(pass ? "OK  " : "FAIL")} {name} : {detail}"); ok &= pass; }
        for (int i = 0; i < Cases.Length; i++)
        {
            var c = Cases[i];
            var ik = avatars[i].FindChild("AvatarIk", true, false) as AvatarIk;
            if (ik == null) { Check(c.Name, false, "pas de squelette riggé (assets/models/avatar_*_rig.glb)"); continue; }
            var at = new Vector3(x0 + i * Spacing, 0, 0);
            Check($"{c.Name} : pieds au sol", ik.FinalAnkleY >= c.Floor + 0.04f, $"cheville la plus basse {ik.FinalAnkleY - c.Floor:0.00} m au-dessus du sol");
            float headY = ik.FinalHead.Y + 0.13f;   // os Tete = base du crâne ; centre de la tête 13 cm plus haut
            if (c.Name != "enfant debout" && !c.HoldsBall)
                Check($"{c.Name} : hauteur de tête", Mathf.Abs(headY - c.Head.Y) < 0.22f, $"modèle {headY:0.00} m, joueur {c.Head.Y:0.00} m");
            if (c.Name == "enfant debout")
                Check($"{c.Name} : pas accroupi", headY > 1.5f, $"tête du modèle à {headY:0.00} m");
            if (c.Name is "armé VR" or "parade accroupie")   // mains à portée du bras du modèle
            {
                // cible du poignet = main mise à l'échelle, moins le décalage paume ; tolérance 6 cm
                var turn = new Basis(Vector3.Up, yaw);
                var l = at + c.Head + turn * (c.Left - c.Head);
                var r = at + c.Head + turn * (c.Right - c.Head);
                float dl = ik.FinalWristL.DistanceTo(l), dr = ik.FinalWristR.DistanceTo(r);
                Check($"{c.Name} : poignets sur les mains réseau", dl < 0.16f && dr < 0.16f, $"écart gauche {dl:0.00} m, droite {dr:0.00} m");
            }
            if (c.Name == "main levée") Check($"{c.Name} : main au-dessus de la tête", ik.FinalWristR.Y > headY + 0.1f, $"poignet droit à {ik.FinalWristR.Y:0.00} m");
            if (c.HoldsBall)
            {
                Check($"{c.Name} : bras armé", ik.FinalWristR.Y > 1.4f && ik.FinalWristL.Y > 1.0f, $"poignet lanceur à {ik.FinalWristR.Y:0.00} m, garde à {ik.FinalWristL.Y:0.00} m");
                float d = ball != null ? ball.GlobalPosition.DistanceTo(ik.FinalWristR) : 99f;
                Check($"{c.Name} : balle dans la main armée", d < 0.2f, $"centre de la balle à {d:0.00} m du poignet");
            }
            bool finite = float.IsFinite(ik.FinalWristL.X + ik.FinalWristR.Y + ik.FinalHead.Z + ik.FinalAnkleY);
            if (!finite) Check($"{c.Name} : pose valide", false, "NaN");
        }
        GD.Print(ok ? "[AvatarTest] TOUT OK" : "[AvatarTest] À REVOIR");
    }
}
