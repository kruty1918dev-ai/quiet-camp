using System.Collections.Generic;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    public sealed class TentCloth : MonoBehaviour
    {
        readonly List<Material> _owned = new List<Material>();
        readonly List<FabricGeometry> _geometry = new List<FabricGeometry>();
        readonly List<Material> _additionalFabric=new List<Material>();
        readonly List<Material> _warmFabric=new List<Material>();
        Material _interiorBelongings;
        readonly Dictionary<Material,(MeshRenderer renderer,Vector4 centre)> _storage=new Dictionary<Material,(MeshRenderer,Vector4)>();
        string _season="summer";float _treeDensity,_storageTarget,_storageAmount;
        Vector4 _appliedSurface;bool _surfaceApplied,_warmthApplied;
        public float Wetness {get;private set;}
        public float SnowCover {get;private set;}
        public float LeafCover {get;private set;}
        public float StoragePressure=>_storageAmount;
        public int DeformationVersion {get;private set;}
        Material _interior;Texture2D _atlas;Mesh _glowMesh;
        bool _initialized;
        public static int CachedGeometryCount => FabricGeometry.CachedCount;
        public float ShelterWarmth { get; private set; }
        public static void Apply(GameObject root)
        {
            var owner = root.GetComponent<TentCloth>() ?? root.AddComponent<TentCloth>();
            if (root.activeInHierarchy) owner.Initialize();
        }
        void OnEnable() => Initialize();
        void Initialize()
        {
            if (_initialized) return;
            // Unity does not call OnDestroy on behaviours that were never
            // activated. Defer owned GPU resources until the first OnEnable.
            var shader=Resources.Load<Shader>("QuietCamp/TentCloth");
            if (shader==null) return;
            _initialized = true;_warmthApplied=false;
            var root = gameObject; var owner = this;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if(mesh==null) continue;
                var mats=renderer.sharedMaterials;bool changed=false;ulong fabricSlots=0;
                for(int i=0;i<mats.Length;i++)
                {
                    var source=mats[i];if(source==null)continue;
                    // Only fabric, with rigid wood, pegs and the door marker left alone.
                    var name=source.name.ToLowerInvariant();if(!name.Contains("red")&&!name.Contains("yellow")&&!name.Contains("fabric"))continue;
                    var mat=new Material(shader) { name=source.name+" (tent fabric)" }; mat.SetColor("_BaseColor",source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.color);
                    mat.SetVector("_MeshMin",mesh.bounds.min);mat.SetVector("_MeshSize",mesh.bounds.size);
                    var door = root.transform.Find("DoorMarker");
                    var anchor = door != null ? renderer.transform.InverseTransformPoint(door.position) : mesh.bounds.min;
                    mat.SetVector("_DoorAnchor",new Vector4(anchor.x,anchor.y,anchor.z,mesh.bounds.size.y*.65f));
                    mats[i]=mat;owner._owned.Add(mat);changed=true;
                    if(i<64)fabricSlots|=1UL<<i;
                }
                if(changed)
                {
                    renderer.sharedMaterials=mats;
                    var geometry=FabricGeometry.Acquire(mesh,fabricSlots);
                    if(geometry!=null)
                    {
                        owner._geometry.Add(geometry);renderer.GetComponent<MeshFilter>().sharedMesh=geometry.Mesh;
                        // Poles protrude above the fabric. Their bounds must never
                        // define the roof's pin mask or pull its ridge off the frame.
                        for(int i=0;i<mats.Length;i++) if((fabricSlots & (1UL<<i))!=0)
                        {
                            mats[i].SetVector("_MeshMin",geometry.FabricBounds.min);
                            mats[i].SetVector("_MeshSize",geometry.FabricBounds.size);
                            var b=geometry.FabricBounds;
                            mats[i].SetVector("_DoorAnchor",new Vector4(b.center.x,b.min.y+b.size.y*.15f,b.max.z,b.size.y*.7f));
                        }
                        var bounds=geometry.Mesh.bounds;bounds.Expand(new Vector3(mesh.bounds.size.y*.5f,.04f,mesh.bounds.size.y*.5f));renderer.localBounds=bounds;
                        renderer.GetComponent<RainSurface>()?.RefreshGeometry();
                    }
                }
            }
            owner.BuildInterior();
            owner.ApplySurfaceState(true);
        }
        void BuildInterior()
        {
            if(_owned.Count==0)return;
            var door=transform.Find("DoorMarker");if(door==null)return;
            var room=TentLivingSpace.Measure(transform);var bounds=room.Bounds;
            // Presentation does not depend on primitive-generated colliders
            // being preserved by IL2CPP stripping.
            var glow=new GameObject("TentInteriorGlow");
            glow.layer=gameObject.layer;
            glow.transform.SetParent(transform,false);glow.transform.localPosition=new Vector3(bounds.center.x,room.Floor+.034f,bounds.center.z);
            glow.transform.localRotation=Quaternion.Euler(90,0,0);glow.transform.localScale=new Vector3(room.Width*.58f,room.Depth*.55f,1);
            _glowMesh=new Mesh{name="Sheltered floor light"};
            _glowMesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
            _glowMesh.triangles=new[]{0,2,1,0,3,2};_glowMesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};_glowMesh.RecalculateNormals();
            var colors=new Color[_glowMesh.vertexCount];for(int i=0;i<colors.Length;i++)colors[i]=Color.white;
            _glowMesh.colors=colors;glow.AddComponent<MeshFilter>().sharedMesh=_glowMesh;
            _atlas=CozyParticleAtlas.Create();_interior=CozyParticleMaterial.Create(_atlas,0,.6f);
            _interior.mainTextureScale=new Vector2(.5f,.5f);_interior.mainTextureOffset=new Vector2(.5f,.5f);
            var renderer=glow.AddComponent<MeshRenderer>();renderer.sharedMaterial=_interior;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            SetShelterWarmth(ShelterWarmth);
        }
        public void SetShelterWarmth(float value)
        {
            value=Mathf.Clamp01(value);
            if(_warmthApplied&&ShelterWarmth==value)return;
            ShelterWarmth=value;_warmthApplied=true;
            foreach(var mat in _owned)mat.SetFloat("_ShelterGlow",ShelterWarmth);
            foreach(var mat in _warmFabric)if(mat!=null)mat.SetFloat("_ShelterGlow",ShelterWarmth);
            if(_interiorBelongings!=null)_interiorBelongings.SetFloat("_ShelterGlow",ShelterWarmth);
            if(_interior!=null)_interior.SetColor("_Tint",new Color(1,.65f,.24f,ShelterWarmth*.38f));
        }
        public void ConfigureSeason(QuietCamp.Domain.LevelData level)
        {
            var environment=QuietCamp.Domain.EnvironmentCompositionData.For(level);
            _season=environment.seasonId;_treeDensity=environment.treeDensity;
            ApplySurfaceState();
        }
        public void RegisterInterior(Material material){_interiorBelongings=material;_warmthApplied=false;SetShelterWarmth(ShelterWarmth);}
        public void RegisterFabric(Material material,bool receivesWarmth=false)
        {
            if(material!=null&&!_additionalFabric.Contains(material))_additionalFabric.Add(material);
            if(material!=null&&receivesWarmth&&!_warmFabric.Contains(material))_warmFabric.Add(material);
            if(receivesWarmth){_warmthApplied=false;SetShelterWarmth(ShelterWarmth);}
            ApplySurfaceState(true);
        }
        public void SetStorage(float amount,Bounds room,bool leftCorner=false)
        {
            _storageTarget=Mathf.Clamp01(amount);
            var at=new Vector3(room.center.x+room.size.x*(leftCorner?-.37f:.37f),room.min.y+room.size.y*.24f,
                room.center.z+room.size.z*(leftCorner?-.10f:.29f));
            var visualSpace=transform.Find("LiftNode")??transform;
            foreach(var renderer in GetComponentsInChildren<MeshRenderer>())
            foreach(var material in renderer.sharedMaterials)
            {
                if(!_owned.Contains(material))continue;
                // Packing belongs to the fabric, not the tweened lift/scale.
                var local=renderer.transform.InverseTransformPoint(visualSpace.TransformPoint(at));
                float radius=renderer.transform.InverseTransformVector(visualSpace.TransformVector(Vector3.right*room.size.x*.28f)).magnitude;
                var centre=new Vector4(local.x,local.y,local.z,radius);
                _storage[material]=(renderer,centre);material.SetVector("_StorageBulge",centre);
            }
            DeformationVersion++;
            ApplySurfaceState(true);
        }
        public void AdvanceSurface(float seconds,float rain,float snowfall,float wind)
        {
            float dt=Mathf.Clamp(seconds,0,120);
            rain=Mathf.Clamp01(rain);snowfall=Mathf.Clamp01(snowfall);wind=Mathf.Clamp01(wind);
            Wetness=Mathf.MoveTowards(Wetness,rain>.06f?Mathf.Min(.8f,rain*.8f+.08f):0,dt/(rain>.06f?8:90));
            if(_season=="winter")
            {
                // A lull in snowfall is not a thaw. Snow stays on the roof
                // until rain washes it off or the environment changes season.
                SnowCover=Mathf.MoveTowards(SnowCover,Mathf.Max(SnowCover,snowfall*.68f),dt*snowfall/55);
                if(snowfall<.02f&&rain>.1f)SnowCover=Mathf.Max(0,SnowCover-dt*rain/160);
            }
            else SnowCover=Mathf.MoveTowards(SnowCover,0,dt/35);
            float leaves=_season=="autumn"?Mathf.Clamp01(_treeDensity)*(.05f+.10f*wind)*(1-.7f*rain):0;
            LeafCover=Mathf.MoveTowards(LeafCover,leaves,dt/180);
            float previous=_storageAmount;_storageAmount=Mathf.MoveTowards(_storageAmount,_storageTarget,dt/.65f);
            if(previous!=_storageAmount)DeformationVersion++;
            ApplySurfaceState();
        }
        void ApplySurfaceState(bool force=false)
        {
            var state=new Vector4(Wetness,SnowCover,LeafCover,_storageAmount);
            if(!force&&_surfaceApplied&&state.Equals(_appliedSurface))return;
            _surfaceApplied=true;_appliedSurface=state;
            void Set(Material material)
            {
                if(material==null)return;
                material.SetFloat("_ClothWetness",Wetness);material.SetFloat("_SnowCover",SnowCover);material.SetFloat("_LeafCover",LeafCover);
                material.SetFloat("_StorageAmount",_storage.ContainsKey(material)?_storageAmount:0);
            }
            foreach(var material in _owned)Set(material);foreach(var material in _additionalFabric)Set(material);
        }
        /// <summary>CPU counterpart of the cloth vertex shader, also used by cosmetic rain contacts.</summary>
        public static Vector3 DeformVertex(Vector3 p, Bounds bounds, Transform space, Vector2 wind, float strength, float seconds)
            => DeformVertex(p,bounds,space.localToWorldMatrix,space.worldToLocalMatrix,wind,strength,seconds);
        public static Vector3 DeformVertex(Vector3 p, Bounds bounds, Matrix4x4 localToWorld, Matrix4x4 worldToLocal, Vector2 wind, float strength, float seconds,Vector4 storage=default,float pressure=0)
        {
            var min=bounds.min;var size=bounds.size;
            var u=new Vector3(Mathf.Clamp01((p.x-min.x)/Mathf.Max(.001f,size.x)),Mathf.Clamp01((p.y-min.y)/Mathf.Max(.001f,size.y)),Mathf.Clamp01((p.z-min.z)/Mathf.Max(.001f,size.z)));
            float pin=Mathf.Sin(u.y*Mathf.PI)*Mathf.Sin(u.x*Mathf.PI)*(.35f+.65f*Mathf.Sin(u.z*Mathf.PI));
            var world=localToWorld.MultiplyPoint3x4(p);
            float wave=Mathf.Sin(seconds*2.1f+world.x*.8f+world.z*1.1f)+.3f*Mathf.Sin(seconds*4.4f+world.x);
            float ripple=.18f*Mathf.Sin(seconds*3.3f+world.z*2.4f-world.x*1.6f);
            float height=localToWorld.MultiplyVector(Vector3.up*size.y).magnitude;
            var localWind=QuietCamp.Infrastructure.WindFieldMath.Evaluate(wind,strength,seconds,localToWorld.MultiplyPoint3x4(Vector3.zero),height*.6f,QuietCamp.Infrastructure.WindFieldMath.CurrentShelter);
            float falloff=Mathf.Clamp01(1-Vector3.Distance(p,new Vector3(storage.x,storage.y,storage.z))/Mathf.Max(.001f,storage.w));falloff*=falloff;
            p.x+=(storage.x>=bounds.center.x?1:-1)*falloff*pin*Mathf.Clamp01(pressure)*size.x*.24f;
            return p+worldToLocal.MultiplyVector(new Vector3(localWind.DirectionXZ.x*wave,ripple,localWind.DirectionXZ.y*wave)*(pin*Mathf.Sqrt(localWind.Strength)*height*.22f));
        }
        void OnDestroy(){foreach(var geometry in _geometry)geometry.Release();foreach(var m in _owned) if(m!=null)Destroy(m);if(_interior!=null)Destroy(_interior);if(_atlas!=null)Destroy(_atlas);if(_glowMesh!=null)Destroy(_glowMesh);}
    }
}
