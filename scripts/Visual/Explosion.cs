using Godot;

namespace DogeballVR.Visual;

/// <summary>Onde de choc de la balle bombe : une sphère additive qui grossit jusqu'au rayon de l'explosion puis disparaît (0,45 s).</summary>
public partial class Explosion : MeshInstance3D
{
    const float Life = 0.45f;
    double age;
    StandardMaterial3D material = null!;
    float radius;

    public static void Spawn(Node parent, Vector3 position, float radius)
    {
        var e = new Explosion { radius = radius, Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 20, Rings = 10 } };
        e.material = Materials.Additive(new Color(1f, 0.75f, 0.15f, 0.6f));
        e.MaterialOverride = e.material;
        parent.AddChild(e);
        e.GlobalPosition = position;
        e.Scale = Vector3.One * 0.2f;
    }

    public override void _Process(double delta)
    {
        age += delta;
        float t = Mathf.Clamp((float)(age / Life), 0f, 1f);
        Scale = Vector3.One * Mathf.Lerp(0.2f, radius, 1f - (1f - t) * (1f - t));
        material.AlbedoColor = new Color(1f, 0.75f, 0.15f, 0.6f * (1f - t));
        if (t >= 1f) QueueFree();
    }
}
