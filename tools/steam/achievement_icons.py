#!/usr/bin/env python3
"""Icônes des succès Steam (256x256, version couleur et version grisée) d'après docs/steam/achievements.csv.

Usage : python3 tools/steam/achievement_icons.py [DOSSIER]   (par défaut docs/steam/succes-icones)
Style des menus du jeu : rayons orange et marine, pastille jaune, police Lilita One. Une planche contact est écrite à côté.
"""
import csv
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FONT = os.path.join(ROOT, "assets", "fonts", "LilitaOne-Regular.ttf")
NAVY, NAVY_SOFT, ORANGE, BLUE, YELLOW, CREAM = (16, 24, 48), (28, 42, 82), (255, 106, 26), (58, 123, 213), (255, 210, 63), (255, 244, 224)
SIZE, SS = 256, 4   # dessin 4 fois plus grand puis réduit : bords lisses

# Gros texte et légende de chaque succès ; « * » dessine une étoile.
GLYPHS = {
    "ACH_FIRST_WIN": ("1", "VICTOIRE", ORANGE),
    "ACH_KO_10": ("10", "KO", ORANGE),
    "ACH_KO_100": ("100", "KO", ORANGE),
    "ACH_PERFECT_CATCH": ("*", "PARFAIT", BLUE),
    "ACH_CATCH_50": ("50", "ATTRAPES", BLUE),
    "ACH_ASSIST": ("+1", "PASSE", BLUE),
    "ACH_CHARGED_KO": ("III", "CHARGÉ", ORANGE),
    "ACH_RING_OUT": ("OUT", "FOSSE", ORANGE),
    "ACH_TACKLE": ("!", "TACLE", ORANGE),
    "ACH_FLAWLESS": ("3-0", "SANS BAVURE", ORANGE),
    "ACH_ONLINE_FRIEND": ("2+", "ENTRE POTES", BLUE),
    "ACH_VR_PLAYER": ("VR", "IMMERSION", BLUE),
    "ACH_WINS_10": ("10", "VICTOIRES", ORANGE),
    "ACH_WINS_50": ("50", "VICTOIRES", ORANGE),
    "ACH_MATCHES_25": ("25", "MATCHS", BLUE),
    "ACH_PERFECT_25": ("25", "PARFAITS", BLUE),
    "ACH_PASS_50": ("50", "PASSES", BLUE),
    "ACH_HAT_TRICK": ("x3", "KO", ORANGE),
    "ACH_WALL": ("x5", "ATTRAPES", BLUE),
    "ACH_MVP": ("*", "MVP", ORANGE),
    "ACH_HARD_BOTS": ("BOT", "DIFFICILE", ORANGE),
    "ACH_LEVEL_10": ("10", "NIVEAU", BLUE),
    "ACH_LEVEL_25": ("25", "NIVEAU", BLUE),
}


def star(draw, cx, cy, r, fill, outline, width):
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        rr = r if i % 2 == 0 else r * 0.45
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    draw.polygon(pts, fill=fill, outline=outline, width=width)


def fit(text, max_w, start):
    size = start
    while size > 10:
        f = ImageFont.truetype(FONT, size)
        if f.getbbox(text)[2] <= max_w:
            return f
        size -= 4
    return ImageFont.truetype(FONT, size)


def icon(api, big, caption, accent):
    s = SIZE * SS
    img = Image.new("RGB", (s, s), NAVY)
    d = ImageDraw.Draw(img)
    # Rayons alternés autour du centre, comme l'écran de victoire.
    c = s / 2
    for i in range(16):
        a0, a1 = i * math.tau / 16, (i + 0.5) * math.tau / 16
        d.polygon([(c, c), (c + s * math.cos(a0), c + s * math.sin(a0)), (c + s * math.cos(a1), c + s * math.sin(a1))], fill=accent if i % 2 == 0 else NAVY_SOFT)
    # Pastille centrale jaune cerclée de marine.
    r = s * 0.36
    d.ellipse([c - r - 18 * SS / 4, c - r - 18 * SS / 4 + 8 * SS, c + r + 18 * SS / 4, c + r + 18 * SS / 4 + 8 * SS], fill=NAVY)
    d.ellipse([c - r - 18 * SS / 4, c - r - 18 * SS / 4, c + r + 18 * SS / 4, c + r + 18 * SS / 4], fill=NAVY)
    d.ellipse([c - r, c - r, c + r, c + r], fill=YELLOW)
    if big == "*":
        star(d, c, c - 10 * SS, r * 0.62, NAVY, None, 0)
    else:
        f = fit(big, r * 1.6, int(r * 1.05))
        d.text((c, c - 12 * SS), big, font=f, fill=NAVY, anchor="mm")
    # Légende sur un bandeau incliné en bas.
    f = fit(caption, s * 0.78, 30 * SS)
    w = f.getbbox(caption)[2] + 28 * SS
    y = s * 0.80
    band = [(c - w / 2 + 6 * SS, y - 22 * SS), (c + w / 2 + 6 * SS, y - 22 * SS), (c + w / 2 - 6 * SS, y + 22 * SS), (c - w / 2 - 6 * SS, y + 22 * SS)]
    d.polygon(band, fill=NAVY)
    d.text((c, y), caption, font=f, fill=CREAM, anchor="mm")
    # Coins arrondis : fond marine, l'icône reste carrée pour Steam.
    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, s, s], radius=40 * SS, fill=255)
    out = Image.new("RGB", (s, s), NAVY)
    out.paste(img, (0, 0), mask)
    return out.resize((SIZE, SIZE), Image.LANCZOS)


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "docs", "steam", "succes-icones")
    os.makedirs(out_dir, exist_ok=True)
    with open(os.path.join(ROOT, "docs", "steam", "achievements.csv"), encoding="utf-8") as fh:
        rows = [r for r in csv.DictReader(fh) if r["type"] == "achievement"]
    tiles = []
    for r in rows:
        api = r["api_name"]
        big, caption, accent = GLYPHS.get(api, ("?", r["name_fr"].upper(), ORANGE))
        color = icon(api, big, caption, accent)
        grey = ImageOps.grayscale(color).point(lambda v: int(v * 0.55)).convert("RGB")
        color.save(os.path.join(out_dir, f"{api}.png"))
        grey.save(os.path.join(out_dir, f"{api}_gris.png"))
        tiles.append((color, grey))
    # Planche contact : couleur au-dessus, grisée en dessous.
    cols = 8
    rows_n = math.ceil(len(tiles) / cols)
    sheet = Image.new("RGB", (cols * 136, rows_n * 272), (8, 12, 24))
    for i, (col, gr) in enumerate(tiles):
        x, y = (i % cols) * 136 + 4, (i // cols) * 272 + 4
        sheet.paste(col.resize((128, 128)), (x, y))
        sheet.paste(gr.resize((128, 128)), (x, y + 132))
    sheet.save(os.path.join(out_dir, "planche.png"))
    print(f"{len(tiles)} succès, {2 * len(tiles)} icônes dans {out_dir}")


if __name__ == "__main__":
    main()
