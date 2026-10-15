"""Additional corrections identified in full-resolution U07 inspection.

Preserves the rejected frame, receipt and editable draft before replacing it.
Reuses the existing mesh authoring primitives and native PWB placement boundary.
"""
import hashlib,json,shutil
from copy import deepcopy
from build_campaign_landmarks import Mesh,PALETTE,box
from story_campaign_review import ROOT,QA


def packed(mesh,source=None):
    return dict(id=mesh.id,sourceModelId=source,shading='solid',seasonalRole='solid',
        vertices=[v for t in mesh.tris for p in t[:3] for v in p],
        normals=[v for t in mesh.tris for _ in range(3) for v in t[3]],
        colors=[PALETTE[t[4]] if isinstance(t[4],str) else t[4] for t in mesh.tris for _ in range(3)])


def main():
    review=ROOT/'Design/LevelAuthoring/StoryCampaign/Review';stage=QA/'UserSettings/QuietCampCampaignReview/U07'
    archive=review/'U07Additional';archive.mkdir(parents=True,exist_ok=True)
    history=review/'U07/corrections.json';rows=json.loads(history.read_text(encoding='utf-8'))
    for n in (16,47):
        stem=f'L{n:03}';path=stage/(stem+'.draft.json')
        if (archive/(stem+'.draft.json.gz')).exists():raise RuntimeError('Additional correction already staged: '+stem)
        import gzip
        (archive/(stem+'.draft.json.gz')).write_bytes(gzip.compress(path.read_bytes(),mtime=0))
        for suffix in ('.png','.json','.pwb.json'):shutil.copy2(review/'U07'/(stem+suffix),archive/(stem+suffix))
        d=json.loads(path.read_text(encoding='utf-8'));art=d['art'];hero=next(i for i in art['instances'] if i['id']==art['primaryLandmarkInstanceId']);models={m['id']:m for m in art['models']}
        profile=json.loads((stage/(stem+'.details.json')).read_text(encoding='utf-8'))
        if n==16:
            mesh=Mesh('qc_story_stop_mosaic_panel',1600,'Weathered mural bolted between the existing curved stop posts')
            box(mesh,0,1.85,1.15,4.16,1.42,.14,'concrete_dark')
            for row in range(6):
                for col in range(17):
                    distance=abs(col-8)+abs(row-2.5)*1.7
                    colour='blue_dark' if 2.5<distance<4.8 else 'rust' if 5.7<distance<7.5 else 'plaster_worn'
                    box(mesh,(col-8)*.235,1.25+row*.235,1.067,.219,.219,.026,colour)
            for x in (-2.06,2.06):box(mesh,x,1.85,1.15,.12,1.52,.2,'steel_dark')
            model=packed(mesh);model['authoringRecipe']=json.dumps(dict(operation='external',backend='existing-mesh-primitives',label='Мозаїка зупинки',category='Декор'),ensure_ascii=False)
            art['models'].append(model)
            anchor=dict(instanceId=hero['id']+'/mosaic-panel',model=model['id'],label='Мозаїчне панно на опорах зупинки',x=hero['x'],z=hero['z'],y=hero['y'],yaw=hero['yaw'],scale=hero['scale'],water=True,age='dirt',ageAmount=.12,weathering=dict(material='stone',patchSize=.45,distribution='lower'))
            issue='The curved stop has no readable mosaic despite its narrative name.'
            change='A muted geometric mural is physically fastened between the existing posts, behind the bench; separate editable native PWB instance.'
        else:
            original=models[hero['modelId']];model=deepcopy(original);model['id']='qc_story_windmill_rotor_clearance';model['sourceModelId']='qc_windmill_ruin'
            vertices=model['vertices'];moved=0
            for k in range(0,len(vertices),9):
                # The original shell stops at z=.86. This selects the complete
                # rotor triangles, preserving its broken fourth blade and scale.
                if min(vertices[k+2:k+9:3])>.9:
                    for j in range(k,k+9,3):vertices[j+1]+=.4;vertices[j+2]+=.65
                    moved+=1
            assert moved>40
            support=Mesh('shaft',24,'Horizontal axle supporting the forward rotor')
            support.beam((0,3.85,.3),(0,3.85,1.88),.18,'wood_dark',True)
            extra=packed(support)
            for key in ('vertices','normals','colors'):model[key]+=extra[key]
            if model.get('weatherExposure') is not None:model['weatherExposure'] += [1]*len(extra['colors'])
            if model.get('mossCoverage') is not None:model['mossCoverage'] += [0]*len(extra['colors'])
            for key in ('uv','tangents'):model.pop(key,None)
            model['authoringRecipe']=json.dumps(dict(operation='external',backend='rotor-mount-clearance',sourceId='qc_windmill_ruin',movedTriangles=moved),ensure_ascii=False)
            art['models'].append(model);hero['modelId']=model['id']
            anchor=dict(instanceId=hero['id'],model='qc_windmill_ruin',label='Вітряк із відкритим силуетом лопатей',x=hero['x'],z=hero['z'],y=hero.get('surfaceOffset',0),yaw=hero['yaw'],scale=hero['scale'],water=False,age='dirt',ageAmount=0,preserveGeometry=True)
            issue='The rotor overlaps the roof from the game camera and reads as roof debris rather than a windmill.'
            change='Move the complete metric rotor .65 m forward and .4 m upward on a supported horizontal axle; retain the original broken blades and tower.'
        profile['world']['anchors']=[anchor];profile['world']['refineExistingPlants']=False;profile['refitPrimaryAfterPlacement']=True
        path.write_text(json.dumps(d,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8')
        (stage/(stem+'.details.json')).write_text(json.dumps(profile,ensure_ascii=False),encoding='utf-8')
        (review/'U07'/(stem+'.pwb.json')).unlink()
        receipt=json.loads((archive/(stem+'.json')).read_text(encoding='utf-8'))
        row=next(r for r in rows if r['order']==n);row['additionalReviews']=[dict(issue=issue,change=change,rejectedImageSha256=receipt['imageSha256'],archive='Review/U07Additional/'+stem)]
    history.write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Two additional inspected corrections staged; fresh native frames required.')


if __name__=='__main__':main()
