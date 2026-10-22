using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Caméra de capture (`--vista`) : tour lent du toit à hauteur d'œil, regard vers la ville. Sert aux images de décor.
/// `--vista-orbit` : plan de bande-annonce, tour lent en hauteur autour de l'arène, regard sur le centre pendant que la partie se joue.
/// </summary>
public partial class VistaCamera : Camera3D
{
    double t;

    public override void _Ready()
    {
        Fov = 62f;
        Current = true;
    }

    static readonly bool Orbit = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--vista-orbit") >= 0;

    public override void _Process(double delta)
    {
        t += delta;
        if (Orbit)
        {
            float o = 0.6f + (float)(t * 0.12);
            GlobalPosition = new Vector3(Mathf.Cos(o) * 17f, 9.5f - (float)t * 0.25f, Mathf.Sin(o) * 13f);
            LookAt(new Vector3(0, 0.8f, 0), Vector3.Up);
            Current = true;
            return;
        }
        float a = (float)(t * 0.35);
        var from = new Vector3(Mathf.Cos(a) * 12f, 1.8f, Mathf.Sin(a) * 9f);
        var dir = new Vector3(Mathf.Cos(a + 0.35f), 0f, Mathf.Sin(a + 0.35f));
        GlobalPosition = from;
        LookAt(from + dir * 10f + new Vector3(0, 2.2f, 0), Vector3.Up);
        Current = true;
        if (++frames % 60 == 0)   // budget mesuré : appels de rendu et triangles de l'image
            GD.Print($"[Budget] {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} appels, " +
                     $"{Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)} triangles, {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame)} objets");
    }
    int frames;
}
