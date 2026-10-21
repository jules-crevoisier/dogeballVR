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
    // Interpolation : on affiche les autres joueurs avec un léger retard (≈ 2 snapshots + gigue), entre deux poses reçues,
    // au lieu de courir après la dernière : mouvement régulier malgré la gigue et les paquets perdus.
    readonly Dictionary<int, List<(double t, PoseData pose)>> history = new();
    double clockOffset, jitter, lastHostRaw = -1, hostWraps;
    bool clockKnown;
    /// <summary>Retard d'affichage des autres joueurs (s), adapté à la gigue mesurée.</summary>
    public double InterpDelay => Mathf.Clamp(1.5 / 30.0 + 2.0 * jitter, 0.06, 0.25);
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
        double hostTime = UnwrapHostTime(NetSession.HostTimeOf(data));
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
            if (hostTime >= 0)
            {
                if (!history.TryGetValue(p.Id, out var h)) history[p.Id] = h = new();
                if (h.Count == 0 || hostTime > h[^1].t) h.Add((hostTime, p.Pose));
                if (h.Count > 24) h.RemoveAt(0);
            }
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

    /// <summary>Horloge de l'hôte rendue continue (elle repart à 0 tous les 1000 s) et calage de l'horloge locale dessus.</summary>
    double UnwrapHostTime(float raw)
    {
        if (raw < 0) return -1;
        if (lastHostRaw >= 0 && raw < lastHostRaw - NetSession.HostClockWrap / 2) hostWraps += NetSession.HostClockWrap;
        lastHostRaw = raw;
        double hostTime = raw + hostWraps;
        double sample = hostTime - Time.GetTicksMsec() / 1000.0;
        if (!clockKnown) { clockOffset = sample; clockKnown = true; }
        else
        {
            // Un snapshot en retard ne recule pas l'horloge ; un snapshot en avance la rattrape vite.
            double err = sample - clockOffset;
            clockOffset += err > 0 ? err * 0.3 : err * 0.02;
            jitter += (System.Math.Abs(err) - jitter) * 0.05;
        }
        return hostTime;
    }

    /// <summary>Pose interpolée d'un joueur à l'instant hôte <paramref name="renderTime"/> (null si l'historique ne suffit pas).</summary>
    PoseData? Sample(int id, double renderTime)
    {
        if (!history.TryGetValue(id, out var h) || h.Count == 0) return null;
        if (renderTime <= h[0].t) return h[0].pose;
        for (int i = h.Count - 1; i > 0; i--)
        {
            if (h[i - 1].t > renderTime) continue;
            var (t0, a) = h[i - 1];
            var (t1, b) = h[i];
            float k = (float)((renderTime - t0) / System.Math.Max(1e-4, t1 - t0));
            // Téléportation (retour de KO, échange de plaques) : pas de glissement à travers l'arène.
            if (a.Head.DistanceTo(b.Head) > 3f) return k < 0.5f ? a : b;
            return a.Lerp(b, Mathf.Clamp(k, 0f, 1f));
        }
        return h[^1].pose;   // au-delà de la dernière pose reçue (paquets perdus) : on la tient
    }

    public override void _Process(double delta)
    {
        float t = 1f - Mathf.Exp(-25f * (float)delta);
        float tFine = 1f - Mathf.Exp(-60f * (float)delta);
        double renderTime = Time.GetTicksMsec() / 1000.0 + clockOffset - InterpDelay;
        foreach (var (id, avatar) in avatars)
        {
            var cur = new PoseData(avatar.Head.Position, avatar.Head.Rotation.Y, avatar.LeftHand.Position, avatar.RightHand.Position);
            var sampled = clockKnown ? Sample(id, renderTime) : null;
            var next = sampled is { } s ? cur.Lerp(s, tFine) : cur.Lerp(poseTargets[id], t);
            avatar.Pose(next.Head, next.Yaw, next.Left, next.Right);
        }
    }
}
