"""Découpe des rendus maîtres (capsules.py) aux formats Steam et pose du logo.

    python3 tools/blender/capsules_compose.py <dossier_maitres> <logo.png> <dossier_sortie>

Formats : en-tête 920×430, capsule principale 1232×706, petite 462×174, verticale 748×896, capsule de bibliothèque
600×900, en-tête de bibliothèque 920×430, fond de bibliothèque 3840×1240 (sans texte), fond de page 1438×810.
"""
import os
import sys

from PIL import Image, ImageEnhance, ImageFilter


def cover(img, w, h, cx=0.5, cy=0.5):
    """Recadre img pour remplir w×h (centre de recadrage cx, cy en fraction de l'image)."""
    s = max(w / img.width, h / img.height)
    r = img.resize((round(img.width * s), round(img.height * s)), Image.LANCZOS)
    x = min(max(0, round(r.width * cx - w / 2)), r.width - w)
    y = min(max(0, round(r.height * cy - h / 2)), r.height - h)
    return r.crop((x, y, x + w, y + h))


def shade(img, side="top", amount=0.55, size=0.45):
    """Assombrit un bord en dégradé : le logo reste lisible sur le décor."""
    w, h = img.size
    grad = Image.new("L", (w, h), 0)
    px = grad.load()
    n = int((h if side in ("top", "bottom") else w) * size)
    for i in range(n):
        a = int(255 * amount * (1 - i / n) ** 1.6)
        if side == "top":
            for x in range(w):
                px[x, i] = a
        elif side == "bottom":
            for x in range(w):
                px[x, h - 1 - i] = a
        elif side == "left":
            for y in range(h):
                px[i, y] = a
    dark = Image.new("RGB", (w, h), (12, 16, 40))
    return Image.composite(dark, img.convert("RGB"), grad)


def put_logo(img, logo, width_frac, cx, cy):
    lw = round(img.width * width_frac)
    lg = logo.resize((lw, round(logo.height * lw / logo.width)), Image.LANCZOS)
    out = img.convert("RGBA")
    x = round(img.width * cx - lg.width / 2)
    y = round(img.height * cy - lg.height / 2)
    out.alpha_composite(lg, (max(0, x), max(0, y)))
    return out.convert("RGB")


def main(src, logo_path, out):
    os.makedirs(out, exist_ok=True)
    large = Image.open(os.path.join(src, "maitre_large.png")).convert("RGB")
    vert = Image.open(os.path.join(src, "maitre_vertical.png")).convert("RGB")
    hero = Image.open(os.path.join(src, "maitre_hero.png")).convert("RGB")
    logo = Image.open(logo_path).convert("RGBA")
    pop = lambda im: ImageEnhance.Contrast(ImageEnhance.Color(im).enhance(1.08)).enhance(1.04)   # noqa: E731
    large, vert, hero = pop(large), pop(vert), pop(hero)

    main_c = put_logo(shade(cover(large, 1232, 706, 0.5, 0.55), "top", 0.5, 0.42), logo, 0.56, 0.5, 0.17)
    main_c.save(os.path.join(out, "main_capsule_1232x706.png"))
    header = put_logo(shade(cover(large, 920, 430, 0.5, 0.6), "top", 0.55, 0.5), logo, 0.6, 0.5, 0.22)
    header.save(os.path.join(out, "header_capsule_920x430.png"))
    header.save(os.path.join(out, "library_header_920x430.png"))
    small = put_logo(shade(cover(large, 462, 174, 0.5, 0.45), "top", 0.6, 1.0), logo, 0.86, 0.5, 0.5)
    small.save(os.path.join(out, "small_capsule_462x174.png"))
    vc = put_logo(shade(cover(vert, 748, 896, 0.5, 0.5), "top", 0.55, 0.42), logo, 0.88, 0.5, 0.15)
    vc.save(os.path.join(out, "vertical_capsule_748x896.png"))
    lib_c = put_logo(shade(cover(vert, 600, 900, 0.5, 0.5), "top", 0.55, 0.42), logo, 0.9, 0.5, 0.15)
    lib_c.save(os.path.join(out, "library_capsule_600x900.png"))
    cover(hero, 3840, 1240, 0.5, 0.5).save(os.path.join(out, "library_hero_3840x1240.png"))
    bg = cover(large, 1438, 810).filter(ImageFilter.GaussianBlur(6))
    ImageEnhance.Brightness(bg).enhance(0.55).save(os.path.join(out, "page_background_1438x810.png"))
    logo.save(os.path.join(out, "library_logo_1280.png"))
    print("capsules écrites dans", out)


if __name__ == "__main__":
    main(*sys.argv[1:4])
