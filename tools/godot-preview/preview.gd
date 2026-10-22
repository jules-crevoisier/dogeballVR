extends Node3D
const KINDS := {
 "brique": ["#B5533C", "#8E3F30", 1, 0.5], "brique_jaune": ["#D9A441", "#B58530", 1, 0.5], "beton": ["#5A6180", "#4A5170", 2, 1.0],
 "beton_clair": ["#8C93B0", "#767E9E", 2, 1.0], "beton_zone": ["#707898", "#5E6684", 7, 1.0], "asphalte": ["#6E6D7C", "#5E5D6E", 7, 1.0],
 "bois": ["#B07A45", "#8A5A30", 3, 1.0], "metal_bleu": ["#2F6F8F", "#245A75", 6, 1.0], "danger": ["#F2C230", "#1A1C26", 4, 1.0],
 "fenetres": ["#232A4D", "#F6C56A", 5, 1.0], "herbe": ["#4E8F4A", "#3F7A3E", 2, 1.0]}
var toon: Shader
func mk(color: Color, alt: Color, pattern: int, emission: float, scale: float = 1.0) -> ShaderMaterial:
	var m := ShaderMaterial.new(); m.shader = toon; m.set_shader_parameter("pattern_scale", scale)
	m.set_shader_parameter("base_color", color); m.set_shader_parameter("alt_color", alt)
	m.set_shader_parameter("pattern", pattern); m.set_shader_parameter("emission_energy", emission)
	return m
var outline: Shader
func apply(n: Node, ol: bool = false) -> void:
	if n is MeshInstance3D and n.mesh:
		for s in n.mesh.get_surface_count():
			var src = n.mesh.surface_get_material(s) as BaseMaterial3D
			if src == null: continue
			var nm: String = src.resource_name
			var m: ShaderMaterial
			if nm.begins_with("neon_") or (src.emission_enabled and src.emission_energy_multiplier > 1.5):
				m = mk(src.albedo_color, src.albedo_color, 0, 2.5)
			elif KINDS.has(nm):
				m = mk(Color(KINDS[nm][0]), Color(KINDS[nm][1]), KINDS[nm][2], 0.0, KINDS[nm][3])
			else:
				m = mk(src.albedo_color, src.albedo_color, 0, 0.0)
			if ol and not nm.begins_with("neon_"):
				var o := ShaderMaterial.new(); o.shader = outline; o.set_shader_parameter("width", 0.02); m.next_pass = o
			n.set_surface_override_material(s, m)
	for c in n.get_children(): apply(c, ol)
func _ready() -> void:
	toon = load("res://assets/shaders/kc_toon.gdshader")
	outline = load("res://assets/shaders/kc_outline.gdshader")
	for f in ["toit", "toit_ville"]:
		var inst = (load("res://assets/models/%s.glb" % f) as PackedScene).instantiate()
		apply(inst); add_child(inst)
	var obs = [[0.3, 0.0, -7.2, 3.2, 1.3, 0.25, "cover"], [1.8, 0.0, -8.5, 0.25, 1.3, 3.2, "cover"], [-8.3, 0.0, -7.2, 3.2, 1.3, 0.25, "cover"], [-9.8, 0.0, -8.5, 0.25, 1.3, 3.2, "cover"], [-4.0, 0.0, -9.0, 0.8, 2.2, 0.8, "cover"], [7.2, 0.0, 0.3, 0.25, 1.3, 3.2, "cover"], [8.5, 0.0, 1.8, 3.2, 1.3, 0.25, "cover"], [7.2, 0.0, -8.3, 0.25, 1.3, 3.2, "cover"], [8.5, 0.0, -9.8, 3.2, 1.3, 0.25, "cover"], [9.0, 0.0, -4.0, 0.8, 2.2, 0.8, "cover"], [-0.3, 0.0, 7.2, 3.2, 1.3, 0.25, "cover"], [-1.8, 0.0, 8.5, 0.25, 1.3, 3.2, "cover"], [8.3, 0.0, 7.2, 3.2, 1.3, 0.25, "cover"], [9.8, 0.0, 8.5, 0.25, 1.3, 3.2, "cover"], [4.0, 0.0, 9.0, 0.8, 2.2, 0.8, "cover"], [-7.2, 0.0, -0.3, 0.25, 1.3, 3.2, "cover"], [-8.5, 0.0, -1.8, 3.2, 1.3, 0.25, "cover"], [-7.2, 0.0, 8.3, 0.25, 1.3, 3.2, "cover"], [-8.5, 0.0, 9.8, 3.2, 1.3, 0.25, "cover"], [-9.0, 0.0, 4.0, 0.8, 2.2, 0.8, "cover"], [0, 0.0, 0, 1.6, 3.4, 1.6, "bumper"]]
	for o in obs:
		var mi := MeshInstance3D.new(); var bm := BoxMesh.new(); bm.size = Vector3(o[3], o[4], o[5]); mi.mesh = bm
		mi.position = Vector3(o[0], o[1] + o[4] / 2.0, o[2])
		var mm: ShaderMaterial = mk(Color("3F8FB5"), Color("245A75"), 6, 0.0) if o[6] == "bumper" else mk(Color("8C93B0"), Color("767E9E"), 2, 0.0)
		var ov := ShaderMaterial.new(); ov.shader = outline; ov.set_shader_parameter("width", 0.03); mm.next_pass = ov
		mi.material_override = mm; add_child(mi)
	for k in 4:
		var arm := MeshInstance3D.new(); var ab := BoxMesh.new(); ab.size = Vector3(4.0, 2.4, 0.4); arm.mesh = ab
		arm.rotation.y = k * PI / 2.0; arm.position = Vector3(0, 1.8, 0) + Vector3(2.2, 0, 0).rotated(Vector3.UP, k * PI / 2.0)
		var ao := ShaderMaterial.new(); ao.shader = outline; ao.set_shader_parameter("width", 0.03)
		var am := mk(Color("5A6180"), Color("4A5170"), 0, 0.0); am.next_pass = ao; arm.material_override = am; arm.scale = Vector3(1, 0.15, 1); add_child(arm)
	var team := [["avatar_orange", Vector3(-4.5, 0, -10.7), 0.0], ["avatar_bleu", Vector3(10.7, 0, -4.5), 90.0], ["avatar_orange", Vector3(4.5, 0, 10.7), 180.0], ["avatar_bleu", Vector3(-10.7, 0, 4.5), 270.0]]
	for tm in team:
		var av = (load("res://assets/models/%s.glb" % tm[0]) as PackedScene).instantiate(); apply(av, true); av.position = tm[1]; av.rotation_degrees.y = tm[2] + 180.0; add_child(av)
	var ball := MeshInstance3D.new(); var sm := SphereMesh.new(); sm.radius = 0.15; sm.height = 0.3; ball.mesh = sm
	var bmat := ShaderMaterial.new(); bmat.shader = toon; bmat.set_shader_parameter("base_color", Color("D6FF2E")); bmat.set_shader_parameter("emission_energy", 1.6)
	var bo := ShaderMaterial.new(); bo.shader = outline; bo.set_shader_parameter("width", 0.018); bmat.next_pass = bo; ball.material_override = bmat; ball.position = Vector3(-2.5, 1.6, -7.5); add_child(ball)
	var sky := ProceduralSkyMaterial.new()
	sky.sky_top_color = Color("4C8DE8"); sky.sky_horizon_color = Color("FFDDBA"); sky.sky_curve = 0.18
	sky.ground_horizon_color = Color("EBCDB4"); sky.ground_bottom_color = Color("8A7BB5")
	var env := Environment.new(); env.background_mode = Environment.BG_SKY; env.sky = Sky.new(); env.sky.sky_material = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY; env.ambient_light_energy = 1.0
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.fog_enabled = true; env.fog_light_color = Color("E6D4EA"); env.fog_density = 0.0022
	env.glow_enabled = true; env.glow_intensity = 0.7; env.glow_bloom = 0.08; env.glow_hdr_threshold = 1.0
	var we := WorldEnvironment.new(); we.environment = env; add_child(we)
	var sun := DirectionalLight3D.new(); sun.rotation_degrees = Vector3(-48, -35, 0); sun.light_energy = 1.4; sun.light_color = Color("FFE6C2"); add_child(sun)
	var cam := Camera3D.new(); cam.fov = 75; add_child(cam)
	var views = [[Vector3(-7.5, 1.75, -14.0), Vector3(1, 1.5, 0)], [Vector3(-4, 42, 27), Vector3(0, 0, -3)], [Vector3(9, 3.2, -9), Vector3(0, 2, 0)], [Vector3(-6, 2.4, 9), Vector3(6, 7.5, -22)]]
	var i := 0
	for v in views:
		cam.position = v[0]; cam.look_at(v[1])
		await get_tree().process_frame; await get_tree().process_frame; await get_tree().process_frame
		get_viewport().get_texture().get_image().save_png("res://shot%d.png" % i); i += 1
	get_tree().quit()
