#!/usr/bin/env python3
"""Bounded autonomous real domain/save/solver checks, without MSBuild or Unity."""
import datetime,glob,hashlib,json,os,subprocess,tempfile,xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
REPORT=ROOT/'Design/Campaign100/2026-10-11/FirstTenRefinement'

def main():
    status=(Path.home()/'.local/state/sys-guard/status').read_text()
    if any(x in status for x in ('STATE=hard','STATE=crit')):raise SystemExit('Postponed: host hard/crit')
    project=ROOT/'tools/MonetizationProbe.csproj';sources=set()
    for node in ET.parse(project).iter('Compile'):
        for pattern in node.attrib['Include'].split(';'):
            sources.update(Path(p).resolve() for p in glob.glob(str(project.parent/pattern),recursive=True))
    refs=sorted(Path('/usr/lib/dotnet/packs/Microsoft.NETCore.App.Ref').glob('10.0.*/ref/net10.0'))[-1]
    newtonsoft=Path.home()/'.nuget/packages/newtonsoft.json/13.0.3/lib/net6.0/Newtonsoft.Json.dll'
    env=dict(os.environ,DOTNET_GCHeapHardLimit='0x0c000000',DOTNET_GCConserveMemory='9',DOTNET_gcServer='0',DOTNET_CLI_TELEMETRY_OPTOUT='1')
    with tempfile.TemporaryDirectory(prefix='qc-first-ten-domain-') as directory:
        p=Path(directory);rsp=p/'compile.rsp';dll=p/'probe.dll'
        rsp.write_text('\n'.join(['-nologo','-parallel-','-target:exe','-langversion:9.0','-define:QC_MONETIZATION_PROBE','-out:'+str(dll)]+['-r:'+str(x) for x in refs.glob('*.dll')]+['-r:'+str(newtonsoft)]+[str(x) for x in sorted(sources)])+'\n')
        subprocess.run(['dotnet','/usr/lib/dotnet/sdk/10.0.112/Roslyn/bincore/csc.dll','@'+str(rsp)],env=env,check=True)
        import shutil
        shutil.copy(newtonsoft,p/'Newtonsoft.Json.dll')
        (p/'probe.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net10.0','framework':{'name':'Microsoft.NETCore.App','version':'10.0.2'}}}))
        result=subprocess.run(['dotnet',str(dll)],cwd=ROOT,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=300)
        print(result.stdout,flush=True);result.check_returncode()
        resources=ROOT/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp'
        data=dict(checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),result='Passed',sources={str(x.relative_to(ROOT)):hashlib.sha256(x.read_bytes()).hexdigest() for x in sorted(sources)},levels={p.stem:json.loads(p.read_text())['contentHash'] for p in list((resources/'Levels').glob('QC0*.json'))+list((resources/'GeneratedLevels').glob('*.json'))},output=result.stdout,unityLaunched=False,nativeVerified=False,playerBuild=False)
        (REPORT/'domain-contracts.json').write_text(json.dumps(data,indent=2)+'\n')

if __name__=='__main__':main()
