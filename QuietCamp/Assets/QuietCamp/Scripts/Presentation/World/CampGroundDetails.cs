using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Owns scene-local tent pressure and weather changes to the natural surface.
    /// Scene-local presentation; there are no fictitious steps when tents or routes change.</summary>
    public sealed class CampGroundDetails : MonoBehaviour
    {
        public const int MaximumPrints=256;
        WorldWellShafts.Opening[] _wellOpenings=Array.Empty<WorldWellShafts.Opening>();
        bool InWell(float x,float z){for(int i=0;i<_wellOpenings.Length;i++)if(_wellOpenings[i].Contains(x,z))return true;return false;}
        LevelData _level; CampArtDefinition _art; Material _ground,_fieldMaterial;
        CampAtmosphere _atmosphere; SeasonAtmosphereView _previewWeather; Func<bool> _paused,_reduced;
        Texture2D _useTexture; Color32[] _pixels; float _refresh,_melt=1,_printClock;
        readonly List<Material> _bound=new List<Material>();
        readonly List<Transform> _tufts=new List<Transform>();
        readonly List<Vector3> _tuftScales=new List<Vector3>();
        GameObject _fieldCover,_pebbleCover;
        public Rect VegetationBounds {get;private set;}
        public int VegetationPatchCount {get;private set;}
        public int SnowPatchCount {get;private set;}
        public CampSurfaceUseField Surface {get;private set;}
        public int PrintCount=>0;
        public IReadOnlyList<Vector3> PrintCentres=>Array.Empty<Vector3>();
        public float MeltAmount=>_melt;
        public int Residents=>0;
        public void Configure(LevelData level,Material ground,CampArtDefinition art=null)
        {
            _level=level;_ground=ground;_art=art;_wellOpenings=WorldWellShafts.Find(art);
            VegetationBounds=CoverBounds(art?.fieldCover??new CampArtDefinition.FieldCoverSettings());
            var finish=art?.groundSurface??new CampArtDefinition.GroundSurfaceSettings();
            if(ground!=null&&ground.HasProperty("_GroundFinish"))ground.SetVector("_GroundFinish",new Vector4(finish.patchScale,finish.facetScale,finish.relief,0));
            Surface=new CampSurfaceUseField(level.width,level.height,CampSurfaceUseSettings.Load(),art?.atmosphere?.wetness??CampWeatherTimeline.Initial(level.environment?.weatherId).Rain);
            int n=Surface.Settings.resolution;
            _useTexture=new Texture2D(n,n,TextureFormat.RGBA32,false,true){name="Observed camp surface use",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            _pixels=new Color32[n*n];BuildSmallStones();BuildFieldCover();BindMaterial(ground);PublishHeat();Publish();
        }
        void BuildSmallStones()
        {
            var vertices=new List<Vector3>();var colors=new List<Color>();var random=new System.Random(_level.decorSeed^789);
            float snow=_art?.atmosphere?.snowCover??SeasonProfile.For(_level).SnowCoverage;
            var directions=new[]{Vector3.left,Vector3.forward,Vector3.right,Vector3.back};
            var finish=_art?.groundSurface??new CampArtDefinition.GroundSurfaceSettings();
            for(int k=0;k<_level.width*_level.height*2;k++)
            {
                var at=new Vector3(((float)random.NextDouble()-.5f)*(_level.width+3),0,((float)random.NextDouble()-.5f)*(_level.height+3));at.y=Height(at)+.003f;if(at.y<-.08f||InWell(at.x,at.z))continue;
                // Irregular groups leave quiet open soil instead of uniformly scattered dots.
                if(Mathf.PerlinNoise(at.x*.65f+19,at.z*.65f+37)<.43f)continue;
                var cell=BoardMath.CellOf(_level,at);
                if(CampAccess.IsReserved(_level,cell)||Array.Exists(_level.noise,n=>n[0]==cell.X&&n[1]==cell.Z))continue;
                bool earth=k%4==0;
                float size=(.026f+(float)random.NextDouble()*.045f)*finish.stoneScale*(earth?1.6f:1);
                bool onField=Mathf.Abs(at.x)<_level.width*.5f&&Mathf.Abs(at.z)<_level.height*.5f;
                float elevation=earth?(onField ? .008f : .025f):size*.45f;
                var top=at+Vector3.up*elevation;
                var tint=earth?(_art==null?new Color(.40f,.44f,.25f):CampArtDefinition.Color(_art.soilColor)):
                    Color.Lerp(new Color(.42f,.44f,.34f),new Color(.61f,.58f,.45f),(float)random.NextDouble());
                float yaw=(float)random.NextDouble()*360;var rotation=Quaternion.Euler(0,yaw,0);
                for(int face=0;face<4;face++)
                {
                    var color=Color.Lerp(tint,new Color(.76f,.83f,.86f),snow*.85f);if(QualitySettings.activeColorSpace==ColorSpace.Linear)color=color.linear;
                    var a=at+rotation*directions[face]*size;var b=at+rotation*directions[(face+1)%4]*size;
                    a.y=Height(a)+.002f;b.y=Height(b)+.002f;
                    vertices.AddRange(new[]{a,top,b});colors.AddRange(new[]{color,color,color});
                }
            }
            var mesh=new Mesh{name="Small natural ground stones"};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(Enumerable.Range(0,vertices.Count).ToArray(),0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            _pebbleCover=new GameObject("Small ground stones",typeof(MeshFilter),typeof(MeshRenderer),typeof(OwnedCampMesh),typeof(OwnedCampMaterial));_pebbleCover.transform.SetParent(transform,false);_pebbleCover.layer=BoardRenderer.DecorLayer;
            _pebbleCover.GetComponent<MeshFilter>().sharedMesh=mesh;_pebbleCover.GetComponent<OwnedCampMesh>().Mesh=mesh;
            var material=new Material(Resources.Load<Shader>("QuietCamp/CampArtLit"));material.SetColor("_ArtAmbient",_art==null?new Color(.22f,.24f,.16f):CampArtDefinition.Color(_art.atmosphere?.ambientColor??_art.ambientColor)*.55f);_pebbleCover.GetComponent<Renderer>().sharedMaterial=material;_pebbleCover.GetComponent<OwnedCampMaterial>().Material=material;
        }
        void BuildFieldCover()
        {
            var season=_art?.atmosphere?.season??SeasonProfile.For(_level).Id;if(season=="winter"){BuildSnowCover();return;}
            var library=CozyVegetationLibrary.Load();var shapes=library?.plants.Where(p=>p.id.Contains("grass")&&p.mesh!=null&&p.mesh.isReadable&&p.colors?.Length>0)
                .Select(p=>new{plant=p,points=p.mesh.vertices,slots=Enumerable.Range(0,p.mesh.subMeshCount).SelectMany(slot=>p.mesh.GetTriangles(slot)).ToArray(),bounds=p.mesh.bounds}).ToArray();
            if(shapes==null||shapes.Length==0)return;
            var style=_art?.fieldCover??new CampArtDefinition.FieldCoverSettings();style.Validate();
            var broad=shapes.Where(s=>s.plant.id.IndexOf("leaf",StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            var fine=shapes.Where(s=>s.plant.id.IndexOf("leaf",StringComparison.OrdinalIgnoreCase)<0).ToArray();
            var water=style==null?null:new RiverSurfaceField(_art?.landscape);
            var vertices=new List<Vector3>();var colors=new List<Color>();var response=new List<Vector2>();
            var palette=SeasonPalette.For(season);float fringe=style.fringe;
            var area=VegetationBounds;
            var blocked=new HashSet<Cell>((_level.blocked??Array.Empty<int[]>()).Concat(_level.noise??Array.Empty<int[]>()).Where(p=>p?.Length==2).Select(p=>new Cell(p[0],p[1])));
            var flowers=library.plants.Where(p=>p.kind==CozyVegetationLibrary.PlantKind.Flower&&p.mesh!=null&&p.mesh.isReadable&&p.colors?.Length>0)
                .Select(p=>new{plant=p,points=p.mesh.vertices,bounds=p.mesh.bounds,groups=Enumerable.Range(0,p.mesh.subMeshCount)
                    .Select(slot=>new{indices=p.mesh.GetTriangles(slot),color=p.colors[Mathf.Min(slot,p.colors.Length-1)]}).ToArray()}).ToArray();
            _fieldCover=new GameObject("Natural map cover",typeof(OwnedCampMaterial));_fieldCover.transform.SetParent(transform,false);
            var material=new Material(Resources.Load<Shader>("QuietCamp/CampArtLit"));_fieldMaterial=material;material.SetFloat("_ArtGrass",1);material.SetFloat("_ArtMeadow",style.continuousMeadow?1:0);material.SetColor("_ArtAmbient",_art==null?new Color(.22f,.24f,.16f):CampArtDefinition.Color(_art.atmosphere?.ambientColor??_art.ambientColor)*.55f);
            _fieldCover.GetComponent<OwnedCampMaterial>().Material=material;BindMaterial(material);
            void AddMeadowBlade(Vector3 root,float angle,float height,float width,Color tint)
            {
                var forward=Quaternion.Euler(0,angle,0)*Vector3.forward;
                var side=Quaternion.Euler(0,angle,0)*Vector3.right*width*.5f;
                var tip=root+Vector3.up*height+forward*height*.8f;
                vertices.Add(root-side);vertices.Add(tip);vertices.Add(root+side);
                colors.Add(tint);colors.Add(tint);colors.Add(tint);
                response.Add(Vector2.up);response.Add(Vector2.one);response.Add(Vector2.up);
            }
            // World-anchored patches cover the entire authored terrain. Separate bounds
            // let Unity cull off-screen grass without a separate object for every blade.
            const float patchSize=8;
            for(int tx=Mathf.FloorToInt(area.xMin/patchSize);tx<Mathf.CeilToInt(area.xMax/patchSize);tx++)
            for(int tz=Mathf.FloorToInt(area.yMin/patchSize);tz<Mathf.CeilToInt(area.yMax/patchSize);tz++)
            {
            var tile=Rect.MinMaxRect(Mathf.Max(area.xMin,tx*patchSize),Mathf.Max(area.yMin,tz*patchSize),Mathf.Min(area.xMax,(tx+1)*patchSize),Mathf.Min(area.yMax,(tz+1)*patchSize));
            var random=new System.Random(unchecked(_level.decorSeed^4157^tx*73856093^tz*19349663));
            vertices.Clear();colors.Clear();response.Clear();int flowerCount=0;
            float nearest=Mathf.Max(0,Mathf.Max(Mathf.Max(tile.xMin,-tile.xMax)-_level.width*.5f,Mathf.Max(tile.yMin,-tile.yMax)-_level.height*.5f));
            float density=style.continuousMeadow?CoverDensity(nearest,style.density):style.density;
            int attempts=Mathf.CeilToInt(tile.width*tile.height*density);
            for(int k=0;k<attempts;k++)
            {
                float x=Mathf.Lerp(tile.xMin,tile.xMax,(float)random.NextDouble()),z=Mathf.Lerp(tile.yMin,tile.yMax,(float)random.NextDouble());
                var root=new Vector3(x,0,z);
                if(InWell(x,z))continue;
                float edge=Mathf.Max(Mathf.Abs(x)-_level.width*.5f,Mathf.Abs(z)-_level.height*.5f);
                float border=Mathf.Min(Mathf.Min(x-area.xMin,area.xMax-x),Mathf.Min(z-area.yMin,area.yMax-z));
                float fade=style.continuousMeadow?Mathf.SmoothStep(0,1,border/.6f)*CoverDensity(edge,style.density)/density
                    :1-Mathf.SmoothStep(0,1,Mathf.Max(0,edge)/fringe);
                float patch=Mathf.PerlinNoise(x*.46f+13,z*.46f+37);
                float coverage=style.continuousMeadow ? .68f+.28f*patch : .32f+.58f*patch;
                if(blocked.Contains(BoardMath.CellOf(_level,root))||random.NextDouble()>coverage*fade)continue;
                if(water!=null&&water.TrySample(x,z,out var wet)&&wet.Wet)continue;
                root.y=Height(root)+.008f;if(root.y<-.08f&&(_art?.landscape==null||!style.continuousMeadow))continue;
                float size=.09f+(float)random.NextDouble()*.14f;var rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
                var tint=Color.Lerp(palette.GrassDark,palette.GrassLight,(float)random.NextDouble()*.65f);
                if(season=="autumn")tint=Color.Lerp(tint,new Color(.57f,.42f,.19f),.55f);
                if(QualitySettings.activeColorSpace==ColorSpace.Linear)tint=tint.linear;
                if(style.continuousMeadow)
                {
                    bool onField=edge<=0;
                    float bladeCeiling=onField?Mathf.Min(style.maximumHeight,.075f+patch*.08f)
                        :Mathf.Min(.32f,style.maximumHeight*2)*(.8f+patch*.2f);
                    if(season=="autumn")bladeCeiling*=.55f;
                    var carpet=Color.Lerp(palette.GrassDark,palette.GrassLight,.42f+patch*.18f);
                    if(_art!=null)
                    {
                        var substrate=Color.Lerp(CampArtDefinition.Color(_art.groundLow),CampArtDefinition.Color(_art.groundHigh),.4f+patch*.3f);
                        carpet=Color.Lerp(substrate,carpet,.60f);
                    }
                    if(season=="autumn")carpet=Color.Lerp(carpet,new Color(.57f,.42f,.19f),.5f);
                    if(QualitySettings.activeColorSpace==ColorSpace.Linear)carpet=carpet.linear;
                    float yaw=(float)random.NextDouble()*360;
                    // Dense, very small blades overlap; no solid discs or repeated turf polygons.
                    for(int blade=0;blade<3;blade++)
                    {
                        float distanceBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(3,12,edge));
                        var offset=Quaternion.Euler(0,yaw+blade*119,0)*Vector3.forward*Mathf.Lerp(.045f,.13f,distanceBlend);
                        float bladeHeight=bladeCeiling*(.78f+(float)random.NextDouble()*.3f);
                        AddMeadowBlade(root+offset,yaw+blade*119,bladeHeight,Mathf.Lerp(.035f+patch*.020f,.065f+patch*.025f,distanceBlend),carpet);
                    }
                    float flowerPatch=Mathf.PerlinNoise(x*.25f+73,z*.25f+21);
                    if(season!="autumn"&&flowers.Length>0&&flowerCount<4&&vertices.Count<150000
                        &&flowerPatch>Mathf.Lerp(.82f,.42f,style.flowerCoverage)
                        &&random.NextDouble()<style.flowerCoverage*.3f)
                    {
                        var flower=flowers[(int)(Mathf.PerlinNoise(x*.12f+7,z*.12f+31)*flowers.Length)%flowers.Length];
                        float flowerHeight=Mathf.Min(onField ? .16f : .25f,Mathf.Max(bladeCeiling*1.3f,.12f));
                        float flowerScale=flowerHeight/Mathf.Max(.01f,flower.bounds.size.y);
                        foreach(var group in flower.groups)foreach(int index in group.indices)
                        {
                            var point=flower.points[index];point.y-=flower.bounds.min.y;
                            vertices.Add(root+rotation*point*flowerScale);
                            colors.Add(QualitySettings.activeColorSpace==ColorSpace.Linear?group.color.linear:group.color);
                            response.Add(new Vector2(Mathf.Clamp01(point.y/Mathf.Max(.01f,flower.bounds.size.y)),1));
                        }
                        flowerCount++;
                    }
                    continue;
                }
                var choices=shapes;
                if(style!=null&&broad.Length>0&&fine.Length>0)choices=random.NextDouble()<style.broadLeafShare?broad:fine;
                var shape=choices[random.Next(choices.Length)];float height=Mathf.Max(.01f,shape.bounds.size.y),scale=CozyVegetationLibrary.GroundcoverScale(shape.plant.kind,shape.bounds,size);
                if(style!=null)scale=Mathf.Min(scale,Mathf.Min(style.maximumHeight/height,style.maximumSpan/Mathf.Max(.001f,Mathf.Max(shape.bounds.size.x,shape.bounds.size.z))));
                foreach(int index in shape.slots)
                {
                    var p=shape.points[index];p.y-=shape.bounds.min.y;float responseHeight=p.y/height;if(season=="autumn")p.y*=.32f;
                    vertices.Add(root+rotation*p*scale);colors.Add(tint);response.Add(new Vector2(responseHeight,1));
                }
            }
            if(vertices.Count==0)continue;
            var mesh=new Mesh{name=$"Natural map cover {tx},{tz}",indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(1,response);mesh.SetTriangles(Enumerable.Range(0,vertices.Count).ToArray(),0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var patchObject=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer),typeof(OwnedCampMesh));patchObject.transform.SetParent(_fieldCover.transform,false);patchObject.layer=BoardRenderer.DecorLayer;
            patchObject.GetComponent<MeshFilter>().sharedMesh=mesh;patchObject.GetComponent<OwnedCampMesh>().Mesh=mesh;
            var carpetRenderer=patchObject.GetComponent<MeshRenderer>();carpetRenderer.sharedMaterial=material;
            carpetRenderer.shadowCastingMode=style.continuousMeadow?UnityEngine.Rendering.ShadowCastingMode.Off:UnityEngine.Rendering.ShadowCastingMode.On;
            carpetRenderer.receiveShadows=true;
            // Pressure and weather are shader-driven; no CPU geometry edits follow.
            mesh.UploadMeshData(true);
            VegetationPatchCount++;
            }
        }
        void BuildSnowCover()
        {
            float cover=_art?.atmosphere?.snowCover??SeasonProfile.For(_level).SnowCoverage;
            float depth=(_art?.groundSurface??new CampArtDefinition.GroundSurfaceSettings()).snowDepth*cover;
            if(depth<=.001f||cover<=0)return;
            var water=new RiverSurfaceField(_art?.landscape);var area=CoverBounds(new CampArtDefinition.FieldCoverSettings());VegetationBounds=area;
            _fieldCover=new GameObject("Settled map snow");_fieldCover.transform.SetParent(transform,false);
            // The legacy meadow owns its shader grid. Reuse that material so snow cannot hide it.
            var material=_art==null?_ground:new Material(Resources.Load<Shader>("QuietCamp/CampArtLit")){name="Soft settled snow"};
            if(_art!=null)
            {
                _fieldMaterial=material;material.SetFloat("_ArtGround",1);material.SetFloat("_ArtSnowSurface",1);
                material.SetColor("_GroundLow",CampArtDefinition.Color(_art.groundLow));material.SetColor("_GroundHigh",CampArtDefinition.Color(_art.groundHigh));material.SetColor("_SoilTone",CampArtDefinition.Color(_art.soilColor));
                material.SetColor("_ArtAmbient",CampArtDefinition.Color(_art.atmosphere?.ambientColor??_art.ambientColor)*.55f);
                _fieldCover.AddComponent<OwnedCampMaterial>().Material=material;
            }
            BindMaterial(material);
            var anchors=new Dictionary<Vector2Int,List<Vector2>>();
            var models=_art?.models?.ToDictionary(m=>m.id);
            foreach(var item in _art?.instances??Array.Empty<CampArtDefinition.Instance>())
            {
                if(item.modelId==null||models==null||!models.TryGetValue(item.modelId,out var model)||model.seasonalRole=="groundcover"||model.seasonalRole=="flower")continue;
                float low=float.MaxValue,high=float.MinValue;for(int v=1;v<model.vertices.Length;v+=3){low=Mathf.Min(low,model.vertices[v]);high=Mathf.Max(high,model.vertices[v]);}
                if((high-low)*(item.scaleXYZ?[1]??item.scale)<.8f||Mathf.Abs(item.y-Height(new Vector3(item.x,0,item.z)))>.6f)continue;
                var key=new Vector2Int(Mathf.FloorToInt(item.x/4),Mathf.FloorToInt(item.z/4));
                if(!anchors.TryGetValue(key,out var group))anchors[key]=group=new List<Vector2>();group.Add(new Vector2(item.x,item.z));
            }
            float heading=(_art?.atmosphere?.windHeading??25)*Mathf.Deg2Rad;var lee=new Vector2(Mathf.Sin(heading),Mathf.Cos(heading))*.65f;
            float SnowY(float x,float z)
            {
                float edge=Mathf.Max(Mathf.Abs(x)-_level.width*.5f,Mathf.Abs(z)-_level.height*.5f);
                float transition=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,2,edge));
                float bank=0;int ax=Mathf.FloorToInt(x/4),az=Mathf.FloorToInt(z/4);var point=new Vector2(x,z);
                for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)if(anchors.TryGetValue(new Vector2Int(ax+dx,az+dz),out var group))
                    foreach(var anchor in group)bank=Mathf.Max(bank,Mathf.Exp(-(point-anchor-lee).sqrMagnitude*.65f));
                var along=new Vector2(lee.y,-lee.x).normalized;
                float ripple=Mathf.PerlinNoise(Vector2.Dot(point,along)*.7f+43,Vector2.Dot(point,lee.normalized)*.22f+61);
                float drift=.22f+.9f*Mathf.PerlinNoise(x*.34f+17,z*.34f+31)+ripple*.18f+bank*.8f;
                float packed=Mathf.Min(.058f,depth*.32f)+Mathf.PerlinNoise(x*.62f+9,z*.62f+41)*.004f;
                float ground=Height(new Vector3(x,0,z));
                float field=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.15f,.25f,edge));
                // Never flatten raised terrain outside the puzzle: it would pierce the snow.
                float baseY=Mathf.Lerp(ground+packed,Mathf.Min(ground+packed,BoardMath.TentY-.004f),field);
                return Mathf.Lerp(baseY,ground+depth*drift,transition);
            }
            bool Dry(float x,float z)=>!water.TrySample(x,z,out var sample)||!sample.Wet;
            const float size=8;
            for(int tx=Mathf.FloorToInt(area.xMin/size);tx<Mathf.CeilToInt(area.xMax/size);tx++)
            for(int tz=Mathf.FloorToInt(area.yMin/size);tz<Mathf.CeilToInt(area.yMax/size);tz++)
            {
                float x0=Mathf.Max(area.xMin,tx*size),z0=Mathf.Max(area.yMin,tz*size),x1=Mathf.Min(area.xMax,(tx+1)*size),z1=Mathf.Min(area.yMax,(tz+1)*size);
                bool near=x0<_level.width*.5f+1&&x1>-_level.width*.5f-1&&z0<_level.height*.5f+1&&z1>-_level.height*.5f-1;
                float step=near?.25f:.5f;int nx=Mathf.CeilToInt((x1-x0)/step),nz=Mathf.CeilToInt((z1-z0)/step);
                var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();var indices=new List<int>();
                for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
                {
                    float px=Mathf.Lerp(x0,x1,x/(float)nx),pz=Mathf.Lerp(z0,z1,z/(float)nz);float y=SnowY(px,pz);vertices.Add(new Vector3(px,y,pz));
                    var normal=new Vector3(SnowY(px-.1f,pz)-SnowY(px+.1f,pz),.2f,SnowY(px,pz-.1f)-SnowY(px,pz+.1f)).normalized;normals.Add(normal);
                    // Broad drift shading survives small mobile pixels without grain or triangle colours.
                    float crest=Mathf.PerlinNoise(px*.34f+17,pz*.34f+31);
                    // An occlusion multiplier, not sRGB paint. Keep it continuous across the field.
                    colors.Add(Color.Lerp(new Color(.76f,.84f,.94f),Color.white,.22f+crest*.78f));
                }
                for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
                {
                    int a=z*(nx+1)+x,b=a+nx+1,c=b+1,d=a+1;
                    if(!Dry(vertices[a].x,vertices[a].z)||!Dry(vertices[b].x,vertices[b].z)||!Dry(vertices[c].x,vertices[c].z)||!Dry(vertices[d].x,vertices[d].z))continue;
                    indices.AddRange(new[]{a,b,c,a,c,d});
                }
                if(indices.Count==0)continue;
                if(_wellOpenings.Length>0)
                {
                    var snow=new LandscapeMesh{name="snow",vertices=vertices.SelectMany(p=>new[]{p.x,p.y,p.z}).ToArray(),normals=normals.SelectMany(p=>new[]{p.x,p.y,p.z}).ToArray(),
                        uv=new float[vertices.Count*2],colors=colors.SelectMany(p=>new[]{p.r,p.g,p.b,p.a}).ToArray(),triangles=indices.ToArray()};
                    var cut=WorldWellShafts.Cut(snow,_wellOpenings);
                    vertices=Enumerable.Range(0,cut.vertices.Length/3).Select(i=>new Vector3(cut.vertices[i*3],cut.vertices[i*3+1],cut.vertices[i*3+2])).ToList();
                    normals=Enumerable.Range(0,cut.normals.Length/3).Select(i=>new Vector3(cut.normals[i*3],cut.normals[i*3+1],cut.normals[i*3+2])).ToList();
                    colors=Enumerable.Range(0,cut.colors.Length/4).Select(i=>new Color(cut.colors[i*4],cut.colors[i*4+1],cut.colors[i*4+2],cut.colors[i*4+3])).ToList();indices=cut.triangles.ToList();
                }
                var mesh=new Mesh{name=$"Settled snow {tx},{tz}"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
                var node=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer),typeof(OwnedCampMesh));node.transform.SetParent(_fieldCover.transform,false);node.layer=BoardRenderer.DecorLayer;
                node.GetComponent<MeshFilter>().sharedMesh=mesh;node.GetComponent<OwnedCampMesh>().Mesh=mesh;node.GetComponent<MeshRenderer>().sharedMaterial=material;
                mesh.UploadMeshData(true);SnowPatchCount++;
            }
        }
        public void Bind(Transform decor,CampAtmosphere atmosphere=null,Func<bool> paused=null,Func<bool> reduced=null,SeasonAtmosphereView previewWeather=null)
        {
            _atmosphere=atmosphere;_previewWeather=previewWeather;_paused=paused;_reduced=reduced;
            foreach(var renderer in GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)BindMaterial(material);
            if(decor!=null)foreach(var renderer in decor.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)BindMaterial(material);
            _tufts.Clear();_tuftScales.Clear();
            foreach(var t in GetComponentsInChildren<Transform>(true))if(t.name=="Camp field grass"){_tufts.Add(t);_tuftScales.Add(t.localScale);}
            Publish();
        }
        void BindMaterial(Material material)
        {
            if(material==null||!material.HasProperty("_CampUse")||_bound.Contains(material))return;
            var cover=_art?.fieldCover??new CampArtDefinition.FieldCoverSettings();
            var area=CoverBounds(cover);
            if(material.HasProperty("_MeadowExtent"))material.SetVector("_MeadowExtent",cover.continuousMeadow
                ?new Vector4(Mathf.Max(Mathf.Abs(area.xMin),Mathf.Abs(area.xMax)),Mathf.Max(Mathf.Abs(area.yMin),Mathf.Abs(area.yMax)),.6f,1)
                :new Vector4(_level.width*.5f,_level.height*.5f,cover.fringe,0));
            material.SetTexture("_CampUse",_useTexture);material.SetVector("_CampUseBounds",new Vector4(Surface.Bounds.xMin,Surface.Bounds.yMin,1/Surface.Bounds.width,1/Surface.Bounds.height));_bound.Add(material);
        }
        void Publish()
        {
            if(Surface==null||_useTexture==null)return;
            Surface.CopyPixels(_pixels);_useTexture.SetPixels32(_pixels);_useTexture.Apply(false,false);
            var p=_art?.atmosphere;float snow=p?.snowCover??SeasonProfile.For(_level).SnowCoverage;
            foreach(var m in _bound)if(m!=null)m.SetVector("_CampClimate",new Vector4(Surface.Moisture,snow,p?.snow??_atmosphere?.Weather?.Snowfall??0,1));
            PublishFieldWeather();
            for(int i=0;i<_tufts.Count;i++)if(_tufts[i]!=null){var at=_tufts[i].position;float pressure=Surface.Sample(new Vector2(at.x,at.z)).g;var scale=_tuftScales[i];_tufts[i].localScale=new Vector3(scale.x,scale.y*(1-pressure*.87f),scale.z);}
        }
        void PublishFieldWeather()
        {
            var p=_art?.atmosphere;
            if(_fieldMaterial!=null)
            {
                var weather=_atmosphere?.SeasonalWeather??_previewWeather;float clock=weather?.WeatherClock??_printClock;
                var direction=weather?.WindDirection??new Vector2(Mathf.Sin((p?.windHeading??25)*Mathf.Deg2Rad),Mathf.Cos((p?.windHeading??25)*Mathf.Deg2Rad));
                PlantWeatherResponse.Set(_fieldMaterial,clock,_reduced?.Invoke()==true?0:weather?.WindStrength??p?.wind??_atmosphere?.Wind.Strength??0,p?.rain??_atmosphere?.Weather?.RainAmount??0);
                _fieldMaterial.SetVector("_QCWind",new Vector4(direction.x,direction.y,0,0));
            }
        }
        static float CoverDensity(float edge,float density)=>Mathf.Lerp(density*6,Mathf.Max(32,density*1.7f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(2,12,edge)));
        Rect CoverBounds(CampArtDefinition.FieldCoverSettings cover)
        {
            if(!cover.continuousMeadow)return new Rect(-_level.width*.5f-cover.fringe,-_level.height*.5f-cover.fringe,_level.width+cover.fringe*2,_level.height+cover.fringe*2);
            var landscape=_art?.landscape;
            if(landscape!=null)return new Rect(landscape.originX,landscape.originZ,landscape.size,landscape.size);
            float half=_art!=null?Mathf.Max(19.2f,_art.surroundExtent):Mathf.Max(_level.width,_level.height)*.5f+24;
            return new Rect(-half,-half,half*2,half*2);
        }
        float Height(Vector3 p)=>_art?.GroundHeight(p.x,p.z)??MeadowSurface.GroundY;
        public void Rebuild(IReadOnlyList<Placement> placements)
        {
            Surface.ClearOccupancy();
            foreach(var p in placements){var at=BoardMath.TentCenter(_level,p.x,p.z);Surface.Tent(new Rect(at.x-1,at.z-1,2,2),Stable(p.guestId));}
            Publish();
        }
        static int Stable(string id){uint h=2166136261;foreach(char c in id??"")h=unchecked((h^c)*16777619);return (int)(h&65535);}
        public void Advance(float seconds,float rain,float snowfall,bool reduced=false)
        {
            if(Surface==null||seconds<=0)return;
            Surface.Advance(seconds,rain,snowfall);AdvanceHeat(seconds,_atmosphere?.RainShelter?.FireStrength??1,snowfall);
            _refresh+=seconds;_printClock+=seconds;PublishFieldWeather();
            if(_refresh>=.25f){_refresh=0;if(Surface.Dirty)Publish();}
        }
        void LateUpdate()
        {
            if(!UnityEngine.Application.isPlaying||_paused?.Invoke()==true)return;
            var p=_atmosphere?.SeasonalWeather?.Preset??_art?.atmosphere;
            Advance(Mathf.Min(Time.deltaTime,.1f),p?.wetness??_atmosphere?.Weather?.RainAmount??0,p?.snow??_atmosphere?.Weather?.Snowfall??0,_reduced?.Invoke()??false);
        }
        public static float HeatAt(LevelData level, Vector3 point)
        {
            float heat = 0;
            foreach (var fire in level.noise ?? Array.Empty<int[]>())
            {
                if (fire?.Length != 2) continue;
                var centre = BoardMath.CellCenterWorld(level, new Cell(fire[0],fire[1]));
                float distance = new Vector2(point.x-centre.x,point.z-centre.z).magnitude;
                heat = Mathf.Max(heat, 1-Mathf.SmoothStep(0,1,Mathf.Clamp01((distance-.35f)/1.15f)));
            }
            return heat;
        }

        public void AdvanceHeat(float seconds, float fireStrength, float snowfall)
        {
            // Wet ash stays exposed after quenching; falling snow slowly covers a cold pit again.
            float target = fireStrength > .15f ? 1 : 0;
            _melt = Mathf.MoveTowards(_melt, target, Mathf.Max(0,seconds)
                * (target > _melt ? 1/12f : Mathf.Clamp01(snowfall)/180f));
            PublishHeat();
        }
        void PublishHeat()
        {
            if (_ground == null) return;
            _ground.SetFloat("_HeatMelt", _melt);
            for (int i=0;i<4;i++)
            {
                Vector4 site = Vector4.zero;
                if (i<(_level.noise?.Length??0) && _level.noise[i]?.Length == 2)
                {
                    var point = transform.TransformPoint(BoardMath.CellCenterWorld(_level,new Cell(_level.noise[i][0],_level.noise[i][1])));
                    site = new Vector4(point.x,point.z,1.50f,1);
                }
                _ground.SetVector("_HeatSite"+i,site);
            }
        }

        static void Release(UnityEngine.Object o){if(o==null)return;if(UnityEngine.Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
        void OnDestroy()
        {
            foreach(var m in _bound)if(m!=null){m.SetTexture("_CampUse",Texture2D.blackTexture);m.SetVector("_CampUseBounds",Vector4.zero);}
            Release(_pebbleCover);Release(_fieldCover);Release(_useTexture);
        }
    }
}
