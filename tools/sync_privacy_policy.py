#!/usr/bin/env python3
"""Sync the complete reviewed static policy into Unity, without enabling services."""
import argparse
import json
from html.parser import HTMLParser
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RESOURCES = ROOT / 'QuietCamp/Assets/QuietCamp/Resources/QuietCamp'
BASE = 'https://oleksiy-havryliuk.de/quiet-camp/privacy'


class Text(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.parts = []

    def handle_data(self, text):
        self.parts.append(text)

    def handle_starttag(self, tag, attrs):
        if tag == 'br':
            self.parts.append('\n')


def plain(markup):
    parser = Text()
    parser.feed(markup)
    return ''.join(parser.parts)


def main():
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument('--site-root', type=Path, required=True)
    cli.add_argument('--write', action='store_true')
    args = cli.parse_args()
    content = json.loads((args.site_root/'legal/quiet-camp/content.json').read_text(encoding='utf8'))
    bundle_path = RESOURCES/'privacy_content.json'
    bundle = json.loads(bundle_path.read_text(encoding='utf8'))
    legal_path = RESOURCES/'legal.json'
    legal = json.loads(legal_path.read_text(encoding='utf8'))
    revision = content['en']['revision']
    assert set(content) == {'en', 'uk', 'de'}
    for variant in bundle['document']['variants']:
        lang = variant['language']
        source = content[lang]
        assert source['revision'] == revision
        suffix = '' if lang == 'en' else '/' + lang
        html = (args.site_root/'public/quiet-camp/privacy'/suffix.lstrip('/')/'index.html').read_text(encoding='utf8')
        assert '<script' not in html.lower() and '{{' not in html and '\ufffd' not in html
        assert f'<link rel="canonical" href="{BASE + suffix}">' in html
        assert len(source['sections']) == 14
        paragraphs = [source['title'], source['updated'] + ': ' + source['date'] + ' · ' + revision,
                      source['status'], source['notice'], source['intro']]
        for number, section in enumerate(source['sections'], 1):
            assert f'id="{section["id"]}"' in html
            paragraphs.append(f'{number}. {section["title"]}')
            paragraphs.extend(plain(p) for p in section['paragraphs'])
            for p in section['paragraphs']:
                assert p in html, (lang, section['id'])
            if section.get('table'):
                paragraphs.append(source['tableCaption'])
                paragraphs.append(' | '.join(source['tableHead']))
                paragraphs.extend(' | '.join(row) for row in source['rows'])
        body = '\n\n'.join(paragraphs)
        assert '{{' not in body and '???' not in body
        if args.write:
            variant.update(revision=revision, bundledBody=body, url='', sourceUrl=BASE + suffix)
        else:
            assert variant['revision'] == revision and variant['bundledBody'] == body, lang
        summary = next(s for s in bundle['summaries'] if s['language'] == lang)
        text = source['intro'] + '\n\n' + source['sections'][1]['paragraphs'][1] + '\n\n' + source['notice']
        if args.write:
            summary.update(revision=revision, text=plain(text))
        else:
            assert summary['revision'] == revision and summary['text'] == plain(text), lang
    if args.write:
        # Explicit draft remains in force until separate native/account release review.
        bundle['draft'] = True
        legal['revision'] = revision
        for path, data in [(bundle_path, bundle), (legal_path, legal)]:
            path.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    assert legal['revision'] == revision and bundle['draft'] and legal['policyIsDraft']
    print('PASS complete static policy / Unity bundle: en, uk, de, 14 sections, revision ' + revision)


if __name__ == '__main__':
    main()
