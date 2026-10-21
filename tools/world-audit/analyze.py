"""Read-only scene inventory and physical contact facts from frozen source meshes.
No generated authoring documents or review approvals are written.
"""
import csv,gzip,json,math,sys,collections
from pathlib import Path
import numpy as np
root=Path(sys.argv[1]);out=root/'analysis';out.mkdir(exist_ok=True)
def rotation(e):
 x,y,z=np.radians(e);cx,sx=math.cos(x),math.sin(x);cy,sy=math.cos(y),math.sin(y);cz,sz=math.cos(z),math.sin(z)
 return np.array([[cy,0,sy],[0,1,0],[-sy,0,cy]])@np.array([[1,0,0],[0,cx,-sx],[0,sx,cx]])@np.array([[cz,-sz,0],[sz,cz,0],[0,0,1]])
def world(m,p):return (np.asarray(m['vertices']).reshape(-1,3)*np.array(p.get('scaleXYZ') or [p.get('scale',1)]*3))@rotation(p.get('rotationEuler') or [0,p.get('yaw',0),0]).T+[p['x'],p.get('y',0),p['z']]
def height(land,x,z):
 n=land['resolution'];u=np.clip((x-land['originX'])/land['size']*(n-1),0,n-1);v=np.clip((z-land['originZ'])/land['size']*(n-1),0,n-1);a=min(int(u),n-2);b=min(int(v),n-2);h=land['heights'];return float(((1-u+a)*h[b*n+a]+(u-a)*h[b*n+a+1])*(1-v+b)+((1-u+a)*h[(b+1)*n+a]+(u-a)*h[(b+1)*n+a+1])*(v-b))
def water_sections(land):
 segments=[]
 for m in land['meshes']:
  if not m.get('water'):continue
  v=np.asarray(m['vertices']).reshape(-1,3);uv=np.asarray(m['uv']).reshape(-1,2);rows=[];i=0
  while i<len(v):
   j=i+1
   while j<len(v) and abs(uv[i,1]-uv[j,1])<.0001:j+=1
   if j-i<2:rows=[];break
   side=(v[j-1]-v[i])[[0,2]];width=np.linalg.norm(side);rows.append(((v[i]+v[j-1])/2,side/width,width/2));i=j
  segments.extend(zip(rows,rows[1:]))
 return segments
def water_at(segments,x,z):
 best=None;nearest=math.inf;p=np.array([x,z])
 for a,b in segments:
  start=a[0][[0,2]];delta=b[0][[0,2]]-start;length=np.linalg.norm(delta)
  if length<.0001:continue
  t=float(np.clip(np.dot(p-start,delta)/(length*length),0,1));centre=a[0]*(1-t)+b[0]*t;offset=p-centre[[0,2]];distance=np.dot(offset,offset);long=np.dot(offset,delta/length)
  if distance>=nearest or (t==0 and long<-.01) or (t==1 and long>.01):continue
  side=a[1]*(1-t)+b[1]*t;side/=np.linalg.norm(side);half=a[2]*(1-t)+b[2]*t;across=np.dot(offset,side)
  best={'surfaceY':float(centre[1]),'signedBankDistance':float(abs(across)-half),'wet':bool(abs(across)<=half),'flowDirection':(delta/length).tolist(),'halfWidth':float(half)};nearest=distance
 return best
def mesh_y(v,x,z):
 t=v.reshape(-1,3,3);a=t[:,0];b=t[:,1];c=t[:,2];den=(b[:,2]-c[:,2])*(a[:,0]-c[:,0])+(c[:,0]-b[:,0])*(a[:,2]-c[:,2]);mask=np.abs(den)>1e-10;u=np.zeros(len(t));w=u.copy();u[mask]=((b[:,2]-c[:,2])*(x-c[:,0])+(c[:,0]-b[:,0])*(z-c[:,2]))[mask]/den[mask];w[mask]=((c[:,2]-a[:,2])*(x-c[:,0])+(a[:,0]-c[:,0])*(z-c[:,2]))[mask]/den[mask];valid=mask&(u>=-1e-5)&(w>=-1e-5)&(u+w<=1.00001);hits=(u*a[:,1]+w*b[:,1]+(1-u-w)*c[:,1])[valid];return float(max(hits)) if len(hits) else None
summaries=[];sources=set();unknown=[]
for file in sorted((root/'bundles').glob('*.gz')):
 d=json.load(gzip.open(file,'rt'));a=d['art'];l=d['level'];models={m['id']:m for m in a['models']};land=a['landscape'];segments=water_sections(land);catalog=[];special=[]
 for m in a['models']:
  source=m.get('sourceModelId') or m['id'];sources.add(source);label=''
  try:label=json.loads(m.get('authoringRecipe') or '{}').get('label','')
  except (ValueError,TypeError):pass
  v=np.array(m['vertices']).reshape(-1,3);deg=0
  if len(v):
   t=v.reshape(-1,3,3);deg=int((np.linalg.norm(np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0]),axis=1)<1e-10).sum())
  catalog.append({'modelId':m['id'],'sourceModelId':source,'role':m.get('seasonalRole'),'label':label,'triangles':len(v)//3,'degenerateTriangles':deg})
  if '???' in label:unknown.append({'order':d['order'],'modelId':m['id'],'sourceModelId':source,'problem':'Corrupted authoring label, geometry identity retained; not proof of an unrelated object'})
 for p in a['instances']:
  m=models.get(p.get('modelId'));source=(m or {}).get('sourceModelId') or p.get('modelId') or p.get('assetId') or ''
  if not m or not (any(k in source for k in ['well','footbridge','watermill','aircraft']) or p.get('id')==a.get('primaryLandmarkInstanceId')):continue
  v=world(m,p)
  if not len(v):continue
  row={'id':p['id'],'sourceModelId':source,'boundsMin':v.min(axis=0).tolist(),'boundsMax':v.max(axis=0).tolist(),'sourcePosition':[p['x'],p.get('y',0),p['z']],'groundAtOrigin':height(land,p['x'],p['z']),'waterAtOrigin':water_at(segments,p['x'],p['z'])}
  if 'footbridge' in source:
   local=np.array(m['vertices']).reshape(-1,3);lo,hi=local[:,2].min(),local[:,2].max();r=rotation(p.get('rotationEuler') or [0,p.get('yaw',0),0]);samples=[]
   for label,q in [('approach-A',lo-.65),('end-A',lo+.15),('midpoint',(lo+hi)/2),('end-B',hi-.15),('approach-B',hi+.65)]:
    point=np.array([0,0,q])@r.T+[p['x'],p.get('y',0),p['z']];x,z=float(point[0]),float(point[2]);deck=mesh_y(v,x,z);wet=water_at(segments,x,z);ground=height(land,x,z);samples.append({'where':label,'xz':[x,z],'deckTopY':deck,'terrainY':ground,'terrainAboveDeck':None if deck is None else ground-deck,'water':wet})
   row['crossingSamples']=samples
  if 'watermill_wheel' in source:
   centre=(v.min(axis=0)+v.max(axis=0))/2;wet=water_at(segments,float(centre[0]),float(centre[2]));normal=rotation(p.get('rotationEuler') or [0,p.get('yaw',0),0])@np.array([1,0,0]);row['wheelCentre']=centre.tolist();row['waterAtWheel']=wet
   if wet:row.update(wheelLowestY=float(v[:,1].min()),waterAboveWheelBottom=float(wet['surfaceY']-v[:,1].min()),flowDotWheelAxle=abs(float(np.dot(normal[[0,2]],wet['flowDirection']))))
  special.append(row)
 record={'order':d['order'],'id':d['id'],'source':d['source'],'sourceValidationFailure':d.get('sourceValidationFailure'),'season':(a.get('atmosphere') or {}).get('season'),'primaryInstanceId':a.get('primaryLandmarkInstanceId'),'authoredInstances':len(a['instances']),'gameplayInstances':len(d.get('gameplayModels',[])),'models':catalog,'geography':special,'shadeCells':l['shade'],'limitations':['Facts describe frozen source geometry before the runtime river/meadow/weather rebuild.','Supports need native validation against rebuilt terrain.','No automatic artistic acceptance or per-device FPS claim.']}
 (out/f"{d['order']:03}-facts.json").write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n');summaries.append({k:v for k,v in record.items() if k not in ('models','geography','limitations')});print('ANALYZED',d['order'],flush=True)
(out/'level-summary.json').write_text(json.dumps(summaries,ensure_ascii=False,indent=2)+'\n');(out/'source-model-ids.json').write_text(json.dumps(sorted(sources),ensure_ascii=False,indent=2)+'\n');(out/'metadata-label-issues.json').write_text(json.dumps(unknown,ensure_ascii=False,indent=2)+'\n')
