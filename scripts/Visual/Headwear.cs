using System.Collections.Generic;
using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Couvre-chefs du personnage (récompenses de niveau, voir Online.Cosmetics) : formes simples modelées ici, fusionnées en un seul
/// maillage (une surface par matériau) et accrochées à l'os de la tête pour suivre les animations.
/// Repère de construction : origine 3 cm sous le sommet du crâne (<see cref="FrameAboveBone"/> au-dessus de l'os de la tête), +Y en
/// haut, regard vers -Z, en mètres. Mesures du mannequin, sommets de l'os Head une fois la peau appliquée (pose de liaison comprise) :
/// sommet du crâne à +0,03, demi-largeur 0,082, demi-profondeur 0,106, yeux vers -0,082, menton vers -0,2. Règles : rien au-dessus de <see cref="MaxTop"/> (la boîte de touche ne compte pas le chapeau, un chapeau
/// haut dépasserait d'un muret sans pouvoir être touché) ; pas de lime (signal du HUD) ; l'accent est la couleur d'équipe du porteur.
/// </summary>
public static class Headwear
{
    /// <summary>Nom du nœud accroché au squelette (un seul couvre-chef à la fois).</summary>
    public const string NodeName = "CouvreChef";

    // Crâne du mannequin, dans le repère ci-dessus.
    const float Top = 0.03f, Rx = 0.082f, DepthScale = 1.3f, Front = 0.106f, Eye = -0.082f;
    /// <summary>
    /// Hauteur de l'origine du repère au-dessus de l'os de la tête au repos. Mesurée sur le maillage rendu : la pose de liaison de la
    /// peau remonte la tête de 8,7 cm par rapport aux sommets bruts du fichier (sommet du crâne à 0,117 + 0,13 de l'os).
    /// </summary>
    public const float FrameAboveBone = Avatar.HeadAboveBone + 0.087f;
    /// <summary>Plus haut point permis d'un couvre-chef (7 cm au-dessus du crâne).</summary>
    public const float MaxTop = Top + 0.07f;

    static readonly Color Ink = new("15141a"), Paper = new("f3ecdd"), Yellow = new("F2C230"), White = new("F4F4F4"),
        Moss = new("3B5B4F"), MossDark = new("2C443B"), Ochre = new("C98A2E"), OchreDark = new("8E5E1C"), Straw = new("D9B866"),
        Khaki = new("7E7E52"), Gold = new("E8B730"), Silver = new("C9CED6"), Night = new("2A2830"), Ruby = new("D8262E"), Lens = new("2A3550");

    static readonly Dictionary<string, ArrayMesh?> cache = new();
    static readonly Dictionary<(Color, float, float, float), StandardMaterial3D> materials = new();

    static StandardMaterial3D Mat(Color c, float rough = 0.65f, float metal = 0f, float emission = 0f)
    {
        var key = (c, rough, metal, emission);
        if (materials.TryGetValue(key, out var m)) return m;
        m = new StandardMaterial3D { AlbedoColor = c, Roughness = rough, Metallic = metal };
        if (emission > 0f) { m.EmissionEnabled = true; m.Emission = c; m.EmissionEnergyMultiplier = emission; }
        return materials[key] = m;
    }

    /// <summary>Pièces du modèle avant fusion : maillage, matériau, placement.</summary>
    sealed class Parts
    {
        public readonly List<(Mesh mesh, Material mat, Transform3D at)> List = new();

        public void Add(Mesh mesh, Material mat, Vector3 pos, Vector3? rotDeg = null, Vector3? scale = null)
        {
            var r = rotDeg ?? Vector3.Zero;
            var basis = Basis.FromEuler(new Vector3(Mathf.DegToRad(r.X), Mathf.DegToRad(r.Y), Mathf.DegToRad(r.Z))).Scaled(scale ?? Vector3.One);
            List.Add((mesh, mat, new Transform3D(basis, pos)));
        }

        public void Add(Mesh mesh, Material mat, Transform3D at) => List.Add((mesh, mat, at));

        /// <summary>Une seule ArrayMesh, une surface par matériau : quelques appels de rendu par chapeau, même à 8 joueurs sur Quest.</summary>
        public ArrayMesh Bake()
        {
            var result = new ArrayMesh();
            var order = new List<Material>();
            foreach (var p in List) if (!order.Contains(p.mat)) order.Add(p.mat);
            foreach (var mat in order)
            {
                var st = new SurfaceTool();
                st.Begin(Mesh.PrimitiveType.Triangles);
                foreach (var p in List) if (p.mat == mat) st.AppendFrom(p.mesh, 0, p.at);
                st.Commit(result);
                result.SurfaceSetMaterial(result.GetSurfaceCount() - 1, mat);
            }
            return result;
        }
    }

    /// <summary>Calotte qui coiffe le crâne : demi-sphère étirée vers l'arrière comme la tête (base à <paramref name="baseY"/>).</summary>
    static void Dome(Parts p, Material mat, float r, float height, float baseY, float z = 0.004f) =>
        p.Add(new SphereMesh { Radius = r, Height = r, IsHemisphere = true, RadialSegments = 24, Rings = 8 }, mat,
            new Vector3(0, baseY, z), null, new Vector3(1, height / r, DepthScale));

    /// <summary>Anneau ou cylindre autour de la tête (étiré en profondeur comme elle).</summary>
    static void Ring(Parts p, Material mat, float top, float bottom, float h, float y, float tiltDeg = 0f, float z = 0.004f) =>
        p.Add(new CylinderMesh { TopRadius = top, BottomRadius = bottom, Height = h, RadialSegments = 28 }, mat, new Vector3(0, y, z), new Vector3(tiltDeg, 0, 0),
            new Vector3(1, 1, DepthScale));

    /// <summary>Maillage fusionné du couvre-chef, null pour « tête nue » ou un identifiant inconnu. Mis en cache par modèle et équipe.</summary>
    public static ArrayMesh? MeshOf(string? id, int team)
    {
        if (string.IsNullOrEmpty(id)) return null;
        string key = $"{id}#{team}";
        if (cache.TryGetValue(key, out var hit)) return hit;
        var p = new Parts();
        var teamMat = Mat(team == 0 ? new Color("f2612d") : new Color("2e86de"), 0.55f);
        bool ok = true;
        switch (id)
        {
            case "casquette":
            case "casquette_encre":
            {
                bool inkCap = id == "casquette_encre";
                Dome(p, Mat(inkCap ? Ink : Paper, 0.7f), Rx + 0.006f, 0.072f, -0.034f);
                p.Add(new BoxMesh { Size = new Vector3(0.15f, 0.012f, 0.09f) }, Mat(inkCap ? Paper : Ink, 0.6f), new Vector3(0, -0.03f, -Front - 0.04f), new Vector3(-8, 0, 0));
                p.Add(new CylinderMesh { TopRadius = 0.011f, BottomRadius = 0.011f, Height = 0.01f }, Mat(inkCap ? Paper : Ink), new Vector3(0, 0.04f, 0.004f));   // bouton
                p.Add(new BoxMesh { Size = new Vector3(0.045f, 0.026f, 0.004f) }, teamMat, new Vector3(0, 0.0f, -Front + 0.006f), new Vector3(-38, 0, 0));       // écusson
                break;
            }
            case "bandeau":
            case "bandeau_papier":
            {
                var band = Mat(id == "bandeau" ? Ink : Paper, 0.9f);
                // Serré sur le front, plus bas devant (8°), juste au-dessus des sourcils.
                p.Add(new TorusMesh { InnerRadius = 0.079f, OuterRadius = 0.091f, Rings = 28, RingSegments = 6 }, band, new Vector3(0, Top - 0.08f, 0.006f),
                    new Vector3(-8, 0, 0), new Vector3(1, 2.6f, DepthScale));
                for (int s = -1; s <= 1; s += 2)   // deux pans noués derrière, à la couleur d'équipe
                    p.Add(new BoxMesh { Size = new Vector3(0.03f, 0.14f, 0.008f) }, teamMat, new Vector3(0.022f * s, Top - 0.14f, 0.11f), new Vector3(-24, 0, 16 * s));
                break;
            }
            case "bonnet":
            case "bonnet_ocre":
            {
                bool ochre = id == "bonnet_ocre";
                Dome(p, Mat(ochre ? Ochre : Moss, 0.95f), Rx + 0.012f, 0.106f, -0.062f);
                Ring(p, Mat(ochre ? OchreDark : MossDark, 0.95f), Rx + 0.012f, Rx + 0.014f, 0.045f, -0.045f);   // revers sur le front
                p.Add(new SphereMesh { Radius = 0.026f, Height = 0.052f }, Mat(Paper, 1f), new Vector3(0, Top + 0.032f, 0.008f));     // pompon, sous la limite
                break;
            }
            case "lunettes":
            case "lunettes_or":
            {
                // Masque enveloppant d'une pièce : verre réfléchissant, monture encre, sangle à la couleur d'équipe.
                bool gold = id == "lunettes_or";
                // Trois pans (face et deux tempes à 40°) pour qu'il épouse la tête au lieu d'un bandeau plat.
                var frame = Mat(Ink, 0.5f);
                var glass = Mat(gold ? Gold : Lens, 0.05f, 0.8f);
                var hinge = new Vector3(0.05f, Eye, -Front + 0.012f);
                p.Add(new BoxMesh { Size = new Vector3(0.1f, 0.058f, 0.03f) }, frame, new Vector3(0, Eye, hinge.Z));
                p.Add(new BoxMesh { Size = new Vector3(0.096f, 0.048f, 0.01f) }, glass, new Vector3(0, Eye, -Front - 0.006f));
                for (int s = -1; s <= 1; s += 2)
                {
                    float a = Mathf.DegToRad(40f);
                    var side = new Vector3(s * (hinge.X + 0.03f * Mathf.Cos(a)), Eye, hinge.Z + 0.03f * Mathf.Sin(a));
                    var outward = new Vector3(s * Mathf.Sin(a), 0, -Mathf.Cos(a));
                    p.Add(new BoxMesh { Size = new Vector3(0.064f, 0.054f, 0.03f) }, frame, side, new Vector3(0, -40f * s, 0));
                    p.Add(new BoxMesh { Size = new Vector3(0.054f, 0.044f, 0.01f) }, glass, side + outward * 0.018f, new Vector3(0, -40f * s, 0));
                }
                p.Add(new TorusMesh { InnerRadius = Rx - 0.002f, OuterRadius = Rx + 0.006f, Rings = 28, RingSegments = 6 }, teamMat, new Vector3(0, Eye + 0.004f, 0.004f),
                    null, new Vector3(1, 1.8f, DepthScale));
                break;
            }
            case "casque":
            case "casque_blanc":
            {
                var shell = Mat(id == "casque" ? Yellow : White, 0.45f);
                Dome(p, shell, Rx + 0.016f, 0.084f, -0.03f);
                Ring(p, shell, Rx + 0.028f, Rx + 0.032f, 0.012f, -0.034f);   // bord
                p.Add(new BoxMesh { Size = new Vector3(0.024f, 0.022f, 0.2f) }, shell, new Vector3(0, 0.05f, 0.004f));   // nervure
                p.Add(new BoxMesh { Size = new Vector3(0.05f, 0.028f, 0.004f) }, teamMat, new Vector3(0, -0.002f, -Front - 0.012f), new Vector3(-28, 0, 0));   // insigne
                break;
            }
            case "bob":
            case "bob_kaki":
            {
                var cloth = Mat(id == "bob" ? Straw : Khaki, 0.95f);
                Ring(p, cloth, Rx - 0.008f, Rx + 0.008f, 0.07f, 0.0f);
                Ring(p, cloth, Rx + 0.012f, Rx + 0.06f, 0.024f, -0.042f);   // bord tombant
                Ring(p, teamMat, Rx + 0.006f, Rx + 0.01f, 0.02f, -0.024f);   // ruban d'équipe
                break;
            }
            case "crete":
            case "crete_papier":
            {
                bool paper = id == "crete_papier";
                var hair = Mat(paper ? Paper : Ink, 0.8f);
                var tips = paper ? teamMat : Mat(Paper, 0.8f);
                const int n = 8;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)(n - 1);
                    float z = Mathf.Lerp(-0.05f, 0.1f, t);
                    float zr = z / (Rx * DepthScale);
                    float y = Top - 0.085f * (1f - Mathf.Sqrt(Mathf.Max(0f, 1f - zr * zr)));   // suit l'arrondi du crâne
                    float h = Mathf.Lerp(0.05f, 0.07f, Mathf.Sin(t * Mathf.Pi));
                    float tilt = -20f + 55f * t;   // les mèches s'ouvrent en éventail, de l'avant vers l'arrière
                    // Mèches en cônes aplatis (pointes lisibles sous tous les angles), penchées autour de l'axe X ; bout d'une autre couleur.
                    var lean = new Basis(Vector3.Right, Mathf.DegToRad(tilt)).Scaled(new Vector3(0.5f, 1f, 1f));
                    var up = new Basis(Vector3.Right, Mathf.DegToRad(tilt)) * Vector3.Up;
                    var root = new Vector3(0, y - 0.012f, z);
                    const float r = 0.03f, cut = 0.8f;   // rayon à la base ; part de la mèche sous le bout
                    p.Add(new CylinderMesh { BottomRadius = r, TopRadius = r * (1f - cut), Height = h * cut, RadialSegments = 8, Rings = 1 }, hair,
                        new Transform3D(lean, root + up * (h * cut / 2f)));
                    p.Add(new CylinderMesh { BottomRadius = r * (1f - cut), TopRadius = 0f, Height = h * (1f - cut), RadialSegments = 8, Rings = 1 }, tips,
                        new Transform3D(lean, root + up * (h * cut + h * (1f - cut) / 2f)));
                }
                break;
            }
            case "couronne":
            case "couronne_argent":
            case "couronne_noire":
            {
                var metal = id switch
                {
                    "couronne_argent" => Mat(Silver, 0.3f, 0.6f, 0.05f),
                    "couronne_noire" => Mat(Night, 0.35f, 0.5f),
                    _ => Mat(Gold, 0.35f, 0.3f, 0.12f),
                };
                Ring(p, metal, Rx - 0.006f, Rx - 0.012f, 0.035f, Top - 0.005f);
                for (int i = 0; i < 6; i++)
                {
                    float a = Mathf.Tau * i / 6f;
                    var at = new Vector3(Mathf.Sin(a) * (Rx - 0.008f), Top + 0.027f, -Mathf.Cos(a) * (Rx - 0.008f) * DepthScale + 0.004f);
                    p.Add(new PrismMesh { Size = new Vector3(0.036f, 0.03f, 0.008f) }, metal, at, new Vector3(0, -Mathf.RadToDeg(a), 0));
                    p.Add(new SphereMesh { Radius = 0.007f, Height = 0.014f, RadialSegments = 8, Rings = 4 }, metal, at + new Vector3(0, 0.017f, 0));
                }
                p.Add(new SphereMesh { Radius = 0.012f, Height = 0.024f }, Mat(id == "couronne_noire" ? Ruby : new Color("E8333A"), 0.2f, 0.2f, 0.4f),
                    new Vector3(0, Top - 0.004f, -(Rx - 0.004f) * DepthScale));
                break;
            }
            default:
                ok = false;
                break;
        }
        return cache[key] = ok ? p.Bake() : null;
    }

    /// <summary>
    /// Pose le couvre-chef sur un modèle animé : accroché à l'os de la tête (« Tete », ou « Head » pour le mannequin), redressé
    /// pour que son repère soit celui du modèle au repos (haut = +Y, regard -Z), quel que soit l'axe de l'os. Remplace le précédent.
    /// Sans squelette ni os de tête : rien (avatar de secours en formes simples). Renvoie le nœud posé (null : tête nue).
    /// </summary>
    public static Node3D? Attach(Node3D model, string? id, int team)
    {
        foreach (var old in model.FindChildren(NodeName, "", true, false)) { old.GetParent()?.RemoveChild(old); old.QueueFree(); }
        if (MeshOf(id, team) is not { } mesh) return null;
        if (model.FindChildren("*", "Skeleton3D", true, false) is not { Count: > 0 } skels || skels[0] is not Skeleton3D skel) return null;
        int bone = skel.FindBone("Tete");
        if (bone < 0) bone = skel.FindBone("Head");
        if (bone < 0) return null;
        var toModel = skel.Transform;
        for (var n = skel.GetParent(); n != null && n != model; n = n.GetParent())
            if (n is Node3D n3) toModel = n3.Transform * toModel;
        var restInModel = toModel * skel.GetBoneGlobalRest(bone);
        var center = restInModel.Origin + Vector3.Up * FrameAboveBone;
        var holder = new BoneAttachment3D { Name = NodeName, BoneName = skel.GetBoneName(bone) };
        skel.AddChild(holder);
        holder.AddChild(new MeshInstance3D
        {
            Name = "Modele", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Transform = restInModel.AffineInverse() * new Transform3D(Basis.Identity, center),
        });
        return holder;
    }
}
