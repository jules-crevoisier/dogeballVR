"""Outils communs pour générer les assets 3D du jeu avec Blender (bpy)."""
import math
import bpy
import bmesh
from mathutils import Vector

PALETTE = {
    "sol": "#3A3F52", "mur": "#1B2540", "ligne": "#F2F4F8",
    "orange": "#FF6A1A", "bleu": "#6CC8FF", "balle": "#D6FF2E",
    "panneau": "#101830", "sol_orange": "#4A3F4C", "sol_bleu": "#364866",
    "jambes": "#14192B", "visiere": "#0B1020", "neutre": "#8A91A8",
}


def hex_rgba(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    lin = [(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c]
    return (*lin, 1.0)


_mats = {}


def mat(name, color, emissive=0.0):
    key = (name, color, emissive)
    if key in _mats and _mats[key].name in bpy.data.materials:
        return _mats[key]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    rgba = hex_rgba(PALETTE.get(color, color))
    b.inputs["Base Color"].default_value = rgba
    b.inputs["Roughness"].default_value = 0.85
    b.inputs["Metallic"].default_value = 0.0
    if emissive > 0:
        b.inputs["Emission Color"].default_value = rgba
        b.inputs["Emission Strength"].default_value = emissive
    m.diffuse_color = rgba
    _mats[key] = m
    return m


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mats.clear()


def finish(obj, material, name=None, smooth=True, bevel=0.0, parent=None):
    if name:
        obj.name = name
        obj.data.name = name
    obj.data.materials.clear()
    obj.data.materials.append(material)
    if bevel > 0:
        m = obj.modifiers.new("bev", "BEVEL")
        m.width = bevel
        m.segments = 2
        m.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.modifier_apply(modifier="bev")
    for p in obj.data.polygons:
        p.use_smooth = smooth
    if parent:
        obj.parent = parent
    return obj


def box(size, loc=(0, 0, 0), material=None, name="box", bevel=0.02, parent=None, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0))
    o = bpy.context.active_object
    o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    finish(o, material, name, smooth=False, bevel=bevel, parent=parent)
    o.rotation_euler = rot
    o.location = loc
    return o


def cyl(r, h, loc=(0, 0, 0), material=None, name="cyl", verts=16, bevel=0.0,
        rot=(0, 0, 0), parent=None, smooth=True, r2=None):
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=h, vertices=verts, location=loc, rotation=rot)
    else:
        bpy.ops.mesh.primitive_cone_add(radius1=r, radius2=r2, depth=h, vertices=verts, location=loc, rotation=rot)
    o = bpy.context.active_object
    return finish(o, material, name, smooth=smooth and bevel == 0, bevel=bevel, parent=parent)


def sphere(r, loc=(0, 0, 0), material=None, name="sph", sub=2, scale=(1, 1, 1), parent=None):
    bpy.ops.mesh.primitive_ico_sphere_add(radius=r, subdivisions=sub, location=loc)
    o = bpy.context.active_object
    o.scale = scale
    bpy.ops.object.transform_apply(scale=True)
    return finish(o, material, name, smooth=True, parent=parent)


def torus(R, r, loc=(0, 0, 0), material=None, name="torus", seg=24, rseg=6, rot=(0, 0, 0), parent=None):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=seg,
                                     minor_segments=rseg, location=loc, rotation=rot)
    return finish(bpy.context.active_object, material, name, smooth=True, parent=parent)


def outline_shell(obj, thickness, material, name=None):
    """Coque inversée (contour sombre) pour une sphère centrée à l'origine."""
    dup = obj.copy()
    dup.data = obj.data.copy()
    bpy.context.collection.objects.link(dup)
    r = max(abs(v.co.length) for v in dup.data.vertices)
    k = (r + thickness) / r
    for v in dup.data.vertices:
        v.co *= k
    bm = bmesh.new()
    bm.from_mesh(dup.data)
    bmesh.ops.reverse_faces(bm, faces=bm.faces)
    bm.to_mesh(dup.data)
    bm.free()
    dup.data.materials.clear()
    dup.data.materials.append(material)
    dup.name = name or obj.name + "_contour"
    return dup


def tris(objs):
    dg = bpy.context.evaluated_depsgraph_get()
    n = 0
    for o in objs:
        if o.type == "MESH":
            me = o.evaluated_get(dg).to_mesh()
            n += sum(len(p.vertices) - 2 for p in me.polygons)
    return n


def export(path, objs=None):
    bpy.ops.object.select_all(action="DESELECT")
    objs = objs or [o for o in bpy.context.scene.objects]
    for o in objs:
        o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True,
                              export_apply=True, export_yup=True, export_materials="EXPORT",
                              export_cameras=False, export_lights=False)
    return tris([o for o in objs])
