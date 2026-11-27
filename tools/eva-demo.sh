#!/usr/bin/env bash
# Lanceur rapide pour la démonstration en salle EVA (Linux / macOS).
# Usage :
#   ./tools/eva-demo.sh host [croix_neon|fosse_croisee]   # PC hôte LAN + raccourcis opérateur (F6/F7/F8/F10)
#   ./tools/eva-demo.sh solo [croix_neon|fosse_croisee]   # Démo solo PC contre bots
#   ./tools/eva-demo.sh spectator [croix_neon|fosse_croisee] # Vitrine caméra orbitale auto (écran TV)
#   ./tools/eva-demo.sh quest-cfg                         # Pousse un eva.cfg de démo sur un Quest branché en USB (adb)

set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GODOT="${GODOT_BIN:-godot}"
MODE="${1:-host}"
MAP="${2:-croix_neon}"

case "$MODE" in
  host)
    echo "[EVA] Lancement PC Hôte LAN — Arène : $MAP (F6 Relancer | F7 Arène | F8 Caméra TV | F10 Masquer)"
    exec "$GODOT" --path "$ROOT" -- --host --map="$MAP" --eva-demo --eva-end-hold=15
    ;;
  solo)
    echo "[EVA] Lancement Démo Solo — Arène : $MAP"
    exec "$GODOT" --path "$ROOT" -- --map="$MAP" --eva-demo --eva-end-hold=15
    ;;
  spectator)
    echo "[EVA] Lancement Vitrine TV (bots + caméra orbitale) — Arène : $MAP"
    exec "$GODOT" --path "$ROOT" -- --map="$MAP" --pc-autoplay --vista-orbit --eva-demo
    ;;
  quest-cfg)
    PKG="${EVA_QUEST_PKG:-com.julescrevoisier.dogeballvr}"
    TMP="$(mktemp)"
    cat > "$TMP" <<'EOF'
[eva]
haptics=85
volume=85
reduce_flashes=false
end_hold=15
EOF
    echo "[EVA] Envoi de eva.cfg sur le Quest ($PKG)..."
    adb shell "mkdir -p /sdcard/Android/data/$PKG/files"
    adb push "$TMP" "/sdcard/Android/data/$PKG/files/eva.cfg"
    rm -f "$TMP"
    echo "[EVA] eva.cfg installé sur le casque."
    ;;
  *)
    echo "Usage: $0 {host|solo|spectator|quest-cfg} [croix_neon|fosse_croisee]"
    exit 1
    ;;
esac
