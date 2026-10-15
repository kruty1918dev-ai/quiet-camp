"""Serial native authoring/review of the remaining canonical campaign places."""
import argparse,json,subprocess,sys,html,os,ctypes,hashlib,uuid
from pathlib import Path
sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parents[1];QA=Path('C:/Users/Public/qcui-20261006/QuietCamp');DEST=ROOT/'Design/LevelAuthoring/StoryCampaign'
p=argparse.ArgumentParser();p.add_argument('--round',required=True);p.add_argument('--no-sheets',action='store_true');p.add_argument('--compose',action='store_true');p.add_argument('--start',type=int,default=11);p.add_argument('--end',type=int,default=100);a=p.parse_args()
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
   # AGENTS requires postponement OR cleanup before low-memory work. The Editor
   # cleanup above is the Windows equivalent; keep requests serial and resumable.
   if memory.available//1048576<750:raise RuntimeError('Paused after cleanup: less than 750 MiB is available for a single native request. Existing files are resumable.')
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
 details=drafts/(name+'.details.json');placement=output/(name+'.pwb.json')
 if details.exists() and not placement.exists():
  native=path.with_name(name+'.'+uuid.uuid4().hex+'.draft.json')
  proof=request('opening-details',profile=details,draft=path,output=native)
  frozen=json.loads(native.read_text(encoding='utf-8'))
  native.write_text(json.dumps(frozen,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8')
  del frozen
  os.replace(native,path)
  placement.write_text(json.dumps(proof,ensure_ascii=False,indent=2),encoding='utf-8')
 png=output/(name+'.png');receipt=output/(name+'.json');valid=False
 if png.exists() and receipt.exists():
  valid=hashlib.sha256(png.read_bytes()).hexdigest()==json.loads(receipt.read_text(encoding='utf-8')).get('imageSha256')
 if not valid:request('draft-capture',draft=path,output=output,name=name)
 print(a.round,name,by_order[i]['story']['title'],flush=True)
if not a.no_sheets:
 from story_campaign_review import sheets
 sheets(a.round)
