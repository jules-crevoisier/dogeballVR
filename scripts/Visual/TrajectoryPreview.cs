using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Prévision 3D en temps réel de la trajectoire de la balle (tir tendu, Tir Courbé autour d'un pilier, Tir en Cloche par le haut).
/// Réutilise un pool fixe de sphères lumineuses + un anneau d'impact (zéro allocation GC par image, 100 % fluide).
/// Simule la même physique que <see cref="Gameplay.Ball"/> (gravité + effet Magnus).
/// </summary>
public partial class TrajectoryPreview : Node3D
{
    const int DotCount = 32;

    readonly MeshInstance3D[] dots = new MeshInstance3D[DotCount];
    readonly StandardMaterial3D dotMat = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        NoDepthTest = false,
    };
    MeshInstance3D ring = null!;

    public override void _Ready()
    {
        TopLevel = true;
        var sphere = new SphereMesh { Radius = 0.055f, Height = 0.11f, RadialSegments = 8, Rings = 4 };
        for (int i = 0; i < DotCount; i++)
        {
            dots[i] = new MeshInstance3D
            {
                Mesh = sphere,
                MaterialOverride = dotMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = false,
            };
            AddChild(dots[i]);
        }
        ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.28f, OuterRadius = 0.40f, Rings = 20, RingSegments = 6 },
            MaterialOverride = dotMat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Scale = new Vector3(1, 0.2f, 1),
            Visible = false,
        };
        AddChild(ring);
    }

    public void HidePreview()
    {
        if (!ring.Visible && !dots[0].Visible) return;
        for (int i = 0; i < DotCount; i++) dots[i].Visible = false;
        ring.Visible = false;
    }

    /// <summary>Met à jour l'arc 3D en simulant la physique exacte de <see cref="Gameplay.Ball"/> (GravityFactor / ArcGravity, LinearDamp, Magnus).</summary>
    public void ShowArc(Vector3 start, Vector3 initialVel, float initialSpin, int mode, Color color)
    {
        dotMat.AlbedoColor = new Color(color, 0.92f);
        var space = GetWorld3D()?.DirectSpaceState;
        float stepDt = mode == Gameplay.Ball.ModeLob ? 0.06f : 0.038f;
        float gravity = mode == 0 ? 9.8f * Gameplay.Ball.GravityFactor : Gameplay.Ball.ArcGravity;
        var pos = start;
        var vel = initialVel;
        float spin = initialSpin;
        Vector3 hitPos = start;
        bool hasHit = false;

        for (int i = 0; i < DotCount; i++)
        {
            var prev = pos;
            if (mode == 0)
            {
                if (spin != 0f && vel.LengthSquared() > 1f)
                {
                    var side = vel.Cross(Vector3.Up).Normalized();
                    vel += side * (spin * vel.Length() * 0.13f) * stepDt;
                    spin = Mathf.MoveToward(spin, 0f, stepDt * 1.8f);
                }
                vel *= Mathf.Max(0f, 1f - 0.1f * stepDt);   // même amortissement que Ball.LinearDamp
            }
            vel.Y -= gravity * stepDt;
            pos += vel * stepDt;

            if (space != null)
            {
                var q = PhysicsRayQueryParameters3D.Create(prev, pos, 1u);
                var hit = space.IntersectRay(q);
                if (hit.Count > 0)
                {
                    hitPos = hit["position"].AsVector3();
                    hasHit = true;
                    for (int j = i; j < DotCount; j++) dots[j].Visible = false;
                    break;
                }
            }

            dots[i].Visible = true;
            dots[i].GlobalPosition = pos;
            float s = Mathf.Lerp(1.15f, 0.55f, i / (float)DotCount);
            dots[i].Scale = Vector3.One * s;
            hitPos = pos;
        }

        ring.Visible = true;
        ring.GlobalPosition = hitPos + Vector3.Up * (hasHit ? 0.04f : 0f);
    }
}
