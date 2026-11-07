using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Couvre-chefs du personnage (récompenses de niveau, voir Online.Cosmetics) : formes simples modelées ici, accrochées à l'os
/// de la tête pour suivre les animations. Repère de construction : origine au centre de la tête, +Y en haut, regard vers -Z,
/// en mètres (tête d'environ 0,23 m de haut). Couleurs neutres : l'orange et le bleu restent aux équipes.
/// </summary>
public static class Headwear
{
    /// <summary>Nom du nœud accroché au squelette (un seul couvre-chef à la fois).</summary>
    public const string NodeName = "CouvreChef";

    // Tête du mannequin autour du centre : sommet du crâne et front.
    const float Top = 0.115f, Front = 0.105f, Radius = 0.108f;

    static readonly Color Ink = new("15141a"), Paper = new("f3ecdd"), Lime = new("d2f53c"), Yellow = new("F2C230"),
        Wine = new("7E2A3A"), Straw = new("D9B866"), Gold = new("E8B730"), Glass = new("1C2230");

    static StandardMaterial3D Mat(Color c, float rough = 0.65f, float metal = 0f, float emission = 0f)
    {
        var m = new StandardMaterial3D { AlbedoColor = c, Roughness = rough, Metallic = metal };
        if (emission > 0f) { m.EmissionEnabled = true; m.Emission = c; m.EmissionEnergyMultiplier = emission; }
        return m;
    }

    static MeshInstance3D Part(Mesh mesh, Material mat, Vector3 pos, Vector3? rotDeg = null, Vector3? scale = null) => new()
    {
        Mesh = mesh, MaterialOverride = mat, Position = pos, RotationDegrees = rotDeg ?? Vector3.Zero, Scale = scale ?? Vector3.One,
    };

    /// <summary>Dôme qui coiffe le crâne (demi-sphère un peu plus large que la tête).</summary>
    static SphereMesh Dome(float r, float h) => new() { Radius = r, Height = h, IsHemisphere = true, RadialSegments = 24, Rings = 8 };

    /// <summary>Modèle du couvre-chef, null pour « tête nue » ou un identifiant inconnu.</summary>
    public static Node3D? Build(string? id)
    {
        var root = new Node3D { Name = NodeName };
        switch (id)
        {
            case "casquette":
            {
                var crown = Mat(Paper);
                root.AddChild(Part(Dome(Radius + 0.012f, 0.11f), crown, new Vector3(0, Top - 0.075f, 0.005f)));
                root.AddChild(Part(new BoxMesh { Size = new Vector3(0.17f, 0.012f, 0.1f) }, Mat(Ink), new Vector3(0, Top - 0.07f, -Front - 0.045f), new Vector3(-8, 0, 0)));
                root.AddChild(Part(new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.012f, Height = 0.01f }, Mat(Ink), new Vector3(0, Top + 0.04f, 0.005f)));   // bouton
                root.AddChild(Part(new BoxMesh { Size = new Vector3(0.05f, 0.03f, 0.004f) }, Mat(Lime), new Vector3(0, Top - 0.035f, -Front - 0.012f), new Vector3(-30, 0, 0)));   // écusson
                break;
            }
            case "bandeau":
            {
                var band = Mat(Lime, 0.9f);
                root.AddChild(Part(new TorusMesh { InnerRadius = Radius - 0.014f, OuterRadius = Radius + 0.004f, Rings = 24, RingSegments = 6 }, band,
                    new Vector3(0, Top - 0.088f, 0.004f), new Vector3(-8, 0, 0), new Vector3(1, 2.8f, 1.06f)));   // serré sur le front
                for (int s = -1; s <= 1; s += 2)   // deux pans noués derrière
                    root.AddChild(Part(new BoxMesh { Size = new Vector3(0.03f, 0.1f, 0.008f) }, band, new Vector3(0.025f * s, Top - 0.13f, Radius + 0.02f), new Vector3(-20, 0, 18 * s)));
                break;
            }
            case "bonnet":
            {
                var wool = Mat(Wine, 0.95f);
                root.AddChild(Part(Dome(Radius + 0.012f, 0.15f), wool, new Vector3(0, Top - 0.07f, 0.004f), null, new Vector3(1, 1.25f, 1)));
                root.AddChild(Part(new CylinderMesh { TopRadius = Radius + 0.018f, BottomRadius = Radius + 0.02f, Height = 0.05f, RadialSegments = 24 }, Mat(Paper, 0.95f),
                    new Vector3(0, Top - 0.06f, 0.004f)));   // revers
                root.AddChild(Part(new SphereMesh { Radius = 0.036f, Height = 0.072f }, Mat(Paper, 1f), new Vector3(0, Top + 0.07f, 0.01f)));   // pompon
                break;
            }
            case "lunettes":
            {
                var frame = Mat(Ink, 0.4f);
                var lens = Mat(Glass, 0.08f, 0.6f);
                float y = 0.0f, z = -Front + 0.004f;
                for (int s = -1; s <= 1; s += 2)
                {
                    root.AddChild(Part(new BoxMesh { Size = new Vector3(0.072f, 0.04f, 0.012f) }, frame, new Vector3(0.042f * s, y, z)));
                    root.AddChild(Part(new BoxMesh { Size = new Vector3(0.062f, 0.032f, 0.006f) }, lens, new Vector3(0.042f * s, y, z - 0.005f)));
                    root.AddChild(Part(new BoxMesh { Size = new Vector3(0.008f, 0.01f, Front + 0.01f) }, frame, new Vector3((Radius - 0.004f) * s, y + 0.008f, z / 2 + 0.01f)));   // branches
                }
                root.AddChild(Part(new BoxMesh { Size = new Vector3(0.02f, 0.008f, 0.008f) }, frame, new Vector3(0, y + 0.01f, z)));   // pont
                root.AddChild(Part(new BoxMesh { Size = new Vector3(0.144f, 0.006f, 0.004f) }, Mat(Lime), new Vector3(0, y + 0.022f, z - 0.006f)));   // liseré
                break;
            }
            case "casque":
            {
                var shell = Mat(Yellow, 0.45f);
                root.AddChild(Part(Dome(Radius + 0.02f, 0.13f), shell, new Vector3(0, Top - 0.07f, 0.004f), null, new Vector3(1, 1.05f, 1.08f)));
                root.AddChild(Part(new CylinderMesh { TopRadius = Radius + 0.04f, BottomRadius = Radius + 0.045f, Height = 0.012f, RadialSegments = 28 }, shell,
                    new Vector3(0, Top - 0.072f, -0.004f), null, new Vector3(1, 1, 1.1f)));   // bord
                root.AddChild(Part(new BoxMesh { Size = new Vector3(0.026f, 0.03f, 0.24f) }, shell, new Vector3(0, Top + 0.055f, 0.004f)));   // nervure
                root.AddChild(Part(new BoxMesh { Size = new Vector3(0.05f, 0.03f, 0.004f) }, Mat(Ink), new Vector3(0, Top - 0.03f, -Front - 0.032f), new Vector3(-28, 0, 0)));   // insigne
                break;
            }
            case "bob":
            {
                var cloth = Mat(Straw, 0.95f);
                root.AddChild(Part(new CylinderMesh { TopRadius = Radius - 0.01f, BottomRadius = Radius + 0.016f, Height = 0.085f, RadialSegments = 24 }, cloth,
                    new Vector3(0, Top - 0.04f, 0.004f)));
                root.AddChild(Part(new CylinderMesh { TopRadius = Radius + 0.02f, BottomRadius = Radius + 0.075f, Height = 0.03f, RadialSegments = 28 }, cloth,
                    new Vector3(0, Top - 0.095f, 0.004f)));   // bord tombant
                root.AddChild(Part(new CylinderMesh { TopRadius = Radius + 0.007f, BottomRadius = Radius + 0.013f, Height = 0.022f, RadialSegments = 24 }, Mat(Ink, 0.9f),
                    new Vector3(0, Top - 0.065f, 0.004f)));   // ruban
                break;
            }
            case "crete":
            {
                var hair = Mat(Lime, 0.8f);
                for (int i = 0; i < 6; i++)
                {
                    float t = i / 5f;
                    float z = Mathf.Lerp(-Front + 0.02f, Radius - 0.01f, t);
                    float h = Mathf.Lerp(0.08f, 0.13f, Mathf.Sin(t * Mathf.Pi));
                    float y = Top - 0.012f - 0.06f * (t - 0.4f) * (t - 0.4f) * 2.2f;
                    root.AddChild(Part(new PrismMesh { Size = new Vector3(0.032f, h, 0.06f) }, hair, new Vector3(0, y + h / 2 - 0.01f, z), new Vector3(-25 + 50 * t, 0, 0)));
                }
                break;
            }
            case "couronne":
            {
                var gold = Mat(Gold, 0.35f, 0.3f, 0.12f);
                root.AddChild(Part(new CylinderMesh { TopRadius = Radius - 0.012f, BottomRadius = Radius - 0.022f, Height = 0.05f, RadialSegments = 24 }, gold,
                    new Vector3(0, Top + 0.01f, 0.004f)));
                for (int i = 0; i < 6; i++)
                {
                    float a = Mathf.Tau * i / 6f;
                    var p = new Vector3(Mathf.Sin(a) * (Radius - 0.014f), Top + 0.055f, -Mathf.Cos(a) * (Radius - 0.014f) + 0.004f);
                    root.AddChild(Part(new PrismMesh { Size = new Vector3(0.042f, 0.045f, 0.01f) }, gold, p, new Vector3(0, -Mathf.RadToDeg(a), 0)));
                    root.AddChild(Part(new SphereMesh { Radius = 0.008f, Height = 0.016f }, gold, p + new Vector3(0, 0.026f, 0)));
                }
                root.AddChild(Part(new SphereMesh { Radius = 0.013f, Height = 0.026f }, Mat(new Color("E8333A"), 0.2f, 0.2f, 0.4f), new Vector3(0, Top + 0.012f, -Radius + 0.006f)));
                break;
            }
            default:
                root.Free();
                return null;
        }
        foreach (var n in root.GetChildren()) if (n is GeometryInstance3D g) g.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return root;
    }

    /// <summary>
    /// Pose le couvre-chef sur un modèle animé : accroché à l'os de la tête (« Tete », ou « Head » pour le mannequin), redressé
    /// pour que son repère soit celui du modèle au repos (haut = +Y, regard -Z), quel que soit l'axe de l'os. Remplace le précédent.
    /// Sans squelette ni os de tête : rien (avatar de secours en formes simples).
    /// </summary>
    public static void Attach(Node3D model, string? id)
    {
        foreach (var old in model.FindChildren(NodeName, "", true, false)) { old.GetParent()?.RemoveChild(old); old.QueueFree(); }
        if (Build(id) is not { } hat) return;
        if (model.FindChildren("*", "Skeleton3D", true, false) is not { Count: > 0 } skels || skels[0] is not Skeleton3D skel) { hat.Free(); return; }
        int bone = skel.FindBone("Tete");
        if (bone < 0) bone = skel.FindBone("Head");
        if (bone < 0) { hat.Free(); return; }
        var toModel = skel.Transform;
        for (var n = skel.GetParent(); n != null && n != model; n = n.GetParent())
            if (n is Node3D n3) toModel = n3.Transform * toModel;
        var restInModel = toModel * skel.GetBoneGlobalRest(bone);
        var center = restInModel.Origin + Vector3.Up * Avatar.HeadAboveBone;
        var holder = new BoneAttachment3D { Name = NodeName, BoneName = skel.GetBoneName(bone) };
        skel.AddChild(holder);
        hat.Name = "Modele";
        hat.Transform = restInModel.AffineInverse() * new Transform3D(Basis.Identity, center);
        holder.AddChild(hat);
    }
}
