using Godot;
using DogeballVR.Gameplay;
using DogeballVR.Maps;
using DogeballVR.Xr;
using DogeballVR.Zones;

namespace DogeballVR;

/// <summary>
/// Scène de départ (étape « balle seule ») : origine XR, deux mains, un sol, une balle qui réapparaît.
/// Les zones, le réseau et les règles sont branchés ensuite.
/// </summary>
public partial class Main : Node3D
{
    PackedScene? ballScene;
    Ball? ball;
    double respawnTimer;

    public override void _Ready()
    {
        XrBootstrap.Start(GetViewport());

        var map = MapData.Load("res://maps/demo_2v2.json");
        var zones = new ZoneManager { Name = "ZoneManager" };
        AddChild(zones);
        zones.Setup(map, localZoneId: 0);
        zones.EdgeWarning += d => GD.Print($"Bord de zone à {d:0.00} m");

        // Joueur local : placé au centre de sa zone.
        var origin = new XROrigin3D { Name = "XROrigin3D", Position = map.Zones[0].Center };
        origin.AddChild(new XRCamera3D { Name = "XRCamera3D" });
        origin.AddChild(new HandThrower { Name = "LeftHand", Tracker = "left_hand" });
        origin.AddChild(new HandThrower { Name = "RightHand", Tracker = "right_hand" });
        AddChild(origin);

        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-60, 30, 0) });
        AddFloor();
        SpawnBall(map.Zones[0].Center + new Vector3(0, 1.4f, -1.0f));
    }

    void AddFloor()
    {
        var floor = new StaticBody3D { Name = "Floor" };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(60, 0.2f, 40) }, Position = new Vector3(0, -0.1f, 0) });
        floor.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(60, 0.2f, 40) }, Position = new Vector3(0, -0.1f, 0) });
        AddChild(floor);
    }

    void SpawnBall(Vector3 position)
    {
        ball?.QueueFree();
        ball = new Ball { Name = "Ball" };
        ball.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.11f } });
        ball.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.11f, Height = 0.22f } });
        AddChild(ball);
        ball.GlobalPosition = position;
    }

    public override void _Process(double delta)
    {
        // Banc d'essai : une balle neuve toutes les 8 s si la précédente est tombée loin.
        respawnTimer += delta;
        if (ball != null && ball.GlobalPosition.Y < -5f || respawnTimer > 30)
        {
            respawnTimer = 0;
            SpawnBall(new Vector3(-8, 1.4f, -6));
        }
    }
}
