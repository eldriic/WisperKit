#!/bin/zsh
# Génère le projet, compile WisperKit.app (Release) et le copie dans build/.
#   ./Scripts/build.sh            build seul
#   ./Scripts/build.sh --install  build + copie dans /Applications + relance
#
# Le DerivedData est hors de ~/Documents : iCloud Drive y ajoute des attributs
# étendus (FinderInfo) qui font échouer codesign.
# WK_SIGN_IDENTITY="WisperKit Dev" pour signer avec un certificat local stable
# (sinon ad-hoc : l'autorisation Accessibilité est à redonner après chaque build).
set -euo pipefail
cd "$(dirname "$0")/.."

DERIVED="$HOME/Library/Caches/WisperKit/DerivedData"
IDENTITY="${WK_SIGN_IDENTITY:--}"

xcodegen generate --quiet
xcodebuild -project WisperKit.xcodeproj -scheme WisperKit -configuration Release \
  -derivedDataPath "$DERIVED" CODE_SIGN_IDENTITY="$IDENTITY" build -quiet

APP="$DERIVED/Build/Products/Release/WisperKit.app"
rm -rf build/WisperKit.app && mkdir -p build
ditto --norsrc --noextattr "$APP" build/WisperKit.app
echo "→ build/WisperKit.app"

if [[ "${1:-}" == "--install" ]]; then
  pkill -x WisperKit 2>/dev/null || true
  rm -rf /Applications/WisperKit.app
  ditto --norsrc --noextattr "$APP" /Applications/WisperKit.app
  open /Applications/WisperKit.app
  echo "→ /Applications/WisperKit.app lancé"
fi
