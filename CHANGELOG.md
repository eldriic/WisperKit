# Changelog

All notable changes to WisperKit are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [0.3.0] - 2026-11-27

Updating from 0.2.1 or earlier is manual (download the new version, or `brew upgrade --cask wisperkit`); from 0.3.0 on, WisperKit updates itself.

### Added
- **Updates** (macOS and Windows): WisperKit checks GitHub once a day for a new version and offers it in its menu (**Update available**); on Windows a notification also appears once per version. One click downloads, verifies and installs it, then WisperKit reopens with your settings and models. Turn the check off or run it by hand in **Settings → Updates**.
- macOS updates use [Sparkle](https://sparkle-project.org) with EdDSA-signed archives; Windows installers must match a SHA-256 listed in an ECDSA-signed manifest. The signing keys never leave the release machine.

### Changed
- **Privacy**: besides the model download, the update check is now the only network request (no data sent, can be turned off).
- **Homebrew**: the cask is marked `auto_updates`, since WisperKit updates itself.
- **Windows**: the diagnostic log uses local time with the date, instead of UTC time only.

### Fixed
- **Windows**: models could not be downloaded (the download never completed).

## [0.2.1] - 2026-11-27

### Fixed
- **Windows**: the app quit right after launch and no icon appeared in the notification area (the tray icon library could not convert the rendered icon).
- **Windows**: the wave indicator barely moved with a normal voice. Windows delivers the raw microphone signal with no gain control, often 15–25 dB quieter than macOS; the level now adapts to the microphone's noise floor and your voice. The peak and noise levels of each dictation are written to the diagnostic log.

### Changed
- **Windows installer**: dark look matching the app (black background, violet halo, the recording wave), welcome page with what WisperKit does and where to find it.
- **Windows installer**: install for yourself (default, no admin rights) or for all users (admin, Program Files); a running WisperKit is closed before updating, a previous copy in the other scope is removed, the start-at-sign-in entry is checked after install, a missing executable (antivirus) is reported, and the uninstaller offers to delete the downloaded models and settings. Optional desktop shortcut. Setup log in `%TEMP%`.

## [0.2.0] - 2026-11-07

### Added
- **Windows version** (Windows 10/11, x64 and ARM64): notification-area app in C# / WPF with the same features as macOS — global shortcut (Ctrl+Alt+D, toggle or hold, Esc to cancel), whisper.cpp transcription on CPU or GPU (Vulkan), auto-insert by paste or clipboard-free typing (kept out of the Win+V history), wave indicator, English/French settings and first-launch guide.
- Per-user installer (no admin rights) and winget manifests.
- **Translate to English** (macOS and Windows): speak any language and get English text, using Whisper's translation task, or forced English output with `large-v3-turbo`.

### Changed
- The dictation language now defaults to **auto-detect** on both platforms, so the text comes out in the language you speak.

## [0.1.0] - 2026-11-06

First public release.

### Added
- Menu bar app with a global dictation shortcut (<kbd>⌃⌥D</kbd> by default), toggle or push-to-talk, <kbd>Esc</kbd> to cancel.
- On-device transcription with Whisper through WhisperKit (Core ML / Neural Engine); `large-v3-turbo` by default, `large-v3`, `small` and `base` available.
- Auto-insert at the cursor in any app (native, Chromium and Electron), with clipboard restore; pasted text is flagged as transient for clipboard managers.
- "Never use the clipboard" mode that types the text key by key.
- English interface by default, French available, switchable instantly from the settings.
- Floating recording indicator: voice-reactive waves, timer, transcribing state.
- Settings window: general, model downloads, keyboard, permissions, and a first-launch welcome guide.
- Diagnostic log at `~/Library/Logs/WisperKit/wisperkit.log`.

### Installation
- Signed DMG on GitHub Releases and a Homebrew cask (`brew tap eldriic/tap && brew trust eldriic/tap && brew install --cask wisperkit`).

[0.3.0]: https://github.com/eldriic/WisperKit/releases/tag/v0.3.0
[0.2.1]: https://github.com/eldriic/WisperKit/releases/tag/v0.2.1
[0.2.0]: https://github.com/eldriic/WisperKit/releases/tag/v0.2.0
[0.1.0]: https://github.com/eldriic/WisperKit/releases/tag/v0.1.0
