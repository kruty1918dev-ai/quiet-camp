using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Owns camp surface pressure and observed foot traffic, using the canonical evaluator's routes.
    /// Scene-local presentation; there are no fictitious steps when tents or routes change.</summary>
    public sealed class CampGroundDetails : MonoBehaviour
    {
        public const int MaximumPrints=256;
        LevelData _level; CampArtDefinition _art; Material _ground,_actorMaterial,_fieldMaterial;
        CampAtmosphere _atmosphere; SeasonAtmosphereView _previewWeather; Func<bool> _paused,_reduced;
        Texture2D _useTexture; Color32[] _pixels; float _refresh,_melt=1,_printClock;
        readonly List<Material> _bound=new List<Material>();
        readonly List<Vector3> _centres=new List<Vector3>();
        readonly List<Resident> _residents=new List<Resident>();
        readonly List<Transform> _tufts=new List<Transform>();
        readonly List<Vector3> _tuftScales=new List<Vector3>();
        GameObject _fieldCover,_pebbleCover;
        Mesh _printMesh; Material _printMaterial; GameObject _printRoot;
        struct Print {public Vector3 at,forward;public float age;}
        readonly List<Print> _prints=new List<Print>();
        sealed class Resident
        {
            public string id;public GameObject root;public Transform left,right;public Vector3[] route;
            public int node,direction=1,parity;public float wait,distance,phase;
        }
        public CampSurfaceUseField Surface {get;private set;}
        public int PrintCount=>_centres.Count;
        public IReadOnlyList<Vector3> PrintCentres=>_centres;
        public float MeltAmount=>_melt;
        public int Residents=>_residents.Count;
        public void Configure(LevelData level,Material ground,CampArtDefinition art=null)
        {
            _level=level;_ground=ground;_art=art;
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
            for(int k=0;k<_level.width*_level.height/2;k++)
            {
                var at=new Vector3(((float)random.NextDouble()-.5f)*(_level.width+3),0,((float)random.NextDouble()-.5f)*(_level.height+3));at.y=Height(at)+.003f;if(at.y<-.08f)continue;
                float size=.026f+(float)random.NextDouble()*.045f;var top=at+Vector3.up*size*.65f;var tint=Color.Lerp(new Color(.42f,.44f,.34f),new Color(.61f,.58f,.45f),(float)random.NextDouble());
                for(int face=0;face<4;face++)
                {
                    var color=Color.Lerp(tint,new Color(.76f,.83f,.86f),snow*.85f);if(QualitySettings.activeColorSpace==ColorSpace.Linear)color=color.linear;
                    vertices.AddRange(new[]{at+directions[face]*size,top,at+directions[(face+1)%4]*size});colors.AddRange(new[]{color,color,color});
                }
            }
            var mesh=new Mesh{name="Small natural ground stones"};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(Enumerable.Range(0,vertices.Count).ToArray(),0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            _pebbleCover=new GameObject("Small ground stones",typeof(MeshFilter),typeof(MeshRenderer),typeof(OwnedCampMesh),typeof(OwnedCampMaterial));_pebbleCover.transform.SetParent(transform,false);_pebbleCover.layer=BoardRenderer.DecorLayer;
            _pebbleCover.GetComponent<MeshFilter>().sharedMesh=mesh;_pebbleCover.GetComponent<OwnedCampMesh>().Mesh=mesh;
            var material=new Material(Resources.Load<Shader>("QuietCamp/CampArtLit"));material.SetColor("_ArtAmbient",_art==null?new Color(.22f,.24f,.16f):CampArtDefinition.Color(_art.atmosphere?.ambientColor??_art.ambientColor)*.55f);_pebbleCover.GetComponent<Renderer>().sharedMaterial=material;_pebbleCover.GetComponent<OwnedCampMaterial>().Material=material;
        }
        void BuildFieldCover()
        {
            var season=_art?.atmosphere?.season??SeasonProfile.For(_level).Id;if(season=="winter")return;
            var library=CozyVegetationLibrary.Load();var shapes=library?.plants.Where(p=>p.id.Contains("grass")&&p.mesh!=null&&p.mesh.isReadable&&p.colors?.Length>0)
                .Select(p=>new{plant=p,points=p.mesh.vertices,slots=Enumerable.Range(0,p.mesh.subMeshCount).SelectMany(slot=>p.mesh.GetTriangles(slot)).ToArray(),bounds=p.mesh.bounds}).ToArray();
            if(shapes==null||shapes.Length==0)return;
            var vertices=new List<Vector3>();var colors=new List<Color>();var response=new List<Vector2>();var random=new System.Random(_level.decorSeed^4157);
            var palette=SeasonPalette.For(season);float halfX=_level.width*.5f+1.1f,halfZ=_level.height*.5f+1.1f;
            var blocked=new HashSet<Cell>((_level.blocked??Array.Empty<int[]>()).Concat(_level.noise??Array.Empty<int[]>()).Where(p=>p?.Length==2).Select(p=>new Cell(p[0],p[1])));
            int attempts=Mathf.CeilToInt(halfX*halfZ*4*12);
            for(int k=0;k<attempts;k++)
            {
                float x=Mathf.Lerp(-halfX,halfX,(float)random.NextDouble()),z=Mathf.Lerp(-halfZ,halfZ,(float)random.NextDouble());
                var root=new Vector3(x,0,z);
                if(blocked.Contains(BoardMath.CellOf(_level,root))||random.NextDouble()>.32f+.58f*Mathf.PerlinNoise(x*.8f+13,z*.8f))continue;
                root.y=Height(root)+.008f;if(root.y<-.08f)continue;
                float size=.09f+(float)random.NextDouble()*.14f;var rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
                var tint=Color.Lerp(palette.GrassDark,palette.GrassLight,(float)random.NextDouble()*.65f);
                if(season=="autumn")tint=Color.Lerp(tint,new Color(.57f,.42f,.19f),.55f);
                if(QualitySettings.activeColorSpace==ColorSpace.Linear)tint=tint.linear;
                var shape=shapes[random.Next(shapes.Length)];float height=Mathf.Max(.01f,shape.bounds.size.y),scale=CozyVegetationLibrary.GroundcoverScale(shape.plant.kind,shape.bounds,size);
                foreach(int index in shape.slots)
                {
                    var p=shape.points[index];p.y-=shape.bounds.min.y;float responseHeight=p.y/height;if(season=="autumn")p.y*=.32f;
                    vertices.Add(root+rotation*p*scale);colors.Add(tint);response.Add(new Vector2(responseHeight,1));
                }
            }
            var mesh=new Mesh{name="Unoccupied natural field cover",indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(1,response);mesh.SetTriangles(Enumerable.Range(0,vertices.Count).ToArray(),0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            _fieldCover=new GameObject("Natural field cover",typeof(MeshFilter),typeof(MeshRenderer),typeof(OwnedCampMesh),typeof(OwnedCampMaterial));_fieldCover.transform.SetParent(transform,false);_fieldCover.layer=BoardRenderer.DecorLayer;
            _fieldCover.GetComponent<MeshFilter>().sharedMesh=mesh;_fieldCover.GetComponent<OwnedCampMesh>().Mesh=mesh;
            var material=new Material(Resources.Load<Shader>("QuietCamp/CampArtLit"));_fieldMaterial=material;material.SetFloat("_ArtGrass",1);material.SetColor("_ArtAmbient",_art==null?new Color(.22f,.24f,.16f):CampArtDefinition.Color(_art.atmosphere?.ambientColor??_art.ambientColor)*.55f);
            _fieldCover.GetComponent<Renderer>().sharedMaterial=material;_fieldCover.GetComponent<OwnedCampMaterial>().Material=material;BindMaterial(material);
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
        float Height(Vector3 p)=>_art?.GroundHeight(p.x,p.z)??MeadowSurface.GroundY;
        public void Rebuild(IReadOnlyList<Placement> placements)
        {
            Surface.ClearOccupancy();
            foreach(var p in placements){var at=BoardMath.TentCenter(_level,p.x,p.z);Surface.Tent(new Rect(at.x-1,at.z-1,2,2),Stable(p.guestId));}
            var routes=RuleEvaluator.Evaluate(_level,placements,false).Routes.Where(r=>r.Reachable&&!string.IsNullOrEmpty(r.GuestId)&&r.Cells.Length>1).ToArray();
            foreach(var resident in _residents.ToArray())if(!routes.Any(r=>r.GuestId==resident.id)){Release(resident.root);_residents.Remove(resident);}
            foreach(var route in routes)
            {
                var points=WalkingCurve(route.Cells);
                var resident=_residents.FirstOrDefault(r=>r.id==route.GuestId);
                if(resident!=null&&resident.route.SequenceEqual(points))continue;
                if(resident!=null){Release(resident.root);_residents.Remove(resident);}
                resident=CreateResident(route.GuestId,points);_residents.Add(resident);
            }
            Publish();
        }
        Vector3[] WalkingCurve(Cell[] cells)
        {
            var result=new List<Vector3>();
            for(int i=0;i<cells.Length;i++)
            {
                var at=BoardMath.CellCenterWorld(_level,cells[i]);
                if(i>0&&i<cells.Length-1)
                {
                    var incoming=(at-BoardMath.CellCenterWorld(_level,cells[i-1])).normalized;
                    var outgoing=(BoardMath.CellCenterWorld(_level,cells[i+1])-at).normalized;
                    if(Vector3.Dot(incoming,outgoing)<.5f)
                    {
                        // The bevel lies entirely inside the current free cell, so it cannot cut across an obstacle.
                        result.Add(at-incoming*.20f);result.Add(at+(outgoing-incoming)*.07f);result.Add(at+outgoing*.20f);continue;
                    }
                }
                result.Add(at);
            }
            return result.ToArray();
        }
        static int Stable(string id){uint h=2166136261;foreach(char c in id??"")h=unchecked((h^c)*16777619);return (int)(h&65535);}
        Resident CreateResident(string id,Vector3[] points)
        {
            if(_actorMaterial==null){_actorMaterial=new Material(Resources.Load<Shader>("QuietCamp/FoliageLit"));_actorMaterial.SetColor("_BaseColor",Color.white);_actorMaterial.SetFloat("_VertexTint",0);_actorMaterial.SetFloat("_SwayAmp",0);_actorMaterial.SetFloat("_FlutterAmp",0);}
            var root=new GameObject("Camp resident / "+id);root.transform.SetParent(transform,false);root.layer=BoardRenderer.DecorLayer;
            var rng=new System.Random(Stable(id));var coat=Color.Lerp(new Color(.35f,.40f,.30f),new Color(.55f,.42f,.25f),(float)rng.NextDouble());
            Transform Part(string name,Vector3 at,Vector3 size,Color color,PrimitiveType shape=PrimitiveType.Cube)
            {
                var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(root.transform,false);go.transform.localPosition=at;go.transform.localScale=size;go.layer=root.layer;
                var collider=go.GetComponent<Collider>();if(collider!=null)Release(collider);
                var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=_actorMaterial;var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);renderer.SetPropertyBlock(block);return go.transform;
            }
            Part("Jacket",new Vector3(0,.98f,0),new Vector3(.32f,.48f,.22f),coat);
            Part("Head",new Vector3(0,1.38f,0),Vector3.one*.23f,new Color(.69f,.55f,.41f));
            Part("Cap",new Vector3(0,1.53f,0),new Vector3(.28f,.09f,.28f),coat*.75f);
            Part("Left arm",new Vector3(-.21f,.95f,0),new Vector3(.10f,.42f,.11f),coat);
            Part("Right arm",new Vector3(.21f,.95f,0),new Vector3(.10f,.42f,.11f),coat);
            var left=Part("Left leg",new Vector3(-.095f,.34f,0),new Vector3(.13f,.68f,.15f),new Color(.29f,.29f,.25f));
            var right=Part("Right leg",new Vector3(.095f,.34f,0),new Vector3(.13f,.68f,.15f),new Color(.29f,.29f,.25f));
            var start=points[0];start.y=Height(start)+.025f;root.transform.position=start;
            root.SetActive(false);
            return new Resident{id=id,root=root,left=left,right=right,route=points,node=1,wait=1+Stable(id)%7};
        }
        public void Advance(float seconds,float rain,float snowfall,bool reduced=false)
        {
            if(Surface==null||seconds<=0)return;
            Surface.Advance(seconds,rain,snowfall);AdvanceHeat(seconds,_atmosphere?.RainShelter?.FireStrength??1,snowfall);
            foreach(var r in _residents)
            {
                if(reduced){r.left.localRotation=r.right.localRotation=Quaternion.identity;var a=r.left.localPosition;var b=r.right.localPosition;a.y=b.y=.34f;r.left.localPosition=a;r.right.localPosition=b;continue;}
                float remaining=seconds;
                // Consume elapsed movement without skipping contact samples on long frames.
                while(remaining>0)
                {
                    float dt=Mathf.Min(.05f,remaining);remaining-=dt;
                    if(r.wait>0){r.wait=Mathf.Max(0,r.wait-dt);continue;}
                    r.root.SetActive(true);
                    var target=r.route[r.node];target.y=Height(target)+.025f;
                    var previous=r.root.transform.position;var next=Vector3.MoveTowards(previous,target,dt*Surface.Settings.walkingSpeed);
                    var direction=next-previous;direction.y=0;
                    if(direction.sqrMagnitude>1e-8f)r.root.transform.rotation=Quaternion.LookRotation(direction);
                    r.root.transform.position=next;r.distance+=Vector3.Distance(previous,next);r.phase+=dt*7;
                    float angle=Mathf.Sin(r.phase)*18;
                    r.left.localRotation=Quaternion.Euler(angle,0,0);r.right.localRotation=Quaternion.Euler(-angle,0,0);
                    var left=r.left.localPosition;left.y=.34f*Mathf.Cos(angle*Mathf.Deg2Rad);r.left.localPosition=left;
                    var right=r.right.localPosition;right.y=left.y;r.right.localPosition=right;
                    if(r.distance>=Surface.Settings.stride)
                    {
                        r.distance-=Surface.Settings.stride;var forward=direction.sqrMagnitude>0?direction.normalized:r.root.transform.forward;
                        var leg=r.parity++%2==0?r.left:r.right;
                        var foot=leg.TransformPoint(new Vector3(0,-.5f,0));
                        Surface.Step(new Vector2(foot.x,foot.z));
                        if((_art?.atmosphere?.snowCover??SeasonProfile.For(_level).SnowCoverage)>.1f||Surface.Moisture>.2f&&Surface.Sample(new Vector2(foot.x,foot.z)).r<.55f)AddPrint(foot,forward);
                    }
                    if(Vector3.Distance(next,target)<.002f){if(r.node==r.route.Length-1||r.node==0){r.direction=-r.direction;r.wait=3+Stable(r.id)%4;if(r.node==0)r.root.SetActive(false);}r.node+=r.direction;}
                }
            }
            for(int i=_prints.Count-1;i>=0;i--){var p=_prints[i];p.age+=seconds*(.05f+snowfall);if(p.age>Surface.Settings.snowRecoverySeconds)_prints.RemoveAt(i);else _prints[i]=p;}
            _refresh+=seconds;_printClock+=seconds;PublishFieldWeather();
            if(_refresh>=.25f){_refresh=0;if(Surface.Dirty)Publish();RebuildPrintMesh();}
        }
        void LateUpdate()
        {
            if(!UnityEngine.Application.isPlaying||_paused?.Invoke()==true)return;
            var p=_atmosphere?.SeasonalWeather?.Preset??_art?.atmosphere;
            Advance(Mathf.Min(Time.deltaTime,.1f),p?.wetness??_atmosphere?.Weather?.RainAmount??0,p?.snow??_atmosphere?.Weather?.Snowfall??0,_reduced?.Invoke()??false);
        }
        void AddPrint(Vector3 at,Vector3 forward)
        {
            if(_prints.Count==MaximumPrints)_prints.RemoveAt(0);at.y=Height(at)+.014f;_prints.Add(new Print{at=at,forward=forward});
        }
        void RebuildPrintMesh()
        {
            _centres.Clear();if(_prints.Count==0&&_printMesh==null)return;
            if(_printRoot==null)
            {
                _printRoot=new GameObject("Observed snow footsteps",typeof(MeshFilter),typeof(MeshRenderer));_printRoot.transform.SetParent(transform,false);_printRoot.layer=gameObject.layer;
                _printMesh=new Mesh{name="Bounded recessed snow soles"};_printRoot.GetComponent<MeshFilter>().sharedMesh=_printMesh;
                _printMaterial=new Material(Resources.Load<Shader>("QuietCamp/FoliageLit"));_printMaterial.SetColor("_BaseColor",Color.white);_printMaterial.SetFloat("_VertexTint",1);_printMaterial.SetFloat("_SwayAmp",0);_printMaterial.SetFloat("_FlutterAmp",0);
                var renderer=_printRoot.GetComponent<MeshRenderer>();renderer.sharedMaterial=_printMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            var vertices=new List<Vector3>();var colors=new List<Color>();var indices=new List<int>();
            foreach(var p in _prints)
            {
                var side=Vector3.Cross(Vector3.up,p.forward);float fade=1-Mathf.Clamp01(p.age/Surface.Settings.snowRecoverySeconds);
                bool snow=(_art?.atmosphere?.snowCover??SeasonProfile.For(_level).SnowCoverage)>.1f;
                var clean=snow?new Color(.84f,.89f,.90f):Color.Lerp(SeasonPalette.For(_level).GrassLight,SeasonPalette.For(_level).Soil,.55f);
                var compressed=snow?new Color(.52f,.63f,.67f):clean*.89f;
                var tint=Color.Lerp(clean,compressed,fade*(snow?.65f:.35f));_centres.Add(p.at);
                int start=vertices.Count;vertices.Add(p.at);colors.Add(tint);
                for(int k=0;k<8;k++){float a=k*Mathf.PI*.25f;vertices.Add(p.at+side*Mathf.Cos(a)*.047f+p.forward*Mathf.Sin(a)*.105f+Vector3.up*.004f);colors.Add(Color.Lerp(tint,clean,.65f));}
                for(int k=0;k<8;k++)indices.AddRange(new[]{start,start+1+(k+1)%8,start+1+k});
            }
            _printMesh.Clear();_printMesh.SetVertices(vertices);_printMesh.SetColors(colors);_printMesh.SetTriangles(indices,0);_printMesh.RecalculateNormals();_printMesh.RecalculateBounds();
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
            foreach(var r in _residents)Release(r.root);Release(_pebbleCover);Release(_fieldCover);Release(_actorMaterial);Release(_useTexture);Release(_printRoot);Release(_printMesh);Release(_printMaterial);
        }
    }
}
