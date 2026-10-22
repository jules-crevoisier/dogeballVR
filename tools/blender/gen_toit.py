"""Exporte la carte Toit : assets/models/toit.glb (décor proche) et toit_ville.glb (skyline).
Usage : python3 -I tools/blender/gen_toit.py assets/models"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402

import lib  # noqa: E402
import map_toit  # noqa: E402

out = sys.argv[1]
os.makedirs(out, exist_ok=True)
for name, fn in (("toit", map_toit.build_toit), ("toit_ville", map_toit.build_ville)):
    lib.reset()
    fn()
    print(name, lib.export(os.path.join(out, name + ".glb")), "triangles")
