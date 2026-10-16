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
    readonly Dictionary<int, PoseData> poseTargets = new();
    readonly Dictionary<int, int> lives = new();

    public int Lives(int playerId) => lives.TryGetValue(playerId, out int n) ? n : 0;
    public int BallCount => balls.Count;
    public int AvatarCount => avatars.Count;

    public override void _Ready()
    {
        Net.SnapshotReceived += OnSnapshot;
        Net.HudReceived += text => Hud.Text = text;
    }

    void OnSnapshot(float[] data)
    {
        var (players, snapBalls) = NetSession.Decode(data);
        foreach (var p in players)
        {
            lives[p.Id] = p.Lives;
            if (p.Id == LocalPlayerId) { LocalHurtbox.Eliminated = p.Eliminated; continue; }
            if (!avatars.TryGetValue(p.Id, out var avatar))
            {
                int team = Map.Zones.Find(z => z.Id == p.Id)?.Team ?? 0;
                avatar = Avatar.Create(team, p.Bot);
                AddChild(avatar);
                avatars[p.Id] = avatar;
                poseTargets[p.Id] = p.Pose;
                avatar.Pose(p.Pose.Head, p.Pose.Yaw, p.Pose.Left, p.Pose.Right);
            }
            poseTargets[p.Id] = p.Pose;
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
            ball.ApplyNet(b.Position, b.Velocity, b.Team, b.OwnerId, b.Charge, b.Held);
        }
        foreach (var id in new List<int>(balls.Keys))
        {
            if (seen.Contains(id)) continue;
            if (IsInstanceValid(balls[id])) balls[id].QueueFree();
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
