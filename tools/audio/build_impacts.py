"""Sons d'impact et d'attrape retravaillés : une balle en caoutchouc gonflée, pas un coup de poing.

Les sons d'origine (packs Kenney, voir assets/audio/CREDITS.md) gardés dans tools/audio/impacts-src/ avaient 77 à 98 %
de leur énergie sous 250 Hz : un « boum » sourd que les haut-parleurs de portable et du Steam Deck ne rendent presque
pas, et rien qui dise « balle ». On garde ce corps grave (atténué et nettoyé sous 45 Hz) et on y ajoute, synthétisés :

- le claquement du caoutchouc : bruit très bref (2 à 3 ms) filtré entre 1,2 et 6 kHz ;
- la résonance de la balle gonflée : trois partiels amortis (rapports 1 : 2,13 : 3,41, comme une membrane), hauteur qui
  glisse vers le bas de 6 % pendant l'écrasement, fondamentale 520 à 650 Hz selon la variante ;
- touche (hit) : claquement fort et résonance claire ; touche reçue (hurtself) : le même étouffé ;
- attrape (catch) : claquement de paume plus mat (filtré à 3,5 kHz) et un « pomf » grave court, résonance tenue courte
  (la main l'étouffe) ; attrape parfaite : la même plus la résonance laissée libre et le carillon d'origine.

Mesures écrites à la fin (part sous 250 Hz, centroïde, crête) : le script refuse un résultat qui sature.
Usage : python3 tools/audio/build_impacts.py   (ffmpeg requis ; graines fixes, sortie identique à chaque fois)
"""
import os
import subprocess
import sys

import numpy as np

SR = 44100
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "tools", "audio", "impacts-src")
OUT = os.path.join(ROOT, "assets", "audio", "sfx")
PEAK = 10 ** (-3 / 20)   # -3 dBFS, comme build_audio.py


def load(path):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).astype(np.float64)


def save(path, y):
    pcm = np.clip(y, -1, 1).astype(np.float32).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-",
                    "-c:a", "libvorbis", "-q:a", "6", path], input=pcm, check=True)


def biquad(x, kind, f, q=0.707):
    """Filtre RBJ (passe-bas, passe-haut, passe-bande) appliqué échantillon par échantillon."""
    w = 2 * np.pi * f / SR
    a = np.sin(w) / (2 * q)
    c = np.cos(w)
    if kind == "lp":
        b0, b1, b2 = (1 - c) / 2, 1 - c, (1 - c) / 2
    elif kind == "hp":
        b0, b1, b2 = (1 + c) / 2, -(1 + c), (1 + c) / 2
    else:   # passe-bande, gain de crête 0 dB
        b0, b1, b2 = a, 0.0, -a
    a0, a1, a2 = 1 + a, -2 * c, 1 - a
    b0, b1, b2, a1, a2 = b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0
    y = np.zeros_like(x)
    x1 = x2 = y1 = y2 = 0.0
    for i, v in enumerate(x):
        o = b0 * v + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, v, y1, o
        y[i] = o
    return y


def db(g):
    return 10 ** (g / 20)


def smack(rng, dur=0.004, lo=1200, hi=6000, tau=0.006):
    """Claquement : bruit blanc enveloppé (attaque 0,3 ms, décroissance exponentielle), bande lo-hi."""
    n = int(SR * 0.04)
    t = np.arange(n) / SR
    env = np.minimum(t / 0.0003, 1) * np.exp(-np.maximum(t - dur, 0) / tau)
    x = rng.standard_normal(n) * env
    x = biquad(biquad(x, "hp", lo), "lp", hi)
    return x / (np.max(np.abs(x)) + 1e-9)


def ring(f0, decay, glide=0.06, partials=((1.0, 1.0), (2.13, 0.45), (3.41, 0.22)), dur=0.25):
    """Résonance de balle gonflée : partiels amortis, hauteur qui descend de `glide` en 40 ms (écrasement)."""
    n = int(SR * dur)
    t = np.arange(n) / SR
    bend = 1 + glide * np.exp(-t / 0.04)   # part plus haut, se pose
    y = np.zeros(n)
    for ratio, amp in partials:
        phase = 2 * np.pi * np.cumsum(f0 * ratio * bend) / SR
        y += amp * np.sin(phase) * np.exp(-t / (decay / ratio ** 0.7))
    y *= np.minimum(t / 0.0008, 1)
    return y / (np.max(np.abs(y)) + 1e-9)


def pomf(f=95, decay=0.035):
    """Coup sourd court (la paume qui absorbe la balle)."""
    n = int(SR * 0.15)
    t = np.arange(n) / SR
    y = np.sin(2 * np.pi * f * t * (1 + 0.5 * np.exp(-t / 0.01))) * np.exp(-t / decay) * np.minimum(t / 0.001, 1)
    return y


def mix(parts):
    n = max(len(p) for p, _ in parts)
    y = np.zeros(n)
    for p, g in parts:
        y[:len(p)] += p * db(g)
    return y


def finish(y):
    y = biquad(y, "hp", 45)   # rien d'utile sous 45 Hz, ça mange la marge
    # Fin propre : fondu de 30 ms, silence final coupé sous -60 dB.
    a = np.abs(y)
    last = np.where(a > 1e-3 * a.max())[0][-1] + int(SR * 0.03)
    y = y[:min(len(y), last)]
    f = min(len(y), int(SR * 0.03))
    y[-f:] *= np.linspace(1, 0, f)
    return y * PEAK / np.max(np.abs(y))


def body(name):
    """Corps d'origine, sans son extrême grave."""
    return biquad(load(os.path.join(SRC, name)), "hp", 60)


def a_weight(f):
    """Pondération A (sensibilité de l'oreille à faible niveau), en amplitude."""
    f2 = f ** 2
    ra = (12194 ** 2 * f2 ** 2) / ((f2 + 20.6 ** 2) * np.sqrt((f2 + 107.7 ** 2) * (f2 + 737.9 ** 2)) * (f2 + 12194 ** 2))
    return ra


def loud(y):
    """Énergie pondérée A (volume perçu, à une constante près)."""
    X = np.abs(np.fft.rfft(y))
    f = np.fft.rfftfreq(len(y), 1 / SR)
    return ((X * a_weight(f)) ** 2).sum()


def measure(y):
    """Part de l'énergie entendue (pondérée A) au-dessus de 400 Hz, ce qu'un petit haut-parleur (portable, Steam Deck) rend :
    claquement et résonance de la balle ; et part de l'énergie brute sous 250 Hz."""
    X = np.abs(np.fft.rfft(y * np.hanning(len(y))))
    f = np.fft.rfftfreq(len(y), 1 / SR)
    low = (X[f < 250] ** 2).sum() / (X ** 2).sum()
    A = (X * a_weight(f)) ** 2
    presence = A[f > 400].sum() / A.sum()
    return low, presence


def main():
    made = []
    rng = np.random.default_rng(7)
    # Touche : quatre balles légèrement différentes (taille, pression).
    for i, f0 in enumerate((560, 610, 520, 650), start=1):
        y = mix([(body(f"hit_{i}.ogg"), -9), (smack(rng), 0), (ring(f0, 0.04), -5)])
        made.append((f"hit_{i}.ogg", finish(y)))
    # Touche reçue : la même, plus sourde (on est dedans).
    for i, f0 in enumerate((520, 560), start=1):
        y = mix([(body(f"hurtself_{i}.ogg"), -6), (biquad(smack(rng), "lp", 2500), -2), (ring(f0, 0.045), -7)])
        made.append((f"hurtself_{i}.ogg", finish(y)))
    # Attrape : paume mate, résonance étouffée.
    for i, f0 in enumerate((540, 590, 520, 570), start=1):
        y = mix([(body(f"catch_{i}.ogg"), -9), (smack(rng, lo=900, hi=3500, tau=0.005), 0), (pomf(), -6),
                 (ring(f0, 0.02, glide=0.03), -6)])
        made.append((f"catch_{i}.ogg", finish(y)))
    # Attrape parfaite : la même, résonance libre, carillon d'origine gardé dans le corps.
    for i, f0 in enumerate((580, 560, 610), start=1):
        y = mix([(body(f"perfect_{i}.ogg"), -3), (smack(rng, lo=900, hi=4000), -4), (pomf(), -7),
                 (ring(f0, 0.05), -9)])
        made.append((f"perfect_{i}.ogg", finish(y)))

    print(f"{'son':16} {'<250 Hz avant':>13} {'après':>6} {'>400 Hz (A) avant':>17} {'après':>6}")
    for name, y in made:
        src = load(os.path.join(SRC, name))
        # Volume perçu (énergie pondérée A) : au plus 3 dB au-dessus de l'ancien son, pour ne pas déséquilibrer le mixage.
        over = 10 * np.log10(loud(y) / loud(src))
        if over > 3:
            y = y * db(3 - over)
        # Plus faible que l'ancien (le claquement prend la crête) : on remonte et on arrondit les crêtes (tanh), comme un
        # limiteur, jusqu'à retrouver l'ancien volume à 1 dB près.
        for _ in range(4):
            over = 10 * np.log10(loud(y) / loud(src))
            if over >= -1:
                break
            y = PEAK * np.tanh(y * db(-over) / PEAK)
        lo0, c0 = measure(src)
        lo1, c1 = measure(y)
        if np.max(np.abs(y)) > 0.99:
            sys.exit(f"{name} sature")
        over = 10 * np.log10(loud(y) / loud(src))
        print(f"{name:16} {lo0:13.2f} {lo1:6.2f} {c0:17.2f} {c1:6.2f}   volume {over:+.1f} dB")
        save(os.path.join(OUT, name), y)


if __name__ == "__main__":
    main()
