"""Immeubles détaillés de la ville autour de l'arène (DA rétro années 50-60 : brique, béton crème, corniches, châteaux d'eau).

Chaque façade est construite en volumes : piliers et allèges en saillie, vitrage en retrait (vraies ombres dans les
embrasures), rez-de-chaussée commerçant avec auvent et vitrine, corniche à gradins, couronne art déco ou château d'eau,
escalier de secours sur un côté. Peinture des sommets comme les props (couleur, occlusion, rugosité dans l'alpha).

Six types sont cuits une fois, puis placés en copies liées (glTF : maillage partagé) sur les deux premiers anneaux de la
ville ; les anneaux lointains restent des blocs simples (map_toit.build_ville).

Repère d'un immeuble : origine au pied, centre ; façade principale vers -Y.
"""
import math
import random

import bpy
from mathutils import Vector

import lib
from props_kit import C, Prop, TAU, box, cyl, mix, sstep, text_mesh

FLOOR = 3.3
QUEST = False   # version Quest : sans meneaux, escaliers de secours, auvents rayés ni bric-à-brac de toit


def _glass(P, w, h, loc):
    """Vitrage en retrait : bleu nuit brillant, reflet de ciel en haut."""
    def g(p, n, f):
        return mix(C("#1E2A48"), C("#5F7FB0"), sstep(loc[2] - h / 2, loc[2] + h / 2, p.z) * 0.6)
    P.add(box((w, 0.1, h), loc, bev=0.0, seg=1), g, wear=0, grime=0, noise_amt=0.0, rough=0.12)


def facade(P, rnd, w, floors, y, wall, trim, bay=2.6, depth=0.35, lit=None, rot=0.0, cx=0.0):
    """Façade d'un côté : piliers verticaux et allèges horizontales en saillie de `depth` devant un vitrage continu.
    rot : angle autour de Z (0 = face -Y). Retourne le nombre de baies."""
    nb = max(2, int(w / bay))
    bw = w / nb
    H = floors * FLOOR
    ca, sa = math.cos(rot), math.sin(rot)

    def at(x, yy, z):
        return (cx + x * ca - yy * sa, x * sa + yy * ca, z)

    def add(size, loc, col, **kw):
        sx, sy, sz = size
        if abs(sa) > 0.5:
            sx, sy = sy, sx
        P.add(box((sx, sy, sz), at(*loc), bev=kw.pop("bev", 0.04), seg=1), col, **kw)
    z0 = FLOOR * 1.25   # rez-de-chaussée plus haut
    # vitrage continu des étages, posé en retrait
    gw, gh = w - 0.2, H - z0
    gx, gy, gz = at(0, y + depth, z0 + gh / 2)
    sx, sy = (gw, 0.1) if abs(sa) < 0.5 else (0.1, gw)
    P.add(box((sx, sy, gh), (gx, gy, gz), bev=0.0, seg=1), lambda p, n, f: mix(C("#121A30"), C("#2E4672"), 0.5 + 0.5 * math.sin(p.z * 0.37 + p.x * 0.21)),
          wear=0, grime=0, noise_amt=0.0, rough=0.22)
    # meneaux : un montant clair au milieu de chaque baie, devant le vitrage
    for i in range(0 if QUEST else nb):
        x = -w / 2 + (i + 0.5) * bw
        add((0.09, 0.12, gh), (x, y + depth - 0.08, z0 + gh / 2), trim, wear=0.2, grime=0, bev=0.0)
    # piliers
    for i in range(nb + 1):
        x = -w / 2 + i * bw
        add((0.45, depth + 0.2, gh), (x, y + depth / 2, z0 + gh / 2), wall, wear=0.4)
    # allèges + appuis de fenêtre
    for k in range(floors - 1):
        z = z0 + k * FLOOR
        add((w, depth + 0.1, 0.95), (0, y + depth / 2, z + 0.47), wall, wear=0.4)
        add((w + 0.1, depth + 0.3, 0.12), (0, y + 0.02, z + 0.98), trim, wear=0.6, bev=0.03)
    # fenêtres éclairées (lumière chaude douce), quelques-unes
    if lit is not None:
        for k in range(floors - 1):
            for i in range(nb):
                if rnd.random() < 0.18:
                    x = -w / 2 + (i + 0.5) * bw
                    z = z0 + k * FLOOR + 0.95 + (FLOOR - 0.95) / 2
                    lx, ly, lz = at(x, y + depth - 0.08, z)
                    sxx, syy = (bw - 0.6, 0.04) if abs(sa) < 0.5 else (0.04, bw - 0.6)
                    lit.append(box((sxx, syy, FLOOR - 1.2), (lx, ly, lz), bev=0.0, seg=1))
    return nb


def storefront(P, rnd, w, y, wall, awning, rot=0.0, sign=None, cx=0.0):
    """Rez-de-chaussée : bandeau sombre, vitrine éclairée, auvent rayé, enseigne."""
    ca, sa = math.cos(rot), math.sin(rot)

    def at(x, yy, z):
        return (cx + x * ca - yy * sa, x * sa + yy * ca, z)

    def sz(sx, sy, s):
        return (sy, sx, s) if abs(sa) > 0.5 else (sx, sy, s)
    zt = FLOOR * 1.25
    P.add(box(sz(w, 0.5, 0.7), at(0, y + 0.1, zt - 0.35), bev=0.05, seg=1), "#2C2F3E", wear=0.5)
    P.add(box(sz(w - 1.2, 0.1, zt - 1.0), at(0, y + 0.5, (zt - 1.0) / 2 + 0.1), bev=0.0, seg=1),
          lambda p, n, f: mix(C("#F6C56A"), C("#7A5A3A"), sstep(0.2, zt - 1.0, p.z) * 0.7), wear=0, grime=0, noise_amt=0, rough=0.15)
    for x in (-w / 2 + 0.3, w / 2 - 0.3):
        P.add(box(sz(0.6, 0.6, zt), at(x, y + 0.2, zt / 2), bev=0.04, seg=1), wall, wear=0.5)
    # auvent rayé en pente
    n = 1 if QUEST else max(4, int(w / 0.8))
    a1, a2 = C(awning), C("#F2EFE6")
    for i in range(n):
        x = -w / 2 + 0.7 + (i + 0.5) * (w - 1.4) / n
        P.add(box(sz((w - 1.4) / n, 1.4, 0.08), at(x, y - 0.5, zt - 0.9), rot=(math.radians(-18), 0, rot), bev=0.0, seg=1),
              a1 if i % 2 == 0 else a2, wear=0.2, grime=0.3)
    if sign:
        t = text_mesh(sign, size=0.9, extrude=0.08, loc=at(0, y - 0.25, zt - 0.35), rot=(math.pi / 2, 0, rot), name="enseigne")
        P.light(t, kind=rnd.choice(["neon_rose", "neon_cyan", "neon_jaune", "neon_orange"]))


def cornice(P, w, d, H, col, crown=False):
    P.add(box((w + 0.5, d + 0.5, 0.35), (0, 0, H + 0.17), bev=0.06, seg=2), col, wear=0.6)
    P.add(box((w + 0.8, d + 0.8, 0.25), (0, 0, H + 0.47), bev=0.06, seg=2), col, wear=0.6)
    P.add(box((w + 0.2, d + 0.2, 0.9), (0, 0, H + 1.0), bev=0.05, seg=1), col, wear=0.5)
    if crown:   # couronne art déco à gradins
        for k, s in enumerate((0.7, 0.5, 0.3)):
            P.add(box((w * s, d * s, 2.2), (0, 0, H + 1.4 + k * 2.2 + 1.1), bev=0.08, seg=2), col, wear=0.5)
        P.add(cyl(0.12, 6.0, (0, 0, H + 1.4 + 6.6)), "#9AA2B6", wear=0.4)


def water_tower(P, x, y, z):
    for k in range(4):
        a = TAU * k / 4 + TAU / 8
        P.add(cyl(0.1, 2.6, (x + math.cos(a) * 1.0, y + math.sin(a) * 1.0, z)), "#3A3E52", wear=0.6, wear_col="#9A5A3A")
    P.add(cyl(1.35, 0.12, (x, y, z + 2.6), n=20), "#3A3E52", wear=0.6)

    def wood(p, n, f):
        a = math.atan2(p.y - y, p.x - x)
        return C("#9C6A40") * (0.9 + 0.1 * math.sin(a * 40))
    P.add(cyl(1.3, 2.8, (x, y, z + 2.72), n=24), wood, wear=0.3, noise_amt=0.08)
    for hz in (0.6, 1.6, 2.4):
        P.add(cyl(1.33, 0.08, (x, y, z + 2.72 + hz), n=24), "#3A3E52", wear=0.6)
    P.add(cyl(1.45, 1.0, (x, y, z + 5.5), n=24, r2=0.05), "#6A4A36", wear=0.4)


def fire_escape(P, x0, y, floors, w=3.4, rot=0.0, cx=0.0):
    """Escalier de secours (paliers + garde-corps + volées en biais) le long d'une façade latérale."""
    ca, sa = math.cos(rot), math.sin(rot)

    def at(x, yy, z):
        return (cx + x * ca - yy * sa, x * sa + yy * ca, z)

    def sz(sx, sy, s):
        return (sy, sx, s) if abs(sa) > 0.5 else (sx, sy, s)
    col = "#2D3142"
    for k in range(1, floors):
        z = FLOOR * 1.25 + (k - 1) * FLOOR
        P.add(box(sz(w, 1.1, 0.08), at(x0, y - 0.55, z), bev=0.0, seg=1), col, wear=0.8, wear_col="#9A5A3A", grime=0)
        P.add(box(sz(w, 0.05, 0.05), at(x0, y - 1.1, z + 0.95), bev=0.0, seg=1), col, wear=0.8, grime=0)
        for i in range(5):
            P.add(box(sz(0.04, 0.04, 0.95), at(x0 - w / 2 + i * w / 4, y - 1.1, z + 0.47), bev=0.0, seg=1), col, wear=0.5, grime=0)
        if k < floors - 1:
            dz = FLOOR
            ang = math.atan2(dz, w * 0.8)
            P.add(box(sz(math.hypot(w * 0.8, dz), 0.7, 0.07), at(x0, y - 0.6, z + dz / 2), rot=(0, (-1 if k % 2 else 1) * ang, rot), bev=0.0, seg=1),
                  col, wear=0.6, grime=0)


def rooftop_clutter(P, rnd, w, d, H):
    for _ in range(rnd.randint(1, 3)):
        x, y = rnd.uniform(-w / 3, w / 3), rnd.uniform(-d / 3, d / 3)
        P.add(box((rnd.uniform(1.2, 2.2), rnd.uniform(1.0, 1.6), rnd.uniform(0.8, 1.3)), (x, y, H + 1.45 + 0.5), bev=0.06, seg=1), "#B9C0CE", wear=0.7)


STYLES = [
    # mur, garnitures, auvent, enseigne, étages, largeur, profondeur, couronne, château d'eau, escalier
    ("#B5533C", "#E9DCC0", "#D9473A", "DINER", 9, 14, 12, False, True, True),
    ("#D9A441", "#F2EFE6", "#2B8C99", "RADIO", 12, 12, 12, True, False, False),
    ("#C2BCB4", "#5A6180", "#F4C431", "BOWL", 15, 16, 14, True, False, True),
    ("#8E3F30", "#E9DCC0", "#3C78C8", "HOTEL", 11, 13, 11, False, True, False),
    ("#2B8C99", "#F2EFE6", "#FF6A1A", "SODA", 8, 11, 11, False, True, True),
    ("#E9DCC0", "#B5533C", "#3F9A62", "CINE", 13, 15, 13, True, False, False),
]


def building(i, seed=5):
    rnd = random.Random(seed + i * 17)
    wall_h, trim_h, awning, sign, floors, w, d, crown, tower, escape = STYLES[i]
    P = Prop(f"Bat{i}", ao_dist=1.2, ao_strength=0.6, rays=24, grime_h=2.0)
    wall = C(wall_h)

    def wall_fn(p, n, f):   # brique ou enduit, bas plus sali, léger dégradé vertical
        return mix(wall * 0.8, wall, sstep(0.0, 8.0, p.z)) * (0.96 + 0.04 * math.sin(p.z * 7.0))
    H = floors * FLOOR
    lit = []
    # noyau (dos et intérieur), en retrait des façades
    P.add(box((w - 1.0, d - 1.0, H), (0, 0, H / 2), bev=0.0, seg=1), wall_fn, wear=0.3)
    facade(P, rnd, w, floors, -d / 2, wall_fn, trim_h, lit=lit)
    facade(P, rnd, d, floors, -w / 2, wall_fn, trim_h, lit=lit, rot=-math.pi / 2)
    facade(P, rnd, d, floors, -w / 2, wall_fn, trim_h, lit=lit, rot=math.pi / 2)
    storefront(P, rnd, w, -d / 2 - 0.05, wall_fn, awning, sign=sign)
    cornice(P, w, d, H, trim_h, crown)
    if tower:
        water_tower(P, rnd.uniform(-w / 5, w / 5), rnd.uniform(0, d / 5), H + 1.45)
    if escape and not QUEST:
        fire_escape(P, 0, -w / 2 - 0.35, floors, w=min(4.0, d * 0.4), rot=-math.pi / 2)
    if not QUEST:
        rooftop_clutter(P, rnd, w, d, H)
    objs = P.bake()
    if lit:
        m = lib.mat("lumiere_fenetre", "#FFD9A0", emissive=1.2)
        for o in lit:
            o.data.materials.append(m)
        bpy.ops.object.select_all(action="DESELECT")
        for o in lit:
            o.select_set(True)
        bpy.context.view_layer.objects.active = lit[0]
        bpy.ops.object.join()
        lit[0].name = f"Bat{i}_fenetres"
        objs.append(lit[0])
    return objs, (w, d, H)


def place_ring(dist_scale=1.25, rings=((30, 14), (48, 18)), seed=3):
    """Cuit les six types puis les place en copies liées autour de l'arène, façade vers le centre."""
    types = [building(i) for i in range(len(STYLES))]
    rnd = random.Random(seed)
    for t in types:   # gabarits cachés : on ne garde que les copies placées
        for o in t[0]:
            o.hide_render = True
    n = 0
    for dist, count in rings:
        for k in range(count):
            a = k / count * TAU + rnd.uniform(-0.06, 0.06)
            ti = rnd.randrange(len(types))
            objs, (w, d, H) = types[ti]
            dd = dist + rnd.uniform(0, 4) + d / 2
            x, y = math.cos(a) * dd * dist_scale, math.sin(a) * dd
            yaw = math.atan2(-y, -x) + math.pi / 2   # façade (-Y local) tournée vers le centre
            base = rnd.uniform(12, 20)   # pied dans la rue, sous le niveau du toit de l'arène
            for o in objs:
                c = o.copy()   # données partagées
                c.name = f"{o.name}_{n}"
                c.hide_render = False
                c.location = (x, y, -base)
                c.rotation_euler = (0, 0, yaw)
                bpy.context.collection.objects.link(c)
            n += 1
    for t in types:
        for o in t[0]:
            bpy.data.objects.remove(o, do_unlink=True)
    return n
