"""Bind the current agent's explicit artistic verdict to intact V03 evidence."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',type=Path,required=True)
    p.add_argument('--scores',type=float,nargs=5,required=True,metavar=('HOOK','GAMEPLAY','PAYOFF','EDIT','CRAFT'))
    p.add_argument('--notes',type=Path,required=True);p.add_argument('--decision',choices=['Review','Final'],required=True)
    p.add_argument('--unresolved-gates',nargs='*',type=int,default=[]);a=p.parse_args()
    if any(not 0<=s<=5 for s in a.scores):raise SystemExit('Scores must be 0..5.')
    folder=a.project.resolve()/'Marketing/Exports/v03/v03';path=folder/'Review/packet.json'
    packet=json.loads(path.read_text());movie=Path(packet['movie'])
    if sha(movie)!=packet['movieSha256']:raise SystemExit('Video changed after review.')
    for picture in packet['analysis']['images']:
        if sha(Path(picture['path']))!=picture['sha256']:raise SystemExit('Review picture changed.')
    result=subprocess.run([sys.executable,str(Path(__file__).with_name('qc_world.py')),'--project',str(a.project),
        'teaser','validate','--version','v03'],capture_output=True,text=True,encoding='utf-8',check=True)
    current=json.loads(result.stdout)
    if current['status']!='ok' or current['result']['sourceHash']!=packet['sourceHash']:
        raise SystemExit('Source changed after recording: re-record before an artistic verdict.')
    if a.decision=='Final' and (min(a.scores)<4 or a.unresolved_gates or packet['analysis']['blackStartOffsetFrames']):
        raise SystemExit('Final requires all scores >=4 and no unresolved gates/boundary errors.')
    verdict=dict(reviewer='current-agent-internal-review',decision=a.decision,scores=a.scores,
        weights=[.25,.25,.20,.15,.15],weightedScore=round(sum(s*w for s,w in zip(a.scores,[.25,.25,.20,.15,.15])),3),
        unresolvedGates=a.unresolved_gates,notes=str(a.notes.resolve()),notesSha256=sha(a.notes),
        sourceHash=packet['sourceHash'],packetSha256=sha(path),movieSha256=packet['movieSha256'],
        independentAudienceTest=False)
    output=folder/'Review/verdict.json';output.write_text(json.dumps(verdict,indent=2),encoding='utf-8')
    print(json.dumps(dict(decision=a.decision,weightedScore=verdict['weightedScore'],verdict=str(output))))


if __name__=='__main__':main()
