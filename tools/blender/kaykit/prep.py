"""Prépare un personnage KayKit (CC0) pour le jeu : retire armes et cape, applique la palette d'équipe, garde les animations utiles.
Usage : python3 -I prep.py <Rogue.glb> <texture_equipe.png> <sortie.glb>"""
import os
import sys

import bpy

KEEP = {"Idle", "Unarmed_Idle", "Running_A", "Running_B", "Running_Strafe_Left", "Running_Strafe_Right", "Walking_A", "Walking_Backwards",
        "Throw", "Dodge_Backward", "Dodge_Forward", "Dodge_Left", "Dodge_Right", "Hit_A", "Hit_B", "Death_A", "Death_B", "Jump_Start", "Jump_Idle",
        "Jump_Land", "Cheer", "Block", "Blocking", "Spellcast_Raise", "PickUp", "Interact", "Unarmed_Pose", "T-Pose"}
DROP = ("Knife", "1H_", "2H_", "Throwable", "Rogue_Cape", "Icosphere")

src, tex, out = sys.argv[-3:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
for o in list(bpy.context.scene.objects):
    if o.type == "MESH" and o.name.startswith(DROP):
        bpy.data.objects.remove(o, do_unlink=True)
for a in list(bpy.data.actions):
    if a.name not in KEEP:
        bpy.data.actions.remove(a)
img = bpy.data.images.load(tex)
for m in bpy.data.materials:
    for n in m.node_tree.nodes:
        if n.type == "TEX_IMAGE":
            n.image = img
            n.interpolation = "Closest"
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_animations=True, export_animation_mode="ACTIONS", export_apply=False,
                          export_materials="EXPORT", export_cameras=False, export_lights=False)
print("ok", out, os.path.getsize(out))
