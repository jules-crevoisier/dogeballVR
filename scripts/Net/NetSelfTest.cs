using Godot;
using DogeballVR.Gameplay;

namespace DogeballVR.Net;

/// <summary>
/// Joueur automatique pour tester le réseau sans casque ni clavier : le client déplace sa « main » sur la balle libre
/// la plus proche, l'attrape, puis la renvoie vers l'équipe adverse. Les traces <c>[NetTest]</c> sont lues par la CI.
/// </summary>
public partial class NetSelfTest : Node
{
    public NetSession Net { get; set; } = null!;
    public ClientWorld World { get; set; } = null!;
    public int PlayerId { get; set; }
    public Vector3 AimAt { get; set; }
    /// <summary>Zone du joueur automatique : il ne ramasse que les balles à sa portée (l'hôte refuse le reste).</summary>
    public Vector3 ZoneCenter { get; set; }
    public Vector2 ZoneHalfSize { get; set; }

    Vector3 hand;
    double timer;
    int catches, throws;
    double logTimer;

    public override void _Ready()
    {
        hand = ZoneCenter + Vector3.Up * 1.2f;
        Net.LocalPose = () => new PoseData(new Vector3(hand.X, 1.6f, hand.Z), 0, hand, hand);
    }

    const float HandSpeed = 8f;   // sous la vitesse maximale que l'hôte accepte (12 m/s)

    public override void _PhysicsProcess(double delta)
    {
        timer += delta;
        logTimer += delta;
        if (logTimer > 5)
        {
            logTimer = 0;
            GD.Print($"[NetTest] connecte={Net.Connected} snapshots={Net.SnapshotsReceived} balles={World.BallCount} avatars={World.AvatarCount} prises={catches} lancers={throws}");
        }

        var held = BallActions.FindHeld(GetTree(), PlayerId);
        if (held != null)
        {
            if (timer < 0.5) return;
            timer = 0;
            var dir = (AimAt - held.GlobalPosition).Normalized();
            Net.RequestThrow(1, dir * 18f + Vector3.Up * 1f, 0f);
            throws++;
            return;
        }

        // Aller vers la balle libre la plus proche de la zone, puis demander à l'attraper.
        Ball? best = null;
        float bestD = float.MaxValue;
        foreach (var n in GetTree().GetNodesInGroup("balls"))
        {
            if (n is not Ball { IsHeld: false } b) continue;
            var rel = b.GlobalPosition - ZoneCenter;
            if (Mathf.Abs(rel.X) > ZoneHalfSize.X || Mathf.Abs(rel.Z) > ZoneHalfSize.Y || b.GlobalPosition.Y > 2.5f) continue;
            float d = b.GlobalPosition.DistanceTo(hand);
            if (d < bestD) { bestD = d; best = b; }
        }
        if (best == null) return;
        hand = hand.MoveToward(best.GlobalPosition, HandSpeed * (float)delta);
        if (bestD < 0.5f && timer > 0.4)
        {
            timer = 0;
            Net.RequestCatch(best.NetId, 1);
            catches++;
        }
    }
}
