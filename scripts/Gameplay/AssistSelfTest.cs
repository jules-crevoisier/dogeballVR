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
        Check("Balle volée", Ball.ChargeOnCatch(2, false, true, false) == 0 && Ball.ChargeOnCatch(2, false, true, true) == 1, "0, ou 1 sur attrape parfaite");

        GD.Print(ok ? "[AssistTest] TOUT OK" : "[AssistTest] ÉCHEC");
        GetTree().Quit(ok ? 0 : 1);
    }
}
