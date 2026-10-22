using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using DogeballVR.Maps;
using DogeballVR.Net;

namespace DogeballVR.Online;

public enum LobbyState { Idle, Searching, Connecting, InRoom, InMatch, Reconnecting }

/// <summary>Un joueur de la salle (avant et pendant la partie).</summary>
public class LobbyMember
{
    public int PeerId;
    public string Name = "";
    public string Token = "";
    public int Team;
    public bool Ready;
    public bool Vr;
    /// <summary>Zone tenue en partie (-1 : pas en partie).</summary>
    public int Zone = -1;
    public bool Host => PeerId == 1;
}

/// <summary>
/// Salle d'avant-match, persistante (nœud /root/OnlineLobby, il survit au rechargement de la scène de jeu).
/// L'hôte ouvre la salle (ENet en LAN ou par IP, Steam si disponible), les joueurs entrent, choisissent leur équipe et se
/// déclarent prêts ; l'hôte lance : chacun recharge la scène de jeu avec sa zone (<see cref="GameLaunch"/>), les places vides
/// sont tenues par des bots. On peut entrer en cours de partie (à la place d'un bot) et revenir après une coupure : le jeton
/// d'identité rend la même zone, avec ses vies et son score.
/// Le menu appelle <see cref="QuickMatch"/>, <see cref="CreatePrivate"/>, <see cref="JoinByCode"/>, <see cref="HostLan"/>,
/// <see cref="JoinAddress"/> puis affiche <see cref="LobbyScreen"/>.
/// </summary>
public partial class OnlineLobby : Node
{
    public static OnlineLobby? Instance { get; private set; }

    const double JoinRequestTimeout = 10.0, ConnectTimeout = 10.0, ReconnectWindow = 30.0, ReconnectEvery = 3.0;
    const int MaxLobbyMessagesPerSecond = 8;
    static readonly string[] MapChoices = { "croix_neon", "fosse_croisee", "tour_4v4", "neon_court", "la_fosse" };

    public LobbyState State { get; private set; } = LobbyState.Idle;
    public bool IsHost { get; private set; }
    public string Map { get; private set; } = "croix_neon";
    /// <summary>Code à donner aux amis (IP:port encodé ou lobby Steam), vide tant qu'inconnu.</summary>
    public string Code { get; private set; } = "";
    /// <summary>Vrai si le code marche depuis Internet (lobby Steam, ou port ouvert par UPnP) ; sinon il ne vaut que sur le réseau local.</summary>
    public bool CodeIsInternet { get; private set; }
    /// <summary>Dernier message d'état ou d'erreur, pour l'écran du lobby et le menu.</summary>
    public string Status { get; private set; } = "";
    /// <summary>Après une partie : secondes avant la revanche automatique (0 = aucune). Annulée dès que quelqu'un n'est plus prêt.</summary>
    public double RematchIn { get; private set; }
    public const double RematchSeconds = 10.0;
    /// <summary>Partie rapide : secondes avant le lancement automatique, les places libres étant tenues par des bots (0 = aucun).
    /// Le compte à rebours s'arrête tant qu'un joueur n'est pas prêt.</summary>
    public double QuickStartIn { get; private set; }
    /// <summary>Attente d'une salle de partie rapide seule ; un nouveau venu remet au moins <see cref="QuickJoinGrace"/> ; salle pleine : <see cref="QuickFullSeconds"/>.</summary>
    public const double QuickFillSeconds = 20.0, QuickJoinGrace = 8.0, QuickFullSeconds = 4.0;
    public IReadOnlyList<LobbyMember> Members => members.Values.OrderBy(m => m.PeerId == 1 ? 0 : 1).ThenBy(m => m.PeerId).ToList();
    public int MaxPlayers => Math.Max(2, MapZoneCount(Map));
    /// <summary>Identifiant aléatoire de la salle (annonce LAN) : deux salles ouvertes en même temps par « partie rapide » fusionnent vers la plus petite.</summary>
    readonly string roomId = $"{new RandomNumberGenerator().Randi():x8}";
    bool quickHosted;
    double searchFor;
    readonly Dictionary<int, (double second, int count)> rate = new();
    public LanDiscovery Lan { get; private set; } = null!;
    public static IReadOnlyList<string> Maps => MapChoices;

    public event Action? Changed;
    /// <summary>Échec définitif (connexion impossible, refus, hôte parti) : le menu reprend la main avec ce message.</summary>
    public event Action<string>? Failed;

    readonly Dictionary<int, LobbyMember> members = new();
    readonly Dictionary<int, double> pendingPeers = new();      // hôte : pair connecté sans demande d'entrée
    readonly Dictionary<int, int> matchZones = new();           // hôte : zone attribuée au lancement, par pair
    int port = NetSession.DefaultPort;
    string address = "";
    double stateClock, reconnectClock, nextReconnect;
    int myTeam;
    bool autoReady;
    int autoStart;
    Upnp? upnp;
    bool announceOnLan;   // salle ENet visible des parties rapides du réseau local (pas une salle privée)
    /// <summary>Salle ENet annoncée sur le réseau local (Partie rapide la trouve) ; faux pour une salle privée.</summary>
    public bool ListedOnLan => announceOnLan;
    int upnpTicket;   // salle pour laquelle le port est demandé : une ouverture qui aboutit après la fermeture est annulée

    // ------------------------------------------------------------------ création

    /// <summary>Crée le nœud persistant s'il n'existe pas ; <paramref name="then"/> est appelé une fois qu'il est dans l'arbre.</summary>
    public static void Ensure(SceneTree tree, Action<OnlineLobby>? then = null)
    {
        if (Instance != null && IsInstanceValid(Instance) && Instance.IsInsideTree()) { then?.Invoke(Instance); return; }
        // Déjà en cours de création (ajout différé) : un seul nœud, sinon les RPC visent deux salles.
        var lobby = creating != null && IsInstanceValid(creating) ? creating : null;
        if (lobby == null)
        {
            lobby = creating = new OnlineLobby { Name = "OnlineLobby" };
            tree.Root.CallDeferred(Node.MethodName.AddChild, lobby);
        }
        if (then != null) lobby.Ready += () => then(lobby);
    }

    static OnlineLobby? creating;

    public override void _EnterTree()
    {
        Instance = this;
        if (creating == this) creating = null;
    }

    public override void _Ready()
    {
        Lan = new LanDiscovery { Name = "Lan" };
        AddChild(Lan);
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
        var args = Tools.DevFlags.Allowed ? OS.GetCmdlineUserArgs() : Array.Empty<string>();   // --auto-ready, --autostart= : tests CI
        autoReady = Array.IndexOf(args, "--auto-ready") >= 0 || IsVr;   // pas d'écran de salle dans le casque : un joueur VR est prêt d'office
        NetHandshake.Install(Multiplayer);
        NetHandshake.Rejected += reason => { if (!IsHost) Fail(reason); };
        foreach (var a in args)
            if (a.StartsWith("--autostart=") && int.TryParse(a["--autostart=".Length..], out int n)) autoStart = n;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) AnnounceHostClosing();
    }

    /// <summary>Hôte qui ferme : les joueurs reviennent tout de suite au menu, sans attendre 45 s de reconnexion inutile.</summary>
    internal void AnnounceHostClosing()
    {
        if (!IsHost || State is not (LobbyState.InRoom or LobbyState.InMatch)) return;
        foreach (var m in members.Values) if (!m.Host) RpcId(m.PeerId, MethodName.HostClosingRpc);
        if (Multiplayer.MultiplayerPeer is ENetMultiplayerPeer enet) enet.Host?.Flush();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void HostClosingRpc()
    {
        GD.Print("[Lobby] L'hôte a fermé la partie");
        Fail("L'hôte a quitté la partie");
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
        ClosePortMapping();
        // Fermeture du jeu (la salle vit sous /root jusqu'au bout) : on lâche le transport et la poignée de main,
        // sinon le moteur trouve encore des références C# vers eux à l'arrêt et peut planter en les libérant.
        if (IsInsideTree() && Multiplayer.MultiplayerPeer is { } peer and not OfflineMultiplayerPeer) { peer.Close(); Multiplayer.MultiplayerPeer = null; }
        NetHandshake.Release();
    }

    /// <summary>Options de test de la ligne de commande (CI) : <c>--lobby-host</c>, <c>--lobby-join=ADRESSE</c>, <c>--lobby-quick</c>,
    /// avec <c>--auto-ready</c> et <c>--autostart=N</c> (l'hôte lance dès que N joueurs sont prêts). Vrai si une option a été trouvée.</summary>
    public static bool RunCommandLine(SceneTree tree)
    {
        var cfg = NetConfig.Parse();
        string? join = null;
        bool host = false, quick = false;
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a == "--lobby-host") host = true;
            else if (a == "--lobby-quick") quick = true;
            else if (a.StartsWith("--lobby-join=")) join = a["--lobby-join=".Length..];
        }
        ulong steamLobby = SteamBridge.PendingLaunchLobby();
        if (steamLobby != 0) { Ensure(tree, lobby => lobby.JoinSteamLobby(steamLobby)); return true; }
        if (!host && !quick && join == null) return false;
        Ensure(tree, lobby =>
        {
            if (host) lobby.HostLan(cfg.Port, cfg.Map);
            else if (quick) lobby.QuickMatch();
            else lobby.JoinAddress(join!, cfg.Port);
        });
        return true;
    }

    // ------------------------------------------------------------------ API du menu

    /// <summary>Partie rapide : une salle ouverte sur le réseau local (ou Steam), sinon on en ouvre une.</summary>
    public void QuickMatch()
    {
        Reset();
        if (SteamBridge.Available) { SteamBridge.QuickMatch(this); return; }
        if (Lan.Listen() != Error.Ok) { HostLan(NetSession.DefaultPort, Map); if (State == LobbyState.InRoom) BeginQuickRoom(); return; }
        searchFor = 1.2 + new RandomNumberGenerator().Randf() * 1.6;   // durée aléatoire : deux joueurs lancés ensemble n'ouvrent pas deux salles
        SetState(LobbyState.Searching, "Recherche d'une partie sur le réseau local…");
    }

    /// <summary>Salle privée : invitation par Steam si disponible, sinon par code (adresse IP). Sans Steam, elle n'est pas
    /// annoncée sur le réseau local : seul qui a le code y entre (Partie rapide ne la trouve pas).</summary>
    public void CreatePrivate(string? map = null)
    {
        Reset();
        if (SteamBridge.Available) { SteamBridge.Host(this, publicLobby: false, map ?? Map); return; }
        HostLan(NetSession.DefaultPort, map ?? Map, announce: false);
    }

    public void JoinByCode(string code)
    {
        Reset();
        if (!JoinCode.TryParse(code, out string ip, out int codePort, out ulong steamLobby)) { Fail("Code invalide"); return; }
        if (steamLobby != 0)
        {
            if (!SteamBridge.Available) { Fail("Ce code demande Steam : lancez le jeu depuis Steam"); return; }
            SteamBridge.Join(this, steamLobby);
            return;
        }
        JoinAddress(ip, codePort);
    }

    /// <summary>
    /// Ouvre une salle ENet (réseau local, ou Internet si le port est ouvert). <paramref name="announce"/> : visible des
    /// parties rapides du réseau local. UPnP seulement si le joueur l'a activé dans Options (jamais en mode headless).
    /// </summary>
    public void HostLan(int listenPort = NetSession.DefaultPort, string? map = null, bool announce = true)
    {
        Reset();
        var peer = new ENetMultiplayerPeer();
        var err = peer.CreateServer(listenPort, NetSession.MaxPlayers - 1);
        if (err != Error.Ok) { Fail($"Impossible d'ouvrir la partie (port {listenPort} : {err})"); return; }
        port = listenPort;
        OpenRoomAsHost(peer, map ?? Map);
        Code = JoinCode.FromAddress(LocalIPv4(), port);
        CodeIsInternet = false;
        announceOnLan = announce;
        Lan.Announcement = announce ? Announcement : null;
        if (UI.GameSettings.Upnp && DisplayServer.GetName() != "headless" && Array.IndexOf(OS.GetCmdlineUserArgs(), "--no-upnp") < 0) OpenPortMapping(port);
        GD.Print($"[Lobby] Salle ouverte sur le port {port}, code {Code}");
    }

    /// <summary>Entre dans la salle d'un hôte ENet.</summary>
    public void JoinAddress(string ip, int joinPort = NetSession.DefaultPort)
    {
        if (State != LobbyState.Reconnecting) Reset();
        var peer = new ENetMultiplayerPeer();
        var err = peer.CreateClient(ip, joinPort);
        if (err != Error.Ok) { Fail($"Connexion impossible : {err}"); return; }
        address = ip; port = joinPort;
        SteamBridge.Target = 0;
        Multiplayer.MultiplayerPeer = peer;
        NetSession.RelaxTimeout(peer, 1);
        IsHost = false;
        if (State != LobbyState.Reconnecting) SetState(LobbyState.Connecting, $"Connexion à {ip}:{joinPort}…");
    }

    /// <summary>Utilisé par la couche Steam : la connexion est ouverte, la salle aussi.</summary>
    internal void OpenRoomAsHost(MultiplayerPeer peer, string map)
    {
        Multiplayer.MultiplayerPeer = peer;
        IsHost = true;
        Map = MapChoices.Contains(map) ? map : Map;
        members.Clear();
        members[1] = new LobbyMember { PeerId = 1, Name = Identity.Name, Token = Identity.Token, Team = 0, Ready = true, Vr = IsVr };
        SetState(LobbyState.InRoom, "Salle ouverte : en attente de joueurs");
        BroadcastRoster();
    }

    /// <summary>Utilisé par la couche Steam : la connexion client est lancée vers l'hôte du lobby.</summary>
    internal void ConnectAsClient(MultiplayerPeer peer, string description)
    {
        Multiplayer.MultiplayerPeer = peer;
        IsHost = false;
        if (State != LobbyState.Reconnecting) SetState(LobbyState.Connecting, description);
    }

    /// <summary>Salle ouverte par « Partie rapide » : elle se lance seule au bout de <see cref="QuickFillSeconds"/>, avec des bots aux places libres.</summary>
    internal void BeginQuickRoom()
    {
        quickHosted = true;
        QuickStartIn = QuickFillSeconds;
        GD.Print($"[Lobby] Partie rapide : lancement dans {QuickFillSeconds} s, avec des bots si personne ne vient");
        BroadcastRoster();
    }

    internal void SetCode(string code, bool internet = true) { Code = code; CodeIsInternet = internet; Changed?.Invoke(); }

    /// <summary>Utilisé par la couche Steam : attente d'une réponse de Steam (recherche, création, entrée dans un salon).</summary>
    internal void SetSearching(string status) => SetState(LobbyState.Connecting, status);

    /// <summary>Utilisé par la couche Steam : échec définitif.</summary>
    internal void FailExternal(string reason) => Fail(reason);

    /// <summary>Invitation Steam acceptée, « Rejoindre la partie », ou code « S… » : entre dans le salon Steam.</summary>
    public void JoinSteamLobby(ulong lobbyId)
    {
        // Invitation acceptée pendant une salle ou une partie : on la quitte proprement (les autres sont prévenus),
        // et la scène de jeu laisse la place à l'écran de salle pendant que l'on rejoint l'ami.
        bool inGame = GetTree().CurrentScene?.GetNodeOrNull("NetSession") != null;
        if (State != LobbyState.Idle) Leave();
        else Reset();
        if (!SteamBridge.Available) { Fail("Steam n'est pas lancé"); return; }
        if (inGame)
        {
            GameLaunch.ShowLobby = true;
            GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
        }
        SteamBridge.Join(this, lobbyId);
    }

    public void SetReady(bool ready)
    {
        if (IsHost) { if (members.TryGetValue(1, out var me)) me.Ready = ready; BroadcastRoster(); }
        else if (State == LobbyState.InRoom) RpcId(1, MethodName.SetReadyRpc, ready);
    }

    public void SetTeam(int team)
    {
        team = Mathf.Clamp(team, 0, 1);
        myTeam = team;
        if (IsHost) { if (members.TryGetValue(1, out var me)) { me.Team = team; BroadcastRoster(); } }
        else if (State == LobbyState.InRoom) RpcId(1, MethodName.SetTeamRpc, team);
    }

    /// <summary>Hôte : choisit la carte (la salle ne peut pas dépasser son nombre de zones).</summary>
    public void SetMap(string map)
    {
        if (!IsHost || State != LobbyState.InRoom || !MapChoices.Contains(map)) return;
        if (members.Count > MapZoneCount(map)) { Status = "Trop de joueurs pour cette carte"; Changed?.Invoke(); return; }
        Map = map;
        BroadcastRoster();
    }

    /// <summary>Format de la partie : celui du PC en ligne (façon Knockout City), sauf si un joueur VR est dans la salle (5 manches de 2 min d'EVA).
    /// Hôte et clients le déduisent de la même liste de joueurs.</summary>
    public bool OnlineFormat => !members.Values.Any(m => m.Vr);

    public bool CanStart => IsHost && State == LobbyState.InRoom && members.Values.All(m => m.Ready);

    /// <summary>Hôte : lance la partie. L'hôte recharge la scène d'abord ; les autres reçoivent leur zone quand il est prêt (<see cref="HostMatchReady"/>).</summary>
    public void StartMatch()
    {
        if (!IsHost || State != LobbyState.InRoom) return;
        RematchIn = 0;
        QuickStartIn = 0;
        var map = LoadMap(Map);
        if (map == null) { Status = "Carte introuvable"; Changed?.Invoke(); return; }
        matchZones.Clear();
        var free = new[] { map.Zones.Where(z => z.Team == 0).Select(z => z.Id).ToList(), map.Zones.Where(z => z.Team == 1).Select(z => z.Id).ToList() };
        foreach (var m in Members)
        {
            int team = m.Team;
            if (free[team].Count == 0) team = 1 - team;   // équipe pleine : l'autre
            if (free[team].Count == 0) continue;
            matchZones[m.PeerId] = free[team][0];
            free[team].RemoveAt(0);
        }
        var cfg = BaseConfig();
        cfg.Mode = NetMode.Host;
        cfg.Map = Map;
        cfg.HostZone = matchZones.TryGetValue(1, out int hz) ? hz : 0;
        cfg.Preconnected = true;
        cfg.OnlineFormat = OnlineFormat;
        GD.Print($"[Lobby] Lancement : {Map}, {members.Count} joueur(s), format {(OnlineFormat ? "en ligne" : "VR")}, zones {string.Join(", ", matchZones.Select(kv => $"{kv.Key}->{kv.Value}"))}");
        if (announceOnLan) Lan.Announcement = Announcement;
        SetState(LobbyState.InMatch, "Partie en cours");
        BroadcastRoster();
        SteamBridge.OnMatchStarted(this);
        RelaxTimeouts();
        GameLaunch.Set(cfg);
        GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
    }

    /// <summary>Avant un rechargement de scène (lancement, retour à la salle) : délais longs, le chargement peut figer la machine.</summary>
    void RelaxTimeouts()
    {
        if (IsHost) foreach (int p in Multiplayer.GetPeers()) NetSession.RelaxTimeout(Multiplayer.MultiplayerPeer, p);
        else NetSession.RelaxTimeout(Multiplayer.MultiplayerPeer, 1);
    }

    /// <summary>Hôte : la scène de jeu est construite et la session réseau écoute ; les joueurs de la salle peuvent entrer.</summary>
    public void HostMatchReady()
    {
        if (!IsHost || State != LobbyState.InMatch) return;
        foreach (var (peer, zone) in matchZones)
            if (peer != 1 && members.ContainsKey(peer))
            {
                NetSession.Current?.ReserveForLoading(zone, peer);   // un joueur qui entre pendant ce chargement ne prend pas sa place
                RpcId(peer, MethodName.StartRpc, Map, zone);
            }
    }

    /// <summary>
    /// Hôte, fin de partie : tout le monde revient dans la salle (mêmes joueurs, mêmes équipes, toujours prêts).
    /// La revanche part seule au bout de <see cref="RematchSeconds"/> si personne ne se déclare « pas prêt » entre-temps.
    /// </summary>
    public void ReturnToLobby()
    {
        if (!IsHost || State != LobbyState.InMatch) return;
        foreach (var m in members.Values) if (!m.Host) RpcId(m.PeerId, MethodName.ReturnToLobbyRpc);
        SetState(LobbyState.InRoom, "De retour dans la salle");
        RematchIn = RematchSeconds;
        GD.Print("[Lobby] Retour à la salle : revanche dans " + RematchSeconds + " s si tout le monde reste prêt");
        BroadcastRoster();
        RelaxTimeouts();
        GameLaunch.ShowLobby = true;
        ReloadQuietly(0.5);
    }

    /// <summary>
    /// Fin de partie : la scène de jeu se fige (plus rien n'est envoyé), puis se recharge un peu plus tard.
    /// Les paquets de jeu encore en route trouvent ainsi la session réseau en place au lieu d'un nœud disparu.
    /// </summary>
    void ReloadQuietly(double delay)
    {
        if (GetTree().CurrentScene is { } scene) scene.ProcessMode = ProcessModeEnum.Disabled;
        GetTree().CreateTimer(delay).Timeout += () => GetTree().ReloadCurrentScene();
    }

    /// <summary>Hôte : arrête le compte à rebours de la revanche (on attend, on change de carte…).</summary>
    public void CancelRematch()
    {
        if (!IsHost || RematchIn <= 0) return;
        RematchIn = 0;
        BroadcastRoster();
    }

    /// <summary>Quitte la salle (ou la partie) et ferme la connexion.</summary>
    public void Leave()
    {
        AnnounceHostClosing();
        ReconnectSecret.Clear();
        bool wasConnected = State is LobbyState.InRoom or LobbyState.InMatch or LobbyState.Reconnecting;
        Reset();
        SetState(LobbyState.Idle, "");
        if (wasConnected) GD.Print("[Lobby] Salle quittée");
    }

    // ------------------------------------------------------------------ partie : reconnexion et entrée tardive

    /// <summary>Client en partie : la connexion à l'hôte est perdue. On retente pendant 45 s, puis retour au menu.</summary>
    public void OnMatchConnectionLost()
    {
        if (IsHost || State is not (LobbyState.InMatch or LobbyState.InRoom)) return;
        GD.Print("[Lobby] Connexion perdue : tentative de reconnexion");
        Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
        reconnectClock = 0;
        nextReconnect = 1.0;
        SetState(LobbyState.Reconnecting, "Connexion perdue : reconnexion…");
    }

    /// <summary>Client : l'hôte a attribué une autre zone que celle chargée (deux entrées simultanées) : on recharge avec la bonne.</summary>
    public void OnWelcomedZone(int zone, int loadedZone)
    {
        if (IsHost || zone == loadedZone) return;
        GD.Print($"[Lobby] L'hôte attribue la zone {zone} (chargée : {loadedZone}) : rechargement");
        LaunchClient(Map, zone);
    }

    /// <summary>Client : l'hôte refuse l'entrée en partie.</summary>
    public void OnMatchRefused(string reason)
    {
        if (IsHost) return;
        Fail(reason);
    }

    // ------------------------------------------------------------------ boucle

    // Test (CI) : `--quit-file=CHEMIN` ferme proprement le jeu dès que ce fichier existe (fin des clients du test).
    static readonly string? quitFile = Tools.DevFlags.Value("--quit-file=");
    double quitCheck;

    public override void _Process(double delta)
    {
        stateClock += delta;
        RenewPortMapping(delta);
        if (quitFile != null && (quitCheck += delta) > 0.5)
        {
            quitCheck = 0;
            if (System.IO.File.Exists(quitFile)) { GD.Print("[Lobby] Fin du test : fermeture"); GetTree().Quit(); }
        }
        switch (State)
        {
            case LobbyState.Searching:
                var room = Lan.Rooms.Values.Where(r => r.Protocol == NetSession.ProtocolVersion && r.Players < r.MaxPlayers).OrderByDescending(r => r.Players).FirstOrDefault();
                if (room != null) { Lan.StopListening(); GD.Print($"[Lobby] Partie trouvée : {room.Name} ({room.Address}:{room.GamePort})"); JoinAddress(room.Address, room.GamePort); }
                else if (stateClock > searchFor)
                {
                    GD.Print("[Lobby] Aucune partie trouvée : ouverture d'une salle");
                    HostLan(NetSession.DefaultPort, Map);
                    if (State == LobbyState.InRoom) { BeginQuickRoom(); Lan.Listen(); }   // on continue d'écouter : fusion si une autre salle s'ouvre en même temps
                }
                break;
            case LobbyState.InRoom when IsHost && quickHosted && members.Count == 1:
                // Deux « parties rapides » simultanées ont chacune ouvert une salle : la salle seule rejoint celle d'identifiant plus petit.
                var other = Lan.Rooms.Values.Where(r => r.Protocol == NetSession.ProtocolVersion && r.Players < r.MaxPlayers && !r.InMatch && string.CompareOrdinal(r.Id, roomId) < 0).FirstOrDefault();
                if (other != null) { GD.Print($"[Lobby] Autre salle ouverte en même temps ({other.Address}:{other.GamePort}) : on la rejoint"); var (a, gp) = (other.Address, other.GamePort); Reset(); JoinAddress(a, gp); }
                break;
            case LobbyState.Connecting:
                if (stateClock > ConnectTimeout * (SteamBridge.Target != 0 || Multiplayer.MultiplayerPeer is OfflineMultiplayerPeer ? 2 : 1)) Fail("L'hôte ne répond pas");
                break;
            case LobbyState.Reconnecting:
                reconnectClock += delta;
                if (reconnectClock > ReconnectWindow) { Fail("Reconnexion impossible : l'hôte a quitté la partie"); break; }
                if (reconnectClock >= nextReconnect && Multiplayer.MultiplayerPeer is OfflineMultiplayerPeer)
                {
                    nextReconnect = reconnectClock + ReconnectEvery;
                    Status = $"Connexion perdue : reconnexion… ({(int)(ReconnectWindow - reconnectClock)} s)";
                    Changed?.Invoke();
                    if (SteamBridge.Target != 0) SteamBridge.Reconnect(this);
                    else JoinAddress(address, port);
                }
                break;
        }
        if (IsHost)
        {
            double now = Time.GetTicksMsec() / 1000.0;
            foreach (var (peer, since) in new Dictionary<int, double>(pendingPeers))
                if (now - since > JoinRequestTimeout) { pendingPeers.Remove(peer); Multiplayer.MultiplayerPeer.DisconnectPeer(peer); }
            if (autoStart > 0 && State == LobbyState.InRoom && members.Count >= autoStart + 1 && CanStart) { autoStart = 0; StartMatch(); }   // une fois : ensuite, c'est la revanche
            if (RematchIn > 0)
            {
                if (State != LobbyState.InRoom || !CanStart) { GD.Print("[Lobby] Revanche annulée"); CancelRematch(); }
                else if ((RematchIn -= delta) <= 0) { GD.Print("[Lobby] Revanche"); StartMatch(); }
            }
            else if (QuickStartIn > 0 && autoStart == 0 && State == LobbyState.InRoom && CanStart && (QuickStartIn -= delta) <= 0)
            {
                GD.Print($"[Lobby] Partie rapide : lancement à {members.Count}, les places libres vont aux bots");
                StartMatch();
            }
        }
        else
        {
            if (RematchIn > 0) RematchIn = Math.Max(0, RematchIn - delta);
            if (QuickStartIn > 0 && State == LobbyState.InRoom && members.Values.All(m => m.Ready)) QuickStartIn = Math.Max(0, QuickStartIn - delta);
        }
    }

    // ------------------------------------------------------------------ signaux réseau

    void OnPeerConnected(long id)
    {
        if (!IsHost) return;
        pendingPeers[(int)id] = Time.GetTicksMsec() / 1000.0;
        NetSession.RelaxTimeout(Multiplayer.MultiplayerPeer, (int)id);
    }

    /// <summary>Hôte : un pair qui inonde la salle de messages est déconnecté.</summary>
    bool Flooding(int peer)
    {
        double second = Math.Floor(Time.GetTicksMsec() / 1000.0);
        var (s0, n) = rate.TryGetValue(peer, out var r) ? r : (second, 0);
        if (s0 != second) n = 0;
        rate[peer] = (second, n + 1);
        if (n + 1 <= MaxLobbyMessagesPerSecond) return false;
        GD.Print($"[Lobby] Pair {peer} déconnecté : trop de messages");
        Multiplayer.MultiplayerPeer.DisconnectPeer(peer);
        return true;
    }

    /// <summary>Hôte : refus, puis déconnexion un peu plus tard (le message part d'abord) ; un client modifié ne garde pas la place.</summary>
    void Refuse(int peer, string reason)
    {
        GD.Print($"[Lobby] Entrée refusée au pair {peer} : {reason}");
        RpcId(peer, MethodName.RefuseRpc, reason);
        GetTree().CreateTimer(0.5).Timeout += () =>
        {
            if (IsHost && !members.ContainsKey(peer) && Array.IndexOf(Multiplayer.GetPeers(), peer) >= 0) Multiplayer.MultiplayerPeer.DisconnectPeer(peer);
        };
    }

    void OnPeerDisconnected(long id)
    {
        if (!IsHost) return;
        pendingPeers.Remove((int)id);
        if (members.Remove((int)id, out var m))
        {
            GD.Print($"[Lobby] {m.Name} a quitté la salle");
            BroadcastRoster();
        }
    }

    void OnConnectedToServer()
    {
        if (IsHost || State is not (LobbyState.Connecting or LobbyState.Reconnecting)) return;
        GD.Print("[Lobby] Connecté à l'hôte : demande d'entrée");
        RpcId(1, MethodName.JoinRequestRpc, NetSession.ProtocolVersion, GameVersion, Identity.Name, ReconnectSecret.Get(), IsVr, myTeam);
    }

    void OnConnectionFailed()
    {
        if (IsHost) return;
        if (State == LobbyState.Reconnecting) { Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer(); return; }   // on retentera
        if (State == LobbyState.Connecting) Fail("Connexion impossible");
    }

    void OnServerDisconnected()
    {
        if (IsHost) return;
        if (State == LobbyState.InMatch) return;   // la session de jeu prévient (OnMatchConnectionLost)
        if (State == LobbyState.Reconnecting) { Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer(); return; }
        if (State == LobbyState.InRoom) Fail("L'hôte a fermé la salle");
    }

    // ------------------------------------------------------------------ RPC

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void JoinRequestRpc(int protocol, string version, string name, string token, bool vr, int team)
    {
        if (!IsHost) return;
        int peer = Multiplayer.GetRemoteSenderId();
        if (Flooding(peer) || members.ContainsKey(peer)) return;   // une seule demande par pair
        pendingPeers.Remove(peer);
        token ??= "";
        if (State == LobbyState.InMatch && NetSession.Current?.LivePeerOf(token) is int live and not 0 && live != peer)
        {
            // L'ancienne connexion de ce joueur parlait encore il y a moins de 1,5 s (il a relancé très vite après un crash) :
            // on laisse passer ce délai pour la juger, morte (on lui rend sa place) ou vivante (copie du secret : nouvelle place).
            GD.Print($"[Lobby] Retour du pair {peer} : ancienne connexion {live} encore active, décision dans 1,6 s");
            GetTree().CreateTimer(1.6).Timeout += () =>
            {
                if (IsHost && System.Array.IndexOf(Multiplayer.GetPeers(), peer) >= 0 && !members.ContainsKey(peer))
                    HandleJoinRequest(peer, protocol, version, name, token, vr, team);
            };
            return;
        }
        HandleJoinRequest(peer, protocol, version, name, token, vr, team);
    }

    void HandleJoinRequest(int peer, int protocol, string version, string name, string token, bool vr, int team)
    {
        // Retour d'un joueur dont l'ancienne connexion n'est pas encore tombée (salle pleine) : l'ancienne place se libère d'abord.
        if (State == LobbyState.InMatch && NetSession.Current?.StalePeerOf(token ?? "") is int stale and not 0 && stale != peer && members.Remove(stale))
            GD.Print($"[Lobby] Ancienne connexion du joueur qui revient (pair {stale}) retirée de la salle");
        string? refusal = null;
        if (protocol != NetSession.ProtocolVersion) refusal = $"Version différente (hôte {GameVersion}, vous {Identity.Clean(version)}) : mettez le jeu à jour";
        else if (members.Count >= MaxPlayers && !members.ContainsKey(peer)) refusal = "Partie pleine";
        else if (State == LobbyState.InMatch && NetSession.Current == null) refusal = "Partie en cours de chargement : réessayez";
        if (refusal != null) { Refuse(peer, refusal); return; }
        token = (token ?? "").Length > 64 ? token![..64] : token ?? "";
        var member = new LobbyMember { PeerId = peer, Name = Identity.Clean(name), Token = token, Vr = vr, Team = Mathf.Clamp(team, 0, 1) };
        // Équipes équilibrées par défaut.
        int[] count = { members.Values.Count(m => m.Team == 0), members.Values.Count(m => m.Team == 1) };
        if (count[member.Team] > count[1 - member.Team]) member.Team = 1 - member.Team;
        members[peer] = member;
        GD.Print($"[Lobby] {member.Name} entre dans la salle (pair {peer}, équipe {member.Team + 1})");
        if (QuickStartIn > 0)
        {
            // Partie rapide : on est venu jouer, donc prêt d'office ; un peu de temps pour changer d'équipe, moins si la salle est pleine.
            member.Ready = true;
            QuickStartIn = members.Count >= MaxPlayers ? Math.Min(QuickStartIn, QuickFullSeconds) : Math.Max(QuickStartIn, QuickJoinGrace);
        }
        if (State == LobbyState.InMatch)
        {
            // Entrée en cours de partie ou retour après une coupure : l'hôte choisit la zone (celle du jeton si elle est libre).
            int zone = NetSession.Current!.ZoneForJoin(token, member.Team, peer);
            if (zone < 0) { members.Remove(peer); Refuse(peer, "Partie pleine"); return; }
            matchZones[peer] = zone;
            BroadcastRoster();
            RpcId(peer, MethodName.StartRpc, Map, zone);
            return;
        }
        BroadcastRoster();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void RefuseRpc(string reason)
    {
        GD.Print($"[Lobby] Refusé par l'hôte : {reason}");
        Fail(reason);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void SetReadyRpc(bool ready)
    {
        if (!IsHost || Flooding(Multiplayer.GetRemoteSenderId()) || !members.TryGetValue(Multiplayer.GetRemoteSenderId(), out var m) || m.Ready == ready) return;
        m.Ready = ready;
        BroadcastRoster();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void SetTeamRpc(int team)
    {
        if (!IsHost || Flooding(Multiplayer.GetRemoteSenderId()) || State != LobbyState.InRoom || !members.TryGetValue(Multiplayer.GetRemoteSenderId(), out var m)) return;
        team = Mathf.Clamp(team, 0, 1);
        if (m.Team == team) return;
        // Équipe pleine (autant de joueurs que de zones de cette équipe) : refusé.
        if (members.Values.Count(x => x.Team == team) >= Math.Max(1, MaxPlayers / 2)) return;
        m.Team = team;
        BroadcastRoster();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void RosterRpc(string json)
    {
        var parsed = Json.ParseString(json);
        if (parsed.VariantType != Variant.Type.Dictionary) return;
        var d = parsed.AsGodotDictionary();
        Map = (string)d["map"];
        members.Clear();
        foreach (var v in d["members"].AsGodotArray())
        {
            var md = v.AsGodotDictionary();
            var m = new LobbyMember { PeerId = (int)md["peer"], Name = (string)md["name"], Team = (int)md["team"], Ready = (bool)md["ready"], Vr = (bool)md["vr"],
                Zone = md.ContainsKey("zone") ? (int)md["zone"] : -1 };
            members[m.PeerId] = m;
        }
        bool hostInMatch = d.ContainsKey("inMatch") && (bool)d["inMatch"];
        RematchIn = d.ContainsKey("rematch") ? (double)d["rematch"] : 0;
        QuickStartIn = d.ContainsKey("quick") ? (double)d["quick"] : 0;
        if (State is LobbyState.Connecting) SetState(LobbyState.InRoom, "Dans la salle");
        else if (State is LobbyState.Reconnecting && !hostInMatch)
        {
            // Reconnecté pendant que l'hôte est revenu à la salle : on quitte l'ancienne scène de jeu.
            SetState(LobbyState.InRoom, "De retour dans la salle");
            GameLaunch.ShowLobby = true;
            GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
        }
        else Changed?.Invoke();
        if (autoReady && State == LobbyState.InRoom && members.TryGetValue(Multiplayer.GetUniqueId(), out var me) && !me.Ready) SetReady(true);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void StartRpc(string map, int zone)
    {
        GD.Print($"[Lobby] L'hôte lance la partie : {map}, zone {zone}");
        LaunchClient(map, zone);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void ReturnToLobbyRpc()
    {
        RelaxTimeouts();
        SetState(LobbyState.InRoom, "De retour dans la salle");
        GameLaunch.ShowLobby = true;
        ReloadQuietly(0.25);
    }

    // ------------------------------------------------------------------ outils

    void LaunchClient(string map, int zone)
    {
        Map = map;
        var cfg = BaseConfig();
        cfg.Mode = NetMode.Client;
        cfg.Map = map;
        cfg.Zone = zone;
        cfg.Preconnected = true;
        cfg.OnlineFormat = OnlineFormat;
        SetState(LobbyState.InMatch, "Partie en cours");
        RelaxTimeouts();
        GameLaunch.Set(cfg);
        GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
    }

    /// <summary>Base de configuration : garde les options de test de la ligne de commande (latence, perte, joueur automatique).</summary>
    static NetConfig BaseConfig()
    {
        var cfg = NetConfig.Parse();
        cfg.Token = Identity.Token;
        cfg.PlayerName = Identity.Name;
        return cfg;
    }

    /// <summary>Nom du joueur humain qui tient cette zone en partie, null si c'est un bot (HUD, fil des KO, écran de fin).</summary>
    public string? NameOfZone(int zone) => State == LobbyState.InMatch ? members.Values.FirstOrDefault(m => m.Zone == zone)?.Name : null;

    void BroadcastRoster()
    {
        if (!IsHost) { Changed?.Invoke(); return; }
        var list = new Godot.Collections.Array();
        foreach (var m in Members)
        {
            m.Zone = State == LobbyState.InMatch && matchZones.TryGetValue(m.PeerId, out int zone) ? zone : -1;
            list.Add(new Godot.Collections.Dictionary { ["peer"] = m.PeerId, ["name"] = m.Name, ["team"] = m.Team, ["ready"] = m.Ready, ["vr"] = m.Vr, ["zone"] = m.Zone });
        }
        string json = Json.Stringify(new Godot.Collections.Dictionary { ["map"] = Map, ["members"] = list, ["inMatch"] = State == LobbyState.InMatch, ["rematch"] = RematchIn, ["quick"] = QuickStartIn });
        foreach (var m in members.Values) if (!m.Host) RpcId(m.PeerId, MethodName.RosterRpc, json);
        SteamBridge.OnRosterChanged(this);
        Changed?.Invoke();
    }

    Godot.Collections.Dictionary? Announcement()
    {
        if (!IsHost || State is not (LobbyState.InRoom or LobbyState.InMatch)) return null;
        return new Godot.Collections.Dictionary
        {
            ["game"] = "dogeball", ["protocol"] = NetSession.ProtocolVersion, ["port"] = port, ["name"] = Identity.Name, ["map"] = Map,
            ["players"] = members.Count, ["max"] = MaxPlayers, ["inMatch"] = State == LobbyState.InMatch, ["id"] = roomId,
        };
    }

    void SetState(LobbyState state, string status)
    {
        if (State != state) GD.Print($"[Lobby] État : {State} -> {state}");
        State = state;
        Status = status;
        stateClock = 0;
        Changed?.Invoke();
    }

    void Fail(string reason)
    {
        GD.Print($"[Lobby] Échec : {reason}");
        // Échec pendant une partie (refus, reconnexion ratée, hôte parti) : on quitte la scène de jeu morte pour le menu.
        bool inGame = State is LobbyState.InMatch or LobbyState.Reconnecting || GetTree().CurrentScene?.GetNodeOrNull("NetSession") != null;
        Reset();
        SetState(LobbyState.Idle, reason);
        Failed?.Invoke(reason);
        if (!inGame) return;
        GameLaunch.ShowLobby = false;
        GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
    }

    void Reset()
    {
        if (Multiplayer.MultiplayerPeer is not OfflineMultiplayerPeer and not null) Multiplayer.MultiplayerPeer.Close();
        Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
        SteamBridge.Leave();
        Lan.Announcement = null;
        announceOnLan = false;
        Lan.StopListening();
        ClosePortMapping();
        members.Clear();
        pendingPeers.Clear();
        matchZones.Clear();
        RematchIn = 0;
        QuickStartIn = 0;
        IsHost = false;
        quickHosted = false;
        rate.Clear();
        Code = "";
        CodeIsInternet = false;
    }

    public static string GameVersion => (string)ProjectSettings.GetSetting("application/config/version", "dev");
    static bool IsVr => Xr.XrBootstrap.Active;

    static MapData? LoadMap(string name)
    {
        string path = $"res://maps/{name}.json";
        return FileAccess.FileExists(path) ? MapData.Load(path) : null;
    }

    static readonly Dictionary<string, int> zoneCounts = new();
    static int MapZoneCount(string name)
    {
        if (!zoneCounts.TryGetValue(name, out int n)) zoneCounts[name] = n = LoadMap(name)?.Zones.Count ?? 4;
        return n;
    }

    /// <summary>Adresse sur le réseau local : réseaux de box (192.168, 10.) avant les autres (Docker, Hyper-V, VPN).</summary>
    static string LocalIPv4()
    {
        var all = IP.GetLocalAddresses().Where(a => a.Count(c => c == '.') == 3 && !a.StartsWith("127.") && !a.StartsWith("169.254.")).ToList();
        return all.FirstOrDefault(a => a.StartsWith("192.168.")) ?? all.FirstOrDefault(a => a.StartsWith("10.")) ?? all.FirstOrDefault() ?? "127.0.0.1";
    }

    // UPnP : ouverture automatique du port sur la box (souvent refusée ; sinon, ouvrir le port UDP à la main ou passer par Steam).
    void OpenPortMapping(int mapPort)
    {
        int ticket = upnpTicket;
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var u = new Upnp();
                if (u.Discover(2000, 2, "") != (int)Upnp.UpnpResult.Success || u.GetGateway() is not { } gw || !gw.IsValidGateway()) { GD.Print("[Lobby] UPnP : pas de box compatible"); return; }
                if (u.AddPortMapping(mapPort, mapPort, "Dogeball", "UDP", UpnpLease) != (int)Upnp.UpnpResult.Success) { GD.Print("[Lobby] UPnP : ouverture du port refusée"); return; }
                string external = u.QueryExternalAddress();
                Callable.From(() =>
                {
                    if (ticket != upnpTicket || !IsHost)
                    {
                        System.Threading.Tasks.Task.Run(() => { try { u.DeletePortMapping(mapPort, "UDP"); } catch (Exception) { } });
                        return;
                    }
                    upnp = u;
                    upnpRenew = UpnpLease * 0.8;
                    if (IsHost && external.Count(c => c == '.') == 3) { Code = JoinCode.FromAddress(external, mapPort); CodeIsInternet = true; GD.Print($"[Lobby] UPnP : port {mapPort} ouvert, code Internet {Code}"); Changed?.Invoke(); }
                }).CallDeferred();
            }
            catch (Exception e) { GD.Print($"[Lobby] UPnP : {e.Message}"); }
        });
    }

    // Bail court (1 h), renouvelé tant que la salle est ouverte : après un plantage, la box referme le port d'elle-même.
    const int UpnpLease = 3600;
    double upnpRenew;

    void RenewPortMapping(double delta)
    {
        if (upnp == null || (upnpRenew -= delta) > 0) return;
        upnpRenew = UpnpLease * 0.8;
        var u = upnp;
        int mapped = port;
        System.Threading.Tasks.Task.Run(() => { try { u.AddPortMapping(mapped, mapped, "Dogeball", "UDP", UpnpLease); } catch (Exception) { } });
    }

    void ClosePortMapping()
    {
        upnpTicket++;
        if (upnp == null) return;
        var u = upnp;
        int mapped = port;
        upnp = null;
        System.Threading.Tasks.Task.Run(() => { try { u.DeletePortMapping(mapped, "UDP"); } catch (Exception) { } });   // la box peut mettre 2 s à répondre
    }
}
