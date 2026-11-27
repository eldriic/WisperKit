#!/bin/zsh
# Prépare une release : build signé, DMG « glisser dans Applications », SHA-256,
# appcast Sparkle signé EdDSA (dist/appcast.xml), cask Homebrew à jour (Casks/wisperkit.rb).
#   ./Scripts/release.sh 0.1.0             prépare dist/ sans rien publier
#   ./Scripts/release.sh 0.1.0 --publish   + crée la release GitHub v0.1.0 (gh)
#
# Les releases doivent toujours être signées avec le même certificat (« WisperKit Dev ») :
# c'est ce qui permet aux utilisateurs de garder l'autorisation Accessibilité d'une version à l'autre.
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION="${1:?usage: release.sh <version> [--publish]}"
REPO="eldriic/WisperKit"
DMG="dist/WisperKit.dmg"

security find-identity -p codesigning | grep -q '"WisperKit Dev"' \
  || { echo "Certificat « WisperKit Dev » absent du trousseau : release refusée." >&2; exit 1; }
# Outils Sparkle livrés avec le paquet SPM (résolu par build.sh) ; clé privée EdDSA dans le trousseau
# (compte « wisperkit », copie dans Vaultwarden).
SPARKLE_BIN="$HOME/Library/Caches/WisperKit/DerivedData.noindex/SourcePackages/artifacts/sparkle/Sparkle/bin"

WK_VERSION="$VERSION" ./Scripts/build.sh

STAGE=$(mktemp -d)
trap 'rm -rf "$STAGE"' EXIT
# Copie sans attributs étendus puis re-signature hors de ~/Documents : iCloud Drive
# ajoute des attributs Finder qui invalident la signature (« app endommagée »).
ditto --norsrc --noextattr build.noindex/WisperKit.app "$STAGE/WisperKit.app"
./Scripts/sign-app.sh "WisperKit Dev" "$STAGE/WisperKit.app"
codesign --verify --deep --strict "$STAGE/WisperKit.app"
ln -s /Applications "$STAGE/Applications"
mkdir -p dist && rm -f "$DMG"
hdiutil create -quiet -volname "WisperKit $VERSION" -srcfolder "$STAGE" -fs HFS+ -format UDZO -ov "$DMG"
codesign --force --sign "WisperKit Dev" "$DMG"
SHA=$(shasum -a 256 "$DMG" | awk '{print $1}')
echo "→ $DMG"
echo "  sha256 $SHA"

[[ -x "$SPARKLE_BIN/sign_update" ]] || { echo "sign_update introuvable ($SPARKLE_BIN)" >&2; exit 1; }
SPARKLE_SIG=$("$SPARKLE_BIN/sign_update" --account wisperkit "$DMG")
python3 Scripts/appcast.py "$VERSION" "$SPARKLE_SIG"

sed -e "s/@VERSION@/$VERSION/" -e "s/@SHA256@/$SHA/" Casks/wisperkit.rb.in > Casks/wisperkit.rb
echo "→ Casks/wisperkit.rb (à copier dans eldriic/homebrew-tap)"

if [[ "${2:-}" == "--publish" ]]; then
  # Notes : section de la version dans CHANGELOG.md.
  NOTES=$(awk -v v="## [$VERSION]" 'index($0, v) == 1 {on=1; next} /^## \[/ {on=0} on' CHANGELOG.md)
  gh release create "v$VERSION" "$DMG" dist/appcast.xml --repo "$REPO" --title "WisperKit $VERSION" --notes "$NOTES"
  echo "Après le workflow Windows (installeurs attachés) : ./Scripts/sign-windows-update.sh $VERSION --publish"
fi
