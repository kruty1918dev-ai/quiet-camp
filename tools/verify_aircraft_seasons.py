"""Check all four aircraft drafts through normal memory-only gameplay; preserve the user's workspace."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import time

sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parents[1]
QA=Path(r'C:\Users\Public\qcui-20261006\QuietCamp')
parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--output',type=Path,default=ROOT/'Design/LevelAuthoring/AircraftSeasons/Verification');args=parser.parse_args()
OUTPUT=args.output.resolve()
OUTPUT.mkdir(parents=True,exist_ok=True)
def request(mode,**kwargs):
    args=[sys.executable,'-X','utf8',str(ROOT/'tools/request_authoring_props.py'),'--project',str(QA),'--mode',mode]
    for k,v in kwargs.items():args+=['--'+k,str(v)]
    run=subprocess.run(args,capture_output=True,text=True,encoding='utf-8')
    if run.returncode:raise RuntimeError(run.stdout+run.stderr)
    return {} if mode=='authoring-stop' else json.loads((QA/'Temp/ai/qc007-art-result.json').read_text(encoding='utf-8'))
def snapshot(folder):
    return {p.relative_to(folder).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in folder.rglob('*') if p.is_file() and p.suffix in ('.mvs','.bak','.txt')}

folder=QA/'Assets/QuietCamp/Authoring/WorldStudies/AircraftSeasons'
progress=Path(request('props-game-status',draft=folder/'spring.draft.json')['persistentDataPath'])
before=snapshot(progress);workspace=request('authoring-workspace-status')
for season in ['spring','summer','autumn','winter']:
    draft=folder/(season+'.draft.json');request('props-play',draft=draft)
    try:
        for _ in range(60):
            playing=request('props-game-status',draft=draft)
            if playing.get('audioPaused'):request('authoring-focus-game')
            if playing.get('ready'):break
            time.sleep(1)
        else:raise RuntimeError('Game not ready: '+season)
        assert playing.get('memoryOnly') and playing.get('identical') and playing.get('forbiddenComponents')==[],playing
        subprocess.run([sys.executable,str(ROOT/'tools/refresh_existing_editor.py'),'--pid','23052','--focus-only'],check=True,capture_output=True)
        request('authoring-focus-game')
        for _ in range(20):
            playing=request('props-game-status',draft=draft)
            if playing.get('playingVoices',0)>0:break
            time.sleep(.5)
        assert playing.get('playingVoices',0)>0,playing
        complete=request('authoring-complete')
        assert all(complete.get(k) for k in ['solved','completed','memoryOnly','progressUnchanged','albumUnchanged','frozenArtMatches']),complete
        (OUTPUT/(season+'-play.json')).write_text(json.dumps({'play':playing,'completion':complete},ensure_ascii=False,indent=2),encoding='utf-8')
    finally:
        request('authoring-stop')
        for _ in range(60):
            if request('authoring-status').get('playing') is False:
                restored=request('authoring-workspace-status')
                if not restored.get('busy'):break
            time.sleep(1)
        else:raise RuntimeError('Workspace not restored')
        assert restored['artSha256']==workspace['artSha256'],'User workspace changed'
    print('Game, audio, solution and restore passed:',season,flush=True)
after=snapshot(progress);assert before==after,'Persistent progress changed'
(OUTPUT/'preservation.json').write_text(json.dumps(dict(unchanged=True,before=before,after=after,workspaceBefore=workspace,workspaceAfter=restored),ensure_ascii=False,indent=2),encoding='utf-8')
print('All four passed; saved progress and user workspace preserved')
