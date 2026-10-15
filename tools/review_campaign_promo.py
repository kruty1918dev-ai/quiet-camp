"""Build inspection sheets from genuine captures or the final encoded movie."""
import argparse
import json
from pathlib import Path
import subprocess

from PIL import Image, ImageDraw, ImageFont

from package_campaign_promo import STAGE, DELIVERY, WORK, FF

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('mode',choices=['stills','motion','transitions'])
p.add_argument('--start',type=int,default=1)
p.add_argument('--end',type=int,default=100)
args=p.parse_args()
out=STAGE/'review';out.mkdir(parents=True,exist_ok=True)
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18)

if args.mode=='stills':
    for start in range(args.start,args.end+1,10):
        numbers=list(range(start,min(args.end+1,start+10)))
        if not all((STAGE/f'L{n:03}/capture.json').exists() and json.loads((STAGE/f'L{n:03}/capture.json').read_text(encoding='utf-8'))['status']=='captured' for n in numbers):continue
        canvas=Image.new('RGB',(1200,((len(numbers)+2)//3)*320),(24,43,32));draw=ImageDraw.Draw(canvas)
        for i,n in enumerate(numbers):
            with Image.open(STAGE/f'L{n:03}/gameplay.png') as im:
                im.thumbnail((400,300));x=i%3*400;y=i//3*320;canvas.paste(im,(x,y+20));draw.text((x+5,y),f'{n:03}',font=font)
        path=out/f'stills-{start:03}.jpg';canvas.save(path,quality=92);print(path)
elif args.mode=='motion':
    for start in range(args.start,args.end+1,5):
        numbers=list(range(start,min(args.end+1,start+5)))
        canvas=Image.new('RGB',(1152,len(numbers)*308),(24,43,32));draw=ImageDraw.Draw(canvas)
        for i,n in enumerate(numbers):
            folder=out/f'motion-{n:03}';folder.mkdir(exist_ok=True)
            subprocess.run([FF,'-y','-hide_banner','-loglevel','error','-threads','1','-i',str(STAGE/f'L{n:03}/gameplay.mp4'),'-vf',"select='eq(n,20)+eq(n,60)+eq(n,118)',scale=384:288",'-fps_mode','vfr','-frames:v','3',str(folder/'f%02d.jpg')],check=True)
            frames=sorted(folder.glob('f*.jpg'));assert len(frames)==3
            for j,f in enumerate(frames):
                with Image.open(f) as im:canvas.paste(im,(j*384,i*308+20))
            draw.text((5,i*308),f'{n:03} · early / middle / completion',font=font)
        path=out/f'motion-{start:03}.jpg';canvas.save(path,quality=92);print(path,flush=True)
else:
    for start in range(max(2,args.start),args.end+1,10):
        numbers=list(range(start,min(args.end+1,start+10)))
        canvas=Image.new('RGB',(1200,((len(numbers)+2)//3)*320),(24,43,32));draw=ImageDraw.Draw(canvas)
        for i,n in enumerate(numbers):
            frame=out/f'transition-{n:03}.jpg'
            subprocess.run([FF,'-y','-hide_banner','-loglevel','error','-ss',str((n-1)*4+.15),'-threads','1','-i',str(DELIVERY/'QuietCamp-100-Levels.mp4'),'-frames:v','1','-vf','scale=400:300',str(frame)],check=True)
            with Image.open(frame) as im:canvas.paste(im,(i%3*400,i//3*320+20))
            draw.text((i%3*400+5,i//3*320),f'{n-1:03} → {n:03}',font=font)
        path=out/f'transitions-{start:03}.jpg';canvas.save(path,quality=92);print(path,flush=True)
