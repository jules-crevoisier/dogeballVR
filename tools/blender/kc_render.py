"""Rendus de la carte Toit (Cycles CPU) : python3 -I kc_render.py <dossier_sortie> [vue ...]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import lib  # noqa: E402
import map_toit  # noqa: E402
from lib import hex_rgba  # noqa: E402

VUES = {
    "joueur": ((-13, 12.5, 1.75), (3, -3, 1.6), 20),
    "survol": ((-4, -27, 42), (0, 3, 0), 26),
    "enseigne": ((-4, -9, 2.2), (6, 22, 8), 20),
    "moyeu": ((9, 9, 3.2), (0, 0, 2.0), 24),
}


def sky():
    w = bpy.data.worlds.new("ciel")
    w.use_nodes = True
    nt = w.node_tree
    bg = nt.nodes["Background"]
    coord = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    nt.links.new(coord.outputs["Generated"], sep.inputs[0])
    # Generated va de 0 à 1 : Z = 0,5 à l'horizon
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = hex_rgba("#FFC59A")
    e = ramp.color_ramp.elements.new(0.1)
    e.color = hex_rgba("#8FDCF7")
    e2 = ramp.color_ramp.elements.new(0.35)
    e2.color = hex_rgba("#3E86E6")
    e3 = ramp.color_ramp.elements.new(0.8)
    e3.color = hex_rgba("#7A4CC4")
    nt.links.new(sep.outputs["Z"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = 1.1
    bpy.context.scene.world = w
    bpy.ops.object.light_add(type="SUN", location=(0, 0, 50))
    s = bpy.context.active_object
    s.data.energy = 7.0
    s.data.color = (1.0, 0.9, 0.75)
    s.rotation_euler = (math.radians(48), math.radians(8), math.radians(-35))
    s.data.angle = math.radians(2.5)
    # nuages : sphères aplaties blanches loin dans le ciel
    for i, (x, y, z, k) in enumerate([(-80, 90, 55, 1.0), (60, 110, 60, 1.3), (-120, -20, 70, 1.1), (100, -60, 52, 0.9), (10, 140, 80, 1.5)]):
        for j in range(4):
            lib.sphere(9 * k, (x + j * 9 * k, y, z + (j % 2) * 2), lib.mat("nuage", "#FFF4F0", 0.6), f"Nuage{i}_{j}", sub=2, scale=(1.4, 1, 0.55))


def shoot(out, names):
    s = bpy.context.scene
    s.render.engine = "CYCLES"
    s.cycles.samples = 40
    s.cycles.device = "CPU"
    s.cycles.use_denoising = True
    try:
        s.cycles.denoiser = "OPENIMAGEDENOISE"
    except Exception:
        pass
    s.render.resolution_x, s.render.resolution_y = 1280, 720
    s.view_settings.view_transform = "Filmic" if "Filmic" in bpy.types.ColorManagedViewSettings.bl_rna.properties["view_transform"].enum_items.keys() else "Standard"
    s.view_settings.look = "None"
    bpy.ops.object.camera_add()
    cam = bpy.context.active_object
    s.camera = cam
    for n in names:
        loc, tgt, lens = VUES[n]
        cam.location = loc
        cam.data.lens = lens
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        s.render.filepath = os.path.join(out, f"toit_{n}.png")
        bpy.ops.render.render(write_still=True)
        print("rendu", n, flush=True)


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    lib.reset()
    map_toit.build_toit(preview=True)
    map_toit.build_ville()
    sky()
    shoot(out, sys.argv[2:] or list(VUES))
