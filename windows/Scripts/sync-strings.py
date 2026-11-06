#!/usr/bin/env python3
"""Génère windows/src/WisperKit.Core/Localization/fr.json.

Source unique des traductions : App/WisperKit/fr.lproj/Localizable.strings (version macOS),
complétée par les textes propres à Windows dans Localization/fr.windows.strings.
Usage : python3 windows/Scripts/sync-strings.py
"""
import json, pathlib, re

root = pathlib.Path(__file__).resolve().parents[2]
sources = [
    root / "App/WisperKit/fr.lproj/Localizable.strings",
    root / "windows/src/WisperKit.Core/Localization/fr.windows.strings",
]
entry = re.compile(r'^\s*"((?:[^"\\]|\\.)*)"\s*=\s*"((?:[^"\\]|\\.)*)"\s*;\s*$')
unescape = lambda s: s.replace('\\"', '"').replace("\\\\", "\\")

table = {}
for src in sources:
    for line in src.read_text(encoding="utf-8").splitlines():
        m = entry.match(line)
        if m:
            table[unescape(m.group(1))] = unescape(m.group(2))

out = root / "windows/src/WisperKit.Core/Localization/fr.json"
out.write_text(json.dumps(dict(sorted(table.items(), key=lambda kv: kv[0].lower())), ensure_ascii=False, indent=2) + "\n",
               encoding="utf-8")
print(f"{out.relative_to(root)} : {len(table)} entrées")
