"""Planche de revue des héros (rendu Cycles) : Riko, Bo, Nova et Kai côte à côte en garde, gros plans des visages
et test de silhouette (ombres noires sur fond blanc, fiche modeleurs § 2).

    python3 -I tools/blender/lineup.py <dossier> [prefixe] [--test]
écrit <prefixe>_groupe.png, <prefixe>_visages.png, <prefixe>_silhouettes.png
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import capsules  # noqa: E402
import hero  # noqa: E402
import lib  # noqa: E402
import rig as rigmod  # noqa: E402

CAST = [("orange", 0, "Riko"), ("orange", 1, "Bo"), ("bleu", 0, "Nova"), ("bleu", 1, "Kai")]
GAP = 1.15


def build(action="Repos", frame=1):
    rigs = []
    for i, (team, variant, _) in enumerate(CAST):
        before = set(bpy.data.actions)
        rig, body = rigmod.rig_character(hero.hero(team, variant), rigmod.HERO_BONES, arm_rest=-14)
        new = {a.name.split(".")[0]: a for a in set(bpy.data.actions) - before}
        for t in rig.animation_data.nla_tracks:
            t.mute = True
        rig.animation_data.action = new[action]
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        mats = {pb.name: pb.matrix_basis.copy() for pb in rig.pose.bones}
        rig.animation_data_clear()
        for pb in rig.pose.bones:
            pb.matrix_basis = mats[pb.name]
        x = (i - 1.5) * GAP
        # trois quarts : chacun tourné de 20° vers le centre
        rig.matrix_world = Matrix.Translation(Vector((x, 0, 0))) @ Matrix.Rotation(math.radians(-x * 12), 4, "Z") @ rig.matrix_world
        rigs.append(rig)
    bpy.context.scene.frame_set(1)
    return rigs


def studio(white=False):
    w = bpy.data.worlds.new("studio")
    w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (1, 1, 1, 1) if white else lib.hex_rgba("#BFD3EA")
    bg.inputs["Strength"].default_value = 1.0 if white else 0.55
    bpy.context.scene.world = w
    if white:
        return
    floor = lib.box((14, 8, 0.02), (0, -1.0, -0.01), lib.mat("sol_studio", "#D9D4CC"), "Sol", 0.0)
    del floor
    for name, loc, energy, size, col in (("Cle", (-3.5, 4.5, 5.0), 900, 3.0, "#FFF1E0"), ("Contre", (3.0, -3.5, 3.6), 700, 2.0, "#9FD8FF"),
                                         ("Remplissage", (4.0, 5.0, 2.0), 220, 4.0, "#E8EEFF")):
        bpy.ops.object.light_add(type="AREA", location=loc)
        L = bpy.context.active_object
        L.name = name
        L.data.energy = energy
        L.data.size = size
        L.data.color = lib.hex_rgba(col)[:3]
        L.rotation_euler = (Vector((0, 0, 1.2)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()


def silhouettes():
    black = lib.mat("noir", "#000000")
    black.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0, 0, 0, 1)
    black.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
    black.node_tree.nodes["Principled BSDF"].inputs["Specular IOR Level"].default_value = 0.0
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.data.materials.clear()
            o.data.materials.append(black)
    for o in [o for o in bpy.data.objects if o.type == "LIGHT" or o.name == "Sol"]:
        bpy.data.objects.remove(o, do_unlink=True)


def shoot(path, loc, tgt, lens, res, test, samples=64):
    s = bpy.context.scene
    s.render.engine = "CYCLES"
    s.cycles.device = "CPU"
    s.cycles.samples = 10 if test else samples
    s.cycles.use_denoising = True
    vt = bpy.types.ColorManagedViewSettings.bl_rna.properties["view_transform"].enum_items.keys()
    s.view_settings.view_transform = "AgX" if "AgX" in vt else "Filmic"
    k = 0.4 if test else 1.0
    s.render.resolution_x, s.render.resolution_y = int(res[0] * k), int(res[1] * k)
    cam = s.camera
    if cam is None:
        bpy.ops.object.camera_add()
        cam = bpy.context.active_object
        s.camera = cam
    cam.location = loc
    cam.data.lens = lens
    cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    s.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("rendu", path, flush=True)


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if a != "--test"]
    test = "--test" in sys.argv
    out, prefix = args[0], (args[1] if len(args) > 1 else "heros")
    os.makedirs(out, exist_ok=True)
    lib.reset()
    build()
    capsules.rough_from_alpha()
    studio()
    shoot(os.path.join(out, f"{prefix}_groupe.png"), (0, 6.2, 1.25), (0, 0, 0.95), 42, (1920, 1080), test)
    for i, (_, _, name) in enumerate(CAST):
        x = (i - 1.5) * GAP
        shoot(os.path.join(out, f"{prefix}_visage_{name}.png"), (x + 0.12, 1.25, 1.66), (x, 0, 1.6), 85, (640, 720), test, 48)
    silhouettes()
    studio(white=True)
    shoot(os.path.join(out, f"{prefix}_silhouettes.png"), (0, 6.2, 1.25), (0, 0, 0.95), 42, (1920, 1080), test, 8)
