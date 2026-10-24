#!/usr/bin/env python3
"""Sync the complete reviewed static policy into Unity, without enabling services."""
import argparse
import json
import html as html_module
import re
import hashlib
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
    modes = cli.add_mutually_exclusive_group()
    modes.add_argument('--write', action='store_true', help='Update an existing draft candidate only')
    modes.add_argument('--write-published', action='store_true', help='Update an already published policy; preserve all service-review flags')
    args = cli.parse_args()
    content = json.loads((args.site_root/'legal/quiet-camp/content.json').read_text(encoding='utf8'))
    controller = json.loads((args.site_root/'legal/controller.json').read_text(encoding='utf8'))
    def markup(value):
        return re.sub(r'\{\{(legalName|publicName|ukrainianName|postalAddress)\}\}',
                      lambda match: html_module.escape(controller[match[1]], quote=True), value)
    bundle_path = RESOURCES/'privacy_content.json'
    bundle = json.loads(bundle_path.read_text(encoding='utf8'))
    legal_path = RESOURCES/'legal.json'
    legal = json.loads(legal_path.read_text(encoding='utf8'))
    draft = bundle['draft']
    assert isinstance(draft, bool) and legal['policyIsDraft'] is draft, 'Policy publication states differ'
    if args.write_published:
        assert not draft, '--write-published requires an already published policy'
    elif args.write:
        assert draft, 'Candidate sync refuses to overwrite a published policy; use --write-published explicitly'
    write = args.write or args.write_published
    revision = content['en']['revision']
    assert set(content) == {'en', 'uk', 'de'}
    for variant in bundle['document']['variants']:
        lang = variant['language']
        source = content[lang]
        assert source['revision'] == revision
        suffix = '' if lang == 'en' else '/' + lang
        html = (args.site_root/'dist/quiet-camp/privacy'/suffix.lstrip('/')/'index.html').read_text(encoding='utf8')
        # Current site uses one local progressive-enhancement script. The policy
        # must remain complete without it; reject inline or third-party scripts.
        scripts = re.findall(r'<script\b[^>]*>.*?</script>', html, flags=re.I | re.S)
        assert all(tag == '<script src="/assets/site.js" defer></script>' for tag in scripts)
        assert '{{' not in html and '\ufffd' not in html
        assert f'<link rel="canonical" href="{BASE + suffix}">' in html
        assert len(source['sections']) == 14
        paragraphs = [source['title'], source['updated'] + ': ' + source['date'] + ' · ' + revision,
                      source['status'], source['notice'], source['intro']]
        for number, section in enumerate(source['sections'], 1):
            assert f'id="{section["id"]}"' in html
            paragraphs.append(f'{number}. {section["title"]}')
            paragraphs.extend(plain(markup(p)) for p in section['paragraphs'])
            for p in section['paragraphs']:
                assert markup(p) in html, (lang, section['id'])
            if section.get('table'):
                paragraphs.append(source['tableCaption'])
                paragraphs.append(' | '.join(source['tableHead']))
                paragraphs.extend(' | '.join(row) for row in source['rows'])
        body = '\n\n'.join(paragraphs)
        assert '{{' not in body and '???' not in body
        if write:
            variant.update(revision=revision, bundledBody=body, url='' if draft else BASE + suffix, sourceUrl=BASE + suffix)
        else:
            assert variant['revision'] == revision and variant['bundledBody'] == body, lang
        summary = next(s for s in bundle['summaries'] if s['language'] == lang)
        text = source['intro'] + '\n\n' + source['sections'][1]['paragraphs'][1] + '\n\n' + source['notice']
        if write:
            summary.update(revision=revision, text=plain(markup(text)))
        else:
            assert summary['revision'] == revision and summary['text'] == plain(markup(text)), lang
    if write:
        # Synchronize text only. Publication state and service-review gates are preserved.
        bundle['draft'] = draft
        legal['revision'] = revision
        for path, data in [(bundle_path, bundle), (legal_path, legal)]:
            path.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    assert legal['revision'] == revision and bundle['draft'] is draft and legal['policyIsDraft'] is draft
    hashes = {v['language']: hashlib.sha256(v['bundledBody'].encode('utf8')).hexdigest()
              for v in bundle['document']['variants']}
    metadata = {'revision': revision,
                'gameResource': str(bundle_path.relative_to(ROOT)).replace('\\', '/'),
                'bodySha256': hashes,
                'status': ('candidate; native Android, account and operational evidence pending' if draft
                           else 'published policy text synchronized; native Android and service activation evidence remain separate')}
    metadata_path = args.site_root/'legal/bundled-policy-source.json'
    if write:
        metadata_path.write_text(json.dumps(metadata, indent=2)+'\n', encoding='utf8')
    else:
        stored = json.loads(metadata_path.read_text(encoding='utf8'))
        assert stored['revision'] == revision and stored['bodySha256'] == hashes
    print('PASS complete static policy / Unity bundle: en, uk, de, 14 sections, revision ' + revision)


if __name__ == '__main__':
    main()
