"""U04: material-specific wear and legibility after actual U03 frame inspection."""
import argparse,hashlib,json
from copy import deepcopy
from story_campaign_review import ROOT,QA,fit_primary_camera,terrain_height

STAGE=QA/'UserSettings/QuietCampCampaignReview'
REVIEW=ROOT/'Design/LevelAuthoring/StoryCampaign/Review'

def wear(entry):
    family,form=entry['construction'].split(':')
    material='wood'
    if family in ('memorial','abutment','sluice','wall','spring','waystone','terrace') or form in ('masonry','embankment','root','stone','vault'):material='stone'
    if family in ('transformer','clock') or form in ('iron','water') or entry['modelId']=='ua_power_pylon_rusted':material='steel'
    if family=='aircraft':material='aluminium'
    if entry['modelId'] in ('ua_abandoned_house','ua_hydro_service_building','ua_dam_breached','ua_bus_shelter_mosaic'):material='stone'
    backend={'wood':'streaks','stone':'moss','steel':'rust','aluminium':'oxidation'}[material]
    amount=.23 if material=='aluminium' else .32 if family=='memorial' else .58 if material=='steel' else .56
    return material,backend,amount

def main():
    p=argparse.ArgumentParser();p.add_argument('--start',type=int,default=1);p.add_argument('--end',type=int,default=100);a=p.parse_args()
    profiles=json.loads((ROOT/'Design/LevelAuthoring/StoryCampaign/unique-profiles.json').read_text(encoding='utf-8'))
    entries=json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Authoring/CampaignLandmarks/catalog.json').read_text(encoding='utf-8'))['entries']
    inspected={r['order']:r for r in json.loads((REVIEW/'U03/inspection.json').read_text(encoding='utf-8'))['levels']}
    target=STAGE/'U04';target.mkdir(parents=True,exist_ok=True);output=REVIEW/'U04';output.mkdir(parents=True,exist_ok=True)
    record=output/'corrections.json';rows=json.loads(record.read_text(encoding='utf-8')) if record.exists() else []
    for n in range(a.start,a.end+1):
        stem=f'L{n:03}';destination=target/(stem+'.draft.json')
        if destination.exists():raise RuntimeError('Refusing to overwrite staged correction: '+stem)
        row=inspected[n];assert row['inspected'] and row['imageSha256']==hashlib.sha256((REVIEW/'U03'/(stem+'.png')).read_bytes()).hexdigest()
        source=STAGE/'U03'/(stem+'.draft.json');d=json.loads(source.read_text(encoding='utf-8'));art=d['art']
        primary=next(i for i in art['instances'] if i['id']==art['primaryLandmarkInstanceId']);entry=entries[n-1];family=entry['construction'].split(':')[0]
        material,backend,amount=wear(entry)
        if n in (6,24,47,70):primary['yaw']=(primary['yaw']+180)%360
        if n==35:primary['yaw']=(art['cameraEuler'][1]-20)%360
        if n==98:primary['yaw']=art['cameraEuler'][1]
        if n==38:primary['yaw']=(art['cameraEuler'][1]+30)%360
        if n==69:
            # Three broken piers now explain the old road bridge beside its
            # smaller wooden replacement; both structures retain metre scale.
            primary.update(x=-14.3,z=3.8,y=terrain_height(art,-14.3,3.8),surfaceOffset=0)
        if family in ('house','hut','store','shelter','well','rack','station','apiary','gazebo','kiosk','kitchen','workshop','stage') or n in (6,12,19,24,29,30,47,55,60,70,92):art['cameraEuler'][0]=35
        absolute=not primary.get('surfaceAttached',True)
        anchor=dict(instanceId=primary['id'],model=entry['modelId'],label=entry['label'],x=primary['x'],z=primary['z'],y=primary['y'] if absolute else primary.get('surfaceOffset',0),yaw=primary['yaw'],scale=primary['scale'],water=absolute,age=backend,ageAmount=amount,weathering=dict(material=material,patchSize=.7 if material=='wood' else 1.1,distribution='natural'),preserveGeometry=n!=69,motion=primary.get('motion'))
        profile=deepcopy(profiles[n-1]);profile['world']['anchors']=[anchor];profile['world']['refineExistingPlants']=False
        profile['refitPrimaryAfterPlacement']=True
        # Refitting happens once more after native PWB's surface/field clearance
        # resolution; the initial fit is only a provisional preview.
        fit_primary_camera(d,margin=.12)
        if n==44:art.update(cameraTarget=[0,1,-1.3],orthographicSize=12.3)
        if n==56:art.update(cameraTarget=[4,.8,-2.5],orthographicSize=13)
        if n in (44,56):profile['cameraOverrides']={key:art[key] for key in ('cameraTarget','orthographicSize')}
        destination.write_text(json.dumps(d,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8')
        (target/(stem+'.details.json')).write_text(json.dumps(profile,ensure_ascii=False),encoding='utf-8')
        rows.append(dict(order=n,baseline='U03',correction='U04',issues=row['issues'],change=dict(material=material,weathering=backend,amount=amount,preserveGeometry=n!=69,cameraEuler=art['cameraEuler'],water='Frozen river cameras explicitly request depth and opaque textures.',snow='One ground-shader authority, dormant substrate and spatial snow patches.'),sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),residual='Fresh native PWB placement, capture and visual inspection required.'))
    record.write_text(json.dumps(sorted(rows,key=lambda r:r['order']),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Material and legibility corrections staged:',len(rows))

if __name__=='__main__':main()
