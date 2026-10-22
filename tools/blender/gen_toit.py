"""Exporte la carte Toit : assets/models/toit.glb (décor proche) et toit_ville.glb (skyline), plus leurs versions _quest
(détails retirés, fusion par matériau).
Autres cartes : toit_fosse, toit_tour, toit_court, toit_long (même toit, dimensions et fosse de chaque carte, voir map_toit.VARIANTS).
Usage : python3 -I tools/blender/gen_toit.py assets/models [noms...]"""
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

only = sys.argv[2:]   # noms à reconstruire (par défaut : tout)
jobs = [("toit_ville", map_toit.build_ville, False), ("toit_ville_quest", map_toit.build_ville, True)]
for v in map_toit.VARIANTS:   # toit de chaque carte (toit = croix_neon) ; la ville est commune
    jobs += [(v, map_toit.build_toit, False), (v + "_quest", map_toit.build_toit, True)]
for name, fn, light in jobs:
    if only and name not in only:
        continue
    lib.reset()
    map_toit.configure(name.replace("_quest", "") if fn is map_toit.build_toit else "toit")
    city_kit.QUEST = light
    fn()
    if light:   # Quest : un maillage par matériau (et par secteur pour la ville, qui entoure le joueur)
        quest.merge_by_material(sectors=8 if name.startswith("toit_ville") else 1)
    print(name, lib.export(os.path.join(out, name + ".glb")), "triangles")
