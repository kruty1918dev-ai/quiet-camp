#!/usr/bin/env python3
"""Verify genuine after frames and successful serial native batches."""
import argparse,datetime,hashlib,json,xml.etree.ElementTree as ET
from pathlib import Path
from PIL import Image
from campaign100_capture_sources import ROOT,current_manifest
DEST=ROOT/'Design/Campaign100/2026-10-11/FirstTenRefinement'
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--archived',action='store_true',help='Verify frozen experiment without claiming current runtime evidence');args=parser.parse_args()
    if not args.archived and not (DEST/'runtime-publication.json').exists():raise SystemExit('Experiment is deferred; use --archived to verify its historical evidence.')
    prepared=json.loads(((DEST/'After' if args.archived else DEST.parent)/'capture-source-manifest.json').read_text())
    revision=prepared['revision'] if args.archived else current_manifest()['revision'];frames=[];batches=[]
    assert prepared['revision']==revision,'Captured sources changed'
    assert hashlib.sha256(json.dumps(prepared['files'],separators=(',',':')).encode()).hexdigest()==revision
    for first in (1,3,5,7,9):
        last=first+1;path=DEST/f'After/Tests/first-ten-{first}-{last}-playmode.xml'
        if not path.exists():path=ROOT/f'TestResults/first-ten-{first}-{last}-playmode.xml'
        xml=ET.parse(path).getroot()
        assert xml.attrib['result']=='Passed' and int(xml.attrib['failed'])==0,path
        batch=json.loads((DEST/f'After/native-batch-{first}-{last}.json').read_text())
        assert batch['result']=='Passed' and batch['captureCount']==4 and not batch['nativeGameView']
        batches.append(dict(first=first,last=last,result='Passed',xmlSha256=hashlib.sha256(path.read_bytes()).hexdigest(),startUtc=xml.attrib['start-time'],endUtc=xml.attrib['end-time']))
    for number in range(1,11):
        level_path=DEST/f'After/PublishedLevels/QC{number:03d}.json' if args.archived else ROOT/f'QuietCamp/Assets/QuietCamp/Resources/QuietCamp/Levels/QC{number:03d}.json'
        level=json.loads(level_path.read_text())
        reference=level['bakedEnvironment']
        for orientation,width,height in [('landscape',1280,800),('portrait',720,1600)]:
            path=DEST/f'After/Renders/level-{number:03d}-{orientation}.json';data=json.loads(path.read_text());png=path.with_suffix('.png')
            assert data['kind']=='native-unity-world-camera' and data['levelId']==level['id'] and data['orientation']==orientation
            assert data['contentHash']==level['contentHash'] and data['sourceRevision']==revision
            assert data['bakedEnvironment']==reference and data['bakedSourceRevision']==reference.split('/')[-2]
            assert len(data['bakedParts'])==5 and data['syntheticWitnessLayout'] and data['reducedMotion']
            assert hashlib.sha256(png.read_bytes()).hexdigest()==data['imageSha256']
            with Image.open(png) as image:
                assert image.size==(width,height) and image.getbbox() is not None
            frames.append(dict(levelId=level['id'],orientation=orientation,capturedUtc=data['capturedUtc'],contentHash=data['contentHash'],imageSha256=data['imageSha256']))
    result=dict(verifiedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),result='Passed',sourceRevision=revision,count=20,serialNativeBatches=batches,frames=frames,nativeWorldCamera=True,nativeGameView=False,chromeClosed=False,playerBuild=False,mobileFpsMeasured=False,fullPlanAccepted=False)
    if args.archived:
        original=json.loads((DEST/'After/capture-verification.json').read_text())
        assert original['sourceRevision']==revision and original['frames']==frames and original['serialNativeBatches']==batches
        print('PASS archived experiment: 20 unchanged PNGs/receipts and 5 passed XML batches; frozen level/source hashes match. Current runtime evidence not claimed; historical verification receipt unchanged.')
        return
    (DEST/'After/capture-verification.json').write_text(json.dumps(result,indent=2)+'\n')
    print('PASS 20 genuine after frames and 5 successful serial native batches; hashes, source revision, actual baked world, dimensions and provenance match. No HUD/full-plan/mobile-FPS claim.')
if __name__=='__main__':main()
