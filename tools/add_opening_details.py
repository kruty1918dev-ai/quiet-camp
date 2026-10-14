"""Fifth inspected correction: purposeful human groups placed by native PWB."""
import json,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];QA=Path('C:/Users/Public/qcui-20261006/QuietCamp');BASE=QA/'Temp/ai/OpeningCampaign'
output=ROOT/'Design/LevelAuthoring/OpeningCampaign/Review/C05';output.mkdir(parents=True,exist_ok=True)
target=BASE/'C05';target.mkdir(parents=True,exist_ok=True)
groups=[
 [('qc_backpack',-5.7,4,0),('qc_kenney_survival_bedroll_packed',-6.2,3.5,35)],
 [('qc_backpack',4.6,4.9,30),('qc_mug',5.2,6,.78),('qc_kenney_survival_bedroll_packed',5.7,4.5,80)],
 [('qc_bench',-6,1.2,45),('qc_mug',-6,1.2,.55),('qc_bucket',-7,3.5,25)],
 [('qc_grain_sack',6,4.7,30),('qc_kenney_survival_bedroll_packed',-5.3,4.2,90),('qc_mug',-6,5,.55)],
 [('qc_apple_basket',-5.6,1.5,15),('qc_apple_basket',-4.7,1.8,60),('qc_garden_ladder',5.3,6.3,20)],
 [('qc_kenney_survival_bedroll_packed',-6.8,3,45),('qc_barrel',-7.8,4.4,10),('qc_bucket',-7.4,4,15)],
 [('qc_broken_beam',6,5.5,70),('qc_stone_wall_fragment',6.7,5.5,25),('qc_garden_ladder',-6,5.6,45)],
 [('qc_kenney_survival_bedroll_packed',5.2,4.4,20),('qc_mug',5,6,.55),('qc_rope_coil',5.4,5.2,40)],
 [('qc_bucket',6.7,4.5,65),('qc_broken_beam',7,5.6,30),('qc_rope_coil',5.2,4.9,15)],
 [('qc_mug',-6,6,.55),('qc_kenney_survival_bedroll_packed',-5.2,5.1,80),('qc_bucket',5.5,4.5,20)],
]
profiles=json.loads((ROOT/'Design/LevelAuthoring/OpeningCampaign/profiles.json').read_text(encoding='utf-8'));reviews=[]
for index,group in enumerate(groups,1):
 ident=f'QC{index:03}';profile=profiles[index-1];anchors=[]
 for model,x,z,last in group:
  anchors.append(dict(model=model,label=profile['story']['title']+' — людський слід',x=x,z=z,y=last if model=='qc_mug' else 0,yaw=0 if model=='qc_mug' else last,scale=1,age='dirt'))
 profile['world']['anchors']=anchors;path=target/(ident+'.profile.json');path.write_text(json.dumps(profile,ensure_ascii=False),encoding='utf-8')
 result=subprocess.run([sys.executable,str(ROOT/'tools/request_authoring_props.py'),'--project',str(QA),'--mode','opening-details','--draft',str(BASE/'C04'/(ident+'.draft.json')),'--profile',str(path),'--output',str(target/(ident+'.draft.json'))],capture_output=True,text=True,encoding='utf-8')
 if result.returncode:raise RuntimeError(result.stdout+result.stderr)
 proof=json.loads((QA/'Temp/ai/qc007-art-result.json').read_text(encoding='utf-8'));(output/(ident+'.pwb.json')).write_text(json.dumps(proof,ensure_ascii=False,indent=2),encoding='utf-8')
 reviews.append(dict(id=ident,issue='Головний предмет став видимим, але місце ще не показує конкретного користування людьми.',correction='Через PWB додав пов’язані з місцем речі: '+', '.join(i[0] for i in group)+'. Кухлі спираються на лавку/стіл; решта предметів — на землю.',nativeFrame=f'Review/C05/{ident}.png'))
 print('PWB human group',ident,flush=True)
(output/'corrections.json').write_text(json.dumps(reviews,ensure_ascii=False,indent=2),encoding='utf-8')
