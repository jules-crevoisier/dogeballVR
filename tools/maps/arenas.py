#!/usr/bin/env python3
"""Génère les arènes libres du PC (maps/*.json, layout « arena »).

Chaque carte est décrite pour l'équipe orange (côté -Z), puis copiée par symétrie centrale (x, z) -> (-x, -z) pour l'équipe
bleue : les deux camps ont exactement le même terrain, tourné d'un demi-tour (même flanc fort de chaque côté).
Usage : python3 tools/maps/arenas.py   (réécrit maps/marche_suspendu.json, maps/piscine.json)
Les cotes sont en mètres ; le sol praticable le plus bas est à y = 0 (règle d'ArenaGeometry : pas de hauteur négative).
"""
import json
import math
import os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

COMMON_RULES = {
    "invulnerableAfterHitSeconds": 0.4,
    "respawnProtectionSeconds": 1.5,
    "heartRegenSeconds": 10,
    "pcHeartRegenSeconds": 0,
    "koTarget": 10,
}
COMMON_BALL_RULES = {"maxBallsInPlay": 7, "maxFlightSeconds": 1.6, "ballGravityFactor": 0.3, "ballMagnetRadius": 0.5}


class Arena:
    def __init__(self):
        self.blocks, self.ramps, self.pads, self.spawns, self.decor, self.balls = [], [], [], [], [], []

    # --- pièces décrites pour l'équipe orange ; mirror=True ajoute la copie bleue
    def block(self, id, kind, x, z, sx, sz, y1, y0=None, rot=0, mirror=True, color_b=None, **extra):
        """color_b : couleur de la copie bleue (accents d'équipe) ; sinon la même que l'orange."""
        y0 = (y1 - {"roof": 10, "floor": 10, "deck": 10, "bridge": 0.6}.get(kind, y1)) if y0 is None else y0
        b = {"id": id, "kind": kind, "x": x, "z": z, "sx": sx, "sz": sz, "y0": y0, "y1": y1, "rotY": rot, **extra}
        self.blocks.append(b)
        if mirror:
            m = {**b, "id": id + "_b", "x": -x, "z": -z, "rotY": rot}
            if color_b: m["color"] = color_b
            self.blocks.append(m)

    def ramp(self, id, x, z, sx, sz, h0, h1, dir, stairs=False, mirror=True):
        r = {"id": id, "x": x, "z": z, "sx": sx, "sz": sz, "h0": h0, "h1": h1, "dir": dir, "stairs": stairs}
        self.ramps.append(r)
        if mirror:
            flip = {"+x": "-x", "-x": "+x", "+z": "-z", "-z": "+z"}[dir]
            self.ramps.append({**r, "id": id + "_b", "x": -x, "z": -z, "dir": flip})

    def pad(self, id, x, z, y, tx, tz, ty, apex=2.2, mirror=True):
        p = {"id": id, "x": x, "z": z, "y": y, "radius": 0.8, "tx": tx, "tz": tz, "ty": ty, "apex": apex, "cooldownSeconds": 2}
        self.pads.append(p)
        if mirror:
            self.pads.append({**p, "id": id + "_b", "x": -x, "z": -z, "tx": -tx, "tz": -tz})

    def spawn(self, x, z):
        # orange regarde vers +Z (180°), bleu vers -Z (0°) ; les places alternent orange/bleu pour que 2v2, 3v3 et 4v4
        # prennent les premières places de chaque équipe dans l'ordre.
        self.spawns.append({"team": 0, "x": x, "z": z, "facingDegrees": 180})
        self.spawns.append({"team": 1, "x": -x, "z": -z, "facingDegrees": 0})

    def ball(self, id, x, z, y, interval=12, mirror=True):
        b = {"id": id, "kind": "pedestal", "zone": -1, "x": x, "y": y, "z": z, "intervalSeconds": interval, "maxStored": 1, "startBalls": 1}
        self.balls.append(b)
        if mirror:
            self.balls.append({**b, "id": id + "_b", "x": -x, "z": -z})

    def deco(self, mirror=True, color_b=None, **d):
        self.decor.append(d)
        if mirror:
            m = dict(d)
            m["x"], m["z"] = -d["x"], -d["z"]
            m["rotY"] = d.get("rotY", 0) + 180
            if color_b: m["color"] = color_b
            self.decor.append(m)

    def skyline(self, bounds, seed, colors, ring=10, base=-14, brick=True, max_h=30):
        """Immeubles autour de l'arène (décor sans collision) : fenêtres, hauteurs variées, à 8-30 m des bords."""
        import random
        rnd = random.Random(seed)
        x0, x1, z0, z1 = bounds
        spots = []
        for k in range(ring):
            t = k / ring
            for side in range(4):
                far = rnd.uniform(16, 34)
                if side == 0: x, z = x0 + (x1 - x0) * t, z0 - far
                elif side == 1: x, z = x1 + far, z0 + (z1 - z0) * t
                elif side == 2: x, z = x1 - (x1 - x0) * t, z1 + far
                else: x, z = x0 - far, z1 - (z1 - z0) * t
                spots.append((x, z, far))
        for i, (x, z, far) in enumerate(spots):
            w, d = rnd.uniform(6, 11), rnd.uniform(6, 11)
            h = rnd.uniform(4, 12) if far < 22 else rnd.uniform(10, max_h)   # proches : bas (ne cachent pas l'arène) ; loin : la silhouette
            c1, c2 = colors[i % len(colors)]
            self.decor.append({"x": round(x, 1), "y": base, "z": round(z, 1), "sx": round(w, 1), "sy": round(h - base, 1), "sz": round(d, 1),
                               "color": c1, "color2": c2, "pattern": 5 if (i % 3 or not brick) else 1})

    def check_heights(self):
        """Règle de lecture : un couvert bas s'enjambe (1,2 m ou moins), un couvert haut est un mur (1,65 m ou plus).
        Entre les deux, un humain (saut 1,04 m + marche 0,45) monte et un bot (NavGrid.MaxJumpUp 1,3) ne monte pas."""
        for b in self.blocks:
            h = b["y1"] - b["y0"]
            if b["kind"] in ("cover", "low", "crate") and 1.25 < h < 1.65:
                raise SystemExit(f"{b['id']} : hauteur {h:.2f} m ambiguë (ni enjambable ni mur)")

    def write(self, name, meta):
        self.check_heights()
        data = {
            "name": name, "schemaVersion": 4, "layout": "arena", **meta,
            "rules": {**COMMON_RULES, **meta.get("rules", {})},
            "ballRules": {**COMMON_BALL_RULES, **meta.get("ballRules", {})},
            "ballReturn": {"seconds": 3},
            "arena": {**meta.get("arena", {}), "blocks": self.blocks, "ramps": self.ramps, "pads": self.pads, "spawns": self.spawns, "decor": self.decor},
            "ballSpawns": self.balls,
        }
        path = os.path.join(ROOT, "maps", name + ".json")
        with open(path, "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=1)
        print(f"{path} : {len(self.blocks)} blocs, {len(self.ramps)} rampes, {len(self.pads)} tremplins, {len(self.spawns)} places, {len(self.balls)} balles")


def garland(a, x0, z0, x1, z1, y, sag=0.6, step=1.4, color="#FFC46A"):
    """Guirlande d'ampoules entre deux points (décor sans collision) : petites ampoules espacées qui pendent en arc, pas un néon continu."""
    import math
    n = max(2, int(math.hypot(x1 - x0, z1 - z0) / step))
    for k in range(n + 1):
        t = k / n
        a.deco(mirror=False, x=round(x0 + (x1 - x0) * t, 2), y=round(y - sag * 4 * t * (1 - t), 2), z=round(z0 + (z1 - z0) * t, 2),
               sx=0.13, sy=0.16, sz=0.13, color=color, glow=1.3)


ORANGE, BLEU = "#E36A2E", "#2FA8E0"   # accents d'équipe (copie bleue via color_b)


def marche_suspendu():
    """
    Toits du marché : deux toits face à face au-dessus d'une rue de marché (le vide, ring-out, 8 m de bord à bord), reliés
    par un pont central coupé en deux couloirs par une charrette et par deux planches étroites. Idée : tenir les ponts.
    Chaque toit a une terrasse du fond (1,2 m, départ), un front avec deux couverts pleins (conteneur, cage d'escalier) et
    des demi-couverts (clim, caisses), un château d'eau en repère, et une passerelle d'enseigne haute (3 m) au bord du vide,
    atteinte par un tremplin : la meilleure vue, mais on y est exposé et à un tacle de la chute.
    Densité visée : environ 170 m² par joueur en 3 contre 3 (portée utile des lancers 10-15 m).
    """
    a = Arena()
    # Toits (côté orange z < 0) : x -17..17, z -18..-4. Le vide de la rue va de z = -4 à z = 4.
    a.block("toit", "roof", 0, -11, 34, 14, 0)
    a.block("parapet_fond", "parapet", 0, -18.2, 34.8, 0.4, 3.0, 0)
    a.block("parapet_g", "parapet", -17.2, -11, 0.4, 14, 1.6, 0)
    a.block("parapet_d", "parapet", 17.2, -11, 0.4, 14, 1.6, 0)
    # Terrasse du fond (apparition) et ses deux rampes latérales.
    a.block("terrasse", "deck", 0, -15.5, 12, 5, 1.2)
    a.ramp("rampe_terrasse_g", -7.5, -16, 3, 3, 0, 1.2, "+x")
    a.ramp("rampe_terrasse_d", 7.5, -16, 3, 3, 0, 1.2, "-x")
    a.block("muret_terrasse", "low", 0, -13.2, 5, 0.4, 2.2, 1.2)
    # Château d'eau : repère visible de partout, couvert plein (4,5 m).
    a.block("chateau_eau", "tower", -13, -15, 3.2, 3.2, 4.5, 0, color="#7A5A3A")   # réservoir en bois cerclé, toit conique
    for yy in (1.2, 2.4, 3.6):
        a.deco(x=-13, y=yy, z=-15, sx=3.3, sy=0.12, sz=3.3, color="#3A3F4A", pattern=0)
    for yy, w in ((4.5, 2.8), (4.9, 1.8), (5.3, 0.7)):
        a.deco(x=-13, y=yy, z=-15, sx=w, sy=0.4, sz=w, color="#5A6180", pattern=0)
    a.block("cheminee", "wall", 14, -15, 1.6, 2.4, 2.4, 0)
    # Front : deux couverts pleins à moins de 3 m du bord (conteneur, cage d'escalier), demi-couverts par-dessus lesquels on tire.
    a.block("conteneur", "container", -7, -8, 6, 2.4, 2.6, 0, color=ORANGE, color_b=BLEU)
    a.block("cage_escalier", "wall", 6, -8.5, 2.6, 2.6, 2.4, 0)
    a.block("clim_1", "cover", -15.2, -7.2, 2.4, 1.6, 1.2, 0, model="clim")   # laisse 4 m de sortie à la planche
    a.block("caisses_1", "crate", -1.5, -6, 2, 1, 0.9, 0)
    a.block("caisses_2", "crate", 15, -9, 1.4, 1.4, 1.0, 0, model="caisse_bois")
    a.block("clim_2", "cover", 4, -12.5, 2, 2, 1.2, 0, model="clim")
    a.block("lanterneau", "low", 0.5, -11, 3, 2, 0.8, 0)
    # Passerelle d'enseigne : haute (3 m), au bord du vide, côté droit ; tremplin pour y monter, on en redescend en sautant.
    a.block("passerelle_enseigne", "bridge", 12, -5.3, 6, 2.2, 3.0, 2.6)
    a.block("pied_enseigne_1", "wall", 9.5, -5.3, 0.4, 0.4, 2.6, 0)
    a.block("pied_enseigne_2", "wall", 14.5, -5.3, 0.4, 0.4, 2.6, 0)
    a.block("enseigne", "wall", 12, -6.55, 6, 0.25, 5.4, 3.0, color=ORANGE, color_b=BLEU)
    a.pad("tremplin_enseigne", 12, -10.5, 0, 12, -5.2, 3.0, apex=1.6)
    # Ponts au-dessus de la rue : pont central (5 m) coupé par une charrette en deux couloirs de 1,8 m au bord du vide,
    # et deux planches étroites (1,8 m) sur les flancs.
    a.block("pont_central", "bridge", 0, 0, 5, 8, 0, -0.6, mirror=False)
    a.block("charrette", "cover", 0, 0, 1.4, 2.4, 1.2, 0, mirror=False, color="#F2C230")   # jaune marché, neutre
    a.block("planche", "bridge", -12, 0, 1.8, 8, 0, -0.4, mirror=False)
    a.block("planche_b", "bridge", 12, 0, 1.8, 8, 0, -0.4, mirror=False)
    # Places : 4 par équipe sur la terrasse du fond.
    for x, z in [(-1.8, -16.5), (1.8, -16.5), (-4.5, -15.5), (4.5, -15.5)]:
        a.spawn(x, z)
    # Balles : une avancée devant le conteneur, une en retrait, une de chaque côté de la charrette.
    a.ball("balle_avant", -9, -5.2, 1.0)
    a.ball("balle_fond", 8, -12, 1.0)
    a.ball("balle_pont", 1.6, -0.6, 0.9, interval=10)   # au milieu d'un couloir, exposée : la prendre engage
    # La rue, 20 m plus bas : étals bâchés, deux rangées de guirlandes (parallaxe), et des bâches en porte-à-faux sous le bord
    # de chaque toit, aux couleurs de l'équipe : le marché se voit depuis le toit.
    for x, c in [(-14, "#E36A2E"), (-7, "#5FE0C8"), (0, "#F2C230"), (7, "#E36A2E"), (14, "#5FE0C8")]:
        a.deco(mirror=False, x=x, y=-20, z=-2.2, sx=4.5, sy=2.2, sz=2.2, color="#8E95AD")
        a.deco(mirror=False, x=x, y=-17.6, z=-2.2, sx=5, sy=0.3, sz=3, color=c, pattern=0)
        a.deco(mirror=False, x=x + 3, y=-20, z=2.4, sx=4.5, sy=2.2, sz=2.2, color="#8E95AD")
        a.deco(mirror=False, x=x + 3, y=-17.6, z=2.4, sx=5, sy=0.3, sz=3, color=c, pattern=0)
    for z in (-3.0, 3.0):
        garland(a, -17, z, 17, z, -2.0, sag=0.8, step=1.6)
    for z in (-1.5, 1.5):
        garland(a, -17, z, 17, z, -7.0, sag=1.0, step=1.8)
    for i, x in enumerate((-14, -8, 2, 8, 14)):
        c, cb = (ORANGE, BLEU) if i % 2 == 0 else ("#F4EDE0", "#F4EDE0")
        a.deco(x=x, y=-3.0, z=-3.5, sx=4.5, sy=0.15, sz=1.0, color=c, color_b=cb, pattern=0)
        a.deco(x=x, y=-3.5, z=-3.0, sx=4.5, sy=0.5, sz=0.05, color=c, color_b=cb, pattern=0)   # lambrequin : se lit comme du tissu, pas comme un rebord
    a.deco(model="enseigne_bulle", x=-16.6, y=1.6, z=-10, rotY=90, scale=1.0)
    a.deco(model="lampadaire", x=8, y=1.2, z=-17.6, scale=1.0)
    a.skyline((-19, 19, -20, 20), 7, [("#4A4E6A", "#F6C56A"), ("#5B4A5E", "#FFB070"), ("#3A3F5C", "#F6C56A"), ("#6B5A50", "#F6C56A")], base=-20,
              brick=False, max_h=22)   # ville sourde et plus basse : l'aire de jeu reste ce qui attire l'œil
    a.write("marche_suspendu", {
        "ambiance": "chantier",
        "info": {"title": "Toits du Marché", "tagline": "Deux toits, une rue, trois ponts.",
                 "idea": "Tenir les ponts : le vide de la rue sanctionne chaque tacle, la passerelle d'enseigne domine tout.", "teamSize": 3},
        "arenaBounds": {"minX": -19, "maxX": 19, "minZ": -20, "maxZ": 20, "wallHeight": 14, "ceilingHeight": 14, "wallKind": "plain"},
        "arena": {"killY": -4, "stepUp": 0.45, "floorBelowY": -20, "floorBelowColor": "#3A3346"},
    })


def piscine():
    """
    La Piscine : club sur un toit-terrasse, bassin vide au centre (1,6 m plus bas, six escaliers) avec un plongeoir et sa
    planche au milieu (3,2 m), atteint par deux tremplins au fond du bassin. Idée : le bas contre le haut. On tient la plage
    ou on descend dans le bassin (couverts : chaise de maître-nageur, bouées empilées) pour couper la route ; le plongeoir
    voit tout mais ne cache rien. Les grands côtés sont ouverts sur le vide (ring-out) ; les cabines font une porte vers
    chaque bande latérale (couloir au bord du vide ou au bord du bassin). Environ 175 m² par joueur en 3 contre 3.
    """
    a = Arena()
    D = 1.6  # hauteur de la plage (le fond du bassin est à 0)
    # Plage (deck) côté orange : bande du fond (x -16..16, z -16.5..-6.5) + deux bandes latérales jusqu'au milieu.
    a.block("plage_fond", "deck", 0, -11.5, 32, 10, D)
    a.block("plage_g", "deck", -13, -3.25, 6, 6.5, D)
    a.block("plage_d", "deck", 13, -3.25, 6, 6.5, D)
    # Fond du bassin (y = 0), x -10..10, z -6.5..0 (la moitié bleue par symétrie).
    a.block("bassin", "pool", 0, -3.25, 20, 6.5, 0, -1)
    # Six escaliers (trois par moitié), le haut à la plage.
    a.ramp("escalier_bassin_g", -8, -5, 4, 3, 0, D, "-z", stairs=True)
    a.ramp("escalier_bassin_c", 3.5, -5, 3, 3, 0, D, "-z", stairs=True)
    a.ramp("escalier_bassin_d", 8, -1.5, 4, 3, 0, D, "+x", stairs=True)
    # Plongeoir central (2,2 m de côté, 3,2 m) et sa planche qui dépasse de chaque côté au-dessus du bassin.
    a.block("plongeoir", "tower", 0, 0, 2.2, 2.2, 3.2, 0, mirror=False, color="#DDE7EE")
    a.block("planche_plongeoir", "bridge", 0, 0, 7, 0.9, 3.2, 3.0, mirror=False, color="#E8ECF2")
    a.pad("tremplin_bassin", -4.5, -3, 0, -0.6, -0.6, 3.2, apex=1.4)
    # Couverts du bassin : chaise de maître-nageur (pleine) et bouées empilées (mi-hauteur).
    a.block("chaise_maitre_nageur", "tower", -6.5, -1.5, 1.2, 1.2, 2.4, 0, color="#F4F4F4")
    a.deco(x=-6.5, y=2.4, z=-1.5, sx=1.4, sy=0.15, sz=1.4, color="#F4F4F4", pattern=0)
    a.deco(x=-6.5, y=2.55, z=-2.05, sx=1.4, sy=0.8, sz=0.1, color="#F4F4F4", pattern=0)   # dossier
    for dx in (-0.45, 0.45):   # montants de l'échelle
        a.deco(x=-6.5 + dx, y=0, z=-0.85, sx=0.08, sy=2.4, sz=0.08, color="#C9D2DA", pattern=0)
    a.deco(x=0, y=2.6, z=0, sx=2.26, sy=0.3, sz=2.26, color="#2FB4CF", pattern=2, mirror=False)   # carreaux du plongeoir
    for k, x in enumerate((6.6, 7.8)):   # bouées empilées : couvert de poitrine (1,7 m), on ne monte pas dessus
        a.block(f"bouees_{k + 1}", "cover", x, -5, 1.2, 1.2, 1.7, 0, color="#F4F4F4")
        for yy, c, cb in ((0.1, ORANGE, BLEU), (0.7, "#F4EDE0", "#F4EDE0"), (1.3, ORANGE, BLEU)):   # anneaux empilés
            a.deco(x=x, y=yy, z=-5, sx=1.24, sy=0.3, sz=1.24, color=c, color_b=cb, pattern=0)
    # Couverts de la plage : cabines (pleines, 2,6 m) en porte vers les bandes latérales, bar, transats, jardinières.
    for nom, x in (("cabine_g", -13), ("cabine_d", 13)):   # cabines blanches, bande d'équipe face au bassin
        a.block(nom, "stall", x, -8.5, 3, 3, D + 2.6, D, color="#EDEFF5")
        a.deco(x=x, y=D + 2.0, z=-6.95, sx=3.1, sy=0.5, sz=0.08, color=ORANGE, color_b=BLEU, pattern=0)
    a.block("bar", "cover", 0, -12.5, 7, 1.2, D + 1.7, D)   # vrai mur : cache les places du plongeoir
    a.block("transat_1", "low", -7, -10.5, 2.2, 0.9, D + 0.7, D)
    a.block("transat_2", "low", 7.5, -10, 2.2, 0.9, D + 0.7, D)
    a.block("jardiniere_g", "cover", -14.5, -3.5, 1.2, 3, D + 1.1, D, model="jardiniere")
    a.block("jardiniere_d", "cover", 14.5, -1.5, 1.2, 3, D + 1.1, D, model="jardiniere")
    a.block("parapet_fond", "parapet", 0, -16.7, 32.8, 0.4, D + 1.6, D)
    # Places : derrière le bar, sur la plage du fond.
    for x, z in [(-2.5, -15.3), (2.5, -15.3), (-7.5, -15.6), (7.5, -15.6)]:
        a.spawn(x, z)
    a.ball("balle_plage", -11, -12, D + 0.9)
    a.ball("balle_bassin", 5, -1.5, 0.9)
    a.ball("balle_plongeoir", 0, 0, 3.2 + 0.25, interval=14, mirror=False)
    # Le bassin se lit comme une piscine : parois carrelées cyan, margelle blanche, lignes de couloir au fond.
    tile, rim, lane = "#2FB4CF", "#EDEFF5", "#1E4E8C"
    a.deco(x=0, y=0, z=-6.47, sx=20, sy=D, sz=0.06, color=tile, pattern=2)
    a.deco(x=-9.97, y=0, z=-3.25, sx=0.06, sy=D, sz=6.5, color=tile, pattern=2)
    a.deco(x=9.97, y=0, z=-3.25, sx=0.06, sy=D, sz=6.5, color=tile, pattern=2)
    a.deco(x=0, y=D, z=-6.7, sx=20.8, sy=0.05, sz=0.4, color=rim, pattern=0)
    a.deco(x=-10.2, y=D, z=-3.25, sx=0.4, sy=0.05, sz=6.5, color=rim, pattern=0)
    a.deco(x=10.2, y=D, z=-3.25, sx=0.4, sy=0.05, sz=6.5, color=rim, pattern=0)
    for x in (-6, -2, 2, 6):
        a.deco(mirror=False, x=x, y=0.0, z=0, sx=0.25, sy=0.02, sz=12.4, color=lane, pattern=0)
    # Décor : ville du soir et guirlandes.
    for x in (-12, 12):
        garland(a, x, -16.5, x, 16.5, 5.4, sag=0.9)
    a.deco(model="lampadaire", x=-15.3, y=D, z=-16)
    a.skyline((-19, 19, -19, 19), 11, [("#3A3F5C", "#F6C56A"), ("#4A4E6A", "#FFB070"), ("#5B4A5E", "#F6C56A"), ("#2E3350", "#F6C56A")], base=-14, brick=False, max_h=22)   # ville froide et sombre : le club se détache
    a.write("piscine", {
        "ambiance": "soiree",
        "info": {"title": "La Piscine", "tagline": "Un bassin vide, un plongeoir, deux camps.",
                 "idea": "Le bas contre le haut : on plonge dans le bassin pour couper la route, le plongeoir voit tout mais ne cache rien.", "teamSize": 3},
        "arenaBounds": {"minX": -19, "maxX": 19, "minZ": -19, "maxZ": 19, "wallHeight": 14, "ceilingHeight": 14, "wallKind": "plain"},
        "arena": {"killY": -3, "stepUp": 0.45, "floorBelowY": -14, "floorBelowColor": "#2B2F45"},
    })


if __name__ == "__main__":
    marche_suspendu()
    piscine()
