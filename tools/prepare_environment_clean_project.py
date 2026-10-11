#!/usr/bin/env python3
"""Prepare a small synthetic Unity project. Does NOT launch/import/build Unity."""
import argparse, hashlib, json, pathlib, shutil
from verify_environment_cores import ROOT, PACKAGES, SLUGS

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--output',type=pathlib.Path,required=True);parser.add_argument('--only',choices=SLUGS)
    args=parser.parse_args();target=args.output.resolve()
    if target.exists() and not (target/'environment-tools-clean-project.json').exists():raise SystemExit('Refusing to overwrite an unrelated project')
    target.mkdir(parents=True,exist_ok=True)
    selected=(args.only,) if args.only=='scene-placement' else ('scene-placement',args.only) if args.only else SLUGS
    manifest={'dependencies':{'com.unity.test-framework':'1.8.0'},'testables':[f'com.kruty1918.{slug}' for slug in selected]}
    sources={}
    for slug in selected:
        origin=PACKAGES/f'com.kruty1918.{slug}';destination=target/'Packages'/origin.name
        if destination.exists():shutil.rmtree(destination)
        shutil.copytree(origin,destination,ignore=shutil.ignore_patterns('bin','obj','.git','VENDORED.json'))
        for file in sorted(origin.rglob('*')):
            if file.is_file() and not any(p in ('bin','obj','.git') for p in file.parts):sources[str(file.relative_to(ROOT))]=hashlib.sha256(file.read_bytes()).hexdigest()
    (target/'Packages/manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    (target/'ProjectSettings').mkdir(exist_ok=True);shutil.copy2(ROOT/'QuietCamp/ProjectSettings/ProjectVersion.txt',target/'ProjectSettings/ProjectVersion.txt')
    (target/'Assets').mkdir(exist_ok=True)
    (target/'environment-tools-clean-project.json').write_text(json.dumps({'quiet_camp_assets':False,'unity_launched':False,'native_verified':False,'packages':list(selected),'sources':sources},indent=2)+'\n')
    print(f'Prepared {target}; no Unity launch, no native pass claimed.')

if __name__=='__main__':main()
