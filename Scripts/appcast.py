#!/usr/bin/env python3
"""Génère dist/appcast.xml (flux Sparkle) pour une version de WisperKit.

Usage : appcast.py <version> <sortie de sign_update>
  <sortie de sign_update> : 'sparkle:edSignature="…" length="…"' (signature EdDSA du DMG)
Les notes de version viennent de la section <version> de CHANGELOG.md (Markdown simple → HTML).
L'appcast est attaché à la release : l'app le lit à releases/latest/download/appcast.xml.
"""
import datetime, html, pathlib, re, sys

version, signature = sys.argv[1], sys.argv[2]
if not re.fullmatch(r'sparkle:edSignature="[A-Za-z0-9+/=]+" length="\d+"', signature.strip()):
    sys.exit(f"signature Sparkle inattendue : {signature!r}")

root = pathlib.Path(__file__).resolve().parents[1]
changelog = (root / "CHANGELOG.md").read_text(encoding="utf-8")
section = re.search(rf"^## \[{re.escape(version)}\][^\n]*\n(.*?)(?=^## \[|\Z)", changelog, re.S | re.M)
if not section:
    sys.exit(f"CHANGELOG.md : pas de section [{version}]")


def inline(text: str) -> str:
    text = html.escape(text, quote=False)
    text = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", text)
    text = re.sub(r"`(.+?)`", r"<code>\1</code>", text)
    return re.sub(r"\[(.+?)\]\((https://[^)\s]+)\)", r'<a href="\2">\1</a>', text)


parts, in_list = [], False
for line in section.group(1).strip().splitlines():
    if line.startswith("- "):
        if not in_list:
            parts.append("<ul>")
            in_list = True
        parts.append(f"<li>{inline(line[2:])}</li>")
        continue
    if in_list:
        parts.append("</ul>")
        in_list = False
    if line.startswith("### "):
        parts.append(f"<h3>{inline(line[4:])}</h3>")
    elif line.strip():
        parts.append(f"<p>{inline(line)}</p>")
if in_list:
    parts.append("</ul>")
notes = "\n".join(parts).replace("]]>", "]]&gt;")

url = f"https://github.com/eldriic/WisperKit/releases/download/v{version}/WisperKit.dmg"
date = datetime.datetime.now(datetime.timezone.utc).strftime("%a, %d %b %Y %H:%M:%S +0000")
xml = f"""<?xml version="1.0" encoding="utf-8"?>
<rss version="2.0" xmlns:sparkle="http://www.andymatuschak.org/xml-namespaces/sparkle">
  <channel>
    <title>WisperKit</title>
    <link>https://github.com/eldriic/WisperKit</link>
    <item>
      <title>WisperKit {version}</title>
      <pubDate>{date}</pubDate>
      <sparkle:version>{version}</sparkle:version>
      <sparkle:shortVersionString>{version}</sparkle:shortVersionString>
      <sparkle:minimumSystemVersion>15.0</sparkle:minimumSystemVersion>
      <description><![CDATA[
{notes}
      ]]></description>
      <enclosure url="{url}" type="application/octet-stream" {signature.strip()} />
    </item>
  </channel>
</rss>
"""
out = root / "dist/appcast.xml"
out.parent.mkdir(exist_ok=True)
out.write_text(xml, encoding="utf-8")
print(f"→ {out.relative_to(root)}")
