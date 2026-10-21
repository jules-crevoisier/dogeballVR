using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Shader toon « peint main » (assets/shaders/kc_toon.gdshader) et contour d'encre. Les matériaux des glb gardent le nom de leur
/// type (brique, beton, danger, fenetres, neon_*) : on le lit à l'import pour choisir le motif procédural.
/// </summary>
public static class KcShaders
{
    static Shader? toon, outline;

    // Matériaux à motif : le glb n'en garde qu'une couleur blanche (l'exporteur ignore la couleur d'un matériau à texture procédurale),
    // donc les deux couleurs du motif sont définies ici. nom : (couleur 1, couleur 2, motif, échelle)
    static readonly System.Collections.Generic.Dictionary<string, (string c1, string c2, int pattern, float scale)> Kinds = new()
    {
        ["brique"] = ("#B5533C", "#8E3F30", 1, 0.5f),
        ["brique_jaune"] = ("#D9A441", "#B58530", 1, 0.5f),
        ["beton"] = ("#5A6180", "#4A5170", 2, 1f),
        ["beton_clair"] = ("#8C93B0", "#767E9E", 2, 1f),
        ["beton_zone"] = ("#707898", "#5E6684", 7, 1f),
        ["asphalte"] = ("#6E6D7C", "#5E5D6E", 7, 1f),
        ["bois"] = ("#B07A45", "#8A5A30", 3, 1f),
        ["metal_bleu"] = ("#2F6F8F", "#245A75", 6, 1f),
        ["danger"] = ("#F2C230", "#1A1C26", 4, 1f),
        ["fenetres"] = ("#232A4D", "#F6C56A", 5, 1f),
        ["herbe"] = ("#4E8F4A", "#3F7A3E", 2, 1f),
    };

    public static ShaderMaterial Toon(Color color, Color? alt = null, int pattern = 0, float scale = 1f, float emission = 0f)
    {
        toon ??= GD.Load<Shader>("res://assets/shaders/kc_toon.gdshader");
        var m = new ShaderMaterial { Shader = toon };
        m.SetShaderParameter("base_color", color);
        m.SetShaderParameter("alt_color", alt ?? color);
        m.SetShaderParameter("pattern", pattern);
        m.SetShaderParameter("pattern_scale", scale);
        m.SetShaderParameter("emission_energy", emission);
        return m;
    }

    public static ShaderMaterial Outline(float width = 0.02f)
    {
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
                else if (Kinds.TryGetValue(name, out var k))
                    m = Toon(new Color(k.c1), new Color(k.c2), k.pattern, k.scale, 0f);
                else
                    m = Toon(color, null, 0, 1f, src.EmissionEnabled ? src.EmissionEnergyMultiplier * 0.8f : 0f);
                if (withOutline && !name.StartsWith("neon_")) m.NextPass = Outline();
                mi.SetSurfaceOverrideMaterial(s, m);
            }
        }
        foreach (var child in root.GetChildren()) Apply(child, withOutline);
    }
}
