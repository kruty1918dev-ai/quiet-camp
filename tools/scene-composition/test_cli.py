"""Integration contracts in temporary authoring copies. Never patches production recipes."""
import hashlib,json,os,shutil,subprocess,tempfile
from pathlib import Path
source=Path('QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition')
with tempfile.TemporaryDirectory(prefix='qc-composition-contract-') as temporary:
 root=Path(temporary)/'source';shutil.copytree(source,root);env=dict(os.environ,QC_COMPOSITION_SOURCE=str(root))
 def run(*args,good=True):
  result=subprocess.run(['dotnet','run','--no-build','--project','tools/roadmap-pipeline/RoadmapPipeline.csproj','--','--composition',*args],env=env,text=True,capture_output=True)
  assert (result.returncode==0)==good,result.stdout+result.stderr
  return result.stdout
 document=root/'region-0.json';initial=document.read_bytes();d=json.loads(initial);entity=d['ensembles'][0]['id'];h=hashlib.sha256(initial).hexdigest();patch=Path(temporary)/'patch.json'
 patch.write_text(json.dumps({'fenceVariant':'picket'}));run('patch',entity,'--expected-hash','0'*64,'--patch-file',str(patch),good=False);assert document.read_bytes()==initial
 patch.write_text(json.dumps({'placement':{'zone':'missing'}}));run('patch',entity,'--expected-hash',h,'--patch-file',str(patch),good=False);assert document.read_bytes()==initial
 patch.write_text(json.dumps({'unsupportedField':True}));run('patch',entity,'--expected-hash',h,'--patch-file',str(patch),good=False);assert document.read_bytes()==initial
 patch.write_text(json.dumps({'fenceVariant':'picket'}));run('patch',entity,'--expected-hash',h,'--patch-file',str(patch));assert json.loads(document.read_text())['ensembles'][0]['fenceVariant']=='picket'
 run('patch',entity,'--expected-hash',h,'--patch-file',str(patch),good=False)
 inspected=json.loads(run('inspect',entity+'/house'));assert inspected['generated'] and inspected['patchTarget']==entity
 templates=root/'templates.json';th=hashlib.sha256(templates.read_bytes()).hexdigest();patch.write_text(json.dumps({'width':11.1}));run('patch','ua.homestead','--expected-hash',th,'--patch-file',str(patch));assert json.loads(templates.read_text())[0]['width']==11.1
 asset=root/'assets.json';ah=hashlib.sha256(asset.read_bytes()).hexdigest();patch.write_text(json.dumps({'height':-1}));run('patch','ua_whitewashed_house','--expected-hash',ah,'--patch-file',str(patch),good=False);assert hashlib.sha256(asset.read_bytes()).hexdigest()==ah
 run('bake');baked=(root/'bake-preview.json').read_bytes();first=json.loads(baked)['sourceHash'];run('bake');assert (root/'bake-preview.json').read_bytes()==baked
 patch.write_text(json.dumps({'state':'remembered'}));h2=hashlib.sha256(document.read_bytes()).hexdigest();run('patch',entity,'--expected-hash',h2,'--patch-file',str(patch));out=run('compose','--dry-run')
 diff=json.loads(out[out.rfind('\n{')+1:]);assert diff['sourceHash']!=first
 assert diff['changed'] and all(child.startswith(entity+'/') for child in diff['changed']),diff
 assert not diff['added'] and not diff['removed'],diff
 assert (root/'bake-preview.json').read_bytes()==baked
 broken=json.loads(document.read_text());broken['ensembles'][0]['placement']['zone']='missing';document.write_text(json.dumps(broken));run('bake',good=False);assert (root/'bake-preview.json').read_bytes()==baked
 print('PASS CLI contracts: stale patch, unknown fields, semantic rejection, valid patch, deterministic bake, dry-run and failed-bake retention')
