using System.Collections.Generic;
using Godot;
using DogeballVR.Maps;
using DogeballVR.Visual;
using DogeballVR.Zones;

namespace DogeballVR.Gameplay;

/// <summary>
/// Bot pour compléter les équipes (solo et places vides en ligne), en trois niveaux (<see cref="Level"/>).
/// Il lit les tirs qui arrivent : il décide une fois par balle d'attraper (parfois parfaitement, au dernier moment), d'esquiver
/// d'un pas de côté ou de prendre le coup. Balle en main, il arme un élan visible avant de tirer, fait des passes pour charger
/// la balle (palier 1 au plus, comme les passes des joueurs) et feinte pour piéger l'attrape adverse.
/// </summary>
public partial class Bot : Node3D
{
    const float Speed = 3.0f;
    const float CatchRadius = 1.6f;

    /// <summary>Niveau des bots : 0 facile, 1 normal, 2 difficile. Posé par Main depuis les options (l'hôte décide en ligne).</summary>
    public static int Level { get; set; } = 1;

    /// <summary>Ce qui change d'un niveau à l'autre.</summary>
    /// <param name="Catch">chance d'attraper un tir adverse rapide</param>
    /// <param name="Perfect">part des attrapes réussies qui sont parfaites</param>
    /// <param name="Dodge">chance d'esquiver d'un pas de côté un tir qu'il n'attrape pas</param>
    /// <param name="Pass">chance de passer une balle non chargée à un allié avant de tirer</param>
    /// <param name="Feint">chance de feinter avant le vrai tir</param>
    /// <param name="Think">délai entre la prise et le tir (s)</param>
    /// <param name="Windup">élan visible avant que la balle parte (s)</param>
    /// <param name="Lead">part de l'anticipation du déplacement de la cible</param>
    public sealed record Skill(string Name, float Catch, float Perfect, float Dodge, float Pass, float Feint,
        float MinSpeed, float MaxSpeed, Vector2 Think, float Windup, float Lead, float Aim);

    public static readonly Skill[] Skills =
    {
        new("Facile", Catch: 0.2f, Perfect: 0f, Dodge: 0.15f, Pass: 0.05f, Feint: 0f, MinSpeed: 13f, MaxSpeed: 18f, Think: new(0.9f, 1.6f), Windup: 0.4f, Lead: 0.3f, Aim: 0.6f),
        new("Normal", Catch: 0.4f, Perfect: 0.15f, Dodge: 0.35f, Pass: 0.2f, Feint: 0.12f, MinSpeed: 16f, MaxSpeed: 24f, Think: new(0.4f, 0.9f), Windup: 0.3f, Lead: 0.8f, Aim: 0.25f),
        new("Difficile", Catch: 0.65f, Perfect: 0.4f, Dodge: 0.6f, Pass: 0.35f, Feint: 0.25f, MinSpeed: 20f, MaxSpeed: 28f, Think: new(0.25f, 0.6f), Windup: 0.25f, Lead: 1f, Aim: 0.1f),
    };

    static Skill Current => Skills[Mathf.Clamp(Level, 0, Skills.Length - 1)];

    // Compteurs de tous les bots depuis la dernière fin de manche (journal, pour régler les niveaux).
    static int nCatches, nPerfects, nMissed, nDodges, nPasses, nFeints, nThrows, nPunish;

    /// <summary>Ligne de journal de fin de manche, puis remise à zéro des compteurs.</summary>
    public static string Report()
    {
        var line = $"niveau {Current.Name} : {nThrows} tirs, {nPasses} passes, {nFeints} feintes, {nCatches} attrapes de tirs ({nPerfects} parfaites), {nMissed} ratées, {nDodges} esquives, {nPunish} punitions";
        nCatches = nPerfects = nMissed = nDodges = nPasses = nFeints = nThrows = nPunish = 0;
        return line;
    }

    /// <summary>Entraînement (tutoriel) : les bots attendent plus longtemps et lancent moins fort, pour qu'on apprenne à attraper.</summary>
    public static bool Gentle { get; set; }
    /// <summary>Entraînement, premières étapes (regarder, bouger, ramasser) : les bots ne lancent pas du tout.</summary>
    public static bool Calm { get; set; }
    /// <summary>Vrai tant que les lancers sont interdits (compte à rebours de manche) ; posé par Main.</summary>
    public System.Func<bool>? HoldFire { get; set; }
    /// <summary>Le joueur local vient-il de rater une attrape (bouton bloqué) ? Posé par Main ; entre bots, on le sait directement.</summary>
    public static System.Func<Node3D, bool>? ArmsLocked { get; set; }

    public ZoneDef Zone { get; set; } = null!;
    public float ZoneWidth { get; set; } = 15f;
    public float ZoneDepth { get; set; } = 8f;
    public Hurtbox Hurtbox { get; set; } = null!;
    public Avatar? Avatar { get; private set; }
    /// <summary>Positions des autres joueurs (visées).</summary>
    public IList<Node3D> Targets { get; set; } = new List<Node3D>();
    public IList<int> TargetTeams { get; set; } = new List<int>();

    Node3D holdPoint = null!;
    Ball? held;
    double throwDelay;
    double catchCooldown; // évite de rattraper aussitôt la balle qu'on vient de lancer
    double whiffLeft;     // attrape ratée : bras pris, c'est le moment de le punir
    float targetX;
    double changeDirTimer;
    readonly RandomNumberGenerator rng = new();

    // Tir en préparation : élan en cours (la balle part à la fin), feinte déjà faite pour cette balle.
    double windupLeft = -1;
    Node3D? windupTarget;
    bool windupPass, feinted;
    // Lecture des tirs : décision prise une fois par balle et par vol (attraper, parfaite ou non, esquiver).
    readonly Dictionary<Ball, (bool take, bool perfect, bool dodge)> reads = new();
    double dodgeLeft;
    float dodgeDir;

    public override void _Ready()
    {
        rng.Randomize();
        holdPoint = new Node3D { Name = "HoldPoint", Position = new Vector3(0, 1.4f, 0) };
        AddChild(holdPoint);
        Avatar = Avatar.Create(Hurtbox.Team, bot: true, Hurtbox.PlayerId);
        AddChild(Avatar);
        Hurtbox.Position = new Vector3(0, 0.85f, 0);
        AddChild(Hurtbox);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Hurtbox.Eliminated) { DropHeld(); Visible = false; return; }
        Visible = true;
        if (held != null && !IsInstanceValid(held)) { held = null; windupLeft = -1; } // balle supprimée au changement de manche
        catchCooldown -= delta;
        whiffLeft -= delta;
        TrackTargets(delta);
        ReadThreats();
        Dodge(delta);
        FaceNearestEnemy(delta);
        if (held == null && catchCooldown <= 0) TryCatch();
        else ThinkThrow(delta);
    }

    /// <summary>Le bot disparaît (remplacé par un joueur) : il lâche sa balle au lieu de l'emporter.</summary>
    public void DropHeld()
    {
        if (held != null && IsInstanceValid(held)) held.Release(Vector3.Zero, 0f);
        held = null;
        windupLeft = -1;
    }

    Vector3 Chest => GlobalPosition + Vector3.Up * 0.8f;

    /// <summary>Tir adverse rapide qui arrive sur le bot : temps avant l'impact (s), sinon -1.</summary>
    float IncomingIn(Ball ball)
    {
        if (ball.IsHeld || ball.OwnerTeam < 0 || ball.OwnerTeam == Hurtbox.Team) return -1;
        var v = ball.LinearVelocity;
        float speed = v.Length();
        if (speed < 8f) return -1;
        var to = Chest - ball.GlobalPosition;
        float along = to.Dot(v / speed);
        if (along <= 0) return -1;
        // Passe-t-elle près de nous ? Écart à la trajectoire au moment le plus proche.
        if ((to - v / speed * along).Length() > 1.4f) return -1;
        return along / speed;
    }

    /// <summary>Une décision par tir adverse dès qu'il est à moins de 0,7 s : attraper (parfaite ou non), esquiver ou encaisser.</summary>
    void ReadThreats()
    {
        var gone = new List<Ball>();
        foreach (var b in reads.Keys) if (!IsInstanceValid(b) || b.IsHeld || b.LinearVelocity.Length() < 8f) gone.Add(b);
        foreach (var b in gone) reads.Remove(b);
        if (Calm) return;
        foreach (var node in GetTree().GetNodesInGroup("balls"))
        {
            if (node is not Ball ball || reads.ContainsKey(ball)) continue;
            float t = IncomingIn(ball);
            if (t < 0 || t > 0.7f) continue;
            var s = Current;
            float catchChance = Gentle ? 0.15f : s.Catch;
            // Mains prises (balle tenue) : pas d'attrape possible, seulement l'esquive.
            bool take = held == null && rng.Randf() < catchChance;
            bool perfect = take && rng.Randf() < s.Perfect;
            bool dodge = !take && rng.Randf() < s.Dodge;
            reads[ball] = (take, perfect, dodge);
            if (dodge) { StartDodge(ball); nDodges++; }
        }
    }

    /// <summary>Pas de côté, perpendiculaire au tir, vers le côté le plus libre de la zone.</summary>
    void StartDodge(Ball ball)
    {
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(Zone.FacingDegrees));
        var side = (basis.Inverse() * ball.LinearVelocity).Cross(Vector3.Up);
        float local = (basis.Inverse() * (GlobalPosition - Zone.Center)).X;
        dodgeDir = Mathf.Abs(side.X) > 0.1f ? Mathf.Sign(side.X) : (local > 0 ? -1f : 1f);
        if (Mathf.Abs(local + dodgeDir * 1.5f) > ZoneWidth * 0.5f - 0.5f) dodgeDir = -dodgeDir;   // pas de pas vers le mur
        dodgeLeft = 0.35;
        Avatar?.PlayAction("Esquive", 0.4);
    }

    void Dodge(double delta)
    {
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(Zone.FacingDegrees));
        var local = basis.Inverse() * (GlobalPosition - Zone.Center);
        float limitX = ZoneWidth * 0.5f - 0.5f;
        float limitZ = ZoneDepth * 0.5f - 0.5f;

        // Sans balle en main : aller chercher la balle libre la plus proche qui est dans sa zone.
        Vector3? goal = null;
        if (held == null)
        {
            float best = float.MaxValue;
            foreach (var node in GetTree().GetNodesInGroup("balls"))
            {
                if (node is not Ball { IsHeld: false } ball) continue;
                var bl = basis.Inverse() * (ball.GlobalPosition - Zone.Center);
                if (Mathf.Abs(bl.X) > limitX + 0.5f || Mathf.Abs(bl.Z) > limitZ + 0.5f) continue;
                float d = ball.GlobalPosition.DistanceTo(GlobalPosition);
                if (d < best) { best = d; goal = bl; }
            }
        }

        if (dodgeLeft > 0)
        {
            // Esquive : pas rapide sur le côté (2,5 fois la marche).
            dodgeLeft -= delta;
            local.X += dodgeDir * Speed * 2.5f * (float)delta;
            targetX = local.X;
        }
        else if (goal is Vector3 g)
        {
            local = local.MoveToward(new Vector3(g.X, 0, g.Z), Speed * (float)delta);
        }
        else
        {
            // Allers-retours latéraux dans la zone (plus courts et plus fréquents au niveau difficile).
            changeDirTimer -= delta;
            if (changeDirTimer <= 0)
            {
                targetX = rng.RandfRange(-limitX, limitX);
                changeDirTimer = Level >= 2 ? rng.RandfRange(0.3f, 0.8f) : rng.RandfRange(0.4f, 1.2f);
            }
            local.X = Mathf.MoveToward(local.X, targetX, Speed * (float)delta);
        }
        local.X = Mathf.Clamp(local.X, -limitX, limitX);
        local.Z = Mathf.Clamp(local.Z, -limitZ, limitZ);
        local.Y = 0;
        GlobalPosition = Zone.Center + basis * local;
    }

    /// <summary>Le modèle se tourne vers l'adversaire le plus proche (il ne reste plus figé face au même mur).</summary>
    void FaceNearestEnemy(double delta)
    {
        if (Avatar == null) return;
        Node3D? best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < Targets.Count; i++)
        {
            if (TargetTeams[i] == Hurtbox.Team || !IsInstanceValid(Targets[i])) continue;
            float d = Targets[i].GlobalPosition.DistanceSquaredTo(GlobalPosition);
            if (d < bestD) { bestD = d; best = Targets[i]; }
        }
        if (best == null) return;
        var to = best.GlobalPosition - GlobalPosition;
        if (to.X * to.X + to.Z * to.Z < 0.01f) return;
        float want = Mathf.Atan2(-to.X, -to.Z) - GlobalRotation.Y;   // les modèles regardent vers -Z
        var r = Avatar.Rotation;
        r.Y = Mathf.LerpAngle(r.Y, want, 1f - Mathf.Exp(-8f * (float)delta));
        Avatar.Rotation = r;
    }

    /// <summary>Le bot peut-il prendre cette balle ? (point unique : règles de prise des passes adverses.)</summary>
    bool CanTake(Ball ball) => !(ball.IsPass && ball.OwnerTeam != Hurtbox.Team);

    void TryCatch()
    {
        foreach (var node in GetTree().GetNodesInGroup("balls"))
        {
            if (node is not Ball ball || ball.IsHeld) continue;
            if (!CanTake(ball)) continue;
            if (ball.GlobalPosition.DistanceTo(Chest) > CatchRadius) continue;
            bool perfect = false;
            if (reads.TryGetValue(ball, out var read))
            {
                // Tir adverse lu à l'avance : on s'y tient (raté = bras pris un instant, la balle passe et touche).
                if (!read.take) { catchCooldown = whiffLeft = 0.5f; nMissed++; return; }
                // Attrape au dernier moment : on attend que la balle soit tout près (moins de 0,15 s) pour une parfaite.
                if (read.perfect && IncomingIn(ball) is float t && t > 0.15f) return;
                perfect = read.perfect;
            }
            else if (ball.OwnerTeam >= 0 && ball.OwnerTeam != Hurtbox.Team && ball.LinearVelocity.Length() > 8f && rng.Randf() > Current.Catch)
            {
                // Tir adverse rapide pas encore lu (lancé à bout portant) : même chance d'attrape que le niveau.
                catchCooldown = whiffLeft = 0.5f;
                nMissed++;
                return;
            }
            held = ball;
            if (reads.Remove(ball) || ball.OwnerTeam >= 0 && ball.OwnerTeam != Hurtbox.Team && ball.LinearVelocity.Length() > 8f) { nCatches++; if (perfect) nPerfects++; }
            ball.Hold(holdPoint, Hurtbox.PlayerId, Hurtbox.Team, perfect);
            feinted = false;
            windupLeft = -1;
            var think = Current.Think;
            throwDelay = Gentle ? rng.RandfRange(2.0f, 3.5f) : rng.RandfRange(think.X, think.Y);
            return;
        }
    }

    const float MaxUsefulRange = 16f;
    int waitedForRange;
    readonly Dictionary<Node3D, Vector3> lastPositions = new();
    readonly Dictionary<Node3D, Vector3> velocities = new();

    /// <summary>Vitesse estimée de chaque cible (pour l'anticipation du tir).</summary>
    void TrackTargets(double delta)
    {
        if (delta <= 0) return;
        foreach (var t in Targets)
        {
            if (!IsInstanceValid(t)) continue;
            var p = t.GlobalPosition;
            if (lastPositions.TryGetValue(t, out var prev))
                velocities[t] = velocities.TryGetValue(t, out var old) ? old.Lerp((p - prev) / (float)delta, 0.3f) : (p - prev) / (float)delta;
            lastPositions[t] = p;
        }
    }

    void ThinkThrow(double delta)
    {
        if (held == null) return;
        if (windupLeft >= 0)
        {
            // Élan en cours : la balle part à la fin (le geste se voit avant, comme l'élan de Knockout City).
            windupLeft -= delta;
            if (windupLeft < 0) Launch();
            return;
        }
        throwDelay -= delta;
        // Punition (guide de KC) : une cible qui vient de rater son attrape a les bras pris une demi-seconde, on tire aussitôt.
        if (throwDelay > 0.1 && Level >= 1 && !Gentle && !Calm && Nearest(enemy: true) is { } prey && ArmsBusy(prey)) { throwDelay = 0.1; nPunish++; }
        if (throwDelay > 0) return;
        // Pas de lancer avant le « GO ! » ni pendant les étapes calmes de l'entraînement : la balle reste en main.
        if (Calm || HoldFire?.Invoke() == true) { throwDelay = rng.RandfRange(0.2f, 0.6f); return; }
        var s = Current;
        // Passe : seulement une balle pas encore chargée (la passe ne charge que jusqu'au palier 1), vers l'allié vivant le plus proche.
        var mate = held.Charge < 1 && !Gentle && rng.Randf() < s.Pass ? Nearest(enemy: false) : null;
        var target = mate ?? Nearest(enemy: true);
        if (target == null) { throwDelay = 0.3; return; }
        float dist = target.GlobalPosition.DistanceTo(GlobalPosition);
        // Hors de portée utile (18 m et plus : aucune touche mesurée) : on attend un peu que la cible se rapproche.
        if (mate == null && dist > MaxUsefulRange && waitedForRange < 3) { waitedForRange++; throwDelay = 0.5; return; }
        waitedForRange = 0;
        // Feinte : l'élan sans lâcher la balle, puis le vrai tir un peu plus tard (piège une attrape trop précoce).
        if (mate == null && !feinted && rng.Randf() < s.Feint)
        {
            feinted = true;
            nFeints++;
            Avatar?.PlayAction("Lancer", 0.45);
            throwDelay = rng.RandfRange(0.45f, 0.8f);
            return;
        }
        windupTarget = target;
        windupPass = mate != null;
        windupLeft = Gentle ? 0.5 : s.Windup;
        Avatar?.PlayAction(windupPass ? "Passe" : "Lancer", windupLeft + 0.35);
    }

    void Launch()
    {
        var target = windupTarget;
        windupLeft = -1;
        if (held == null || target == null || !IsInstanceValid(target)) { throwDelay = 0.2; return; }
        if (windupPass)
        {
            var v = DesktopPlayer.ArcVelocity(holdPoint.GlobalPosition, target.GlobalPosition + Vector3.Up * 0.8f, 4.2f);
            held.Release(Assist.PlanFor(held, v, Ball.ModePass), 0f, Ball.ModePass);
            nPasses++;
        }
        else
        {
            var s = Current;
            float speed = ThrowSpeed(s);
            float dist = target.GlobalPosition.DistanceTo(GlobalPosition);
            // Anticipation : on vise là où la cible sera à l'arrivée de la balle (en partie seulement aux niveaux bas).
            var lead = velocities.TryGetValue(target, out var tv) ? tv * (dist / speed) * s.Lead : Vector3.Zero;
            var aim = target.GlobalPosition + lead + new Vector3(rng.RandfRange(-s.Aim, s.Aim), 1.1f, rng.RandfRange(-s.Aim, s.Aim));
            var dir = (aim - holdPoint.GlobalPosition).Normalized();
            held.Release(Assist.PlanFor(held, dir * speed + Vector3.Up * 1.2f, 0), 0f);
            nThrows++;
        }
        held = null;
        catchCooldown = 0.6;
    }

    /// <summary>Cible qui vient de rater une attrape et ne peut pas attraper tout de suite.</summary>
    static bool ArmsBusy(Node3D t) => t is Bot b ? b.whiffLeft > 0 : ArmsLocked?.Invoke(t) == true;

    /// <summary>Vitesse du tir (point unique : les règles de palier s'appliquent ici).</summary>
    float ThrowSpeed(Skill s) => Gentle ? rng.RandfRange(12f, 15f) : rng.RandfRange(s.MinSpeed, s.MaxSpeed);

    /// <summary>Adversaire le plus proche (ou allié le plus proche, hors soi), vivant.</summary>
    Node3D? Nearest(bool enemy)
    {
        Node3D? best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < Targets.Count; i++)
        {
            var t = Targets[i];
            if (t == this || !IsInstanceValid(t) || (TargetTeams[i] != Hurtbox.Team) != enemy) continue;
            if (t is Bot { Hurtbox.Eliminated: true } || !t.Visible) continue;
            float d = t.GlobalPosition.DistanceTo(GlobalPosition);
            if (d < bestDist) { bestDist = d; best = t; }
        }
        return best;
    }
}
