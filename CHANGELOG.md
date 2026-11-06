# Changelog

All notable changes to WisperKit are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

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

[0.1.0]: https://github.com/eldriic/WisperKit/releases/tag/v0.1.0
