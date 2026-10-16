using Godot;
using DogeballVR.Gameplay;
using DogeballVR.Visual;

namespace DogeballVR.Net;

/// <summary>
/// Côté hôte : un joueur distant. Son avatar suit la pose reçue, sa hurtbox suit sa tête, et les balles qu'il attrape
/// sont rattachées à ses mains (l'hôte garde la physique). Le nœud reste à l'origine : <see cref="Body"/> marque ses pieds.
/// </summary>
public partial class RemotePlayer : Node3D
{
    public int PeerId { get; set; }
    public int PlayerId { get; set; }
    public int Team { get; set; }
    /// <summary>Pose avant le premier paquet reçu : le centre de sa zone (pas de flash à l'origine du monde).</summary>
    public PoseData StartPose { get; set; }
    public Hurtbox Hurtbox { get; private set; } = null!;
    public Avatar Avatar { get; private set; } = null!;
    /// <summary>Point au sol sous le joueur : cible des bots.</summary>
    public Node3D Body { get; private set; } = null!;

    public double LastPoseAt { get; set; }
    public double NextCatchAt { get; set; }
    /// <summary>Lift tenu par l'hôte : altitude acceptée, début de la montée, prochaine montée possible.</summary>
    public float HostElevation { get; set; }
    public double LiftHighSince { get; set; }
    public double CrouchOnPadSince { get; set; }
    /// <summary>Dash (jeu libre) : charges restantes tenues par l'hôte, fin de l'invulnérabilité, compteur de dashs du client, saut de retour de KO toléré.</summary>
    public float DashBudget { get; set; } = 2f;
    public double DashUntil { get; set; }
    public int LastDashCount { get; set; }
    public double LastPerfectAt { get; set; } = -10;
    public bool TeleportPending { get; set; }
    public double TeleportDeadline { get; set; }
    /// <summary>Dernière pose acceptée par l'hôte (non lissée) : référence de la vérification de vitesse.</summary>
    public PoseData TargetPose => hasPose ? target : new PoseData(Vector3.Zero, 0, Vector3.Zero, Vector3.Zero);
    PoseData target, current;
    bool hasPose;

    public override void _Ready()
    {
        Avatar = Avatar.Create(Team, bot: false);
        AddChild(Avatar);
        Body = new Node3D { Name = "Body" };
        AddChild(Body);
        SetPose(StartPose);
        Avatar.Pose(StartPose.Head, StartPose.Yaw, StartPose.Left, StartPose.Right);
        Hurtbox = new Hurtbox { Name = $"Hurtbox{PlayerId}", PlayerId = PlayerId, Team = Team, Head = Avatar.Head };
        AddChild(Hurtbox);
    }

    public PoseData GetPose() => hasPose ? current : new PoseData(Vector3.Zero, 0, Vector3.Zero, Vector3.Zero);

    public void SetPose(PoseData pose)
    {
        target = pose;
        if (!hasPose) { current = pose; hasPose = true; }
    }

    /// <summary>Lâche les balles tenues (départ du joueur, KO) : elles retombent au sol.</summary>
    public void DropBalls()
    {
        for (int hand = 0; hand < 2; hand++) HeldBall(hand)?.Release(Vector3.Zero, 0f);
    }

    public Node3D HandNode(int hand) => hand == 0 ? Avatar.LeftHand : Avatar.RightHand;

    public Ball? HeldBall(int hand)
    {
        foreach (var child in HandNode(hand).GetChildren())
            if (child is Ball b && IsInstanceValid(b)) return b;
        return null;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!hasPose) return;
        current = current.Lerp(target, 1f - Mathf.Exp(-25f * (float)delta));
        Avatar.Pose(current.Head, current.Yaw, current.Left, current.Right);
        Body.Position = new Vector3(current.Head.X, current.Elevation, current.Head.Z);
        Hurtbox.Elevation = current.Elevation;
        Hurtbox.DashInvulnerable = Time.GetTicksMsec() / 1000.0 < DashUntil;
        Avatar.Visible = !Hurtbox.Eliminated;
        if (Hurtbox.Eliminated)
            DropBalls();
    }
}
