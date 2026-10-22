using Godot;

namespace DogeballVR.Visual;

/// <summary>Caméra de capture (`--vista`) : tour lent du toit à hauteur d'œil, regard vers la ville. Sert aux images de décor.</summary>
public partial class VistaCamera : Camera3D
{
    double t;

    public override void _Ready()
    {
        Fov = 62f;
        Current = true;
    }

    public override void _Process(double delta)
    {
        t += delta;
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
