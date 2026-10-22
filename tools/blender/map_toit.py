"""Carte « Toit » : arène 30 × 16 m sur le toit d'un immeuble, ville en arrière-plan, DA inspirée de Knockout City
(briques chaudes, corniches arrondies, bandes jaune et noir, néons, enseignes, caisses en bois).
Noms, enseignes et motifs sont originaux.

Deux ensembles :  build_toit()  décor proche (jouable)  ·  build_ville()  skyline lointaine (sans collision).
Axes : X = longueur (équipe orange à -X, bleue à +X), Y = largeur, Z = haut. Sol de jeu en Z = 0.
"""
import math
import random

import bpy

import lib
from kc import NEON, arch_cap, block, dome, kmat, tube

HX, HY = 18.0, 15.0   # demi-dimensions de l'aire de jeu (arenaBounds de croix_neon.json ; Blender Y = -Z Godot)
PIT = None            # fosse (demi-largeurs en X et en Y Blender), ou None
HUB = True            # moyeu central (anneau au sol) : cartes en anneau seulement

AMBIANCE = "jour"     # props signature du quartier (signatures.py) ; jour = Croix Néon (tuyaux et enseigne FIZZ)

# Variantes du toit pour les autres cartes : (demi-largeur X, demi-largeur Y, fosse (sx, sz) ou None, moyeu, ambiance)
# L'ambiance doit être la même que le champ « ambiance » de la carte (ciel et soleil côté Godot, ArenaBuilder.Moods).
VARIANTS = {
    "toit": (18.0, 15.0, None, True, "jour"),             # croix_neon : plein jour
    "toit_fosse": (18.0, 15.0, (9, 9), True, "chantier"),   # fosse_croisee : chantier, fin d'après-midi
    "toit_tour": (19.0, 15.0, (16, 10), True, "coucher"),   # tour_4v4 : gratte-ciel au coucher du soleil
    "toit_court": (16.0, 10.0, None, False, "nuit"),      # neon_court : marché de nuit
    "toit_long": (16.0, 10.5, (32, 3), False, "brume"),    # la_fosse : port, matin brumeux ; fosse en bande sur toute la longueur
}


def configure(name):
    """Règle les dimensions globales sur une variante de VARIANTS."""
    global HX, HY, PIT, HUB, AMBIANCE
    HX, HY, pit, HUB, AMBIANCE = VARIANTS[name]
    PIT = None if pit is None else (pit[0] / 2, pit[1] / 2)


def in_pit(x, y, margin=0.4):
    return PIT is not None and abs(x) < PIT[0] + margin and abs(y) < PIT[1] + margin


def hazard_frame(z=0.011):
    """Bande jaune et noire qui borde l'aire de jeu (quatre bandes)."""
    w = 0.7
    block((2 * HX + 2 * w, w, 0.02), (0, HY - w / 2, z), "danger", "BandeN", 0.0)
    block((2 * HX + 2 * w, w, 0.02), (0, -HY + w / 2, z), "danger", "BandeS", 0.0)
    if PIT is not None and PIT[0] >= HX - 0.01:   # fosse en bande : les bandes des bouts s'arrêtent au bord du trou
        L = HY - PIT[1]
        for sx in (-1, 1):
            for sy in (-1, 1):
                block((w, L, 0.02), (sx * (HX - w / 2), sy * (PIT[1] + L / 2), z), "danger", f"Bande{sx}{sy}", 0.0)
        return
    block((w, 2 * HY, 0.02), (HX - w / 2, 0, z), "danger", "BandeE", 0.0)
    block((w, 2 * HY, 0.02), (-HX + w / 2, 0, z), "danger", "BandeO", 0.0)


def street_markings():
    """Marquage routier continu (un seul espace, sans rectangles de zones) : axes en pointillés jaunes, anneau autour du
    moyeu, passages piétons aux quatre bouts, plaques d'égout et grilles. Tout est plat et sans collision."""
    z = 0.012
    for ax in (0, 1):   # axes de la croix : tirets de 2 m tous les 3,6 m
        half = HX if ax == 0 else HY
        n = int((half - 6.0) / 3.6)
        for sgn in (-1, 1):
            for i in range(n):
                d = sgn * (6.6 + i * 3.6)
                size = (2.0, 0.14, 0.02) if ax == 0 else (0.14, 2.0, 0.02)
                loc = (d, 0, z) if ax == 0 else (0, d, z)
                if not in_pit(loc[0], loc[1], 1.2):
                    block(size, loc, "danger_ligne", f"Tiret{ax}{sgn}{i}", 0.0)
    if HUB and (PIT is None or max(PIT) < 4.4):
        for r, kind, w in ((5.6, "danger_ligne", 0.22), (5.1, "peinture_blanche", 0.1)):   # anneau du moyeu
            tube(r + w, 0.016, (0, 0, z - 0.004), kind, f"AnneauExt{r}", 48)
            if PIT is None:
                tube(r - w, 0.02, (0, 0, z - 0.002), "asphalte", f"AnneauInt{r}", 48)
    for sgn in (-1, 1):   # passages piétons au bout de chaque branche
        for ax in (0, 1):
            for j in range(-3, 4):
                if ax == 0:
                    if not in_pit(sgn * (HX - 3.2), j * 0.9):
                        block((2.6, 0.45, 0.02), (sgn * (HX - 3.2), j * 0.9, z), "peinture_blanche", f"Zebre{ax}{sgn}{j}", 0.0)
                else:
                    block((0.45, 2.6, 0.02), (j * 0.9, sgn * (HY - 3.2), z), "peinture_blanche", f"Zebre{ax}{sgn}{j}", 0.0)
    for i, (x, y) in enumerate([(-12, 6), (12, -6), (-6, -11), (6, 11), (13, 9), (-13, -9)]):   # plaques d'égout
        x, y = x * HX / 18, y * HY / 15
        if in_pit(x, y, 1.0):
            continue
        tube(0.62, 0.022, (x, y, z), "metal_sombre", f"Egout{i}", 20)
        tube(0.45, 0.026, (x, y, z), "capuchon", f"EgoutCentre{i}", 20)
    for i, (x, y) in enumerate([(-3, 12.5), (3, -12.5), (-14.5, 0.0), (14.5, 0.0)]):   # grilles d'évacuation
        x, y = x * HX / 18, y * HY / 15
        if in_pit(x, y, 1.0):
            continue
        block((1.6, 0.7, 0.02), (x, y, z), "metal_sombre", f"Grille{i}", 0.0)
        for s in range(5):
            block((0.08, 0.6, 0.026), (x - 0.6 + s * 0.3, y, z), "capuchon", f"GrilleBarre{i}{s}", 0.0)


def roof():
    W, D = 2 * HX + 14, 2 * HY + 12
    if PIT is None:
        block((W, D, 1.2), (0, 0, -0.6), "asphalte", "Dalle", 0.06)
    else:
        pit_shaft(W, D)
    hazard_frame()
    street_markings()


def pit_shaft(W, D, depth=7.0):
    """Toit percé : dalle en quatre morceaux autour de la fosse, puits en briques sombres, fond noir et néons d'alerte.
    Une fosse aussi longue que l'aire de jeu traverse tout le toit (la_fosse)."""
    px, py = PIT
    if px >= HX - 0.01:
        px = W / 2
    block((W, D / 2 - py, 1.2), (0, (D / 2 + py) / 2, -0.6), "asphalte", "DalleN", 0.06)
    block((W, D / 2 - py, 1.2), (0, -(D / 2 + py) / 2, -0.6), "asphalte", "DalleS", 0.06)
    if px < W / 2:
        block((W / 2 - px, 2 * py, 1.2), ((W / 2 + px) / 2, 0, -0.6), "asphalte", "DalleE", 0.06)
        block((W / 2 - px, 2 * py, 1.2), (-(W / 2 + px) / 2, 0, -0.6), "asphalte", "DalleO", 0.06)
    t = 0.5
    for sy in (-1, 1):   # parois du puits
        block((2 * px + 2 * t, t, depth), (0, sy * (py + t / 2), -depth / 2), "brique", f"PuitsL{sy}", 0.04)
        block((2 * px, 0.08, 0.14), (0, sy * (py - 0.04), -0.5), "neon_orange", f"PuitsNeonL{sy}", 0.0)
        for k in range(int(2 * px / 2.4)):   # bandes de danger sur la paroi
            block((1.0, 0.04, 0.5), (-px + 1.2 + k * 2.4, sy * (py - 0.02), -1.1), "danger", f"PuitsBande{sy}_{k}", 0.0)
    for sx in (-1, 1):
        block((t, 2 * py, depth), (sx * (px + t / 2), 0, -depth / 2), "brique", f"PuitsP{sx}", 0.04)
        block((0.08, 2 * py, 0.14), (sx * (px - 0.04), 0, -0.5), "neon_orange", f"PuitsNeonP{sx}", 0.0)
    block((2 * px, 2 * py, 0.3), (0, 0, -depth), "metal_sombre", "PuitsFond", 0.0)
    for k in range(-2, 3):   # tuyaux et lueur au fond
        block((2 * px, 0.25, 0.25), (0, k * py * 0.35, -depth + 0.3), "tuyau", f"PuitsTuyau{k}", 0.05)


def parapets():
    """Parapet de brique avec capuchon arrondi sombre et piliers, comme les toits du trailer."""
    h, t = 1.1, 0.7
    for sy in (-1, 1):
        block((2 * HX + 2.8, t, h), (0, sy * (HY + 1.45), h / 2), "brique", f"ParapetL{sy}", 0.05)
        arch_cap(2 * HX + 3.0, (0, sy * (HY + 1.45), h + 0.12), f"CapL{sy}", "x")
    for sx in (-1, 1):
        block((t, 2 * HY + 2.8, h), (sx * (HX + 1.45), 0, h / 2), "brique", f"ParapetP{sx}", 0.05)
        arch_cap(2 * HY + 3.0, (sx * (HX + 1.45), 0, h + 0.12), f"CapP{sx}", "y")
    # liserés néon sur la face intérieure : cyan sur les longs côtés, couleur d'équipe aux extrémités
    for sy in (-1, 1):
        block((2 * HX, 0.08, 0.12), (0, sy * (HY + 1.45 - 0.37), 0.75), "neon_cyan", f"NeonL{sy}", 0.0)
    block((0.08, 2 * HY, 0.12), (-HX - 1.45 + 0.37, 0, 0.75), "neon_orange", "NeonO", 0.0)
    block((0.08, 2 * HY, 0.12), (HX + 1.45 - 0.37, 0, 0.75), "neon_cyan", "NeonB", 0.0)
    # piliers de jaune sur les côtés longs
    for sy in (-1, 1):
        for i in range(-4, 5):
            block((0.9, 0.95, h + 0.25), (i * 4.4, sy * (HY + 1.45), (h + 0.25) / 2), "brique_jaune", f"Pilier{sy}_{i}", 0.05)
            tube(0.36, 0.5, (i * 4.4, sy * (HY + 1.45), h + 0.3), "capuchon", f"PilierCap{sy}_{i}", 12)


def tower(x, y, w, d, h, name, facing=1, sign=None):
    """Cage d'escalier en briques à bandes jaunes, porte, fenêtre éclairée, toit à capuchon."""
    block((w, d, h), (x, y, h / 2), "brique", name, 0.06)
    for k in (0.3, 0.62):
        block((w + 0.08, d + 0.08, 0.35), (x, y, h * k), "brique_jaune", f"{name}_bande{k}", 0.03)
    arch_cap(w + 0.4, (x, y, h + 0.15), f"{name}_toit", "x")
    arch_cap(d + 0.4, (x, y, h + 0.15), f"{name}_toit2", "y")
    fy = y - facing * (d / 2 + 0.02)
    block((1.1, 0.06, 2.0), (x, fy, 1.0), "metal_sombre", f"{name}_porte", 0.03)
    block((1.0, 0.06, 0.9), (x - w * 0.25, fy, h * 0.62 + 0.9), "neon_jaune", f"{name}_fen", 0.0)
    block((w * 0.8, 0.08, 0.5), (x, fy, h * 0.9), "metal_bleu", f"{name}_enseigne", 0.03)
    block((w * 0.8 - 0.2, 0.09, 0.08), (x, fy, h * 0.9), "neon_cyan", f"{name}_neon", 0.0)


def towers():
    tower(-HX - 4.5, -HY - 1.0, 4.2, 3.6, 6.2, "TourO1", 1)
    tower(HX + 4.5, -HY - 1.0, 4.2, 3.6, 5.4, "TourB1", 1)
    tower(-HX - 4.5, HY + 1.0, 4.2, 3.6, 5.0, "TourO2", -1)
    tower(HX + 4.5, HY + 1.0, 4.2, 3.6, 6.6, "TourB2", -1)


def crates():
    rnd = random.Random(7)
    spots = [(-8, HY + 3.4, 1), (-6.8, HY + 3.9, 1), (-7.6, HY + 3.7, 2), (9, -HY - 3.4, 1), (10.3, -HY - 3.9, 1), (9.5, -HY - 3.6, 2), (-12, -HY - 3.5, 1), (14, HY + 3.5, 1)]
    for i, (x, y, lv) in enumerate(spots):
        s = 1.25
        block((s, s, s), (x, y, s / 2 + (lv - 1) * s - 0.0), "bois", f"Caisse{i}", 0.05, rot=(0, 0, rnd.uniform(-0.25, 0.25)))
        block((s + 0.06, s + 0.06, 0.1), (x, y, (lv - 1) * s + 0.05), "metal_sombre", f"CaisseBas{i}", 0.0)
    block((6.0, 2.4, 2.5), (-1.5, -HY - 5.0, 1.25), "metal_bleu", "Conteneur", 0.05)
    block((6.05, 0.06, 0.3), (-1.5, -HY - 3.78, 2.0), "danger", "ConteneurBande", 0.0)


COVERS = [  # (x, z Godot, base y, sx, sy, sz) tirés de maps/croix_neon.json
    (0.3, -7.2, 3.2, 1.3, 0.25), (1.8, -8.5, 0.25, 1.3, 3.2), (-8.3, -7.2, 3.2, 1.3, 0.25), (-9.8, -8.5, 0.25, 1.3, 3.2),
    (7.2, 0.3, 0.25, 1.3, 3.2), (8.5, 1.8, 3.2, 1.3, 0.25), (7.2, -8.3, 0.25, 1.3, 3.2), (8.5, -9.8, 3.2, 1.3, 0.25),
    (-0.3, 7.2, 3.2, 1.3, 0.25), (-1.8, 8.5, 0.25, 1.3, 3.2), (8.3, 7.2, 3.2, 1.3, 0.25), (9.8, 8.5, 0.25, 1.3, 3.2),
    (-7.2, -0.3, 0.25, 1.3, 3.2), (-8.5, -1.8, 3.2, 1.3, 0.25), (-7.2, 8.3, 0.25, 1.3, 3.2), (-8.5, 9.8, 3.2, 1.3, 0.25),
]
PILLARS = [(-4, -9), (9, -4), (4, 9), (-9, 4)]
ZONES = [(-4, -8.5, "orange_equipe"), (8.5, -4, "bleu_equipe"), (4, 8.5, "orange_equipe"), (-8.5, 4, "bleu_equipe")]
DISPENSERS = [(-1.5, -10), (10, -1.5), (1.5, 10), (-10, 1.5)]


def gameplay_preview():
    """Reproduit les éléments de jeu construits par le code (abris, pilier, moyeu, distributeurs, zones) pour les rendus."""
    for x, z, w, h, d in [(c[0], c[1], c[2], c[3], c[4]) for c in COVERS]:
        i = len(bpy.data.objects)
        block((w, d, h), (x, -z, h / 2), "beton_clair", f"Abri{i}", 0.08)
        block((w + 0.02, d + 0.02, 0.1), (x, -z, h + 0.04), "capuchon", f"AbriHaut{i}", 0.02)
    for x, z in PILLARS:
        block((0.8, 0.8, 2.2), (x, -z, 1.1), "metal_bleu", f"Pilier{x}{z}", 0.05)
        block((0.9, 0.9, 0.12), (x, -z, 2.25), "neon_cyan", f"PilierNeon{x}{z}", 0.0)
    for i, (x, z) in enumerate(DISPENSERS):
        tube(0.3, 1.0, (x, -z, 0.5), "metal_sombre", f"Distrib{i}", 12)
        tube(0.65, 0.05, (x, -z, 0.025), "metal_bleu", f"DistribPlot{i}", 6)
        tube(0.5, 0.012, (x, -z, 1.01), "neon_cyan", f"DistribAnneau{i}", 6)
    # moyeu : sol 9 × 9, pylône 1,6 × 3,4, rotor à 4 bras
    block((1.6, 1.6, 3.4), (0, 0, 1.7), "metal_bleu", "Pylone", 0.06)
    block((1.9, 1.9, 0.2), (0, 0, 3.45), "neon_cyan", "PyloneAnneau", 0.0)
    tube(0.33, 0.4, (0, 0, 4.0), "neon_jaune", "Lanceur", 14)
    for i in range(4):
        a = i * math.pi / 2
        block((4.0, 0.4, 0.4), (math.cos(a) * 2.2, math.sin(a) * 2.2, 1.8), "metal_sombre", f"Bras{i}", 0.05, rot=(0, 0, a))
        block((0.5, 0.5, 0.5), (math.cos(a) * 4.0, math.sin(a) * 4.0, 1.8), "orange_equipe" if i % 2 == 0 else "bleu_equipe", f"Pointe{i}", 0.05)


def clutter():
    """Réservoir d'eau sur pilotis, blocs de climatisation et lampadaires : le désordre d'un vrai toit."""
    x, y = HX + 11, -HY - 8
    for dx in (-1.2, 1.2):
        for dy in (-1.2, 1.2):
            block((0.25, 0.25, 3.0), (x + dx, y + dy, 1.5), "metal_sombre", f"PilotisR{dx}{dy}", 0.02)
    tube(1.9, 2.8, (x, y, 4.4), "bois", "Reservoir", 20)
    tube(1.95, 0.25, (x, y, 5.9), "capuchon", "ReservoirToit", 20, rot=(0, 0, 0))
    tube(0.25, 0.9, (x, y, 6.5), "metal_sombre", "ReservoirPointe", 8)
    for i, (cx, cy) in enumerate([(-HX - 9, 0), (-HX - 9, 5), (HX + 9, 3), (HX + 10, -4)]):
        block((2.2, 1.6, 1.4), (cx, cy, 0.7), "metal_bleu", f"Clim{i}", 0.06)
        tube(0.55, 0.12, (cx, cy, 1.45), "metal_sombre", f"ClimVentilo{i}", 14)
    for i, (px, py) in enumerate([(-HX - 2.6, -6), (-HX - 2.6, 6), (HX + 2.6, -6), (HX + 2.6, 6), (0, -HY - 2.8), (0, HY + 2.8)]):
        tube(0.09, 5.2, (px, py, 2.6), "metal_sombre", f"Poteau{i}", 8)
        block((0.9, 0.35, 0.15), (px, py, 5.3), "metal_sombre", f"Lampe{i}", 0.02)
        block((0.7, 0.25, 0.08), (px, py, 5.2), "neon_jaune", f"Ampoule{i}", 0.0)


def pipes():
    """Gros tuyaux orange lumineux au-dessus du terrain (les tubes de la ruelle) : coude en quart de cercle + colliers blancs."""
    for k in range(7):
        a = k * math.radians(14)
        x, y = -HX + 3 + math.sin(a) * 9.0, HY + 8.0
        z = 7.0 + (1 - math.cos(a)) * 9.0
        tube(0.6, 2.6, (x, y, z), "tuyau", f"Tuyau{k}", 16, rot=(0, math.pi / 2 - a, 0))
        if k % 2 == 0:
            tube(0.68, 0.32, (x, y, z), "peinture_blanche", f"Collier{k}", 16, rot=(0, math.pi / 2 - a, 0))


def sign_fizz(x, y, z, face=-1):
    """Enseigne « FIZZ » originale : médaillon rayé et panneau néon (aucun logo existant)."""
    block((7.0, 0.5, 3.2), (x, y, z), "capuchon", "EnseigneFizz", 0.12)
    block((6.5, 0.2, 2.7), (x, y + face * 0.3, z), "metal_bleu", "EnseigneFace", 0.1)
    block((6.5, 0.22, 0.12), (x, y + face * 0.32, z + 1.2), "neon_orange", "EnseigneLiserePlus", 0.0)
    block((6.5, 0.22, 0.12), (x, y + face * 0.32, z - 1.2), "neon_orange", "EnseigneLiseréMoins", 0.0)
    dome(1.0, (x - 2.2, y + face * 0.45, z), "neon_rose", "EnseigneBille")
    bpy.ops.object.text_add(location=(x + 0.8, y + face * 0.45, z - 0.35), rotation=(math.pi / 2, 0, math.pi if face > 0 else 0))
    t = bpy.context.active_object
    t.data.body, t.data.size, t.data.extrude, t.data.align_x = "FIZZ", 1.6, 0.05, "CENTER"
    bpy.ops.object.convert(target="MESH")
    t = bpy.context.active_object
    t.name = "EnseigneTexte"
    t.data.materials.clear()
    t.data.materials.append(kmat("neon_jaune"))


def build_toit(preview=False):
    roof()
    parapets()
    towers()
    crates()
    clutter()
    if AMBIANCE == "jour":
        pipes()
        sign_fizz(4, HY + 14, 10)
    else:
        import signatures
        signatures.SIGNATURES[AMBIANCE](HX, HY)
    if preview:
        gameplay_preview()


# --------------------------------------------------------------- ville lointaine
def build_ville(seed=3):
    rnd = random.Random(seed)
    kinds = ["fenetres", "fenetres", "fenetres", "brique", "brique_jaune", "beton"]
    neons = list(NEON)
    n = 0
    import city_kit
    # premiers anneaux : immeubles détaillés (façades en volume, enseignes, châteaux d'eau) ; Quest : le premier seulement
    near = ((30, 14),) if city_kit.QUEST else ((30, 14), (48, 18))
    city_kit.place_ring(rings=near)
    far = [(72, 22), (105, 22)] if not city_kit.QUEST else [(48, 18), (72, 22), (105, 14)]
    for ring, (dist, count) in enumerate(far, start=len(near)):
        for i in range(count):
            a = i / count * math.tau + rnd.uniform(-0.1, 0.1)
            d = dist + rnd.uniform(-3, 5)
            w, dp = rnd.uniform(9, 16), rnd.uniform(9, 16)
            h = rnd.uniform(22, 38) + ring * 16 + rnd.uniform(0, 14)
            top = rnd.uniform(6, 24) if ring == 0 else rnd.uniform(0, 30) + ring * 5
            x, y = math.cos(a) * d * 1.25, math.sin(a) * d
            k = kinds[rnd.randrange(len(kinds))]
            block((w, dp, h), (x, y, top - h / 2), k, f"Immeuble{n}", 0.1)
            if rnd.random() < 0.5:   # couronne arrondie, signe des immeubles art déco
                c = tube(dp / 2 * 0.95, w, (x, y, top), "capuchon", f"Couronne{n}", 16, rot=(0, math.pi / 2, 0))
                c.scale = (0.16, 1, 1)   # demi-cylindre bas : la corniche arrondie, pas un tambour
            else:
                block((w + 0.6, dp + 0.6, 0.8), (x, y, top + 0.4), "capuchon", f"Toit{n}", 0.05)
            if rnd.random() < 0.6:   # retrait supérieur
                block((w * 0.6, dp * 0.6, 4.0), (x, y, top + 2.6), k, f"Retrait{n}", 0.08)
            nk = neons[rnd.randrange(len(neons))]
            sgn = -1 if y > 0 else 1
            block((w + 0.2, 0.3, 0.3), (x, y + sgn * (dp / 2 + 0.12), top - 2.0), nk, f"Neon{n}", 0.0)
            block((0.3, 0.3, min(h * 0.6, 14)), (x + w / 2 + 0.12, y + sgn * (dp / 2 + 0.12), top - min(h * 0.3, 7)), nk, f"NeonV{n}", 0.0)
            n += 1
    for k in range(4):   # tour radio (treillis)
        a = k * math.pi / 2 + math.pi / 4
        tube(0.14, 30, (math.cos(a) * 1.6 + 38, math.sin(a) * 1.6 + 34, 15), "metal_sombre", f"RadioPied{k}", 6)
    for h in range(3, 30, 4):
        block((3.6, 0.12, 0.12), (38, 35.6, h), "metal_sombre", f"RadioBarre{h}", 0.0)
        block((0.12, 3.6, 0.12), (39.6, 34, h + 2), "metal_sombre", f"RadioBarreB{h}", 0.0)
    dome(0.7, (38, 34, 31), "neon_rose", "RadioLampe")
