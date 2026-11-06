# WisperKit

Dictée locale pour macOS : un raccourci global (⌃⌥D par défaut) enregistre le micro,
Whisper (via [WhisperKit](https://github.com/argmaxinc/WhisperKit), Core ML / Neural Engine)
transcrit sur le Mac, et le texte est tapé directement dans l'app au premier plan.

- App de barre de menus (pas d'icône Dock) + fenêtre Réglages : Général, Modèle, Clavier, Autorisations.
- Modèles téléchargés dans `~/Library/Application Support/WisperKit/Models` (défaut : large-v3-turbo).
- Autorisations : Microphone, et Accessibilité pour le collage au curseur (sinon, presse-papiers seul).

## Développement

```bash
swift test --scratch-path ~/Library/Caches/WisperKit/spm          # tests du cœur
WK_INTEGRATION=1 swift test --scratch-path ~/Library/Caches/WisperKit/spm --filter Integration
./Scripts/build.sh            # build/WisperKit.app (Release)
./Scripts/build.sh --install  # copie dans /Applications et relance
```

Les builds se font hors de `~/Documents` : iCloud Drive y ajoute des attributs étendus qui cassent `codesign`.

En Debug, deux modes utilitaires :
`WisperKit --snapshots <dossier>` (PNG de chaque écran) et `WisperKit --selftest <s> [--paste <bundle id>]` (micro → modèle → presse-papiers, collage dans l'app donnée).
Diagnostic du collage : `~/Library/Logs/WisperKit/wisperkit.log`.

Signature ad-hoc par défaut : après chaque nouveau build, macOS peut demander de réactiver
l'Accessibilité. Avec un certificat local de signature de code « WisperKit Dev » :
`WK_SIGN_IDENTITY="WisperKit Dev" ./Scripts/build.sh`.
