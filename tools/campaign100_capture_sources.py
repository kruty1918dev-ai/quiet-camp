#!/usr/bin/env python3
"""Version the sources used by native campaign photography, including shaders."""
from pathlib import Path
import json,hashlib,datetime
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Design/Campaign100/2026-10-11'
ASSETS=ROOT/'QuietCamp/Assets'
def current_manifest():
 files=set((ASSETS/'QuietCamp/Scripts/Presentation/World').rglob('*.cs'))
 files.add(ASSETS/'QuietCamp/Scripts/Presentation/UI/RoadmapModelLibrary.cs')
 files.add(ASSETS/'QuietCamp/Tests/PlayMode/Campaign100CaptureTests.cs')
 files.update((ASSETS/'QuietCamp/Settings/URP').glob('*.asset'))
 for folder in ['QuietCamp/Resources','Resources']:
  directory=ASSETS/folder
  for path in directory.rglob('*'):
   if path.suffix not in ['.shader','.mat','.json','.asset','.bytes']:continue
   if any(part in ['Levels','GeneratedLevels','ContentRevisions','Fonts'] for part in path.parts):continue
   files.add(path)
 rows=[dict(path='Assets/'+str(p.relative_to(ASSETS)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in sorted(files)]
 revision=hashlib.sha256(json.dumps(rows,separators=(',',':')).encode()).hexdigest()
 return dict(revision=revision,files=rows)
def main():
 data=current_manifest();data['preparedUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
 (OUT/'capture-source-manifest.json').write_text(json.dumps(data,indent=2)+'\n')
 print('Native source revision:',data['revision'],'files:',len(data['files']))
if __name__=='__main__':main()
