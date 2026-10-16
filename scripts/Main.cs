using System.Collections.Generic;
using Godot;
using DogeballVR.Gameplay;
using DogeballVR.Maps;
using DogeballVR.Xr;
using DogeballVR.Zones;

namespace DogeballVR;

/// <summary>
/// Scène principale de la démo 2v2 sur 4 zones. Zone 0 = joueur local (VR ou clavier-souris),
/// les 3 autres zones sont tenues par des bots tant que le réseau n'est pas branché.
/// </summary>
public partial class Main : Node3D
{
    Node3D? localBody;
    Node3D localMarker = null!;

    public override void _Ready()
    {
        bool xrActive = XrBootstrap.Start(GetViewport());

        var map = MapData.Load("res://maps/demo_2v2.json");
        var zones = new ZoneManager { Name = "ZoneManager" };
        AddChild(zones);
        zones.Setup(map, localZoneId: 0);
        zones.EdgeWarning += d => GD.Print($"Bord de zone à {d:0.00} m");

        var match = new MatchController { Name = "MatchController" };
        AddChild(match);

        foreach (var zone in map.Zones) match.SpawnZones.Add(zone.Center);

        var localZone = map.Zones[0];
        var local = new Hurtbox { Name = "LocalHurtbox", PlayerId = localZone.Id, Team = localZone.Team };
        match.Register(local);
        if (xrActive)
        {
            var origin = new XROrigin3D { Name = "XROrigin3D", Position = localZone.Center };
            var camera = new XRCamera3D { Name = "XRCamera3D" };
            origin.AddChild(camera);
            origin.AddChild(new HandThrower { Name = "LeftHand", Tracker = "left_hand", Hurtbox = local });
            origin.AddChild(new HandThrower { Name = "RightHand", Tracker = "right_hand", Hurtbox = local });
            local.Head = camera;
            origin.AddChild(local);
            AddChild(origin);
            localBody = camera;
        }
        else
        {
            // Pas de casque : joueur clavier-souris pour tester (voir DesktopPlayer).
            var desktop = new DesktopPlayer { Name = "DesktopPlayer", Zones = zones, Hurtbox = local };
            AddChild(desktop);
            localBody = desktop;
            AddChild(new DesktopHud { Name = "DesktopHud", Player = desktop, Match = match, PlayerId = localZone.Id });
        }

        // Cibles des bots : le joueur local (marqueur qui suit son corps) puis les autres zones.
        localMarker = new Node3D { Name = "LocalMarker" };
        AddChild(localMarker);
        var targets = new List<Node3D> { localMarker };
        var teams = new List<int> { localZone.Team };
        var bots = new List<Bot>();
        foreach (var z in map.Zones)
        {
            if (z.Id == localZone.Id) continue;
            var hurtbox = new Hurtbox { Name = $"Hurtbox{z.Id}", PlayerId = z.Id, Team = z.Team };
            match.Register(hurtbox);
            var bot = new Bot
            {
                Name = $"Bot{z.Id}", Zone = z, ZoneWidth = map.ZoneWidth, ZoneDepth = map.ZoneDepth,
                Hurtbox = hurtbox, Position = z.Center,
            };
            AddChild(bot);
            bots.Add(bot);
            targets.Add(bot);
            teams.Add(z.Team);
        }
        foreach (var b in bots) { b.Targets = targets; b.TargetTeams = teams; }

        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-60, 30, 0) });
        AddChild(ArenaBuilder.Build(map));
        match.StartMatch();
    }

    public override void _Process(double delta)
    {
        if (localBody == null) return;
        var p = localBody.GlobalPosition;
        localMarker.GlobalPosition = new Vector3(p.X, 0, p.Z);
    }
}
