"""Validate/apply the reviewed ten through the existing history/conflict boundary."""
import json,subprocess,sys,shutil,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];PROJECT=ROOT/'QuietCamp';QA=Path('C:/Users/Public/qcui-20261006/QuietCamp');STAGE=QA/'Temp/ai/OpeningCampaign'
DEST=ROOT/'Design/LevelAuthoring/OpeningCampaign';VERIFY=DEST/'Verification';VERIFY.mkdir(parents=True,exist_ok=True)
before={k.replace(chr(92),'/'):v for k,v in json.loads((STAGE/'before-publication.json').read_text(encoding='utf-8')).items()}
def request(mode,**values):
 args=[sys.executable,str(ROOT/'tools/request_authoring_props.py'),'--project',str(QA),'--mode',mode]
 for key,value in values.items():args+=['--'+key,str(value)]
 r=subprocess.run(args,capture_output=True,text=True,encoding='utf-8')
 if r.returncode:raise RuntimeError(r.stdout+r.stderr)
 return json.loads((QA/'Temp/ai/qc007-art-result.json').read_text(encoding='utf-8'))
proof=request('authoring-workspace-status');(VERIFY/'workspace-before.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2),encoding='utf-8')
originals={};sources={};changed=[];rules=[]
for i in range(1,11):
 ident=f'QC{i:03}'
 for folder in ['Levels','ArtCompositions']:
  path=PROJECT/'Assets/QuietCamp/Resources/QuietCamp'/folder/(ident+'.json');key=path.relative_to(ROOT).as_posix()
  if hashlib.sha256(path.read_bytes()).hexdigest()!=before[key]:raise RuntimeError('Campaign source changed: '+key)
  sources[key]=path.read_bytes();originals[key]=json.loads(path.read_text(encoding='utf-8-sig'))
 old=originals[f'QuietCamp/Assets/QuietCamp/Resources/QuietCamp/Levels/{ident}.json']
 draft=json.loads((STAGE/'C07'/(ident+'.draft.json')).read_text(encoding='utf-8'))
 keys=['ruleVersion','width','height','entry','accessPoints','blocked','shade','noise','exteriorWalkable','guests','friends','witness','objects','canopies']
 assert all(old.get(k)==draft['level'].get(k) for k in keys),ident+' changed puzzle rules'
 rules.append(dict(id=ident,unchanged=keys))
 request('opening-apply',draft=STAGE/'C07'/(ident+'.draft.json'));print('Validated/applied in QA:',ident,flush=True)
 for folder in ['Levels','ArtCompositions']:
  path=Path('Assets/QuietCamp/Resources/QuietCamp')/folder/(ident+'.json');changed.append(path)
 changed.append(Path('Assets/QuietCamp/Authoring/CampaignLevels')/(ident+'.draft.json'))
 # Copy only newly needed history. Never overwrite existing user revision files.
 for folder in ['ContentRevisions','ArtRevisions']:
  path=Path('Assets/QuietCamp/Resources/QuietCamp')/folder/(old['contentHash']+'.json')
  if not (PROJECT/path).exists():changed.append(path)
# QA is staged first. All ten succeeded before replacing any repository level.
backups={path:(PROJECT/path).read_bytes() if (PROJECT/path).exists() else None for path in changed}
try:
 for path in changed:
  target=PROJECT/path;target.parent.mkdir(parents=True,exist_ok=True);pending=target.with_suffix(target.suffix+'.opening-pending');pending.write_bytes((QA/path).read_bytes());pending.replace(target)
  meta=Path(str(QA/path)+'.meta')
  if meta.exists() and not Path(str(target)+'.meta').exists():shutil.copy2(meta,Path(str(target)+'.meta'))
except Exception:
 for path,data in backups.items():
  target=PROJECT/path
  if data is not None:target.write_bytes(data)
  elif target.exists():target.unlink()
 raise
for key,old_hash in before.items():
 if key=='userStatus' or key in sources:continue
 assert hashlib.sha256((ROOT/key).read_bytes()).hexdigest()==old_hash,'Unselected content changed: '+key
(VERIFY/'publication.json').write_text(json.dumps(dict(status='published',levels=10,otherContentUnchanged=True,unchangedRules=rules,ownedFiles=[(Path('QuietCamp')/p).as_posix() for p in changed],idsPreserved=True,historyPreserved=True),ensure_ascii=False,indent=2),encoding='utf-8')
output=DEST/'Final';output.mkdir(exist_ok=True)
for i in range(1,11):
 ident=f'QC{i:03}';request('capture',level=ident,output=output,name=ident);print('Captured published level:',ident,flush=True)
