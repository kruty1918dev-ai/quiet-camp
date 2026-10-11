#!/usr/bin/env python3
"""Publish original source only, privately; require current Core/compiler receipts."""
import argparse, hashlib, json, pathlib, shutil, subprocess
from verify_environment_cores import ROOT,PACKAGES,SLUGS
OWNER='kruty1918dev-ai'
REPORT=ROOT/'Design/Campaign100/2026-10-11/FirstTenRefinement'

def run(*args,cwd=None):
    return subprocess.check_output(args,cwd=cwd,text=True,stderr=subprocess.STDOUT).strip()

def verify_receipts():
    for name,count in [('core-contracts',8),('integration-contracts',1),('unity-source-checks',32)]:
        receipt=json.loads((REPORT/(name+'.json')).read_text());assert len(receipt['checks'])==count
        assert receipt['unity_launched'] is False
        for check in receipt['checks']:
            for filename,digest in check['sources'].items():
                assert hashlib.sha256((ROOT/filename).read_bytes()).hexdigest()==digest,'Stale verification: '+filename

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--publish',action='store_true');args=parser.parse_args()
    verify_receipts()
    if not args.publish:print('Current receipts match; --publish explicitly creates/pushes private source repositories.');return
    assert run('gh','api','user','--jq','.login')==OWNER
    work=pathlib.Path.home()/'.cache/quietcamp/environment-tool-upstreams'
    work.mkdir(parents=True,exist_ok=True);records=[]
    for slug in SLUGS:
        full=OWNER+'/'+slug;folder=PACKAGES/f'com.kruty1918.{slug}';checkout=work/slug
        owned_marker=checkout/'.quietcamp-original-tools-owner'
        if checkout.exists() and not owned_marker.exists():raise SystemExit('Refusing unrelated checkout: '+str(checkout))
        lookup=subprocess.run(['gh','api','repos/'+full],text=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
        if lookup.returncode:
            if '404' not in lookup.stderr:raise SystemExit(lookup.stderr)
            run('gh','repo','create',full,'--private','--description',slug+' — reusable deterministic Unity environment authoring; Core verified, native acceptance pending')
        elif not owned_marker.exists():raise SystemExit('Existing upstream requires inspection: '+full)
        assert json.loads(run('gh','api','repos/'+full))['private'] is True
        if not checkout.exists():
            checkout.mkdir();owned_marker.write_text(full+'\n');run('git','init','-b','main',cwd=checkout)
            run('git','config','user.name',run('git','config','user.name',cwd=ROOT),cwd=checkout)
            run('git','config','user.email',run('git','config','user.email',cwd=ROOT),cwd=checkout)
            run('git','remote','add','origin','https://github.com/'+full+'.git',cwd=checkout)
            (checkout/'.git/info/exclude').write_text('.quietcamp-original-tools-owner\n')
        # No game files, imported models, textures, photographs, archives or Library.
        for item in folder.iterdir():
            if item.name in ('VENDORED.json','.git','bin','obj'):continue
            destination=checkout/item.name
            if item.is_dir():
                if destination.exists():shutil.rmtree(destination)
                shutil.copytree(item,destination,ignore=shutil.ignore_patterns('bin','obj','.git'))
            else:shutil.copy2(item,destination)
        run('git','add','--all',cwd=checkout)
        if run('git','diff','--cached','--name-only',cwd=checkout):run('git','commit','-m','Implement portable authoring Core and Unity adapters; native acceptance pending',cwd=checkout)
        commit=run('git','rev-parse','HEAD',cwd=checkout);run('git','push','-u','origin','main',cwd=checkout)
        assert run('gh','api','repos/'+full+'/commits/main','--jq','.sha')==commit
        record={'package':folder.name,'repository':'https://github.com/'+full,'private':True,'upstreamCommit':commit,'version':'0.1.0','coreVerified':True,'cachedCompilerVerified':True,'nativeVerified':False,'gameVisualAccepted':False}
        (folder/'VENDORED.json').write_text(json.dumps(record,indent=2)+'\n');records.append(record);print('PUSH private '+full+' '+commit,flush=True)
    (REPORT/'package-upstreams.json').write_text(json.dumps({'packages':records,'player_build':False},indent=2)+'\n')

if __name__=='__main__':main()
