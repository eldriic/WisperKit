#!/bin/zsh
# Génère le projet, compile WisperKit.app (Release) et le copie dans build/.
#   ./Scripts/build.sh            build seul
#   ./Scripts/build.sh --install  build + copie dans /Applications + relance
#
# Le DerivedData est hors de ~/Documents : iCloud Drive y ajoute des attributs
# étendus (FinderInfo) qui font échouer codesign.
# Signature : certificat local « WisperKit Dev » s'il existe (ou WK_SIGN_IDENTITY),
# sinon ad-hoc. Avec un certificat stable, l'autorisation Accessibilité survit aux
# mises à jour ; en ad-hoc, macOS la lie à l'empreinte exacte du binaire.
set -euo pipefail
cd "$(dirname "$0")/.."

DERIVED="$HOME/Library/Caches/WisperKit/DerivedData"
# Certificat local « WisperKit Dev » utilisé automatiquement s'il est dans le trousseau.
if [[ -z "${WK_SIGN_IDENTITY:-}" ]] && security find-identity -p codesigning | grep -q '"WisperKit Dev"'; then
  WK_SIGN_IDENTITY="WisperKit Dev"
fi
IDENTITY="${WK_SIGN_IDENTITY:--}"
echo "Signature : ${IDENTITY/#-/ad-hoc}"

xcodegen generate --quiet
# Compilation signée ad-hoc, puis re-signature avec l'identité choisie : Xcode refuse
# les certificats auto-signés (non approuvés), codesign les accepte.
xcodebuild -project WisperKit.xcodeproj -scheme WisperKit -configuration Release \
  -derivedDataPath "$DERIVED" CODE_SIGN_IDENTITY="-" build -quiet

APP="$DERIVED/Build/Products/Release/WisperKit.app"
rm -rf build/WisperKit.app && mkdir -p build
ditto --norsrc --noextattr "$APP" build/WisperKit.app
if [[ "$IDENTITY" != "-" ]]; then
  codesign --force --deep --sign "$IDENTITY" --identifier com.eldriic.wisperkit build/WisperKit.app
fi
codesign --verify --deep build/WisperKit.app
echo "→ build/WisperKit.app"

if [[ "${1:-}" == "--install" ]]; then
  pkill -x WisperKit 2>/dev/null || true
  while pgrep -x WisperKit >/dev/null; do sleep 0.2; done  # sinon open échoue (-600)
  rm -rf /Applications/WisperKit.app
  ditto --norsrc --noextattr build/WisperKit.app /Applications/WisperKit.app
  open /Applications/WisperKit.app
  echo "→ /Applications/WisperKit.app lancé"
fi
