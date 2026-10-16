using Godot;

namespace DogeballVR.Gameplay;

/// <summary>
/// Balle de dodgeball. Côté serveur (autorité) : physique et touches. Côté clients : affichage.
/// Charge de passe (0 à 3) : chaque passe d'un allié ajoute une charge, la balle devient dangereuse et lumineuse.
/// </summary>
public partial class Ball : RigidBody3D
{
    public const int MaxCharge = 3;

    public int OwnerPlayerId { get; set; } = -1;
    public int OwnerTeam { get; set; } = -1;
    public int Charge { get; private set; }
    public bool IsHeld { get; private set; }

    /// <summary>Effet de courbe : vitesse angulaire du poignet au lâcher (rad/s, axe vertical).</summary>
    float curveSpin;

    public override void _Ready()
    {
        Mass = 0.6f;
        GravityScale = 0.6f; // un peu flottante, plus lisible en VR
        ContinuousCd = true;
    }

    public void Hold(Node3D hand)
    {
        IsHeld = true;
        Freeze = true;
        FreezeMode = FreezeModeEnum.Kinematic;
        Reparent(hand, false);
        Position = Vector3.Zero;
    }

    public void Release(Vector3 velocity, float spin)
    {
        var world = GetTree().CurrentScene;
        var xf = GlobalTransform;
        Reparent(world, false);
        GlobalTransform = xf;
        IsHeld = false;
        Freeze = false;
        curveSpin = spin;
        LinearVelocity = velocity;
    }

    public void AddCharge()
    {
        Charge = Mathf.Min(Charge + 1, MaxCharge);
    }

    public void ResetCharge() => Charge = 0;

    public override void _PhysicsProcess(double delta)
    {
        if (IsHeld || curveSpin == 0f) return;
        // Force latérale (Magnus simplifié) : perpendiculaire à la vitesse, dans le plan horizontal.
        var v = LinearVelocity;
        var side = v.Cross(Vector3.Up).Normalized();
        ApplyCentralForce(side * curveSpin * v.Length() * 0.05f);
        curveSpin = Mathf.MoveToward(curveSpin, 0f, (float)delta * 4f);
    }
}
