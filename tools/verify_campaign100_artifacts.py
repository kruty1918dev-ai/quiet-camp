#!/usr/bin/env python3
"""Audit delivered evidence against current campaign/render sources; never a build."""
from pathlib import Path
import argparse,json,hashlib,datetime
from PIL import Image,ImageStat
from campaign100_capture_sources import current_manifest
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Design/Campaign100/2026-10-11'
def check(allow_incomplete=False):
 campaign=json.loads((OUT/'Authoring/campaign100.json').read_text())['levels']
 receipt=json.loads((OUT/'publication-receipt.json').read_text())
 models=json.loads((OUT/'model-map.json').read_text())
 audit=json.loads((OUT/'existing-content-audit.json').read_text())
 source=current_manifest();errors=[];pending=[];captured=[]
 def require(value,note):
  if not value:errors.append(note)
 require(len(campaign)==100 and len({l['id'] for l in campaign})==100,'Exactly 100 unique active IDs')
 require([l['number'] for l in campaign]==list(range(1,101)),'Contiguous campaign order')
 require(len({l['titleUk'] for l in campaign})==100,'100 distinct place names')
 require(len(audit['levels'])==274,'All 274 original levels audited')
 require(len(models)==1342 and len({m['number'] for m in models})==1342,'Stable model source IDs')
 published={l['id']:l['hash'] for l in receipt['content']}
 require(receipt['count']==100 and receipt['validator'] and receipt['witness'] and receipt['independentSolver'],'Validated publication receipt')
 for l in campaign:
  file=ROOT/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp'/('GeneratedLevels' if l['id'].startswith('gen:') else 'Levels')/(l['id'].replace(':','_')+'.json')
  level=json.loads(file.read_text());require(level['contentHash']==published[l['id']],l['id']+' changed after publication')
  require(bool(l['storyUk'] and l['whyUk'] and l['detailsUk']),l['id']+' missing story/composition explanation')
  require(abs(level['environment']['seasonTimeline']-(l['number']-1)/99)<1e-6,l['id']+' broken seasonal gradient')
  for orientation,size in [('landscape',(1280,800)),('portrait',(720,1600))]:
   stem=f'level-{l["number"]:03d}-{orientation}';png=OUT/'Renders'/(stem+'.png');meta=png.with_suffix('.json')
   if not png.is_file() or not meta.is_file():pending.append(stem+' absent');continue
   proof=json.loads(meta.read_text())
   require(proof['imageSha256']==hashlib.sha256(png.read_bytes()).hexdigest(),stem+' PNG checksum mismatch')
   require(proof['levelId']==l['id'] and proof['orientation']==orientation,stem+' wrong identity')
   with Image.open(png) as image:
    require(image.size==size,stem+' wrong resolution')
    image.thumbnail((96,96));require(max(ImageStat.Stat(image).stddev)>3,stem+' nearly empty frame')
   require(not proof['playerBuild'] and not proof['mobileFpsMeasured'],stem+' misleading evidence kind')
   if proof['contentHash']!=level['contentHash'] or proof.get('sourceRevision')!=source['revision']:
    pending.append(stem+' stale');continue
   expected={p['id'] for b in level['environmentalStory']['beats'] for p in b['props']}
   require(expected=={p['id'] for p in proof['props']},stem+' incomplete authored prop list')
   captured.append(stem)
 sources={m['number']:m for m in audit['modelSources']}
 def file_hash(path):
  sha=hashlib.sha256()
  with path.open('rb') as stream:
   for block in iter(lambda:stream.read(1024*1024),b''):sha.update(block)
  return sha.hexdigest()
 for m in models:
  require(m['sourceSha256']==sources[m['number']]['sourceSha256'],m['number']+' mismatched source hash')
  model_path=ROOT/'QuietCamp'/sources[m['number']].get('photographySource',m['path'])
  require(model_path.is_file(),m['number']+' source model absent')
  if model_path.is_file():require(file_hash(model_path)==m['sourceSha256'],m['number']+' source changed after photography')
  require(len(m['images'])==2,m['number']+' missing source angles')
  for name in m['images']:
   path=OUT/name;require(path.is_file(),m['number']+' local photograph absent')
   if path.is_file():
    with Image.open(path) as pic:require(min(pic.size)>50,m['number']+' invalid photograph')
 require(hashlib.sha256((ROOT/'QuietCamp/Assets/QuietCamp/Settings/URP/QC_Renderer.asset').read_bytes()).hexdigest()=='a1a30b94df5b0fa694c556a101a692e96055150bd99525b25d0db9321f0611bd','User renderer asset changed')
 if not allow_incomplete:require(len(captured)==200,'All 200 current native scene frames required')
 result=dict(result='Failed' if errors else 'Pending' if pending else 'Passed',verifiedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),sourceRevision=source['revision'],activeLevels=100,inspectedOldLevels=274,modelSources=1342,matchingNativeFrames=len(captured),expectedNativeFrames=200,pendingFrames=pending,errors=errors,playerBuild=False,mobileFpsMeasured=False)
 (OUT/'Verification/artifact-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
 print(json.dumps({k:v for k,v in result.items() if k!='pendingFrames'},ensure_ascii=False));return bool(errors)
if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--allow-incomplete',action='store_true');args=parser.parse_args()
 raise SystemExit(check(args.allow_incomplete))
