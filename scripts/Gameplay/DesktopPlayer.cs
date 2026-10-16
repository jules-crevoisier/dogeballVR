using Godot;
using DogeballVR.Net;
using DogeballVR.Maps;
using DogeballVR.Zones;

namespace DogeballVR.Gameplay;

/// <summary>
/// Joueur PC (clavier-souris), pensé comme un jeu PC à part entière, inspiré des commandes de Knockout City :
/// ZQSD/WASD bouger, Maj sprinter, Espace sauter, Ctrl/C s'accroupir, E esquive, souris viser,
/// clic gauche maintenu puis relâché : lancer (tap = lancer rapide, maintenu = lancer chargé),
/// clic droit : fenêtre d'attrape (0,5 s) suivie d'une courte recharge. Les balles lentes qui passent
/// près des pieds sont ramassées automatiquement. PageHaut/PageBas : sensibilité. Échap : libérer la souris.
/// Le joueur ne peut pas sortir de sa zone. Option de test : <c>--pc-autoplay</c> fait jouer le joueur seul.
/// </summary>
public partial class DesktopPlayer : Node3D
{
    // Déplacement
    const float WalkSpeed = 5.5f;
    const float SprintSpeed = 7.5f;
    const float ChargeSpeed = 3.8f;      // on ralentit en armant un lancer : choix tactique
    const float AccelUp = 45f, AccelDown = 60f;
    const float JumpSpeed = 3.5f, Gravity = 12f;   // ~0,5 m de saut ; sous le plafond vertical de l'hôte (4 m/s)
    const float DodgeSpeed = 10f, DodgeTime = 0.18f, DodgeCooldown = 1.2f;
    const float PlayerRadius = 0.3f;
    const float EyeHeight = 1.6f;
    const float CrouchHeight = 0.9f;
    const float EyeSpeed = 3.5f;         // m/s : l'hôte plafonne la vitesse verticale à 4 m/s

    // Lancer et attrape
    const float MinThrow = 14f;          // un tap part déjà vite : le lancer ne doit pas être mou
    const float MaxThrow = 30f;
    const float MaxChargeTime = 0.7f;
    const float AssistCone = 7f;         // degrés : l'aide à la visée ne corrige que les presque-touches
    const float AssistStrength = 0.5f;
    const double CatchWindow = 0.5, CatchRecovery = 0.55;
    const float CatchReach = 1.1f;       // de la main tendue ; l'hôte accepte 1,2 m + marge de latence
    const float HandExtend = 0.95f;      // main tendue devant la tête (l'hôte plafonne à 1,0 m)
    const float PickupSlowSpeed = 3.5f;
    const float PickupRadius = 0.9f;

    public ZoneManager Zones { get; set; } = null!;
    public Hurtbox Hurtbox { get; set; } = null!;

    /// <summary>Charge du lancer en cours (0 à 1), pour la jauge du HUD.</summary>
    public float ThrowCharge01 => charging ? (float)(chargeTime / MaxChargeTime) : 0f;
    /// <summary>Altitude du plateau (lift) : le joueur et sa caméra montent avec.</summary>
    public float Elevation { get; set; }
    public float HeadHeight => camera.Position.Y;
    public Ball? HeldBall => held != null && IsInstanceValid(held) ? held : null;

    /// <summary>Un adversaire est sous le viseur (le HUD passe le viseur en rouge).</summary>
    public bool AimingAtEnemy { get; private set; }
    /// <summary>Attrape : 0 prête, 1 fenêtre ouverte, 2 recharge.</summary>
    public int CatchState => catchWindowLeft > 0 ? 1 : catchCooldownLeft > 0 ? 2 : 0;
    public float CatchCooldown01 => CatchState == 2 ? (float)(1 - catchCooldownLeft / CatchRecovery) : 1f;
    public float DodgeCooldown01 => (float)Mathf.Clamp(1 - dodgeCooldownLeft / DodgeCooldown, 0, 1);
    public bool Sprinting { get; private set; }
    public float Sensitivity => sensitivity;
    /// <summary>Message bref à afficher (changement de sensibilité…).</summary>
    public string Toast { get; private set; } = "";
    public double ToastUntil { get; private set; }
    public double Time01 => time;

    Camera3D camera = null!;
    Node3D holdPoint = null!;
    ZoneDef zone = null!;
    double ignoreHeldUntil;          // client : laisse le temps à l'hôte de confirmer un lancer

    /// <summary>Pose envoyée aux autres joueurs : tête, cap, main gauche (repos) et main droite (tendue, ramassage ou tenue).</summary>
    public PoseData Pose => new(camera.GlobalPosition, camera.GlobalRotation.Y,
        camera.GlobalPosition + camera.GlobalTransform.Basis * new Vector3(-0.3f, -0.25f, -0.6f), RightHand(), Elevation);

    Ball? held;
    Vector2 localPos;                // position dans le repère de la zone (centre = 0)
    Vector2 velocity;                // m/s, dans le repère monde (x, z)
    float yaw, pitch;
    float eyeY = EyeHeight;
    float jumpY, jumpVel;
    float sensitivity = 0.0022f;
    double chargeTime;
    bool charging;
    double catchWindowLeft, catchCooldownLeft;
    double dodgeLeft, dodgeCooldownLeft;
    Vector2 dodgeDir;
    double scoopUntil;               // la main droite se baisse vers une balle à ramasser (pour que l'hôte valide)
    double pickupBlockedUntil;
    bool prevJump, prevDodge, prevCatch;
    float fov = 90f;
    double time;

    // Mode test : le joueur se pilote seul avec la même chaîne d'actions que le clavier-souris.
    bool autoplay;
    readonly RandomNumberGenerator rng = new();
    double apFireStart = -1, apNextJump = 2, apNextDodge = 4, apCrouchUntil;

    struct PcInput { public Vector2 Move; public bool Sprint, Crouch, Jump, Dodge, Fire, Catch; }

    public override void _Ready()
    {
        camera = new Camera3D { Name = "Camera", Current = true, Position = new Vector3(0, EyeHeight, 0), Fov = fov, Near = 0.05f };
        AddChild(camera);

        holdPoint = new Node3D { Name = "HoldPoint", Position = new Vector3(0.28f, -0.28f, -0.85f) };
        camera.AddChild(holdPoint);

        Hurtbox.Head = camera;
        AddChild(Hurtbox);

        zone = Zones.Map.Zones.Find(z => z.Id == Zones.LocalZoneId)!;
        yaw = Mathf.DegToRad(zone.FacingDegrees);
        autoplay = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--pc-autoplay") >= 0;
        rng.Seed = 4242;
        LoadSettings();
        if (!autoplay) Input.MouseMode = Input.MouseModeEnum.Captured;
        ApplyTransform();
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
            GetViewport().SetInputAsHandled();
        }
    }

    void ChangeSensitivity(float factor)
    {
        sensitivity = Mathf.Clamp(sensitivity * factor, 0.0005f, 0.01f);
        SaveSettings();
        ShowToast($"Sensibilité : {sensitivity / 0.0022f:0.00}x");
    }

    public override void _PhysicsProcess(double delta)
    {
        time += delta;
        float dt = (float)delta;
        var input = autoplay ? AutoplayInput(delta) : ReadInput();
        if (!autoplay && Input.MouseMode != Input.MouseModeEnum.Captured) input = default;   // souris libérée : plus de réaction aux clics

        UpdateMotion(input, dt);
        if (BallActions.IsClient)
            held = Time.GetTicksMsec() / 1000.0 < ignoreHeldUntil ? null : BallActions.FindHeld(GetTree(), Hurtbox.PlayerId);
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
        bool jump = Input.IsKeyPressed(Key.Space), dodge = Input.IsKeyPressed(Key.E), catchDown = Input.IsMouseButtonPressed(MouseButton.Right);
        var input = new PcInput
        {
            Move = m.LimitLength(1f),
            Sprint = Input.IsKeyPressed(Key.Shift),
            Crouch = Input.IsKeyPressed(Key.Ctrl) || Input.IsKeyPressed(Key.C),
            Jump = jump && !prevJump,
            Dodge = dodge && !prevDodge,
            Catch = catchDown && !prevCatch,
            Fire = Input.IsMouseButtonPressed(MouseButton.Left),
        };
        prevJump = jump; prevDodge = dodge; prevCatch = catchDown;
        return input;
    }

    void UpdateMotion(PcInput input, float dt)
    {
        dodgeCooldownLeft = Mathf.Max(0, dodgeCooldownLeft - dt);
        var forward = new Vector2(-Mathf.Sin(yaw), -Mathf.Cos(yaw));
        var right = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
        var wish = right * input.Move.X - forward * input.Move.Y;

        // Esquive : une impulsion courte dans la direction voulue (ou vers l'avant), avec recharge.
        if (input.Dodge && dodgeCooldownLeft <= 0 && dodgeLeft <= 0 && !Hurtbox.Eliminated)
        {
            dodgeDir = wish.LengthSquared() > 0.01f ? wish.Normalized() : forward;
            dodgeLeft = DodgeTime;
            dodgeCooldownLeft = DodgeCooldown;
        }

        bool crouched = eyeY < (EyeHeight + CrouchHeight) * 0.5f + 0.1f;
        Sprinting = input.Sprint && !charging && !crouched && input.Move.Y < -0.1f;
        float top = charging ? ChargeSpeed : Sprinting ? SprintSpeed : WalkSpeed;
        if (crouched) top *= 0.6f;

        Vector2 target = wish * top;
        if (dodgeLeft > 0)
        {
            dodgeLeft -= dt;
            velocity = dodgeDir * DodgeSpeed;
        }
        else
        {
            float accel = target.LengthSquared() > velocity.LengthSquared() ? AccelUp : AccelDown;
            velocity = velocity.MoveToward(target, accel * dt);
        }

        // Saut : la caméra monte de ~0,5 m ; pas de saut accroupi.
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

        eyeY = Mathf.MoveToward(eyeY, input.Crouch ? CrouchHeight : EyeHeight, EyeSpeed * dt);

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

    /// <summary>Les couverts et plots sont solides : on glisse le long (cercle de 0,3 m contre le rectangle de l'obstacle).</summary>
    void PushOutOfObstacles()
    {
        var zoneBasis = new Basis(Vector3.Up, Mathf.DegToRad(zone.FacingDegrees));
        var p3 = zone.Center + zoneBasis * new Vector3(localPos.X, 0, localPos.Y);
        var p = new Vector2(p3.X, p3.Z);
        bool moved = false;
        foreach (var o in Zones.Map.Obstacles)
        {
            if (o.Kind == "pit" || o.Y > Elevation + 1.5f || o.Y + o.Sy < Elevation + 0.3f) continue;
            float rot = Mathf.DegToRad(o.RotY);
            var rel = (p - new Vector2(o.X, o.Z)).Rotated(rot);      // repère de l'obstacle
            var half = new Vector2(o.Sx, o.Sz) * 0.5f;
            var closest = new Vector2(Mathf.Clamp(rel.X, -half.X, half.X), Mathf.Clamp(rel.Y, -half.Y, half.Y));
            var diff = rel - closest;
            float d = diff.Length();
            if (d >= PlayerRadius) continue;
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
        if (!moved) return;
        var local = zoneBasis.Inverse() * new Vector3(p.X - zone.Center.X, 0, p.Y - zone.Center.Z);
        localPos = new Vector2(local.X, local.Z);
        float hw = Zones.Map.ZoneWidth * 0.5f - 0.3f, hd = Zones.Map.ZoneDepth * 0.5f - 0.3f;
        localPos = new Vector2(Mathf.Clamp(localPos.X, -hw, hw), Mathf.Clamp(localPos.Y, -hd, hd));
    }

    Vector3 FlatForward()
    {
        var f = -camera.GlobalTransform.Basis.Z with { Y = 0 };
        return f.LengthSquared() < 1e-4f ? Vector3.Zero : f.Normalized();
    }

    /// <summary>Point de ramassage au niveau des pieds, devant soi (à moins de 1 m de la tête, limite de l'hôte).</summary>
    Vector3 ScoopPoint() => camera.GlobalPosition + new Vector3(0, -0.85f, 0) + FlatForward() * 0.5f;

    Vector3 RightHand()
    {
        if (catchWindowLeft > 0) return camera.GlobalPosition + (-camera.GlobalTransform.Basis.Z) * HandExtend;
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
        }

        // Prend une balle : attrape (main tendue) ou ramassage automatique des balles lentes près des pieds.
        if (held == null && (!BallActions.IsClient || Time.GetTicksMsec() / 1000.0 >= ignoreHeldUntil))
        {
            var hand = camera.GlobalPosition + (-camera.GlobalTransform.Basis.Z) * HandExtend;
            var scoop = ScoopPoint();
            foreach (var node in GetTree().GetNodesInGroup("balls"))
            {
                if (node is not Ball { IsHeld: false } ball) continue;
                bool viaCatch = catchWindowLeft > 0 && ball.GlobalPosition.DistanceTo(hand) <= CatchReach;
                bool slow = ball.LinearVelocity.Length() < PickupSlowSpeed && time > pickupBlockedUntil;
                float scoopDist = ball.GlobalPosition.DistanceTo(scoop);
                if (slow && scoopDist <= PickupRadius + 0.6f) scoopUntil = time + 0.2;
                if (!viaCatch && !(slow && scoopDist <= PickupRadius)) continue;
                if (!BallActions.IsClient) held = ball;
                BallActions.Catch(ball, holdPoint, 1, Hurtbox.PlayerId, Hurtbox.Team);
                if (viaCatch) { catchWindowLeft = 0; catchCooldownLeft = 0; }   // attrape réussie : pas de recharge
                if (BallActions.IsClient) pickupBlockedUntil = time + 0.3;
                break;
            }
        }

        AimingAtEnemy = BestEnemy(3.5f) != null;

        if (held != null)
        {
            if (input.Fire) { charging = true; chargeTime = Mathf.Min(chargeTime + delta, MaxChargeTime); }
            else if (charging)
            {
                float t = (float)(chargeTime / MaxChargeTime);
                float speed = Mathf.Lerp(MinThrow, MaxThrow, t);
                BallActions.Throw(held, 1, AimDirection() * speed, 0f);
                ignoreHeldUntil = Time.GetTicksMsec() / 1000.0 + 0.4;
                held = null;
                charging = false;
                chargeTime = 0;
                pickupBlockedUntil = time + 0.5;      // ne pas ramasser sa propre balle qui part
                fov = Mathf.Min(fov + 6f, 100f);      // petit coup de FOV : le lancer se sent
            }
        }
        else { charging = false; chargeTime = 0; }
        holdPoint.Position = HoldLocalPosition();
    }

    Vector3 HoldLocalPosition()
    {
        float crouch = Mathf.Clamp((EyeHeight - eyeY) / (EyeHeight - CrouchHeight), 0f, 1f);
        var rest = new Vector3(0.28f, Mathf.Lerp(-0.28f, -0.5f, crouch), Mathf.Lerp(-0.85f, -0.7f, crouch));
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

    /// <summary>Direction du lancer : le regard, corrigé à moitié vers un adversaire presque aligné.</summary>
    Vector3 AimDirection()
    {
        var fwd = -camera.GlobalTransform.Basis.Z;
        var enemy = BestEnemy(AssistCone);
        if (enemy == null) return fwd;
        var to = (enemy.GlobalPosition - camera.GlobalPosition).Normalized();
        return fwd.Normalized().Lerp(to, AssistStrength).Normalized();   // petit angle : un mélange linéaire suffit (Slerp lève une exception si l'axe n'est pas parfaitement normalisé)
    }

    void UpdateCamera(float dt)
    {
        float target = charging ? 82f : Sprinting ? 96f : 90f;
        fov = Mathf.Lerp(fov, target, 1f - Mathf.Exp(-10f * dt));
        camera.Fov = fov;
    }

    void ApplyTransform()
    {
        var zoneBasis = new Basis(Vector3.Up, Mathf.DegToRad(zone.FacingDegrees));
        GlobalPosition = zone.Center + zoneBasis * new Vector3(localPos.X, 0, localPos.Y) + new Vector3(0, Elevation, 0);
        camera.Position = new Vector3(0, eyeY + jumpY, 0);
        camera.Rotation = new Vector3(pitch, yaw, 0);
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
            float d = h.GlobalPosition.DistanceTo(camera.GlobalPosition);
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
                float d = b.GlobalPosition.DistanceTo(camera.GlobalPosition);
                if (d < ballDist) { ballDist = d; target = b; }
            }
            if (target != null) aimPoint = target.GlobalPosition;
        }

        if (aimPoint is Vector3 p)
        {
            var to = p - camera.GlobalPosition;
            float wantYaw = Mathf.Atan2(-to.X, -to.Z);
            float wantPitch = Mathf.Atan2(to.Y, new Vector2(to.X, to.Z).Length());
            float maxTurn = 5f * (float)delta;
            yaw += Mathf.Clamp(Mathf.AngleDifference(yaw, wantYaw), -maxTurn, maxTurn);
            pitch = Mathf.Clamp(Mathf.MoveToward(pitch, wantPitch, maxTurn), -1.4f, 1.4f);
            if (target != null)
            {
                input.Move = new Vector2(0, -1);
                input.Sprint = true;
                input.Crouch = target.GlobalPosition.Y < 0.5f && ballDist < 2.5f;
            }
            else input.Move = new Vector2(Mathf.Sin((float)time * 0.9f), 0);   // strafe d'un côté à l'autre
        }

        // Attrape les balles qui arrivent vite droit sur nous.
        if (held == null)
            foreach (var node in GetTree().GetNodesInGroup("balls"))
            {
                if (node is not Ball { IsHeld: false } b || b.LinearVelocity.Length() < 10f) continue;
                var rel = camera.GlobalPosition - b.GlobalPosition;
                if (rel.Length() < 6f && rel.Normalized().Dot(b.LinearVelocity.Normalized()) > 0.9f) { input.Catch = true; break; }
            }

        // Lancer : on arme un temps aléatoire une fois aligné, puis on relâche.
        if (held != null && enemy != null)
        {
            float err = Mathf.RadToDeg((-camera.GlobalTransform.Basis.Z).AngleTo(enemy.GlobalPosition - camera.GlobalPosition));
            if (apFireStart < 0 && err < 6f) apFireStart = time + rng.RandfRange(0.0f, 0.7f);
            input.Fire = apFireStart < 0 || time < apFireStart;
        }
        else apFireStart = -1;

        if (time > apNextJump) { input.Jump = true; apNextJump = time + rng.RandfRange(2.5f, 5f); }
        if (time > apNextDodge) { input.Dodge = true; apNextDodge = time + rng.RandfRange(5f, 8f); }
        if (time < apCrouchUntil) input.Crouch = true;
        else if (rng.Randf() < 0.002f) apCrouchUntil = time + 1.0;
        return input;
    }
}
