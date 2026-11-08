# WisperKit

Dictée Whisper 100 % locale : raccourci global → enregistrement → transcription sur la machine → texte inséré au curseur. Deux apps natives qui partagent le même comportement, plus un site vitrine.

## Structure

| Dossier | Contenu |
| --- | --- |
| `Sources/WisperKitCore/` | Cœur macOS (package SPM) : audio, transcription WhisperKit (Core ML), machine à états `DictationController`, `TextCleaner`, `TextInserter` |
| `App/WisperKit/` | App SwiftUI barre de menus (target Xcode généré par XcodeGen depuis `project.yml`) |
| `Tests/WisperKitCoreTests/` | Swift Testing |
| `windows/` | App Windows C# .NET 10 / WPF : `WisperKit.Core` (portable, testable sur Mac), `WisperKit.App`, installeur Inno Setup, manifestes winget |
| `site/` | Site statique (wisperkit.eldriic.com, Vercel) : HTML/CSS/JS sans framework |
| `Scripts/` | `build.sh` (build/installation), `release.sh` (DMG + cask) |
| `Casks/` | Cask Homebrew : `wisperkit.rb.in` = modèle, `wisperkit.rb` = généré par `release.sh` |

`WisperKit.xcodeproj` est **généré** (ignoré par git) : modifier `project.yml`, jamais le projet Xcode.

## Commandes

```bash
swift test                                  # tests du cœur macOS
WK_INTEGRATION=1 swift test --filter Integration   # vraie transcription (modèle requis)
./Scripts/build.sh                          # build Release → build.noindex/WisperKit.app
./Scripts/build.sh --install                # + copie dans /Applications et relance

cd windows && dotnet test tests/WisperKit.Core.Tests           # tests Windows (marchent sur macOS)
cd windows && dotnet publish src/WisperKit.App -c Release -r win-x64 --self-contained -o publish/x64

python3 windows/Scripts/sync-strings.py     # régénère fr.json Windows depuis les .strings
python3 site/build.py                       # régénère la liste des versions de site/changelog.html
```

Prévisualisation du site : config `site` dans `.claude/launch.json` (port 4173).

Modes debug de l'app macOS : `--snapshots <dossier>` (rend chaque écran en PNG), `--selftest <secondes> [--paste <bundle id>]` (micro → modèle → presse-papiers). L'app Windows accepte aussi `--snapshots <dossier> [--first-launch]`.

## Règles du projet

- **Parité macOS / Windows** : une fonctionnalité ou une règle de comportement s'implémente des deux côtés (ex. `DecodingPlan` : `large-v3-turbo` n'a pas la tâche « translate », on force la sortie `en`). Penser aux tests des deux côtés.
- **Langues** : textes sources de l'UI en anglais. Traductions FR dans `App/WisperKit/fr.lproj/Localizable.strings` (source unique) ; textes propres à Windows dans `windows/src/WisperKit.Core/Localization/fr.windows.strings`. Ne pas éditer `fr.json` à la main : lancer `sync-strings.py`.
- **Commentaires de code en français** ; README, CHANGELOG et site en anglais (le README a une section française en fin).
- **Confidentialité** : aucun réseau hors téléchargement des modèles (Hugging Face), pas de télémétrie, l'audio n'est jamais écrit sur disque ni journalisé. Le log de diagnostic (`~/Library/Logs/WisperKit/wisperkit.log`, `%LOCALAPPDATA%\WisperKit\Logs\`) ne doit pas contenir le texte dicté.
- **Pas de sandbox ni de hardened runtime** sur macOS : nécessaires pour envoyer ⌘V via CGEvent. Ne pas les réactiver.

## Signature et builds macOS

- `build.sh` signe avec le certificat local auto-signé **« WisperKit Dev »** s'il est dans le trousseau (sinon ad-hoc). Une signature stable conserve l'autorisation Accessibilité entre deux builds ; en ad-hoc il faut la redonner à chaque fois.
- DerivedData et sorties dans des dossiers `.noindex` hors de `~/Documents` : iCloud Drive ajoute des attributs étendus qui cassent `codesign`. Copier avec `ditto --norsrc --noextattr`.
- Pas de notarisation (pas de compte Apple payant) ; le cask retire la quarantaine.

## Release

1. Mettre à jour `CHANGELOG.md` (Keep a Changelog, SemVer) et `MARKETING_VERSION` dans `project.yml`.
2. `./Scripts/release.sh <version>` → `dist/WisperKit.dmg` + `Casks/wisperkit.rb` (refuse sans le certificat « WisperKit Dev »). `--publish` crée la release GitHub avec les notes du CHANGELOG.
3. Copier le cask dans le dépôt `eldriic/homebrew-tap`.
4. La publication de la release déclenche le workflow `windows.yml`, qui attache les installeurs x64/arm64 et produit les manifestes winget (à soumettre à `winget-pkgs`).

Ne jamais publier (`--publish`, `gh release`, push du tap) sans accord explicite.

## CI et déploiement

- `.github/workflows/ci.yml` : `swift test` + build Xcode sur macOS.
- `.github/workflows/windows.yml` : tests, publish x64/arm64, captures, installeurs, manifestes winget (déclenché seulement si `windows/**` change).
- Vercel : déploie `site/` à chaque push qui touche `site/`, `CHANGELOG.md` ou `vercel.json` (build = `python3 site/build.py`). `.env.local` et `.vercel/` sont locaux, ne pas les lire ni les versionner.
- Le site télécharge directement depuis `releases/latest/download/` : garder les noms d'assets stables (`WisperKit.dmg`, `WisperKit-Setup-x64.exe`, `WisperKit-Setup-arm64.exe`).
