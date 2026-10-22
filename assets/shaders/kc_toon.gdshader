// Shader « peint main » de la DA Knockout City : éclairage à paliers doux, ombres teintées, liseré de lumière,
// motifs procéduraux en espace monde (briques, béton, planches, bandes de danger, fenêtres, tôle ondulée), néons émissifs.
// Aucune texture : tout est calculé, donc léger pour le Quest (un seul passage, pas de SSAO ni d'ombre dynamique).
shader_type spatial;
render_mode diffuse_burley, specular_disabled, cull_back;

#include "res://assets/shaders/kc_common.gdshaderinc"

uniform vec4 shadow_tint : source_color = vec4(0.52, 0.47, 0.78, 1.0);   // les ombres virent au violet, jamais au noir
uniform float bands = 3.0;
uniform float softness = 0.12;
uniform vec4 rim_color : source_color = vec4(1.0, 0.82, 0.62, 1.0);
uniform float rim_power = 3.0;
uniform float rim_strength = 0.35;

void fragment() {
    vec3 n = normalize(wnorm);
    vec3 col;
    if (pattern == 9) col = texture(albedo_tex, UV).rgb;                  // 9 : texture d'albédo (personnages rigés)
    else if (pattern == 8) col = vcol.rgb;                                 // 8 : peinture dans les couleurs de sommet (glTF : déjà linéaire)
    else col = surface(wpos, n);
    ALBEDO = col;
    ROUGHNESS = 1.0;
    float lit_window = 0.0;
    if (pattern == 5) {
        vec2 w = vec2(face_uv(wpos, n).x * pattern_scale / 3.0, face_uv(wpos, n).y * pattern_scale / 3.6);
        vec2 id = floor(w); vec2 f = fract(w);
        float win = step(0.18, f.x) * step(f.x, 0.82) * step(0.2, f.y) * step(f.y, 0.8);
        lit_window = win * step(0.65, hash(id)) * 0.6;
    }
    EMISSION = col * (emission_energy + lit_window);
}

// Éclairage à paliers : la lumière directe est quantifiée en quelques bandes aux bords adoucis,
// l'ombre prend une teinte froide plutôt qu'un noir (aspect peint à la main).
void light() {
    float ndl = dot(NORMAL, LIGHT) * 0.5 + 0.5;
    float t = ndl * bands;
    float step_v = floor(t) + smoothstep(0.5 - softness * bands, 0.5 + softness * bands, fract(t));
    float lit = clamp(step_v / bands, 0.0, 1.0) * ATTENUATION;
    vec3 tint = mix(shadow_tint.rgb, vec3(1.0), lit);
    DIFFUSE_LIGHT += ALBEDO * LIGHT_COLOR * tint * lit / PI;
}
