"""Audit actual frozen primary geometry; declared IDs alone cannot prove uniqueness."""
import argparse,json,sys
from pathlib import Path
from build_campaign_landmarks import fingerprint,rigid_shape_profile,is_rigid_clone

ROOT=Path(__file__).resolve().parents[1]
QA=Path('C:/Users/Public/qcui-20261006/QuietCamp')
p=argparse.ArgumentParser();p.add_argument('--round',required=True);p.add_argument('--opening-round');p.add_argument('--require-all',action='store_true');a=p.parse_args()
catalog=json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Authoring/CampaignLandmarks/catalog.json').read_text(encoding='utf-8'))['entries']
rows=[];missing=[];errors=[];seen={};rigid=[]
for entry in catalog:
    order=entry['order'];stage=a.opening_round if order<=10 and a.opening_round else a.round
    path=QA/'Temp/ai/StoryCampaign'/stage/f'L{order:03}.draft.json'
    if not path.exists():missing.append(order);continue
    doc=json.loads(path.read_text(encoding='utf-8'));art=doc['art'];models={m['id']:m for m in art['models']}
    matches=[i for i in art['instances'] if i['id']==art.get('primaryLandmarkInstanceId')]
    if len(matches)!=1:errors.append(f'{order}: missing actual primary instance');continue
    model=models[matches[0]['modelId']];root=model.get('sourceModelId') or model['id']
    if art.get('primaryLandmarkId')!=entry['modelId'] or root!=entry['modelId']:errors.append(f'{order}: wrong primary source {root}')
    shape=fingerprint(model['vertices'])
    if shape in seen:errors.append(f'{order}: primary geometry repeats level {seen[shape]}')
    seen[shape]=order
    profile=rigid_shape_profile(model['vertices'])
    for previous,other in rigid:
        if is_rigid_clone(profile,other):errors.append(f'{order}: suspected rigidly transformed copy of primary {previous}; visual review and replacement required')
    rigid.append((order,profile))
    rows.append(dict(order=order,id=doc['sourceLevelId'],modelId=entry['modelId'],geometryHash=shape,source=path.as_posix()))
out=ROOT/'Design/LevelAuthoring/StoryCampaign/Verification';out.mkdir(parents=True,exist_ok=True)
report=dict(status='passed' if not errors and not missing else 'incomplete' if not errors else 'failed',count=len(rows),errors=errors,missing=missing,levels=rows)
(out/'primary-geometry.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='levels'}))
if errors or (a.require_all and missing):sys.exit(1)
