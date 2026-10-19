"""Stage-based V03 production through the existing WorldOps Editor, never a new Unity launch."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import time

ROOT=Path(__file__).resolve().parents[1]


def invoke(*args):
    result=subprocess.run([sys.executable,*map(str,args)],cwd=ROOT)
    if result.returncode:raise SystemExit(result.returncode)


def capture(project,version):
    invoke('tools/qc_world.py','--project',project,'teaser','record','--version',version)
    journal=project/'UserSettings/Teaser'/('job-'+version+'.json');deadline=time.monotonic()+1200;last=None
    while time.monotonic()<deadline:
        try:job=json.loads(journal.read_text(encoding='utf-8-sig'))
        except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.5);continue
        state=(job['status'],job['shot'],len(job['receipts']))
        if state!=last:print(version,state,flush=True);last=state
        if job['status']=='error':raise RuntimeError(job['error'])
        if job['status']=='captured':
            invoke('tools/package_teaser.py','--project',project,'--version',version)
            return
        time.sleep(1)
    raise TimeoutError('Existing Editor capture did not finish; no new Editor was started.')


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',type=Path,required=True)
    p.add_argument('stage',choices=['hooks','rough','fine']);a=p.parse_args();project=a.project.resolve()
    if a.stage=='hooks':
        for version in ['v03-a','v03-b','v03-c']:capture(project,version)
    else:capture(project,'v03-rough' if a.stage=='rough' else 'v03')


if __name__=='__main__':main()
