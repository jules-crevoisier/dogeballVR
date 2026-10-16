using System.Collections.Generic;
using Godot;
using DogeballVR.Gameplay;
using DogeballVR.Maps;
using DogeballVR.Visual;

namespace DogeballVR.Net;

/// <summary>
/// Côté client : le monde est une image de celui de l'hôte. Balles et avatars des autres joueurs sont des marionnettes
/// déplacées par les snapshots ; l'état du joueur local (KO, vies) vient aussi de l'hôte.
/// </summary>
public partial class ClientWorld : Node3D
{
    public NetSession Net { get; set; } = null!;
    public MapData Map { get; set; } = null!;
    public int LocalPlayerId { get; set; }
    public Hurtbox LocalHurtbox { get; set; } = null!;
    public Label3D Hud { get; set; } = null!;

    readonly Dictionary<int, Ball> balls = new();
    readonly Dictionary<int, Avatar> avatars = new();
    // Cibles passives (groupe « hurtboxes ») : l'aide à la visée et le viseur du joueur PC en ont besoin côté client.
    readonly Dictionary<int, Hurtbox> aimTargets = new();
    readonly Dictionary<int, PoseData> poseTargets = new();
    readonly Dictionary<int, int> lives = new();

    readonly Dictionary<int, float> elevations = new();
    /// <summary>Altitude du plateau d'une zone (lift), d'après le dernier snapshot.</summary>
    public float ElevationOf(int playerId) => elevations.TryGetValue(playerId, out float e) ? e : 0f;

    public int Lives(int playerId) => lives.TryGetValue(playerId, out int n) ? n : 0;
    public int BallCount => balls.Count;
    public int AvatarCount => avatars.Count;

    public override void _Ready()
    {
        Net.SnapshotReceived += OnSnapshot;
        Net.HudReceived += text => Hud.Text = text;
    }

    readonly HashSet<int> loggedKinds = new(), explodedSeen = new();

    void OnSnapshot(float[] data)
    {
        var (players, snapBalls) = NetSession.Decode(data);
        float rotorAngle = NetSession.RotorAngleOf(data);
        if (rotorAngle >= 0f && Maps.Rotor.Current is { } rotor) { rotor.Authority = false; rotor.SetRemoteAngle(rotorAngle); }
        foreach (var p in players)
        {
            lives[p.Id] = p.Lives;
            elevations[p.Id] = p.Pose.Elevation;
            if (p.Id == LocalPlayerId) { LocalHurtbox.Eliminated = p.Eliminated; continue; }
            if (!avatars.TryGetValue(p.Id, out var avatar))
            {
                int team = Map.Zones.Find(z => z.Id == p.Id)?.Team ?? 0;
                avatar = Avatar.Create(team, p.Bot);
                AddChild(avatar);
                avatars[p.Id] = avatar;
                poseTargets[p.Id] = p.Pose;
                avatar.Pose(p.Pose.Head, p.Pose.Yaw, p.Pose.Left, p.Pose.Right);
                var marker = new Hurtbox { Name = $"Aim{p.Id}", PlayerId = p.Id, Team = team, Passive = true };
                AddChild(marker);
                aimTargets[p.Id] = marker;
            }
            poseTargets[p.Id] = p.Pose;
            if (aimTargets.TryGetValue(p.Id, out var aim))
            {
                aim.GlobalPosition = p.Pose.Head + new Vector3(0, -0.4f, 0);
                aim.Eliminated = p.Eliminated;
            }
            avatar.Visible = !p.Eliminated;
        }

        var seen = new HashSet<int>();
        foreach (var b in snapBalls)
        {
            seen.Add(b.NetId);
            if (!balls.TryGetValue(b.NetId, out var ball) || !IsInstanceValid(ball))
            {
                ball = Ball.CreatePuppet(b.NetId);
                AddChild(ball);
                balls[b.NetId] = ball;
            }
            ball.ApplyNet(b.Position, b.Velocity, b.Team, b.OwnerId, b.Charge, b.Held, b.Pass, b.Kind);
            if (b.Kind == Ball.KindExploded && explodedSeen.Add(b.NetId)) { DogeballVR.Visual.Explosion.Spawn(this, b.Position, Ball.BombRadius); GD.Print("[Client] Onde de choc de bombe reçue"); }
            if (b.Kind != 0 && loggedKinds.Add(b.NetId)) GD.Print($"[Client] Balle spéciale reçue : type {b.Kind}");
        }
        foreach (var id in new List<int>(balls.Keys))
        {
            if (seen.Contains(id)) continue;
            if (IsInstanceValid(balls[id]))
            {
                balls[id].QueueFree();
            }
            balls.Remove(id);
        }
    }

    public override void _Process(double delta)
    {
        float t = 1f - Mathf.Exp(-25f * (float)delta);
        foreach (var (id, avatar) in avatars)
        {
            var cur = new PoseData(avatar.Head.Position, avatar.Head.Rotation.Y, avatar.LeftHand.Position, avatar.RightHand.Position);
            var next = cur.Lerp(poseTargets[id], t);
            avatar.Pose(next.Head, next.Yaw, next.Left, next.Right);
        }
    }
}
