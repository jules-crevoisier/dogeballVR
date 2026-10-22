#!/usr/bin/env bash
# Prépare le dépôt pour l'export « Meta Quest » (APK Android, OpenXR, rendu Vulkan mobile).
# Utilisé par la CI (.github/workflows/quest.yml) et utilisable à la main sous Linux, macOS ou Git Bash.
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

# 1. Plugin OpenXR Vendors : seuls Meta (Android) et les bibliothèques de l'éditeur sont gardés (l'archive complète fait 200 Mo).
ADDON="$ROOT/addons/godotopenxrvendors"
if [ ! -f "$ADDON/plugin.gdextension" ]; then
  TMP="$(mktemp -d)"
  curl -sSfL -o "$TMP/vendors.zip" "https://github.com/GodotVR/godot_openxr_vendors/releases/download/${VENDORS_VERSION}/godotopenxrvendorsaddon.zip"
  unzip -q "$TMP/vendors.zip" -d "$TMP/v"
  SRC="$TMP/v/asset/addons/godotopenxrvendors"
  rm -rf "$SRC/.bin/macos" "$SRC/magicleap" "$SRC/pico" "$SRC/khronos"
  find "$SRC/.bin/android" -name "*.aar" ! -name "godotopenxr-meta-*" -delete
  mkdir -p "$ROOT/addons"
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
case "$(uname -s)" in
  Darwin) CONF="$HOME/Library/Application Support/Godot" ;;
  MINGW*|MSYS*|CYGWIN*) CONF="$APPDATA/Godot" ;;
  *) CONF="${XDG_CONFIG_HOME:-$HOME/.config}/godot" ;;
esac
SETTINGS="$CONF/editor_settings-4.3.tres"
if [ ! -f "$SETTINGS" ]; then
  mkdir -p "$CONF"
  cat > "$SETTINGS" <<TRES
[gd_resource type="EditorSettings" format=3]

[resource]
export/android/android_sdk_path = "$ANDROID_HOME"
export/android/java_sdk_path = "$JAVA_HOME"
export/android/debug_keystore = "$KEYSTORE"
export/android/debug_keystore_user = "androiddebugkey"
export/android/debug_keystore_pass = "android"
TRES
  echo "Réglages de l'éditeur écrits : $SETTINGS"
elif ! grep -q "export/android/android_sdk_path" "$SETTINGS"; then
  echo "Attention : $SETTINGS existe sans chemin du SDK Android. Le régler dans Éditeur > Paramètres de l'éditeur > Export > Android."
fi

# Le keystore passe aussi par l'environnement : il prime sur les réglages de l'éditeur.
echo "GODOT_ANDROID_KEYSTORE_DEBUG_PATH=$KEYSTORE"
echo "GODOT_ANDROID_KEYSTORE_DEBUG_USER=androiddebugkey"
echo "GODOT_ANDROID_KEYSTORE_DEBUG_PASSWORD=android"
