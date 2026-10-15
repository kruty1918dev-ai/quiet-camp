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
        LevelData _level; CampArtDefinition _art; Material _ground,_fieldMaterial;
        CampAtmosphere _atmosphere; SeasonAtmosphereView _previewWeather; Func<bool> _paused,_reduced;
        Texture2D _useTexture; Color32[] _pixels; float _refresh,_melt=1,_printClock;
        readonly List<Material> _bound=new List<Material>();
        readonly List<Transform> _tufts=new List<Transform>();
        readonly List<Vector3> _tuftScales=new List<Vector3>();
        GameObject _fieldCover,_pebbleCover;
        public CampSurfaceUseField Surface {get;private set;}
        public int PrintCount=>0;
        public IReadOnlyList<Vector3> PrintCentres=>Array.Empty<Vector3>();
        public float MeltAmount=>_melt;
        public int Residents=>0;
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
