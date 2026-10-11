#!/usr/bin/env python3
"""Small, serial Roslyn contracts. No Unity/MSBuild/NuGet; bounded managed heaps."""
import argparse, hashlib, json, os, pathlib, subprocess, tempfile, time

ROOT=pathlib.Path(__file__).resolve().parents[1]
SLUGS=('scene-placement','mesh-style','model-weathering','fence-authoring','road-authoring','power-lines','rock-placement','vegetation-placement')
PACKAGES=ROOT/'QuietCamp/Packages'

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--only',choices=SLUGS);parser.add_argument('--integration',action='store_true');parser.add_argument('--receipt',type=pathlib.Path)
    args=parser.parse_args();chosen=('integration',) if args.integration else (args.only,) if args.only else SLUGS
    status=pathlib.Path.home()/'.local/state/sys-guard/status'
    if status.exists() and any(word in status.read_text() for word in ('STATE=hard','STATE=crit')):raise SystemExit('Postponed: host hard/crit state')
    sdk=pathlib.Path('/usr/lib/dotnet/sdk/10.0.112/Roslyn/bincore/csc.dll')
    refs=sorted(pathlib.Path('/usr/lib/dotnet/packs/Microsoft.NETCore.App.Ref').glob('10.0.*/ref/net10.0'))[-1]
    runtime=subprocess.check_output(['dotnet','--list-runtimes'],text=True).splitlines()
    version=next(row.split()[1] for row in runtime if row.startswith('Microsoft.NETCore.App 10.'))
    env=dict(os.environ,DOTNET_GCHeapHardLimit='0x06000000',DOTNET_GCConserveMemory='9',DOTNET_gcServer='0',DOTNET_CLI_TELEMETRY_OPTOUT='1')
    receipts=[]
    with tempfile.TemporaryDirectory(prefix='qc-environment-core-') as work:
        work=pathlib.Path(work)
        for slug in chosen:
            folder=PACKAGES/f'com.kruty1918.{slug}'
            if slug=='integration':sources=[ROOT/'tools/EnvironmentIntegrationProbe.cs']+sorted(PACKAGES.glob('com.kruty1918.*/Runtime/Core/*.cs'))
            else:
                sources=sorted((folder/'Runtime/Core').glob('*.cs'))+[folder/'Samples~/Example.cs',folder/'Tests~/Contracts.cs']
                if slug!='scene-placement':sources+=sorted((PACKAGES/'com.kruty1918.scene-placement/Runtime/Core').glob('*.cs'))
            dll=work/f'{slug}.dll';rsp=work/f'{slug}.rsp'
            rsp.write_text('\n'.join(['-nologo','-target:exe','-langversion:9.0','-nullable:disable','-out:'+str(dll)]+['-r:'+str(f) for f in sorted(refs.glob('*.dll'))]+[str(f) for f in sources])+'\n')
            started=time.time()
            command=['dotnet',str(sdk),'@'+str(rsp)]
            # /usr/bin/time measures the compiler's own maximum RSS; no shared server is started.
            rss_file=work/f'{slug}.rss';command=['/usr/bin/time','-f','%M','-o',str(rss_file)]+command
            compiled=subprocess.run(command,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=40)
            if compiled.returncode:raise SystemExit(compiled.stdout)
            (work/f'{slug}.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net10.0','framework':{'name':'Microsoft.NETCore.App','version':version}}}))
            tested=subprocess.run(['dotnet',str(dll)],env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=40)
            print(tested.stdout.strip(),flush=True)
            if tested.returncode:raise SystemExit(tested.returncode)
            item={'package':slug,'core_only':True,'native_verified':False,'compiler_peak_rss_kib':int(rss_file.read_text().strip()),'seconds':round(time.time()-started,3),'assertions':tested.stdout.strip(),'sources':{str(f.relative_to(ROOT)):hashlib.sha256(f.read_bytes()).hexdigest() for f in sources}}
            receipts.append(item)
    if args.receipt:
        args.receipt.parent.mkdir(parents=True,exist_ok=True)
        args.receipt.write_text(json.dumps({'captured_utc':time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime()),'checks':receipts,'unity_launched':False,'player_build':False},indent=2)+'\n')
    print(f'{len(receipts)} standalone Core contract(s) passed; native acceptance pending.')

if __name__=='__main__':main()
