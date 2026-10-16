using Godot;
using DogeballVR.Visual;

namespace DogeballVR.Maps;

/// <summary>
/// Rotor du moyeu : deux bras de 4 m (une barre de 8 m) qui tournent entre 0,6 et 3 m de haut. Il dévie les balles tendues
/// (corps animé, physique de l'hôte) et repousse sans dégât les joueurs PC. L'hôte (ou le jeu hors ligne) le fait tourner ;
/// un client avance de son côté à la même vitesse et se recale sur l'angle de chaque snapshot.
/// </summary>
public partial class Rotor : AnimatableBody3D
{
    /// <summary>Le rotor de l'arène courante (un seul par scène).</summary>
    public static Rotor? Current { get; private set; }

    public float ArmLength { get; private set; }
    public float Thickness { get; private set; }
    public float YMin { get; private set; }
    public float YMax { get; private set; }
    public float DegPerSecond { get; private set; }
    /// <summary>Faux sur un client : l'angle vient des snapshots.</summary>
    public bool Authority { get; set; } = true;

    /// <summary>Angle autour de Y (radians), repère de Godot.</summary>
    public float Angle { get; private set; }
    float target;
    bool hasTarget;

    public static Rotor Create(HubDef hub)
    {
        var r = hub.Rotor!;
        var rotor = new Rotor
        {
            Name = "Rotor",
            Position = new Vector3(hub.X, (r.YMin + r.YMax) / 2f, hub.Z),
            SyncToPhysics = true,
            ArmLength = r.ArmLength, Thickness = r.Thickness, YMin = r.YMin, YMax = r.YMax, DegPerSecond = r.DegPerSecond,
        };
        var size = new Vector3(r.ArmLength * 2f, r.YMax - r.YMin, r.Thickness);
        rotor.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = Materials.Flat(new Color("b7bcc8")) });
        rotor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        return rotor;
    }

    public override void _Ready()
    {
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--rotor-test") >= 0) AddChild(new RotorBallTest());
    }

    public override void _EnterTree() => Current = this;
    public override void _ExitTree() { if (Current == this) Current = null; }

    /// <summary>Client : angle reçu de l'hôte.</summary>
    public void SetRemoteAngle(float angle) { target = angle; hasTarget = true; }

    public override void _PhysicsProcess(double delta)
    {
        float step = Mathf.DegToRad(DegPerSecond) * (float)delta;
        if (Authority) Angle = Mathf.Wrap(Angle + step, 0f, Mathf.Tau);
        else
        {
            Angle = Mathf.Wrap(Angle + step, 0f, Mathf.Tau);
            if (hasTarget)
            {
                float err = Mathf.AngleDifference(Angle, target);
                Angle = Mathf.Wrap(Angle + err * 0.2f, 0f, Mathf.Tau);   // recalage doux entre deux snapshots
                hasTarget = false;
            }
        }
        Rotation = new Vector3(0, Angle, 0);
    }
}

/// <summary>Test headless (--rotor-test) : des balles tendues visent le moyeu à hauteur de bras ; on compte celles que le rotor arrête.</summary>
public partial class RotorBallTest : Node
{
    readonly System.Collections.Generic.List<(Gameplay.Ball ball, float angle)> shots = new();
    double timer;
    int fired;
    bool reported;

    public override void _PhysicsProcess(double delta)
    {
        var rotor = Rotor.Current;
        if (rotor == null || reported) return;
        timer += delta;
        if (fired < 30 && timer >= 0.4)
        {
            timer = 0; fired++;
            var ball = Gameplay.Ball.Create();
            GetTree().CurrentScene.AddChild(ball);
            var c = rotor.GlobalPosition;
            ball.GlobalPosition = new Vector3(c.X + 7f, 1.8f, c.Z + 0.6f);
            ball.GravityScale = 0f;
            ball.LinearVelocity = new Vector3(-22f, 0, 0);
            shots.Add((ball, rotor.Angle));
        }
        if (fired >= 30 && timer > 1.0)
        {
            reported = true;
            int stopped = 0, passed = 0;
            foreach (var (b, _) in shots)
            {
                float rel = b.GlobalPosition.X - rotor.GlobalPosition.X;
                if (rel > 3.2f) stopped++; else passed++;
            }
            GD.Print($"[Rotor] test balles : {stopped} arrêtées ou renvoyées par un bras, {passed} passées");
        }
    }
}
