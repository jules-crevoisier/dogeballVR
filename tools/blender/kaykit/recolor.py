"""Re-skin streetwear de la palette KayKit (CC0) : verts -> couleur d'équipe, cuirs -> jogging marine, dégradés conservés.
Usage : python3 -I recolor.py <rogue_texture.png> <sortie_dossier>"""
import colorsys
import os
import sys

from PIL import Image

CELL_W, CELL_H = 128, 256
RAMPS = {   # sombre, moyen, clair
    "orange": ((0x8E, 0x2F, 0x06), (0xFF, 0x6A, 0x1A), (0xFF, 0xB0, 0x70)),
    "bleu": ((0x1E, 0x5E, 0x9C), (0x4F, 0xB8, 0xF5), (0xA8, 0xE4, 0xFF)),
    "marine": ((0x10, 0x16, 0x2C), (0x2B, 0x36, 0x58), (0x4A, 0x5C, 0x8E)),
    "cheveux_orange": ((0x6A, 0x24, 0x0C), (0xC8, 0x56, 0x1E), (0xF2, 0x9A, 0x4A)),
    "cheveux_bleu": ((0x7C, 0x8C, 0xB0), (0xD6, 0xE0, 0xF2), (0xFF, 0xFF, 0xFF)),
    "blanc": ((0x8A, 0x92, 0xA8), (0xE4, 0xE8, 0xF2), (0xFF, 0xFF, 0xFF)),
}


def ramp(name, l):
    d, m, c = RAMPS[name]
    if l < 0.5:
        t = l / 0.5
        return tuple(d[i] + (m[i] - d[i]) * t for i in range(3))
    t = (l - 0.5) / 0.5
    return tuple(m[i] + (c[i] - m[i]) * t for i in range(3))


def classify(h, s, v, row, col):
    if (row, col) == (0, 1):
        return "cheveux"
    """Retourne le nom de rampe (ou None : on garde la couleur d'origine)."""
    if s > 0.35 and 70 <= h <= 200:   # verts et turquoises : la tenue
        return "team"
    if (row, col) in {(0, 5), (0, 6), (0, 7), (1, 5), (1, 6), (1, 7), (2, 3), (2, 4), (2, 5), (2, 6)}:   # cuirs et marrons : jogging
        return "marine"
    if (row, col) in {(0, 3), (1, 2), (1, 3)}:   # gris clairs : baskets blanches
        return "blanc"
    return None


def recolor(src, team):
    img = Image.open(src).convert("RGBA")
    px = img.load()
    w, h_ = img.size
    for y in range(h_):
        for x in range(w):
            r, g, b, a = px[x, y]
            row, col = y // CELL_H, x // CELL_W
            hh, ss, vv = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            cls = classify(hh * 360, ss, vv, row, col)
            if cls is None:
                continue
            name = team if cls == "team" else ("cheveux_" + team if cls == "cheveux" else cls)
            l = max(0.0, min(1.0, vv * 0.8 + (1 - ss) * 0.2))
            nr, ng, nb = ramp(name, l)
            px[x, y] = (int(nr), int(ng), int(nb), a)
    return img


if __name__ == "__main__":
    src, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    for team in ("orange", "bleu"):
        recolor(src, team).save(os.path.join(out, f"rogue_{team}.png"))
