#!/usr/bin/env python3
"""Bounded autonomous real domain/save/solver checks, without MSBuild or Unity."""
import argparse,datetime,glob,hashlib,json,os,shutil,subprocess,tempfile,xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
REPORT=ROOT/'Design/Campaign100/2026-10-11/FirstTenRefinement'

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--publication-contract',action='store_true');args=parser.parse_args()
    status=(Path.home()/'.local/state/sys-guard/status').read_text()
    if any(x in status for x in ('STATE=hard','STATE=crit')):raise SystemExit('Postponed: host hard/crit')
    if args.publication_contract:
        # Same bounded 192 MB domain process; the fixture copies small files
        # serially and never loads the whole environment into memory.
        disk=shutil.disk_usage(ROOT)
        if disk.free*10<=disk.total:raise SystemExit('Require >10% disk free')
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
        shutil.copy(newtonsoft,p/'Newtonsoft.Json.dll')
        (p/'probe.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net10.0','framework':{'name':'Microsoft.NETCore.App','version':'10.0.2'}}}))
        result=subprocess.run(['dotnet',str(dll)],cwd=ROOT,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=300)
        print(result.stdout,flush=True);result.check_returncode()
        resources=ROOT/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp'
        if args.publication_contract:
            fixture=p/'publication';design=Path('Design/Campaign100/2026-10-11');relative=Path('QuietCamp/Assets/QuietCamp/Resources/QuietCamp')
            shutil.copytree(resources,fixture/relative)
            shutil.copytree(ROOT/design/'Baseline',fixture/design/'Baseline')
            (fixture/design/'Authoring').mkdir(parents=True)
            shutil.copy2(ROOT/design/'Authoring/curated-gameplay-models.json',fixture/design/'Authoring/curated-gameplay-models.json')
            target=fixture/design/'FirstTenRefinement';(target/'Baked').mkdir(parents=True)
            pointer=json.loads((REPORT/'Baked/current.json').read_text());revision=pointer['revision']
            shutil.copy2(REPORT/'Baked/current.json',target/'Baked/current.json')
            shutil.copytree(REPORT/'Baked'/revision,target/'Baked'/revision)
            baked=json.loads((target/'Baked'/revision/'receipt.json').read_text())
            for path in baked['sources']:
                destination=fixture/path;destination.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/path,destination)
            def snapshots(folder):
                return {str(path.relative_to(folder)):path.read_bytes() for name in ('Levels','GeneratedLevels') for path in (folder/name).glob('*.json')}
            before=snapshots(resources);published=fixture/relative
            bad=target/'Baked'/revision/'QC005/groundcover.bytes'
            with bad.open('r+b') as f:
                f.seek(-1,2);value=f.read(1);f.seek(-1,2);f.write(bytes([value[0]^1]))
            rejected=subprocess.run(['dotnet',str(dll),'--author-first-ten',str(fixture)],cwd=fixture,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=300)
            assert rejected.returncode and 'Mesh checksum QC005' in rejected.stdout,rejected.stdout
            assert snapshots(published)==before and not (published/'BakedEnvironments').exists() and not (target/'runtime-publication.json').exists()
            with bad.open('r+b') as f:f.seek(-1,2);f.write(value)
            candidate=subprocess.run(['dotnet',str(dll),'--author-first-ten',str(fixture)],cwd=fixture,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=300)
            print(candidate.stdout,flush=True);candidate.check_returncode()
            after=snapshots(published);changes=[]
            for path,raw in before.items():
                old=json.loads(raw);current=json.loads(after[path]);n=old.get('order',0)
                if 1<=n<=10 and old['id'].startswith('QC') and old['id']!='QC_TEST':
                    assert current['bakedEnvironment'].endswith('/'+old['id']) and current['contentHash']!=old['contentHash']
                    for key in ['width','height','entry','objects','guests','shade','canopies','noise','friends','witness','accessPoints','exteriorWalkable']:
                        assert current.get(key)==old.get(key),(old['id'],key)
                    changes.append(old['id'])
                    for part in json.loads((published/'BakedEnvironments'/revision/old['id']/'manifest.json').read_text())['parts']:
                        assert hashlib.sha256((published/'BakedEnvironments'/revision/old['id']/(part['file']+'.bytes')).read_bytes()).hexdigest()==part['sha256']
                else:assert old==current,old['id']
                stored=published/'ContentRevisions'/(old['contentHash']+'.json')
                assert json.loads(stored.read_text())==old,'Missing exact old revision '+old['id']
            assert sorted(changes)==[f'QC{i:03d}' for i in range(1,11)]
            validated=subprocess.run(['dotnet',str(dll)],cwd=fixture,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=300)
            print(validated.stdout,flush=True);validated.check_returncode()
            assert snapshots(resources)==before,'Main repository content was changed'
            proof=dict(checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),result='Passed',bakedRevision=revision,publishedOnlyInTemporaryFixture=True,changedLevels=changes,otherLevelsUnchanged=True,puzzlesUnchanged=True,previousPublishedRevisionsPreserved=True,corruptBufferRejectedBeforePublication=True,allHundredCandidateLevelsAndSaveContractsPassed=True,output=validated.stdout,sources={str(x.relative_to(ROOT)):hashlib.sha256(x.read_bytes()).hexdigest() for x in sorted(sources)},unityLaunched=False,nativeAccepted=False,playerBuild=False)
            (REPORT/'publication-contracts.json').write_text(json.dumps(proof,indent=2)+'\n')
            print('PASS temporary publication: exactly first ten changed, all previous revisions retained, corruption rejected before publication; main content untouched.')
        data=dict(checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),result='Passed',sources={str(x.relative_to(ROOT)):hashlib.sha256(x.read_bytes()).hexdigest() for x in sorted(sources)},levels={p.stem:json.loads(p.read_text())['contentHash'] for p in list((resources/'Levels').glob('QC0*.json'))+list((resources/'GeneratedLevels').glob('*.json'))},output=result.stdout,unityLaunched=False,nativeVerified=False,playerBuild=False)
        (REPORT/'domain-contracts.json').write_text(json.dumps(data,indent=2)+'\n')

if __name__=='__main__':main()
