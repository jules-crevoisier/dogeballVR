"""Corps organiques : squelette de sommets + modificateur Skin + Subdivision, couleurs et occlusion cuites dans les couleurs de sommets."""
import math
import random

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

from lib import hex_rgba, mat


def vcol_mat():
    """Matériau « peinture » : la couleur vient de l'attribut de sommet COLOR_0 (le moteur la lit via le shader toon)."""
    key = "peinture"
    if key in bpy.data.materials:
        return bpy.data.materials[key]
    m = bpy.data.materials.new(key)
    m.use_nodes = True
    n = m.node_tree.nodes
    b = n["Principled BSDF"]
    a = n.new("ShaderNodeVertexColor")
    a.layer_name = "Col"
    m.node_tree.links.new(a.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.9
    return m


def skin_body(nodes, edges, subdiv=2, name="Corps"):
    """nodes : liste de (x, y, z, rx, ry). Retourne l'objet lissé (modificateurs appliqués)."""
    me = bpy.data.meshes.new(name)
    me.from_pydata([(n[0], n[1], n[2]) for n in nodes], edges, [])
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    sk = ob.modifiers.new("skin", "SKIN")
    sk.branch_smoothing = 0.6
    sk.use_smooth_shade = True
    ob.data.skin_vertices[0].data[0].use_root = True
    for i, n in enumerate(nodes):
        ob.data.skin_vertices[0].data[i].radius = (n[3], n[4])
    ss = ob.modifiers.new("sub", "SUBSURF")
    ss.levels = subdiv
    ss.render_levels = subdiv
    for m in ("skin", "sub"):
        bpy.ops.object.modifier_apply(modifier=m)
    for p in ob.data.polygons:
        p.use_smooth = True
    return ob


def paint(obj, color_fn, ao_rays=24, ao_dist=0.35, ao_strength=0.65, seed=3, extra_blockers=()):
    """Couleur de sommet = color_fn(position, normale) assombrie par l'occlusion ambiante (lancer de rayons sur le maillage lui-même)."""
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    tree = BVHTree.FromBMesh(bm)
    rnd = random.Random(seed)
    layer = me.color_attributes.get("Col") or me.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
    for i, v in enumerate(bm.verts):
        n = v.normal.normalized()
        hit = 0
        for _ in range(ao_rays):
            d = Vector((rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, 1))).normalized()
            if d.dot(n) < 0:
                d = -d
            if tree.ray_cast(v.co + n * 0.01, d, ao_dist)[0] is not None:
                hit += 1
        ao = 1.0 - ao_strength * hit / max(1, ao_rays)
        c = color_fn(v.co, n)
        r, g, b = c[:3]
        rough = c[4] if len(c) > 4 else 0.8   # rugosité PBR rangée dans l'alpha
        # éclairage peint : dessus plus clair, dessous plus froid
        up = 0.9 + 0.2 * max(-1.0, min(1.0, n.z)) * 0.5
        layer.data[i].color = (r * ao * up, g * ao * up, b * ao * up, rough)
    bm.free()
    obj.data.materials.clear()
    obj.data.materials.append(vcol_mat())
    return obj
