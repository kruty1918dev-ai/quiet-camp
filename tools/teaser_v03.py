"""Stage-based V03 production through the existing WorldOps Editor, never a new Unity launch."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import time
import zipfile

ROOT=Path(__file__).resolve().parents[1]


def invoke(*args):
    result=subprocess.run([sys.executable,*map(str,args)],cwd=ROOT)
    if result.returncode:raise SystemExit(result.returncode)


def capture(project,version):
    invoke('tools/qc_world.py','--project',project,'teaser','record','--version',version)
    journal=project/'UserSettings/Teaser'/('job-'+version+'.json');deadline=time.monotonic()+1200;last=None
    while time.monotonic()<deadline:
        try:job=json.loads(journal.read_text(encoding='utf-8-sig'))
        except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.5);continue
        state=(job['status'],job['shot'],len(job['receipts']))
        if state!=last:print(version,state,flush=True);last=state
        if job['status']=='error':raise RuntimeError(job['error'])
        if job['status']=='captured':
            invoke('tools/package_teaser.py','--project',project,'--version',version,*(['--finish-v03'] if version in ('v03','v04') else []))
            return
        time.sleep(1)
    raise TimeoutError('Existing Editor capture did not finish; no new Editor was started.')


def deliver(project):
    from package_teaser import takes
    def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
    final=project/'Marketing/Exports/v03/v03';review=final/'Review'
    packet=json.loads((review/'packet.json').read_text());verdict=json.loads((review/'verdict.json').read_text())
    if verdict['decision']!='Review':raise RuntimeError('This delivery stage is explicitly the Review package.')
    notes=ROOT/'QuietCamp/Assets/Marketing/TeaserV03/QA_REPORT.md'
    if verdict['packetSha256']!=sha(review/'packet.json') or verdict['notesSha256']!=sha(notes):
        raise RuntimeError('Review evidence or notes changed after the verdict.')
    output=ROOT/'QuietCamp/Marketing/Exports/v03';destination=output/'Delivery';destination.mkdir(parents=True,exist_ok=True)
    files=[]
    def copy(source,relative):
        target=destination/relative;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target);files.append(target)
    for version in ['v03-a','v03-b','v03-c','v03-rough','v03']:
        takes(project,version)
        folder=project/'Marketing/Exports/v03'/version;data=json.loads((folder/'export-verification.json').read_text())
        movie=Path(data['file'])
        if sha(movie)!=data['sha256']:raise RuntimeError('Delivery movie changed: '+str(movie))
        if version=='v03' and sha(movie)!=packet['movieSha256']:raise RuntimeError('Review movie mismatch.')
        copy(movie,movie.name)
    for file in sorted(notes.parent.glob('*.md')):copy(file,Path('Documentation')/file.name)
    for file in [review/'packet.json',review/'verdict.json',*sorted(review.glob('contact-*.jpg'))]:copy(file,Path('QA')/file.name)
    copy(final/'ATTRIBUTION.md','ATTRIBUTION.md')
    proofs=dict(priorVersionsChecked=False,progressChecked=False)
    snapshot=project/'UserSettings/Teaser/v03-preserved-before.json'
    if snapshot.exists():
        previous=json.loads(snapshot.read_text());changed=[p for p,h in previous.items() if not (ROOT/p).exists() or sha(ROOT/p)!=h]
        if changed:raise RuntimeError('Prior authored version changed: '+str(changed))
        proofs.update(priorVersionsChecked=True,priorFiles=len(previous),changed=[])
    snapshot=project/'UserSettings/Teaser/v03-saves-before.json'
    if snapshot.exists():
        from capture_campaign_promo import persisted
        previous=json.loads(snapshot.read_text())
        if previous!=persisted():raise RuntimeError('Persistent progress changed; investigate before delivery.')
        proofs.update(progressChecked=True,persistentFiles=len(previous),progressUnchanged=True)
    proof=destination/'QA/preservation.json';proof.write_text(json.dumps(proofs,indent=2),encoding='utf-8');files.append(proof)
    manifest=destination/'manifest.json';manifest.write_text(json.dumps({str(f.relative_to(destination)):dict(bytes=f.stat().st_size,sha256=sha(f)) for f in files},indent=2),encoding='utf-8');files.append(manifest)
    archive=output/'QuietCamp_V03_Review_Package.zip'
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as bundle:
        for file in files:bundle.write(file,file.relative_to(destination))
    with zipfile.ZipFile(archive) as bundle:
        if bundle.testzip() is not None:raise RuntimeError('ZIP CRC verification failed.')
    if archive.stat().st_size>=100_000_000:raise RuntimeError('Delivery exceeds 100 MB.')
    print(json.dumps(dict(archive=str(archive),bytes=archive.stat().st_size,sha256=sha(archive),videos=5,files=len(files),zipCrcVerified=True,**proofs)))


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',type=Path,required=True)
    p.add_argument('stage',choices=['hooks','rough','fine','deliver']);a=p.parse_args();project=a.project.resolve()
    if a.stage=='hooks':
        for version in ['v03-a','v03-b','v03-c']:capture(project,version)
    elif a.stage=='deliver':deliver(project)
    else:capture(project,'v03-rough' if a.stage=='rough' else 'v03')


if __name__=='__main__':main()
