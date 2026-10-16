using System.Collections.Generic;
using Godot;
using DogeballVR.Gameplay;
using DogeballVR.Maps;
using DogeballVR.Net;
using DogeballVR.Xr;
using DogeballVR.Zones;

namespace DogeballVR;

/// <summary>
/// Scène principale de la démo 2v2 sur 4 zones. Trois modes (voir <see cref="NetConfig"/>) :
/// hors ligne (le joueur local tient la zone 0, des bots les autres), hôte (idem, et un joueur distant peut prendre
/// une zone à la place d'un bot), client (affiche le monde de l'hôte et ne simule rien).
/// </summary>
public partial class Main : Node3D
{
    readonly NetConfig cfg = NetConfig.Parse();
    MapData map = null!;
    ZoneManager zones = null!;
    MatchController match = null!;
    NetSession? net;
    Node3D? localBody;
    Node3D? xrOrigin;
    DesktopPlayer? desktopPlayer;
    Camera3D? xrCamera;
    LiftController? lift;
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

    public override void _Ready()
    {
        bool xrActive = XrBootstrap.Start(GetViewport());
        bool client = cfg.Mode == NetMode.Client;

        string mapPath = $"res://maps/{cfg.Map}.json";
        if (!FileAccess.FileExists(mapPath))
        {
            GD.PrintErr($"Map introuvable : {mapPath}. Repli sur neon_court.");
            mapPath = "res://maps/neon_court.json";
        }
        map = MapData.Load(mapPath);
        int localZoneId = client ? cfg.Zone : 0;
        var localZone = map.Zones.Find(z => z.Id == localZoneId)
            ?? throw new System.InvalidOperationException($"Zone inconnue : {localZoneId}");

        zones = new ZoneManager { Name = "ZoneManager" };
        AddChild(zones);
        zones.Setup(map, localZone.Id);
        zones.EdgeWarning += d => GD.Print($"Bord de zone à {d:0.00} m");
        var arena = ArenaBuilder.Build(map, localZone.Id);
        AddChild(arena.Root);
        platforms = arena.Platforms;

        if (cfg.Mode != NetMode.Offline)
        {
            net = new NetSession { Name = "NetSession" };
            AddChild(net);
            if (client) BallActions.Net = net;
        }

        localHurtbox = new Hurtbox { Name = "LocalHurtbox", PlayerId = localZone.Id, Team = localZone.Team, Passive = client };
        BuildLocalPlayer(xrActive, localZone);
        if (localZone.Lift != null) BuildLift(localZone);

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
            var desktop = new DesktopPlayer { Name = "DesktopPlayer", Zones = zones, Hurtbox = localHurtbox };
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
                return map.LiftRules.LockWhenHoldingChargedBall && ball is { Charge: >= 2 };
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
        if (zoneId == localHurtbox.PlayerId) return lift?.Elevation ?? 0f;
        if (clientWorld != null) return clientWorld.ElevationOf(zoneId);
        return net != null && net.PoseProviders.TryGetValue(zoneId, out var provider) && !bots.ContainsKey(zoneId) ? provider().Elevation : 0f;
    }

    // ------------------------------------------------------------------ hôte / hors ligne

    void BuildHost(ZoneDef localZone)
    {
        match = new MatchController { Name = "MatchController" };
        AddChild(match);
        match.ConfigureMap(map);
        match.ZoneElevation = ZoneElevation;
        match.Register(localHurtbox);

        targets.Add(localMarker);
        targetTeams.Add(localZone.Team);
        targetIndexOfZone[localZone.Id] = 0;
        foreach (var z in map.Zones)
            if (z.Id != localZone.Id) SpawnBot(z);

        if (localBody is DesktopPlayer desktop)
            AddChild(new DesktopHud { Name = "DesktopHud", Player = desktop, Lives = () => match.Rules.LivesOf(localZone.Id), MaxLives = match.Rules.LivesPerPlayer });

        if (net != null)
        {
            net.PoseProviders[localZone.Id] = () => localPose!();
            net.LivesOf = id => match.Rules.LivesOf(id);
            net.IsEliminated = id => match.IsEliminated(id);
            net.IsBot = id => bots.ContainsKey(id);
            net.ZoneRect = id =>
            {
                var z = map.Zones.Find(zz => zz.Id == id);
                return z == null ? net.ArenaXZ : new Rect2(z.X - map.ZoneWidth / 2 - 0.5f, z.Z - map.ZoneDepth / 2 - 0.5f, map.ZoneWidth + 1f, map.ZoneDepth + 1f);
            };
            net.MaxElevation = map.MaxElevation;
            var bnds = map.Bounds;
            net.ArenaXZ = new Rect2(bnds.MinX, bnds.MinZ, bnds.MaxX - bnds.MinX, bnds.MaxZ - bnds.MinZ);
            net.HudText = () => match.HudText;
            net.PeerHello += OnPeerHello;
            net.PeerLeft += OnPeerLeft;
            if (cfg.Mode == NetMode.Host)
            {
                var err = net.Host(cfg.Port);
                if (err != Error.Ok) GD.PrintErr($"[Net] Impossible d'héberger : {err}");
            }
        }
        match.StartMatch();
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
        bots.Remove(zoneId);
        bot.DropHeld();
        bot.QueueFree();
        match.Unregister(zoneId);

        var remote = new RemotePlayer { Name = $"Remote{peerId}", PeerId = peerId, PlayerId = zoneId, Team = zone.Team,
            StartPose = new PoseData(zone.Center + Vector3.Up * 1.6f, Mathf.DegToRad(zone.FacingDegrees), zone.Center + new Vector3(-0.3f, 1.0f, 0), zone.Center + new Vector3(0.3f, 1.0f, 0)) };
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
        match.Unregister(zoneId);
        remote.DropBalls();
        remote.QueueFree();
        SpawnBot(map.Zones.Find(z => z.Id == zoneId)!);
        GD.Print($"[Net] Joueur de la zone {zoneId} parti, un bot reprend");
    }

    // ------------------------------------------------------------------ client

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
        clientWorld = world;
        net!.LocalPose = localPose;
        if (localBody is DesktopPlayer desktop)
            AddChild(new DesktopHud { Name = "DesktopHud", Player = desktop, Lives = () => world.Lives(localZone.Id) });

        if (cfg.SelfTest)
        {
            // Le joueur automatique remplace les entrées : il vise le centre de la zone adverse.
            var enemy = map.Zones.Find(z => z.Team != localZone.Team)!;
            AddChild(new NetSelfTest { Name = "NetSelfTest", Net = net, World = world, PlayerId = localZone.Id, AimAt = enemy.Center + Vector3.Up,
                ZoneCenter = localZone.Center, ZoneHalfSize = new Vector2(map.ZoneWidth / 2 - 0.3f, map.ZoneDepth / 2 - 0.3f) });
        }
        net.SimulatedLag = cfg.LagMs / 1000.0;
        var err = net.Join(cfg.Address, cfg.Port, localZone.Id);
        if (err != Error.Ok) GD.PrintErr($"[Net] Impossible de rejoindre : {err}");
    }

    public override void _Process(double delta)
    {
        if (localBody == null) return;
        var p = localBody.GlobalPosition;
        float elevation = lift?.Elevation ?? 0f;
        localMarker.GlobalPosition = new Vector3(p.X, elevation, p.Z);
        foreach (var (id, platform) in platforms)
            platform.Position = new Vector3(0, ZoneElevation(id), 0);
    }
}
