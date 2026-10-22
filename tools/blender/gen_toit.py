"""Exporte la carte Toit : assets/models/toit.glb (décor proche) et toit_ville.glb (skyline), plus leurs versions _quest
(détails retirés, fusion par matériau).
Usage : python3 -I tools/blender/gen_toit.py assets/models"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402

import lib  # noqa: E402
import map_toit  # noqa: E402

out = sys.argv[1]
os.makedirs(out, exist_ok=True)
import city_kit  # noqa: E402
import quest  # noqa: E402

for name, fn, light in (("toit", map_toit.build_toit, False), ("toit_ville", map_toit.build_ville, False),
                        ("toit_quest", map_toit.build_toit, True), ("toit_ville_quest", map_toit.build_ville, True)):
    lib.reset()
    city_kit.QUEST = light
    fn()
    if light:   # Quest : un maillage par matériau (quelques appels de rendu au lieu de centaines)
        quest.merge_by_material()
    print(name, lib.export(os.path.join(out, name + ".glb")), "triangles")
