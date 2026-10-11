#!/usr/bin/env python3
"""Lossless triangle-source conversion for native GLB photography; original files unchanged."""
from pathlib import Path
import sys,json,hashlib
import numpy as np
sys.path.insert(0,str(Path(__file__).parent/'scene-composition'))
from prepare_staging_landmarks import read_glb
root=Path(__file__).resolve().parents[1]
out=root/'Design/Campaign100/2026-10-11/SourcePhotos~';out.mkdir(exist_ok=True)
for name in ['airplane-source','ship-source']:
 source=root/'QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Models/StagingLandmarks'/(name+'.glb')
 triangles,raw=read_glb(source)
 points=np.concatenate([t[0] for t in triangles]);lo=points.min(axis=0);hi=points.max(axis=0);height=float(hi[1]-lo[1]);origin=(lo+hi)/2;origin[1]=lo[1]
 data=[];colors=[]
 for points,uv,color in triangles:
  normal=np.cross(points[1]-points[0],points[2]-points[0]);length=np.linalg.norm(normal)
  if length<1e-8:continue
  normal/=length
  rgb=np.clip(np.round(color*255),0,255).astype(int);colors.append(int(rgb[0]<<16|rgb[1]<<8|rgb[2]))
  for p in points:data.extend([float(x) for x in np.r_[(p-origin)/height,normal]])
 model=dict(id=name,data=data,colors=colors,sourceHeight=height)
 (out/(name+'.json')).write_text(json.dumps(model,separators=(',',':'))+'\n')
 (out/(name+'-receipt.json')).write_text(json.dumps(dict(source=str(source.relative_to(root)),sha256=hashlib.sha256(raw).hexdigest(),triangles=len(colors),conversion='All non-degenerate source triangles with node transforms; sampled source base colour per face; native photograph uses lit vertex colour, not original full PBR.',originalUnchanged=True),indent=2)+'\n')
 print(name,len(colors),'triangles')
