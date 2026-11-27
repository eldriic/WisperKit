#!/bin/zsh
# Signe WisperKit.app avec une identité donnée, de l'intérieur vers l'extérieur :
#   Scripts/sign-app.sh "<identité>" <chemin/WisperKit.app>
# Pas de --deep : il donnerait aux composants de Sparkle (installeur XPC, Autoupdate, Updater.app)
# l'identifiant de l'app. Chacun garde le sien ; seule l'app reçoit com.eldriic.wisperkit.
set -euo pipefail
IDENTITY="$1"
APP="$2"
SPARKLE="$APP/Contents/Frameworks/Sparkle.framework/Versions/B"
# Attributs étendus (iCloud Drive, Finder) : refusés par codesign.
xattr -cr "$APP"
if [[ -d "$SPARKLE" ]]; then
  codesign --force --sign "$IDENTITY" "$SPARKLE/XPCServices/Installer.xpc"
  codesign --force --sign "$IDENTITY" --preserve-metadata=entitlements "$SPARKLE/XPCServices/Downloader.xpc"
  codesign --force --sign "$IDENTITY" "$SPARKLE/Autoupdate"
  codesign --force --sign "$IDENTITY" "$SPARKLE/Updater.app"
  codesign --force --sign "$IDENTITY" "$APP/Contents/Frameworks/Sparkle.framework"
fi
codesign --force --sign "$IDENTITY" --identifier com.eldriic.wisperkit "$APP"
