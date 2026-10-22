using System.Collections.Generic;
using Godot;

namespace DogeballVR.Gameplay;

/// <summary>
/// Test headless (--assist-test) des aides VR de Assist, sans casque : attrape par fenêtre de grip à 80 ms de latence,
/// main fermée en permanence, temps de vol minimal d'un tir, attrape parfaite à 3 m. Quitte avec le code 0 si tout passe.
/// </summary>
public partial class AssistSelfTest : Node
{
    public override void _Ready()
    {
        bool ok = true;
        void Check(string name, bool pass, string detail) { GD.Print($"[AssistTest] {(pass ? "OK  " : "FAIL")} {name} : {detail}"); ok &= pass; }

        var rng = new RandomNumberGenerator { Seed = 42 };
        const float dt = 1f / 60f, latency = 0.08f;
        Assist.VrRules = true;

        // 1) Attrape : balle à 8..20 m/s vers la main (écart latéral <= 0,25 m), grip pressé 0,05..0,30 s avant l'arrivée, hôte en avance de 80 ms.
        int caught = 0, closedCaught = 0;
        for (int i = 0; i < 100; i++)
        {
            float speed = rng.RandfRange(8f, 20f), lateral = rng.RandfRange(-0.25f, 0.25f);
            var hand = Vector3.Zero;
            var dir = new Vector3(0, 0, -1);
            double arrive = 0, pressedAt = arrive - rng.RandfRange(0.05f, 0.30f);
            var pos = new Vector3(lateral, 0, speed * 1.5f);   // 1,5 s avant d'arriver
            bool got = false, gotClosed = false;
            for (double t = arrive - 1.5; t < arrive + 0.6 && !got; t += dt)
            {
                var next = pos + dir * speed * dt;
                if (Assist.Contact(pos, next, hand))
                {
                    var host = next + dir * speed * latency;
                    if (Assist.GripValid(t, pressedAt) && Assist.HostAccepts(host, hand, speed)) got = true;
                    if (Assist.GripValid(t, -10) && Assist.HostAccepts(host, hand, speed)) gotClosed = true;
                }
                pos = next;
            }
            if (got) caught++;
            if (gotClosed) closedCaught++;
        }
        Check("attrape à 80 ms", caught >= 95, $"{caught}/100 (>= 95 requis)");
        Check("main fermée en permanence", closedCaught == 0, $"{closedCaught}/100 (0 requis)");

        // 2) Temps de vol : ennemi plein axe à 3,5..20 m, geste à 30 m/s : le tir ne doit pas arriver en moins de 0,45 s.
        float worst = 99f;
        var from = new Vector3(0, 1.5f, 0);
        for (float d = 3.5f; d <= 20f; d += 0.5f)
        {
            var enemies = new List<Vector3> { from + new Vector3(0, 0, -d) };
            var (_, speed) = Assist.Plan(from, new Vector3(0, 0, -30), enemies);
            worst = Mathf.Min(worst, d / speed);
        }
        Check("temps de vol >= 0,45 s à partir de 3,5 m", worst >= 0.45f, $"pire cas {worst:0.00} s");

        // 2b) Alerte : un tir planifié visant un joueur est signalé au plus tard 0,3 s avant l'impact (alerte dès t <= 0,6 s ou dès le lâcher).
        float minLead = 99f;
        for (float d = 3.5f; d <= 20f; d += 0.5f)
        {
            var target = from + new Vector3(0, 0, -d);
            var (v, speed) = Assist.Plan(from, new Vector3(0, 0, -30), new List<Vector3> { target });
            float t = Visual.ThreatIndicator.TimeToImpact(from, v, target);
            minLead = Mathf.Min(minLead, Mathf.Min(t, Visual.ThreatIndicator.WarnSeconds));
        }
        Check("alerte >= 0,3 s avant l'impact", minLead >= 0.3f, $"préavis minimal {minLead:0.00} s");

        // 3) Attrape parfaite possible à 3 m : la balle arrive à sa vitesse de lancer (plafonnée), fenêtre de grip de 0,15 s.
        {
            var enemies = new List<Vector3> { from + new Vector3(0, 0, -3f) };
            var (_, speed) = Assist.Plan(from, new Vector3(0, 0, -30), enemies);
            Check("attrape parfaite possible à 3 m", Assist.PerfectAllowed(speed, speed) && speed >= Ball.HitMinSpeed, $"vitesse {speed:0.0} m/s, seuil {Ball.HitMinSpeed:0.0}");
            Check("attrape parfaite refusée si la balle a trop ralenti", !Assist.PerfectAllowed(0.7f * speed + 0.1f, speed + 4f) || speed < Ball.HitMinSpeed, "vitesse < 0,8 × lancer");
        }

        // 4) Un geste mou est relevé au minimum réglé, un non-lancer reste tel quel.
        var (_, soft) = Assist.Plan(from, new Vector3(0, 0, -2.5f), new List<Vector3>());
        var (_, none) = Assist.Plan(from, new Vector3(0, 0, -1f), new List<Vector3>());
        Check("plancher de vitesse", Mathf.IsEqualApprox(soft, Assist.MinLaunchSpeed) && none == 0f, $"mou {soft:0.0}, nul {none:0.0}");

        // Paliers de passe : une suite de passes entre alliés ne dépasse jamais le palier 1 (pas de KO en un coup sans risque).
        int c = 0;
        for (int i = 0; i < 4; i++) c = Ball.ChargeOnCatch(c, true, false, false);
        Check("Passes plafonnées", c == 1, $"palier {c} après 4 passes");
        Check("Palier haut gardé", Ball.ChargeOnCatch(2, true, false, false) == 2, "une passe ne fait pas baisser une balle déjà chargée");
        Check("Balle volée", Ball.ChargeOnCatch(2, false, true, false) == 0 && Ball.ChargeOnCatch(1, false, true, true) == 2 && Ball.ChargeOnCatch(3, false, true, true) == 3, "0, ou charge gardée +1 (max 3) sur attrape parfaite");

        KnockoutCityRules(Check);
        // Caméra du PC : un coup suivi d'une image très longue (chargement, Alt+Tab) ne doit pas faire diverger le ressort.
        Visual.CameraFx.Reset();
        Visual.CameraFx.Kick(new Vector2(3f, 2f));
        var (fxRot, _, _) = Visual.CameraFx.Step(0.5f);
        for (int i = 0; i < 30; i++) (fxRot, _, _) = Visual.CameraFx.Step(1f / 60f);
        Visual.CameraFx.Reset();
        Check("Ressort de caméra stable", float.IsFinite(fxRot.X) && Mathf.Abs(fxRot.X) < 0.5f && Mathf.Abs(fxRot.Y) < 0.5f, $"après une image de 0,5 s : {fxRot}");

        GD.Print(ok ? "[AssistTest] TOUT OK" : "[AssistTest] ÉCHEC");
        GetTree().Quit(ok ? 0 : 1);
    }

    /// <summary>Décisions de règles n° 2 à 7 (04-gameplay-complet.md), une vérification au moins par règle, avec les valeurs par défaut des maps.</summary>
    static void KnockoutCityRules(System.Action<string, bool, string> check)
    {
        var defaults = new Maps.RulesDef();
        KcRules.Configure(defaults, true);
        float perfect = Assist.PerfectWindowSeconds, grip = Assist.GripWindowSeconds;

        // n° 2 : 1 cœur par coup quel que soit le palier ; une map peut rétablir l'ancien KO au palier 2.
        bool oneHeart = true;
        for (int c = 0; c <= Ball.MaxCharge; c++) oneHeart &= KcRules.Damage(c) == 1;
        var rules = new Rules.MatchRules();
        rules.AddPlayer(0, 0); rules.AddPlayer(1, 1);
        for (int i = 0; i < KcRules.Damage(Ball.MaxCharge); i++) rules.RegisterHit(0, 1);   // même boucle que MatchController.OnHit
        KcRules.Configure(new Maps.RulesDef { DoubleDamageCharge = 2 }, true);
        bool legacy = KcRules.Damage(2) == 2 && KcRules.Damage(1) == 1;
        KcRules.Configure(defaults, true);
        check("n° 2 : 1 cœur par coup", oneHeart && rules.LivesOf(1) == rules.LivesPerPlayer - 1 && legacy, $"palier 3 : {rules.LivesOf(1)}/{rules.LivesPerPlayer} vies ; doubleDamageCharge = 2 rétablit le KO");

        // n° 2 (VR) : fenêtre normale 0,35 / 0,30 / 0,27 / 0,25 s, jamais moins que parfaite + 0,1 s, parfaite inchangée.
        var w = new float[4];
        for (int c = 0; c < 4; c++) w[c] = Assist.CatchWindow(c, true);
        bool windows = Mathf.IsEqualApprox(w[0], 0.35f) && Mathf.IsEqualApprox(w[1], 0.30f) && Mathf.IsEqualApprox(w[2], 0.27f) && Mathf.IsEqualApprox(w[3], 0.25f)
            && Mathf.IsEqualApprox(Assist.CatchWindow(3, false), grip);
        Assist.PerfectWindowSeconds = 0.2f;
        bool gap = Mathf.IsEqualApprox(Assist.CatchWindow(3, true), 0.30f);
        Assist.PerfectWindowSeconds = perfect;
        // Appui 0,28 s avant l'impact : pris au palier 0, raté au palier 3 ; 0,22 s avant : pris aux deux.
        bool early = Assist.GripValid(0.28, 0, w[0]) && !Assist.GripValid(0.28, 0, w[3]) && Assist.GripValid(0.22, 0, w[3]);
        check("n° 2 : fenêtre d'attrape VR par palier", windows && gap && early, $"{w[0]:0.00} / {w[1]:0.00} / {w[2]:0.00} / {w[3]:0.00} s ; parfaite 0,20 s → palier 3 à 0,30 s");
        var (_, vrSpeed) = Assist.Plan(Vector3.Zero, new Vector3(0, 0, -40), new List<Vector3>());
        check("n° 2 : VR plafonnée à 20 m/s", vrSpeed <= 20f + 1e-3f, $"{vrSpeed:0.0} m/s");

        // n° 2 (PC) : pleine charge 24 m/s, +15 % par palier (cumulé), 40 m/s au plus.
        var shot = new Vector3(0, 0, -30);
        float s0 = KcRules.PcTierVelocity(shot, 0).Length(), s2 = KcRules.PcTierVelocity(shot * 2, 2).Length(), s3 = KcRules.PcTierVelocity(shot * 2, 3).Length();
        float tier1 = 24f * KcRules.PcTierFactor(1);
        KcRules.Configure(new Maps.RulesDef { PcTierSpeedBonus = 0.4f }, false);
        float capped = KcRules.PcTierVelocity(shot * 3, 3).Length();
        KcRules.Configure(defaults, true);
        check("n° 2 : palier = vitesse au PC", Mathf.IsEqualApprox(s0, 24f) && Mathf.Abs(tier1 - 27.6f) < 0.05f && Mathf.Abs(s2 - 31.74f) < 0.05f && Mathf.Abs(s3 - 36.5f) < 0.05f && Mathf.IsEqualApprox(capped, 40f),
            $"{s0:0.0} / {tier1:0.0} / {s2:0.0} / {s3:0.0} m/s, plafond {capped:0.0}");
        check("n° 2 : lift VR +1", KcRules.ChargeAfterLift(0) == 1 && KcRules.ChargeAfterLift(Ball.MaxCharge) == Ball.MaxCharge, $"0 → {KcRules.ChargeAfterLift(0)}");

        // n° 3 : une passe adverse s'intercepte par toute attrape, +1 palier si parfaite (passe au palier 1 → 2).
        bool take = KcRules.CanTake(true, 1, 0) && KcRules.CanTake(true, 0, 0) && KcRules.PerfectOnPass;
        bool tiers = Ball.ChargeOnCatch(1, false, true, true) == 2 && Ball.ChargeOnCatch(1, false, true, false) == 0;
        KcRules.Configure(new Maps.RulesDef { InterceptPasses = false }, true);
        bool off = !KcRules.CanTake(true, 1, 0) && KcRules.CanTake(true, 0, 0);
        KcRules.Configure(defaults, true);
        check("n° 3 : passes interceptables", take && tiers && off, "toute attrape, parfaite = palier 2 ; interceptPasses = false la protège");

        // n° 4 : vraie tentative = balle adverse lancée (≥ 5 m/s) à moins de 0,6 s du corps ou de la main.
        var body = new Vector3(0, 1.2f, 0);
        var hand = body + new Vector3(0.3f, 0.1f, -0.3f);
        KcRules.BallSeen Toward(float dist, float speed, int team, bool free = true) => new(body + new Vector3(0, 0, -dist), new Vector3(0, 0, speed), team, free, 2);
        bool real = KcRules.RealCatchAttempt(body, hand, 0, new[] { Toward(5f, 10f, 1) }) == 2
            && KcRules.RealCatchAttempt(body, hand, 0, new[] { Toward(10f, 10f, 1) }) < 0       // 1 s : trop loin
            && KcRules.RealCatchAttempt(body, hand, 0, new[] { Toward(3f, 10f, 0) }) < 0        // balle alliée
            && KcRules.RealCatchAttempt(body, hand, 0, new[] { Toward(1f, 3f, 1) }) < 0         // balle lente
            && KcRules.RealCatchAttempt(body, hand, 0, new[] { Toward(3f, 10f, -1) }) < 0       // balle neutre
            && KcRules.RealCatchAttempt(body, hand, 0, new[] { Toward(3f, 10f, 1, false) }) < 0; // tenue
        var lockA = new CatchLock();
        lockA.Press(0, Assist.CatchWindow(0, true), true);
        bool notYet = !lockA.Update(0.34) && !lockA.Locked(0.34);
        bool starts = lockA.Update(0.36) && lockA.Locked(0.5) && !lockA.Locked(0.96);
        bool ignored = !lockA.Press(0.6, 0.35, true) && lockA.Press(1.0, 0.35, false);
        var lockB = new CatchLock();
        lockB.Press(0, 0.35, true); lockB.Press(0.3, 0.35, true);   // l'autre main ne prolonge pas la fenêtre
        lockB.Update(0.36);
        var lockC = new CatchLock();
        lockC.Press(0, 0.35, true); lockC.Caught(); lockC.Update(1.0);
        check("n° 4 : blocage de 0,6 s après une vraie tentative", real && notYet && starts && ignored && lockB.Locked(0.4) && !lockC.Locked(0.5),
            "fenêtre puis blocage jusqu'à 0,95 s, appui ignoré pendant, prise = pas de blocage");
        // Critère d'acceptation du § 3.7 : 0 blocage sur 50 prises de plot (balle posée) ou de balle au sol ratées.
        var plots = new CatchLock();
        for (int i = 0; i < 50; i++)
        {
            double t = i * 1.5;
            var ground = new[] { new KcRules.BallSeen(hand + new Vector3(0, -0.2f, -0.4f), Vector3.Zero, -1, true), new KcRules.BallSeen(body + new Vector3(0, -1f, -0.5f), new Vector3(0, 0, 1.5f), 1, true) };
            plots.Press(t, 0.35, KcRules.RealCatchAttempt(body, hand, 0, ground) >= 0);
            plots.Update(t + 1.0);
        }
        check("n° 4 : plots et balles au sol ne bloquent jamais", plots.Locks == 0, $"{plots.Locks} blocage(s) sur 50 prises ratées");

        // n° 5 : l'attrape ne soigne plus ; régénération 10 s en VR (toutes les cartes), aucune au PC.
        bool regen = !KcRules.CatchHeals && Mathf.IsEqualApprox(KcRules.HeartRegenSeconds(true), 10f) && KcRules.HeartRegenSeconds(false) == 0f;

        // n° 6 : échanges suspendus par défaut, réactivables par la carte, forcés par --swap-test.
        bool swaps = KcRules.PlateSwapTimes(false).Length == 0 && KcRules.PlateSwapTimes(true).Length == 4;
        KcRules.Configure(new Maps.RulesDef { PlateSwapTimes = new List<double> { 30, 60 } }, true);
        swaps &= KcRules.PlateSwapTimes(false).Length == 2;
        KcRules.Configure(defaults, true);

        // Les cartes livrées gardent ces valeurs par défaut (aucune ne les redéfinit).
        string mapsDetail = "";
        bool mapsOk = true;
        foreach (var name in new[] { "neon_court", "la_fosse", "demo_2v2", "croix_neon", "fosse_croisee", "tour_4v4" })
        {
            var map = Maps.MapData.Load($"res://maps/{name}.json");
            KcRules.Configure(map.Rules, true);
            bool m = KcRules.Damage(2) == 1 && KcRules.HeartRegenSeconds(true) == 10f && KcRules.HeartRegenSeconds(false) == 0f && !KcRules.CatchHeals
                && KcRules.PlateSwapTimes(false).Length == 0 && KcRules.CanTake(true, 1, 0) && KcRules.RoundBreakSeconds(true) == 20f && KcRules.ChargeAfterLift(0) == 1;
            if (!m) mapsDetail += name + " ";
            mapsOk &= m;
        }
        KcRules.Configure(defaults, true);
        check("n° 5 : pas de soin à l'attrape, régénération VR seule", regen, "attrape 0 cœur, VR 10 s, PC aucune");
        check("n° 6 : échanges de plaques suspendus", swaps, "aucun par défaut, plateSwapTimes les réactive");
        check("Cartes livrées : règles par défaut", mapsOk, mapsOk ? "6 cartes" : "écart : " + mapsDetail);

        // n° 7 : pause de 20 s (hôte VR) ou 12 s (PC), et l'intermission passe au client par le texte du tableau.
        bool pause = KcRules.RoundBreakSeconds(true) == 20f && KcRules.RoundBreakSeconds(false) == 12f;
        var playing = MatchView.FromText(MatchController.FormatHud("MANCHE 2/5", "", 1, 0, 2, 2, 31), default);
        var brk = MatchView.FromText(MatchController.FormatHud(MatchController.BreakBanner("MANCHE POUR L'ÉQUIPE BLEUE", 3, 5, 12), "", 1, 1, 2, 3, 0), playing);
        var decisive = MatchView.FromText(MatchController.FormatHud(MatchController.BreakBanner("MANCHE NULLE", 6, 5, 7), "", 2, 2, 1, 1, 0), playing);
        var legacyText = MatchView.FromText("MANCHE POUR L'ÉQUIPE ORANGE\nÉquipe 1 : 2  -  1 : Équipe 2\nKO 3 - 1   |   0s", playing);
        var cd = MatchView.FromText(MatchController.FormatHud("MANCHE 3/5 - DÉPART DANS 2", "", 1, 1, 0, 0, 120), brk);
        bool text = brk.Phase == MatchView.Phase.RoundOver && brk.Winner == 1 && brk.NextRoundIn == 12 && brk.Round == 2 && brk.Kos0 == 2 && brk.Kos1 == 3 && brk.Rounds1 == 1
            && decisive.Phase == MatchView.Phase.RoundOver && decisive.Winner < 0 && decisive.NextRoundIn == 7
            && legacyText.Phase == MatchView.Phase.RoundOver && legacyText.Winner == 0 && legacyText.NextRoundIn == 0
            && cd.Phase == MatchView.Phase.Countdown && cd.Round == 3 && cd.NextRoundIn == 0;
        check("n° 7 : pause 20 s VR / 12 s PC, intermission lue par le client", pause && text, $"client : {brk.Phase}, gagnant {brk.Winner}, suite dans {brk.NextRoundIn} s, KO {brk.Kos0}-{brk.Kos1}");
    }
}
