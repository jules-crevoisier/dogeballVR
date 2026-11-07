#!/usr/bin/env python3
"""Capsules Steam tirées des captures du jeu (tools/steam/shots.sh) et du logo du menu.

Usage : python3 tools/steam/capsules.py DOSSIER_DES_CAPTURES
Écrit DOSSIER/capsules/ aux tailles exigées par Steam (docs/steam/CHECKLIST-SORTIE-STEAM.md, section 2).
Le logo reprend celui du menu (interface « Affiche », scripts/UI/MainMenu.cs) : DOGEBALL en capitales Anton, trois
tirages décalés (bleu, orange, papier) comme une sérigraphie mal calée, filet et devise en vert acide (Barlow ExtraBold).
Devise en anglais : la page Steam est d'abord lue en anglais. Nom de travail : à refaire quand le nom définitif est
choisi (docs/steam/PAGE-STORE.md, section 1).
"""
import glob
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageStat

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FONT = os.path.join(ROOT, "assets", "fonts", "Anton-Regular.ttf")
TAG_FONT = os.path.join(ROOT, "assets", "fonts", "Barlow-ExtraBold.ttf")
NAME = os.environ.get("GAME_NAME", "DOGEBALL")
TAGLINE = os.environ.get("GAME_TAGLINE", "DODGE  ·  CATCH  ·  THROW BACK")
# Palette de l'interface (scripts/UI/UiKit.cs).
INK, PAPER, ORANGE, BLUE, LIME = (8, 8, 11), (243, 236, 221), (242, 97, 45), (46, 134, 222), (210, 245, 60)
NAVY = INK


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
    """Logo du menu sur fond transparent, à la largeur demandée (mêmes proportions que MainMenu.cs à 150 px)."""
    size = 600
    font = ImageFont.truetype(FONT, size)
    box = font.getbbox(NAME)
    tw, th = box[2] - box[0], box[3] - box[1]
    k = size / 150                                   # décalages du menu (7 px à 150 px), mis à l'échelle
    off = int(7 * k)
    pad = off * 2
    tag_h = 0
    if tagline:
        tfont = ImageFont.truetype(TAG_FONT, int(26 * k * 1.25))
        tbox = tfont.getbbox(TAGLINE)
        tag_h = int(30 * k) + (tbox[3] - tbox[1])
    canvas = Image.new("RGBA", (tw + pad * 2, th + pad * 2 + tag_h), (0, 0, 0, 0))
    d = ImageDraw.Draw(canvas)
    ox, oy = pad - box[0], pad - box[1]
    d.text((ox - int(6 * k), oy + off), NAME, font=font, fill=BLUE)
    d.text((ox + off, oy + off), NAME, font=font, fill=ORANGE)
    d.text((ox, oy), NAME, font=font, fill=PAPER)
    if tagline:
        y = pad + th + off + int(14 * k)
        d.rectangle((pad, y, pad + tw, y + int(5 * k)), fill=LIME)
        d.text((pad - tbox[0], y + int(13 * k) - tbox[1]), TAGLINE, font=tfont, fill=LIME)
    canvas = canvas.crop(canvas.getbbox())
    return canvas.resize((width, int(canvas.height * width / canvas.width)), Image.LANCZOS)


def monogram(size):
    """Icône carrée : l'initiale du nom en trois tirages décalés sur l'encre, filet vert acide dessous (lisible à 32 px)."""
    icon = Image.new("RGBA", (size, size), INK + (255,))
    font = ImageFont.truetype(FONT, int(size * 0.78))
    letter = NAME[0]
    box = font.getbbox(letter)
    lw, lh = box[2] - box[0], box[3] - box[1]
    off = int(size * 0.03)
    x = (size - lw) // 2 - box[0]
    y = int(size * 0.44) - lh // 2 - box[1]
    d = ImageDraw.Draw(icon)
    d.text((x - off, y + off), letter, font=font, fill=BLUE)
    d.text((x + off, y + off), letter, font=font, fill=ORANGE)
    d.text((x, y), letter, font=font, fill=PAPER)
    bar_y = y + box[3] + off * 2
    d.rectangle(((size - lw) // 2, bar_y, (size + lw) // 2, bar_y + int(size * 0.035)), fill=LIME)
    return icon


def ink_plate(lg, margin=0.06):
    """Aplat d'encre derrière le logo, comme la colonne du menu : le logo se lit sur n'importe quelle capture."""
    m = int(lg.width * margin)
    plate = Image.new("RGBA", (lg.width + m * 2, lg.height + m * 2), INK + (228,))
    plate.alpha_composite(lg, (m, m))
    return plate


def vignette(img, strength=0.55):
    """Assombrit les bords et le bas pour que le logo se lise sur la capture."""
    img = img.convert("RGB")
    w, h = img.size
    mask = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(mask)
    d.ellipse((-w * 0.25, -h * 0.35, w * 1.25, h * 1.15), fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(min(w, h) // 6))
    dark = Image.new("RGB", (w, h), NAVY)
    return Image.composite(img, Image.blend(img, dark, strength), mask)


def with_logo(bg, logo_width_ratio, anchor="center", tagline=True):
    """Capture assombrie sur les bords, logo posé sur un aplat d'encre (la colonne du menu) pour rester lisible sur tout fond."""
    bg = vignette(bg.convert("RGB"), 0.4)
    lg = ink_plate(logo(int(bg.width * logo_width_ratio), tagline))
    if lg.height > bg.height * 0.8:
        lg = lg.resize((int(lg.width * bg.height * 0.8 / lg.height), int(bg.height * 0.8)), Image.LANCZOS)
    x = (bg.width - lg.width) // 2
    y = {"center": (bg.height - lg.height) // 2, "top": int(bg.height * 0.06), "bottom": bg.height - lg.height - int(bg.height * 0.06)}[anchor]
    out = bg.convert("RGBA")
    out.alpha_composite(lg, (x, y))
    return out.convert("RGB")


def detail(path):
    """Densité de contours : haute pour un plan large et riche, basse pour un plan bouché par un mur proche."""
    return ImageStat.Stat(Image.open(path).convert("L").filter(ImageFilter.FIND_EDGES)).mean[0]


def main():
    src = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "docs", "steam", "captures")
    # Fonds des capsules : captures sans HUD (« capsule-* ») si elles existent, sinon les captures de jeu.
    shots = sorted(glob.glob(os.path.join(src, "capsule-*.png"))) or sorted(
        p for p in glob.glob(os.path.join(src, "*.png")) if "menu" not in os.path.basename(p) and "fin-de-partie" not in os.path.basename(p))
    if not shots:
        sys.exit(f"Aucune capture dans {src} : lancer tools/steam/shots.sh d'abord")
    out = os.path.join(src, "capsules")
    os.makedirs(out, exist_ok=True)
    ranked = sorted(shots, key=detail, reverse=True)
    main_src = ranked[0]
    hero_src = ranked[1] if len(ranked) > 1 else ranked[0]
    print(f"Capture principale : {os.path.basename(main_src)} ; héros de bibliothèque : {os.path.basename(hero_src)}")
    main_img, hero_img = Image.open(main_src), Image.open(hero_src)

    jobs = {
        "header_capsule_920x430.png": with_logo(crop_to(main_img, 920, 430), 0.62),
        "small_capsule_462x174.png": with_logo(crop_to(main_img, 462, 174), 0.86, tagline=False),
        "main_capsule_1232x706.png": with_logo(crop_to(main_img, 1232, 706), 0.6, "top"),
        "vertical_capsule_748x896.png": with_logo(crop_to(main_img, 748, 896), 0.88, "top"),
        "library_capsule_600x900.png": with_logo(crop_to(hero_img, 600, 900), 0.8, "top", tagline=False),
        "library_header_920x430.png": with_logo(crop_to(main_img, 920, 430), 0.62),
        "page_background_1438x810.png": vignette(crop_to(hero_img, 1438, 810), 0.75),
        # Héros de bibliothèque : aucun texte, l'action dans la zone sûre centrale (860x380).
        "library_hero_3840x1240.png": crop_to(hero_img, 3840, 1240, (0.5, 0.55)).convert("RGB"),
    }
    for name, img in jobs.items():
        img.save(os.path.join(out, name))
    logo(1280).save(os.path.join(out, "library_logo_1280.png"))
    icon = monogram(512)
    icon.convert("RGB").resize((184, 184), Image.LANCZOS).save(os.path.join(out, "community_icon_184x184.jpg"), quality=92)
    icon.resize((256, 256), Image.LANCZOS).save(os.path.join(out, "shortcut_icon_256x256.png"))
    icon.save(os.path.join(out, "app_icon_512.png"))
    for name in sorted(os.listdir(out)):
        with Image.open(os.path.join(out, name)) as im:
            print(f"  {name} : {im.size[0]}x{im.size[1]}")


if __name__ == "__main__":
    main()
