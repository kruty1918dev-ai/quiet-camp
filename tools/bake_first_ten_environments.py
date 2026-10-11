#!/usr/bin/env python3
"""Serial bounded Core bake; no Editor, player build, or runtime publication."""
import argparse,json,os,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--compile-only',action='store_true');parser.add_argument('--validate-layouts',action='store_true');args=parser.parse_args()
    status=(Path.home()/'.local/state/sys-guard/status').read_text()
    available=int(status.split('MEM_AVAIL_MB=')[1].split()[0])
    if (available<1500 and not (args.compile_only or args.validate_layouts)) or any(x in status for x in ('STATE=hard','STATE=crit')):raise SystemExit('Bake postponed: require 1500 MB available RAM')
    refs=sorted(Path('/usr/lib/dotnet/packs/Microsoft.NETCore.App.Ref').glob('10.0.*/ref/net10.0'))[-1]
    json_dll=sorted((Path.home()/'.nuget/packages/newtonsoft.json/13.0.3/lib/net6.0').glob('*.dll'))[0]
    sources=[ROOT/'tools/FirstTenEnvironmentBake.cs']+sorted((ROOT/'QuietCamp/Packages').glob('com.kruty1918.*/Runtime/Core/*.cs'))
    env=dict(os.environ,DOTNET_GCHeapHardLimit='0x06000000' if args.compile_only or args.validate_layouts else '0x0c000000',DOTNET_GCConserveMemory='9',DOTNET_gcServer='0')
    with tempfile.TemporaryDirectory(prefix='qc-first-ten-bake-') as directory:
        p=Path(directory);dll=p/'bake.dll';rsp=p/'compile.rsp'
        rsp.write_text('\n'.join(['-nologo','-parallel-','-target:exe','-langversion:9.0','-out:'+str(dll)]+['-r:'+str(x) for x in refs.glob('*.dll')]+['-r:'+str(json_dll)]+[str(x) for x in sources])+'\n')
        subprocess.run(['dotnet','/usr/lib/dotnet/sdk/10.0.112/Roslyn/bincore/csc.dll','@'+str(rsp)],env=env,check=True)
        if args.compile_only:
            print('PASS first-ten bake adapter compiles; no bake, Unity, publication or player build.')
            return
        import shutil
        shutil.copy(json_dll,p/'Newtonsoft.Json.dll')
        (p/'bake.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net10.0','framework':{'name':'Microsoft.NETCore.App','version':'10.0.2'}}}))
        subprocess.run(['dotnet',str(dll),str(ROOT)]+(['--validate-layouts'] if args.validate_layouts else []),env=env,check=True)

if __name__=='__main__':main()
