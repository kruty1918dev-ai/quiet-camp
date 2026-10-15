"""U03: inspectable camera, flower-patch and structural-clearance corrections.

Existing ForestRegrowthAuthoring performs the vegetation edit in Unity. This
script stages framing and optional PWB poses; it never applies campaign content.
"""
import argparse,hashlib,json,math
from copy import deepcopy
from story_campaign_review import ROOT,QA,projected_bounds,terrain_height,fit_primary_camera

p=argparse.ArgumentParser();p.add_argument('--start',type=int,default=1);p.add_argument('--end',type=int,default=100);a=p.parse_args()
source=QA/'UserSettings/QuietCampCampaignReview/U02';target=QA/'UserSettings/QuietCampCampaignReview/U03';target.mkdir(parents=True,exist_ok=True)
review=ROOT/'Design/LevelAuthoring/StoryCampaign/Review';output=review/'U03';output.mkdir(parents=True,exist_ok=True)
inspection={r['order']:r for r in json.loads((review/'U02/inspection.json').read_text(encoding='utf-8'))['levels']}
profiles=json.loads((ROOT/'Design/LevelAuthoring/StoryCampaign/unique-profiles.json').read_text(encoding='utf-8'))
catalog=json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Authoring/CampaignLandmarks/catalog.json').read_text(encoding='utf-8'))['entries']
record=output/'corrections.json';rows=json.loads(record.read_text(encoding='utf-8')) if record.exists() else []
for n in range(a.start,a.end+1):
    name=f'L{n:03}';path=target/(name+'.draft.json')
    if path.exists():raise RuntimeError('Refusing to overwrite '+name)
    evidence=inspection[n]
    assert evidence['inspected'] and evidence['imageSha256']==hashlib.sha256((review/'U02'/(name+'.png')).read_bytes()).hexdigest()
    original=source/(name+'.draft.json');d=json.loads(original.read_text(encoding='utf-8'));art=d['art'];models={m['id']:m for m in art['models']}
    primary=next(i for i in art['instances'] if i['id']==art['primaryLandmarkInstanceId'])
    family=catalog[n-1]['construction'].split(':')[0]
    art['cameraEuler'][0]=42 if family in ('house','hut','store','shelter','well','rack','station','apiary','gazebo','kiosk','kitchen','workshop') or n in (6,12,19,24,47,55,60,70) else 47
    if n in (42,43):art['cameraEuler']=[58,0,0]
    if n==44:art['cameraEuler']=[54,0,0]
    anchors=[]
    if n in (47,60):
        if n==47:primary['yaw']=45
        else:
            primary['x']=-6.9;primary['y']=terrain_height(art,primary['x'],primary['z'])+primary.get('surfaceOffset',0)
            art['cameraEuler']=[44,135,0]
        anchors.append(dict(instanceId=primary['id'],model=catalog[n-1]['modelId'],label=catalog[n-1]['label'],x=primary['x'],z=primary['z'],y=primary.get('surfaceOffset',0),yaw=primary['yaw'],scale=1,age='dirt',ageAmount=.38))
    fit_primary_camera(d,margin=.08 if n in (42,43) else .12)
    if n==56:
        # Keep the broken central section and camp; long dam wings continue
        # outside the frame at their actual physical size.
        art['cameraTarget']=[4,.8,-2.5];art['orthographicSize']=13
    protected=[projected_bounds(primary,models[primary['modelId']],art,exact=True)]
    board=dict(vertices=[v for x in [-d['level']['width']/2,d['level']['width']/2] for y in [0,1.6] for z in [-d['level']['height']/2,d['level']['height']/2] for v in (x,y,z)])
    protected.append(projected_bounds(dict(x=0,y=0,z=0,scale=1),board,art))
    removed=set()
    for pose in art['instances']:
        model=models[pose['modelId']]
        if pose['id']==primary['id'] or model.get('seasonalRole') not in ('deciduous','conifer'):continue
        height=(max(model['vertices'][1::3])-min(model['vertices'][1::3]))*(pose.get('scaleXYZ') or [pose['scale']]*3)[1]
        if height<2:continue
        lo,hi=projected_bounds(pose,model,art)
        if any(lo[2]<b[2] and lo[0]<b[0]+.12 and hi[0]>a0[0]-.12 and lo[1]<b[1]+.12 and hi[1]>a0[1]-.12 for a0,b in protected):removed.add(pose['id'])
    def removed_owner(ident):return ident.startswith('overgrowth/') and ident[11:].rsplit('/',1)[0] in removed
    art['instances']=[i for i in art['instances'] if i['id'] not in removed and not removed_owner(i['id'])]
    path.write_text(json.dumps(d,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8')
    profile=deepcopy(profiles[n-1]);profile['world']['anchors']=anchors;profile['world']['refineExistingPlants']=True
    (target/(name+'.details.json')).write_text(json.dumps(profile,ensure_ascii=False),encoding='utf-8')
    rows.append(dict(order=n,baseline='U02',correction='U03',issues=evidence['issues'],change=dict(cameraEuler=art['cameraEuler'],cameraTarget=art['cameraTarget'],orthographicSize=art['orthographicSize'],occludingTreesRemoved=len(removed),vegetation='Existing ForestRegrowthAuthoring: grouped flowers and clearance below solid geometry; preserve low grass under elevated structures.',ground='Continue undersized legacy ground to the authored surround extent; preserve interior heights and water.'),sourceSha256=hashlib.sha256(original.read_bytes()).hexdigest(),residual='Fresh native capture and inspection required; no acceptance implied.'))
record.write_text(json.dumps(sorted(rows,key=lambda r:r['order']),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('U03 individual corrections staged:',len(rows))
