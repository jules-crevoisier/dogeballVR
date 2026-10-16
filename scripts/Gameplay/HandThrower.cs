using Godot;

using DogeballVR.Net;

namespace DogeballVR.Gameplay;

/// <summary>
/// Une main : attrape (grip) et lance. La vitesse du lancer vient du déplacement réel de la main
/// (moyenne sur les dernières frames), l'effet de la rotation du poignet.
/// À régler sur casque : c'est ici que se joue le plaisir du jeu.
/// </summary>
public partial class HandThrower : XRController3D
{
    const int HistorySize = 6;
    const double PerfectCatchWindow = 0.15; // secondes entre le grip et l'arrivée de la balle

    [Export] public float ThrowSpeedScale = 1.4f;
    [Export] public float MaxThrowSpeed = 30f;

    /// <summary>Hurtbox du joueur (identité et état KO), fournie par la scène principale.</summary>
    public Hurtbox Hurtbox { get; set; } = null!;

    int HandIndex => Tracker == "left_hand" ? 0 : 1;
    double ignoreHeldUntil;
    Area3D catchArea = null!;
    Ball? held;
    double lastGripPressTime = -10;
    bool gripDown;

    readonly Vector3[] posHistory = new Vector3[HistorySize];
    readonly double[] timeHistory = new double[HistorySize];
    readonly float[] yawHistory = new float[HistorySize];
    int historyCount;

    public override void _Ready()
    {
        catchArea = new Area3D { Name = "CatchArea" };
        var shape = new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.18f } };
        catchArea.AddChild(shape);
        AddChild(catchArea);
        catchArea.BodyEntered += OnBodyEntered;

        ButtonPressed += OnButtonPressed;
        ButtonReleased += OnButtonReleased;
    }

    void OnButtonPressed(string name)
    {
        if (name != "grip_click" || Hurtbox.Eliminated) return;
        gripDown = true;
        lastGripPressTime = Time.GetTicksMsec() / 1000.0;
        TryCatchOverlapping();
    }

    void OnButtonReleased(string name)
    {
        if (name != "grip_click") return;
        gripDown = false;
        if (held != null) ThrowHeld();
    }

    void OnBodyEntered(Node3D body)
    {
        if (body is Ball ball && gripDown && held == null && !ball.IsHeld && !Hurtbox.Eliminated) Catch(ball);
    }

    void TryCatchOverlapping()
    {
        foreach (var body in catchArea.GetOverlappingBodies())
            if (body is Ball ball && held == null && !ball.IsHeld) { Catch(ball); return; }
    }

    void Catch(Ball ball)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        bool perfect = now - lastGripPressTime <= PerfectCatchWindow && ball.LinearVelocity.Length() > 8f;
        if (!BallActions.IsClient) held = ball;
        BallActions.Catch(ball, this, HandIndex, Hurtbox.PlayerId, Hurtbox.Team);
        if (perfect)
            GD.Print("Attrapé parfait ! (ralenti + gain de vie à brancher)"); // TODO : Engine.TimeScale court, MatchRules.RegisterPerfectCatch
    }

    void ThrowHeld()
    {
        var (vel, spin) = EstimateHandMotion();
        vel = (vel * ThrowSpeedScale).LimitLength(MaxThrowSpeed);
        BallActions.Throw(held!, HandIndex, vel, spin);
        ignoreHeldUntil = Time.GetTicksMsec() / 1000.0 + 0.4;
        held = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (held != null && !IsInstanceValid(held)) held = null; // balle supprimée au changement de manche
        if (BallActions.IsClient && Time.GetTicksMsec() / 1000.0 >= ignoreHeldUntil)
        {
            // Client : la balle tenue est celle que l'hôte place dans notre main.
            var mine = BallActions.FindHeld(GetTree(), Hurtbox.PlayerId);
            held = mine != null && mine.GlobalPosition.DistanceTo(GlobalPosition) < 0.4f ? mine : null;
        }

        // Historique glissant de la position / rotation de la main.
        for (int i = HistorySize - 1; i > 0; i--)
        {
            posHistory[i] = posHistory[i - 1];
            timeHistory[i] = timeHistory[i - 1];
            yawHistory[i] = yawHistory[i - 1];
        }
        posHistory[0] = GlobalPosition;
        timeHistory[0] = Time.GetTicksMsec() / 1000.0;
        yawHistory[0] = GlobalRotation.Y;
        historyCount = Mathf.Min(historyCount + 1, HistorySize);
    }

    (Vector3 velocity, float spin) EstimateHandMotion()
    {
        if (historyCount < 2) return (Vector3.Zero, 0f);
        int last = historyCount - 1;
        double dt = timeHistory[0] - timeHistory[last];
        if (dt <= 0.0001) return (Vector3.Zero, 0f);
        var vel = (posHistory[0] - posHistory[last]) / (float)dt;
        float spin = Mathf.AngleDifference(yawHistory[last], yawHistory[0]) / (float)dt;
        return (vel, spin);
    }
}
