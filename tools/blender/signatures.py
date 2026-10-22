"""Props signature de chaque carte, posés hors de l'aire de jeu (sans collision) : ce qui donne son identité au quartier.

    chantier  (Fosse croisée) : grue à tour dont la flèche passe au-dessus du terrain, échafaudages bâchés, cônes et barrières
    coucher   (Tour 4v4)      : gratte-ciel art déco voisin à flèche lumineuse, panneau « ZÉNITH », antennes à feux rouges
    nuit      (Néon Court)    : étals du marché de nuit à auvents rayés, guirlandes d'ampoules au-dessus du terrain, lanternes
    brume     (La Fosse)      : piles de conteneurs, portique de quai, bittes d'amarrage et phare

Noms, enseignes et motifs sont originaux. Axes comme map_toit (X longueur, Y largeur, Z haut).
"""
import math
import random

import bpy
from mathutils import Vector

from kc import block, dome, kmat, tube


def _cone(r, h, loc, kind, name):
    bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=r, radius2=r * 0.12, depth=h, location=(loc[0], loc[1], loc[2] + h / 2))
    o = bpy.context.active_object
    o.name = o.data.name = name
    o.data.materials.append(kmat(kind))
    return o


def _beam(a, b, r, kind, name, verts=6):
    """Tube droit de a à b (treillis, câbles)."""
    a, b = Vector(a), Vector(b)
    d = b - a
    o = tube(r, d.length, tuple((a + b) / 2), kind, name, verts)
    o.rotation_mode = "QUATERNION"
    o.rotation_quaternion = d.to_track_quat("Z", "Y")
    return o


def _lattice(x, y, z0, z1, w, kind, name, step=2.4):
    """Mât en treillis carré : 4 montants et croisillons."""
    h = w / 2
    for i, (dx, dy) in enumerate(((-h, -h), (h, -h), (h, h), (-h, h))):
        _beam((x + dx, y + dy, z0), (x + dx, y + dy, z1), 0.09, kind, f"{name}Montant{i}")
    z, k = z0, 0
    while z + step <= z1:
        for i, ((ax, ay), (bx, by)) in enumerate((((-h, -h), (h, -h)), ((h, -h), (h, h)), ((h, h), (-h, h)), ((-h, h), (-h, -h)))):
            _beam((x + ax, y + ay, z), (x + bx, y + by, z + step), 0.05, kind, f"{name}X{k}_{i}")
        z += step
        k += 1


def _text(body, loc, size, kind, name, rot=(math.pi / 2, 0, 0)):
    bpy.ops.object.text_add(location=loc, rotation=rot)
    t = bpy.context.active_object
    t.data.body, t.data.size, t.data.extrude, t.data.align_x = body, size, 0.03 if size < 1 else 0.06, "CENTER"
    t.data.resolution_u = 2 if size < 1 else 4   # petites enseignes : courbes grossières, invisibles à cette taille
    bpy.ops.object.convert(target="MESH")
    t = bpy.context.active_object
    t.name = name
    t.data.materials.clear()
    t.data.materials.append(kmat(kind))
    return t


# ------------------------------------------------------------------ chantier
def chantier(HX, HY):
    # grue à tour : mât au coin nord-ouest, flèche qui passe au-dessus du terrain, contre-flèche et lest, cabine
    gx, gy, top = -HX - 9, HY + 9, 30.0
    block((5, 5, 1.2), (gx, gy, 0.6), "beton", "GrueSocle", 0.05)
    _lattice(gx, gy, 1.2, top, 2.0, "danger_ligne", "GrueMat")
    jib_dir = Vector((1, -0.75, 0)).normalized()
    tip = Vector((gx, gy, top + 1.0)) + jib_dir * 34
    back = Vector((gx, gy, top + 1.0)) - jib_dir * 11
    for dz, r in ((0.0, 0.16), (1.4, 0.1)):
        _beam((gx, gy, top + 1.0 + dz), tuple(tip + Vector((0, 0, dz * 0.3))), r, "danger_ligne", f"GrueFleche{dz}")
        _beam((gx, gy, top + 1.0 + dz), tuple(back + Vector((0, 0, dz * 0.3))), r, "danger_ligne", f"GrueContre{dz}")
    for k in range(14):   # treillis de la flèche
        p = Vector((gx, gy, top + 1.0)) + jib_dir * (k * 2.4)
        _beam(tuple(p), tuple(p + jib_dir * 2.4 + Vector((0, 0, 1.4 - k * 0.03))), 0.05, "danger_ligne", f"GrueTreillis{k}")
    _beam((gx, gy, top + 1.0), (gx, gy, top + 6.5), 0.14, "danger_ligne", "GruePoincon")
    _beam((gx, gy, top + 6.5), tuple(tip), 0.03, "metal_sombre", "GrueTirant")
    _beam((gx, gy, top + 6.5), tuple(back), 0.03, "metal_sombre", "GrueTirantB")
    block((3.0, 2.2, 2.4), tuple(back + Vector((0, 0, -1.4))), "beton", "GrueLest", 0.05)
    block((1.8, 1.8, 1.8), (gx + 1.6, gy - 1.2, top - 0.6), "peinture_blanche", "GrueCabine", 0.08)
    block((1.6, 0.06, 0.9), (gx + 1.6, gy - 2.12, top - 0.4), "fenetres", "GrueVitre", 0.0)
    dome(0.25, tuple(tip + Vector((0, 0, 0.3))), "neon_orange", "GrueFeu")
    # charge suspendue : poutrelle orange au bout du câble, au-dessus du terrain
    hook = Vector((gx, gy, top + 1.0)) + jib_dir * 24
    _beam(tuple(hook), tuple(hook + Vector((0, 0, -12))), 0.025, "metal_sombre", "GrueCable")
    block((6.0, 0.5, 0.5), tuple(hook + Vector((0, 0, -12.6))), "tuyau", "GruePoutre", 0.03)
    # échafaudage bâché le long du côté sud, hors parapet
    y0 = -HY - 4.2
    for i in range(9):
        x = -HX + 4 + i * 3.6
        for dy in (0, 1.4):
            _beam((x, y0 - dy, 0), (x, y0 - dy, 9.0), 0.06, "metal_sombre", f"EchafPoteau{i}_{dy}")
        for z in (3.0, 6.0, 9.0):
            block((3.6, 1.6, 0.12), (x + 1.8, y0 - 0.7, z), "bois", f"EchafPlancher{i}_{z}", 0.0)
    for k in range(4):   # bâches orange et vertes
        block((7.0, 0.06, 5.5), (-HX + 7 + k * 8.2, y0 - 1.5, 5.2), "tuyau" if k % 2 == 0 else "herbe", f"Bache{k}", 0.0)
    rnd = random.Random(5)
    for i in range(10):   # cônes et barrières de chantier au pied des parapets
        x, y = rnd.uniform(-HX, HX), (HY + 3.0) * (1 if i % 2 else -1)
        _cone(0.28, 0.75, (x, y, 0), "tuyau", f"Cone{i}")
        tube(0.3, 0.08, (x, y, 0.04), "metal_sombre", f"ConeBase{i}", 12)
    for i, x in enumerate((-10, -3, 6, 13)):
        block((2.2, 0.12, 0.35), (x, HY + 4.4, 0.9), "danger", f"Barriere{i}", 0.02)
        for dx in (-0.9, 0.9):
            block((0.1, 0.5, 0.9), (x + dx, HY + 4.4, 0.45), "peinture_blanche", f"BarrierePied{i}{dx}", 0.0)
    block((4.5, 2.2, 2.4), (HX + 8, HY + 6, 1.2), "brique_jaune", "BetonniereCaisse", 0.08)   # bétonnière
    tube(1.0, 2.6, (HX + 8, HY + 6, 3.3), "tuyau", "BetonniereCuve", 16, rot=(0, math.pi / 2.6, 0))


# ------------------------------------------------------------------ coucher
def coucher(HX, HY):
    # gratte-ciel art déco voisin : gradins, liserés néon, couronne et flèche lumineuse
    x, y = HX + 22, HY + 20
    z = -30.0
    for k, (w, h) in enumerate(((18, 52), (14, 12), (10, 8), (6, 6))):
        block((w, w, h), (x, y, z + h / 2), "brique_jaune" if k % 2 == 0 else "beton_clair", f"DecoGradin{k}", 0.1)
        for sx in (-1, 1):
            block((0.25, w + 0.3, 0.25), (x + sx * w / 2, y, z + h), "neon_rose", f"DecoNeon{k}{sx}", 0.0)
        z += h
    _cone(2.2, 14, (x, y, z), "capuchon", "DecoFleche")
    dome(0.6, (x, y, z + 14.2), "neon_rose", "DecoFeu")
    # panneau publicitaire « ZÉNITH » sur structure, côté nord
    py = HY + 6.5
    for i, px in enumerate((-8, 8)):
        _lattice(px, py, 0, 9, 0.8, "metal_sombre", f"PanneauPied{i}", step=3.0)
    block((20, 0.6, 5.2), (0, py, 11.4), "capuchon", "Panneau", 0.15)
    block((19.2, 0.1, 4.4), (0, py - 0.32, 11.4), "metal_bleu", "PanneauFond", 0.0)
    _text("ZÉNITH", (0, py - 0.45, 10.3), 3.2, "neon_orange", "PanneauTexte", rot=(math.pi / 2, 0, math.pi))
    for i in range(10):
        block((0.3, 0.3, 0.3), (-9 + i * 2, py - 0.5, 14.2), "neon_jaune", f"PanneauAmpoule{i}", 0.0)
    # antennes à feux rouges sur les cages d'escalier
    for i, (ax, ay) in enumerate(((-HX - 4.5, -HY - 1.0), (HX + 4.5, HY + 1.0))):
        _beam((ax, ay, 6.0), (ax, ay, 15.0), 0.08, "metal_sombre", f"Antenne{i}")
        dome(0.22, (ax, ay, 15.2), "neon_rose", f"AntenneFeu{i}")
    # nuages rosés bas au coucher (bandes plates, sans relief)
    for i in range(5):
        a = i / 5 * math.tau + 0.4
        block((40, 8, 0.4), (math.cos(a) * 140, math.sin(a) * 120, 36 + i * 5), "neon_rose", f"Nuage{i}", 0.0,
              rot=(0, 0, a + math.pi / 2))


# ------------------------------------------------------------------ nuit
def nuit(HX, HY):
    rnd = random.Random(11)
    awn = ["orange_equipe", "bleu_equipe", "danger_ligne", "neon_vert", "peinture_blanche"]
    signs = ["RAMEN", "TACOS", "DISQUES", "FRITES", "CAFÉ", "JOUETS"]
    k = 0
    for sy in (-1, 1):   # étals au-delà des parapets nord et sud
        for i in range(6):
            x = -HX + 2.5 + i * (2 * HX - 5) / 5
            y = sy * (HY + 4.6)
            block((3.4, 1.6, 1.0), (x, y, 0.5), "bois", f"EtalComptoir{k}", 0.0)
            for dx in (-1.6, 1.6):
                for dy in (-0.75, 0.75):
                    _beam((x + dx, y + dy, 0), (x + dx, y + dy, 2.6), 0.05, "metal_sombre", f"EtalPoteau{k}{dx}{dy}")
            c1, c2 = awn[k % len(awn)], "peinture_blanche"
            for s in range(6):   # auvent rayé incliné vers le terrain
                block((0.56, 2.2, 0.06), (x - 1.4 + s * 0.56, y - sy * 0.2, 2.75), c1 if s % 2 == 0 else c2, f"Auvent{k}_{s}", 0.0,
                      rot=(sy * 0.25, 0, 0))
            block((2.6, 0.08, 0.5), (x, y - sy * 0.82, 3.25), "capuchon", f"EtalEnseigne{k}", 0.0)
            _text(signs[k % len(signs)], (x, y - sy * 0.88, 3.08), 0.36, ["neon_rose", "neon_cyan", "neon_jaune", "neon_vert"][k % 4],
                  f"EtalTexte{k}", rot=(math.pi / 2, 0, 0 if sy > 0 else math.pi))
            for j in range(3):
                block((0.26, 0.26, 0.34), (x - 1.0 + j, y, 2.3), "neon_jaune", f"Lanterne{k}_{j}", 0.0)
            for j in range(rnd.randint(2, 4)):   # cageots
                block((0.5, 0.4, 0.3), (x - 1.2 + j * 0.7, y + sy * 0.3, 1.15), "bois", f"Cageot{k}_{j}", 0.0)
            k += 1
    # guirlandes d'ampoules tendues au-dessus du terrain (au-dessus du plafond de jeu)
    poles = [(-HX - 1.8, -HY - 2.6), (HX + 1.8, -HY - 2.6), (HX + 1.8, HY + 2.6), (-HX - 1.8, HY + 2.6), (0, -HY - 2.6), (0, HY + 2.6)]
    for i, (px, py) in enumerate(poles):
        _beam((px, py, 0), (px, py, 9.5), 0.1, "metal_sombre", f"GuirlandeMat{i}")
    pairs = [(0, 2), (1, 3), (4, 5), (0, 5), (1, 5), (3, 4), (2, 4)]
    n = 0
    for a, b in pairs:
        pa, pb = Vector((*poles[a], 9.3)), Vector((*poles[b], 9.3))
        steps = int((pb - pa).length / 1.4)
        prev = pa
        for s in range(1, steps + 1):
            t = s / steps
            p = pa.lerp(pb, t)
            p.z -= math.sin(t * math.pi) * 1.4   # chaînette
            _beam(tuple(prev), tuple(p), 0.012, "metal_sombre", f"Fil{n}")
            block((0.2, 0.2, 0.24), tuple(p + Vector((0, 0, -0.14))), "neon_jaune" if s % 3 else "neon_orange", f"Ampoule{n}", 0.0)
            prev = p
            n += 1
    # grande enseigne verticale « MARCHÉ DE NUIT »
    block((2.2, 0.6, 12.0), (HX + 6.5, 0, 6.0), "capuchon", "MarcheEnseigne", 0.15)
    for j, ch in enumerate("MARCHÉ"):
        _text(ch, (HX + 6.18, 0, 10.6 - j * 1.75), 1.4, "neon_rose", f"MarcheLettre{j}", rot=(math.pi / 2, 0, -math.pi / 2))


# ------------------------------------------------------------------ brume
def brume(HX, HY):
    rnd = random.Random(21)
    cols = ["orange_equipe", "metal_bleu", "brique", "herbe", "brique_jaune", "peinture_blanche"]

    def container(x, y, z, rotz, name):
        c = cols[rnd.randrange(len(cols))]
        block((6.0, 2.4, 2.6), (x, y, z + 1.3), c, name, 0.04, rot=(0, 0, rotz))
        for r in range(9):   # nervures de la tôle
            dx = -2.6 + r * 0.65
            block((0.08, 2.46, 2.4), (x + dx * math.cos(rotz), y + dx * math.sin(rotz), z + 1.3), c, f"{name}N{r}", 0.0, rot=(0, 0, rotz))
    k = 0
    for sx in (-1, 1):   # piles de conteneurs au-delà des bouts du terrain
        for col in range(3):
            for lv in range(rnd.randint(1, 3)):
                container(sx * (HX + 7 + lv * 0.1), -7 + col * 7, lv * 2.6, math.pi / 2, f"Conteneur{k}")
                k += 1
    # portique de quai rouge, au nord, au-dessus d'un conteneur suspendu
    gy = HY + 9
    for i, gx in enumerate((-12, 12)):
        for dy in (-3, 3):
            _beam((gx, gy + dy, 0), (gx, gy + dy, 22), 0.4, "brique", f"PortiqueJambe{i}{dy}")
    block((34, 3.0, 2.2), (0, gy - 6, 23), "brique", "PortiquePoutre", 0.1)
    block((30, 1.2, 1.4), (0, gy + 3, 23.4), "brique", "PortiquePoutreArriere", 0.1)
    block((4, 3.6, 3.2), (-2, gy - 6, 20.6), "peinture_blanche", "PortiqueChariot", 0.08)
    for dx in (-1.2, 1.2):
        _beam((-2 + dx, gy - 6, 19.0), (-2 + dx, gy - 6, 10.0), 0.04, "metal_sombre", f"PortiqueCable{dx}")
    container(-2, gy - 6, 7.4, 0.0, "ConteneurSuspendu")
    # bittes d'amarrage et cordages le long du côté sud
    for i in range(6):
        x = -HX + 3 + i * (2 * HX - 6) / 5
        tube(0.32, 0.8, (x, -HY - 3.4, 0.4), "metal_sombre", f"Bitte{i}", 12)
        dome(0.38, (x, -HY - 3.4, 0.82), "capuchon", f"BitteTete{i}", scale=(1, 1, 0.5))
    # phare rayé au loin, lanterne allumée
    lx, ly = -HX - 24, -HY - 30
    for j in range(6):
        tube(2.4 - j * 0.15, 3.0, (lx, ly, 1.5 + j * 3.0), "peinture_blanche" if j % 2 == 0 else "orange_equipe", f"Phare{j}", 20)
    tube(1.4, 2.0, (lx, ly, 19.0), "neon_jaune", "PhareLanterne", 16)
    _cone(1.8, 2.2, (lx, ly, 20.0), "capuchon", "PhareToit")
    for i in range(3):   # mouettes stylisées (deux ailes en V)
        x, y, z = rnd.uniform(-20, 20), HY + rnd.uniform(4, 14), rnd.uniform(14, 20)
        for s in (-1, 1):
            block((0.9, 0.12, 0.05), (x + s * 0.4, y, z), "peinture_blanche", f"Mouette{i}{s}", 0.0, rot=(0, -s * 0.35, 0))


SIGNATURES = {"chantier": chantier, "coucher": coucher, "nuit": nuit, "brume": brume}
