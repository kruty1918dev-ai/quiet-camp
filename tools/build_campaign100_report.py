#!/usr/bin/env python3
"""Local, offline-readable evidence report. Missing native evidence stays explicitly pending."""
from pathlib import Path
import json,html,re,datetime,hashlib
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Design/Campaign100/2026-10-11'
def main():
 campaign=json.loads((OUT/'Authoring/campaign100.json').read_text())
 audit=json.loads((OUT/'existing-content-audit.json').read_text())
 previous=json.loads((ROOT/'Design/Roadmap/ModelCatalogue/catalogue.json').read_text())['models']
 by_path={m['path']:m for m in previous}
 models=[]
 for source in audit['modelSources']:
  number=source['number'];old=by_path.get(source['path']);new_file=OUT/'ModelPreviews~'/(number+'.json')
  captured=json.loads(new_file.read_text()) if new_file.exists() else None
  native=captured if captured and not captured.get('error') and captured.get('sourceHash')==source['sourceSha256'] else old if source['existingNativeProof'] else None
  folder='ModelPreviews~/' if native is captured and captured else '../../Roadmap/ModelCatalogue/LocalPreviews~/'
  title=old.get('titleUk') if old else Path(source['path']).stem.replace('_',' ')
  bounds=native.get('bounds',[]) if native else []
  desc=(title or Path(source['path']).stem)+'.'
  if bounds:desc+=' Габарити: '+' × '.join(f'{v:.2f}' for v in bounds)+' м (ширина × висота × глибина).'
  if captured and captured.get('materialBinding')=='two-sided-neutral-source-geometry-photography':desc+=' Фото показує двосторонню форму з нейтральним матеріалом; початковий матеріал у цьому прев’ю не відтворено.'
  if native:desc+=' Сіток: '+str(native.get('meshes',0))+'; трикутників джерела у кадрі: '+str(native.get('triangles',0))+'.'
  if old and old.get('categoryUk')=='персонажі':desc+=' Джерело містить групу фігур або її частини; фото показує склад джерела.'
  if source['container']=='.glb':desc+=' Контейнер GLB; фото повної геометрії після перетворення зі збереженням source triangles, кольорів граней і node transforms; PBR не відтворюється повністю.'
  images=[folder+i for i in (native.get('images',[]) if native else []) if (OUT/folder/i).is_file()]
  models.append(dict(number=number,name=title,path=source['path'],descriptionUk=desc,images=images,
     sourceSha256=source['sourceSha256'],captureDate='2026-10-11' if captured and native is captured else source['previousCaptureDate'],
     native=bool(len(images)==2),status=native.get('status','previous-reviewed') if native else 'capture-pending'))
 levels=[]
 for level in campaign['levels']:
  row=dict(level);row['renders']={};row['captureEvidence']={}
  for orientation in ['landscape','portrait']:
   base='Renders/level-'+str(row['number']).zfill(3)+'-'+orientation
   if (OUT/(base+'.png')).is_file() and (OUT/(base+'.json')).is_file():
    receipt=json.loads((OUT/(base+'.json')).read_text())
    level_file=ROOT/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp'/('GeneratedLevels' if row['id'].startswith('gen:') else 'Levels')/(row['id'].replace(':','_')+'.json')
    current=json.loads(level_file.read_text())
    if receipt.get('levelId')!=row['id'] or receipt.get('contentHash')!=current['contentHash']:continue
    row['renders'][orientation]=base+'.png';row['captureEvidence'][orientation]=receipt
  levels.append(row)
 data=dict(levels=levels,models=models,audit=audit['levels'],
  counts=dict(levels=100,levelCaptures=sum(bool(l['renders']) for l in levels),bothOrientations=sum(len(l['renders'])==2 for l in levels),
    models=len(models),modelCaptures=sum(m['native'] for m in models),inspected=audit['totalLevels']),
  generatedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
 (OUT/'model-map.json').write_text(json.dumps(models,ensure_ascii=False,indent=2)+'\n')
 template=Path(__file__).with_name('campaign100_report.html').read_text()
 template=template.replace('__DATA__',json.dumps(data,ensure_ascii=False,separators=(',',':')).replace('</','<\\/'))
 (OUT/'index.html').write_text(template)
 print(json.dumps(data['counts']))
if __name__=='__main__':main()
