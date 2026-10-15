"""Checkpoint short, serial batches; stop on content errors or a real lack of progress."""
import argparse,hashlib,json,subprocess,sys
from campaign_capture_receipt import current
from pathlib import Path
sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parents[1];QA=Path('C:/Users/Public/qcui-20261006/QuietCamp')
p=argparse.ArgumentParser();p.add_argument('--start',type=int,default=1);p.add_argument('--end',type=int,default=100);p.add_argument('--round',default='U00');p.add_argument('--compose',action='store_true');a=p.parse_args()
output=ROOT/'Design/LevelAuthoring/StoryCampaign/Review'/a.round

def complete(i):
    png=output/f'L{i:03}.png';receipt=output/f'L{i:03}.json'
    draft=QA/'UserSettings/QuietCampCampaignReview'/a.round/f'L{i:03}.draft.json'
    if not png.exists() or not receipt.exists() or not draft.exists():return False
    return current(QA,png,receipt,draft)

def progress(i):
    draft=QA/'UserSettings/QuietCampCampaignReview'/a.round/f'L{i:03}.draft.json'
    pwb=output/f'L{i:03}.pwb.json'
    return int(complete(i))+int(a.compose and draft.exists() and pwb.exists())

cursor=a.start
while cursor<=a.end:
    while cursor<=a.end and complete(cursor):cursor+=1
    if cursor>a.end:break
    end=min(a.end,cursor+2);before=sum(progress(i) for i in range(cursor,end+1))
    args=[sys.executable,str(ROOT/'tools/run_story_campaign_batch.py'),'--round',a.round,'--start',str(cursor),'--end',str(end)]
    if a.compose:args.append('--compose')
    result=subprocess.run(args,cwd=ROOT,capture_output=True,text=True,encoding='utf-8')
    log=QA/'UserSettings/QuietCampCampaignReview/BatchProofs'/f'{a.round}-{cursor:03}-{end:03}.log';log.parent.mkdir(parents=True,exist_ok=True);log.write_text(result.stdout+result.stderr,encoding='utf-8')
    after=sum(complete(i) for i in range(cursor,end+1));print(f'{a.round}: {cursor:03}–{end:03}: {after}/'+str(end-cursor+1)+' native frames; user-workspace roundtrip recorded.',flush=True)
    if not result.returncode and sum(progress(i) for i in range(cursor,end+1))<=before:
        raise SystemExit('Native batch returned without a current checkpoint. Stop and inspect its receipt; refusing unchanged retries.')
    if result.returncode and ('Paused after cleanup' not in result.stdout+result.stderr or sum(progress(i) for i in range(cursor,end+1))<=before):
        print((result.stdout+result.stderr)[-2200:],flush=True);sys.exit(result.returncode)
    # A memory pause may follow a completed frame. Restore, checkpoint and resume
    # only the unfinished part; no retry when there was no measurable progress.
print('Requested native frames complete. This is not artistic acceptance.',flush=True)
