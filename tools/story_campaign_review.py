"""Shared camera geometry and review sheets for real, native captures."""
import argparse,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
QA=Path('C:/Users/Public/qcui-20261006/QuietCamp')


def projected_bounds(instance,model,art,exact=False):
    v=model['vertices'];lo=[min(v[k::3]) for k in range(3)];hi=[max(v[k::3]) for k in range(3)]
    pitch,yaw=map(math.radians,art['cameraEuler'][:2]);angle=math.radians(instance.get('yaw',0));scale=instance.get('scaleXYZ') or [instance['scale']]*3
    points=[]
    coordinates=zip(v[::3],v[1::3],v[2::3]) if exact else ([hi[k] if mask&(1<<k) else lo[k] for k in range(3)] for mask in range(8))
    for point in coordinates:
        x,y,z=[point[k]*scale[k] for k in range(3)]
        if instance.get('rotationEuler'):
            rx,angle,rz=map(math.radians,instance['rotationEuler'])
            x,y=x*math.cos(rz)-y*math.sin(rz),x*math.sin(rz)+y*math.cos(rz)
            y,z=y*math.cos(rx)-z*math.sin(rx),y*math.sin(rx)+z*math.cos(rx)
        x,z=x*math.cos(angle)+z*math.sin(angle),-x*math.sin(angle)+z*math.cos(angle)
        x+=instance['x'];y+=instance['y'];z+=instance['z']
        points.append([x*math.cos(yaw)-z*math.sin(yaw),x*math.sin(yaw)*math.sin(pitch)+y*math.cos(pitch)+z*math.cos(yaw)*math.sin(pitch),x*math.sin(yaw)*math.cos(pitch)-y*math.sin(pitch)+z*math.cos(yaw)*math.cos(pitch)])
    return [min(p[k] for p in points) for k in range(3)],[max(p[k] for p in points) for k in range(3)]


def terrain_height(art,x,z):
    land=art['landscape'];n=land['resolution'];size=land['size'];px=max(0,min(n-1,(x-land['originX'])/size*(n-1)));pz=max(0,min(n-1,(z-land['originZ'])/size*(n-1)));ix=int(px);iz=int(pz);fx=px-ix;fz=pz-iz;h=land['heights']
    return sum(h[min(n-1,iz+dz)*n+min(n-1,ix+dx)]*(fx if dx else 1-fx)*(fz if dz else 1-fz) for dx in [0,1] for dz in [0,1])

def fit_primary_camera(document,margin=.12,minimum=7.4):
    art=document['art'];models={m['id']:m for m in art['models']}
    primary=next(i for i in art['instances'] if i['id']==art['primaryLandmarkInstanceId'])
    board=dict(vertices=[v for x in [-document['level']['width']/2,document['level']['width']/2] for y in [0,1.8] for z in [-document['level']['height']/2,document['level']['height']/2] for v in (x,y,z)])
    bounds=[projected_bounds(primary,models[primary['modelId']],art,exact=True),projected_bounds(dict(x=0,y=0,z=0,scale=1),board,art,exact=True)]
    lo=[min(b[0][k] for b in bounds) for k in range(2)];hi=[max(b[1][k] for b in bounds) for k in range(2)]
    right,up=[(lo[k]+hi[k])/2 for k in range(2)]
    pitch,yaw=map(math.radians,art['cameraEuler'][:2]);height=.8
    assert abs(math.sin(pitch))>.1 and abs(art['cameraEuler'][2])<.001
    forward_ground=(up-height*math.cos(pitch))/math.sin(pitch)
    art['cameraTarget']=[right*math.cos(yaw)+forward_ground*math.sin(yaw),height,-right*math.sin(yaw)+forward_ground*math.cos(yaw)]
    art['cameraAspect']=1
    art['orthographicSize']=max(minimum,max(hi[k]-lo[k] for k in range(2))*.5*(1+margin))


def sheets(round_name,start=1,end=100):
    from PIL import Image,ImageDraw,ImageFont
    import textwrap
    output=ROOT/'Design/LevelAuthoring/StoryCampaign/Review'/round_name
    temp=QA/'UserSettings/QuietCampCampaignReview'/round_name;temp.mkdir(parents=True,exist_ok=True)
    profiles=json.loads((ROOT/'Design/LevelAuthoring/StoryCampaign/unique-profiles.json').read_text(encoding='utf-8'))
    font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',14)
    # Assemble unmodified native captures without starting a browser alongside
    # the memory-constrained Editor. These are review sheets, not new renders.
    for begin in range(start,end+1,10):
        indices=[i for i in range(begin,min(end+1,begin+10)) if (output/f'L{i:03}.png').exists() and (output/f'L{i:03}.json').exists()]
        if not indices:continue
        canvas=Image.new('RGB',(1152,424*((len(indices)+2)//3)),(32,48,40));draw=ImageDraw.Draw(canvas)
        for slot,i in enumerate(indices):
            x=slot%3*384;y=slot//3*424
            draw.multiline_text((x+4,y+2),'\n'.join(textwrap.wrap(str(i)+' · '+profiles[i-1]['story']['title'],width=47)),font=font,fill='white',spacing=1)
            with Image.open(output/f'L{i:03}.png') as native:canvas.paste(native.convert('RGB').resize((384,384),Image.Resampling.LANCZOS),(x,y+40))
        canvas.save(temp/f'{begin:03}-{min(end,begin+9):03}.png')
    print('Native review contact sheets:',temp)


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--round',required=True);p.add_argument('--start',type=int,default=1);p.add_argument('--end',type=int,default=100);a=p.parse_args();sheets(a.round,a.start,a.end)
