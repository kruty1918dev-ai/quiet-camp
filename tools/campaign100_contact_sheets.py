#!/usr/bin/env python3
"""Native-image contact sheets for human inspection; originals remain untouched."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Design/Campaign100/2026-10-11'
font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',15)
small=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',11)
rows=json.loads((OUT/'Authoring/campaign100.json').read_text())['levels']
(OUT/'ContactSheets').mkdir(exist_ok=True)
for first in range(1,101,10):
 for orientation in ('landscape','portrait'):
  selected=rows[first-1:first+9]
  if not all((OUT/f'Renders/level-{r["number"]:03d}-{orientation}.png').exists() for r in selected):continue
  cw,ch=(384,280) if orientation=='landscape' else (216,530)
  sheet=Image.new('RGB',(cw*5,ch*2),(241,237,223));draw=ImageDraw.Draw(sheet)
  for j,r in enumerate(selected):
   x=(j%5)*cw;y=(j//5)*ch
   with Image.open(OUT/f'Renders/level-{r["number"]:03d}-{orientation}.png') as source:
    source.thumbnail((cw-12,ch-45));sheet.paste(source,(x+(cw-source.width)//2,y))
   draw.text((x+7,y+ch-40),f'{r["number"]:03d} · {r["id"]}',fill=(37,63,50),font=font)
   title=r['titleUk'];draw.text((x+7,y+ch-21),title[:32 if orientation=='landscape' else 24],fill=(37,63,50),font=small)
  sheet.save(OUT/f'ContactSheets/{first:03d}-{first+9:03d}-{orientation}.jpg',quality=88)
print('Native contact sheets updated.')
