using Godot;

namespace DogeballVR.Tools;

/// <summary>
/// Ramasse-miettes au petit pas : une collecte rapide (génération 0) dès que 4 Mo ont été alloués depuis la précédente.
/// Sans ça, .NET cale son budget sur le cache du processeur (~90 Mo mesurés) : une collecte toutes les ~30 s, mais de 150
/// à 380 ms, en pleine partie. Mesuré avec <c>--perf-log</c> sur Tour 4v4. Le réglage <c>GCgen0size</c> du runtime n'est pas
/// lu quand Godot lance le jeu, d'où ce rythme imposé depuis le jeu.
/// </summary>
public partial class GcPacer : Node
{
    const long Budget = 4L << 20;
    long last = System.GC.GetTotalAllocatedBytes();

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _Process(double delta)
    {
        long now = System.GC.GetTotalAllocatedBytes();
        if (now - last < Budget) return;
        last = now;
        System.GC.Collect(0, System.GCCollectionMode.Forced, blocking: true, compacting: false);
    }
}
