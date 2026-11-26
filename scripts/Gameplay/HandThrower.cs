using System.Collections.Generic;
using Godot;

using DogeballVR.Net;

namespace DogeballVR.Gameplay;

/// <summary>
/// Une main : attrape (grip) et lance au geste du bras. Le geste est lu par <see cref="VrThrowGesture"/> : pic de vitesse de la main,
/// effet si le bras balaie de côté, cloche par en dessous, passe vers un allié, tir chargé gâchette tenue (docs/VR-GESTES.md).
/// À régler sur casque : c'est ici que se joue le plaisir du jeu.
/// </summary>
public partial class HandThrower : XRController3D
{
    const int HistorySize = 32;   // ~0,35 s à 90 images/s : la fenêtre de lecture du geste (0,15 s) y tient toujours

    /// <summary>Hurtbox du joueur (identité et état KO), fournie par la scène principale.</summary>
    public Hurtbox Hurtbox { get; set; } = null!;

    /// <summary>
    /// Vibration de la manette (OpenXR : action « haptic » de l'action map), à la force choisie par l'opérateur EVA et par le joueur (GameSettings.Haptics, 0 coupe tout).
    /// Alerte de sécurité (bord de zone) : jamais sous <see cref="UI.EvaSettings.SafetyFloor"/>, même vibrations coupées.
    /// </summary>
    public void Pulse(float amplitude, float seconds, bool safety = false)
    {
        float scale = UI.EvaSettings.Haptics * UI.GameSettings.Haptics;
        if (safety) scale = Mathf.Max(scale, UI.EvaSettings.SafetyFloor);
        float a = Mathf.Clamp(amplitude, 0f, 1f) * scale;
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
    /// <summary>Horloge des images (s) : celle des mesures de la main et de la charge (au casque, le temps réel).</summary>
    double physClock;
    /// <summary>Gâchette enfoncée (suivie par les boutons : une gâchette déjà serrée à la prise lance la charge).</summary>
    bool triggerDown;
    /// <summary>Gâchette tenue depuis (horloge physique, s), balle en main : tir chargé. -1 = pas de charge en cours.</summary>
    double chargeSince = -1;
    int chargeStep;
    MeshInstance3D? chargeRing;
    StandardMaterial3D? chargeMat;

    /// <summary>Dernier geste lu au lâcher (journal, tests au casque).</summary>
    public ThrowGesture LastGesture { get; private set; }

    readonly List<HandSample> history = new(HistorySize);

    /// <summary>Charge du tir en cours (0..1), 0 sans balle ou gâchette relâchée.</summary>
    public float Charge => chargeSince < 0 ? 0f : VrThrowGesture.ChargeFraction(physClock - chargeSince, KcRules.Def.VrGestures);

    public override void _Ready()
    {
        ButtonPressed += OnButtonPressed;
        ButtonReleased += OnButtonReleased;
        // Anneau de charge autour de la balle tenue (local : seul le lanceur le voit, les autres voient le tir partir).
        chargeMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        chargeRing = new MeshInstance3D
        {
            Name = "ChargeRing", TopLevel = true, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new TorusMesh { InnerRadius = 0.15f, OuterRadius = 0.17f, Rings = 24, RingSegments = 6 },
            MaterialOverride = chargeMat,
        };
        AddChild(chargeRing);
    }

    /// <summary>Essai sans casque (--vr-sim, <see cref="Xr.VrSimDriver"/>) : même chemin qu'un bouton de la manette.</summary>
    public void SimButton(string name, bool pressed) { if (pressed) OnButtonPressed(name); else OnButtonReleased(name); }

    /// <summary>Essai sans casque : la main prend cette balle comme par une attrape.</summary>
    public void SimCatch(Ball ball) { if (CanCatch) Catch(ball); }

    void OnButtonPressed(string name)
    {
        if (name == "trigger_click")
        {
            triggerDown = true;
            if (HeldBall != null || (BallActions.IsClient && Hurtbox.HeldHand == HandIndex)) { chargeSince = physClock; chargeStep = 0; }
            return;
        }
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
        if (name == "trigger_click") { triggerDown = false; chargeSince = -1; return; }   // gâchette relâchée avant le lancer : charge perdue (comme lâcher le clic sur PC)
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
        var def = KcRules.Def.VrGestures;
        var head = Hurtbox.Head != null ? Hurtbox.Head.GlobalPosition : GlobalPosition + Vector3.Up * 0.5f;
        var (allies, enemies) = Players();
        float maxSpeed = Assist.VrRules ? Assist.MaxLaunchSpeedVR : Assist.MaxLaunchSpeedOther;
        var g = VrThrowGesture.Read(history, GlobalPosition, head, Charge, allies, enemies, def, Assist.SpeedMultiplier, Assist.MinLaunchSpeed, maxSpeed);
        LastGesture = g;
        chargeSince = -1;
        if (g.Velocity.Length() >= VrThrowGesture.DropSpeed)   // rotation de la trajectoire journalisée : réglage de l'effet au casque
            GD.Print($"[VR] Lancer {g.Kind} à {g.Velocity.Length():0.0} m/s (rotation de la main {VrThrowGesture.TurnRate(history, def.VelocityWindowSeconds):0.0} rad/s)");
        if (held != null) BallActions.Throw(held, HandIndex, g.Velocity, g.Spin, g.Mode);
        else BallActions.Net!.RequestThrow(HandIndex, g.Velocity, g.Spin, g.Mode); // relâché avant la confirmation de la prise : l'hôte les traite dans l'ordre
        if (g.IsPower) Pulse(0.9f, 0.12f);
        // Nom du coup au-dessus de la main : on apprend les gestes en jouant.
        string? word = g.Velocity.Length() < VrThrowGesture.DropSpeed ? null : g.IsLob ? UI.L.T("CLOCHE !") : g.IsPass ? UI.L.T("PASSE !")
            : g.IsPower ? UI.L.T("TIR CHARGÉ !") : g.Spin > 0 ? UI.L.T("EFFET →") : g.Spin < 0 ? UI.L.T("← EFFET") : null;
        if (word != null) Xr.VrToast.AtHand(this, word, g.IsPower ? new Color(1f, 0.35f, 0.75f) : Visual.Palette.Team(Hurtbox.Team));
        ignoreHeldUntil = Time.GetTicksMsec() / 1000.0 + 0.4;
        held = null;
        Hurtbox.HeldHand = -1;
    }

    /// <summary>Torses des autres joueurs vivants : alliés (passe) et adversaires (cible de la cloche, passe refusée si un adversaire est dans l'axe).</summary>
    (List<Vector3> allies, List<Vector3> enemies) Players()
    {
        var allies = new List<Vector3>();
        var enemies = new List<Vector3>();
        foreach (var n in GetTree().GetNodesInGroup("hurtboxes"))
        {
            if (n is not Hurtbox h || h == Hurtbox || h.Eliminated || !IsInstanceValid(h)) continue;
            (h.Team == Hurtbox.Team ? allies : enemies).Add(h.GlobalPosition);
        }
        return (allies, enemies);
    }

    /// <summary>Charge en cours : vibration à chaque tiers, déclic net à pleine charge, anneau qui se referme sur la balle.</summary>
    void UpdateCharge(double now)
    {
        bool holding = HeldBall != null || (BallActions.IsClient && Hurtbox.HeldHand == HandIndex);
        if (!holding || Hurtbox.Eliminated) chargeSince = -1;
        else if (chargeSince < 0 && triggerDown) { chargeSince = physClock; chargeStep = 0; }   // gâchette déjà serrée à la prise
        float c = Charge;
        if (chargeSince >= 0)
        {
            int step = Mathf.FloorToInt(c * 3f + 0.001f);
            if (step > chargeStep)
            {
                chargeStep = step;
                Pulse(step >= 3 ? 0.7f : 0.25f, step >= 3 ? 0.08f : 0.03f);
                Audio.Sfx.Play(this, Audio.SfxKind.ChargeTick, GlobalPosition, 0.9f + 0.25f * step, step >= 3 ? 0f : -6f);
                if (step >= 3 && HandIndex == 1) GD.Print("[VR] Tir chargé prêt");
            }
        }
        if (chargeRing == null || chargeMat == null) return;
        var ball = HeldBall;
        chargeRing.Visible = chargeSince >= 0 && c > 0.05f;
        if (!chargeRing.Visible) return;
        var at = ball != null ? ball.GlobalPosition : GlobalPosition;
        var face = Hurtbox.Head != null ? (Hurtbox.Head.GlobalPosition - at) : Vector3.Back;
        var basis = face.LengthSquared() > 0.001f ? Basis.LookingAt(face.Normalized(), Mathf.Abs(face.Normalized().Y) > 0.95f ? Vector3.Forward : Vector3.Up) : Basis.Identity;
        float r = Mathf.Lerp(1.6f, 0.75f, c);   // l'anneau se resserre sur la balle
        chargeRing.GlobalTransform = new Transform3D(basis * new Basis(Vector3.Right, Mathf.Pi / 2f) * Basis.FromScale(Vector3.One * r), at);
        var full = new Color(1f, 0.35f, 0.75f);
        var col = c >= 0.999f ? full : Colors.White.Lerp(full, c * 0.6f);
        col.A = 0.35f + 0.6f * c;
        chargeMat.AlbedoColor = col;
    }

    /// <summary>
    /// Mesure de la main à chaque image (72 à 90 par seconde au casque, plus que les 60 images physiques) : position et, si le
    /// runtime la donne, vitesse. La vitesse du runtime est dans l'espace de suivi brut : on lui applique le recentrage
    /// (XRServer.ReferenceFrame), l'échelle et l'orientation de l'origine, comme Godot le fait pour la position.
    /// </summary>
    public override void _Process(double delta)
    {
        physClock += delta;
        var pose = GetPose();
        bool hasVel = pose != null && pose.HasTrackingData && pose.LinearVelocity.LengthSquared() > 1e-6f;
        var vel = Vector3.Zero;
        if (hasVel)
        {
            vel = XRServer.GetReferenceFrame().Basis * (pose!.LinearVelocity * (float)XRServer.WorldScale);
            if (GetParent() is Node3D origin) vel = origin.GlobalBasis * vel;
        }
        if (history.Count == HistorySize) history.RemoveAt(HistorySize - 1);
        history.Insert(0, new HandSample(physClock, GlobalPosition, vel, hasVel));
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

        UpdateCharge(now);
    }
}
