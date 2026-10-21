using Godot;

namespace DogeballVR.Gameplay;

/// <summary>
/// Zone où un joueur (ou un bot) peut être touché. Pour un joueur : suit la tête (la hauteur de la capsule
/// baisse quand on s'accroupit). Pour un bot : capsule fixe. Un joueur KO (Eliminated) ne peut plus être touché.
/// </summary>
public partial class Hurtbox : Area3D
{
    [Signal] public delegate void HitEventHandler(int playerId, int shooterId, int charge, float throwDistance);

    public int PlayerId { get; set; }
    public int Team { get; set; }
    public bool Eliminated { get; set; }

    /// <summary>Hôte, joueur distant : délai avant d'appliquer un coup rapide, pendant lequel une prise valide (CatchRpc) l'annule.</summary>
    public float HitGraceSeconds { get; set; }

    /// <summary>Main (0 gauche, 1 droite) qui tient une balle, -1 sinon : un joueur ne tient qu'une balle à la fois.</summary>
    public int HeldHand { get; set; } = -1;

    /// <summary>Côté client : la touche est jugée par l'hôte, cette zone ne détecte rien.</summary>
    public bool Passive { get; set; }

    /// <summary>Dash en cours : les balles passent à travers.</summary>
    public bool DashInvulnerable { get; set; }

    /// <summary>Altitude des pieds (plateau haut du lift) : la capsule va des pieds à la tête.</summary>
    public float Elevation { get; set; }

    /// <summary>Caméra à suivre (joueur). Si null, la hurtbox reste là où elle est (bot).</summary>
    public Node3D? Head { get; set; }

    CapsuleShape3D capsule = new() { Radius = 0.3f, Height = 1.7f };

    public override void _Ready()
    {
        AddChild(new CollisionShape3D { Shape = capsule });
        Monitoring = !Passive;
        AddToGroup("hurtboxes");
        BodyEntered += OnBodyEntered;
        if (Head == null) Position = new Vector3(Position.X, 0.85f, Position.Z);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Head == null) return;
        float h = Mathf.Max(Head.GlobalPosition.Y - Elevation + 0.1f, 0.6f);
        capsule.Height = h;
        GlobalPosition = new Vector3(Head.GlobalPosition.X, Elevation + h * 0.5f, Head.GlobalPosition.Z);
    }

    /// <summary>Posé par le MatchController : faux pendant l'invulnérabilité après une touche.</summary>
    public System.Func<bool>? Vulnerable { get; set; }

    void OnBodyEntered(Node3D body)
    {
        if (Eliminated || DashInvulnerable || body is not Ball ball || ball.IsHeld || ball.HasExploded) return;
        if (ball.OwnerTeam < 0 || ball.OwnerTeam == Team) return;          // neutre ou tir allié
        if (ball.IsPass) return;                                           // une passe ne blesse personne
        if (ball.LinearVelocity.Length() < (ball.ThrowMode == Ball.ModeLob ? Ball.LobHitMinSpeed : Ball.HitMinSpeed)) return;       // trop lente
        if (Vulnerable != null && !Vulnerable()) return;                     // invulnérable : la balle passe, elle n'est pas consommée
        if (HitGraceSeconds > 0f && !graceBalls.Contains(ball) && ball.Kind != Ball.KindBomb && ball.LinearVelocity.Length() >= 10f)
        {
            graceBalls.Add(ball);
            GetTree().CreateTimer(HitGraceSeconds).Timeout += () => { if (!IsInstanceValid(this)) return; graceBalls.Remove(ball); if (IsInstanceValid(ball)) Apply(ball); };
            return;
        }
        Apply(ball);
    }

    readonly System.Collections.Generic.HashSet<Ball> graceBalls = new();

    void Apply(Ball ball)
    {
        if (Eliminated || DashInvulnerable || ball.IsHeld || ball.HasExploded || ball.IsPass || ball.OwnerTeam < 0 || ball.OwnerTeam == Team) return;
        if (Vulnerable != null && !Vulnerable()) return;
        int shooter = ball.OwnerPlayerId;
        int charge = ball.Charge;
        float distance = ball.ThrowDistance;
        bool bomb = ball.Kind == Ball.KindBomb;
        int shooterTeam = ball.OwnerTeam;
        ball.Spent();
        EmitSignal(SignalName.Hit, PlayerId, shooter, charge, distance);
        if (bomb) ball.Explode(PlayerId, shooterTeam, shooter);
    }
}
