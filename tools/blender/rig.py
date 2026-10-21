"""Squelette et animations des personnages : un seul maillage skinné (corps Skin + accessoires collés à leur os) et des actions clés.

Os (Blender, Z haut, face +Y) posés sur le squelette Skin du corps. Les accessoires (tête, gants, semelles...) sont fusionnés
au corps avec un poids de 1 sur leur os : l'export glTF donne un seul maillage, que Godot importe avec son Skeleton3D et ses animations.
"""
import math

import bpy
from mathutils import Euler, Quaternion, Vector

FPS = 30
BONES = {   # nom : (tête, queue, parent)
    "Hanches": ((0, 0, 0.84), (0, 0, 1.0), None),
    "Dos": ((0, 0, 1.0), (0, 0, 1.3), "Hanches"),
    "Cou": ((0, 0, 1.3), (0, 0, 1.45), "Dos"),
    "Tete": ((0, 0, 1.45), (0, 0, 2.0), "Cou"),
    "Bras.G": ((-0.3, 0, 1.3), (-0.5, 0.02, 1.08), "Dos"),
    "AvantBras.G": ((-0.5, 0.02, 1.08), (-0.58, 0.07, 0.86), "Bras.G"),
    "Main.G": ((-0.58, 0.07, 0.86), (-0.63, 0.1, 0.74), "AvantBras.G"),
    "Bras.D": ((0.3, 0, 1.3), (0.5, 0.02, 1.08), "Dos"),
    "AvantBras.D": ((0.5, 0.02, 1.08), (0.58, 0.07, 0.86), "Bras.D"),
    "Main.D": ((0.58, 0.07, 0.86), (0.63, 0.1, 0.74), "AvantBras.D"),
    "Cuisse.G": ((-0.16, 0, 0.8), (-0.17, 0.02, 0.44), "Hanches"),
    "Tibia.G": ((-0.17, 0.02, 0.44), (-0.17, 0, 0.16), "Cuisse.G"),
    "Pied.G": ((-0.17, 0, 0.16), (-0.17, 0.26, 0.08), "Tibia.G"),
    "Cuisse.D": ((0.16, 0, 0.8), (0.17, 0.02, 0.44), "Hanches"),
    "Tibia.D": ((0.17, 0.02, 0.44), (0.17, 0, 0.16), "Cuisse.D"),
    "Pied.D": ((0.17, 0, 0.16), (0.17, 0.26, 0.08), "Tibia.D"),
}
HERO_BONES = {   # personnages aux proportions de la fiche (hero.J)
    "Hanches": ((0, 0, 0.93), (0, 0, 1.07), None),
    "Dos": ((0, 0, 1.07), (0, -0.005, 1.38), "Hanches"),
    "Cou": ((0, -0.005, 1.38), (0, 0, 1.49), "Dos"),
    "Tete": ((0, 0, 1.49), (0, 0.005, 1.75), "Cou"),
    "Bras.G": ((-0.19, -0.01, 1.385), (-0.39, 0.0, 1.19), "Dos"),
    "AvantBras.G": ((-0.39, 0.0, 1.19), (-0.54, 0.03, 1.02), "Bras.G"),
    "Main.G": ((-0.54, 0.03, 1.02), (-0.59, 0.05, 0.9), "AvantBras.G"),
    "Bras.D": ((0.19, -0.01, 1.385), (0.39, 0.0, 1.19), "Dos"),
    "AvantBras.D": ((0.39, 0.0, 1.19), (0.54, 0.03, 1.02), "Bras.D"),
    "Main.D": ((0.54, 0.03, 1.02), (0.59, 0.05, 0.9), "AvantBras.D"),
    "Cuisse.G": ((-0.095, 0, 0.9), (-0.108, 0.018, 0.5), "Hanches"),
    "Tibia.G": ((-0.108, 0.018, 0.5), (-0.115, -0.005, 0.13), "Cuisse.G"),
    "Pied.G": ((-0.115, -0.005, 0.13), (-0.115, 0.2, 0.05), "Tibia.G"),
    "Cuisse.D": ((0.095, 0, 0.9), (0.108, 0.018, 0.5), "Hanches"),
    "Tibia.D": ((0.108, 0.018, 0.5), (0.115, -0.005, 0.13), "Cuisse.D"),
    "Pied.D": ((0.115, -0.005, 0.13), (0.115, 0.2, 0.05), "Tibia.D"),
}


def side(o):
    return "G" if o.location.x + sum((v.co.x for v in o.data.vertices), 0) / max(1, len(o.data.vertices)) < 0 else "D"


def bone_for(o):
    n = o.name
    if n.startswith("H_"):
        return "Tete"
    if n.startswith(("Gant", "Pouce")):
        return "Main." + side(o)
    if n.startswith("Poignet"):
        return "AvantBras." + side(o)
    if n.startswith("Neon"):
        return "Bras." + side(o)
    if n.startswith(("Semelle", "Bout", "Basket")):
        return "Pied." + side(o)
    if n.startswith("Revers"):
        return "Tibia." + side(o)
    if n.startswith("Ourlet") or n.startswith("Poche"):
        return "Hanches"
    return "Dos"   # zip, cordons, capuche, emblème


def build_armature(bones=BONES):
    arm = bpy.data.armatures.new("Squelette")
    rig = bpy.data.objects.new("Rig", arm)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, (h, t, parent) in bones.items():
        b = arm.edit_bones.new(name)
        b.head, b.tail = h, t
        b.roll = 0
        if parent:
            b.parent = arm.edit_bones[parent]
            b.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def skin_to_rig(objs, rig):
    """Corps : poids automatiques ; vêtements « W_ » : poids copiés du corps le plus proche ; autres accessoires : 100 % sur leur os ;
    puis tout est fusionné en un maillage."""
    body = next(o for o in objs if o.name.startswith("Corps"))
    others = [o for o in objs if o is not body and o.type == "MESH"]
    worn = [o for o in others if o.name.startswith("W_")]
    others = [o for o in others if o not in worn]
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    for o in others:
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        vg = o.vertex_groups.new(name=bone_for(o))
        vg.add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
        o.select_set(False)
    for o in worn:
        bpy.ops.object.select_all(action="DESELECT")
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        dt = o.modifiers.new("poids", "DATA_TRANSFER")
        dt.object = body
        dt.use_vert_data = True
        dt.data_types_verts = {"VGROUP_WEIGHTS"}
        dt.vert_mapping = "POLYINTERP_NEAREST"
        dt.layers_vgroup_select_src = "ALL"
        dt.layers_vgroup_select_dst = "NAME"
        bpy.ops.object.datalayout_transfer(modifier="poids")
        bpy.ops.object.modifier_apply(modifier="poids")
    bpy.ops.object.select_all(action="DESELECT")
    for o in others + worn:
        o.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    body.name = "Personnage"
    return body


# ------------------------------------------------------------------ animations
def _rot(pb, frame, x=0.0, y=0.0, z=0.0):
    """Rotation donnée dans le repère du personnage (X : bascule avant/arrière, Y : sur le côté, Z : torsion), convertie en local d'os."""
    b = pb.bone.matrix_local.to_quaternion()
    w = Euler((math.radians(x), math.radians(y), math.radians(z)), "XYZ").to_quaternion()
    pb.rotation_mode = "QUATERNION"
    pb.rotation_quaternion = b.inverted() @ w @ b
    pb.keyframe_insert("rotation_quaternion", frame=frame)


def _loc(pb, frame, x=0.0, y=0.0, z=0.0):
    """Décalage donné dans le repère du personnage (mètres), converti en local d'os."""
    b = pb.bone.matrix_local.to_quaternion()
    pb.location = b.inverted() @ Vector((x, y, z))
    pb.keyframe_insert("location", frame=frame)


def _action(rig, name, keys, length, loop=True):
    """keys : {image: {os: (rx, ry, rz) ou ("loc", x, y, z)}}. Les os non cités restent au repos."""
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = act
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = Quaternion()
        pb.location = (0, 0, 0)
    used = set()
    for f in sorted(keys):
        for bone, v in keys[f].items():
            used.add(bone)
            pb = rig.pose.bones[bone]
            if v[0] == "loc":
                _loc(pb, f, *v[1:])
            else:
                _rot(pb, f, *v)
    if loop:   # même pose à la fin qu'au début
        for bone, v in keys[min(keys)].items():
            pb = rig.pose.bones[bone]
            if v[0] == "loc":
                _loc(pb, length, *v[1:])
            else:
                _rot(pb, length, *v)
    track = rig.animation_data.nla_tracks.new()
    track.name = name
    track.strips.new(name, 1, act)
    rig.animation_data.action = None
    return act


def animate(rig, arm_rest=6):
    """Conventions : X positif = vers l'avant pour un membre qui pend (bras, cuisse), vers l'arrière pour le dos et la tête.
    Y positif écarte le bras gauche, Y négatif écarte le bras droit. Tous les os cités à une clé doivent l'être aux autres clés."""
    A = "AvantBras"
    R, R2 = arm_rest, arm_rest + 3   # écart des bras au repos (degrés, en plus de la pose en A du squelette)
    arms0 = {"Bras.G": (0, R, 0), "Bras.D": (0, -R, 0), f"{A}.G": (12, 0, 0), f"{A}.D": (12, 0, 0)}
    _action(rig, "Repos", {
        1: {"Hanches": ("loc", 0, 0, 0), "Dos": (0, 0, 0), "Tete": (0, 0, 0), **arms0},
        31: {"Hanches": ("loc", 0, 0, -0.012), "Dos": (3, 0, 0), "Tete": (-3, 0, 0), "Bras.G": (0, R2, 0), "Bras.D": (0, -R2, 0),
             f"{A}.G": (16, 0, 0), f"{A}.D": (16, 0, 0)},
    }, 61)
    run = {}
    for i, f in enumerate((1, 6, 11, 16)):
        s = (1, 0, -1, 0)[i]
        up = (0, 1, 0, 1)[i]
        run[f] = {
            "Hanches": ("loc", 0, 0, 0.035 * up - 0.02), "Dos": (-14, 0, 8 * s), "Tete": (10, 0, -6 * s),
            "Cuisse.G": (42 * s, 0, 0), "Cuisse.D": (-42 * s, 0, 0),
            "Tibia.G": (-70 * max(0, -s) - 20 * (1 - abs(s)) - 10, 0, 0), "Tibia.D": (-70 * max(0, s) - 20 * (1 - abs(s)) - 10, 0, 0),
            "Bras.G": (-48 * s, 10, 0), "Bras.D": (48 * s, -10, 0), f"{A}.G": (70, 0, 0), f"{A}.D": (70, 0, 0),
        }
    _action(rig, "Course", run, 21)
    D = {"Dos": (0, 0, 0), "Bras.D": (0, -R, 0), f"{A}.D": (12, 0, 0), "Bras.G": (0, R, 0), f"{A}.G": (12, 0, 0), "Tete": (0, 0, 0)}
    _action(rig, "Lancer", {
        1: dict(D),
        8: {"Dos": (10, 0, 40), "Bras.D": (-30, -95, 0), f"{A}.D": (0, -60, 0), "Bras.G": (60, 30, 0), f"{A}.G": (20, 0, 0), "Tete": (0, 0, -30)},
        12: {"Dos": (-22, 0, -30), "Bras.D": (130, -20, 0), f"{A}.D": (20, 0, 0), "Bras.G": (-30, 20, 0), f"{A}.G": (40, 0, 0), "Tete": (5, 0, 20)},
        20: {"Dos": (-12, 0, -15), "Bras.D": (70, 10, 0), f"{A}.D": (30, 0, 0), "Bras.G": (-10, 10, 0), f"{A}.G": (30, 0, 0), "Tete": (0, 0, 8)},
        28: dict(D),
    }, 28, loop=False)
    _action(rig, "Attrape", {
        1: dict(D),
        5: {"Dos": (6, 0, 0), "Bras.G": (85, 25, 0), "Bras.D": (85, -25, 0), f"{A}.G": (95, 0, 0), f"{A}.D": (95, 0, 0), "Tete": (-6, 0, 0)},
        11: {"Dos": (-10, 0, 0), "Bras.G": (55, 10, 0), "Bras.D": (55, -10, 0), f"{A}.G": (120, -30, 0), f"{A}.D": (120, 30, 0), "Tete": (6, 0, 0)},
        20: dict(D),
    }, 20, loop=False)
    crouch = {"Hanches": ("loc", 0, 0, -0.2), "Dos": (-15, 28, 0), "Cuisse.G": (55, 0, 0), "Cuisse.D": (55, 0, 0),
              "Tibia.G": (-95, 0, 0), "Tibia.D": (-95, 0, 0), "Bras.G": (40, 35, 0), "Bras.D": (30, -50, 0)}
    stand = {"Hanches": ("loc", 0, 0, 0), "Dos": (0, 0, 0), "Cuisse.G": (0, 0, 0), "Cuisse.D": (0, 0, 0),
             "Tibia.G": (0, 0, 0), "Tibia.D": (0, 0, 0), "Bras.G": (0, R, 0), "Bras.D": (0, -R, 0)}
    _action(rig, "Esquive", {1: dict(stand), 5: dict(crouch), 12: dict(crouch), 18: dict(stand)}, 18, loop=False)
    _action(rig, "Touche", {
        1: {"Dos": (0, 0, 0), "Tete": (0, 0, 0), "Bras.G": (0, R, 0), "Bras.D": (0, -R, 0), "Hanches": ("loc", 0, 0, 0)},
        3: {"Dos": (28, 0, 10), "Tete": (30, 0, 0), "Bras.G": (-20, 60, 0), "Bras.D": (-20, -60, 0), "Hanches": ("loc", 0, -0.08, 0)},
        14: {"Dos": (0, 0, 0), "Tete": (0, 0, 0), "Bras.G": (0, R, 0), "Bras.D": (0, -R, 0), "Hanches": ("loc", 0, 0, 0)},
    }, 14, loop=False)
    ko0 = {"Hanches": ("loc", 0, 0, 0), "Dos": (0, 0, 0), "Tete": (0, 0, 0), "Bras.G": (0, R, 0), "Bras.D": (0, -R, 0),
           "Cuisse.G": (0, 0, 0), "Cuisse.D": (0, 0, 0), "Tibia.G": (0, 0, 0), "Tibia.D": (0, 0, 0)}
    _action(rig, "KO", {
        1: ko0,
        7: {"Hanches": ("loc", 0, -0.25, -0.25), "Dos": (35, 0, 0), "Tete": (30, 0, 0), "Bras.G": (-10, 120, 0), "Bras.D": (-10, -120, 0),
            "Cuisse.G": (60, 0, 0), "Cuisse.D": (40, 0, 0), "Tibia.G": (-60, 0, 0), "Tibia.D": (-30, 0, 0)},
        16: {"Hanches": ("loc", 0, -0.55, -0.62), "Dos": (75, 0, 0), "Tete": (15, 0, 15), "Bras.G": (20, 150, 0), "Bras.D": (20, -150, 0),
             "Cuisse.G": (85, 0, -10), "Cuisse.D": (70, 0, 10), "Tibia.G": (-40, 0, 0), "Tibia.D": (-10, 0, 0)},
        24: {"Hanches": ("loc", 0, -0.6, -0.68), "Dos": (85, 0, 0), "Tete": (10, 0, 20), "Bras.G": (25, 160, 0), "Bras.D": (25, -160, 0),
             "Cuisse.G": (90, 0, -10), "Cuisse.D": (80, 0, 10), "Tibia.G": (-30, 0, 0), "Tibia.D": (-5, 0, 0)},
    }, 24, loop=False)


def rig_character(objs, bones=BONES, arm_rest=6):
    rig = build_armature(bones)
    body = skin_to_rig(objs, rig)
    animate(rig, arm_rest)
    bpy.context.scene.render.fps = FPS
    return [rig, body]
