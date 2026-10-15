"""U07: legible primary fronts, restrained exposure and useful camera framing.

Only applies to individually inspected U06 images. The metric models, puzzles,
seasons and surface simulation remain the shared project implementation.
"""
import argparse,hashlib,json,math
from copy import deepcopy
from story_campaign_review import ROOT,QA,fit_primary_camera,projected_bounds,terrain_height
from revise_story_campaign_belongings import bounds


def remove_intruding_trees(document):
    art=document['art'];models={m['id']:m for m in art['models']}
    hero=next(i for i in art['instances'] if i['id']==art['primaryLandmarkInstanceId'])
    primary=projected_bounds(hero,models[hero['modelId']],art,exact=True)
    candidates=[]
    for item in art['instances']:
        if item is hero or item['id'].startswith('overgrowth/'):continue
        if abs(item['x'])<document['level']['width']/2+.6 and abs(item['z'])<document['level']['height']/2+.6:continue
        model=models[item['modelId']]
        if model.get('seasonalRole') not in ('deciduous','conifer'):continue
        tree=projected_bounds(item,model,art,exact=True)
        widths=[max(0,min(tree[1][k],primary[1][k])-max(tree[0][k],primary[0][k])) for k in (0,1)]
        area=widths[0]*widths[1];primary_area=max(.01,(primary[1][0]-primary[0][0])*(primary[1][1]-primary[0][1]))
        root=projected_bounds(dict(x=item['x'],y=item['y'],z=item['z'],scale=1),dict(vertices=[0,0,0]),art)[0]
        # Crown rectangles may overlap far behind the landmark. Only remove a
        # foreground trunk whose crown covers a substantial part of its subject.
        if root[2]<primary[0][2]-.25 and primary[0][0]<root[0]<primary[1][0] and area/primary_area>.18:
            candidates.append((area,item['id']))
    removed={ident for _,ident in sorted(candidates,reverse=True)[:3]}
    art['instances']=[i for i in art['instances'] if i['id'] not in removed and not any(i['id'].startswith('overgrowth/'+ident+'/') for ident in removed)]
    return sorted(removed)


def main():
    p=argparse.ArgumentParser();p.add_argument('--start',type=int,default=1);p.add_argument('--end',type=int,default=100);args=p.parse_args()
    review=ROOT/'Design/LevelAuthoring/StoryCampaign/Review';stage=QA/'UserSettings/QuietCampCampaignReview'
    inspected={r['order']:r for r in json.loads((review/'U06/inspection.json').read_text(encoding='utf-8'))['levels']}
    profiles=json.loads((ROOT/'Design/LevelAuthoring/StoryCampaign/unique-profiles.json').read_text(encoding='utf-8'))
    target=stage/'U07';target.mkdir(parents=True,exist_ok=True);out=review/'U07';out.mkdir(parents=True,exist_ok=True)
    record=out/'corrections.json';rows=json.loads(record.read_text(encoding='utf-8')) if record.exists() else []
    for n in range(args.start,args.end+1):
        stem=f'L{n:03}';destination=target/(stem+'.draft.json')
        if destination.exists():raise RuntimeError('Refusing to overwrite '+stem)
        row=inspected[n];assert row['inspected'] and row['imageSha256']==hashlib.sha256((review/'U06'/(stem+'.png')).read_bytes()).hexdigest()
        source=stage/'U06'/(stem+'.draft.json');d=json.loads(source.read_text(encoding='utf-8'));art=d['art'];models={m['id']:m for m in art['models']}
        hero=next(i for i in art['instances'] if i['id']==art['primaryLandmarkInstanceId']);before_camera={k:deepcopy(art[k]) for k in ('cameraEuler','cameraTarget','orthographicSize')};anchors=[]
        def anchor(pose):
            m=models[pose['modelId']];absolute=not pose.get('surfaceAttached',True)
            return dict(instanceId=pose['id'],model=m.get('sourceModelId') or m['id'],label=pose['id'],x=pose['x'],z=pose['z'],y=pose['y'] if absolute else pose.get('surfaceOffset',0),yaw=pose['yaw'],scale=pose['scale'],water=absolute,age='dirt',ageAmount=0,preserveGeometry=True)
        if n in (6,12,66):
            hero['yaw']=(hero['yaw']+180)%360 if n==6 else art['cameraEuler'][1]+(15 if n==12 else 0)
            if hero.get('rotationEuler'):hero['rotationEuler'][1]=hero['yaw']
            anchors.append(anchor(hero))
        if n in (16,26,30,61,66,80):art['cameraEuler'][0]=30
        if n==43:art['cameraEuler']=[55,80,0]
        if n not in (42,44,56):fit_primary_camera(d,margin=.085,minimum=6.8)
        removed=remove_intruding_trees(d)
        if n==47:
            patches=sorted((i for i in art['instances'] if '/site-plant-' in i['id']),key=lambda i:i['id'])
            hb=bounds(hero,models[hero['modelId']])
            for item,(x,z) in zip(patches,((hb[0]-.7,hero['z']+.6),(hero['x']-.9,hb[3]+.6),(hero['x']+.65,hb[3]+.85))):
                item.update(x=x,z=z,y=terrain_height(art,x,z),surfaceOffset=0,surfaceAttached=True);anchors.append(anchor(item))
        atmosphere=art.get('atmosphere');exposure_change=0
        if atmosphere:
            # The inspected night surfaces merged into undergrowth. Retain
            # weather/colour/fog and lift exposure rather than replacing the mood.
            exposure_change=.18 if atmosphere['sunIntensity']<.65 else .10 if n in (42,44,56) else 0
            atmosphere['exposure']+=exposure_change
        profile=deepcopy(profiles[n-1]);profile['world']['anchors']=anchors;profile['world']['refineExistingPlants']=False
        profile['refitPrimaryAfterPlacement']=n not in (42,44,56)
        # Refit actual PWB-resolved poses, which can move a newly rotated model
        # out of the field. Do not restore a provisional pre-placement target.
        profile['cameraFitMargin']=.085;profile['cameraFitMinimum']=6.8
        destination.write_text(json.dumps(d,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8')
        (target/(stem+'.details.json')).write_text(json.dumps(profile,ensure_ascii=False),encoding='utf-8')
        rows.append(dict(order=n,baseline='U06',correction='U07',issues=row['issues'],sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),change=dict(cameraBefore=before_camera,cameraAfter={k:art[k] for k in before_camera},removedForegroundTrees=removed,placements=anchors,exposureChange=exposure_change),residual='Fresh native render and individual acceptance inspection required.'))
        record.write_text(json.dumps(sorted(rows,key=lambda r:r['order']),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Reviewed camera/front corrections staged:',len(rows))


if __name__=='__main__':main()
