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

    public static Rotor Create(HubDef hub, bool kc = false)
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
        if (kc)
        {
            // bras en tôle bleu acier ; rayures de danger seulement aux extrémités (lisibles sans saturer l'écran de près)
            var body = KcShaders.Toon(new Color("3A5A7A"), new Color("2C4662"), 6);
            body.NextPass = KcShaders.Outline(0.03f);
            rotor.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = body });
            const float stripe = 1.1f;
            foreach (float side in new[] { -1f, 1f })
                rotor.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(stripe, size.Y + 0.02f, size.Z + 0.02f) },
                    Position = new Vector3(side * (r.ArmLength - 0.18f - stripe / 2f), 0, 0),
                    MaterialOverride = KcShaders.Toon(new Color("F2C230"), new Color("1A1C26"), 4, 1f),
                });
            foreach (float y in new[] { -1f, 1f })   // liserés cyan le long des arêtes haute et basse
                rotor.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(size.X - 0.4f, 0.05f, size.Z + 0.03f) },
                    Position = new Vector3(0, y * (size.Y / 2f - 0.12f), 0),
                    MaterialOverride = KcShaders.Toon(new Color("7FE8FF"), new Color("7FE8FF"), 0, 1f, 2.0f),
                });
            // arbre central sombre et pointes lumineuses : on lit le sens de rotation de loin
            var hubMat = KcShaders.Toon(new Color("2B3658"), null, 0);
            hubMat.NextPass = KcShaders.Outline(0.03f);
            rotor.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.6f, Height = size.Y + 0.15f }, MaterialOverride = hubMat });
            foreach (float side in new[] { -1f, 1f })
                rotor.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.18f, size.Y + 0.06f, size.Z + 0.06f) },
                    Position = new Vector3(side * (r.ArmLength - 0.09f), 0, 0),
                    MaterialOverride = KcShaders.Toon(new Color("FF6A1A"), new Color("FF6A1A"), 0, 1f, 2.5f),
                });
        }
        else rotor.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = Materials.Flat(new Color("b7bcc8")) });
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

/// <summary>
/// Test headless (--rotor-test) : toutes les 0,4 s une balle tendue part à 22 m/s vers le moyeu, à 1,5 m de l'axe (hors du pylône : seul un bras
/// peut la gêner). 1,3 s plus tard on la juge : passée si elle est de l'autre côté du moyeu, arrêtée ou renvoyée sinon. Avec --rotor-off le rotor
/// n'a plus de collision : le test doit alors compter 0 balle arrêtée.
/// </summary>
public partial class RotorBallTest : Node
{
    readonly System.Collections.Generic.List<(Gameplay.Ball ball, double at)> shots = new();
    double timer, clock;
    int fired, stopped, passed;
    bool reported;

    public override void _Ready()
    {
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--rotor-off") >= 0 && Rotor.Current != null) Rotor.Current.CollisionLayer = 0;
    }

    public override void _PhysicsProcess(double delta)
    {
        var rotor = Rotor.Current;
        if (rotor == null || reported) return;
        clock += delta; timer += delta;
        var c = rotor.GlobalPosition;
        if (fired < 30 && timer >= 0.4)
        {
            timer = 0;
            var ball = Gameplay.Ball.Create();
            GetTree().CurrentScene.AddChild(ball);
            ball.GlobalPosition = new Vector3(c.X + 7f, 1.8f, c.Z + (fired % 2 == 0 ? 1.5f : -1.5f));
            ball.GravityScale = 0f;
            ball.LinearVelocity = new Vector3(-22f, 0, 0);
            shots.Add((ball, clock));
            fired++;
        }
        for (int i = shots.Count - 1; i >= 0; i--)
        {
            var (b, at) = shots[i];
            if (clock - at < 1.3) continue;
            if (b.GlobalPosition.X < c.X - 1.0f) passed++; else stopped++;
            b.QueueFree();
            shots.RemoveAt(i);
        }
        if (fired >= 30 && shots.Count == 0)
        {
            reported = true;
            GD.Print($"[Rotor] test balles : {stopped} arrêtées ou renvoyées par un bras, {passed} passées");
        }
    }
}
