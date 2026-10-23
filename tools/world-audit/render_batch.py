"""Runs a frozen read-only Blender renderer, continuing and recording failures."""
import sys,runpy,json,time,hashlib,traceback,gc
from pathlib import Path
args=sys.argv[sys.argv.index('--')+1:];bundles=Path(args[0]);output=Path(args[1]);repo=Path(args[2]);renderer=Path(args[3]);output.mkdir(parents=True,exist_ok=True)
orders=set(map(int,args[4].split(','))) if len(args)>4 and args[4] else None
selected=[f for f in sorted(bundles.glob('*.json.gz')) if orders is None or int(f.name[:3]) in orders];state={'started':time.time(),'completed':[],'resumed':[],'failed':[],'rendererSha256':hashlib.sha256(renderer.read_bytes()).hexdigest()}
for f in selected:
 try:
  existing=output/(f.stem.split('.')[0]+'-inventory.json')
  if existing.is_file():
   old=json.loads(existing.read_text(encoding="utf-8-sig"));frames=old.get('frames',[])
   if frames and old.get('rendererSourceSha256')==state['rendererSha256'] and old.get('bundleSha256')==hashlib.sha256(f.read_bytes()).hexdigest() and all((output/fr['path']).is_file() and (not fr.get('sha256') or hashlib.sha256((output/fr['path']).read_bytes()).hexdigest()==fr['sha256']) for fr in frames):
    state['completed'].append(int(f.name[:3]));state['resumed'].append(int(f.name[:3]));(output/'progress.json').write_text(json.dumps(state,indent=2)+'\n', encoding="utf-8");print('BATCH_RESUME',f.name,flush=True);continue
  assert hashlib.sha256(renderer.read_bytes()).hexdigest()==state['rendererSha256'],'Renderer changed during capture'
  sys.argv=['blender','--',str(f),str(output),str(repo),'BLENDER_WORKBENCH'];runpy.run_path(str(renderer),run_name='__main__')
  record=output/(f.stem.split('.')[0]+'-inventory.json');d=json.loads(record.read_text(encoding="utf-8-sig"));
  for frame in d['frames']:frame['sha256']=hashlib.sha256((output/frame['path']).read_bytes()).hexdigest()
  d['rendererSourceSha256']=state['rendererSha256'];d['bundleSha256']=hashlib.sha256(f.read_bytes()).hexdigest();record.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n', encoding="utf-8")
  state['completed'].append(int(f.name[:3]))
 except Exception as e:traceback.print_exc();state['failed'].append({'file':f.name,'error':str(e)})
 (output/'progress.json').write_text(json.dumps(state,indent=2)+'\n', encoding="utf-8");print('BATCH_PROGRESS',len(state['completed']),len(state['failed']),flush=True);gc.collect()
if state['failed']:raise SystemExit(1)
