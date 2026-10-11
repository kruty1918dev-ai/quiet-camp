#!/usr/bin/env python3
"""Add installed package model sources, including unimported Samples~, without renumbering."""
import json,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Design/Campaign100/2026-10-11'
def main():
 p=OUT/'existing-content-audit.json';audit=json.loads(p.read_text());rows=audit['modelSources'];existing={r['path']:r for r in rows};number=max(int(r['number'][1:]) for r in rows)+1
 added=[]
 for source in sorted((ROOT/'QuietCamp/Library/PackageCache').rglob('*')):
  if source.suffix.lower() not in {'.fbx','.obj','.glb','.gltf','.blend','.dae','.3ds'}:continue
  relative=source.relative_to(ROOT/'QuietCamp/Library/PackageCache');package=relative.parts[0].split('@')[0]
  logical='Packages/'+package+'/'+str(Path(*relative.parts[1:]))
  if logical in existing:continue
  row=dict(number=f'M{number:04d}',path=logical,sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),existingNativeProof=False,
    needsCapture=True,container=source.suffix.lower(),previousImages=[],previousCaptureDate=None,
    photographySource=str(source.relative_to(ROOT/'QuietCamp')),packageSample='Samples~' in source.parts)
  rows.append(row);added.append(row);number+=1
 audit['modelSourceCount']=len(rows);audit['packageModelSources']=sum(r['path'].startswith('Packages/') for r in rows)
 audit['nativeModelCapturesNeeded']=sum(r['needsCapture'] for r in rows)
 p.write_text(json.dumps(audit,ensure_ascii=False,indent=2)+'\n')
 (OUT/'package-model-sources.json').write_text(json.dumps(added or [r for r in rows if r['path'].startswith('Packages/')],ensure_ascii=False,indent=2)+'\n')
 print('Package sources:',audit['packageModelSources'],'total:',len(rows))
if __name__=='__main__':main()
