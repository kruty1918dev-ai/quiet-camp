"""Staged composition corrections and genuine Game View comparisons; no publication."""
import argparse
from copy import deepcopy
import hashlib
import json
import math
from pathlib import Path
import shutil
import subprocess

from story_campaign_review import ROOT, QA, projected_bounds

CONTENT=Path('Assets/QuietCamp/Resources/QuietCamp')
WORK=QA/'UserSettings/QuietCampCompositionReview'
DOC=ROOT/'Design/LevelAuthoring/StoryCampaign/CompositionReview'
BASE=QA/'UserSettings/QuietCampPromoCapture/2026-10-15-AudioVerified'
FF=QA/'UserSettings/QuietCampPromoTools/ffmpeg.exe'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write(path,value):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(value,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8')


def ids():
    values=json.loads((QA/CONTENT/'campaign.json').read_text(encoding='utf-8'))['campaignLevelIds']
    assert len(values)==len(set(values))==100
    return values


def baseline():
    review=json.loads((ROOT/'Design/LevelAuthoring/StoryCampaign/Promo/visual-review.json').read_text(encoding='utf-8'))
    assert review['sourceFolder']==BASE.name and len(review['sourceStills'])==100
    rows=[]
    for n,ident in enumerate(ids(),1):
        safe=ident.replace(':','_');source=BASE/f'L{n:03}'
        captured=json.loads((source/'capture.json').read_text(encoding='utf-8'))
        art=QA/CONTENT/'ArtCompositions'/(safe+'.json')
        level=QA/CONTENT/('GeneratedLevels' if ident.startswith('gen:') else 'Levels')/(safe+'.json')
        assert sha(art)==captured['inputs']['artSha256'] and sha(level)==captured['inputs']['levelSha256'],ident
        assert sha(source/'gameplay.png')==captured['imageSha256'] and sha(source/'gameplay.mp4')==captured['movieSha256']
        assert review['sourceStills'][n-1]['imageSha256']==captured['imageSha256']
        assert sha(ROOT/'QuietCamp'/art.relative_to(QA))==sha(art)
        folder=WORK/'B00'/f'L{n:03}';folder.mkdir(parents=True,exist_ok=True)
        shutil.copy2(source/'gameplay.png',folder/'placement.png')
        if not (folder/'complete.png').exists():
            subprocess.run([str(FF),'-y','-hide_banner','-loglevel','error','-threads','1','-i',str(source/'gameplay.mp4'),'-vf','select=eq(n\\,118)','-frames:v','1',str(folder/'complete.png')],check=True)
        shutil.copy2(art,WORK/'B00'/(safe+'.json'))
        draft=ROOT/'QuietCamp/Assets/QuietCamp/Authoring/CampaignLevels'/(safe+'.draft.json')
        shutil.copy2(draft,WORK/'B00'/f'L{n:03}.draft.json')
        rows.append(dict(order=n,id=ident,sourceArtSha256=sha(art),levelSha256=sha(level),placementSha256=sha(folder/'placement.png'),completeSha256=sha(folder/'complete.png'),sourceMovieSha256=captured['movieSha256'],nativeSettings=dict(width=1440,height=1080,fps=30,completedFrame=118),visualReview='Verified native source still and temporal samples inspected during Promo acceptance; source hashes checked again for this baseline.'))
    write(DOC/'baseline.json',dict(status='preserved-native-before',levels=rows))
    print('Preserved 100 genuine before pairs and their immutable source art.',flush=True)


def proxy(model):
    v=model['vertices'];lo=[min(v[k::3]) for k in range(3)];hi=[max(v[k::3]) for k in range(3)]
    return dict(vertices=[value for mask in range(8) for value in [hi[k] if mask&(1<<k) else lo[k] for k in range(3)]])


def blockers(art,level,models):
    board=dict(vertices=[v for x in [-level['width']/2-.6,level['width']/2+.6] for y in [0,2.1] for z in [-level['height']/2-.6,level['height']/2+.6] for v in (x,y,z)])
    box=projected_bounds(dict(x=0,y=0,z=0,scale=1),board,art)
    area=(box[1][0]-box[0][0])*(box[1][1]-box[0][1]);found=[]
    for item in art['instances']:
        model=models.get(item.get('modelId'))
        if model is None:continue
        v=model['vertices'];height=(max(v[1::3])-min(v[1::3]))*(item.get('scaleXYZ') or [item['scale']]*3)[1]
        if height<1.15:continue
        bounds=projected_bounds(item,model,art)
        if bounds[0][2]>=box[1][2]:continue
        overlap=math.prod(max(0,min(bounds[1][k],box[1][k])-max(bounds[0][k],box[0][k])) for k in [0,1])
        if overlap/area>.012:found.append(dict(id=item['id'],fraction=overlap/area,height=height))
    return sorted(found,key=lambda row:-row['fraction'])


def stage_camera(start,end):
    corrections=[]
    for n,ident in enumerate(ids(),1):
        if not start<=n<=end:continue
        safe=ident.replace(':','_');original=WORK/'B00'/f'L{n:03}.draft.json'
        d=json.loads(original.read_text(encoding='utf-8'));art=d['art'];level=d['level']
        # The baseline draft and actual published art must agree, including prior manual edits.
        assert dict(art,levelId=ident)==json.loads((WORK/'B00'/(safe+'.json')).read_text(encoding='utf-8')),ident
        models={m['id']:proxy(m) for m in art['models']}
        before={k:deepcopy(art.get(k)) for k in ['cameraEuler','cameraTarget','orthographicSize','cameraAspect']}
        previous=blockers(art,level,models)
        candidates=[];base_yaw=art['cameraEuler'][1]
        for pitch in [48,52,56]:
            for delta in [0,-30,30,-60,60,-90,90,180]:
                art['cameraEuler']=[pitch,(base_yaw+delta)%360,0]
                bad=blockers(art,level,models)
                # This is an authoring proposal, never visual acceptance. Preserve
                # a coherent angle when several candidates have similar overlap.
                score=sum(min(1,row['fraction']) for row in bad)+abs(delta)*.0005+abs(pitch-52)*.003
                candidates.append((score,deepcopy(art['cameraEuler']),bad))
        best=min(candidates,key=lambda row:row[0]);art['cameraEuler']=best[1]
        art.update(frameGameplayBoard=True,gameplayCameraMargin=1.12,cameraTarget=[0,1.05,0],cameraAspect=1)
        board=dict(vertices=[v for x in [-level['width']/2-.8,level['width']/2+.8] for y in [-.1,2.2] for z in [-level['height']/2-.8,level['height']/2+.8] for v in (x,y,z)])
        bounds=projected_bounds(dict(x=0,y=0,z=0,scale=1),board,art)
        art['orthographicSize']=max((bounds[1][k]-bounds[0][k])*.56 for k in [0,1])
        write(WORK/'C01'/f'L{n:03}.draft.json',d);write(WORK/'C01'/(safe+'.json'),dict(art,levelId=ident))
        corrections.append(dict(order=n,id=ident,baseline='B00',round='C01',cameraBefore=before,cameraAfter={k:art[k] for k in [*before,'frameGameplayBoard','gameplayCameraMargin']},projectedBlockersBefore=previous,projectedBlockersAfter=best[2],status='requires-native-game-view-inspection',change='Centre the gameplay volume inside its actual UI viewport; choose pitch/yaw by projected obstruction; decor no longer controls target or size.'))
    write(DOC/'C01/corrections.json',corrections)
    print('Staged camera proposals:',len(corrections),'(not published or visually accepted).',flush=True)


def sheets(round_name,start,end):
    from PIL import Image,ImageDraw,ImageFont
    font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',17)
    out=WORK/round_name/'review';out.mkdir(parents=True,exist_ok=True)
    for begin in range(start,end+1,5):
        numbers=list(range(begin,min(end+1,begin+5)))
        canvas=Image.new('RGB',(768,len(numbers)*312),(23,42,32));draw=ImageDraw.Draw(canvas)
        for row,n in enumerate(numbers):
            folder=WORK/round_name/f'L{n:03}'
            placement=folder/('placement.png' if round_name=='B00' else 'gameplay.png')
            complete=folder/'complete.png'
            if not complete.exists():
                subprocess.run([str(FF),'-y','-hide_banner','-loglevel','error','-threads','1','-i',str(folder/'gameplay.mp4'),'-vf','select=eq(n\\,118)','-frames:v','1',str(complete)],check=True)
            for column,path in enumerate([placement,complete]):
                with Image.open(path) as im:
                    assert im.size==(1440,1080)
                    im.thumbnail((384,288));canvas.paste(im,(column*384,row*312+24))
            draw.text((5,row*312),f'{n:03} · {round_name} · placement / complete',font=font)
        canvas.save(out/f'{begin:03}.jpg',quality=94)
    print('Game View placement/completion comparison sheets:',out,flush=True)


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('step',choices=['baseline','stage-camera','sheets']);p.add_argument('--round',default='C01');p.add_argument('--start',type=int,default=1);p.add_argument('--end',type=int,default=100);a=p.parse_args()
    if a.step=='baseline':baseline()
    elif a.step=='stage-camera':stage_camera(a.start,a.end)
    else:sheets(a.round,a.start,a.end)
