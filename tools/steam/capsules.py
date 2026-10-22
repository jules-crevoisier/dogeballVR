#!/usr/bin/env python3
"""Capsules Steam provisoires tirées des captures du jeu (tools/steam/shots.sh) et du logo du menu.

Usage : python3 tools/steam/capsules.py DOSSIER_DES_CAPTURES
Écrit DOSSIER/capsules/ aux tailles exigées par Steam (docs/steam/CHECKLIST-SORTIE-STEAM.md, section 2).
Le logo reprend celui du menu (police Lilita One, orange, contour marine, ombre jaune, penché de 4°) avec le nom de travail :
à refaire quand le nom définitif est choisi (docs/steam/PAGE-STORE.md, section 1).
"""
import glob
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageStat

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FONT = os.path.join(ROOT, "assets", "fonts", "LilitaOne-Regular.ttf")
NAME = os.environ.get("GAME_NAME", "DOGEBALL")
TAGLINE = "ESQUIVE · ATTRAPE · RENVOIE"
ORANGE, NAVY, YELLOW = (255, 106, 26), (16, 24, 48), (255, 210, 63)


def crop_to(img, w, h, focus=(0.5, 0.5)):
    """Recadre au rapport w/h autour du point focus, puis redimensionne."""
    sw, sh = img.size
    if sw / sh > w / h:
        cw, ch = int(sh * w / h), sh
    else:
        cw, ch = sw, int(sw * h / w)
    x = min(max(int(focus[0] * sw - cw / 2), 0), sw - cw)
    y = min(max(int(focus[1] * sh - ch / 2), 0), sh - ch)
    return img.crop((x, y, x + cw, y + ch)).resize((w, h), Image.LANCZOS)


def logo(width, tagline=True):
    """Logo du menu sur fond transparent, à la largeur demandée."""
    size = 400
    font = ImageFont.truetype(FONT, size)
    box = font.getbbox(NAME)
    tw, th = box[2] - box[0], box[3] - box[1]
    stroke = size // 9
    pad = stroke * 2 + 40
    tag_h = int(size * 0.32) if tagline else 0
    canvas = Image.new("RGBA", (tw + pad * 2 + 30, th + pad * 2 + 30 + tag_h), (0, 0, 0, 0))
    d = ImageDraw.Draw(canvas)
    origin = (pad - box[0], pad - box[1])
    d.text((origin[0] + 22, origin[1] + 28), NAME, font=font, fill=YELLOW, stroke_width=stroke, stroke_fill=YELLOW)
    d.text(origin, NAME, font=font, fill=ORANGE, stroke_width=stroke, stroke_fill=NAVY)
    if tagline:
        tfont = ImageFont.truetype(FONT, int(size * 0.22))
        d.text((origin[0] + 30, pad + th + stroke + 30), TAGLINE, font=tfont, fill=(255, 255, 255), stroke_width=stroke // 3, stroke_fill=NAVY)
    canvas = canvas.rotate(4, resample=Image.BICUBIC, expand=True)
    canvas = canvas.crop(canvas.getbbox())
    return canvas.resize((width, int(canvas.height * width / canvas.width)), Image.LANCZOS)


def vignette(img, strength=0.55):
    """Assombrit les bords et le bas pour que le logo se lise sur la capture."""
    w, h = img.size
    mask = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(mask)
    d.ellipse((-w * 0.25, -h * 0.35, w * 1.25, h * 1.15), fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(min(w, h) // 6))
    dark = Image.new("RGB", (w, h), NAVY)
    return Image.composite(img, Image.blend(img, dark, strength), mask)


def with_logo(bg, logo_width_ratio, anchor="center", tagline=True):
    bg = vignette(bg.convert("RGB"))
    lg = logo(int(bg.width * logo_width_ratio), tagline)
    if lg.height > bg.height * 0.8:
        lg = lg.resize((int(lg.width * bg.height * 0.8 / lg.height), int(bg.height * 0.8)), Image.LANCZOS)
    x = (bg.width - lg.width) // 2
    y = {"center": (bg.height - lg.height) // 2, "top": int(bg.height * 0.06), "bottom": bg.height - lg.height - int(bg.height * 0.06)}[anchor]
    out = bg.convert("RGBA")
    out.alpha_composite(lg, (x, y))
    return out.convert("RGB")


def busiest(paths):
    """Capture la plus riche (écart type des couleurs) : souvent celle où il se passe le plus de choses."""
    return max(paths, key=lambda p: sum(ImageStat.Stat(Image.open(p).convert("RGB")).stddev))


def main():
    src = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "docs", "steam", "captures")
    shots = sorted(p for p in glob.glob(os.path.join(src, "*.png")) if "menu" not in os.path.basename(p) and "fin-de-partie" not in os.path.basename(p))
    if not shots:
        sys.exit(f"Aucune capture dans {src} : lancer tools/steam/shots.sh d'abord")
    out = os.path.join(src, "capsules")
    os.makedirs(out, exist_ok=True)
    hero_src = busiest([p for p in shots if "tour" in p] or shots)
    main_src = busiest([p for p in shots if "croix" in p] or shots)
    print(f"Capture principale : {os.path.basename(main_src)} ; héros de bibliothèque : {os.path.basename(hero_src)}")
    main_img, hero_img = Image.open(main_src), Image.open(hero_src)

    jobs = {
        "header_capsule_920x430.png": with_logo(crop_to(main_img, 920, 430), 0.62),
        "small_capsule_462x174.png": with_logo(crop_to(main_img, 462, 174), 0.86, tagline=False),
        "main_capsule_1232x706.png": with_logo(crop_to(main_img, 1232, 706), 0.6, "top"),
        "vertical_capsule_748x896.png": with_logo(crop_to(main_img, 748, 896), 0.88, "top"),
        "library_capsule_600x900.png": with_logo(crop_to(hero_img, 600, 900), 0.9, "top", tagline=False),
        "library_header_920x430.png": with_logo(crop_to(main_img, 920, 430), 0.62),
        "page_background_1438x810.png": vignette(crop_to(hero_img, 1438, 810), 0.75),
        # Héros de bibliothèque : aucun texte, l'action dans la zone sûre centrale (860x380).
        "library_hero_3840x1240.png": crop_to(hero_img, 3840, 1240, (0.5, 0.55)),
    }
    for name, img in jobs.items():
        img.save(os.path.join(out, name))
    logo(1280).save(os.path.join(out, "library_logo_1280.png"))
    icon = Image.new("RGBA", (512, 512), NAVY + (255,))
    ball = logo(470, tagline=False)
    icon.alpha_composite(ball, ((512 - ball.width) // 2, (512 - ball.height) // 2))
    icon.convert("RGB").resize((184, 184), Image.LANCZOS).save(os.path.join(out, "community_icon_184x184.jpg"), quality=92)
    icon.resize((256, 256), Image.LANCZOS).save(os.path.join(out, "shortcut_icon_256x256.png"))
    icon.save(os.path.join(out, "app_icon_512.png"))
    for name in sorted(os.listdir(out)):
        with Image.open(os.path.join(out, name)) as im:
            print(f"  {name} : {im.size[0]}x{im.size[1]}")


if __name__ == "__main__":
    main()
