#!/usr/bin/env python3
"""Prepare inspectable scene sources. Publication and native captures are separate."""
import json, math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Design/Campaign100/2026-10-11'
DATA=ROOT/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp'
REMOVED={19,25,27,47,55,59,65,75,83,95}
# Curated project meshes; normalized source geometry is copied, never recreated from text.
ENSEMBLES={
 'trail':['sign','log','flower_yellowA'],
 'shelter':['story_trail_shelter','log_stack','sign'],
 'well':['ua_valley_well','log','ua_valley_fence_broken_0'],
 'orchard':['ua_valley_orchard','log','ua_valley_fence_broken_1'],
 'bus':['ua_bus_shelter_mosaic','ua_village_wood_pole','ua_valley_poplar'],
 'yard':['story_foundation','log','ua_valley_gate'],
 'house':['ua_valley_house','ua_valley_gate','ua_valley_fence_broken_2'],
 'gate':['ua_valley_gate','ua_valley_fence_broken_0','ua_valley_orchard'],
 'coop':['ua_chicken_coop','ua_chicken','ua_valley_orchard'],
 'apiary':['ua_beehive','ua_valley_orchard','ua_beehive'],
 'barn':['ua_barn','log_stack','ua_valley_gate'],
 'greenhouse':['branch_greenhouse','log','ua_valley_fence_broken_1'],
 'willow':['ua_valley_willow','ua_reed_clump','ua_mooring_post'],
 'power':['ua_village_wood_pole','ua_village_broken_pole','ua_valley_fence_broken_2'],
 'station':['branch_station','log','ua_valley_poplar'],
 'crossing':['ua_valley_bridge_remnants','ua_valley_footbridge','ua_valley_willow'],
 'mooring':['ua_mooring_post','story_workboat','ua_valley_willow'],
 'field':['ua_sunflower_patch','ua_field_weeds','sign'],
 'aircraft-fore':['staging_aircraft_fore','ua_field_weeds','flower_yellowA'],
 'aircraft-tail':['staging_aircraft_tail','ua_field_weeds','sign'],
 'aircraft-pair':['staging_aircraft_fore','staging_aircraft_tail','flower_yellowA'],
 'memorial':['story_memorial_garden','log','flower_yellowA'],
 'stranded':['story_workboat','ua_mooring_post','ua_reed_clump'],
 'water':['ua_valley_willow','ua_mooring_post','story_workboat'],
 'service':['ua_hydro_service_building','ua_reed_clump','ua_mooring_post'],
 'dam':['ua_pond_spillway_ruin','ua_hydro_service_building','ua_valley_willow'],
 'hut':['ua_forester_hut','log_stack','log'],
 'haven':['story_trail_shelter','ua_valley_well','flower_yellowA'],
}

def scene_corridors(level):
 w,h=level['width'],level['height']; ext={tuple(c) for c in level.get('exteriorWalkable',[]) or []}
 authored=[(x+.5-w/2,z+.5-h/2) for x,z in ext] if level.get('ruleVersion')==2 else []
 segments=[]
 for x,z in [level['entry']]+[[p['x'],p['z']] for p in level.get('accessPoints',[])]:
  if z==0:fx,fz=0,-1
  elif x==w-1:fx,fz=1,0
  elif z==h-1:fx,fz=0,1
  elif x==0:fx,fz=-1,0
  else:
   d=min(x,w-1-x,z,h-1-z)
   fx,fz=(0,-1) if d==z else (1,0) if d==w-1-x else (0,1) if d==h-1-z else (-1,0)
  end=0
  if level.get('ruleVersion')==2:
   for step in range(1,len(ext)+1):
    if (x+fx*step,z+fz*step) not in ext:break
    end=step+.5
  phase=(level['decorSeed']-int(level['decorSeed']/97)*97)*.09
  sign=1 if (level['decorSeed']^x^z)&1==0 else -1
  def point(d):
   fade=max(0,min(1,(d-max(.6,end))/1.4));fade=fade*fade*(3-2*fade)
   bend=(math.sin(max(0,d-max(.6,end))*.48)*.55+math.sin(d*.95+phase)*.12)*fade*sign
   return x+.5-w/2+fx*d+fz*bend,z+.5-h/2+fz*d-fx*bend
  points=[point(i*.75) for i in range(13)]
  segments+=list(zip(points,points[1:]))
 return authored,segments

def safe_bounds(level,bounds,occupied,corridors,spans_water=False):
 x0,z0,x1,z1=bounds;w,h=level['width']/2,level['height']/2
 if x0<w+.85 and x1>-w-.85 and z0<h+.85 and z1>-h-.85:return False
 if any(x0<b[2]+.25 and x1>b[0]-.25 and z0<b[3]+.25 and z1>b[1]-.25 for b in occupied):return False
 shore=level['environment'].get('shore')
 if shore and not spans_water:
  lo,hi=(-x1,-x0) if shore['side']=='left' else (x0,x1) if shore['side']=='right' else (-z1,-z0) if shore['side']=='back' else (z0,z1)
  if hi>shore['offset']-shore['width']/2-1.4 and lo<shore['offset']+shore['width']/2+1.4:return False
 authored,segments=corridors
 xs=[min(x0+i*.75,x1) for i in range(math.ceil((x1-x0)/.75)+2)]
 zs=[min(z0+i*.75,z1) for i in range(math.ceil((z1-z0)/.75)+2)]
 for x in xs:
  for z in zs:
   if any(max(abs(x-a),abs(z-b))<1.92 or (x-a)**2+(z-b)**2<2.25 for a,b in authored):return False
   for (a,b),(c,d) in segments:
    dx,dz=c-a,d-b;t=max(0,min(1,((x-a)*dx+(z-b)*dz)/max(.001,dx*dx+dz*dz)))
    if (x-a-dx*t)**2+(z-b-dz*t)**2<1.88**2:return False
 return True

def place_prop(level,points,height,preferred,occupied,corridors,bridge=False,variant=0,face_yaw=0):
 shore=level['environment'].get('shore');w,h=level['width']/2,level['height']/2
 def box(x,z,yaw):
  angle=math.radians(yaw);c,s=math.cos(angle),math.sin(angle)
  transformed=[((p[0]*c+p[2]*s)*height,(-p[0]*s+p[2]*c)*height) for p in points]
  return [x+min(p[0] for p in transformed),z+min(p[1] for p in transformed),x+max(p[0] for p in transformed),z+max(p[1] for p in transformed)]
 if bridge and shore:
  side=shore['side'];fx,fz={'left':(-1,0),'right':(1,0),'back':(0,-1),'front':(0,1)}[side]
  for along in [variant*4.4,-3,3,-5,5,-7,7]:
   bend=math.sin(along*.27+shore['seed']*.01)*.48+math.sin(along*.63)*.12
   x,z=fx*(shore['offset']+bend)+fz*along,fz*(shore['offset']+bend)-fx*along
   yaw=90 if fx else 0
   # Both bridges have a transverse span in different source axes.
   if variant==0:yaw=(yaw+90)%180
   b=box(x,z,yaw)
   if safe_bounds(level,b,occupied,corridors,True):return x,z,yaw,b,True
  raise ValueError('No supported bridge location '+level['id'])
 ex=max(abs(p[0]) for p in points)*height;ez=max(abs(p[2]) for p in points)*height
 candidates=[preferred]
 for gap in [.3,1,2,3,4,5,6]:
  for along in [0,-1.5,1.5,-3,3,-4.5,4.5,-6,6]:
   candidates += [(-w- .85-gap-ex,along),(w+.85+gap+ex,along),(along,-h- .85-gap-ez),(along,h+.85+gap+ez)]
 candidates.sort(key=lambda p:(p[0]-preferred[0])**2+(p[1]-preferred[1])**2)
 for x,z in candidates:
  b=box(x,z,face_yaw)
  if safe_bounds(level,b,occupied,corridors):return x,z,face_yaw,b,False
 raise ValueError('No safe scenic placement '+level['id'])

def main():
 a=json.loads((OUT/'existing-content-audit.json').read_text())
 selected=[x for x in a['levels'] if x['journey']=='main' and x['originalPosition'] not in REMOVED]
 assert len(selected)==100
 lines=[x.split('|') for x in (OUT/'Authoring/places.tsv').read_text().splitlines()]
 assert len(lines)==100
 source_models=[]
 for p in [DATA/'roadmap_models.json',DATA/'roadmap_story_models.json',ROOT/'QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Models/EnvironmentKit/models.json',ROOT/'QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Models/StagingLandmarks/models.json',ROOT/'QuietCamp/Assets/QuietCamp/Editor/RoadmapArchive/roadmap_culture_models.json']:
  source_models+=json.loads(p.read_text())
 source_models+=json.loads((OUT/'Authoring/detail-models.json').read_text())
 by_id={x['id']:x for x in source_models}
 missing={id for ids in ENSEMBLES.values() for id in ids}-by_id.keys();assert not missing,missing
 places=[]
 prepared=[]
 for n,(old,line) in enumerate(zip(selected,lines),1):
  title,ensemble,details,inference=line
  chosen=list(ENSEMBLES[ensemble])
  if 'лавк' in details:
   if 'log' in chosen:chosen[chosen.index('log')]='camp_bench'
   else:chosen.append('camp_bench')
  if 'ліхтар' in title+details:chosen.append('camp_lantern')
  if any(word in details for word in ['речі','пакун','запас']):chosen.append('camp_bag')
  if any(word in title+details for word in ['волонтер','майстерн']):chosen.append('camp_crate')
  if n==54:chosen+=['branch_pond','branch_pond']
  t=(n-1)/99
  # A continuous timeline; weather is an intensity curve rather than a cyclic binary switch.
  rain=.10+.22*(math.sin(t*math.pi*3.2)**2)+.32*math.exp(-((t-.65)/.105)**2)
  env=dict(seasonTimeline=round(t,6),weatherIntensity=round(rain,6),moisture=round(.28+rain*.6,6),
    treeDensity=round(.40+.18*math.sin(t*math.pi)**2,6),clusterSeed=31000+n*173,
    meadowSpecies=['grass','daisy'] if n<30 or n>88 else ['grass','poppy'] if n<51 else ['grass','wheat'],storyMotifs=[])
  row=dict(number=n,id=old['id'],titleUk=title,ensemble=ensemble,detailsUk=details,
    storyUk=inference,whyUk=details+' '+inference,
    priorPosition=old['originalPosition'],priorHash=old['contentHash'],puzzleSignature=old['puzzleSignature'],
    dimensions=old['dimensions'],mechanicCounts=old['counts'],authoringPressureScore=old['authoringPressureScore'],
    environmentalSource=env,modelIds=chosen,
    chapter=(n-1)//10+1,compositionVariant=(n-1)%10,
    careUk='Після завершення табір зберігає облаштовані гостями намети та особисті речі; сюжетний декор не змінює правил.',
    historyScope='Авторське місце після війни; не точна реконструкція історичного об’єкта.',nativeCapture=None)
  places.append(row)
  level=json.loads((OUT/'Baseline/Levels'/(old['id'].replace(':','_')+'.json')).read_text())
  level.update(order=n,chapter=row['chapter'],lighting='day',generatorVersion='cozy-campaign-100-v1')
  level['environment'].update(env)
  level['environment']['seasonId']='spring' if t<.20 or t>=.88 else 'summer' if t<.48 else 'autumn' if t<.70 else 'winter'
  level['environment']['weatherId']='cloudy' if rain>.38 else 'clear'
  level['environment']['shore']=None
  water=ensemble in {'willow','crossing','mooring','water','service','dam','stranded'}
  occupied_sides=set()
  for c in [level['entry']]+[[p['x'],p['z']] for p in level.get('accessPoints',[])]:
   if c[0]==0:occupied_sides.add('left')
   if c[0]==level['width']-1:occupied_sides.add('right')
   if c[1]==0:occupied_sides.add('back')
   if c[1]==level['height']-1:occupied_sides.add('front')
  side=next((v for v in ['left','back','right','front'] if v not in occupied_sides),None)
  if water and side:
   half=(level['width'] if side in ['left','right'] else level['height'])/2
   level['environment']['shore']=dict(kind='stream',side=side,width=2.4,offset=half+5.6,seed=31000+n*13)
  level['environment']['biomeId']='shore' if level['environment']['shore'] else 'pines' if 70<=n<=84 else 'forest' if n%3 else 'meadow'
  level['environmentPreset']='pines' if level['environment']['biomeId'] in ['pines','forest'] else 'meadow'
  props=[]
  positions=[]
  corridors=scene_corridors(level)
  occupied=[]
  for i,model_id in enumerate(row['modelIds']):
   m=by_id[model_id];v=m['data'];points=[v[j:j+3] for j in range(0,len(v),6)]
   extent_x=max(abs(p[0]) for p in points);extent_z=max(abs(p[2]) for p in points)
   height=1.85 if i==0 else 1.15 if i==1 else 1.35
   if 'aircraft' in model_id:height=1.05 if 'fore' in model_id else 1.9
   if model_id in ['sign','log','log_stack','ua_mooring_post']:height=.22 if model_id=='log' else .7 if model_id=='log_stack' else 1.05
   if 'flower' in model_id or 'weeds' in model_id:height=.34
   if 'pole' in model_id:height=3.1 if 'broken' not in model_id else .65
   if 'poplar' in model_id or 'willow' in model_id or 'orchard' in model_id:height=2.4
   if 'bridge' in model_id:height=.72 if 'remnants' in model_id else .5
   if model_id=='ua_beehive':height=.72
   if model_id=='story_memorial_garden':height=.65
   if model_id=='story_foundation':height=.55
   if model_id=='story_workboat':height=.85
   if model_id=='camp_bench':height=.72
   if model_id=='camp_lantern':height=.52
   if model_id in ['camp_crate','camp_bag']:height=.6
   if model_id=='branch_pond':height=.18
   ex=extent_x*height;ez=extent_z*height
   w,h=level['width']/2,level['height']/2
   # Each part owns a separate apron; no automatic runtime relocation.
   if i==0:x,z=-w-1.15-ex,(-.8+(n%4)*.45)
   elif i==1:x,z=(.25+(n%3)*.45),-h-1.15-ez
   elif i==2:x,z=w+1.15+ex,-h-1.15-ez
   elif i==3:x,z=0,h+1.15+ez
   elif i==4:x,z=-w-1.15-ex,h*.65
   else:x,z=w+1.15+ex,h*.65
   if side=='left' and water:x=-x if i==0 else x
   if side=='back' and water:z=-z
   # Hero and tertiary part can share one side after water reflection; separate along it.
   if water and side=='left' and i==2:z=-h-4.5-ez
   facade=model_id in ['ua_bus_shelter_mosaic','ua_valley_house','ua_valley_well','ua_hydro_service_building','ua_barn','ua_chicken_coop','camp_bench']
   x,z,yaw,bounds,spans=place_prop(level,points,height,(x,z),occupied,corridors,
     model_id in ['ua_valley_bridge_remnants','ua_valley_footbridge','ua_pond_spillway_ruin'],0 if model_id=='ua_pond_spillway_ruin' else i,180 if facade else 0)
   occupied.append(bounds)
   pid=f'place-{n:03d}-prop-{i+1}'
   nature=any(k in model_id for k in ['orchard','willow','poplar','reed','flower','weeds','sunflower'])
   category='nature' if nature else 'recovery' if 'aircraft' in model_id or 'memorial' in model_id else 'civilian'
   motif='orchard' if nature else 'civilian-debris' if category=='recovery' else 'rural-infrastructure'
   props.append(dict(id=pid,category=category,motif=motif,returningProp='',claimId=None,
    roadmapVisibility='hidden',levelDetailVisibility='detail',appearance='always',contentTags=['fiction-inspired'],roadmap=None,
    level=dict(assetId=model_id,x=round(x,5),z=round(z,5),elevation=.025,height=height,yaw=yaw,sway=nature,spansWater=spans)))
   positions.append(dict(id=pid,assetId=model_id,bounds=[round(v,3) for v in bounds],spansWater=spans))
  level['environmentalStory']=dict(schemaVersion=1,id=f'campaign100-{n:03d}',references=[],beats=[dict(
   id=f'place-{n:03d}',storyBeat=inference,heroLandmark=props[0]['id'],returningProp='',characterFocus='мандрівники й люди, що повертають повсякденне життя',
   worldStateBefore=details,worldStateAfter='Доглянутий табір із вільними проходами знову дозволяє людям користуватися місцем.',
   intendedInference=inference,alternativeInference='Гравець може просто помітити гарне затишне місце без обов’язкового прочитання історії.',
   culturalContext='Україна після війни; авторська сцена, не документальна реконструкція.',historicalReference=None,referenceConfidence='fictional',
   roadmapVisibility='hidden',levelDetailVisibility='detail',levelIds=[level['id']],props=props)])
  assert sum(len(by_id[id]['colors']) for id in row['modelIds'])<=20000,(n,'Story vertex buffer exceeded')
  row['authoredFootprints']=positions
  prepared.append(level)
 used={id for row in places for id in row['modelIds']}
 # Extra models only; retain the existing shared gameplay library.
 base={x['id'] for x in json.loads((DATA/'roadmap_models.json').read_text())+json.loads((DATA/'roadmap_story_models.json').read_text())}
 curated=[by_id[id] for id in sorted(used-base)]
 (OUT/'Authoring/curated-gameplay-models.json').write_text(json.dumps(curated,separators=(',',':'))+'\n')
 selection={x['id']:x['number'] for x in places}
 for row in a['levels']:
  row['newPosition']=selection.get(row['id'])
  row['decisionUk']='Збережений перевірений пазл; індивідуальне середовище та сюжет перебудовуються.' if row['id'] in selection else 'Збережений у попередньому контенті; не входить до нових 100 базових рівнів. '
  if row['journey']=='main' and row['id'] not in selection:
   row['decisionUk']+='Вилучений з активного маршруту для менш щільного ритму та розширеного сюжету; старий прогрес лишається.'
  elif row['journey']!='main':row['decisionUk']+='Окрема подорож або бонус відкладені для майбутніх пакетів.'
 a['independentSolverEvidence']='../../Roadmap/Paused/2026-10-11/source-check-receipt.json'
 (OUT/'existing-content-audit.json').write_text(json.dumps(a,ensure_ascii=False,indent=2)+'\n')
 (OUT/'Authoring/prepared-levels.json').write_text(json.dumps(prepared,ensure_ascii=False,indent=2)+'\n')
 (OUT/'Authoring/campaign100.json').write_text(json.dumps(dict(schemaVersion=1,bundledCount=100,levels=places),ensure_ascii=False,indent=2)+'\n')
 print('Prepared 100 unique designs,',len(curated),'additional shared gameplay models; no publication or renders claimed.')
if __name__=='__main__':main()
