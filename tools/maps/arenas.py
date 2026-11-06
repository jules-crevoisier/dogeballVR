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
    def block(self, id, kind, x, z, sx, sz, y1, y0=None, rot=0, mirror=True, **extra):
        y0 = (y1 - {"roof": 10, "floor": 10, "deck": 10, "bridge": 0.6}.get(kind, y1)) if y0 is None else y0
        b = {"id": id, "kind": kind, "x": x, "z": z, "sx": sx, "sz": sz, "y0": y0, "y1": y1, "rotY": rot, **extra}
        self.blocks.append(b)
        if mirror:
            self.blocks.append({**b, "id": id + "_b", "x": -x, "z": -z, "rotY": rot})

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

    def deco(self, mirror=True, **d):
        self.decor.append(d)
        if mirror:
            m = dict(d)
            m["x"], m["z"] = -d["x"], -d["z"]
            m["rotY"] = d.get("rotY", 0) + 180
            self.decor.append(m)

    def skyline(self, bounds, seed, colors, ring=10, base=-14):
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
                spots.append((x, z))
        for i, (x, z) in enumerate(spots):
            w, d = rnd.uniform(6, 11), rnd.uniform(6, 11)
            h = rnd.uniform(4, 12) if far < 22 else rnd.uniform(10, 30)   # proches : bas (ne cachent pas l'arène) ; loin : la silhouette
            c1, c2 = colors[i % len(colors)]
            self.decor.append({"x": round(x, 1), "y": base, "z": round(z, 1), "sx": round(w, 1), "sy": round(h - base, 1), "sz": round(d, 1),
                               "color": c1, "color2": c2, "pattern": 5 if i % 3 else 1})

    def write(self, name, meta):
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


def marche_suspendu():
    """
    Toits du marché : deux toits face à face au-dessus d'une rue de marché (le vide, ring-out), reliés par une large passerelle
    centrale et deux planches étroites sur les flancs. Idée : tenir les ponts. Chaque toit a une terrasse du fond (1,2 m, départ),
    un château d'eau (repère et gros couvert), un conteneur, des climatiseurs, et une passerelle d'enseigne haute (3 m) au bord
    du vide, atteinte par un tremplin : la meilleure vue, mais on y est exposé et à un tacle de la chute.
    """
    a = Arena()
    # Toits (côté orange z < 0). Le vide de la rue va de z = -5 à z = 5.
    a.block("toit", "roof", 0, -14, 40, 18, 0)
    a.block("parapet_fond", "parapet", 0, -23.2, 40.8, 0.4, 3.0, 0)
    a.block("parapet_g", "parapet", -20.2, -14, 0.4, 18, 1.6, 0)
    a.block("parapet_d", "parapet", 20.2, -14, 0.4, 18, 1.6, 0)
    # Terrasse du fond (apparition) et ses deux rampes latérales.
    a.block("terrasse", "deck", 0, -20, 14, 6, 1.2)
    a.ramp("rampe_terrasse_g", -8.5, -20.5, 3, 3, 0, 1.2, "+x")
    a.ramp("rampe_terrasse_d", 8.5, -20.5, 3, 3, 0, 1.2, "-x")
    a.block("muret_terrasse", "low", 0, -16.8, 5, 0.4, 2.2, 1.2)
    # Château d'eau : repère visible de partout, couvert plein (4,5 m).
    a.block("chateau_eau", "tower", -14.5, -17, 3.2, 3.2, 4.5, 0)
    a.deco(model="bidon", x=-14.5, y=4.5, z=-17, scale=2.2)
    # Conteneur (couvert haut) et climatiseurs (couverts à mi-hauteur, montables d'un saut).
    a.block("conteneur", "container", -6.5, -12.5, 6, 2.4, 2.6, 0)
    a.block("clim_1", "cover", -12, -9, 2.4, 1.6, 1.3, 0, model="clim")
    a.block("clim_2", "cover", 5.5, -12, 2, 2, 1.3, 0, model="clim")
    a.block("caisses_1", "crate", -2.5, -8, 2, 1, 0.9, 0)
    a.block("caisses_2", "crate", 12, -9.5, 1.4, 1.4, 1.0, 0, model="caisse_bois")
    a.block("lanterneau", "low", 1.5, -14.5, 3, 2, 0.8, 0)
    a.block("cheminee", "wall", 16.5, -16.5, 1.6, 2.4, 2.4, 0)
    # Passerelle d'enseigne : haute (3 m), au bord du vide, côté droit ; tremplin pour y monter, on en redescend en sautant.
    a.block("passerelle_enseigne", "bridge", 15.5, -6.6, 7, 2.2, 3.0, 2.6)
    a.block("pied_enseigne_1", "wall", 12.6, -6.6, 0.4, 0.4, 2.6, 0)
    a.block("pied_enseigne_2", "wall", 18.4, -6.6, 0.4, 0.4, 2.6, 0)
    a.block("enseigne", "wall", 15.5, -7.85, 7, 0.25, 4.8, 3.0, mirror=True, color="#2E1260")
    a.pad("tremplin_enseigne", 15.5, -12.5, 0, 15.5, -6.4, 3.0, apex=1.6)
    # Ponts au-dessus de la rue : passerelle centrale (8 m, deux caisses basses) et planches étroites (2 m) aux flancs.
    a.block("pont_central", "bridge", 0, 0, 8, 10, 0, -0.6, mirror=False)
    a.block("caisse_pont", "crate", -2.2, -1.8, 1.6, 0.8, 0.9, 0)
    a.block("planche", "bridge", -17, 0, 2.2, 10, 0, -0.4, mirror=False)
    a.block("planche_b", "bridge", 17, 0, 2.2, 10, 0, -0.4, mirror=False)
    # Places : 4 par équipe sur la terrasse du fond.
    for x, z in [(-1.8, -20.5), (1.8, -20.5), (-5, -19.5), (5, -19.5)]:
        a.spawn(x, z)
    # Balles : deux piédestaux par toit (un avancé, un en retrait), un au centre du pont.
    a.ball("balle_avant", -10, -6.5, 1.0)
    a.ball("balle_fond", 10, -15.5, 1.0)
    a.ball("balle_pont", 0, 1.6, 0.9, interval=10)
    # La rue en contrebas : étals bâchés, guirlandes, et la ville autour.
    for i, (x, c) in enumerate([(-14, "#E36A2E"), (-7, "#5FE0C8"), (0, "#F2C230"), (7, "#E36A2E"), (14, "#5FE0C8")]):
        a.deco(mirror=False, x=x, y=-12, z=-2.2, sx=4.5, sy=2.2, sz=2.2, color="#8E95AD")
        a.deco(mirror=False, x=x, y=-9.6, z=-2.2, sx=5, sy=0.3, sz=3, color=c, pattern=0)
        a.deco(mirror=False, x=x + 3, y=-12, z=2.4, sx=4.5, sy=2.2, sz=2.2, color="#8E95AD")
        a.deco(mirror=False, x=x + 3, y=-9.6, z=2.4, sx=5, sy=0.3, sz=3, color=c, pattern=0)
    for z in (-3.6, 3.6):
        garland(a, -20, z, 20, z, -2.0, sag=0.8, step=1.6)
    a.deco(model="enseigne_bulle", x=-19.6, y=1.6, z=-10, rotY=90, scale=1.0)
    a.deco(model="lampadaire", x=8, y=1.2, z=-22.6, scale=1.0)
    a.skyline((-22, 22, -25, 25), 7, [("#B5533C", "#F6C56A"), ("#5A6180", "#F6C56A"), ("#D9A441", "#232A4D"), ("#8E95AD", "#F6C56A")])
    a.write("marche_suspendu", {
        "ambiance": "chantier",
        "info": {"title": "Toits du Marché", "tagline": "Deux toits, une rue, trois ponts.",
                 "idea": "Tenir les ponts : le vide de la rue sanctionne chaque tacle, la passerelle d'enseigne domine tout.", "teamSize": 3},
        "arenaBounds": {"minX": -22, "maxX": 22, "minZ": -25, "maxZ": 25, "wallHeight": 14, "ceilingHeight": 14, "wallKind": "plain"},
        "arena": {"killY": -4, "stepUp": 0.45, "floorBelowY": -12, "floorBelowColor": "#3A3346"},
    })


def piscine():
    """
    La Piscine : club sur un toit-terrasse, bassin vide au centre (1,6 m plus bas) avec escaliers dans les coins, plongeoir
    haut au milieu (3,2 m) atteint par deux tremplins au fond du bassin. Idée : le bas contre le haut. On tient le pont
    du bord (plage) ou on plonge dans le bassin pour couper la route ; le plongeoir voit tout mais ne se couvre de rien.
    Les deux grands côtés sont ouverts sur le vide (ring-out) ; les petits côtés sont fermés par les cabines.
    """
    a = Arena()
    D = 1.6  # hauteur de la plage (le fond du bassin est à 0)
    # Plage (deck) côté orange : bande du fond + deux bandes latérales jusqu'au milieu.
    a.block("plage_fond", "deck", 0, -14, 34, 10, D)
    a.block("plage_g", "deck", -14, -4.5, 6, 9, D)
    a.block("plage_d", "deck", 14, -4.5, 6, 9, D)
    # Fond du bassin (y = 0), en deux moitiés pour la symétrie.
    a.block("bassin", "pool", 0, -4.5, 22, 9, 0, -1)
    # Escaliers du bassin, dans les coins (le haut à la plage).
    a.ramp("escalier_bassin_g", -9, -7.5, 4, 3, 0, D, "-z", stairs=True)
    a.ramp("escalier_bassin_d", 9, -1.5, 4, 3, 0, D, "+x", stairs=True)
    # Plongeoir central : tour de 3,2 m au milieu du bassin (sur la ligne médiane, non dupliqué).
    a.block("plongeoir", "tower", 0, 0, 3, 3, 3.2, 0, mirror=False)
    a.pad("tremplin_bassin", -4.5, -3.5, 0, -0.8, -0.8, 3.2, apex=1.4)
    # Couverts de la plage : cabines (pleines, 2,6 m), transats (bas), jardinières.
    a.block("cabine_g", "stall", -14, -16.5, 4, 3, D + 2.6, D)
    a.block("cabine_d", "stall", 14, -16.5, 4, 3, D + 2.6, D)
    a.block("bar", "cover", 0, -11.5, 6, 1.2, D + 1.2, D)
    a.block("transat_1", "low", -7, -10.5, 2.2, 0.9, D + 0.7, D)
    a.block("transat_2", "low", 7.5, -10, 2.2, 0.9, D + 0.7, D)
    a.block("jardiniere_g", "cover", -15.5, -6, 1.2, 3, D + 1.1, D, model="jardiniere")
    a.block("jardiniere_d", "cover", 15.5, -2.5, 1.2, 3, D + 1.1, D, model="jardiniere")
    a.block("bouee", "low", 5, -6, 1.6, 1.6, 0.8, 0, color="#E36A2E")
    a.block("parapet_fond", "parapet", 0, -19.2, 34.8, 0.4, D + 1.6, D)
    # Places : derrière le bar, sur la plage du fond.
    for x, z in [(-2.5, -15.5), (2.5, -15.5), (-7, -16.5), (7, -16.5)]:
        a.spawn(x, z)
    a.ball("balle_plage", -11, -12, D + 0.9)
    a.ball("balle_bassin", 6, -2.5, 0.9)
    a.ball("balle_plongeoir", 0, 0, 3.2 + 0.25, interval=14, mirror=False)
    # Décor : ville du soir, bouées et guirlandes.
    for x in (-12, 12):
        garland(a, x, -18, x, 18, 5.4, sag=0.9)
    a.deco(model="lampadaire", x=-16, y=D, z=-18.5)
    a.skyline((-20, 20, -22, 22), 11, [("#2E3A8C", "#F6C56A"), ("#5A6180", "#FF915A"), ("#B5533C", "#F6C56A"), ("#232A4D", "#F6C56A")])
    a.write("piscine", {
        "ambiance": "coucher",
        "info": {"title": "La Piscine", "tagline": "Un bassin vide, un plongeoir, deux camps.",
                 "idea": "Le bas contre le haut : on plonge dans le bassin pour couper la route, le plongeoir voit tout mais ne cache rien.", "teamSize": 3},
        "arenaBounds": {"minX": -21, "maxX": 21, "minZ": -21, "maxZ": 21, "wallHeight": 14, "ceilingHeight": 14, "wallKind": "plain"},
        "arena": {"killY": -3, "stepUp": 0.45, "floorBelowY": -14, "floorBelowColor": "#2B2F45"},
    })


if __name__ == "__main__":
    marche_suspendu()
    piscine()
