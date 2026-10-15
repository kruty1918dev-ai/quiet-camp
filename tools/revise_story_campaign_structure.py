"""U02: corrections identified in the inspected U01 native contact sheets.

Physical model sizes stay in metres. Changes are staged, never published here.
"""
import hashlib,json,math
from copy import deepcopy
from story_campaign_review import ROOT,QA,projected_bounds,terrain_height

source=QA/'UserSettings/QuietCampCampaignReview/U01';target=QA/'UserSettings/QuietCampCampaignReview/U02'
review=ROOT/'Design/LevelAuthoring/StoryCampaign/Review/U02'
target.mkdir(parents=True,exist_ok=True);review.mkdir(parents=True,exist_ok=True)
catalog=json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Authoring/CampaignLandmarks/catalog.json').read_text(encoding='utf-8'))['entries']
new_models={m['id']:m for m in json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Authoring/CampaignLandmarks/models.json').read_text(encoding='utf-8'))['models']}
profiles=json.loads((ROOT/'Design/LevelAuthoring/StoryCampaign/unique-profiles.json').read_text(encoding='utf-8'))
records=[]
for n in range(1,101):
    name=f'L{n:03}';destination=target/(name+'.draft.json')
    if destination.exists():raise RuntimeError('Refusing to overwrite staged work: '+name)
    original=source/(name+'.draft.json');d=json.loads(original.read_text(encoding='utf-8'));art=d['art']
    assert (ROOT/'Design/LevelAuthoring/StoryCampaign/Review/U01'/(name+'.png')).exists()
    models={m['id']:m for m in art['models']};hero=next(i for i in art['instances'] if i['id']==art['primaryLandmarkInstanceId'])
    entry=catalog[n-1];family,form=entry['construction'].split(':');anchors=[];changes=[]
    # A 1.5 authored aspect was multiplying square previews and exposing the
    # landscape boundary. Refit real vertices rather than a giant tilted AABB.
    art['cameraAspect']=1
    if n not in (42,43,44):
        front=family in ('house','hut','store','workshop','cabinet','clock','memorial','shelter','kitchen','kiosk','station','rack','well','bench','apiary','wind')
        yaw=art['cameraEuler'][1]+(15 if family in ('house','hut','workshop') else 0) if front else hero['yaw']
        if n==48:hero.update(x=-6.5,z=3.5,y=terrain_height(art,-6.5,3.5),surfaceOffset=0)
        hero['yaw']=yaw
        model=new_models.get(entry['modelId'],models[hero['modelId']])
        anchor=dict(instanceId=hero['id'],model=entry['modelId'],label=entry['label'],x=hero['x'],z=hero['z'],y=hero.get('surfaceOffset',0) if hero.get('surfaceAttached',True) else hero['y'],yaw=yaw,scale=hero['scale'],water=not hero.get('surfaceAttached',True),age='dirt',ageAmount=.38,motion=hero.get('motion'))
        anchors.append(anchor)
        art['cameraTarget']=[hero['x']*.36,.55,hero['z']*.36]
        if n==28:art['cameraTarget']=[-2.8,4.2,0]
        point=dict(vertices=[0,0,0]);at=dict(x=art['cameraTarget'][0],y=art['cameraTarget'][1],z=art['cameraTarget'][2],scale=1)
        cp=projected_bounds(at,point,art)[0]
        board=dict(vertices=[v for x in [-d['level']['width']/2,d['level']['width']/2] for y in [0,1.6] for z in [-d['level']['height']/2,d['level']['height']/2] for v in (x,y,z)])
        bounds=[projected_bounds(hero,model,art,exact=True),projected_bounds(dict(x=0,y=0,z=0,scale=1),board,art)]
        art['orthographicSize']=max(7.4,max(abs(q[k]-cp[k]) for pair in bounds for q in pair for k in (0,1))*1.12)
        if n==56:
            # The broken central dam is the subject; its 40 m abutments may run
            # beyond the frame, as a real structure would. Do not shrink it.
            art['orthographicSize']=13;art['cameraTarget']=[4,.6,-2.5]
        changes.append('Real-vertex camera fit at square aspect; primary front turned toward the camera where its function was hidden.')
    elif n==43:
        art.update(cameraEuler=[60,90,0],cameraTarget=[-7.5,.5,0],orthographicSize=14.6)
        changes.append('View tail from the opposite side to separate its fin from the far ground boundary.')
    elif n==44:
        dx,dz=4.7,4.1
        for pose in art['instances']:
            if pose['id']==hero['id'] or pose['id'].startswith('crash/wing-support'):
                old=terrain_height(art,pose['x'],pose['z']);pose['x']+=dx;pose['z']+=dz
                pose['y']+=terrain_height(art,pose['x'],pose['z'])-old
        art['instances']=[i for i in art['instances'] if not i['id'].startswith('overgrowth/'+hero['id']+'/')]
        changes.append('Move detached wing and its physical support toward the foreground; keep both fuselage sections as framing.')
    else:
        art['orthographicSize']=13.2;changes.append('Closer square framing of the torn nose, camp and belongings.')
    for pose in art['instances']:
        m=models[pose['modelId']]
        if (m.get('sourceModelId') or m['id'])=='qc_kenney_nature_log_large' and pose['id']!=hero['id']:
            anchors.append(dict(instanceId=pose['id'],model='qc_kenney_nature_log',label='Колода біля стоянки',x=pose['x'],z=pose['z'],y=pose.get('surfaceOffset',0),yaw=pose['yaw']+90,scale=1,age='moss',ageAmount=.24))
    if len(anchors)>int(n not in (42,43,44)):changes.append('Replace the repeated oversized auxiliary trunk with a smaller existing metre-scale log.')
    if n in (16,17,48,53,64,84,100):changes.append('Replace the inspected weak or duplicate structural mesh with its corrected construction.')
    art['review']=None
    destination.write_text(json.dumps(d,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8')
    if anchors:
        profile=deepcopy(profiles[n-1]);profile['world']['anchors']=anchors
        (target/(name+'.details.json')).write_text(json.dumps(profile,ensure_ascii=False),encoding='utf-8')
    records.append(dict(order=n,baseline='U01',correction='U02',issues=changes,sourceSha256=hashlib.sha256(original.read_bytes()).hexdigest(),residual='Requires native render, then inspection; no acceptance implied.'))
(review/'corrections.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('100 U02 corrections staged; native PWB replacements and capture pending.')
