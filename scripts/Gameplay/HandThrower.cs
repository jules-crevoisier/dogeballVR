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
    const int HistorySize = 4;   // fenêtre courte : une moyenne trop longue mélange armé du bras et lâcher (lancers mous)
    const double PerfectCatchWindow = 0.15; // secondes entre le grip et l'arrivée de la balle

    [Export] public float ThrowSpeedScale = 1.4f;
    [Export] public float MaxThrowSpeed = 30f;

    /// <summary>Hurtbox du joueur (identité et état KO), fournie par la scène principale.</summary>
    public Hurtbox Hurtbox { get; set; } = null!;

    int HandIndex => Tracker == "left_hand" ? 0 : 1;
    double ignoreHeldUntil;
    double catchRequestedAt = -10;
    double nextRetry;
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
        if (held != null || (BallActions.IsClient && Hurtbox.HeldHand == HandIndex)) ThrowHeld();
    }

    void OnBodyEntered(Node3D body)
    {
        // Différé : Hold change l'état physique de la balle, interdit pendant le traitement des collisions.
        if (body is Ball ball && gripDown && CanCatch && !ball.IsHeld) CallDeferred(MethodName.CatchDeferred, ball);
    }

    /// <summary>Une seule balle par joueur, et pas de prise tant que l'hôte n'a pas confirmé la précédente.</summary>
    bool CanCatch => held == null && Hurtbox.HeldHand < 0 && !Hurtbox.Eliminated;

    void CatchDeferred(Ball ball)
    {
        if (IsInstanceValid(ball) && gripDown && CanCatch && !ball.IsHeld) Catch(ball);
    }

    void TryCatchOverlapping()
    {
        if (!CanCatch) return;
        foreach (var body in catchArea.GetOverlappingBodies())
            if (body is Ball ball && !ball.IsHeld) { Catch(ball); return; }
    }

    void Catch(Ball ball)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        bool perfect = now - lastGripPressTime <= PerfectCatchWindow && ball.LinearVelocity.Length() > 8f;
        if (!BallActions.IsClient) held = ball;
        Hurtbox.HeldHand = HandIndex;
        catchRequestedAt = now;
        BallActions.Catch(ball, this, HandIndex, Hurtbox.PlayerId, Hurtbox.Team);
        if (perfect)
            GD.Print("Attrapé parfait ! (ralenti + gain de vie à brancher)"); // TODO : Engine.TimeScale court, MatchRules.RegisterPerfectCatch
    }

    void ThrowHeld()
    {
        var (vel, spin) = EstimateHandMotion();
        vel = (vel * ThrowSpeedScale).LimitLength(MaxThrowSpeed);
        if (held != null) BallActions.Throw(held, HandIndex, vel, spin);
        else BallActions.Net!.RequestThrow(HandIndex, vel, spin); // relâché avant la confirmation de la prise : l'hôte les traite dans l'ordre
        ignoreHeldUntil = Time.GetTicksMsec() / 1000.0 + 0.4;
        held = null;
        Hurtbox.HeldHand = -1;
    }

    public override void _PhysicsProcess(double delta)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        if (held != null && !IsInstanceValid(held)) held = null; // balle supprimée au changement de manche
        if (BallActions.IsClient)
        {
            // Client : la balle tenue est celle que l'hôte place dans notre main (même en retard sur la main : pas de test de distance).
            if (Hurtbox.HeldHand == HandIndex)
            {
                var mine = BallActions.FindHeld(GetTree(), Hurtbox.PlayerId);
                if (mine != null) held = mine;
                else if (now > catchRequestedAt + 0.6) { held = null; Hurtbox.HeldHand = -1; } // refusée ou perdue (KO, manche)
            }
        }
        else if (held != null && Hurtbox.Eliminated)
        {
            held.Release(Vector3.Zero, 0f);
            held = null;
        }
        if (held == null && !BallActions.IsClient && Hurtbox.HeldHand == HandIndex) Hurtbox.HeldHand = -1;

        // Grip serré sans balle en main : on retente régulièrement (une balle déjà dans la zone n'« entre » plus).
        if (gripDown && now >= nextRetry) { nextRetry = now + 0.25; TryCatchOverlapping(); }

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
