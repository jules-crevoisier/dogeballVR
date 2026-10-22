using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Shader toon « peint main » (assets/shaders/kc_toon.gdshader) et contour d'encre. Les matériaux des glb gardent le nom de leur
/// type (brique, beton, danger, fenetres, neon_*) : on le lit à l'import pour choisir le motif procédural.
/// </summary>
public static class KcShaders
{
    /// <summary>Rendu choisi : PBR stylisé (défaut), toon à paliers avec contour d'encre, ou gouache (toon très doux sans contour). `--look=pbr|toon|gouache`.</summary>
    public enum LookKind { Pbr, Toon, Gouache }
    public static LookKind Look { get; set; } = LookKind.Pbr;

    static Shader? toon, pbr, outline;

    public static void ParseLook()
    {
        foreach (var a in Tools.DevFlags.Args)
            if (a.StartsWith("--look="))
                Look = a["--look=".Length..] switch { "toon" => LookKind.Toon, "gouache" => LookKind.Gouache, _ => LookKind.Pbr };
    }

    // Matériaux à motif : le glb n'en garde qu'une couleur blanche (l'exporteur ignore la couleur d'un matériau à texture procédurale),
    // donc les deux couleurs du motif sont définies ici. nom : (couleur 1, couleur 2, motif, échelle)
    static readonly System.Collections.Generic.Dictionary<string, (string c1, string c2, int pattern, float scale)> Kinds = new()
    {
        ["brique"] = ("#B5533C", "#8E3F30", 1, 0.5f),
        ["brique_jaune"] = ("#D9A441", "#B58530", 1, 0.5f),
        ["beton"] = ("#5A6180", "#4A5170", 2, 1f),
        ["beton_clair"] = ("#B8BDD0", "#A2A8BE", 2, 1f),
        ["beton_zone"] = ("#707898", "#5E6684", 7, 1f),
        ["asphalte"] = ("#4A4E63", "#3E4256", 7, 1f),
        ["bois"] = ("#B07A45", "#8A5A30", 3, 1f),
        ["metal_bleu"] = ("#2F6F8F", "#245A75", 6, 1f),
        ["danger"] = ("#F2C230", "#2A2C38", 4, 1f),
        ["fenetres"] = ("#232A4D", "#F6C56A", 5, 1f),
        ["herbe"] = ("#4E8F4A", "#3F7A3E", 2, 1f),
    };

    public static ShaderMaterial Toon(Color color, Color? alt = null, int pattern = 0, float scale = 1f, float emission = 0f)
    {
        Shader shader;
        if (Look == LookKind.Pbr) shader = pbr ??= GD.Load<Shader>("res://assets/shaders/kc_pbr.gdshader");
        else shader = toon ??= GD.Load<Shader>("res://assets/shaders/kc_toon.gdshader");
        var m = new ShaderMaterial { Shader = shader };
        if (Look == LookKind.Gouache)
        {
            m.SetShaderParameter("bands", 6f);
            m.SetShaderParameter("softness", 0.45f);
            m.SetShaderParameter("shadow_tint", new Color(0.72f, 0.66f, 0.8f));
            m.SetShaderParameter("rim_strength", 0.2f);
        }
        m.SetShaderParameter("base_color", color);
        m.SetShaderParameter("alt_color", alt ?? color);
        m.SetShaderParameter("pattern", pattern);
        m.SetShaderParameter("pattern_scale", scale);
        m.SetShaderParameter("emission_energy", emission);
        return m;
    }

    /// <summary>Contour d'encre : seulement avec le rendu toon (null sinon, ce qui laisse NextPass vide).</summary>
    public static ShaderMaterial? Outline(float width = 0.02f)
    {
        if (Look != LookKind.Toon) return null;
        outline ??= GD.Load<Shader>("res://assets/shaders/kc_outline.gdshader");
        var m = new ShaderMaterial { Shader = outline };
        m.SetShaderParameter("width", width);
        return m;
    }

    /// <summary>Remplace les matériaux standard d'un modèle importé par le shader toon (avec contour si demandé).</summary>
    public static void Apply(Node root, bool withOutline)
    {
        if (root is MeshInstance3D mi && mi.Mesh != null)
        {
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                var src = mi.Mesh.SurfaceGetMaterial(s) as BaseMaterial3D;
                if (src == null) continue;
                string name = src.ResourceName;
                var color = src.AlbedoColor;
                ShaderMaterial m;
                if (name.StartsWith("neon_") || src.EmissionEnabled && src.EmissionEnergyMultiplier > 1.5f)
                    m = Toon(color, null, 0, 1f, 2.5f);
                else if (src.AlbedoTexture != null)
                {
                    m = Toon(Colors.White, null, 9);
                    m.SetShaderParameter("albedo_tex", src.AlbedoTexture);
                }
                else if (name == "peinture")
                    m = Toon(Colors.White, null, 8);
                else if (Kinds.TryGetValue(name, out var k))
                    m = Toon(new Color(k.c1), new Color(k.c2), k.pattern, k.scale, 0f);
                else
                    m = Toon(color, null, 0, 1f, src.EmissionEnabled ? src.EmissionEnergyMultiplier * 0.8f : 0f);
                if (withOutline && !name.StartsWith("neon_") && name is not ("oeil" or "pupille" or "peau_nez" or "bouche")) m.NextPass = Outline();
                mi.SetSurfaceOverrideMaterial(s, m);
            }
        }
        foreach (var child in root.GetChildren()) Apply(child, withOutline);
    }
}
