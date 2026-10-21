"""Read-only external review backend for WorldOps. Never publishes authoring data."""
import hashlib,json,re,shutil,subprocess,sys,time
from pathlib import Path

def original_prefabs(repo,output):
 folder=output/'prefab-sources';folder.mkdir(parents=True,exist_ok=True)
 catalog=json.loads((repo/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp/asset_catalog.json').read_text());materials={};prefabs={}
 def read(path):
  local=repo/path
  return local.read_bytes() if local.is_file() else subprocess.check_output(['git','-C',str(repo),'show','HEAD:'+path])
 for item in catalog['models']:
  asset=Path(item.get('prefab','')).stem
  if asset not in ['campfire_stones','log','stone_largeA','stump_round','tree_default','tent_detailedOpen','tent_smallOpen']:continue
  source='QuietCamp/'+item['sourceUnity'];data=read(source);(folder/Path(source).name).write_bytes(data)
  for line in data.decode().splitlines():
   if line.startswith('mtllib '):
    path=str(Path(source).parent/line.split(' ',1)[1]);mtl=read(path);(folder/Path(path).name).write_bytes(mtl)
    for row in mtl.decode().splitlines():
     if row.startswith('newmtl '):
      name=row.split(' ',1)[1];text=read('QuietCamp/Assets/QuietCamp/Materials/'+name+'.mat').decode();m=re.search(r'_BaseColor: \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)',text)
      if not m:raise ValueError('Unresolved actual material '+name)
      materials[name]=list(map(float,m.groups()))
  text=read('QuietCamp/'+item['prefab']).decode();blocks=text.split('--- !u!4 ');visual=next((b for b in blocks if re.search(r'm_LocalScale: \{x: (?!1,)',b)),None)
  def vec(key):return list(map(float,re.search(key+r': \{x: ([^,]+), y: ([^,]+), z: ([^}]+)',visual).groups()))
  prefabs[asset]={'file':Path(source).name,'position':vec('m_LocalPosition') if visual else [0,0,0],'scale':vec('m_LocalScale') if visual else [item['uniformScaleOBJ']]*3,'sourcePath':source}
 (folder/'materials.json').write_text(json.dumps(materials,indent=2)+'\n');(folder/'prefabs.json').write_text(json.dumps(prefabs,indent=2)+'\n')

def run(args):
 repo=Path(__file__).resolve().parents[1];output=args.output.resolve();project=args.project.resolve()
 if output==project or output.is_relative_to(project/'Assets') or output.is_relative_to(repo/'QuietCamp/Assets'):raise SystemExit('Diagnostic output must be outside Unity Assets; no authoring writes are allowed.')
 resources=(args.resource_root or project/'Assets/QuietCamp/Resources').resolve()
 if output.is_relative_to(resources):raise SystemExit('Output must not be inside the input Resources tree.')
 if args.orders and (len(set(args.orders))!=len(args.orders) or any(n<1 or n>100 for n in args.orders)):raise SystemExit('Campaign orders must be unique integers from 1 to 100.')
 output.mkdir(parents=True,exist_ok=True)
 original_prefabs(repo,output)
 dotnet=args.dotnet or shutil.which('dotnet')
 if not dotnet:raise SystemExit('Install the free .NET10 SDK or pass --dotnet. Unity is neither required nor launched.')
 cmd=[dotnet,'run','--project',str(repo/'tools/world-audit/Export.csproj'),'--',str(resources),str(output/'bundles')]
 if args.orders:cmd.append(','.join(map(str,args.orders)))
 if getattr(args,"composition_candidates",False):cmd.extend(["--composition-candidates","--repair-kinds="+args.repair_kinds])
 sources=sorted(set(list((repo/'tools/world-audit').glob('*.cs'))+list((repo/'tools/world-audit').glob('*.csproj'))+list((repo/'QuietCamp/Assets/QuietCamp/Scripts/Domain').glob('*.cs'))+[repo/'QuietCamp/Assets/QuietCamp/Editor/LevelAuthoring/WorldOps/WorldOpsCompositionRepairs.cs']+[repo/'QuietCamp/Assets/QuietCamp/Scripts/Infrastructure'/n for n in ['CampArtDefinition.cs','FrozenLandscape.cs','WorldWellShafts.cs','RiverSurfaceField.cs','SeasonAtmosphereCatalog.cs']]+[repo/'QuietCamp/Assets/QuietCamp/Scripts/Presentation/World'/n for n in ['SeasonModelAppearance.cs','SeasonProfile.cs','SeasonPalette.cs','SeasonalTreeGeometry.cs']]))
 sources=[p for p in sources if p.name not in ('CompositionChecks.cs','WellChecks.cs')]
 source_hashes={str(p.relative_to(repo)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}
 (output/'managed-source-manifest.json').write_text(json.dumps(source_hashes,indent=2)+'\n')
 if not args.skip_export:subprocess.run(cmd,check=True,cwd=repo)
 if any(hashlib.sha256((repo/p).read_bytes()).hexdigest()!=sha for p,sha in source_hashes.items()):raise SystemExit('Managed geometry sources changed during export; discard this mixed run.')
 scripts=output/'scripts';scripts.mkdir(exist_ok=True);renderer=repo/'tools/world-audit/render_blender.py';frozen=scripts/'render-frozen.py';shutil.copy2(renderer,frozen)
 receipt={'schemaVersion':1,'mode':'read-only-external-diagnostic','unityLaunched':False,'playerBuild':False,'authoringPublished':False,'nativeVisualAcceptance':False,'repositoryHead':subprocess.check_output(['git','-C',str(repo),'rev-parse','HEAD'],text=True).strip(),'resourceRoot':str(resources),'rendererSha256':hashlib.sha256(frozen.read_bytes()).hexdigest(),'orders':args.orders or list(range(1,101))}
 (output/'external-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
 if args.render:
  blender=args.blender or shutil.which('blender')
  if not blender:raise SystemExit('Blender4.3+ required for rendering. Export is complete; no Unity request was made.')
  subprocess.run([blender,'--background','--threads','2','--python-exit-code','1','--python',str(repo/'tools/world-audit/render_batch.py'),'--',str(output/'bundles'),str(output/'baseline'),str(repo),str(frozen),','.join(map(str,args.orders or range(1,101)))],check=True,cwd=repo)
 return 0
