"""Serial native previews of the same demo level for all twenty mood presets."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--round',required=True);p.add_argument('--only');a=p.parse_args()
QA=Path(r'C:\Users\Public\qcui-20261006\QuietCamp')
presets=json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp/season_atmosphere.json').read_text(encoding='utf-8'))['presets']
source=ROOT/'Design/LevelAuthoring/WorldImprovement/Levels/watermill/QC_STUDY_WATERMILL.draft.json'
review=ROOT/'Design/LevelAuthoring/SeasonAtmosphere/Review'/a.round;review.mkdir(parents=True,exist_ok=True)
(review/'presets.json').write_text(json.dumps(presets,ensure_ascii=False,indent=2),encoding='utf-8')
work=QA/'Temp/ai/SeasonAtmosphere'/a.round
def request(mode,**kwargs):
 args=[sys.executable,str(ROOT/'tools/request_authoring_props.py'),'--project',str(QA),'--mode',mode]
 for k,v in kwargs.items():args.extend(['--'+k,str(v)])
 result=subprocess.run(args,capture_output=True,text=True,encoding='utf-8')
 if result.returncode:raise RuntimeError(result.stdout+result.stderr)
 return json.loads((QA/'Temp/ai/qc007-art-result.json').read_text(encoding='utf-8'))
for preset in presets:
 if a.only and not preset['id'].startswith(a.only):continue
 id=preset['id'];d=request('mood-demo',draft=source,profile=id,output=work,name=id)
 if d.get('errors'):raise RuntimeError(str(d['errors']))
 receipt=request('draft-capture',draft=d['draftPath'],output=review,name=id)
 if receipt.get('shaderErrors'):raise RuntimeError(str(receipt['shaderErrors']))
 print(a.round,id,'captured',flush=True)
