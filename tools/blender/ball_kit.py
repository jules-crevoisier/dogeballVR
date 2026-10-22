"""Balles de jeu (DA : caoutchouc stylisé, rainure lumineuse à la couleur de l'équipe qui lance).

Trois balles de 0,30 m de diamètre (hitbox 0,22 m) :
- normale : coque en caoutchouc blanc cassé à deux panneaux, séparés par une couture en « balle de tennis » en relief ;
- bombe : coque anthracite à bandes jaunes, culot en acier riveté, mèche courbée et étincelle ;
- rebond : coque violette couverte de picots souples.

La rainure est un objet à part, « Rainure » (matériau neon_balle) : Ball.cs la teinte à la couleur de l'équipe ou du type.
Coque peinte comme les props (couleur + occlusion + rugosité dans l'alpha des sommets).

    python3 -I tools/blender/ball_kit.py assets/models            # les trois balles
    python3 -I tools/blender/ball_kit.py assets/models bombe
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402,F401
import bmesh  # noqa: E402,F401
from mathutils import Vector, noise  # noqa: E402

import lib  # noqa: E402
from props_kit import C, Prop, TAU, cyl, displace, lathe, mix, sphere, sstep, sweep, torus  # noqa: E402

R = 0.15


def seam(n=160, r=R):
    """Couture de balle de tennis : courbe fermée sur la sphère, deux panneaux égaux de part et d'autre."""
    a, b = 0.75, 0.25
    pts = []
    for i in range(n):
        t = TAU * i / n
        p = Vector((a * math.cos(t) + b * math.cos(3 * t), a * math.sin(t) - b * math.sin(3 * t), 2 * math.sqrt(a * b) * math.sin(2 * t)))
        pts.append(p.normalized() * r)
    return pts


SEAM = seam()


def seam_info(p):
    """(distance à la couture le long de la surface, côté +1/-1) pour un point p de la coque."""
    d = p.normalized() * R
    best, bi = 1e9, 0
    for i, s in enumerate(SEAM):
        dd = (s - d).length_squared
        if dd < best:
            best, bi = dd, i
    s = SEAM[bi]
    t = SEAM[(bi + 1) % len(SEAM)] - SEAM[bi - 1]
    side = 1.0 if s.normalized().cross(t).dot(d - s) > 0 else -1.0
    return math.sqrt(best), side


def shell(sub=4, bumps=0.0):
    """Icosphère lisse ; bumps > 0 : picots en relief (balle rebond)."""
    ob = sphere(R, sub=sub, name="Coque")

    def f(co):
        n = co.normalized()
        r = R + (bumps * _bump(n) if bumps > 0 else 0.0)
        r += 0.00012 * noise.noise(n * 60)   # grain du caoutchouc : casse les reflets
        return n * r
    return displace(ob, f)


BUMPS = [v.co.normalized() for v in sphere(1, sub=2).data.vertices]


def _bump(n):
    """Picot le plus proche : bosse douce en cosinus, 162 picots répartis."""
    best = max(n.dot(b) for b in BUMPS)
    ang = math.acos(min(1.0, best))
    return max(0.0, math.cos(min(math.pi / 2, ang / 0.085 * math.pi / 2))) ** 1.5


def seam_lip(P, color, rough=0.6):
    """Bourrelet sombre de la couture, sous le jonc lumineux : bord net quelle que soit la densité de la coque."""
    P.add(sweep(seam(120, R - 0.005), 0.0125, n=6, closed=True, name="Levre"), color, wear=0, grime=0, noise_amt=0, rough=rough)


def groove_tube(r=R + 0.004, w=0.0078):
    ob = sweep(seam(120, r), w, n=6, closed=True, name="Rainure")
    ob.data.materials.append(lib.mat("neon_balle", "#FFFFFF", emissive=2.5))
    return ob


def _prop(name, ao=0.03):
    P = Prop(name, ao_dist=ao, ao_strength=0.55, rays=40, grime_h=0.0)
    P.soft = True
    P.ground = -5.0
    return P


def normale():
    P = _prop("balle_normale")
    a, b = C("#F1F2F6"), C("#C9CFDD")
    P.add(shell(), lambda p, nn, f: a if seam_info(p)[1] > 0 else b, wear=0.0, grime=0, noise_amt=0.02, rough=0.42)
    seam_lip(P, "#2E3244")
    return P, [groove_tube()]


def bombe():
    P = _prop("balle_bombe", ao=0.05)
    P.add(shell(), "#2B2D3A", wear=0.0, grime=0, noise_amt=0.03, rough=0.5)
    seam_lip(P, "#14151C")
    # ceinture de danger : anneau plat jaune à rayures noires en biais (couleur par face : rayures nettes)
    belt = lathe([(R + 0.002, -0.026), (R + 0.007, -0.022), (R + 0.007, 0.022), (R + 0.002, 0.026)], n=96, closed=False)

    def bfn(p, nn, f):
        a = math.atan2(f.y, f.x)
        return C("#F2C230") if math.floor((a / TAU * 24) + f.z * 14) % 2 == 0 else C("#1C1D26")
    P.add(belt, bfn, wear=0.0, grime=0, noise_amt=0.0, rough=0.45)
    top = R - 0.004
    cap = lathe([(0.0, top + 0.03), (0.035, top + 0.03), (0.045, top + 0.022), (0.05, top + 0.005), (0.052, top - 0.012), (0.0, top - 0.012)], n=20)
    P.add(cap, "#8F98AE", wear=0.8, wear_col="#D5DAE6", grime=0, rough=0.3)
    for k in range(6):
        ang = TAU * k / 6
        P.add(sphere(0.006, (math.cos(ang) * 0.042, math.sin(ang) * 0.042, top + 0.017), sub=1), "#5A6178", wear=0, grime=0, rough=0.3)
    path = [Vector((0.004 * math.sin(t * 3), 0.02 * t * t, top + 0.03 + 0.07 * t)) for t in [i / 10 for i in range(11)]]
    P.add(sweep(path, 0.0075, n=8, name="Meche"), lambda p, nn, f: mix(C("#CDBA92"), C("#3A2E25"), sstep(top + 0.07, top + 0.1, p.z)),
          wear=0, grime=0, noise_amt=0.05, rough=0.95)
    spark = sphere(0.016, path[-1], sub=2, name="Etincelle")
    spark.data.materials.append(lib.mat("neon_etincelle", "#FFB23A", emissive=3.0))
    return P, [groove_tube(), spark]


def rebond():
    P = _prop("balle_rebond")
    a, b = C("#A27BFF"), C("#7C55E6")

    def fn(p, nn, f):
        n = p.normalized()
        return mix(a if seam_info(p)[1] > 0 else b, C("#D9C9FF"), 0.45 * _bump(n))   # sommets des picots plus clairs
    P.add(shell(sub=4, bumps=0.007), fn, wear=0.0, grime=0, noise_amt=0.02, rough=0.55)
    seam_lip(P, "#2A1E52")
    return P, [groove_tube(r=R + 0.006)]


BALLS = {"normale": normale, "bombe": bombe, "rebond": rebond}


def build(kind, out_dir):
    lib.reset()
    P, lights = BALLS[kind]()
    objs = P.bake()
    objs[0].name = objs[0].data.name = "Coque"
    for lo in lights:
        lo.data.shade_smooth()
    path = os.path.join(out_dir, f"balle_{kind}.glb")
    return lib.export(path, objs + lights)


if __name__ == "__main__":
    argv = sys.argv[1:]
    out = argv[0] if argv else "assets/models"
    for k in argv[1:] or list(BALLS):
        print(f"balle_{k:10s} {build(k, out):6d} triangles")
