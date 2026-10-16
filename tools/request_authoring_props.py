"""Serial requests to an already open QuietCamp Editor. Never launches Unity."""
import argparse
import json
import os
from pathlib import Path
import time
import sys
sys.stdout.reconfigure(encoding='utf-8')

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--project',type=Path,required=True)
p.add_argument('--mode',choices=['learning-tests','learning-apply','worldops-tests','clearing-tests','authoring-memory','authoring-suspend-workspace','authoring-resume-workspace','authoring-trim','opening-water-tests','capture','opening-details','opening-gameplay','opening-tests','opening-compose','opening-apply','object-overgrowth','surface-tests','authoring-place-witness','forest-motion','forest-tests','forest-regrowth','props-tests','aging-ui','aging-ui-restore','crash-compose','season-variant','campaign-weather-upgrade','weather-motion','authoring-load-draft','authoring-focus-game','campaign-regenerate','mood-tests','mood-demo','world-export','world-tests','world-demo','props-demo','props-play','props-game-status','draft-capture','authoring-tests','authoring-profile','authoring-page','authoring-workspace-status','authoring-current-play','authoring-stop','authoring-status','authoring-restored','authoring-complete','gallery-tests','terrain-tests'],required=True)
p.add_argument('--level')
p.add_argument('--draft',type=Path)
p.add_argument('--profile')
p.add_argument('--output',type=Path)
p.add_argument('--name',default='props')
p.add_argument('--seed',type=int,default=12007)
p.add_argument('--timeout',type=int,default=1200)
a=p.parse_args()
lock_path=a.project.resolve()/'UserSettings/QuietCampCampaignReview/native-request.lock'
lock_path.parent.mkdir(parents=True,exist_ok=True)
_request_lock=lock_path.open('a+b')
if not lock_path.stat().st_size:_request_lock.write(b'0');_request_lock.flush()
_request_lock.seek(0)
try:
    if os.name=='nt':
        import msvcrt
        msvcrt.locking(_request_lock.fileno(),msvcrt.LK_NBLCK,1)
    else:
        import fcntl
        fcntl.flock(_request_lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
except OSError:raise SystemExit('Another native request is running. Wait for its final result before starting the next one.')
temp=a.project.resolve()/'Temp/ai';temp.mkdir(parents=True,exist_ok=True)
request=temp/'qc007-art-request.json';result=temp/'qc007-art-result.json'
if request.exists():raise SystemExit('A native request is pending; refusing to overwrite it.')
# Under the exclusive request lock, discard the previous completed response.
# Identical fast responses can retain an older Windows filesystem timestamp;
# wall-clock comparisons must not leave an already completed request waiting.
result.unlink(missing_ok=True)
payload=dict(mode=a.mode,profile=a.profile,name=a.name,seed=a.seed,width=1024 if a.mode=='capture' else 1536,height=1024)
if a.level:payload['levelId']=a.level
if a.draft:payload['draftPath']=a.draft.resolve().as_posix()
if a.output:payload['output']=a.output.resolve().as_posix()
started=time.time();pending=request.with_suffix('.pending')
pending.write_text(json.dumps(payload),encoding='utf-8');os.replace(pending,request)
while time.time()-started<a.timeout:
    if a.mode=='authoring-stop' and not request.exists():
        print('{"status":"stop-request-consumed"}');break
    if not request.exists() and result.exists():
        try:d=json.loads(result.read_text(encoding='utf-8'))
        except (json.JSONDecodeError,PermissionError):time.sleep(.1);continue
        if d.get('status') in ('tests-running','campaign-generating','profiling'):time.sleep(.3);continue
        if a.mode.endswith('tests') and d.get('status') not in ('tests','error'):
            raise SystemExit('Unexpected result during a test run; refusing to report it as completed tests: '+str(d.get('status')))
        print(json.dumps({k:v for k,v in d.items() if k in ('status','error','errors','draftPath','output','playing','ready','memoryOnly','objects','baked','identical','solved','completed','result','passed','failed','total','duration','forbiddenComponents','persistentDataPath')},ensure_ascii=False))
        if d.get('status')=='error' or d.get('errors') or d.get('failed',0):raise SystemExit(1)
        break
    time.sleep(.3)
else:raise SystemExit('Timed out; inspect the existing Editor. No new Editor was started.')
