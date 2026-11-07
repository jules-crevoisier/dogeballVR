"""Musiques de match propres à chaque arène PC (bus Music) : morceaux CC0 d'OpenGameArt rognés (intro calme, fin en fondu)
puis bouclés sans couture (la fin se fond dans le début sur 4 s), à -14 LUFS comme les autres musiques. Voir CREDITS.md.
Usage : build_arena_music.py <dossier des téléchargements> <dossier assets/audio du dépôt>"""
import os, re, subprocess, sys
import numpy as np

DL, OUT = sys.argv[1], sys.argv[2]
SR, XF = 44100, 4.0

# fichier source, début et fin gardés (s), nom de sortie
TRACKS = [
    ("raspberry_jam.ogg", 0.0, 169.5, "match_marche.ogg"),       # Toits du Marché : funk new jack swing, ville la nuit
    ("wednesday_night.ogg", 11.0, 160.0, "match_piscine.ogg"),   # La Piscine : jazz-funk posé, soirée d'été
    ("rhythm_factory_0.mp3", 9.0, 215.0, "match_depot.ogg"),     # Le Dépôt : électro-funk mécanique, atelier au petit matin
]


def load(path):
    """Décode en stéréo float32 à 44,1 kHz (forme [n, 2])."""
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-ac", "2", "-ar", str(SR), "-f", "f32le", "-"], capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2).copy()


def loop(x, start, end):
    """Garde [start, end + XF] et fond les XF dernières secondes dans le début : la boucle fait end - start secondes."""
    a, b, f = int(start * SR), int(end * SR), int(XF * SR)
    y = x[a:b].copy()
    tail = x[b:b + f]
    r = np.linspace(0, 1, len(tail))[:, None]
    y[:len(tail)] = y[:len(tail)] * r + tail * (1 - r)
    return y


def lufs(y):
    p = subprocess.run(["ffmpeg", "-v", "info", "-f", "f32le", "-ar", str(SR), "-ac", "2", "-i", "-", "-af", "ebur128", "-f", "null", "-"],
                       input=y.astype(np.float32).tobytes(), capture_output=True)
    m = re.findall(r"I:\s+(-?[\d.]+) LUFS", p.stderr.decode())
    return float(m[-1]) if m else -14.0


for src, start, end, name in TRACKS:
    y = loop(load(os.path.join(DL, src)), start, end)
    y = y * 10 ** ((-14.0 - lufs(y)) / 20)
    pk = np.max(np.abs(y))
    if pk > 10 ** (-1 / 20):
        y = y * 10 ** (-1 / 20) / pk
    path = os.path.join(OUT, "music", name)
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "2", "-i", "-", "-c:a", "libvorbis", "-q:a", "4", path],
                   input=y.astype(np.float32).tobytes(), check=True)
    print(path, f"{len(y) / SR:.1f} s", f"{lufs(y):.1f} LUFS")
