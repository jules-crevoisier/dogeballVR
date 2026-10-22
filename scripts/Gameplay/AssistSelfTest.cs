using System.Collections.Generic;
using Godot;

namespace DogeballVR.Gameplay;

/// <summary>
/// Test headless (--assist-test) des aides VR de Assist, sans casque : attrape par fenêtre de grip à 80 ms de latence,
/// main fermée en permanence, contrôle de l'hôte (prise à distance refusée, avance de 30 à 150 ms et rebond acceptés),
/// temps de vol minimal d'un tir, attrape parfaite à 3 m, règles de manche avec un joueur parti. Quitte avec le code 0 si tout passe.
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
                    if (Assist.GripValid(t, pressedAt) && Assist.HostAccepts(host, hand, speed, Assist.PathDistance(PathTo(host, dir * speed), hand))) got = true;
                    if (Assist.GripValid(t, -10) && Assist.HostAccepts(host, hand, speed, Assist.PathDistance(PathTo(host, dir * speed), hand))) gotClosed = true;
                }
                pos = next;
            }
            if (got) caught++;
            if (gotClosed) closedCaught++;
        }
        Check("attrape à 80 ms", caught >= 95, $"{caught}/100 (>= 95 requis)");
        Check("main fermée en permanence", closedCaught == 0, $"{closedCaught}/100 (0 requis)");

        // Trajectoire gardée par l'hôte (Ball.PathSeconds, un point par tick) d'une balle en ligne droite qui finit en `host`.
        List<Vector3> PathTo(Vector3 host, Vector3 velocity)
        {
            var path = new List<Vector3>();
            for (float t = (float)Ball.PathSeconds; t >= 0f; t -= dt) path.Add(host - velocity * t);
            return path;
        }

        // 1b) Triche : un client déclare la prise d'un tir qui passe à 1,8..3 m de sa main sans jamais s'en approcher.
        int stolen = 0;
        for (int i = 0; i < 100; i++)
        {
            float speed = rng.RandfRange(8f, 20f), miss = rng.RandfRange(1.8f, 3f);
            var velocity = new Vector3(0, 0, -speed);
            var host = new Vector3(miss, 0, rng.RandfRange(-1f, 1f));   // la balle croise la main de côté
            if (Assist.HostAccepts(host, Vector3.Zero, speed, Assist.PathDistance(PathTo(host, velocity), Vector3.Zero))) stolen++;
        }
        Check("prise à distance refusée", stolen == 0, $"{stolen}/100 acceptées (0 requis)");

        // 1c) Prise légitime, hôte en avance de 30, 100 et 150 ms sur ce que voyait le client, écart latéral <= 0,5 m.
        int legit = 0, legitTotal = 0;
        foreach (float lead in new[] { 0.03f, 0.1f, 0.15f })
            for (int i = 0; i < 50; i++, legitTotal++)
            {
                float speed = rng.RandfRange(8f, 20f);
                var velocity = new Vector3(0, 0, -speed);
                var host = new Vector3(rng.RandfRange(-0.5f, 0.5f), 0, 0) + velocity * lead;
                if (Assist.HostAccepts(host, Vector3.Zero, speed, Assist.PathDistance(PathTo(host, velocity), Vector3.Zero))) legit++;
            }
        Check("prise avec 30 à 150 ms d'avance de l'hôte acceptée", legit == legitTotal, $"{legit}/{legitTotal}");

        // 1d) Rebond juste derrière la main : la balle frôle la main (0,2 m), tape le mur 1 m derrière et repart de côté ;
        // l'hôte, 0,22 s plus tard, la voit à plus de 2 m, sa vitesse ne pointe plus vers la main.
        {
            const float speed = 18f;
            var path = new List<Vector3>();
            for (float z = 6f; z > -1f; z -= speed * dt) path.Add(new Vector3(0.2f, 0, z));   // arrivée
            var wall = new Vector3(0.2f, 0, -1f);
            var bounceDir = new Vector3(1f, 0, 0.3f).Normalized();
            for (int i = 0; i <= 13; i++) path.Add(wall + bounceDir * speed * 0.55f * (i * dt));   // rebond à 0,55, 13 pas (0,22 s)
            var host = path[^1];
            Check("prise juste avant un rebond acceptée", host.Length() > Assist.TrajectoryCheckFrom && Assist.HostAccepts(host, Vector3.Zero, speed * 0.55f, Assist.PathDistance(path, Vector3.Zero)),
                $"balle à {host.Length():0.0} m après le rebond");
            // Même balle, main 1,9 m plus loin dans le sens du rebond : assez près pour la marge de latence,
            // mais la balle ne fait que s'en approcher (sa trajectoire n'est jamais passée à portée) : refus.
            var farHand = host + new Vector3(1.9f, 0, 0);
            Check("prise d'une balle qui n'est pas encore arrivée refusée", !Assist.HostAccepts(host, farHand, speed * 0.55f, Assist.PathDistance(path, farHand)), "main à 1,9 m devant la balle");
        }

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

        // Caméra du PC : un coup suivi d'une image très longue (chargement, Alt+Tab) ne doit pas faire diverger le ressort.
        Visual.CameraFx.Reset();
        Visual.CameraFx.Kick(new Vector2(3f, 2f));
        var (fxRot, _, _) = Visual.CameraFx.Step(0.5f);
        for (int i = 0; i < 30; i++) (fxRot, _, _) = Visual.CameraFx.Step(1f / 60f);
        Visual.CameraFx.Reset();
        Check("Ressort de caméra stable", float.IsFinite(fxRot.X) && Mathf.Abs(fxRot.X) < 0.5f && Mathf.Abs(fxRot.Y) < 0.5f, $"après une image de 0,5 s : {fxRot}");

        // Lift VR : un enfant (tête à 1,15 m debout) ne le déclenche pas debout ; l'adulte moyen garde le seuil de 1,15 m.
        Check("Accroupi du lift relatif à la taille", LiftController.CrouchThreshold(1.15f) < 1.0f && Mathf.IsEqualApprox(LiftController.CrouchThreshold(1.6f), 1.152f)
            && LiftController.CrouchThreshold(1.85f) > 1.15f && LiftController.CrouchThreshold(2.1f) < 1.25f, $"enfant {LiftController.CrouchThreshold(1.15f):0.00} m, adulte {LiftController.CrouchThreshold(1.6f):0.00} m, grand {LiftController.CrouchThreshold(1.85f):0.00} m");

        // Sécurité en salle : toute carte jouable en casque garde 1,5 m entre zones adverses et alerte dès 1,5 m du bord.
        {
            var unsafeMaps = new System.Collections.Generic.List<string>();
            int vrMaps = 0;
            foreach (var file in DirAccess.GetFilesAt("res://maps"))
            {
                if (!file.EndsWith(".json")) continue;
                var m = Maps.MapData.Load($"res://maps/{file}");
                if (!m.VrAllowed) continue;
                vrMaps++;
                float gap = m.MinAdversaryGap();
                if (gap < 1.5f - 0.001f || m.EdgeWarningDistance < 1.5f) unsafeMaps.Add($"{m.Name} (écart {gap:0.0#} m, alerte {m.EdgeWarningDistance:0.0#} m)");
            }
            Check("Cartes VR : 1,5 m entre zones adverses", vrMaps > 0 && unsafeMaps.Count == 0, unsafeMaps.Count == 0 ? $"{vrMaps} cartes VR conformes" : string.Join(", ", unsafeMaps));
        }

        // Réglages de salle EVA : fichier de l'opérateur lu et borné (vibrations en %, durée de fin entre 5 et 120 s).
        {
            var previous = FileAccess.FileExists(UI.EvaSettings.FilePath) ? FileAccess.GetFileAsBytes(UI.EvaSettings.FilePath) : null;   // fichier réel d'un poste : remis après
            var cfg = new ConfigFile();
            cfg.SetValue("eva", "haptics", 40);
            cfg.SetValue("eva", "volume", 150);
            cfg.SetValue("eva", "reduce_flashes", true);
            cfg.SetValue("eva", "end_hold", 300);
            cfg.Save(UI.EvaSettings.FilePath);
            UI.EvaSettings.Load();
            bool read = Mathf.IsEqualApprox(UI.EvaSettings.Haptics, 0.4f) && Mathf.IsEqualApprox(UI.EvaSettings.Volume, 1f)
                && UI.EvaSettings.ReduceFlashes && Mathf.IsEqualApprox(UI.EvaSettings.EndHoldSeconds, 120f);
            Check("Réglages de salle EVA (eva.cfg)", read, $"vibrations {UI.EvaSettings.Haptics:0.00}, volume {UI.EvaSettings.Volume:0.00}, fin {UI.EvaSettings.EndHoldSeconds:0} s");
            if (previous != null) { using var f = FileAccess.Open(UI.EvaSettings.FilePath, FileAccess.ModeFlags.Write); f.StoreBuffer(previous); }
            else DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(UI.EvaSettings.FilePath));
            UI.EvaSettings.Haptics = 1f; UI.EvaSettings.Volume = 0.85f; UI.EvaSettings.ReduceFlashes = false; UI.EvaSettings.EndHoldSeconds = 20f;
        }

        // Joueur parti (identifiant inconnu des règles) : aucune exception, rien de compté, pas de boucle.
        {
            var rules = new Rules.MatchRules();
            rules.AddPlayer(1, 0);
            rules.AddPlayer(2, 1);
            bool threw = false, eliminatedUnknown = true;
            try
            {
                rules.RegisterHit(1, 99);
                rules.RegisterHit(99, 2);
                rules.RegisterFall(99);
                rules.RegisterPerfectCatch(99);
                rules.Respawn(99);
                eliminatedUnknown = rules.Eliminate(1, 99) || rules.Eliminate(99, 2);
            }
            catch (System.Exception) { threw = true; }
            bool ringOut = rules.Eliminate(1, 2) && rules.LivesOf(2) == 0 && rules.Kos[0] == 1 && !rules.Eliminate(1, 2);
            Check("règles avec un joueur parti", !threw && !eliminatedUnknown && rules.LivesOf(99) == 0 && !rules.HasPlayer(99) && ringOut,
                $"exception {threw}, KO d'un inconnu {eliminatedUnknown}, ring-out compté une fois {ringOut}");
        }

        GD.Print(ok ? "[AssistTest] TOUT OK" : "[AssistTest] ÉCHEC");
        GetTree().Quit(ok ? 0 : 1);
    }
}
