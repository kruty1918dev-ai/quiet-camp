"""Recreate interrupted authoring drafts from owned sources, outside Unity Temp.

This is recovery, not publication or visual acceptance. Previous captures remain
in U03Interrupted. Each recovered composition needs a fresh receipt and review.
"""
import argparse,hashlib,json,subprocess,sys
from copy import deepcopy
from story_campaign_review import ROOT,QA,terrain_height,fit_primary_camera,projected_bounds

sys.stdout.reconfigure(encoding='utf-8')
STAGE=QA/'UserSettings/QuietCampCampaignReview'
REVIEW=ROOT/'Design/LevelAuthoring/StoryCampaign/Review'

def write(path,value):
    path.parent.mkdir(parents=True,exist_ok=True)
    pending=path.with_suffix(path.suffix+'.pending')
    pending.write_text(json.dumps(value,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8')
    pending.replace(path)

def request(mode,**arguments):
    command=[sys.executable,str(ROOT/'tools/request_authoring_props.py'),'--project',str(QA),'--mode',mode]
    for key,value in arguments.items():command+=['--'+key,str(value)]
    result=subprocess.run(command,cwd=ROOT,capture_output=True,text=True,encoding='utf-8')
    if result.returncode:raise RuntimeError(result.stdout+result.stderr)
    proof=json.loads((QA/'Temp/ai/qc007-art-result.json').read_text(encoding='utf-8'))
    if proof.get('errors') or proof.get('shaderErrors'):raise RuntimeError(str(proof))
    return proof

def stage(raw,profile,entry,correction,camera_edit):
    d=deepcopy(raw);art=d['art'];models={m['id']:m for m in art['models']}
    primary=next(i for i in art['instances'] if i['id']==art['primaryLandmarkInstanceId'])
    n=d['level']['order'];family=entry['construction'].split(':')[0]
    if camera_edit.get('primaryPose'):
        primary.update(camera_edit['primaryPose']);primary['surfaceAttached']=True
        primary['y']=terrain_height(art,primary['x'],primary['z'])+primary.get('surfaceOffset',0)
    if n not in (42,43,44):
        if family in ('house','hut','store','workshop','cabinet','clock','memorial','shelter','kitchen','kiosk','station','rack','well','bench','apiary','wind'):
            primary['yaw']=art['cameraEuler'][1]+(15 if family in ('house','hut','workshop') else 0)
        if n==48:primary.update(x=-6.5,z=3.5,y=terrain_height(art,-6.5,3.5),surfaceOffset=0)
        if n==47:primary['yaw']=45
        if n==60:primary.update(x=-6.9,y=terrain_height(art,-6.9,primary['z'])+primary.get('surfaceOffset',0))
    elif n==44:
        for pose in art['instances']:
            if pose['id']==primary['id'] or pose['id'].startswith('crash/wing-support'):
                previous=terrain_height(art,pose['x'],pose['z']);pose['x']+=4.7;pose['z']+=4.1
                pose['y']+=terrain_height(art,pose['x'],pose['z'])-previous
    # Restore explicitly water-attached accessories before native placement.
    for index,anchor in enumerate(profile['world']['anchors']):
        if not anchor.get('water'):continue
        pose=next((i for i in art['instances'] if i['id']==f'opening/{profile["story"]["id"]}/landmark-{index}'),None)
        if pose:pose.update(y=anchor.get('y',0),surfaceAttached=False)
    anchors=[]
    if n not in (42,43,44):
        anchors.append(dict(instanceId=primary['id'],model=entry['modelId'],label=entry['label'],x=primary['x'],z=primary['z'],y=primary.get('surfaceOffset',0) if primary.get('surfaceAttached',True) else primary['y'],yaw=primary['yaw'],scale=primary['scale'],water=not primary.get('surfaceAttached',True),age='dirt',ageAmount=.38,motion=primary.get('motion')))
    for pose in art['instances']:
        m=models[pose['modelId']]
        if pose['id']!=primary['id'] and (m.get('sourceModelId') or m['id'])=='qc_kenney_nature_log_large':
            anchors.append(dict(instanceId=pose['id'],model='qc_kenney_nature_log',label='Колода біля стоянки',x=pose['x'],z=pose['z'],y=pose.get('surfaceOffset',0),yaw=pose['yaw']+90,scale=1,age='moss',ageAmount=.24))
    art['cameraEuler']=correction['change']['cameraEuler']
    art['cameraAspect']=1;art['review']=None
    details=deepcopy(profile);details['world']['anchors']=anchors;details['world']['refineExistingPlants']=True
    return d,details

def frame(d):
    art=d['art'];n=d['level']['order'];models={m['id']:m for m in art['models']}
    hero=next(i for i in art['instances'] if i['id']==art['primaryLandmarkInstanceId'])
    fit_primary_camera(d,margin=.08 if n in (42,43) else .12)
    if n==56:art.update(cameraTarget=[4,.8,-2.5],orthographicSize=13)
    board=dict(vertices=[v for x in [-d['level']['width']/2,d['level']['width']/2] for y in [0,1.6] for z in [-d['level']['height']/2,d['level']['height']/2] for v in (x,y,z)])
    protected=[projected_bounds(hero,models[hero['modelId']],art,True),projected_bounds(dict(x=0,y=0,z=0,scale=1),board,art)]
    removed=set()
    for pose in art['instances']:
        m=models[pose['modelId']]
        if pose['id']==hero['id'] or m.get('seasonalRole') not in ('deciduous','conifer'):continue
        if (max(m['vertices'][1::3])-min(m['vertices'][1::3]))*(pose.get('scaleXYZ') or [pose['scale']]*3)[1]<2:continue
        lo,hi=projected_bounds(pose,m,art)
        if any(lo[2]<b[2] and lo[0]<b[0]+.12 and hi[0]>a[0]-.12 and lo[1]<b[1]+.12 and hi[1]>a[1]-.12 for a,b in protected):removed.add(pose['id'])
    art['instances']=[i for i in art['instances'] if i['id'] not in removed and not (i['id'].startswith('overgrowth/') and i['id'][11:].rsplit('/',1)[0] in removed)]
    return len(removed)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--start',type=int,default=1);parser.add_argument('--end',type=int,default=100);args=parser.parse_args()
    assert 1<=args.start<=args.end<=100
    profiles=json.loads((ROOT/'Design/LevelAuthoring/StoryCampaign/unique-profiles.json').read_text(encoding='utf-8'))
    catalog=json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Authoring/CampaignLandmarks/catalog.json').read_text(encoding='utf-8'))['entries']
    corrections={r['order']:r for r in json.loads((REVIEW/'U03/corrections.json').read_text(encoding='utf-8'))}
    camera=json.loads((ROOT/'Design/LevelAuthoring/StoryCampaign/camera-corrections.json').read_text(encoding='utf-8'))
    status=request('authoring-workspace-status')
    if status['status']!='workspace-empty':raise RuntimeError('Recovery requires the original workspace to be preserved first; never replace it.')
    for n in range(args.start,args.end+1):
        stem=f'L{n:03}';directory=STAGE/'Recovery';directory.mkdir(parents=True,exist_ok=True)
        raw=directory/(stem+'.raw.draft.json');target=STAGE/'U03'/(stem+'.draft.json');proof_path=directory/(stem+'.checkpoint.json')
        if not target.exists():
            profile=directory/(stem+'.profile.json');write(profile,profiles[n-1])
            if not raw.exists():write(directory/(stem+'.compose.json'),request('opening-compose',profile=profile,draft=raw))
            document,details=stage(json.loads(raw.read_text(encoding='utf-8')),profiles[n-1],catalog[n-1],corrections[n],camera.get(str(n),{}))
            candidate=directory/(stem+'.candidate.draft.json');detail_path=directory/(stem+'.details.json');write(candidate,document);write(detail_path,details)
            target.parent.mkdir(parents=True,exist_ok=True)
            native=directory/(stem+'.refined.draft.json')
            proof=request('opening-details',profile=detail_path,draft=candidate,output=native)
            document=json.loads(native.read_text(encoding='utf-8'));removed=frame(document);write(target,document)
            write(REVIEW/'U03'/(stem+'.pwb.json'),proof)
            write(proof_path,dict(order=n,recovered=True,rawSha256=hashlib.sha256(raw.read_bytes()).hexdigest(),draftSha256=hashlib.sha256(target.read_bytes()).hexdigest(),occludingTreesRemoved=removed,accepted=False))
        result=subprocess.run([sys.executable,str(ROOT/'tools/render_story_campaign.py'),'--round','U03','--start',str(n),'--end',str(n),'--no-sheets'],cwd=ROOT,capture_output=True,text=True,encoding='utf-8')
        if result.returncode:raise RuntimeError(result.stdout+result.stderr)
        print(f'U03 recovered and captured {n:03}/100; visual review pending',flush=True)
        request('authoring-trim')

if __name__=='__main__':main()
