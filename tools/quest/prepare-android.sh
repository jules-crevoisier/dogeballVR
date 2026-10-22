#!/usr/bin/env bash
# Prépare le dépôt pour l'export « Meta Quest » (APK Android, OpenXR, rendu Vulkan mobile).
# Utilisé par la CI (.github/workflows/quest.yml) et utilisable à la main sous Linux ou Git Bash (Windows).
#
#   GODOT=/chemin/vers/godot-mono ANDROID_HOME=... JAVA_HOME=... tools/quest/prepare-android.sh
#
# 1. Plugin Godot OpenXR Vendors 3.1.2 (chargeur OpenXR de Meta), réduit aux fichiers utiles.
# 2. Keystore de debug (tools/quest/debug.keystore ; pas de clé de release : APK à installer soi-même, pas pour le Store).
# 3. Chemins du SDK Android et du JDK 17 dans les réglages de l'éditeur, s'ils n'y sont pas déjà.
# Le modèle Gradle d'Android (android/) s'installe à l'export : --install-android-build-template.
# Les dossiers android/ et addons/godotopenxrvendors/ sont ignorés par git.
set -euo pipefail

VENDORS_VERSION=3.1.2-stable
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
: "${GODOT:?Indiquer GODOT (exécutable Godot 4.3 .NET)}"
: "${ANDROID_HOME:?Indiquer ANDROID_HOME (SDK Android avec build-tools 34, platform 34, NDK 23.2.8568313)}"
: "${JAVA_HOME:?Indiquer JAVA_HOME (JDK 17)}"

# 1. Plugin OpenXR Vendors : seuls Meta (Android) et les bibliothèques de l'éditeur Windows et Linux sont gardés
#    (l'archive complète fait 200 Mo).
ADDON="$ROOT/addons/godotopenxrvendors"
if [ ! -f "$ADDON/plugin.gdextension" ]; then
  TMP="$(mktemp -d)"
  curl -sSfL -o "$TMP/vendors.zip" "https://github.com/GodotVR/godot_openxr_vendors/releases/download/${VENDORS_VERSION}/godotopenxrvendorsaddon.zip"
  unzip -q "$TMP/vendors.zip" -d "$TMP/v"
  SRC="$TMP/v/asset/addons/godotopenxrvendors"
  rm -rf "$SRC/.bin/macos" "$SRC/magicleap" "$SRC/pico" "$SRC/khronos"
  find "$SRC/.bin/android" -name "*.aar" ! -name "godotopenxr-meta-*" -delete
  mkdir -p "$ROOT/addons"
  rm -rf "$ADDON"   # un dossier incomplet laissé par un essai précédent
  mv "$SRC" "$ADDON"
  rm -rf "$TMP"
  echo "Plugin OpenXR Vendors ${VENDORS_VERSION} installé (Meta seulement)."
fi

# 2. Keystore de debug (mot de passe et alias standards d'Android). Celui du dépôt est fixe : chaque nouvel APK
#    s'installe par-dessus le précédent sans désinstaller. Ce n'est pas une clé de sortie, elle ne protège rien.
KEYSTORE="${GODOT_ANDROID_KEYSTORE_DEBUG_PATH:-$ROOT/tools/quest/debug.keystore}"
if [ ! -f "$KEYSTORE" ]; then
  mkdir -p "$(dirname "$KEYSTORE")"
  "$JAVA_HOME/bin/keytool" -genkeypair -v -keystore "$KEYSTORE" -storepass android -alias androiddebugkey \
    -keypass android -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=Android Debug,O=Android,C=US" > /dev/null 2>&1
  echo "Keystore de debug créé : $KEYSTORE"
fi

# 3. Réglages de l'éditeur Godot 4.3 : SDK Android et JDK (un fichier existant n'est pas écrasé).
# Sous Git Bash, les chemins sont écrits pour Windows (C:/Users/…) : Godot ne lit ni /c/Users/… ni les \ (échappements).
native() { if command -v cygpath > /dev/null; then cygpath -m "$1"; else printf '%s' "$1"; fi; }
case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) CONF="$(cygpath -u "$APPDATA")/Godot" ;;
  *) CONF="${XDG_CONFIG_HOME:-$HOME/.config}/godot" ;;
esac
SETTINGS="$CONF/editor_settings-4.3.tres"
if [ ! -f "$SETTINGS" ]; then
  mkdir -p "$CONF"
  cat > "$SETTINGS" <<TRES
[gd_resource type="EditorSettings" format=3]

[resource]
export/android/android_sdk_path = "$(native "$ANDROID_HOME")"
export/android/java_sdk_path = "$(native "$JAVA_HOME")"
export/android/debug_keystore = "$(native "$KEYSTORE")"
export/android/debug_keystore_user = "androiddebugkey"
export/android/debug_keystore_pass = "android"
TRES
  echo "Réglages de l'éditeur écrits : $SETTINGS"
else
  # Réglages déjà présents (éditeur déjà ouvert) : on ne les touche pas. Sans ce keystore, l'APK construit ici serait signé
  # par une autre clé et ne s'installerait pas par-dessus celui de la CI (INSTALL_FAILED_UPDATE_INCOMPATIBLE).
  echo "Réglages de l'éditeur déjà présents ($SETTINGS), non modifiés. À vérifier dans Éditeur > Paramètres de l'éditeur > Export > Android :"
  echo "  SDK Android = $(native "$ANDROID_HOME") ; JDK = $(native "$JAVA_HOME")"
  echo "  Debug Keystore = $(native "$KEYSTORE") ; utilisateur androiddebugkey ; mot de passe android"
fi
