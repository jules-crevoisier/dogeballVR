using System.Collections.Generic;
using Godot;
using DogeballVR.Maps;

namespace DogeballVR.Gameplay;

/// <summary>
/// Règles Knockout City des décisions du 22/10 (n° 2 à 7), lues dans le bloc « rules » de la map (<see cref="RulesDef"/>) et
/// réunies ici en fonctions pures pour être testées sans casque (<see cref="AssistSelfTest"/>). Chaque machine lit la même map ;
/// ce qui décide d'un résultat (dégâts, vitesse, régénération, pause, blocage miroir) est appliqué par l'hôte.
/// </summary>
public static class KcRules
{
    /// <summary>Règles en cours (map chargée). Hors partie : valeurs par défaut des décisions.</summary>
    public static RulesDef Def { get; private set; } = new();
    /// <summary>Vrai si cette machine joue au casque (l'hôte en VR impose la pause de 20 s et la régénération).</summary>
    public static bool Vr { get; private set; }

    public static void Configure(RulesDef rules, bool vr)
    {
        Def = rules;
        Vr = vr;
    }

    // ------------------------------------------------------------------ n° 2 : effet du palier

    /// <summary>Cœurs retirés par un coup direct : 1 par coup, sauf si la map rétablit le KO au palier (doubleDamageCharge).</summary>
    public static int Damage(int charge) => Def.DoubleDamageCharge > 0 && charge >= Def.DoubleDamageCharge ? 2 : 1;

    /// <summary>
    /// VR : fenêtre d'attrape normale d'une balle adverse selon son palier (0,35 / 0,30 / 0,27 / 0,25 s par défaut),
    /// jamais moins que la fenêtre parfaite + 0,1 s. La fenêtre parfaite, elle, ne change pas.
    /// </summary>
    public static float VrCatchWindow(int charge, float baseWindow, float perfectWindow)
    {
        var cuts = Def.VrCatchWindowCutByCharge;
        float cut = cuts.Count == 0 ? 0f : cuts[Mathf.Clamp(charge, 0, cuts.Count - 1)];
        return Mathf.Max(baseWindow - cut, perfectWindow + Def.VrCatchWindowMinGap);
    }

    /// <summary>Clavier-souris : vitesse de pleine charge (palier 0).</summary>
    public static float PcFullChargeSpeed => Def.PcFullChargeSpeed;

    /// <summary>
    /// Clavier-souris : multiplicateur de vitesse d'un tir tendu selon le palier de la balle, +15 % par palier cumulé
    /// (pleine charge 24 → 27,6 → 31,7 → 36,5 m/s), sans dépasser le plafond de 40 m/s. Le tireur l'applique (sa visée compense la chute).
    /// </summary>
    public static float PcTierFactor(int charge)
    {
        if (charge <= 0 || Def.PcFullChargeSpeed <= 0f) return 1f;
        float cap = Mathf.Max(Def.PcTierSpeedCap, Def.PcFullChargeSpeed) / Def.PcFullChargeSpeed;
        return Mathf.Min(Mathf.Pow(1f + Def.PcTierSpeedBonus, charge), cap);
    }

    /// <summary>Clavier-souris, côté hôte : plafond d'un tir tendu, pleine charge × palier (24 m/s au palier 0, 40 m/s au plus).</summary>
    public static Vector3 PcTierVelocity(Vector3 velocity, int charge) => velocity.LimitLength(Def.PcFullChargeSpeed * PcTierFactor(charge));

    /// <summary>Lift VR de La Fosse : palier après le bonus du premier tir lancé du plateau haut (+1, plafonné à Ball.MaxCharge).</summary>
    public static int ChargeAfterLift(int charge) => Mathf.Min(charge + Mathf.Max(0, Def.LiftChargeBonus), Ball.MaxCharge);

    // ------------------------------------------------------------------ n° 3 : interception des passes

    /// <summary>Un joueur peut-il prendre cette balle ? Une passe adverse ne se prend que si l'interception est permise (par toute attrape).</summary>
    public static bool CanTake(bool isPass, int ownerTeam, int team) => !isPass || ownerTeam == team || ownerTeam < 0 || Def.InterceptPasses;

    /// <summary>Une passe adverse interceptée peut être une attrape parfaite (+1 palier, voir Ball.ChargeOnCatch).</summary>
    public static bool PerfectOnPass => Def.InterceptPasses;

    // ------------------------------------------------------------------ n° 4 : blocage après une attrape ratée (VR)

    /// <summary>Balle vue au moment d'un appui : position, vitesse, équipe du tireur (-1 neutre), libre (ni tenue ni explosée).</summary>
    public readonly record struct BallSeen(Vector3 Position, Vector3 Velocity, int OwnerTeam, bool Free, int Charge = 0);

    /// <summary>
    /// Vraie tentative d'attrape : une balle adverse lancée (≥ HitMinSpeed) dont l'impact prévu sur le corps ou la main est à moins
    /// de 0,6 s (calcul de <see cref="Visual.ThreatIndicator.TimeToImpact"/>). Un plot, une balle au sol ou une balle alliée : jamais.
    /// Renvoie le palier de la balle la plus proche de l'impact (pour sa fenêtre d'attrape), ou -1 s'il n'y a pas de vraie tentative.
    /// </summary>
    public static int RealCatchAttempt(Vector3 body, Vector3 hand, int team, IEnumerable<BallSeen> balls)
    {
        int charge = -1;
        float best = float.PositiveInfinity;
        foreach (var b in balls)
        {
            if (!b.Free || b.OwnerTeam < 0 || b.OwnerTeam == team || b.Velocity.Length() < Ball.HitMinSpeed) continue;
            float t = Mathf.Min(Visual.ThreatIndicator.TimeToImpact(b.Position, b.Velocity, body), Visual.ThreatIndicator.TimeToImpact(b.Position, b.Velocity, hand));
            if (t <= Def.VrMissedCatchThreatSeconds && t < best) { best = t; charge = b.Charge; }
        }
        return charge;
    }

    /// <summary>Balles de la scène vues par <see cref="RealCatchAttempt"/>.</summary>
    public static List<BallSeen> SeenBalls(SceneTree tree)
    {
        var list = new List<BallSeen>();
        foreach (var n in tree.GetNodesInGroup("balls"))
            if (n is Ball b && GodotObject.IsInstanceValid(b)) list.Add(new BallSeen(b.GlobalPosition, b.LinearVelocity, b.OwnerTeam, !b.IsHeld && !b.HasExploded, b.Charge));
        return list;
    }

    /// <summary>
    /// Appui du grip (joueur VR local, ou miroir de l'hôte pour un joueur distant) : arme le blocage si c'est une vraie tentative.
    /// Renvoie faux si le joueur est bloqué (l'appui ne compte pas). `slack` allonge la fenêtre (marge de latence de l'hôte).
    /// </summary>
    public static bool PressGrip(Hurtbox player, Vector3 hand, double now, double slack = 0)
    {
        int charge = RealCatchAttempt(player.GlobalPosition, hand, player.Team, SeenBalls(player.GetTree()));
        return player.CatchLock.Press(now, Assist.CatchWindow(Mathf.Max(charge, 0), true) + slack, charge >= 0);
    }

    // ------------------------------------------------------------------ n° 5 : soins

    /// <summary>Attraper rend-il un cœur ? Non par défaut (Knockout City ne soigne jamais).</summary>
    public static bool CatchHeals => Def.CatchHealsHeart;

    /// <summary>Régénération d'un cœur (s sans être touché, 0 = aucune) : en VR sur toutes les cartes, aucune au clavier-souris.</summary>
    public static float HeartRegenSeconds(bool vr) => vr ? Def.HeartRegenSeconds : Def.PcHeartRegenSeconds;

    // ------------------------------------------------------------------ n° 6 et 7 : plaques, pause

    /// <summary>Instants des échanges de plaques : ceux de la map (vide = suspendus) ; le test forcé reprend les anciens.</summary>
    public static double[] PlateSwapTimes(bool forced) =>
        Def.PlateSwapTimes.Count > 0 ? Def.PlateSwapTimes.ToArray() : forced ? new double[] { 20, 45, 70, 95 } : System.Array.Empty<double>();

    /// <summary>Pause entre deux manches, décidée par l'hôte : 20 s s'il est en VR, 12 s au clavier-souris.</summary>
    public static float RoundBreakSeconds(bool vrHost) => vrHost ? Def.VrRoundBreakSeconds : Def.PcRoundBreakSeconds;
}

/// <summary>
/// Blocage après une attrape ratée (VR, décision n° 4), tenu par joueur (sur <see cref="Hurtbox"/>) pour que l'autre main ne le
/// contourne pas, et en miroir par l'hôte pour un joueur distant. Prêt → Armé à l'appui d'une vraie tentative ; une prise le
/// remet à Prêt ; la fenêtre écoulée sans prise → Bloqué pendant LockSeconds (aucune prise, aucun nouvel appui), puis Prêt.
/// Un appui sans vraie tentative (plot, balle au sol, balle alliée) n'arme rien et ne bloque jamais.
/// </summary>
public sealed class CatchLock
{
    double windowEnd = double.NegativeInfinity;
    bool armed;
    public double LockedUntil { get; private set; } = double.NegativeInfinity;
    /// <summary>Nombre de blocages déclenchés (tests, journal).</summary>
    public int Locks { get; private set; }

    public bool Locked(double now) => now < LockedUntil;
    public bool Armed => armed;

    /// <summary>Appui du grip. Renvoie faux si le joueur est bloqué (l'appui est ignoré). Un appui pendant Armé ne prolonge pas la fenêtre.</summary>
    public bool Press(double now, double window, bool realAttempt)
    {
        Update(now);
        if (Locked(now)) return false;
        if (realAttempt && !armed) { armed = true; windowEnd = now + window; }
        return true;
    }

    /// <summary>Prise réussie : plus de blocage à craindre pour cet appui.</summary>
    public void Caught() => armed = false;

    /// <summary>À appeler à chaque tick. Renvoie vrai à l'instant où un blocage commence.</summary>
    public bool Update(double now, double lockSeconds = -1)
    {
        if (!armed || now < windowEnd) return false;
        armed = false;
        double lockFor = lockSeconds >= 0 ? lockSeconds : KcRules.Def.VrMissedCatchLockSeconds;
        if (lockFor <= 0) return false;
        LockedUntil = windowEnd + lockFor;
        Locks++;
        return true;
    }

    public void Reset() { armed = false; LockedUntil = double.NegativeInfinity; }
}
