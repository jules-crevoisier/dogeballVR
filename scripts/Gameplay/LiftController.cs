using System;
using Godot;
using DogeballVR.Maps;

namespace DogeballVR.Gameplay;

/// <summary>
/// Lift d'une zone (maps à niveaux) : debout sur le pad puis accroupi pendant 1 s, fondu de 0,4 s, et tout le plateau
/// de la zone monte. Au maximum MaxHighSeconds (6 s sur La Fosse) en haut, retour automatique, puis recharge. Verrouillé si le joueur tient une balle
/// chargée, et un joueur éliminé redescend. Aucune locomotion au joystick : on monte dans un cadre fixe (le pad).
/// </summary>
public partial class LiftController : Node
{
    enum State { Low, FadingUp, High, FadingDown }

    public LiftDef Lift { get; set; } = null!;
    public LiftRulesDef Rules { get; set; } = new();
    /// <summary>Position de la tête dans le monde.</summary>
    public Func<Vector3> HeadPosition { get; set; } = null!;
    /// <summary>Hauteur de la tête au-dessus des pieds (détecte l'accroupi).</summary>
    public Func<float> HeadHeight { get; set; } = null!;
    public Func<bool> Locked { get; set; } = () => false;
    public Func<bool> Eliminated { get; set; } = () => false;
    /// <summary>Applique l'altitude (déplace le joueur et son plateau).</summary>
    public Action<float> Apply { get; set; } = _ => { };
    /// <summary>Opacité du fondu au noir (0 à 1).</summary>
    public Action<float> Fade { get; set; } = _ => { };

    public float Elevation { get; private set; }
    /// <summary>L'hôte a refusé ou repris l'altitude : on redescend.</summary>
    public void ForceDown()
    {
        if (state == State.High) StartMove(State.FadingDown);
    }

    public bool IsHigh => state is State.High or State.FadingDown;

    /// <summary>Hauteur de tête debout supposée avant la première mesure (adulte moyen) : seuil d'accroupi 1,15 m.</summary>
    const float DefaultStanding = 1.6f;

    /// <summary>
    /// Seuil d'accroupi relatif à la taille du joueur (72 % de sa hauteur de tête debout, entre 0,6 et 1,2 m) : avec un seuil fixe
    /// de 1,15 m, un enfant déclenchait le lift debout et un grand joueur devait s'accroupir très bas. Plafond 1,2 m : l'hôte
    /// refuse un accroupi au-dessus de 1,25 m (NetSession).
    /// </summary>
    public static float CrouchThreshold(float standingHeadHeight) => Mathf.Clamp(0.72f * standingHeadHeight, 0.6f, 1.2f);

    float standing = DefaultStanding;
    bool measured;
    State state = State.Low;
    double holdTimer, stateTimer, cooldown;
    bool applied;

    public override void _PhysicsProcess(double delta)
    {
        double dt = delta;
        switch (state)
        {
            case State.Low:
                cooldown -= dt;
                bool onPad = new Vector2(HeadPosition().X - Lift.Pad.X, HeadPosition().Z - Lift.Pad.Z).Length() < Lift.Pad.Radius;
                float head = HeadHeight();
                // Hauteur debout = la plus haute tête vue (la première mesure remplace la valeur par défaut).
                if (head > 0.5f && (!measured || head > standing)) { standing = Mathf.Min(head, 2.1f); measured = true; }
                bool ready = cooldown <= 0 && onPad && head < CrouchThreshold(standing) && !Locked() && !Eliminated();
                holdTimer = ready ? holdTimer + dt : 0;
                if (holdTimer >= Rules.HoldCrouchSeconds) StartMove(State.FadingUp);
                break;
            case State.FadingUp:
            case State.FadingDown:
                Move(dt);
                break;
            case State.High:
                stateTimer += dt;
                bool forced = Rules.DownOnElimination && Eliminated();
                if (stateTimer >= Lift.MaxHighSeconds || forced) StartMove(State.FadingDown);
                break;
        }
    }

    void StartMove(State next)
    {
        state = next;
        stateTimer = 0;
        holdTimer = 0;
        applied = false;
    }

    /// <summary>Fondu au noir, changement d'altitude au milieu, retour de la vue.</summary>
    void Move(double dt)
    {
        stateTimer += dt;
        float half = Mathf.Max(Lift.FadeSeconds, 0.05f) * 0.5f;
        float t = (float)(stateTimer / half);
        Fade(Mathf.Clamp(t <= 1f ? t : 2f - t, 0f, 1f));
        if (!applied && stateTimer >= half)
        {
            applied = true;
            Elevation = state == State.FadingUp ? Lift.HighElevation : 0f;
            Apply(Elevation);
        }
        if (stateTimer < half * 2) return;
        Fade(0f);
        if (state == State.FadingUp) { state = State.High; stateTimer = 0; }
        else { state = State.Low; cooldown = Lift.CooldownSeconds; }
    }
}
