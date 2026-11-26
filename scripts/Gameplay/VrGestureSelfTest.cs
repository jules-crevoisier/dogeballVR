using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Godot;
using DogeballVR.Maps;

namespace DogeballVR.Gameplay;

/// <summary>
/// Test headless (--gesture-test) des gestes de lancer au casque (<see cref="VrThrowGesture"/>), sans casque : gestes de main
/// synthétiques à 72 images/s (Quest 2), lus comme au lâcher. Vérifie le pic de vitesse (contre l'ancienne moyenne des 4 dernières
/// images), l'effet d'un lancer de côté (sens et zone morte), la cloche par en dessous et sa cible, la passe vers un allié (refusée
/// si un adversaire est dans l'axe), le tir chargé (gâchette), le simple lâcher, le plafond relevé de l'hôte, la taille de zone.
/// Imprime aussi les trajectoires simulées (lignes « TRAJ ») pour l'image des coups. Quitte avec le code 0 si tout passe.
/// </summary>
public partial class VrGestureSelfTest : Node
{
    const double Dt = 1.0 / 72.0;
    static readonly Vector3 Head = new(0, 1.7f, 0);
    static readonly Vector3 Enemy = new(1f, 1.2f, -12f);
    static readonly Vector3 Ally = new(6f, 1.2f, -6f);

    public override void _Ready()
    {
        bool ok = true;
        void Check(string name, bool pass, string detail) { GD.Print($"[GestureTest] {(pass ? "OK  " : "FAIL")} {name} : {detail}"); ok &= pass; }

        var def = new VrGestureDef();
        Assist.VrRules = true;
        Assist.MaxLaunchSpeedVR = 20f; Assist.MinLaunchSpeed = 5.5f; Assist.SpeedMultiplier = 1.4f; Assist.MinFlightSeconds = 0.5f;
        var none = new List<Vector3>();
        var enemies = new List<Vector3> { Enemy };
        var allies = new List<Vector3> { Ally };
        ThrowGesture Read(List<HandSample> s, float charge = 0f, List<Vector3>? al = null, List<Vector3>? en = null) =>
            VrThrowGesture.Read(s, s[0].Position, Head, charge, al ?? none, en ?? enemies, def, Assist.SpeedMultiplier, Assist.MinLaunchSpeed, Assist.MaxLaunchSpeedVR);

        // 1) Par-dessus l'épaule : pic à 10 m/s 30 ms avant le lâcher, la main freine ensuite. Tir tendu, droit, vers l'avant.
        var over = Overhand(10f, new Vector3(0.25f, 1.85f, 0.3f));   // pic 50 ms avant le lâcher
        var peak = VrThrowGesture.PeakVelocity(over, def.VelocityWindowSeconds);
        float old = OldAverage(over).Length();
        Check("Pic de vitesse", peak.Length() > 9f, $"pic lu {peak.Length():0.0} m/s (vrai 10), ancienne moyenne {old:0.0} m/s");
        var g = Read(over);
        Check("Tir tendu par-dessus l'épaule", g.Mode == 0 && g.Spin == 0f && g.Velocity.Length() >= 12.5f && g.Velocity.Normalized().Z < -0.95f,
            $"{g.Kind}, {g.Velocity.Length():0.0} m/s, effet {g.Spin:0.0}");
        // Fouetté : la main plonge au lâcher (élévation -8° au pic, -38° 60 ms plus tard). La balle garde l'élévation du pic.
        var whip = Integrate(new Vector3(0.25f, 1.85f, 0.3f), 0.26, t =>
        {
            float pitch = Mathf.DegToRad(-8f - 30f * Mathf.Clamp((float)((t - 0.20) / 0.06), 0f, 1f));
            return new Vector3(0, Mathf.Sin(pitch), -Mathf.Cos(pitch)) * 10f * Mathf.Exp(-Mathf.Pow((float)((t - 0.20) / 0.05), 2));
        });
        var pw = VrThrowGesture.PeakVelocity(whip, def.VelocityWindowSeconds);
        float whipElev = Mathf.RadToDeg(Mathf.Atan2(pw.Y, new Vector2(pw.X, pw.Z).Length()));
        float lastElev = Mathf.RadToDeg(Mathf.Atan2(whip[0].Position.Y - whip[1].Position.Y, Mathf.Abs(whip[0].Position.Z - whip[1].Position.Z)));
        Check("Fouetté : élévation du pic", whipElev > -16f && whipElev < -4f, $"élévation lue {whipElev:0} ° (pic -8 °, main au lâcher {lastElev:0} °)");
        // Rebond : la main freine sec puis repart en arrière juste avant que le grip s'ouvre. La balle garde la direction du pic.
        var bounce = Integrate(new Vector3(0.25f, 1.85f, 0.3f), 0.25, t => t < 0.22
            ? new Vector3(0, -0.15f, -1f).Normalized() * 10f * Mathf.Exp(-Mathf.Pow((float)((t - 0.18) / 0.04), 2))
            : new Vector3(0.4f, 0, 2.5f));
        var pb = VrThrowGesture.PeakVelocity(bounce, def.VelocityWindowSeconds);
        Check("Rebond au lâcher : direction du pic", pb.Z < -0.9f * pb.Length(), $"direction lue ({pb.X:0.0} ; {pb.Y:0.0} ; {pb.Z:0.0}), main au lâcher vers l'arrière");
        var withVel = WithRuntimeVelocity(over);
        var g2 = Read(withVel);
        Check("Vitesse du runtime", Mathf.Abs(g2.Velocity.Length() - g.Velocity.Length()) < 1.2f, $"{g2.Velocity.Length():0.0} contre {g.Velocity.Length():0.0} m/s");

        // 2) Lancer de côté : le bras balaie, la balle tourne du même côté. Trajectoire presque droite : pas d'effet.
        var left = Sidearm(9f, 10f);
        var right = Sidearm(9f, -10f);
        var straight = Sidearm(9f, 2f);
        var gl = Read(left); var gr = Read(right); var gs = Read(straight);
        Check("Effet à gauche", gl.Spin < -3f && gl.Mode == 0, $"{gl.Kind}, effet {gl.Spin:0.0} (rotation de la main {VrThrowGesture.TurnRate(left, def.VelocityWindowSeconds * 1.5f):0.0} rad/s)");
        Check("Effet à droite", gr.Spin > 3f && gr.Mode == 0, $"{gr.Kind}, effet {gr.Spin:0.0}");
        Check("Zone morte de l'effet", gs.Spin == 0f, $"rotation 2 rad/s : effet {gs.Spin:0.0}");
        // Par-dessus l'épaule avec le tronc qui tourne (trajectoire de la main qui tourne de 8 rad/s, main à hauteur de tête) : pas d'effet.
        var trunk = Integrate(new Vector3(0.3f, 1.8f, 0.2f), 0.2, t => Turn(t, 0.2, 10f, 8f) + Vector3.Down * 1.2f);
        var gt = Read(trunk);
        Check("Tronc qui tourne par-dessus l'épaule : pas d'effet", gt.Spin == 0f && gt.Mode == 0, $"{gt.Kind}, rotation {VrThrowGesture.TurnRate(trunk, def.VelocityWindowSeconds):0.0} rad/s, main à {trunk[0].Position.Y:0.00} m");
        var vl = Assist.Plan(left[0].Position, gl.Velocity, none).velocity;
        var vr = Assist.Plan(right[0].Position, gr.Velocity, none).velocity;
        float dl = Lateral(left[0].Position, vl, Fly(vl, gl.Spin, 0, left[0].Position, out var trajL));
        float dr = Lateral(right[0].Position, vr, Fly(vr, gr.Spin, 0, right[0].Position, out var trajR));
        Check("La balle courbe", dl < -0.5f && dr > 0.5f, $"écart à la ligne de tir à 12 m : gauche {dl:0.00} m, droite {dr:0.00} m");

        // 3) Cloche : par en dessous, main à la hanche, vers le haut. Calée sur l'adversaire du cône.
        var under = Line(new Vector3(0.2f, 0.7f, 0f), new Vector3(0, 1f, -1f).Normalized() * 7f, 0.1);   // lâché à 1,2 m, sous la poitrine
        var gLob = Read(under);
        var land = Fly(gLob.Velocity, 0f, Ball.ModeLob, under[0].Position, out var trajLob);
        Check("Cloche par en dessous", gLob.IsLob, $"{gLob.Kind}, {gLob.Velocity.Length():0.0} m/s");
        Check("Cloche sur l'adversaire visé", new Vector2(land.X - Enemy.X, land.Z - Enemy.Z).Length() < 0.6f, $"chute à ({land.X:0.0} ; {land.Z:0.0}), adversaire à ({Enemy.X:0} ; {Enemy.Z:0})");
        var high = Line(new Vector3(0.2f, 1.9f, 0f), new Vector3(0, 1f, -1f).Normalized() * 9f);
        var gHigh = Read(high);
        Check("Tir montant par-dessus l'épaule : pas une cloche", gHigh.Mode == 0, gHigh.Kind);

        // 4) Passe vers l'allié ; refusée si un adversaire est dans l'axe.
        var passFrom = new Vector3(0.2f, 1.5f, 0f);
        var toAlly = Line(passFrom, new Vector3(Ally.X - passFrom.X, 0, Ally.Z - passFrom.Z).Normalized() * 8f);
        var gPass = Read(toAlly, 0f, allies);
        var passLand = Fly(gPass.Velocity, 0f, Ball.ModePass, toAlly[0].Position, out var trajPass);
        Check("Passe vers l'allié", gPass.IsPass && new Vector2(passLand.X - Ally.X, passLand.Z - Ally.Z).Length() < 0.8f, $"{gPass.Kind}, arrivée à ({passLand.X:0.0} ; {passLand.Z:0.0})");
        var gBlocked = Read(toAlly, 0f, allies, new List<Vector3> { new(8.5f, 1.2f, -8.5f) });
        Check("Adversaire dans l'axe : pas de passe", !gBlocked.IsPass, gBlocked.Kind);
        // Allié à 6° du tir, adversaire à 11° derrière lui : on visait l'adversaire, pas de passe.
        var aim = new Vector3(Ally.X - passFrom.X, 0, Ally.Z - passFrom.Z).Normalized().Rotated(Vector3.Up, Mathf.DegToRad(-6f));
        var nearAxis = Line(passFrom, aim * 8f);
        var foeBehind = passFrom + new Vector3(Ally.X - passFrom.X, 0, Ally.Z - passFrom.Z).Normalized().Rotated(Vector3.Up, Mathf.DegToRad(-17f)) * 14f;
        var gNear = Read(nearAxis, 0f, allies, new List<Vector3> { foeBehind });
        Check("Adversaire presque dans l'axe : pas de passe", !gNear.IsPass, gNear.Kind);

        // 5) Tir chargé : gâchette tenue 0,8 s. Plein : +15 % et plafond relevé ; 90 % : tir normal.
        var hard = Overhand(16f, new Vector3(0.25f, 1.85f, 0.3f));
        var gNorm = Read(hard);
        var gPow = Read(hard, VrThrowGesture.ChargeFraction(0.85, def));
        var gPart = Read(hard, VrThrowGesture.ChargeFraction(0.72, def));
        Check("Tir chargé", gPow.IsPower && gPow.Velocity.Length() > gNorm.Velocity.Length() + 2f, $"{gPow.Velocity.Length():0.0} m/s contre {gNorm.Velocity.Length():0.0} m/s");
        Check("Charge incomplète : tir normal", gPart.Mode == 0, $"charge {VrThrowGesture.ChargeFraction(0.72, def):0.00}");
        var (pv, ps) = Assist.Plan(Vector3.Zero, Vector3.Forward * 30f, none, 1f + def.ChargeSpeedBonus);
        var (_, psNear) = Assist.Plan(Vector3.Zero, Vector3.Forward * 30f, new[] { new Vector3(0, 0, -8f) }, 1f + def.ChargeSpeedBonus);
        Check("Hôte : plafond du tir chargé", Mathf.IsEqualApprox(ps, 23f) && psNear < 18.5f && psNear > 18.3f, $"sans adversaire {ps:0.0} m/s, adversaire à 8 m {psNear:0.0} m/s (vol {8f / psNear:0.00} s)");
        Fly(Assist.Plan(hard[0].Position, gPow.Velocity, none, 1.15f).velocity, 0f, 0, hard[0].Position, out var trajPow);
        Fly(Assist.Plan(over[0].Position, g.Velocity, none).velocity, 0f, 0, over[0].Position, out var trajStraight);

        // 6) Main qui s'ouvre sans geste : la balle tombe.
        var still = Line(new Vector3(0.3f, 1.3f, -0.3f), new Vector3(0.2f, -0.3f, 0.1f));
        var gDrop = Read(still);
        Check("Simple lâcher", gDrop.Velocity.Length() < VrThrowGesture.DropSpeed && gDrop.Mode == 0, $"{gDrop.Kind}, {gDrop.Velocity.Length():0.00} m/s");

        // 7) Zone de jeu du casque contre zone du jeu (15 × 8 m).
        var room = new[] { new Vector3(-7.5f, 0, -4), new Vector3(7.5f, 0, -4), new Vector3(7.5f, 0, 4), new Vector3(-7.5f, 0, 4) };
        var small = new[] { new Vector3(-2, 0, -2), new Vector3(2, 0, -2), new Vector3(2, 0, 2), new Vector3(-2, 0, 2) };
        Check("Recalage borné", Xr.ZoneCalibration.CanRecenter(0.4f, 4f) && !Xr.ZoneCalibration.CanRecenter(0.8f, 0f) && !Xr.ZoneCalibration.CanRecenter(0.2f, 8f),
            "0,4 m et 4° acceptés ; 0,8 m ou 8° refusés");
        // Recalages successifs : déjà décalé de 0,4 m, la tête encore 0,4 m plus loin dans le repère recalé : 0,8 m de la salle, refusé.
        var shifted = new Transform3D(Basis.Identity, new Vector3(-0.4f, 0, 0));   // repère après un premier recalage, tête brute à x = 0,4 : inverse de T(0,4)
        var headAgain = new Transform3D(Basis.Identity, new Vector3(0.4f, 1.65f, 0));
        var (cum, _) = Xr.ZoneCalibration.TotalAfterRecenter(shifted, headAgain);
        var (back, yawBack) = Xr.ZoneCalibration.TotalAfterRecenter(shifted, new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(3f)), new Vector3(-0.3f, 1.65f, 0)));
        Check("Recalages successifs : pas de dérive", !Xr.ZoneCalibration.CanRecenter(cum, 0f) && Xr.ZoneCalibration.CanRecenter(back, yawBack),
            $"0,4 m puis 0,4 m : {cum:0.00} m de la salle, refusé ; retour vers le centre : {back:0.00} m et {yawBack:0} °, accepté");
        var (_, yawDown) = Xr.ZoneCalibration.TotalAfterRecenter(Transform3D.Identity, new Transform3D(new Basis(Vector3.Right, Mathf.DegToRad(-75f)), new Vector3(0, 1.65f, 0)));
        Check("Tête penchée : recalage refusé", !Xr.ZoneCalibration.CanRecenter(0f, yawDown), "regard 75° vers le sol : cap indéfini");
        // Coins du terrain dans le contour Guardian : 15 × 8 tracé tel quel tient ; décalé de 1 m, non ; tourné de 30°, non.
        var shiftedRoom = new[] { new Vector3(-6.5f, 0, -4), new Vector3(8.5f, 0, -4), new Vector3(8.5f, 0, 4), new Vector3(-6.5f, 0, 4) };
        var turnedRoom = new Vector3[4];
        for (int i = 0; i < 4; i++) turnedRoom[i] = room[i].Rotated(Vector3.Up, Mathf.DegToRad(30f)) * 1.15f;
        bool inRoom = Xr.ZoneCalibration.ZoneInside(room, 15f, 8f, Transform3D.Identity);
        bool inShifted = Xr.ZoneCalibration.ZoneInside(shiftedRoom, 15f, 8f, Transform3D.Identity);
        bool inTurned = Xr.ZoneCalibration.ZoneInside(turnedRoom, 15f, 8f, Transform3D.Identity);
        Check("Terrain dans le Guardian", inRoom && !inShifted && !inTurned && !Xr.ZoneCalibration.ZoneInside(small, 15f, 8f, Transform3D.Identity),
            $"tel quel {inRoom}, décalé de 1 m {inShifted}, tourné de 30° (boîte plus grande) {inTurned}");
        Check("Zone de jeu", Xr.ZoneCalibration.Fits(Xr.ZoneCalibration.PlayAreaSize(room), 15f, 8f) && !Xr.ZoneCalibration.Fits(Xr.ZoneCalibration.PlayAreaSize(small), 15f, 8f)
            && Xr.ZoneCalibration.Fits(new Vector2(8f, 15f), 15f, 8f), "15 × 8 tient, 4 × 4 non, 8 × 15 tient");

        Print("tendu", trajStraight); Print("effet-gauche", trajL); Print("effet-droite", trajR);
        Print("cloche", trajLob); Print("passe", trajPass); Print("charge", trajPow);
        GD.Print($"[GestureTest] CIBLES adversaire {F(Enemy)} allié {F(Ally)}");

        GD.Print(ok ? "[GestureTest] TOUT OK" : "[GestureTest] ÉCHEC");
        GetTree().Quit(ok ? 0 : 1);
    }

    // ------------------------------------------------------------------ gestes synthétiques (plus récent d'abord)

    /// <summary>Par-dessus l'épaule vers -Z : vitesse en cloche (pic « peak » 50 ms avant le lâcher, la main freine), légèrement vers le bas.</summary>
    static List<HandSample> Overhand(float peak, Vector3 start)
    {
        var dir = new Vector3(0, -0.15f, -1f).Normalized();
        return Integrate(start, 0.25, t => dir * peak * Mathf.Exp(-Mathf.Pow((float)((t - 0.20) / 0.05), 2)));
    }

    /// <summary>De côté : cap de la main qui tourne à « turn » rad/s (positif : vers la gauche), lâché face à -Z, main à hauteur de poitrine.</summary>
    static List<HandSample> Sidearm(float speed, float turn) => Integrate(new Vector3(0.6f, 1.3f, 0f), 0.2, t => Turn(t, 0.2, speed, turn));

    static Vector3 Turn(double t, double T, float speed, float turn)
    {
        float h = turn * (float)(t - T);
        return new Vector3(-Mathf.Sin(h), 0, -Mathf.Cos(h)) * speed;
    }

    /// <summary>Main à vitesse constante pendant « duration » secondes.</summary>
    static List<HandSample> Line(Vector3 start, Vector3 vel, double duration = 0.2) => Integrate(start, duration, _ => vel);

    /// <summary>Écart signé (m, positif à droite) du point d'arrivée à la ligne de tir de départ, vue de dessus.</summary>
    static float Lateral(Vector3 from, Vector3 v0, Vector3 land)
    {
        var dir = new Vector3(v0.X, 0, v0.Z).Normalized();
        return (land - from).Dot(dir.Cross(Vector3.Up));
    }

    static List<HandSample> Integrate(Vector3 start, double duration, System.Func<double, Vector3> vel)
    {
        var list = new List<HandSample>();
        var p = start;
        for (double t = 0; t <= duration + 1e-9; t += Dt)
        {
            list.Insert(0, new HandSample(t, p));
            p += vel(t) * (float)Dt;
        }
        return list;
    }

    static List<HandSample> WithRuntimeVelocity(List<HandSample> s)
    {
        var o = new List<HandSample>();
        for (int i = 0; i < s.Count; i++)
        {
            var v = i + 1 < s.Count ? (s[i].Position - s[i + 1].Position) / (float)(s[i].Time - s[i + 1].Time) : Vector3.Zero;
            o.Add(new HandSample(s[i].Time, s[i].Position, v, true));
        }
        return o;
    }

    /// <summary>Ancienne lecture (avant ce changement) : déplacement moyen sur les 4 dernières images à 60 images/s.</summary>
    static Vector3 OldAverage(List<HandSample> s)
    {
        int last = Mathf.Min(3, s.Count - 1);
        return (s[0].Position - s[last].Position) / (float)(s[0].Time - s[last].Time);
    }

    // ------------------------------------------------------------------ vol simulé (mêmes lois que Ball._PhysicsProcess)

    /// <summary>
    /// Vol de la balle jusqu'à 12 m devant (tirs tendus) ou jusqu'à la retombée à hauteur de torse (cloche, passe). Renvoie le point
    /// d'arrivée. Tir tendu : gravité × 0,6, frottement 0,1, force latérale effet × vitesse × 0,05 / masse 0,6, effet qui s'éteint à 4/s.
    /// </summary>
    static Vector3 Fly(Vector3 v, float spin, int mode, Vector3 from, out List<Vector3> path)
    {
        path = new List<Vector3> { from };
        var p = from;
        const float dt = 1f / 120f;
        float g = mode == 0 ? 9.8f * Ball.GravityFactor : Ball.ArcGravity;
        for (float t = 0; t < 3f; t += dt)
        {
            if (mode == 0)
            {
                var side = v.Cross(Vector3.Up).Normalized();
                v += side * spin * v.Length() * 0.05f / 0.6f * dt;
                v *= 1f - 0.1f * dt;
                spin = Mathf.MoveToward(spin, 0f, dt * 4f);
            }
            v += Vector3.Down * g * dt;
            var next = p + v * dt;
            if (mode == 0 && next.Z <= -12f) { p = p.Lerp(next, (p.Z + 12f) / (p.Z - next.Z)); path.Add(p); return p; }
            if (mode != 0 && v.Y < 0 && next.Y <= 1.2f) { p = p.Lerp(next, (p.Y - 1.2f) / Mathf.Max(p.Y - next.Y, 1e-4f)); path.Add(p); return p; }
            p = next;
            if (path.Count < 400 && (int)(t / dt) % 6 == 0) path.Add(p);
        }
        return p;
    }

    static void Print(string name, List<Vector3> path)
    {
        var sb = new StringBuilder($"[GestureTest] TRAJ {name} ");
        foreach (var p in path) sb.Append(F(p)).Append(';');
        GD.Print(sb.ToString());
    }

    static string F(Vector3 p) => string.Format(CultureInfo.InvariantCulture, "{0:0.00},{1:0.00},{2:0.00}", p.X, p.Y, p.Z);
}
