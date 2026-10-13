"""Install pinned, freely redistributable authoring sources without launching Unity."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import urllib.request
import uuid
import zipfile
import math
import tarfile
import re

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / 'QuietCamp/Temp/FreeWorldAssets'
SOURCES = {
    'meshsimplifier': ('https://codeload.github.com/Whinarn/UnityMeshSimplifier/zip/53fdb3122645bcd3ad2c258235200dff3dfbaa9b', 'MIT'),
    'ezyslice': ('https://codeload.github.com/DavidArayan/ezy-slice/zip/073efdcaf595a25f1086bdc5b7a23335a80a6755', 'MIT'),
    'nature': ('https://kenney.nl/media/pages/assets/nature-kit/37ac38a37b-1677698939/kenney_nature-kit.zip', 'CC0-1.0'),
    'survival': ('https://kenney.nl/media/pages/assets/survival-kit/4065a8185b-1712149243/kenney_survival-kit.zip', 'CC0-1.0'),
}
ARCHIVE_SHA256 = {
    'meshsimplifier': 'fc2f4229268c3c49574be199be4b9d575917eaac1b052e51cef294a5ebe0b52a',
    'ezyslice': '52393db125ea4cb06f2ab7eb5659c656069187fd008b3f7e258bfccb95036cde',
    'nature': 'fa7974a0d342bfe63c38664ba9f8ec1a4aab8ea25f099bdc56870e33588c4d9d',
    'survival': 'c3586341b5932c87eb43d75d915434f47daed168b17ed36a03e8ca9977c7443e',
}
# Source name, Ukrainian label, category, target metres, sizing axis, brush eligibility.
NATURE = [
    ('tree_oak','Дуб — широка крона','Дерева',9,'height',False),
    ('tree_default','Листяне дерево','Дерева',8,'height',False),
    ('tree_thin','Струнке листяне дерево','Дерева',10,'height',False),
    ('tree_tall','Високе листяне дерево','Дерева',12,'height',False),
    ('tree_pineTallA','Сосна висока А','Дерева',13,'height',False),
    ('tree_pineTallB','Сосна висока Б','Дерева',11,'height',False),
    ('tree_pineRoundA','Сосна з широкою кроною','Дерева',8,'height',False),
    ('tree_pineSmallA','Молода сосна','Дерева',3,'height',False),
    ('stump_old','Старий пень','Лісові деталі',.7,'height',False),
    ('stump_roundDetailed','Пень із корінням','Лісові деталі',.6,'height',False),
    ('stump_squareDetailedWide','Широкий пень','Лісові деталі',.55,'height',False),
    ('log','Повалена колода','Лісові деталі',3,'longest',False),
    ('log_large','Велика повалена колода','Лісові деталі',4.5,'longest',False),
    ('log_stack','Складені колоди','Лісові деталі',2,'longest',False),
    ('plant_bush','Лісовий кущ','Кущі',.9,'height',True),
    ('plant_bushDetailed','Розлогий кущ','Кущі',1.1,'height',True),
    ('plant_bushSmall','Молодий кущ','Кущі',.4,'height',True),
    ('plant_flatShort','Низький підлісок','Кущі',.3,'height',True),
    ('grass_large','Високий пучок трави','Трава',.4,'height',True),
    ('grass_leafsLarge','Широколиста трава','Трава',.3,'height',True),
    ('mushroom_redGroup','Група червоних грибів','Лісові деталі',.18,'height',True),
    ('mushroom_tanGroup','Група лісових грибів','Лісові деталі',.16,'height',True),
    ('mushroom_red','Окремий червоний гриб','Лісові деталі',.14,'height',True),
    ('rock_largeA','Лісовий валун А','Каміння',1.6,'longest',False),
    ('rock_largeC','Лісовий валун Б','Каміння',1.9,'longest',False),
    ('rock_smallFlatA','Плаский лісовий камінь','Каміння',.7,'longest',False),
    ('rock_tallA','Витягнутий камінь','Каміння',1.3,'height',False),
    ('flower_purpleA','Лісові фіолетові квіти','Квіти',.3,'height',True),
    ('flower_yellowB','Лісові жовті квіти','Квіти',.32,'height',True),
    ('flower_redC','Лісові червоні квіти','Квіти',.28,'height',True),
    ('fence_gate','Дерев’яна хвіртка','Паркани',1.2,'height',False),
    ('fence_planks','Паркан із дощок','Паркани',1.1,'height',False),
]
SURVIVAL = [
    ('bedroll-packed','Згорнутий спальник','Табірні деталі',.5,'longest',False),
    ('bedroll','Розкладений спальник','Табірні деталі',1.9,'longest',False),
    ('bottle','Похідна пляшка','Табірні деталі',.28,'height',False),
    ('tool-axe','Лісова сокира','Табірні деталі',.65,'longest',False),
    ('tool-shovel','Лопата','Табірні деталі',1.1,'longest',False),
    ('signpost','Старий дерев’яний вказівник','Табірні деталі',1.8,'height',False),
    ('floor-old','Старий дощатий настил','Табірні деталі',2,'longest',False),
    ('metal-panel','Металева панель уламка','Табірні деталі',1.4,'longest',False),
]

def fetch(name, url):
    CACHE.mkdir(parents=True, exist_ok=True)
    target = CACHE / (name + '.zip')
    if not target.exists():
        request = urllib.request.Request(url, headers={'User-Agent':'QuietCamp-Free-Asset-Installer'})
        data = urllib.request.urlopen(request, timeout=90).read()
        target.write_bytes(data)
    with zipfile.ZipFile(target) as archive:
        if archive.testzip(): raise ValueError('Invalid archive: '+name)
    if hashlib.sha256(target.read_bytes()).hexdigest() != ARCHIVE_SHA256[name]:
        raise ValueError('Source archive changed; review before import: '+name)
    return target

def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    raw = data if isinstance(data, bytes) else data.encode('utf-8')
    if not path.exists() or path.read_bytes() != raw: path.write_bytes(raw)

def meta(path, project):
    relative = path.relative_to(project).as_posix()
    sidecar = Path(str(path)+'.meta')
    if sidecar.exists(): return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'quietcamp-free-assets/'+relative).hex
    write(sidecar, 'fileFormatVersion: 2\nguid: '+guid+'\n')

def install_polybrush(project):
    """Embed the official package and preserve full 64-bit mesh ownership IDs in Unity 6.6."""
    archive_path=CACHE/'polybrush-1.2.1.tgz'
    url='https://packages.unity.com/com.unity.polybrush/-/com.unity.polybrush-1.2.1.tgz'
    if not archive_path.exists(): archive_path.write_bytes(urllib.request.urlopen(url,timeout=90).read())
    digest=hashlib.sha256(archive_path.read_bytes()).hexdigest()
    if digest!='5301457572f17e32b4892b6cd782e43fb765a07771f29bffcd43b56fbbda3975':
        raise ValueError('Polybrush source archive changed; review before import')
    dest=project/'Packages/com.unity.polybrush'
    with tarfile.open(archive_path,'r:gz') as archive:
        for file in archive.getmembers():
            relative=PurePosixPath(file.name).parts[1:]
            if not file.isfile() or not relative or '..' in relative: continue
            data=archive.extractfile(file).read()
            if file.name.endswith('/Editor/Classes/PolyAsset.cs'):
                data=data.replace(b'AssetDatabase.GetAssetPath(this.GetInstanceID())',b'AssetDatabase.GetAssetPath(this)')
            if file.name.endswith('/Editor/Classes/EditableObject.cs'):
                text=data.decode('utf-8-sig')
                pattern=r'internal static int GetMeshId\(Mesh mesh\)\s*\{.*?\n\s*\}\s*\n\s*static bool MeshInstanceMatchesGameObject'
                replacement='''internal static EntityId GetMeshId(Mesh mesh)
        {
            if (mesh == null || !mesh.name.StartsWith(k_MeshInstancePrefix)) return EntityId.None;
            ulong raw;
            return ulong.TryParse(mesh.name.Substring(k_MeshInstancePrefix.Length), out raw)
                ? EntityId.FromULong(raw) : EntityId.None;
        }

        static bool MeshInstanceMatchesGameObject'''
                text,count=re.subn(pattern,replacement,text,flags=re.S)
                if count!=1: raise ValueError('Polybrush ownership API changed')
                text=text.replace('gameObjectAttached.GetInstanceID()', 'EntityId.ToULong(gameObjectAttached.GetEntityId())')
                text=text.replace('int gameObjectId = go.GetInstanceID();','var gameObjectId = go.GetEntityId();')
                text=text.replace('int meshId = GetMeshId(mesh);','var meshId = GetMeshId(mesh);')
                text=text.replace('meshId == -1','meshId == EntityId.None')
                text=text.replace('EditorUtility.InstanceIDToObject(meshId)','EditorUtility.EntityIdToObject(meshId)')
                text=text.replace('go.GetInstanceID()','EntityId.ToULong(go.GetEntityId())')
                data=text.encode('utf-8')
            if file.name.endswith('/Editor/Interface/HierarchyChanged.cs'):
                text=data.decode('utf-8-sig').replace('id != -1','id != EntityId.None')
                text=text.replace('id != gameObject.GetInstanceID()', 'id != gameObject.GetEntityId()')
                text=text.replace('EditorUtility.InstanceIDToObject(id)', 'EditorUtility.EntityIdToObject(id)')
                text=text.replace('gameObject.GetInstanceID()', 'EntityId.ToULong(gameObject.GetEntityId())')
                data=text.encode('utf-8')
            if file.name.endswith('/Editor/Utility/ReflectionUtility.cs'):
                data=data.replace(b'AppDomain.CurrentDomain.GetAssemblies()', b'UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies()')
            write(dest.joinpath(*relative),data)
    write(dest/'QUIETCAMP-COMPATIBILITY.md','Polybrush 1.2.1, official Unity registry archive SHA256 '+digest+'.\n\nUnity 6000.6 compatibility patch: PolyAsset uses the object overload of GetAssetPath. Mesh-instance ownership uses EntityId/ToULong/FromULong instead of obsolete int InstanceID. Reflection uses CurrentAssemblies instead of AppDomain, which can include unloaded assemblies in CoreCLR. Mesh names are editor-local ownership tokens, never stable content IDs. Unknown legacy names trigger a safe mesh copy. Original package GUIDs and Unity Companion License retained. Reproduce with tools/install_free_world_assets.py.\n')

def obj_model(archive, pack, spec):
    name,label,category,metres,axis,small = spec
    source = 'Models/OBJ format/'+name+'.obj'
    text = archive.read(source).decode('utf-8-sig')
    points=[]; uv=[]; triangles=[]; material=''
    materials={}; current=None
    for line in archive.read(source[:-4]+'.mtl').decode('utf-8-sig').splitlines():
        words=line.split()
        if words and words[0]=='newmtl': current=words[1]
        elif words and words[0]=='Kd': materials[current]=tuple(float(n) for n in words[1:4])
    for line in text.splitlines():
        w=line.split()
        if not w: continue
        if w[0]=='v': points.append(tuple(float(n) for n in w[1:4]))
        elif w[0]=='vt': uv.append(tuple(float(n) for n in w[1:3]))
        elif w[0]=='usemtl': material=w[1]
        elif w[0]=='f':
            indices=[]
            for token in w[1:]:
                fields=token.split('/');i=int(fields[0]);i=i-1 if i>0 else len(points)+i
                ti=int(fields[1]) if len(fields)>1 and fields[1] else 0
                indices.append((i, ti-1 if ti>0 else len(uv)+ti))
            for i in range(1,len(indices)-1): triangles.append(([indices[0],indices[i],indices[i+1]],material))
    low=[min(p[a] for p in points) for a in range(3)]
    high=[max(p[a] for p in points) for a in range(3)]
    size=[high[a]-low[a] for a in range(3)]
    factor=metres/(size[1] if axis=='height' else max(size))
    origin=((low[0]+high[0])/2,low[1],(low[2]+high[2])/2)
    positions=[];normals=[];colors=[]
    # Source palettes are deliberately muted; preserve hue families, not bright donor saturation.
    def tint(rgb):
        if category=='Каміння':
            value=.5+.12*(sum(rgb)/3);return (value*.99,value,value*.89)
        if pack=='survival':
            # The CC0 colormap stores palette swatches. Sample the OBJ UV in its actual texture.
            return rgb
        r,g,b=rgb
        if 'leaf' in mat.lower() or 'grass' in mat.lower(): return (.38,.46,.23)
        if 'wood' in mat.lower(): return (.43,.33,.22) if 'dark' in mat.lower() else (.55,.43,.28)
        return tuple(min(.88,max(.12,c*.7+.12)) for c in rgb)
    texture=None
    if pack=='survival':
        from PIL import Image
        import io
        texture=Image.open(io.BytesIO(archive.read('Models/OBJ format/Textures/colormap.png'))).convert('RGB')
    for indices,mat in triangles:
        p=[tuple((points[i][a]-origin[a])*factor for a in range(3)) for i,_ in indices]
        a=[p[1][j]-p[0][j] for j in range(3)];b=[p[2][j]-p[0][j] for j in range(3)]
        normal=[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
        length=math.sqrt(sum(n*n for n in normal))
        if length<1e-10: continue
        normal=[n/length for n in normal]
        for vertex,(_,ti) in zip(p,indices):
            rgb=materials.get(mat,(.5,.5,.5))
            if texture is not None and uv:
                u,v=uv[ti];rgb=tuple(c/255 for c in texture.getpixel((min(texture.width-1,max(0,int(u*texture.width))),min(texture.height-1,max(0,int((1-v)*texture.height))))))
                rgb=tuple(min(.88,max(.12,c*.72+.1)) for c in rgb)
            rgb=tint(rgb)
            positions.extend(round(c,6) for c in vertex);normals.extend(round(c,6) for c in normal)
            c=[round(v*255) for v in rgb];colors.append(c[0]<<16|c[1]<<8|c[2])
    stable='qc_kenney_'+pack+'_'+name.replace('-','_')
    model=dict(id=stable,shading='foliage' if small and category in ('Кущі','Трава','Квіти') else 'solid',vertices=positions,normals=normals,colors=colors)
    entry=dict(id=stable,label=label,category=category,smallPlant=small,source=source,sourcePack=pack,role='decor',targetMetres=metres,sizingAxis=axis,originalBounds=[low,high])
    return model,entry,source

def install(project, archives):
    project=project.resolve()
    if not (project/'ProjectSettings/ProjectVersion.txt').exists(): raise ValueError('Not a Unity project: '+str(project))
    install_polybrush(project)
    for name in ('meshsimplifier','ezyslice'):
        dest=project/'Packages'/('com.quietcamp.'+name+'.editor')
        with zipfile.ZipFile(archives[name]) as archive:
            for file in archive.namelist():
                relative=PurePosixPath(file).parts[1:]
                if not relative or '..' in relative: continue
                selected=relative[0] in (('Runtime','Editor','LICENSE.md','Third Party Notices.md','README.md') if name=='meshsimplifier' else ('EzySlice','LICENSE','README.md'))
                if not selected or file.endswith('/'): continue
                data=archive.read(file)
                if file.endswith('.asmdef'):
                    config=json.loads(data);config['includePlatforms']=['Editor'];data=(json.dumps(config,indent=2)+'\n').encode()
                write(dest.joinpath(*relative),data)
        write(dest/'package.json',json.dumps(dict(name=dest.name,version='1.0.0',displayName='QuietCamp: '+name+' (Editor only)',unity='6000.0',description='Pinned upstream MIT tool; source and license retained. Editor-only packaging for authoring.'),indent=2)+'\n')
        if name=='ezyslice':
            write(dest/'EzySlice/QuietCamp.EzySlice.Editor.asmdef',json.dumps(dict(name='QuietCamp.EzySlice.Editor',includePlatforms=['Editor'],references=[]),indent=2)+'\n')
        for file in list(dest.rglob('*')):
            if not file.name.endswith('.meta') and file.name!='package.json': meta(file,project)
    output=project/'Assets/QuietCamp/Authoring/FreeWorldAssets'
    source_root=project/'Assets/QuietCamp/Editor/FreeWorldSources'
    models=[];entries=[]
    for pack,specs in (('nature',NATURE),('survival',SURVIVAL)):
        with zipfile.ZipFile(archives[pack]) as archive:
            write(source_root/pack/'License.txt',archive.read('License.txt'))
            for spec in specs:
                model,entry,source=obj_model(archive,pack,spec);models.append(model);entries.append(entry)
                for file in (source,source[:-4]+'.mtl'):
                    write(source_root/pack/Path(file).name,archive.read(file))
            texture='Models/OBJ format/Textures/colormap.png'
            if texture in archive.namelist(): write(source_root/pack/'Textures/colormap.png',archive.read(texture))
    write(output/'models.json',json.dumps(dict(models=models),separators=(',',':'))+'\n')
    write(output/'catalog.json',json.dumps(dict(version=1,entries=entries),ensure_ascii=False,indent=2)+'\n')
    receipt=dict(units='metres',count=len(entries),sources={name:dict(url=SOURCES[name][0],license=SOURCES[name][1],sha256=hashlib.sha256(path.read_bytes()).hexdigest()) for name,path in archives.items()},registryPackages={'com.unity.probuilder':'6.1.3','com.unity.polybrush':'1.2.1'},polybrushCompatibility='Editor EntityId, official embedded package, reproducible patch')
    write(output/'installation.json',json.dumps(receipt,indent=2)+'\n')
    write(output/'ATTRIBUTION.md','# Free authoring sources\n\nKenney Nature Kit and Survival Kit: Kenney, https://kenney.nl/, CC0-1.0. Original licenses and OBJ/MTL files are retained in Editor/FreeWorldSources. Derived palette geometry uses metric sizing and muted colours.\n\nUnityMeshSimplifier: Mattias Edlund / Whinarn, MIT; upstream revision 53fdb3122645bcd3ad2c258235200dff3dfbaa9b.\n\nEzySlice: David Arayan, MIT; upstream revision 073efdcaf595a25f1086bdc5b7a23335a80a6755. Convex meshes only; no vertex-colour preservation in the upstream slicer.\n\nProBuilder 6.1.3 and Polybrush 1.2.1: Unity registry packages, subject to their included Unity package licenses.\n')
    for base in (output,source_root):
        for path in [base,*list(base.rglob('*'))]:
            if not path.name.endswith('.meta'): meta(path,project)
    manifest=project/'Packages/manifest.json';config=json.loads(manifest.read_text(encoding='utf-8-sig'))
    config['dependencies'].update(receipt['registryPackages'])
    config['dependencies'].update({'com.quietcamp.'+name+'.editor':'file:com.quietcamp.'+name+'.editor' for name in ('meshsimplifier','ezyslice')})
    write(manifest,json.dumps(config,indent=2)+'\n')
    print(json.dumps(dict(project=str(project),models=len(entries),triangles=sum(len(m['colors'])//3 for m in models))))

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--project',type=Path,action='append',required=True)
    args=parser.parse_args();archives={name:fetch(name,url) for name,(url,_) in SOURCES.items()}
    for project in args.project: install(project,archives)
