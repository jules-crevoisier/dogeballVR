"""Rend des .glb en PNG (3 vues) pour la revue visuelle.

Usage : python3 -I tools/blender/render.py <dossier_glb> <dossier_png> [nom ...]
"""
import math
import os
import sys

import bpy
from mathutils import Vector


def render_one(glb, out_prefix, res=420):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb)
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    ctr, size = (lo + hi) / 2, max((hi - lo).length, 0.1)

    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.07, 0.13, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    bpy.context.scene.world = w
    for loc, e in (((3, -4, 6), 3.5), ((-5, 3, 2), 2.0), ((0, 6, 3), 1.5)):
        bpy.ops.object.light_add(type="SUN", location=loc)
        l = bpy.context.active_object
        l.data.energy = e
        l.rotation_euler = (ctr - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    bpy.ops.object.camera_add()
    cam = bpy.context.active_object
    cam.data.lens = 50
    bpy.context.scene.camera = cam
    s = bpy.context.scene
    s.render.engine = "CYCLES"
    s.cycles.samples = 24
    s.cycles.device = "CPU"
    s.render.resolution_x = s.render.resolution_y = res
    s.view_settings.view_transform = "Standard"
    d = size * 1.7
    # glTF importé : Y haut devient Z haut dans Blender (conversion inverse faite par l'importeur)
    for name, (az, el) in {"3q": (35, 25), "face": (0, 8), "haut": (35, 70)}.items():
        a, e = math.radians(az), math.radians(el)
        cam.location = ctr + Vector((math.sin(a) * math.cos(e), math.cos(a) * math.cos(e), math.sin(e))) * d
        cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", "Y").to_euler()
        s.render.filepath = f"{out_prefix}_{name}.png"
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    src, dst = sys.argv[1], sys.argv[2]
    os.makedirs(dst, exist_ok=True)
    names = sys.argv[3:] or sorted(f[:-4] for f in os.listdir(src) if f.endswith(".glb"))
    for n in names:
        render_one(os.path.join(src, n + ".glb"), os.path.join(dst, n))
        print("rendu", n)
