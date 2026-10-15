"""Play the published opening or full campaign through Check/Next, in memory only."""
import argparse,hashlib,json,os,subprocess,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];QA=Path('C:/Users/Public/qcui-20261006/QuietCamp');OUT=ROOT/'Design/LevelAuthoring/OpeningCampaign';VERIFY=OUT/'Verification';GAME=OUT/'Game';GAME.mkdir(parents=True,exist_ok=True)
p=argparse.ArgumentParser();p.add_argument('--count',type=int,choices=[10,100],default=10);a=p.parse_args()
if a.count==100:
 OUT=ROOT/'Design/LevelAuthoring/StoryCampaign';VERIFY=OUT/'Verification';GAME=OUT/'Game';GAME.mkdir(parents=True,exist_ok=True)
ids=json.loads((QA/'Assets/QuietCamp/Resources/QuietCamp/campaign.json').read_text(encoding='utf-8'))['campaignLevelIds'][:a.count]
assert len(ids)==a.count and len(set(ids))==a.count
def request(mode,**values):
 args=[sys.executable,str(ROOT/'tools/request_authoring_props.py'),'--project',str(QA),'--mode',mode]
 for key,value in values.items():args+=['--'+key,str(value)]
 r=subprocess.run(args,capture_output=True,text=True,encoding='utf-8')
 if r.returncode:raise RuntimeError(r.stdout+r.stderr)
 return {} if mode=='authoring-stop' else json.loads((QA/'Temp/ai/qc007-art-result.json').read_text(encoding='utf-8'))
def saved_files():
 base=Path(os.environ['USERPROFILE'])/'AppData/LocalLow/kruty1918';result={}
 for name in ['Quiet Camp','Quiet Camp QA']:
  folder=base/name
  if folder.exists():
   for p in folder.rglob('*'):
    if p.is_file() and p.suffix in ['.mvs','.bak','.txt','.json']:result[str(p)]=hashlib.sha256(p.read_bytes()).hexdigest()
 return result
before=saved_files();workspace=request('authoring-workspace-status');proofs=[]
def ready(ident):
 limit=time.monotonic()+180
 while time.monotonic()<limit:
  status=request('opening-gameplay',name='status')
  if status.get('status')=='opening-game-ready' and status.get('id')==ident:return status
  time.sleep(.4)
 raise RuntimeError('Camp did not become ready: '+ident)
def frame(ident):
 path=GAME/(ident.replace(':','_')+'.png');start=time.time();request('opening-gameplay',name='frame',output=path)
 limit=time.monotonic()+30
 while time.monotonic()<limit:
  if path.exists() and path.stat().st_mtime>=start and path.stat().st_size>1000:return dict(path=path.relative_to(OUT).as_posix(),sha256=hashlib.sha256(path.read_bytes()).hexdigest())
  time.sleep(.2)
 raise RuntimeError('Gameplay screenshot was not written: '+ident)
try:
 request('opening-gameplay',name='start')
 for i,ident in enumerate(ids,1):
  status=ready(ident)
  if a.count==100 and i==1:
   entitlement=request('opening-gameplay',name='pro');assert entitlement['pro'] and entitlement['memoryOnly'],entitlement
  request('authoring-focus-game');status=request('opening-gameplay',name='status')
  assert status['memoryOnly'] and status['frozenArtMatches'] and status['story'],status
  assert status['surfaceSteps']==0,'Camp starts with invented traffic'
  request('authoring-place-witness');time.sleep(.7)
  screenshot=frame(ident);used=request('opening-gameplay',name='status')
  result=request('opening-gameplay',name='check')
  assert result['solved'] and result['recorded'] and result['memoryOnly'],result
  assert set(result['completed'])==set(ids[:i]),result
  proofs.append(dict(id=ident,ready=status,used=used,completion=result,screenshot=screenshot))
  (VERIFY/'gameplay.json').write_text(json.dumps(dict(status='running',levels=proofs),ensure_ascii=False,indent=2),encoding='utf-8')
  if i<a.count:
   assert result['next']==ids[i],result
   route=request('opening-gameplay',name='next');assert route['to']==ids[i],route;proofs[-1]['transition']=route
  elif a.count==100:assert result['next'] is None,'Campaign must end after level 100.'
  print('Completed canonical campaign level:',ident,flush=True)
finally:
 status=request('authoring-status')
 if status.get('playing'):request('authoring-stop')
 limit=time.monotonic()+120
 while time.monotonic()<limit:
  if not request('authoring-status').get('playing'):break
  time.sleep(.4)
restored=request('authoring-workspace-status');after=saved_files()
assert before==after,'Saved progress changed'
assert workspace['artSha256']==restored['artSha256'] and workspace['objects']==restored['objects'],'Manual workspace changed'
(VERIFY/'gameplay.json').write_text(json.dumps(dict(status='passed',levels=proofs,canonicalCheckAndNext=True,memoryOnly=True,progressFilesUnchanged=before==after,manualDraftUnchanged=True,workspaceObjects=restored['objects'],testKind='automated witness placement and actual gameplay action routing; not a human usability test'),ensure_ascii=False,indent=2),encoding='utf-8')
(VERIFY/'workspace-after.json').write_text(json.dumps(restored,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'{a.count} completions, {a.count-1} transitions; persisted progress and manual draft unchanged.',flush=True)
