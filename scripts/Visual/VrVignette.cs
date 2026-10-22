using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// VR : liseré de couleur sur les bords du champ de vision, fixe dans le casque, sans aucun mouvement (une secousse de la vue donne la nausée).
/// Remplace la secousse du clavier-souris : rouge quand on est touché, plus long au KO, doré à l'attrape parfaite.
/// Quad de 0,75 m collé à la caméra XR (0,25 m) : ses bords tombent au bord du champ du Quest (~100°). Dessiné par-dessus tout, transparent au centre.
/// </summary>
public partial class VrVignette : MeshInstance3D
{
    static VrVignette? current;
    ShaderMaterial mat = null!;
    Tween? tween;

    const string Code = @"shader_type spatial;
render_mode unshaded, depth_test_disabled, cull_disabled, shadows_disabled;
uniform vec4 tint : source_color = vec4(1.0, 0.1, 0.1, 1.0);
uniform float amount = 0.0;
void fragment() {
    vec2 d = (UV - 0.5) * 2.0;
    float r = length(d * vec2(1.0, 1.15));
    float edge = smoothstep(0.4, 0.88, r);
    ALBEDO = tint.rgb;
    ALPHA = edge * amount * tint.a;
}";

    /// <summary>Pose le liseré sous la caméra XR (une seule fois par scène).</summary>
    public static void Attach(Node3D xrCamera)
    {
        var v = new VrVignette { Name = "VrVignette", Mesh = new QuadMesh { Size = new Vector2(0.75f, 0.75f) }, Position = new Vector3(0, 0, -0.25f) };
        v.CastShadow = ShadowCastingSetting.Off;
        v.mat = new ShaderMaterial { Shader = new Shader { Code = Code }, RenderPriority = 120 };
        v.MaterialOverride = v.mat;
        v.Visible = false;
        xrCamera.AddChild(v);
    }

    public override void _EnterTree() => current = this;
    public override void _ExitTree() { if (current == this) current = null; }

    /// <summary>Allume le liseré (strength 0..1) puis l'éteint en fondu sur seconds. Sans casque, ne fait rien.</summary>
    public static void Flash(Color color, float strength, float seconds)
    {
        if (current == null || !IsInstanceValid(current)) return;
        var v = current;
        v.tween?.Kill();
        v.mat.SetShaderParameter("tint", color);
        v.mat.SetShaderParameter("amount", Mathf.Clamp(strength, 0f, 1f));
        v.Visible = true;
        v.tween = v.CreateTween();
        v.tween.TweenMethod(Callable.From<float>(a => v.mat.SetShaderParameter("amount", a)), Mathf.Clamp(strength, 0f, 1f), 0f, seconds).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        v.tween.TweenCallback(Callable.From(() => v.Visible = false));
    }
}
