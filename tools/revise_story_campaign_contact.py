"""U06: supported belongings, local cover compression and specific planting.

No global vegetation increase, tent pads or pre-authored footpaths. Canonical
CampGroundDetails continues to own changes caused by actual tent/resident use.
"""
import argparse,hashlib,json,math
from copy import deepcopy
from story_campaign_review import ROOT,QA,terrain_height
from campaign_surface_contact import settle,surface_height,world_vertices
from revise_story_campaign_belongings import bounds

def local_point(pose,x,y,z):return next(world_vertices(pose,dict(vertices=[x,y,z])))

def compress_cover(document,props):
    art=document['art'];models={m['id']:m for m in art['models']};changed=0
    footprints=[bounds(p,models[p['modelId']]) for p in props]
    for item in art['instances']:
        model=models[item['modelId']]
        if model.get('seasonalRole') not in ('groundcover','flower') or item['id'].startswith('overgrowth/'):continue
        if abs(item['x'])<document['level']['width']/2+.2 and abs(item['z'])<document['level']['height']/2+.2:continue
        if not any(b[0]-.12<item['x']<b[2]+.12 and b[1]-.12<item['z']<b[3]+.12 for b in footprints):continue
        scale=item.get('scaleXYZ') or [item['scale']]*3
        height=(max(model['vertices'][1::3])-min(model['vertices'][1::3]))*scale[1]
        if height<.055:continue
        item['scaleXYZ']=[scale[0],scale[1]*.18,scale[2]]
        item['y']=terrain_height(art,item['x'],item['z'])-min(model['vertices'][1::3])*item['scaleXYZ'][1]
        item['surfaceOffset']=item['y']-terrain_height(art,item['x'],item['z']);changed+=1
    return changed

def support_cups(document,models):
    art=document['art'];supported=[]
    for mug in art['instances']:
        if (models[mug['modelId']].get('sourceModelId') or mug['modelId'])!='qc_mug':continue
        options=[]
        for table in art['instances']:
            source=models[table['modelId']].get('sourceModelId') or table['modelId']
            if source not in ('qc_picnic_table','qc_barrel'):continue
            distance=math.hypot(table['x']-mug['x'],table['z']-mug['z'])
            if distance>3:continue
            for x,z in ((.18,.12),(-.16,.08),(0,0)):
                px,_,pz=local_point(table,x,0,z);h=surface_height(table,models[table['modelId']],px,pz)
                if h is not None:options.append((distance,px,h,pz,table['id']))
        if not options:continue
        _,x,y,z,owner=min(options)
        mug.update(x=x,y=y-min(models[mug['modelId']]['vertices'][1::3])*(mug.get('scaleXYZ') or [mug['scale']]*3)[1]+.006,z=z,surfaceAttached=False,rotationEuler=[0,mug.get('yaw',0),0])
        supported.append(dict(item=mug['id'],support=owner,height=y))
    return supported

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--start',type=int,default=1);parser.add_argument('--end',type=int,default=100);a=parser.parse_args()
    stage=QA/'UserSettings/QuietCampCampaignReview';review=ROOT/'Design/LevelAuthoring/StoryCampaign/Review'
    inspection={r['order']:r for r in json.loads((review/'U05/inspection.json').read_text(encoding='utf-8'))['levels']}
    profiles=json.loads((ROOT/'Design/LevelAuthoring/StoryCampaign/unique-profiles.json').read_text(encoding='utf-8'))
    out=review/'U06';out.mkdir(parents=True,exist_ok=True);target=stage/'U06';target.mkdir(parents=True,exist_ok=True)
    record=out/'corrections.json';records=json.loads(record.read_text(encoding='utf-8')) if record.exists() else []
    for n in range(a.start,a.end+1):
        stem=f'L{n:03}';destination=target/(stem+'.draft.json')
        if destination.exists():raise RuntimeError('Refusing to overwrite '+stem)
        row=inspection[n];image=review/'U05'/(stem+'.png');assert row['inspected'] and row['imageSha256']==hashlib.sha256(image.read_bytes()).hexdigest()
        source=stage/'U05'/(stem+'.draft.json');d=json.loads(source.read_text(encoding='utf-8'));art=d['art'];models={m['id']:m for m in art['models']}
        hero=next(i for i in art['instances'] if i['id']==art['primaryLandmarkInstanceId'])
        items=[i for i in art['instances'] if '/human-trace-' in i['id']]
        before={i['id']:deepcopy(i) for i in items}
        for item in items:settle(item,models[item['modelId']],art)
        if n==92:
            # Returned residents set their lamp on the porch, not in its grass.
            lamp=next(i for i in items if (models[i['modelId']].get('sourceModelId') or i['modelId'])=='qc_lantern')
            x,_,z=local_point(hero,1.4,0,-3.15)
            ground=terrain_height(art,x,z)
            support=surface_height(hero,models[hero['modelId']],x,z,max_height=ground+.8)
            if support is None:raise RuntimeError('The veranda lamp has no actual support surface.')
            lamp.update(x=x,z=z,y=support-min(models[lamp['modelId']]['vertices'][1::3])*lamp['scale']+.006,surfaceAttached=False,rotationEuler=[0,lamp['yaw'],0])
        compressed=compress_cover(d,items);cups=support_cups(d,models);anchors=[]
        def anchor(pose):
            m=models[pose['modelId']];absolute=not pose.get('surfaceAttached',True)
            return dict(instanceId=pose['id'],model=m.get('sourceModelId') or m['id'],label=pose['id'],x=pose['x'],z=pose['z'],y=pose['y'] if absolute else pose.get('surfaceOffset',0),yaw=pose['yaw'],scale=pose['scale'],water=absolute,age='dirt',ageAmount=0,preserveGeometry=True)
        anchors.extend(anchor(i) for i in items)
        anchors.extend(anchor(next(i for i in art['instances'] if i['id']==cup['item'])) for cup in cups)
        planting=[]
        def plant(model,x,z,scale=1,yaw=0,height=None):
            if abs(x)<d['level']['width']/2+.6 and abs(z)<d['level']['height']/2+.6:return
            if height is None and terrain_height(art,x,z)<.03:return
            placement=dict(instanceId=f'opening/{d["sourceLevelId"]}/site-plant-{len(planting)}',model=model,label='Рослина історії місця',x=x,z=z,y=0 if height is None else height,yaw=yaw,scale=scale,age='dirt',ageAmount=0,water=height is not None)
            planting.append(placement);anchors.append(placement)
        # These are small, purposeful additions to already dense scenes.
        # The sunflower composition uses the existing corrected patch geometry.
        if n==47:
            for j,(x,z) in enumerate(((4.7,4.9),(6,5.6),(7.3,4.7),(6.9,7.1),(4.8,7.4))):plant('ua_sunflower_patch',x,z,.88+(j%3)*.07,17+j*43)
        if n in (25,67,86,91,94):
            v=models[hero['modelId']]['vertices'];low=[min(v[k::3]) for k in range(3)];high=[max(v[k::3]) for k in range(3)]
            for j in range(6):
                x=low[0]+(high[0]-low[0])*(.28+(j%2)*.4);z=low[2]+(high[2]-low[2])*(.22+(j//2)*.27)
                px,_,pz=local_point(hero,x,0,z)
                ground=terrain_height(art,px,pz);surface=surface_height(hero,models[hero['modelId']],px,pz,max_height=ground+.8)
                # Do not plant on rafters; a bed surface must be near ground.
                surface=surface if surface is not None else ground
                plant('ua_orchard_tree' if n==91 else 'qc_kenney_nature_flower_yellowB',px,pz,.13 if n==91 else .7,j*67,surface+.012)
        if n in (7,15,18,21,29,45,96):
            hb=bounds(hero,models[hero['modelId']])
            for j in range(4):plant('qc_kenney_nature_flower_purpleA' if n in (15,45,96) else 'qc_kenney_nature_flower_yellowB',hb[0]-.45-j*.31,hb[1]+.35+(j%2)*.55,.75,j*31)
        # U05 already performed global clearance. Keep these deliberately placed
        # flowers: running the generic flower-cluster filter again would remove
        # authored bed plants according to its unrelated random patch mask.
        profile=deepcopy(profiles[n-1]);profile['world']['anchors']=anchors;profile['world']['refineExistingPlants']=False
        destination.write_text(json.dumps(d,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8')
        (target/(stem+'.details.json')).write_text(json.dumps(profile,ensure_ascii=False),encoding='utf-8')
        records.append(dict(order=n,baseline='U05',correction='U06',issues=row['issues'],change=dict(settled=[dict(id=i['id'],before=before[i['id']],after=i) for i in items],compressedCover=compressed,supportedCups=cups,sitePlanting=planting,field='No clearing, pre-worn routes or vegetation increase on the playable field.'),sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),residual='Native PWB, seasonal conversion and visual contact inspection required.'))
        record.write_text(json.dumps(sorted(records,key=lambda r:r['order']),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Contact and selective planting corrections staged:',len(records))

if __name__=='__main__':main()
