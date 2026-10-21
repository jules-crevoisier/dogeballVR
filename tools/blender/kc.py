"""Matériaux « peints » (aperçu Cycles) et pièces de décor pour la DA Knockout City.

Chaque matériau porte un nom de type (brique, beton, danger, fenetres, neon_*) : le moteur Godot reconnaît ce nom et lui
applique le shader toon (scripts/Visual/KcShaders). Dans le glb il ne reste qu'une couleur unie ; le motif vient du shader.
"""
import math
import random

import bpy
from mathutils import Vector

import lib
from lib import hex_rgba

KINDS = {
    # nom: (couleur de base, couleur 2, motif)
    "brique": ("#B5533C", "#8E3F30", "brique"),
    "brique_jaune": ("#D9A441", "#B58530", "brique"),
    "beton": ("#444B66", "#363C54", "bruit"),
    "beton_clair": ("#707898", "#5B6384", "bruit"),
    "beton_zone": ("#5B6382", "#4B5373", "bruit"),
    "danger_ligne": ("#F2C230", "#F2C230", "uni"),
    "capuchon": ("#262B42", "#1B1F33", "uni"),
    "bois": ("#B07A45", "#8A5A30", "planches"),
    "metal_bleu": ("#2F6F8F", "#245A75", "tole"),
    "metal_sombre": ("#303548", "#25293A", "uni"),
    "danger": ("#F2C230", "#1A1C26", "danger"),
    "fenetres": ("#232A4D", "#F6C56A", "fenetres"),
    "peinture_blanche": ("#EDEFF5", "#EDEFF5", "uni"),
    "orange_equipe": ("#FF6A1A", "#FF6A1A", "uni"),
    "bleu_equipe": ("#6CC8FF", "#6CC8FF", "uni"),
    "herbe": ("#4E8F4A", "#3F7A3E", "bruit"),
    "tuyau": ("#FF7A2E", "#FF7A2E", "uni"),
}
NEON = {"neon_cyan": "#37E8FF", "neon_orange": "#FF8A2A", "neon_rose": "#FF3FB5", "neon_jaune": "#FFE34D", "neon_vert": "#6CFF6A"}


def _principled(m):
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        if n.type != "OUTPUT_MATERIAL":
            nt.nodes.remove(n)
    out = nt.nodes["Material Output"]
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(b.outputs[0], out.inputs[0])
    b.inputs["Roughness"].default_value = 0.8
    return nt, b


def kmat(kind):
    name = kind
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    m = bpy.data.materials.new(name)
    nt, b = _principled(m)
    if kind in NEON:
        rgba = hex_rgba(NEON[kind])
        b.inputs["Base Color"].default_value = rgba
        b.inputs["Emission Color"].default_value = rgba
        b.inputs["Emission Strength"].default_value = 6.0
        m.diffuse_color = rgba
        return m
    c1, c2, pat = KINDS[kind]
    a, bb = hex_rgba(c1), hex_rgba(c2)
    m.diffuse_color = a
    b.inputs["Base Color"].default_value = a
    coord = nt.nodes.new("ShaderNodeTexCoord")
    geo = nt.nodes.new("ShaderNodeNewGeometry")

    def projected(tex_factory):
        """Projection triplanaire simple : une texture par axe, choisie selon la normale (évite l'étirement sur les faces latérales)."""
        outs = []
        for ax in range(3):
            sw = nt.nodes.new("ShaderNodeCombineXYZ")
            sx = nt.nodes.new("ShaderNodeSeparateXYZ")
            nt.links.new(coord.outputs["Object"], sx.inputs[0])
            pick = [(0, 1), (1, 2), (0, 2)][ax]   # plan XY, YZ ou XZ
            nt.links.new(sx.outputs[pick[0]], sw.inputs[0])
            nt.links.new(sx.outputs[pick[1]], sw.inputs[1])
            outs.append(tex_factory(sw.outputs[0]))
        nsep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(geo.outputs["Normal"], nsep.inputs[0])
        absn = []
        for i in range(3):
            ab = nt.nodes.new("ShaderNodeMath")
            ab.operation = "ABSOLUTE"
            nt.links.new(nsep.outputs[i], ab.inputs[0])
            absn.append(ab)
        # axe dominant : z -> plan XY (idx0), x -> plan YZ (idx1), y -> plan XZ (idx2)
        def gt(p, q):
            g = nt.nodes.new("ShaderNodeMath")
            g.operation = "GREATER_THAN"
            nt.links.new(p.outputs[0], g.inputs[0])
            nt.links.new(q.outputs[0], g.inputs[1])
            return g
        zdom = nt.nodes.new("ShaderNodeMath")
        zdom.operation = "MULTIPLY"
        nt.links.new(gt(absn[2], absn[0]).outputs[0], zdom.inputs[0])
        nt.links.new(gt(absn[2], absn[1]).outputs[0], zdom.inputs[1])
        mix_a = nt.nodes.new("ShaderNodeMixRGB")     # y/x selon la dominante
        nt.links.new(gt(absn[0], absn[1]).outputs[0], mix_a.inputs[0])
        nt.links.new(outs[2], mix_a.inputs[1])
        nt.links.new(outs[1], mix_a.inputs[2])
        mix_b = nt.nodes.new("ShaderNodeMixRGB")
        nt.links.new(zdom.outputs[0], mix_b.inputs[0])
        nt.links.new(mix_a.outputs[0], mix_b.inputs[1])
        nt.links.new(outs[0], mix_b.inputs[2])
        return mix_b.outputs[0]

    def brick_tex(vec):
        t = nt.nodes.new("ShaderNodeTexBrick")
        t.inputs["Color1"].default_value, t.inputs["Color2"].default_value = a, bb
        t.inputs["Mortar"].default_value = (*[x * 0.45 for x in a[:3]], 1)
        t.inputs["Scale"].default_value = 1.0
        t.inputs["Mortar Size"].default_value = 0.03
        t.inputs["Brick Width"].default_value = 0.6
        t.inputs["Row Height"].default_value = 0.25
        nt.links.new(vec, t.inputs["Vector"])
        return t.outputs["Color"]

    def window_tex(vec):
        t = nt.nodes.new("ShaderNodeTexBrick")
        t.inputs["Color1"].default_value = hex_rgba(c2)
        t.inputs["Color2"].default_value = hex_rgba("#7BB7FF")
        t.inputs["Mortar"].default_value = a
        t.inputs["Scale"].default_value = 1.0
        t.inputs["Mortar Size"].default_value = 0.2
        t.inputs["Mortar Smooth"].default_value = 0.02
        t.inputs["Brick Width"].default_value = 3.0
        t.inputs["Row Height"].default_value = 3.6
        t.offset = 0.0
        nt.links.new(vec, t.inputs["Vector"])
        return t.outputs["Color"]

    def noise_tex(vec):
        t = nt.nodes.new("ShaderNodeTexNoise")
        t.inputs["Scale"].default_value = 1.5
        t.inputs["Detail"].default_value = 6.0
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].color, ramp.color_ramp.elements[1].color = a, bb
        nt.links.new(vec, t.inputs["Vector"])
        nt.links.new(t.outputs["Fac"], ramp.inputs["Fac"])
        return ramp.outputs["Color"]

    def plank_tex(vec):
        sx = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(vec, sx.inputs[0])
        t = nt.nodes.new("ShaderNodeTexWave")
        t.inputs["Scale"].default_value = 5.0
        t.inputs["Distortion"].default_value = 3.0
        nt.links.new(vec, t.inputs["Vector"])
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].color, ramp.color_ramp.elements[1].color = bb, a
        nt.links.new(t.outputs["Color"], ramp.inputs["Fac"])
        return ramp.outputs["Color"]

    def danger_tex(vec):
        t = nt.nodes.new("ShaderNodeTexWave")
        t.wave_type = "BANDS"
        t.bands_direction = "DIAGONAL"
        t.inputs["Scale"].default_value = 1.4
        t.inputs["Distortion"].default_value = 0.0
        nt.links.new(vec, t.inputs["Vector"])
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.interpolation = "CONSTANT"
        ramp.color_ramp.elements[0].color = bb
        e = ramp.color_ramp.elements.new(0.5)
        e.color = a
        nt.links.new(t.outputs["Color"], ramp.inputs["Fac"])
        return ramp.outputs["Color"]

    def tole_tex(vec):
        t = nt.nodes.new("ShaderNodeTexWave")
        t.inputs["Scale"].default_value = 12.0
        nt.links.new(vec, t.inputs["Vector"])
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].color, ramp.color_ramp.elements[1].color = bb, a
        nt.links.new(t.outputs["Color"], ramp.inputs["Fac"])
        return ramp.outputs["Color"]

    fac = {"brique": brick_tex, "bruit": noise_tex, "planches": plank_tex, "danger": danger_tex, "fenetres": window_tex, "tole": tole_tex}.get(pat)
    if fac:
        col = projected(fac)
        nt.links.new(col, b.inputs["Base Color"])
        if pat == "fenetres":
            nt.links.new(col, b.inputs["Emission Color"])
            b.inputs["Emission Strength"].default_value = 0.5
    return m


# ----------------------------------------------------------------- briques de construction
def block(size, loc, kind, name, bevel=0.04, rot=(0, 0, 0), parent=None):
    return lib.box(size, loc, kmat(kind), name, bevel, parent, rot)


def tube(r, h, loc, kind, name, verts=20, rot=(0, 0, 0), bevel=0.0):
    return lib.cyl(r, h, loc, kmat(kind), name, verts=verts, rot=rot, bevel=bevel)


def dome(r, loc, kind, name, scale=(1, 1, 1)):
    return lib.sphere(r, loc, kmat(kind), name, sub=2, scale=scale)


def arch_cap(length, loc, name, along="x", kind="capuchon"):
    """Capuchon de parapet en demi-cylindre sombre (la corniche arrondie typique des toits)."""
    rot = (0, math.pi / 2, 0) if along == "x" else (math.pi / 2, 0, 0)
    o = lib.cyl(0.3, length, loc, kmat(kind), name, verts=12, rot=rot)
    o.scale = (1, 1, 1)
    return o
