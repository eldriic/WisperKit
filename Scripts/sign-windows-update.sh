#!/bin/zsh
# Signe le manifeste de mise à jour Windows d'une release (après le workflow windows.yml,
# qui y attache les installeurs) :
#   ./Scripts/sign-windows-update.sh 0.3.0             prépare dist/update-windows.json(.sig)
#   ./Scripts/sign-windows-update.sh 0.3.0 --publish   + les attache à la release v0.3.0
#
# La clé privée ECDSA P-256 reste sur ce Mac (trousseau, copie dans Vaultwarden), jamais sur GitHub :
# un compte GitHub compromis ne suffit pas à faire installer un binaire par l'app.
# Vérifie avant de signer que les empreintes correspondent à celles affichées par le job « Installers »
# du workflow (journal de la CI).
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION="${1:?usage: sign-windows-update.sh <version> [--publish]}"
REPO="eldriic/WisperKit"
TAG="v$VERSION"
KEY_SERVICE="WisperKit update signing (Windows)"

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT
gh release download "$TAG" --repo "$REPO" --dir "$WORK" --pattern 'WisperKit-Setup-x64.exe' --pattern 'WisperKit-Setup-arm64.exe'

python3 - "$VERSION" "$WORK" > "$WORK/update-windows.json" <<'PY'
import hashlib, json, pathlib, sys
version, work = sys.argv[1], pathlib.Path(sys.argv[2])
base = f"https://github.com/eldriic/WisperKit/releases/download/v{version}"
installers = {}
for arch in ("x64", "arm64"):
    data = (work / f"WisperKit-Setup-{arch}.exe").read_bytes()
    installers[arch] = {"url": f"{base}/WisperKit-Setup-{arch}.exe",
                        "sha256": hashlib.sha256(data).hexdigest().upper(), "size": len(data)}
    print(f"{arch}  {installers[arch]['sha256']}  {len(data)} o", file=sys.stderr)
json.dump({"version": version, "notes": f"https://github.com/eldriic/WisperKit/releases/tag/v{version}",
           "installers": installers}, sys.stdout, indent=2)
PY

# Clé privée lue depuis le trousseau dans un fichier temporaire 0600, effacé à la sortie.
umask 077
security find-generic-password -a wisperkit -s "$KEY_SERVICE" -w | base64 -D > "$WORK/key.pem"
openssl dgst -sha256 -sign "$WORK/key.pem" "$WORK/update-windows.json" | base64 > "$WORK/update-windows.json.sig"
rm -f "$WORK/key.pem"
# Contrôle avec la clé publique embarquée dans l'app.
sed -n '/BEGIN PUBLIC KEY/,/END PUBLIC KEY/p' windows/src/WisperKit.Core/Updates.cs | sed 's/^ *//' > "$WORK/pub.pem"
base64 -D -i "$WORK/update-windows.json.sig" > "$WORK/sig.der"
openssl dgst -sha256 -verify "$WORK/pub.pem" -signature "$WORK/sig.der" "$WORK/update-windows.json"

mkdir -p dist
cp "$WORK/update-windows.json" "$WORK/update-windows.json.sig" dist/
echo "→ dist/update-windows.json(.sig)"

if [[ "${2:-}" == "--publish" ]]; then
  gh release upload "$TAG" dist/update-windows.json dist/update-windows.json.sig --repo "$REPO" --clobber
fi
