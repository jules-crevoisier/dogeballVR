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

    Vector3 hand = new(0, 1.2f, 0);
    double timer;
    int catches, throws;
    double logTimer;

    public override void _Ready() => Net.LocalPose = () => new PoseData(new Vector3(hand.X, 1.6f, hand.Z), 0, hand, hand);

    public override void _PhysicsProcess(double delta)
    {
        timer += delta;
        logTimer += delta;
        if (logTimer > 5)
        {
            logTimer = 0;
            GD.Print($"[NetTest] connecte={Net.Connected} snapshots={Net.SnapshotsReceived} balles={World.BallCount} avatars={World.AvatarCount} prises={catches} lancers={throws}");
        }
        if (timer < 1.0) return;
        timer = 0;

        var held = BallActions.FindHeld(GetTree(), PlayerId);
        if (held != null)
        {
            var dir = (AimAt - held.GlobalPosition).Normalized();
            Net.RequestThrow(1, dir * 18f + Vector3.Up * 1f, 0f);
            throws++;
            return;
        }
        Ball? best = null;
        float bestD = float.MaxValue;
        foreach (var n in GetTree().GetNodesInGroup("balls"))
        {
            if (n is not Ball { IsHeld: false } b) continue;
            float d = b.GlobalPosition.DistanceTo(hand);
            if (d < bestD) { bestD = d; best = b; }
        }
        if (best == null) return;
        hand = best.GlobalPosition; // la main va sur la balle, puis demande à l'attraper
        Net.RequestCatch(best.NetId, 1);
        catches++;
    }
}
