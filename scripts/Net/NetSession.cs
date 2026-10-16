using System;
using System.Collections.Generic;
using Godot;
using DogeballVR.Gameplay;

namespace DogeballVR.Net;

/// <summary>
/// Session réseau (ENet). L'hôte est seul maître des balles, des touches et du score ; il diffuse un snapshot
/// de l'état (joueurs, balles) 30 fois par seconde. Les clients n'envoient que leur pose (tête et mains) et des
/// demandes d'attraper / de lancer. Même principe qu'un serveur dédié plus tard (4v4, inter-lieux).
/// </summary>
public partial class NetSession : Node
{
    public const int DefaultPort = 7777;
    public const int MaxPlayers = 4;
    const float SnapshotInterval = 1f / 30f;
    const float PoseInterval = 1f / 30f;
    const float CatchReach = 1.2f;    // tolérance de l'hôte : la main (déjà bornée près de la tête) doit être près de la balle
    const float MaxThrowSpeed = 30f;  // = plafond côté joueur (HandThrower, DesktopPlayer)
    const float MaxMoveSpeed = 8f;    // m/s : au-delà, la pose est ramenée (pas de téléportation)
    const double CatchCooldown = 0.2; // entre un lancer et la prise suivante
    const double MissedCatchCooldown = 0.15; // une demande refusée n'en autorise pas une autre avant ce délai
    const double MinPoseInterval = 1.0 / 60.0; // au-delà, les paquets de pose sont ignorés (anti-inondation)
    const double HelloTimeout = 5.0;          // un pair sans Hello est déconnecté
    const int PlayerFloats = 15;
    const float MaxHandReach = 1.0f;   // une main reste près de la tête
    const float MaxSpin = 12f;
    const int BallFloats = 11;

    public NetMode Mode { get; private set; } = NetMode.Offline;
    public bool IsClient => Mode == NetMode.Client;
    public bool IsHost => Mode == NetMode.Host;
    public bool Connected => Multiplayer.MultiplayerPeer is { } p && p.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;
    public int ZoneId { get; set; }
    /// <summary>Latence simulée (s, par sens) : envois du client et snapshots reçus sont retardés. 0 = désactivée.</summary>
    public double SimulatedLag { get; set; }
    readonly Queue<(double due, Action action)> delayed = new();

    void Later(Action action)
    {
        if (SimulatedLag <= 0) action();
        else delayed.Enqueue((Time.GetTicksMsec() / 1000.0 + SimulatedLag, action));
    }

    // Hôte : qui sont les joueurs et où ils sont (fournisseurs de pose), état de vie, texte du tableau.
    public Dictionary<int, Func<PoseData>> PoseProviders { get; } = new();
    public Func<int, int> LivesOf { get; set; } = _ => 0;
    public Func<int, bool> IsEliminated { get; set; } = _ => false;
    public Func<int, bool> IsBot { get; set; } = _ => false;
    /// <summary>Limites de l'arène (x, z) : l'hôte n'accepte aucune pose en dehors.</summary>
    public Rect2 ArenaXZ { get; set; } = new(-100, -100, 200, 200);
    /// <summary>Rectangle (x, z) de la zone d'un joueur, marge comprise : un client ne sort pas de sa zone.</summary>
    public Func<int, Rect2> ZoneRect { get; set; } = _ => new Rect2(-100, -100, 200, 200);
    /// <summary>Altitude maximale d'un plateau (lift) : borne la hauteur acceptée pour un joueur distant.</summary>
    public float MaxElevation { get; set; }
    /// <summary>Lift de la zone d'un joueur (null : pas de lift) : l'hôte tient la machine d'états, le client ne fait que demander.</summary>
    public Func<int, Maps.LiftDef?> LiftOf { get; set; } = _ => null;
    /// <summary>Vrai si un coéquipier est déjà en haut (une seule zone haute par équipe).</summary>
    public Func<int, bool> TeammateHigh { get; set; } = _ => false;
    public Func<string> HudText { get; set; } = () => "";
    public Dictionary<int, RemotePlayer> Remotes { get; } = new();   // par pair réseau

    // Client.
    public Func<PoseData>? LocalPose { get; set; }

    public event Action<int, int>? PeerHello;       // pair, zone demandée
    public event Action<int>? PeerLeft;
    public event Action<float[]>? SnapshotReceived;
    public event Action<string>? HudReceived;

    double snapshotTimer, poseTimer;
    string lastHud = "";
    public int SnapshotsSent { get; private set; }
    public int SnapshotsReceived { get; private set; }

    public Error Host(int port = DefaultPort)
    {
        var peer = new ENetMultiplayerPeer();
        var err = peer.CreateServer(port, MaxPlayers - 1);
        if (err != Error.Ok) return err;
        Multiplayer.MultiplayerPeer = peer;
        Mode = NetMode.Host;
        Multiplayer.PeerDisconnected += id => PeerLeft?.Invoke((int)id);
        GD.Print($"[Net] Hôte en écoute sur le port {port}");
        return err;
    }

    public Error Join(string address, int port, int zoneId)
    {
        var peer = new ENetMultiplayerPeer();
        var err = peer.CreateClient(address, port);
        if (err != Error.Ok) return err;
        Multiplayer.MultiplayerPeer = peer;
        Mode = NetMode.Client;
        ZoneId = zoneId;
        Multiplayer.ConnectedToServer += () =>
        {
            GD.Print($"[Net] Connecté à l'hôte, zone {zoneId}");
            RpcId(1, MethodName.HelloRpc, zoneId);
        };
        Multiplayer.ServerDisconnected += () => { GD.Print("[Net] Hôte déconnecté"); HudReceived?.Invoke("HÔTE DÉCONNECTÉ"); };
        Multiplayer.ConnectionFailed += () => { GD.Print("[Net] Connexion impossible"); HudReceived?.Invoke("CONNEXION IMPOSSIBLE"); };
        return err;
    }

    public override void _PhysicsProcess(double delta)
    {
        double clockNow = Time.GetTicksMsec() / 1000.0;
        while (delayed.Count > 0 && delayed.Peek().due <= clockNow) delayed.Dequeue().action();
        if (Mode == NetMode.Host) HostTick(delta);
        else if (Mode == NetMode.Client) ClientTick(delta);
    }

    // ---------------------------------------------------------------- client -> hôte

    void ClientTick(double delta)
    {
        poseTimer += delta;
        if (poseTimer < PoseInterval || LocalPose == null || !Connected) return;
        poseTimer = 0;
        var p = LocalPose();
        Later(() => RpcId(1, MethodName.PoseRpc, p.Head, p.Yaw, p.Left, p.Right, p.Elevation));
    }

    public void RequestCatch(int ballId, int hand)
    {
        if (Connected) Later(() => RpcId(1, MethodName.CatchRpc, ballId, hand));
    }

    public void RequestThrow(int hand, Vector3 velocity, float spin)
    {
        if (Connected) Later(() => RpcId(1, MethodName.ThrowRpc, hand, velocity, spin));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void HelloRpc(int zoneId)
    {
        if (!IsHost) return;
        int peer = Multiplayer.GetRemoteSenderId();
        GD.Print($"[Net] Pair {peer} demande la zone {zoneId}");
        if (Remotes.ContainsKey(peer)) return;   // un seul Hello par pair
        PeerHello?.Invoke(peer, zoneId);
        if (!Remotes.ContainsKey(peer)) RpcId(peer, MethodName.HudRpc, "ZONE REFUSÉE : DÉJÀ PRISE OU INCONNUE");
        else RpcId(peer, MethodName.HudRpc, HudText());
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    void PoseRpc(Vector3 head, float yaw, Vector3 left, Vector3 right, float elevation)
    {
        if (!IsHost) return;
        if (!Remotes.TryGetValue(Multiplayer.GetRemoteSenderId(), out var remote)) return;
        if (!head.IsFinite() || !left.IsFinite() || !right.IsFinite() || !float.IsFinite(yaw) || !float.IsFinite(elevation)) return;
        elevation = ValidateElevation(remote, head, elevation);
        // Bornes de l'hôte : la tête reste dans l'arène, les mains près de la tête (pas de téléportation hors de portée).
        var zone = ZoneRect(remote.PlayerId);
        head = new Vector3(Mathf.Clamp(head.X, zone.Position.X, zone.End.X), Mathf.Clamp(head.Y, 0.8f + elevation, 2.3f + elevation),
            Mathf.Clamp(head.Z, zone.Position.Y, zone.End.Y));
        // Vitesse maximale : on ramène la tête sur la distance parcourable depuis la dernière pose acceptée.
        double now = Time.GetTicksMsec() / 1000.0;
        if (remote.LastPoseAt > 0 && now - remote.LastPoseAt < MinPoseInterval) return;   // inondation : paquet ignoré
        var last = remote.GetPose().Head;
        // Allocation sur le temps réellement écoulé (le premier paquet part de la pose de départ, sur 0,1 s).
        float allowed = MaxMoveSpeed * (float)Mathf.Clamp(remote.LastPoseAt > 0 ? now - remote.LastPoseAt : 0.1, MinPoseInterval, 0.5);
        var horizontal = new Vector3(head.X - last.X, 0, head.Z - last.Z);
        if (horizontal.Length() > allowed)
        {
            var limited = horizontal.LimitLength(allowed);
            head = new Vector3(last.X + limited.X, head.Y, last.Z + limited.Z);
        }
        // Vitesse verticale plafonnée : pas de vol ni d'accroupi instantané pour réduire la cible.
        float dt = (float)Mathf.Clamp(remote.LastPoseAt > 0 ? now - remote.LastPoseAt : 0.1, MinPoseInterval, 0.5);
        // La vitesse verticale se mesure par rapport au plateau (le lift monte à part).
        var lastPose = remote.GetPose();
        float lastRel = last.Y - lastPose.Elevation;
        head.Y = Mathf.Clamp(head.Y - elevation, lastRel - 4f * dt, lastRel + 4f * dt) + elevation;
        remote.LastPoseAt = now;
        left = head + (left - head).LimitLength(MaxHandReach);
        right = head + (right - head).LimitLength(MaxHandReach);
        remote.SetPose(new PoseData(head, yaw, left, right, elevation));
    }

    /// <summary>
    /// Le client demande à être en haut ; l'hôte accorde l'altitude du lift seulement si la tête est sur le pad, hors recharge,
    /// sans coéquipier déjà en haut, et la reprend après la durée maximale ou à l'élimination.
    /// </summary>
    float ValidateElevation(RemotePlayer remote, Vector3 head, float requested)
    {
        double t = Time.GetTicksMsec() / 1000.0;
        var lift = LiftOf(remote.PlayerId);
        float accepted = remote.HostElevation;
        if (lift == null) accepted = 0f;
        else if (accepted > 0f)
        {
            if (requested <= 0.1f || t - remote.LiftHighSince > lift.MaxHighSeconds + 0.6 || IsEliminated(remote.PlayerId))
            {
                accepted = 0f;
                remote.LiftReadyAt = t + lift.CooldownSeconds;
            }
        }
        else if (requested > 0.1f && t >= remote.LiftReadyAt && !IsEliminated(remote.PlayerId) && !TeammateHigh(remote.PlayerId)
                 && new Vector2(head.X - lift.Pad.X, head.Z - lift.Pad.Z).Length() < lift.Pad.Radius + 0.8f)
        {
            accepted = lift.HighElevation;
            remote.LiftHighSince = t;
        }
        remote.HostElevation = accepted;
        return accepted;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void CatchRpc(int ballId, int hand)
    {
        if (!IsHost || !Remotes.TryGetValue(Multiplayer.GetRemoteSenderId(), out var remote)) return;
        double catchNow = Time.GetTicksMsec() / 1000.0;
        if (hand is < 0 or > 1 || catchNow < remote.NextCatchAt || remote.Hurtbox.Eliminated || remote.HeldBall(0) != null || remote.HeldBall(1) != null) return;   // une seule balle par joueur
        remote.NextCatchAt = catchNow + MissedCatchCooldown;   // remis à 0 si la prise réussit
        foreach (var n in GetTree().GetNodesInGroup("balls"))
        {
            if (n is not Ball { IsHeld: false } ball || ball.IsQueuedForDeletion() || ball.NetId != ballId) continue;
            // Compensation de latence simple : la balle de l'hôte a avancé depuis ce que voyait le client (~120 ms), on élargit d'autant.
            float lagSlack = Mathf.Min(ball.LinearVelocity.Length() * 0.12f, 2.0f);
            if (ball.GlobalPosition.DistanceTo(remote.HandNode(hand).GlobalPosition) > CatchReach + lagSlack) return;
            ball.Hold(remote.HandNode(hand), remote.Hurtbox.PlayerId, remote.Hurtbox.Team);
            remote.NextCatchAt = 0;
            GD.Print($"[Net] Balle {ballId} attrapée par la zone {remote.Hurtbox.PlayerId}");
            return;
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void ThrowRpc(int hand, Vector3 velocity, float spin)
    {
        if (!IsHost || !Remotes.TryGetValue(Multiplayer.GetRemoteSenderId(), out var remote)) return;
        var ball = remote.HeldBall(hand);
        if (ball == null || !velocity.IsFinite() || !float.IsFinite(spin)) return;
        ball.Release(velocity.LimitLength(MaxThrowSpeed), Mathf.Clamp(spin, -MaxSpin, MaxSpin));
        remote.NextCatchAt = Time.GetTicksMsec() / 1000.0 + CatchCooldown;
        GD.Print($"[Net] Balle {ball.NetId} lancée par la zone {remote.Hurtbox.PlayerId} à {velocity.Length():0.0} m/s");
    }

    // ---------------------------------------------------------------- hôte -> clients

    readonly Dictionary<int, double> connectedAt = new();

    void HostTick(double delta)
    {
        double clock = Time.GetTicksMsec() / 1000.0;
        foreach (var peerId in Multiplayer.GetPeers())
        {
            if (Remotes.ContainsKey(peerId)) { connectedAt.Remove(peerId); continue; }
            if (!connectedAt.TryGetValue(peerId, out double since)) { connectedAt[peerId] = clock; continue; }
            if (clock - since > HelloTimeout)
            {
                GD.Print($"[Net] Pair {peerId} sans Hello : déconnecté");
                connectedAt.Remove(peerId);
                Multiplayer.MultiplayerPeer.DisconnectPeer(peerId);
            }
        }
        // Le lift se reprend au tick de l'hôte (durée maximale, élimination), même si le client ne dit plus rien.
        foreach (var remote in Remotes.Values)
        {
            var lift = LiftOf(remote.PlayerId);
            if (remote.HostElevation <= 0f || lift == null) continue;
            if (clock - remote.LiftHighSince <= lift.MaxHighSeconds + 0.6 && !IsEliminated(remote.PlayerId)) continue;
            var p = remote.GetPose();
            var down = Vector3.Up * p.Elevation;
            remote.HostElevation = 0f;
            remote.LiftReadyAt = clock + lift.CooldownSeconds;
            remote.SetPose(new PoseData(p.Head - down, p.Yaw, p.Left - down, p.Right - down, 0f));
        }
        if (Multiplayer.GetPeers().Length == 0) return;
        string hud = HudText();
        if (hud != lastHud) { lastHud = hud; Rpc(MethodName.HudRpc, hud); }
        snapshotTimer += delta;
        if (snapshotTimer < SnapshotInterval - 0.002f) return;   // marge : 1/30 en float dépasse 2 ticks de 1/60 et tombait à 20 Hz
        snapshotTimer = 0;
        Rpc(MethodName.SnapshotRpc, BuildSnapshot());
        SnapshotsSent++;
    }

    float[] BuildSnapshot()
    {
        var balls = new List<Ball>();
        foreach (var n in GetTree().GetNodesInGroup("balls"))
            if (n is Ball b && IsInstanceValid(b) && !b.IsQueuedForDeletion()) balls.Add(b);

        var data = new List<float> { PoseProviders.Count, balls.Count };
        foreach (var (id, provider) in PoseProviders)
        {
            var p = provider();
            data.AddRange(new[]
            {
                id, p.Head.X, p.Head.Y, p.Head.Z, p.Yaw, p.Left.X, p.Left.Y, p.Left.Z, p.Right.X, p.Right.Y, p.Right.Z,
                IsEliminated(id) ? 1f : 0f, LivesOf(id), IsBot(id) ? 1f : 0f, p.Elevation,
            });
        }
        foreach (var b in balls)
        {
            var pos = b.GlobalPosition;
            var vel = b.IsHeld ? Vector3.Zero : b.LinearVelocity;
            data.AddRange(new[]
            {
                b.NetId, pos.X, pos.Y, pos.Z, vel.X, vel.Y, vel.Z, b.OwnerTeam, b.OwnerPlayerId, b.Charge, b.IsHeld ? 1f : 0f,
            });
        }
        return data.ToArray();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    void SnapshotRpc(float[] data)
    {
        SnapshotsReceived++;
        Later(() => SnapshotReceived?.Invoke(data));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void HudRpc(string text) => HudReceived?.Invoke(text);

    /// <summary>Décode un snapshot : renvoie les joueurs et les balles. Partagé avec les tests.</summary>
    public static (List<SnapshotPlayer> players, List<SnapshotBall> balls) Decode(float[] d)
    {
        var players = new List<SnapshotPlayer>();
        var balls = new List<SnapshotBall>();
        if (d.Length < 2) return (players, balls);
        int np = (int)d[0], nb = (int)d[1], i = 2;
        if (d.Length < 2 + np * PlayerFloats + nb * BallFloats) return (players, balls);
        for (int k = 0; k < np; k++, i += PlayerFloats)
            players.Add(new SnapshotPlayer((int)d[i],
                new PoseData(new Vector3(d[i + 1], d[i + 2], d[i + 3]), d[i + 4], new Vector3(d[i + 5], d[i + 6], d[i + 7]), new Vector3(d[i + 8], d[i + 9], d[i + 10]), d[i + 14]),
                d[i + 11] > 0.5f, (int)d[i + 12], d[i + 13] > 0.5f));
        for (int k = 0; k < nb; k++, i += BallFloats)
            balls.Add(new SnapshotBall((int)d[i], new Vector3(d[i + 1], d[i + 2], d[i + 3]), new Vector3(d[i + 4], d[i + 5], d[i + 6]),
                (int)d[i + 7], (int)d[i + 8], (int)d[i + 9], d[i + 10] > 0.5f));
        return (players, balls);
    }
}

public record SnapshotPlayer(int Id, PoseData Pose, bool Eliminated, int Lives, bool Bot);

public record SnapshotBall(int NetId, Vector3 Position, Vector3 Velocity, int Team, int OwnerId, int Charge, bool Held);
