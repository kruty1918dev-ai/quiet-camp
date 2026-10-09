"""One-time source migration. Existing artist-authored recipes are never overwritten."""
import json, math
from pathlib import Path
root=Path('QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition'); root.mkdir(parents=True,exist_ok=True)
base=json.load(open('QuietCamp/Assets/QuietCamp/Authoring/Roadmap/main.json'))
def write(name,data):
 p=root/name
 if p.exists(): raise SystemExit(f'Refusing to overwrite {p}')
 p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
models=json.load(open('QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Models/UkrainianRural/manifest.json'))['models']
assets=[]
for m in models:
 w,h,d=m['dimensionsXYZ']; height={'ua_whitewashed_house':2.6,'ua_power_pylon':5.8,'ua_bus_shelter':1.8,'ua_well':.95,'ua_orchard_tree':2.8}.get(m['id'],h)
 if 'fence' in m['id']: height=h*3.3/w
 assets.append(dict(id=m['id'],source=m['source'],license='original-project-owned',sourceHash=m['sourceSha256'],height=height,width=w/h*height,depth=d/h*height,radius=math.hypot(w,d)/h*height/2,wind=('patch' in m['id'] or 'tree' in m['id'])))
for name,height,radius in [('tree_default',3.5,1.7),('tree_pineRoundA',4.0,1.6),('grass_leafsLarge',.28,.35),('flower_yellowA',.3,.3),('stone_largeC',.7,.6),('log_stack',.6,.7),('tent_smallOpen',1.18,1.2)]:
 assets.append(dict(id=name,height=height,radius=radius,width=radius*2,depth=radius*2,wind=name.startswith(('tree','grass','flower')),source='existing-QuietCamp-catalog',license='see-project-credits'))
write('assets.json',assets)
roles=[dict(id='house',asset='ua_whitewashed_house',x=-.7,z=-1.5,height=2.6),dict(id='well',asset='ua_well',parent='house',x=2.8,z=1,height=.95)]
for side in ['front','back','left','right']:
 for i in [-1,0,1]:
  if side=='front' and i==0: continue
  x,z,yaw=(i*3.3,4.95,0) if side=='front' else (i*3.3,-4.95,0) if side=='back' else (-4.95,i*3.3,90) if side=='left' else (4.95,i*3.3,90)
  roles.append(dict(id=f'fence-{side}-{i}',asset='$fence',parent='house',x=x,z=z,height=0,yaw=yaw))
roles.append(dict(id='gate',asset='ua_gate',parent='house',x=0,z=4.95,height=1.69))
roles.extend(dict(id=f'orchard-{i}',asset='ua_orchard_tree',parent='house',x=x,z=-2.4,height=2.1,required=False) for i,x in enumerate([-3.1,3.1]))
templates=[dict(id='ua.homestead',width=11,depth=11,entrance=True,roles=roles),dict(id='ua.abandoned-homestead',width=11,depth=11,entrance=True,ruined=True,roles=roles)]
for tid,spec in [
 ('ua.roadside-stop',[dict(id='shelter',asset='ua_bus_shelter',height=1.8,x=0,z=0),dict(id='entrance',asset='stone_largeC',parent='shelter',height=.15,x=0,z=2.2)]),
 ('ua.orchard',[dict(id='tree'+str(i),asset='ua_orchard_tree',height=2.5,x=(i%3-1)*3,z=(i//3-1)*3) for i in range(9)]),
 ('ua.field-margin',[dict(id='marker',asset='stone_largeC',height=.35,x=0,z=0)]),
 ('ua.woodland-glade',[dict(id='fallen-log',asset='log_stack',height=.45,x=0,z=0)]),
 ('ua.civilian-foundation',[dict(id='foundation',asset='stone_largeC',height=.3,x=0,z=0),dict(id='fence-remnant',asset='ua_picket_fence',parent='foundation',height=1.1,x=2,z=0,yaw=60)]),
 ('ua.water-crossing',[dict(id='crossing-anchor',asset='log_stack',height=.4,x=0,z=0)])]:
 templates.append(dict(id=tid,width=11 if tid=='ua.orchard' else 5,depth=11 if tid=='ua.orchard' else 5,entrance=tid=='ua.roadside-stop',ruined=tid=='ua.civilian-foundation',roles=spec))
write('templates.json',templates)
files=[]
for ri,r in enumerate(base['regions']):
 length=r['height']/22.936256
 nodes=[dict(id=n['id'],x=round((n['x']-.5)*26+math.sin(n['order']*1.67)*1.1,3),z=round(n['y']/22.936256+math.sin(n['order']*.8)*.7,3),radius=5.6) for n in r['nodePositions']]
 prefix=f'r{ri}'
 zones=[dict(id=prefix+'-'+side+'-forest',kind='forest',x=x,z=length/2,width=32,depth=length,density=.91) for side,x in [('left',-25),('right',25)]]
 zones+=[dict(id=prefix+'-meadow',kind='meadow',x=0,z=length/2,width=30,depth=length,density=.9)]
 # Broad connected fields, edged by belts; source geometry is filled offline.
 zones+=[dict(id=prefix+'-field',kind='field',species='ua_sunflower_patch' if ri==2 else 'ua_wheat_patch',x=24 if ri%2==0 else -25,z=length*.58,width=24,depth=length*.52,density=.87)]
 zones+=[dict(id=prefix+'-yards-'+side,kind='parcel',x=x,z=length/2,width=32,depth=length,density=1) for side,x in [('left',-23),('right',23)]]
 routes=[dict(id=prefix+'-main-road',kind='path',width=.55,points=[dict(x=nodes[0]['x']*.7,z=-1)]+[dict(x=n['x'],z=n['z']) for n in nodes]+[dict(x=nodes[-1]['x']*.6,z=length+1)])]
 if ri in [1,3]:
  routes.append(dict(id=prefix+'-transmission',kind='power',supportAsset='ua_power_pylon',width=.5,minSpan=22,maxSpan=44,supportHeight=5.8,points=[dict(x=39,z=-3),dict(x=30,z=length*.48),dict(x=43,z=length+3)]))
 ensembles=[]
 for ei,ni in enumerate([min(1,len(nodes)-1),max(0,len(nodes)-2)]):
  side='left' if (ri+ei)%2==0 else 'right'
  ensembles.append(dict(id=prefix+'-homestead-'+str(ei),template='ua.abandoned-homestead' if (ri+ei)%3==1 else 'ua.homestead',placement=dict(zone=prefix+'-yards-'+side,nearNode=nodes[ni]['id']),entranceConnectsTo=prefix+'-main-road',fenceVariant=['wattle','picket','concrete'][ri%3],state='partly-reclaimed'))
 doc=dict(schemaVersion=1,id=r['id'],seed=1918+ri*313,season=r['season'],nodes=nodes,zones=zones,routes=routes,ensembles=ensembles,landmarks=[])
 name=f'region-{ri}.json';write(name,doc);files.append(name)
write('manifest.json',dict(schemaVersion=1,id='quiet-camp-main',regions=files,assets='assets.json',templates='templates.json',coordinateSpace='metres-y-up',compilerRevision='semantic-composer-1'))
