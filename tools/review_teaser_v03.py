"""Decode real V03 takes; flag editorial candidates, never invent artistic approval."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import cv2
import numpy as np
import scenedetect
from PIL import Image, ImageDraw
from scenedetect import open_video, SceneManager, ContentDetector
from package_teaser import takes, verify
from review_teaser import audio

cv2.setNumThreads(1)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def analyze(movie, folder, planned_cuts, black_at):
    folder.mkdir(parents=True, exist_ok=True)
    cap=cv2.VideoCapture(str(movie)); count=int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    indexes=sorted(set([n for n in range(0,count,30)]+list(range(0,min(60,count),8))+
        [n+d for n in planned_cuts for d in (-1,0,8) if 0<=n+d<count]+[count-1]))
    changes=[]; black=[]; images=[]; hashes=[]; red_sizes=[]; previous=None
    for n in range(count):
        ok,frame=cap.read()
        if not ok: raise RuntimeError('Failed to decode frame '+str(n))
        small=cv2.resize(frame,(160,90),interpolation=cv2.INTER_AREA)
        gray=cv2.cvtColor(small,cv2.COLOR_BGR2GRAY)
        if int(np.max(small))<5: black.append(n)
        if previous is not None: changes.append(float(np.abs(small.astype(float)-previous).mean()))
        previous=small.astype(float)
        thumb=cv2.resize(gray,(9,8)); hashes.append((thumb[:,1:]>thumb[:,:-1]).reshape(-1))
        # An explicit heuristic, not a tent detector: warm-coloured connected regions.
        hsv=cv2.cvtColor(small,cv2.COLOR_BGR2HSV)
        mask=cv2.inRange(hsv,np.array([0,70,50]),np.array([20,255,245]))
        _,_,stats,_=cv2.connectedComponentsWithStats(mask)
        red_sizes.append(int(stats[1:,cv2.CC_STAT_AREA].max()) if len(stats)>1 else 0)
        if n in indexes:
            path=folder/('frame-'+str(n).zfill(4)+'.jpg');cv2.imwrite(str(path),frame,[cv2.IMWRITE_JPEG_QUALITY,95])
            images.append(dict(frame=n,seconds=round(n/30,3),path=str(path),sha256=sha(path)))
    cap.release()
    expected=list(range(round(black_at*30),count))
    # Older rough captures compare a float32 cutoff with a double Timeline time.
    # Report that one-frame boundary defect explicitly; never tolerate an interior gap.
    black_offset=1 if expected and black==expected[1:] else 0
    if black!=expected and not black_offset: raise RuntimeError('Unexpected black frame range: '+str(black))
    manager=SceneManager();manager.add_detector(ContentDetector(threshold=18,min_scene_len=6))
    video=open_video(str(movie));manager.detect_scenes(video,show_progress=False)
    detected=[a.get_frames() for a,b in manager.get_scene_list()[1:]]
    static=[]; start=None
    for n,value in enumerate(changes+[999]):
        quiet=value<.18 and n not in planned_cuts
        if quiet and start is None: start=n
        if not quiet and start is not None:
            if n-start>=60:static.append(dict(first=start,last=n,seconds=round((n-start)/30,2)))
            start=None
    duplicate_pairs=[]
    for left,right in zip([0]+planned_cuts,planned_cuts+[count]):
        # Long runs of exactly duplicated decoded frames are technical candidates.
        run=0
        for n in range(left+1,right):
            run=run+1 if changes[n-1]<.005 else 0
            if run==15: duplicate_pairs.append(dict(first=n-15,last=n))
    for page,offset in enumerate(range(0,len(images),12)):
        group=images[offset:offset+12];sheet=Image.new('RGB',(1440,294*((len(group)+2)//3)),(20,25,22));draw=ImageDraw.Draw(sheet)
        for i,item in enumerate(group):
            x=(i%3)*480;y=(i//3)*294
            with Image.open(item['path']) as picture:sheet.paste(picture.resize((480,270),Image.Resampling.LANCZOS),(x,y+24))
            draw.text((x+8,y+6),str(item['seconds'])+' s | frame '+str(item['frame']),fill='white')
        sheet.save(folder/('contact-'+str(page+1).zfill(2)+'.jpg'),quality=94)
    similar=[]
    boundaries=[0]+planned_cuts+[count]
    for i,(a,b) in enumerate(zip(boundaries[:-1],boundaries[1:])):
        for j in range(i+1,len(boundaries)-1):
            c,d=boundaries[j:j+2]
            distance=int(np.count_nonzero(hashes[(a+b)//2]!=hashes[(c+d)//2]))
            if distance<=12:similar.append(dict(leftShot=i,rightShot=j,hamming=distance))
    return dict(images=images,blackFrames=black,expectedBlackFrames=expected,blackStartOffsetFrames=black_offset,plannedCuts=planned_cuts,detectedCuts=detected,
        unmatchedDetectedCuts=[n for n in detected if not any(abs(n-c)<=2 for c in planned_cuts)],
        undetectedPlannedCuts=[c for c in planned_cuts if not any(abs(n-c)<=2 for n in detected)],
        nearStaticCandidates=static,duplicateFrameRunCandidates=duplicate_pairs,similarShotCandidates=similar,
        warmRegionAreaMedian=float(np.median(red_sizes)),warmRegionAreaMeaning='Heuristic candidate only; not proof of subject visibility.',
        thresholds=dict(contentCut=18,similarHash=12,nearStaticDifference=.18,nearStaticSeconds=2),
        averageFrameDifference=round(float(np.mean(changes)),4),hashes=[np.packbits(h).tobytes().hex() for h in hashes])


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',type=Path,required=True)
    p.add_argument('--version',required=True,choices=['v03-a','v03-b','v03-c','v03-rough','v03','v04-pilot','v04']);a=p.parse_args()
    project=a.project.resolve();job=json.loads((project/'UserSettings/Teaser'/('job-'+a.version+'.json')).read_text(encoding='utf-8-sig'))
    receipts=takes(project,a.version);output=Path(job['output']);movie=output/(job['videoName']+'.mp4')
    ffmpeg=project/'UserSettings/QuietCampPromoTools/ffmpeg.exe';ffprobe=ffmpeg.with_name('ffprobe.exe')
    verify(ffprobe,ffmpeg,movie,job['totalFrames']);cuts=np.cumsum([r['frames'] for r in receipts])[:-1].tolist()
    folder=output/'Review';analysis=analyze(movie,folder,cuts,job['blackAt'])
    # Independently compare both actual boundary pictures with their native takes.
    delivery=cv2.VideoCapture(str(movie));boundary_checks=[];offset=0
    for receipt in receipts:
        native=cv2.VideoCapture(receipt['video'])
        for frame in (0,receipt['frames']-1):
            native.set(cv2.CAP_PROP_POS_FRAMES,frame);ok,a_native=native.read()
            delivery.set(cv2.CAP_PROP_POS_FRAMES,offset+frame);fine,b_delivery=delivery.read()
            if not ok or not fine:raise RuntimeError('Missing native edit boundary picture.')
            delta=float(np.abs(cv2.resize(a_native,(160,90)).astype(float)-cv2.resize(b_delivery,(160,90))).mean())
            if delta>4:raise RuntimeError('Delivery does not match native cut at '+str(offset+frame))
            boundary_checks.append(dict(frame=offset+frame,shot=receipt['shotId'],meanAbsoluteRgbError=round(delta,4)))
        native.release();offset+=receipt['frames']
    delivery.release()
    raw=subprocess.run([str(ffmpeg),'-v','error','-i',str(movie),'-vn','-f','f32le','-ac','2','-ar','48000','-'],capture_output=True,check=True)
    samples=np.frombuffer(raw.stdout,dtype=np.float32)
    windows=[round(float(20*np.log10(max(1e-10,np.sqrt(np.mean(samples[n:n+96000]**2))))),2) for n in range(0,len(samples),96000)]
    exported=json.loads((output/'export-verification.json').read_text())
    if exported['sha256']!=sha(movie) or exported['sourceHash']!=job['sourceHash']:raise RuntimeError('Stale export verification.')
    packet=dict(version=a.version,sourceHash=job['sourceHash'],movie=str(movie),movieSha256=sha(movie),
        validatorSha256=sha(Path(__file__)),tools=dict(opencv=cv2.__version__,scenedetect=scenedetect.__version__),
        deliverySettings=exported,
        audioRmsDbfsPerSecond=windows,
        frames=job['totalFrames'],audio=audio(ffmpeg,movie),analysis=analysis,nativeBoundaryChecks=boundary_checks,
        artisticStatus='awaiting-agent-review',receipts=receipts,
        limitations=['Content changes and colour regions are review candidates, not aesthetic judgments.',
            'No independent audience test or mobile performance inference from this recording.'])
    path=folder/'packet.json';path.write_text(json.dumps(packet,indent=2),encoding='utf-8')
    print(json.dumps(dict(packet=str(path),sha256=sha(path),frames=packet['frames'],audio=packet['audio'],
        plannedCuts=cuts,detectedCuts=analysis['detectedCuts'],static=analysis['nearStaticCandidates'],
        duplicates=analysis['duplicateFrameRunCandidates'])))


if __name__=='__main__':main()
