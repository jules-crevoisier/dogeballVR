// Partie commune des shaders Knockout City (toon et PBR stylisé) : motifs procéduraux en espace monde, couleurs de sommet, texture d'albédo.
uniform vec4 base_color : source_color = vec4(0.7, 0.7, 0.7, 1.0);
uniform vec4 alt_color : source_color = vec4(0.5, 0.5, 0.5, 1.0);
uniform int pattern = 0;            // 0 uni, 1 briques, 2 béton, 3 planches, 4 danger, 5 fenêtres, 6 tôle, 7 asphalte, 8 couleurs de sommet, 9 texture d'albédo (personnages rigés)
uniform float pattern_scale = 1.0;
uniform float emission_energy = 0.0;

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_disable;
varying vec3 wpos;
varying vec3 wnorm;
varying vec4 vcol;

void vertex() {
    wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    wnorm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
    vcol = COLOR;
}

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}

// Coordonnées 2D selon la face dominante (triplanar sans mélange : les arêtes sont de toute façon biseautées).
vec2 face_uv(vec3 p, vec3 n) {
    vec3 a = abs(n);
    if (a.y > a.x && a.y > a.z) return p.xz;
    if (a.x > a.z) return p.zy;
    return p.xy;
}

vec3 surface(vec3 p, vec3 n) {
    vec2 uv = face_uv(p, n) * pattern_scale;
    vec3 c = base_color.rgb;
    if (pattern == 1) {                       // briques
        vec2 b = vec2(uv.x / 0.6, uv.y / 0.25);
        float row = floor(b.y);
        b.x += 0.5 * mod(row, 2.0);
        vec2 f = fract(b);
        vec2 id = vec2(floor(b.x), row);
        float mortar = step(f.x, 0.04) + step(f.y, 0.10);
        float tone = hash(id);
        c = mix(base_color.rgb, alt_color.rgb, tone * 0.8);
        c *= 0.92 + 0.12 * vnoise(uv * 3.0);
        c = mix(c, base_color.rgb * 0.42, clamp(mortar, 0.0, 1.0));
    } else if (pattern == 2) {                // béton : lavis irrégulier + joints de dalle
        float n1 = vnoise(uv * 0.7) * 0.6 + vnoise(uv * 3.1) * 0.4;
        c = mix(base_color.rgb, alt_color.rgb, n1);
        vec2 g = abs(fract(uv / 3.0 - 0.5) - 0.5) * 3.0;
        c = mix(c, base_color.rgb * 0.62, 1.0 - smoothstep(0.0, 0.035, min(g.x, g.y)));
    } else if (pattern == 3) {                // planches de bois
        float plank = floor(uv.x / 0.22 + uv.y * 0.0);
        float grain = vnoise(vec2(uv.y * 1.5, plank * 7.0)) * 0.5 + vnoise(vec2(uv.y * 9.0, plank * 3.0)) * 0.2;
        c = mix(base_color.rgb, alt_color.rgb, grain + hash(vec2(plank, 1.0)) * 0.3);
        float gap = step(fract(uv.x / 0.22), 0.04);
        c = mix(c, alt_color.rgb * 0.5, gap);
    } else if (pattern == 4) {                // bande jaune et noire à 45 degrés
        float s = fract((uv.x + uv.y) * 0.75);
        c = s < 0.5 ? base_color.rgb : alt_color.rgb;
    } else if (pattern == 5) {                // façade : fenêtres éclairées de façon irrégulière
        vec2 w = vec2(uv.x / 3.0, uv.y / 3.6);
        vec2 id = floor(w);
        vec2 f = fract(w);
        float win = step(0.18, f.x) * step(f.x, 0.82) * step(0.2, f.y) * step(f.y, 0.8);
        float lit = step(0.45, hash(id));
        c = mix(base_color.rgb, mix(base_color.rgb * 1.5, alt_color.rgb, lit), win);
    } else if (pattern == 7) {                // asphalte : lavis sombre irrégulier, sans joints
        float a1 = vnoise(uv * 0.35) * 0.55 + vnoise(uv * 1.7) * 0.3 + vnoise(uv * 6.0) * 0.15;
        c = mix(base_color.rgb, alt_color.rgb, smoothstep(0.25, 0.75, a1));
    } else if (pattern == 6) {                // tôle ondulée
        float w = 0.5 + 0.5 * sin((uv.x) * 18.0);
        c = mix(alt_color.rgb, base_color.rgb, w);
    }
    // De loin, les motifs fins scintillent : on les fond vers leur couleur moyenne selon la taille d'un pixel sur le motif.
    if (pattern == 1 || pattern == 2 || pattern == 3 || pattern == 6 || pattern == 7) {
        float aa = fwidth(uv.x) + fwidth(uv.y);
        vec3 avg = mix(base_color.rgb, alt_color.rgb, 0.4);
        if (pattern == 1) avg *= 0.88;
        c = mix(c, avg, smoothstep(0.12, 0.5, aa));
    }
    return c;
}
