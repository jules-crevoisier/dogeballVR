using System.Collections.Generic;
using System.Linq;
using Godot;
using DogeballVR.Gameplay;
using DogeballVR.Maps;
using DogeballVR.Net;
using DogeballVR.Xr;
using DogeballVR.Zones;

namespace DogeballVR;

/// <summary>
/// Scène principale de la démo 2v2 sur 4 zones (la Tour 4v4 en a huit). Trois modes (voir <see cref="NetConfig"/>) :
/// hors ligne (le joueur local tient la zone 0, des bots les autres), hôte (idem, et un joueur distant peut prendre
/// une zone à la place d'un bot), client (affiche le monde de l'hôte et ne simule rien).
/// </summary>
public partial class Main : Node3D
{
    readonly NetConfig cfg = Online.GameLaunch.Take();   // partie lancée par le lobby, sinon options de la ligne de commande
    MapData map = null!;
    ZoneManager zones = null!;
    MatchController match = null!;
    NetSession? net;
    Node3D? localBody;
    Node3D? xrOrigin;
    DesktopPlayer? desktopPlayer;
    Camera3D? xrCamera;
    LiftController? lift;
    double hostDeniedFor;
    ClientWorld? clientWorld;
    System.Collections.Generic.Dictionary<int, Node3D> platforms = new();
    Node3D localMarker = null!;
    Hurtbox localHurtbox = null!;
    System.Func<PoseData>? localPose;

    // Hôte : bots par zone, et listes de cibles partagées avec les bots.
    readonly Dictionary<int, Bot> bots = new();
    readonly List<Node3D> targets = new();
    readonly List<int> targetTeams = new();
    readonly Dictionary<int, int> targetIndexOfZone = new();

    /// <summary>Retour au menu principal demandé (pause, fin de partie) : la scène est rechargée et affiche le menu même si le jeu a été lancé avec des options.</summary>
    public static bool ReturnToMenu;

    /// <summary>Partie d'entraînement guidée (tutoriel) : hors ligne, bots qui lancent doucement, consignes pas à pas.</summary>
    public static bool Tutorial { get; private set; }

    static bool PlayOfflineOnLoad;

    void PlayForReal()
    {
        PlayOfflineOnLoad = true;
        ReturnToMenu = false;
        Engine.TimeScale = 1.0;
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }

    /// <summary>Le noir du menu se dissipe une fois la partie construite.</summary>
    void FadeIn()
    {
        var layer = new CanvasLayer { Layer = 90 };
        var rect = new ColorRect { Color = new Color(0, 0, 0, 1), MouseFilter = Control.MouseFilterEnum.Ignore };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(rect);
        AddChild(layer);
        var tw = rect.CreateTween();
        tw.TweenInterval(0.15);
        tw.TweenProperty(rect, "color:a", 0.0, 0.5);
        tw.TweenCallback(Callable.From(layer.QueueFree));
    }

    /// <summary>Écran de la salle en ligne sur le décor du menu ; <paramref name="start"/> lance l'action choisie (partie rapide, code…).</summary>
    void ShowLobbyScreen(System.Action<Online.OnlineLobby>? start)
    {
        if (DisplayServer.GetName() == "headless") { if (start != null) Online.OnlineLobby.Ensure(GetTree(), start); return; }
        if (GetNodeOrNull("MenuBackdrop") == null) AddChild(new UI.MenuBackdrop { Name = "MenuBackdrop" });
        Online.OnlineLobby.Ensure(GetTree(), l =>
        {
            AddChild(new Online.LobbyScreen { Name = "LobbyScreen" });
            start?.Invoke(l);
        });
    }

    /// <summary>Recharge la scène sur le menu principal (ferme la connexion réseau, quitte la salle en ligne).</summary>
    public void GoToMenu()
    {
        Online.OnlineLobby.Instance?.Leave();
        ReturnToMenu = true;
        Engine.TimeScale = 1.0;
        GetTree().Paused = false;
        Multiplayer.MultiplayerPeer = null;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        GetTree().ReloadCurrentScene();
    }

    public override void _Ready()
    {
        Online.CrashReporter.Start(GetTree());
        Online.SteamBridge.Init();   // sans effet sans Steam (CI, LAN)
        MatchView.HumanName = z => Online.OnlineLobby.Instance?.NameOfZone(z);   // noms des joueurs de la salle (null : bot)
        bool xrActive = XrBootstrap.Start(GetViewport());
        UI.GameSettings.Load();
        UI.InputSetup.Register();
        UI.GameSettings.Apply(xrActive ? null : GetViewport());
        Engine.TimeScale = 1.0;
        GetTree().Paused = false;
        pcProfile = !xrActive;
        Assist.VrRules = xrActive;
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--assist-test") >= 0) { AddChild(new AssistSelfTest()); return; }
        // Salle en ligne ouverte (retour de fin de partie, échec) ou options de test du lobby : écran du lobby, la partie attend le lancement.
        if (!cfg.Preconnected && (Online.GameLaunch.ShowLobby || Online.OnlineLobby.Instance == null && Online.OnlineLobby.RunCommandLine(GetTree())))
        {
            Online.GameLaunch.ShowLobby = false;
            ShowLobbyScreen(null);
            return;
        }
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--tutorial") >= 0) Tutorial = true;
        if (PlayOfflineOnLoad)
        {
            // Fin du tutoriel : « vraie partie » recharge la scène et lance directement contre les bots.
            PlayOfflineOnLoad = false;
            Tutorial = false;
            cfg.Choose(NetMode.Offline);
            StartMatch(xrActive);
            FadeIn();
            return;
        }
        // Lancement sans option, avec un écran : menu principal (les options de ligne de commande et la CI passent directement)
        if (!xrActive && !cfg.Preconnected && DisplayServer.GetName() != "headless" && (ReturnToMenu || System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--menu") >= 0 || OS.GetCmdlineUserArgs().Length == 0 && OS.GetCmdlineArgs().Length <= 1))
        {
            ReturnToMenu = false;
            var backdrop = new UI.MenuBackdrop { Name = "MenuBackdrop" };
            AddChild(backdrop);
            var menu = new UI.MainMenu { Name = "MainMenu" };
            void Begin(NetMode mode, string address, bool tutorial)
            {
                menu.QueueFree();
                backdrop.QueueFree();
                cfg.Choose(mode, address);
                Tutorial = tutorial;
                StartMatch(xrActive);
                FadeIn();
            }
            menu.Chosen += (mode, address) => Begin(mode, address, false);
            menu.TutorialChosen += () => Begin(NetMode.Offline, "", true);
            // En ligne : la salle (Steam si disponible, sinon réseau local / code) remplace le menu, le décor reste derrière.
            void Room(System.Action<Online.OnlineLobby> start) { menu.QueueFree(); ShowLobbyScreen(start); }
            menu.QuickMatch += () => Room(l => l.QuickMatch());
            menu.CreatePrivate += () => Room(l => l.CreatePrivate());
            menu.HostLan += () => Room(l => l.HostLan());
            menu.JoinCode += code => Room(l => l.JoinByCode(code));
            menu.JoinAddress += ip => Room(l => l.JoinAddress(ip));
            AddChild(menu);
            return;
        }
        StartMatch(xrActive);
    }

    void StartMatch(bool xrActive)
    {
        Audio.Music.PlayMatch();
        bool client = cfg.Mode == NetMode.Client;

        string mapPath = $"res://maps/{cfg.Map}.json";
        if (!FileAccess.FileExists(mapPath))
        {
            GD.PrintErr($"Map introuvable : {mapPath}. Repli sur neon_court.");
            mapPath = "res://maps/neon_court.json";
        }
        map = MapData.Load(mapPath);
        int localZoneId = cfg.LocalZone;
        var localZone = map.Zones.Find(z => z.Id == localZoneId)
            ?? throw new System.InvalidOperationException($"Zone inconnue : {localZoneId}");

        zones = new ZoneManager { Name = "ZoneManager" };
        AddChild(zones);
        zones.Setup(map, localZone.Id);
        double lastEdgeLog = -10;
        zones.EdgeWarning += d =>
        {
            double now = Time.GetTicksMsec() / 1000.0;
            if (map.IsRing && xrOrigin == null) return;   // jeu libre au clavier : pas de bord de zone
            if (now - lastEdgeLog < 2.0) return;   // pas un message par image contre le bord
            lastEdgeLog = now;
            GD.Print($"Bord de zone à {d:0.00} m");
        };
        var arena = ArenaBuilder.Build(map, localZone.Id, !xrActive);
        AddChild(arena.Root);
        platforms = arena.Platforms;

        if (cfg.Mode != NetMode.Offline)
        {
            net = new NetSession { Name = "NetSession", Token = Online.ReconnectSecret.Get() };
            AddChild(net);
            if (DisplayServer.GetName() != "headless") AddChild(new Online.NetStatusOverlay { Name = "NetStatus", Net = net });
            if (client) BallActions.Net = net;
        }

        localHurtbox = new Hurtbox { Name = "LocalHurtbox", PlayerId = localZone.Id, Team = localZone.Team, Passive = client };
        BuildLocalPlayer(xrActive, localZone);
        if (localZone.Lift != null && !(map.IsRing && xrOrigin == null)) BuildLift(localZone);   // le lift est un geste VR ; au clavier, tremplins et terrasses

        if (client) BuildClient(localZone);
        else BuildHost(localZone);
    }

    // ------------------------------------------------------------------ joueur local

    void BuildLocalPlayer(bool xrActive, ZoneDef localZone)
    {
        if (xrActive)
        {
            var origin = new XROrigin3D { Name = "XROrigin3D", Position = localZone.Center };
            var camera = new XRCamera3D { Name = "XRCamera3D" };
            var left = new HandThrower { Name = "LeftHand", Tracker = "left_hand", Hurtbox = localHurtbox };
            var right = new HandThrower { Name = "RightHand", Tracker = "right_hand", Hurtbox = localHurtbox };
            origin.AddChild(camera);
            origin.AddChild(left);
            origin.AddChild(right);
            localHurtbox.Head = camera;
            origin.AddChild(localHurtbox);
            AddChild(origin);
            localBody = camera;
            xrOrigin = origin;
            xrCamera = camera;
            localPose = () => new PoseData(camera.GlobalPosition, camera.GlobalRotation.Y, left.GlobalPosition, right.GlobalPosition, origin.Position.Y - localZone.Y);
        }
        else
        {
            // Pas de casque : joueur clavier-souris pour tester (voir DesktopPlayer).
            var desktop = new DesktopPlayer { Name = "DesktopPlayer", Zones = zones, Hurtbox = localHurtbox, FreeRoam = map.IsRing };
            AddChild(desktop);
            localBody = desktop;
            desktopPlayer = desktop;
            localPose = () => desktop.Pose;
        }
        localMarker = new Node3D { Name = "LocalMarker" };
        AddChild(localMarker);
    }

    // ------------------------------------------------------------------ lift (maps à niveaux)

    void BuildLift(ZoneDef zone)
    {
        var hands = new System.Collections.Generic.List<HandThrower>();
        if (xrOrigin != null) foreach (var c in xrOrigin.GetChildren()) if (c is HandThrower h) hands.Add(h);
        lift = new LiftController
        {
            Name = "LiftController", Lift = zone.Lift!, Rules = map.LiftRules,
            HeadPosition = () => localBody!.GlobalPosition,
            HeadHeight = () => desktopPlayer != null ? desktopPlayer.HeadHeight : xrCamera!.Position.Y,
            Locked = () =>
            {
                var ball = desktopPlayer?.HeldBall;
                foreach (var h in hands) ball ??= h.HeldBall;
                if (map.LiftRules.LockWhenHoldingChargedBall && ball is { Charge: >= 2 }) return true;
                // Une seule zone haute par équipe : verrouillé tant qu'un coéquipier est en haut.
                if (map.LiftRules.OneHighZonePerTeam)
                    foreach (var z in map.Zones)
                        if (z.Team == zone.Team && z.Id != zone.Id && ZoneElevation(z.Id) > 0.1f) return true;
                return false;
            },
            Eliminated = () => localHurtbox.Eliminated,
            Apply = e =>
            {
                localHurtbox.Elevation = e;
                if (desktopPlayer != null) desktopPlayer.Elevation = e;
                if (xrOrigin != null) xrOrigin.Position = new Vector3(xrOrigin.Position.X, zone.Y + e, xrOrigin.Position.Z);
            },
            Fade = MakeFade(),
        };
        AddChild(lift);
    }

    /// <summary>Fondu au noir du lift : calque 2D au clavier-souris, voile devant la caméra en VR.</summary>
    System.Action<float> MakeFade()
    {
        if (xrCamera != null)
        {
            var veil = new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = new Vector2(4, 4) },
                Position = new Vector3(0, 0, -0.25f),
                Visible = false,
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0, 0, 0, 0), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha, NoDepthTest = true, RenderPriority = 100,
                },
            };
            xrCamera.AddChild(veil);
            return a => { veil.Visible = a > 0.001f; ((StandardMaterial3D)veil.MaterialOverride!).AlbedoColor = new Color(0, 0, 0, a); };
        }
        var layer = new CanvasLayer { Layer = 10 };
        var rect = new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(rect);
        AddChild(layer);
        return a => rect.Color = new Color(0, 0, 0, a);
    }

    /// <summary>Altitude du plateau d'une zone : lift local, pose d'un joueur distant (hôte), snapshot (client), 0 pour un bot.</summary>
    float ZoneElevation(int zoneId)
    {
        if (zoneId == localHurtbox.PlayerId) return lift != null ? lift.Elevation : desktopPlayer?.Elevation ?? 0f;
        if (clientWorld != null) return clientWorld.ElevationOf(zoneId);
        return net != null && net.PoseProviders.TryGetValue(zoneId, out var provider) && !bots.ContainsKey(zoneId) ? provider().Elevation : 0f;
    }

    // ------------------------------------------------------------------ hôte / hors ligne

    int currentPlate;
    System.Action<float>? swapFade;

    /// <summary>Échange de plaques : si la table dit que ce joueur change de plaque, on le déplace (fondu en VR) ; chaque machine déplace son propre joueur.</summary>
    void ApplyPlates(int[] plates)
    {
        int id = localHurtbox.PlayerId;
        if (id >= plates.Length || plates[id] == currentPlate) return;
        var from = map.Zones.Find(z => z.Id == currentPlate)!;
        var to = map.Zones.Find(z => z.Id == plates[id])!;
        currentPlate = to.Id;
        swapFade ??= MakeFade();
        swapFade(1f);
        if (desktopPlayer != null) desktopPlayer.MoveToPlate(to);
        else if (xrOrigin != null)
        {
            // On tourne autour de la tête (pas de l'origine) : elle se retrouve au même endroit relatif de la plaque d'arrivée.
            var wanted = PlateSwap.Remap(xrCamera!.GlobalPosition, from, to);
            xrOrigin.RotateY(PlateSwap.YawDelta(from, to));
            var shift = wanted - xrCamera.GlobalPosition;
            xrOrigin.GlobalPosition += new Vector3(shift.X, 0, shift.Z);
            zones.LocalZoneId = to.Id;
        }
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(v => swapFade(v)), 1f, 0f, 0.4);
    }

    static bool PcAutoplay => System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--pc-autoplay") >= 0;

    bool pcProfile = true;   // fosse, tremplins et tacle : réservés au clavier-souris, la VR garde ses règles

    void BuildHost(ZoneDef localZone)
    {
        match = new MatchController { Name = "MatchController" };
        AddChild(match);
        match.PcProfile = pcProfile;
        match.ConfigureMap(map);
        match.ZoneElevation = ZoneElevation;
        match.PlatformsMove = !(map.IsRing && xrOrigin == null);
        match.IsBot = id => bots.ContainsKey(id);
        match.PushVictim = (victimId, dir) =>
        {
            if (victimId == localZone.Id) desktopPlayer?.ApplyPush(dir);
            else net?.SendPush(victimId, dir);
        };
        match.Register(localHurtbox);
        currentPlate = localZone.Id;
        match.PlateSwaps = map.IsRing && (!pcProfile || System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--swap-test") >= 0);
        match.HeadOf = zoneId => net != null && net.PoseProviders.TryGetValue(zoneId, out var provider) ? provider().Head : null;
        match.PlatesChanged += (plates, targets) =>
        {
            ApplyPlates(plates);
            net?.BroadcastPlates(plates, targets);
        };

        targets.Add(localMarker);
        targetTeams.Add(localZone.Team);
        targetIndexOfZone[localZone.Id] = 0;
        foreach (var z in map.Zones)
            if (z.Id != localZone.Id) SpawnBot(z);

        if (localBody is DesktopPlayer desktop)
        {
            // HUD et écran de fin du joueur PC ; un vrai joueur choisit REJOUER ou MENU, le joueur automatique et la CI relancent seuls.
            var view = MatchView.ForHost(match, map, localZone.Id, localZone.Team);
            AddChild(new DesktopHud { Name = "DesktopHud", Player = desktop, View = view });
            AddChild(new UI.EndScreen { Name = "EndScreen", View = view, QuitToMenu = GoToMenu, Autoplay = PcAutoplay });
            // En ligne, l'hôte ne fait pas attendre les autres sur l'écran de fin : la partie suivante démarre seule (mêmes joueurs, bots en renfort).
            match.AutoRestart = PcAutoplay || DisplayServer.GetName() == "headless" || cfg.Mode == NetMode.Host;
        }
        if (localBody is DesktopPlayer) match.FloatingHudVisible = false;
        if (localBody is DesktopPlayer) AddPauseMenu(canFreeze: cfg.Mode == NetMode.Offline, restart: () => match.StartMatch());
        if (Tutorial && localBody is DesktopPlayer tutee && cfg.Mode == NetMode.Offline)
            AddChild(new UI.Tutorial { Name = "Tutorial", Player = tutee, PlayForReal = PlayForReal, BackToMenu = GoToMenu });
        AddKnockoutFeedback(localZone, id => match.Rules.LivesOf(id), match.Rules.LivesPerPlayer, () => match.HudText);

        if (net != null)
        {
            net.PoseProviders[localZone.Id] = () => localPose!();
            net.LivesOf = id => match.Rules.LivesOf(id);
            net.IsEliminated = id => match.IsEliminated(id);
            net.IsBot = id => bots.ContainsKey(id);
            net.FreeRoam = map.IsRing && xrOrigin == null;
            net.SpawnOf = id => map.Pc?.Spawns.Find(sp => sp.Zone == match.PlateOf(id)) is { } s ? new Vector3(s.X, 0, s.Z) : null;
            net.ZoneRect = id =>
            {
                if (net.FreeRoam) return net.ArenaXZ;
                var z = map.Zones.Find(zz => zz.Id == match.PlateOf(id));
                return z == null ? net.ArenaXZ : new Rect2(z.X - map.ZoneWidth / 2 - 0.5f, z.Z - map.ZoneDepth / 2 - 0.5f, map.ZoneWidth + 1f, map.ZoneDepth + 1f);
            };
            net.MaxElevation = map.MaxElevation;
            net.Map = map;
            net.LiftOf = id => net.FreeRoam ? null : map.Zones.Find(zz => zz.Id == id)?.Lift;
            net.TeammateHigh = id =>
            {
                if (!map.LiftRules.OneHighZonePerTeam) return false;
                var me = map.Zones.Find(zz => zz.Id == id);
                return me != null && map.Zones.Exists(zz => zz.Team == me.Team && zz.Id != id && ZoneElevation(zz.Id) > 0.1f);
            };
            var bnds = map.Bounds;
            net.ArenaXZ = new Rect2(bnds.MinX, bnds.MinZ, bnds.MaxX - bnds.MinX, bnds.MaxZ - bnds.MinZ);
            net.HudText = () => match.HudText;
            net.StatsData = () => MatchView.EncodeStats(match.Stats, id => bots.ContainsKey(id));
            net.PeerHello += OnPeerHello;
            net.PeerLeft += OnPeerLeft;
            net.ZoneAvailable = id => bots.ContainsKey(id) && id != localZone.Id;
            net.PickZone = PickFreeZone;
            if (cfg.Mode == NetMode.Host && cfg.Preconnected) net.AdoptHost(fromLobby: true);
            else if (cfg.Mode == NetMode.Host)
            {
                var err = net.Host(cfg.Port);
                if (err != Error.Ok) GD.PrintErr($"[Net] Impossible d'héberger : {err}");
            }
        }
        AddAchievementTracker(localZone, match);
        match.StartMatch();
        if (cfg.Preconnected) Online.OnlineLobby.Instance?.HostMatchReady();   // les joueurs de la salle peuvent entrer
    }

    /// <summary>Zone tenue par un bot pour un joueur qui entre : dans l'équipe demandée si possible, sinon dans celle qui a le moins d'humains.</summary>
    int PickFreeZone(int preferredTeam, System.Func<int, bool> allowed)
    {
        int Humans(int team) => map.Zones.Count(z => z.Team == team && !bots.ContainsKey(z.Id));
        var free = map.Zones.Where(z => bots.ContainsKey(z.Id) && z.Id != localHurtbox.PlayerId && allowed(z.Id)).ToList();
        if (free.Count == 0) return -1;
        var pick = free.Where(z => z.Team == preferredTeam).FirstOrDefault()
            ?? free.OrderBy(z => Humans(z.Team)).ThenBy(z => z.Id).First();
        return pick.Id;
    }

    void SpawnBot(ZoneDef z)
    {
        var hurtbox = new Hurtbox { Name = $"Hurtbox{z.Id}", PlayerId = z.Id, Team = z.Team };
        match.Register(hurtbox);
        var bot = new Bot
        {
            Name = $"Bot{z.Id}", Zone = z, ZoneWidth = map.ZoneWidth, ZoneDepth = map.ZoneDepth,
            Hurtbox = hurtbox, Position = z.Center, Targets = targets, TargetTeams = targetTeams,
        };
        AddChild(bot);
        bots[z.Id] = bot;
        if (!targetIndexOfZone.TryGetValue(z.Id, out int index))
        {
            index = targets.Count;
            targets.Add(bot);
            targetTeams.Add(z.Team);
            targetIndexOfZone[z.Id] = index;
        }
        else targets[index] = bot;
        if (net != null)
        {
            net.PoseProviders[z.Id] = () => BotPose(bot, z);
        }
    }

    static PoseData BotPose(Bot bot, ZoneDef z)
    {
        var a = bot.Avatar!;
        return new PoseData(a.Head.GlobalPosition, Mathf.DegToRad(z.FacingDegrees), a.LeftHand.GlobalPosition, a.RightHand.GlobalPosition);
    }

    /// <summary>Un joueur distant prend la place du bot de sa zone.</summary>
    void OnPeerHello(int peerId, int zoneId)
    {
        var zone = map.Zones.Find(z => z.Id == zoneId);
        if (zone == null || zoneId == localHurtbox.PlayerId || !bots.TryGetValue(zoneId, out var bot))
        {
            GD.Print($"[Net] Zone {zoneId} refusée pour le pair {peerId}");
            return;
        }
        match.RestorePlates();
        bots.Remove(zoneId);
        bot.DropHeld();
        bot.QueueFree();
        match.Unregister(zoneId);

        var start = zone.Center;
        if (net != null && net.FreeRoam && map.Pc?.Spawns.Find(sp => sp.Zone == zoneId) is { } sp0) start = new Vector3(sp0.X, 0, sp0.Z);   // jeu libre : le client apparaît à son point d'apparition
        var remote = new RemotePlayer { Name = $"Remote{peerId}", PeerId = peerId, PlayerId = zoneId, Team = zone.Team,
            StartPose = new PoseData(start + Vector3.Up * 1.6f, Mathf.DegToRad(zone.FacingDegrees), start + new Vector3(-0.3f, 1.0f, 0), start + new Vector3(0.3f, 1.0f, 0)) };
        AddChild(remote);
        match.Register(remote.Hurtbox);
        net!.Remotes[peerId] = remote;
        net.PoseProviders[zoneId] = () => remote.GetPose();
        targets[targetIndexOfZone[zoneId]] = remote.Body;
        GD.Print($"[Net] Joueur distant en zone {zoneId}");
    }

    /// <summary>Un joueur part : un bot reprend sa zone.</summary>
    void OnPeerLeft(int peerId)
    {
        if (!net!.Remotes.Remove(peerId, out var remote)) return;
        int zoneId = remote.PlayerId;
        net.LiftPlayerLeft(remote);
        match.Unregister(zoneId);
        remote.DropBalls();
        remote.QueueFree();
        match.RestorePlates();
        SpawnBot(map.Zones.Find(z => z.Id == zoneId)!);
        GD.Print($"[Net] Joueur de la zone {zoneId} parti, un bot reprend");
    }

    /// <summary>Menu pause du joueur clavier-souris (Échap).</summary>
    void AddPauseMenu(bool canFreeze, System.Action? restart)
    {
        var lockLayer = new CanvasLayer { Name = "LockOn", Layer = 5 };
        lockLayer.AddChild(new UI.LockOnMarker { Player = desktopPlayer! });
        AddChild(lockLayer);
        AddChild(new UI.PauseMenu { Name = "PauseMenu", CanFreeze = canFreeze, Restart = restart, QuitToMenu = GoToMenu });
    }

    // ------------------------------------------------------------------ client

    /// <summary>Adaptation Knockout City : alerte de tir, sons, bulles, cœurs, panneaux muraux de score (communs à l'hôte et au client).</summary>
    /// <summary>Succès Steam (ou leur repli local) : KO, prises, victoires. Sans match local (client), la victoire vient de l'hôte.</summary>
    void AddAchievementTracker(ZoneDef localZone, MatchController? localMatch) =>
        AddChild(new Online.Steam.AchievementTracker
        {
            Name = "AchievementTracker", LocalPlayerId = localZone.Id, LocalTeam = localZone.Team, Match = localMatch,
            TeamOf = id => map.Zones.Find(z => z.Id == id)?.Team ?? -1, Vr = xrOrigin != null, Online = net != null,
        });

    void AddKnockoutFeedback(ZoneDef localZone, System.Func<int, int> lives, int maxLives, System.Func<string> score)
    {
        var hands = new List<HandThrower>();
        if (xrOrigin != null) foreach (var c in xrOrigin.GetChildren()) if (c is HandThrower h) hands.Add(h);
        AddChild(new Visual.ThreatIndicator { Name = "ThreatIndicator", Local = localHurtbox, Haptic = hands.Count == 0 ? null : (a, d) => { foreach (var h in hands) h.Pulse(a, d); } });
        AddChild(new Visual.Feedback { Name = "Feedback", Local = localHurtbox, Lives = lives, MaxLives = maxLives, GrayWhenKo = xrOrigin == null });
        if (xrOrigin != null) foreach (var z in map.Zones) AddChild(Visual.ScorePanel.Create(z, map, score));   // au clavier-souris, le score est dans le HUD : pas de textes flottants dans le décor
        // Cœurs au dos de la main gauche (VR) : deux cœurs lisibles d'un coup d'œil sur le poignet.
        var left = hands.Find(h => h.Tracker == "left_hand");
        if (left != null)
        {
            var hearts = Visual.HeartsDisplay.Create(() => lives(localZone.Id), maxLives, Visual.Palette.Team(localZone.Team), 0.0007f, false);
            hearts.Position = new Vector3(0, 0.08f, 0.04f);
            hearts.RotationDegrees = new Vector3(-90, 0, 0);
            left.AddChild(hearts);
        }
    }

    void BuildClient(ZoneDef localZone)
    {
        var hud = new Label3D
        {
            Name = "Hud", Position = new Vector3(0, 4f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 96, PixelSize = 0.004f, NoDepthTest = true,
        };
        AddChild(hud);
        var world = new ClientWorld
        {
            Name = "ClientWorld", Net = net!, Map = map, LocalPlayerId = localZone.Id, LocalHurtbox = localHurtbox, Hud = hud,
        };
        AddChild(world);
        if (net != null) net.PushReceived = dir => desktopPlayer?.ApplyPush(dir);
        clientWorld = world;
        net!.LocalPose = localPose;
        currentPlate = localZone.Id;
        net.PlatesReceived += ApplyPlates;
        net.StatsReceived += MatchView.ApplyRemoteStats;   // statistiques de fin et bots, envoyées par l'hôte
        if (localBody is DesktopPlayer desktop)
        {
            // Le client relit le texte du tableau de score ; touches, statistiques et bots arrivent de l'hôte par le réseau.
            var view = MatchView.ForClient(() => hud.Text, world.Lives, map, localZone.Id, localZone.Team);
            AddChild(new DesktopHud { Name = "DesktopHud", Player = desktop, View = view });
            AddChild(new UI.EndScreen { Name = "EndScreen", View = view, QuitToMenu = GoToMenu, Autoplay = PcAutoplay });
        }
        if (localBody is DesktopPlayer) hud.Visible = false;
        if (localBody is DesktopPlayer) AddPauseMenu(canFreeze: false, restart: null);
        AddKnockoutFeedback(localZone, world.Lives, 2, () => hud.Text);

        if (cfg.SelfTest)
        {
            // Le joueur automatique remplace les entrées : il vise le centre de la zone adverse.
            var enemy = map.Zones.Find(z => z.Team != localZone.Team)!;
            AddChild(new NetSelfTest { Name = "NetSelfTest", Net = net, World = world, PlayerId = localZone.Id, AimAt = enemy.Center + Vector3.Up,
                Lift = localZone.Lift, ZoneCenter = localZone.Center, ZoneHalfSize = new Vector2(map.ZoneWidth / 2 - 0.3f, map.ZoneDepth / 2 - 0.3f) });
        }
        AddAchievementTracker(localZone, null);
        net.SimulatedLag = cfg.LagMs / 1000.0;
        net.SimulatedLoss = cfg.LossRate;
        if (Online.OnlineLobby.Instance is { } lobby)
        {
            // Partie lancée par le lobby : reconnexion automatique, zone corrigée par l'hôte, refus renvoyé au menu.
            net.Disconnected += lobby.OnMatchConnectionLost;
            net.Welcomed += (zone, secret) => { Online.ReconnectSecret.Set(secret); lobby.OnWelcomedZone(zone, localZone.Id); };
            net.Refused += lobby.OnMatchRefused;
        }
        if (cfg.Preconnected) net.AdoptClient(localZone.Id);
        else
        {
            var err = net.Join(cfg.Address, cfg.Port, localZone.Id);
            if (err != Error.Ok) GD.PrintErr($"[Net] Impossible de rejoindre : {err}");
        }
    }

    public override void _Process(double delta)
    {
        if (localBody == null) return;
        var p = localBody.GlobalPosition;
        float elevation = lift?.Elevation ?? 0f;
        // Client : l'hôte décide. S'il ne confirme pas notre altitude pendant 1 s, on redescend.
        if (clientWorld != null && lift != null)
        {
            hostDeniedFor = lift.IsHigh && clientWorld.ElevationOf(localHurtbox.PlayerId) < 0.1f ? hostDeniedFor + delta : 0;
            if (hostDeniedFor > 1.0) { lift.ForceDown(); hostDeniedFor = 0; }
        }
        localMarker.GlobalPosition = new Vector3(p.X, elevation, p.Z);
        if (map.IsRing && xrOrigin == null) return;   // clavier-souris en moulinet : pas de plateaux de lift
        foreach (var (id, platform) in platforms)
        {
            float e = ZoneElevation(id);
            platform.Position = new Vector3(0, e, 0);
            // Garde-corps : seulement en haut.
            if (platform.GetNodeOrNull<StaticBody3D>("GardeCorps") is { } rail)
            {
                rail.Visible = e > 0.1f;
                rail.CollisionLayer = e > 0.1f ? 1u : 0u;
            }
        }
    }
}
