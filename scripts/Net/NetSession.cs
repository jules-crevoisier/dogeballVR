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
    public const int MaxPlayers = 8;   // la Tour 4v4 a huit plaques ; la zone demandée au Hello doit exister dans la carte
    const float SnapshotInterval = 1f / 30f;
    const float PoseInterval = 1f / 30f;
    const float MaxMoveSpeed = 11f;   // m/s (esquive PC à 10 m/s) : au-delà, la pose est ramenée (pas de téléportation)
    const double CatchCooldown = 0.2; // entre un lancer et la prise suivante
    const double MissedCatchCooldown = 0.15; // une demande refusée n'en autorise pas une autre avant ce délai
    const double MinPoseInterval = 1.0 / 60.0; // au-delà, les paquets de pose sont ignorés (anti-inondation)
    const double HelloTimeout = 5.0;          // un pair sans Hello est déconnecté
    const int PlayerFloats = 15;
    const float MaxHandReach = 1.0f;   // une main reste près de la tête
    const float MaxSpin = 12f;
    const double MinCrouchOnPad = 0.8;   // le lift exige un accroupi tenu sur le pad (le client en demande 1 s)
    const int BallFloats = 12;

    public NetMode Mode { get; private set; } = NetMode.Offline;
    public bool IsClient => Mode == NetMode.Client;
    public bool IsHost => Mode == NetMode.Host;
    public bool Connected => Multiplayer.MultiplayerPeer is { } p && p.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;
    public int ZoneId { get; set; }
    /// <summary>Lobby PC en moulinet : les joueurs circulent sur toute l'arène, dash (4 m en 0,2 s) et grand saut permis.</summary>
    public bool FreeRoam { get; set; }
    /// <summary>Point d'apparition d'un joueur en jeu libre (retour de KO).</summary>
    public Func<int, Vector3?> SpawnOf { get; set; } = _ => null;
    const float FreeRunCap = 9f, FreeDashCap = 26f;     // course 7,5 m/s + marge ; dash 20 m/s pendant 0,2 s
    const double DashSlack = 0.25, DashRecharge = 1.6;   // fenêtre d'un dash validé (latence comprise) ; recharge d'une charge
    float MoveSpeedCap => FreeRoam ? FreeRunCap : MaxMoveSpeed;
    /// <summary>Carte à tremplins : les vols vont plus haut (sommet à 3 m des pieds) et les pieds peuvent être sur une terrasse.</summary>
    public Maps.MapData? Map { get; set; }
    bool PadMap => FreeRoam && Map?.Pc?.JumpPads.Count > 0;
    float VerticalSpeedCap => FreeRoam ? 5.5f : 4f;
    float HeadCeiling => FreeRoam ? 2.9f : 2.3f;
    const float FlightVerticalCap = 6.5f, FlightCeiling = 5.0f;   // seulement pendant un vol de tremplin ou sur une terrasse
    const double TerraceMaxStay = 8.0, TerraceCooldown = 7.5;   // recharge comptée depuis l'acceptation de l'hôte : le client compte 10 s depuis le lancer, ~2 s avant l'arrivée
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
    readonly Dictionary<int, double> liftReadyAtByZone = new();   // la recharge reste à la zone, pas au joueur (reconnexion)
    /// <summary>Un joueur part (ou est remplacé) : s'il était en haut, la recharge de sa zone démarre.</summary>
    public void LiftPlayerLeft(RemotePlayer remote)
    {
        var lift = LiftOf(remote.PlayerId);
        if (lift != null && remote.HostElevation > 0f) liftReadyAtByZone[remote.PlayerId] = Time.GetTicksMsec() / 1000.0 + lift.CooldownSeconds;
        remote.HostElevation = 0f;
    }

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
        Later(() => RpcId(1, MethodName.PoseRpc, p.Head, p.Yaw, p.Left, p.Right, p.Elevation, p.DashCount));
    }

    public void RequestCatch(int ballId, int hand, bool perfect = false)
    {
        if (Connected) Later(() => RpcId(1, MethodName.CatchRpc, ballId, hand, perfect));
    }

    public void RequestParry(int ballId, int hand, Vector3 normal)
    {
        if (Connected) Later(() => RpcId(1, MethodName.ParryRpc, ballId, hand, normal));
    }

    public void RequestThrow(int hand, Vector3 velocity, float spin, int mode = 0)
    {
        if (Connected) Later(() => RpcId(1, MethodName.ThrowRpc, hand, velocity, spin, mode));
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
    void PoseRpc(Vector3 head, float yaw, Vector3 left, Vector3 right, float elevation, int dashCount)
    {
        if (!IsHost) return;
        if (!Remotes.TryGetValue(Multiplayer.GetRemoteSenderId(), out var remote)) return;
        if (!head.IsFinite() || !left.IsFinite() || !right.IsFinite() || !float.IsFinite(yaw) || !float.IsFinite(elevation)) return;
        // Inondation : paquet ignoré avant toute modification d'état.
        double now = Time.GetTicksMsec() / 1000.0;
        if (remote.LastPoseAt > 0 && now - remote.LastPoseAt < MinPoseInterval) return;
        // Échange de plaques : l'hôte a déjà posé la copie à la place attendue ; les poses d'avant l'échange (ou d'un client qui ignore l'ordre) sont jetées.
        if (now < remote.SwapUntil)
        {
            if (new Vector2(head.X - remote.SwapTarget.X, head.Z - remote.SwapTarget.Z).Length() > 6f) { remote.LastPoseAt = now; return; }
            remote.SwapUntil = 0;
            GD.Print($"[Net] Échange confirmé : la zone {remote.PlayerId} a rejoint sa nouvelle place");
        }
        // Bornes de l'hôte : la tête reste dans la zone, les mains près de la tête (pas de téléportation hors de portée).
        var zone = ZoneRect(remote.PlayerId);
        head = new Vector3(Mathf.Clamp(head.X, zone.Position.X, zone.End.X), head.Y, Mathf.Clamp(head.Z, zone.Position.Y, zone.End.Y));
        // Vitesse maximale : on ramène la tête sur la distance parcourue depuis la dernière pose acceptée (temps réel écoulé).
        var last = remote.TargetPose.Head;
        float cap = MoveSpeedCap;
        if (FreeRoam)
        {
            // Dash : l'hôte tient le compte des charges (2, une toutes les 1,6 s) ; sans charge le signal est ignoré.
            remote.DashBudget = Mathf.Min(2f, remote.DashBudget + (float)((remote.LastPoseAt > 0 ? now - remote.LastPoseAt : 0) / DashRecharge));
            if (remote.LastPoseAt <= 0) remote.LastDashCount = dashCount;   // première pose : on se cale sur le compteur du client
            bool dashNew = dashCount != remote.LastDashCount;   // compteur de dashs : insensible aux paquets perdus
            remote.LastDashCount = dashCount;
            if (dashNew && remote.DashBudget >= 0.6f && !IsEliminated(remote.PlayerId)) { remote.DashBudget -= 1f; remote.DashUntil = now + DashSlack + 0.05; GD.Print($"[Net] Dash validé pour la zone {remote.PlayerId}"); }
            if (now < remote.DashUntil) cap = FreeDashCap;
            else if (now < remote.PushUntil) cap = 14f;   // poussée de 8 m/s : marge sans autoriser de téléportation
            ArmFlight(remote, head, now);
            // Retour de KO : le client est ramené à son point d'apparition, l'hôte accepte ce saut une fois.
            if (IsEliminated(remote.PlayerId)) { remote.TeleportPending = true; remote.TeleportDeadline = 0; }
            else if (remote.TeleportPending)
            {
                // Retour de KO : l'hôte remet la copie au point d'apparition et ignore les poses encore anciennes du client (au plus 1,5 s).
                var spawn = SpawnOf(remote.PlayerId);
                if (spawn is Vector3 sp)
                {
                    if (remote.TeleportDeadline <= 0) { remote.TeleportDeadline = now + 1.5; remote.SetPose(new PoseData(sp + Vector3.Up * 1.6f, yaw, sp + new Vector3(-0.3f, 1.0f, 0), sp + new Vector3(0.3f, 1.0f, 0))); }
                    if (new Vector2(head.X - sp.X, head.Z - sp.Z).Length() > 3f && now < remote.TeleportDeadline) { remote.LastPoseAt = now; return; }
                }
                remote.TeleportPending = false; remote.TeleportDeadline = 0;
            }
        }
        float allowed = cap * (float)Mathf.Clamp(remote.LastPoseAt > 0 ? now - remote.LastPoseAt : 0.1, MinPoseInterval, 0.5);
        var horizontal = new Vector3(head.X - last.X, 0, head.Z - last.Z);
        if (horizontal.Length() > allowed)
        {
            var limited = horizontal.LimitLength(allowed);
            head = new Vector3(last.X + limited.X, head.Y, last.Z + limited.Z);
        }
        // Le lift est jugé sur la tête acceptée (pas sur la tête brute du paquet) : accroupi sur le pad depuis assez longtemps.
        var liftDef = LiftOf(remote.PlayerId);
        bool crouchedOnPad = liftDef != null && head.Y - Mathf.Clamp(elevation, 0f, MaxElevation) < 1.25f
            && new Vector2(head.X - liftDef.Pad.X, head.Z - liftDef.Pad.Z).Length() < liftDef.Pad.Radius + 0.8f;
        remote.CrouchOnPadSince = crouchedOnPad ? (remote.CrouchOnPadSince > 0 ? remote.CrouchOnPadSince : now) : 0;
        elevation = ValidateElevation(remote, head, elevation);
        // Jeu libre : la hauteur de tête est absolue (la terrasse ne la décale pas) ; au-dessus de la fosse elle peut descendre (chute).
        float lowY = FreeRoam ? (Map != null && Map.OverPit(head.X, head.Z, 0.1f) ? -8f : 0.8f) : 0.8f + elevation;
        bool airborne = FreeRoam && PadMap && (Time.GetTicksMsec() / 1000.0 < remote.FlightUntil || elevation > 0f);
        head.Y = Mathf.Clamp(head.Y, lowY, FreeRoam ? (airborne ? FlightCeiling : HeadCeiling) : HeadCeiling + elevation);
        // Vitesse verticale plafonnée : pas de vol ni d'accroupi instantané pour réduire la cible.
        float dt = (float)Mathf.Clamp(remote.LastPoseAt > 0 ? now - remote.LastPoseAt : 0.1, MinPoseInterval, 0.5);
        // La vitesse verticale se mesure par rapport au plateau (le lift monte à part).
        var lastPose = remote.TargetPose;
        float refElevation = FreeRoam ? 0f : elevation;
        float lastRel = last.Y - (FreeRoam ? 0f : lastPose.Elevation);
        float vCap = airborne ? FlightVerticalCap : VerticalSpeedCap;
        head.Y = Mathf.Clamp(head.Y - refElevation, lastRel - vCap * dt, lastRel + vCap * dt) + refElevation;
        remote.LastPoseAt = now;
        left = head + (left - head).LimitLength(MaxHandReach);
        right = head + (right - head).LimitLength(MaxHandReach);
        remote.SetPose(new PoseData(head, yaw, left, right, elevation));
        if (FreeRoam && remote.PendingTackle is { } pt)
        {
            remote.PendingTackle = null;
            if (now <= pt.until && now <= remote.DashUntil + 0.1) Gameplay.MatchController.RequestTackle(remote.PlayerId, pt.victim, pt.dir);
        }
    }

    /// <summary>
    /// Le client demande à être en haut ; l'hôte accorde l'altitude du lift seulement si la tête est sur le pad, hors recharge,
    /// sans coéquipier déjà en haut, et la reprend après la durée maximale ou à l'élimination.
    /// </summary>
    /// <summary>Tremplin (PC) : le vol n'est accordé que tête au-dessus d'un tremplin de son camp, hors recharge ; il relève le plafond de tête 2,2 s.</summary>
    void ArmFlight(RemotePlayer remote, Vector3 head, double now)
    {
        if (!PadMap || head.Y > 2.3f || IsEliminated(remote.PlayerId) || Map?.Pc == null) return;
        int team = Map.Zones.Find(z => z.Id == remote.PlayerId)?.Team ?? -1;
        foreach (var pad in Map.Pc.JumpPads)
        {
            if (Map.Zones.Find(z => z.Id == pad.Zone)?.Team != team) continue;
            if (new Vector2(head.X - pad.X, head.Z - pad.Z).Length() > pad.Radius + 1.0f) continue;
            remote.FlightUntil = now + 2.2; remote.FlightZone = pad.Zone;   // renouvelé à chaque pose au sol près du tremplin : le lancer est toujours couvert
            return;
        }
    }

    float ValidateElevation(RemotePlayer remote, Vector3 head, float requested)
    {
        double t = Time.GetTicksMsec() / 1000.0;
        if (FreeRoam) return ValidateTerrace(remote, head, requested, t);
        var lift = LiftOf(remote.PlayerId);
        float accepted = remote.HostElevation;
        if (lift == null) accepted = 0f;
        else if (accepted > 0f)
        {
            if (requested <= 0.1f || t - remote.LiftHighSince > lift.MaxHighSeconds + 1.0 || IsEliminated(remote.PlayerId))
            {
                accepted = 0f;
                liftReadyAtByZone[remote.PlayerId] = t + lift.CooldownSeconds;
            }
        }
        else if (requested > 0.1f && t >= (liftReadyAtByZone.TryGetValue(remote.PlayerId, out double readyAt) ? readyAt : 0) && remote.CrouchOnPadSince > 0 && t - remote.CrouchOnPadSince >= MinCrouchOnPad && !IsEliminated(remote.PlayerId) && !TeammateHigh(remote.PlayerId)
                 && new Vector2(head.X - lift.Pad.X, head.Z - lift.Pad.Z).Length() < lift.Pad.Radius + 0.8f)
        {
            accepted = lift.HighElevation;
            remote.LiftHighSince = t;
        }
        remote.HostElevation = accepted;
        return accepted;
    }

    /// <summary>Terrasse (PC) : l'altitude n'est accordée que tête au-dessus de la terrasse et assez haut (arrivée par tremplin) ; reprise après 8 s, puis recharge de 10 s.</summary>
    float ValidateTerrace(RemotePlayer remote, Vector3 head, float requested, double t)
    {
        var terrace = Map?.TerraceAt(head.X, head.Z, 0.3f);
        float accepted = remote.HostElevation;
        if (terrace == null || requested <= 0.1f || IsEliminated(remote.PlayerId))
        {
            accepted = 0f;
        }
        else if (accepted > 0f)
        {
            if (t - remote.LiftHighSince > TerraceMaxStay + 1.0) { accepted = 0f; liftReadyAtByZone[remote.PlayerId] = t + TerraceCooldown; }   // reprise : même recharge que le lift
        }
        else if (head.Y >= terrace.Sy + 0.6f && t < remote.FlightUntil + 0.5 && terrace.Zone == remote.FlightZone && t >= (liftReadyAtByZone.TryGetValue(remote.PlayerId, out double readyAt) ? readyAt : 0))
        {
            accepted = terrace.Sy;
            remote.LiftHighSince = t;
            liftReadyAtByZone[remote.PlayerId] = t + TerraceCooldown;
            GD.Print($"[Net] Terrasse {terrace.Id} atteinte par la zone {remote.PlayerId}");
        }
        remote.HostElevation = accepted;
        return accepted;
    }

    // ---------------------------------------------------------------- tacle et poussée

    /// <summary>Le client annonce un tacle (son dash touche la victime) ; l'hôte exige un dash validé et juge le reste (distance, invulnérabilité…).</summary>
    public void RequestTackle(int victimId, Vector2 dir)
    {
        if (Connected) Later(() => RpcId(1, MethodName.TackleRpc, victimId, dir));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void TackleRpc(int victimId, Vector2 dir)
    {
        if (!IsHost || !FreeRoam || !Remotes.TryGetValue(Multiplayer.GetRemoteSenderId(), out var remote) || !dir.IsFinite()) return;
        // Le RPC (fiable) peut devancer la pose (non fiable) : le tacle attend la prochaine pose (dash validé, position à jour), 0,3 s au plus.
        remote.PendingTackle = (victimId, dir, Time.GetTicksMsec() / 1000.0 + 0.3);
    }

    /// <summary>Hôte : pousse un joueur distant (tacle validé). Le plafond de vitesse de sa copie s'élargit 0,45 s.</summary>
    public void SendPush(int victimId, Vector2 dir)
    {
        foreach (var remote in Remotes.Values)
        {
            if (remote.PlayerId != victimId) continue;
            remote.PushUntil = Time.GetTicksMsec() / 1000.0 + 0.3;
            RpcId(remote.PeerId, MethodName.PushRpc, dir);
        }
    }

    /// <summary>Client : l'hôte annonce une poussée subie.</summary>
    public Action<Vector2>? PushReceived { get; set; }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void PushRpc(Vector2 dir)
    {
        if (!IsClient || Multiplayer.GetRemoteSenderId() != 1 || !dir.IsFinite()) return;
        PushReceived?.Invoke(dir);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void CatchRpc(int ballId, int hand, bool perfect)
    {
        if (!IsHost || !Remotes.TryGetValue(Multiplayer.GetRemoteSenderId(), out var remote)) return;
        double catchNow = Time.GetTicksMsec() / 1000.0;
        if (hand is < 0 or > 1 || catchNow < remote.NextCatchAt || remote.Hurtbox.Eliminated || remote.HeldBall(0) != null || remote.HeldBall(1) != null) return;   // une seule balle par joueur
        remote.NextCatchAt = catchNow + MissedCatchCooldown;   // remis à 0 si la prise réussit
        foreach (var n in GetTree().GetNodesInGroup("balls"))
        {
            if (n is not Ball { IsHeld: false } ball || ball.IsQueuedForDeletion() || ball.NetId != ballId) continue;
            // Compensation de latence simple : la balle de l'hôte a avancé depuis ce que voyait le client (~120 ms), on élargit d'autant.
            if (!Assist.HostAccepts(ball.GlobalPosition, remote.HandNode(hand).GlobalPosition, ball.LinearVelocity.Length())) return;
            if (ball.IsPass && ball.OwnerTeam != remote.Hurtbox.Team) return;   // une passe ne s'intercepte pas (phase 2)
            // Attrape parfaite : le client la déclare (appui dans les 0,18 s avant l'arrivée) ; l'hôte n'accorde le palier que sur un tir adverse rapide.
            // + la balle devait être encore hors de portée 0,3 s plus tôt (0,18 s de fenêtre + latence) et un perfect au plus toutes les 2 s.
            bool perfectOk = perfect && ball.OwnerTeam >= 0 && ball.OwnerTeam != remote.Hurtbox.Team && ball.LinearVelocity.Length() >= Ball.HitMinSpeed
                && ball.ThrowMode == 0 && Assist.PerfectAllowed(ball.LinearVelocity.Length(), ball.SpeedCap) && catchNow - remote.LastPerfectAt > 1.0
                && ball.GlobalPosition.DistanceTo(remote.HandNode(hand).GlobalPosition) + 0.01f < (ball.GlobalPosition - ball.LinearVelocity * 0.3f).DistanceTo(remote.HandNode(hand).GlobalPosition);
            if (perfectOk) remote.LastPerfectAt = catchNow;
            ball.Hold(remote.HandNode(hand), remote.Hurtbox.PlayerId, remote.Hurtbox.Team, perfectOk);
            remote.NextCatchAt = 0;
            if (perfectOk) GD.Print($"[Net] Attrape parfaite de la zone {remote.Hurtbox.PlayerId}");
            GD.Print($"[Net] Balle {ballId} attrapée par la zone {remote.Hurtbox.PlayerId}");
            return;
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void ParryRpc(int ballId, int hand, Vector3 normal)
    {
        if (!IsHost || !Remotes.TryGetValue(Multiplayer.GetRemoteSenderId(), out var remote)) return;
        double now = Time.GetTicksMsec() / 1000.0;
        if (hand is < 0 or > 1 || !normal.IsFinite() || normal.LengthSquared() < 0.01f || now - remote.LastParryAt < 1.4 || remote.Hurtbox.Eliminated
            || remote.HeldBall(0) != null || remote.HeldBall(1) != null) return;
        foreach (var n in GetTree().GetNodesInGroup("balls"))
        {
            if (n is not Ball { IsHeld: false } ball || ball.IsQueuedForDeletion() || ball.NetId != ballId) continue;
            if (ball.OwnerTeam < 0 || ball.OwnerTeam == remote.Hurtbox.Team || ball.LinearVelocity.Length() < Ball.HitMinSpeed || ball.LinearVelocity.Dot(remote.Hurtbox.GlobalPosition - ball.GlobalPosition) <= 0f || !Assist.HostAccepts(ball.GlobalPosition, remote.HandNode(hand).GlobalPosition, ball.LinearVelocity.Length())) return;
            if (!ball.Deflect(normal)) return;   // chargée, passe, tenue : pas de parade
            remote.LastParryAt = now;
            GD.Print($"[Net] Balle {ballId} parée par la zone {remote.Hurtbox.PlayerId}");
            return;
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void ThrowRpc(int hand, Vector3 velocity, float spin, int mode)
    {
        if (!IsHost || !Remotes.TryGetValue(Multiplayer.GetRemoteSenderId(), out var remote)) return;
        var ball = remote.HeldBall(hand);
        if (ball == null || !velocity.IsFinite() || !float.IsFinite(spin)) return;
        mode = Mathf.Clamp(mode, 0, 2);
        ball.Release(Assist.PlanFor(ball, velocity, mode), Mathf.Clamp(spin, -MaxSpin, MaxSpin), mode);   // cloche et passe : vitesse plafonnée à 24 m/s
        remote.NextCatchAt = Time.GetTicksMsec() / 1000.0 + CatchCooldown;
        GD.Print($"[Net] Balle {ball.NetId} lancée par la zone {remote.Hurtbox.PlayerId} à {velocity.Length():0.0} m/s" + (mode == Ball.ModePass ? " (passe)" : mode == Ball.ModeLob ? " (cloche)" : spin != 0 ? " (effet)" : ""));
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
        // Terrasse (PC) : reprise à l'expiration ou à l'élimination même si le client ne dit plus rien.
        if (FreeRoam)
            foreach (var remote in Remotes.Values)
            {
                if (remote.HostElevation <= 0f) continue;
                if (clock - remote.LiftHighSince <= TerraceMaxStay + 1.0 && !IsEliminated(remote.PlayerId)) continue;
                var tp = remote.GetPose();
                remote.HostElevation = 0f;
                remote.SetPose(new PoseData(tp.Head, tp.Yaw, tp.Left, tp.Right, 0f, tp.DashCount));
            }
        // Le lift se reprend au tick de l'hôte (durée maximale, élimination), même si le client ne dit plus rien.
        foreach (var remote in Remotes.Values)
        {
            var lift = LiftOf(remote.PlayerId);
            if (remote.HostElevation <= 0f || lift == null) continue;
            if (clock - remote.LiftHighSince <= lift.MaxHighSeconds + 1.0 && !IsEliminated(remote.PlayerId)) continue;
            var p = remote.GetPose();
            var down = Vector3.Up * p.Elevation;
            remote.HostElevation = 0f;
            liftReadyAtByZone[remote.PlayerId] = clock + lift.CooldownSeconds;
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
        foreach (var n in GetTree().GetNodesInGroup("exploded_balls"))   // bombes explosées : type 3 pendant 0,15 s
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
                b.NetId, pos.X, pos.Y, pos.Z, vel.X, vel.Y, vel.Z, b.OwnerTeam, b.OwnerPlayerId, b.Charge, b.IsHeld ? 1f : (b.IsPass ? 2f : 0f), b.Kind,   // 2 = passe en vol ; Kind : 0 normale, 1 bombe, 2 rebond
            });
        }
        data.Add(Maps.Rotor.Current?.Angle ?? -1f);   // dernier flottant : angle du rotor (-1 = pas de rotor)
        return data.ToArray();
    }

    /// <summary>Angle du rotor porté par un snapshot, ou -1.</summary>
    public static float RotorAngleOf(float[] d)
    {
        if (d.Length < 2) return -1f;
        int end = 2 + (int)d[0] * PlayerFloats + (int)d[1] * BallFloats;
        return d.Length > end ? d[end] : -1f;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    void SnapshotRpc(float[] data)
    {
        SnapshotsReceived++;
        Later(() => SnapshotReceived?.Invoke(data));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void HudRpc(string text) => HudReceived?.Invoke(text);

    /// <summary>Nouvelle table des plaques (indexée par joueur) : l'hôte l'envoie à tous et accepte le saut des joueurs déplacés.</summary>
    public event Action<int[]>? PlatesReceived;
    public void BroadcastPlates(int[] plateOf, Dictionary<int, Vector3> targets)
    {
        if (!IsHost) return;
        double now = Time.GetTicksMsec() / 1000.0;
        foreach (var r in Remotes.Values)
        {
            if (!targets.TryGetValue(r.PlayerId, out var target)) continue;
            var old = r.TargetPose;
            var shift = target - old.Head;
            shift.Y = 0;
            r.SnapPose(new PoseData(old.Head + shift, old.Yaw, old.Left + shift, old.Right + shift, old.Elevation, old.DashCount));
            r.SwapTarget = target;
            r.SwapUntil = now + 1.5;
        }
        Rpc(MethodName.PlatesRpc, plateOf);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void PlatesRpc(int[] plateOf) => PlatesReceived?.Invoke(plateOf);

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
                (int)d[i + 7], (int)d[i + 8], (int)d[i + 9], d[i + 10] > 0.5f && d[i + 10] < 1.5f, d[i + 10] > 1.5f, (int)d[i + 11]));
        return (players, balls);
    }
}

public record SnapshotPlayer(int Id, PoseData Pose, bool Eliminated, int Lives, bool Bot);

public record SnapshotBall(int NetId, Vector3 Position, Vector3 Velocity, int Team, int OwnerId, int Charge, bool Held, bool Pass = false, int Kind = 0);
