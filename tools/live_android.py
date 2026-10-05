#!/usr/bin/env python3
"""Prepare the real project for Android Tools, which require an ASCII project path."""
from pathlib import Path
import argparse
import json
import re
import shutil
import subprocess
ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'QuietCamp'
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--target',type=Path,default=Path('/tmp/quietcamp-live-build/QuietCamp'),help='Stable ASCII build directory; use a cache path for long builds.')
parser.add_argument('--menu-ui-qa',action='store_true',help='Use an isolated Android app identity and inject the passive menu test probe. Does not change the source project.')
args=parser.parse_args()
TARGET=args.target.expanduser().resolve()
if not str(TARGET).isascii():
    parser.error('Android build tools require an ASCII target path.')
TARGET.mkdir(parents=True,exist_ok=True)
for name in ('Assets','ProjectSettings','Packages'):
    (TARGET/name).mkdir(exist_ok=True)
    subprocess.run(['rsync','-a','--delete',str(SOURCE/name)+'/',str(TARGET/name)+'/'],check=True)
if not (TARGET/'Library').exists():
    subprocess.run(['rsync','-a','--exclude=SourceAssetDB-lock','--exclude=ArtifactDB-lock',str(SOURCE/'Library')+'/',str(TARGET/'Library')+'/'],check=True)
manifest=TARGET/'Packages/manifest.json'
data=json.loads(manifest.read_text())
for key,value in data['dependencies'].items():
    if value.startswith('file:../../../'):
        data['dependencies'][key]='file:'+str(ROOT.parent/value.split('/')[-1])
manifest.write_text(json.dumps(data,indent=2)+'\n')
if args.menu_ui_qa:
    settings=TARGET/'ProjectSettings/ProjectSettings.asset'
    text=settings.read_text()
    text=re.sub(r'(?m)^  productName:.*$', '  productName: Quiet Camp UI QA', text)
    text=re.sub(r'(?m)(^  applicationIdentifier:\n    Android: )[^\n]+',
                r'\g<1>com.kruty1918.quietcamp.uiqa', text, count=1)
    settings.write_text(text)
    fixture=TARGET/'Assets/UiDeviceQa'
    fixture.mkdir(exist_ok=True)
    shutil.copyfile(ROOT/'tools/qa/MenuDeviceProbe.cs',fixture/'MenuDeviceProbe.cs')
print(TARGET)
