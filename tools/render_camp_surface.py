"""Serial native captures in the existing Editor. No player build or new Unity launch."""
import argparse,copy,json,subprocess,sys,html
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];QA=Path('C:/Users/Public/qcui-20261006/QuietCamp')
p=argparse.ArgumentParser();p.add_argument('--round',required=True);p.add_argument('--base',type=Path,required=True);a=p.parse_args()
output=ROOT/'Design/LevelAuthoring/AircraftSeasons/GroundReview'/a.round;output.mkdir(parents=True,exist_ok=True)
base=json.loads(a.base.read_text(encoding='utf-8'));drafts=QA/'Temp/ai/GroundReview'/a.round;drafts.mkdir(parents=True,exist_ok=True)
def request(mode,**kwargs):
 args=[sys.executable,str(ROOT/'tools/request_authoring_props.py'),'--project',str(QA),'--mode',mode]
 for k,v in kwargs.items():args+=['--'+k,str(v)]
 r=subprocess.run(args,capture_output=True,text=True,encoding='utf-8')
 if r.returncode:raise RuntimeError(r.stdout+r.stderr)
 result=json.loads((QA/'Temp/ai/qc007-art-result.json').read_text(encoding='utf-8'))
 if result.get('errors') or result.get('shaderErrors'):raise RuntimeError(str(result))
 return result
captures=[]
for season,preset in [('spring','spring_sunny'),('summer','summer_day'),('autumn','autumn_golden'),('winter','winter_snowfall'),('rain','summer_rain')]:
 source=copy.deepcopy(base);source['level']['id']=source['art']['levelId']='QC_STUDY_AIRCRAFT_'+season.upper()
 source['sourceLevelId']=source['sourceArtHash']=source['sourceContentHash']=None
 source_path=drafts/(season+'.source.draft.json');source_path.write_text(json.dumps(source,ensure_ascii=False),encoding='utf-8')
 result=request('season-variant',draft=source_path,profile=preset,output=drafts/season,name=season)
 draft=Path(result['draftPath']);detail=json.loads(draft.read_text(encoding='utf-8'));detail['art']['orthographicSize']=9.6;detail['art']['cameraTarget']=[0,1.1,0]
 detail_path=drafts/(season+'.detail.draft.json');detail_path.write_text(json.dumps(detail,ensure_ascii=False),encoding='utf-8')
 if season!='rain':
  for path,name in [(draft,season),(detail_path,season+'-detail')]:request('draft-capture',draft=path,output=output,name=name);captures.append(name)
 if season in ('spring','winter','rain'):
  for profile in ['empty','used']:
   name=season+'-'+profile;request('draft-capture',draft=detail_path,profile=profile,output=output,name=name);captures.append(name)
 print(a.round,season,'captured',flush=True)
from playwright.sync_api import sync_playwright
with sync_playwright() as engine:
 browser=engine.chromium.launch(executable_path=r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',headless=True)
 for names,label in [(['spring','summer','autumn','winter'],'seasons'),(['spring-detail','summer-detail','autumn-detail','winter-detail'],'details'),(['spring-empty','spring-detail','spring-used','rain-empty','rain-used','winter-empty','winter-used','winter-detail'],'surface')]:
  page_path=drafts/(label+'.html');cards=''.join(f'<article><div>{html.escape(n)}</div><img src="{(output/(n+".png")).as_uri()}"></article>' for n in names)
  page_path.write_text('<meta charset="utf-8"><style>body{margin:0;background:#ddd;font:16px sans-serif}.grid{display:grid;grid-template-columns:repeat(2,512px)}article div{height:25px;text-align:center}img{display:block;width:512px;height:512px}</style><main class="grid">'+cards+'</main>',encoding='utf-8')
  page=browser.new_page(viewport={'width':1024,'height':537*((len(names)+1)//2)});page.goto(page_path.as_uri());page.wait_for_function('Array.from(document.images).every(i=>i.complete&&i.naturalWidth>0)');page.screenshot(path=str(drafts/(label+'.png')));page.close()
 browser.close()
print('Captured',len(captures),'native frames. Sheets:',drafts,flush=True)
