using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DogeballVR.Tools;

/// <summary>
/// Mesure de performances sur une partie : <c>--perf-log</c> relève chaque image (durée réelle, temps des scripts et de la physique,
/// appels de dessin, triangles, nœuds) et imprime un bilan <c>[Perf]</c> toutes les 30 s et à la fermeture.
/// Le temps CPU se mesure partout (même en headless) ; les appels de dessin et triangles demandent un vrai rendu.
/// </summary>
public partial class PerfProbe : Node
{
    readonly List<float> frameMs = new();
    double processMs, physicsMs, draws, prims;
    int maxDraws, maxNodes, samples;
    ulong last;
    double sinceReport;
    int part;
    int lastG0, lastG2;
    long allocAt = System.GC.GetTotalAllocatedBytes();
    ulong reportAt = Time.GetTicksMsec();
    int gc0 = System.GC.CollectionCount(0), gc2 = System.GC.CollectionCount(2);

    static readonly bool memGc = DevFlags.Has("--mem-gc");

    public static void AttachIfAsked(Node parent)
    {
        if (DevFlags.Has("--perf-log")) parent.AddChild(new PerfProbe { Name = "PerfProbe" });
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        GD.Print($"[Perf] ramasse-miettes : serveur {System.Runtime.GCSettings.IsServerGC}, mode {System.Runtime.GCSettings.LatencyMode}");
    }

    public override void _Process(double delta)
    {
        ulong now = Time.GetTicksUsec();
        if (last != 0)
        {
            float ms = (now - last) / 1000f;
            frameMs.Add(ms);
            int g2now = System.GC.CollectionCount(2), g0now = System.GC.CollectionCount(0);
            if (ms > 50f) GD.Print($"[Perf] image lente : {ms:0} ms à t={Time.GetTicksMsec() / 1000.0:0.0} s{(g2now != lastG2 ? " (GC complète)" : g0now != lastG0 ? " (GC rapide)" : "")}");
            lastG2 = g2now; lastG0 = g0now;
        }
        last = now;
        processMs += Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0;
        physicsMs += Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0;
        int d = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        draws += d;
        maxDraws = Mathf.Max(maxDraws, d);
        prims += Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);
        maxNodes = Mathf.Max(maxNodes, (int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount));
        samples++;
        if ((sinceReport += delta) >= 30) { sinceReport = 0; Report(); }
    }

    public override void _ExitTree() => Report();

    void Report()
    {
        if (frameMs.Count < 10) return;
        var sorted = frameMs.OrderBy(x => x).ToList();
        float avg = sorted.Average();
        float p95 = sorted[(int)(sorted.Count * 0.95f)];
        // « 1 % low » : moyenne du pire centième des images, en i/s.
        int worst = Mathf.Max(1, sorted.Count / 100);
        float low1 = 1000f / sorted.Skip(sorted.Count - worst).Average();
        int slow = sorted.Count(x => x > 1000f / 60f);
        int g0 = System.GC.CollectionCount(0) - gc0, g2 = System.GC.CollectionCount(2) - gc2;
        gc0 = System.GC.CollectionCount(0); gc2 = System.GC.CollectionCount(2);
        long alloc = System.GC.GetTotalAllocatedBytes();
        double secs = Mathf.Max(0.001, (Time.GetTicksMsec() - reportAt) / 1000.0);
        double mbps = (alloc - allocAt) / 1048576.0 / secs;
        allocAt = alloc; reportAt = Time.GetTicksMsec();
        part++;
        GD.Print($"[Perf] tranche {part} : {sorted.Count} images, {1000f / avg:0} i/s en moyenne, 1 % low {low1:0} i/s, p95 {p95:0.0} ms, " +
                 $"scripts {processMs / samples:0.00} ms, physique {physicsMs / samples:0.00} ms, dessin {draws / samples:0} appels (max {maxDraws}), " +
                 $"{prims / samples / 1000:0} k triangles, {maxNodes} nœuds au plus, {slow} images au-delà de 16,7 ms (pire {sorted[^1]:0} ms), " +
                 $"GC : {g0} collectes rapides, {g2} complètes, {mbps:0.0} Mo alloués par seconde, tas {System.GC.GetTotalMemory(false) / 1048576} Mo");
        // Fuites : ces compteurs doivent rester à plat d'une tranche à l'autre sur une longue session.
        // --mem-gc : ramasse-miettes forcé avant la mesure ; sinon les objets Godot que seul un objet C# mort retient
        // attendent la prochaine collecte complète et passent pour une fuite.
        if (memGc) { System.GC.Collect(); System.GC.WaitForPendingFinalizers(); System.GC.Collect(); }
        GD.Print($"[Mémoire] tranche {part} : moteur {OS.GetStaticMemoryUsage() / 1048576.0:0.0} Mo, tas .NET {System.GC.GetTotalMemory(false) / 1048576.0:0.0} Mo, " +
                 $"{(int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)} nœuds, {(int)Performance.GetMonitor(Performance.Monitor.ObjectCount)} objets, " +
                 $"{(int)Performance.GetMonitor(Performance.Monitor.ObjectResourceCount)} ressources, {(int)Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount)} nœuds orphelins");
        frameMs.Clear();
        processMs = physicsMs = draws = prims = 0;
        samples = 0;
        maxDraws = 0;
    }
}
