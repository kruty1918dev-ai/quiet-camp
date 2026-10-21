from pathlib import Path
from PIL import Image,ImageDraw
import json,sys,hashlib
root=Path(sys.argv[1]);source=root/'review-v2';target=root/'review-sheets';target.mkdir(exist_ok=True)
for start in range(1,101,4):
 orders=list(range(start,min(start+4,101)))
 if not all((source/f'{n:03}-inventory.json').exists() for n in orders):continue
 sheet=Image.new('RGB',(1200,len(orders)*400),'#eceee4');draw=ImageDraw.Draw(sheet);proof=[]
 for j,n in enumerate(orders):
  draw.text((12,j*400+7),f'{n:03} / landscape | portrait | reference tents',(20,30,20));x=0
  for mode,width in [('landscape',480),('portrait',240),('solved',480)]:
   p=source/f'{n:03}-{mode}.png';im=Image.open(p).convert('RGB');im.thumbnail((width,360));sheet.paste(im,(x+(width-im.width)//2,j*400+30));x+=width;proof.append({'order':n,'frame':p.name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
 file=target/f'{start:03}-{orders[-1]:03}.jpg';sheet.save(file,quality=95);(file.with_suffix('.json')).write_text(json.dumps({'images':proof,'sheetSha256':hashlib.sha256(file.read_bytes()).hexdigest()},indent=2)+'\n')
print('Sheets',len(list(target.glob('*.jpg'))))
