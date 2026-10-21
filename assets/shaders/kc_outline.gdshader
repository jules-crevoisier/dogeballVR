// Contour d'encre : coque gonflée le long des normales, faces avant masquées, sans éclairage.
shader_type spatial;
render_mode unshaded, cull_front;
uniform vec4 outline_color : source_color = vec4(0.06, 0.07, 0.14, 1.0);
uniform float width = 0.02;
void vertex() { VERTEX += NORMAL * width; }
void fragment() { ALBEDO = outline_color.rgb; }
