#!/bin/zsh
# Prépare une release : build signé, DMG « glisser dans Applications », SHA-256,
# cask Homebrew à jour (Casks/wisperkit.rb).
#   ./Scripts/release.sh 0.1.0             prépare dist/ sans rien publier
#   ./Scripts/release.sh 0.1.0 --publish   + crée la release GitHub v0.1.0 (gh)
#
# Les releases doivent toujours être signées avec le même certificat (« WisperKit Dev ») :
# c'est ce qui permet aux utilisateurs de garder l'autorisation Accessibilité d'une version à l'autre.
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION="${1:?usage: release.sh <version> [--publish]}"
REPO="eldriic/WisperKit"
DMG="dist/WisperKit-$VERSION.dmg"

security find-identity -p codesigning | grep -q '"WisperKit Dev"' \
  || { echo "Certificat « WisperKit Dev » absent du trousseau : release refusée." >&2; exit 1; }

WK_VERSION="$VERSION" ./Scripts/build.sh

STAGE=$(mktemp -d)
trap 'rm -rf "$STAGE"' EXIT
# Copie sans attributs étendus puis re-signature hors de ~/Documents : iCloud Drive
# ajoute des attributs Finder qui invalident la signature (« app endommagée »).
ditto --norsrc --noextattr build.noindex/WisperKit.app "$STAGE/WisperKit.app"
codesign --force --deep --sign "WisperKit Dev" --identifier com.eldriic.wisperkit "$STAGE/WisperKit.app"
codesign --verify --deep --strict "$STAGE/WisperKit.app"
ln -s /Applications "$STAGE/Applications"
mkdir -p dist && rm -f "$DMG"
hdiutil create -quiet -volname "WisperKit $VERSION" -srcfolder "$STAGE" -fs HFS+ -format UDZO -ov "$DMG"
codesign --force --sign "WisperKit Dev" "$DMG"
SHA=$(shasum -a 256 "$DMG" | awk '{print $1}')
echo "→ $DMG"
echo "  sha256 $SHA"

sed -e "s/@VERSION@/$VERSION/" -e "s/@SHA256@/$SHA/" Casks/wisperkit.rb.in > Casks/wisperkit.rb
echo "→ Casks/wisperkit.rb (à copier dans eldriic/homebrew-tap)"

if [[ "${2:-}" == "--publish" ]]; then
  # Notes : section de la version dans CHANGELOG.md.
  NOTES=$(awk -v v="## [$VERSION]" 'index($0, v) == 1 {on=1; next} /^## \[/ {on=0} on' CHANGELOG.md)
  gh release create "v$VERSION" "$DMG" --repo "$REPO" --title "WisperKit $VERSION" --notes "$NOTES"
fi
