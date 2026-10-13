"""Check the offline QuietCamp tutorial and optionally exercise it in Chromium."""
import argparse
from html.parser import HTMLParser
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
GUIDE = ROOT / 'Design/LevelAuthoring/index.html'


class References(HTMLParser):
    def __init__(self):
        super().__init__(); self.paths = []; self.ids = set(); self.anchors = []

    def handle_starttag(self, tag, attributes):
        attrs = dict(attributes)
        if 'id' in attrs:
            assert attrs['id'] not in self.ids, 'Duplicate ID: ' + attrs['id']
            self.ids.add(attrs['id'])
        for key in ['src', 'href']:
            value = attrs.get(key, '')
            if value.startswith('#'):
                self.anchors.append(value[1:])
            elif value:
                assert '://' not in value and not value.startswith('//'), 'Network dependency: ' + value
                self.paths.append(value)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--browser', type=Path, help='Chromium/Edge executable; requires playwright')
    args = parser.parse_args()
    refs = References(); refs.feed(GUIDE.read_text(encoding='utf-8'))
    for value in refs.paths:
        assert (GUIDE.parent / value).is_file(), 'Missing local file: ' + value
    assert all(anchor in refs.ids for anchor in refs.anchors), 'Broken section link'
    print(f'PASS offline references: {len(refs.paths)} local files, {len(refs.anchors)} section links')
    if not args.browser:
        return
    from playwright.sync_api import sync_playwright
    with sync_playwright() as engine:
        browser = engine.chromium.launch(executable_path=str(args.browser), headless=True)
        page = browser.new_page(viewport={'width': 1280, 'height': 900})
        errors = []; network = []
        page.on('pageerror', lambda error: errors.append(str(error)))
        page.on('request', lambda request: network.append(request.url) if request.url.startswith(('http:', 'https:')) else None)
        page.goto(GUIDE.as_uri()); page.wait_for_load_state('load')
        page.locator('#search').fill('\u0440\u0456\u0447\u043a\u0430')
        assert page.locator('#river').is_visible()
        page.locator('#search').fill('zzzzzz'); assert page.locator('[data-search]:visible').count() == 0
        page.locator('#search').fill(''); assert page.locator('[data-search]:visible').count() == page.locator('[data-search]').count()
        for details in page.locator('details').all():
            details.evaluate('(element) => element.open = true')
        for img in page.locator('img[data-zoom]').all():
            img.scroll_into_view_if_needed()
            img.evaluate('(element) => { element.loading = "eager"; }')
            assert img.evaluate('(element) => element.complete && element.naturalWidth > 200'), 'Unreadable tutorial image'
        image = page.locator('img[data-zoom]').first
        image.focus(); page.keyboard.press('Enter'); assert page.locator('#zoom').evaluate('(element) => element.open')
        page.keyboard.press('Escape'); assert not page.locator('#zoom').evaluate('(element) => element.open')
        page.set_viewport_size({'width': 390, 'height': 844})
        assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), 'Mobile horizontal overflow'
        page.emulate_media(media='print'); assert page.locator('main').is_visible()
        assert not errors, errors
        assert not network, network
        browser.close()
    print('PASS browser: search, all images, keyboard zoom, mobile width, print, no JS errors or network requests')


if __name__ == '__main__':
    main()
