// Rendu « PBR stylisé » façon Knockout City : mêmes couleurs et motifs que le toon, mais éclairage physique doux
// (Burley + Schlick), reflets légers, liseré de lumière discret, aucune bande ni contour d'encre.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx, cull_back;

#include "res://assets/shaders/kc_common.gdshaderinc"

uniform float roughness_base = 0.62;
uniform float rim_amount = 0.25;

void fragment() {
    vec3 n = normalize(wnorm);
    vec3 col;
    if (pattern == 9) col = texture(albedo_tex, UV).rgb;
    else if (pattern == 8) col = vcol.rgb;   // couleurs de sommet glTF déjà linéaires
    else col = surface(wpos, n);
    ALBEDO = col;
    // tôle et néons un peu brillants, béton et briques mats, personnages satinés
    ROUGHNESS = pattern == 6 ? 0.38 : (pattern == 1 || pattern == 2 || pattern == 7 ? 0.85 : roughness_base);
    if (pattern == 8) ROUGHNESS = vcol.a;   // personnages : rugosité par matière (tissu, nylon, caoutchouc, peau) rangée dans l'alpha
    METALLIC = pattern == 6 ? 0.35 : 0.0;
    SPECULAR = 0.5;
    RIM = rim_amount;
    RIM_TINT = 0.6;
    if (pattern == 8 || pattern == 9) { RIM = 0.4; RIM_TINT = 0.3; }   // personnages : liseré fort, lisibles de loin
    float lit_window = 0.0;
    if (pattern == 5) {
        vec2 w = vec2(face_uv(wpos, n).x * pattern_scale / 3.0, face_uv(wpos, n).y * pattern_scale / 3.6);
        vec2 id = floor(w); vec2 f = fract(w);
        float win = step(0.18, f.x) * step(f.x, 0.82) * step(0.2, f.y) * step(f.y, 0.8);
        lit_window = win * step(0.65, hash(id)) * 0.6;
        ROUGHNESS = mix(ROUGHNESS, 0.15, win);   // vitres brillantes
    }
    EMISSION = col * (emission_energy + lit_window);
}
