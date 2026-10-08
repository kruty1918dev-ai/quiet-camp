"""Side-by-side native render evidence; does not score artistic quality."""
import json,sys
from pathlib import Path
from PIL import Image,ImageDraw
before,after=map(Path,sys.argv[1:3])
images=[]
for source in (before,after):
 image_path=source.with_suffix('.png')
 if not image_path.is_file():raise SystemExit('Missing native PNG beside sidecar: '+str(image_path))
 with Image.open(image_path) as opened:
  image=opened.convert('RGB');image.thumbnail((1080,1600));images.append(image)
w=sum(im.width for im in images);h=max(im.height for im in images)
sheet=Image.new('RGB',(w,h+64),(242,238,220));draw=ImageDraw.Draw(sheet);x=0
for source,image in zip((before,after),images):
 sheet.paste(image,(x,64));draw.text((x+12,12),source.stem,fill=(30,61,47));x+=image.width
out=after.with_name(after.stem+'-comparison.png');sheet.save(out)
before_doc,after_doc=json.loads(before.read_text()),json.loads(after.read_text())
a,b=before_doc,after_doc
def entities(doc):return {e['id']:e for e in doc.get('entities',[])}
a,b=entities(a),entities(b)
report={'before':str(before),'after':str(after),'contactSheet':str(out),'added':sorted(b.keys()-a.keys()),'removed':sorted(a.keys()-b.keys()),'changed':sorted(k for k in a.keys()&b.keys() if a[k]!=b[k]),'metrics':{'before':before_doc.get('metrics',{}),'after':after_doc.get('metrics',{}),'delta':{k:after_doc['metrics'][k]-before_doc['metrics'][k] for k in before_doc.get('metrics',{}).keys()&after_doc.get('metrics',{}).keys() if isinstance(before_doc['metrics'][k],(int,float)) and isinstance(after_doc['metrics'][k],(int,float))}},'artisticAcceptance':'Requires human/native render inspection'}
out.with_suffix('.json').write_text(json.dumps(report,indent=2)+'\n');print(out)
