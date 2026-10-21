using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Shader toon « peint main » (assets/shaders/kc_toon.gdshader) et contour d'encre. Les matériaux des glb gardent le nom de leur
/// type (brique, beton, danger, fenetres, neon_*) : on le lit à l'import pour choisir le motif procédural.
/// </summary>
public static class KcShaders
{
    static Shader? toon, outline;

    // nom du matériau glb : (couleur 2, motif, échelle)
    static readonly System.Collections.Generic.Dictionary<string, (string alt, int pattern, float scale)> Kinds = new()
    {
        ["brique"] = ("#8E3F30", 1, 1f),
        ["brique_jaune"] = ("#B58530", 1, 1f),
        ["beton"] = ("#4A5268", 2, 1f),
        ["beton_clair"] = ("#757D97", 2, 1f),
        ["bois"] = ("#8A5A30", 3, 1f),
        ["metal_bleu"] = ("#245A75", 6, 1f),
        ["danger"] = ("#1A1C26", 4, 1f),
        ["fenetres"] = ("#F6C56A", 5, 1f),
        ["herbe"] = ("#3F7A3E", 2, 1f),
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
                    m = Toon(color, new Color(k.alt), k.pattern, k.scale, src.EmissionEnabled ? 0.8f : 0f);
                else
                    m = Toon(color, null, 0, 1f, src.EmissionEnabled ? src.EmissionEnergyMultiplier * 0.8f : 0f);
                if (withOutline && !name.StartsWith("neon_")) m.NextPass = Outline();
                mi.SetSurfaceOverrideMaterial(s, m);
            }
        }
        foreach (var child in root.GetChildren()) Apply(child, withOutline);
    }
}
