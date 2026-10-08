#!/usr/bin/env python3
"""Check the published guide's links, images, capture receipts and locale coverage."""
from html.parser import HTMLParser
from pathlib import Path
import hashlib
import json
import re
import struct
import sys
from urllib.parse import unquote, urlsplit
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
GUIDES = [ROOT / 'README.md', ROOT / 'CAMPAIGN_ROADMAP.md', ROOT / 'Design/README.md',
          ROOT / 'PERFORMANCE_MAP.md', ROOT / 'tools/qa/PERFORMANCE-AUDIT-UA.md',
          ROOT / 'Documentation/README.md', ROOT / 'QuietCamp/Packages/README.md',
          ROOT / 'tools/qa/DOCS-CAPTURE-UA.md', *sorted((ROOT / 'docs').glob('*.md'))]
ERRORS = []


def slug(text):
    text = re.sub(r'\[([^\]]+)\]\([^)]*\)', r'\1', text)
    text = re.sub(r'<[^>]*>', '', text).replace('`', '').lower()
    return ''.join(c for c in text if c.isalnum() or c in ' _-').replace(' ', '-')


def markdown_anchors(path):
    used, anchors = {}, set()
    for line in path.read_text(encoding='utf-8').splitlines():
        m = re.match(r'^#{1,6}\s+(.+?)\s*#*$', line)
        if not m:
            continue
        key = slug(m.group(1))
        count = used.get(key, 0)
        used[key] = count + 1
        anchors.add(key if not count else f'{key}-{count}')
    return anchors


class Page(HTMLParser):
    def __init__(self):
        super().__init__()
        self.links, self.ids, self.locale_keys, self.screens = [], set(), set(), set()
        self.missing_alt = 0

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        for key in ('src', 'href'):
            if key in attrs:
                self.links.append(attrs[key])
        if 'id' in attrs:
            self.ids.add(attrs['id'])
        if 'data-i18n' in attrs:
            self.locale_keys.add(attrs['data-i18n'])
        if 'data-screen' in attrs:
            self.screens.add(attrs['data-screen'])
        if tag == 'img' and 'alt' not in attrs:
            self.missing_alt += 1


def check_link(origin, href):
    parts = urlsplit(href)
    if parts.scheme or parts.netloc:
        return
    target = (origin.parent / unquote(parts.path)).resolve() if parts.path else origin
    if not target.is_relative_to(ROOT):
        ERRORS.append(f'{origin.relative_to(ROOT)}: outside repository: {href}')
    elif not target.exists():
        ERRORS.append(f'{origin.relative_to(ROOT)}: missing {href}')
    elif parts.fragment:
        if target.suffix == '.md':
            anchors = markdown_anchors(target)
        elif target.suffix == '.html':
            page = Page(); page.feed(target.read_text(encoding='utf-8')); anchors = page.ids
        else:
            return
        if unquote(parts.fragment) not in anchors:
            ERRORS.append(f'{origin.relative_to(ROOT)}: missing anchor {href}')


def main():
    links = 0
    for path in GUIDES:
        content = path.read_text(encoding='utf-8')
        page = Page(); page.feed(content)
        hrefs = re.findall(r'!?\[[^\]]*\]\(([^\s)]+)(?:\s+"[^"]*")?\)', content) + page.links
        for href in hrefs:
            check_link(path, href); links += 1
    html = ROOT / 'docs/index.html'
    page = Page(); page.feed(html.read_text(encoding='utf-8'))
    for link in page.links:
        check_link(html, link); links += 1
    audit_html = ROOT / 'docs/performance.html'
    audit_page = Page(); audit_page.feed(audit_html.read_text(encoding='utf-8'))
    for link in audit_page.links:
        check_link(audit_html, link); links += 1
    if audit_page.missing_alt:
        ERRORS.append('Performance plots need alt attributes')
    if page.missing_alt:
        ERRORS.append('Site images need alt attributes')
    records = json.loads((ROOT / 'docs/site-content.json').read_text(encoding='utf-8'))
    locales = json.loads((ROOT / 'docs/site-i18n.json').read_text(encoding='utf-8'))
    if len({r['id'] for r in records}) != len(records) or page.screens != {r['id'] for r in records}:
        ERRORS.append('Site cards/content IDs differ or repeat')
    for lang in ('uk', 'en', 'de'):
        if not page.locale_keys <= locales.get(lang, {}).keys():
            ERRORS.append(f'Missing site translations for {lang}')
        for item in records:
            if not item['title'].get(lang) or not item['description'].get(lang):
                ERRORS.append(f'Missing card translation: {item["id"]}/{lang}')
            check_link(html, f'images/captures/2026-10-08/{item["id"]}.png')
    captures = ROOT / 'docs/images/captures/2026-10-08'
    manifest = json.loads((captures / 'manifest.json').read_text(encoding='utf-8'))
    listed = {entry['png'] for entry in manifest['images']}
    if len(listed) != manifest['count'] or listed != {p.name for p in captures.glob('*.png')}:
        ERRORS.append('Capture manifest coverage mismatch')
    for entry in manifest['images']:
        data = (captures / entry['png']).read_bytes()
        meta = json.loads((captures / entry['metadata']).read_text(encoding='utf-8'))
        dimensions = struct.unpack('>II', data[16:24])
        if data[:8] != b'\x89PNG\r\n\x1a\n' or hashlib.sha256(data).hexdigest() != entry['sha256']:
            ERRORS.append(f'Invalid PNG/hash: {entry["png"]}')
        if dimensions != (entry['width'], entry['height']) or dimensions != (meta['width'], meta['height']):
            ERRORS.append(f'Dimensions differ: {entry["png"]}')
        if meta['kind'] != 'native-unity-game-view' or not meta['syntheticProgress'] or 'DocsQA' not in meta['qaProduct']:
            ERRORS.append(f'Capture identity/provenance missing: {entry["png"]}')
    receipt = json.loads((captures / 'verification.json').read_text(encoding='utf-8'))
    if not all(r['result'] == 'Passed' for r in receipt['runs']) or not all(receipt['restorationChecks'].values()):
        ERRORS.append('Native capture/restoration receipt has a failed check')
    for svg in (ROOT / 'docs/images/diagrams').glob('*.svg'):
        root = ET.parse(svg).getroot()
        if root.find('{http://www.w3.org/2000/svg}title') is None:
            ERRORS.append(f'SVG lacks accessible title: {svg.name}')
    if ERRORS:
        print('\n'.join(ERRORS), file=sys.stderr)
        return 1
    print(f'PASS: {links} guide/site links; {len(records)} cards in 3 languages; {manifest["count"]} native PNG hashes/sidecars; capture and restoration receipts.')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
