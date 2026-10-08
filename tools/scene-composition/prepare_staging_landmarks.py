"""Bake attributable donor geometry into anonymous, weathered diorama sections.
Inputs are GLB files already obtained from the credited sources. No network/install step.
"""
import hashlib,io,json,math,struct,sys
from pathlib import Path
import numpy as np
from PIL import Image
root=Path('QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Models/StagingLandmarks');root.mkdir(parents=True,exist_ok=True)
def read_glb(path):
 b=Path(path).read_bytes();assert b[:4]==b'glTF';n=struct.unpack_from('<I',b,12)[0];g=json.loads(b[20:20+n]);offset=20+n;size=struct.unpack_from('<I',b,offset)[0];binary=b[offset+8:offset+8+size]
 def accessor(i):
  a=g['accessors'][i];v=g['bufferViews'][a['bufferView']];cols={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']];fmt={5126:'f',5125:'I',5123:'H',5121:'B'}[a['componentType']];step=struct.calcsize(fmt)*cols;stride=v.get('byteStride',step);start=v.get('byteOffset',0)+a.get('byteOffset',0)
  return np.array([struct.unpack_from('<'+fmt*cols,binary,start+j*stride) for j in range(a['count'])])
 transforms={}
 def node(index,parent):
  n=g['nodes'][index];m=np.eye(4)
  if 'matrix'in n:m=np.array(n['matrix']).reshape((4,4),order='F')
  else:
   x,y,z,w=n.get('rotation',[0,0,0,1]);m[:3,:3]=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])@np.diag(n.get('scale',[1,1,1]));m[:3,3]=n.get('translation',[0,0,0])
  m=parent@m
  if 'mesh'in n:transforms[n['mesh']]=m
  for c in n.get('children',[]):node(c,m)
 for n in g['scenes'][g.get('scene',0)]['nodes']:node(n,np.eye(4))
 triangles=[]
 for mi,mesh in enumerate(g['meshes']):
  m=transforms.get(mi,np.eye(4))
  for p in mesh['primitives']:
   positions=accessor(p['attributes']['POSITION']);positions=(m@np.c_[positions,np.ones(len(positions))].T).T[:,:3]
   uv=accessor(p['attributes']['TEXCOORD_0']) if 'TEXCOORD_0'in p['attributes'] else np.zeros((len(positions),2));indices=accessor(p['indices']).astype(int).ravel();material=g['materials'][p.get('material',0)]['pbrMetallicRoughness'];factor=np.array(material.get('baseColorFactor',[1,1,1,1]),dtype=float)[:3];image=None
   if 'baseColorTexture'in material:
    texture=g['textures'][material['baseColorTexture']['index']];ii=texture.get('source',texture.get('extensions',{}).get('EXT_texture_webp',{}).get('source'));im=g['images'][ii];view=g['bufferViews'][im['bufferView']];data=binary[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']];image=Image.open(io.BytesIO(data)).convert('RGB')
   for ids in indices.reshape((-1,3)):
    color=factor.copy()
    if image:
     u,v=uv[ids].mean(axis=0);color*=np.array(image.getpixel((int(u%1*(image.width-1)),int((1-v%1)*(image.height-1)))))/255
    triangles.append((positions[ids].copy(),uv[ids].copy(),color))
 return triangles,b
models=[]
def clustered(triangles,cap):
 if len(triangles)<=cap:return triangles
 grid=.025
 while True:
  output=[]
  for points,uv,color in triangles:
   p=np.round(points/grid)*grid
   if np.linalg.norm(np.cross(p[1]-p[0],p[2]-p[0]))>1e-7:output.append((p,uv,color))
  if len(output)<=cap:return output
  grid*=1.3

def export(name,triangles):
 pts=np.concatenate([t[0] for t in triangles]);lo=pts.min(axis=0);hi=pts.max(axis=0);height=float(hi[1]-lo[1]);origin=(lo+hi)/2;origin[1]=lo[1];data=[];uvs=[];colors=[];obj=['# Derivative; see ATTRIBUTION.md. Metres, Y up.'];vi=1
 for points,uv,color in triangles:
  n=np.cross(points[1]-points[0],points[2]-points[0]);length=np.linalg.norm(n)
  if length<1e-8:continue
  n/=length
  # Neutral weathered palette; no original lettering/branding is transferred as text.
  color=np.clip(color*.77+np.array([.61,.62,.53])*.23,0,1);rgb=np.round(color*255).astype(int);colors.append(int(rgb[0]<<16|rgb[1]<<8|rgb[2]))
  for p,t in zip(points,uv):
   data.extend([round(float(x),6) for x in np.r_[(p-origin)/height,n]]);uvs.extend([round(float(x),6) for x in t]);obj.append('v '+' '.join(f'{x:.6f}'for x in p)+' '+' '.join(f'{x:.5f}'for x in color));obj.append('vt '+' '.join(f'{x:.6f}'for x in t));obj.append('vn '+' '.join(f'{x:.6f}'for x in n))
  obj.append('f '+' '.join(f'{j}/{j}/{j}' for j in range(vi,vi+3)));vi+=3
 (root/(name+'.obj')).write_text('\n'.join(obj)+'\n');models.append(dict(id=name,data=data,colors=colors,uvs=uvs,sourceHeight=height))
 return dict(id=name,triangles=len(colors),bounds=dict(min=lo.tolist(),max=hi.tolist()),height=height,width=float(hi[0]-lo[0]),depth=float(hi[2]-lo[2]))
def main():
 plane,pb=read_glb(sys.argv[1]);ship,sb=read_glb(sys.argv[2]);(root/'airplane-source.glb').write_bytes(pb);(root/'ship-source.glb').write_bytes(sb)
 # Cut the middle fuselage/wing intersection and distribute two separated physical sections.
 fore=[];tail=[]
 for p,uv,c in plane:
  mean=p[:,2].mean()
  if mean<-3.5:fore.append((p*.31,uv,c))
  elif mean>3.5:tail.append((p*.31,uv,c))
 # All donor triangles retained in the side selected by cut, without random triangle removal.
 metadata=[export('staging_aircraft_tail',clustered(fore,6000)),export('staging_aircraft_fore',clustered(tail,6000))]
 for name,geometry in [('staging_aircraft_tail',fore),('staging_aircraft_fore',tail)]:
  metadata.append(export(name+'_coarse',clustered(geometry,1500)))
  metadata.append(export(name+'_silhouette',clustered(geometry,200)))
 ship_positions=np.concatenate([t[0]for t in ship]);span=np.ptp(ship_positions,axis=0);scale=31/max(span[0],span[2]);ship=[(p*scale,uv,c)for p,uv,c in ship];metadata.append(export('staging_cargo_ship',ship));metadata.append(export('staging_cargo_ship_coarse',clustered(ship,1500)));metadata.append(export('staging_cargo_ship_silhouette',clustered(ship,200)))
 (root/'models.json').write_text(json.dumps(models,separators=(',',':'))+'\n')
 (root/'manifest.json').write_text(json.dumps(dict(staging=True,published=False,sourceHashes={'airplane':hashlib.sha256(pb).hexdigest(),'ship':hashlib.sha256(sb).hexdigest()},models=metadata),indent=2)+'\n')
 print(json.dumps(metadata,indent=2))

if __name__=="__main__":main()
