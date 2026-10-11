#!/usr/bin/env python3
"""Serial bounded cached-reference compilation, NOT Unity import/test execution."""
import argparse, hashlib, json, os, pathlib, re, subprocess, tempfile, time
from verify_environment_cores import ROOT, PACKAGES, SLUGS

NAMES=dict(zip(SLUGS,('ScenePlacement','MeshStyle','ModelWeathering','FenceAuthoring','RoadAuthoring','PowerLines','RockPlacement','VegetationPlacement')))
PROJECT=ROOT/'QuietCamp'

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--receipt',type=pathlib.Path);args=parser.parse_args()
    status=pathlib.Path.home()/'.local/state/sys-guard/status'
    if status.exists() and any(s in status.read_text() for s in ('STATE=hard','STATE=crit')):raise SystemExit('Postponed: host hard/crit state')
    compiler=pathlib.Path('/usr/lib/dotnet/sdk/10.0.112/Roslyn/bincore/csc.dll')
    bee=PROJECT/'Library/Bee/artifacts';templates={}
    for category,name in [('runtime','QuietCamp.Presentation'),('editor','QuietCamp.Tests.Editor')]:
        candidates=list(bee.glob('*/'+name+'.rsp'))
        if not candidates:raise SystemExit('Missing genuine Unity compiler response: '+name)
        templates[category]=max(candidates,key=lambda p:p.stat().st_mtime)
    env=dict(os.environ,DOTNET_GCHeapHardLimit='0x06000000',DOTNET_GCConserveMemory='9',DOTNET_gcServer='0',DOTNET_CLI_TELEMETRY_OPTOUT='1')
    compiled={};checks=[]
    with tempfile.TemporaryDirectory(prefix='qc-environment-unity-sources-') as work:
        work=pathlib.Path(work)
        for slug in SLUGS:
            folder=PACKAGES/f'com.kruty1918.{slug}';ns=NAMES[slug]
            for layer,relative in [('Core','Runtime/Core'),('Unity','Runtime/Unity'),('Editor','Editor'),('Tests','Tests/Editor')]:
                name=f'Kruty1918.{ns}.{layer}';options=[];template=templates['runtime' if layer in ('Core','Unity') else 'editor']
                for line in template.read_text().splitlines():
                    if line.startswith(('-out:','-refout:','/additionalfile:','-analyzer:')) or line.strip('"').endswith('.cs'):continue
                    if line.startswith('-r:'):
                        ref=pathlib.Path(line[3:].strip('"'));stem=ref.name.removesuffix('.ref.dll').removesuffix('.dll')
                        if stem.startswith(('QuietCamp.','Kruty1918.')):continue
                        if not ref.is_absolute():ref=PROJECT/ref
                        if not ref.exists():raise SystemExit('Missing cached reference: '+str(ref))
                        line='-r:"'+str(ref)+'"'
                    options.append(line)
                declaration=json.loads((folder/relative/(name+'.asmdef')).read_text())
                # Bee only lists engine modules used by the old assembly. New adapters
                # also use Terrain and PackageManager; use genuine installed modules.
                managed=pathlib.Path('/home/oleks/Unity/Hub/Editor/6000.6.2f1/Editor/Data/Managed/UnityEngine')
                present={pathlib.Path(line[3:].strip('"')).name for line in options if line.startswith('-r:')}
                for module in sorted(managed.glob('*.dll')):
                    if module.name not in present and (layer in ('Editor','Tests') or module.name.startswith('UnityEngine.')):options.append('-r:"'+str(module)+'"')
                for ref in declaration.get('references',[]):
                    if ref in compiled:options.append('-r:"'+str(compiled[ref])+'"')
                    else:
                        cached=PROJECT/'Library/ScriptAssemblies'/(ref+'.dll')
                        if cached.exists():options.append('-r:"'+str(cached)+'"')
                        else:raise SystemExit('Missing assembly dependency: '+ref)
                target=work/(name+'.dll');sources=sorted((folder/relative).glob('*.cs'))
                options+=['-out:"'+str(target)+'"','-target:library']+['"'+str(f)+'"' for f in sources]
                rsp=work/(name+'.rsp');rsp.write_text('\n'.join(options)+'\n')
                done=subprocess.run(['dotnet',str(compiler),'@'+str(rsp)],cwd=PROJECT,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=40)
                if done.returncode:raise SystemExit(done.stdout)
                warnings=done.stdout.count(': warning ');print(f'PASS cached-reference compilation: {name} ({warnings} warnings)',flush=True)
                compiled[name]=target;checks.append({'assembly':name,'warnings':warnings,'native_verified':False,'sources':{str(f.relative_to(ROOT)):hashlib.sha256(f.read_bytes()).hexdigest() for f in sources}})
    if args.receipt:
        args.receipt.parent.mkdir(parents=True,exist_ok=True);args.receipt.write_text(json.dumps({'captured_utc':time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime()),'checks':checks,'unity_launched':False,'native_verified':False,'player_build':False},indent=2)+'\n')
    print('32 assemblies compiled against cached Unity references; no Unity import/native tests/player build.')

if __name__=='__main__':main()
