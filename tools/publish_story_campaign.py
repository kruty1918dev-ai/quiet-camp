"""Publish 100 accepted compositions through the existing history/conflict boundary.

No bypass of visual acceptance, source conflicts, puzzle validation or history.
QA is verified first. Repository replacements roll back together on failure.
"""
import argparse,hashlib,json,os,shutil,subprocess,sys,uuid
from pathlib import Path
from campaign_publication_gate import ROOT,QA,require_review,sha

PROJECT=ROOT/'QuietCamp';CONTENT=Path('Assets/QuietCamp/Resources/QuietCamp')
DEST=ROOT/'Design/LevelAuthoring/StoryCampaign';VERIFY=DEST/'Verification'
RULES=['ruleVersion','width','height','entry','accessPoints','blocked','shade','noise','exteriorWalkable','guests','friends','witness','objects','canopies']

def request(mode,**values):
    args=[sys.executable,str(ROOT/'tools/request_authoring_props.py'),'--project',str(QA),'--mode',mode]
    for key,value in values.items():args+=['--'+key,str(value)]
    result=subprocess.run(args,capture_output=True,text=True,encoding='utf-8')
    if result.returncode:raise RuntimeError(result.stdout+result.stderr)
    return json.loads((QA/'Temp/ai/qc007-art-result.json').read_text(encoding='utf-8'))

def replace(path,data):
    path.parent.mkdir(parents=True,exist_ok=True)
    pending=path.with_name(path.name+'.'+uuid.uuid4().hex+'.pending')
    try:pending.write_bytes(data);os.replace(pending,path)
    finally:
        if pending.exists():pending.unlink()

def main():
    p=argparse.ArgumentParser();p.add_argument('--round',required=True);a=p.parse_args()
    cycles=require_review(a.round)
    subprocess.run([sys.executable,str(ROOT/'tools/audit_story_landmarks.py'),'--round',a.round,'--require-all'],cwd=ROOT,check=True)
    ids=json.loads((PROJECT/CONTENT/'campaign.json').read_text(encoding='utf-8'))['campaignLevelIds']
    assert len(ids)==100 and len(set(ids))==100
    stage=QA/'Temp/ai/StoryCampaign'/a.round;planned=[];rules=[];unchanged={}
    # Freeze original history, including pre-existing user revision files.
    for base in (PROJECT,QA):
        for folder in ('ContentRevisions','ArtRevisions'):
            for path in (base/CONTENT/folder).glob('*.json'):unchanged[path]=sha(path)
    for order,ident in enumerate(ids,1):
        safe=ident.replace(':','_');level=CONTENT/('GeneratedLevels' if ident.startswith('gen:') else 'Levels')/(safe+'.json')
        art=CONTENT/'ArtCompositions'/(safe+'.json');draft=Path('Assets/QuietCamp/Authoring/CampaignLevels')/(safe+'.draft.json')
        d=json.loads((stage/f'L{order:03}.draft.json').read_text(encoding='utf-8'))
        assert d['sourceLevelId']==ident and d['level']['order']==order
        for base in (PROJECT,QA):
            assert sha(base/level)==d['sourceContentHash'],f'Content source changed: {ident}'
            actual=sha(base/art) if (base/art).exists() else 'absent'
            assert actual==d['sourceArtHash'],f'Art source changed: {ident}'
        old=json.loads((PROJECT/level).read_text(encoding='utf-8-sig'))
        assert all(old.get(k)==d['level'].get(k) for k in RULES),f'Puzzle rules changed: {ident}'
        paths=[level,art,draft]
        for folder in ('ContentRevisions','ArtRevisions'):
            path=CONTENT/folder/(old['contentHash']+'.json')
            if not (PROJECT/path).exists():paths.append(path)
        planned.append((order,ident,paths));rules.append(dict(id=ident,unchanged=RULES))
    owned=list(dict.fromkeys(path for _,_,paths in planned for path in paths))
    rollback_paths=owned+[Path(str(path)+'.meta') for path in owned]
    backups={(base,path):(base/path).read_bytes() if (base/path).exists() else None for base in (PROJECT,QA) for path in rollback_paths}
    workspace=request('authoring-workspace-status');VERIFY.mkdir(parents=True,exist_ok=True)
    try:
        for order,ident,_ in planned:
            request('opening-apply',draft=stage/f'L{order:03}.draft.json')
            print('Validated/applied in QA:',order,ident,flush=True)
        for path in owned:
            # Human-editable drafts are compact JSON; parsed values are preserved.
            if path.name.endswith('.draft.json'):
                value=json.loads((QA/path).read_text(encoding='utf-8'))
                replace(QA/path,(json.dumps(value,ensure_ascii=False,separators=(',',':'))+'\n').encode('utf-8'))
            replace(PROJECT/path,(QA/path).read_bytes())
            metadata=Path(str(path)+'.meta');source=QA/metadata
            if not source.exists():
                guid=uuid.uuid5(uuid.NAMESPACE_URL,'quietcamp/'+path.as_posix()).hex
                replace(source,('fileFormatVersion: 2\nguid: '+guid+'\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode())
            if not (PROJECT/metadata).exists():shutil.copy2(source,PROJECT/metadata)
        assert all(sha(path)==value for path,value in unchanged.items()),'Historical revision changed'
        after=request('authoring-workspace-status')
        assert workspace.get('artSha256')==after.get('artSha256') and workspace.get('objects')==after.get('objects'),'Manual draft changed'
    except Exception:
        for (base,path),data in backups.items():
            if data is None:
                if (base/path).exists():(base/path).unlink()
            else:replace(base/path,data)
        raise
    report=dict(status='published',levels=100,round=a.round,inspectedCorrectionCycles=cycles,idsPreserved=True,historyPreserved=True,manualDraftUnchanged=True,unchangedRules=rules,ownedFiles=[(Path('QuietCamp')/path).as_posix() for path in owned])
    (VERIFY/'publication.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('100 accepted compositions published. Fresh published captures and real gameplay verification remain required.')

if __name__=='__main__':main()
