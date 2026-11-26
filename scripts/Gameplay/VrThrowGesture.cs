using System.Collections.Generic;
using Godot;
using DogeballVR.Maps;

namespace DogeballVR.Gameplay;

/// <summary>Une mesure de la main au casque : temps (s), position monde, vitesse monde donnée par le runtime OpenXR si elle existe.</summary>
public struct HandSample
{
    public double Time;
    public Vector3 Position;
    public Vector3 Velocity;
    public bool HasVelocity;

    public HandSample(double time, Vector3 position, Vector3 velocity = default, bool hasVelocity = false)
    {
        Time = time; Position = position; Velocity = velocity; HasVelocity = hasVelocity;
    }
}

/// <summary>Ce que le lâcher produit : vitesse à envoyer à l'hôte, effet, mode (0 tendu, cloche, passe, tir chargé).</summary>
public readonly struct ThrowGesture
{
    public readonly Vector3 Velocity;
    public readonly float Spin;
    public readonly int Mode;
    public readonly string Kind;

    public ThrowGesture(Vector3 velocity, float spin, int mode, string kind)
    {
        Velocity = velocity; Spin = spin; Mode = mode; Kind = kind;
    }

    public bool IsLob => Mode == Ball.ModeLob;
    public bool IsPass => Mode == Ball.ModePass;
    public bool IsPower => Mode == Ball.ModePower;
    public bool IsCurve => Spin != 0f;
}

/// <summary>
/// Lancer au casque, comme Knockout City mais au geste du bras (fonctions pures, testées sans casque par <see cref="VrGestureSelfTest"/>) :
/// <list type="bullet">
/// <item>tir tendu : la vitesse est le pic de vitesse de la main juste avant le lâcher (la fin du geste freine : la moyenne des
/// dernières images donnait des tirs mous) ;</item>
/// <item>effet : lancer de côté (main sous les épaules, tir à plat), le bras balaie en arc ; la balle tourne du même côté que le
/// bras (gauche ou droite). Un lancer par-dessus l'épaule ne donne jamais d'effet, même si le tronc tourne ;</item>
/// <item>cloche : lancer par en dessous (main sous la poitrine, vers le haut) ; le point de chute suit la force du geste et se cale
/// sur l'adversaire visé s'il y en a un dans le cône ;</item>
/// <item>passe : lancer vers un allié (plus près de lui que de tout adversaire) : balle en cloche vers ses mains ;</item>
/// <item>tir chargé : gâchette tenue avec la balle en main, pleine charge en 0,8 s : +15 % de vitesse et de plafond.</item>
/// </list>
/// </summary>
public static class VrThrowGesture
{
    /// <summary>En dessous : pas un lancer, la balle tombe des mains.</summary>
    public const float DropSpeed = 1.5f;

    /// <summary>Écart maximal (°) entre le cap de la main au lâcher et celui du pic : au-delà, la main rebondit, on garde le pic.</summary>
    public const float MaxHeadingChangeDegrees = 60f;

    /// <summary>
    /// Vitesse de lancer lue dans la fenêtre (mesures les plus récentes d'abord) : la force est le pic de vitesse de la main, lissé
    /// avec ses deux voisins (la main freine juste avant que le grip s'ouvre). Le cap (vu de dessus) est celui de la main au lâcher :
    /// un bras qui balaie de côté change de cap à chaque image, le pic indiquerait où la main allait 50 ms plus tôt. L'élévation est
    /// celle du pic : par-dessus l'épaule, l'avant-bras fouette et la main plonge au lâcher, la balle partirait dans le sol.
    /// Vitesse du runtime si elle existe (plus juste sur Quest), sinon différence de positions.
    /// </summary>
    public static Vector3 PeakVelocity(IReadOnlyList<HandSample> samples, float window)
    {
        var v = Velocities(samples, window);
        if (v.Count == 0) return Vector3.Zero;
        int best = 0;
        for (int i = 1; i < v.Count; i++) if (v[i].LengthSquared() > v[best].LengthSquared()) best = i;
        var sum = Vector3.Zero;
        int n = 0;
        for (int i = Mathf.Max(0, best - 1); i <= Mathf.Min(v.Count - 1, best + 1); i++) { sum += v[i]; n++; }
        var peak = sum / n;
        float speed = peak.Length();
        var lastFlat = new Vector2(v[0].X, v[0].Z);
        if (v.Count > 1) lastFlat = (lastFlat + new Vector2(v[1].X, v[1].Z)) * 0.5f;   // deux dernières mesures : moins de bruit
        var peakFlat = new Vector2(peak.X, peak.Z);
        // Main presque arrêtée au lâcher, ou qui repart en arrière (rebond après un freinage sec) : direction du pic.
        if (speed < 1e-4f || peakFlat.Length() < 1e-3f || lastFlat.Length() < 0.3f * peakFlat.Length()
            || Mathf.RadToDeg(Mathf.Abs(lastFlat.AngleTo(peakFlat))) > MaxHeadingChangeDegrees) return peak;
        var heading = lastFlat.Normalized() * peakFlat.Length();
        return new Vector3(heading.X, peak.Y, heading.Y);   // même norme que le pic : cap du lâcher, élévation du pic
    }

    /// <summary>
    /// Rotation de la trajectoire de la main vue de dessus (rad/s, positif = vers la gauche, sens de Godot) sur la fenêtre,
    /// mesurée sur les images où la main va vite à l'horizontale. 0 s'il n'y a pas assez de mesures.
    /// </summary>
    public static float TurnRate(IReadOnlyList<HandSample> samples, float window)
    {
        var v = Velocities(samples, window);
        int newest = -1, oldest = -1;
        for (int i = 0; i < v.Count; i++)
        {
            if (new Vector2(v[i].X, v[i].Z).Length() < 1.5f) continue;
            if (newest < 0) newest = i;
            oldest = i;
        }
        if (newest < 0 || oldest - newest < 2) return 0f;
        double dt = samples[newest].Time - samples[oldest].Time;
        if (dt < 0.025) return 0f;
        float a = Heading(v[oldest]), b = Heading(v[newest]);
        return Mathf.AngleDifference(a, b) / (float)dt;
    }

    /// <summary>Effet transmis à la balle : 0 sous la zone morte, puis de 35 % à 100 % de l'effet maximal. Positif = la balle tourne à droite.</summary>
    public static float CurveSpin(float turnRate, VrGestureDef def)
    {
        float a = Mathf.Abs(turnRate);
        if (a < def.CurveDeadzone) return 0f;
        float f = Mathf.Clamp((a - def.CurveDeadzone) / Mathf.Max(def.CurveFull - def.CurveDeadzone, 0.01f), 0f, 1f);
        return -Mathf.Sign(turnRate) * Mathf.Lerp(0.35f, 1f, f) * def.CurveMaxSpin;   // la trajectoire tourne à gauche (+) : la balle file à gauche (effet négatif)
    }

    /// <summary>Charge du tir (0..1) après « held » secondes de gâchette tenue.</summary>
    public static float ChargeFraction(double held, VrGestureDef def) =>
        held <= 0 ? 0f : Mathf.Clamp((float)(held / Mathf.Max(def.ChargeSeconds, 0.05f)), 0f, 1f);

    /// <summary>
    /// Lit le geste au lâcher. hand / head : positions monde ; allies / enemies : positions (au sol ou au torse) des autres joueurs vivants.
    /// speedMultiplier, minSpeed, maxSpeed : réglages de la map (<see cref="Assist"/>). L'hôte replanifie de toute façon (Assist.PlanFor).
    /// </summary>
    public static ThrowGesture Read(IReadOnlyList<HandSample> samples, Vector3 hand, Vector3 head, float charge,
        IReadOnlyList<Vector3> allies, IReadOnlyList<Vector3> enemies, VrGestureDef def,
        float speedMultiplier, float minSpeed, float maxSpeed)
    {
        var raw = PeakVelocity(samples, def.VelocityWindowSeconds);
        var vel = raw * speedMultiplier;
        float speed = vel.Length();
        if (speed < DropSpeed) return new ThrowGesture(vel, 0f, 0, "lâcher");

        var flat = new Vector3(vel.X, 0, vel.Z);
        float elevation = Mathf.RadToDeg(Mathf.Atan2(vel.Y, flat.Length()));

        // Cloche : par en dessous, vers le haut.
        if (elevation >= def.LobMinElevationDegrees && hand.Y <= head.Y - def.LobHandBelowHead && flat.Length() > 0.4f)
        {
            var dir = flat.Normalized();
            float range = Mathf.Clamp(raw.Length() * def.LobDistancePerSpeed, def.LobMinDistance, def.LobMaxDistance);
            var to = hand + dir * range;
            to.Y = head.Y - 0.6f;   // à hauteur de torse
            float bestAngle = def.LobAimConeDegrees;
            foreach (var e in enemies)
            {
                var rel = new Vector3(e.X - hand.X, 0, e.Z - hand.Z);
                if (rel.Length() < 2f || rel.Length() > def.LobMaxDistance * 1.25f) continue;
                float ang = Mathf.RadToDeg(dir.AngleTo(rel.Normalized()));
                if (ang < bestAngle) { bestAngle = ang; to = e + Vector3.Up * 0.3f; }   // torse de l'adversaire, comme la cloche du PC
            }
            return new ThrowGesture(DesktopPlayer.ArcVelocity(hand, to, 3.0f), 0f, Ball.ModeLob, "cloche");
        }

        // Passe : vers un allié, nettement plus dans l'axe que tout adversaire (au moins deux fois plus près en angle, et 6° d'écart).
        if (flat.Length() > 0.5f)
        {
            var dir = flat.Normalized();
            float allyAngle = def.PassConeDegrees;
            Vector3? mate = null;
            foreach (var a in allies)
            {
                var rel = new Vector3(a.X - hand.X, 0, a.Z - hand.Z);
                if (rel.Length() < 1.5f || rel.Length() > 25f) continue;
                float ang = Mathf.RadToDeg(dir.AngleTo(rel.Normalized()));
                if (ang < allyAngle) { allyAngle = ang; mate = a; }
            }
            if (mate is { } m)
            {
                bool enemyInAxis = false;
                float guard = Mathf.Max(allyAngle * 2f, allyAngle + 6f);
                foreach (var e in enemies)
                {
                    var rel = new Vector3(e.X - hand.X, 0, e.Z - hand.Z);
                    if (rel.Length() > 1f && Mathf.RadToDeg(dir.AngleTo(rel.Normalized())) <= guard) enemyInAxis = true;
                }
                if (!enemyInAxis)
                    return new ThrowGesture(DesktopPlayer.ArcVelocity(hand, m + Vector3.Up * 0.4f, 4.2f), 0f, Ball.ModePass, "passe");   // vers les mains de l'allié, à sa hauteur
            }
        }

        // Tir tendu, avec effet si le bras a balayé, chargé si la gâchette était tenue.
        bool power = charge >= 0.999f;
        float boost = power ? 1f + def.ChargeSpeedBonus : 1f;
        vel = vel.LimitLength(maxSpeed * boost);
        if (vel.Length() < minSpeed) vel = vel.Normalized() * minSpeed;   // geste net mais mou : part quand même tendu
        if (power) vel = (vel * boost).LimitLength(maxSpeed * boost);
        // Effet : seulement un lancer de côté (main sous les épaules, tir à peu près à plat). Par-dessus l'épaule, la rotation du
        // tronc fait aussi tourner la trajectoire de la main : pas d'effet par mégarde.
        bool sidearm = hand.Y <= head.Y - def.CurveHandBelowHead && Mathf.Abs(elevation) <= def.CurveMaxElevationDegrees;
        float spin = sidearm ? CurveSpin(TurnRate(samples, def.VelocityWindowSeconds), def) : 0f;
        string kind = (power ? "chargé" : "tendu") + (spin > 0 ? ", effet à droite" : spin < 0 ? ", effet à gauche" : "");
        return new ThrowGesture(vel, spin, power ? Ball.ModePower : 0, kind);
    }

    static float Heading(Vector3 v) => Mathf.Atan2(-v.X, -v.Z);

    /// <summary>Vitesses des mesures de la fenêtre (index 0 = plus récente).</summary>
    static List<Vector3> Velocities(IReadOnlyList<HandSample> s, float window)
    {
        var list = new List<Vector3>();
        if (s.Count == 0) return list;
        double t0 = s[0].Time;
        for (int i = 0; i < s.Count && t0 - s[i].Time <= window + 1e-4; i++)
        {
            if (s[i].HasVelocity) { list.Add(s[i].Velocity); continue; }
            if (i + 1 >= s.Count) break;
            double dt = s[i].Time - s[i + 1].Time;
            if (dt <= 1e-4) { list.Add(list.Count > 0 ? list[^1] : Vector3.Zero); continue; }
            list.Add((s[i].Position - s[i + 1].Position) / (float)dt);
        }
        return list;
    }
}
