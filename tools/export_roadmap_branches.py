#!/usr/bin/env python3
"""Offline branch scenery + presentation fixtures; does not publish playable content."""
import json, math, uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]/'QuietCamp/Assets/QuietCamp'
CONTENT=ROOT/'Resources/QuietCamp'
def save(path,data):
 path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
 meta=Path(str(path)+'.meta')
 if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
def box(stream,colors,centre,size,color):
 x,y,z=centre;w,h,d=[v/2 for v in size]
 corners=[(x-w,y-h,z-d),(x+w,y-h,z-d),(x+w,y+h,z-d),(x-w,y+h,z-d),(x-w,y-h,z+d),(x+w,y-h,z+d),(x+w,y+h,z+d),(x-w,y+h,z+d)]
 for face,n in [((0,3,2,1),(0,0,-1)),((4,5,6,7),(0,0,1)),((0,4,7,3),(-1,0,0)),((1,2,6,5),(1,0,0)),((3,7,6,2),(0,1,0)),((0,1,5,4),(0,-1,0))]:
  for tri in [(face[0],face[1],face[2]),(face[0],face[2],face[3])]:
   for i in tri:stream.extend([*corners[i],*n])
   colors.append(color)
def model(name,parts):
 v=[];c=[]
 for p in parts:box(v,c,*p)
 # All baked models have normalized height 1.
 height=max(v[i] for i in range(1,len(v),6));floor=min(v[i] for i in range(1,len(v),6))
 for i in range(0,len(v),6):v[i]/=height-floor;v[i+1]=(v[i+1]-floor)/(height-floor);v[i+2]/=height-floor
 return dict(id=name,data=v,colors=c)
models=json.loads((CONTENT/'roadmap_story_models.json').read_text());models=[m for m in models if not m['id'].startswith('branch_')]
pond=model('branch_pond',[((0,.03,0),(.1,.06,.1),0x526e67)])
v=[];c=[]
for i in range(14):
 a=i*math.tau/14;b=(i+1)*math.tau/14
 for point in [(0,.025,0),(math.cos(b)*1.2,.025,math.sin(b)*.85),(math.cos(a)*1.2,.025,math.sin(a)*.85)]:v.extend([*point,0,1,0])
 c.append(0x507d80)
# normalize by low stone height; renderer uses a matching shallow display height.
height=.13
for i in range(0,len(v),6):v[i]/=height;v[i+1]/=height;v[i+2]/=height
models.append(dict(id='branch_pond',data=v,colors=c))
parts=[((0,.48,0),(.29,.9,.29),0xc7c1a1),((0,.27,0),(.30,.11,.30),0x876448),((0,.69,0),(.30,.11,.30),0x876448),((0,.94,0),(.39,.11,.39),0x4a5d59),((0,.88,0),(.32,.10,.32),0xf0cf7e)]
models.append(model('branch_lighthouse',parts))
parts=[((0,.07,0),(3,.14,.85),0x777260),((0,.59,.17),(2.5,.16,.95),0x7d6b51)]
for x in [-1,1]:parts.append(((x,.32,.30),(.10,.5,.10),0x675b47))
for x in [-.22,.22]:parts.append(((x,.04,-1),(.045,.04,3.5),0x4b5652))
for z in [-2.5,-2,-1.5,-1,-.5,0]:parts.append(((0,.025,z),(.85,.035,.10),0x827252))
for x in [-1.2,1.2]:
 parts.append(((x,.55,-1.5),(.05,1.1,.05),0x6b725f));parts.append(((x,1.04,-1.5),(.45,.04,.05),0x6b725f))
models.append(model('branch_station',parts))
parts=[((0,.12,0),(2,.24,1.2),0x71865a)]
for x in [-1,0,1]:
 for z in [-.6,.6]:parts.append(((x,.54,z),(.055,.9,.055),0x988b6e))
for y in [.30,.70,1.0]:
 for z in [-.6,.6]:parts.append(((0,y,z),(2.05,.05,.05),0x988b6e))
for x in [-1,1]:parts.append(((x,.7,0),(.05,.05,1.2),0x988b6e))
for x in [-.6,0,.6]:parts.append(((x,.42,0),(.18,.35,.70),0x517548))
parts.append(((0,.08,.85),(2.6,.05,.18),0x608f8d));models.append(model('branch_greenhouse',parts))
save(CONTENT/'roadmap_story_models.json',models)
def props(identity):
 result=[]
 if identity=='fireflies':
  for i in range(8):
   a=i*math.tau/8
   result.append(dict(assetId='stone_largeA',x=math.cos(a)*3.15,z=math.sin(a)*2.25,height=.32,yaw=i*41,sway=False))
 trees='tree_pineRoundA' if identity=='fireflies' else 'tree_default'
 for i,(x,z) in enumerate([(-3,1.8),(3.8,2.8),(-3.5,-2.8),(3.6,-3)]):result.append(dict(assetId=trees,x=x,z=z,height=2.6 if identity!='lighthouse' else 1.5,yaw=i*43,sway=True))
 for i in range(6):
  result.append(dict(assetId='flower_yellowA' if identity=='garden' else 'stone_largeA' if identity=='lighthouse' else 'grass',x=-3+i*1.1,z=2 if i%2 else -2,height=.22 if identity=='garden' else .4,yaw=i*29,sway=identity!='lighthouse'))
 return result
def upgrade(branch,identity,kind,level_ids):
 branch.update(visualIdentity=identity,type=kind,descriptionKey='branch.'+identity+'.description',heroLandmark='branch_'+{'fireflies':'pond','lighthouse':'lighthouse','station':'station','garden':'greenhouse'}[identity],biome={'fireflies':'forest','lighthouse':'coast','station':'outskirts','garden':'meadow'}[identity],season='summer',lighting='night' if identity=='fireflies' else 'day',world=dict(extent=460,props=props(identity)),teaserDepth=1,
 accessOptions=[dict(method=branch.get('accessRule','progression'))])
 branch['nodes']=[dict(id=branch['id']+':node:'+str(i),levelId=lid,previewId=branch['previewId'],x=(1 if branch['x']>.5 else -1)*(3.8+i*1.2),y=-2.8-i*1.8,world=dict(extent=460,props=[dict(assetId='tent_smallOpen',x=0,z=0,height=1,yaw=0,sway=False)])) for i,lid in enumerate(level_ids)]
# Migration only adds presentation metadata. No journey/bonus content is published.
for path in [ROOT/'Authoring/Roadmap/main.json',CONTENT/'roadmap_regions.json',ROOT/'Authoring/Roadmap/foundation-slice.json',CONTENT/'Roadmaps/foundation_slice.json']:
 if not path.exists():continue
 data=json.loads(path.read_text())
 for region in data['regions']:
  for branch in region['branches']:
   if branch.get('journeyId')=='lighthouse':upgrade(branch,'lighthouse','story-journey',['QC_LH'+str(i).zfill(3) for i in range(1,9)])
   elif branch.get('bonusId'):upgrade(branch,'fireflies' if branch['bonusId'].endswith(':2') else 'garden','bonus',[branch['bonusId']])
 save(path,data)
# Four identities across four regions; 1 / 4 / 12 nodes, with multiple access offers.
fixture=ROOT/'Tests/Fixtures/Roadmap'
data=json.loads((fixture/'transition-summer-autumn.json').read_text());data['revision']='branch-presentation-v1'
for i,region in enumerate(data['regions']):
 identity=['fireflies','lighthouse','station','garden'][i];kind=['bonus','story-journey','mini-trail','mini-trail'][i];count=[1,12,4,3][i]
 anchor=region['nodePositions'][1]
 branch=dict(id='branch:fixture:'+identity,anchorNodeId=anchor['id'],journeyId='fixture-'+identity if kind!='bonus' else None,bonusId='fixture-fireflies' if kind=='bonus' else None,titleKey='branch.'+identity+'.title',previewId=region['previewId'],x=.18 if anchor['x']>.5 else .82,y=anchor['y']+210,radius=95,published=False,requires=[anchor['levelId']],accessRule='progression',teaserState='silhouette')
 upgrade(branch,identity,kind,['branch-fixture:'+identity+':'+str(j) for j in range(count)])
 branch['accessOptions']=[dict(method='progression'),dict(method='embers'),dict(method='rewarded',placement='branch.'+identity),dict(method='permanent-purchase',productId='journey.'+identity),dict(method='subscription',entitlementId='subscription.story'),dict(method='free-story')]
 region['branches']=[branch]
save(fixture/'branch-presentation.json',data);save(fixture/'branch-presentation-summaries.json',json.loads((fixture/'transition-summer-autumn-summaries.json').read_text()))
print('Exported four branch identities; synthetic fixture only, published flags unchanged')
