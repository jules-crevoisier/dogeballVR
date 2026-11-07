#!/usr/bin/env bash
# Captures de la page Steam prises dans le vrai jeu (joueur PC automatique), puis capsules recadrées.
# Usage : tools/steam/shots.sh [DOSSIER]   (Linux : xvfb-run si pas d'écran ; Windows : lancer les mêmes commandes dans Git Bash)
# GODOT doit pointer vers l'éditeur Godot 4.3 .NET ; projet compilé avant (dotnet build) et importé (la table anglaise
# assets/i18n/textes.en.translation est produite à l'import).
# Le joueur automatique cadre au hasard : on prend beaucoup d'instants par arène et on garde les meilleures à la main.
set -u
OUT="${1:-$PWD/docs/steam/captures}"
GODOT="${GODOT:-godot}"
RES="${RES:-1920x1080}"
mkdir -p "$OUT"
RUN=()
[ -z "${DISPLAY:-}" ] && RUN=(xvfb-run -a -s "-screen 0 ${RES}x24")
# Rendu du jeu (Mobile, Vulkan). Sans carte graphique : installer mesa-vulkan-drivers (lavapipe) ;
# en dernier recours DRIVER="--rendering-method gl_compatibility --rendering-driver opengl3" (rendu dégradé).
DRIVER=(${DRIVER---rendering-driver vulkan})
shoot() { # nom carte instants options...
  local name=$1 map=$2 at=$3; shift 3
  "${RUN[@]}" "$GODOT" --path . "${DRIVER[@]}" --resolution "$RES" -- --map="$map" --format=online --shots="$OUT" \
    --shots-name="$name" --shots-at="$at" --lang=en "$@" 2>&1 | grep -E "\[Captures\]|ERROR: (System|Unhandled)" || true
}
# Page Steam : les trois arènes PC (la VR n'est pas vendue sur Steam), en anglais (langue de repli du store), au format
# en ligne (3 manches) comme une vraie partie. La balle spéciale est annoncée à 40 s et posée à 45 s.
shoot toits marche_suspendu 9,14,19,24,29,34,42,47,52 --pc-autoplay
shoot piscine piscine 9,14,19,24,29,34,42,47,52 --pc-autoplay
shoot depot depot 9,14,19,24,29,34,42,47,52 --pc-autoplay
# Fonds des capsules : caméra de plateau (vue d'ensemble de l'arène, partie en cours), sans HUD ni étiquettes.
shoot capsule-toits marche_suspendu 20,35,47 --pc-autoplay --shots-clean --shots-cam=15,9,-21,0,-1,0,55
shoot capsule-piscine piscine 20,35,47 --pc-autoplay --shots-clean --shots-cam=13,8,-15,0,0,0,58
shoot capsule-depot depot 20,35,47 --pc-autoplay --shots-clean --shots-cam=15,9,-19,0,0,0,55
# Écran de fin : sans joueur automatique (il relancerait une partie aussitôt la précédente finie).
shoot fin-de-partie marche_suspendu 28 --end-screen-test
shoot menu marche_suspendu 6 --menu --pc-autoplay
python3 "$(dirname "$0")/capsules.py" "$OUT"
