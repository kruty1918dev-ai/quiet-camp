"""Preserve/restore exact editable native drafts independently of Unity Temp."""
import argparse,gzip,hashlib,json,shutil
from pathlib import Path
from story_campaign_review import ROOT,QA

def digest(data):return hashlib.sha256(data).hexdigest()

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--round',required=True);parser.add_argument('--restore',action='store_true');args=parser.parse_args()
    stage=QA/'UserSettings/QuietCampCampaignReview'/args.round
    archive=ROOT/'Design/LevelAuthoring/StoryCampaign/Drafts'/args.round
    review=ROOT/'Design/LevelAuthoring/StoryCampaign/Review'/args.round
    manifest=archive/'manifest.json'
    if args.restore:
        records=json.loads(manifest.read_text(encoding='utf-8'))['drafts']
        prepared=[]
        for row in records:
            data=gzip.decompress((archive/row['file']).read_bytes());assert digest(data)==row['sha256']
            target=stage/row['file'].removesuffix('.gz')
            if target.exists() and digest(target.read_bytes())!=row['sha256']:raise RuntimeError('Refusing to overwrite newer editing work: '+str(target))
            prepared.append((target,data))
        for target,data in prepared:
            if target.exists():continue
            target.parent.mkdir(parents=True,exist_ok=True);pending=target.with_suffix(target.suffix+'.pending');pending.write_bytes(data);pending.replace(target)
        print('Exact editable drafts restored:',len(prepared));return
    archive.mkdir(parents=True,exist_ok=True);records=[]
    for n in range(1,101):
        stem=f'L{n:03}';source=stage/(stem+'.draft.json');data=source.read_bytes();receipt=json.loads((review/(stem+'.json')).read_text(encoding='utf-8'))
        assert receipt.get('draftSha256')==digest(data),'Uncaptured or changed draft: '+stem
        assert receipt['imageSha256']==digest((review/(stem+'.png')).read_bytes())
        doc=json.loads(data);assert doc['level']['order']==n
        filename=stem+'.draft.json.gz';compressed=gzip.compress(data,compresslevel=6,mtime=0)
        pending=archive/(filename+'.pending');pending.write_bytes(compressed);pending.replace(archive/filename)
        records.append(dict(order=n,id=doc['sourceLevelId'],file=filename,sha256=digest(data),bytes=len(data),compressedBytes=len(compressed),imageSha256=receipt['imageSha256']))
    manifest.write_text(json.dumps(dict(version=1,round=args.round,status='staged-not-published',drafts=records),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    shutil.copy2(ROOT/'QuietCamp/Assets/QuietCamp/Authoring/LevelProps/ATTRIBUTION.md',archive/'ATTRIBUTION.md')
    print('Captured editable checkpoints:',len(records),'compressed bytes:',sum(r['compressedBytes'] for r in records))

if __name__=='__main__':main()
