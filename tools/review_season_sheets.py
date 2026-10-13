"""Show native Unity captures together in a local browser for visual comparison."""
import argparse
import html
import json
from pathlib import Path
from playwright.sync_api import sync_playwright
ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('--round',required=True);a=p.parse_args()
source=ROOT/'Design/LevelAuthoring/SeasonAtmosphere/Review'/a.round
output=Path(r'C:\Users\Public\qcui-20261006\QuietCamp\Temp\ai\SeasonSheets')/a.round;output.mkdir(parents=True,exist_ok=True)
presets=json.loads((source/'presets.json').read_text(encoding='utf-8'))
with sync_playwright() as engine:
 browser=engine.chromium.launch(executable_path=r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',headless=True)
 for season in ['spring','summer','autumn','winter']:
  cards=''.join(f'<article><div>{html.escape(p["label"])} · {p["id"]}</div><img src="{(source/(p["id"]+".png")).as_uri()}"></article>' for p in presets if p['season']==season)
  page=browser.new_page(viewport={'width':1536,'height':1094},device_scale_factor=1)
  document=output/(season+'.html');document.write_text('<meta charset="utf-8"><style>body{margin:0;background:#ddd;font:15px sans-serif}.grid{display:grid;grid-template-columns:repeat(3,512px)}article div{height:35px;display:flex;align-items:center;justify-content:center}img{display:block;width:512px;height:512px}</style><main class="grid">'+cards+'</main>',encoding='utf-8')
  page.goto(document.as_uri())
  page.wait_for_function('Array.from(document.images).every(i=>i.complete&&i.naturalWidth>0)')
  page.screenshot(path=str(output/(season+'.png')));page.close()
 browser.close()
print('Four browser comparison sheets from twenty unmodified native source images')
