"""Illustrations des capsules Steam (rendu Cycles complet) : Riko, Bo, Nova et Kai en plein échange sur le toit de Croix Néon.

Trois rendus maîtres, puis découpe et logo (tools/blender/capsules_compose.py) :
    large   2560 × 1440 : capsule principale, en-tête, petite capsule, fond de page
    vertical 1200 × 1440 : capsule verticale, capsule de bibliothèque
    hero    3840 × 1240 : fond de bibliothèque (sans texte)

    python3 -I tools/blender/capsules.py <dossier> [large|vertical|hero ...] [--test]
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import ball_kit  # noqa: E402
import hero  # noqa: E402
import kc_render  # noqa: E402
import lib  # noqa: E402
import map_toit  # noqa: E402
import rig as rigmod  # noqa: E402
from props_kit import sweep  # noqa: E402

# (équipe, variante, action, image, position, regard en degrés : 0 = vers +X)
DY = 9.0   # scène décalée vers le parapet nord : immeubles derrière, pas le cercle sombre du moyeu
CAST = [
    ("orange", 0, "Lancer", 5, (-2.2, DY + 0.2, 0), -28),       # Riko lâche la balle
    ("orange", 1, "Course", 6, (-4.6, DY + 2.0, 0), -15),       # Bo arrive en renfort
    ("bleu", 0, "Esquive", 6, (2.6, DY - 0.6, 0), 218),        # Nova plonge sous le tir
    ("bleu", 1, "Attrape", 1, (4.6, DY + 1.7, 0), 205),        # Kai tend les mains
]
BALL = Vector((0.9, DY - 0.25, 1.62))

SHOTS = {   # caméra : position, cible, focale (mm), résolution
    "large": ((0.2, DY - 5.6, 0.95), (0.2, DY + 0.6, 1.8), 25, (2560, 1440)),
    "vertical": ((0.1, DY - 4.4, 0.95), (0.1, DY + 0.2, 1.65), 24, (1200, 1440)),
    "hero": ((0.2, DY - 7.6, 1.3), (0.2, DY + 0.6, 1.75), 22, (3840, 1240)),
}


def rough_from_alpha():
    """Le jeu lit la rugosité dans l'alpha des couleurs de sommet : même branchement pour le rendu."""
    m = bpy.data.materials.get("peinture")
    if m is None:
        return
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    vc = next(n for n in nt.nodes if n.type == "VERTEX_COLOR")
    nt.links.new(vc.outputs["Alpha"], b.inputs["Roughness"])


def pose_cast():
    for team, variant, act_name, frame, loc, yaw in CAST:
        before = set(bpy.data.actions)
        rig, body = rigmod.rig_character(hero.hero(team, variant), rigmod.HERO_BONES, arm_rest=-14)
        acts = {a.name.split(".")[0]: a for a in set(bpy.data.actions) - before}
        for t in rig.animation_data.nla_tracks:
            t.mute = True
        rig.animation_data.action = acts[act_name]
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        mats = {pb.name: pb.matrix_basis.copy() for pb in rig.pose.bones}
        rig.animation_data_clear()
        for pb in rig.pose.bones:
            pb.matrix_basis = mats[pb.name]
        # le modèle regarde +Y : on le tourne vers la direction voulue
        rig.matrix_world = Matrix.Translation(Vector(loc)) @ Matrix.Rotation(math.radians(yaw - 90), 4, "Z") @ rig.matrix_world
    bpy.context.scene.frame_set(1)


def ball_and_trail():
    objs = ball_kit.parts("normale")
    glow = bpy.data.materials["neon_balle"]
    b = glow.node_tree.nodes["Principled BSDF"]
    orange = lib.hex_rgba("#FF7A1A")
    b.inputs["Base Color"].default_value = orange
    b.inputs["Emission Color"].default_value = orange
    b.inputs["Emission Strength"].default_value = 8.0
    for o in objs:
        o.location = BALL
        o.rotation_euler = (0.5, 0.9, 0.2)
    # traînée : arc depuis la main de Riko, ruban orange et cœur blanc qui s'effilent vers l'arrière
    start = Vector((-1.8, DY + 0.3, 1.85))
    pts = []
    for i in range(24):
        t = i / 23
        p = start.lerp(BALL, t)
        p.z += math.sin(t * math.pi) * 0.12
        pts.append(p)
    for r, col, e, name in ((0.11, "#FF8A2A", 3.0, "Trainee"), (0.035, "#FFF2D8", 6.0, "TraineeCoeur")):
        tr = sweep(pts, r, n=10, name=name, rfn=lambda i, t, r=r: r * (0.15 + 0.85 * t ** 1.5))
        m = lib.mat("trainee_" + name, col, emissive=e)
        nt = m.node_tree
        pb = nt.nodes["Principled BSDF"]
        pb.inputs["Alpha"].default_value = 0.55 if name == "Trainee" else 0.9
        tr.data.materials.append(m)
    # lignes de vitesse fines autour de la balle
    for k in range(5):
        a = k / 5 * math.tau
        off = Vector((0, math.cos(a) * 0.26, math.sin(a) * 0.26))
        seg = [BALL + off + Vector((-0.25 - 0.7 * s, 0, 0)) for s in (0, 0.5, 1)]
        ln = sweep(seg, 0.008, n=6, name=f"Vitesse{k}")
        ln.data.materials.append(lib.mat("vitesse", "#FFFFFF", emissive=2.0))


def render(out, names, test=False):
    s = bpy.context.scene
    s.render.engine = "CYCLES"
    s.cycles.device = "CPU"
    s.cycles.samples = 12 if test else 72
    s.cycles.use_denoising = True
    s.cycles.max_bounces = 6
    try:
        s.cycles.denoiser = "OPENIMAGEDENOISE"
    except Exception:
        pass
    vt = bpy.types.ColorManagedViewSettings.bl_rna.properties["view_transform"].enum_items.keys()
    s.view_settings.view_transform = "AgX" if "AgX" in vt else "Filmic"
    s.view_settings.look = "AgX - Punchy" if "AgX - Punchy" in [i.identifier for i in bpy.types.ColorManagedViewSettings.bl_rna.properties["look"].enum_items] else "None"
    bpy.ops.object.camera_add()
    cam = bpy.context.active_object
    s.camera = cam
    for n in names:
        loc, tgt, lens, (rx, ry) = SHOTS[n]
        k = 0.25 if test else 1.0
        s.render.resolution_x, s.render.resolution_y = int(rx * k), int(ry * k)
        cam.location = loc
        cam.data.lens = lens
        cam.data.dof.use_dof = not test
        cam.data.dof.focus_distance = (Vector(tgt) - Vector(loc)).length
        cam.data.dof.aperture_fstop = 4.0
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        s.render.filepath = os.path.join(out, f"maitre_{n}.png")
        bpy.ops.render.render(write_still=True)
        print("rendu", n, flush=True)


if __name__ == "__main__":
    args = sys.argv[1:]
    test = "--test" in args
    args = [a for a in args if a != "--test"]
    out = args[0]
    os.makedirs(out, exist_ok=True)
    lib.reset()
    map_toit.build_toit()
    map_toit.build_ville()
    for o in list(bpy.data.objects):   # cadrage : pas de disque intérieur du moyeu (rendu noir) ni de gros tuyaux en haut
        if o.name.startswith(("AnneauInt", "Tuyau", "Collier")):
            bpy.data.objects.remove(o, do_unlink=True)
    kc_render.sky()
    pose_cast()
    ball_and_trail()
    rough_from_alpha()
    render(out, args[1:] or list(SHOTS), test)
