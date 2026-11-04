"""Avatars mannequins (Quaternius, Universal Animation Library 1 et 2, CC0) pour remplacer les persos.

Squelette type Unreal (pelvis, spine_01..03, neck_01, Head, upperarm_l, lowerarm_l, hand_l, thigh_l, calf_l, foot_l, doigts),
13,7k triangles (homme) et 14,6k (femme). Les animations de la bibliothèque 1 sont faites pour le rig Rigify
(DEF-hips…) au repos identique : on les renomme os par os vers les noms Unreal, sans retarget.

Sortie, dans DOSSIER (assets/models par défaut) :
  avatar_{orange,bleu}_rig.glb (homme), avatar_{orange,bleu}2_rig.glb (femme), et leurs *_quest.glb (8k triangles).
Matériaux : « mannequin » (gris) et « neon_equipe » (articulations, couleur d'équipe émissive) ; KcShaders les reconnaît.
Animations au nom du jeu : Repos, Course, Lancer, Passe, Armer, Attrape, Esquive, Touche, KO, Saut, Ramasser,
Celebration, Victoire, Defaite.

Usage : python mannequin.py [DOSSIER]   (bpy, Blender 4.2+ en module Python)
"""
import os
import re
import sys

import bpy

H = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(H, "sources", "quaternius")
UAL1 = os.path.join(SRC, "UAL1_Standard.glb")      # rig Rigify « Rig », 46 animations
UAL2 = os.path.join(SRC, "UAL2_Standard.glb")      # rig Unreal « Armature », mannequin homme, 43 animations
FEMALE = os.path.join(SRC, "Mannequin_F.glb")      # même rig Unreal, mannequin femme, sans animation
OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith(".py") else os.path.join(H, "..", "..", "assets", "models")

def srgb(h):
    """#RRGGBB -> couleur linéaire (glTF et Blender travaillent en linéaire)."""
    c = [int(h[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    return tuple(v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in c)


TEAMS = {"orange": srgb("#FF6A1A"), "bleu": srgb("#2E9BFF")}   # mêmes teintes que les tenues d'équipe de gen_assets.py
GREY = srgb("#7C7F86")   # gris moyen du mannequin Unreal
QUEST_TRIS = 8000
SCALE = 0.95   # os Head à 1,57 m -> 1,49 m, comme « Tete »

# nom du jeu -> (animation source, boucle)
GAME = {
    "Repos": ("Idle_Loop", True),
    "Course": ("Jog_Fwd_Loop", True),
    "Attrape": ("Pistol_Aim_Neutral", False),
    "Esquive": ("Roll", False),
    "Touche": ("Hit_Chest", False),
    "KO": ("Death01", False),
    "Saut": ("Jump_Start", False),
    "Ramasser": ("PickUp_Table", False),
    "Celebration": ("Dance_Loop", True),
    "Victoire": ("Dance_Loop", True),
    "Defaite": ("Idle_No_Loop", True),
}
ARMER_FROM, ARMER_FRAME = "OverhandThrow", 6   # bras armé derrière la tête, tenu pendant la charge
# Lancer et Passe : seulement l'élan d'OverhandThrow (images 0 à 7, jusqu'au bras armé). Le coup de bras au lâcher est fait par
# l'IK d'Avatar (StartPunch) ; la fente du clip complet avancerait les épaules hors de portée de la balle tenue.
THROW_FROM, THROW_END = "OverhandThrow", 7


def rigify_to_ue(name):
    """DEF-upper_arm.L -> upperarm_l, DEF-f_index.02.R -> index_02_r…"""
    fixed = {"DEF-hips": "pelvis", "DEF-spine.001": "spine_01", "DEF-spine.002": "spine_02", "DEF-spine.003": "spine_03",
             "DEF-neck": "neck_01", "DEF-head": "Head", "root": "root"}
    if name in fixed:
        return fixed[name]
    m = re.match(r"DEF-(shoulder|upper_arm|forearm|hand|thigh|shin|foot|toe)\.([LR])$", name)
    if m:
        part = {"shoulder": "clavicle", "upper_arm": "upperarm", "forearm": "lowerarm", "hand": "hand",
                "thigh": "thigh", "shin": "calf", "foot": "foot", "toe": "ball"}[m.group(1)]
        return f"{part}_{m.group(2).lower()}"
    m = re.match(r"DEF-(?:f_)?(index|middle|pinky|ring|thumb)\.0(\d)\.([LR])$", name)
    if m:
        return f"{m.group(1)}_0{m.group(2)}_{m.group(3).lower()}"
    return None


def fcurve_lists(act):
    """Courbes d'une action, API à calques (Blender 4.4+) ou ancienne."""
    if hasattr(act, "layers") and act.layers:
        for layer in act.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    yield bag.fcurves
    elif hasattr(act, "fcurves"):
        yield act.fcurves


def rename_bones(act, mapping):
    for fcs in fcurve_lists(act):
        for fc in list(fcs):
            m = re.match(r'pose\.bones\["([^"]+)"\](.*)', fc.data_path)
            if not m:
                continue
            dst = mapping(m.group(1))
            if dst is None:
                fcs.remove(fc)
            else:
                fc.data_path = f'pose.bones["{dst}"]{m.group(2)}'


def held_pose(src, frame, name):
    """Action de deux clés identiques : la pose de src à l'image frame."""
    act = src.copy()
    act.name = name
    for fcs in fcurve_lists(act):
        for fc in fcs:
            v = fc.evaluate(frame)
            fc.keyframe_points.clear()
            fc.keyframe_points.insert(0, v)
            fc.keyframe_points.insert(10, v)
    return act


def trimmed(src, f0, f1, name):
    """Action réduite aux images f0..f1 de src, recalée à 0."""
    act = src.copy()
    act.name = name
    for fcs in fcurve_lists(act):
        for fc in fcs:
            vals = [(f - f0, fc.evaluate(f)) for f in range(f0, f1 + 1)]
            fc.keyframe_points.clear()
            for f, v in vals:
                fc.keyframe_points.insert(f, v)
    return act


def imported(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for o in [o for o in new if o.type == "MESH" and o.name.startswith("Icosphere")]:
        bpy.data.objects.remove(o)
    return [o for o in bpy.data.objects if o not in before]


def build_actions():
    """Toutes les animations du jeu, sur les noms d'os Unreal."""
    objs = imported(UAL1)
    for o in objs:   # on ne garde que les animations du rig Rigify
        bpy.data.objects.remove(o)
    ual1 = {a.name for a in bpy.data.actions}
    for name in ual1:
        rename_bones(bpy.data.actions[name], rigify_to_ue)
    objs = imported(UAL2)
    for o in objs:
        bpy.data.objects.remove(o)
    game = {}
    for gname, (src, _loop) in GAME.items():
        act = bpy.data.actions[src].copy()
        act.name = gname
        game[gname] = act
    game["Armer"] = held_pose(bpy.data.actions[ARMER_FROM], ARMER_FRAME, "Armer")
    for gname in ("Lancer", "Passe"):
        game[gname] = trimmed(bpy.data.actions[THROW_FROM], 0, THROW_END, gname)
    for a in game.values():
        a.use_fake_user = True
    return game


def team_materials(mesh, team):
    for i, m in enumerate(mesh.data.materials):
        joint = "Joint" in m.name
        n = bpy.data.materials.new(f"neon_equipe_{team}" if joint else "mannequin")
        n.use_nodes = True
        b = n.node_tree.nodes["Principled BSDF"]
        c = TEAMS[team] if joint else GREY
        b.inputs["Base Color"].default_value = (*c, 1)
        b.inputs["Roughness"].default_value = 0.35 if joint else 0.5
        if joint:
            b.inputs["Emission Color"].default_value = (*c, 1)
            b.inputs["Emission Strength"].default_value = 2.0
        mesh.data.materials[i] = n


def export(path, game, female, team, quest):
    bpy.ops.wm.read_homefile(use_empty=True)
    acts = build_actions()
    objs = imported(FEMALE if female else UAL2)
    arm = next(o for o in objs if o.type == "ARMATURE")
    mesh = next(o for o in objs if o.type == "MESH")
    if arm.animation_data:
        arm.animation_data.action = None
        for t in list(arm.animation_data.nla_tracks):
            arm.animation_data.nla_tracks.remove(t)
    for a in [a for a in bpy.data.actions if a.name not in acts]:
        bpy.data.actions.remove(a)
    arm.name = "Mannequin"
    team_materials(mesh, team)
    if quest:
        d = mesh.modifiers.new("quest", "DECIMATE")
        d.ratio = QUEST_TRIS / sum(len(p.vertices) - 2 for p in mesh.data.polygons)
        bpy.context.view_layer.objects.active = mesh
        bpy.ops.object.modifier_apply(modifier=d.name)
    # le jeu regarde vers +Y dans Blender (comme les persos de rig.py) : demi-tour, appliqué au rig et au maillage
    arm.rotation_mode = "XYZ"
    arm.rotation_euler = (0.0, 0.0, 3.14159265)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    # taille du jeu : centre de la tête à 1,62 m comme les persos de rig.py (Avatar.HeadHeight). Échelle laissée sur le nœud,
    # pas appliquée, pour que les translations des animations (bassin, sauts) restent justes.
    arm.scale = (SCALE, SCALE, SCALE)
    arm.animation_data_create()
    for name, act in acts.items():
        tr = arm.animation_data.nla_tracks.new()
        tr.name = name
        st = tr.strips.new(name, 0, act)
        if hasattr(st, "action_slot") and act.slots:
            st.action_slot = act.slots[0]
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", export_animations=True, export_animation_mode="NLA_TRACKS",
                              export_apply=False, export_yup=True)
    tris = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    print("glb", os.path.basename(path), tris, "triangles", len(acts), "animations")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for team in TEAMS:
        for female, suffix in ((False, ""), (True, "2")):
            for quest in (False, True):
                name = f"avatar_{team}{suffix}_rig" + ("_quest" if quest else "")
                export(os.path.join(OUT, name + ".glb"), GAME, female, team, quest)
