"""Serial native authoring/review of the remaining canonical campaign places."""
import argparse,json,subprocess,sys,html,os,ctypes
from pathlib import Path
sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parents[1];QA=Path('C:/Users/Public/qcui-20261006/QuietCamp');DEST=ROOT/'Design/LevelAuthoring/StoryCampaign'
p=argparse.ArgumentParser();p.add_argument('--round',required=True);p.add_argument('--compose',action='store_true');p.add_argument('--start',type=int,default=11);p.add_argument('--end',type=int,default=100);a=p.parse_args()
drafts=QA/'Temp/ai/StoryCampaign'/a.round;drafts.mkdir(parents=True,exist_ok=True);output=DEST/'Review'/a.round;output.mkdir(parents=True,exist_ok=True)
profiles=json.loads((DEST/'unique-profiles.json').read_text(encoding='utf-8'));by_order={i+1:v for i,v in enumerate(profiles)}
def request(mode,**kwargs):
 if os.name=='nt':
  class Memory(ctypes.Structure):
   _fields_=[('length',ctypes.c_ulong),('load',ctypes.c_ulong)]+[(n,ctypes.c_ulonglong) for n in ['total','available','pageTotal','pageAvailable','virtualTotal','virtualAvailable','extended']]
  memory=Memory();memory.length=ctypes.sizeof(memory);ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(memory))
  if memory.available//1048576<1500:
   cleanup=subprocess.run([sys.executable,str(ROOT/'tools/request_authoring_props.py'),'--project',str(QA),'--mode','authoring-trim'],capture_output=True,text=True,encoding='utf-8')
   if cleanup.returncode:raise RuntimeError(cleanup.stdout+cleanup.stderr)
   ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(memory))
   if memory.available//1048576<1500:raise RuntimeError('Paused after cleanup: available RAM is below 1500 MiB. Existing files are resumable.')
 args=[sys.executable,str(ROOT/'tools/request_authoring_props.py'),'--project',str(QA),'--mode',mode]
 for k,v in kwargs.items():args+=['--'+k,str(v)]
 r=subprocess.run(args,capture_output=True,text=True,encoding='utf-8')
 if r.returncode:raise RuntimeError(r.stdout+r.stderr)
 result=json.loads((QA/'Temp/ai/qc007-art-result.json').read_text(encoding='utf-8'))
 if result.get('errors') or result.get('shaderErrors'):raise RuntimeError(str(result))
 return result
for i in range(a.start,a.end+1):
 name=f'L{i:03}';path=drafts/(name+'.draft.json')
 if a.compose and not path.exists():
  profile=drafts/(name+'.profile.json');profile.write_text(json.dumps(by_order[i],ensure_ascii=False),encoding='utf-8')
  proof=request('opening-compose',profile=profile,draft=path);(output/(name+'.pwb.json')).write_text(json.dumps(proof,ensure_ascii=False,indent=2),encoding='utf-8')
 if not (output/(name+'.png')).exists():request('draft-capture',draft=path,output=output,name=name)
 print(a.round,name,by_order[i]['story']['title'],flush=True)
from playwright.sync_api import sync_playwright
with sync_playwright() as engine:
 browser=engine.chromium.launch(channel='msedge',headless=True)
 for begin in range(1,101,10):
  indices=[i for i in range(begin,min(101,begin+10)) if (output/f'L{i:03}.png').exists()]
  if not indices:continue
  page_path=drafts/f'{begin:03}-{begin+9:03}.html'
  page_path.write_text('<meta charset="utf-8"><style>body{margin:0;background:#203028;display:grid;grid-template-columns:repeat(3,384px);color:white;font:14px sans-serif}figure{margin:0}img{width:384px;display:block}figcaption{padding:4px;height:32px}</style>'+''.join('<figure><figcaption>'+str(i)+' · '+html.escape(by_order[i]['story']['title'])+'</figcaption><img src="'+(output/f'L{i:03}.png').as_uri()+'"></figure>' for i in indices),encoding='utf-8')
  page=browser.new_page(viewport={'width':1152,'height':424*((len(indices)+2)//3)});page.goto(page_path.as_uri());page.wait_for_function('Array.from(document.images).every(i=>i.complete&&i.naturalWidth>0)');page.screenshot(path=str(drafts/f'{begin:03}-{begin+9:03}.png'));page.close()
 browser.close()
print('Review sheets:',drafts,flush=True)
