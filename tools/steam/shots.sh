#!/usr/bin/env bash
# Captures de la page Steam prises dans le vrai jeu (joueur PC automatique), puis capsules recadrées.
# Usage : tools/steam/shots.sh [DOSSIER]   (Linux : xvfb-run si pas d'écran ; Windows : lancer les mêmes commandes dans Git Bash)
# GODOT doit pointer vers l'éditeur Godot 4.3 .NET ; projet compilé avant (dotnet build).
set -u
OUT="${1:-$PWD/docs/steam/captures}"
GODOT="${GODOT:-godot}"
RES="${RES:-1920x1080}"
mkdir -p "$OUT"
RUN=()
[ -z "${DISPLAY:-}" ] && RUN=(xvfb-run -a -s "-screen 0 ${RES}x24")
# Sans carte graphique (serveur, CI), le rendu Compatibilité passe par Mesa ; sur un vrai PC, retirer ces deux options pour le rendu complet.
DRIVER=(${DRIVER---rendering-method gl_compatibility --rendering-driver opengl3})
shoot() { # nom carte instants options...
  local name=$1 map=$2 at=$3; shift 3
  "${RUN[@]}" "$GODOT" --path . "${DRIVER[@]}" --resolution "$RES" -- --map="$map" --pc-autoplay --shots="$OUT" --shots-name="$name" --shots-at="$at" "$@" 2>&1 | grep -E "\[Captures\]|ERROR: (System|Unhandled)" || true
}
shoot croix-neon croix_neon 9,16,23,31
shoot tour-4v4 tour_4v4 9,18,27
shoot fosse-croisee fosse_croisee 10,20
shoot fin-de-partie croix_neon 19 --end-screen-test
shoot menu croix_neon 4 --menu
python3 "$(dirname "$0")/capsules.py" "$OUT"
