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

    /// <summary>Hurtbox du joueur (identité et état KO), fournie par la scène principale.</summary>
    public Hurtbox Hurtbox { get; set; } = null!;

    /// <summary>Vibration de la manette (OpenXR : action « haptic » de l'action map).</summary>
    /// <summary>Vibration de cette manette, à l'intensité réglée par le joueur (GameSettings.Haptics, 0 coupe tout).</summary>
    public void Pulse(float amplitude, float seconds)
    {
        float a = Mathf.Clamp(amplitude * UI.GameSettings.Haptics, 0f, 1f);
        if (a > 0f) TriggerHapticPulse("haptic", 0f, a, seconds, 0f);
    }

    public Ball? HeldBall => held != null && IsInstanceValid(held) ? held : null;
    int HandIndex => Tracker == "left_hand" ? 0 : 1;
    double ignoreHeldUntil;
    double catchRequestedAt = -10;
    Ball? held;
    double lastGripPressTime = -10;
    double seenLockUntil = double.NegativeInfinity;
    bool gripDown;

    readonly Vector3[] posHistory = new Vector3[HistorySize];
    readonly double[] timeHistory = new double[HistorySize];
    readonly float[] yawHistory = new float[HistorySize];
    int historyCount;

    public override void _Ready()
    {
        ButtonPressed += OnButtonPressed;
        ButtonReleased += OnButtonReleased;
    }

    void OnButtonPressed(string name)
    {
        if (name != "grip_click" || Hurtbox.Eliminated) return;
        gripDown = true;
        double now = Time.GetTicksMsec() / 1000.0;
        // Blocage après une attrape ratée (tenu par le joueur, pour les deux mains) : l'appui ne compte pas, la main reste fermée sans prendre.
        if (!KcRules.PressGrip(Hurtbox, GlobalPosition, now)) { Pulse(0.15f, 0.05f); return; }
        lastGripPressTime = now;
        if (BallActions.IsClient) BallActions.Net!.RequestGrip(HandIndex);   // l'hôte tient le même blocage en miroir
    }

    void OnButtonReleased(string name)
    {
        if (name != "grip_click") return;
        gripDown = false;
        if (held != null || (BallActions.IsClient && Hurtbox.HeldHand == HandIndex)) ThrowHeld();
    }

    /// <summary>Une seule balle par joueur, et pas de prise tant que l'hôte n'a pas confirmé la précédente.</summary>
    bool CanCatch => held == null && Hurtbox.HeldHand < 0 && !Hurtbox.Eliminated && !Hurtbox.CatchLock.Locked(Time.GetTicksMsec() / 1000.0);

    /// <summary>Une passe adverse s'intercepte par n'importe quelle attrape (décision n° 3, réglable : interceptPasses).</summary>
    bool Catchable(Ball ball) => !ball.IsHeld && !ball.HasExploded && KcRules.CanTake(ball.IsPass, ball.OwnerTeam, Hurtbox.Team);

    static bool Hostile(Ball ball, int team) => ball.OwnerTeam >= 0 && ball.OwnerTeam != team;

    /// <summary>
    /// Prise par balayage : la fenêtre de grip (pressé il y a moins de GripWindowSeconds, main encore fermée) ouvre la main ; une balle dont
    /// le trajet du tick passe dans le volume de prise (ou dans l'aimant si elle s'approche) est attrapée. Une main fermée en permanence n'attrape rien.
    /// Une balle adverse à palier demande un appui plus tardif : sa fenêtre se resserre (Assist.CatchWindow, 0,35 → 0,25 s au palier 3).
    /// </summary>
    void TryCatchSweep(double now, double delta)
    {
        TryParry(now, delta);
        if (!gripDown || !CanCatch || !Assist.GripValid(now, lastGripPressTime)) return;
        var hand = GlobalPosition;
        foreach (var n in GetTree().GetNodesInGroup("balls"))
        {
            if (n is not Ball ball || !Catchable(ball)) continue;
            if (!Assist.GripValid(now, lastGripPressTime, Assist.CatchWindow(ball.Charge, Hostile(ball, Hurtbox.Team)))) continue;
            var now3 = ball.GlobalPosition;
            if (!Assist.Contact(now3 - ball.LinearVelocity * (float)delta, now3, hand)) continue;
            Catch(ball);
            return;
        }
    }

    double nextParry;

    /// <summary>
    /// Parade de la paume (palier 0 seulement) : main ouverte, vide, levée devant la poitrine, une balle chargée de rien qui arrive à moins de 0,3 m est déviée.
    /// Une seule parade par seconde et demie et par main : pas de bouclier permanent. La normale de déviation est l'axe main vers balle
    /// (l'axe de la paume dépend du modèle de manette, à confirmer au casque).
    /// </summary>
    void TryParry(double now, double delta)
    {
        if (gripDown || held != null || Hurtbox.HeldHand >= 0 || Hurtbox.Eliminated || now < nextParry) return;
        var head = Hurtbox.Head;
        if (head == null || GlobalPosition.Y < head.GlobalPosition.Y - 0.55f || GlobalPosition.Y > head.GlobalPosition.Y + 0.1f) return;
        var hand = GlobalPosition;
        if (head.GlobalPosition.DistanceTo(hand) < 0.25f || head.GlobalPosition.DistanceTo(hand) > 0.8f) return;
        foreach (var n in GetTree().GetNodesInGroup("balls"))
        {
            if (n is not Ball ball || ball.IsHeld || ball.HasExploded || ball.Charge > 0 || ball.IsPass || ball.OwnerTeam < 0 || ball.OwnerTeam == Hurtbox.Team) continue;
            var pos = ball.GlobalPosition;
            if (ball.LinearVelocity.Length() < Ball.HitMinSpeed || !Assist.Contact(pos - ball.LinearVelocity * (float)delta, pos, hand)) continue;
            nextParry = now + 1.5;
            BallActions.Parry(ball, HandIndex, (pos - hand).Normalized());
            Pulse(0.8f, 0.1f);
            Audio.Sfx.Play(this, Audio.SfxKind.Parry, pos);   // la parade était muette
            return;
        }
    }

    void Catch(Ball ball)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        bool perfect = now - lastGripPressTime <= Assist.PerfectWindowSeconds && Hostile(ball, Hurtbox.Team)
            && (ball.ThrowMode == 0 || (ball.IsPass && KcRules.PerfectOnPass)) && Assist.PerfectAllowed(ball.LinearVelocity.Length(), ball.SpeedCap);
        if (!BallActions.IsClient) held = ball;
        Hurtbox.HeldHand = HandIndex;
        Hurtbox.CatchLock.Caught();
        catchRequestedAt = now;
        BallActions.Catch(ball, this, HandIndex, Hurtbox.PlayerId, Hurtbox.Team, perfect);
        if (perfect) GD.Print("[VR] Attrape parfaite demandée");
    }

    void ThrowHeld()
    {
        var (vel, spin) = EstimateHandMotion();
        vel = (vel * Assist.SpeedMultiplier).LimitLength(Assist.VrRules ? Assist.MaxLaunchSpeedVR : Assist.MaxLaunchSpeedOther);
        // Vrai lancer (mouvement net) : vitesse minimale de la map, pour qu'un geste mou parte quand même tendu (l'hôte replanifie, voir Assist.Plan).
        if (vel.Length() > 1.5f && vel.Length() < Assist.MinLaunchSpeed) vel = vel.Normalized() * Assist.MinLaunchSpeed;
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
                else if (now > catchRequestedAt + 0.3) { held = null; Hurtbox.HeldHand = -1; } // refusée ou perdue (KO, manche)
            }
        }
        else if (held != null && Hurtbox.Eliminated)
        {
            held.Release(Vector3.Zero, 0f);
            held = null;
        }
        if (held == null && !BallActions.IsClient && Hurtbox.HeldHand == HandIndex) Hurtbox.HeldHand = -1;

        // Fenêtre écoulée sans prise après une vraie tentative : 0,6 s de blocage, vibration « raté » sur chaque main.
        Hurtbox.CatchLock.Update(now);
        if (Hurtbox.CatchLock.LockedUntil != seenLockUntil && Hurtbox.CatchLock.Locked(now))
        {
            seenLockUntil = Hurtbox.CatchLock.LockedUntil;
            Pulse(0.45f, 0.12f);
            if (HandIndex == 1) GD.Print($"[VR] Attrape ratée : blocage {KcRules.Def.VrMissedCatchLockSeconds:0.##} s");
        }

        TryCatchSweep(now, delta);

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
