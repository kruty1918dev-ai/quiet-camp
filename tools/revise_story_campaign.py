"""Apply a specified correction after inspecting the corresponding native frames."""
import argparse,hashlib,json,math
from pathlib import Path
from story_campaign_review import ROOT,QA,projected_bounds,terrain_height

p=argparse.ArgumentParser();p.add_argument('--from-round',required=True);p.add_argument('--to-round',required=True);p.add_argument('--start',type=int,required=True);p.add_argument('--end',type=int,required=True);p.add_argument('--replace-unrendered',action='store_true');p.add_argument('--correction',choices=['primary-framing'],required=True);a=p.parse_args()
stage=QA/'Temp/ai/StoryCampaign';target=stage/a.to_round;target.mkdir(parents=True,exist_ok=True)
out=ROOT/'Design/LevelAuthoring/StoryCampaign/Review'/a.to_round;out.mkdir(parents=True,exist_ok=True)
record=out/'corrections.json';rows=json.loads(record.read_text(encoding='utf-8')) if record.exists() else []
profiles=json.loads((ROOT/'Design/LevelAuthoring/StoryCampaign/unique-profiles.json').read_text(encoding='utf-8'))
camera_edits=json.loads((ROOT/'Design/LevelAuthoring/StoryCampaign/camera-corrections.json').read_text(encoding='utf-8'))
for order in range(a.start,a.end+1):
    name=f'L{order:03}';source=stage/a.from_round/(name+'.draft.json');destination=target/(name+'.draft.json')
    if destination.exists():
        if not a.replace_unrendered or (out/(name+'.png')).exists() or (out/(name+'.json')).exists():
            raise RuntimeError('Refusing to overwrite reviewed work: '+str(destination))
        rows=[r for r in rows if r['order']!=order]
    receipt=ROOT/'Design/LevelAuthoring/StoryCampaign/Review'/a.from_round/(name+'.json')
    if not receipt.exists():raise RuntimeError('No native baseline for '+name)
    d=json.loads(source.read_text(encoding='utf-8'));art=d['art'];models={m['id']:m for m in art['models']}
    hero=next(i for i in art['instances'] if i['id']==art['primaryLandmarkInstanceId']);model=models[hero['modelId']]
    camera_edit=camera_edits.get(str(order))
    if camera_edit and camera_edit.get('primaryPose'):
        hero.update(camera_edit['primaryPose']);hero['surfaceAttached']=True
        hero['y']=terrain_height(art,hero['x'],hero['z'])+hero.get('surfaceOffset',0)
        art['instances']=[i for i in art['instances'] if not i['id'].startswith('overgrowth/'+hero['id']+'/')]
    water_heights=[]
    profile=profiles[order-1]
    for n,anchor in enumerate(profile['world']['anchors']):
        if not anchor.get('water'):continue
        pose=next((i for i in art['instances'] if i['id']==f'opening/{profile["story"]["id"]}/landmark-{n}'),None)
        if pose is not None and abs(pose['y']-anchor.get('y',0))>.0001:
            water_heights.append(dict(id=pose['id'],before=pose['y'],after=anchor.get('y',0)))
            pose['y']=anchor.get('y',0);pose['surfaceAttached']=False
    art['cameraTarget']=[hero['x']*.18,.55,hero['z']*.18]
    # Fit the actual landmark and the entire board without changing physical model sizes.
    board=dict(id='board',vertices=[v for x in [-d['level']['width']/2,d['level']['width']/2] for y in [0,1.6] for z in [-d['level']['height']/2,d['level']['height']/2] for v in (x,y,z)])
    board_pose=dict(x=0,y=0,z=0,yaw=0,scale=1)
    target_pose=dict(x=art['cameraTarget'][0],y=art['cameraTarget'][1],z=art['cameraTarget'][2],yaw=0,scale=1)
    point=dict(vertices=[0,0,0]);centre=projected_bounds(target_pose,point,art)[0]
    union=[projected_bounds(hero,model,art),projected_bounds(board_pose,board,art)]
    art['orthographicSize']=max(6.8,max(abs(q[k]-centre[k]) for pair in union for q in pair for k in (0,1))*1.17)
    if camera_edit:
        for field in ('cameraEuler','cameraTarget','orthographicSize'):
            if field in camera_edit:art[field]=camera_edit[field]
    protected=projected_bounds(hero,model,art);removed=[];kept=[]
    for i in art['instances']:
        m=models[i['modelId']]
        scale_y=(i.get('scaleXYZ') or [i['scale']]*3)[1]
        tall=(max(m['vertices'][1::3])-min(m['vertices'][1::3]))*scale_y>2
        if tall and i['id']!=hero['id'] and not i['id'].startswith('opening/') and m.get('seasonalRole') in ('deciduous','conifer'):
            lo,hi=projected_bounds(i,m,art);a0,b0=protected
            if lo[2]<a0[2] and lo[0]<b0[0]+.25 and hi[0]>a0[0]-.25 and lo[1]<b0[1]+.25 and hi[1]>a0[1]-.25:
                removed.append(i['id']);continue
        kept.append(i)
    art['instances']=kept
    # Attachments of removed trees cannot remain floating.
    art['instances']=[i for i in art['instances'] if not any(i['id'].startswith('overgrowth/'+r+'/') for r in removed)]
    destination.write_text(json.dumps(d,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8')
    rows.append(dict(order=order,baseline=a.from_round,correction=a.to_round,issue=camera_edit['issue'] if camera_edit else 'The primary silhouette is small or screened by foreground tree crowns in the inspected native frame.',change=dict(cameraEuler=art['cameraEuler'],cameraTarget=art['cameraTarget'],orthographicSize=art['orthographicSize'],occludingTreesRemoved=len(removed),waterHeightsRestored=water_heights),sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),resultSha256=hashlib.sha256(destination.read_bytes()).hexdigest(),residual='Requires a fresh native capture and visual inspection; no acceptance implied.'))
record.write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'Prepared {len(rows)} recorded corrections; native review pending.')
