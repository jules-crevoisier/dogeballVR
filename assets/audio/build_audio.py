"""Construit les sons du jeu à partir des packs CC0 téléchargés (et de quelques synthèses) :
mixage de couches, normalisation de crête (-3 dBFS pour les sfx), intensité -14 LUFS pour la musique, encodage Ogg Vorbis.
Usage : build_audio.py <dossier des téléchargements> <dossier assets/audio du dépôt>"""
import os, re, subprocess, sys, tempfile
import numpy as np

DL, OUT = sys.argv[1], sys.argv[2]
SR = 44100
K = DL + "/kenney_impact-sounds/Audio/"
I = DL + "/kenney_interface-sounds/Audio/"
U = DL + "/kenney_ui-audio/Audio/"
F = DL + "/kenney_sci-fi-sounds/Audio/"
J = DL + "/kenney_music-jingles/Audio/"
O = DL + "/oga/"
SYN = DL + "/synth/"
os.makedirs(SYN, exist_ok=True)


def load(path, rate=1.0, ch=1, start=0.0, dur=None, lowpass=None):
    """Décode en float32 (canaux, n) à 44,1 kHz ; rate < 1 ralentit et grave (comme un pitch de bande)."""
    af = []
    if rate != 1.0:
        af += [f"asetrate={int(SR * rate)}", f"aresample={SR}"]
    if lowpass:
        af += [f"lowpass=f={lowpass}"]
    cmd = ["ffmpeg", "-v", "error", "-ss", str(start)]
    if dur:
        cmd += ["-t", str(dur)]
    cmd += ["-i", path, "-ac", str(ch), "-ar", str(SR)]
    if af:
        cmd += ["-af", ",".join(af)]
    cmd += ["-f", "f32le", "-"]
    raw = subprocess.run(cmd, capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, ch).T.copy()


def db(x):
    return 10 ** (x / 20)


def mix(layers, ch=1):
    """layers : liste de (chemin, gain dB, options) ; options : rate, delay (s), start, dur, lowpass."""
    parts = []
    for path, g, opt in layers:
        x = load(path, opt.get("rate", 1.0), ch, opt.get("start", 0.0), opt.get("dur"), opt.get("lowpass")) * db(g)
        d = int(opt.get("delay", 0) * SR)
        if d:
            x = np.concatenate([np.zeros((ch, d), np.float32), x], axis=1)
        parts.append(x)
    n = max(p.shape[1] for p in parts)
    y = np.zeros((ch, n), np.float32)
    for p in parts:
        y[:, :p.shape[1]] += p
    return y


def trim_silence(y, thr=db(-60)):
    a = np.max(np.abs(y), axis=0)
    idx = np.where(a > thr)[0]
    if len(idx) == 0:
        return y
    return y[:, idx[0]:idx[-1] + 1]


def fade(y, fin=0.002, fout=0.012):
    n = y.shape[1]
    a, b = min(n, int(fin * SR)), min(n, int(fout * SR))
    if a:
        y[:, :a] *= np.linspace(0, 1, a)
    if b:
        y[:, n - b:] *= np.linspace(1, 0, b)
    return y


def write_ogg(y, path, q=4):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with tempfile.NamedTemporaryFile(suffix=".f32") as t:
        t.write(np.ascontiguousarray(y.T).astype(np.float32).tobytes())
        t.flush()
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", str(y.shape[0]), "-i", t.name,
                        "-c:a", "libvorbis", "-q:a", str(q), path], check=True)


def peak_norm(y, target=-3.0):
    pk = np.max(np.abs(y))
    return y * (db(target) / pk) if pk > 0 else y


def lufs(path):
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", path, "-af", "ebur128=peak=true", "-f", "null", "-"],
                       capture_output=True, text=True).stderr
    return float(re.findall(r"I:\s+(-?[\d.]+) LUFS", r)[-1])


def write_wav(y, path):
    with tempfile.NamedTemporaryFile(suffix=".f32") as t:
        t.write(np.ascontiguousarray(y.T).astype(np.float32).tobytes())
        t.flush()
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", str(y.shape[0]), "-i", t.name, path], check=True)


# ---------- synthèses ----------

def synth_whistle(blasts, path):
    """Sifflet d'arbitre à bille : ~2,9 kHz, trille (modulation rapide et irrégulière de la bille), souffle filtré."""
    rng = np.random.default_rng(3)
    total = sum(d + gap for d, gap in blasts) + 0.1
    t = np.arange(int(total * SR)) / SR
    y = np.zeros_like(t)
    pos = 0.0
    for d, gap in blasts:
        m = (t >= pos) & (t < pos + d)
        tt = t[m] - pos
        trill_rate = 32 + 6 * np.sin(2 * np.pi * 0.7 * tt)
        trill = np.sin(2 * np.pi * np.cumsum(trill_rate) / SR)
        jitter = np.convolve(rng.standard_normal(len(tt)), np.ones(400) / 400, mode="same") * 12
        f = 2950 + 90 * trill + 40 * jitter + 60 * np.minimum(tt / 0.05, 1)
        ph = 2 * np.pi * np.cumsum(f) / SR
        am = 0.72 + 0.28 * trill
        tone = np.sin(ph) + 0.22 * np.sin(2 * ph + 0.3) + 0.06 * np.sin(3 * ph)
        noise = rng.standard_normal(len(tt))
        # souffle : bruit passe-bande grossier autour du ton (différence de deux passe-bas exponentiels)
        lp1 = np.zeros_like(noise); lp2 = np.zeros_like(noise)
        a1, a2 = 0.55, 0.25
        for i in range(1, len(noise)):
            lp1[i] = lp1[i - 1] + a1 * (noise[i] - lp1[i - 1])
            lp2[i] = lp2[i - 1] + a2 * (noise[i] - lp2[i - 1])
        breath = (lp1 - lp2) * 0.35
        env = np.minimum(tt / 0.012, 1) * np.minimum((d - tt) / 0.04, 1)
        y[m] = (tone * am + breath) * env
        pos += d + gap
    y = peak_norm(y[None, :], -1)
    write_wav(y, path)
    return path


def synth_thump(path, f0=120, f1=42, dur=0.32):
    """Coup grave sous les impacts : sinus qui plonge, attaque nette, légère saturation."""
    t = np.arange(int(dur * SR)) / SR
    f = f1 + (f0 - f1) * np.exp(-t / 0.045)
    ph = 2 * np.pi * np.cumsum(f) / SR
    env = np.minimum(t / 0.002, 1) * np.exp(-t / 0.09)
    y = np.tanh(1.8 * np.sin(ph) * env)
    write_wav(peak_norm(y[None, :], -1), path)
    return path


whistle_double = synth_whistle([(0.16, 0.09), (0.62, 0)], SYN + "whistle_double.wav")
whistle_long = synth_whistle([(0.85, 0)], SYN + "whistle_long.wav")
whistle_short = synth_whistle([(0.30, 0)], SYN + "whistle_short.wav")
thump = synth_thump(SYN + "thump.wav")
thump_deep = synth_thump(SYN + "thump_deep.wav", 95, 36, 0.45)

# ---------- effets sonores ----------
P = lambda name: K + name + ".ogg"
SW = lambda n: O + f"swishes/swish-{n}.wav"
SFX = {
    "throw": [[(SW(n), 0, {}), (SW(n), -7, {"rate": 0.62})] for n in (5, 7, 9, 6)],
    "catch": [[(P(f"impactPunch_medium_00{n}"), 0, {}), (P(f"impactSoft_medium_00{n}"), -5, {})] for n in range(4)],
    "perfect": [[(P(f"impactPunch_heavy_00{n}"), 0, {}), (I + "confirmation_004.ogg", -7, {"delay": 0.03})] for n in (0, 2, 3)],
    "tier": [[(I + f"maximize_00{n}.ogg", 0, {})] for n in (1, 2, 6)],
    "ko": [[(P(f"impactPunch_heavy_00{n}"), 0, {}), (F + "lowFrequency_explosion_001.ogg", -4, {})] for n in (2, 3)],
    "alert": [[(I + "pluck_001.ogg", 0, {})], [(I + "pluck_002.ogg", 0, {})]],
    "parry": [[(P(f"impactPlate_medium_00{n}"), 0, {}), (P(f"impactPunch_medium_00{m}"), -5, {})] for n, m in ((0, 1), (1, 2), (4, 3))],
    "bomb": [[(F + f"explosionCrunch_00{n}.ogg", 0, {}), (F + "lowFrequency_explosion_000.ogg", -3, {})] for n in (0, 1, 2)],
    "hit": [[(P(f"impactPunch_heavy_00{n}"), 0, {}), (P(f"impactSoft_heavy_00{m}"), -1, {})] for n, m in ((0, 0), (1, 4), (4, 2), (3, 1))],
    "click": [[(U + f"click{n}.ogg", 0, {})] for n in (1, 2, 3)],
    "uihover": [[(U + f"rollover{n}.ogg", 0, {})] for n in (1, 2, 3)],
    "uiclick": [[(I + f"select_00{n}.ogg", 0, {})] for n in (1, 2)],
    "uiback": [[(I + f"back_00{n}.ogg", 0, {})] for n in (1, 2)],
    "countdown": [[(I + "bong_001.ogg", 0, {})]],
    "go": [[(whistle_short, 0, {}), (I + "confirmation_004.ogg", -6, {})]],
    "whistle": [[(whistle_double, 0, {})], [(whistle_long, 0, {})]],
    "dash": [[(SW(n), 0, {"rate": 0.72}), (SW(n), -6, {"rate": 0.5})] for n in (3, 8, 9)],
    "jump": [[(SW(n), 0, {"rate": 1.12})] for n in (1, 2, 4)],
    "land": [[(P(f"impactSoft_medium_00{n}"), 0, {}), (P(f"footstep_concrete_00{n}"), -3, {})] for n in range(3)],
    "chargetick": [[(I + "tick_004.ogg", 0, {})]],
    "koconfirm": [[(J + "hit/jingles_HIT13.ogg", 0, {})], [(J + "hit/jingles_HIT16.ogg", 0, {})]],
    "victory": [[(J + "sax/jingles_SAX02.ogg", 0, {})]],
    "defeat": [[(J + "sax/jingles_SAX01.ogg", 0, {})]],
    "hurtself": [[(P(f"impactPunch_heavy_00{n}"), 0, {"lowpass": 3000}), (P(f"impactSoft_heavy_00{m}"), 0, {"lowpass": 2000})] for n, m in ((2, 3), (3, 0))],
    "bounce": [[(P(f"impactSoft_medium_00{n}"), 0, {"rate": 1.15}), (P(f"impactGeneric_light_00{n}"), -6, {})] for n in (1, 3, 4)],
    "low": [[(thump, 0, {})], [(thump_deep, 0, {})]],
}


def crowd_clips():
    """Trois bouffées d'applaudissements (les 2,6 s les plus fournies), mono, fondu doux."""
    src = O + "applause-clapping-church-crowd-immersive.wav"
    x = load(src, ch=1)[0]
    w = int(2.6 * SR)
    hop = int(0.5 * SR)
    rms = [(np.sqrt(np.mean(x[i:i + w] ** 2)), i) for i in range(0, len(x) - w, hop)]
    picks = []
    for r, i in sorted(rms, reverse=True):
        if all(abs(i - j) > w for j in picks):
            picks.append(i)
        if len(picks) == 3:
            break
    out = []
    for i in picks:
        y = x[i:i + w][None, :].copy()
        out.append(fade(y, 0.15, 0.9))
    return out


manifest = []
sfx_dir = OUT + "/sfx"
os.makedirs(sfx_dir, exist_ok=True)
for f in os.listdir(sfx_dir):
    if f.endswith(".ogg"):
        os.remove(os.path.join(sfx_dir, f))
for kind, variants in SFX.items():
    for k, layers in enumerate(variants, 1):
        y = fade(peak_norm(trim_silence(mix(layers)), -3.0))
        y = np.concatenate([y, np.zeros((1, int(0.04 * SR)), np.float32)], axis=1)   # queue de silence : Vorbis abîme les fichiers très courts
        write_ogg(y, f"{sfx_dir}/{kind}_{k}.ogg")
        manifest.append((f"sfx/{kind}_{k}.ogg", [os.path.relpath(l[0], DL) for l in layers]))
for k, y in enumerate(crowd_clips(), 1):
    write_ogg(peak_norm(y, -3.0), f"{sfx_dir}/crowd_{k}.ogg")
    manifest.append((f"sfx/crowd_{k}.ogg", ["oga/applause-clapping-church-crowd-immersive.wav"]))

# ---------- musique ----------

def music(src, dst, target=-14.0, loop=True, tail_fade=0.0, ch=2):
    y = load(src, ch=ch)
    if not loop:
        y = trim_silence(y, db(-50))
        if tail_fade:
            y = fade(y, 0.003, tail_fade)
    else:
        y = fade(trim_silence(y, db(-70)), 0.0015, 0.0015)   # micro-fondus : pas de clic à la jointure de boucle
    tmp = SYN + "tmp_norm.wav"
    write_wav(y, tmp)
    g = target - lufs(tmp)
    y = y * db(g)
    pk = np.max(np.abs(y))
    if pk > db(-1):   # limiteur doux : compression des seules crêtes au-dessus de -1 dBFS
        lim = db(-1)
        y = np.where(np.abs(y) > lim * 0.8, np.sign(y) * (lim * 0.8 + (lim * 0.2) * np.tanh((np.abs(y) - lim * 0.8) / (lim * 0.2))), y)
    write_ogg(y.astype(np.float32), dst, 4)
    manifest.append((os.path.relpath(dst, OUT), [os.path.relpath(src, DL)]))


def ambience(src, dst, seconds=40.0, xf=3.0, target=-26.0):
    """Boucle de circulation lointaine : passe-bas (au loin, sous le toit), boucle par fondu enchaîné fin → début."""
    x = load(src, ch=1, lowpass=1600)
    L, Fn = int(seconds * SR), int(xf * SR)
    y = x[:, :L].copy()
    r = np.linspace(0, 1, Fn)
    y[:, :Fn] = x[:, :Fn] * r + x[:, L:L + Fn] * (1 - r)
    tmp = SYN + "tmp_amb.wav"
    write_wav(y, tmp)
    y = y * db(target - lufs(tmp))
    write_ogg(y.astype(np.float32), dst, 3)
    manifest.append((os.path.relpath(dst, OUT), [os.path.relpath(src, DL)]))


music(O + "funkymenuloop-longer.wav", OUT + "/music/menu.ogg")
music(O + "SimpleBeat_0.mp3", OUT + "/music/match.ogg")
music(O + "funky-victory-loop_0.mp3", OUT + "/music/victory.ogg", loop=False, tail_fade=1.2)
music(J + "steel/jingles_STEEL06.ogg", OUT + "/music/defeat.ogg", loop=False, tail_fade=0.3)
ambience(O + "gatve%20Varniu_2.ogg", OUT + "/ambience/rooftop.ogg")

with open(SYN + "manifest.txt", "w") as f:
    for dst, srcs in manifest:
        f.write(f"{dst}\t{' + '.join(srcs)}\n")
print(len(manifest), "fichiers")
