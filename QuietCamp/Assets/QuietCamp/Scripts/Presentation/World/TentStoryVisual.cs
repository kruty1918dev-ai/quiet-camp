using System.Collections.Generic;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Small belongings and a cloth entrance tell a guest's story without text or gameplay colliders.</summary>
    public sealed class TentStoryVisual:MonoBehaviour
    {
        static readonly int[] BoxFaces={0,3,2,1,4,5,6,7,0,4,7,3,1,2,6,5,0,1,5,4,3,7,6,2};
        readonly List<Material> _materials=new List<Material>();readonly List<Mesh> _meshes=new List<Mesh>();
        Transform _curtain;GameObject _pennant;float _closure,_target,_pennantCheck;bool _reduced;
        System.Func<bool> _pennantEnabled;
        LevelData _level;string _guestId;int _identitySeed,_shelteredMask=3;TentCloth _cloth;
        Transform _inside,_outside;Mesh _insideMesh,_outsideMesh;Material _belongingsMaterial;
        Rect[] _outdoor=new Rect[2];Placement _lastPlacement;Transform _outdoorParent;
        bool _pending,_held,_built,_retiring,_lifeMotion=true;float _settleAt;
        bool _cared;
        public bool Cared => _cared;
        public TentLivingSpace LivingSpace {get;private set;}
        public bool Settled=>!_pending;
        public int ShelteredOutdoorCount=>(_shelteredMask&1)+((_shelteredMask>>1)&1);
        public int ShelteredOutdoorMask=>_shelteredMask;
        public Transform OutdoorRoot=>_outside;
        public Rect[] OutdoorRects=>(Rect[])_outdoor.Clone();
        public float Closure=>_closure;
        public bool PennantVisible=>_pennant!=null&&_pennant.activeSelf;
        public static void Attach(GameObject tent,LevelData level,string guestId,System.Func<bool> pennantEnabled=null)
        {
            if(tent==null||tent.GetComponent<TentStoryVisual>()!=null)return;
            var story=tent.AddComponent<TentStoryVisual>();
            story._pennantEnabled=pennantEnabled??(()=>
                ((QuietCampBootstrap.ServicesRef?.Progression.CosmeticFlags??0)&QuietCamp.Application.TutorialDirector.PennantFlag)!=0
                &&(QuietCampBootstrap.ServicesRef?.Settings.trailPennant??false));
            story._level=level;story._guestId=guestId;
            // Inactive pools own no meshes/materials until their first enable.
            // Unity skips OnDestroy for objects which were never activated.
            if(tent.activeInHierarchy)story.Build(level,guestId);
        }
        void OnEnable(){if(!_built&&_level!=null)Build(_level,_guestId);}
        public static void CloseAll(Transform world,bool reducedMotion)
        {
            if(world==null)return;
            foreach(var story in world.GetComponentsInChildren<TentStoryVisual>())story.Close(reducedMotion);
        }
        public void Close(bool reducedMotion)
        {
            _reduced=reducedMotion;_target=1;
            RefreshLifeMotion();
            if(_reduced){_closure=1;Pose();}
        }
        public void ResetCare()
        {
            if (!_built || !_cared) return;
            _cared = false; BuildInside(_shelteredMask);
        }
        public void ShowCare()
        {
            if (!_built || _cared) return;
            _cared = true; _target = .15f; _closure = .15f; Pose();
            BuildInside(_shelteredMask);
            if (_inside != null) _inside.gameObject.SetActive(true);
        }
        void Build(LevelData level,string guestId)
        {
            _built=true;_level=level;
            var guest=System.Array.Find(level.guests??System.Array.Empty<GuestData>(),g=>g.id==guestId);
            string identity=string.IsNullOrEmpty(guest?.nameKey)?guestId:guest.nameKey;
            _identitySeed=17;foreach(char c in identity??"")_identitySeed=unchecked(_identitySeed*31+c);
            TentCloth.Apply(gameObject);_cloth=GetComponent<TentCloth>();_cloth.ConfigureSeason(level);
            LivingSpace=TentLivingSpace.Measure(transform);
            _inside=new GameObject("Guest belongings").transform;
            // A metre stays a metre: VisualCenter itself has prefab-specific scale.
            _inside.SetParent(transform.Find("LiftNode")??transform,false);_inside.gameObject.layer=gameObject.layer;
            _insideMesh=new Mesh{name="Sheltered guest belongings"};_meshes.Add(_insideMesh);
            _inside.gameObject.AddComponent<MeshFilter>().sharedMesh=_insideMesh;
            _belongingsMaterial=new Material(Resources.Load<Shader>("QuietCamp/TentBelongings")){name="Quiet guest belongings"};
            _belongingsMaterial.SetColor("_BaseColor",Color.white);_materials.Add(_belongingsMaterial);_cloth.RegisterInterior(_belongingsMaterial);
            RefreshLifeMotion();
            var insideRenderer=_inside.gameObject.AddComponent<MeshRenderer>();insideRenderer.sharedMaterial=_belongingsMaterial;
            insideRenderer.shadowCastingMode=ShadowCastingMode.On;insideRenderer.receiveShadows=true;
            BuildInside(3);_inside.gameObject.SetActive(false);_pending=true;_settleAt=Time.unscaledTime+.42f;
            RefreshPennant();
            var door=transform.Find("DoorMarker");if(door==null)return;
            // The logical door cell is offset sideways by half a cell. It is
            // a route cue, not the physical entrance's normal or dimensions.
            // Fit the closure to the actual fabric after prefab scaling.
            float halfWidth=.43f,height=.79f,front=.87f,bottom=.035f,centreX=0;
            var color=new Color(.59f,.32f,.22f);bool fitted=false;
            foreach(var r in GetComponentsInChildren<MeshRenderer>())
            {
                foreach(var m in r.sharedMaterials)
                {
                    if(m==null||m.shader.name!="QuietCamp/TentCloth")continue;
                    color=m.GetColor("_BaseColor");
                    var min=m.GetVector("_MeshMin");var size=m.GetVector("_MeshSize");
                    var a=transform.InverseTransformPoint(r.transform.TransformPoint(min));
                    var b=transform.InverseTransformPoint(r.transform.TransformPoint((Vector3)min+(Vector3)size));
                    halfWidth=Mathf.Abs(b.x-a.x)*.5f;height=Mathf.Abs(b.y-a.y);
                    centreX=(a.x+b.x)*.5f;bottom=Mathf.Min(a.y,b.y);front=Mathf.Max(a.z,b.z)+.006f;
                    fitted=halfWidth>.01f&&height>.01f;if(fitted)break;
                }
                if(fitted)break;
            }
            _curtain=new GameObject("Settling entrance cloth").transform;
            _curtain.SetParent(transform.Find("LiftNode")??transform,false);
            _curtain.localPosition=new Vector3(centreX,bottom,front);
            var curtainMesh=new Mesh{name="Tent entrance panels"};
            curtainMesh.vertices=new[]{new Vector3(-halfWidth,0,0),new Vector3(halfWidth,0,0),new Vector3(0,height,0),new Vector3(0,0,.018f),
                new Vector3(-halfWidth*.5f,height*.5f,0),new Vector3(0,height*.5f,.009f),new Vector3(-halfWidth*.5f,0,.009f),
                new Vector3(halfWidth*.5f,height*.5f,0),new Vector3(halfWidth*.5f,0,.009f)};
            // Intermediate fabric vertices can breathe in the wind; the
            // authored corner/ridge-only panel had every vertex pinned.
            curtainMesh.triangles=new[]{0,6,4,6,3,5,4,5,2,6,5,4,3,8,5,8,1,7,5,7,2,8,7,5};
            curtainMesh.RecalculateNormals();curtainMesh.RecalculateBounds();var curtainBounds=curtainMesh.bounds;curtainBounds.Expand(new Vector3(height*.6f,.04f,height*.6f));curtainMesh.bounds=curtainBounds;_meshes.Add(curtainMesh);
            _curtain.gameObject.AddComponent<MeshFilter>().sharedMesh=curtainMesh;
            var shader=Resources.Load<Shader>("QuietCamp/TentCloth");var entranceFabric=new Material(shader){name="Entrance canvas"};
            entranceFabric.SetColor("_BaseColor",color);entranceFabric.SetVector("_MeshMin",new Vector4(-halfWidth,0,0,0));entranceFabric.SetVector("_MeshSize",new Vector4(halfWidth*2,height,.04f,0));_materials.Add(entranceFabric);
            var curtainRenderer=_curtain.gameObject.AddComponent<MeshRenderer>();curtainRenderer.sharedMaterial=entranceFabric;curtainRenderer.shadowCastingMode=ShadowCastingMode.On;curtainRenderer.receiveShadows=true;
            entranceFabric.SetVector("_DoorAnchor",new Vector4(0,height*.18f,0,height*.75f));
            _cloth.RegisterFabric(entranceFabric,true);
            Pose();
        }
        void Update()
        {
            if(!_built||_retiring)return;
            if(Time.unscaledTime>=_pennantCheck)
            {
                _pennantCheck=Time.unscaledTime+.5f;RefreshPennant();
                RefreshLifeMotion();
            }
            if(_pending&&!_held&&Time.unscaledTime>=_settleAt)Settle();
            if(_closure==_target)return;
            _closure=Mathf.MoveTowards(_closure,_target,Time.unscaledDeltaTime/1.15f);Pose();
        }
        public void SetHeld(bool held)=>_held=held;
        public void HideOutdoor(){if(_outside!=null)_outside.gameObject.SetActive(false);}
        public void BeginRemoval(){_retiring=true;_pending=false;HideOutdoor();}
        public void Schedule(Placement placement,Rect[] outdoor,Transform parent,float delay,bool reduced)
        {
            if(_retiring)return;
            bool moved=_lastPlacement==null||_lastPlacement.x!=placement.x||_lastPlacement.z!=placement.z||_lastPlacement.rotation!=placement.rotation;
            bool changed=moved;for(int i=0;i<2;i++)changed|=_outdoor[i]!=outdoor[i];
            if(!changed)return;
            _lastPlacement=placement.Copy();_outdoor=(Rect[])outdoor.Clone();_outdoorParent=parent;
            HideOutdoor();if(moved)_inside.gameObject.SetActive(false);
            _pending=true;_reduced=reduced;_settleAt=Time.unscaledTime+Mathf.Max(0,delay);
            RefreshLifeMotion();
            if(delay<=0&&!_held)Settle();
        }
        void Settle()
        {
            if(_retiring)return;
            _pending=false;int mask=0;for(int i=0;i<TentBelongingsLayout.OutdoorItems;i++)if(_outdoor[i].width<=0)mask|=1<<i;
            if(_shelteredMask!=mask){_shelteredMask=mask;BuildInside(mask);}
            _inside.gameObject.SetActive(true);
            _cloth?.SetStorage(ShelteredOutdoorCount*.5f,LivingSpace.Bounds,(_shelteredMask&1)!=0);
            if(_outdoorParent==null)return;
            if(_outside==null)
            {
                _outside=new GameObject("Ground belongings: "+(_lastPlacement?.guestId??"guest")).transform;
                _outside.SetParent(_outdoorParent,false);_outside.gameObject.layer=BoardRenderer.DecorLayer;
                _outsideMesh=new Mesh{name="Weatherproof outdoor belongings"};_meshes.Add(_outsideMesh);
                _outside.gameObject.AddComponent<MeshFilter>().sharedMesh=_outsideMesh;
                var renderer=_outside.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=_belongingsMaterial;
                renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            }
            var vertices=new List<Vector3>();var indices=new List<int>();var colors=new List<Color>();
            for(int i=0;i<2;i++)if(_outdoor[i].width>0)OutdoorItem(i,new Vector3(_outdoor[i].center.x,.008f,_outdoor[i].center.y),vertices,indices,colors);
            for(int i=0;i<colors.Count;i++){var color=colors[i];color.a=0;colors[i]=color;}
            Fill(_outsideMesh,vertices,indices,colors);_outside.gameObject.SetActive(vertices.Count>0);
            if(vertices.Count>0)
            {
                RainSurface.Attach(_outside.gameObject);
                _outside.GetComponent<RainSurface>()?.RefreshGeometry();
            }
        }
        static void Box(List<Vector3> vertices,List<int> indices,List<Color> colors,Vector3 centre,Vector3 size,Color color)
        {
            // Split the six faces so their normals remain flat. Shared corner
            // normals made wood and folded possessions look like soft blobs.
            var half=size*.5f;
            var corners=new[]{new Vector3(-half.x,-half.y,-half.z),new Vector3(half.x,-half.y,-half.z),new Vector3(half.x,half.y,-half.z),new Vector3(-half.x,half.y,-half.z),
                new Vector3(-half.x,-half.y,half.z),new Vector3(half.x,-half.y,half.z),new Vector3(half.x,half.y,half.z),new Vector3(-half.x,half.y,half.z)};
            for(int face=0;face<6;face++)
            {
                int first=vertices.Count;
                for(int i=0;i<4;i++){vertices.Add(centre+corners[BoxFaces[face*4+i]]);colors.Add(color);}
                indices.Add(first);indices.Add(first+1);indices.Add(first+2);
                indices.Add(first);indices.Add(first+2);indices.Add(first+3);
            }
        }
        static void Fill(Mesh mesh,List<Vector3> vertices,List<int> indices,List<Color> colors)
        {mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.SetColors(colors);mesh.RecalculateNormals();mesh.RecalculateBounds();}
        static void Face(List<Vector3> vertices,List<int> indices,List<Color> colors,Color color,Vector3 a,Vector3 b,Vector3 c,Vector3? d=null)
        {
            int first=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
            colors.Add(color);colors.Add(color);colors.Add(color);indices.Add(first);indices.Add(first+1);indices.Add(first+2);
            if(!d.HasValue)return;
            vertices.Add(d.Value);colors.Add(color);indices.Add(first);indices.Add(first+2);indices.Add(first+3);
        }
        static Vector3 Rim(Vector3 at,float radius,float height,int side)
        {float angle=side*Mathf.PI*.25f;return at+new Vector3(Mathf.Cos(angle)*radius,height,Mathf.Sin(angle)*radius);}
        static void Cylinder(List<Vector3> vertices,List<int> indices,List<Color> colors,Vector3 at,float bottom,float top,float height,Color color,bool closed=true)
        {
            for(int side=0;side<8;side++)
            {
                var a=Rim(at,bottom,0,side);var b=Rim(at,bottom,0,side+1);
                var c=Rim(at,top,height,side);var d=Rim(at,top,height,side+1);
                Face(vertices,indices,colors,color,a,c,d,b);
                Face(vertices,indices,colors,color,at,a,b);
                if(closed)Face(vertices,indices,colors,color,at+Vector3.up*height,d,c);
            }
        }
        static void Cup(List<Vector3> vertices,List<int> indices,List<Color> colors,Vector3 at)
        {
            var clay=new Color(.59f,.43f,.28f);Cylinder(vertices,indices,colors,at,.039f,.044f,.10f,clay,false);
            for(int side=0;side<8;side++)
            {
                var top=Rim(at,.044f,.10f,side);var nextTop=Rim(at,.044f,.10f,side+1);
                var inner=Rim(at,.033f,.10f,side);var nextInner=Rim(at,.033f,.10f,side+1);
                Face(vertices,indices,colors,clay,top,inner,nextInner,nextTop);
                Face(vertices,indices,colors,clay*.82f,Rim(at,.027f,.016f,side+1),nextInner,inner,Rim(at,.027f,.016f,side));
                Face(vertices,indices,colors,clay*.82f,at+Vector3.up*.016f,Rim(at,.027f,.016f,side+1),Rim(at,.027f,.016f,side));
            }
            // An open handle, not a solid block attached to the cup.
            Box(vertices,indices,colors,at+new Vector3(.065f,.055f,0),new Vector3(.016f,.055f,.023f),clay);
            for(int i=0;i<2;i++)Box(vertices,indices,colors,at+new Vector3(.053f,i==0?.030f:.080f,0),new Vector3(.03f,.012f,.023f),clay);
        }
        void OutdoorItem(int item,Vector3 at,List<Vector3> vertices,List<int> indices,List<Color> colors)
        {
            if(item==0)
            {
                // Only a weatherproof stool and closed cooking pot go outside.
                // Footwear, papers, bags and keepsakes always stay under canvas.
                var wood=new Color(.40f,.31f,.22f);
                Box(vertices,indices,colors,at+Vector3.up*.19f,new Vector3(.27f,.035f,.25f),wood);
                for(int leg=0;leg<4;leg++)Box(vertices,indices,colors,at+new Vector3((leg%2==0?-1:1)*.095f,.085f,(leg<2?-1:1)*.085f),new Vector3(.03f,.17f,.03f),wood*.86f);
            }
            else
            {
                var iron=new Color(.30f,.34f,.31f);
                Cylinder(vertices,indices,colors,at,.064f,.080f,.10f,iron);
                Cylinder(vertices,indices,colors,at+Vector3.up*.102f,.085f,.090f,.014f,new Color(.39f,.42f,.37f));
                Cylinder(vertices,indices,colors,at+Vector3.up*.119f,.016f,.017f,.023f,iron);
                for(int side=0;side<2;side++)Box(vertices,indices,colors,at+new Vector3((side==0?-1:1)*.098f,.065f,0),new Vector3(.026f,.015f,.045f),iron);
            }
        }
        void BuildInside(int shelteredMask)
        {
            var vertices=new List<Vector3>();var indices=new List<int>();var colors=new List<Color>();
            var b=LivingSpace.Bounds;float w=LivingSpace.Width,h=LivingSpace.Height,d=LivingSpace.Depth,y=LivingSpace.Floor;
            var fabric=(_identitySeed&2)==0?new Color(.39f,.49f,.43f):new Color(.57f,.45f,.31f);
            void Add(Vector3 at,Vector3 size,Color color)=>Box(vertices,indices,colors,at,size,color);
            // Sleep, rest, and reading are visible through the real open entrance.
            Add(new Vector3(b.center.x,y+.014f,b.center.z),new Vector3(w*.64f,.028f,d*.67f),new Color(.33f,.36f,.28f));
            Add(new Vector3(b.center.x-w*.12f,y+.055f,b.center.z-d*.03f),new Vector3(w*.30f,.07f,d*.56f),fabric);
            Add(new Vector3(b.center.x-w*.12f,y+.10f,b.center.z-d*.26f),new Vector3(w*.23f,.07f,d*.12f),new Color(.78f,.73f,.57f));
            for(int fold=0;fold<3;fold++)Add(new Vector3(b.center.x-w*.12f,y+.093f,b.center.z+d*(.10f+fold*.025f)),new Vector3(w*.29f,.012f,d*.012f),fabric*.86f);
            var pack=new Vector3(b.center.x+w*.19f,y+h*.075f,b.center.z-d*.25f);
            Add(pack,new Vector3(w*.16f,h*.15f,d*.15f),fabric);
            for(int strap=0;strap<2;strap++)Add(pack+new Vector3((strap==0?-1:1)*w*.045f,0,d*.078f),new Vector3(.022f,h*.16f,.02f),new Color(.31f,.24f,.18f));
            // Valuable papers, seeds, keepsakes and a mug live indoors in every weather.
            Add(new Vector3(b.center.x+w*.12f,y+.023f,b.center.z+d*.22f),new Vector3(w*.13f,.026f,d*.11f),new Color(.71f,.65f,.50f));
            Add(new Vector3(b.center.x+w*.12f,y+.041f,b.center.z+d*.22f),new Vector3(.016f,.012f,d*.10f),new Color(.40f,.35f,.27f));
            Cup(vertices,indices,colors,new Vector3(b.center.x+w*.28f,y,b.center.z+d*.19f));
            var box=new Vector3(b.center.x+w*.27f,y+h*.085f,b.center.z-d*.08f);
            Add(box,new Vector3(w*.17f,h*.17f,d*.17f),new Color(.48f,.34f,.23f));
            Add(box+Vector3.up*(h*.091f),new Vector3(w*.18f,.025f,d*.18f),new Color(.56f,.42f,.28f));
            // Overflow is real geometry under the roof, not an invisible flag.
            // A neatly paired pair of shoes is always sheltered, including clear weather.
            for(int shoe=0;shoe<2;shoe++)
            {
                var at=new Vector3(b.center.x-w*.18f+(shoe==0?-1:1)*.074f,y+.035f,b.center.z+d*.36f);
                Add(at,new Vector3(.105f,.07f,.22f),new Color(.26f,.22f,.18f));
                Add(at+new Vector3(0,.043f,-.065f),new Vector3(.09f,.10f,.085f),new Color(.31f,.25f,.19f));
            }
            if((shelteredMask&1)!=0)
            {
                // Folded furniture can fit in a corner without piercing the pitched roof.
                var at=new Vector3(b.center.x-w*.34f,y,b.center.z-d*.10f);
                Add(at+Vector3.up*.11f,new Vector3(.065f,.22f,.20f),new Color(.40f,.31f,.22f));
                Add(at+new Vector3(.04f,.09f,0),new Vector3(.025f,.18f,.16f),new Color(.33f,.26f,.19f));
            }
            if((shelteredMask&2)!=0)OutdoorItem(1,new Vector3(b.center.x+w*.18f,y,b.center.z+d*.29f),vertices,indices,colors);
            var lamp=new Vector3(b.center.x,y,b.center.z-d*.37f);
            Add(lamp+Vector3.up*.02f,new Vector3(.11f,.04f,.11f),new Color(.29f,.31f,.26f));
            Add(lamp+Vector3.up*.10f,new Vector3(.065f,.12f,.065f),new Color(1.25f,.79f,.30f));
            Add(lamp+Vector3.up*.18f,new Vector3(.12f,.035f,.12f),new Color(.65f,.47f,.23f));
            if(EnvironmentCompositionData.For(_level).seasonId=="summer")
            {
                var bird=new Vector3(b.center.x,y+.03f,b.center.z+d*.30f);
                Add(bird,new Vector3(.11f,.045f,.06f),new Color(.43f,.31f,.20f));
                Add(bird+new Vector3(.04f,.04f,0),new Vector3(.045f,.05f,.045f),new Color(.48f,.36f,.24f));
            }
            // Bake the nearby lantern's reach into vertex alpha. The shader
            // uses it only at night/in rain; outdoor belongings have alpha 0.
            // No extra light, shadow map or per-frame distance calculation.
            if (_cared)
            {
                Cup(vertices,indices,colors,new Vector3(b.center.x+w*.16f,y,b.center.z+d*.19f));
                Add(new Vector3(b.center.x-w*.29f,y+h*.43f,b.center.z-d*.08f),new Vector3(w*.14f,h*.24f,.018f),fabric);
                Add(new Vector3(b.center.x-w*.29f,y+h*.565f,b.center.z-d*.08f),new Vector3(w*.20f,.022f,.03f),new Color(.44f,.32f,.22f));
            }
            var lightAt=lamp+Vector3.up*.13f;
            for(int i=0;i<colors.Count;i++)
            {
                var color=colors[i];float reach=Mathf.Clamp01(1-(vertices[i]-lightAt).magnitude/Mathf.Max(w,d));
                color.a=reach*reach;colors[i]=color;
            }
            Fill(_insideMesh,vertices,indices,colors);
            _inside.GetComponent<RainSurface>()?.RefreshGeometry();
        }
        void RefreshPennant()
        {
            bool visible=_pennantEnabled?.Invoke()??false;
            if(visible&&_pennant==null)BuildPennant();
            if(_pennant!=null&&_pennant.activeSelf!=visible)_pennant.SetActive(visible);
        }
        void RefreshLifeMotion()
        {
            if(_belongingsMaterial==null)return;
            bool enabled=!(QuietCampBootstrap.ServicesRef?.ReducedMotion??_reduced);
            if(_lifeMotion==enabled)return;
            _lifeMotion=enabled;_belongingsMaterial.SetFloat("_LifeMotion",enabled?1:0);
        }
        void BuildPennant()
        {
            var lift=transform.Find("LiftNode");Transform visual=null;
            foreach(var candidate in GetComponentsInChildren<Transform>())if(candidate.name=="VisualCenter"){visual=candidate;break;}
            _pennant=new GameObject("Earned trail pennant");_pennant.transform.SetParent(visual!=null?visual:lift!=null?lift:transform,false);
            _pennant.layer=gameObject.layer;
            _pennant.transform.localPosition=new Vector3(.43f,1.04f,-.42f);
            var vertices=new List<Vector3>();var triangles=new List<int>();EnvironmentComposer.AppendBox(vertices,triangles,Vector3.up*.13f,new Vector3(.017f,.36f,.017f));
            var support=new Mesh{name="Rigid pennant support"};support.SetVertices(vertices);support.SetTriangles(triangles,0);support.RecalculateNormals();support.RecalculateBounds();_meshes.Add(support);
            _pennant.AddComponent<MeshFilter>().sharedMesh=support;
            var poleMaterial=new Material(Shader.Find("Universal Render Pipeline/Simple Lit")){name="Trail pennant support"};poleMaterial.SetColor("_BaseColor",new Color(.38f,.29f,.21f));_materials.Add(poleMaterial);
            _pennant.AddComponent<MeshRenderer>().sharedMaterial=poleMaterial;
            var flag=new GameObject("Wind carried fabric");flag.transform.SetParent(_pennant.transform,false);flag.transform.localPosition=Vector3.up*.24f;flag.layer=gameObject.layer;
            var cloth=new Mesh{name="Tessellated trail pennant"};
            cloth.vertices=new[]{new Vector3(0,-.065f,0),new Vector3(0,0,0),new Vector3(0,.065f,0),new Vector3(.11f,-.035f,.009f),new Vector3(.11f,0,.009f),new Vector3(.11f,.035f,.009f),new Vector3(.23f,0,0)};
            cloth.triangles=new[]{0,1,3,1,4,3,1,2,4,2,5,4,3,4,6,4,5,6};cloth.RecalculateNormals();cloth.RecalculateBounds();var bounds=cloth.bounds;bounds.Expand(.03f);cloth.bounds=bounds;_meshes.Add(cloth);
            flag.AddComponent<MeshFilter>().sharedMesh=cloth;
            var fabric=new Material(Resources.Load<Shader>("QuietCamp/TentCloth")){name="Earned soft trail pennant"};fabric.SetColor("_BaseColor",new Color(.72f,.62f,.37f));
            fabric.SetVector("_MeshMin",new Vector4(0,-.065f,0,0));fabric.SetVector("_MeshSize",new Vector4(.23f,.13f,.018f,0));_materials.Add(fabric);
            var renderer=flag.AddComponent<MeshRenderer>();renderer.sharedMaterial=fabric;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            _cloth?.RegisterFabric(fabric);
        }
        void Pose()
        {
            if(_curtain==null)return;
            _curtain.gameObject.SetActive(_closure>.001f);
            _curtain.localScale=new Vector3(Mathf.SmoothStep(0,1,_closure),1,1);
        }
        void OnDestroy(){if(_outside!=null)Destroy(_outside.gameObject);foreach(var mesh in _meshes)if(mesh!=null)Destroy(mesh);foreach(var material in _materials)if(material!=null)Destroy(material);}
    }
}
