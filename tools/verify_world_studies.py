"""Export reviewed drafts and verify each in the existing Editor's normal game."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import time
sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--project',type=Path,required=True)
p.add_argument('--export-only',action='store_true')
a=p.parse_args()
BASE=ROOT/'Design/LevelAuthoring/WorldImprovement'
PROOF=BASE/'Verification';PROOF.mkdir(parents=True,exist_ok=True)
profiles=json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Authoring/WorldStudies/profiles.json').read_text(encoding='utf-8'))
def request(mode,**kwargs):
 args=[sys.executable,str(ROOT/'tools/request_authoring_props.py'),'--project',str(a.project),'--mode',mode]
 for k,v in kwargs.items():args.extend(['--'+k,str(v)])
 result=subprocess.run(args,capture_output=True,text=True,encoding='utf-8')
 if result.returncode:raise RuntimeError(result.stdout+result.stderr)
 receipt=json.loads((a.project/'Temp/ai/qc007-art-result.json').read_text(encoding='utf-8')) if mode!='authoring-stop' else {'status':'stop-request-consumed'}
 if receipt.get('status')=='error':raise RuntimeError(receipt.get('error'))
 return receipt
def wait_ready(mode,**kwargs):
 for _ in range(45):
  d=request(mode,**kwargs)
  if d.get('ready'):return d
  time.sleep(1)
 raise RuntimeError('Game/editor did not become ready: '+str(d))
def wait_restored():
 for _ in range(45):
  if request('authoring-status').get('playing') is False:
   info=request('authoring-workspace-status')
   if not info.get('busy'):return info
  time.sleep(1)
 raise RuntimeError('Editor did not restore the workspace')
def snapshot(folder):
 return {f.relative_to(folder).as_posix():hashlib.sha256(f.read_bytes()).hexdigest() for f in folder.rglob('*') if f.is_file() and f.suffix in ('.mvs','.bak','.txt')}
progress=None;before=None;results=[];workspace_before=None
for profile in profiles:
 id=profile['id'];output=BASE/'Levels'/id
 d=request('world-export',draft=a.project/'Temp/ai/WorldStudies/R07'/f'{id}.draft.json',output=output)
 if d.get('errors'):raise RuntimeError(str(d['errors']))
 draft=next(output.glob('*.draft.json'));export=json.loads(draft.read_text(encoding='utf-8'))
 # Minify the review artifact, without touching authoring sources or its data.
 draft.write_text(json.dumps(export,separators=(',',':'),ensure_ascii=False),encoding='utf-8')
 capture=json.loads((BASE/'Review/R07'/f'{id}.json').read_text(encoding='utf-8'))
 art_hash=hashlib.sha256(next(output.glob('*.art.json')).read_bytes()).hexdigest()
 assert art_hash==capture['artSha256'],f'{id}: exported composition differs from reviewed frame'
 if a.export_only:
  print('Exported',id,flush=True);continue
 if progress is None:
  # Read the game's actual persistent path before Play; do not guess a company/product folder.
  info=request('props-game-status',draft=draft)
  progress=Path(info['persistentDataPath']);before=snapshot(progress)
  workspace_before=request('authoring-workspace-status')
 request('props-play',draft=draft)
 try:
  playing=wait_ready('props-game-status',draft=draft)
  assert playing.get('memoryOnly') is True and playing.get('identical') is True and playing.get('forbiddenComponents')==[],playing
  complete=request('authoring-complete')
  assert all(complete.get(k) is True for k in ('solved','completed','memoryOnly','progressUnchanged','albumUnchanged','frozenArtMatches')),complete
  proof={'profile':id,'exportArtSha256':art_hash,'reviewedArtSha256':capture['artSha256'],'play':playing,'completion':complete}
  (PROOF/f'{id}-play.json').write_text(json.dumps(proof,indent=2,ensure_ascii=False),encoding='utf-8');results.append(id)
 finally:
  request('authoring-stop')
  restored=wait_restored()
  assert restored['artSha256']==workspace_before['artSha256'],'Original workspace changed after Play'
 print('Verified game and export:',id,flush=True)
if not a.export_only:
 after=snapshot(progress);proof={'path':str(progress),'before':before,'after':after,'unchanged':before==after,'verifiedLevels':results}
 (PROOF/'progress-preservation.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')
 assert before==after,'Player progress changed'
 print('All five passed; progress preserved',flush=True)
