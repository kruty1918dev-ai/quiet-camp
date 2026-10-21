"""Runs a frozen read-only Blender renderer, continuing and recording failures."""
import sys,runpy,json,time,hashlib,traceback,gc
from pathlib import Path
args=sys.argv[sys.argv.index('--')+1:];bundles=Path(args[0]);output=Path(args[1]);repo=Path(args[2]);renderer=Path(args[3]);output.mkdir(parents=True,exist_ok=True)
selected=sorted(bundles.glob('*.json.gz'));state={'started':time.time(),'completed':[],'failed':[],'rendererSha256':hashlib.sha256(renderer.read_bytes()).hexdigest()}
for f in selected:
 try:
  assert hashlib.sha256(renderer.read_bytes()).hexdigest()==state['rendererSha256'],'Renderer changed during capture'
  sys.argv=['blender','--',str(f),str(output),str(repo),'BLENDER_WORKBENCH'];runpy.run_path(str(renderer),run_name='__main__')
  record=output/(f.stem.split('.')[0]+'-inventory.json');d=json.loads(record.read_text());d['rendererSourceSha256']=state['rendererSha256'];d['bundleSha256']=hashlib.sha256(f.read_bytes()).hexdigest();record.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
  state['completed'].append(int(f.name[:3]))
 except Exception as e:traceback.print_exc();state['failed'].append({'file':f.name,'error':str(e)})
 (output/'progress.json').write_text(json.dumps(state,indent=2)+'\n');print('BATCH_PROGRESS',len(state['completed']),len(state['failed']),flush=True);gc.collect()
if state['failed']:raise SystemExit(1)
