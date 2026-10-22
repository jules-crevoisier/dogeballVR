"""Génère tous les assets .glb du jeu.

Usage : python3 -I tools/blender/gen_assets.py <dossier_sortie> [nom ...]
Les unités sont des mètres, Z haut dans Blender (converti en Y haut par l'export glTF).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from lib import (box, cyl, export, mat, outline_shell, reset, sphere, torus, tris)  # noqa: E402
from lib import hex_rgba  # noqa: E402
from organic import paint, skin_body  # noqa: E402


# ---------------------------------------------------------------- balles
def _ball_core(kind):
    r = 0.15
    body = sphere(r, material=mat("balle", "balle", 1.0), name="Balle", sub=3)
    parts = [body]
    dark = mat("bande", "panneau")
    if kind == "normale":
        parts.append(torus(r * 1.005, 0.012, material=dark, name="Bande", seg=32, rseg=4))
    elif kind == "bombe":
        parts.append(torus(r * 1.005, 0.014, material=dark, name="Bande", seg=32, rseg=4))
        parts.append(torus(r * 1.005, 0.014, material=dark, name="Bande2", seg=32, rseg=4, rot=(math.pi / 2, 0, 0)))
        parts.append(cyl(0.06, 0.06, (0, 0, r + 0.012), dark, "Culot", verts=12))
        parts.append(cyl(0.022, 0.1, (0, 0, r + 0.07), mat("meche", "ligne"), "Meche", verts=8))
        parts.append(sphere(0.035, (0, 0, r + 0.14), mat("etincelle", "#FF6A1A", 2.0), "Etincelle", sub=1))
    else:  # rebond : trois anneaux inclinés
        for i in range(2):
            parts.append(torus(r * 1.005, 0.016, material=dark, name=f"Anneau{i}", seg=32, rseg=4,
                               rot=(math.pi / 2, 0, i * math.pi / 2)))
    return parts


def ball(kind):  # noqa
    def f():
        return _ball_core(kind)
    return f


# ---------------------------------------------------------------- avatars
def avatar(team, part):
    """Personnage trapu façon dessin animé : grosse tête, sweat ample, gros gants et baskets, cheveux et visage expressifs."""
    orange = team == "orange"
    c = mat("combi_" + team, "orange" if orange else "bleu")
    cd = mat("combi_" + team + "_ombre", "#C94A0A" if orange else "#3E8FCB")
    wh = mat("blanc", "ligne")
    glow = mat("neon_" + team, "#FFB347" if orange else "#8FE3FF", 2.5)
    ink = mat("pupille", "#14192B")
    tc = hex_rgba("#FF6A1A" if orange else "#4FB8F5")
    td = hex_rgba("#B8400A" if orange else "#2A78B8")
    jog = hex_rgba("#2B3658")
    jog_d = hex_rgba("#1B2440")
    white = hex_rgba("#F2F4F8")
    skin = hex_rgba("#F0B994")
    p = []
    # --- corps : squelette Skin + Subdivision, couleur et occlusion peintes dans les sommets
    N = [  # x, y, z, rx, ry
        (0, 0, 0.84, 0.25, 0.2), (0, 0, 1.1, 0.35, 0.25), (0, 0, 1.36, 0.1, 0.1),                      # 0 bassin, 1 buste, 2 cou
        (-0.34, 0, 1.28, 0.15, 0.15), (-0.5, 0.02, 1.08, 0.13, 0.13), (-0.58, 0.07, 0.86, 0.1, 0.1),  # 3-5 bras G
        (0.34, 0, 1.28, 0.15, 0.15), (0.5, 0.02, 1.08, 0.13, 0.13), (0.58, 0.07, 0.86, 0.1, 0.1),     # 6-8 bras D
        (-0.16, 0, 0.74, 0.16, 0.16), (-0.17, 0.02, 0.44, 0.145, 0.145), (-0.17, 0, 0.17, 0.12, 0.12),
        (0.16, 0, 0.74, 0.16, 0.16), (0.17, 0.02, 0.44, 0.145, 0.145), (0.17, 0, 0.17, 0.12, 0.12),
        (-0.17, 0.17, 0.1, 0.14, 0.11), (0.17, 0.17, 0.1, 0.14, 0.11),                                     # 15-16 baskets
    ]
    E = [(0, 1), (1, 2), (1, 3), (3, 4), (4, 5), (1, 6), (6, 7), (7, 8),
         (0, 9), (9, 10), (10, 11), (11, 15), (0, 12), (12, 13), (13, 14), (14, 16)]
    body = skin_body(N, E, subdiv=2, name="Corps")

    def body_color(co, n):
        ax = abs(co.x)
        if co.z < 0.2:                                   # baskets blanches, bout plus clair
            return white
        if co.z < 0.3 and co.y > 0.1:
            return white
        if co.z < 0.86 and ax < 0.5:                     # jogging : bas plus sombre
            return jog if co.z > 0.3 else jog_d
        if co.z < 0.96 and ax > 0.5:                     # gants
            return white
        t = max(0.0, min(1.0, (1.42 - co.z) / 0.6))
        base = tuple(tc[i] * (1 - t * 0.4) + td[i] * t * 0.4 for i in range(3))
        if ax > 0.45 and co.z < 1.05:                    # poignets plus foncés
            return td
        return base + (1,)
    paint(body, body_color, ao_rays=30, ao_dist=0.28)
    p.append(body)
    for s_ in (-1, 1):
        p.append(sphere(0.105, (s_ * 0.6, 0.08, 0.83), wh, f"Gant{s_}", sub=3, scale=(1.0, 0.9, 1.0)))
        p.append(sphere(0.045, (s_ * 0.68, 0.1, 0.87), wh, f"Pouce{s_}", sub=2))
        p.append(torus(0.095, 0.03, (s_ * 0.585, 0.065, 0.945), glow, f"Poignet{s_}", seg=16, rseg=6, rot=(0, -s_ * math.radians(10), 0)))
        p.append(box((0.2, 0.36, 0.025), (s_ * 0.17, 0.11, 0.022), glow, f"Semelle{s_}", 0.01))
        p.append(box((0.16, 0.12, 0.06), (s_ * 0.17, 0.27, 0.12), wh, f"Bout{s_}", 0.025))
        p.append(torus(0.135, 0.035, (s_ * 0.17, 0.0, 0.28), cd, f"Revers{s_}", seg=18, rseg=6))
    p.append(cyl(0.34, 0.09, (0, 0, 0.9), cd, "Ourlet", verts=22))
    p[-1].scale = (1.08, 0.8, 1)
    p.append(box((0.4, 0.06, 0.17), (0, 0.235, 0.98), cd, "Poche", 0.04))
    p.append(box((0.018, 0.02, 0.4), (0, 0.255, 1.22), wh, "Zip", 0.004))
    for s_ in (-1, 1):
        p.append(cyl(0.012, 0.18, (s_ * 0.05, 0.25, 1.2), wh, f"Cordon{s_}", verts=6, bevel=0.0))
    p.append(sphere(0.2, (0, -0.15, 1.38), c, "Capuche", sub=3, scale=(1.35, 0.7, 0.8)))
    if orange:
        p.append(cyl(0.085, 0.014, (-0.17, 0.26, 1.2), wh, "Signe", verts=26, rot=(math.pi / 2, 0, 0)))
    else:
        p.append(cyl(0.1, 0.014, (-0.17, 0.26, 1.2), wh, "Signe", verts=4, rot=(math.pi / 2, 0, math.pi / 4), smooth=False))
    # --- tête
    head = sphere(0.27, (0, 0, 1.72), mat("peau_" + team, "#F0B994"), "H_Tete", sub=4, scale=(1.12, 1.0, 1.08))
    hair_c = hex_rgba("#B5471C" if orange else "#DDE6F5")

    def head_color(co, n):   # cuir chevelu : dessus et arrière de la tête
        edge = co.y * 0.9 + (co.z - 1.72) * 0.55
        return (hair_c if edge < 0.0 and co.z > 1.62 or co.z > 1.93 else skin) + (1,)
    paint(head, head_color, ao_rays=10, ao_dist=0.1, ao_strength=0.25)
    p.append(head)
    for s_ in (-1, 1):   # oreilles
        p.append(sphere(0.06, (s_ * 0.295, 0.0, 1.71), mat("peau_oreille", "#E5A57F"), f"H_Oreille{s_}", sub=2, scale=(0.45, 0.8, 1.0)))
    eye_w = mat("oeil", "#FFFFFF")
    iris = mat("iris", "#6B3E1E" if orange else "#2E6FA8")
    for s_ in (-1, 1):
        ex = s_ * 0.105
        p.append(sphere(0.072, (ex, 0.235, 1.74), eye_w, f"H_Oeil{s_}", sub=3, scale=(0.92, 0.5, 1.1)))
        p.append(sphere(0.05, (ex - s_ * 0.006, 0.262, 1.742), iris, f"H_Iris{s_}", sub=3, scale=(1.0, 0.4, 1.1)))
        p.append(sphere(0.03, (ex - s_ * 0.006, 0.272, 1.745), ink, f"H_Pupille{s_}", sub=2, scale=(1.0, 0.4, 1.1)))
        p.append(sphere(0.014, (ex + s_ * 0.012, 0.279, 1.765), eye_w, f"H_Reflet{s_}", sub=1))
        p.append(sphere(0.085, (ex, 0.232, 1.762), mat("peau_paupiere", "#F0B994"), f"H_Paupiere{s_}", sub=3, scale=(1.0, 0.5, 0.55)))
        p.append(box((0.12, 0.035, 0.03), (ex, 0.24, 1.84), ink, f"H_Sourcil{s_}", 0.012, rot=(0, math.radians(-s_ * 16), 0)))
        p.append(sphere(0.045, (s_ * 0.17, 0.2, 1.64), mat("joue", "#F29A82"), f"H_Joue{s_}", sub=2, scale=(1.0, 0.25, 0.7)))
    p.append(sphere(0.036, (0, 0.268, 1.69), mat("peau_nez", "#E8A47E"), "H_Nez", sub=2))
    p.append(sphere(0.07, (0, 0.25, 1.605), mat("bouche", "#7A2A30"), "H_Bouche", sub=3, scale=(1.0, 0.3, 0.42)))
    p.append(box((0.06, 0.012, 0.014), (0, 0.268, 1.62), wh, "H_Dents", 0.005))
    # --- coiffure et accessoire propres à l'équipe
    if orange:   # banane rousse + serre-tête lumineux
        hair = mat("cheveux_o", "#B5471C")
        hd = skin_body([(0, -0.05, 1.93, 0.17, 0.16), (0, 0.1, 2.02, 0.15, 0.12), (0, 0.24, 2.0, 0.12, 0.1), (0, 0.32, 1.93, 0.09, 0.08),
                        (-0.18, 0.0, 1.88, 0.1, 0.12), (0.18, 0.0, 1.88, 0.1, 0.12), (0, -0.22, 1.82, 0.2, 0.12)],
                       [(0, 1), (1, 2), (2, 3), (0, 4), (0, 5), (0, 6)], subdiv=2, name="H_Cheveux")
        hc = hex_rgba("#B5471C")
        hc2 = hex_rgba("#F28A3C")
        paint(hd, lambda co, n: tuple(hc[i] * (1 - max(0, min(1, (co.z - 1.8) / 0.28))) + hc2[i] * max(0, min(1, (co.z - 1.8) / 0.28)) for i in range(3)) + (1,), ao_rays=12, ao_dist=0.12)
        p.append(hd)
        p.append(torus(0.272, 0.024, (0, 0, 1.86), cd, "H_Bandeau", seg=28, rseg=6))
    else:        # boucles argentées + casque audio
        hair = hex_rgba("#DDE6F5")
        hd = skin_body([(0, -0.04, 1.95, 0.17, 0.15), (-0.14, 0.04, 2.0, 0.1, 0.1), (0.14, 0.04, 2.0, 0.1, 0.1), (0, 0.12, 2.04, 0.1, 0.1),
                        (-0.2, -0.05, 1.86, 0.1, 0.12), (0.2, -0.05, 1.86, 0.1, 0.12), (0, -0.2, 1.84, 0.18, 0.12)],
                       [(0, 1), (0, 2), (0, 3), (0, 4), (0, 5), (0, 6)], subdiv=2, name="H_Cheveux")
        paint(hd, lambda co, n: tuple(hair[i] * (0.82 + 0.3 * max(0, min(1, (co.z - 1.85) / 0.25))) for i in range(3)) + (1,), ao_rays=12, ao_dist=0.12)
        p.append(hd)
        for s_ in (-1, 1):
            p.append(cyl(0.12, 0.075, (s_ * 0.3, 0, 1.74), c, f"H_Coussin{s_}", verts=20, rot=(0, math.pi / 2, 0)))
            p.append(cyl(0.09, 0.02, (s_ * 0.343, 0, 1.74), glow, f"H_Led{s_}", verts=20, rot=(0, math.pi / 2, 0)))
        p.append(torus(0.3, 0.022, (0, 0, 1.78), c, "H_Arceau", seg=30, rseg=6, rot=(math.pi / 2, 0, 0)))
    if part == "corps":   # les gants du jeu sont le modèle « main » posé sur les mains suivies
        for o in [o for o in p if o.name.startswith(("Gant", "Pouce"))]:
            p.remove(o)
            bpy.data.objects.remove(o, do_unlink=True)
    keep = p if part == "tout" else [o for o in p if (o.name.startswith("H_")) == (part == "tete")]
    for o in p:
        if o not in keep:
            bpy.data.objects.remove(o, do_unlink=True)
    # origine = pivot de l'avatar Godot : tête au centre de la tête, torse à 0,5 m sous la tête
    dz = 0 if part == "tout" else (1.72 if part == "tete" else 1.13)
    for o in keep:
        o.location.z -= dz
    return lambda: keep


def hand():
    wh = mat("blanc", "ligne")
    p = [sphere(0.115, (0, 0, 0), wh, "Paume", sub=3, scale=(1.0, 0.8, 1.05))]
    for i in range(4):  # quatre doigts fusionnés en capsules courtes
        p.append(sphere(0.045, ((i - 1.5) * 0.06, 0.0, 0.125 - abs(i - 1.5) * 0.015), wh, f"Doigt{i}", sub=3, scale=(1.0, 1.0, 1.6)))
    th = sphere(0.05, (0.125, 0.0, 0.03), wh, "Pouce", sub=3, scale=(1.0, 1.0, 1.6))
    th.rotation_euler = (0, math.radians(-35), 0)
    p.append(th)
    p.append(cyl(0.085, 0.07, (0, 0, -0.1), mat("manchette", "#2B3658"), "Manchette", verts=12))
    return lambda: p


# ---------------------------------------------------------------- plots hexagonaux
def pad(team):
    base = {"neutre": "neutre", "orange": "orange", "bleu": "bleu"}[team]
    R = 0.62
    body = cyl(R, 0.06, (0, 0, 0.03), mat("plot_base", "mur"), "Plaque", verts=6, bevel=0.015)
    ring = cyl(R * 0.82, 0.012, (0, 0, 0.066), mat("plot_" + team, base, 0.6 if team != "neutre" else 0.0),
               "Anneau", verts=6, smooth=False)
    inner = cyl(R * 0.62, 0.014, (0, 0, 0.068), mat("plot_base", "mur"), "Centre", verts=6, smooth=False)
    return lambda: [body, ring, inner]


# ---------------------------------------------------------------- moyeu / lanceur
def hub():
    dk = mat("hub_base", "panneau")
    gr = mat("sol", "sol")
    wh = mat("ligne", "ligne")
    p = []
    p.append(cyl(1.25, 0.2, (0, 0, 0.1), gr, "Socle", verts=24, bevel=0.04))
    p.append(cyl(0.62, 1.1, (0, 0, 0.75), dk, "Fut", verts=16, bevel=0.0))
    p.append(torus(0.64, 0.06, (0, 0, 1.0), wh, "Bague", seg=24, rseg=6))
    p.append(cyl(0.55, 0.22, (0, 0, 1.4), gr, "Tete", verts=16, r2=0.42))
    # bouche du lanceur : ouverture orientée vers le haut, balle posée dessus
    p.append(torus(0.26, 0.05, (0, 0, 1.52), mat("balle", "balle", 1.0), "Bouche", seg=20, rseg=6))
    # rotor séparé (nœud « Rotor ») : 4 bras
    rot = bpy.data.objects.new("Rotor", None)
    bpy.context.collection.objects.link(rot)
    rot.location = (0, 0, 0.95)
    bm = mat("bras", "#2B3658")
    for i in range(4):
        a = i * math.pi / 2
        arm = box((1.9, 0.3, 0.2), (math.cos(a) * 1.1, math.sin(a) * 1.1, 0.95), bm, f"Bras{i}", 0.03, rot=(0, 0, a))
        arm.parent = rot
        arm.matrix_parent_inverse = rot.matrix_world.inverted()
        p.append(arm)
        tip = box((0.4, 0.4, 0.26), (math.cos(a) * 2.05, math.sin(a) * 2.05, 0.95), mat("pointe", "orange" if i % 2 == 0 else "bleu"), f"Pointe{i}", 0.03)
        tip.parent = rot
        tip.matrix_parent_inverse = rot.matrix_world.inverted()
        p.append(tip)
    p.append(rot)
    return lambda: p


# ---------------------------------------------------------------- arène
def wall(team="neutre"):
    m = mat("mur", "mur")
    p = [box((4.0, 0.3, 2.4), (0, 0, 1.2), m, "Mur", 0.05)]
    p.append(box((4.0, 0.33, 0.12), (0, 0, 0.06), mat("plinthe", "sol"), "Plinthe", 0.02))
    p.append(box((4.0, 0.335, 0.06), (0, 0, 0.5), mat("bande_lim", "ligne", 1.5), "Bande", 0.0))
    if team != "neutre":
        cm = mat("chevron_" + team, "orange" if team == "orange" else "bleu")
        for i in range(4):  # chevrons en « V » inversé, larges, aux couleurs de l'équipe
            x = -1.5 + i * 1.0
            for s in (-1, 1):
                p.append(box((0.62, 0.02, 0.2), (x + s * 0.2, 0.16, 1.45), cm, f"Chevron{i}_{s}", 0.0, rot=(0, s * math.radians(-38), 0)))
    return lambda: p


def floor_tile(team="neutre"):
    base = {"neutre": "sol", "orange": "sol_orange", "bleu": "sol_bleu"}[team]
    p = [box((4.0, 4.0, 0.1), (0, 0, -0.05), mat("sol_" + team, base), "Sol", 0.012)]
    j = mat("joint", "mur")
    p.append(box((3.96, 0.03, 0.012), (0, 1.97, 0.002), j, "JointN", 0.0))
    p.append(box((3.96, 0.03, 0.012), (0, -1.97, 0.002), j, "JointS", 0.0))
    p.append(box((0.03, 3.96, 0.012), (1.97, 0, 0.002), j, "JointE", 0.0))
    p.append(box((0.03, 3.96, 0.012), (-1.97, 0, 0.002), j, "JointO", 0.0))
    p.append(box((0.08, 3.9, 0.014), (-1.5, 0, 0.003), mat("ligne_sol", "#9AA3BC"), "Ligne", 0.0))
    if team != "neutre":
        p.append(box((1.0, 3.9, 0.014), (1.1, 0, 0.004), mat("bande_" + team, "orange" if team == "orange" else "bleu"), "Bande", 0.0))
    return lambda: p


def cover_block():
    p = [box((1.2, 0.5, 1.0), (0, 0, 0.5), mat("abri", "#2B3658"), "Abri", 0.07)]
    p.append(box((1.22, 0.52, 0.06), (0, 0, 1.0), mat("abri_haut", "neutre"), "Haut", 0.02))
    return lambda: p


def ramp():
    import bmesh
    from lib import finish
    me = bpy.data.meshes.new("Tremplin")
    # coin : 3 m de large (x), 4 m de long (y), haut de 1 m côté y>0
    v = [(-1.5, -2, 0), (1.5, -2, 0), (1.5, 2, 0), (-1.5, 2, 0), (-1.5, 2, 1), (1.5, 2, 1)]
    f = [(0, 3, 2, 1), (3, 4, 5, 2), (0, 1, 5, 4)[::-1], (0, 4, 3), (1, 2, 5)]
    me.from_pydata(v, [], [(0, 3, 2, 1), (3, 4, 5, 2), (0, 1, 5, 4), (0, 4, 3)[::-1], (1, 2, 5)])
    me.update()
    o = bpy.data.objects.new("Tremplin", me)
    bpy.context.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()
    finish(o, mat("tremplin", "sol"), "Tremplin", smooth=False, bevel=0.03)
    stripe = box((3.0, 0.14, 0.05), (0, 1.9, 0.98), mat("tremplin_bord", "ligne"), "Bord", 0.0)
    return lambda: [o, stripe]


def pit_edge():
    p = [box((4.0, 0.7, 0.2), (0, 0, 0.0), mat("fosse_bord", "neutre"), "Rebord", 0.05)]
    p.append(box((4.0, 0.12, 0.04), (0, 0.2, 0.11), mat("fosse_ligne", "ligne", 1.0), "Ligne", 0.0))
    p.append(box((4.0, 0.5, 0.06), (0, 0.9, -0.15), mat("fosse_danger", "orange", 0.8), "Danger", 0.0))
    return lambda: p


def scoreboard():
    pan = mat("panneau", "panneau")
    p = [box((4.4, 0.24, 1.5), (0, 0, 0), pan, "Panneau", 0.1)]
    p.append(box((4.5, 0.26, 0.07), (0, 0, 0.78), mat("lis", "ligne", 1.0), "LisereHaut", 0.0))
    p.append(box((2.0, 0.1, 1.2), (-1.1, -0.1, 0), mat("score_o", "orange", 0.6), "ZoneOrange", 0.03))
    p.append(box((2.0, 0.1, 1.2), (1.1, -0.1, 0), mat("score_b", "bleu", 0.6), "ZoneBleue", 0.03))
    for x in (-1.1, 1.1):
        bpy.ops.object.text_add(location=(x, -0.2, -0.38), rotation=(math.pi / 2, 0, math.pi))
        t = bpy.context.active_object
        t.data.body = "00"
        t.data.size = 0.85
        t.data.extrude = 0.03
        t.data.align_x = "CENTER"
        bpy.ops.object.convert(target="MESH")
        t = bpy.context.active_object
        t.name = "Chiffre"
        t.data.materials.clear()
        t.data.materials.append(mat("chiffre", "ligne", 1.0))
        p.append(t)
    for s in (-1, 1):
        p.append(cyl(0.06, 3.0, (s * 1.8, 0, 2.2), mat("cable", "jambes"), f"Cable{s}", verts=6))
    return lambda: p


def lamp():
    p = [cyl(0.7, 0.1, (0, 0, 0), mat("lampe_lumiere", "ligne", 4.0), "Lumiere", verts=20, bevel=0.0)]
    p.append(cyl(0.78, 0.07, (0, 0, 0.085), mat("lampe_cadre", "panneau"), "Cadre", verts=20))
    p.append(cyl(0.75, 0.05, (0, 0, -0.07), mat("lampe_bord", "#2B3658"), "Bord", verts=20))
    return lambda: p


# ---------------------------------------------------------------- effets
def fx_ring():
    r = torus(1.0, 0.07, material=mat("fx_anneau", "ligne", 2.0), name="Onde", seg=40, rseg=4)
    return lambda: [r]


def fx_blast():
    p = []
    for i, (r, h) in enumerate(((1.0, 0.0), (0.72, 0.22), (0.42, 0.4))):
        p.append(torus(r, 0.09 - i * 0.015, (0, 0, h), mat("fx_souffle", "ligne", 1.5), f"Anneau{i}", seg=36, rseg=5))
    return lambda: p


def fx_spark():
    p = []
    for i in range(4):
        o = box((0.7, 0.06, 0.06), (0, 0, 0), mat("fx_etincelle", "ligne", 2.0), f"Trait{i}", 0.0)
        o.rotation_euler = (0, 0, i * math.pi / 4)
        p.append(o)
    return lambda: p


def L(f, *a):
    return lambda: f(*a)()


ASSETS = {
    "balle_normale": L(ball, "normale"), "balle_bombe": L(ball, "bombe"), "balle_rebond": L(ball, "rebond"),
    "avatar_orange_corps": L(avatar, "orange", "corps"), "avatar_orange_tete": L(avatar, "orange", "tete"),
    "avatar_orange": L(avatar, "orange", "tout"), "avatar_bleu": L(avatar, "bleu", "tout"),
    "avatar_bleu_corps": L(avatar, "bleu", "corps"), "avatar_bleu_tete": L(avatar, "bleu", "tete"), "main": L(hand),
    "plot_neutre": L(pad, "neutre"), "plot_orange": L(pad, "orange"), "plot_bleu": L(pad, "bleu"),
    "moyeu": L(hub), "mur": L(wall), "mur_orange": L(wall, "orange"), "mur_bleu": L(wall, "bleu"), "sol_tuile": L(floor_tile), "sol_tuile_orange": L(floor_tile, "orange"), "sol_tuile_bleu": L(floor_tile, "bleu"), "abri": L(cover_block),
    "tremplin": L(ramp), "bord_fosse": L(pit_edge), "tableau_score": L(scoreboard), "lampe": L(lamp),
    "fx_onde": L(fx_ring), "fx_souffle": L(fx_blast), "fx_etincelle": L(fx_spark),
}


def main():
    out = sys.argv[1]
    names = sys.argv[2:] or list(ASSETS)
    os.makedirs(out, exist_ok=True)
    for n in names:
        reset()
        parts = ASSETS[n]()
        objs = [o for o in bpy.context.scene.objects]
        t = export(os.path.join(out, n + ".glb"), objs)
        print(f"{n}: {t} triangles")


if __name__ == "__main__":
    main()
