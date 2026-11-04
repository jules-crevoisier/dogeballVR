#!/usr/bin/env bash
# Télécharge les bibliothèques natives Steam (steam_api64.dll, libsteam_api.so) dans addons/steam_natives/.
# Elles ne sont pas dans le paquet NuGet Steamworks.NET et ne sont pas versionnées (voir .gitignore).
# Usage : tools/steam/fetch-steam-natives.sh [dossier_cible]
# Export : copier aussi ces fichiers à côté de l'exécutable exporté (le résolveur les cherche là en premier).
set -euo pipefail

VERSION="2024.8.0"
URL="https://github.com/rlabrecque/Steamworks.NET/releases/download/${VERSION}/Steamworks.NET-Standalone_${VERSION}.zip"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DEST="${1:-$ROOT/addons/steam_natives}"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

echo "[steam] Téléchargement de Steamworks.NET ${VERSION} (natifs)…"
curl -fsSL -o "$TMP/standalone.zip" "$URL"
mkdir -p "$TMP/x"
unzip -q "$TMP/standalone.zip" -d "$TMP/x"

mkdir -p "$DEST"
copy_one() {   # copie le premier fichier trouvé sous un chemin qui contient $1/$2
  local found
  found="$(find "$TMP/x" -type f -path "*$1/$2" | head -n 1)"
  if [ -z "$found" ]; then echo "[steam] Introuvable dans l'archive : $1/$2" >&2; exit 1; fi
  cp "$found" "$DEST/$2"
  echo "[steam] $2 -> $DEST"
}
copy_one "Windows-x64" "steam_api64.dll"
copy_one "OSX-Linux-x64" "libsteam_api.so"

# Tests hors Steam : Spacewar (480) par défaut. Un build Steam livré n'a pas de steam_appid.txt.
touch "$DEST/.gdignore"   # Godot n'importe pas ce dossier
echo "[steam] Terminé. Pour tester avec un client Steam lancé : echo 480 > steam_appid.txt (facultatif, 480 par défaut)."
