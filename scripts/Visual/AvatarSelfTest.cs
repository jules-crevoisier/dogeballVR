using System.Collections.Generic;
using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Test (--avatar-test) de l'IK des avatars, sans réseau ni casque : une rangée de personnages riggés reçoit des poses fixes
/// comme celles d'un joueur VR distant (armé au-dessus de l'épaule, de face et de biais, parade accroupie, accroupi profond,
/// main levée « PASSE ! », enfant), d'un joueur PC (accroupi sur une terrasse, balle en main) et de bots (balle en main, élan).
/// Un dernier joueur, hors champ, est tombé dans la fosse. Après 1,6 s : poignets sur les mains réseau, chevilles posées sur le sol
/// (ni dessous ni en l'air), tête du modèle sur la vraie tête, bras armé levé, balle dans la main. « [AvatarTest] TOUT OK » sinon
/// la liste. --ik-off rejoue la scène sans IK (témoin : le test doit échouer). --avatar-side met tout le monde de profil,
/// --avatar-shot=chemin.png enregistre une capture (hors headless).
/// </summary>
public partial class AvatarSelfTest : Node3D
{
    enum Kind { Vr, VrFromBot, VrToBot, VrBallClient, Pc, PcLan, PcBall, PcBallClient, Bot, BotWindup, BotCatch, BotSteal }

    /// <param name="TurnDeg">Coup d'œil : à 2 s la tête tourne de cet angle, les mains restent devant.</param>
    /// <param name="DuckAt">Baissé brusque à cet instant (s) : la tête passe à <c>DuckTo</c> ; vérifié 0,1 s plus tard.</param>
    record struct Case(string Name, Kind Kind, Vector3 Head, Vector3 Left, Vector3 Right, float Floor = 0f, float YawDeg = 0f, bool OffCamera = false,
        float DuckAt = 0f, float DuckTo = 0f, float TurnDeg = 0f);

    // Lacet 0 : le personnage regarde vers -Z, la caméra est devant lui. Positions dans le repère de chaque personnage.
    static readonly Case[] Cases =
    {
        new("armé VR", Kind.Vr, new(0, 1.65f, 0), new(-0.25f, 1.25f, -0.45f), new(0.32f, 1.75f, 0.18f)),
        new("armé VR, de biais", Kind.Vr, new(0, 1.65f, 0), new(-0.25f, 1.25f, -0.45f), new(0.32f, 1.75f, 0.18f), YawDeg: 37f),
        new("parade accroupie", Kind.Vr, new(0, 1.15f, 0), new(-0.22f, 1.05f, -0.5f), new(0.22f, 1.1f, -0.52f)),
        new("accroupi profond", Kind.Vr, new(0, 0.85f, 0), new(-0.3f, 0.55f, -0.25f), new(0.3f, 0.55f, -0.25f)),
        new("main levée", Kind.Vr, new(0, 1.65f, 0), new(-0.3f, 1.0f, -0.1f), new(0.25f, 1.95f, -0.1f)),
        new("enfant debout", Kind.Vr, new(0, 1.3f, 0), new(-0.22f, 0.9f, -0.25f), new(0.22f, 0.9f, -0.25f)),
        new("PC accroupi, terrasse", Kind.Pc, new(0, 0.5f + 0.9f, 0), default, default, Floor: 0.5f),
        new("PC balle en main", Kind.PcBall, new(0, 1.6f, 0), default, default),
        new("bot balle en main", Kind.Bot, default, default, default),
        new("bot, élan", Kind.BotWindup, default, default, default),
        new("bot, attrape", Kind.BotCatch, default, default, default),
        new("joueur VR à la place d'un bot", Kind.VrFromBot, new(0, 1.65f, 0), new(-0.3f, 1.45f, -0.4f), new(0.3f, 1.45f, -0.4f)),
        new("baissé d'esquive", Kind.Vr, new(0, 1.65f, 0), new(-0.25f, 1.2f, -0.3f), new(0.25f, 1.2f, -0.3f), OffCamera: true, DuckAt: 2.0f, DuckTo: 1.05f),
        // Hors champ : séquences et cas réseau.
        new("coup d'œil derrière", Kind.Vr, new(0, 1.65f, 0), new(-0.25f, 1.2f, -0.35f), new(0.25f, 1.2f, -0.35f), OffCamera: true, TurnDeg: 80f),
        new("enfant remplacé par un bot", Kind.VrToBot, new(0, 1.3f, 0), new(-0.22f, 0.9f, -0.25f), new(0.22f, 0.9f, -0.25f), OffCamera: true),
        new("PC en LAN sans salle", Kind.PcLan, new(0, 1.6f, 0), default, default, OffCamera: true),
        new("VR balle en main, vu d'un client", Kind.VrBallClient, new(0, 1.65f, 0), new(-0.25f, 1.25f, -0.45f), new(0.32f, 1.75f, 0.18f), OffCamera: true),
        new("PC balle en main, vu d'un client", Kind.PcBallClient, new(0, 1.6f, 0), default, default, OffCamera: true),
        new("bot, balle volée", Kind.BotSteal, default, default, default, OffCamera: true),
        new("chute dans la fosse", Kind.Vr, new(0, -1.2f, 0), new(-0.3f, -1.5f, -0.3f), new(0.3f, -1.5f, -0.3f), OffCamera: true),
    };
    const float Spacing = 1.15f;
    // Mains d'un joueur PC (DesktopPlayer.Pose) : gauche fixe devant, droite = point de tenue de la balle.
    static readonly Vector3 PcLeft = new(-0.3f, -0.25f, -0.6f), PcRight = new(0.3f, -0.3f, -0.9f);

    readonly List<Avatar> avatars = new();
    readonly Dictionary<int, Gameplay.Ball> balls = new();
    float sideYaw;   // --avatar-side : tout le monde de profil (genoux, buste penché de l'accroupi)
    readonly Dictionary<int, float> duckChecked = new();
    Node3D? stealer;
    readonly Dictionary<int, float> catchChecked = new();
    readonly Dictionary<int, Node3D> holds = new();   // point de tenue du jeu (hôte) : le point de lâcher ne doit pas bouger
    bool pcSpinTracked;                               // PC en LAN : bras pris pour des bras VR pendant le coup de souris
    bool swapped;   // tête du modèle 0,1 s après le baissé
    double clock;
    bool done;

    static float X(int i) => Cases[i].OffCamera ? 40f + 3f * i : (i - (Visible - 1) / 2f) * Spacing;
    static readonly int Visible = System.Array.FindAll(Cases, c => !c.OffCamera).Length;
    const double Duration = 2.5;   // la taille debout se mesure sur 1 s de tête stable, l'échelle suit ensuite
    float Yaw(int i) => sideYaw + Mathf.DegToRad(Cases[i].YawDeg);
    Vector3 At(int i) => new(X(i), 0, 0);

    /// <summary>Position du repère du personnage vers le monde (tête comprise).</summary>
    Vector3 World(int i, Vector3 local) => At(i) + Cases[i].Head + new Basis(Vector3.Up, Yaw(i)) * (local - Cases[i].Head);

    public override void _Ready()
    {
        Gameplay.Assist.VrRules = true;   // sans salle en ligne : les humains jouent en casque, sauf les joueurs PC forcés ci-dessous
        if (Tools.DevFlags.Has("--ik-off")) Avatar.IkEnabled = false;
        if (Tools.DevFlags.Has("--avatar-side")) sideYaw = -Mathf.Pi / 2f;
        for (int i = 0; i < Cases.Length; i++)
        {
            var c = Cases[i];
            bool bot = c.Kind is Kind.Bot or Kind.BotWindup or Kind.BotCatch or Kind.BotSteal;
            var avatar = Avatar.Create(i % 2, bot || c.Kind == Kind.VrFromBot, playerId: 100 + i);
            if (c.Kind == Kind.VrFromBot) avatar.IsBot = false;   // comme ClientWorld quand un joueur prend la zone d'un bot
            if (c.Kind is Kind.Pc or Kind.PcBall or Kind.PcBallClient) avatar.TrackHands = false;
            AddChild(avatar);
            avatars.Add(avatar);
            if (bot) { avatar.Position = At(i); avatar.Rotation = new Vector3(0, Yaw(i), 0); }   // un bot déplace son nœud, sans Pose
            if (c.Floor > 0f) AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1.0f, c.Floor, 1.0f) }, Position = At(i) + Vector3.Up * c.Floor / 2f,
                MaterialOverride = Materials.Flat(new Color(0.42f, 0.44f, 0.5f)),
            });
            Node3D? hold = null;
            if (bot)   // comme Bot : point de tenue du jeu au milieu de la poitrine ; l'avatar montre la balle dans sa main
            {
                hold = new Node3D { Position = new Vector3(0, 1.4f, 0) };
                avatar.AddChild(hold);
            }
            else if (c.Kind == Kind.PcBall)
            {
                hold = new Node3D { Position = World(i, c.Head + PcRight) };
                AddChild(hold);
            }
            if (c.Kind is Kind.VrBallClient or Kind.PcBallClient)
            {
                // Client : marionnette tenue par ce joueur, extrapolée 0,5 m devant sa main réseau (avatar en retard d'affichage).
                var puppet = Gameplay.Ball.CreatePuppet(1000 + i);
                AddChild(puppet);
                balls[i] = puppet;
                continue;
            }
            if (hold == null) continue;
            holds[i] = hold;
            var ball = Gameplay.Ball.Create();
            AddChild(ball);
            ball.Hold(hold, 100 + i, i % 2);
            balls[i] = ball;
        }
        if (DisplayServer.GetName() == "headless") return;
        AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(16, 8) }, MaterialOverride = Materials.Flat(new Color(0.3f, 0.32f, 0.38f)) });
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
                Text = Cases[i].Name, Position = At(i) + new Vector3(0, 2.3f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = 0.0028f, FontSize = 48, OutlineSize = 10, Modulate = Colors.White, Width = 420, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
        var cam = new Camera3D { Current = true, Fov = 30 };   // cadré pour une capture en 3:1 (2400x800)
        AddChild(cam);
        cam.LookAtFromPosition(new Vector3(0, 1.75f, -9.8f), new Vector3(0, 1.1f, 0));
    }

    public override void _Process(double delta)
    {
        if (done) return;
        clock += delta;
        for (int i = 0; i < Cases.Length; i++)
        {
            var c = Cases[i];
            if (c.Kind == Kind.BotSteal && clock > 1.5 && stealer == null && balls.TryGetValue(i, out var stolen))
            {
                // balle prise par un autre (attrape d'un adversaire) : pas de lâcher, l'armé doit s'éteindre sans erreur
                stealer = new Node3D { Position = At(i) + new Vector3(0, 1.2f, -2f) };
                AddChild(stealer);
                stolen.Hold(stealer, 999, 1 - i % 2);
            }
            if (c.Kind is Kind.VrBallClient or Kind.PcBallClient && balls.TryGetValue(i, out var net))
            {
                var hand = World(i, c.Kind == Kind.PcBallClient ? c.Head + PcRight : c.Right);
                net.ApplyNet(hand + new Vector3(0, 0, -0.5f), new Vector3(0, 0, -4f), i % 2, 100 + i, 0, true);
            }
            if (c.Kind == Kind.VrToBot && clock > 1.5 && !swapped)
            {
                swapped = true;
                avatars[i].IsBot = true;   // comme ClientWorld : un bot prend la zone (et a la taille du modèle)
            }
            if (c.Kind is Kind.Bot or Kind.BotWindup or Kind.BotCatch or Kind.BotSteal)
            {
                // bot : pas de pose, comme en jeu (il se déplace comme un nœud) ; élan = animation « Lancer » balle en main
                if (c.Kind == Kind.BotWindup && clock > 1.8) avatars[i].PlayAction("Lancer", 5.0);
                if (c.Kind == Kind.BotCatch && clock > 2.0) avatars[i].PlayAction("Attrape", 5.0);
                // balle dans la main 0,2 s après, en plein geste (le clip dure moins que le test) ; la balle suit la main de
                // l'image d'avant : mesuré seulement à 20 images/s ou plus (pas pendant une capture en rendu logiciel)
                if (c.Kind == Kind.BotCatch && clock >= 2.2 && delta < 0.05 && !catchChecked.ContainsKey(i) && balls.TryGetValue(i, out var caught)
                    && avatars[i].FindChild("AvatarIk", true, false) is AvatarIk cik)
                    catchChecked[i] = caught.ShownPosition.DistanceTo(cik.FinalWristR);
                continue;
            }
            bool pc = c.Kind is Kind.Pc or Kind.PcLan or Kind.PcBall or Kind.PcBallClient;
            var head = c.DuckAt > 0f && clock >= c.DuckAt ? new Vector3(c.Head.X, c.DuckTo, c.Head.Z) : c.Head;
            if (c.Kind == Kind.VrToBot && swapped) head = new Vector3(c.Head.X, 1.62f, c.Head.Z);
            var left = pc ? c.Head + PcLeft : c.Left;
            var right = pc ? c.Head + PcRight : c.Right;
            float yaw = Yaw(i) + (c.TurnDeg != 0f && clock >= 2.0 ? Mathf.DegToRad(c.TurnDeg) : 0f);
            // PC en LAN : coup de souris à 300 °/s de 1,5 à 2 s ; la main interpolée à part traîne de 40 ms derrière la tête
            const float Spin = 5.24f;
            float spun = c.Kind == Kind.PcLan ? Spin * Mathf.Clamp((float)clock - 1.5f, 0f, 0.5f) : 0f;
            float handLag = c.Kind == Kind.PcLan && clock > 1.5 && clock < 2.04 ? Spin * 0.04f : 0f;
            yaw += spun;
            var lw = World(i, left + (head - c.Head));
            if (pc) lw = World(i, head) + new Basis(Vector3.Up, yaw - handLag) * PcLeft;   // main factice fixée à la tête
            var rw = World(i, right + (head - c.Head));
            if (pc) rw = World(i, head) + new Basis(Vector3.Up, yaw - handLag) * PcRight;
            avatars[i].Pose(World(i, head), yaw, lw, rw, c.Floor);
            if (c.Kind == Kind.PcLan && clock > 1.4 && avatars[i].ArmsTracked) pcSpinTracked = true;
            if (c.DuckAt > 0f && clock >= c.DuckAt + 0.1f && delta < 0.05 && !duckChecked.ContainsKey(i))   // pas pendant une capture en rendu logiciel
                duckChecked[i] = (avatars[i].FindChild("AvatarIk", true, false) as AvatarIk)?.FinalHead.Y + 0.13f * avatars[i].ModelScale ?? 99f;
        }
        if (clock < Duration) return;
        done = true;
        Report();
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

    void Report()
    {
        bool ok = true;
        void Check(string name, bool pass, string detail) { GD.Print($"[AvatarTest] {(pass ? "OK  " : "FAIL")} {name} : {detail}"); ok &= pass; }
        for (int i = 0; i < Cases.Length; i++)
        {
            var c = Cases[i];
            var ik = avatars[i].FindChild("AvatarIk", true, false) as AvatarIk;
            if (ik == null) { Check(c.Name, false, "pas de squelette riggé (assets/models/avatar_*_rig.glb)"); continue; }
            var basis = new Basis(Vector3.Up, Yaw(i));
            float ankle = ik.FinalAnkleY - c.Floor;
            float headY = ik.FinalHead.Y + 0.13f * avatars[i].ModelScale;   // os Tete = base du crâne, centre de la tête 13 cm plus haut
            bool finite = float.IsFinite(ik.FinalWristL.X + ik.FinalWristR.Y + ik.FinalHead.Z + ik.FinalAnkleY);
            if (!finite) { Check($"{c.Name} : pose valide", false, "NaN"); continue; }
            if (c.OffCamera && c.Head.Y < 0f)
            {
                Check($"{c.Name} : le corps suit la tête", ik.FinalAnkleY < -1.0f && ik.FinalHead.Y < 0f, $"cheville à {ik.FinalAnkleY:0.00} m, tête à {headY:0.00} m");
                continue;
            }
            Check($"{c.Name} : pieds posés", ankle > 0.06f && ankle < 0.2f, $"cheville la plus basse à {ankle:0.00} m du sol");
            float wantHead = c.DuckAt > 0f ? c.DuckTo : c.Kind == Kind.VrToBot ? 1.62f : c.Head.Y;
            if (c.Kind is Kind.Vr or Kind.VrFromBot or Kind.VrToBot or Kind.Pc)
                Check($"{c.Name} : hauteur de tête", Mathf.Abs(headY - wantHead) < 0.06f, $"modèle {headY:0.00} m, joueur {wantHead:0.00} m");
            if (c.DuckAt > 0f && duckChecked.TryGetValue(i, out float early))
                Check($"{c.Name} : tête baissée en 0,1 s", Mathf.Abs(early - c.DuckTo) < 0.12f, $"tête du modèle à {early:0.00} m pour {c.DuckTo:0.00} m");
            if (c.Kind is Kind.Vr or Kind.VrFromBot or Kind.VrBallClient && c.DuckAt == 0f)
            {
                // poignet = main mise à l'échelle, moins 6 cm de paume ; main levée : seule la gauche est à portée
                var l = World(i, c.Left);
                var r = World(i, c.Right);
                float dl = ik.FinalWristL.DistanceTo(l), dr = c.Name == "main levée" ? 0f : ik.FinalWristR.DistanceTo(r);
                Check($"{c.Name} : poignets sur les mains réseau", dl < 0.1f && dr < 0.1f, $"écart gauche {dl:0.00} m, droite {dr:0.00} m");
            }
            if (c.Name == "main levée") Check($"{c.Name} : main au-dessus de la tête", ik.FinalWristR.Y > headY + 0.15f, $"poignet droit à {ik.FinalWristR.Y:0.00} m");
            if (c.TurnDeg != 0f)
            {
                float body = Mathf.RadToDeg(Mathf.Abs(Mathf.Wrap(avatars[i].BodyYaw - Yaw(i), -Mathf.Pi, Mathf.Pi)));
                Check($"{c.Name} : le corps garde son cap", body < c.TurnDeg - 40f, $"corps tourné de {body:0} ° pour une tête à {c.TurnDeg:0} °");
            }
            if (c.Kind is Kind.VrBallClient or Kind.PcBallClient && balls.TryGetValue(i, out var shown))
            {
                float d = shown.GlobalPosition.DistanceTo(ik.FinalWristR);
                Check($"{c.Name} : balle dans la main de l'avatar", d < 0.2f, $"centre de la balle à {d:0.00} m du poignet");
            }
            if (c.Kind == Kind.PcBallClient)
                Check($"{c.Name} : bras armé", ik.FinalWristR.Y > 1.4f, $"poignet lanceur à {ik.FinalWristR.Y:0.00} m");
            if (c.Kind == Kind.BotSteal)
                Check($"{c.Name} : bras rendus à l'animation", ik.RightWeight < 0.05f && ik.LeftWeight < 0.05f, $"poids de l'IK {ik.RightWeight:0.00}");
            if (c.Kind == Kind.PcLan)
                Check($"{c.Name} : reste PC pendant un coup de souris à 300 °/s", !pcSpinTracked, pcSpinTracked ? "bras passés sur les mains factices" : "bras animés");
            if (holds.TryGetValue(i, out var hold) && balls.TryGetValue(i, out var held) && held.IsHeld && held.OwnerPlayerId == 100 + i)
            {
                float moved = held.GlobalPosition.DistanceTo(hold.GlobalPosition);
                Check($"{c.Name} : point de lâcher du jeu inchangé", moved < 0.001f, $"balle de jeu à {moved:0.000} m du point de tenue");
            }
            if (c.Kind is Kind.Pc or Kind.PcLan)
            {
                float d = ik.FinalWristL.DistanceTo(World(i, c.Head + PcLeft));
                Check($"{c.Name} : bras non pilotés par les mains PC", d > 0.15f, $"main gauche à {d:0.00} m de la main réseau figée");
            }
            if (c.Kind == Kind.PcBall && balls.TryGetValue(i, out var pcBall))
            {
                // balle de jeu au point de visée de DesktopPlayer (0,9 m devant la tête), montrée dans la main armée
                float reach = pcBall.ShownPosition.DistanceTo(ik.FinalWristR);
                Check($"{c.Name} : balle montrée dans la main", reach < 0.2f && ik.FinalWristR.Y > 1.4f, $"balle à {reach:0.00} m du poignet, poignet à {ik.FinalWristR.Y:0.00} m");
            }
            if (c.Kind == Kind.BotCatch && catchChecked.TryGetValue(i, out float inHand))
            {
                Check($"{c.Name} : balle dans la main animée", inHand < 0.25f, $"centre de la balle à {inHand:0.00} m du poignet");
            }
            if (c.Kind == Kind.BotCatch)
                Check($"{c.Name} : l'animation d'attrape garde les bras", ik.RightWeight < 0.5f, $"poids de l'IK {ik.RightWeight:0.00}");
            if (c.Kind is Kind.Bot or Kind.BotWindup && balls.TryGetValue(i, out var botBall))
            {
                Check($"{c.Name} : bras armé", ik.FinalWristR.Y > 1.4f && ik.FinalWristL.Y > 1.0f, $"poignet lanceur à {ik.FinalWristR.Y:0.00} m, garde à {ik.FinalWristL.Y:0.00} m");
                float d = botBall.ShownPosition.DistanceTo(ik.FinalWristR);
                Check($"{c.Name} : balle dans la main", d < 0.2f, $"centre de la balle à {d:0.00} m du poignet");
                if (c.Kind == Kind.BotWindup)
                {
                    float back = (basis.Inverse() * (ik.FinalWristR - At(i))).Z;
                    Check($"{c.Name} : bras tiré en arrière", back > 0.15f, $"poignet à {back:0.00} m derrière les épaules");
                }
            }
        }
        GD.Print(ok ? "[AvatarTest] TOUT OK" : "[AvatarTest] À REVOIR");
    }
}
