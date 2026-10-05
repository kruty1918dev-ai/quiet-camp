#!/usr/bin/env python3
"""Rebuild the game's antialiased pictograms from vector primitives."""
from pathlib import Path
from PIL import Image, ImageDraw
import math, uuid
import argparse

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--only',nargs='+',help='Regenerate only the named pictograms.')
requested=parser.parse_args().only
generated=0

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'QuietCamp/Assets/QuietCamp/Resources/QuietCamp/UI/Icons'
OUT.mkdir(parents=True, exist_ok=True)
GREEN = '#284E3B'
LIGHT = '#698666'
GOLD = '#D6AC55'
CREAM = '#FFF1C6'
S = 4

def icon(name, draw):
    global generated
    if requested and name not in requested:return
    im = Image.new('RGBA', (128*S, 128*S))
    d = ImageDraw.Draw(im)
    def line(points, color=GREEN, width=9):
        p = [(round(x*S), round(y*S)) for x,y in points]
        d.line(p, fill=color, width=round(width*S), joint='curve')
        r=width*S/2
        for x,y in (p[0],p[-1]): d.ellipse((x-r,y-r,x+r,y+r),fill=color)
    def poly(points, color=GREEN): d.polygon([(x*S,y*S) for x,y in points], fill=color)
    def oval(box, color=GREEN): d.ellipse(tuple(v*S for v in box),fill=color)
    def rect(box, radius=8, color=GREEN): d.rounded_rectangle(tuple(v*S for v in box),radius=radius*S,fill=color)
    draw(line,poly,oval,rect)
    im.save(OUT/(name+'.png'))
    generated+=1
    meta=OUT/(name+'.png.meta')
    if not meta.exists():
        meta.write_text('''fileFormatVersion: 2
guid: %s
TextureImporter:
  externalObjects: {}
  mipmaps:
    enableMipMap: 0
  isReadable: 0
  sRGBTexture: 1
  alphaIsTransparency: 1
  textureType: 2
  spriteMode: 1
  spritePixelsToUnits: 100
  maxTextureSize: 256
  textureCompression: 0
  filterMode: 1
  wrapMode: 1
''' % uuid.uuid4().hex)

icon('check',lambda l,p,o,r:l([(25,65),(51,88),(104,36)],width=13))
icon('check_light',lambda l,p,o,r:l([(25,65),(51,88),(104,36)],color=CREAM,width=13))
icon('remove',lambda l,p,o,r:(l([(35,35),(93,93)],width=11),l([(93,35),(35,93)],width=11)))
icon('back',lambda l,p,o,r:l([(78,25),(40,64),(78,103)],width=10))
icon('play',lambda l,p,o,r:p([(41,24),(41,104),(105,64)]))
icon('guests',lambda l,p,o,r:(o((18,23,53,58),LIGHT),r((13,65,58,106),14,LIGHT),o((65,15,106,56)),r((61,63,111,111),17)))
icon('lock',lambda l,p,o,r:(l([(39,59),(39,40),(45,26),(64,21),(83,26),(89,40),(89,59)],width=9),r((29,53,99,111),12),o((58,72,70,84),CREAM),r((61,80,67,95),2,CREAM)))
def tent(l,p,o,r):
    p([(14,99),(61,22),(112,99)],GOLD);p([(61,22),(68,101),(112,99)],GREEN)
    p([(61,45),(31,101),(76,101)],'#345C43');l([(14,101),(114,101)],width=5)
    l([(61,22),(16,100)],CREAM,4)
icon('tent',tent)
def tree(l,p,o,r):
    r((57,77,70,113),3,'#87643F');p([(64,11),(23,68),(38,68),(14,93),(113,93),(91,68),(105,68)])
    p([(64,11),(64,90),(23,90),(38,68),(23,68)],LIGHT)
icon('shade',tree)
def path(l,p,o,r):
    l([(22,105),(35,88),(71,77),(85,56),(64,43),(43,30),(59,14)],LIGHT,13)
    o((9,93,31,115),GREEN);o((48,7,69,28),GOLD)
icon('path',path)
def mapicon(l,p,o,r):
    p([(10,33),(44,18),(84,34),(118,19),(118,98),(84,113),(44,97),(10,113)],LIGHT)
    p([(44,18),(84,34),(84,113),(44,97)],'#91A17A');l([(30,79),(52,58),(80,73),(101,47)],CREAM,6)
    o((88,33,110,55),GOLD)
icon('map',mapicon)
def fire(l,p,o,r):
    l([(30,107),(95,107)],GREEN,12);p([(64,10),(72,47),(89,35),(105,71),(101,89),(85,102),(44,102),(25,85),(24,64),(45,41),(44,67)],GOLD)
    p([(63,52),(81,81),(75,100),(50,100),(44,83)],CREAM)
icon('fire',fire)
def leaf(l,p,o,r):
    p([(109,15),(79,18),(44,32),(23,57),(19,79),(32,99),(57,104),(80,87),(98,60)],LIGHT)
    l([(19,110),(92,34)],GREEN,7);l([(49,78),(48,53)],GREEN,4);l([(65,60),(86,61)],GREEN,4)
icon('quiet',leaf)
def hint(l,p,o,r):
    l([(49,29),(49,20),(57,12),(72,12),(80,20),(80,29)],width=6)
    r((35,32,94,98),8,GOLD);r((46,44,83,85),4,CREAM);r((28,96,102,108),4)
    l([(36,32),(94,32)],width=7);l([(64,47),(64,83)],GREEN,5)
icon('hint',hint)
def settings(l,p,o,r):
    for i in range(8):
        a=i*math.pi/4;l([(64+math.cos(a)*31,64+math.sin(a)*31),(64+math.cos(a)*47,64+math.sin(a)*47)],width=15)
    o((25,25,103,103));o((45,45,83,83),CREAM)
icon('settings',settings)
def curved(l,p,o,r,reverse=False,rotate=False):
    pts=[(64+math.cos(math.radians(a))*38,65+math.sin(math.radians(a))*38) for a in range(-38,220,4)]
    if reverse: pts=[(128-x,y) for x,y in pts]
    l(pts,width=9);end=pts[-1];sign=-1 if reverse else 1
    p([(end[0]-sign*18,end[1]-6),(end[0]+sign*14,end[1]-17),(end[0]+sign*10,end[1]+17)])
    if rotate:r((54,55,74,75),4,GOLD)
icon('undo',lambda l,p,o,r:curved(l,p,o,r))
icon('redo',lambda l,p,o,r:curved(l,p,o,r,True))
icon('rotate',lambda l,p,o,r:curved(l,p,o,r,False,True))
print(f'Generated {generated} lossless 512px pictograms (256px runtime import).')
