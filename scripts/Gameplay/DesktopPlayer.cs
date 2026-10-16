using Godot;
using DogeballVR.Zones;

namespace DogeballVR.Gameplay;

/// <summary>
/// Joueur PC pour tester sans casque. ZQSD/WASD : bouger dans sa zone, Maj : s'accroupir,
/// souris : viser, clic droit maintenu : attraper, clic gauche maintenu puis relâché : lancer (plus long = plus fort),
/// Échap : libérer la souris. Le joueur ne peut pas sortir de sa zone.
/// </summary>
public partial class DesktopPlayer : Node3D
{
    const float CatchReach = 2.2f;
    const float MoveSpeed = 6f;      // nerveux : on traverse la zone en 2-3 s
    const float MinThrow = 9f;
    const float MaxThrow = 30f;
    const float MaxChargeTime = 0.8f;
    const float EyeHeight = 1.6f;
    const float CrouchHeight = 0.9f;

    public ZoneManager Zones { get; set; } = null!;
    public Hurtbox Hurtbox { get; set; } = null!;

    /// <summary>Charge du lancer en cours (0 à 1), pour la jauge du HUD.</summary>
    public float ThrowCharge01 => charging ? (float)(chargeTime / MaxChargeTime) : 0f;
    public Ball? HeldBall => held != null && IsInstanceValid(held) ? held : null;

    Camera3D camera = null!;
    Node3D holdPoint = null!;
    Ball? held;
    Vector2 localPos;                // position dans le repère de la zone (centre = 0)
    float yaw, pitch;
    double chargeTime;
    bool charging;
    double catchCooldown;

    public override void _Ready()
    {
        camera = new Camera3D { Name = "Camera", Current = true, Position = new Vector3(0, EyeHeight, 0) };
        AddChild(camera);

        holdPoint = new Node3D { Name = "HoldPoint", Position = new Vector3(0.3f, -0.25f, -0.6f) };
        camera.AddChild(holdPoint);

        Hurtbox.Head = camera;
        AddChild(Hurtbox);

        Input.MouseMode = Input.MouseModeEnum.Captured;
        var zone = Zones.Map.Zones.Find(z => z.Id == Zones.LocalZoneId)!;
        yaw = Mathf.DegToRad(zone.FacingDegrees);
        ApplyTransform();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            yaw -= motion.Relative.X * 0.0025f;
            pitch = Mathf.Clamp(pitch - motion.Relative.Y * 0.0025f, -1.3f, 1.3f);
        }
        else if (e is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
        else if (e is InputEventMouseButton { Pressed: true } && Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        MoveInZone(dt);
        if (held != null && !IsInstanceValid(held)) { held = null; charging = false; chargeTime = 0; }
        if (Hurtbox.Eliminated)
        {
            if (held != null) { held.Release(Vector3.Zero, 0f); held = null; charging = false; chargeTime = 0; }
        }
        else HandleCatchAndThrow(delta);
        ApplyTransform();
    }

    void MoveInZone(float dt)
    {
        var dir = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Z)) dir.Y -= 1;
        if (Input.IsKeyPressed(Key.S)) dir.Y += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Q)) dir.X -= 1;
        if (Input.IsKeyPressed(Key.D)) dir.X += 1;
        if (dir != Vector2.Zero)
        {
            // Mouvement relatif à la direction du regard, converti dans le repère de la zone.
            var fwd = new Vector3(-Mathf.Sin(yaw), 0, -Mathf.Cos(yaw));
            var right = new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw));
            var world = (right * dir.X - fwd * dir.Y).Normalized() * MoveSpeed * dt;
            var zone = Zones.Map.Zones.Find(z => z.Id == Zones.LocalZoneId)!;
            var basis = new Basis(Vector3.Up, Mathf.DegToRad(zone.FacingDegrees)).Inverse();
            var local = basis * world;
            localPos += new Vector2(local.X, local.Z);
        }
        // Le joueur ne sort pas de sa zone.
        float hw = Zones.Map.ZoneWidth * 0.5f - 0.3f;
        float hd = Zones.Map.ZoneDepth * 0.5f - 0.3f;
        localPos = new Vector2(Mathf.Clamp(localPos.X, -hw, hw), Mathf.Clamp(localPos.Y, -hd, hd));
    }

    void HandleCatchAndThrow(double delta)
    {
        bool catching = Input.IsMouseButtonPressed(MouseButton.Right);
        bool leftDown = Input.IsMouseButtonPressed(MouseButton.Left);

        catchCooldown -= delta;
        if (catching && held == null && catchCooldown <= 0)
        {
            // Au clavier-souris on attrape toute balle à portée de bras, même au sol.
            foreach (var node in GetTree().GetNodesInGroup("balls"))
            {
                if (node is not Ball { IsHeld: false } ball) continue;
                if (ball.GlobalPosition.DistanceTo(camera.GlobalPosition) > CatchReach) continue;
                held = ball;
                ball.Hold(holdPoint, Hurtbox.PlayerId, Hurtbox.Team);
                break;
            }
        }

        if (held != null)
        {
            if (leftDown) { charging = true; chargeTime = Mathf.Min(chargeTime + delta, MaxChargeTime); }
            else if (charging)
            {
                float t = (float)(chargeTime / MaxChargeTime);
                float speed = Mathf.Lerp(MinThrow, MaxThrow, t);
                var forward = -camera.GlobalTransform.Basis.Z;
                held.Release(forward * speed, 0f);
                held = null;
                charging = false;
                chargeTime = 0;
                catchCooldown = 0.4;
            }
        }
    }

    void ApplyTransform()
    {
        var zone = Zones.Map.Zones.Find(z => z.Id == Zones.LocalZoneId)!;
        var zoneBasis = new Basis(Vector3.Up, Mathf.DegToRad(zone.FacingDegrees));
        GlobalPosition = zone.Center + zoneBasis * new Vector3(localPos.X, 0, localPos.Y);
        camera.Position = new Vector3(0, Input.IsKeyPressed(Key.Shift) ? CrouchHeight : EyeHeight, 0);
        camera.Rotation = new Vector3(pitch, yaw, 0);
    }
}
