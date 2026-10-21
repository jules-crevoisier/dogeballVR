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
        GroundRing.Spawn(parent, new Vector3(position.X, 0.06f, position.Z), radius);
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

/// <summary>Anneau au sol de la zone d'effet de la bombe : reste 1,2 s à son rayon exact (BombRadius) pour montrer qui était dans le souffle.</summary>
public partial class GroundRing : MeshInstance3D
{
    const float Life = 1.2f;
    double age;
    StandardMaterial3D material = null!;

    public static void Spawn(Node parent, Vector3 position, float radius)
    {
        var r = new GroundRing { Mesh = new TorusMesh { InnerRadius = radius - 0.07f, OuterRadius = radius, Rings = 48, RingSegments = 4 }, Scale = new Vector3(1, 0.05f, 1) };
        r.material = Materials.Additive(new Color(1f, 0.75f, 0.15f, 0.7f));
        r.MaterialOverride = r.material;
        parent.AddChild(r);
        r.GlobalPosition = position;
    }

    public override void _Process(double delta)
    {
        age += delta;
        material.AlbedoColor = new Color(1f, 0.75f, 0.15f, 0.7f * Mathf.Clamp(1f - (float)(age / Life), 0f, 1f));
        if (age >= Life) QueueFree();
    }
}
