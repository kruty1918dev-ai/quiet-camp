"""Read-only geometry review. Authored meshes/season resolver, never substitute models.
Blender materials, frozen river (before runtime rebuild), meadow/weather and HUD
frustum are explicitly not a Unity visual-equivalence claim.
"""
import bpy,sys,json,gzip,math,time,re
from pathlib import Path
from mathutils import Matrix,Vector
import numpy as np
args=sys.argv[sys.argv.index('--')+1:];bundle=Path(args[0]);out=Path(args[1]);root=Path(args[2]);out.mkdir(parents=True,exist_ok=True)
with gzip.open(bundle,'rt') as f:d=json.load(f)
art=d['art'];level=d['level'];order=d['order'];started=time.time();records=[]
bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene
scene.render.engine=args[3] if len(args)>3 else 'CYCLES';scene.cycles.device='CPU';scene.cycles.samples=16;scene.cycles.use_denoising=False
scene.render.threads_mode='FIXED';scene.render.threads=2;scene.render.image_settings.file_format='PNG'
scene.render.image_settings.color_mode='RGB';scene.view_settings.view_transform='Standard';scene.view_settings.exposure=0
scene.world=bpy.data.worlds.new('World');scene.world.use_nodes=True
at=art.get('atmosphere') or {};sky=at.get('ambientColor',art['ambientColor'])
def rgb(n):return [(n>>16&255)/255,(n>>8&255)/255,(n&255)/255]
def linear(v):v=np.array(v);return np.where(v<=.04045,v/12.92,((v+.055)/1.055)**2.4)
world=scene.world.node_tree.nodes.get('Background');world.inputs['Color'].default_value=(*rgb(sky),1);world.inputs['Strength'].default_value=.65
T=Matrix(((1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)))
def euler(v):
 x,y,z=[math.radians(a) for a in v];return Matrix.Rotation(y,4,'Y')@Matrix.Rotation(x,4,'X')@Matrix.Rotation(z,4,'Z')
def transform(i):return T@Matrix.Translation((i['x'],i.get('y',0),i['z']))@euler(i.get('rotationEuler') or [0,i.get('yaw',0),0])@Matrix.Diagonal((*((i.get('scaleXYZ') or [i.get('scale',1)]*3)),1))@T.inverted()
def material(name,water=False):
 m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;p=n.get('Principled BSDF')
 if water:
  p.inputs['Base Color'].default_value=(*linear([.22,.40,.38]),1);p.inputs['Roughness'].default_value=.25;p.inputs['IOR'].default_value=1.333;p.inputs['Transmission Weight'].default_value=.16
 else:
  attr=n.new('ShaderNodeVertexColor');attr.layer_name='GameColor';m.node_tree.links.new(attr.outputs['Color'],p.inputs['Base Color']);p.inputs['Roughness'].default_value=.92
  if at.get('snowCover',0)>0:
   geo=n.new('ShaderNodeNewGeometry');sep=n.new('ShaderNodeSeparateXYZ');m.node_tree.links.new(geo.outputs['Normal'],sep.inputs[0]);ramp=n.new('ShaderNodeMapRange');ramp.inputs['From Min'].default_value=.35;ramp.inputs['From Max'].default_value=.9;ramp.inputs['To Max'].default_value=at['snowCover'];m.node_tree.links.new(sep.outputs['Z'],ramp.inputs['Value']);mix=n.new('ShaderNodeMixRGB');mix.blend_type='MIX';mix.inputs[2].default_value=(*linear([.87,.90,.9]),1);m.node_tree.links.new(ramp.outputs['Result'],mix.inputs[0]);m.node_tree.links.new(attr.outputs['Color'],mix.inputs[1]);m.node_tree.links.new(mix.outputs[0],p.inputs['Base Color'])
 return m
solid=material('Authored vertex colours (Blender approximation)');watermat=material('Frozen water surface (not Unity shader)',True)
def mesh_data(name,vertices,triangles,colors,land=False):
 v=np.asarray(vertices,dtype=np.float32).reshape(-1,3);v=v[:,[0,2,1]]
 f=np.asarray(triangles if triangles is not None else np.arange(len(v)),dtype=np.int32).reshape(-1,3)[:,[0,2,1]]
 m=bpy.data.meshes.new(name);m.from_pydata(v.tolist(),[],f.tolist());m.update()
 rgba=np.asarray(colors,dtype=np.float32)
 if not land:
  c=np.asarray(colors,dtype=np.int32);rgba=np.stack(((c>>16&255)/255,(c>>8&255)/255,(c&255)/255,np.ones(len(c))),axis=1)
 else:rgba=rgba.reshape(-1,4)
 rgba[:,:3]=linear(rgba[:,:3]);rgba[:,3]=1
 ca=m.color_attributes.new(name='GameColor',type='FLOAT_COLOR',domain='POINT');ca.data.foreach_set('color',rgba.ravel())
 return m
models={};rawmodels={m['id']:m for m in art['models']}
for m in art['models']:
 if not m['vertices']:continue
 data=mesh_data(m['id'],m['vertices'],None,m['colors']);data.materials.append(solid);models[m['id']]=data
for j,m in enumerate(art['landscape']['meshes']):
 data=mesh_data(m['name'],m['vertices'],m['triangles'],m['colors'],True);data.materials.append(watermat if m.get('water') else solid);o=bpy.data.objects.new(m['name'],data);scene.collection.objects.link(o)
 # Frozen mesh coordinates are already converted; do not apply T a second time.
 o.pass_index=30000+j
for i in art['instances']:
 model=rawmodels.get(i.get('modelId'));base={'id':i.get('id'),'modelId':i.get('modelId'),'sourceModelId':model.get('sourceModelId') if model else None,'assetId':i.get('assetId'),'source':'authored-instance','position':[i['x'],i.get('y',0),i['z']],'motion':i.get('motion'),'seasonalRole':model.get('seasonalRole') if model else None}
 if i.get('modelId') not in models:base.update(rendered=False,reason='Seasonally empty mesh' if model and not model['vertices'] else 'Prefab/mesh not resolved');records.append(base);continue
 if (i.get('id') or '').startswith('regrowth/') and model.get('shading')=='foliage':
  h=2166136261
  for c in i['id']:h=((h^ord(c))*16777619)&0xffffffff
  border=max(abs(i['x'])-art['width']*.5,abs(i['z'])-art['height']*.5)
  band=0 if model.get('seasonalRole')=='flower' or border<2 else h%3
  if band>1:base.update(rendered=False,reason='Balanced quality regrowth band2');records.append(base);continue
 o=bpy.data.objects.new(i.get('id') or i['modelId'],models[i['modelId']]);scene.collection.objects.link(o);o.matrix_world=transform(i);o.pass_index=len(records)+1;base.update(rendered=True,objectIndex=o.pass_index,blenderName=o.name);records.append(base)
# Exact source OBJ mesh, Unity base materials, real seasonal geometry/palette.
# C# exported these through the actual runtime seasonal classes, without Unity binaries.
tents=[]
for r in d.get('gameplayModels',[]):
 model=r['model'];data=mesh_data(r['assetId'],model['vertices'],None,model['colors']);data.materials.append(solid)
 o=bpy.data.objects.new(r['id'],data);scene.collection.objects.link(o);o.matrix_world=T@Matrix.Translation(r['position'])@euler([0,r['yaw'],0])@T.inverted()
 record={k:v for k,v in r.items() if k!='model'};record.update(rendered=True,objectIndex=len(records)+1,blenderName=o.name);o.pass_index=record['objectIndex'];records.append(record)
 if r.get('state')=='solved':o.hide_render=True;o.hide_viewport=True;tents.append(o)
# A restrained actual-scale grid identifies the playable board; not a marketing camera.
for axis,size in [(0,level['width']),(2,level['height'])]:
 for n in range(size+1):
  x=n-size/2;v=[]
  if axis==0:v=[x-.009,.006,-level['height']/2,x+.009,.006,-level['height']/2,x+.009,.006,level['height']/2,x-.009,.006,level['height']/2]
  else:v=[-level['width']/2,.006,x-.009,level['width']/2,.006,x-.009,level['width']/2,.006,x+.009,-level['width']/2,.006,x+.009]
  data=mesh_data('Grid',v,[0,1,2,0,2,3],[0x667653]*4);data.materials.append(solid);o=bpy.data.objects.new('Board grid',data);scene.collection.objects.link(o)
sun=bpy.data.lights.new('Authored sun','SUN');sun.energy=at.get('sunIntensity',art['sunIntensity'])*2;sun.angle=math.radians(5);sun.color=rgb(at.get('sunColor',art['sunColor']));light=bpy.data.objects.new('Authored sun',sun);scene.collection.objects.link(light);sr=T@euler(at.get('sunEuler',art['sunEuler']));direction=sr.to_3x3()@Vector((0,0,1));light.rotation_euler=direction.to_track_quat('-Z','Y').to_euler()
camdata=bpy.data.cameras.new('Source camera direction / fitted board');cam=bpy.data.objects.new('Source camera direction / fitted board',camdata);scene.collection.objects.link(cam);scene.camera=cam;camdata.type='ORTHO';camdata.clip_end=150
rot=T@euler(art['cameraEuler']);right=rot.to_3x3()@Vector((1,0,0));up=rot.to_3x3()@Vector((0,1,0));forward=rot.to_3x3()@Vector((0,0,1));cam.rotation_euler=Matrix((right,up,-forward)).transposed().to_euler();cam.location=-forward*25
if scene.render.engine=='BLENDER_WORKBENCH':
 scene.display.shading.light='STUDIO';scene.display.shading.color_type='VERTEX'
 scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True
 scene.display.shading.cavity_type='SCREEN';scene.display.shading.curvature_ridge_factor=1.2
 scene.display.shading.curvature_valley_factor=.8;scene.display.shading.show_specular_highlight=False
 scene.display.shading.background_type='WORLD';scene.display.render_aa='FXAA'
 scene.display.light_direction=-direction.normalized()
bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
shade=[];to_sun=-direction.normalized()
for x,z in level['shade']:
 p=T.to_3x3()@Vector((x+.5-level['width']/2,.06,z+.5-level['height']/2));hit,loc,n,face,ob,matrix=scene.ray_cast(deps,p,to_sun,distance=150);shade.append({'cell':[x,z],'authoredGeometryShadow':bool(hit),'caster':ob.name if hit else None})
# Mathematically fit the actual board bounds to the source camera direction.
# HUD reserve below is a disclosed approximation: runtime safe-area/text layout is not available offline.
frame=[]
for mode,width,height,reserve in [('landscape',960,720,.76),('portrait',540,960,.62)]:
 scene.render.resolution_x=width;scene.render.resolution_y=height;scene.render.resolution_percentage=100
 ext=[level['width']/2+.4,.4,level['height']/2+.4]
 points=[T.to_3x3()@Vector((sx*ext[0],sy*ext[1],sz*ext[2])) for sx in [-1,1] for sy in [-1,1] for sz in [-1,1]]
 maxx=max(abs(right.dot(p)) for p in points);maxy=max(abs(up.dot(p)) for p in points);camdata.ortho_scale=2*max(maxx/(width/height)*1.03,maxy/reserve)
 scene.render.filepath=str(out/f'{order:03d}-{mode}.png');bpy.ops.render.render(write_still=True)
 frame.append({'path':f'{order:03d}-{mode}.png','width':width,'height':height,'sourceEuler':art['cameraEuler'],'orthographicScale':camdata.ortho_scale,'hudReserveAssumption':reserve})
 if scene.render.engine=='BLENDER_WORKBENCH':
  shading=scene.display.shading;before=(shading.light,shading.color_type,shading.show_shadows,shading.show_cavity,shading.background_type,tuple(shading.background_color),scene.display.render_aa)
  shading.light='FLAT';shading.color_type='OBJECT';shading.show_shadows=False;shading.show_cavity=False;shading.background_type='VIEWPORT';shading.background_color=(0,0,0);scene.display.render_aa='OFF'
  for ob in scene.objects:
   if ob.type=='MESH':ob.color=(*linear(rgb(ob.pass_index)),1)
  ids_path=out/f'{order:03d}-{mode}-ids.png';scene.render.filepath=str(ids_path);bpy.ops.render.render(write_still=True)
  image=bpy.data.images.load(str(ids_path),check_existing=False);image.colorspace_settings.name='Non-Color';pixels=np.empty(len(image.pixels),dtype=np.float32);image.pixels.foreach_get(pixels)
  pix=(np.clip(pixels.reshape(-1,4)[:,:3],0,1)*255+.5).astype(np.int32);indices=pix[:,0]*65536+pix[:,1]*256+pix[:,2];unique,counts=np.unique(indices,return_counts=True);counts=dict(zip(unique.tolist(),counts.tolist()))
  for r in records:
   if r.get('rendered'):r.setdefault('visiblePixels',{})[mode]=counts.get(r['objectIndex'],0)
  bpy.data.images.remove(image)
  shading.light,shading.color_type,shading.show_shadows,shading.show_cavity,shading.background_type,shading.background_color,scene.display.render_aa=before

 # Per-object visibility estimates use projected geometry bounds; pixel-perfect occlusion is not claimed.
 from bpy_extras.object_utils import world_to_camera_view
 for r in records:
  if not r.get('rendered'):continue
  ob=scene.collection.objects.get(r.get('blenderName',r['id']))
  if ob is None:continue
  p=[world_to_camera_view(scene,cam,ob.matrix_world@Vector(c)) for c in ob.bound_box];r.setdefault('frustum',{})[mode]=bool(max(v.x for v in p)>=0 and min(v.x for v in p)<=1 and max(v.y for v in p)>=0 and min(v.y for v in p)<=1)
# The third state includes the real tent source meshes in the actual reference solution.
scene.render.resolution_x=960;scene.render.resolution_y=720
points=[T.to_3x3()@Vector((sx*ext[0],sy*ext[1],sz*ext[2])) for sx in [-1,1] for sy in [-1,1] for sz in [-1,1]]
camdata.ortho_scale=2*max(max(abs(right.dot(p)) for p in points)/(960/720)*1.03,max(abs(up.dot(p)) for p in points)/.76)
for ob in tents:ob.hide_render=False;ob.hide_viewport=False
scene.render.filepath=str(out/f'{order:03d}-solved.png');bpy.ops.render.render(write_still=True)
frame.append({'path':f'{order:03d}-solved.png','state':'saved-reference-layout','sourceEuler':art['cameraEuler']})
# Detail views keep the player's camera direction; target/scale differ and are labelled diagnostics.
for r in records:
 source=(r.get('sourceModelId') or r.get('modelId') or '')
 if not r.get('rendered') or not any(k in source for k in ['well','watermill','footbridge']):continue
 ob=scene.collection.objects.get(r.get('blenderName',r['id']))
 if ob is None:continue
 corners=[ob.matrix_world@Vector(p) for p in ob.bound_box];centre=sum(corners,Vector())/len(corners)
 cam.location=centre-forward*25;camdata.ortho_scale=max(2,max((p-centre).length for p in corners)*1.7)
 name=source.replace('/','_');scene.render.filepath=str(out/f'{order:03d}-detail-{name}.png');bpy.ops.render.render(write_still=True)
 frame.append({'path':f'{order:03d}-detail-{name}.png','state':'diagnostic-detail-same-direction','targetObject':r['id'],'notGameplayFraming':True})
result={'order':order,'id':d['id'],'source':d['source'],'scope':d['scope'],'seconds':time.time()-started,'renderer':bpy.app.version_string,'quality':'Balanced authored regrowth bands; source-season model resolution','frames':frame,'shade':shade,'objects':records,'limitations':['Not Unity Game View. HUD reserve estimated from documented layout, orientation and board-fit math retained.','Frozen authored river, not runtime RiverLandscapeGeometry/river-bank extra meshes.','Procedural meadow, motion, weather, screen effects and dynamic cloth/story items are not reproduced; tents use original OBJ and reference poses.','Visible pixels refer to Blender colour-ID pass of included opaque geometry; missing procedural effects and translucent water are not native visibility proof.','Gameplay OBJ axes/importer smoothing may differ; real source palette and seasonal branch geometry are resolved in C#.']}
(out/f'{order:03d}-inventory.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print('AUDIT_RENDER_DONE',order,round(result['seconds'],2),flush=True)
