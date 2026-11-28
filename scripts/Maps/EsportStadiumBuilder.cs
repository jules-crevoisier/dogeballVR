using Godot;
using DogeballVR.Visual;

namespace DogeballVR.Maps;

/// <summary>
/// Architecture 3D procédurale des arènes esport futuristes lumineuses (style Overwatch / The Finals / EVA VR),
/// fidèles aux concepts Solaris Dome (stade sous dôme vitré) et Helios Concourse (grand hall commun traversable).
/// Optimisé pour Meta Quest 72 FPS (géométrie simple, matériaux partagés, zéro surcharge de draw calls).
/// </summary>
public static class EsportStadiumBuilder
{
    public static bool Handles(string? decor) => decor is "solaris_dome" or "helios_concourse" or "tuto_arena";

    public static void Build(Node3D root, MapData map)
    {
        var holder = new Node3D { Name = $"Stadium_{map.Decor}" };
        root.AddChild(holder);

        bool isHelios = map.Decor == "helios_concourse";
        var white = Materials.Flat(new Color(isHelios ? "DDE3EE" : "E6ECF4"));
        var slate = Materials.Flat(new Color(isHelios ? "1C2230" : "252D3D"));
        var floorMat = Materials.Flat(new Color(isHelios ? "5E697D" : "8692A6"));
        var outerFloor = Materials.Flat(new Color(isHelios ? "465063" : "6B778C"));
        var standMat = Materials.Flat(new Color(isHelios ? "7D8799" : "A4AEBE"));
        var campOrange = Materials.Flat(new Color(isHelios ? "9E5A3E" : "B87D63"));
        var campBlue = Materials.Flat(new Color(isHelios ? "3B6B9E" : "5E8AB8"));
        var glassMat = new StandardMaterial3D
        {
            AlbedoColor = new Color("9CD8FF", 0.16f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.08f,
            Metallic = 0.1f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var lineWhite = Materials.Glow(new Color("FFFFFF"), 0.85f);
        var glowOrange = Materials.Glow(new Color("FF6A1A"), 1.6f);
        var glowBlue = Materials.Glow(new Color("1995FF"), 1.6f);
        var glowCyan = Materials.Glow(isHelios ? new Color("FFB703") : Palette.Beacon, 1.5f);

        float w = map.Bounds.MaxX - map.Bounds.MinX;
        float d = map.Bounds.MaxZ - map.Bounds.MinZ;
        float hw = w * 0.5f, hd = d * 0.5f;

        static void Box(Node3D parent, Vector3 pos, Vector3 size, Material mat, Vector3? rotDeg = null)
        {
            var m = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = size },
                MaterialOverride = mat,
                Position = pos,
            };
            if (rotDeg is Vector3 r) m.RotationDegrees = r;
            parent.AddChild(m);
        }

        // Sol principal athlétique + zones de camp Orange (gauche) / Bleu (droite) + marquage des 3 lanes.
        Box(holder, new Vector3(0, -0.06f, 0), new Vector3(w + 1.5f, 0.12f, d + 1.5f), floorMat);
        Box(holder, new Vector3(-hw * 0.62f, 0.002f, 0), new Vector3(hw * 0.68f, 0.008f, d - 3.6f), campOrange);
        Box(holder, new Vector3(hw * 0.62f, 0.002f, 0), new Vector3(hw * 0.68f, 0.008f, d - 3.6f), campBlue);
        Box(holder, new Vector3(0, -0.14f, 0), new Vector3(w + 26f, 0.16f, d + 26f), outerFloor);

        // Lignes des 3 lanes (couloirs Nord / Centre / Sud) + ligne médiane.
        Box(holder, new Vector3(0, 0.008f, 0), new Vector3(0.18f, 0.01f, d - 1.2f), lineWhite);
        Box(holder, new Vector3(0, 0.008f, -hd * 0.52f), new Vector3(w - 2.4f, 0.01f, 0.12f), lineWhite);
        Box(holder, new Vector3(0, 0.008f, hd * 0.52f), new Vector3(w - 2.4f, 0.01f, 0.12f), lineWhite);
        Box(holder, new Vector3(-hw + 0.6f, 0.008f, 0), new Vector3(0.14f, 0.01f, d - 1.2f), glowOrange);
        Box(holder, new Vector3(hw - 0.6f, 0.008f, 0), new Vector3(0.14f, 0.01f, d - 1.2f), glowBlue);
        Box(holder, new Vector3(-hw * 0.5f, 0.008f, -hd + 0.6f), new Vector3(hw - 0.8f, 0.01f, 0.14f), glowOrange);
        Box(holder, new Vector3(-hw * 0.5f, 0.008f, hd - 0.6f), new Vector3(hw - 0.8f, 0.01f, 0.14f), glowOrange);
        Box(holder, new Vector3(hw * 0.5f, 0.008f, -hd + 0.6f), new Vector3(hw - 0.8f, 0.01f, 0.14f), glowBlue);
        Box(holder, new Vector3(hw * 0.5f, 0.008f, hd - 0.6f), new Vector3(hw - 0.8f, 0.01f, 0.14f), glowBlue);

        // Anneaux concentriques lumineux au centre de l'arène.
        foreach (var (rIn, rOut, mat) in new (float, float, Material)[]
        {
            (1.5f, 1.72f, glowCyan),
            (4.4f, 4.62f, lineWhite),
            (5.2f, 5.34f, glowCyan),
        })
        {
            holder.AddChild(new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = rIn, OuterRadius = rOut, Rings = 40, RingSegments = 6 },
                MaterialOverride = mat,
                Position = new Vector3(0, 0.015f, 0),
                Scale = new Vector3(1, 0.15f, 1),
            });
        }

        // Murets périphériques propres (bord d'arène + verre transparent).
        void WallSide(Vector3 pos, Vector3 size, Material accent)
        {
            Box(holder, pos, size, white);
            Box(holder, pos + new Vector3(0, size.Y * 0.5f + 0.05f, 0), new Vector3(size.X, 0.08f, size.Z), slate);
            Box(holder, pos + new Vector3(0, size.Y * 0.5f + 0.85f, 0), new Vector3(size.X * 0.98f, 1.5f, Mathf.Min(size.X, size.Z) * 0.25f), glassMat);
            Box(holder, pos + new Vector3(0, size.Y * 0.5f + 1.62f, 0), new Vector3(size.X, 0.09f, size.Z * 0.6f), accent);
        }
        WallSide(new Vector3(0, 0.6f, -hd - 0.6f), new Vector3(w + 1.4f, 1.2f, 0.55f), lineWhite);
        WallSide(new Vector3(0, 0.6f, hd + 0.6f), new Vector3(w + 1.4f, 1.2f, 0.55f), lineWhite);
        WallSide(new Vector3(-hw - 0.6f, 0.6f, 0), new Vector3(0.55f, 1.2f, d + 1.4f), glowOrange);
        WallSide(new Vector3(hw + 0.6f, 0.6f, 0), new Vector3(0.55f, 1.2f, d + 1.4f), glowBlue);

        // Gradins de stade propres en arrière-plan (nord & sud).
        for (int s = -1; s <= 1; s += 2)
        {
            for (int row = 0; row < 5; row++)
            {
                float z = s * (hd + 2.6f + row * 1.45f);
                float y = 0.8f + row * 0.75f;
                Box(holder, new Vector3(0, y, z), new Vector3(w + 10f, 0.72f, 1.35f), row % 2 == 0 ? standMat : white);
            }
            Box(holder, new Vector3(0, 5.2f, s * (hd + 10.2f)), new Vector3(w + 14f, 2.2f, 0.6f), slate);
        }

        // Pads lumineux au sol pour rendre les tremplins immédiatement lisibles.
        if (map.Pc != null)
        {
            foreach (var pad in map.Pc.JumpPads)
            {
                holder.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = pad.Radius, BottomRadius = pad.Radius + 0.12f, Height = 0.08f, RadialSegments = 20 },
                    MaterialOverride = slate,
                    Position = new Vector3(pad.X, 0.04f, pad.Z),
                });
                holder.AddChild(new MeshInstance3D
                {
                    Mesh = new TorusMesh { InnerRadius = pad.Radius * 0.65f, OuterRadius = pad.Radius * 0.92f, Rings = 24, RingSegments = 6 },
                    MaterialOverride = glowCyan,
                    Position = new Vector3(pad.X, 0.09f, pad.Z),
                    Scale = new Vector3(1, 0.25f, 1),
                });
            }
        }

        if (map.Decor == "solaris_dome")
            BuildSolarisArchAndDome(holder, hw, hd, white, slate, glowOrange, glowBlue, glassMat);
        else
            BuildHeliosConcourseHall(holder, hw, hd, white, slate, glowOrange, glowBlue, glassMat);
    }

    static void BuildSolarisArchAndDome(Node3D holder, float hw, float hd, Material white, Material slate, Material glowOrange, Material glowBlue, Material glass)
    {
        // Deux grands piliers d'arche (Orange côté gauche, Bleu côté droit) + arche cintrée monumentale.
        static void Box(Node3D p, Vector3 pos, Vector3 sz, Material m, Vector3? rot = null)
        {
            var mi = new MeshInstance3D { Mesh = new BoxMesh { Size = sz }, MaterialOverride = m, Position = pos };
            if (rot is Vector3 r) mi.RotationDegrees = r;
            p.AddChild(mi);
        }

        float span = hw + 1.8f;
        float archH = 12.5f;
        const int segs = 14;
        for (int i = 0; i < segs; i++)
        {
            float a0 = Mathf.Pi * (i / (float)segs);
            float a1 = Mathf.Pi * ((i + 1) / (float)segs);
            float am = (a0 + a1) * 0.5f;
            var p0 = new Vector3(-Mathf.Cos(a0) * span, Mathf.Sin(a0) * archH + 1.8f, 0);
            var p1 = new Vector3(-Mathf.Cos(a1) * span, Mathf.Sin(a1) * archH + 1.8f, 0);
            var mid = (p0 + p1) * 0.5f;
            float len = p0.DistanceTo(p1) + 0.35f;
            float deg = Mathf.RadToDeg(Mathf.Atan2(p1.Y - p0.Y, p1.X - p0.X));
            Box(holder, mid, new Vector3(len, 1.35f, 1.9f), white, new Vector3(0, 0, deg));
            Box(holder, mid + new Vector3(0, -0.58f, 0), new Vector3(len, 0.25f, 1.55f), slate, new Vector3(0, 0, deg));
        }

        // Pieds massifs de l'arche avec liserés d'équipe.
        Box(holder, new Vector3(-span, 2.2f, 0), new Vector3(2.1f, 4.4f, 2.2f), slate);
        Box(holder, new Vector3(-span, 2.2f, 0), new Vector3(1.5f, 4.5f, 2.3f), white);
        Box(holder, new Vector3(-span + 1.08f, 2.0f, 0), new Vector3(0.08f, 3.6f, 0.25f), glowOrange);

        Box(holder, new Vector3(span, 2.2f, 0), new Vector3(2.1f, 4.4f, 2.2f), slate);
        Box(holder, new Vector3(span, 2.2f, 0), new Vector3(1.5f, 4.5f, 2.3f), white);
        Box(holder, new Vector3(span - 1.08f, 2.0f, 0), new Vector3(0.08f, 3.6f, 0.25f), glowBlue);

        // Anneau supérieur du dôme verrière (ouvert sur le ciel bleu).
        holder.AddChild(new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 8.5f, OuterRadius = 9.4f, Rings = 36, RingSegments = 8 },
            MaterialOverride = white,
            Position = new Vector3(0, 15.2f, 0),
            Scale = new Vector3(1.25f, 0.5f, 1f),
        });
        // Nervures radiales de la verrière.
        for (int k = 0; k < 8; k++)
        {
            float ang = k * Mathf.Pi / 4f;
            var dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
            Box(holder, dir * 16f + new Vector3(0, 12.2f, 0), new Vector3(0.45f, 0.55f, 18f), white, new Vector3(18f, -Mathf.RadToDeg(ang) + 90f, 0));
        }
        // Verrière légère au sommet.
        Box(holder, new Vector3(0, 15.6f, 0), new Vector3(22f, 0.08f, 18f), glass);
    }

    static void BuildHeliosConcourseHall(Node3D holder, float hw, float hd, Material white, Material slate, Material glowOrange, Material glowBlue, Material glass)
    {
        static void Box(Node3D p, Vector3 pos, Vector3 sz, Material m, Vector3? rot = null)
        {
            var mi = new MeshInstance3D { Mesh = new BoxMesh { Size = sz }, MaterialOverride = m, Position = pos };
            if (rot is Vector3 r) mi.RotationDegrees = r;
            p.AddChild(mi);
        }

        // Galeries latérales vitrées (Orange à l'ouest, Bleu à l'est) comme dans le concept Helios Concourse.
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * (hw + 2.4f);
            var accent = s < 0 ? glowOrange : glowBlue;
            Box(holder, new Vector3(x, 4.4f, 0), new Vector3(3.4f, 0.55f, hd * 2f + 4f), white);
            Box(holder, new Vector3(x, 8.6f, 0), new Vector3(4.2f, 0.8f, hd * 2f + 6f), white);
            Box(holder, new Vector3(x - s * 1.55f, 6.5f, 0), new Vector3(0.12f, 3.8f, hd * 2f + 2f), glass);
            Box(holder, new Vector3(x - s * 1.55f, 4.75f, 0), new Vector3(0.14f, 0.14f, hd * 2f + 2f), accent);
            Box(holder, new Vector3(x - s * 1.55f, 8.25f, 0), new Vector3(0.14f, 0.14f, hd * 2f + 2f), accent);
        }

        // Grande verrière géométrique au plafond (quadrillage blanc lumineux laissant passer le ciel).
        float roofY = 10.6f;
        for (int i = -3; i <= 3; i++)
            Box(holder, new Vector3(i * 5.2f, roofY, 0), new Vector3(0.45f, 0.65f, hd * 2f + 4f), white);
        for (int j = -2; j <= 2; j++)
            Box(holder, new Vector3(0, roofY, j * 5.6f), new Vector3(hw * 2f + 4f, 0.65f, 0.45f), white);
        Box(holder, new Vector3(0, roofY + 0.35f, 0), new Vector3(hw * 2f + 2f, 0.08f, hd * 2f + 2f), glass);
        // Cadre de plafond blanc.
        Box(holder, new Vector3(0, roofY - 0.2f, -hd - 1.5f), new Vector3(hw * 2f + 8f, 1.4f, 2.2f), slate);
        Box(holder, new Vector3(0, roofY - 0.2f, hd + 1.5f), new Vector3(hw * 2f + 8f, 1.4f, 2.2f), slate);
    }
}
