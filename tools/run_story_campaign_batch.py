"""Run one serial native batch while preserving/releasing the user's terrain workspace."""
import argparse,json,subprocess,sys,time
from pathlib import Path
from datetime import datetime,timezone
sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parents[1];QA=Path('C:/Users/Public/qcui-20261006/QuietCamp')
p=argparse.ArgumentParser();p.add_argument('--round',required=True);p.add_argument('--start',type=int,required=True);p.add_argument('--end',type=int,required=True);p.add_argument('--compose',action='store_true');a=p.parse_args()

def request(mode):
    r=subprocess.run([sys.executable,str(ROOT/'tools/request_authoring_props.py'),'--project',str(QA),'--mode',mode],capture_output=True,text=True,encoding='utf-8')
    if r.returncode:raise RuntimeError(r.stdout+r.stderr)
    return json.loads((QA/'Temp/ai/qc007-art-result.json').read_text(encoding='utf-8'))

if (QA/'UserSettings/QuietCampCampaignReview/campaign-user-workspace.json').exists():raise RuntimeError('A preserved user workspace exists. Resume it before starting another batch.')
before=request('authoring-workspace-status');deadline=time.monotonic()+90
while before.get('busy') and time.monotonic()<deadline:
    time.sleep(.3);before=request('authoring-workspace-status')
suspended=False;code=1;proof=dict(started=datetime.now(timezone.utc).isoformat(),round=a.round,start=a.start,end=a.end,before=before)
try:
    if before['status']=='workspace':
        assert not before.get('busy'),'Wait for the user terrain update before suspension.'
        paused=request('authoring-suspend-workspace');suspended=True;assert paused['artSha256']==before['artSha256']
        print('User workspace preserved; temporary terrain released for native authoring.',flush=True)
    args=[sys.executable,str(ROOT/'tools/render_story_campaign.py'),'--round',a.round,'--start',str(a.start),'--end',str(a.end),'--no-sheets']
    if a.compose:args.append('--compose')
    code=subprocess.run(args,cwd=ROOT).returncode
finally:
    if suspended:
        request('authoring-resume-workspace');after=request('authoring-workspace-status');proof['after']=after
        proof['workspaceUnchanged']=after.get('artSha256')==before['artSha256'] and after.get('objects')==before['objects']
        assert proof['workspaceUnchanged'],'User workspace changed during batch restoration.'
        print('User workspace restored; exact art hash unchanged.',flush=True)
    proof['nativeBatchExitCode']=code
    out=QA/'UserSettings/QuietCampCampaignReview/BatchProofs';out.mkdir(parents=True,exist_ok=True)
    (out/f'{a.round}-{a.start:03}-{a.end:03}.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
sys.exit(code)
