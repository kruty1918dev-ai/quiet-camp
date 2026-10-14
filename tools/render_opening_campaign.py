"""Serial editable composition/camera evidence in the existing Editor only."""
import argparse,json,subprocess,sys,html
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
QA=Path('C:/Users/Public/qcui-20261006/QuietCamp')
DEST=ROOT/'Design/LevelAuthoring/OpeningCampaign'
p=argparse.ArgumentParser();p.add_argument('--round',required=True);p.add_argument('--compose',action='store_true');p.add_argument('--start',type=int,default=1);p.add_argument('--end',type=int,default=10);a=p.parse_args()
drafts=QA/'Temp/ai/OpeningCampaign'/a.round;drafts.mkdir(parents=True,exist_ok=True)
output=DEST/'Review'/a.round;output.mkdir(parents=True,exist_ok=True)
profiles=json.loads((DEST/'profiles.json').read_text(encoding='utf-8'))
def request(mode,**kwargs):
 args=[sys.executable,str(ROOT/'tools/request_authoring_props.py'),'--project',str(QA),'--mode',mode]
 for k,v in kwargs.items():args+=['--'+k,str(v)]
 r=subprocess.run(args,capture_output=True,text=True,encoding='utf-8')
 if r.returncode:raise RuntimeError(r.stdout+r.stderr)
 proof=json.loads((QA/'Temp/ai/qc007-art-result.json').read_text(encoding='utf-8'))
 if proof.get('errors') or proof.get('shaderErrors'):raise RuntimeError(str(proof))
 return proof
for i in range(a.start,a.end+1):
 ident=f'QC{i:03}';path=drafts/(ident+'.draft.json')
 if a.compose:
  profile=drafts/(ident+'.profile.json');profile.write_text(json.dumps(profiles[i-1],ensure_ascii=False),encoding='utf-8')
  proof=request('opening-compose',profile=profile,draft=path)
  (output/(ident+'.pwb.json')).write_text(json.dumps(proof,ensure_ascii=False,indent=2),encoding='utf-8')
 proof=request('draft-capture',draft=path,output=output,name=ident)
 print(a.round,ident,'native capture; draft preserved',flush=True)
from playwright.sync_api import sync_playwright
with sync_playwright() as engine:
 browser=engine.chromium.launch(channel='msedge',headless=True)
 for group in [range(1,6),range(6,11)]:
  label='01-05' if group.start==1 else '06-10'
  page_path=drafts/(label+'.html')
  page_path.write_text('<meta charset="utf-8"><style>body{margin:0;background:#203028;display:grid;grid-template-columns:1fr 1fr;color:white;font:16px sans-serif}figure{margin:0}img{width:512px;display:block}figcaption{padding:4px}</style>'+''.join('<figure><figcaption>'+html.escape(profiles[i-1]['story']['title'])+'</figcaption><img src="'+(output/f'QC{i:03}.png').as_uri()+'"></figure>' for i in group if (output/f'QC{i:03}.png').exists()),encoding='utf-8')
  page=browser.new_page(viewport={'width':1024,'height':1620});page.goto(page_path.as_uri());page.wait_for_function('Array.from(document.images).every(i=>i.complete&&i.naturalWidth>0)');page.screenshot(path=str(drafts/(label+'.png')));page.close()
 browser.close()
print('Sheets:',drafts,flush=True)
