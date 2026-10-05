using System.Collections;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    public sealed class CampWaterVisualsPlayModeTests
    {
        readonly System.Collections.Generic.List<GameObject> _owned=new System.Collections.Generic.List<GameObject>();
        GameObject Own(string name){var root=new GameObject(name);_owned.Add(root);return root;}
        [UnityTearDown] public IEnumerator ReleaseOwnedObjects()
        {foreach(var root in _owned)if(root!=null)Object.Destroy(root);_owned.Clear();yield return null;}
        [UnityTest] public IEnumerator DryLevelsDoNotAllocateWaterOrReflectionResources()
        {
            var root=Own("dry shore test");
            Assert.IsNull(CampWaterVisuals.Attach(new LevelData{width=5,height=5},root.transform,null));
            Assert.IsEmpty(root.GetComponentsInChildren<Renderer>());
            Assert.IsEmpty(root.GetComponentsInChildren<ReflectionProbe>());
            Object.Destroy(root);yield return null;
        }
        [UnityTest] public IEnumerator ShoreExcludesTerrestrialTrailAccentsAndForestDetails()
        {
            foreach(var side in new[]{"left","right","front","back"})
            {
                var level=QuietCamp.Infrastructure.LevelLoader.Load("QC007");
                level.environment.shore=new ShorelineData{side=side,offset=6,width=5,seed=19};
                var root=Own("shore decor exclusion "+side);
                var understory=root.AddComponent<CozyUnderstory>();understory.Build(level);
                foreach(var point in understory.PlantRoots)
                    Assert.IsFalse(ShorelineGeometry.Contains(level.environment.shore,point,.15f),"Trail-side terrestrial plant grew inside water");
                var detail=Own("shore forest details "+side);detail.AddComponent<ForestDetails>().Build(level,detail.transform);
                foreach(var filter in detail.GetComponentsInChildren<MeshFilter>())
                foreach(var vertex in filter.sharedMesh.vertices)
                    Assert.IsFalse(ShorelineGeometry.Contains(level.environment.shore,filter.transform.TransformPoint(vertex)),"Forest accent enters water");
                Object.Destroy(root);Object.Destroy(detail);yield return null;
            }
        }

        [UnityTest] public IEnumerator ShoreGridIsFiniteReflectsSkyAndKeepsWavesOutsideThePuzzleWithoutExtraCameras()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
                Assert.Ignore("Water shader compatibility requires a rendered Editor; a null graphics device cannot validate it.");
            var source=Resources.Load<Material>("QuietCamp/Water/CampLakeMobile");
            Assert.NotNull(source);Assert.NotNull(source.shader);Assert.IsTrue(source.shader.isSupported,"Stylized Water 3 shader unsupported on the active graphics API");
            var root=Own("water shore test");var cameraObject=Own("water test camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            int cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length;
            var level=new LevelData{width=7,height=5,decorSeed=19,environment=new EnvironmentCompositionData{seasonId="summer",shore=new ShorelineData{kind="lake",side="left",offset=10,width=7,seed=19}}};
            var visual=CampWaterVisuals.Attach(level,root.transform,camera);Assert.NotNull(visual);Assert.AreEqual(6,visual.ChunkCount);Assert.IsTrue(visual.HasReflection);
            yield return null;
            Assert.AreEqual(cameras,Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length,"Water must reuse a small weather cube without reflection cameras");
            Assert.IsEmpty(root.GetComponentsInChildren<Collider>());
            var probe=visual.GetComponent<ReflectionProbe>();Assert.NotNull(probe);Assert.AreEqual(ReflectionProbeMode.Custom,probe.mode);
            var sky=probe.customBakedTexture as Cubemap;Assert.NotNull(sky);Assert.AreEqual(16,sky.width);
            foreach(CubemapFace face in new[]{CubemapFace.PositiveX,CubemapFace.PositiveY,CubemapFace.NegativeZ})
                Assert.IsTrue(sky.GetPixels(face).All(c=>float.IsFinite(c.r)&&float.IsFinite(c.g)&&float.IsFinite(c.b)));
            var meshes=visual.GetComponentsInChildren<MeshFilter>();Assert.AreEqual(12,meshes.Length);
            foreach(var filter in meshes)
            {
                var mesh=filter.sharedMesh;Assert.NotNull(mesh);Assert.IsTrue(mesh.vertices.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)));
                Assert.IsTrue(mesh.normals.All(n=>n.y>.9f),"Both banks and water must face upward");
                foreach(var local in mesh.vertices)
                {
                    var p=filter.transform.TransformPoint(local);
                    Assert.IsFalse(Mathf.Abs(p.x)<level.width*.5f+1.4f&&Mathf.Abs(p.z)<level.height*.5f+1.4f,"Authored shoreline enters the playable clearing");
                }
            }
            var water=meshes.First(m=>m.name.StartsWith("Shore water "));var material=water.GetComponent<MeshRenderer>().sharedMaterial;
            var profile=material.GetTexture("_WaveProfile") as Texture2D;Assert.NotNull(profile);Assert.IsTrue(profile.isReadable,"Wave clearance verification reads the authored LUT");
            float amplitude=0;int layers=Mathf.Min(profile.width,material.GetInt("_WaveMaxLayers"));
            for(int i=0;i<layers;i++){var parameters=profile.GetPixel(i,0);if(parameters.a>0)amplitude+=Mathf.Abs(parameters.r);}
            float maximumDisplacement=amplitude*(.023f+.026f); // fully exposed gust
            Assert.Greater(water.sharedMesh.vertices.Min(p=>p.y)-maximumDisplacement,0,"Lowest water crest must remain above meadow ground");
            Assert.LessOrEqual(material.GetFloat("_WaveHeight"),.0491f);
            Object.Destroy(root);Object.Destroy(cameraObject);yield return null;
            Assert.IsTrue(sky==null,"The weather cube must be released with its scene owner");
        }
    }
}
