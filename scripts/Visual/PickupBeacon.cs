using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Plot de ramassage hexagonal : dalle claire (plus claire que le sol), faisceau turquoise (Palette.Beacon) à spirale visible d'une autre zone,
/// anneau de recharge. Le faisceau ne s'allume que si une balle est sur le plot ; sinon l'anneau au sol se remplit pendant l'attente.
/// </summary>
public partial class PickupBeacon : Node3D
{
    public float TopY { get; set; }
    MeshInstance3D beam = null!, ring = null!;
    readonly MeshInstance3D[] spiral = new MeshInstance3D[3];
    bool loaded;
    double emptyFor, age;

    public override void _Ready()
    {
        var slab = ModelLibrary.Instance("plot_neutre");
        if (slab != null) { slab.Position = new Vector3(0, -0.02f, 0); AddChild(slab); }
        else AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.62f, BottomRadius = 0.62f, Height = 0.04f, RadialSegments = 6, Rings = 1 },
            MaterialOverride = Materials.Flat(new Color("6b7389")), Position = new Vector3(0, 0.03f, 0), RotationDegrees = new Vector3(0, 30, 0),
        });
        beam = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.16f, Height = 5f, RadialSegments = 8 },
            MaterialOverride = Materials.Additive(new Color(Palette.Beacon, 0.30f)), Position = new Vector3(0, 2.5f + TopY, 0),
        };
        AddChild(beam);
        for (int i = 0; i < spiral.Length; i++)
        {
            spiral[i] = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 0.22f, OuterRadius = 0.26f, Rings = 24, RingSegments = 6 },
                MaterialOverride = Materials.Additive(new Color(Palette.Beacon, 0.7f)),
            };
            AddChild(spiral[i]);
        }
        ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.5f, OuterRadius = 0.58f, Rings = 36, RingSegments = 4 },
            MaterialOverride = Materials.Additive(new Color(Palette.Beacon, 0.8f)), Position = new Vector3(0, 0.07f, 0), Scale = new Vector3(1, 0.1f, 1),
        };
        AddChild(ring);
    }

    public override void _PhysicsProcess(double delta)
    {
        age += delta;
        loaded = false;
        foreach (var n in GetTree().GetNodesInGroup("balls"))
            if (n is Node3D b && b.GlobalPosition.DistanceTo(GlobalPosition + new Vector3(0, TopY + 0.15f, 0)) < 0.7f) { loaded = true; break; }
        emptyFor = loaded ? 0 : emptyFor + delta;
        beam.Visible = loaded;
        // Recharge : l'anneau se remplit (rayon croissant) pendant que le plot attend sa prochaine balle.
        float fill = loaded ? 1f : Mathf.Clamp((float)(emptyFor / 6.0), 0.15f, 1f);
        ring.Scale = new Vector3(fill, 0.1f, fill);
        for (int i = 0; i < spiral.Length; i++)
        {
            spiral[i].Visible = loaded;
            float phase = (float)(age * 0.6 + i / (float)spiral.Length) % 1f;
            spiral[i].Position = new Vector3(0, TopY + phase * 4.5f, 0);
            spiral[i].Rotation = new Vector3(Mathf.Pi / 2f, 0, (float)age * 2f + i);
            spiral[i].Scale = Vector3.One * (1f - phase * 0.5f);
        }
    }
}
