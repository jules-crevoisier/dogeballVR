using Godot;
using DogeballVR.Net;
using DogeballVR.Maps;
using DogeballVR.Zones;
using DogeballVR.Audio;
using DogeballVR.UI;
using DogeballVR.Visual;

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
    const float PadGravity = 5f;          // vol de tremplin : gravité réduite, sommet à 3 m (vitesse verticale 5,5 m/s, sous le plafond de l'hôte)
    const float VoidMargin = 0.8f;        // le dash s'arrête à 0,8 m du vide
    const float PushSpeed = 8f, PushTime = 0.25f;   // poussée de tacle : 2 m en 0,25 s
    const float TackleRadius = 1.1f;      // un dash qui passe à moins de 1,1 m d'un adversaire le tacle
    const float CameraDistance = 3.4f;   // 3e personne : distance de la caméra derrière la tête (assez loin pour voir autour de soi)
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
    /// <summary>Adversaire sur lequel le lancer sera corrigé (balle en main), ou null.</summary>
    public Hurtbox? LockTarget { get; private set; }
    /// <summary>Attrape : 0 prête, 1 fenêtre ouverte, 2 recharge.</summary>
    public int CatchState => catchWindowLeft > 0 ? 1 : catchCooldownLeft > 0 ? 2 : 0;
    public float CatchCooldown01 => CatchState == 2 ? (float)(1 - catchCooldownLeft / CatchRecovery) : 1f;
    public bool Autoplay => autoplay;
    public float Sensitivity => GameSettings.Sensitivity;
    /// <summary>Dash en cours (lignes de vitesse du HUD).</summary>
    public bool Dashing => dodgeLeft > 0 && FreeRoam;
    /// <summary>Compteurs d'actions du joueur (tutoriel, statistiques locales).</summary>
    public int Jumps, Dashes, Throws, ChargedThrows, Catches, PerfectCatches, Passes, Lobs, Feints, Pickups;
    /// <summary>Rotation cumulée de la vue (radians) et distance parcourue (m) : tutoriel.</summary>
    public float LookTravel, WalkTravel;
    /// <summary>Vitesse au sol (m/s), pour les effets de vitesse.</summary>
    public float GroundSpeed => velocity.Length();
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
    // Tremplins, terrasses, fosse, tacle (jeu libre)
    bool padFlight;                  // vol de tremplin en cours : trajectoire guidée jusqu'à la terrasse
    Vector2 padVel;
    double padReadyAt, terraceSince, evictUntil, pushUntil;
    Vector2 evictDir, pushDir;
    double fallenFor;
    int tackledDash = -1;            // numéro du dash qui a déjà tacle (un seul tacle par dash)
    int MaxCharges => FreeRoam ? DashMaxCharges : 1;
    double RechargeTime => FreeRoam ? DashRecharge : DodgeCooldown;
    public int DashCharges => dashCharges;
    public int DashMax => MaxCharges;
    public float DashRecharge01 => dashCharges >= MaxCharges ? 1f : (float)(1 - dashRechargeLeft / RechargeTime);
    bool padActive;
    bool prevJump, prevDodge, prevCatch, prevPass, prevLob, prevFeint, prevCrouch, crouchToggled;
    double catchOpenedAt = -10, feintUntil, perfectClaimAt = -10;
    bool ignoreClicksUntilRelease, fireLatch;
    float fov = 100f;   // champ horizontal (Knockout City : large, réglable) ; cadré sur la largeur pour ne pas être en fisheye en 16:9
    float lean;         // roulis de la caméra en pas chassé (degrés)
    double jumpPressedAt = -10, groundedAt = -10;   // tampon d'appui et « coyote time » du saut
    bool wasAirborne;
    float airVel;
    int chargeTicks;
    double lastGhostAt;    // paliers sonores de la charge du lancer (1/3, 2/3, plein)
    double time;

    // Mode test : le joueur se pilote seul avec la même chaîne d'actions que le clavier-souris.
    bool autoplay;
    readonly RandomNumberGenerator rng = new();
    bool apCurvePending = true;   // le premier tir en prend un déjà
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
        GameSettings.Load();
        InputSetup.Register();
        fov = GameSettings.Fov;
        CameraFx.Reset();
    }

    void ShowToast(string text) { Toast = text; ToastUntil = time + 1.5; }

    public override void _Input(InputEvent e)
    {
        // Dernier périphérique utilisé : la manette joue même quand la souris n'est pas capturée.
        if (e is InputEventJoypadButton { Pressed: true } || e is InputEventJoypadMotion jm && Mathf.Abs(jm.AxisValue) > 0.4f) padActive = true;
        else if (e is InputEventMouseMotion mm && mm.Relative.LengthSquared() > 4f || e is InputEventKey { Pressed: true } || e is InputEventMouseButton { Pressed: true }) padActive = false;
    }

    /// <summary>Visée au stick droit : courbe cubique (précision au centre), un peu de friction sur un adversaire (aide manette uniquement).</summary>
    void PadLook(float dt)
    {
        var look = Input.GetVector("look_left", "look_right", "look_up", "look_down");
        if (look == Vector2.Zero) return;
        float mag = look.Length();
        look = look / mag * (mag * mag * mag);
        float speed = Mathf.DegToRad(260f) * GameSettings.Sensitivity / GameSettings.DefaultSensitivity;   // degrés par seconde à fond
        if (AimingAtEnemy && held != null) speed *= Mathf.Lerp(1f, 0.55f, GameSettings.AimAssist);
        yaw -= look.X * speed * dt;
        pitch = Mathf.Clamp(pitch - look.Y * speed * 0.7f * dt * (GameSettings.InvertY ? -1f : 1f), -1.45f, 1.45f);
        LookTravel += Mathf.Abs(look.X * speed * dt) + Mathf.Abs(look.Y * speed * dt);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (autoplay) return;
        if (e is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            float sens = GameSettings.Sensitivity;
            yaw -= motion.Relative.X * sens;
            LookTravel += Mathf.Abs(motion.Relative.X * sens) + Mathf.Abs(motion.Relative.Y * sens);
            pitch = Mathf.Clamp(pitch - motion.Relative.Y * sens * (GameSettings.InvertY ? -1f : 1f), -1.45f, 1.45f);
        }
        else if (e is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode is Key.Pageup or Key.KpAdd) ChangeSensitivity(1.15f);
            else if (key.Keycode is Key.Pagedown or Key.KpSubtract) ChangeSensitivity(1f / 1.15f);
        }
        else if (e is InputEventMouseButton { Pressed: true } && Input.MouseMode != Input.MouseModeEnum.Captured && !PauseMenu.IsOpen)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;   // le premier clic reprend la souris et ne lance pas
            ignoreClicksUntilRelease = true;
            GetViewport().SetInputAsHandled();
        }
    }

    void ChangeSensitivity(float factor)
    {
        GameSettings.Sensitivity = Mathf.Clamp(GameSettings.Sensitivity * factor, 0.0005f, 0.01f);
        GameSettings.Save();
        ShowToast($"Sensibilité : {GameSettings.Sensitivity / GameSettings.DefaultSensitivity:0.00}x");
    }

    public override void _Process(double delta)
    {
        if (!autoplay && !PauseMenu.IsOpen) PadLook((float)delta);
        head.Rotation = new Vector3(pitch, yaw, 0);   // la vue suit la souris à chaque image (pas seulement à 60 Hz)
        // Effets de caméra en temps réel : la secousse continue pendant l'arrêt sur image (comme les jeux de combat).
        float real = (float)(delta / Mathf.Max(Engine.TimeScale, 0.01));
        var fx = CameraFx.Step(Mathf.Min(real, 0.1f));
        if (FreeRoam) PlaceCamera((float)delta, fx.rotation, fx.offset, real);
        camera.Fov = Mathf.Clamp(fov + fx.fov, 60f, 130f);
    }

    public override void _PhysicsProcess(double delta)
    {
        time += delta;
        float dt = (float)delta;
        var input = autoplay ? AutoplayInput(delta) : ReadInput();
        if (!autoplay && (PauseMenu.IsOpen || Input.MouseMode != Input.MouseModeEnum.Captured && !padActive))
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
        // Actions de l'InputMap (touches physiques réassignables, voir UI.InputSetup) : ZQSD sur AZERTY, WASD sur QWERTY.
        static bool A(string action) => Input.IsActionPressed(action);
        var m = Input.GetVector("move_left", "move_right", "move_forward", "move_back");   // stick analogique ou touches
        bool jump = A("jump"), dash = A(FreeRoam ? "dash" : "curve"), catchDown = A("catch");   // cartes en zones : E esquive, Maj sprint
        bool pass = A("pass"), lob = A("lob"), feint = A("feint"), crouchKey = A("crouch");
        if (GameSettings.ToggleCrouch) { if (crouchKey && !prevCrouch) crouchToggled = !crouchToggled; }
        else crouchToggled = false;
        var input = new PcInput
        {
            Move = m.LimitLength(1f),
            Crouch = GameSettings.ToggleCrouch ? crouchToggled : crouchKey,
            Jump = jump && !prevJump,
            Dodge = dash && !prevDodge,
            Sprint = !FreeRoam && A("dash"),
            Catch = catchDown && !prevCatch,
            Fire = A("fire"),
            Pass = FreeRoam && pass && !prevPass,
            Lob = FreeRoam && lob && !prevLob,
            Feint = FreeRoam && feint && !prevFeint,
            Curve = FreeRoam && A("curve"),
        };
        prevJump = jump; prevDodge = dash; prevCatch = catchDown; prevPass = pass; prevLob = lob; prevFeint = feint; prevCrouch = crouchKey;
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
        if (input.Dodge && dashCharges > 0 && dodgeLeft <= 0 && time - dashStartedAt >= DashGap && !Hurtbox.Eliminated && !(FreeRoam && jumpY > 0.1f && PitGap(worldPos) < 6f))   // pas de dash en l'air près de la fosse
        {
            dodgeDir = wish.LengthSquared() > 0.01f ? wish.Normalized() : forward;
            dodgeLeft = FreeRoam ? DashTime : DodgeTime;
            if (dashCharges == MaxCharges) dashRechargeLeft = RechargeTime;
            dashCharges--;
            dashStartedAt = time;
            dashCount++;
            Dashes++;
            if (FreeRoam && !autoplay) { CameraFx.FovPunch(9f); Sfx.Play(this, SfxKind.Dash, null, 0.95f + 0.1f * dashCharges); }
            if (FreeRoam && Elevation + jumpY < 0.2f) Juice.Dust(this, new Vector3(worldPos.X, Elevation, worldPos.Y), 12, 2.2f);
        }
        Hurtbox.DashInvulnerable = FreeRoam && dodgeLeft > 0;
        if (Hurtbox.DashInvulnerable && time - lastGhostAt > 0.035)
        {
            lastGhostAt = time;
            Juice.Afterimage(this, new Vector3(worldPos.X, Elevation + jumpY, worldPos.Y), Palette.Team(Hurtbox.Team));
        }

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

        // Forces qui prennent la main sur les commandes : vol de tremplin, poussée de tacle, redescente forcée de la terrasse.
        if (FreeRoam)
        {
            if (padFlight) velocity = padVel;
            else if (time < pushUntil) velocity = pushDir * PushSpeed;
            else if (time < evictUntil) velocity = evictDir * 4f;
        }

        // Saut ; pas de saut accroupi.
        if (FreeRoam) StepVerticalFree(input, dt, crouched);
        else
        {
            if (jumpY <= 0f && jumpVel <= 0f)
            {
                jumpY = 0; jumpVel = 0;
                if (input.Jump && !crouched && !Hurtbox.Eliminated) jumpVel = JumpSpeed;
            }
            if (jumpVel > 0 || jumpY > 0)
            {
                jumpVel -= Gravity * dt;
                jumpY += jumpVel * dt;
                if (jumpY <= 0) { jumpY = 0; jumpVel = 0; }
            }
        }

        if (jumpY <= 0f) eyeY = Mathf.MoveToward(eyeY, input.Crouch ? CrouchHeight : EyeHeight, EyeSpeed * dt);   // pas d'accroupi en l'air : la vitesse verticale reste sous le plafond de l'hôte

        if (FreeRoam)
        {
            var step = velocity * dt;
            bool dashing = velocity.Length() > RunSpeed + 1f && !padFlight && time >= pushUntil;
            if (dashing)
            {
                if (PitGap(worldPos + step) < VoidMargin && PitGap(worldPos + step) < PitGap(worldPos) && PitGap(worldPos) > 0f)
                {
                    // Le dash s'arrête à 0,8 m du vide : il ne suffit pas d'un dash pour tomber, il faut courir ou être poussé.
                    step = Vector2.Zero; velocity = Vector2.Zero; dodgeLeft = 0; Hurtbox.DashInvulnerable = false;
                    GD.Print("[PC] Dash arrêté avant le vide");
                }
                else if (Elevation + jumpY < 0.3f) TryTackle();
            }
            worldPos += step;
            WalkTravel += step.Length();
            var b = Zones.Map.Bounds;
            var c = new Vector2(Mathf.Clamp(worldPos.X, b.MinX + 0.5f, b.MaxX - 0.5f), Mathf.Clamp(worldPos.Y, b.MinZ + 0.5f, b.MaxZ - 0.5f));
            if (c != worldPos) velocity = Vector2.Zero;
            worldPos = c;
            PushOutOfObstacles();
            KeepInsidePit();
            if (Elevation > 0 && time - terraceSince > TerraceMaxStay && time >= evictUntil)
            {
                // Plus de 8 s sur la terrasse : on est ramené vers la plaque.
                var tr = Zones.Map.TerraceAt(worldPos.X, worldPos.Y);
                if (tr != null) evictDir = (new Vector2(zone.Center.X - tr.X, zone.Center.Z - tr.Z)).Normalized();
                evictUntil = time + 1.2;
                ShowToast("Terrasse : tu redescends");
            }
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

    const double TerraceMaxStay = 8.0;

    /// <summary>Distance (en m, norme du max) entre le point et le bord de la fosse : positive dehors, négative dedans, grande sans fosse.</summary>
    float PitGap(Vector2 p)
    {
        var pit = Zones.Map.PitRect;
        if (pit == null) return 1000f;
        return Mathf.Max(Mathf.Abs(p.X - pit.X) - pit.Sx / 2f, Mathf.Abs(p.Y - pit.Z) - pit.Sz / 2f);
    }

    /// <summary>
    /// Verticale du jeu libre, en hauteur absolue des pieds : sol, terrasse (bloc plein), fosse (pas de sol : on tombe), vol de tremplin.
    /// L'altitude (Elevation) est celle de la terrasse quand on est au-dessus, 0 sinon ; elle voyage dans la pose et l'hôte la vérifie.
    /// </summary>
    void StepVerticalFree(PcInput input, float dt, bool crouched)
    {
        var map = Zones.Map;
        float feet = Elevation + jumpY;
        var terrace = map.TerraceAt(worldPos.X, worldPos.Y);
        float ground = terrace != null && feet >= terrace.Sy - 0.25f ? terrace.Sy : map.OverPit(worldPos.X, worldPos.Y, -0.05f) ? float.NegativeInfinity : 0f;
        bool grounded = jumpVel <= 0f && ground > float.NegativeInfinity && feet <= ground + 0.03f;
        if (input.Jump) jumpPressedAt = time;
        // Tampon de 0,12 s (appui juste avant de toucher le sol) et « coyote time » de 0,1 s (appui juste après avoir quitté une terrasse).
        bool wantJump = time - jumpPressedAt <= 0.12 && !crouched && !Hurtbox.Eliminated && time >= evictUntil;
        if (grounded)
        {
            groundedAt = time;
            if (wasAirborne && !autoplay)
            {
                // Réception : la vue plonge un peu, plus fort après une grande chute ; son de pas lourd.
                float impact = Mathf.Clamp(-airVel / 9f, 0f, 1f);
                if (impact > 0.15f) { CameraFx.Kick(new Vector2(0, -2.5f * impact)); Sfx.Play(this, SfxKind.Land, null, 1f, -8f + 6f * impact); }
                if (impact > 0.35f) Juice.Dust(this, new Vector3(worldPos.X, ground, worldPos.Y), 8 + (int)(10 * impact), 1.2f + impact);
            }
            wasAirborne = false;
            feet = ground; jumpVel = 0f; padFlight = false;
            if (wantJump) { jumpVel = FreeJumpSpeed; jumpPressedAt = -10; Jumps++; if (!autoplay) Sfx.Play(this, SfxKind.Jump, null, 1f, -6f); }
            else if (!Hurtbox.Eliminated) TryPad();
        }
        else if (wantJump && !padFlight && jumpVel <= 0f && jumpVel > -3f && time - groundedAt <= 0.1 && ground > float.NegativeInfinity)
        {
            jumpVel = FreeJumpSpeed; jumpPressedAt = -10;
            if (!autoplay) Sfx.Play(this, SfxKind.Jump, null, 1f, -6f);
        }
        if (!grounded || jumpVel > 0f)
        {
            float prev = feet;
            jumpVel -= (padFlight ? PadGravity : Gravity) * dt;
            feet += jumpVel * dt;
            if (jumpVel < 0f && ground > float.NegativeInfinity && prev >= ground && feet <= ground) { feet = ground; jumpVel = 0f; padFlight = false; }
            if (feet < -6f) { feet = -6f; jumpVel = 0f; }   // fond de la fosse : l'hôte a déjà jugé la chute depuis longtemps
            if (jumpVel != 0f) { wasAirborne = true; airVel = jumpVel; }
        }
        float elevation = terrace != null && feet >= terrace.Sy - 0.05f ? terrace.Sy : 0f;
        if (elevation != Elevation)
        {
            if (elevation > 0f) { terraceSince = time; GD.Print($"[PC] Terrasse atteinte ({terrace!.Id})"); }
            Elevation = elevation;
        }
        jumpY = feet - Elevation;
    }

    /// <summary>Tremplin de son camp sous les pieds : vol guidé de 1,8 s (sommet à 3 m) qui se pose au centre de la terrasse de la plaque. Recharge 10 s.</summary>
    void TryPad()
    {
        if (time < padReadyAt || Zones.Map.Pc == null) return;
        foreach (var pad in Zones.Map.Pc.JumpPads)
        {
            var padZone = Zones.Map.Zones.Find(z => z.Id == pad.Zone);
            var terrace = Zones.Map.Pc.Terraces.Find(t => t.Zone == pad.Zone);
            if (padZone == null || padZone.Team != Hurtbox.Team || terrace == null) continue;
            if (new Vector2(worldPos.X - pad.X, worldPos.Y - pad.Z).Length() > pad.Radius + 0.1f) continue;
            float vy = Mathf.Sqrt(2f * PadGravity * pad.LaunchToY);
            float flight = (vy + Mathf.Sqrt(Mathf.Max(vy * vy - 2f * PadGravity * terrace.Sy, 0f))) / PadGravity;   // durée jusqu'à redescendre à hauteur de la terrasse
            padVel = (new Vector2(terrace.X, terrace.Z) - worldPos) / flight;
            jumpVel = vy; padFlight = true;
            padReadyAt = time + pad.CooldownSeconds;
            GD.Print($"[PC] Tremplin {pad.Id}");
            return;
        }
    }

    /// <summary>Un dash qui touche un adversaire le tacle : il lâche sa balle et est poussé de 2 m dans la direction du dash. L'hôte juge (distance, invulnérabilité).</summary>
    void TryTackle()
    {
        if (tackledDash == dashCount || Hurtbox.Eliminated || Zones.Map.PitRect == null) return;   // le tacle n'existe que sur les cartes à fosse
        foreach (var node in GetTree().GetNodesInGroup("hurtboxes"))
        {
            if (node is not Hurtbox h || h.Team == Hurtbox.Team || h.Eliminated || h.DashInvulnerable) continue;
            var hp = h.GlobalPosition;
            if (new Vector2(hp.X - worldPos.X, hp.Z - worldPos.Y).Length() > TackleRadius || Mathf.Abs(hp.Y - (Elevation + jumpY + 0.85f)) > 1.2f) continue;
            tackledDash = dashCount;
            BallActions.Tackle(Hurtbox.PlayerId, h.PlayerId, dodgeDir);
            GD.Print($"[PC] Tacle sur le joueur {h.PlayerId}");
            ShowToast("Tacle !");
            dodgeLeft = 0;
            velocity = dodgeDir * RunSpeed * 0.4f;   // le tacleur ralentit net
            return;
        }
    }

    /// <summary>Poussée subie (tacle validé par l'hôte) : 2 m en 0,25 s ; sur une terrasse, on est évincé vers le bas.</summary>
    public void ApplyPush(Vector2 dir)
    {
        if (!FreeRoam || !dir.IsFinite() || dir.LengthSquared() < 0.01f) return;
        pushDir = dir.Normalized();
        pushUntil = time + PushTime;
        dodgeLeft = 0; charging = false; chargeTime = 0; padFlight = false;
        if (Elevation > 0f) { evictDir = pushDir; evictUntil = time + 1.0; }
        GD.Print("[PC] Poussé par un tacle");
        ShowToast("Poussé !");
    }

    /// <summary>Quand les pieds sont sous le bord de la fosse, on tombe droit : le mur de la fosse nous garde à l'intérieur.</summary>
    void KeepInsidePit()
    {
        var pit = Zones.Map.PitRect;
        if (pit == null || Elevation + jumpY > -0.15f || !Zones.Map.OverPit(worldPos.X, worldPos.Y, 0.3f)) return;
        worldPos = new Vector2(Mathf.Clamp(worldPos.X, pit.X - pit.Sx / 2f + 0.3f, pit.X + pit.Sx / 2f - 0.3f), Mathf.Clamp(worldPos.Y, pit.Z - pit.Sz / 2f + 0.3f, pit.Z + pit.Sz / 2f - 0.3f));
        velocity = Vector2.Zero;
    }

    double lastRotorLog = -10;

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
        if (FreeRoam && Zones.Map.Pc != null && Elevation + jumpY < 99f)
            foreach (var t in Zones.Map.Pc.Terraces)
                if (Elevation + jumpY < t.Sy - 0.25f) Collide(new Vector2(t.X, t.Z), new Vector2(t.Sx, t.Sz) * 0.5f, 0f);   // la terrasse est un bloc : on y monte par le tremplin
        if (FreeRoam && Zones.Map.PitRect == null && Maps.Rotor.Current is { } rotor && Elevation + jumpY < rotor.YMax && Elevation + jumpY + 1.6f > rotor.YMin)
        {
            var hubPos = new Vector2(rotor.GlobalPosition.X, rotor.GlobalPosition.Z);
            var before = p;
            Collide(hubPos, new Vector2(rotor.ArmLength, rotor.Thickness * 0.5f), rotor.Angle);   // la barre entière (deux bras), repère tourné
            if (p != before && time - lastRotorLog > 1.0) { lastRotorLog = time; GD.Print("[PC] Poussé par le rotor"); }
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
                bool fastShot = hostileFast && ball.ThrowMode != Ball.ModeLob && ball.LinearVelocity.Length() >= PerfectMinSpeed;
                bool perfect = FreeRoam && !ally && viaCatch && fastShot && time - catchOpenedAt <= PerfectCatchWindow;
                bool slow = ball.LinearVelocity.Length() < PickupSlowSpeed && time > pickupBlockedUntil;
                float scoopDist = ball.GlobalPosition.DistanceTo(scoop);
                if (slow && scoopDist <= PickupRadius + 0.6f) scoopUntil = time + 0.2;
                if (!viaCatch && !(slow && scoopDist <= PickupRadius)) continue;
                if (!BallActions.IsClient) held = ball;
                BallActions.Catch(ball, holdPoint, 1, Hurtbox.PlayerId, Hurtbox.Team, perfect);
                if (perfect && !BallActions.IsClient) { ShowToast("Attrape parfaite !"); GD.Print("[PC] Attrape parfaite"); }
                if (viaCatch && !ally && !autoplay)
                {
                    // Attrape d'un tir : la vue encaisse le choc ; l'attrape parfaite fige un instant le monde.
                    CameraFx.Kick(new Vector2(0, -1.4f));
                    CameraFx.Shake(perfect ? 0.3f : 0.14f);
                    CameraFx.FovPunch(perfect ? -6f : -3f);
                    if (perfect) Juice.HitStop(this, 0.1f, 0.1f);
                }
                else if (perfect) perfectClaimAt = time;   // client : on annonce quand l'hôte a confirmé (balle tenue avec un palier)
                if (ally) { GD.Print("[PC] Passe reçue"); pickupBlockedUntil = time + 0.3; }
                if (viaCatch && !ally && hostileFast) { Catches++; if (perfect) PerfectCatches++; }
                else if (!viaCatch) Pickups++;
                if (viaCatch) { catchWindowLeft = 0; catchCooldownLeft = 0; }   // attrape réussie : pas de recharge
                if (BallActions.IsClient) pickupBlockedUntil = time + 0.3;
                break;
            }
        }

        AimingAtEnemy = BestEnemy(3.5f) != null;
        // Cible verrouillée (affichée par UI.LockOnMarker) : l'adversaire que l'aide à la visée corrigera si on lance maintenant.
        LockTarget = held != null && GameSettings.AimAssist > 0.01f ? BestEnemy(AssistCone * Mathf.Lerp(0.6f, 1f, GameSettings.AimAssist)) : null;
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
                Feints++;
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
                var to = enemy != null ? enemy.GlobalPosition + Vector3.Up * 0.3f : camera.GlobalPosition + AimFwd * AimDistance();
                var flat = new Vector3(to.X - holdPoint.GlobalPosition.X, 0, to.Z - holdPoint.GlobalPosition.Z);
                if (flat.Length() > 30f) { var cut = holdPoint.GlobalPosition + flat.Normalized() * 30f; to = new Vector3(cut.X, to.Y, cut.Z); }   // au-delà, la vitesse maximale ne suffit plus
                Launch(ArcVelocity(holdPoint.GlobalPosition, to, 3.0f), 0f, Ball.ModeLob);
            }
            else if (fire)
            {
                if (!charging) chargeTicks = 0;
                charging = true; chargeTime = Mathf.Min(chargeTime + delta, MaxChargeTime);
                int tick = Mathf.FloorToInt((float)(chargeTime / MaxChargeTime) * 3f + 0.001f);
                if (tick > chargeTicks)
                {
                    chargeTicks = tick;
                    if (!autoplay) Sfx.Play(this, SfxKind.ChargeTick, null, 0.9f + 0.25f * tick, tick >= 3 ? 0f : -6f);
                }
            }
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
        // Lancer chargé à fond : la balle tremble dans la main, prête à partir.
        if (charging && chargeTime >= MaxChargeTime * 0.99)
            holdPoint.Position += new Vector3((float)GD.RandRange(-1.0, 1.0), (float)GD.RandRange(-1.0, 1.0), 0) * 0.012f;
    }

    void Launch(Vector3 velocity, float spin, int mode)
    {
        if (mode == Ball.ModePass) Passes++;
        else if (mode == Ball.ModeLob) Lobs++;
        else { Throws++; if (charging && chargeTime >= MaxChargeTime * 0.95) ChargedThrows++; }
        BallActions.Throw(held!, 1, velocity, spin, mode);
        if (spin != 0) apCurvePending = false; else if (mode == 0) apCurvePending = true;
        GD.Print($"[PC] Lancer {(mode == Ball.ModePass ? "passe" : mode == Ball.ModeLob ? "cloche" : spin != 0 ? "effet" : "tendu")} à {velocity.Length():0.0} m/s");
        if (BallActions.IsClient) held!.Visible = false;   // pas de balle fantôme dans la main en attendant la confirmation de l'hôte
        ignoreHeldUntil = Time.GetTicksMsec() / 1000.0 + 0.4;
        held = null;
        charging = false;
        chargeTime = 0;
        pickupBlockedUntil = time + 0.5;      // ne pas ramasser sa propre balle qui part
        catchWindowLeft = 0;                  // une fenêtre encore ouverte ne doit pas reprendre la balle lancée
        // Le lancer se sent : recul de la vue vers le haut et coup de champ, proportionnels à la vitesse ; secousse au lancer plein.
        float power = Mathf.Clamp((velocity.Length() - MinThrow) / (MaxThrow - MinThrow), 0f, 1f);
        if (!autoplay)
        {
            CameraFx.Kick(new Vector2(0, 0.6f + 1.6f * power));
            CameraFx.FovPunch(3f + 5f * power);
            if (power > 0.95f) CameraFx.Shake(0.15f);
        }
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
        var fwd = AimFwd;
        Hurtbox? best = null, nearest = null;
        float bestAngle = 35f, nearestDist = 33f;   // au-delà de 33 m la passe (24 m/s au plus) retomberait courte
        foreach (var node in GetTree().GetNodesInGroup("hurtboxes"))
        {
            if (node is not Hurtbox h || h == Hurtbox || h.Team != Hurtbox.Team || h.Eliminated) continue;
            var to = h.GlobalPosition - from;
            float angle = Mathf.RadToDeg(fwd.AngleTo(to));
            float d = h.GlobalPosition.DistanceTo(head.GlobalPosition);
            if (angle < bestAngle && d <= nearestDist) { bestAngle = angle; best = h; }
            if (d < nearestDist) { nearestDist = d; nearest = h; }
        }
        return best ?? nearest;
    }

    /// <summary>Direction de visée sans les secousses de caméra (une secousse ne doit jamais dévier un tir).</summary>
    Vector3 AimFwd => FreeRoam ? -(new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, pitch)).Z : -camera.GlobalTransform.Basis.Z;

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
        var fwd = AimFwd;
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
        var to = from + AimFwd * 30f;
        var space = GetWorld3D().DirectSpaceState;
        var exclude = new Godot.Collections.Array<Rid>();
        for (int i = 0; i < 4; i++)
        {
            var query = PhysicsRayQueryParameters3D.Create(from, to, 0xFFFFFFFF, exclude);
            var hit = space.IntersectRay(query);
            if (hit.Count == 0) return 30f;
            if (hit["collider"].AsGodotObject() is StaticBody3D and not Maps.Rotor) return Mathf.Clamp(from.DistanceTo(hit["position"].AsVector3()), 3f, 30f);
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
        var fwd = AimFwd.Normalized();
        var enemy = GameSettings.AimAssist > 0.01f ? BestEnemy(AssistCone * Mathf.Lerp(0.6f, 1f, GameSettings.AimAssist)) : null;
        float dist = AimDistance();
        if (enemy != null)
        {
            var to = enemy.GlobalPosition - camera.GlobalPosition;
            dist = to.Length();
            fwd = fwd.Lerp(to / dist, AssistStrength * GameSettings.AimAssist).Normalized();   // petit angle : mélange linéaire (Slerp lève une exception si l'axe n'est pas parfaitement normalisé)
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
        float target = charging ? GameSettings.Fov - 6f : GameSettings.Fov;   // on resserre en armant : la cible se rapproche
        fov = Mathf.Lerp(fov, target, 1f - Mathf.Exp(-10f * dt));
    }

    /// <summary>3e personne : la caméra flotte derrière l'épaule droite, orientée comme la tête, et reste dans l'arène.</summary>
    void PlaceCamera(float dt, Vector3 fxRot, Vector3 fxOffset, float realDt)
    {
        var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, pitch);
        // Pas chassé : la caméra s'incline légèrement du côté où l'on court (lisible sans donner le mal de mer).
        var right2 = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
        float side = autoplay ? 0f : velocity.Dot(right2);
        lean = Mathf.Lerp(lean, -side * 0.28f, 1f - Mathf.Exp(-8f * realDt));
        var from = head.GlobalPosition;
        var ideal = from + basis * new Vector3(0.85f, 0.35f, CameraDistance);   // par-dessus l'épaule droite : le viseur n'est jamais caché par le personnage
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
            if (hit["collider"].AsGodotObject() is StaticBody3D and not Maps.Rotor && !arenaWall) { allowed = Mathf.Min(full, Mathf.Max(0.2f, from.DistanceTo(hp) - 0.25f)); break; }
            exclude.Add(hit["rid"].AsRid());
        }
        float f = allowed / full;
        camFraction = f < camFraction ? f : Mathf.Lerp(camFraction, f, 1f - Mathf.Exp(-9f * dt));
        var pos = from.Lerp(ideal, camFraction);
        var b = Zones.Map.Bounds;
        pos.X = Mathf.Clamp(pos.X, b.MinX + 0.2f, b.MaxX - 0.2f);
        pos.Z = Mathf.Clamp(pos.Z, b.MinZ + 0.2f, b.MaxZ - 0.2f);
        var shaken = basis * Basis.FromEuler(new Vector3(Mathf.DegToRad(fxRot.X), Mathf.DegToRad(fxRot.Y), Mathf.DegToRad(fxRot.Z + lean)));
        camera.GlobalTransform = new Transform3D(shaken, pos + basis * fxOffset);
    }

    /// <summary>Échange de plaques : le joueur se retrouve au même endroit relatif de la plaque d'arrivée (jeu libre) ou y est simplement posé (zones).</summary>
    public void MoveToPlate(ZoneDef to)
    {
        var from = zone;
        zone = to;
        Zones.LocalZoneId = to.Id;
        if (FreeRoam)
        {
            var p = PlateSwap.Remap(new Vector3(worldPos.X, 0, worldPos.Y), from, to);
            worldPos = new Vector2(p.X, p.Z);
            velocity = Vector2.Zero;
            var spawn = Zones.Map.Pc?.Spawns.Find(sp => sp.Zone == to.Id);   // retour de KO ou de chute : sur la place de la plaque occupée
            if (spawn != null) spawnPos = new Vector2(spawn.X, spawn.Z);
        }
        yaw += PlateSwap.YawDelta(from, to);
        GD.Print($"[PC] Échange de plaques : plaque {from.Id} -> {to.Id}, en {worldPos.X:0.0} / {worldPos.Y:0.0}");
    }

    void ApplyTransform()
    {
        var zoneBasis = new Basis(Vector3.Up, Mathf.DegToRad(zone.FacingDegrees));
        if (FreeRoam)
        {
            if (wasEliminated && !Hurtbox.Eliminated)   // retour de KO : au point d'apparition, au sol
            {
                worldPos = spawnPos; velocity = Vector2.Zero;
                jumpY = 0; jumpVel = 0; Elevation = 0; padFlight = false; pushUntil = 0; evictUntil = 0;
            }
            // Tombé et jamais jugé (pause entre deux manches…) : après 1,5 s au fond on remonte au point d'apparition.
            fallenFor = !Hurtbox.Eliminated && Elevation + jumpY < -2.5f ? fallenFor + 1.0 / 60.0 : 0;
            if (fallenFor > 1.5) { fallenFor = 0; worldPos = spawnPos; velocity = Vector2.Zero; jumpY = 0; jumpVel = 0; Elevation = 0; padFlight = false; GD.Print("[PC] Remonté au point d'apparition (chute non jugée)"); }
            if (Hurtbox.Eliminated && Elevation + jumpY < -2.5f)
            {
                // Tombé dans la fosse et déclaré KO : on remonte au point d'apparition tout de suite (invisible, immobile) pour que la copie de l'hôte suive avant le retour.
                worldPos = spawnPos; velocity = Vector2.Zero;
                jumpY = 0; jumpVel = 0; Elevation = 0; padFlight = false; pushUntil = 0; evictUntil = 0;
                GD.Print("[PC] Remonté au point d'apparition après la chute");
            }
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
                if (node is not Ball { IsHeld: false } b || b.LinearVelocity.Length() > PickupSlowSpeed || b.GlobalPosition.Y < -0.5f || Zones.Map.OverPit(b.GlobalPosition.X, b.GlobalPosition.Z, 0.8f)) continue;   // pas de balle dans ou au-dessus de la fosse
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
        // Le pilote automatique ne marche pas droit dans la fosse en allant chercher une balle ; seul le scénario de test le fait exprès.
        if (FreeRoam && Zones.Map.PitRect != null && input.Move.LengthSquared() > 0.01f)
        {
            var wish = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw)) * input.Move.X - new Vector2(-Mathf.Sin(yaw), -Mathf.Cos(yaw)) * input.Move.Y;
            if (PitGap(worldPos + wish.Normalized() * 1.6f) < 0.3f || PitGap(worldPos + velocity.Normalized() * 2.4f) < 0.2f && velocity.Length() > 4f) input.Move = Vector2.Zero;   // freinage : à 7,5 m/s l'inertie porte à près de 2 m
        }
        ScriptedMoves(ref input, (float)delta);
        return input;
    }

    // Scénario du mode test sur une carte à fosse : tremplin et terrasse, tacle dans la fosse (l'équipe 1 sert d'appât au bord, l'équipe 0 pousse),
    // puis dash qui s'arrête avant le vide et chute volontaire. La CI vérifie que chaque geste est validé par l'hôte.
    bool apDashedToVoid, apFell;

    bool SegmentHitsPit(Vector2 a, Vector2 b, float margin)
    {
        for (int i = 0; i <= 16; i++)
            if (PitGap(a.Lerp(b, i / 16f)) < margin) return true;
        return false;
    }

    /// <summary>Waypoint qui contourne la fosse quand la ligne droite la traverse (coins de la fosse élargis de 1,8 m).</summary>
    Vector2 AroundPit(Vector2 from, Vector2 to)
    {
        var pit = Zones.Map.PitRect;
        if (pit == null) return to;
        float margin = Mathf.Min(1.2f, Mathf.Max(PitGap(to), 0f) * 0.7f);   // cible au bord de la fosse : la ligne droite est bonne tant qu'elle ne la touche pas
        if (!SegmentHitsPit(from, to, margin)) return to;
        Vector2? best = null; float bestLen = float.MaxValue;
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
            {
                var corner = new Vector2(pit.X + sx * (pit.Sx / 2f + 1.8f), pit.Z + sz * (pit.Sz / 2f + 1.8f));
                if (SegmentHitsPit(from, corner, 1.0f)) continue;
                float len = from.DistanceTo(corner) + corner.DistanceTo(to);
                if (len < bestLen) { bestLen = len; best = corner; }
            }
        return best ?? to;
    }

    bool CoverBlocks(Vector2 a, Vector2 b, out ObstacleDef? hit)
    {
        hit = null;
        float len = a.DistanceTo(b);
        for (float d = 0.7f; d <= len; d += 0.25f)
        {
            var q = a.Lerp(b, len < 0.01f ? 0f : d / len);
            foreach (var o in Zones.Map.Obstacles)
            {
                if (o.Kind == "pit" || o.Y > 1.0f) continue;
                if (Mathf.Abs(q.X - o.X) < o.Sx / 2f + 0.55f && Mathf.Abs(q.Y - o.Z) < o.Sz / 2f + 0.55f) { hit = o; return true; }
            }
        }
        return false;
    }

    /// <summary>Le scénario n'a pas de pathfinding : contourne par le coin le plus court le premier couvert qui barre la route.</summary>
    Vector2 AroundCover(Vector2 from, Vector2 to)
    {
        if (!CoverBlocks(from, to, out var o) || o == null) return to;
        Vector2? best = null; float bestLen = float.MaxValue;
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
            {
                var corner = new Vector2(o.X + sx * (o.Sx / 2f + 0.9f), o.Z + sz * (o.Sz / 2f + 0.9f));
                if (CoverBlocks(from, corner, out _) || SegmentHitsPit(from, corner, 0.5f)) continue;
                float len = from.DistanceTo(corner) + corner.DistanceTo(to);
                if (len < bestLen) { bestLen = len; best = corner; }
            }
        return best ?? to;
    }

    bool SteerTo(Vector2 target, ref PcInput input, float stop, float dt, bool avoidPit = true)
    {
        float finalDist = (target - worldPos).Length();
        if (avoidPit) target = AroundPit(worldPos, target);
        target = AroundCover(worldPos, target);
        var to = target - worldPos;
        float want = Mathf.Atan2(-to.X, -to.Y);
        yaw += Mathf.Clamp(Mathf.AngleDifference(yaw, want), -8f * dt, 8f * dt);
        pitch = Mathf.MoveToward(pitch, 0f, 4f * dt);
        input.Move = finalDist > stop ? new Vector2(0, -1) : Vector2.Zero;
        return finalDist <= stop;
    }

    bool SpotBlocked(Vector2 q)
    {
        foreach (var o in Zones.Map.Obstacles)
        {
            if (o.Kind == "pit" || o.Y > 1.0f) continue;
            if (Mathf.Abs(q.X - o.X) < o.Sx / 2f + 0.9f && Mathf.Abs(q.Y - o.Z) < o.Sz / 2f + 0.9f) return true;
        }
        return false;
    }

    /// <summary>Place de l'appât à 1,5 m du bord, décalée le long du bord tant que sa place ou celle du pousseur (2 m derrière) tombe dans un couvert.</summary>
    Vector2 BaitSpot(Vector2 center, Vector2 edge, Vector2 dirOut)
    {
        var tangent = new Vector2(-dirOut.Y, dirOut.X);
        for (int i = 0; i < 16; i++)
        {
            var p = edge + dirOut * 1.5f + tangent * (i % 2 == 0 ? 1 : -1) * 1.0f * ((i + 1) / 2);
            var back = p + (p - center).Normalized() * 2.0f;
            if (!SpotBlocked(p) && !SpotBlocked(back) && PitGap(p) is > 0.5f and < 1.8f) return p;   // assez près du bord pour qu'un tacle suffise à tomber
        }
        GD.Print("[PC] Test : aucune place d'appât libre, repli sur le bord");
        return edge + dirOut * 1.5f;
    }

    /// <summary>Décale un point de test le long de `tangent` tant qu'il tombe dans un couvert ou un pilier.</summary>
    Vector2 FreeSpot(Vector2 p, Vector2 tangent)
    {
        for (int i = 0; i < 8; i++)
        {
            var q = p + tangent * (i % 2 == 0 ? 1 : -1) * 1.4f * ((i + 1) / 2);
            bool blocked = false;
            foreach (var o in Zones.Map.Obstacles)
            {
                if (o.Kind == "pit" || o.Y > 1.0f) continue;
                if (Mathf.Abs(q.X - o.X) < o.Sx / 2f + 0.9f && Mathf.Abs(q.Y - o.Z) < o.Sz / 2f + 0.9f) { blocked = true; break; }
            }
            if (!blocked) return q;
        }
        return p;
    }

    void ScriptedMoves(ref PcInput input, float dt)
    {
        var map = Zones.Map;
        if (Hurtbox.Eliminated && apDashedToVoid) apFell = true;   // le scénario s'arrête après la chute volontaire
        if (FreeRoam && map.PitRect == null && map.Hub?.Rotor != null && !Hurtbox.Eliminated && time >= 8 && time < 20)
        {
            // Test du rotor : on se poste à 2,6 m du centre, sur la trajectoire d'un bras (chaque joueur sur un axe différent), sans tirer.
            float a = Hurtbox.PlayerId * Mathf.Pi / 2f;
            var post = new Vector2(map.Hub.X + Mathf.Cos(a) * 2.6f, map.Hub.Z + Mathf.Sin(a) * 2.6f);
            input.Fire = false; input.Dodge = false; input.Jump = false;
            if (SteerTo(post, ref input, 0.3f, dt, false))
            {
                var look = new Vector2(map.Hub.X, map.Hub.Z) - worldPos;
                yaw = Mathf.Atan2(-look.X, -look.Y);
            }
            return;
        }
        if (!FreeRoam || map.PitRect is not { } pit || Hurtbox.Eliminated || apFell || time < 8 || time >= 62) return;
        var center = new Vector2(pit.X, pit.Z);
        if (time < 22)
        {
            // Phase 0 : tremplin de son camp, puis on tient la terrasse en tirant de là-haut.
            if (padFlight) { input.Move = Vector2.Zero; input.Jump = false; return; }
            if (Elevation > 0f) { input.Move = Vector2.Zero; input.Jump = false; input.Dodge = false; return; }
            var pad = map.Pc?.JumpPads.Find(p => map.Zones.Find(z => z.Id == p.Zone)?.Team == Hurtbox.Team && p.Zone == Hurtbox.PlayerId);
            if (pad == null || padReadyAt > 0) return;
            input.Fire = false; input.Jump = false; input.Dodge = false;
            SteerTo(new Vector2(pad.X, pad.Z), ref input, 0.1f, dt);
            return;
        }
        if (time < 47)
        {
            input.Jump = false; input.Pass = false; input.Lob = false; input.Feint = false;
            if (Hurtbox.Team == 0)
            {
                // Appât : debout à 1,5 m du bord, du côté de son point d'apparition, sans bouger ni tirer.
                var dirOut = (spawnPos - center).Normalized();
                var edge = center + dirOut / Mathf.Max(Mathf.Abs(dirOut.X) / (pit.Sx / 2f), Mathf.Abs(dirOut.Y) / (pit.Sz / 2f));   // point du bord sur la demi-droite centre -> apparition
                input.Fire = false; input.Dodge = false;
                if (SteerTo(BaitSpot(center, edge, dirOut), ref input, 0.4f, dt))
                {
                    var look = center - worldPos;
                    yaw = Mathf.Atan2(-look.X, -look.Y);
                }
                return;
            }
            // Pousseur : derrière l'adversaire le plus proche du bord (côté opposé à la fosse), puis dash vers la fosse.
            Hurtbox? prey = null; float bestGap = 3.2f;
            foreach (var node in GetTree().GetNodesInGroup("hurtboxes"))
            {
                if (node is not Hurtbox h || h.Team == Hurtbox.Team || h.Eliminated || h.PlayerId != 0) continue;   // le tacle ne vise qu'un humain : l'appât est le joueur 0 (les autres zones peuvent être des bots)
                float gap = PitGap(new Vector2(h.GlobalPosition.X, h.GlobalPosition.Z));
                if (gap > 0.2f && gap < bestGap) { bestGap = gap; prey = h; }
            }
            if (prey == null) return;   // personne au bord : jeu normal
            var pp = new Vector2(prey.GlobalPosition.X, prey.GlobalPosition.Z);
            var away = (pp - center).Normalized();
            var behind = pp + away * 2.0f;
            input.Fire = false;
            if (SteerTo(behind, ref input, 0.45f, dt))
            {
                var look = center - worldPos;
                yaw = Mathf.Atan2(-look.X, -look.Y);
                input.Dodge = true;
            }
            return;
        }
        // Phase 2 : à 3,4 m du bord, dash vers la fosse (il doit s'arrêter), puis on court dedans.
        {
            input.Fire = false; input.Jump = false; input.Pass = false; input.Lob = false; input.Feint = false; input.Dodge = false;
            float gap = PitGap(worldPos);
            if (!apDashedToVoid)
            {
                var dirOut = (spawnPos - center).Normalized();
                var rim = center + dirOut / Mathf.Max(Mathf.Abs(dirOut.X) / (pit.Sx / 2f), Mathf.Abs(dirOut.Y) / (pit.Sz / 2f));
                var start = FreeSpot(rim + dirOut * 3.4f, new Vector2(-dirOut.Y, dirOut.X));
                if (SteerTo(start, ref input, 0.3f, dt))
                {
                    var look = center - worldPos;
                    yaw = Mathf.Atan2(-look.X, -look.Y);
                    if (dashCharges > 0) { input.Dodge = true; apDashedToVoid = true; GD.Print($"[PC] Test : dash vers la fosse depuis {worldPos.X:0.0} / {worldPos.Y:0.0} (écart {PitGap(worldPos):0.0} m)"); }
                }
                return;
            }
            if (dodgeLeft <= 0 && Elevation + jumpY > -0.15f)
            {
                var look = center - worldPos;
                yaw = Mathf.Atan2(-look.X, -look.Y);
                input.Move = new Vector2(0, -1);   // on court dans la fosse
            }
        }
    }
}
