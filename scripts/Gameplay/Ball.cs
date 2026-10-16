using Godot;

namespace DogeballVR.Gameplay;

/// <summary>
/// Balle de dodgeball. Côté hôte (autorité) : physique et touches. Côté clients : affichage.
/// Charge de passe (0 à 3) : chaque passe d'un allié ajoute une charge, la balle devient plus lumineuse.
/// </summary>
public partial class Ball : RigidBody3D
{
    public const int MaxCharge = 3;
    public const float HitMinSpeed = 6f; // en dessous, la balle ne blesse pas

    static readonly Color[] ChargeColors =
    {
        new(0.9f, 0.9f, 0.9f), new(1f, 0.9f, 0.2f), new(1f, 0.5f, 0.1f), new(1f, 0.1f, 0.1f),
    };

    public int OwnerPlayerId { get; private set; } = -1;
    public int OwnerTeam { get; private set; } = -1;
    public int Charge { get; private set; }
    public bool IsHeld { get; private set; }

    StandardMaterial3D material = null!;

    /// <summary>Effet de courbe : vitesse angulaire du poignet au lâcher (rad/s, axe vertical).</summary>
    float curveSpin;

    public static Ball Create()
    {
        var ball = new Ball { Name = "Ball" };
        ball.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.11f } });
        ball.material = new StandardMaterial3D { EmissionEnabled = true };
        ball.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.11f, Height = 0.22f },
            MaterialOverride = ball.material,
        });
        ball.UpdateLook();
        return ball;
    }

    public override void _Ready()
    {
        AddToGroup("balls");
        Mass = 0.6f;
        GravityScale = 0.6f; // un peu flottante, plus lisible en VR
        ContinuousCd = true;
    }

    /// <summary>Un joueur attrape la balle. Passe d'un allié : +1 charge. Balle volée à l'adversaire : charge à zéro.</summary>
    public void Hold(Node3D hand, int playerId, int team)
    {
        if (OwnerTeam == team && OwnerPlayerId >= 0 && OwnerPlayerId != playerId) AddCharge();
        else if (OwnerTeam != team) ResetCharge();
        OwnerPlayerId = playerId;
        OwnerTeam = team;
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

    /// <summary>La balle a touché : elle devient neutre (plus personne n'est blessé tant qu'elle n'est pas reprise).</summary>
    public void Spent()
    {
        OwnerPlayerId = -1;
        OwnerTeam = -1;
        ResetCharge();
    }

    public void AddCharge()
    {
        Charge = Mathf.Min(Charge + 1, MaxCharge);
        UpdateLook();
    }

    public void ResetCharge()
    {
        Charge = 0;
        UpdateLook();
    }

    void UpdateLook()
    {
        var c = ChargeColors[Charge];
        material.AlbedoColor = c;
        material.Emission = c;
        material.EmissionEnergyMultiplier = 0.3f + Charge * 0.9f;
    }

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
