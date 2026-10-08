#!/usr/bin/env python3
"""Offline presentation fixtures, never playable campaign/save/content replacement."""
import copy,json,uuid
from pathlib import Path
root=Path(__file__).resolve().parents[1]
content=root/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp'
out=root/'QuietCamp/Assets/QuietCamp/Tests/Fixtures/Roadmap'
levels=json.loads((content/'level_summaries.json').read_text())
source=json.loads((content/'roadmap_regions.json').read_text())
templates=[n for r in source['regions'] for n in r['nodePositions']]
def write(name,data):
 p=out/(name+'.json');p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
 meta=Path(str(p)+'.meta')
 if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
for name,stages in [('summer-autumn',[('summer','summer','forest'),('dry-summer','summer','meadow'),('early-autumn','autumn','agricultural-field'),('autumn','autumn','forest')]),('winter-thaw',[('late-autumn','autumn','forest'),('first-frost','winter','meadow'),('winter','winter','pines'),('thaw','spring','river'),('spring','spring','meadow')])]:
 summaries=[];regions=[];previous=None
 for r,(phase,season,biome) in enumerate(stages):
  nodes=[]
  for j in range(4):
   index=r*4+j;summary=copy.deepcopy(levels[index%len(levels)]);identity=f'transition:{name}:{index+1}'
   summary.update(id=identity,number=index+1,lighting='day',environmentPreset='meadow')
   summary['environment'].update(seasonId=season,biomeId=biome,weatherId='calm',shore=None)
   summaries.append(summary);node=copy.deepcopy(templates[index%len(templates)])
   node.update(id='node:'+identity,levelId=identity,previewId=identity,order=index+1,x=.5+[-.15,.18,.06,-.12][j],y=210+j*420,requires=[] if index==0 else [summaries[-2]['id']],branchLinks=[],associatedProps=[])
   nodes.append(node)
  regions.append(dict(id=f'transition:{name}:region:{r}',titleKey='map.region.'+season,biome=biome,season=season,environmentPhase=phase,landmark='sign',previewId=nodes[0]['levelId'],performanceTier='balanced',firstLevel=r*4+1,lastLevel=r*4+4,height=1880,lighting='day',weatherBias=.15,vegetationProfile=biome,terrainProfile='gentle-hills',environmentalMotifs=[],revealRules=dict(nearFuture=2,branchTeaser=1),chunks=[dict(id=f'transition:{name}:chunk:{r}',firstOrder=r*4+1,lastOrder=r*4+4)],transition=dict(fromSeason=previous or season,toSeason=season,kind='blend',length=1680),nodePositions=nodes,branches=[],storyProps=[],historical=dict(interpretation='fictional')))
  previous=season
 write('transition-'+name,dict(schemaVersion=1,revealDistance=2,revision='transition-examples-1',journeyId='main',presentation='world3d',regions=regions))
 write('transition-'+name+'-summaries',summaries)
# Upgrade only roadmap blend span, not puzzle data, IDs, saves or content hashes.
for path in [content/'roadmap_regions.json',root/'QuietCamp/Assets/QuietCamp/Authoring/Roadmap/main.json']:
 data=json.loads(path.read_text())
 for r in data['regions']:r['transition']['length']=min(1680,r['height'])
 path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
print('Exported summer/autumn and winter/thaw fixtures; updated roadmap spans only.')
