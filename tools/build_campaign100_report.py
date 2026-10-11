#!/usr/bin/env python3
"""Local, offline-readable evidence report. Missing native evidence stays explicitly pending."""
from pathlib import Path
import json,html,re,datetime,hashlib
from PIL import Image
from campaign100_capture_sources import current_manifest
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Design/Campaign100/2026-10-11'
def main():
 campaign=json.loads((OUT/'Authoring/campaign100.json').read_text())
 source_revision=current_manifest()['revision']
 audit=json.loads((OUT/'existing-content-audit.json').read_text())
 previous=json.loads((ROOT/'Design/Roadmap/ModelCatalogue/catalogue.json').read_text())['models']
 by_path={m['path']:m for m in previous}
 descriptions=json.loads((OUT/'model-descriptions-ua.json').read_text()) if (OUT/'model-descriptions-ua.json').exists() else {}
 models=[]
 for source in audit['modelSources']:
  number=source['number'];old=by_path.get(source['path']);new_file=OUT/'ModelPreviews~'/(number+'.json')
  captured=json.loads(new_file.read_text()) if new_file.exists() else None
  native=captured if captured and not captured.get('error') and captured.get('sourceHash')==source['sourceSha256'] else old if source['existingNativeProof'] else None
  folder='ModelPreviews~/' if native is captured and captured else '../../Roadmap/ModelCatalogue/LocalPreviews~/'
  override=descriptions.get(number,{})
  title=override.get('nameUk') or (old.get('titleUk') if old else Path(source['path']).stem.replace('_',' '))
  bounds=native.get('bounds',[]) if native else []
  desc=override.get('descriptionUk') or (title or Path(source['path']).stem)+'.'
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
 model_numbers={}
 for model in models:model_numbers.setdefault(Path(model['path']).stem,[]).append(model['number'])
 detail_sources=json.loads((OUT/'Authoring/detail-models-provenance.json').read_text())
 for model in detail_sources:model_numbers[model['id']]=[m['number'] for m in models if m['path']==model['path']]
 for level in campaign['levels']:
  row=dict(level);row['renders']={};row['captureEvidence']={};row['thumbnails']={};row['previousRenders']={};row['previousEvidence']={}
  row['modelRefs']=[dict(assetId=id,numbers=model_numbers.get(id,[])) for id in row['modelIds']]
  for orientation in ['landscape','portrait']:
   base='Renders/level-'+str(row['number']).zfill(3)+'-'+orientation
   if (OUT/(base+'.png')).is_file() and (OUT/(base+'.json')).is_file():
    receipt=json.loads((OUT/(base+'.json')).read_text())
    level_file=ROOT/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp'/('GeneratedLevels' if row['id'].startswith('gen:') else 'Levels')/(row['id'].replace(':','_')+'.json')
    current=json.loads(level_file.read_text())
    if receipt.get('levelId')!=row['id']:continue
    if receipt.get('imageSha256')!=hashlib.sha256((OUT/(base+'.png')).read_bytes()).hexdigest():continue
    matches=receipt.get('contentHash')==current['contentHash'] and receipt.get('sourceRevision')==source_revision
    row['renders' if matches else 'previousRenders'][orientation]=base+'.png'
    row['captureEvidence' if matches else 'previousEvidence'][orientation]=receipt
    thumb='Thumbnails/'+Path(base).name+'.webp'
    (OUT/'Thumbnails').mkdir(exist_ok=True)
    with Image.open(OUT/(base+'.png')) as image:
     image.thumbnail((640,640));image.save(OUT/thumb,quality=85,method=4)
    row['thumbnails'][orientation]=thumb
  levels.append(row)
 data=dict(levels=levels,models=models,audit=audit['levels'],
  counts=dict(levels=100,levelCaptures=sum(bool(l['renders']) for l in levels),bothOrientations=sum(len(l['renders'])==2 for l in levels),
    models=len(models),modelCaptures=sum(m['native'] for m in models),inspected=audit['totalLevels']),
  generatedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
 (OUT/'model-map.json').write_text(json.dumps(models,ensure_ascii=False,indent=2)+'\n')
 template=Path(__file__).with_name('campaign100_report.html').read_text()
 template=template.replace('__DATA__',json.dumps(data,ensure_ascii=False,separators=(',',':')).replace('</','<\\/'))
 (OUT/'index.html').write_text(template)
 public_data=json.loads(json.dumps(data))
 public_data['publicGuide']=True
 site=ROOT/'docs/campaign100';site.mkdir(parents=True,exist_ok=True)
 (site/'Renders').mkdir(exist_ok=True)
 image_index=json.loads((site/'image-source-index.json').read_text()) if (site/'image-source-index.json').exists() else {}
 import shutil
 for row in public_data['levels']:
  for key in ['renders','previousRenders']:
   for orientation,name in list(row[key].items()):
    display='Renders/'+Path(name).with_suffix('.webp').name
    proof=row['captureEvidence' if key=='renders' else 'previousEvidence'][orientation]
    if image_index.get(Path(name).name)!=proof['imageSha256'] or not (site/display).exists():
     with Image.open(OUT/name) as im:im.save(site/display,quality=90,method=4)
    image_index[Path(name).name]=proof['imageSha256']
    row[key][orientation]=display
  for orientation,name in row['thumbnails'].items():
   target=site/name;target.parent.mkdir(exist_ok=True);shutil.copy2(OUT/name,target)
 for model in public_data['models']:model['images']=[]
 public_template=Path(__file__).with_name('campaign100_report.html').read_text()
 # Documentation stays source-linked; licensed full model photographs remain local.
 source_base='https://github.com/kruty1918dev-ai/quiet-camp/blob/main/Design/Campaign100/2026-10-11/'
 for name in ['MECHANICS-UA.md','STORY-UA.md','PLAN-UA.md','CONTENT-PACKS-UA.md','publication-receipt.json','model-map.json','existing-content-audit.json','Baseline/campaign.json']:
  public_template=public_template.replace('href="'+name+'"','href="'+source_base+name+'"')
 public_template=public_template.replace('href="../../Roadmap/Paused/2026-10-11/README-UA.md"','href="https://github.com/kruty1918dev-ai/quiet-camp/blob/main/Design/Roadmap/Paused/2026-10-11/README-UA.md"')
 public_template=public_template.replace('локальний HTML-звіт','HTML-звіт кампанії')
 public_template=public_template.replace('href="../../../README.md"','href="../"')
 public_template=public_template.replace('href="../../../docs/images/brand-icon.png"','href="../images/brand-icon.png"')
 public_template=public_template.replace('__DATA__',json.dumps(public_data,ensure_ascii=False,separators=(',',':')).replace('</','<\\/'))
 (site/'index.html').write_text(public_template)
 (site/'image-source-index.json').write_text(json.dumps(image_index,indent=2)+'\n')
 print(json.dumps(data['counts']))
if __name__=='__main__':main()
