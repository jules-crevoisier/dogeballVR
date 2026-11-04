"""Quatre personnages façon Knockout City (originaux) construits avec le pipeline du jeu (tools/blender/hero.py, rig.py).

Riko et Nova reprennent les tenues de hero.hero(), poussées vers la DA : bomber à boudins, numéro dans le dos, mains et
pieds plus gros. Bo (costaud) et Kai (fin) sont nouveaux. Les mesures (taille, tête, épaules, mains, pieds) viennent du
plan Blockout validé et posé par le plugin Scenario : `cfg_from_plan()` les relit.
"""
import math
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

import hero as Hh
import props_kit as K
import rig as rigmod
from hero import J, HEAD_R, _c, _paint_flat, body_mesh, build_head, face, head_shape, mitt, sneaker, _join
from lib import box, cyl, hex_rgba, mat, sphere, torus
from organic import paint, skin_body

R_RIKO = {"bassin": (0.165, 0.13), "taille": (0.15, 0.125), "buste": (0.235, 0.165), "col": (0.145, 0.11), "basecou": (0.068, 0.066),
          "cou": (0.052, 0.052), "epaule": (0.098, 0.098), "coude": (0.08, 0.08), "avantbras": (0.078, 0.078), "poignet": (0.056, 0.056),
          "hanche": (0.105, 0.105), "cuisse": (0.115, 0.12), "genou": (0.082, 0.088), "mollet": (0.09, 0.095), "cheville": (0.05, 0.05)}
R_NOVA = {"bassin": (0.18, 0.14), "taille": (0.2, 0.16), "buste": (0.255, 0.185), "col": (0.15, 0.115), "basecou": (0.07, 0.068),
          "cou": (0.052, 0.052), "epaule": (0.115, 0.115), "coude": (0.1, 0.1), "avantbras": (0.095, 0.095), "poignet": (0.075, 0.075),
          "hanche": (0.11, 0.11), "cuisse": (0.12, 0.125), "genou": (0.088, 0.092), "mollet": (0.088, 0.092), "cheville": (0.05, 0.05)}
HAUT = ("buste", "col", "epaule", "coude", "avantbras", "poignet", "taille")


def radii(base, w_haut, w_bas=1.0):
    return {k: (v[0] * (w_haut if k in HAUT else w_bas), v[1] * (w_haut if k in HAUT else w_bas)) for k, v in base.items()}


CFG = {
    "Riko": dict(team="orange", r=R_RIKO, jacket="#FF6A1A", jacket_d="#D9500E", rib="#23263A", pants="#2A2840", stripe="#F2F4F8",
                 skin="#C98B62", glove="#F2F4F8", glove_acc="#FF6A1A", sole="#E9E4D8", iris="#4A2A16", brow="#22160F", hair="#22160F",
                 numero="07", boudins=True),
    "Bo": dict(team="orange", r=R_RIKO, jacket="#E8662C", jacket_d="#B9481C", rib="#1E2030", pants="#23263A",
               stripe="#F1E3C2", skin="#7A4A30", glove="#F1E3C2", glove_acc="#E8662C", sole="#F1E3C2", iris="#2A1A10", brow="#1A120C",
               hair="#1A120C", numero="23", bandes=True, jambes=1.2, cargo=True),
    "Nova": dict(team="bleu", r=R_NOVA, jacket="#2F7DE1", jacket_d="#2563B8", rib="#1D2A4A", pants="#1E3A78", stripe="#8FE3FF",
                 skin="#F2C6A0", glove="#8FE3FF", glove_acc="#2A2F45", sole="#2A2F45", iris="#2E6FA8", brow="#3A4A6E", hair="#C9CFE0",
                 numero="11", hoodie=True),
    "Kai": dict(team="bleu", r=R_RIKO, jacket="#2F7DE1", jacket_d="#1F5DB0", rib="#18203A", pants="#18203A",
                stripe="#8FE3FF", skin="#E8B48C", glove="#F2F4F8", glove_acc="#2F7DE1", sole="#F2F4F8", iris="#1F3550", brow="#2A1E16",
                hair="#2A1E16", numero="4", chevron=True, jambes=0.86),
}


def cfg_from_plan(elements, name):
    """Mesures d'un perso relues dans le plan validé : hauteur, tête, épaules, mains et pieds."""
    g = {e["name"][len(name) + 1:]: e for e in elements if e["name"].startswith(name + " ")}
    top = max(e["position"][2] + e["size"][2] / 2 for n, e in g.items() if n in ("Tête",))
    return dict(x=g["Tête"]["position"][0], H=top, head=g["Tête"]["size"][2], shoulders=g["Torse"]["size"][0],
                hand=g["Main G"]["size"][2] / 0.13, foot=g["Basket G"]["size"][1] / 0.236)


def _scale_about(o, c, f):
    """Échelle (uniforme ou par axe) autour du point c, en espace monde."""
    f = Vector(f) if not isinstance(f, (int, float)) else Vector((f, f, f))
    c = Vector(c)
    d = o.location - c
    o.location = c + Vector((d.x * f.x, d.y * f.y, d.z * f.z))
    o.scale = Vector((o.scale.x * f.x, o.scale.y * f.y, o.scale.z * f.z))


def gant(s, glove_hex, acc_hex, hf):
    """Gant XL d'un seul volume : paume ovoïde dans l'axe de l'avant-bras, pouce, manchette au poignet."""
    side = "G" if s < 0 else "D"
    wr = Vector((abs(J["poignet"][0]) * s, J["poignet"][1], J["poignet"][2]))
    el = Vector((abs(J["coude"][0]) * s, J["coude"][1], J["coude"][2]))
    d = (wr - el).normalized()
    q = d.to_track_quat("Z", "Y")
    parts = []
    palm = sphere(1.0, (0, 0, 0), None, "g", sub=3, scale=(0.05 * hf, 0.04 * hf, 0.066 * hf))
    palm.rotation_mode = "QUATERNION"
    palm.rotation_quaternion = q
    palm.location = wr + d * 0.062 * hf
    parts.append(palm)
    thumb = sphere(1.0, (0, 0, 0), None, "g", sub=2, scale=(0.017 * hf, 0.017 * hf, 0.03 * hf))
    thumb.rotation_mode = "QUATERNION"
    thumb.rotation_quaternion = q
    fwd = Vector((0, 1, 0))
    thumb.location = wr + d * 0.05 * hf + fwd * 0.035 * hf + Vector((-s, 0, 0)) * 0.012 * hf
    parts.append(thumb)
    for i in (-1, 0, 1):   # trois doigts courts au bout de la paume
        fi = sphere(1.0, (0, 0, 0), None, "g", sub=2, scale=(0.016 * hf, 0.017 * hf, 0.03 * hf))
        fi.rotation_mode = "QUATERNION"
        fi.rotation_quaternion = q
        fi.location = wr + d * 0.115 * hf + (q @ Vector((0, 1, 0))) * i * 0.022 * hf
        parts.append(fi)
    cuff = cyl(0.036 * hf, 0.04 * hf, (0, 0, 0), None, "g", verts=20)
    cuff.rotation_mode = "QUATERNION"
    cuff.rotation_quaternion = q
    cuff.location = wr + d * 0.012 * hf
    parts.append(cuff)
    glove, acc = _c(glove_hex, 0.55), _c(acc_hex, 0.5)
    for o in parts:
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        _paint_flat(o, acc if o is cuff else glove, ao=10, dist=0.04, strength=0.4)
    return _join(parts, f"Gant.{side}")


def basket(s, upper_hex, accent_hex, sole_hex):
    """Basket montante KC : grosse semelle arrondie au ras de la tige, bout rond, col de cheville. Origine sous la cheville."""
    x, y0 = abs(J["cheville"][0]) * s, 0.06
    sole = box((0.13, 0.3, 0.06), (0, y0, 0.03), None, "b", 0.028)
    upper = skin_body([(0, y0 - 0.11, 0.1, 0.066, 0.056), (0, y0 - 0.01, 0.1, 0.068, 0.064), (0, y0 + 0.09, 0.088, 0.064, 0.05),
                       (0, -0.03, 0.17, 0.062, 0.062)], [(0, 1), (1, 2), (0, 3)], subdiv=2, name="b")
    toe = sphere(0.066, (0, y0 + 0.11, 0.08), None, "b", sub=2, scale=(1.0, 0.95, 0.66))
    collar = torus(0.062, 0.022, (0, -0.035, 0.215), None, "b", seg=16, rseg=6)
    tongue = box((0.062, 0.04, 0.09), (0, 0.025, 0.21), None, "b", 0.01, rot=(math.radians(-20), 0, 0))
    laces = [box((0.09, 0.016, 0.012), (0, 0.04 + i * 0.04, 0.175 - i * 0.025), None, "b", 0.0, rot=(math.radians(-25), 0, 0)) for i in range(3)]
    up, ac, so = _c(upper_hex, 0.5), _c(accent_hex, 0.45), _c(sole_hex, 0.6)
    parts = [(sole, so), (upper, up), (toe, up), (collar, ac), (tongue, ac)] + [(l, _c("#2A2840", 0.6)) for l in laces]
    for o, co in parts:
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        _paint_flat(o, co, ao=10, dist=0.05, strength=0.45)
    b = _join([o for o, _ in parts], f"Basket.{'G' if s < 0 else 'D'}")
    b.location = (x, J["cheville"][1], 0.0)
    return b


def perso(name, m):
    """Construit le perso (pièces non riggées) dans la DA du jeu, aux mesures m du plan."""
    c = CFG[name]
    orange = c["team"] == "orange"
    glow = mat("neon_" + c["team"], "#FFB347" if orange else "#8FE3FF", 2.5)
    k = m["H"] / 1.75
    wf = m["shoulders"] / ((0.76 if c["r"] is R_NOVA else 0.74) * k)   # carrure relue dans le plan, rapportée au modèle de base
    r = radii(c["r"], wf, 1 + (wf - 1) * 0.6)
    # silhouette KC : épaules larges, bras secs, avant-bras et mollets épais, la masse va aux mains et aux pieds
    for kk, f in (("buste", 1.38 if c.get("boudins") else 1.3), ("col", 1.1), ("epaule", 1.0), ("coude", 0.88), ("avantbras", 1.15),
                  ("poignet", 1.05), ("mollet", 1.25), ("genou", 1.08)):
        r[kk] = (r[kk][0] * f, r[kk][1] * f)
    for kk in ("bassin", "hanche", "cuisse", "genou", "mollet"):   # cargo large chez Bo, jogger serré chez Kai
        r[kk] = (r[kk][0] * c.get("jambes", 1.0), r[kk][1] * c.get("jambes", 1.0))
    p = []
    body = body_mesh(r)
    rough_j = 0.62 if (orange and not c.get("bandes")) else 0.85
    jc, jd, rb, pc, sc, sk = (_c(c["jacket"], rough_j), _c(c["jacket_d"], rough_j), _c(c["rib"], 0.9), _c(c["pants"], 0.85),
                              _c(c["stripe"], 0.7), _c(c["skin"], 0.55))

    def col(co, n):
        ax = abs(co.x)
        if co.z > 1.47:
            return sk
        if co.z < 0.99 and ax < 0.3:   # pantalon, bande sur le côté extérieur
            side = (co.x < 0 and n.x < -0.8) or (co.x > 0 and n.x > 0.8)
            return sc if side and co.z > 0.18 else pc
        if c.get("bandes") and ax > 0.24:   # veste de survêt : trois bandes claires sur la manche
            if abs(n.y) < 0.55 and n.z > -0.2 and (n.x * (1 if co.x > 0 else -1)) > 0.2:
                return sc
        if c.get("chevron") and ax < 0.2 and n.y > 0.6 and 1.15 < co.z < 1.36:   # chevron clair sur la poitrine
            v = abs(co.x) * 1.3 + (1.36 - co.z)
            if 0.12 < v < 0.17:
                return sc
        if ax > 0.22:
            return jd if n.z < -0.3 else jc
        return jd if (abs(n.x) > 0.85 and co.z < 1.2) else jc
    paint(body, col, ao_rays=30, ao_dist=0.22, ao_strength=0.6)
    p.append(body)
    hem = torus(r["taille"][0] * 0.95, 0.016, (0, 0, 1.0), None, "W_Ourlet", seg=28, rseg=8)
    hem.scale = (1.0, 0.8, 1.6)
    _paint_flat(hem, jd, ao=8, dist=0.05)
    p.append(hem)
    collar = torus(0.072, 0.02, (0, -0.01, 1.465), None, "W_Col", seg=22, rseg=8)
    collar.scale = (1.0, 1.0, 1.8)
    _paint_flat(collar, rb, ao=8, dist=0.05)
    p.append(collar)
    for s in (-1, 1):
        side = "G" if s < 0 else "D"
        ank = torus(0.056, 0.02, (abs(J["cheville"][0]) * s, J["cheville"][1], 0.22), None, f"Revers.{side}", seg=18, rseg=6)
        _paint_flat(ank, rb)
        p.append(ank)
        p.append(gant(s, c["glove"], c["glove_acc"], m["hand"]))
        shoe = basket(s, "#F2F4F8", c["jacket"], c["sole"])
        f = m["foot"] / 1.4   # baskets KC : hautes et rondes, pas des skis
        _scale_about(shoe, (abs(J["cheville"][0]) * s, J["cheville"][1], 0.0), (1 + (f - 1) * 0.6, 1 + (f - 1) * 0.45, 1 + (f - 1) * 0.9))
        p.append(shoe)
        band = torus(r["epaule"][0] * 0.97, 0.022, (0.314 * s, 0.0, 1.268), glow, f"Neon.{side}", seg=18, rseg=6,
                     rot=(0, math.radians(s * 44), 0))
        p.append(band)
        if name == "Bo":   # épaulettes rembourrées
            ep = sphere(0.1, (abs(J["epaule"][0]) * s + s * 0.02, J["epaule"][1], J["epaule"][2] + 0.01), None, f"W_Epaulette{s}", sub=2,
                        scale=(1.05, 0.95, 0.55))
            _paint_flat(ep, _c("#1E2030", 0.5), ao=6, dist=0.05)
            p.append(ep)
        if c.get("cargo"):   # poches cargo sur le côté des cuisses
            hx, gx = abs(J["hanche"][0]), abs(J["genou"][0])
            pk = box((0.05, 0.13, 0.15), (((hx + gx) / 2 + r["cuisse"][0] * 0.8) * s, (J["hanche"][1] + J["genou"][1]) / 2,
                                          J["genou"][2] + (J["hanche"][2] - J["genou"][2]) * 0.45), None, f"W_Cargo{s}", 0.02)
            _paint_flat(pk, _c("#1E2030", 0.8), ao=6, dist=0.04)
            p.append(pk)
        if name == "Bo":   # genouillères
            kp = sphere(0.075, (abs(J["genou"][0]) * s, J["genou"][1] + 0.065, J["genou"][2]), None, f"W_Genou{s}", sub=2,
                        scale=(1.0, 0.55, 1.15))
            _paint_flat(kp, _c("#1E2030", 0.5), ao=6, dist=0.04)
            p.append(kp)
    zip_ = box((0.03, 0.015, 0.36), (0, r["buste"][1] + 0.0, 1.21), None, "W_Zip", 0.004)
    _paint_flat(zip_, _c("#D8DCE6", 0.3), ao=0)
    p.append(zip_)
    # numéro dans le dos (gros, lisible de loin)
    num = K.text_mesh(c["numero"], size=0.2, extrude=0.008, loc=(0, -r["buste"][1] - 0.02, 1.2), rot=(math.pi / 2, 0, 0), res=6,
                      name="W_Numero")
    _paint_flat(num, _c(c["stripe"], 0.6), ao=0)
    p.append(num)
    if name == "Bo":   # grosse chaîne dorée
        links = []
        for i in range(9):
            a = math.radians(-70 + i * 17.5)
            pos = (math.sin(a) * 0.15, r["buste"][1] * 0.95 + math.cos(a) * 0.02, 1.36 - math.cos(a) * 0.12)
            links.append(torus(0.022, 0.008, pos, None, "W_Maillon", seg=10, rseg=4, rot=(0, math.pi / 2 * (i % 2), a)))
        for o in links:
            bpy.context.view_layer.objects.active = o
            o.select_set(True)
            bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        ch = _join(links, "W_Chaine")
        _paint_flat(ch, _c("#E8B83A", 0.3), ao=0)
        p.append(ch)
    if name == "Kai":   # écharpe épaisse aux couleurs d'équipe, un pan qui tombe devant
        sc_ = torus(0.085, 0.035, (0, 0.0, 1.46), None, "W_Echarpe", seg=24, rseg=8)
        sc_.scale = (1.15, 1.1, 1.0)
        pan = box((0.09, 0.04, 0.24), (0.06, r["buste"][1] + 0.02, 1.33), None, "W_Pan", 0.02, rot=(math.radians(-8), 0, math.radians(6)))
        for o in (sc_, pan):
            _paint_flat(o, _c("#8FE3FF", 0.9), ao=8, dist=0.05)
            p.append(o)
    if c.get("hoodie"):
        hood = skin_body([(-0.19, -0.14, 1.46, 0.16, 0.11), (0, -0.2, 1.52, 0.17, 0.11), (0.19, -0.14, 1.46, 0.16, 0.11),
                          (0, -0.2, 1.34, 0.13, 0.09)], [(0, 1), (1, 2), (1, 3)], subdiv=2, name="W_Capuche")
        _paint_flat(hood, jd, ao=12, dist=0.08, strength=0.5)
        p.append(hood)
        pocket = box((0.24, 0.04, 0.11), (0, 0.155, 1.06), None, "W_Poche", 0.02)
        _paint_flat(pocket, jd, ao=6, dist=0.04)
        p.append(pocket)
    # tête
    hair = _c(c["hair"], 0.5)

    def scalp(d):
        if name == "Riko":
            if d.z > 0.045 + max(0.0, d.y - 0.06) * 0.9 or d.y < -0.06 and d.z > -0.06:
                return hair
        elif name == "Nova":
            if d.z > 0.05 and d.y < 0.08 or d.y < -0.04:
                return hair
        elif d.z > 0.03 and d.y < 0.09 or d.y < -0.05 and d.z > -0.05:   # cheveux courts sous la casquette ou le bonnet
            return hair
        return None
    p.append(build_head(c["skin"], scalp))
    fparts = face(c["iris"], c["skin"], c["brow"])
    for o in fparts:
        for s in (-1, 1):
            if o.name.endswith(str(s)) and o.name.startswith(("H_Oeil", "H_Iris", "H_Pupille", "H_Reflet", "H_Paupiere")):
                _scale_about(o, Hh.head_point(s * 0.43, 0.06, -0.02), 1.3)
        if o.name.startswith("H_Sourcil"):
            _scale_about(o, o.location, (1.35, 1.0, 2.2))
            o.location.z += 0.008
        if o.name == "H_Bouche":
            bpy.context.view_layer.update()
            cen = sum((o.matrix_world @ v.co for v in o.data.vertices), Vector()) / len(o.data.vertices)
            _scale_about(o, cen, 1.6)
    p += fparts
    hx, hy, hz = J["tete"]
    team_c = c["jacket"]
    if name == "Riko":
        q = skin_body([(hx, hy - 0.05, hz + 0.06, 0.07, 0.07), (hx, hy + 0.02, hz + 0.115, 0.075, 0.06), (hx, hy + 0.09, hz + 0.135, 0.06, 0.05),
                       (hx, hy + 0.15, hz + 0.13, 0.045, 0.04)], [(0, 1), (1, 2), (2, 3)], subdiv=2, name="H_Cheveux")
        _paint_flat(q, hair, ao=10, dist=0.06)
        p.append(q)
        bd = torus(0.094, 0.011, (hx, hy + 0.004, hz + 0.078), None, "H_Bandeau", seg=30, rseg=6, rot=(math.radians(-7), 0, 0))
        bd.scale = (1.0, 1.12, 2.2)
        _paint_flat(bd, _c(team_c, 0.6), ao=0)
        p.append(bd)
    elif name == "Nova":
        rnd = random.Random(7)
        curls = []
        for i in range(40):
            if len(curls) >= 12:
                break
            th, ph = rnd.uniform(0, 2 * math.pi), rnd.uniform(0.0, 1.15)
            d = Vector((math.sin(ph) * math.cos(th), math.sin(ph) * math.sin(th) - 0.15, math.cos(ph))).normalized()
            if d.y > 0.55 and d.z < 0.6:
                continue
            cc = Vector(J["tete"]) + head_shape(d) * 1.02
            curls.append(sphere(0.06 + rnd.uniform(0, 0.015), tuple(cc), None, "H_c", sub=2))
        for o in curls:
            bpy.context.view_layer.objects.active = o
            o.select_set(True)
            bpy.ops.object.transform_apply(location=True)
        q = _join(curls, "H_Cheveux")
        _paint_flat(q, hair, ao=12, dist=0.08)
        p.append(q)
        for s in (-1, 1):
            cup = cyl(0.058, 0.05, (s * (HEAD_R[0] + 0.03), hy - 0.005, hz - 0.005), None, f"H_Coussin{s}", verts=24, bevel=0.012,
                      rot=(0, math.pi / 2, 0))
            _paint_flat(cup, _c("#2A2F45", 0.3), ao=6, dist=0.03)
            p.append(cup)
            p.append(cyl(0.04, 0.012, (s * (HEAD_R[0] + 0.057), hy - 0.005, hz - 0.005), glow, f"H_Led{s}", verts=24, rot=(0, math.pi / 2, 0)))
        arc = torus(0.15, 0.018, (hx, hy - 0.005, hz - 0.005), None, "H_Arceau", seg=36, rseg=6, rot=(math.pi / 2, 0, 0))
        arc.scale = (1.0, 1.1, 1.0)
        bm = bmesh.new()
        bm.from_mesh(arc.data)
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.y < 0.0], context="VERTS")
        bm.to_mesh(arc.data)
        bm.free()
        _paint_flat(arc, _c(team_c, 0.3), ao=0)
        p.append(arc)
    elif name == "Bo":   # casquette : calotte, bandeau sombre, visière devant, bouton
        capc = sphere(1.0, (hx, hy - 0.005, hz + 0.035), None, "H_Calotte", sub=3, scale=(HEAD_R[0] * 1.08, HEAD_R[1] * 1.06, HEAD_R[2] * 0.82))
        bm = bmesh.new()
        bm.from_mesh(capc.data)
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < 0.05], context="VERTS")
        bm.to_mesh(capc.data)
        bm.free()
        _paint_flat(capc, _c(team_c, 0.6), ao=8, dist=0.05)
        p.append(capc)
        vis = box((0.19, 0.13, 0.02), (hx, hy + HEAD_R[1] + 0.035, hz + 0.045), None, "H_Visiere", 0.008, rot=(math.radians(-10), 0, 0))
        _paint_flat(vis, _c("#1E2030", 0.6), ao=0)
        p.append(vis)
        bdc = torus(1.0, 0.016, (hx, hy - 0.005, hz + 0.05), None, "H_BandeauCasquette", seg=32, rseg=6)
        bdc.scale = (HEAD_R[0] * 1.07, HEAD_R[1] * 1.05, 1.0)
        _paint_flat(bdc, _c("#1E2030", 0.6), ao=0)
        p.append(bdc)
        bt = sphere(0.016, (hx, hy - 0.005, hz + 0.035 + HEAD_R[2] * 0.82), None, "H_Bouton", sub=1)
        _paint_flat(bt, _c("#1E2030", 0.6), ao=0)
        p.append(bt)
    else:   # Kai : bonnet à revers et pompon, lunettes de ski sur le front
        bon = sphere(1.0, (hx, hy - 0.01, hz + 0.03), None, "H_Bonnet", sub=3, scale=(HEAD_R[0] * 1.1, HEAD_R[1] * 1.08, HEAD_R[2] * 0.95))
        bm = bmesh.new()
        bm.from_mesh(bon.data)
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < 0.12], context="VERTS")
        bm.to_mesh(bon.data)
        bm.free()
        _paint_flat(bon, _c("#18203A", 0.85), ao=8, dist=0.05)
        p.append(bon)
        rev = torus(1.0, 0.03, (hx, hy - 0.01, hz + 0.055), None, "H_Revers", seg=32, rseg=8)
        rev.scale = (HEAD_R[0] * 1.06, HEAD_R[1] * 1.04, 1.0)
        _paint_flat(rev, _c(team_c, 0.85), ao=0)
        p.append(rev)
        pom = sphere(0.05, (hx, hy - 0.02, hz + 0.03 + HEAD_R[2] * 0.95 + 0.03), None, "H_Pompon", sub=2)
        _paint_flat(pom, _c("#F2F4F8", 0.9), ao=0)
        p.append(pom)
        strap = torus(1.0, 0.012, (hx, hy, hz + 0.1), None, "H_SangleLunettes", seg=32, rseg=4)
        strap.scale = (HEAD_R[0] * 1.12, HEAD_R[1] * 1.12, 1.0)
        _paint_flat(strap, _c("#18203A", 0.6), ao=0)
        p.append(strap)
        lens = sphere(1.0, (hx, hy + HEAD_R[1] * 0.98, hz + 0.1), None, "H_Lunettes", sub=3, scale=(0.1, 0.03, 0.04))
        _paint_flat(lens, _c("#2B6CB0", 0.15), ao=0)
        p.append(lens)
    # tête : échelle relue dans le plan (corrigée de la taille globale)
    hf = (m["head"] / 0.27) / k
    for o in p:
        if o.name.startswith("H_"):
            _scale_about(o, J["tete"], hf)
    return p


def _pose_static(rig, action_name, frame):
    """Fige la pose d'une action à une image donnée (évaluation des courbes, sans dépendre de l'image de la scène)."""
    act = None
    for t in rig.animation_data.nla_tracks:
        if t.name == action_name:
            act = t.strips[0].action
        t.mute = True
    rig.animation_data.action = None
    curves = []
    if hasattr(act, "fcurves") and len(getattr(act, "fcurves", [])):
        curves = list(act.fcurves)
    else:
        for layer in act.layers:
            for st in layer.strips:
                for bag in st.channelbags:
                    curves += list(bag.fcurves)
    for fc in curves:
        prop = rig.path_resolve(fc.data_path)
        prop[fc.array_index] = fc.evaluate(frame)


def _z(z):
    """Jambes −15 %, cou −3 cm : proportions KC (tête et buste gros, jambes courtes)."""
    if z <= 0.95: return z * 0.85
    if z <= 1.40: return z - 0.1425
    if z <= 1.50: return z - 0.1425 - 0.03 * (z - 1.40) / 0.10
    return z - 0.1725


def _reproportion(rig, body):
    for v in body.data.vertices:
        v.co.z = _z(v.co.z)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    for b in rig.data.edit_bones:
        b.head.z, b.tail.z = _z(b.head.z), _z(b.tail.z)
    bpy.ops.object.mode_set(mode="OBJECT")


def build(name, m, pose=None):
    objs = perso(name, m)
    rig, body = rigmod.rig_character(objs, rigmod.HERO_BONES, arm_rest=-14)
    rig.name, body.name = f"Rig_{name}", f"Perso_{name}"
    _reproportion(rig, body)
    k = m["H"] / _z(1.75)
    rig.scale = (k, k, k)
    rig.location = (m["x"], 0.0, 0.0)
    if pose:
        _pose_static(rig, *pose)
    return rig, body
