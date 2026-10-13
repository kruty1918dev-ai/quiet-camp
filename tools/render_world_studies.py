"""Serial native Unity captures; never launches an Editor or a player build."""
import argparse
import json
from pathlib import Path
import subprocess
import sys

sys.stdout.reconfigure(encoding='utf-8')
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--round',required=True)
p.add_argument('--only',nargs='*')
p.add_argument('--project',type=Path,default=Path('C:/Users/Public/qcui-20261006/QuietCamp'))
a=p.parse_args()
root=Path(__file__).resolve().parents[1]
out=root/'Design/LevelAuthoring/WorldImprovement/Review'/a.round
out.mkdir(parents=True,exist_ok=True)
profiles=a.project/'Assets/QuietCamp/Authoring/WorldStudies/profiles.json'
(out/'profiles.json').write_bytes(profiles.read_bytes())
entries=json.loads(profiles.read_text(encoding='utf-8'))
for profile in entries:
 if a.only and profile['id'] not in a.only:continue
 draft=a.project/'Temp/ai/WorldStudies'/a.round/(profile['id']+'.draft.json')
 for mode,args in [('world-demo',['--profile',profile['id']]),('draft-capture',['--output',str(out),'--name',profile['id']])]:
  result=subprocess.run([sys.executable,str(root/'tools/request_authoring_props.py'),'--project',str(a.project),'--mode',mode,'--draft',str(draft),*args],capture_output=True,text=True,encoding='utf-8')
  if result.returncode:
   print(result.stdout,result.stderr,flush=True);raise SystemExit(result.returncode)
  print(profile['id'],mode,result.stdout.strip(),flush=True)
