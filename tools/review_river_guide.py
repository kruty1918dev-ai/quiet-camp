"""Serial native seasonal river review, using existing Game View input/capture tooling."""
import argparse,json,subprocess,sys,shutil,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
QA=Path('C:/Users/Public/qcui-20261006/QuietCamp')
WORK=QA/'UserSettings/QuietCampRiverGuide'
DOC=ROOT/'Design/LevelAuthoring/RiverGuide'
ORDERS=[3,36,53,88]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,v):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def capture(name,pid):
    out=WORK/name;out.mkdir(parents=True,exist_ok=True)
    profile=QA/'Assets/QuietCamp/Resources/QuietCamp/river_environment.json'
    snapshot=profile.read_bytes();(out/'profile.json').write_bytes(snapshot)
    for n in ORDERS:
        assert profile.read_bytes()==snapshot,'Common profile changed during the batch'
        args=[sys.executable,str(ROOT/'tools/capture_campaign_promo.py'),'--stage',str(out),'--art-stage',str(QA/'UserSettings/QuietCampCompositionReview/C01'),'--start',str(n),'--count',str(n),'--stills']
        if pid:args+=['--editor-pid',str(pid)]
        subprocess.run(args,cwd=ROOT,check=True)
        for f in ['before.json','session.json']:shutil.copy2(out/f,out/f'L{n:03}'/f)
def sheet(name):
    from PIL import Image,ImageDraw,ImageFont
    canvas=Image.new('RGB',(1152,456*len(ORDERS)),(23,42,32));draw=ImageDraw.Draw(canvas)
    font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',17)
    for row,n in enumerate(ORDERS):
        draw.text((8,row*456+2),f'{name} | {n:03} | placement / completed',font=font)
        for col,file in enumerate(['gameplay.png','complete.png']):
            with Image.open(WORK/name/f'L{n:03}'/file) as im:
                assert im.size==(1440,1080);im.thumbnail((576,432));canvas.paste(im,(col*576,row*456+24))
    canvas.save(WORK/name/'review.jpg',quality=94)
    print(WORK/name/'review.jpg')
def inspect(name,notes):
    values=json.loads(notes.read_text(encoding='utf-8'));rows=[]
    assert sorted(map(int,values))==ORDERS
    for n in ORDERS:
        folder=WORK/name/f'L{n:03}';native=json.loads((folder/'capture.json').read_text(encoding='utf-8'))
        assert native['completed'] and native['memoryOnly'] and native['stillHasPlacementGrid']
        assert native['imageSha256']==sha(folder/'gameplay.png') and native['completeSha256']==sha(folder/'complete.png')
        rows.append(dict(order=n,id=native['id'],inspected=True,imageSha256=native['imageSha256'],completeSha256=native['completeSha256'],
            inputs=native['inputs'],observations=values[str(n)],status='inspected-not-final',nativeReceiptSha256=sha(folder/'capture.json')))
    write(DOC/'Review'/name/'inspection.json',dict(round=name,levels=rows))
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('step',choices=['capture','sheet','inspect']);p.add_argument('--round',required=True);p.add_argument('--notes',type=Path);p.add_argument('--editor-pid',type=int);a=p.parse_args()
    if a.step=='capture':capture(a.round,a.editor_pid)
    elif a.step=='sheet':sheet(a.round)
    else:inspect(a.round,a.notes)
