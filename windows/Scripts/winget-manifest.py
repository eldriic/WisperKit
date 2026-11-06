#!/usr/bin/env python3
"""Génère les manifestes winget (format 1.6) d'une version de WisperKit.

Usage : winget-manifest.py <version> <sha256-x64> [<sha256-arm64>]
Sortie : windows/winget/manifests/e/eldriic/WisperKit/<version>/ — à soumettre à
microsoft/winget-pkgs (PR publique, à faire manuellement).
"""
import pathlib, sys

version, sha_x64 = sys.argv[1], sys.argv[2]
sha_arm = sys.argv[3] if len(sys.argv) > 3 else None
base = f"https://github.com/eldriic/WisperKit/releases/download/v{version}"
out = pathlib.Path(__file__).resolve().parents[1] / "winget/manifests/e/eldriic/WisperKit" / version
out.mkdir(parents=True, exist_ok=True)
pkg = "eldriic.WisperKit"
header = "# yaml-language-server: $schema=https://aka.ms/winget-manifest.{kind}.1.6.0.schema.json\n"

(out / f"{pkg}.yaml").write_text(header.format(kind="version") + f"""PackageIdentifier: {pkg}
PackageVersion: {version}
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.6.0
""")

installers = [("x64", sha_x64)] + ([("arm64", sha_arm)] if sha_arm else [])
lines = "".join(f"""- Architecture: {arch}
  InstallerUrl: {base}/WisperKit-Setup-{version}-{arch}.exe
  InstallerSha256: {sha.upper()}
""" for arch, sha in installers)
(out / f"{pkg}.installer.yaml").write_text(header.format(kind="installer") + f"""PackageIdentifier: {pkg}
PackageVersion: {version}
InstallerType: inno
Scope: user
InstallModes:
- interactive
- silent
UpgradeBehavior: install
Installers:
{lines}ManifestType: installer
ManifestVersion: 1.6.0
""")

(out / f"{pkg}.locale.en-US.yaml").write_text(header.format(kind="defaultLocale") + f"""PackageIdentifier: {pkg}
PackageVersion: {version}
PackageLocale: en-US
Publisher: eldriic
PublisherUrl: https://github.com/eldriic
PackageName: WisperKit
PackageUrl: https://github.com/eldriic/WisperKit
License: MIT
LicenseUrl: https://github.com/eldriic/WisperKit/blob/main/LICENSE
ShortDescription: Private, on-device Whisper dictation — press a shortcut, speak, and the text appears at your cursor.
Tags:
- dictation
- speech-to-text
- whisper
- voice
- offline
ReleaseNotesUrl: https://github.com/eldriic/WisperKit/releases/tag/v{version}
ManifestType: defaultLocale
ManifestVersion: 1.6.0
""")
print(out)
