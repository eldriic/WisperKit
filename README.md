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

Signature : `build.sh` signe avec le certificat local auto-signé « WisperKit Dev » (trousseau de
session) s'il existe, sinon en ad-hoc. Avec le certificat, macOS identifie l'app par
`identifier "com.eldriic.wisperkit" and certificate leaf = …` : l'autorisation Accessibilité
survit aux mises à jour. En ad-hoc, elle est liée à l'empreinte exacte du binaire et saute à
chaque build (collage auto refusé, `accessibilité=false` dans le journal).
