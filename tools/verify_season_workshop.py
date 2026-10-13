"""Serial normal-game checks in the existing Editor; no builds or new Unity process."""
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
p.add_argument('--resume',action='store_true')
p.add_argument('--round',default='R07')
a=p.parse_args()
proof=ROOT/'Design/LevelAuthoring/SeasonAtmosphere'/('Verification' if a.round=='R07' else 'VerificationV2');proof.mkdir(parents=True,exist_ok=True)

def request(mode,**kwargs):
    args=[sys.executable,'-X','utf8',str(ROOT/'tools/request_authoring_props.py'),'--project',str(a.project),'--mode',mode]
    for key,value in kwargs.items():args.extend(['--'+key,str(value)])
    result=subprocess.run(args,capture_output=True,text=True,encoding='utf-8')
    if result.returncode:raise RuntimeError(result.stdout+result.stderr)
    return json.loads((a.project/'Temp/ai/qc007-art-result.json').read_text(encoding='utf-8')) if mode!='authoring-stop' else {}

def snapshot(folder):
    return {f.relative_to(folder).as_posix():hashlib.sha256(f.read_bytes()).hexdigest() for f in folder.rglob('*') if f.is_file() and f.suffix in ('.mvs','.bak','.txt')}

presets=json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp/season_atmosphere.json').read_text(encoding='utf-8'))['presets']
drafts=[(v['id'],a.project/'Temp/ai/SeasonAtmosphere'/a.round/f"{v['id']}.draft.json") for v in presets]
campaign=json.loads((a.project/'Assets/QuietCamp/Resources/QuietCamp/campaign.json').read_text(encoding='utf-8'))['campaignLevelIds']
for index in (0,26,52,78,99):
    key=campaign[index];drafts.append((f'campaign-{index+1:03}',a.project/'Assets/QuietCamp/Authoring/CampaignLevels'/f"{key.replace(':','_')}.draft.json"))
info=request('props-game-status',draft=drafts[0][1]);progress=Path(info['persistentDataPath']);before=snapshot(progress)
workspace=request('authoring-workspace-status');results=[]
for key,path in drafts:
    if a.resume and (proof/f'{key}-play.json').exists():results.append(key);continue
    request('props-play',draft=path)
    try:
        for _ in range(60):
            playing=request('props-game-status',draft=path)
            if playing.get('audioPaused'):request('authoring-focus-game')
            if playing.get('ready'):break
            time.sleep(1)
        else:raise RuntimeError('Game not ready: '+key)
        assert playing.get('memoryOnly') is True and playing.get('identical') is True and playing.get('forbiddenComponents')==[],playing
        assert playing.get('atmosphere'),playing
        subprocess.run([sys.executable,str(ROOT/'tools/refresh_existing_editor.py'),'--pid','23052','--focus-only'],check=True,capture_output=True)
        request('authoring-focus-game')
        if not playing.get('playingVoices'):
            for _ in range(20):
                time.sleep(.5);playing=request('props-game-status',draft=path)
                if playing.get('playingVoices'):break
        assert playing.get('playingVoices',0)>0,'No playing preset audio: '+key+' '+str(playing)
        if key in ('summer_rain','winter_snowfall','winter_blizzard','autumn_wind'):
            request('weather-motion',output=proof/'Motion'/key)
        complete=request('authoring-complete')
        assert all(complete.get(k) is True for k in ('solved','completed','memoryOnly','progressUnchanged','albumUnchanged','frozenArtMatches')),complete
        (proof/f'{key}-play.json').write_text(json.dumps({'play':playing,'completion':complete},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        results.append(key)
    finally:
        request('authoring-stop')
        for _ in range(60):
            if request('authoring-status').get('playing') is False:
                restored=request('authoring-workspace-status')
                if not restored.get('busy'):break
            time.sleep(1)
        else:raise RuntimeError('Workspace did not restore')
        assert restored['artSha256']==workspace['artSha256'],'Workspace changed after Play'
    print('Game/win/restore passed:',key,flush=True)
after=snapshot(progress)
(proof/'progress-preservation.json').write_text(json.dumps({'path':str(progress),'before':before,'after':after,'unchanged':before==after,'verifiedLevels':results},indent=2)+'\n',encoding='utf-8')
assert before==after,'Persistent progress changed'
print('All 25 passed; progress preserved',flush=True)
