"""Four editable seasons of one aircraft campsite; serial requests to the existing Unity Editor."""
import argparse
import copy
import html
import json
from pathlib import Path
import subprocess
import sys

sys.stdout.reconfigure(encoding='utf-8')
ROOT = Path(__file__).resolve().parents[1]
QA = Path(r'C:\Users\Public\qcui-20261006\QuietCamp')
DESIGN = ROOT / 'Design/LevelAuthoring/AircraftSeasons'
SEASONS = [('spring', 'Весняний ліс', 'spring_sunny'), ('summer', 'Літній ліс', 'summer_day'),
           ('autumn', 'Осінній ліс', 'autumn_golden'), ('winter', 'Зимовий ліс', 'winter_snowfall')]
p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--round', required=True)
p.add_argument('--base', type=Path, required=True)
p.add_argument('--sheets', action='store_true')
p.add_argument('--details', action='store_true')
p.add_argument('--review-folder', default='Review')
a = p.parse_args()
review = DESIGN / a.review_folder / a.round
review.mkdir(parents=True, exist_ok=True)
base = json.loads(a.base.read_text(encoding='utf-8'))
(review / 'base.draft.json').write_text(json.dumps(base, ensure_ascii=False), encoding='utf-8')

def request(mode, **kwargs):
    args = [sys.executable, str(ROOT/'tools/request_authoring_props.py'), '--project', str(QA), '--mode', mode]
    for k, v in kwargs.items(): args += ['--'+k, str(v)]
    run = subprocess.run(args, capture_output=True, text=True, encoding='utf-8')
    if run.returncode: raise RuntimeError(run.stdout+run.stderr)
    result = json.loads((QA/'Temp/ai/qc007-art-result.json').read_text(encoding='utf-8'))
    if result.get('errors') or result.get('shaderErrors'): raise RuntimeError(str(result))
    return result

for season, label, preset in SEASONS:
    source = copy.deepcopy(base)
    source['level']['id'] = source['art']['levelId'] = 'QC_STUDY_AIRCRAFT_'+season.upper()
    source['title'] = 'Табір між уламками — '+label
    source['sourceLevelId'] = source['sourceArtHash'] = source['sourceContentHash'] = None
    source_path = QA/'Temp/ai/AircraftSeasons'/a.round/(season+'.source.draft.json')
    source_path.parent.mkdir(parents=True, exist_ok=True)
    source_path.write_text(json.dumps(source, ensure_ascii=False), encoding='utf-8')
    result = request('season-variant', draft=source_path, profile=preset, output=review/season, name=season)
    request('draft-capture', draft=result['draftPath'], output=review, name=season)
    if a.details:
        detail=json.loads(Path(result['draftPath']).read_text(encoding='utf-8'))
        detail['art']['orthographicSize']=9.6
        detail['art']['cameraTarget']=[0,1.1,0]
        detail_path=QA/'Temp/ai/AircraftSeasons'/a.round/(season+'.detail.draft.json')
        detail_path.write_text(json.dumps(detail,ensure_ascii=False),encoding='utf-8')
        request('draft-capture',draft=detail_path,output=review,name=season+'-detail')
    print(a.round, season, 'captured', flush=True)

if a.sheets:
    from playwright.sync_api import sync_playwright
    output = QA/'Temp/ai/AircraftSheets'/a.round
    output.mkdir(parents=True, exist_ok=True)
    page_path = output/'index.html'
    with sync_playwright() as engine:
        browser = engine.chromium.launch(executable_path=r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe', headless=True)
        for suffix in (['','-detail'] if a.details else ['']):
            cards = ''.join(f'<article><div>{html.escape(label)}</div><img src="{(review/(season+suffix+".png")).as_uri()}"></article>' for season,label,_ in SEASONS)
            page_path = output/('index'+suffix+'.html')
            page_path.write_text('<meta charset="utf-8"><style>body{margin:0;background:#ddd;font:18px sans-serif}.grid{display:grid;grid-template-columns:repeat(2,512px)}article div{height:32px;text-align:center}img{display:block;width:512px;height:512px}</style><main class="grid">'+cards+'</main>', encoding='utf-8')
            page = browser.new_page(viewport={'width':1024,'height':1088})
            page.goto(page_path.as_uri())
            page.wait_for_function('Array.from(document.images).every(i=>i.complete&&i.naturalWidth>0)')
            page.screenshot(path=str(output/('comparison'+suffix+'.png')));page.close()
        browser.close()
    print('Comparison:', output/'comparison.png')
