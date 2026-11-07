#!/usr/bin/env python3
"""Regenerates the release list in site/changelog.html from CHANGELOG.md.

Usage: python3 site/build.py
The HTML between <!-- changelog:start --> and <!-- changelog:end --> is replaced;
the rest of changelog.html (nav, footer) is left as it is.
"""
import datetime
import html
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parent.parent
SOURCE = ROOT / "CHANGELOG.md"
PAGE = ROOT / "site" / "changelog.html"
REPO = "https://github.com/eldriic/WisperKit"


def inline(text: str) -> str:
    """Minimal Markdown: code, bold, links; <kbd> tags pass through."""
    kbd = re.findall(r"<kbd>.*?</kbd>", text)
    for i, k in enumerate(kbd):
        text = text.replace(k, f"\x00{i}\x00", 1)
    text = html.escape(text, quote=False)
    parts = re.split(r"(`[^`]+`)", text)
    for i, part in enumerate(parts):
        if not part.startswith("`"):
            part = re.sub(r"(\w)'(\w)", "\\1\u2019\\2", part)
            parts[i] = re.sub(r'"([^"]+)"', "\u201c\\1\u201d", part)
    text = "".join(parts)
    text = re.sub(r"`([^`]+)`", r"<code>\1</code>", text)
    text = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", text)
    text = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", r'<a href="\2">\1</a>', text)
    for i, k in enumerate(kbd):
        text = text.replace(f"\x00{i}\x00", k)
    return text


def parse(md: str):
    releases, cur, section = [], None, None
    for line in md.splitlines():
        m = re.match(r"^## \[([^\]]+)\](?: - (\d{4}-\d{2}-\d{2}))?", line)
        if m:
            cur = {"version": m.group(1), "date": m.group(2), "intro": [], "sections": []}
            releases.append(cur)
            section = None
            continue
        if cur is None or re.match(r"^\[[^\]]+\]: ", line):
            continue
        m = re.match(r"^### (.+)", line)
        if m:
            section = {"name": m.group(1).strip(), "items": []}
            cur["sections"].append(section)
            continue
        m = re.match(r"^- (.+)", line)
        if m and section is not None:
            section["items"].append(m.group(1).strip())
        elif line.startswith("  ") and section and section["items"]:
            section["items"][-1] += " " + line.strip()
        elif line.strip() and section is None:
            cur["intro"].append(line.strip())
    return [r for r in releases if r["version"].lower() != "unreleased" or r["sections"]]


def title(rel) -> str:
    if rel["intro"]:
        return inline(" ".join(rel["intro"]).rstrip("."))
    bold = [b for s in rel["sections"] for i in s["items"] for b in re.findall(r"\*\*(.+?)\*\*", i)]
    if bold:
        bold = bold[:3]
        joined = bold[0] if len(bold) == 1 else ", ".join(bold[:-1]) + " and " + bold[-1].lower()
        return inline(joined)
    return f"Version {html.escape(rel['version'])}"


def render(releases) -> str:
    out = []
    for rel in releases:
        v = html.escape(rel["version"])
        when = ""
        if rel["date"]:
            d = datetime.date.fromisoformat(rel["date"])
            when = f'<time datetime="{d.isoformat()}">{d.strftime("%B")} {d.day}, {d.year}</time>'
        body = []
        for s in rel["sections"]:
            items = "\n".join(f"            <li>{inline(i)}</li>" for i in s["items"])
            body.append(f"          <h3>{html.escape(s['name'])}</h3>\n          <ul>\n{items}\n          </ul>")
        out.append(
            f'      <article class="release" id="v{v}">\n'
            f'        <div class="release__meta">{when}<a class="release__tag" href="{REPO}/releases/tag/v{v}">{v}</a></div>\n'
            f'        <div class="release__body">\n'
            f'          <h2>{title(rel)}</h2>\n' + "\n".join(body) + "\n"
            f"        </div>\n      </article>"
        )
    return "\n".join(out)


def main():
    page = PAGE.read_text(encoding="utf-8")
    block = render(parse(SOURCE.read_text(encoding="utf-8")))
    new, n = re.subn(
        r"(<!-- changelog:start -->).*?(<!-- changelog:end -->)",
        lambda m: m.group(1) + "\n" + block + "\n" + m.group(2),
        page,
        flags=re.S,
    )
    if n != 1:
        raise SystemExit("changelog markers not found in site/changelog.html")
    PAGE.write_text(new, encoding="utf-8")
    print(f"changelog.html: {len(parse(SOURCE.read_text(encoding='utf-8')))} release(s)")


if __name__ == "__main__":
    main()
