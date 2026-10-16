using Godot;
using DogeballVR.Net;
using DogeballVR.Maps;
using DogeballVR.Zones;

namespace DogeballVR.Gameplay;

/// <summary>
/// Joueur PC (clavier-souris), pensé comme un jeu PC à part entière, inspiré des commandes de Knockout City :
/// ZQSD/WASD bouger, Espace sauter, Ctrl/C s'accroupir, souris viser ; en jeu libre Maj = dash (2 charges), sur les cartes en zones Maj = sprint et E = esquive,
/// clic gauche maintenu puis relâché : lancer (tap = lancer rapide, maintenu = lancer chargé),
/// clic droit : fenêtre d'attrape (0,5 s) suivie d'une courte recharge. Les balles lentes qui passent
/// près des pieds sont ramassées automatiquement. PageHaut/PageBas : sensibilité. Échap : libérer la souris.
/// Sur les cartes en zones le joueur ne sort pas de sa zone (en jeu libre : de l'arène). Option de test : <c>--pc-autoplay</c> fait jouer le joueur seul.
/// </summary>
public partial class DesktopPlayer : Node3D
{
    // Déplacement
    const float WalkSpeed = 5.5f;        // cartes en zones : marche, Maj sprint à RunSpeed
    const float RunSpeed = 7.5f;         // course constante en jeu libre (pas de sprint : Maj = dash)
    const float ChargeSpeed = 3.8f;      // on ralentit en armant un lancer : choix tactique
    const float AccelUp = 45f, AccelDown = 60f;
    const float JumpSpeed = 3.5f, Gravity = 12f;
    const float FreeJumpSpeed = 5.0f;    // jeu libre : saut d'environ 1 m (l'hôte laisse 5,5 m/s en lobby PC)
    const float DodgeSpeed = 10f, DodgeTime = 0.18f, DodgeCooldown = 1.2f;
    const float DashSpeed = 21.5f, DashTime = 0.2f, DashRecharge = 1.6f;   // 4 m en 0,2 s (la dernière image est ramenée à la course), 2 charges
    const int DashMaxCharges = 2;
    const double DashGap = 0.35;         // délai minimal entre deux dashs
    const float PlayerRadius = 0.3f;
    const float CameraDistance = 3.0f;   // 3e personne : distance de la caméra derrière la tête
    const float EyeHeight = 1.6f;
    const float CrouchHeight = 0.9f;
    const float EyeSpeed = 3.5f;         // m/s : l'hôte plafonne la vitesse verticale à 4 m/s

    // Lancer et attrape
    const float MinThrow = 14f;          // un tap part déjà vite : le lancer ne doit pas être mou
    const float MaxThrow = 30f;
    const float MaxChargeTime = 0.7f;
    const float AssistCone = 7f;         // degrés : l'aide à la visée ne corrige que les presque-touches
    const float AssistStrength = 0.5f;
    const double CatchWindow = 0.45, CatchRecovery = 0.6;   // fenêtre d'attrape, puis blocage du bouton après un appui raté
    const float PerfectMinSpeed = 10f;                      // l'attrape parfaite ne vaut que sur un tir rapide (pas une cloche)
    const double PerfectCatchWindow = 0.18;                 // balle qui arrive dans les 0,18 s après l'appui : attrape parfaite (palier en plus)
    const float CurveSpin = 6f;                             // effet : rad/s, signe = côté (E maintenu + gauche/droite au lâcher)
    const float PassSpeedCap = 24f;
    const float CatchReach = 1.1f;       // de la main tendue ; l'hôte accepte 1,2 m + marge de latence
    const float HandExtend = 0.95f;      // main tendue devant la tête (l'hôte plafonne à 1,0 m)
    const float PickupSlowSpeed = 3.5f;
    const float PickupRadius = 0.9f;

    public ZoneManager Zones { get; set; } = null!;
    /// <summary>Map en moulinet : le joueur circule sur toute l'arène, caméra à la 3e personne, dash au lieu du sprint.</summary>
    public bool FreeRoam { get; set; }
    public Hurtbox Hurtbox { get; set; } = null!;

    /// <summary>Charge du lancer en cours (0 à 1), pour la jauge du HUD.</summary>
    public float ThrowCharge01 => charging ? (float)(chargeTime / MaxChargeTime) : 0f;
    /// <summary>Altitude du plateau (lift) : le joueur et sa caméra montent avec.</summary>
    public float Elevation { get; set; }
    public float HeadHeight => head.Position.Y;
    public Ball? HeldBall => held != null && IsInstanceValid(held) ? held : null;

    /// <summary>Un adversaire est sous le viseur (le HUD passe le viseur en rouge).</summary>
    public bool AimingAtEnemy { get; private set; }
    /// <summary>Attrape : 0 prête, 1 fenêtre ouverte, 2 recharge.</summary>
    public int CatchState => catchWindowLeft > 0 ? 1 : catchCooldownLeft > 0 ? 2 : 0;
    public float CatchCooldown01 => CatchState == 2 ? (float)(1 - catchCooldownLeft / CatchRecovery) : 1f;
    public bool Autoplay => autoplay;
    public float Sensitivity => sensitivity;
    /// <summary>Message bref à afficher (changement de sensibilité…).</summary>
    public string Toast { get; private set; } = "";
    public double ToastUntil { get; private set; }
    public double Time01 => time;

    Camera3D camera = null!;
    Node3D head = null!;             // la tête du joueur ; en vue à la 1re personne c'est la caméra elle-même
    Visual.Avatar? selfAvatar;
    Node3D holdPoint = null!;
    ZoneDef zone = null!;
    double ignoreHeldUntil;          // client : laisse le temps à l'hôte de confirmer un lancer

    /// <summary>Pose envoyée aux autres joueurs : tête, cap, main gauche (repos) et main droite (tendue, ramassage ou tenue).</summary>
    public PoseData Pose => new(head.GlobalPosition, head.GlobalRotation.Y,
        head.GlobalPosition + head.GlobalTransform.Basis * new Vector3(-0.3f, -0.25f, -0.6f), RightHand(), Elevation, dashCount);

    Ball? held;
    Vector2 worldPos;                // libre : position monde (x, z)
    Vector2 localPos;                // position dans le repère de la zone (centre = 0)
    Vector2 velocity;                // m/s, dans le repère monde (x, z)
    float yaw, pitch;
    float eyeY = EyeHeight;
    float jumpY, jumpVel;
    float sensitivity = 0.0022f;
    double chargeTime;
    bool charging;
    double catchWindowLeft, catchCooldownLeft;
    double dodgeLeft;
    Vector2 dodgeDir;
    double scoopUntil;               // la main droite se baisse vers une balle à ramasser (pour que l'hôte valide)
    double pickupBlockedUntil;
    Vector2 spawnPos;
    bool wasEliminated;
    float camFraction = 1f;          // part de la distance caméra libre de décor
    int dashCharges;
    double dashRechargeLeft, dashStartedAt = -10;
    int dashCount;                   // compteur envoyé à l'hôte : chaque dash le fait avancer d'un cran
    int MaxCharges => FreeRoam ? DashMaxCharges : 1;
    double RechargeTime => FreeRoam ? DashRecharge : DodgeCooldown;
    public int DashCharges => dashCharges;
    public int DashMax => MaxCharges;
    public float DashRecharge01 => dashCharges >= MaxCharges ? 1f : (float)(1 - dashRechargeLeft / RechargeTime);
    bool prevJump, prevDodge, prevCatch, prevPass, prevLob, prevFeint;
    double catchOpenedAt = -10, feintUntil, perfectClaimAt = -10;
    bool ignoreClicksUntilRelease, fireLatch;
    float fov = 100f;   // champ horizontal (Knockout City : large) ; cadré sur la largeur pour ne pas être en fisheye en 16:9
    double time;

    // Mode test : le joueur se pilote seul avec la même chaîne d'actions que le clavier-souris.
    bool autoplay;
    readonly RandomNumberGenerator rng = new();
    bool apCurvePending;
    double apLobWaitUntil;
    int apSpecials;
    double apNextSpecial = 3;
    double apFireStart = -1, apNextJump = 2, apNextDodge = 4, apCrouchUntil;

    struct PcInput { public Vector2 Move; public bool Crouch, Jump, Dodge, Fire, Catch, Sprint, Pass, Lob, Feint, Curve; }

    public override void _Ready()
    {
        camera = new Camera3D { Name = "Camera", Current = true, Position = new Vector3(0, EyeHeight, 0), Fov = fov, Near = 0.05f, KeepAspect = Camera3D.KeepAspectEnum.Width };
        if (FreeRoam)
        {
            head = new Node3D { Name = "Head", Position = new Vector3(0, EyeHeight, 0) };
            AddChild(head);
            camera.TopLevel = true;                    // 3e personne : la caméra est posée dans le monde, derrière l'épaule
            AddChild(camera);
        }
        else
        {
            head = camera;
            AddChild(camera);
        }

        holdPoint = new Node3D { Name = "HoldPoint", Position = new Vector3(0.3f, -0.3f, -0.9f), Scale = Vector3.One * 0.55f };   // balle tenue réduite : elle ne masque pas l'écran
        head.AddChild(holdPoint);
        if (FreeRoam) holdPoint.Scale = Vector3.One;   // à la 3e personne la balle garde sa taille

        Hurtbox.Head = head;
        AddChild(Hurtbox);

        zone = Zones.Map.Zones.Find(z => z.Id == Zones.LocalZoneId)!;
        yaw = Mathf.DegToRad(zone.FacingDegrees);
        if (FreeRoam)
        {
            var spawn = Zones.Map.Pc?.Spawns.Find(sp => sp.Zone == zone.Id);
            worldPos = spawn != null ? new Vector2(spawn.X, spawn.Z) : new Vector2(zone.X, zone.Z);
            spawnPos = worldPos;
            selfAvatar = Visual.Avatar.Create(Hurtbox.Team, false);
            selfAvatar.TopLevel = true;
            AddChild(selfAvatar);
        }
        dashCharges = MaxCharges;
        autoplay = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--pc-autoplay") >= 0;
        rng.Seed = 4242;
        LoadSettings();
        if (!autoplay) Input.MouseMode = Input.MouseModeEnum.Captured;
        ApplyTransform();
        CallDeferred(nameof(HideVrLimitWall));
    }

    /// <summary>Le mur lumineux de bord de zone protège un joueur VR qui marche ; au clavier la zone est déjà bloquée, il ne ferait qu'aveugler.</summary>
    void HideVrLimitWall()
    {
        foreach (var n in GetTree().GetNodesInGroup("limit_wall")) ((MeshInstance3D)n).Visible = false;
    }

    void LoadSettings()
    {
        var cfg = new ConfigFile();
        if (cfg.Load("user://settings.cfg") == Error.Ok)
            sensitivity = Mathf.Clamp((float)cfg.GetValue("pc", "sensitivity", sensitivity).AsDouble(), 0.0005f, 0.01f);
    }

    void SaveSettings()
    {
        var cfg = new ConfigFile();
        cfg.Load("user://settings.cfg");
        cfg.SetValue("pc", "sensitivity", sensitivity);
        cfg.Save("user://settings.cfg");
    }

    void ShowToast(string text) { Toast = text; ToastUntil = time + 1.5; }

    public override void _UnhandledInput(InputEvent e)
    {
        if (autoplay) return;
        if (e is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            yaw -= motion.Relative.X * sensitivity;
            pitch = Mathf.Clamp(pitch - motion.Relative.Y * sensitivity, -1.45f, 1.45f);
        }
        else if (e is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Escape) Input.MouseMode = Input.MouseModeEnum.Visible;
            else if (key.Keycode is Key.Pageup or Key.KpAdd) ChangeSensitivity(1.15f);
            else if (key.Keycode is Key.Pagedown or Key.KpSubtract) ChangeSensitivity(1f / 1.15f);
        }
        else if (e is InputEventMouseButton { Pressed: true } && Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;   // le premier clic reprend la souris et ne lance pas
            ignoreClicksUntilRelease = true;
            GetViewport().SetInputAsHandled();
        }
    }

    void ChangeSensitivity(float factor)
    {
        sensitivity = Mathf.Clamp(sensitivity * factor, 0.0005f, 0.01f);
        SaveSettings();
        ShowToast($"Sensibilité : {sensitivity / 0.0022f:0.00}x");
    }

    public override void _Process(double delta)
    {
        head.Rotation = new Vector3(pitch, yaw, 0);   // la vue suit la souris à chaque image (pas seulement à 60 Hz)
        if (FreeRoam) PlaceCamera((float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        time += delta;
        float dt = (float)delta;
        var input = autoplay ? AutoplayInput(delta) : ReadInput();
        if (!autoplay && Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            input = default;                                   // souris libérée : plus de réaction aux clics
            charging = false; chargeTime = 0;                  // Échap en pleine charge annule le lancer au lieu de le déclencher
        }
        if (ignoreClicksUntilRelease)
        {
            if (Input.IsMouseButtonPressed(MouseButton.Left) || Input.IsMouseButtonPressed(MouseButton.Right)) { input.Fire = false; input.Catch = false; }
            else ignoreClicksUntilRelease = false;
        }

        UpdateMotion(input, dt);
        if (BallActions.IsClient)
        {
            var before = held;
            held = Time.GetTicksMsec() / 1000.0 < ignoreHeldUntil ? null : BallActions.FindHeld(GetTree(), Hurtbox.PlayerId);
            if (before != held && before != null && IsInstanceValid(before)) before.LocalHoldOverride = null;
            if (held != null) held.LocalHoldOverride = holdPoint;
            if (before == null && held != null && time - perfectClaimAt < 1.0 && held.Charge > 0) { perfectClaimAt = -10; ShowToast("Attrape parfaite !"); }
        }
        if (held != null && !IsInstanceValid(held)) { held = null; charging = false; chargeTime = 0; }
        if (Hurtbox.Eliminated)
        {
            if (held != null && !BallActions.IsClient) held.Release(Vector3.Zero, 0f);
            held = null; charging = false; chargeTime = 0; catchWindowLeft = 0; AimingAtEnemy = false;
        }
        else HandleBall(input, delta);
        ApplyTransform();
        UpdateCamera(dt);
    }

    PcInput ReadInput()
    {
        var m = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Z) || Input.IsKeyPressed(Key.Up)) m.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) m.Y += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Q) || Input.IsKeyPressed(Key.Left)) m.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) m.X += 1;
        bool jump = Input.IsKeyPressed(Key.Space), dash = Input.IsKeyPressed(FreeRoam ? Key.Shift : Key.E), catchDown = Input.IsMouseButtonPressed(MouseButton.Right);
        bool pass = Input.IsMouseButtonPressed(MouseButton.Middle) || Input.IsKeyPressed(Key.X), lob = Input.IsKeyPressed(Key.R), feint = Input.IsKeyPressed(Key.F);
        var input = new PcInput
        {
            Move = m.LimitLength(1f),
            Crouch = Input.IsKeyPressed(Key.Ctrl) || Input.IsKeyPressed(Key.C),
            Jump = jump && !prevJump,
            Dodge = dash && !prevDodge,
            Sprint = !FreeRoam && Input.IsKeyPressed(Key.Shift),
            Catch = catchDown && !prevCatch,
            Fire = Input.IsMouseButtonPressed(MouseButton.Left),
            Pass = FreeRoam && pass && !prevPass,
            Lob = FreeRoam && lob && !prevLob,
            Feint = FreeRoam && feint && !prevFeint,
            Curve = FreeRoam && Input.IsKeyPressed(Key.E),
        };
        prevJump = jump; prevDodge = dash; prevCatch = catchDown; prevPass = pass; prevLob = lob; prevFeint = feint;
        return input;
    }

    void UpdateMotion(PcInput input, float dt)
    {
        var forward = new Vector2(-Mathf.Sin(yaw), -Mathf.Cos(yaw));
        var right = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
        var wish = right * input.Move.X - forward * input.Move.Y;

        // Dash (Maj) : 4 m en 0,2 s dans la direction voulue (ou vers l'avant), 2 charges qui reviennent une par une,
        // invulnérable pendant l'impulsion. Les cartes en zones (sans jeu libre) gardent une esquive plus courte.
        if (FreeRoam && Hurtbox.Eliminated) wish = Vector2.Zero;   // KO : on ne bouge plus jusqu'au retour
        if (dashCharges < MaxCharges)
        {
            dashRechargeLeft -= dt;
            if (dashRechargeLeft <= 0) { dashCharges++; dashRechargeLeft = RechargeTime; }
        }
        if (input.Dodge && dashCharges > 0 && dodgeLeft <= 0 && time - dashStartedAt >= DashGap && !Hurtbox.Eliminated)
        {
            dodgeDir = wish.LengthSquared() > 0.01f ? wish.Normalized() : forward;
            dodgeLeft = FreeRoam ? DashTime : DodgeTime;
            if (dashCharges == MaxCharges) dashRechargeLeft = RechargeTime;
            dashCharges--;
            dashStartedAt = time;
            dashCount++;
        }
        Hurtbox.DashInvulnerable = FreeRoam && dodgeLeft > 0;

        bool crouched = eyeY < (EyeHeight + CrouchHeight) * 0.5f + 0.1f;
        float top = charging ? ChargeSpeed : FreeRoam || (input.Sprint && input.Move.Y < -0.1f && !crouched) ? RunSpeed : WalkSpeed;
        if (crouched) top *= 0.6f;

        Vector2 target = wish * top;
        if (dodgeLeft > 0)
        {
            dodgeLeft -= dt;
            velocity = dodgeDir * (FreeRoam ? DashSpeed : DodgeSpeed);
            if (dodgeLeft <= 0 && FreeRoam) velocity = dodgeDir * Mathf.Min(velocity.Length(), RunSpeed);   // fin du dash : pas de glissade, la distance reste celle annoncée (4 m)
        }
        else
        {
            float accel = target.LengthSquared() > velocity.LengthSquared() ? AccelUp : AccelDown;
            velocity = velocity.MoveToward(target, accel * dt);
        }

        // Saut ; pas de saut accroupi.
        float jumpSpeed = FreeRoam ? FreeJumpSpeed : JumpSpeed;
        if (jumpY <= 0f && jumpVel <= 0f)
        {
            jumpY = 0; jumpVel = 0;
            if (input.Jump && !crouched && !Hurtbox.Eliminated) jumpVel = jumpSpeed;
        }
        if (jumpVel > 0 || jumpY > 0)
        {
            jumpVel -= Gravity * dt;
            jumpY += jumpVel * dt;
            if (jumpY <= 0) { jumpY = 0; jumpVel = 0; }
        }

        if (jumpY <= 0f) eyeY = Mathf.MoveToward(eyeY, input.Crouch ? CrouchHeight : EyeHeight, EyeSpeed * dt);   // pas d'accroupi en l'air : la vitesse verticale reste sous le plafond de l'hôte

        if (FreeRoam)
        {
            worldPos += velocity * dt;
            var b = Zones.Map.Bounds;
            var c = new Vector2(Mathf.Clamp(worldPos.X, b.MinX + 0.5f, b.MaxX - 0.5f), Mathf.Clamp(worldPos.Y, b.MinZ + 0.5f, b.MaxZ - 0.5f));
            if (c != worldPos) velocity = Vector2.Zero;
            worldPos = c;
            PushOutOfObstacles();
            return;
        }

        // Déplacement monde -> repère de la zone, borné à la zone.
        var world = new Vector3(velocity.X, 0, velocity.Y) * dt;
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(zone.FacingDegrees)).Inverse();
        var local = basis * world;
        localPos += new Vector2(local.X, local.Z);
        float hw = Zones.Map.ZoneWidth * 0.5f - 0.3f;
        float hd = Zones.Map.ZoneDepth * 0.5f - 0.3f;
        var clamped = new Vector2(Mathf.Clamp(localPos.X, -hw, hw), Mathf.Clamp(localPos.Y, -hd, hd));
        if (clamped != localPos) velocity = Vector2.Zero;   // on s'arrête contre le bord
        localPos = clamped;
        PushOutOfObstacles();
    }

    /// <summary>Les couverts, plots et le pylône sont solides : on glisse le long (cercle de 0,3 m contre le rectangle de l'obstacle).</summary>
    void PushOutOfObstacles()
    {
        var zoneBasis = new Basis(Vector3.Up, Mathf.DegToRad(zone.FacingDegrees));
        Vector2 p;
        if (FreeRoam) p = worldPos;
        else { var p3 = zone.Center + zoneBasis * new Vector3(localPos.X, 0, localPos.Y); p = new Vector2(p3.X, p3.Z); }
        bool moved = false;
        void Collide(Vector2 center, Vector2 half, float rot)
        {
            var rel = (p - center).Rotated(rot);      // repère de l'obstacle
            var closest = new Vector2(Mathf.Clamp(rel.X, -half.X, half.X), Mathf.Clamp(rel.Y, -half.Y, half.Y));
            var diff = rel - closest;
            float d = diff.Length();
            if (d >= PlayerRadius) return;
            Vector2 push;
            if (d > 1e-4f) push = diff / d * (PlayerRadius - d);
            else   // centre dans l'obstacle : on sort par le côté le plus proche
            {
                float ox = half.X - Mathf.Abs(rel.X), oz = half.Y - Mathf.Abs(rel.Y);
                push = ox < oz ? new Vector2(Mathf.Sign(rel.X) * (ox + PlayerRadius), 0) : new Vector2(0, Mathf.Sign(rel.Y) * (oz + PlayerRadius));
            }
            p += push.Rotated(-rot);
            moved = true;
        }
        foreach (var o in Zones.Map.Obstacles)
        {
            float baseY = o.Y + (o.Zone == Zones.LocalZoneId ? Elevation : 0f);   // les couverts d'une zone montent avec son plateau
            if (o.Kind == "pit" || baseY > Elevation + 1.5f || baseY + o.Sy < Elevation + 0.3f) continue;
            Collide(new Vector2(o.X, o.Z), new Vector2(o.Sx, o.Sz) * 0.5f, Mathf.DegToRad(o.RotY));
        }
        if (!moved) return;
        if (FreeRoam)
        {
            var b = Zones.Map.Bounds;
            worldPos = new Vector2(Mathf.Clamp(p.X, b.MinX + 0.5f, b.MaxX - 0.5f), Mathf.Clamp(p.Y, b.MinZ + 0.5f, b.MaxZ - 0.5f));
            return;
        }
        var local = zoneBasis.Inverse() * new Vector3(p.X - zone.Center.X, 0, p.Y - zone.Center.Z);
        float hw = Zones.Map.ZoneWidth * 0.5f - 0.3f, hd = Zones.Map.ZoneDepth * 0.5f - 0.3f;
        localPos = new Vector2(Mathf.Clamp(local.X, -hw, hw), Mathf.Clamp(local.Z, -hd, hd));
    }

    Vector3 FlatForward()
    {
        var f = -head.GlobalTransform.Basis.Z with { Y = 0 };
        return f.LengthSquared() < 1e-4f ? Vector3.Zero : f.Normalized();
    }

    /// <summary>Point de ramassage au niveau des pieds, devant soi (à moins de 1 m de la tête, limite de l'hôte).</summary>
    Vector3 ScoopPoint() => head.GlobalPosition + new Vector3(0, -0.85f, 0) + FlatForward() * 0.5f;

    Vector3 RightHand()
    {
        if (catchWindowLeft > 0 || time < feintUntil) return head.GlobalPosition + (-head.GlobalTransform.Basis.Z) * HandExtend;
        if (time < scoopUntil && held == null) return ScoopPoint();
        return holdPoint.GlobalPosition;
    }

    void HandleBall(PcInput input, double delta)
    {
        catchWindowLeft = Mathf.Max(0, catchWindowLeft - delta);
        if (catchWindowLeft <= 0 && catchCooldownLeft > 0) catchCooldownLeft = Mathf.Max(0, catchCooldownLeft - delta);

        // Clic droit : ouvre la fenêtre d'attrape (impossible en tenant déjà une balle).
        if (input.Catch && held == null && catchWindowLeft <= 0 && catchCooldownLeft <= 0)
        {
            catchWindowLeft = CatchWindow;
            catchCooldownLeft = CatchRecovery;
            catchOpenedAt = time;
        }

        // Prend une balle : attrape (main tendue) ou ramassage automatique des balles lentes près des pieds.
        if (held == null && (!BallActions.IsClient || Time.GetTicksMsec() / 1000.0 >= ignoreHeldUntil))
        {
            var hand = head.GlobalPosition + (-head.GlobalTransform.Basis.Z) * HandExtend;
            var scoop = ScoopPoint();
            foreach (var node in GetTree().GetNodesInGroup("balls"))
            {
                if (node is not Ball { IsHeld: false } ball) continue;
                if (ball.IsPass && ball.OwnerTeam != Hurtbox.Team) continue;   // une passe adverse ne s'intercepte pas
                // Un ballon d'allié (passe) qui arrive est pris tout seul ; les tirs adverses demandent l'appui clic droit.
                bool ally = FreeRoam && time > pickupBlockedUntil && ball.IsPass && ball.OwnerTeam == Hurtbox.Team && ball.OwnerPlayerId != Hurtbox.PlayerId && ball.GlobalPosition.DistanceTo(head.GlobalPosition + Vector3.Down * 0.3f) <= 1.3f;
                bool viaCatch = ally || (catchWindowLeft > 0 && ball.GlobalPosition.DistanceTo(hand) <= CatchReach);
                bool hostileFast = ball.OwnerTeam >= 0 && ball.OwnerTeam != Hurtbox.Team && ball.LinearVelocity.Length() >= Ball.HitMinSpeed;
                bool fastShot = hostileFast && ball.LinearVelocity.Length() >= PerfectMinSpeed;
                bool perfect = FreeRoam && !ally && viaCatch && fastShot && time - catchOpenedAt <= PerfectCatchWindow;
                bool slow = ball.LinearVelocity.Length() < PickupSlowSpeed && time > pickupBlockedUntil;
                float scoopDist = ball.GlobalPosition.DistanceTo(scoop);
                if (slow && scoopDist <= PickupRadius + 0.6f) scoopUntil = time + 0.2;
                if (!viaCatch && !(slow && scoopDist <= PickupRadius)) continue;
                if (!BallActions.IsClient) held = ball;
                BallActions.Catch(ball, holdPoint, 1, Hurtbox.PlayerId, Hurtbox.Team, perfect);
                if (perfect && !BallActions.IsClient) { ShowToast("Attrape parfaite !"); GD.Print("[PC] Attrape parfaite"); }
                else if (perfect) perfectClaimAt = time;   // client : on annonce quand l'hôte a confirmé (balle tenue avec un palier)
                if (ally) { GD.Print("[PC] Passe reçue"); pickupBlockedUntil = time + 0.3; }
                if (viaCatch) { catchWindowLeft = 0; catchCooldownLeft = 0; }   // attrape réussie : pas de recharge
                if (BallActions.IsClient) pickupBlockedUntil = time + 0.3;
                break;
            }
        }

        AimingAtEnemy = BestEnemy(3.5f) != null;
        if (!input.Fire) fireLatch = false;
        else if (held == null) fireLatch = true;          // clic déjà enfoncé au ramassage : il faut relâcher avant d'armer un lancer
        bool fire = input.Fire && !fireLatch;

        if (held != null)
        {
            if (input.Feint)
            {
                // Feinte : le bras part comme pour un tir, sans lâcher. Le clic doit être relâché avant de réarmer.
                charging = false; chargeTime = 0; fireLatch = true; fire = false;
                feintUntil = time + 0.35;
                ShowToast("Feinte !");
            }
            else if (input.Pass)
            {
                var mate = BestAlly();
                if (mate == null) ShowToast("Pas d'allié à portée");
                else Launch(ArcVelocity(holdPoint.GlobalPosition, mate.GlobalPosition + Vector3.Up * 0.4f, 4.2f), 0f, Ball.ModePass);
            }
            else if (input.Lob)
            {
                var enemy = BestEnemy(AssistCone * 2f);
                var to = enemy != null ? enemy.GlobalPosition + Vector3.Up * 0.3f : camera.GlobalPosition - camera.GlobalTransform.Basis.Z * AimDistance();
                var flat = new Vector3(to.X - holdPoint.GlobalPosition.X, 0, to.Z - holdPoint.GlobalPosition.Z);
                if (flat.Length() > 30f) { var cut = holdPoint.GlobalPosition + flat.Normalized() * 30f; to = new Vector3(cut.X, to.Y, cut.Z); }   // au-delà, la vitesse maximale ne suffit plus
                Launch(ArcVelocity(holdPoint.GlobalPosition, to, 3.0f), 0f, Ball.ModeLob);
            }
            else if (fire) { charging = true; chargeTime = Mathf.Min(chargeTime + delta, MaxChargeTime); }
            else if (charging)
            {
                float t = (float)(chargeTime / MaxChargeTime);
                float speed = Mathf.Lerp(MinThrow, MaxThrow, t);
                float spin = input.Curve && Mathf.Abs(input.Move.X) > 0.1f ? Mathf.Sign(input.Move.X) * CurveSpin : 0f;   // effet : E maintenu + gauche/droite
                Launch(ThrowVelocity(speed), spin, 0);
            }
        }
        else { charging = false; chargeTime = 0; }
        holdPoint.Position = HoldLocalPosition();
    }

    void Launch(Vector3 velocity, float spin, int mode)
    {
        BallActions.Throw(held!, 1, velocity, spin, mode);
        if (spin != 0) apCurvePending = false; else if (mode == 0) apCurvePending = true;
        GD.Print($"[PC] Lancer {(mode == Ball.ModePass ? "passe" : mode == Ball.ModeLob ? "cloche" : spin != 0 ? "effet" : "tendu")} à {velocity.Length():0.0} m/s");
        if (BallActions.IsClient) held!.Visible = false;   // pas de balle fantôme dans la main en attendant la confirmation de l'hôte
        ignoreHeldUntil = Time.GetTicksMsec() / 1000.0 + 0.4;
        held = null;
        charging = false;
        chargeTime = 0;
        pickupBlockedUntil = time + 0.5;      // ne pas ramasser sa propre balle qui part
        fov = Mathf.Min(fov + 5f, 108f);      // petit coup de FOV : le lancer se sent
    }

    /// <summary>Vitesse initiale d'une cloche : sommet à `apexY` (au moins 0,8 m au-dessus des deux extrémités), gravité pleine (Ball.ArcGravity).</summary>
    static Vector3 ArcVelocity(Vector3 from, Vector3 to, float apexY)
    {
        float g = Ball.ArcGravity;
        float top = Mathf.Max(from.Y, to.Y);
        float apex = apexY > 3f ? Mathf.Max(apexY, top + 0.8f) : top + apexY;   // passe : sommet à 4,2 m absolus ; lob : hauteur relative
        float vy = Mathf.Sqrt(2f * g * (apex - from.Y));
        float disc = vy * vy - 2f * g * (to.Y - from.Y);
        float time = (vy + Mathf.Sqrt(Mathf.Max(disc, 0f))) / g;
        var v = new Vector3((to.X - from.X) / time, vy, (to.Z - from.Z) / time);
        return v.LimitLength(PassSpeedCap);
    }

    /// <summary>Allié visé : le mieux aligné avec le regard dans 35°, sinon le plus proche (jusqu'à 30 m).</summary>
    Hurtbox? BestAlly()
    {
        var from = camera.GlobalPosition;
        var fwd = -camera.GlobalTransform.Basis.Z;
        Hurtbox? best = null, nearest = null;
        float bestAngle = 35f, nearestDist = 30f;
        foreach (var node in GetTree().GetNodesInGroup("hurtboxes"))
        {
            if (node is not Hurtbox h || h == Hurtbox || h.Team != Hurtbox.Team || h.Eliminated) continue;
            var to = h.GlobalPosition - from;
            float angle = Mathf.RadToDeg(fwd.AngleTo(to));
            if (angle < bestAngle) { bestAngle = angle; best = h; }
            float d = h.GlobalPosition.DistanceTo(head.GlobalPosition);
            if (d < nearestDist) { nearestDist = d; nearest = h; }
        }
        return best ?? nearest;
    }

    Vector3 HoldLocalPosition()
    {
        float crouch = Mathf.Clamp((EyeHeight - eyeY) / (EyeHeight - CrouchHeight), 0f, 1f);
        var rest = new Vector3(0.3f, Mathf.Lerp(-0.3f, -0.5f, crouch), Mathf.Lerp(-0.9f, -0.7f, crouch));
        rest.Z += ThrowCharge01 * 0.15f;       // la balle se recule en armant le lancer
        return rest;
    }

    /// <summary>Hurtbox adverse la mieux alignée avec le regard (null si aucune dans le cône).</summary>
    Hurtbox? BestEnemy(float coneDegrees)
    {
        var from = camera.GlobalPosition;
        var fwd = -camera.GlobalTransform.Basis.Z;
        Hurtbox? best = null;
        float bestAngle = coneDegrees;
        foreach (var node in GetTree().GetNodesInGroup("hurtboxes"))
        {
            if (node is not Hurtbox h || h == Hurtbox || h.Team == Hurtbox.Team || h.Eliminated) continue;
            var to = h.GlobalPosition - from;
            if (to.Length() < 1f || to.Length() > 45f) continue;
            float angle = Mathf.RadToDeg(fwd.AngleTo(to));
            if (angle < bestAngle) { bestAngle = angle; best = h; }
        }
        return best;
    }

    /// <summary>Distance du point visé : premier décor touché par le regard (30 m maximum), pour que la compensation de chute vise juste de près comme de loin.</summary>
    float AimDistance()
    {
        var from = camera.GlobalPosition;
        var to = from + (-camera.GlobalTransform.Basis.Z) * 30f;
        var space = GetWorld3D().DirectSpaceState;
        var exclude = new Godot.Collections.Array<Rid>();
        for (int i = 0; i < 4; i++)
        {
            var query = PhysicsRayQueryParameters3D.Create(from, to, 0xFFFFFFFF, exclude);
            var hit = space.IntersectRay(query);
            if (hit.Count == 0) return 30f;
            if (hit["collider"].AsGodotObject() is StaticBody3D) return Mathf.Clamp(from.DistanceTo(hit["position"].AsVector3()), 3f, 30f);
            exclude.Add(hit["rid"].AsRid());   // balle, hurtbox : on regarde derrière
        }
        return 30f;
    }

    /// <summary>
    /// Vitesse de lancer : le regard (corrigé à moitié vers un adversaire presque aligné), visé depuis la main et non depuis l'œil,
    /// avec compensation de la chute de la balle. Le tir part donc là où pointe le viseur à la distance de la cible.
    /// </summary>
    Vector3 ThrowVelocity(float speed)
    {
        var fwd = (-camera.GlobalTransform.Basis.Z).Normalized();
        var enemy = BestEnemy(AssistCone);
        float dist = AimDistance();
        if (enemy != null)
        {
            var to = enemy.GlobalPosition - camera.GlobalPosition;
            dist = to.Length();
            fwd = fwd.Lerp(to / dist, AssistStrength).Normalized();   // petit angle : mélange linéaire (Slerp lève une exception si l'axe n'est pas parfaitement normalisé)
        }
        var aimPoint = camera.GlobalPosition + fwd * dist;
        float t = dist / speed;
        float gravity = 9.8f * Ball.GravityFactor;
        aimPoint += Vector3.Up * (0.5f * gravity * t * t);             // la balle tombe pendant son vol : on vise au-dessus
        var origin = holdPoint.GlobalPosition;
        return (aimPoint - origin).Normalized() * speed;
    }

    void UpdateCamera(float dt)
    {
        float target = charging ? 94f : 100f;
        fov = Mathf.Lerp(fov, target, 1f - Mathf.Exp(-10f * dt));
        camera.Fov = fov;
    }

    /// <summary>3e personne : la caméra flotte derrière l'épaule droite, orientée comme la tête, et reste dans l'arène.</summary>
    void PlaceCamera(float dt)
    {
        var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, pitch);
        var from = head.GlobalPosition;
        var ideal = from + basis * new Vector3(0.75f, 0.25f, CameraDistance);
        var bnd = Zones.Map.Bounds;
        ideal.Y = Mathf.Max(ideal.Y, Elevation + 0.3f);
        // Le décor plein (couverts, pylône) ne doit pas passer devant : on rapproche la caméra du joueur, vite en entrant, doucement en sortant.
        float full = from.DistanceTo(ideal);
        float allowed = full;
        var space = GetWorld3D().DirectSpaceState;
        var exclude = new Godot.Collections.Array<Rid>();
        for (int i = 0; i < 4; i++)
        {
            var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, ideal, 0xFFFFFFFF, exclude));
            if (hit.Count == 0) break;
            var hp = hit["position"].AsVector3();
            bool arenaWall = hp.X < bnd.MinX + 0.6f || hp.X > bnd.MaxX - 0.6f || hp.Z < bnd.MinZ + 0.6f || hp.Z > bnd.MaxZ - 0.6f || hp.Y > bnd.CeilingHeight - 0.5f;
            if (hit["collider"].AsGodotObject() is StaticBody3D && !arenaWall) { allowed = Mathf.Min(full, Mathf.Max(0.2f, from.DistanceTo(hp) - 0.25f)); break; }
            exclude.Add(hit["rid"].AsRid());
        }
        float f = allowed / full;
        camFraction = f < camFraction ? f : Mathf.Lerp(camFraction, f, 1f - Mathf.Exp(-9f * dt));
        var pos = from.Lerp(ideal, camFraction);
        var b = Zones.Map.Bounds;
        pos.X = Mathf.Clamp(pos.X, b.MinX + 0.2f, b.MaxX - 0.2f);
        pos.Z = Mathf.Clamp(pos.Z, b.MinZ + 0.2f, b.MaxZ - 0.2f);
        camera.GlobalTransform = new Transform3D(basis, pos);
    }

    void ApplyTransform()
    {
        var zoneBasis = new Basis(Vector3.Up, Mathf.DegToRad(zone.FacingDegrees));
        if (FreeRoam)
        {
            if (wasEliminated && !Hurtbox.Eliminated) { worldPos = spawnPos; velocity = Vector2.Zero; }   // retour de KO : au point d'apparition
            wasEliminated = Hurtbox.Eliminated;
            GlobalPosition = new Vector3(worldPos.X, Elevation, worldPos.Y);
        }
        else GlobalPosition = zone.Center + zoneBasis * new Vector3(localPos.X, 0, localPos.Y) + new Vector3(0, Elevation, 0);
        head.Position = new Vector3(0, eyeY + jumpY, 0);
        Hurtbox.Elevation = Elevation + jumpY;   // en sautant, les pieds quittent le sol : une balle basse passe dessous
        head.Rotation = new Vector3(pitch, yaw, 0);
        if (selfAvatar != null)
        {
            var pose = Pose;
            selfAvatar.Visible = !Hurtbox.Eliminated && camFraction > 0.4f;   // caméra collée au joueur : on le fond pour ne pas masquer le viseur
            selfAvatar.Pose(pose.Head, pose.Yaw, pose.Left, pose.Right);
        }
    }

    // ---------------------------------------------------------------- mode test

    PcInput AutoplayInput(double delta)
    {
        var input = new PcInput();
        Hurtbox? enemy = null;
        float best = float.MaxValue;
        foreach (var node in GetTree().GetNodesInGroup("hurtboxes"))
        {
            if (node is not Hurtbox h || h.Team == Hurtbox.Team || h.Eliminated) continue;
            float d = h.GlobalPosition.DistanceTo(head.GlobalPosition);
            if (d < best) { best = d; enemy = h; }
        }

        // Cible : une balle libre proche si on n'en tient pas, sinon l'adversaire.
        Vector3? aimPoint = enemy?.GlobalPosition;
        Ball? target = null;
        float ballDist = 9f;
        if (held == null)
        {
            foreach (var node in GetTree().GetNodesInGroup("balls"))
            {
                if (node is not Ball { IsHeld: false } b || b.LinearVelocity.Length() > PickupSlowSpeed) continue;
                float d = b.GlobalPosition.DistanceTo(head.GlobalPosition);
                if (d < ballDist) { ballDist = d; target = b; }
            }
            if (target != null) aimPoint = target.GlobalPosition;
        }

        if (aimPoint is Vector3 p)
        {
            var to = p - head.GlobalPosition;
            float wantYaw = Mathf.Atan2(-to.X, -to.Z);
            float wantPitch = Mathf.Atan2(to.Y, new Vector2(to.X, to.Z).Length());
            float maxTurn = 5f * (float)delta;
            yaw += Mathf.Clamp(Mathf.AngleDifference(yaw, wantYaw), -maxTurn, maxTurn);
            pitch = Mathf.Clamp(Mathf.MoveToward(pitch, wantPitch, maxTurn), -1.4f, 1.4f);
            if (target != null)
            {
                input.Move = new Vector2(0, -1);
                input.Crouch = target.GlobalPosition.Y < 0.5f && ballDist < 2.5f;
            }
            else input.Move = new Vector2(Mathf.Sin((float)time * 0.9f), 0);   // strafe d'un côté à l'autre
        }

        // Attrape les balles qui arrivent vite droit sur nous.
        if (held == null)
            foreach (var node in GetTree().GetNodesInGroup("balls"))
            {
                if (node is not Ball { IsHeld: false } b || b.LinearVelocity.Length() < 10f) continue;
                var rel = head.GlobalPosition - b.GlobalPosition;
                if (rel.Length() < 6f && rel.Normalized().Dot(b.LinearVelocity.Normalized()) > 0.9f) { input.Catch = true; break; }
            }

        // Lancer : on arme un temps aléatoire une fois aligné, puis on relâche.
        if (held != null && enemy != null)
        {
            float err = Mathf.RadToDeg((-head.GlobalTransform.Basis.Z).AngleTo(enemy.GlobalPosition - head.GlobalPosition));
            if (apFireStart < 0 && err < 6f) apFireStart = time + rng.RandfRange(0.0f, 0.7f);
            input.Fire = apFireStart < 0 || time < apFireStart;
        }
        else apFireStart = -1;
        // Effet : après un tir tendu sans effet, le suivant en prend un (E maintenu + appui latéral), quoi qu'il arrive entre-temps. Déterministe : la CI le vérifie.
        if (FreeRoam && held != null && apCurvePending)
        {
            input.Curve = true;
            if (Mathf.Abs(input.Move.X) < 0.3f) input.Move = new Vector2(1f, input.Move.Y);
        }
        // Coups spéciaux du jeu libre, de temps en temps : passe à un allié, feinte, cloche.
        if (FreeRoam && held != null && time > apNextSpecial)
        {
            // Séquence fixe passe, cloche, feinte : chaque coup revient régulièrement (la CI vérifie passe, cloche et effet).
            int which = apSpecials % 3;
            float lobErr = enemy != null ? Mathf.RadToDeg((-head.GlobalTransform.Basis.Z).AngleTo(enemy.GlobalPosition - head.GlobalPosition)) : 999f;
            if (which == 1 && apLobWaitUntil <= 0) apLobWaitUntil = time + 3.0;
            if (which == 1 && lobErr > AssistCone * 2f && time < apLobWaitUntil) { /* cloche : on attend d'être aligné sur un adversaire, 3 s au plus */ }
            else
            {
                apLobWaitUntil = 0;
                apNextSpecial = time + rng.RandfRange(2.5f, 4f);
                apSpecials++;
                if (which == 0) input.Pass = true;
                else if (which == 1) input.Lob = true;
                else input.Feint = true;
            }
        }

        if (time > apNextJump) { input.Jump = true; apNextJump = time + rng.RandfRange(2.5f, 5f); }
        input.Sprint = !FreeRoam;
        if (time > apNextDodge) { input.Dodge = true; apNextDodge = time + rng.RandfRange(5f, 8f); }
        if (time < apCrouchUntil) input.Crouch = true;
        else if (rng.Randf() < 0.002f) apCrouchUntil = time + 1.0;
        return input;
    }
}
