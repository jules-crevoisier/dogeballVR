using System.Collections.Generic;
using Godot;
using DogeballVR.Maps;

namespace DogeballVR.Gameplay;

/// <summary>
/// Aides VR de l'adaptation Knockout City, réunies et sans état de scène pour être testées sans casque (voir AssistSelfTest).
/// Tout est réglable dans la map (ballRules.throwAssist, ballRules.ballMagnetRadius). Les règles de lancer (vitesse planifiée par l'hôte)
/// ne s'appliquent qu'en VR (VrRules) ; l'attrape par fenêtre de grip vaut pour tous les casques.
/// </summary>
public static class Assist
{
    /// <summary>Vitesse minimale d'un vrai lancer (m/s) : un geste mou part quand même tendu. 5,5 au lieu de 9 (temps de vol minimal).</summary>
    public static float MinLaunchSpeed { get; set; } = 5.5f;
    /// <summary>Plafond de vitesse d'un lancer VR (m/s).</summary>
    public static float MaxLaunchSpeedVR { get; set; } = 20f;
    /// <summary>Plafond hors VR (clavier-souris, vieux réglage).</summary>
    public const float MaxLaunchSpeedOther = 30f;
    public static float SpeedMultiplier { get; set; } = 1.4f;
    /// <summary>Temps de vol minimal visé (s) : l'humain réagit en 200-250 ms.</summary>
    public static float MinFlightSeconds { get; set; } = 0.5f;
    /// <summary>Attrape parfaite : la balle doit avoir encore au moins ce rapport de sa vitesse de lancer (réglable).</summary>
    public static float PerfectCatchFactor { get; set; } = 0.8f;
    /// <summary>Attrape parfaite : délai maximal (s) entre l'appui du grip et la prise (0,15 s en VR).</summary>
    public static float PerfectWindowSeconds { get; set; } = 0.15f;
    /// <summary>Prise acceptée seulement si le grip a été pressé dans ce délai (s) avant le contact.</summary>
    public static float GripWindowSeconds { get; set; } = 0.35f;
    /// <summary>Volume de prise de la main (m) et aimant pour une balle qui s'approche (m).</summary>
    public static float CatchRadius { get; set; } = 0.3f;
    public static float MagnetRadius { get; set; } = 0.5f;
    /// <summary>Joueur distant (VR ou PC) : le coup est différé de ce délai (s) côté hôte pour laisser arriver une prise lancée ~80 ms plus tôt par le client.</summary>
    public const float HitGraceSeconds = 0.1f;
    /// <summary>Tolérance de l'hôte : la main doit être près de la balle (m) + marge de latence.</summary>
    public const float CatchReach = 1.2f;
    public const float LatencyFactor = 0.12f, LatencyMaxSlack = 2.0f;

    /// <summary>Vrai en VR : l'hôte planifie la vitesse de chaque lancer (ThrowPlan).</summary>
    public static bool VrRules { get; set; }

    public static void Configure(BallRulesDef rules)
    {
        var a = rules.ThrowAssist;
        MinLaunchSpeed = a.MinLaunchSpeed;
        MaxLaunchSpeedVR = a.MaxLaunchSpeedVR;
        SpeedMultiplier = a.SpeedMultiplier;
        PerfectCatchFactor = a.PerfectCatchFactor;
        PerfectWindowSeconds = a.PerfectCatchWindowSeconds;
        GripWindowSeconds = a.GripWindowSeconds;
        CatchRadius = a.CatchRadius;
        MagnetRadius = rules.BallMagnetRadius;
    }

    // ------------------------------------------------------------------ lancer

    /// <summary>
    /// Plafond de vitesse pour un tir depuis « from » dans la direction « dir » (unitaire) : v = max(5,5, min(20, d / 0,5)) où d est
    /// la distance parcourue jusqu'au point de passage le plus proche de l'ennemi le plus proche sur la trajectoire (pas de cône :
    /// un ennemi à plus de 1,2 m du tir est ignoré). Sans ennemi sur la trajectoire : plafond de la map.
    /// </summary>
    public static float SpeedCap(Vector3 from, Vector3 dir, IEnumerable<Vector3> enemies)
    {
        float nearest = float.PositiveInfinity;
        foreach (var e in enemies)
        {
            var rel = e - from;
            float t = rel.Dot(dir);
            if (t < 0.3f) continue;
            if ((rel - dir * t).Length() > 1.2f) continue;
            nearest = Mathf.Min(nearest, t);
        }
        if (float.IsPositiveInfinity(nearest)) return MaxLaunchSpeedVR;
        return Mathf.Max(MinLaunchSpeed, Mathf.Min(MaxLaunchSpeedVR, nearest / MinFlightSeconds));
    }

    /// <summary>Applique la vitesse planifiée : plancher MinLaunchSpeed, plafond SpeedCap. Renvoie la vitesse retenue (0 = pas un vrai lancer).</summary>
    public static (Vector3 velocity, float speed) Plan(Vector3 from, Vector3 velocity, IEnumerable<Vector3> enemies)
    {
        float speed = velocity.Length();
        if (speed < 1.5f) return (velocity, 0f);   // pas un vrai lancer : la balle tombe
        var dir = velocity / speed;
        float cap = SpeedCap(from, dir, enemies);
        float planned = Mathf.Clamp(speed, MinLaunchSpeed, cap);
        return (dir * planned, planned);
    }

    /// <summary>Positions de tête des adversaires vivants (groupe des hurtboxes) pour un tireur de l'équipe « team ».</summary>
    public static List<Vector3> EnemyPositions(SceneTree tree, int team)
    {
        var list = new List<Vector3>();
        foreach (var n in tree.GetNodesInGroup("hurtboxes"))
            if (n is Hurtbox { Eliminated: false } h && h.Team != team && GodotObject.IsInstanceValid(h)) list.Add(h.GlobalPosition + new Vector3(0, 0.3f, 0));
        return list;
    }

    /// <summary>Lancer d'une balle tenue côté hôte (joueur hôte ou ThrowRpc) : la vitesse est planifiée en VR et mémorisée dans ball.SpeedCap.</summary>
    public static Vector3 PlanFor(Ball ball, Vector3 velocity, int mode)
    {
        float max = VrRules ? MaxLaunchSpeedVR : MaxLaunchSpeedOther;
        if (mode != 0) return velocity.LimitLength(24f);
        velocity = velocity.LimitLength(max);
        if (!VrRules || !ball.IsInsideTree()) { ball.SpeedCap = velocity.Length(); return velocity; }
        var (planned, speed) = Plan(ball.GlobalPosition, velocity, EnemyPositions(ball.GetTree(), ball.OwnerTeam));
        ball.SpeedCap = speed;
        return planned;
    }

    // ------------------------------------------------------------------ attrape

    /// <summary>Le grip a-t-il été pressé dans la fenêtre avant le contact ? (main fermée en permanence : jamais.)</summary>
    public static bool GripValid(double now, double gripPressedAt) => now - gripPressedAt <= GripWindowSeconds && now >= gripPressedAt;

    /// <summary>Distance minimale entre la main et le segment parcouru par la balle pendant le tick (balayage : 0,33 m par tick à 20 m/s).</summary>
    public static float SweepDistance(Vector3 ballPrev, Vector3 ballNow, Vector3 hand)
    {
        var seg = ballNow - ballPrev;
        float len2 = seg.LengthSquared();
        float t = len2 < 1e-6f ? 0f : Mathf.Clamp((hand - ballPrev).Dot(seg) / len2, 0f, 1f);
        return (ballPrev + seg * t - hand).Length();
    }

    /// <summary>Contact côté joueur : dans le volume de prise, ou dans l'aimant si la balle s'approche de la main.</summary>
    public static bool Contact(Vector3 ballPrev, Vector3 ballNow, Vector3 hand)
    {
        float d = SweepDistance(ballPrev, ballNow, hand);
        if (d <= CatchRadius) return true;
        return d <= MagnetRadius && (ballNow - ballPrev).Dot(hand - ballPrev) > 0f;
    }

    /// <summary>Contrôle de l'hôte : la balle (position hôte, en avance sur ce que voyait le client) doit être près de la main.</summary>
    public static bool HostAccepts(Vector3 ballHost, Vector3 handHost, float ballSpeed)
    {
        float slack = Mathf.Min(ballSpeed * LatencyFactor, LatencyMaxSlack);
        return ballHost.DistanceTo(handHost) <= CatchReach + slack;
    }

    /// <summary>Attrape parfaite : tir adverse encore assez rapide par rapport à sa vitesse de lancer (ball.SpeedCap, 0 = inconnue).</summary>
    public static bool PerfectAllowed(float speed, float launchSpeed) =>
        speed >= Ball.HitMinSpeed && (launchSpeed <= 0f || speed >= PerfectCatchFactor * launchSpeed);
}
