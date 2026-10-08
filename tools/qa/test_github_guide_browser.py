#!/usr/bin/env python3
"""Exercise the static GitHub guide in a browser (requires Python Playwright + Chromium)."""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import threading
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[2]


class QuietHandler(SimpleHTTPRequestHandler):
    def log_message(self, *_):
        pass


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--url', help='Verify the deployed site instead of localhost')
    parser.add_argument('--output', type=Path, default=Path('/tmp/quietcamp-guide-browser'))
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    server = None
    if args.url:
        base = args.url
    else:
        server = ThreadingHTTPServer(('127.0.0.1', 0), partial(QuietHandler, directory=str(ROOT / 'docs')))
        threading.Thread(target=server.serve_forever, daemon=True).start()
        base = f'http://127.0.0.1:{server.server_port}/'
    errors, scenarios = [], []
    try:
        with sync_playwright() as p:
            browser = p.chromium.launch(headless=True, args=['--disable-dev-shm-usage'])
            context = browser.new_context(locale='uk-UA', viewport={'width':1440,'height':1000})
            page = context.new_page()
            page.on('pageerror', lambda error: errors.append(str(error)))
            page.on('response', lambda response: errors.append(f'HTTP {response.status}: {response.url}') if response.status >= 400 else None)
            page.goto(base, wait_until='networkidle')
            page.wait_for_function('document.querySelector("[data-group-label]").textContent.length > 0')
            assert page.locator('[data-screen]:visible').count() == 36
            page.evaluate('document.querySelectorAll("img[src]").forEach(i => i.loading="eager")')
            page.wait_for_function('Array.from(document.querySelectorAll("img[src]")).every(i => i.complete && i.naturalWidth > 0)')
            scenarios.append('All 36 cards and site images load')
            for lang,title in [('en','Find a place for every guest.'),('de','Finde einen Platz für jeden Gast.'),('uk','Знайди місце для кожного гостя.')]:
                page.locator(f'[data-lang="{lang}"]').click()
                assert page.locator('html').get_attribute('lang') == lang
                assert page.locator('h1').inner_text() == title
                assert page.locator(f'[data-lang="{lang}"]').get_attribute('aria-pressed') == 'true'
            page.locator('[data-lang="de"]').click();page.reload(wait_until='networkidle')
            page.wait_for_function('document.documentElement.lang === "de"')
            scenarios.append('UA/EN/DE translation, pressed states and language persistence')
            page.locator('[data-lang="uk"]').click()
            page.locator('[data-filter="privacy"]').click()
            assert page.locator('[data-screen]:visible').count() == 5
            page.locator('#screen-search').fill('Згода')
            assert page.locator('[data-screen]:visible').count() == 1
            page.locator('#screen-search').fill('no-such-screen-xyz')
            assert page.locator('[data-screen]:visible').count() == 0
            assert page.locator('#empty-results').is_visible()
            page.locator('#screen-search').fill('')
            page.locator('[data-filter="all"]').click()
            assert page.locator('[data-screen]:visible').count() == 36
            scenarios.append('Category filters, Unicode search, empty state and reset')
            button = page.locator('[data-view="context-selected-tent"]')
            button.click();assert page.locator('#image-viewer').evaluate('(d)=>d.open')
            assert 'context-selected-tent.png' in page.locator('#viewer-image').get_attribute('src')
            assert page.locator('#viewer-metadata').get_attribute('href').endswith('context-selected-tent.json')
            page.keyboard.press('Escape');assert not page.locator('#image-viewer').evaluate('(d)=>d.open')
            button.click();page.locator('#viewer-close').click();assert not page.locator('#image-viewer').evaluate('(d)=>d.open')
            scenarios.append('Image viewer, provenance links, Escape and Close')
            for width,height in [(320,900),(390,844),(768,1024),(1280,900),(1440,1000),(1920,1080)]:
                page.set_viewport_size({'width':width,'height':height});page.goto(base+'#intro',wait_until='networkidle')
                page.wait_for_function('document.querySelector("[data-group-label]").textContent.length > 0')
                page.evaluate('window.scrollTo({top:0, behavior:"instant"})')
                page.evaluate('async () => { await document.fonts.ready; await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))); }')
                page.wait_for_timeout(200)
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'),f'Overflow at {width}'
                assert page.locator('.sidebar a[href="#screens"]').count() == 1
                assert page.locator('h1').bounding_box()['y'] < height
                if width in (390,1440):page.screenshot(path=str(args.output/f'guide-{width}.png'))
            scenarios.append('No horizontal overflow at 320, 390, 768, 1280, 1440 and 1920 pixels')
            page.set_viewport_size({'width':1440,'height':1000});page.goto(base+'#screens',wait_until='networkidle')
            page.locator('[data-filter="context"]').click();page.screenshot(path=str(args.output/'guide-panels.png'))
            page.goto(base+'images/diagrams/hud-map.svg',wait_until='networkidle');page.screenshot(path=str(args.output/'hud-diagram.png'))
            plain = browser.new_context(java_script_enabled=False,viewport={'width':390,'height':844})
            plainpage=plain.new_page();plainpage.goto(base,wait_until='networkidle')
            assert plainpage.locator('[data-screen]').count()==36 and plainpage.locator('h1').inner_text()=='Знайди місце для кожного гостя.'
            scenarios.append('Static Ukrainian content and all cards available with JavaScript disabled')
            assert not errors,errors
            browser.close()
        receipt={'result':'Passed','url':base,'scenarios':scenarios,'pageErrors':errors}
        (args.output/'browser-results.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n')
        print(json.dumps(receipt,ensure_ascii=False))
    finally:
        if server:server.shutdown();server.server_close()


if __name__=='__main__':main()
