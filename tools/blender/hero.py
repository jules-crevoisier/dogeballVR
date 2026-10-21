"""Personnages jouables aux proportions de la fiche modeleurs : 1,75 m, 6,5 têtes, épaules larges, taille fine,
mains ×1,3 et pieds ×1,4, vêtements streetwear volumineux. Tout est peint dans les couleurs de sommet (couleur + occlusion),
la rugosité PBR de chaque matière est rangée dans l'alpha. Pièces « W_ » : vêtements qui suivent la peau (poids transférés).

Repère Blender : Z haut, face vers +Y, côté gauche du perso en X négatif.
"""
import math

import bmesh
import bpy
from mathutils import Vector

from lib import box, cyl, hex_rgba, mat, sphere, torus
from organic import paint, skin_body

# Articulations (x gauche négatif). Les os du squelette (rig.HERO_BONES) sont posés sur ces points.
J = {
    "bassin": (0, 0, 0.95), "taille": (0, 0, 1.07), "buste": (0, -0.005, 1.25), "col": (0, -0.01, 1.37), "basecou": (0, -0.01, 1.45), "cou": (0, 0.0, 1.53),
    "tete": (0, 0.005, 1.615),
    "epaule": (-0.2, -0.01, 1.385), "coude": (-0.39, 0.0, 1.19), "poignet": (-0.54, 0.03, 1.02), "main": (-0.59, 0.05, 0.9),
    "hanche": (-0.095, 0, 0.9), "genou": (-0.108, 0.018, 0.5), "mollet": (-0.112, -0.008, 0.3), "cheville": (-0.115, -0.005, 0.13),
}
HEAD_R = (0.11, 0.122, 0.138)   # demi-axes de la tête : 0,27 m de haut


def _c(h, rough):
    c = hex_rgba(h)
    return (c[0], c[1], c[2], 1.0, rough)


def _paint_flat(o, color, ao=8, dist=0.05, strength=0.35):
    return paint(o, lambda co, n: color, ao_rays=ao, ao_dist=dist, ao_strength=strength)


# ------------------------------------------------------------------ tête
def head_shape(d):
    """Point de la surface du crâne dans la direction unitaire d (tête centrée à l'origine)."""
    x, y, z = d
    rx, ry, rz = HEAD_R
    sx = 1.0 - 0.18 * max(0.0, -z) ** 1.4               # mâchoire plus étroite
    sx *= 1.0 + 0.06 * math.exp(-((z + 0.05) ** 2 + (y - 0.55) ** 2) * 12)   # pommettes
    sy = 1.0 + (0.06 if y < 0 else 0.0) * min(1.0, -y * 2)  # arrière du crâne
    if y > 0.5:                                          # visage un peu aplati
        sy *= 1.0 - 0.06 * (y - 0.5)
    if z < -0.55 and y > 0.2:                            # menton en avant
        sy *= 1.0 + 0.12 * (y - 0.2) * (-z - 0.55) * 2.5
    brow = 0.035 * math.exp(-((z - 0.22) ** 2) * 60) * max(0.0, y - 0.6) * 2.5   # arcade sourcilière
    return Vector((x * rx * sx, y * ry * sy * (1 + brow), z * rz))


def head_point(x, z, out=0.0):
    """Point du visage vu de face (x, z normalisés -1..1) projeté sur la surface, éventuellement décollé de out mètres."""
    y = math.sqrt(max(0.0, 1 - x * x - z * z))
    p = head_shape((x, y, z))
    n = Vector((x / HEAD_R[0], y / HEAD_R[1], z / HEAD_R[2])).normalized()
    return tuple(Vector(J["tete"]) + p + n * out)


def build_head(skin_hex, scalp_fn):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=40, ring_count=26, radius=1.0)
    o = bpy.context.active_object
    for v in o.data.vertices:
        v.co = head_shape(v.co.normalized()) + Vector(J["tete"])
    o.name = "H_Tete"
    o.data.name = "H_Tete"
    for p in o.data.polygons:
        p.use_smooth = True
    skin = _c(skin_hex, 0.55)
    blush = hex_rgba("#E07A6A")

    def col(co, n):
        d = co - Vector(J["tete"])
        c = scalp_fn(d)
        if c:
            return c
        # joues rosées et dessous du menton plus sombre
        k = 0.0
        for s in (-1, 1):
            k = max(k, math.exp(-((d.x - s * 0.062) ** 2 + (d.z + 0.03) ** 2) * 900) * (d.y > 0.05))
        r, g, b = (skin[i] * (1 - 0.35 * k) + blush[i] * 0.35 * k for i in range(3))
        return (r, g, b, 1.0, 0.55)
    paint(o, col, ao_rays=12, ao_dist=0.08, ao_strength=0.35)
    return o


def face(team_iris, skin_hex, brow_hex):
    p = []
    ink = _c("#141522", 0.3)
    for s in (-1, 1):
        ex = s * 0.43
        c = head_point(ex, 0.06, -0.012)
        eye = sphere(0.028, c, None, f"H_Oeil{s}", sub=3, scale=(1.05, 0.55, 0.9))
        _paint_flat(eye, _c("#F7F7F2", 0.2), ao=0)
        p.append(eye)
        cv = Vector(c)
        iris = sphere(0.0175, tuple(cv + Vector((-s * 0.004, 0.0125, -0.002))), None, f"H_Iris{s}", sub=3, scale=(1.0, 0.45, 1.05))
        _paint_flat(iris, _c(team_iris, 0.2), ao=0)
        p.append(iris)
        pup = sphere(0.0085, tuple(cv + Vector((-s * 0.004, 0.0165, -0.002))), None, f"H_Pupille{s}", sub=2, scale=(1.0, 0.45, 1.05))
        _paint_flat(pup, ink, ao=0)
        p.append(pup)
        hl = sphere(0.004, tuple(cv + Vector((s * 0.002, 0.019, 0.006))), None, f"H_Reflet{s}", sub=1)
        _paint_flat(hl, _c("#FFFFFF", 0.1), ao=0)
        p.append(hl)
        # paupière supérieure (regard assuré) et sourcil épais en biais
        lid = sphere(0.031, tuple(cv + Vector((0, 0.0, 0.019))), None, f"H_Paupiere{s}", sub=2, scale=(1.08, 0.6, 0.45))
        _paint_flat(lid, _c(skin_hex, 0.55), ao=0)
        p.append(lid)
        brow = box((0.06, 0.016, 0.017), head_point(ex + s * 0.03, 0.33, 0.004), None, f"H_Sourcil{s}", 0.007,
                   rot=(math.radians(-12), 0, 0))
        brow.rotation_euler = (math.radians(-12), math.radians(s * 14), math.radians(-s * 9))
        _paint_flat(brow, _c(brow_hex, 0.7), ao=0)
        p.append(brow)
        ear = sphere(0.028, (s * (HEAD_R[0] * 0.96), J["tete"][1] - 0.01, J["tete"][2] - 0.005), None, f"H_Oreille{s}", sub=2,
                     scale=(0.42, 0.75, 1.15))
        _paint_flat(ear, _c(skin_hex, 0.55), ao=6, dist=0.03)
        p.append(ear)
    nose = sphere(0.018, head_point(0, -0.16, 0.0), None, "H_Nez", sub=2, scale=(1.1, 1.25, 0.9))
    _paint_flat(nose, _c(skin_hex, 0.55), ao=0)
    p.append(nose)
    # bouche : arc de sourire en coin (bas d'un tore, 120°)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.03, minor_radius=0.006, major_segments=36, minor_segments=6)
    mouth = bpy.context.active_object
    bm = bmesh.new()
    bm.from_mesh(mouth.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if math.atan2(v.co.y, v.co.x) > -math.radians(30) or
                               math.atan2(v.co.y, v.co.x) < -math.radians(150)], context="VERTS")
    bm.to_mesh(mouth.data)
    bm.free()
    for v in mouth.data.vertices:   # plan du tore -> face avant, posé sur le visage
        x, y, z = v.co
        v.co = Vector(head_point(0.12, -0.42, 0.0)) + Vector((x, 0, y + 0.012)) + Vector((0, z, 0))
    mouth.rotation_euler = (0, 0, 0)
    mouth.name = mouth.data.name = "H_Bouche"
    for f in mouth.data.polygons:
        f.use_smooth = True
    _paint_flat(mouth, _c("#5E2228", 0.4), ao=0)
    p.append(mouth)
    return p


# ------------------------------------------------------------------ mains et baskets
def mitt(s, glove_hex, accent_hex):
    """Gant mitaine ×1,3 posé au poignet, doigts vers le bas le long de l'avant-bras, paume vers le corps."""
    # paume épaisse, quatre doigts courts serrés et pouce écarté vers l'avant, en une peau lissée
    N = [(0, 0, -0.01, 0.05, 0.056), (0, 0.0, -0.07, 0.05, 0.06), (0, 0.004, -0.105, 0.042, 0.054)]
    E = []
    for i in range(4):
        N.append((s * 0.004, -0.03 + i * 0.02, -0.14 + abs(i - 1.5) * 0.008, 0.023, 0.023))
        E.append((2, len(N) - 1))
    N.append((s * 0.025, 0.065, -0.07, 0.024, 0.024))
    E += [(0, 1), (1, 2), (1, len(N) - 1)]
    g = skin_body(N, E, subdiv=1, name=f"Gant.{'G' if s < 0 else 'D'}")
    glove, acc = _c(glove_hex, 0.55), _c(accent_hex, 0.5)
    paint(g, lambda co, n: acc if co.z > -0.03 else glove, ao_rays=12, ao_dist=0.04, ao_strength=0.45)
    g.rotation_euler = (0, math.radians(-s * 40), 0)
    g.scale = (1.3, 1.3, 1.3)
    g.location = (abs(J["poignet"][0]) * s, J["poignet"][1], J["poignet"][2])
    return g


def sneaker(s, upper_hex, accent_hex, sole_hex, glow):
    """Basket montante à grosse semelle (pied ×1,4). Origine sous la cheville."""
    x = abs(J["cheville"][0]) * s
    y0 = 0.07   # le pied dépasse vers l'avant
    parts = []
    sole = box((0.135, 0.33, 0.05), (0, y0, 0.025), None, "b", 0.018)
    parts.append(sole)
    upper = skin_body([(0, y0 - 0.12, 0.09, 0.06, 0.05), (0, y0 - 0.02, 0.09, 0.06, 0.06), (0, y0 + 0.1, 0.075, 0.058, 0.045),
                       (0, -0.03, 0.16, 0.058, 0.058)], [(0, 1), (1, 2), (0, 3)], subdiv=2, name="b")
    parts.append(upper)
    toe = sphere(0.06, (0, y0 + 0.125, 0.07), None, "b", sub=2, scale=(1.05, 0.9, 0.62))
    parts.append(toe)
    collar = torus(0.058, 0.02, (0, -0.035, 0.205), None, "b", seg=16, rseg=6)
    parts.append(collar)
    tongue = box((0.06, 0.04, 0.09), (0, 0.02, 0.2), None, "b", 0.0, rot=(math.radians(-20), 0, 0))
    parts.append(tongue)
    for i in range(3):
        lace = box((0.085, 0.016, 0.012), (0, 0.03 + i * 0.04, 0.16 - i * 0.025), None, "b", 0.0, rot=(math.radians(-25), 0, 0))
        parts.append(lace)
    for o in parts:
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    b = _join(parts, f"Basket.{'G' if s < 0 else 'D'}")
    up, ac, so = _c(upper_hex, 0.5), _c(accent_hex, 0.45), _c(sole_hex, 0.6)

    def col(co, n):
        if co.z < 0.05:
            return so
        if co.z > 0.18 or (abs(n.x) > 0.75 and 0.07 < co.z < 0.13 and co.y > -0.06):   # col et virgule sur les côtés
            return ac
        return up
    paint(b, col, ao_rays=14, ao_dist=0.05, ao_strength=0.5)
    b.location = (x, J["cheville"][1], 0.0)
    stripe = box((0.14, 0.3, 0.012), (x, J["cheville"][1] + y0, 0.03), glow, f"Semelle.{'G' if s < 0 else 'D'}", 0.005)
    return [b, stripe]


def _join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    o.data.name = name
    for p in o.data.polygons:
        p.use_smooth = True
    return o


# ------------------------------------------------------------------ corps
def body_mesh(r):
    """Corps vêtu (veste + pantalon) en une peau : r donne les rayons (x, y) de chaque nœud."""
    nodes, edges = [], []

    def add(name, pos):
        nodes.append((*pos, *r[name]))
        return len(nodes) - 1
    b = add("bassin", J["bassin"])
    t = add("taille", J["taille"])
    c = add("buste", J["buste"])
    k = add("col", J["col"])
    bn = add("basecou", J["basecou"])
    n = add("cou", J["cou"])
    edges += [(b, t), (t, c), (c, k), (k, bn), (bn, n)]
    for s in (-1, 1):
        e = add("epaule", (abs(J["epaule"][0]) * s, *J["epaule"][1:]))
        cd = add("coude", (abs(J["coude"][0]) * s, *J["coude"][1:]))
        cj, wj = Vector(J["coude"]), Vector(J["poignet"])
        fa = cj.lerp(wj, 0.35)
        f = add("avantbras", (abs(fa.x) * s, fa.y, fa.z))
        w = add("poignet", (abs(J["poignet"][0]) * s, *J["poignet"][1:]))
        h = add("hanche", (abs(J["hanche"][0]) * s, *J["hanche"][1:]))
        th = add("cuisse", (abs(J["hanche"][0]) * s + s * 0.006, 0.005, 0.72))
        g = add("genou", (abs(J["genou"][0]) * s, *J["genou"][1:]))
        m = add("mollet", (abs(J["mollet"][0]) * s, *J["mollet"][1:]))
        a = add("cheville", (abs(J["cheville"][0]) * s, *J["cheville"][1:]))
        edges += [(k, e), (e, cd), (cd, f), (f, w), (b, h), (h, th), (th, g), (g, m), (m, a)]
    return skin_body(nodes, edges, subdiv=2, name="Corps")


def hero(team):
    orange = team == "orange"
    glow = mat("neon_" + team, "#FFB347" if orange else "#8FE3FF", 2.5)
    p = []
    if orange:   # « Riko » : bomber orange gonflé, jogging sombre à bande, banane noire, bandeau
        r = {"bassin": (0.165, 0.13), "taille": (0.15, 0.125), "buste": (0.235, 0.165), "col": (0.145, 0.11), "basecou": (0.068, 0.066),
             "cou": (0.052, 0.052), "epaule": (0.098, 0.098), "coude": (0.08, 0.08), "avantbras": (0.078, 0.078), "poignet": (0.056, 0.056),
             "hanche": (0.105, 0.105), "cuisse": (0.115, 0.12), "genou": (0.082, 0.088), "mollet": (0.09, 0.095), "cheville": (0.05, 0.05)}
        jacket, jacket_d, rib, pants, stripe, skin = "#FF6A1A", "#D9500E", "#23263A", "#2E3348", "#F2F4F8", "#C98B62"
    else:        # « Nova » : hoodie oversize bleu, cargo gris, boucles argentées et gros casque audio
        r = {"bassin": (0.18, 0.14), "taille": (0.2, 0.16), "buste": (0.255, 0.185), "col": (0.15, 0.115), "basecou": (0.07, 0.068),
             "cou": (0.052, 0.052), "epaule": (0.115, 0.115), "coude": (0.1, 0.1), "avantbras": (0.095, 0.095), "poignet": (0.075, 0.075),
             "hanche": (0.11, 0.11), "cuisse": (0.12, 0.125), "genou": (0.088, 0.092), "mollet": (0.088, 0.092), "cheville": (0.05, 0.05)}
        jacket, jacket_d, rib, pants, stripe, skin = "#2F7DE1", "#2563B8", "#1D2A4A", "#4A5068", "#8FE3FF", "#F2C6A0"
    body = body_mesh(r)
    jc, jd, rb, pc, sc, sk = (_c(jacket, 0.45 if orange else 0.85), _c(jacket_d, 0.45 if orange else 0.85), _c(rib, 0.9),
                              _c(pants, 0.85), _c(stripe, 0.7), _c(skin, 0.55))

    def col(co, n):
        ax = abs(co.x)
        if co.z > 1.47:
            return sk                                        # cou
        if co.z < 0.99 and ax < 0.25:                         # pantalon, bande sur le côté extérieur
            side = (co.x < 0 and n.x < -0.8) or (co.x > 0 and n.x > 0.8)
            return sc if side and co.z > 0.18 else pc
        # veste : dessous des bras et flancs un peu plus foncés, bande claire en haut de manche
        if ax > 0.22:
            arm_t = (ax - 0.22) / 0.27
            return jd if n.z < -0.3 else jc
        return jd if (abs(n.x) > 0.85 and co.z < 1.2) else jc
    paint(body, col, ao_rays=30, ao_dist=0.22, ao_strength=0.6)
    p.append(body)
    # bord-côtes : col, bas de veste, poignets, chevilles
    hem = torus(r["bassin"][0] * 1.02 if not orange else r["taille"][0] * 1.02, 0.03, (0, 0, 1.0 if orange else 0.92), None, "W_Ourlet", seg=28, rseg=8)
    hem.scale = (1.0, 0.78, 1.0)
    collar = torus(0.072, 0.02, (0, -0.01, 1.465), None, "W_Col", seg=22, rseg=8)
    collar.scale = (1.0, 1.0, 1.8)
    for o in (hem, collar):
        _paint_flat(o, rb, ao=8, dist=0.05)
        p.append(o)
    for s in (-1, 1):
        cuff = torus(r["poignet"][0] * 0.95, 0.022, (abs(J["poignet"][0]) * s + s * 0.008, J["poignet"][1], J["poignet"][2] + 0.03),
                     None, f"Poignet.{'G' if s < 0 else 'D'}", seg=18, rseg=6, rot=(0, math.radians(s * 41), 0))
        _paint_flat(cuff, rb)
        p.append(cuff)
        ank = torus(0.056, 0.02, (abs(J["cheville"][0]) * s, J["cheville"][1], 0.22), None, f"Revers.{'G' if s < 0 else 'D'}", seg=18, rseg=6)
        _paint_flat(ank, rb)
        p.append(ank)
        p.append(mitt(s, "#F2F4F8" if orange else "#8FE3FF", jacket if orange else "#2A2F45"))
        p += sneaker(s, "#F2F4F8", jacket, "#E9E4D8" if orange else "#2A2F45", glow)
        # néons d'équipe en haut de manche (lisibles de loin)
        band = torus(r["epaule"][0] * 0.97, 0.022, (0.314 * s, 0.0, 1.268), glow, f"Neon.{'G' if s < 0 else 'D'}", seg=18, rseg=6,
                     rot=(0, math.radians(s * 44), 0))
        p.append(band)
    # zip et emblème d'équipe sur la poitrine
    zip_ = box((0.03, 0.015, 0.36), (0, r["buste"][1] + 0.0, 1.21), None, "W_Zip", 0.004)
    _paint_flat(zip_, _c("#D8DCE6", 0.3), ao=0)
    p.append(zip_)
    if orange:
        emb = cyl(0.045, 0.012, (-0.1, 0.135, 1.28), None, "W_Embleme", verts=24, rot=(math.radians(80), 0, 0))
        _paint_flat(emb, _c("#F2F4F8", 0.5), ao=0)
        p.append(emb)
        bag = skin_body([(-0.1, 0.2, 1.22, 0.075, 0.055), (0, 0.21, 1.18, 0.075, 0.055), (0.1, 0.2, 1.14, 0.075, 0.055)], [(0, 1), (1, 2)],
                        subdiv=2, name="W_Banane")
        _paint_flat(bag, _c("#1E2030", 0.6), ao=8, dist=0.05)
        p.append(bag)
        strap = torus(0.2, 0.012, (0, 0, 1.2), None, "W_Sangle", seg=32, rseg=4, rot=(math.radians(-62), math.radians(30), 0))
        strap.scale = (0.95, 0.72, 1.0)
        _paint_flat(strap, _c("#1E2030", 0.6), ao=0)
        p.append(strap)
        buckle = box((0.05, 0.02, 0.035), (0.0, 0.27, 1.18), None, "W_Boucle", 0.008)
        _paint_flat(buckle, _c(jacket, 0.4), ao=0)
        p.append(buckle)
    else:
        hood = skin_body([(-0.15, -0.12, 1.45, 0.13, 0.09), (0, -0.16, 1.49, 0.13, 0.09), (0.15, -0.12, 1.45, 0.13, 0.09),
                          (0, -0.17, 1.35, 0.1, 0.07)], [(0, 1), (1, 2), (1, 3)], subdiv=2, name="W_Capuche")
        _paint_flat(hood, jd, ao=12, dist=0.08, strength=0.5)
        p.append(hood)
        pocket = box((0.24, 0.04, 0.11), (0, 0.155, 1.06), None, "W_Poche", 0.02)
        _paint_flat(pocket, jd, ao=6, dist=0.04)
        p.append(pocket)
        for s in (-1, 1):   # cordons et poches cargo
            cord = cyl(0.014, 0.15, (s * 0.04, 0.14, 1.32), None, f"W_Cordon{s}", verts=6)
            _paint_flat(cord, _c("#F2F4F8", 0.7), ao=0)
            p.append(cord)
            cargo = box((0.035, 0.11, 0.12), (abs(J["hanche"][0]) * s + s * 0.075, 0.01, 0.68), None, f"W_Cargo{s}", 0.02)
            _paint_flat(cargo, _c("#3E4459", 0.85), ao=6, dist=0.04)
            p.append(cargo)
    # tête, visage, coiffure
    hair_hex = "#22160F" if orange else "#C9CFE0"
    hair = _c(hair_hex, 0.5)

    def scalp(d):
        if orange:   # côtés rasés : cuir chevelu foncé au-dessus des oreilles et à l'arrière
            if d.z > 0.045 + max(0.0, d.y - 0.06) * 0.9 or d.y < -0.06 and d.z > -0.06:
                return hair
        else:
            if d.z > 0.05 and d.y < 0.08 or d.y < -0.04:
                return hair
        return None
    p.append(build_head(skin, scalp))
    p += face("#4A2A16" if orange else "#2E6FA8", skin, hair_hex if orange else "#3A4A6E")
    hx, hy, hz = J["tete"]
    if orange:   # banane haute vers l'avant + bandeau d'équipe
        q = skin_body([(hx, hy - 0.05, hz + 0.06, 0.07, 0.07), (hx, hy + 0.02, hz + 0.115, 0.075, 0.06), (hx, hy + 0.09, hz + 0.135, 0.06, 0.05),
                       (hx, hy + 0.15, hz + 0.13, 0.045, 0.04)], [(0, 1), (1, 2), (2, 3)], subdiv=2, name="H_Cheveux")
        top = hex_rgba("#3A2418")
        paint(q, lambda co, n: tuple(hair[i] * (1 - max(0, n.z) * 0.5) + top[i] * max(0, n.z) * 0.5 for i in range(3)) + (1.0, 0.45),
              ao_rays=12, ao_dist=0.06)
        p.append(q)
        band = torus(0.094, 0.011, (hx, hy + 0.004, hz + 0.078), None, "H_Bandeau", seg=30, rseg=6, rot=(math.radians(-7), 0, 0))
        band.scale = (1.0, 1.12, 2.2)
        _paint_flat(band, _c(jacket, 0.6), ao=0)
        p.append(band)
    else:        # boucles volumineuses + casque audio
        curls = []
        import random
        rnd = random.Random(7)
        for i in range(40):
            if len(curls) >= 12:
                break
            th = rnd.uniform(0, 2 * math.pi)
            ph = rnd.uniform(0.0, 1.15)
            d = Vector((math.sin(ph) * math.cos(th), math.sin(ph) * math.sin(th) - 0.15, math.cos(ph))).normalized()
            if d.y > 0.55 and d.z < 0.6:
                continue
            c = Vector(J["tete"]) + head_shape(d) * 1.02
            curls.append(sphere(0.06 + rnd.uniform(0, 0.015), tuple(c), None, "H_c", sub=2))
        for o in curls:
            bpy.context.view_layer.objects.active = o
            o.select_set(True)
            bpy.ops.object.transform_apply(location=True)
        q = _join(curls, "H_Cheveux")
        light = hex_rgba("#FFFFFF")
        paint(q, lambda co, n: tuple(hair[i] * (0.85 + 0.15 * max(0, n.z)) for i in range(3)) + (1.0, 0.6), ao_rays=14, ao_dist=0.08, ao_strength=0.5)
        p.append(q)
        for s in (-1, 1):
            cup = cyl(0.058, 0.05, (s * (HEAD_R[0] + 0.03), hy - 0.005, hz - 0.005), None, f"H_Coussin{s}", verts=24, bevel=0.012,
                      rot=(0, math.pi / 2, 0))
            _paint_flat(cup, _c("#2A2F45", 0.3), ao=6, dist=0.03)
            p.append(cup)
            led = cyl(0.04, 0.012, (s * (HEAD_R[0] + 0.057), hy - 0.005, hz - 0.005), glow, f"H_Led{s}", verts=24, rot=(0, math.pi / 2, 0))
            p.append(led)
        arc = torus(0.15, 0.018, (hx, hy - 0.005, hz - 0.005), None, "H_Arceau", seg=36, rseg=6, rot=(math.pi / 2, 0, 0))
        arc.scale = (1.0, 1.1, 1.0)
        bm = bmesh.new()
        bm.from_mesh(arc.data)
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.y < 0.0], context="VERTS")   # Y local = haut une fois tourné
        bm.to_mesh(arc.data)
        bm.free()
        _paint_flat(arc, _c(jacket, 0.3), ao=0)
        p.append(arc)
        del light
    return p
