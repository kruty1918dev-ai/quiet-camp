"""Record revised real levels through WorldOps; retains the V01/V02/V03 exports."""
import argparse
from pathlib import Path
import subprocess
import sys
from teaser_v03 import capture,invoke


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',type=Path,required=True)
    p.add_argument('stage',choices=['pilot','record','review']);a=p.parse_args();project=a.project.resolve()
    if a.stage=='review':
        python=project/'UserSettings/TeaserV03Tools/venv/Scripts/python.exe'
        subprocess.run([str(python),'tools/review_teaser_v03.py','--project',str(project),'--version','v04'],check=True)
    else:
        version='v04-pilot' if a.stage=='pilot' else 'v04'
        invoke('tools/qc_world.py','--project',project,'teaser','create','--version',version)
        capture(project,version)


if __name__=='__main__':main()
