"""Kit de props de décor (DA Knockout City : cartoon rétro 60s, formes arrondies et biseautées, couleurs peintes).

Chaque prop est construit en bmesh (biseaux, tranches, coupes), puis toutes ses pièces sont fusionnées en un seul maillage
« peinture » : couleur de sommet (domaine coin de face, donc rayures nettes possibles) = couleur de la pièce
× occlusion ambiante cuite par lancer de rayons sur tout le prop + le sol, × usure (arêtes vives éclaircies ou écaillées
jusqu'au métal), × crasse au pied, × léger lavis de bruit. Les parties lumineuses restent des objets à part (matériaux
« neon_* » ou matériau émissif doux), que le shader du jeu (kc_toon / kc_pbr) fait briller.

Axes Blender : Z haut, origine au sol au centre du prop, échelle en mètres. Export glTF Y haut.

    python3 -I tools/blender/props_kit.py assets/models/props            # tous les props
    python3 -I tools/blender/props_kit.py assets/models/props cone pneus # quelques-uns
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Euler, Matrix, Vector, noise  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

import lib  # noqa: E402
from kc import kmat  # noqa: E402
from organic import vcol_mat  # noqa: E402

TAU = math.pi * 2


# ------------------------------------------------------------------ couleurs
def C(h):
    """Couleur hexadécimale sRGB -> Vector linéaire."""
    return Vector(lib.hex_rgba(h)[:3])


def mix(a, b, t):
    t = max(0.0, min(1.0, t))
    return a * (1 - t) + b * t


def sstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def flat(h):
    c = C(h)
    return lambda p, n, f: c


def vgrad(h0, h1, z0, z1):
    """Dégradé vertical (bas -> haut)."""
    a, b = C(h0), C(h1)
    return lambda p, n, f: mix(a, b, sstep(z0, z1, p.z))


# ------------------------------------------------------------------ maillages (bmesh, transformés à la création)
def _new_obj(bm, name="piece"):
    me = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    return ob


def _xf(bm, loc=(0, 0, 0), rot=(0, 0, 0)):
    m = Matrix.Translation(Vector(loc)) @ Euler(rot, "XYZ").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)


def _bevel(bm, w, seg=2, angle=math.radians(30), profile=0.5):
    if w <= 0:
        return
    edges = [e for e in bm.edges if e.is_manifold and e.calc_face_angle(0) > angle]
    if edges:
        bmesh.ops.bevel(bm, geom=edges, offset=w, offset_type="OFFSET", segments=seg, profile=profile,
                        affect="EDGES", clamp_overlap=True)


def box(size, loc=(0, 0, 0), rot=(0, 0, 0), bev=0.02, seg=2, cuts=0, name="box"):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if cuts:
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    _bevel(bm, bev, seg)
    _xf(bm, loc, rot)
    return _new_obj(bm, name)


def cyl(r, h, loc=(0, 0, 0), rot=(0, 0, 0), n=16, bev=0.0, seg=2, r2=None, name="cyl", caps=True):
    """Cylindre (ou tronc de cône) posé sur loc (base en loc.z, axe Z avant rotation)."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=caps, cap_tris=False, segments=n, radius1=r, radius2=r if r2 is None else r2, depth=h)
    bmesh.ops.translate(bm, vec=(0, 0, h / 2), verts=bm.verts)
    _bevel(bm, bev, seg, angle=math.radians(40))
    _xf(bm, loc, rot)
    return _new_obj(bm, name)


def sphere(r, loc=(0, 0, 0), sub=2, scale=(1, 1, 1), name="sph"):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r)
    bmesh.ops.scale(bm, vec=Vector(scale), verts=bm.verts)
    _xf(bm, loc)
    return _new_obj(bm, name)


def uvsphere(r, loc=(0, 0, 0), u=12, v=8, scale=(1, 1, 1), rot=(0, 0, 0), name="uvs"):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=r)
    bmesh.ops.scale(bm, vec=Vector(scale), verts=bm.verts)
    _xf(bm, loc, rot)
    return _new_obj(bm, name)


def lathe(prof, n=24, loc=(0, 0, 0), rot=(0, 0, 0), closed=False, name="lathe", twist=None):
    """Révolution d'un profil [(r, z), ...] autour de Z. r == 0 aux extrémités -> pôle (fermé)."""
    bm = bmesh.new()
    rings = []
    for (r, z) in prof:
        if not callable(r) and r <= 1e-6:
            rings.append([bm.verts.new((0, 0, z))])
            continue
        ring = []
        for i in range(n):
            a = TAU * i / n
            rr = r(a) if callable(r) else r
            ring.append(bm.verts.new((math.cos(a) * rr, math.sin(a) * rr, z)))
        rings.append(ring)
    pairs = list(zip(rings[:-1], rings[1:]))
    if closed:
        pairs.append((rings[-1], rings[0]))
    for ra, rb in pairs:
        for i in range(n):
            j = (i + 1) % n
            if len(ra) == 1:
                bm.faces.new((ra[0], rb[i], rb[j]))
            elif len(rb) == 1:
                bm.faces.new((ra[i], rb[0], ra[j]))
            else:
                bm.faces.new((ra[i], rb[i], rb[j], ra[j]))
    if not closed:
        for ring, flip in ((rings[0], True), (rings[-1], False)):
            if len(ring) > 2:
                f = bm.faces.new(ring[::-1] if flip else ring)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    _xf(bm, loc, rot)
    return _new_obj(bm, name)


def prism(poly, depth, loc=(0, 0, 0), rot=(0, 0, 0), bev=0.02, seg=2, angle=30, name="prism", cuts=0):
    """Polygone 2D [(x, z), ...] dans le plan XZ, extrudé sur Y (centré)."""
    bm = bmesh.new()
    vs = [bm.verts.new((x, -depth / 2, z)) for (x, z) in poly]
    f = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    nv = [g for g in ext["geom"] if isinstance(g, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, vec=(0, depth, 0), verts=nv)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if cuts:
        side = [e for e in bm.edges if abs(e.verts[0].co.y - e.verts[1].co.y) > depth * 0.5]
        bmesh.ops.subdivide_edges(bm, edges=side, cuts=cuts, use_grid_fill=False)
    _bevel(bm, bev, seg, angle=math.radians(angle))
    _xf(bm, loc, rot)
    return _new_obj(bm, name)


def fillet(pts, rad, k=4):
    """Arrondit les coins d'une polyligne 3D (arcs de rayon rad, k segments)."""
    pts = [Vector(p) for p in pts]
    out = [pts[0]]
    for i in range(1, len(pts) - 1):
        a, b, c = pts[i - 1], pts[i], pts[i + 1]
        d1, d2 = (a - b).normalized(), (c - b).normalized()
        rr = min(rad, (a - b).length * 0.45, (c - b).length * 0.45)
        p1, p2 = b + d1 * rr, b + d2 * rr
        for s in range(k + 1):
            t = s / k
            q = (1 - t) ** 2 * p1 + 2 * (1 - t) * t * b + t * t * p2
            out.append(q)
    out.append(pts[-1])
    return out


def sweep(path, r, n=10, caps=True, closed=False, name="tube", rfn=None):
    """Tube le long d'une polyligne (repères à transport parallèle). rfn(i, t) -> rayon local."""
    path = [Vector(p) for p in path]
    m = len(path)
    tans = []
    for i in range(m):
        if closed:
            t = path[(i + 1) % m] - path[i - 1]
        else:
            t = path[min(i + 1, m - 1)] - path[max(i - 1, 0)]
        tans.append(t.normalized())
    up = Vector((0, 0, 1)) if abs(tans[0].z) < 0.9 else Vector((1, 0, 0))
    nrm = tans[0].cross(up).normalized()
    bm = bmesh.new()
    rings = []
    for i in range(m):
        if i > 0:
            axis = tans[i - 1].cross(tans[i])
            if axis.length > 1e-6:
                ang = tans[i - 1].angle(tans[i])
                nrm = Matrix.Rotation(ang, 3, axis.normalized()) @ nrm
        bn = tans[i].cross(nrm).normalized()
        rr = rfn(i, i / max(1, m - 1)) if rfn else r
        ring = [bm.verts.new(path[i] + (nrm * math.cos(TAU * j / n) + bn * math.sin(TAU * j / n)) * rr) for j in range(n)]
        rings.append(ring)
    pairs = list(zip(rings[:-1], rings[1:]))
    if closed:
        pairs.append((rings[-1], rings[0]))
    for ra, rb in pairs:
        for j in range(n):
            k = (j + 1) % n
            bm.faces.new((ra[j], ra[k], rb[k], rb[j]))
    if caps and not closed:
        bm.faces.new(rings[0])
        bm.faces.new(rings[-1][::-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _new_obj(bm, name)


def torus(R, r, loc=(0, 0, 0), rot=(0, 0, 0), seg=24, rseg=8, name="tore", arc=TAU):
    full = abs(arc - TAU) < 1e-6
    cnt = seg if full else seg + 1
    pts = [(math.cos(arc * i / seg) * R, math.sin(arc * i / seg) * R, 0) for i in range(cnt)]
    ob = sweep(pts, r, rseg, caps=not full, closed=full, name=name)
    me = ob.data
    m = Matrix.Translation(Vector(loc)) @ Euler(rot, "XYZ").to_matrix().to_4x4()
    me.transform(m)
    return ob


def bisect(ob, planes):
    """Coupe le maillage par des plans [(point, normale), ...] (pour des rayures peintes nettes)."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    for co, no in planes:
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=Vector(co), plane_no=Vector(no).normalized(), dist=1e-5)
    bm.to_mesh(ob.data)
    bm.free()
    return ob


def displace(ob, fn):
    """fn(co) -> nouveau co."""
    for v in ob.data.vertices:
        v.co = fn(v.co.copy())
    return ob


def boolean(ob, cutter, op="DIFFERENCE"):
    bpy.context.view_layer.objects.active = ob
    m = ob.modifiers.new("bool", "BOOLEAN")
    m.object = cutter
    m.operation = op
    m.solver = "EXACT"
    with bpy.context.temp_override(object=ob, active_object=ob):
        bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.data.objects.remove(cutter, do_unlink=True)
    return ob


def text_mesh(body, size=0.3, extrude=0.02, bevel=0.0, loc=(0, 0, 0), rot=(0, 0, 0), align="CENTER", res=2, name="texte"):
    cu = bpy.data.curves.new(name, "FONT")
    cu.body = body
    cu.size = size
    cu.extrude = extrude
    cu.bevel_depth = bevel
    cu.bevel_resolution = 1
    cu.resolution_u = res
    cu.align_x = align
    cu.align_y = "CENTER"
    tob = bpy.data.objects.new(name + "_c", cu)
    bpy.context.collection.objects.link(tob)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tob.evaluated_get(dg))
    bpy.data.objects.remove(tob, do_unlink=True)
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    m = Matrix.Translation(Vector(loc)) @ Euler(rot, "XYZ").to_matrix().to_4x4()
    me.transform(m)
    return ob


# ------------------------------------------------------------------ prop : pièces peintes + lumières, cuisson, export
class Prop:
    GAIN = 0.86   # exposition globale : l'éclairage du jeu (ciel + soleil 1,4) est très lumineux

    def __init__(self, name, ao_dist=0.4, ao_strength=0.7, rays=40, grime_h=0.25):
        self.name = name
        self.parts = []   # (obj, fn, wear, wear_col, grime, noise, groupe)
        self.lights = []
        self.origins = {}   # groupe -> pivot (pièces animables : hélice…)
        self.ao_dist, self.ao_strength, self.rays, self.grime_h = ao_dist, ao_strength, rays, grime_h

    def add(self, ob, fn, wear=0.5, wear_col=None, grime=1.0, noise_amt=0.06, group=None):
        """fn(p, n, centre_de_face) -> couleur linéaire (ou hex). wear : éclaircissement des arêtes vives
        (vers wear_col si donné : peinture écaillée jusqu'au métal). group : nom d'un sous-objet séparé (animable)."""
        if isinstance(fn, str):
            fn = flat(fn)
        if isinstance(wear_col, str):
            wear_col = C(wear_col)
        self.parts.append((ob, fn, wear, wear_col, grime, noise_amt, group))
        return ob

    def light(self, ob, kind=None, color=None, strength=1.0):
        """Pièce lumineuse : kind = 'neon_cyan'… (brille fort) ou color + strength (émissif doux, < 1,5)."""
        mt = kmat(kind) if kind else lib.mat("lumiere_" + color.lstrip("#"), color, emissive=strength)
        ob.data.materials.clear()
        ob.data.materials.append(mt)
        self.lights.append(ob)
        return ob

    def _tree(self):
        bm = bmesh.new()
        for ob in [p[0] for p in self.parts] + self.lights:
            bm.from_mesh(ob.data)
        g = [bm.verts.new(v) for v in ((-40, -40, -0.001), (40, -40, -0.001), (40, 40, -0.001), (-40, 40, -0.001))]
        bm.faces.new(g)
        tree = BVHTree.FromBMesh(bm)
        bm.free()
        return tree

    def _paint(self, ob, fn, wear, wear_col, grime, nz, tree, dirs):
        me = ob.data
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.verts.ensure_lookup_table()
        bm.normal_update()
        ao, convex = [], []
        for v in bm.verts:
            n = v.normal
            hit = tot = 0.0
            for d in dirs:
                c = d.dot(n)
                if c < 0:
                    d, c = -d, -c
                loc, _nn, _i, dist = tree.ray_cast(v.co + n * 0.004, d, self.ao_dist)
                if loc is not None:
                    hit += c * (1.0 - 0.5 * dist / self.ao_dist)
                tot += c
            ao.append(1.0 - self.ao_strength * hit / max(tot, 1e-6))
            s = 0.0
            for e in v.link_edges:
                dv = e.other_vert(v).co - v.co
                if dv.length > 1e-6:
                    s += n.dot(dv.normalized())
            convex.append(max(0.0, min(1.0, -s / max(1, len(v.link_edges)) * 2.2)))
        col = bm.loops.layers.float_color.new("Col")
        tint = Vector((0.55, 0.5, 0.78))   # ombres qui virent au violet (comme le shader)
        grime_c = Vector((0.55, 0.47, 0.42))
        for f in bm.faces:
            fc = f.calc_center_median()
            for lp in f.loops:
                v = lp.vert
                p, n = v.co, v.normal
                c = Vector(fn(p, n, fc))
                cv = convex[v.index]
                if wear > 0:
                    if wear_col is not None:
                        c = mix(c, wear_col, sstep(0.25, 0.7, cv) * wear)
                    else:
                        c = c * (1.0 + 0.55 * wear * cv)
                a = ao[v.index]
                c = Vector([c[k] * (tint[k] + (1 - tint[k]) * a) * a ** 0.35 for k in range(3)])
                if grime > 0:
                    gz = 1.0 - sstep(0.0, self.grime_h, p.z)
                    c = Vector([c[k] * (1 - grime * 0.55 * gz * (1 - grime_c[k])) for k in range(3)])
                c *= (0.93 + 0.1 * max(-1.0, min(1.0, n.z))) * self.GAIN
                if nz > 0:
                    c *= 1.0 + nz * (noise.noise(p * 2.7) + 0.5 * noise.noise(p * 7.1))
                lp[col] = (max(0, c.x), max(0, c.y), max(0, c.z), 1.0)
        bm.to_mesh(me)
        bm.free()
        me.color_attributes.active_color = me.color_attributes["Col"]
        me.materials.clear()
        me.materials.append(vcol_mat())

    def bake(self):
        tree = self._tree()
        rnd = random.Random(11)
        dirs = []
        while len(dirs) < self.rays:
            d = Vector((rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, 1)))
            if d.length > 1e-3:
                dirs.append(d.normalized())
        groups = {}
        for (ob, fn, wear, wear_col, grime, nz, group) in self.parts:
            self._paint(ob, fn, wear, wear_col, grime, nz, tree, dirs)
            groups.setdefault(group, []).append(ob)
        out = []
        for group, objs in groups.items():
            bpy.ops.object.select_all(action="DESELECT")
            for o in objs:
                o.select_set(True)
            bpy.context.view_layer.objects.active = objs[0]
            if len(objs) > 1:
                bpy.ops.object.join()
            body = objs[0]
            nm = self.name if group is None else group
            body.name = nm
            body.data.name = nm
            if group in self.origins:
                o = Vector(self.origins[group])
                body.data.transform(Matrix.Translation(-o))
                body.location = o
            _shade(body)
            out.append(body)
        for lo in self.lights:
            _shade(lo)
        return out + self.lights


def _shade(ob, angle=75):
    me = ob.data
    me.shade_smooth()
    try:
        me.set_sharp_from_angle(angle=math.radians(angle))
    except Exception:
        pass
    bpy.context.view_layer.objects.active = ob
    w = ob.modifiers.new("wn", "WEIGHTED_NORMAL")
    w.keep_sharp = True
    w.weight = 50
    with bpy.context.temp_override(object=ob, active_object=ob):
        bpy.ops.object.modifier_apply(modifier=w.name)


_REG = []


def prop(f):
    """Décorateur : enregistre une fonction de prop dans PROPS (ordre de déclaration)."""
    _REG.append(f)
    return f


# ------------------------------------------------------------------ palette du kit
PAL = {
    "beton": "#C2BCB4", "beton_bas": "#9A938F", "jaune": "#F4C431", "encre": "#2A2C3B", "acier": "#B9C0CE",
    "rouille": "#9A5A3A", "bois": "#C98A4B", "bois_f": "#94592F", "orange": "#FF6A1A", "blanc": "#F2EFE6",
    "creme": "#F1E3C2", "teal": "#2B8C99", "rouge": "#D9473A", "vert": "#3F9A62", "vert_f": "#2C6E4B",
    "bleu": "#3C78C8", "gomme": "#3B3C49", "metal_f": "#4A5068",
}


# ====================================================================== PROPS
def _steel(P, ob, base="#4A5068", wear=1.0):
    return P.add(ob, base, wear=wear, wear_col="#9AA2B6")


@prop
def barriere_jersey():
    """Séparateur de chantier en béton (profil « jersey »), 2 m, bande de chevrons jaune et noir, fentes de passage,
    plaques d'attache en acier aux extrémités, anses de levage."""
    P = Prop("barriere_jersey", ao_dist=0.45, ao_strength=0.75)
    L = 2.0
    prof = [(-0.32, 0.0), (0.32, 0.0), (0.32, 0.07), (0.21, 0.29), (0.13, 0.78), (0.11, 0.82), (-0.11, 0.82), (-0.13, 0.78), (-0.21, 0.29), (-0.32, 0.07)]
    ob = prism(prof, L, rot=(0, 0, math.pi / 2), bev=0.0, name="corps")
    for sx in (-0.55, 0.55):   # fentes de passage au pied
        boolean(ob, box((0.28, 1.0, 0.22), (sx, 0, 0.0), bev=0.0))
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    _bevel(bm, 0.03, 2, angle=math.radians(25))
    bm.to_mesh(ob.data)
    bm.free()
    z0, z1, w = 0.42, 0.66, 0.24
    planes = [((0, 0, z0), (0, 0, 1)), ((0, 0, z1), (0, 0, 1)), ((0, 0, 0), (1, 0, 0))]
    for k in range(-6, 7):
        planes.append(((k * w + z0 * 0, 0, z0), (1, 0, 1)))
        planes.append(((k * w, 0, z0), (-1, 0, 1)))
    bisect(ob, planes)
    displace(ob, lambda co: co + Vector((0, 0.006 * noise.noise(co * 5) * (1 if abs(co.y) > 0.05 else 0), 0)))
    cb, ct, yel, ink = C("#69625D"), C("#958C82"), C("#F0B92A"), C("#25273A")
    stain = C("#5E5550")

    def fn(p, n, f):
        base = mix(cb, ct, sstep(0.0, 0.75, p.z))
        base = mix(base, stain, 0.8 * sstep(0.0, 0.5, noise.noise(p * Vector((1.3, 2.5, 1.8)))))
        if z0 < f.z < z1 and abs(f.y) > 0.1 and abs(f.x) < L / 2 - 0.03:
            s = math.floor((abs(f.x) + (f.z - z0)) / w)
            return (yel if s % 2 == 0 else ink) * (0.92 + 0.08 * sstep(z0, z1, p.z))
        return base
    P.add(ob, fn, wear=0.9, grime=1.0, noise_amt=0.08)
    # anses de levage (fer à béton rouillé) sur le dessus
    for sx in (-0.6, 0.6):
        P.add(torus(0.07, 0.016, (sx, 0, 0.815), (math.pi / 2, 0, 0), seg=10, rseg=6, arc=math.pi), "#8A4E33", wear=0.7, wear_col="#C98B62")
    # plaques d'attache en acier boulonnées aux deux bouts
    for sx in (-1, 1):
        _steel(P, box((0.03, 0.2, 0.5), (sx * (L / 2 + 0.005), 0, 0.42), bev=0.012, seg=1))
        for zz in (0.25, 0.59):
            _steel(P, box((0.06, 0.1, 0.07), (sx * (L / 2 + 0.03), 0, zz), bev=0.015, seg=1), base="#5A6078")
            P.add(cyl(0.022, 0.07, (sx * (L / 2 + 0.03), 0, zz - 0.06), n=8, bev=0.008, seg=1), "#B9C0CE", wear=0.3)
        for zz in (0.32, 0.52):
            P.add(cyl(0.014, 0.012, (sx * (L / 2 + 0.02), 0.07, zz), (0, sx * math.pi / 2, 0), n=6), "#B9C0CE", wear=0.3, grime=0)
            P.add(cyl(0.014, 0.012, (sx * (L / 2 + 0.02), -0.07, zz), (0, sx * math.pi / 2, 0), n=6), "#B9C0CE", wear=0.3, grime=0)
    return P


@prop
def caisse_bois():
    """Caisse de transport 1 m : cadre de chevrons, panneaux de planches à joints, écharpes en diagonale, clous, coins métal."""
    P = Prop("caisse_bois", ao_dist=0.3, ao_strength=0.75)
    S, t = 1.0, 0.11
    h = S / 2
    rnd = random.Random(8)
    frame_c = [C("#7E4F2B"), C("#744826"), C("#875532")]
    plank_c = [C("#A9733F"), C("#9C6838"), C("#B27C47"), C("#966334"), C("#AC7744")]

    def wood(cbase, axis):
        def fn(p, n, f):
            g = 0.88 + 0.22 * noise.noise(Vector((p[axis] * 1.2, p[(axis + 1) % 3] * 14, p[(axis + 2) % 3] * 14)))
            return cbase * g
        return fn
    P.add(box((S - 0.1, S - 0.1, S - 0.1), (0, 0, h), bev=0.0), "#3A2418", wear=0.0, grime=0.0, noise_amt=0.0)
    for (ax, a, b) in [(0, 1, 2), (1, 0, 2), (2, 0, 1)]:
        for sa in (-1, 1):
            for sb in (-1, 1):
                size = [0, 0, 0]
                size[ax] = S if ax == 0 else S - 2 * t + 0.002
                size[a] = t
                size[b] = t
                loc = [0, 0, h]
                loc[a] += sa * (h - t / 2)
                loc[b] += sb * (h - t / 2)
                P.add(box(size, loc, bev=0.02, seg=2), wood(rnd.choice(frame_c), ax), wear=1.0)
    inner = S - 2 * t
    np_ = 4
    pw = inner / np_
    for face in ("+x", "-x", "+y", "-y", "+z"):
        for i in range(np_):
            off = -inner / 2 + pw * (i + 0.5)
            th = 0.035
            d = h - th / 2 - 0.014
            if face in ("+x", "-x"):
                s = 1 if face == "+x" else -1
                o = box((th, inner + 0.01, pw - 0.016), (s * d, 0, h + off), bev=0.009, seg=1)
                axis = 1
            elif face in ("+y", "-y"):
                s = 1 if face == "+y" else -1
                o = box((inner + 0.01, th, pw - 0.016), (0, s * d, h + off), bev=0.009, seg=1)
                axis = 0
            else:
                o = box((inner + 0.01, pw - 0.016, th), (0, off, h + d), bev=0.009, seg=1)
                axis = 0
            P.add(o, wood(rnd.choice(plank_c), axis), wear=0.8)
    diag = math.hypot(inner, inner) - 0.05
    ang = math.atan2(inner, inner)
    for face in ("+x", "-x", "+y", "-y"):
        sgn = 1 if face[0] == "+" else -1
        d = h - 0.016
        flip = rnd.choice((-1, 1))
        if face[1] == "x":
            o = box((0.045, diag, 0.1), (sgn * d, 0, h), rot=(flip * ang, 0, 0), bev=0.012, seg=1)
            axis = 1
        else:
            o = box((diag, 0.045, 0.1), (0, sgn * d, h), rot=(0, flip * ang, 0), bev=0.012, seg=1)
            axis = 0
        P.add(o, wood(rnd.choice(frame_c) * 1.12, axis), wear=1.0)
    for sx in (-1, 1):
        for sy in (-1, 1):
            for z in (0.07, S - 0.07):
                _steel(P, box((0.155, 0.155, 0.155), (sx * (h - 0.07), sy * (h - 0.07), z), bev=0.03, seg=1), base="#3E4459", wear=0.45)
    nail = C("#8C93A8")
    for face in ("+x", "-x", "+y", "-y"):
        sgn = 1 if face[0] == "+" else -1
        for u in (-h + t / 2, h - t / 2):
            for zz in (0.3, 0.7):
                if face[1] == "x":
                    loc, rot = (sgn * (h - 0.004), u, zz), (0, math.pi / 2, 0)
                else:
                    loc, rot = (u, sgn * (h - 0.004), zz), (math.pi / 2, 0, 0)
                P.add(cyl(0.014, 0.012, loc, rot, n=5), lambda p, n, f: nail, wear=0.6, grime=0.0)
    return P


@prop
def bidon():
    """Fût métallique de 200 L : cerclages de roulement, bandes peintes, couvercle à bondes, bosses, coulures de rouille."""
    P = Prop("bidon", ao_dist=0.25, grime_h=0.3, ao_strength=0.75)
    R, H = 0.29, 0.88
    prof = [(0.0, 0.012), (0.268, 0.012), (0.272, 0.0), (0.293, 0.004), (0.299, 0.03), (0.287, 0.047), (R, 0.06),
            (R, 0.12), (R, 0.25), (0.302, 0.265), (0.307, 0.29), (0.302, 0.315), (R, 0.33),
            (R, 0.40), (R, 0.43), (R, 0.47), (R, 0.50),
            (R, 0.55), (0.302, 0.565), (0.307, 0.59), (0.302, 0.615), (R, 0.63),
            (R, 0.70), (R, 0.74), (R, 0.80), (0.287, 0.833), (0.299, 0.85), (0.293, H), (0.272, H - 0.004), (0.262, H - 0.02), (0.0, H - 0.02)]
    ob = lathe(prof, n=30, name="fut")
    rnd = random.Random(5)
    dents = [(rnd.uniform(0, TAU), rnd.uniform(0.15, 0.75), rnd.uniform(0.07, 0.11), rnd.uniform(0.012, 0.02)) for _ in range(3)]

    def dent(co):
        r = math.hypot(co.x, co.y)
        if r > 0.25 and 0.07 < co.z < 0.82:
            a = math.atan2(co.y, co.x)
            k = 1.0
            for (da, dz, dr, dd) in dents:
                dist = math.hypot((a - da + math.pi) % TAU - math.pi, (co.z - dz) / R) * R
                if dist < dr:
                    k -= dd * (1 - dist / dr) ** 2 / r
            co.x *= k
            co.y *= k
        return co
    displace(ob, dent)
    body, body_d, band, lid = C("#226A86"), C("#184C62"), C("#C2A97E"), C("#1E5A72")
    drip = C("#7A4A32")

    def fn(p, n, f):
        r = math.hypot(p.x, p.y)
        if n.z > 0.7 and r < 0.265:
            return lid * (0.9 + 0.2 * sstep(0.0, 0.26, r))
        c = mix(body_d, body, sstep(0.0, 0.7, p.z))
        if 0.43 < f.z < 0.47 or 0.70 < f.z < 0.80:
            c = band
        return c
    P.add(ob, fn, wear=0.8, wear_col="#8C93A5")
    for (bx, by, rr) in ((0.15, 0.06, 0.04), (-0.17, -0.02, 0.028)):
        P.add(cyl(rr, 0.02, (bx, by, H - 0.02), n=10, bev=0.007, seg=1), "#A7AEBF", wear=0.5)
        P.add(cyl(rr * 0.5, 0.03, (bx, by, H - 0.02), n=6, bev=0.004, seg=1), "#5B6278", wear=0.5)
    return P


def _tire(loc, rot, R=0.34, r_in=0.2, hw=0.115, n=22):
    """Pneu : section moulée (épaulement arrondi, rainure de bande, talon), révolution fermée."""
    top = [(R, 0.0), (R, 0.034), (R - 0.012, 0.04), (R - 0.012, 0.052), (R - 0.003, hw - 0.028), (R - 0.022, hw - 0.003),
           (0.3, hw), (0.282, hw + 0.001), (0.235, hw - 0.012), (r_in + 0.004, hw - 0.05), (r_in, 0.0)]
    bot = [(r, -z) for (r, z) in reversed(top[1:-1])]
    prof = top + bot
    ob = lathe(prof, n=n, closed=True, name="pneu")
    m4 = Matrix.Translation(Vector(loc)) @ Euler(rot, "XYZ").to_matrix().to_4x4()
    ob.data.transform(m4)
    return ob


@prop
def pile_pneus():
    """Pile de pneus usés : trois empilés de travers + un appuyé, bande de roulement à crans, liseré coloré sur le flanc."""
    P = Prop("pile_pneus", ao_dist=0.3, ao_strength=0.8)
    rub, side, inner, groove = C("#33343F"), C("#41434F"), C("#1E1F28"), C("#22232C")
    rnd = random.Random(2)
    hw, n = 0.115, 18
    stack = [(0.0, 0.0, 0.0), (0.045, -0.03, 0.4), (-0.03, 0.025, 0.85)]
    stripes = [C("#F0B92A"), C("#E9E4D8"), C("#FF6A1A")]

    def tire_fn(M, st):
        Mi = M.inverted()

        def fn(p, n_, f):
            q, qf = Mi @ p, Mi @ f
            rr, rf = math.hypot(q.x, q.y), math.hypot(qf.x, qf.y)
            if rr < 0.214:
                return inner
            if st is not None and 0.281 < rf < 0.301 and abs(qf.z) > hw * 0.9:
                return st
            if rf > 0.325 and abs(qf.z) < hw - 0.03:   # bande de roulement : crans en quinconce
                a = math.atan2(qf.y, qf.x)
                k = int(math.floor(a / TAU * n * 2)) + (1 if qf.z > 0 else 0)
                if 0.038 < abs(qf.z) < 0.054:
                    return groove
                return rub if k % 2 == 0 else groove * 1.3
            return mix(rub, side, sstep(0.22, 0.29, rr))
        return fn
    for i, (dx, dy, rz) in enumerate(stack):
        z = hw + i * (2 * hw - 0.004)
        rot = (rnd.uniform(-0.035, 0.035), rnd.uniform(-0.035, 0.035), rz)
        M = Matrix.Translation(Vector((dx, dy, z))) @ Euler(rot, "XYZ").to_matrix().to_4x4()
        ob = _tire((0, 0, 0), (0, 0, 0), n=n)
        ob.data.transform(M)
        P.add(ob, tire_fn(M, stripes[i] if i != 1 else None), wear=0.35)
    M = Matrix.Translation((0.47, 0.06, 0.345)) @ Euler((0, math.radians(-74), 0.25), "XYZ").to_matrix().to_4x4()
    ob = _tire((0, 0, 0), (0, 0, 0), n=n)
    ob.data.transform(M)
    P.add(ob, tire_fn(M, stripes[1]), wear=0.35)
    return P


@prop
def cone():
    """Cône de chantier : semelle carrée arrondie à rebord, corps orange, deux bandes réfléchissantes, embout ouvert."""
    P = Prop("cone", ao_dist=0.18, grime_h=0.12, ao_strength=0.75)
    base = box((0.4, 0.4, 0.035), (0, 0, 0.0175), bev=0.016, seg=2, cuts=1)
    P.add(base, "#3A3E52", wear=0.8)
    prof = [(0.0, 0.035), (0.19, 0.035), (0.192, 0.048), (0.18, 0.058), (0.162, 0.068), (0.15, 0.1), (0.13, 0.2), (0.118, 0.26), (0.115, 0.275),
            (0.108, 0.3), (0.092, 0.4), (0.088, 0.415), (0.085, 0.43), (0.075, 0.5), (0.07, 0.52), (0.066, 0.535),
            (0.06, 0.57), (0.058, 0.585), (0.045, 0.66), (0.04, 0.69), (0.036, 0.705), (0.032, 0.712), (0.022, 0.712), (0.02, 0.69), (0.0, 0.69)]
    ob = lathe(prof, n=24, name="cone")
    org, org_d, wht = C("#FF6A1A"), C("#D9480C"), C("#EEEDE6")

    def fn(p, n, f):
        if 0.275 <= f.z <= 0.415 and math.hypot(p.x, p.y) > 0.05:
            return wht
        if 0.52 <= f.z <= 0.585:
            return wht
        if f.z < 0.07:
            return org_d * 0.85
        return mix(org_d, org, sstep(0.05, 0.6, p.z))
    P.add(ob, fn, wear=0.5)
    return P


@prop
def banc_parc():
    """Banc de parc 1,8 m : flasques en fonte tubulaire (pied, accoudoir, dossier), lattes de bois, boulons, patins."""
    P = Prop("banc_parc", ao_dist=0.3, ao_strength=0.75)
    iron, iron_w = "#2C4F4A", "#7FA39A"
    rnd = random.Random(3)
    for sx in (-0.78, 0.78):
        def yz(pts, sx=sx):
            return [(sx, y, z) for (y, z) in pts]
        a = fillet(yz([(-0.29, 0.03), (-0.235, 0.4), (-0.27, 0.64), (-0.05, 0.665), (0.12, 0.63)]), 0.09, k=3)
        b = fillet(yz([(0.27, 0.03), (0.17, 0.4), (0.215, 0.62), (0.3, 0.93)]), 0.1, k=3)
        rail = yz([(-0.27, 0.39), (0.21, 0.37)])
        for path, r in ((a, 0.028), (b, 0.03), (rail, 0.024)):
            P.add(sweep(path, r, n=8), iron, wear=0.9, wear_col=iron_w)
        P.add(sphere(0.04, (sx, 0.12, 0.63), sub=1, scale=(1, 1, 0.8)), iron, wear=0.9, wear_col=iron_w)   # bout d'accoudoir
        P.add(sphere(0.038, (sx, 0.3, 0.93), sub=1), iron, wear=0.9, wear_col=iron_w)
        for (fy, fz) in ((-0.29, 0.0), (0.27, 0.0)):   # patins
            P.add(box((0.09, 0.13, 0.04), (sx, fy, 0.02), bev=0.014, seg=2), "#22252F", wear=0.6)
    cy = fillet([(0, -0.2, 0.0), (0, -0.16, 0.2), (0, 0.0, 0.37), (0, 0.16, 0.2), (0, 0.2, 0.0)], 0.08, k=3)
    P.add(sweep(cy, 0.022, n=8), iron, wear=0.9, wear_col=iron_w)
    P.add(sweep([(0, -0.26, 0.385), (0, 0.2, 0.37)], 0.02, n=8), iron, wear=0.9, wear_col=iron_w)
    woods = [C("#A06A3C"), C("#946036"), C("#A87242"), C("#8E5B32")]

    def wood(cb):
        return lambda p, n, f: cb * (0.86 + 0.24 * noise.noise(Vector((p.x * 1.1, p.y * 16, p.z * 16)))) * (1.05 if n.z > 0.5 else 1.0)
    L = 1.86
    for i in range(4):   # assise : 4 lattes légèrement inclinées vers l'arrière
        y = -0.25 + i * 0.105
        z = 0.425 - i * 0.008
        o = box((L, 0.088, 0.038), (0, y, z), rot=(math.radians(-4), 0, 0), bev=0.012, seg=2)
        bisect(o, [((k * 0.45, 0, 0), (1, 0, 0)) for k in (-1, 1)])
        P.add(o, wood(rnd.choice(woods)), wear=0.8)
        for sx in (-0.78, 0.78):
            P.add(cyl(0.012, 0.008, (sx, y, z + 0.018), n=6), "#9AA2B6", wear=0, grime=0)
    for i in range(3):   # dossier
        t = 0.3 + i * 0.25
        y = 0.215 + (0.3 - 0.215) * t - 0.045
        z = 0.62 + (0.93 - 0.62) * t - 0.05
        o = box((L, 0.035, 0.085), (0, y, z), rot=(math.radians(-15), 0, 0), bev=0.012, seg=2)
        P.add(o, wood(rnd.choice(woods)), wear=0.8)
    return P


@prop
def jardiniere():
    """Jardinière en béton teinté (rebord épais, socle en retrait), terreau, buisson stylisé en boules de feuillage, fleurs."""
    P = Prop("jardiniere", ao_dist=0.35, ao_strength=0.8)
    W, D, H = 1.2, 0.62, 0.5
    terra, terra_d, rim = C("#B85A3C"), C("#8E4130"), C("#BFAE92")
    P.add(box((W - 0.08, D - 0.08, 0.08), (0, 0, 0.04), bev=0.02, seg=2), "#5C3A33", wear=0.4)
    body = box((W, D, H - 0.16), (0, 0, 0.08 + (H - 0.16) / 2), bev=0.04, seg=3)
    bisect(body, [((0, 0, 0.2), (0, 0, 1)), ((0, 0, 0.3), (0, 0, 1))])
    P.add(body, lambda p, n, f: mix(terra_d, terra, sstep(0.08, 0.38, p.z)) * (0.92 if 0.2 < f.z < 0.3 and abs(n.z) < 0.5 else 1.0), wear=0.7)
    for i in range(-2, 3):   # rainures verticales décoratives
        if i == 0:
            continue
        for sy in (-1, 1):
            P.add(box((0.05, 0.03, 0.22), (i * 0.24, sy * (D / 2 + 0.002), 0.25), bev=0.012, seg=2), lambda p, n, f: terra_d * 1.05, wear=0.8)
    # rebord (cadre de 4 pièces arrondies)
    for sy in (-1, 1):
        P.add(box((W + 0.08, 0.1, 0.09), (0, sy * (D / 2 - 0.01), H - 0.045), bev=0.03, seg=3), lambda p, n, f: rim, wear=0.5)
    for sx in (-1, 1):
        P.add(box((0.1, D - 0.1, 0.09), (sx * (W / 2 - 0.01), 0, H - 0.045), bev=0.03, seg=3), lambda p, n, f: rim, wear=0.5)
    soil = box((W - 0.1, D - 0.1, 0.06), (0, 0, H - 0.07), bev=0.0, cuts=3)
    displace(soil, lambda co: co + Vector((0, 0, 0.012 * noise.noise(co * 9))) if co.z > H - 0.05 else co)
    P.add(soil, lambda p, n, f: C("#3B2A22") * (0.85 + 0.3 * noise.noise(p * 12)), wear=0, grime=0)
    # buisson : boules de feuillage bosselées
    rnd = random.Random(6)
    g_lo, g_hi, g_tip = C("#1F5A3A"), C("#4E9A45"), C("#A6D65A")

    def leaf(p, n, f):
        t = sstep(H, H + 0.75, p.z)
        c = mix(g_lo, g_hi, t)
        return mix(c, g_tip, sstep(0.3, 0.9, n.z) * 0.6 * t + 0.25 * max(0, noise.noise(p * 6)))
    blobs = [(-0.32, 0.0, 0.2, 0.26), (0.3, 0.04, 0.2, 0.25), (0.0, -0.05, 0.32, 0.3), (-0.12, 0.1, 0.52, 0.22),
             (0.16, -0.02, 0.5, 0.2), (0.02, 0.06, 0.68, 0.16), (-0.42, 0.06, 0.12, 0.16), (0.44, -0.05, 0.12, 0.15)]
    for (x, y, z, r) in blobs:
        o = sphere(r, (x, y, H + z - 0.02), sub=2 if r > 0.17 else 1, scale=(1.1, 0.95, 0.9))
        c0 = Vector((x, y, H + z))
        displace(o, lambda co, c0=c0, r=r: c0 + (co - c0) * (1 + 0.16 * noise.noise(co * 5.5)))
        P.add(o, leaf, wear=0.25, grime=0, noise_amt=0.0)
    for i in range(9):   # petites fleurs
        a = rnd.uniform(0, TAU)
        bx, by, bz, br = rnd.choice(blobs[:6])
        d = Vector((math.cos(a), math.sin(a) * 0.8, rnd.uniform(0.2, 0.8))).normalized()
        c = Vector((bx, by, H + bz)) + d * br * 1.05
        col = rnd.choice(["#FF6FA8", "#FFD84A", "#FFF3E6"])
        P.add(sphere(0.03, c, sub=1, scale=(1, 1, 0.7)), col, wear=0.2, grime=0, noise_amt=0)
        P.add(sphere(0.012, c + d * 0.02, sub=1), "#FFB020", wear=0, grime=0, noise_amt=0)
    return P


@prop
def tuyaux():
    """Groupe de trois conduites sur traverses : coudes arrondis, brides boulonnées, colonne montante avec vanne à volant."""
    P = Prop("tuyaux", ao_dist=0.35, ao_strength=0.75)
    pipes = [  # (y, rayon, couleur, chemin)
        (-0.34, 0.1, "#E2A62A", [(-1.6, -0.34, 0.33), (1.05, -0.34, 0.33), (1.05, -0.34, 1.55)]),
        (0.0, 0.085, "#C8483A", [(-1.6, 0.0, 0.33), (1.6, 0.0, 0.33)]),
        (0.34, 0.12, "#2F7F93", [(-1.6, 0.34, 0.35), (0.55, 0.34, 0.35), (0.85, 0.34, 0.35), (0.85, 0.34, -0.2)]),
    ]
    for (y, r, col, path) in pipes:
        pts = fillet(path, r * 2.6, k=5)
        dense = []
        for a, b in zip(pts[:-1], pts[1:]):   # recoupe les longues portions (occlusion plus fine)
            a, b = Vector(a), Vector(b)
            k = max(1, int((b - a).length / 0.5))
            for s in range(k):
                dense.append(a.lerp(b, s / k))
        dense.append(Vector(pts[-1]))
        base, dark = C(col), C(col) * 0.6
        o = sweep(dense, r, n=10)

        def fn(p, n, f, base=base, dark=dark):
            c = mix(dark, base, 0.55 + 0.45 * n.z)
            if (int(math.floor((p.x + 2) / 0.9)) % 3 == 0) and abs(p.x) < 1.5 and p.z < 0.6:
                c = c * 0.93
            return c
        P.add(o, fn, wear=0.0)
        # brides boulonnées (dont une bride pleine en bout de conduite)
        flanges = [Vector((-1.6 + 0.03, y, path[0][2])), Vector((0.35, y, path[0][2]))]
        if len(path) == 3:
            flanges.append(Vector((1.05, y, 1.1)))
        for fpos in flanges:
            vertical = fpos.z > 0.8
            rot = (0, 0, 0) if vertical else (0, math.pi / 2, 0)
            off = Vector((0, 0, -0.03)) if vertical else Vector((-0.03, 0, 0))
            P.add(cyl(r * 1.45, 0.06, fpos + off, rot, n=12, bev=0.0), lambda p, n, f, c=C(col): c * 0.85, wear=1.0, wear_col="#A9B0C2")
            for k in range(4):
                a = TAU * k / 4 + 0.785
                if vertical:
                    q = fpos + Vector((math.cos(a) * r * 1.2, math.sin(a) * r * 1.2, -0.045))
                    P.add(cyl(0.017, 0.09, q, n=5), "#3F4458", wear=0.6, wear_col="#A9B0C2")
                else:
                    q = fpos + Vector((-0.045, math.cos(a) * r * 1.2, math.sin(a) * r * 1.2))
                    P.add(cyl(0.017, 0.09, q, (0, math.pi / 2, 0), n=5), "#3F4458", wear=0.6, wear_col="#A9B0C2")
    # vanne à volant au sommet de la colonne
    top = Vector((1.05, -0.34, 1.55))
    P.add(cyl(0.12, 0.08, top + Vector((0, 0, -0.04)), n=14, bev=0.02, seg=2), "#3F4458", wear=0.8, wear_col="#A9B0C2")
    P.add(cyl(0.025, 0.16, top, n=8), "#A9B0C2", wear=0.2)
    P.add(torus(0.16, 0.022, top + Vector((0, 0, 0.16)), seg=18, rseg=6), "#D9473A", wear=0.8, wear_col="#F2A28F")
    for k in range(4):
        a = TAU * k / 4
        P.add(sweep([top + Vector((0, 0, 0.16)), top + Vector((math.cos(a) * 0.16, math.sin(a) * 0.16, 0.16))], 0.014, n=6), "#D9473A", wear=0.4)
    P.add(sphere(0.035, top + Vector((0, 0, 0.17)), sub=1), "#D9473A", wear=0.4)
    # traverses en béton + colliers
    for x in (-1.15, 0.0):
        P.add(box((0.32, 1.2, 0.2), (x, 0, 0.1), bev=0.03, seg=2), lambda p, n, f: mix(C("#6E675F"), C("#958C82"), sstep(0, 0.2, p.z)), wear=0.8)
        for (y, r, col, path) in pipes:
            P.add(torus(r + 0.018, 0.016, (x, y, path[0][2]), (0, math.pi / 2, 0), seg=10, rseg=4, arc=math.pi), "#3F4458", wear=0.7, wear_col="#A9B0C2")
    # bague de sol là où la conduite bleue plonge
    P.add(cyl(0.2, 0.05, (0.85, 0.34, 0.0), n=16, bev=0.015, seg=2), "#4A4F63", wear=0.7)
    return P


@prop
def transformateur():
    """Poste de transformation sur socle : armoire à portes, grilles d'aération à lames, ailettes de refroidissement,
    isolateurs en céramique, plaque d'avertissement (éclair générique)."""
    P = Prop("transformateur", ao_dist=0.35, ao_strength=0.8)
    green, green_d, steel = C("#3E7A5E"), C("#2A5843"), "#9FB3A9"
    W, D, H = 1.2, 0.85, 1.25
    P.add(box((W + 0.4, D + 0.4, 0.14), (0, 0, 0.07), bev=0.03, seg=2), lambda p, n, f: mix(C("#6E675F"), C("#9A9187"), sstep(0, 0.14, p.z)), wear=0.9)

    def paint(p, n, f):
        return mix(green_d, green, sstep(0.1, 1.1, p.z))
    body = box((W, D, H), (0, 0, 0.14 + H / 2), bev=0.045, seg=2)
    bisect(body, [((0, 0, z), (0, 0, 1)) for z in (0.45, 0.8, 1.15)])
    P.add(body, paint, wear=0.9, wear_col=steel)
    # toit débordant légèrement bombé
    roof = box((W + 0.12, D + 0.12, 0.08), (0, 0, 0.14 + H + 0.04), bev=0.035, seg=1)
    displace(roof, lambda co: co + Vector((0, 0, 0.03 * (1 - (co.x / (W / 2 + 0.06)) ** 2) if co.z > H + 0.17 else 0)))
    P.add(roof, lambda p, n, f: green * 1.08, wear=0.9, wear_col=steel)
    # portes doubles en façade (-Y)
    fy = -D / 2 - 0.012
    for sx in (-1, 1):
        P.add(box((W / 2 - 0.09, 0.024, H - 0.22), (sx * (W / 4 - 0.005), fy, 0.14 + H / 2), bev=0.012, seg=2), lambda p, n, f: green * 0.95, wear=0.9, wear_col=steel)
        for zz in (0.42, 1.12):   # charnières
            P.add(cyl(0.022, 0.12, (sx * (W / 2 - 0.035), fy - 0.01, zz), n=6, bev=0.0), "#30343F", wear=0.7, wear_col=steel)
    P.add(box((0.035, 0.05, 0.22), (0.06, fy - 0.03, 0.82), bev=0.012, seg=2), "#C9CFDB", wear=0.4)   # poignée
    P.add(box((0.07, 0.03, 0.08), (-0.06, fy - 0.02, 0.82), bev=0.01, seg=1), "#E2A62A", wear=0.5)   # cadenas
    P.add(torus(0.025, 0.007, (-0.06, fy - 0.02, 0.87), (math.pi / 2, 0, 0), seg=8, rseg=4, arc=math.pi), "#C9CFDB", wear=0.2)
    # plaque d'avertissement : triangle jaune, éclair noir
    tri = [(-0.13, -0.11), (0.13, -0.11), (0.0, 0.12)]
    t = prism(tri, 0.012, (0.0, fy - 0.018, 1.13), (0, 0, 0), bev=0.006, seg=1, angle=50)
    P.add(t, "#F0B92A", wear=0.3, grime=0)
    bolt = [(-0.01, 0.07), (0.035, 0.07), (0.005, 0.0), (0.04, 0.0), (-0.03, -0.085), (-0.005, -0.015), (-0.04, -0.015)]
    P.add(prism(bolt, 0.01, (0.0, fy - 0.024, 1.11), bev=0.0), "#1F2130", wear=0, grime=0)
    # grilles d'aération à lames sur les flancs (±X)
    for sx in (-1, 1):
        x = sx * (W / 2 + 0.012)
        P.add(box((0.03, 0.5, 0.42), (x, 0.05, 0.75), bev=0.012, seg=2), lambda p, n, f: green_d * 0.9, wear=0.8, wear_col=steel)
        for k in range(5):
            P.add(box((0.025, 0.44, 0.045), (x + sx * 0.02, 0.05, 0.6 + k * 0.075), rot=(0, sx * math.radians(-35), 0), bev=0.008, seg=1), lambda p, n, f: green * 1.05, wear=1.0, wear_col=steel)
    # ailettes de refroidissement à l'arrière (+Y)
    for k in range(9):
        x = -0.48 + k * 0.12
        P.add(box((0.03, 0.14, H - 0.4), (x, D / 2 + 0.065, 0.14 + H / 2 - 0.05), bev=0.012, seg=1), paint, wear=1.0, wear_col=steel)
    P.add(box((W - 0.1, 0.1, 0.06), (0, D / 2 + 0.06, 0.14 + H - 0.2), bev=0.02, seg=2), paint, wear=0.8, wear_col=steel)
    # isolateurs (céramique brune) sur le toit
    for x in (-0.32, 0.0, 0.32):
        base = Vector((x, 0.1, 0.14 + H + 0.06))
        prof = [(0.0, 0.0), (0.05, 0.0)]
        z = 0.0
        for k in range(3):
            prof += [(0.04, z + 0.02), (0.075, z + 0.03), (0.075, z + 0.045), (0.04, z + 0.06)]
            z += 0.06
        prof += [(0.03, z + 0.03), (0.0, z + 0.03)]
        P.add(lathe(prof, n=8, loc=base), lambda p, n, f: C("#8C4A2E") * (0.9 + 0.25 * max(0, n.z)), wear=0.6, wear_col="#E0A27E", grime=0)
        P.add(cyl(0.012, 0.08, base + Vector((0, 0, z + 0.02)), n=6), "#C9CFDB", wear=0.2, grime=0)
    return P


@prop
def clim():
    """Bloc de climatisation de toit : caisson crème arrondi, ventilateur sous grille (hélice séparée « helice »,
    pivot au centre, pour la faire tourner), ailettes latérales, coffret électrique, liaisons frigorifiques isolées, rails."""
    P = Prop("clim", ao_dist=0.35, ao_strength=0.8)
    W, D, H = 1.25, 0.95, 0.8
    cream, cream_d, dark = C("#B9AF9C"), C("#8C826F"), "#30343F"
    for sy in (-1, 1):   # rails
        P.add(box((W + 0.2, 0.12, 0.1), (0, sy * (D / 2 - 0.12), 0.05), bev=0.02, seg=2), "#4A4F63", wear=0.9, wear_col="#A9B0C2")
    body = box((W, D, H), (0, 0, 0.1 + H / 2), bev=0.06, seg=2)
    bisect(body, [((0, 0, z), (0, 0, 1)) for z in (0.3, 0.55)])

    def shell(p, n, f):
        c = mix(cream_d, cream, sstep(0.1, 0.8, p.z))
        streak = max(0.0, noise.noise(Vector((p.x * 5, p.y * 5, 0))) - 0.2) * sstep(0.9, 0.4, p.z)
        return mix(c, C("#7D6F60"), streak * 0.8)
    P.add(body, shell, wear=0.8)
    for sx in (-1, 1):   # montants d'angle et cadre supérieur en tôle sombre
        for sy in (-1, 1):
            P.add(box((0.09, 0.09, H + 0.02), (sx * (W / 2 - 0.02), sy * (D / 2 - 0.02), 0.1 + H / 2), bev=0.025, seg=1), "#4A5068", wear=0.9, wear_col="#A9B0C2")
    for sy in (-1, 1):
        P.add(box((W - 0.06, 0.07, 0.06), (0, sy * (D / 2 - 0.015), 0.1 + H - 0.01), bev=0.02, seg=1), "#4A5068", wear=0.9, wear_col="#A9B0C2")
        P.add(box((W - 0.06, 0.07, 0.06), (0, sy * (D / 2 - 0.015), 0.13), bev=0.02, seg=1), "#4A5068", wear=0.9, wear_col="#A9B0C2")
    for k in (-2, 0, 2):   # vis de panneau
        for sy in (-1,):
            P.add(cyl(0.012, 0.01, (k * 0.25, sy * (D / 2 + 0.004), 0.82), (math.pi / 2, 0, 0), n=6), "#6E7488", wear=0, grime=0)
    zt = 0.1 + H
    # bague du ventilateur
    fc = Vector((0.05, 0, zt))
    prof = [(0.36, -0.01), (0.42, 0.0), (0.45, 0.03), (0.44, 0.07), (0.4, 0.08), (0.37, 0.06), (0.36, -0.01)]
    P.add(lathe(prof[:-1], n=24, loc=fc, closed=True), "#7E786C", wear=0.6)
    P.add(cyl(0.36, 0.01, fc + Vector((0, 0, -0.05)), n=16), "#151821", wear=0, grime=0, noise_amt=0)   # fond noir
    # hélice (sous-objet)
    P.add(cyl(0.07, 0.07, fc + Vector((0, 0, -0.05)), n=12, bev=0.02, seg=2), dark, wear=0.6, group="helice")
    for k in range(5):
        a = TAU * k / 5
        blade = box((0.27, 0.12, 0.012), (0, 0, 0), bev=0.006, seg=1)
        bm_ = blade.data
        m = Matrix.Translation(fc + Vector((math.cos(a) * 0.19, math.sin(a) * 0.19, -0.02))) @ Matrix.Rotation(a, 4, "Z") @ Matrix.Rotation(0.4, 4, "X")
        bm_.transform(m)
        P.add(blade, "#8C93A5", wear=0.6, group="helice")
    P.origins["helice"] = tuple(fc)
    # grille : anneaux concentriques + rayons
    for rr in (0.12, 0.22, 0.32):
        P.add(torus(rr, 0.008, fc + Vector((0, 0, 0.07)), seg=20 if rr > 0.2 else 14, rseg=3), dark, wear=0.3, grime=0)
    for k in range(6):
        a = TAU * k / 6 + 0.2
        P.add(sweep([fc + Vector((0, 0, 0.07)), fc + Vector((math.cos(a) * 0.41, math.sin(a) * 0.41, 0.07))], 0.009, n=4), dark, wear=0.3, grime=0)
    P.add(cyl(0.05, 0.03, fc + Vector((0, 0, 0.06)), n=10, bev=0.01, seg=1), dark, wear=0.5, grime=0)
    # ailettes (grilles latérales) en façade et sur le côté
    for k in range(7):
        P.add(box((0.025, 0.7, 0.03), (W / 2 + 0.006, 0, 0.26 + k * 0.07), rot=(0, math.radians(-30), 0), bev=0.008, seg=1), "#6E6A62", wear=0.6)
    P.add(box((0.02, 0.76, 0.52), (W / 2 - 0.004, 0, 0.47), bev=0.01, seg=1), "#26282F", wear=0, grime=0, noise_amt=0)
    # coffret électrique vissé en façade (-Y)
    P.add(box((0.36, 0.06, 0.3), (-0.3, -D / 2 - 0.02, 0.5), bev=0.02, seg=2), "#B9AC94", wear=0.9)
    for (sx, sz) in ((-0.15, 0.13), (0.15, 0.13), (-0.15, -0.13), (0.15, -0.13)):
        P.add(cyl(0.012, 0.01, (-0.3 + sx * 0.95, -D / 2 - 0.052, 0.5 + sz * 0.9), (math.pi / 2, 0, 0), n=6), "#6E7488", wear=0, grime=0)
    P.add(box((0.12, 0.012, 0.05), (-0.3, -D / 2 - 0.054, 0.56), bev=0.004, seg=1), "#F0B92A", wear=0, grime=0)
    # liaisons frigorifiques isolées sortant sur le côté -X jusqu'au sol
    for k, (yy, r) in enumerate(((-0.12, 0.035), (0.05, 0.045))):
        path = fillet([(-W / 2 + 0.05, yy, 0.42 - k * 0.08), (-W / 2 - 0.22, yy, 0.42 - k * 0.08), (-W / 2 - 0.22, yy, 0.02), (-W / 2 - 0.6, yy, 0.02)], 0.12, k=4)
        P.add(sweep(path, r, n=8), "#2E3240" if k == 0 else "#3A3F52", wear=0.4)
        P.add(cyl(r + 0.012, 0.03, (-W / 2 - 0.01, yy, 0.42 - k * 0.08), (0, math.pi / 2, 0), n=8), "#C07A4A", wear=0.5)
    return P


def round_poly(pts, r, k=3):
    """Polygone 2D fermé aux coins arrondis."""
    out = []
    m = len(pts)
    for i in range(m):
        a, b, c = Vector(pts[i - 1]), Vector(pts[i]), Vector(pts[(i + 1) % m])
        d1, d2 = (a - b).normalized(), (c - b).normalized()
        rr = min(r, (a - b).length * 0.45, (c - b).length * 0.45)
        p1, p2 = b + d1 * rr, b + d2 * rr
        for s in range(k + 1):
            t = s / k
            q = (1 - t) ** 2 * p1 + 2 * (1 - t) * t * b + t * t * p2
            out.append((q.x, q.y))
    return out


@prop
def escalier_secours():
    """Module d'escalier de secours (3 m de large, palier à 3 m) : palier en caillebotis, garde-corps à barreaux,
    volée raide à limons et marches, consoles en équerre, platines d'ancrage. Mur côté -Y (Y = 0)."""
    P = Prop("escalier_secours", ao_dist=0.4, ao_strength=0.7)
    iron, rust = "#263F47", "#7E5A44"
    W, Dp, Z = 3.0, 1.0, 3.0
    deck = box((W, Dp, 0.06), (0, Dp / 2 + 0.02, Z), bev=0.012, seg=1)
    bisect(deck, [((-W / 2 + k * 0.15, 0, 0), (1, 0, 0)) for k in range(1, 20)])
    P.add(deck, lambda p, n, f: C("#3A3E52") * (0.7 if (int((f.x + 5) / 0.15) % 2 and n.z > 0.5) else 1.0), wear=0.6, wear_col="#8C93A5")
    for sy in (0.04, Dp):   # cornières de rive
        P.add(box((W, 0.05, 0.12), (0, sy, Z - 0.03), bev=0.012, seg=1), iron, wear=0.8, wear_col=rust)
    for sx in (-1, 1):
        P.add(box((0.05, Dp, 0.12), (sx * W / 2, Dp / 2 + 0.02, Z - 0.03), bev=0.012, seg=1), iron, wear=0.8, wear_col=rust)
    # garde-corps : poteaux, lisses arrondies, barreaux
    H = 1.0
    rail = [(-W / 2, 0.04, Z + H), (-W / 2, Dp, Z + H), (W / 2, Dp, Z + H), (W / 2, 0.04, Z + H)]
    P.add(sweep(fillet(rail, 0.12, k=2), 0.028, n=8), iron, wear=0.5, wear_col=rust)
    mid = [(x, y, Z + 0.5) for (x, y, _z) in rail]
    P.add(sweep(fillet(mid, 0.12, k=2), 0.018, n=6), iron, wear=0.4, wear_col=rust)
    for (x, y) in ((-W / 2, 0.04), (-W / 2, Dp), (0, Dp), (W / 2, Dp), (W / 2, 0.04)):
        P.add(cyl(0.03, H, (x, y, Z), n=8), iron, wear=0.5, wear_col=rust)
        P.add(sphere(0.042, (x, y, Z + H + 0.01), sub=1), iron, wear=0.9, wear_col=rust)
    for k in range(1, 15):
        x = -W / 2 + k * W / 15
        if abs(x - 0.0) < 0.05 or x > 0.55:   # trémie de l'escalier côté +X
            continue
        P.add(box((0.022, 0.022, H), (x, Dp, Z + H / 2), bev=0.0), iron, wear=0.2, wear_col=rust)
    for k in range(1, 4):
        y = k * Dp / 4
        for sx in (-1, 1):
            P.add(box((0.022, 0.022, H), (sx * W / 2, y, Z + H / 2), bev=0.0), iron, wear=0.2, wear_col=rust)
    # volée : descend de (+X, palier) vers le sol, devant le palier
    sw = 0.75
    y0 = Dp + 0.05 + sw / 2
    x_top, x_bot = 1.35, -0.95
    steps = 13
    run = x_top - x_bot
    ang = math.atan2(Z, run)
    L = math.hypot(run, Z)
    for sy in (-1, 1):
        yy = y0 + sy * sw / 2
        P.add(box((L + 0.1, 0.04, 0.2), ((x_top + x_bot) / 2, yy, Z / 2), rot=(0, -ang, 0), bev=0.012, seg=1), iron, wear=0.5, wear_col=rust)
        hr = [(x_bot - 0.1, yy, 0.95), (x_top, yy, Z + 0.95), (x_top + 0.25, yy, Z + 0.95)]
        P.add(sweep(fillet(hr, 0.12, k=2), 0.024, n=8), iron, wear=0.5, wear_col=rust)
        for t in (0.0, 0.33, 0.66):
            x = x_bot + run * t + 0.05
            z = Z * t
            P.add(cyl(0.022, 0.95, (x, yy, z + 0.05), n=6), iron, wear=0.3, wear_col=rust)
    for i in range(steps):
        t = (i + 0.5) / steps
        x = x_bot + run * t
        z = Z * t
        st = box((0.24, sw, 0.035), (x, y0, z), bev=0.01, seg=1)
        P.add(st, lambda p, n, f: C("#3A3E52") * (1.15 if n.z > 0.5 else 0.9) * (0.85 + 0.3 * noise.noise(p * 9)), wear=0.8, wear_col="#9AA2B6")
    # consoles en équerre + platines contre le mur
    for x in (-1.2, 0.0, 1.2):
        P.add(box((0.06, 0.04, 0.9), (x, 0.03, Z - 0.5), bev=0.012, seg=1), "#4A5068", wear=0.6)
        P.add(box((0.05, 1.25, 0.05), (x, Dp / 2 - 0.05, Z - 0.48), rot=(math.atan2(0.8, 0.9), 0, 0), bev=0.01, seg=1), iron, wear=0.4, wear_col=rust)
        P.add(box((0.2, 0.03, 0.2), (x, 0.015, Z - 0.85), bev=0.01, seg=1), "#4A5068", wear=0.6)
    P.add(box((0.5, 0.25, 0.08), (x_bot - 0.08, y0, 0.04), bev=0.02, seg=2), lambda p, n, f: C("#7E7873"), wear=0.8)   # semelle béton
    return P


def _bubble(a, b, n=22, tail=True):
    pts = []
    for i in range(n):
        t = TAU * i / n
        if tail and 3.6 < t < 4.2:
            continue
        pts.append((math.cos(t) * a, math.sin(t) * b))
        if tail and abs(t - TAU * (i + 1) / n) and 3.4 < t < 3.6:
            pts.append((-a * 0.62, -b * 1.45))   # pointe de la bulle
    return pts


@prop
def enseigne_bulle():
    """Enseigne-drapeau néon en bulle de BD « 24/7 » (texte original), lisible des deux côtés, sur potences murales.
    Mur côté -Y (Y = 0), bas de l'enseigne à Z = 0 (la poser en hauteur)."""
    P = Prop("enseigne_bulle", ao_dist=0.25, ao_strength=0.6, grime_h=0.05)
    a, b = 0.72, 0.46
    cz, cy = 0.55, 0.42 + a   # centre (hauteur, éloignement du mur)
    rot = (0, 0, math.pi / 2)

    def to_world(ob):
        ob.data.transform(Matrix.Translation((0, cy, cz)) @ Matrix.Rotation(math.pi / 2, 4, "Z"))
        return ob
    outer = round_poly(_bubble(a + 0.07, b + 0.07), 0.05, 2)
    inner = round_poly(_bubble(a, b), 0.05, 2)
    P.add(to_world(prism(outer, 0.1, bev=0.03, seg=2, angle=40)), "#C93A6E", wear=0.8)
    P.add(to_world(prism(inner, 0.13, bev=0.02, seg=1, angle=40)), lambda p, n, f: C("#1C2140") * (1.0 + 0.3 * sstep(0.0, 1.1, p.z)), wear=0.4, grime=0)
    tube = [(x * 0.9, 0, z * 0.88) for (x, z) in round_poly(_bubble(a, b), 0.05, 2)]
    for side in (-1, 1):
        t = sweep(tube + [tube[0]], 0.02, n=5, caps=False, closed=False)
        t.data.transform(Matrix.Translation((0, side * 0.075, 0)))
        P.light(to_world(t), kind="neon_rose")
        txt = text_mesh("24/7", size=0.42, extrude=0.012, loc=(0, 0, 0), res=1)
        face = Matrix.Rotation(math.pi / 2, 4, "X") if side < 0 else Matrix.Rotation(math.pi, 4, "Z") @ Matrix.Rotation(math.pi / 2, 4, "X")
        txt.data.transform(Matrix.Translation((0, side * 0.078, 0.02)) @ face)
        P.light(to_world(txt), kind="neon_cyan")
    # potences murales + platine
    for zz in (cz + 0.3, cz - 0.3):
        P.add(sweep([(0, 0.0, zz), (0, cy - a + 0.1, zz)], 0.025, n=8), "#3A3F52", wear=0.8, wear_col="#A9B0C2")
        P.add(box((0.16, 0.03, 0.12), (0, 0.015, zz), bev=0.01, seg=1), "#4A5068", wear=0.7)
    P.add(box((0.12, 0.08, 0.14), (0, 0.05, cz - 0.45), bev=0.02, seg=1), "#4A5068", wear=0.6)   # boîtier transfo néon
    return P


@prop
def guirlande_ampoules():
    """Guirlande de 6 m (11 ampoules à douille) tendue entre deux mâts en bois lestés (X = 0 et X = 6), accroche à 2,9 m,
    flèche de 0,6 m."""
    P = Prop("guirlande_ampoules", ao_dist=0.15, ao_strength=0.5, grime_h=0.01)
    Lg, sag, Za = 6.0, 0.6, 2.9
    pts = [(Lg * i / 40, 0, Za - 4 * sag * (i / 40) * (1 - i / 40)) for i in range(41)]
    P.add(sweep(pts, 0.01, n=4), "#23252F", wear=0, grime=0, noise_amt=0)
    for x in (0.0, Lg):   # mâts en bois sur pied lesté
        P.add(cyl(0.065, Za + 0.25, (x, 0, 0.12), n=8, bev=0.015, seg=1), lambda p, n, f: C("#6E4428") * (0.85 + 0.25 * noise.noise(Vector((p.x * 9, p.y * 9, p.z * 0.8)))), wear=0.7)
        P.add(cyl(0.2, 0.14, (x, 0, 0.0), n=10, bev=0.03, seg=2), lambda p, n, f: mix(C("#6E675F"), C("#9A9187"), sstep(0, 0.14, p.z)), wear=0.8)
        P.add(sphere(0.06, (x, 0, Za + 0.38), sub=1), "#2A2D3A", wear=0.5, grime=0)
        P.add(torus(0.035, 0.008, (x + (0.05 if x == 0 else -0.05), 0, Za), (math.pi / 2, 0, 0), seg=8, rseg=4), "#8C93A5", wear=0, grime=0)
    cols = ["#FFC36B", "#FF9A4D", "#FFE08A"]
    for i in range(11):
        u = (i + 0.5) / 11
        x = Lg * u
        z = Za - 4 * sag * u * (1 - u)
        P.add(cyl(0.03, 0.07, (x, 0, z - 0.075), n=6), "#2A2D3A", wear=0.5, grime=0)
        P.add(sweep([(x, 0, z), (x, 0, z - 0.01)], 0.006, n=4), "#23252F", wear=0, grime=0)
        bulb = lathe([(0.0, -0.2), (0.035, -0.19), (0.058, -0.155), (0.055, -0.115), (0.03, -0.08), (0.0, -0.075)], n=8, loc=(x, 0, z))
        P.light(bulb, color=cols[i % 3], strength=4.0)
    return P


@prop
def lampadaire():
    """Lampadaire rétro 4,3 m : socle évasé à moulures, fût conique à bagues, crosse en volute, lanterne à capot et
    diffuseur lumineux (chaud)."""
    P = Prop("lampadaire", ao_dist=0.3, ao_strength=0.7)
    teal, teal_w = "#27495B", "#7FA7B5"
    base = [(0.0, 0.0), (0.24, 0.0), (0.245, 0.04), (0.22, 0.06), (0.2, 0.12), (0.17, 0.16), (0.175, 0.2), (0.15, 0.22),
            (0.12, 0.4), (0.105, 0.55), (0.12, 0.58), (0.12, 0.62), (0.095, 0.65), (0.085, 0.66)]
    pole = base + [(0.075, 0.75), (0.065, 2.4), (0.08, 2.43), (0.08, 2.48), (0.062, 2.51), (0.055, 3.9), (0.072, 3.93), (0.072, 3.99), (0.05, 4.02), (0.0, 4.05)]
    P.add(lathe(pole, n=16), lambda p, n, f: mix(C("#1E3A48"), C(teal), sstep(0.0, 2.5, p.z)), wear=0.9, wear_col=teal_w)
    P.add(sphere(0.07, (0, 0, 4.08), sub=2), teal, wear=0.9, wear_col=teal_w)
    arm = fillet([(0, 0, 3.8), (0.15, 0, 4.05), (0.95, 0, 4.05)], 0.3, k=6)
    P.add(sweep(arm, 0.035, n=8), teal, wear=0.9, wear_col=teal_w)
    curl = [(0.08 + 0.18 * math.cos(t) * (1 - t / 9), 0, 3.85 + 0.12 * math.sin(t) * (1 - t / 9)) for t in [i * 0.45 for i in range(14)]]
    P.add(sweep([(0.6, 0, 4.03)] + [(0.45 - 0.2 * math.sin(t) * (1 - t / 8), 0, 3.86 + 0.13 * math.cos(t) * (1 - t / 8)) for t in [i * 0.45 for i in range(12)]], 0.015, n=6), teal, wear=0.6, wear_col=teal_w)
    # lanterne
    hx = 0.95
    hood = [(0.0, 0.18), (0.05, 0.18), (0.1, 0.15), (0.2, 0.08), (0.27, 0.02), (0.28, 0.0), (0.26, -0.015), (0.2, -0.01), (0.0, -0.01)]
    P.add(lathe(hood, n=18, loc=(hx, 0, 3.95)), lambda p, n, f: C(teal) * (1.15 if n.z > 0.3 else 0.8), wear=0.9, wear_col=teal_w)
    P.add(sphere(0.04, (hx, 0, 4.15), sub=1), teal, wear=0.8, wear_col=teal_w)
    lens = lathe([(0.0, -0.14), (0.08, -0.13), (0.16, -0.08), (0.2, -0.02), (0.2, -0.01), (0.0, -0.01)], n=18, loc=(hx, 0, 3.95))
    P.light(lens, color="#FFE2A8", strength=3.0)
    P.add(box((0.2, 0.012, 0.36), (0, -0.07, 1.3), bev=0.006, seg=1), "#F0B92A", wear=0.3)   # plaque de service
    P.add(box((0.12, 0.035, 0.2), (0, -0.075, 0.9), bev=0.01, seg=1), "#1E3A48", wear=0.8, wear_col=teal_w)   # trappe de visite
    return P


@prop
def poubelle():
    """Poubelle de rue : cuve à nervures verticales, ceinture jaune, couvercle en dôme à ouverture, sac qui dépasse,
    socle, papier froissé."""
    P = Prop("poubelle", ao_dist=0.25, ao_strength=0.75, grime_h=0.3)
    n = 32

    def rib(r):
        return lambda a: r + (0.012 if (int(round(a / TAU * n)) % 2 == 0) else 0.0)
    prof = [(0.0, 0.0), (0.24, 0.0), (0.25, 0.02), (0.245, 0.06), (rib(0.255), 0.07), (rib(0.27), 0.5), (rib(0.275), 0.62),
            (0.29, 0.63), (0.295, 0.65), (0.295, 0.72), (0.29, 0.74), (rib(0.278), 0.75), (rib(0.282), 0.84), (0.29, 0.85),
            (0.29, 0.87), (0.27, 0.875), (0.0, 0.875)]
    body, body_d = C("#2E7A5B"), C("#1F5641")

    def bfn(p, nn, f):
        if 0.63 <= f.z <= 0.74:
            return C("#E9B43A")
        return mix(body_d, body, sstep(0.05, 0.8, p.z))
    P.add(lathe(prof, n=n), bfn, wear=0.7, wear_col="#8FB5A2")
    lid = [(0.0, 1.13), (0.06, 1.125), (0.16, 1.09), (0.22, 1.04), (0.25, 0.99), (0.265, 0.98), (0.27, 0.965), (0.0, 0.965)]
    P.add(lathe(lid, n=24), lambda p, nn, f: C("#2D3142") * (1.2 if nn.z > 0.6 else 1.0), wear=0.8, wear_col="#8C93A5")
    P.add(cyl(0.255, 0.1, (0, 0, 0.875), n=24, bev=0.0), "#14161E", wear=0, grime=0, noise_amt=0)   # ouverture sombre
    for k in range(4):   # montants entre cuve et couvercle
        a = TAU * k / 4 + 0.4
        P.add(box((0.05, 0.035, 0.12), (math.cos(a) * 0.262, math.sin(a) * 0.262, 0.92), rot=(0, 0, a), bev=0.012, seg=1), "#2D3142", wear=0.7)
    bag = lathe([(0.26, 0.8), (0.285, 0.86), (0.3, 0.885), (0.28, 0.9), (0.25, 0.86)], n=16)
    displace(bag, lambda co: co + Vector((co.x, co.y, 0)).normalized() * 0.012 * noise.noise(co * 14))
    P.add(bag, "#3A3F33", wear=0.3, grime=0)
    P.add(lathe([(0.0, 0.0), (0.3, 0.0), (0.31, 0.02), (0.3, 0.05), (0.0, 0.05)], n=24), "#3A3E52", wear=0.8)
    pap = sphere(0.06, (0.38, 0.1, 0.055), sub=2)
    displace(pap, lambda co: co + (co - Vector((0.38, 0.1, 0.055))) * 0.45 * noise.noise(co * 22))
    P.add(pap, "#D9D2C0", wear=0.6, grime=0.3)
    P.add(sphere(0.035, (0, 0, 1.135), sub=1, scale=(1, 1, 0.7)), "#E9B43A", wear=0.4, grime=0)
    return P


@prop
def distributeur():
    """Distributeur de boissons (marque fictive « SODA ») : caisse arrondie bicolore, vitrine rétroéclairée à 4 rayons de
    canettes, colonne de sélection (boutons lumineux, afficheur, fente à pièces), trappe de sortie, fronton lumineux."""
    P = Prop("distributeur", ao_dist=0.35, ao_strength=0.75)
    W, D, H = 1.0, 0.8, 1.95
    red, red_d, cream = C("#D2443A"), C("#9C2E2A"), C("#EADBB8")
    shell = box((W, D, H), (0, 0, 0.06 + H / 2), bev=0.07, seg=2)
    boolean(shell, box((0.62, 0.5, 1.12), (-0.12, -D / 2, 0.62 + 0.56), bev=0.0))   # niche de la vitrine
    bisect(shell, [((0, 0, z), (0, 0, 1)) for z in (0.5, 0.95, 1.4)])

    def sfn(p, n, f):
        c = mix(red_d, red, sstep(0.1, 1.6, p.z))
        if f.y > -D / 2 + 0.02 and f.y < -D / 2 + 0.3 and abs(f.x) < 0.45 and abs(n.y) < 0.6:
            c = C("#2A2D3A")   # intérieur de la niche
        return c
    P.add(shell, sfn, wear=0.9, wear_col="#F08A78")
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.add(cyl(0.05, 0.06, (sx * 0.4, sy * 0.3, 0.0), n=6), "#2A2D3A", wear=0.5)
    fy = -D / 2
    # vitrine : cadre, fond lumineux, rayons, canettes
    vx, vw, vz, vh = -0.12, 0.62, 0.62, 1.12
    for (sz, ox) in (((vw + 0.1, 0.06, 0.06), (vx, fy - 0.01, vz - 0.02)), ((vw + 0.1, 0.06, 0.06), (vx, fy - 0.01, vz + vh + 0.02)),
                     ((0.06, 0.06, vh + 0.1), (vx - vw / 2 - 0.02, fy - 0.01, vz + vh / 2)), ((0.06, 0.06, vh + 0.1), (vx + vw / 2 + 0.02, fy - 0.01, vz + vh / 2))):
        P.add(box(sz, ox, bev=0.02, seg=1), "#2A2D3A", wear=0.8, wear_col="#8C93A5")
    P.light(box((vw, 0.02, vh), (vx, fy + 0.2, vz + vh / 2), bev=0.0), color="#BDEBFF", strength=1.0)
    cans = ["#F0B92A", "#2FA3C7", "#E0457B", "#6CC24A"]
    for r in range(4):
        z = vz + 0.06 + r * 0.27
        P.add(box((vw, 0.22, 0.02), (vx, fy + 0.09, z - 0.01), bev=0.0), "#C9CFDB", wear=0.3, grime=0)
        for c in range(5):
            x = vx - vw / 2 + 0.07 + c * 0.12
            P.add(cyl(0.045, 0.15, (x, fy + 0.08, z), n=8), cans[(r + c // 2) % 4], wear=0.6, wear_col="#E9EDF5", grime=0)
        P.add(box((vw, 0.012, 0.035), (vx, fy - 0.035, z - 0.005), bev=0.0), "#F0B92A", wear=0.2, grime=0)   # étiquettes de prix
    # colonne de sélection
    cx = 0.34
    P.add(box((0.22, 0.04, 1.2), (cx, fy - 0.005, 1.2), bev=0.02, seg=1), "#2A2D3A", wear=0.7, wear_col="#8C93A5")
    P.light(box((0.15, 0.02, 0.09), (cx, fy - 0.03, 1.66), bev=0.005, seg=1), kind="neon_cyan")
    for i in range(6):
        P.light(box((0.07, 0.03, 0.05), (cx - 0.04 + 0.08 * (i % 2), fy - 0.03, 1.48 - 0.08 * (i // 2)), bev=0.01, seg=1), color=["#FFE08A", "#BDEBFF"][i % 2], strength=1.2)
    P.add(box((0.08, 0.02, 0.12), (cx, fy - 0.03, 1.12), bev=0.008, seg=1), "#C9CFDB", wear=0.5)   # monnayeur
    P.add(box((0.012, 0.01, 0.06), (cx, fy - 0.042, 1.13), bev=0.0), "#14161E", wear=0, grime=0)
    P.add(box((0.14, 0.05, 0.1), (cx, fy - 0.01, 0.82), bev=0.02, seg=1), "#14161E", wear=0.3)   # rendu monnaie
    # trappe de sortie
    P.add(box((0.6, 0.05, 0.24), (-0.1, fy - 0.005, 0.32), bev=0.03, seg=1), "#2A2D3A", wear=0.7, wear_col="#8C93A5")
    P.add(box((0.5, 0.03, 0.16), (-0.1, fy - 0.03, 0.33), rot=(0.25, 0, 0), bev=0.015, seg=1), "#4A5068", wear=0.8, wear_col="#A9B0C2")
    # fronton lumineux + texte néon
    P.add(box((W - 0.08, 0.06, 0.24), (0, fy - 0.01, 1.86), bev=0.03, seg=1), "#2A2D3A", wear=0.7)
    P.add(box((W - 0.16, 0.02, 0.18), (0, fy - 0.035, 1.86), bev=0.0), "#1C2140", wear=0, grime=0)
    for (bx, bz, br) in ((0.0, 1.25, 0.16), (0.14, 1.02, 0.09), (-0.1, 0.92, 0.06), (0.1, 1.5, 0.05)):   # bulles décoratives sur les flancs
        for sx in (-1, 1):
            P.add(cyl(br, 0.012, (sx * (W / 2 - 0.004), bx, bz), (0, sx * math.pi / 2, 0), n=14), "#EADBB8", wear=0.4, grime=0)
            P.add(cyl(br * 0.62, 0.016, (sx * (W / 2 - 0.004), bx, bz), (0, sx * math.pi / 2, 0), n=10), "#D2443A", wear=0, grime=0)
    for k in range(5):   # grille d'aération au dos
        P.add(box((0.6, 0.03, 0.03), (0, D / 2 + 0.005, 0.35 + k * 0.07), bev=0.008, seg=1), "#2A2D3A", wear=0.5)
    t = text_mesh("SODA", size=0.17, extrude=0.012, res=1)
    t.data.transform(Matrix.Translation((0, fy - 0.05, 1.85)) @ Matrix.Rotation(math.pi / 2, 4, "X"))
    P.light(t, kind="neon_rose")
    return P


@prop
def passage_pieton():
    """Plateau surélevé de passage piéton (4 × 3,2 m, 10 cm) : rampes, bandes blanches en relief usées, dents de requin
    jaunes sur les rampes, bordures et plots réfléchissants."""
    P = Prop("passage_pieton", ao_dist=0.25, ao_strength=0.6, grime_h=0.02)
    Lx, Ly, Hh, ramp = 4.0, 3.2, 0.1, 0.5
    prof = [(-Ly / 2 - ramp, 0.0), (Ly / 2 + ramp, 0.0), (Ly / 2 + ramp, 0.01), (Ly / 2, Hh), (-Ly / 2, Hh), (-Ly / 2 - ramp, 0.01)]
    tab = prism(prof, Lx, rot=(0, 0, math.pi / 2), bev=0.0, cuts=0)
    # coupes pour les dents de requin (triangles sur les rampes) et l'usure
    planes = [((x, 0, 0), (1, 0, 0)) for x in [-Lx / 2 + k * 0.25 for k in range(1, 16)]]
    for k in range(8):   # flancs des dents de requin (plans verticaux en diagonale)
        x0 = -Lx / 2 + k * 0.5
        for sy in (-1, 1):
            for (xa, xb) in ((x0, x0 + 0.25), (x0 + 0.5, x0 + 0.25)):
                d = Vector((xb - xa, sy * (-ramp + 0.15), 0))
                planes.append(((xa, sy * (Ly / 2 + ramp), 0), (d.y, -d.x, 0)))
    bisect(tab, planes)
    bm = bmesh.new()
    bm.from_mesh(tab.data)
    _bevel(bm, 0.03, 2, angle=math.radians(4))
    bm.to_mesh(tab.data)
    bm.free()
    asph, asph_l = C("#3E3D4A"), C("#4E4D5B")

    def afn(p, n, f):
        c = mix(asph, asph_l, 0.5 + 0.5 * noise.noise(p * 1.7))
        if abs(f.y) > Ly / 2 + 0.05 and n.z > 0.1:   # dents de requin : un triangle sur deux
            u = ((f.x + Lx / 2) % 0.5) / 0.5
            v = (Ly / 2 + ramp - abs(f.y)) / (ramp - 0.15)   # 0 au pied de la rampe, 1 à la pointe
            if v < 1 - abs(u - 0.5) * 2:
                return C("#E9B43A")
        return c
    P.add(tab, afn, wear=0.6, grime=0, noise_amt=0.08)
    rnd = random.Random(9)
    for i in range(7):   # bandes blanches en relief
        x = -Lx / 2 + 0.35 + i * 0.55
        st = box((0.36, Ly - 0.2, 0.012), (x, 0, Hh + 0.005), bev=0.006, seg=1)
        bisect(st, [((0, y, 0), (0, 1, 0)) for y in (-1.0, -0.4, 0.3, 0.9)])
        seed = rnd.random() * 10

        def sfn(p, n, f, seed=seed):
            w = noise.noise(Vector((p.x * 3 + seed, p.y * 2.5, 0)))
            return mix(C("#ECEAE2"), C("#8D8B95"), sstep(0.25, 0.6, w))
        P.add(st, sfn, wear=0.3, grime=0)
    for sx in (-1, 1):   # bordures de trottoir aux deux bouts
        P.add(box((0.25, Ly + 2 * ramp + 0.2, 0.18), (sx * (Lx / 2 + 0.125), 0, 0.09), bev=0.02, seg=1), lambda p, n, f: mix(C("#5E5852"), C("#857D74"), sstep(0, 0.18, p.z)), wear=0.9)
        for y in (-Ly / 2 - 0.25, Ly / 2 + 0.25):
            P.light(box((0.1, 0.06, 0.02), (sx * (Lx / 2 - 0.15), y, 0.06), bev=0.008, seg=1), color="#FFD36B", strength=1.2)
    return P


@prop
def panneau_avertissement():
    """Panneau d'avertissement : triangle à bord rouge, pictogramme original « balle qui fuse », panonceau « ZONE DE JEU »,
    poteau sur plot béton, colliers de fixation au dos."""
    P = Prop("panneau_avertissement", ao_dist=0.25, ao_strength=0.7, grime_h=0.15)
    P.add(cyl(0.16, 0.1, (0, 0, 0), n=14, bev=0.025, seg=2), lambda p, n, f: mix(C("#6E675F"), C("#9A9187"), sstep(0, 0.1, p.z)), wear=0.8)
    P.add(cyl(0.035, 2.35, (0, 0, 0.08), n=10), "#8C93A5", wear=0.3)
    P.add(cyl(0.04, 0.03, (0, 0, 2.42), n=10, bev=0.01, seg=1), "#2A2D3A", wear=0.4)   # bouchon
    cz = 2.0
    s = 0.42
    tri = [(-s, -s * 0.58), (s, -s * 0.58), (0, s * 0.97)]
    outer = round_poly(tri, 0.07, 3)
    inner = round_poly([(x * 0.78, z * 0.78 - 0.01) for (x, z) in tri], 0.05, 3)
    P.add(prism(outer, 0.025, (0, -0.06, cz), bev=0.008, seg=1, angle=50), lambda p, n, f: C("#D2343A") if n.y < 0.5 else C("#9AA2B6"), wear=0.5, grime=0)
    P.add(prism(inner, 0.008, (0, -0.075, cz), bev=0.003, seg=1, angle=50), "#F4C431", wear=0.2, grime=0)
    ink = "#1F2130"
    ball = cyl(0.075, 0.006, (0.08, -0.08, cz - 0.04), (math.pi / 2, 0, 0), n=16)
    P.add(ball, ink, wear=0, grime=0)
    for k, (dz, ln) in enumerate(((0.05, 0.16), (0.0, 0.22), (-0.05, 0.14))):   # traînées de vitesse
        P.add(box((ln, 0.006, 0.028), (-0.04 - ln / 2, -0.081, cz - 0.04 + dz), bev=0.004, seg=1), ink, wear=0, grime=0)
    P.add(box((0.16, 0.006, 0.02), (0.0, -0.081, cz - 0.2), bev=0.0), ink, wear=0, grime=0)   # sol du pictogramme
    # panonceau
    P.add(box((0.62, 0.02, 0.17), (0, -0.06, 1.55), bev=0.012, seg=2), lambda p, n, f: C("#F2EFE6") if n.y < -0.5 else C("#9AA2B6"), wear=0.4, grime=0)
    t = text_mesh("ZONE DE JEU", size=0.085, extrude=0.004, res=1)
    t.data.transform(Matrix.Translation((0, -0.072, 1.55)) @ Matrix.Rotation(math.pi / 2, 4, "X"))
    P.add(t, ink, wear=0, grime=0)
    for zz in (cz - 0.12, cz + 0.12, 1.55):   # colliers
        P.add(torus(0.042, 0.01, (0, 0, zz), seg=10, rseg=4), "#5B6278", wear=0.6)
        P.add(box((0.05, 0.06, 0.04), (0, -0.04, zz), bev=0.008, seg=1), "#5B6278", wear=0.6)
    return P


# ---------------------------------------------------------------------- registre
PROPS = {f.__name__: f for f in _REG}


def build(name, out_dir):
    lib.reset()
    P = PROPS[name]()
    objs = P.bake()
    path = os.path.join(out_dir, name + ".glb")
    n = lib.export(path, objs)
    return n


def main(out_dir, names=None):
    os.makedirs(out_dir, exist_ok=True)
    res = {}
    for nm in names or list(PROPS):
        res[nm] = build(nm, out_dir)
        print(f"{nm:20s} {res[nm]:6d} triangles")
    return res


if __name__ == "__main__":
    argv = sys.argv[1:]
    main(argv[0] if argv else "assets/models/props", argv[1:] or None)
