using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kruty1918.Atmos;
using NUnit.Framework;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QuietCamp.Tests
{
    public class VegetationShadowPlayModeTests
    {
        [UnityTest]
        public IEnumerator NormalBreezeMovesFabricAndFoliageAtPhoneFraming()
        {
            foreach (var id in new[] { "tree_default", "tree_pineRoundA", "tent_smallOpen", "tent_detailedOpen", "grass" })
            {
                using (var rig = new ShadowRig(720, 1600))
                {
                    var subject = Object.Instantiate(AssetCatalog.Load().Prefab(id), rig.Root.transform);
                    foreach (var t in subject.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
                    if (id.StartsWith("tree"))
                    {
                        FoliageSway.Shared.ApplySlots(subject, new[] { FoliageSway.Species.Trunk, id.Contains("pine") ? FoliageSway.Species.Conifer : FoliageSway.Species.Canopy });
                        DecorSpawner.ScaleToHeight(subject, 3); rig.CampPlant(subject);
                    }
                    else if (id == "grass") { FoliageSway.Shared.ApplySpecies(subject, FoliageSway.Species.Grass); rig.CampPlant(subject); }
                    else TentCloth.Apply(subject);
                    rig.Frame(subject, true);
                    yield return rig.CheckVisibleMotion(id + "-phone-view");
                }
                yield return null;
            }
        }
        [UnityTest]
        public IEnumerator ImportedCozyPlantsVisiblyMoveAndCastMovingShadowsOnEveryTier()
        {
            var library=CozyVegetationLibrary.Load();Assert.NotNull(library);Assert.AreEqual(6,library.plants.Length);
            foreach(var source in library.plants)
            {
                using(var rig=new ShadowRig())
                {
                    var plant=rig.ImportedPlant(source);rig.Frame(plant);
                    yield return rig.CheckVisibleMotion(source.id);
                    plant.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.ShadowsOnly;
                    yield return rig.CheckShadows(source.id);
                }
                yield return null;
            }
        }
        [UnityTest]
        public IEnumerator RealGrassCastsMovingShadowsOnEveryQualityTier()
        {
            using(var rig=new ShadowRig())
            {
                var grass=Object.Instantiate(AssetCatalog.Load().Prefab("grass"),rig.Root.transform);
                foreach(var t in grass.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
                FoliageSway.Shared.ApplySpecies(grass,FoliageSway.Species.Grass);
                foreach(var renderer in grass.GetComponentsInChildren<MeshRenderer>())rig.Foliage(renderer,false);
                yield return rig.CheckShadows("grass");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SingleSidedBatchedLeavesCastFromBothFacesAndFollowWind()
        {
            using(var rig=new ShadowRig())
            {
                var plant=new GameObject("single-sided fern",typeof(MeshFilter),typeof(MeshRenderer));
                plant.transform.SetParent(rig.Root.transform,false);plant.layer=30;
                var mesh=new Mesh{name="Rooted leaf ribbons"};rig.Meshes.Add(mesh);
                mesh.vertices=new[]{new Vector3(-.15f,0,0),new Vector3(-.08f,.26f,0),new Vector3(.10f,.26f,0),new Vector3(.03f,0,0)};
                mesh.triangles=new[]{0,1,2,0,2,3};
                mesh.SetUVs(1,Enumerable.Repeat(new Vector4(0,0,0,.26f),4).ToList());
                mesh.RecalculateNormals();mesh.RecalculateBounds();plant.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=plant.GetComponent<MeshRenderer>();rig.Foliage(renderer,true);
                yield return rig.CheckShadows("batched-leaf");
                rig.Sun.transform.Rotate(Vector3.up,180,Space.World);rig.UpdateSun();
                yield return rig.CheckShadows("leaf-backface",false);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameplayAndMenuPlantsUseWindAndKeepShadowsWithReducedMotion()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            var deadline=Time.realtimeSinceStartup+20;
            while(Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady!=true && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<QuietCampBootstrap>().StartupReady);
            var services=QuietCampBootstrap.ServicesRef;services.ReducedMotion=false;services.PendingLevelId="QC010";
            yield return SceneManager.LoadSceneAsync("Camp");
            for(int i=0;i<40;i++)yield return null;
            var details=Object.FindAnyObjectByType<ForestDetails>();Assert.NotNull(details);
            var ferns=details.transform.Find("LushUnderstory/Ferns").GetComponent<MeshRenderer>();
            Assert.AreEqual("QuietCamp/FoliageLit",ferns.sharedMaterial.shader.name);
            Assert.AreEqual(1,ferns.sharedMaterial.GetFloat("_ClusterWind"));
            Assert.AreEqual(0,ferns.sharedMaterial.GetFloat("_Cull"));
            var data=new List<Vector4>();ferns.GetComponent<MeshFilter>().sharedMesh.GetUVs(1,data);
            Assert.Greater(data.Select(p=>new Vector2(p.x,p.y)).Distinct().Count(),5,"Each plant needs its own root, not the batch origin.");
            Assert.IsTrue(data.All(p=>p.w>0));
            float phase=Shader.GetGlobalFloat("_AtmosWindTime");yield return new WaitForSecondsRealtime(.3f);
            Assert.Greater(Shader.GetGlobalFloat("_AtmosWindTime"),phase,"Production wind clock stopped");
            Assert.Greater(Shader.GetGlobalFloat("_AtmosWindStrength"),.02f,"Production atmosphere has no breeze");
            foreach(var r in details.transform.Find("LushUnderstory").GetComponentsInChildren<Renderer>(true))
            {Assert.AreEqual(ShadowCastingMode.On,r.shadowCastingMode);Assert.IsTrue(r.receiveShadows);}
            services.ReducedMotion=true;
            foreach(int quality in new[]{1,2,3})
            {
                services.Settings.quality=quality;yield return new WaitForSecondsRealtime(.5f);
                // The near/far groundcover is now one pooled layer, including
                // grass, flowers and shrubs; do not depend on removed prefab scatter.
                var grass=Object.FindAnyObjectByType<VisibleForestFloor>().GetComponentsInChildren<MeshRenderer>();
                Assert.IsNotEmpty(grass);
                foreach(var r in grass)
                {Assert.AreEqual(ShadowCastingMode.On,r.shadowCastingMode);Assert.IsTrue(r.receiveShadows);Assert.AreEqual("QuietCamp/FoliageLit",r.sharedMaterial.shader.name);}
            }
            Assert.That(Shader.GetGlobalFloat("_AtmosWindStrength"),Is.LessThan(.001f));
            services.ReducedMotion=false;services.Settings.quality=2;
            yield return SceneManager.LoadSceneAsync("MainMenu");for(int i=0;i<40;i++)yield return null;
            var flowers=Object.FindAnyObjectByType<VisibleForestFloor>().GetComponentsInChildren<MeshRenderer>();
            Assert.IsNotEmpty(flowers);
            Assert.IsTrue(flowers.All(r=>r.sharedMaterials.All(m=>m.shader.name=="QuietCamp/FoliageLit")),"Menu camp flowers must share the scene's wind shader.");
        }

        [UnityTest]
        public IEnumerator TentFabricReallyMovesAndSharedRefinementLeavesRigidGeometryIntact()
        {
            int cached=TentCloth.CachedGeometryCount;
            foreach(var id in new[]{"tent_smallOpen","tent_detailedOpen"})
            {
                using(var rig=new ShadowRig())
                {
                    var prefab=AssetCatalog.Load().Prefab(id);
                    var tent=Object.Instantiate(prefab,rig.Root.transform);
                    var clone=Object.Instantiate(prefab,rig.Root.transform);clone.SetActive(false);
                    var renderer=tent.GetComponentInChildren<MeshRenderer>();var filter=renderer.GetComponent<MeshFilter>();
                    var authored=filter.sharedMesh;var materials=renderer.sharedMaterials;
                    var rigid=Enumerable.Range(0,materials.Length).Where(i=>!materials[i].name.ToLowerInvariant().Contains("red")&&!materials[i].name.ToLowerInvariant().Contains("yellow")&&!materials[i].name.ToLowerInvariant().Contains("fabric")).ToArray();
                    var clothIndices=Enumerable.Range(0,materials.Length).Except(rigid).SelectMany(authored.GetTriangles).Distinct().ToArray();
                    float ridge=clothIndices.Max(i=>authored.vertices[i].y);
                    Assert.Less(ridge,authored.bounds.max.y,"Fixture must contain poles above the fabric");
                    var collider=tent.GetComponentInChildren<MeshCollider>();var colliderMesh=collider!=null?collider.sharedMesh:null;
                    TentCloth.Apply(tent);TentCloth.Apply(clone);
                    clone.SetActive(true);clone.SetActive(false);
                    Assert.AreSame(filter.sharedMesh,clone.GetComponentInChildren<MeshFilter>(true).sharedMesh,"Identical tents should share one refined mesh");
                    Assert.AreNotSame(authored,filter.sharedMesh);Assert.Greater(filter.sharedMesh.vertexCount,authored.vertexCount);
                    Assert.LessOrEqual(filter.sharedMesh.vertexCount,authored.vertexCount+authored.triangles.Length);
                    foreach(int slot in rigid)CollectionAssert.AreEqual(authored.GetTriangles(slot),filter.sharedMesh.GetTriangles(slot),"Rigid submesh was changed");
                    foreach(var material in renderer.sharedMaterials.Where(m=>m.shader.name=="QuietCamp/TentCloth"))
                    {
                        Vector3 min=material.GetVector("_MeshMin"),size=material.GetVector("_MeshSize");
                        Assert.That(min.y+size.y,Is.EqualTo(ridge).Within(.00001f),"Wooden pole height determines the cloth mask");
                        var clothBounds=new Bounds(min+size*.5f,size);
                        foreach(int index in clothIndices.Where(i=>Mathf.Abs(authored.vertices[i].y-ridge)<.00001f || Mathf.Abs(authored.vertices[i].y-min.y)<.00001f))
                            foreach(float time in new[]{0f,.4f,1.4f,2.2f})
                                Assert.Less(Vector3.Distance(authored.vertices[index],TentCloth.DeformVertex(authored.vertices[index],clothBounds,renderer.transform,Vector2.right,1,time)),.00001f,"Actual ridge or hem moved off its supports");
                    }
                    if(collider!=null)Assert.AreSame(colliderMesh,collider.sharedMesh);
                    foreach(var t in tent.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
                    rig.Frame(tent);yield return rig.CheckVisibleMotion(id);
                }
                yield return null;yield return null;
            }
            Assert.AreEqual(cached,TentCloth.CachedGeometryCount,"Refined cloth meshes leaked");
        }

        [UnityTest]
        public IEnumerator InactiveTentDefersResourcesAndReleasesThemAfterActivation()
        {
            int before = TentCloth.CachedGeometryCount;
            foreach (var activate in new[] { false, true })
            {
                var parent = new GameObject("inactive cloth lifetime"); parent.SetActive(false);
                var tent = Object.Instantiate(AssetCatalog.Load().Prefab("tent_detailedOpen"), parent.transform);
                var filter = tent.GetComponentInChildren<MeshFilter>(true); var original = filter.sharedMesh;
                TentCloth.Apply(tent);
                Assert.AreEqual(before, TentCloth.CachedGeometryCount, "Inactive cloth allocated unowned resources");
                Assert.AreSame(original, filter.sharedMesh);
                if (activate)
                {
                    parent.SetActive(true); Assert.AreNotSame(original, filter.sharedMesh);
                    parent.SetActive(false);
                }
                Object.Destroy(parent); yield return null; yield return null;
                Assert.AreEqual(before, TentCloth.CachedGeometryCount, "Cloth cache leaked after destruction");
            }
        }

        [UnityTest]
        public IEnumerator BroadleafAndPineSilhouettesMoveAtOrdinaryBreezeOnAllQualityTiers()
        {
            foreach(var id in new[]{"tree_default","tree_pineRoundA"})
            {
                using(var rig=new ShadowRig())
                {
                    var tree=Object.Instantiate(AssetCatalog.Load().Prefab(id),rig.Root.transform);
                    foreach(var t in tree.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
                    FoliageSway.Shared.ApplySlots(tree,new[]{FoliageSway.Species.Trunk,id=="tree_default"?FoliageSway.Species.Canopy:FoliageSway.Species.Conifer});
                    rig.CampPlant(tree);rig.Frame(tree);yield return rig.CheckVisibleMotion(id);
                }
                yield return null;
            }
        }

        sealed class ShadowRig : IDisposable
        {
            public readonly GameObject Root=new GameObject("vegetation-shadow-regression");
            public readonly List<Mesh> Meshes=new List<Mesh>();
            readonly List<Material> _materials=new List<Material>();
            readonly List<Renderer> _plants=new List<Renderer>();
            readonly Light[] _otherLights;readonly Behaviour[] _owners;
            readonly int _quality=QualitySettings.GetQualityLevel();
            readonly Light _oldSun=RenderSettings.sun;
            readonly AmbientMode _oldAmbientMode=RenderSettings.ambientMode;
            readonly Color _oldAmbient=RenderSettings.ambientLight;
            readonly Dictionary<string,float> _floats=new Dictionary<string,float>();
            readonly Dictionary<string,Vector4> _vectors=new Dictionary<string,Vector4>();
            readonly Camera _camera;readonly RenderTexture _target;
            public readonly Light Sun;
            public ShadowRig(int width = 384, int height = 384)
            {
                foreach(var n in new[]{"_AtmosWindStrength","_AtmosWindTime","_AtmosWaveLen","_AtmosWaveSpeed","_AtmosFlutterScale"})_floats[n]=Shader.GetGlobalFloat(n);
                foreach(var n in new[]{"_AtmosWindXZ","_AtmosSunDirW","_AtmosSunColor","_AtmosAmbient"})_vectors[n]=Shader.GetGlobalVector(n);
                _otherLights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.enabled).ToArray();
                foreach(var l in _otherLights)l.enabled=false;
                _owners=Object.FindObjectsByType<CampAtmosphere>(FindObjectsSortMode.None).Cast<Behaviour>()
                    .Concat(Object.FindObjectsByType<AdaptiveCampQuality>(FindObjectsSortMode.None))
                    .Concat(Object.FindObjectsByType<PlacementController>(FindObjectsSortMode.None)).Where(b=>b.enabled).ToArray();
                foreach(var b in _owners)b.enabled=false;
                Root.transform.position=new Vector3(3000,0,0);
                var cameraGo=new GameObject("camera");cameraGo.transform.SetParent(Root.transform,false);
                _camera=cameraGo.AddComponent<Camera>();_camera.enabled=false;_camera.cullingMask=1<<30;
                _camera.transform.localPosition=new Vector3(0,1.7f,1.8f);_camera.transform.LookAt(Root.transform.position);
                _camera.orthographic=true;_camera.orthographicSize=.65f;_camera.nearClipPlane=.1f;_camera.farClipPlane=4.5f;
                _camera.clearFlags=CameraClearFlags.SolidColor;_camera.backgroundColor=Color.black;_camera.allowHDR=false;
                _camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                _target=new RenderTexture(width,height,24);_camera.targetTexture=_target;
                var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.SetParent(Root.transform,false);
                floor.layer=30;floor.transform.localScale=Vector3.one*.3f;floor.transform.localPosition=Vector3.down*.003f;
                var floorMat=new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));floorMat.SetColor("_BaseColor",Color.white);_materials.Add(floorMat);
                floor.GetComponent<Renderer>().sharedMaterial=floorMat;floor.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
                var sunGo=new GameObject("test sun");sunGo.transform.SetParent(Root.transform,false);
                Sun=sunGo.AddComponent<Light>();Sun.type=LightType.Directional;Sun.intensity=1;Sun.shadows=LightShadows.Hard;
                Sun.cullingMask=1<<30;Sun.shadowBias=0;Sun.shadowNormalBias=0;Sun.transform.eulerAngles=new Vector3(45,35,0);
                RenderSettings.sun=Sun;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.black;UpdateSun();
                Shader.SetGlobalVector("_AtmosWindXZ",new Vector4(1,0,0,0));Shader.SetGlobalFloat("_AtmosWaveLen",10);
                Shader.SetGlobalFloat("_AtmosWaveSpeed",1.5f);Shader.SetGlobalFloat("_AtmosFlutterScale",0);
            }
            public void Frame(GameObject subject, bool phoneView = false)
            {
                var renderers=subject.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                _camera.orthographicSize=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z)*.72f;
                _camera.transform.position=bounds.center+new Vector3(3,2.5f,4).normalized*10;
                _camera.transform.LookAt(bounds.center);_camera.farClipPlane=20;
                if (phoneView)
                {
                    _camera.transform.rotation = Quaternion.Euler(CameraFitter.Euler);
                    _camera.transform.position = bounds.center - _camera.transform.forward * CameraFitter.Distance;
                    _camera.orthographicSize = 5.5f; _camera.farClipPlane = CameraFitter.Far;
                }
            }
            public GameObject ImportedPlant(CozyVegetationLibrary.Plant source)
            {
                var plant=new GameObject(source.id,typeof(MeshFilter),typeof(MeshRenderer));plant.transform.SetParent(Root.transform,false);plant.layer=30;
                var mesh=Object.Instantiate(source.mesh);Meshes.Add(mesh);
                mesh.SetUVs(1,Enumerable.Repeat(new Vector4(0,0,0,1),mesh.vertexCount).ToList());
                var bounds=mesh.bounds;bounds.Expand(.42f);mesh.bounds=bounds;
                plant.GetComponent<MeshFilter>().sharedMesh=mesh;
                var species=source.kind==CozyVegetationLibrary.PlantKind.Shrub?FoliageSway.Species.Bush:
                    source.kind==CozyVegetationLibrary.PlantKind.Leaf?FoliageSway.Species.Grass:FoliageSway.Species.FlowerStem;
                var mats=source.colors.Select(color=>
                {
                    var material=new Material(FoliageSway.Shared.MaterialForSpecies(color,species)){shader=Shader.Find("QuietCamp/FoliageLit")};
                    material.SetFloat("_Cull",0);material.SetFloat("_ClusterWind",1);CampFoliageResponse.Apply(material);_materials.Add(material);return material;
                }).ToArray();
                var renderer=plant.GetComponent<MeshRenderer>();renderer.sharedMaterials=mats;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                _plants.Add(renderer);return plant;
            }
            public void CampPlant(GameObject tree)
            {
                foreach(var r in tree.GetComponentsInChildren<MeshRenderer>())
                {
                    var mats=r.sharedMaterials;
                    for(int i=0;i<mats.Length;i++)
                    {
                        var m=new Material(mats[i]){shader=Shader.Find("QuietCamp/FoliageLit")};
                        CampFoliageResponse.Apply(m);_materials.Add(m);mats[i]=m;
                    }
                    r.sharedMaterials=mats;CampFoliageResponse.BindRenderer(r);CampFoliageResponse.ExpandBounds(r);
                }
            }
            public IEnumerator CheckVisibleMotion(string name)
            {
                Shader.SetGlobalVector("_AtmosSunColor",Color.white);
                Shader.SetGlobalVector("_AtmosAmbient",new Color(.5f,.5f,.5f,1));
                for(int tier=0;tier<3;tier++)
                {
                    QualitySettings.SetQualityLevel(tier,true);yield return null;yield return null;
                    Shader.SetGlobalFloat("_AtmosFlutterScale",tier==0?0:1);
                    Shader.SetGlobalFloat("_AtmosWindStrength",.18f);Shader.SetGlobalFloat("_AtmosWindTime",0);
                    var before=Capture();int changed=0;Color[] best=null;
                    foreach(float time in new[]{.4f,.9f,1.4f,2.2f})
                    {
                        Shader.SetGlobalFloat("_AtmosWindTime",time);var after=Capture();
                        int delta=before.Zip(after,(a,b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)>.04f?1:0).Sum();
                        if(delta>changed){changed=delta;best=after;}
                    }
                    Assert.Greater(changed,12,name+" still looks static in a normal breeze at tier "+tier);
                    if (name.EndsWith("-phone-view")) Debug.Log("[WindPhoneViewQA] " + name + " tier=" + tier + " changed pixels=" + changed);
                    Save(before,name+"-"+tier+"-breeze-a");Save(best,name+"-"+tier+"-breeze-b");
                    if(tier==1 && (name=="tent_detailedOpen" || name=="tree_default" || name=="plant_bushDetailed" || name=="grass_leafsLarge"))
                    {
                        for(int frame=0;frame<30;frame++)
                        {
                            Shader.SetGlobalFloat("_AtmosWindTime",frame*.1f);
                            Save(Capture(),name+"-motion-"+frame.ToString("D2"));yield return null;
                        }
                    }
                    Shader.SetGlobalFloat("_AtmosWindStrength",0);Shader.SetGlobalFloat("_AtmosWindTime",0);var still=Capture();
                    Shader.SetGlobalFloat("_AtmosWindTime",2.2f);var stopped=Capture();
                    Assert.AreEqual(0,still.Zip(stopped,(a,b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)>.04f?1:0).Sum(),"Zero wind must stop cosmetic deformation");
                }
            }
            public void UpdateSun()=>Shader.SetGlobalVector("_AtmosSunDirW",-Sun.transform.forward);
            public void Foliage(Renderer renderer,bool cluster)
            {
                var material=new Material(FoliageSway.Shared.MaterialForSpecies(new Color(.3f,.5f,.2f),FoliageSway.Species.Grass))
                    {shader=Shader.Find("QuietCamp/FoliageLit")};
                material.SetFloat("_Cull",0);material.SetFloat("_ClusterWind",cluster?1:0);
                _materials.Add(material);renderer.sharedMaterial=material;
                var bounds=renderer.localBounds;var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                block.SetFloat("_MeshMinY",bounds.min.y);block.SetFloat("_MeshTopY",bounds.max.y);renderer.SetPropertyBlock(block);
                bounds.Expand(.05f);renderer.localBounds=bounds;renderer.shadowCastingMode=ShadowCastingMode.ShadowsOnly;
                _plants.Add(renderer);
            }
            public IEnumerator CheckShadows(string name,bool moving=true)
            {
                for(int tier=0;tier<3;tier++)
                {
                    QualitySettings.SetQualityLevel(tier,true);yield return null;yield return null;
                    foreach(var r in _plants)r.enabled=false;
                    var plain=Capture();foreach(var r in _plants)r.enabled=true;
                    Shader.SetGlobalFloat("_AtmosWindStrength",0);Shader.SetGlobalFloat("_AtmosWindTime",0);
                    var calm=Capture();Save(calm,name+"-"+tier+"-calm");
                    int shadow=plain.Zip(calm,(a,b)=>a.r-b.r>.025f?1:0).Sum();
                    Assert.Greater(shadow,8,name+" has no real cast shadow at tier "+tier);
                    if(!moving)continue;
                    Shader.SetGlobalFloat("_AtmosWindStrength",1);
                    int changed=0;Color[] gust=null;
                    for(int i=1;i<=5;i++)
                    {
                        Shader.SetGlobalFloat("_AtmosWindTime",i*.37f);var frame=Capture();
                        int delta=calm.Zip(frame,(a,b)=>Mathf.Abs(a.r-b.r)>.025f?1:0).Sum();
                        if(delta>changed){changed=delta;gust=frame;}
                    }
                    Assert.Greater(changed,3,name+" shadow did not follow wind at tier "+tier);
                    Save(gust,name+"-"+tier+"-gust");
                }
            }
            Color[] Capture()
            {
                _camera.Render();var previous=RenderTexture.active;RenderTexture.active=_target;
                var image=new Texture2D(_target.width,_target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,_target.width,_target.height),0,0);image.Apply();
                var pixels=image.GetPixels();Object.Destroy(image);RenderTexture.active=previous;return pixels;
            }
            void Save(Color[] pixels,string name)
            {
                var image=new Texture2D(_target.width,_target.height,TextureFormat.RGB24,false);image.SetPixels(pixels);image.Apply();
                var path=Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/Vegetation");Directory.CreateDirectory(path);
                File.WriteAllBytes(Path.Combine(path,name+".png"),image.EncodeToPNG());Object.Destroy(image);
            }
            public void Dispose()
            {
                _camera.targetTexture=null;_target.Release();Object.Destroy(_target);Object.Destroy(Root);
                foreach(var m in _materials)Object.Destroy(m);foreach(var m in Meshes)Object.Destroy(m);
                foreach(var l in _otherLights)if(l!=null)l.enabled=true;
                foreach(var b in _owners)if(b!=null)b.enabled=true;
                foreach(var p in _floats)Shader.SetGlobalFloat(p.Key,p.Value);foreach(var p in _vectors)Shader.SetGlobalVector(p.Key,p.Value);
                RenderSettings.sun=_oldSun;RenderSettings.ambientMode=_oldAmbientMode;RenderSettings.ambientLight=_oldAmbient;
                QualitySettings.SetQualityLevel(_quality,true);
            }
        }
    }
}
