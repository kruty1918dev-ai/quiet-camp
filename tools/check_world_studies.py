"""Check offline gallery, reviewed geometry hashes and the five playable exports."""
import argparse
import hashlib
from html.parser import HTMLParser
import json
from pathlib import Path
import zipfile

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'Design/LevelAuthoring/WorldImprovement'
GUIDE=BASE/'Levels/index.html'
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--browser',type=Path);a=p.parse_args()
class References(HTMLParser):
 def handle_starttag(self,tag,attrs):
  for k,v in attrs:
   if k in ('src','href','data-image'):
    assert '://' not in v,'Network dependency: '+v
    assert (GUIDE.parent/v).is_file(),'Missing local file: '+v
References().feed(GUIDE.read_text(encoding='utf-8'))
ids=[]
for folder in GUIDE.parent.iterdir():
 if not folder.is_dir():continue
 draft=next(folder.glob('*.draft.json'));d=json.loads(draft.read_text(encoding='utf-8'));ids.append(d['level']['id'])
 level=json.loads(next(folder.glob('*.level.json')).read_text(encoding='utf-8'));assert level==d['level']
 art=next(folder.glob('*.art.json'));assert json.loads(art.read_text(encoding='utf-8'))==d['art']
 capture=json.loads((folder/'capture.json').read_text(encoding='utf-8'))
 assert hashlib.sha256(art.read_bytes()).hexdigest()==capture['artSha256']
 assert hashlib.sha256((folder/'render.png').read_bytes()).hexdigest()==capture['imageSha256']
 play=json.loads((BASE/'Verification'/f'{folder.name}-play.json').read_text(encoding='utf-8'))
 assert play['play']['identical'] and play['play']['memoryOnly'] and play['completion']['solved']
assert len(ids)==len(set(ids))==5
assert json.loads((BASE/'Verification/progress-preservation.json').read_text(encoding='utf-8'))['unchanged']
with zipfile.ZipFile(BASE/'quietcamp-five-levels.zip') as archive:
 assert archive.testzip() is None
 assert len([n for n in archive.namelist() if n.endswith('.draft.json')])==5
 assert 'Levels/aircraft/ATTRIBUTION.md' in archive.namelist()
print('PASS five distinct exports, native image/composition hashes, game proofs, progress, ZIP and offline links')
if a.browser:
 from playwright.sync_api import sync_playwright
 with sync_playwright() as engine:
  browser=engine.chromium.launch(executable_path=str(a.browser),headless=True)
  page=browser.new_page(viewport={'width':1280,'height':900});errors=[];network=[]
  page.on('pageerror',lambda error:errors.append(str(error)))
  page.on('request',lambda req:network.append(req.url) if req.url.startswith(('http:','https:')) else None)
  page.goto(GUIDE.as_uri());page.wait_for_load_state('networkidle')
  assert page.locator('article').count()==5
  assert page.locator('article img').evaluate_all('(imgs)=>imgs.every(i=>i.complete&&i.naturalWidth===1536)')
  for card in page.locator('[data-image]').all():
   card.click();assert page.locator('dialog').evaluate('(d)=>d.open');page.keyboard.press('Escape')
  page.set_viewport_size({'width':390,'height':844})
  assert page.evaluate('document.documentElement.scrollWidth<=window.innerWidth')
  assert not errors and not network,(errors,network)
  browser.close()
 print('PASS browser: five native images, zoom/Escape, mobile layout, no JS errors or network requests')
