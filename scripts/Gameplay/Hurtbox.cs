using Godot;

namespace DogeballVR.Gameplay;

/// <summary>
/// Zone où un joueur (ou un bot) peut être touché. Pour un joueur : suit la tête (la hauteur de la capsule
/// baisse quand on s'accroupit). Pour un bot : capsule fixe. Un joueur KO (Eliminated) ne peut plus être touché.
/// </summary>
public partial class Hurtbox : Area3D
{
    [Signal] public delegate void HitEventHandler(int playerId, int shooterId, int charge);

    public int PlayerId { get; set; }
    public int Team { get; set; }
    public bool Eliminated { get; set; }

    /// <summary>Côté client : la touche est jugée par l'hôte, cette zone ne détecte rien.</summary>
    public bool Passive { get; set; }

    /// <summary>Caméra à suivre (joueur). Si null, la hurtbox reste là où elle est (bot).</summary>
    public Node3D? Head { get; set; }

    CapsuleShape3D capsule = new() { Radius = 0.3f, Height = 1.7f };

    public override void _Ready()
    {
        AddChild(new CollisionShape3D { Shape = capsule });
        Monitoring = !Passive;
        BodyEntered += OnBodyEntered;
        if (Head == null) Position = new Vector3(Position.X, 0.85f, Position.Z);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Head == null) return;
        float h = Mathf.Max(Head.GlobalPosition.Y + 0.1f, 0.6f);
        capsule.Height = h;
        GlobalPosition = new Vector3(Head.GlobalPosition.X, h * 0.5f, Head.GlobalPosition.Z);
    }

    void OnBodyEntered(Node3D body)
    {
        if (Eliminated || body is not Ball ball || ball.IsHeld) return;
        if (ball.OwnerTeam < 0 || ball.OwnerTeam == Team) return;          // neutre ou tir allié
        if (ball.LinearVelocity.Length() < Ball.HitMinSpeed) return;       // trop lente
        int shooter = ball.OwnerPlayerId;
        int charge = ball.Charge;
        ball.Spent();
        EmitSignal(SignalName.Hit, PlayerId, shooter, charge);
    }
}
