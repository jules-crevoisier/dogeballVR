"""Ambiances propres à chaque arène PC (boucles de 40 s, bus SFX) : couches CC0 d'OpenGameArt mêlées à la circulation
lointaine déjà construite (ambience/rooftop.ogg). Voir CREDITS.md pour les sources et leurs pages.
Usage : build_ambiences.py <dossier des téléchargements> <dossier assets/audio du dépôt>"""
import os, re, subprocess, sys
import numpy as np

DL, OUT = sys.argv[1], sys.argv[2]
SR = 44100
SECONDS, XF = 40.0, 3.0


def db(x):
    return 10 ** (x / 20)


def load(path, lowpass=None, highpass=None, rate=1.0):
    """Décode en mono float32 à 44,1 kHz, filtré ; rate < 1 ralentit et grave."""
    af = []
    if rate != 1.0:
        af += [f"asetrate={int(SR * rate)}", f"aresample={SR}"]
    if highpass:
        af += [f"highpass=f={highpass}"]
    if lowpass:
        af += [f"lowpass=f={lowpass}"]
    cmd = ["ffmpeg", "-v", "error", "-i", path, "-ac", "1", "-ar", str(SR)]
    if af:
        cmd += ["-af", ",".join(af)]
    cmd += ["-f", "f32le", "-"]
    return np.frombuffer(subprocess.run(cmd, capture_output=True, check=True).stdout, dtype=np.float32).copy()


def tile(x, n, xf=0.5):
    """Répète un extrait court jusqu'à n échantillons, chaque reprise en fondu enchaîné (pas de clic à la jointure)."""
    if len(x) >= n:
        return x[:n].copy()
    f = min(int(xf * SR), len(x) // 4)
    out = x.copy()
    r = np.linspace(0, 1, f)
    while len(out) < n:
        out[-f:] = out[-f:] * (1 - r) + x[:f] * r
        out = np.concatenate([out, x[f:]])
    return out[:n]


def loop(x, seconds=SECONDS, xf=XF):
    """Boucle sans couture : la fin (au-delà de la durée) se fond dans le début."""
    L, F = int(seconds * SR), int(xf * SR)
    x = tile(x, L + F)
    y = x[:L].copy()
    r = np.linspace(0, 1, F)
    y[:F] = x[:F] * r + x[L:L + F] * (1 - r)
    return y


def events(clip, n, times, gain):
    """Sons ponctuels (cloche…) posés aux instants donnés (s) sur une piste de n échantillons."""
    y = np.zeros(n, dtype=np.float32)
    for t in times:
        i = int(t * SR)
        k = min(len(clip), n - i)
        y[i:i + k] += clip[:k] * db(gain)
    return y


def lufs(y):
    """Intensité intégrée (ffmpeg ebur128) d'un signal mono."""
    p = subprocess.run(["ffmpeg", "-v", "info", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-", "-af", "ebur128", "-f", "null", "-"],
                       input=y.astype(np.float32).tobytes(), capture_output=True)
    m = re.findall(r"I:\s+(-?[\d.]+) LUFS", p.stderr.decode())
    return float(m[-1]) if m else -30.0


def write(y, name, target=-26.0):
    y = y * db(target - lufs(y))
    pk = np.max(np.abs(y))
    if pk > db(-3):
        y = y * db(-3) / pk
    path = os.path.join(OUT, "ambience", name)
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-", "-c:a", "libvorbis", "-q:a", "3", path],
                   input=y.astype(np.float32).tobytes(), check=True)
    print(path, f"{lufs(y):.1f} LUFS")


N = int(SECONDS * SR)
traffic = load(os.path.join(OUT, "ambience", "rooftop.ogg"))[:N]
if len(traffic) < N:
    traffic = loop(traffic)

# Toits du Marché : la rue en bas, et le marché (voix lointaines, étouffées) qui monte entre les toits.
crowd = loop(load(DL + "/crowd_shouting_0.ogg", lowpass=900, highpass=180))
write(traffic + crowd * db(-9), "marche.ogg")

# La Piscine, soirée : grillons, eau qui clapote au fond (rivière très filtrée), souffle d'air chaud ; pas de circulation.
crickets = loop(load(DL + "/crickets_1.mp3", highpass=1500))
water = loop(load(DL + "/park_ambience_river.wav", lowpass=700))
wind = loop(load(DL + "/park_ambience_wind.wav", lowpass=500))
write(crickets * db(-4) + water * db(-10) + wind * db(-14), "piscine.ogg")

# Le Dépôt, aube : bourdonnement électrique de la caténaire, oiseaux du matin, circulation plus loin, sonnette de tram.
hum = loop(load(DL + "/loop_generator_1.mp3", lowpass=420, rate=0.8))
birds = loop(load(DL + "/amb_morning_0.ogg", highpass=900))
bell = load(DL + "/bicycle-1.wav", lowpass=3500, rate=0.62)   # plus grave : un timbre de tram, pas un vélo
write(hum * db(-6) + birds * db(-8) + traffic * db(-7) + events(bell, N, (6.5, 27.0), -10), "depot.ogg")
