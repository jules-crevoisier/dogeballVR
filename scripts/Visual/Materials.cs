using Godot;

namespace DogeballVR.Visual;

/// <summary>Matériaux partagés. Shaders courts, sans post-traitement (budget Quest autonome).</summary>
public static class Materials
{
    const string GridCode = @"
shader_type spatial;
render_mode unshaded;
uniform vec4 base_color : source_color;
uniform vec4 line_color : source_color;
varying vec3 wpos;
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
    vec2 g = abs(fract(wpos.xz - 0.5) - 0.5) / fwidth(wpos.xz);
    float line = 1.0 - min(min(g.x, g.y), 1.0);
    ALBEDO = mix(base_color.rgb, line_color.rgb, line * 0.5);
}";

    // Mur de limite : invisible à plus de 2 m, grille magenta qui apparaît jusqu'à 0,8 m, puis pleine et pulsante.
    const string LimitCode = @"
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;
uniform vec4 color : source_color;
varying vec3 wpos;
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
    vec3 cam = INV_VIEW_MATRIX[3].xyz;
    float d = distance(wpos.xz, cam.xz);
    float fade = 1.0 - smoothstep(0.8, 2.0, d);
    float near = 1.0 - smoothstep(0.7, 0.9, d);
    vec2 h = abs(fract(vec2(wpos.x + wpos.z, wpos.y) * 2.0 - 0.5) - 0.5) / fwidth(vec2(wpos.x + wpos.z, wpos.y) * 2.0);
    float line = 1.0 - min(min(h.x, h.y), 1.0);
    float pulse = 0.85 + 0.15 * sin(TIME * 6.0);          // 1 Hz environ : sous le seuil des 3 Hz
    ALBEDO = color.rgb;
    ALPHA = clamp(fade * (0.12 + 0.6 * line) * mix(1.0, pulse, near) + near * 0.15, 0.0, 1.0);
}";

    static Shader? grid;
    static Shader? limit;

    public static ShaderMaterial Grid(Color baseColor, Color lineColor)
    {
        grid ??= new Shader { Code = GridCode };
        var m = new ShaderMaterial { Shader = grid };
        m.SetShaderParameter("base_color", baseColor);
        m.SetShaderParameter("line_color", lineColor);
        return m;
    }

    public static ShaderMaterial LimitWall(Color color)
    {
        limit ??= new Shader { Code = LimitCode };
        var m = new ShaderMaterial { Shader = limit };
        m.SetShaderParameter("color", color);
        return m;
    }

    public static StandardMaterial3D Flat(Color c) => new() { AlbedoColor = c, Roughness = 1f };

    public static StandardMaterial3D Glow(Color c, float energy = 1.5f) => new()
    {
        AlbedoColor = c,
        EmissionEnabled = true,
        Emission = c,
        EmissionEnergyMultiplier = energy,
    };

    /// <summary>Matériau additif sans éclairage (halos, voiles de lumière, traînées).</summary>
    public static StandardMaterial3D Additive(Color c) => new()
    {
        AlbedoColor = c,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        VertexColorUseAsAlbedo = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };
}
