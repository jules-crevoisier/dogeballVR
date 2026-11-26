"""Relève les textes affichés au joueur dans scripts/ (littéraux C# en français) pour la table de traduction.
Écarte les journaux (GD.Print…), chemins, noms de nœuds, options de dev et identifiants. Les chaînes interpolées $"…{x}…" ne sont
pas des clés : le code doit passer par L.F("…{0}…", x) ; elles sont listées à part pour les repérer.
Usage : python3 tools/i18n/extract.py [--interp]   (écrit la liste sur la sortie standard)
        python3 tools/i18n/extract.py --check      (CI : chaque gabarit de L.T / L.F / L.Plural a sa ligne dans assets/i18n/textes.csv,
                                                   avec les mêmes {0}, {1}… des deux côtés ; code de sortie 1 sinon)"""
import csv, os, re, sys

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "scripts")
SKIP_FILES = re.compile(r"(SelfTest|CamTest|PerfProbe|CrashReporter|Steam/|DevFlags|Probe|GameSettings|EvaSettings|NetHandshake\.cs|Music\.cs|AvatarIk|HomeTestRecenter)")
SKIP_LINE = re.compile(r"GD\.Print|GD\.PushWarning|GD\.PushError|Name = \"|DevFlags|FindChild|GetNode|\[Export|Log\(|throw new|Debug|nameof|SetMeta|GetMeta|HasMeta|AddThemeColorOverride\(\"|AddThemeFontSizeOverride\(\"|AddThemeConstantOverride\(\"|AddThemeStyleboxOverride\(\"|AddThemeFontOverride\(\"|ResourceLoader|GD\.Load|FileAccess|Input\.|InputMap|Rpc", re.I)
LIT = re.compile(r'(\$?)@?"((?:[^"\\]|\\.)*)"')


def interesting(s, ui=False):
    if len(s) < 2 or s.startswith(("res://", "user://", "--", "#")) or re.fullmatch(r"[a-z0-9_./:#%-]+", s):
        return False
    if re.fullmatch(r"[0-9A-Fa-f]{6,8}", s) or re.fullmatch(r"[\w.-]+\.(ttf|otf|ogg|png|glb|json|tres|tscn)", s) or re.fullmatch(r"[A-Z][a-zA-Z]+\.[GD]", s):
        return False
    if s in ("SFX", "UI", "USER", "USERNAME", "OpenXR", "HSlider", "VScrollBar"):
        return False
    if re.fullmatch(r"[A-Za-z_]+", s) and not re.search(r"[A-ZÉÈÀ]{2}", s) and not (ui and re.fullmatch(r"[A-Z][a-zé]+", s)):   # identifiants (Camel, snake)
        return False
    return bool(re.search(r"[A-Za-zÀ-ÿ]{2}", s))


def scan(want_interp):
    out = []
    for d, _, files in os.walk(ROOT):
        for f in sorted(files):
            if not f.endswith(".cs"):
                continue
            path = os.path.join(d, f)
            rel = os.path.relpath(path, ROOT)
            if SKIP_FILES.search(rel):
                continue
            for n, line in enumerate(open(path, encoding="utf-8"), 1):
                code = line.split("//")[0] if "\"" not in line.split("//")[0][-1:] else line
                if SKIP_LINE.search(code):
                    continue
                for m in LIT.finditer(code):
                    interp, s = m.group(1) == "$", m.group(2)
                    if interp != want_interp or not interesting(re.sub(r"\{[^}]*\}", "", s), rel.startswith(("UI", "Online/Lobby", "Gameplay/DesktopHud"))):
                        continue
                    out.append((rel, n, s))
    return out


CSV = os.path.join(os.path.dirname(__file__), "..", "..", "assets", "i18n", "textes.csv")
CALL = re.compile(r"\bL\.(?:T|F|Plural)\(")
STR = re.compile(r'(?<![$@])"((?:[^"\\]|\\.)*)"')


def unescape(s):
    return s.replace("\\n", "\n").replace('\\"', '"').replace("\\\\", "\\")


def check():
    """Clés appelées dans le code (premiers littéraux de chaque appel L.T / L.F / L.Plural) absentes de la table, et {n} différents."""
    rows = {r[0]: r[1] for r in list(csv.reader(open(CSV, encoding="utf-8")))[1:]}
    bad = []
    for k, v in rows.items():
        if sorted(re.findall(r"\{\d[^}]*\}", k)) != sorted(re.findall(r"\{\d[^}]*\}", v)):
            bad.append(f"textes.csv : {{n}} différents entre « {k} » et « {v} »")
    for d, _, files in os.walk(ROOT):
        for f in sorted(files):
            if not f.endswith(".cs") or f == "L.cs":
                continue
            path = os.path.join(d, f)
            for n, line in enumerate(open(path, encoding="utf-8"), 1):
                for m in CALL.finditer(line):
                    # Clé : le littéral placé juste après la parenthèse ; L.Plural(n, "une forme", "plusieurs") : les deux formes.
                    rest = line[m.end():]
                    if line[m.start():m.end()].startswith("L.Plural"):
                        rest = rest.split(",", 1)[1] if "," in rest else ""
                    keys = []
                    for _ in range(2 if line[m.start():m.end()].startswith("L.Plural") else 1):
                        lit = STR.match(rest.lstrip())
                        if not lit:
                            break
                        keys.append(unescape(lit.group(1)))
                        rest = rest.lstrip()[lit.end():].lstrip().lstrip(",")
                    for k in keys:
                        if k and k not in rows and re.search(r"[A-Za-zÀ-ÿ]{2}", k):
                            bad.append(f"{os.path.relpath(path, ROOT)}:{n} : « {k} » absent de textes.csv")
    for b in bad:
        print(b)
    print(f"{len(rows)} textes traduits, {len(bad)} problème(s)")
    return 1 if bad else 0


if __name__ == "__main__":
    if "--check" in sys.argv:
        sys.exit(check())
    seen = set()
    for rel, n, s in scan("--interp" in sys.argv):
        if s in seen:
            continue
        seen.add(s)
        print(f"{rel}:{n}\t{s}")
