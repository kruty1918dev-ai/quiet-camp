"""Shared camera geometry and review sheets for real, native captures."""
import argparse,html,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
QA=Path('C:/Users/Public/qcui-20261006/QuietCamp')


def projected_bounds(instance,model,art):
    v=model['vertices'];lo=[min(v[k::3]) for k in range(3)];hi=[max(v[k::3]) for k in range(3)]
    pitch,yaw=map(math.radians,art['cameraEuler'][:2]);angle=math.radians(instance.get('yaw',0));scale=instance.get('scaleXYZ') or [instance['scale']]*3
    points=[]
    for mask in range(8):
        x,y,z=[(hi[k] if mask&(1<<k) else lo[k])*scale[k] for k in range(3)]
        x,z=x*math.cos(angle)+z*math.sin(angle),-x*math.sin(angle)+z*math.cos(angle)
        x+=instance['x'];y+=instance['y'];z+=instance['z']
        points.append([x*math.cos(yaw)-z*math.sin(yaw),x*math.sin(yaw)*math.sin(pitch)+y*math.cos(pitch)+z*math.cos(yaw)*math.sin(pitch),x*math.sin(yaw)*math.cos(pitch)-y*math.sin(pitch)+z*math.cos(yaw)*math.cos(pitch)])
    return [min(p[k] for p in points) for k in range(3)],[max(p[k] for p in points) for k in range(3)]


def terrain_height(art,x,z):
    land=art['landscape'];n=land['resolution'];size=land['size'];px=max(0,min(n-1,(x-land['originX'])/size*(n-1)));pz=max(0,min(n-1,(z-land['originZ'])/size*(n-1)));ix=int(px);iz=int(pz);fx=px-ix;fz=pz-iz;h=land['heights']
    return sum(h[min(n-1,iz+dz)*n+min(n-1,ix+dx)]*(fx if dx else 1-fx)*(fz if dz else 1-fz) for dx in [0,1] for dz in [0,1])


def sheets(round_name,start=1,end=100):
    from playwright.sync_api import sync_playwright
    output=ROOT/'Design/LevelAuthoring/StoryCampaign/Review'/round_name
    temp=QA/'Temp/ai/StoryCampaign'/round_name;temp.mkdir(parents=True,exist_ok=True)
    profiles=json.loads((ROOT/'Design/LevelAuthoring/StoryCampaign/unique-profiles.json').read_text(encoding='utf-8'))
    with sync_playwright() as engine:
        browser=engine.chromium.launch(channel='msedge',headless=True)
        for begin in range(start,end+1,10):
            indices=[i for i in range(begin,min(end+1,begin+10)) if (output/f'L{i:03}.png').exists() and (output/f'L{i:03}.json').exists()]
            if not indices:continue
            page_path=temp/f'{begin:03}-{min(end,begin+9):03}.html'
            page_path.write_text('<meta charset="utf-8"><style>body{margin:0;background:#203028;display:grid;grid-template-columns:repeat(3,384px);color:white;font:14px sans-serif}figure{margin:0}img{width:384px;display:block}figcaption{padding:4px;height:32px}</style>'+''.join('<figure><figcaption>'+str(i)+' · '+html.escape(profiles[i-1]['story']['title'])+'</figcaption><img src="'+(output/f'L{i:03}.png').as_uri()+'"></figure>' for i in indices),encoding='utf-8')
            page=browser.new_page(viewport={'width':1152,'height':424*((len(indices)+2)//3)});page.goto(page_path.as_uri());page.wait_for_function('Array.from(document.images).every(i=>i.complete&&i.naturalWidth>0)');page.screenshot(path=str(page_path.with_suffix('.png')));page.close()
        browser.close()
    print('Native review contact sheets:',temp)


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--round',required=True);p.add_argument('--start',type=int,default=1);p.add_argument('--end',type=int,default=100);a=p.parse_args();sheets(a.round,a.start,a.end)
