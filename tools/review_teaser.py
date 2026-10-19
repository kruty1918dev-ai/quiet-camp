"""Create fresh decoded review frames and technical checks from the actual Recorder MP4s."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import numpy as np
from PIL import Image, ImageDraw
from package_teaser import takes, verify, run


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def audio(ffmpeg, path):
    result = subprocess.run([str(ffmpeg), '-v', 'error', '-i', str(path), '-f', 'f32le', '-ac', '2', '-ar', '48000', '-'], capture_output=True, check=True)
    samples = np.frombuffer(result.stdout, dtype=np.float32)
    peak = float(np.max(np.abs(samples)))
    rms = float(np.sqrt(np.mean(samples * samples)))
    if not np.all(np.isfinite(samples)) or peak < .001 or peak >= 1:
        raise RuntimeError('Silent, clipped or invalid audio: ' + str(path))
    return dict(peakDbfs=round(float(20*np.log10(peak)),2),rmsDbfs=round(float(20*np.log10(rms)),2))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--version',choices=['v01','v02'],default='v01')
    parser.add_argument('--final', action='store_true')
    args = parser.parse_args()
    project = args.project.resolve()
    ffmpeg, ffprobe = [project / 'UserSettings/QuietCampPromoTools' / n for n in ('ffmpeg.exe', 'ffprobe.exe')]
    output = project / ('Marketing/Exports' if args.version=='v01' else 'Marketing/Exports/v02')
    review = output / 'Review'
    review.mkdir(exist_ok=True)
    receipts = takes(project,args.version)
    packet = dict(sourceHash=receipts[0]['sourceHash'],kind='decoded-native-gameplay-video-review',artisticStatus='awaiting-agent-review',shots=[])
    contact = Image.new('RGB',(1920,len(receipts)*244),(20,25,22))
    for row, receipt in enumerate(receipts):
        movie=Path(receipt['video']); frames=receipt['frames']
        verify(ffprobe,ffmpeg,movie,frames)
        metrics=audio(ffmpeg,movie)
        indexes=[0,frames//4,frames//2,3*frames//4,frames-1]
        folder=review/receipt['shotId'];folder.mkdir(exist_ok=True)
        selector='+'.join('eq(n,'+str(n)+')' for n in indexes)
        run([ffmpeg,'-y','-v','error','-i',movie,'-vf','select='+selector.replace(',',r'\,'),'-fps_mode','vfr','-frames:v','5',folder/'frame-%02d.png'])
        images=[]
        for column,path in enumerate(sorted(folder.glob('frame-*.png'))):
            with Image.open(path) as image:
                contact.paste(image.resize((384,216),Image.Resampling.LANCZOS),(column*384,row*244+28))
            images.append(dict(path=str(path),sha256=sha(path),sourceFrame=indexes[column]))
        ImageDraw.Draw(contact).text((12,row*244+8),receipt['shotId']+' | '+receipt['levelId']+' | '+str(frames)+' frames',fill='white')
        packet['shots'].append(dict(id=receipt['shotId'],videoSha256=sha(movie),images=images,audio=metrics))
    contact.save(review/'contact-sheet.jpg',quality=94)
    if args.final:
        final=output/('Teaser_'+args.version+'_1920x1080_30fps.mp4')
        verify(ffprobe,ffmpeg,final,1260)
        packet['finalAudio']=audio(ffmpeg,final)
        # RGB conversion accounts for limited/full video range. Raw gray can
        # legitimately represent black as 16 and would create a false failure.
        decoded=subprocess.run([str(ffmpeg),'-v','error','-i',str(final),'-vf','scale=64:36','-pix_fmt','rgb24','-f','rawvideo','-'],capture_output=True,check=True)
        pixels=np.frombuffer(decoded.stdout,dtype=np.uint8).reshape((-1,64*36*3))
        black=np.flatnonzero(np.max(pixels,axis=1)<5)
        job=json.loads((project/('UserSettings/Teaser/job.json' if args.version=='v01' else 'UserSettings/Teaser/job-v02.json')).read_text(encoding='utf-8-sig'))
        first_black=round(job.get('blackAt',41.4)*30)
        if not np.array_equal(black,np.arange(first_black,1260)):
            raise RuntimeError('Unexpected black frames: '+str(black.tolist()))
        packet['blackFrames'] = dict(first=first_black,last=1259,count=1260-first_black)
        packet['finalSha256']=sha(final)
        final_folder=review/'Final';final_folder.mkdir(exist_ok=True)
        indexes=[75,240,420,600,780,960,1170,1250]
        if args.version=='v02':
            cuts=np.cumsum([r['frames'] for r in receipts])[:-1].tolist()
            indexes=sorted(set(list(range(0,1260,60))+[c+d for c in cuts for d in (-1,0)]+[382,388,394,1250]))
        selector='+'.join('eq(n,'+str(n)+')' for n in indexes)
        run([ffmpeg,'-y','-v','error','-i',final,'-vf','select='+selector.replace(',',r'\,'),'-fps_mode','vfr','-frames:v',str(len(indexes)),final_folder/'frame-%02d.png'])
        sheet=Image.new('RGB',(960,((len(indexes)+1)//2)*294),(20,25,22));packet['finalImages']=[]
        for index,path in enumerate(sorted(final_folder.glob('frame-*.png'))):
            x=(index%2)*480;y=(index//2)*294
            with Image.open(path) as image:sheet.paste(image.resize((480,270),Image.Resampling.LANCZOS),(x,y+24))
            ImageDraw.Draw(sheet).text((x+8,y+6),str(round(indexes[index]/30,2))+' seconds / final MP4',fill='white')
            packet['finalImages'].append(dict(path=str(path),sha256=sha(path),sourceFrame=indexes[index]))
        if args.version=='v02':
            by_frame={i['sourceFrame']:i for i in packet['finalImages']};checks=[];cut=0
            for before,after in zip(packet['shots'][:-1],packet['shots'][1:]):
                cut+=next(r['frames'] for r in receipts if r['shotId']==before['id'])
                for frame,expected in [(cut-1,before['images'][-1]),(cut,after['images'][0])]:
                    with Image.open(by_frame[frame]['path']) as a,Image.open(expected['path']) as b:
                        delta=float(np.mean(np.abs(np.asarray(a.convert('RGB').resize((96,54)),dtype=np.float32)-np.asarray(b.convert('RGB').resize((96,54)),dtype=np.float32))))
                    if delta>4:raise RuntimeError('Final cut does not match native take at frame '+str(frame)+': MAE '+str(delta))
                    checks.append(dict(frame=frame,nativeImageSha256=expected['sha256'],meanAbsoluteRgbError=round(delta,3)))
            packet['cutChecks']=checks
        sheet.save(review/'final-contact-sheet.jpg',quality=94)
        # Review pages retain readable thumbnails for every 2-second checkpoint
        # and both actual final-MP4 frames at each editorial cut.
        for page,offset in enumerate(range(0,len(packet['finalImages']),12)):
            group=packet['finalImages'][offset:offset+12];board=Image.new('RGB',(1440,((len(group)+2)//3)*294),(20,25,22))
            for i,item in enumerate(group):
                x=(i%3)*480;y=(i//3)*294
                with Image.open(item['path']) as picture:board.paste(picture.resize((480,270),Image.Resampling.LANCZOS),(x,y+24))
                ImageDraw.Draw(board).text((x+8,y+6),str(round(item['sourceFrame']/30,3))+' s | frame '+str(item['sourceFrame']),fill='white')
            board.save(review/('final-review-'+str(page+1).zfill(2)+'.jpg'),quality=94)
    (review/'packet.json').write_text(json.dumps(packet,indent=2),encoding='utf-8')
    print(json.dumps(dict(shots=len(packet['shots']),frames=1260,packet=str(review/'packet.json'),sheet=str(review/'contact-sheet.jpg'),finalChecked=args.final)))


if __name__=='__main__':
    main()
