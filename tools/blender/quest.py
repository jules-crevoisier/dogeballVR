"""Allègement pour le Quest : fusion des objets maillés par matériau (un appel de rendu par matériau)."""
import math

import bpy
from mathutils import Vector


def merge_by_material(sectors=8, near=8.0):
    """Rend chaque objet maillé indépendant (copies liées comprises), applique ses transformations, puis fusionne
    les objets qui partagent le même premier matériau et le même secteur angulaire autour du centre : peu d'appels
    de rendu, mais l'élagage hors champ garde de quoi travailler. Les pièces à moins de `near` m du centre forment
    leur propre secteur. Retourne le nombre d'objets restants."""
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for o in [o for o in bpy.context.scene.objects if o.type == "FONT"]:
        meshes.append(o)
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.hide_render = False
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.make_single_user(object=True, obdata=True)
    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    groups = {}
    for o in [o for o in bpy.context.scene.objects if o.type == "MESH"]:
        key = o.data.materials[0].name if o.data.materials and o.data.materials[0] else "_"
        c = sum((Vector(v) for v in o.bound_box), Vector()) / 8
        sec = -1 if c.xy.length < near else int((math.atan2(c.y, c.x) + math.pi) / (2 * math.pi) * sectors) % sectors
        groups.setdefault((key, sec), []).append(o)
    for key, objs in groups.items():
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        if len(objs) > 1:
            bpy.ops.object.join()
        objs[0].name = f"Q_{key[0]}_{key[1]}"
    return len(groups)
