using System.Collections;
using System.IO;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    public class RainRunoffPlayModeTests
    {
        [Test]
        public void RainFallsMostlyDownEvenWithExtremeWind()
        {
            foreach(var wind in new[]{Vector2.zero,Vector2.right,Vector2.up,new Vector2(100,-100)})
            {
                var velocity=CampWeather.DropVelocity(wind);
                Assert.Less(Vector3.Angle(Vector3.down,velocity),4);
                Assert.AreEqual(-4.8f,velocity.y);
            }
            Assert.AreEqual(CampWeather.DropVelocity(Vector2.zero),Vector3.down*4.8f);
        }
        [UnityTest]
        public IEnumerator MarksRequireRealVisibleDropCrossingsOnAllTiers()
        {
            var world=new GameObject("isolated-rain-world");
            var buffer=new ParticleSystem.Particle[96];
            foreach(int tier in new[]{0,1,2})
            {
                var owner=new GameObject("tracked-rain");var weather=owner.AddComponent<CampWeather>();
                weather.World=world.transform;weather.Configure(null,new LevelData{width=5,height=5,decorSeed=0},false);
                weather.Advance(78,tier,false,Vector2.right);
                for(int i=0;i<12;i++)weather.Advance(.05f,tier,false,Vector2.right);
                var rain=owner.transform.Find("GentleRain").GetComponent<ParticleSystem>();
                Assert.Greater(rain.particleCount,0);Assert.AreEqual(0,weather.ContactCount,"A timer fabricated an impact before any drop moved.");
                var renderer=rain.GetComponent<ParticleSystemRenderer>();
                Assert.AreEqual(ParticleSystemRenderMode.Stretch,renderer.renderMode);
                Assert.AreEqual(0,renderer.cameraVelocityScale);Assert.IsTrue(renderer.rotateWithStretchDirection);
                int expected=0;
                for(int step=0;step<4;step++)
                {
                    int count=rain.GetParticles(buffer);
                    for(int i=0;i<count;i++)if(buffer[i].remainingLifetime>0)
                    {
                        var p=buffer[i];var end=p.position+p.velocity*.30f;
                        float ground=Mathf.Abs(end.x)<2.5f&&Mathf.Abs(end.z)<2.5f?.014f:0;
                        if(RainSurface.TryCastSegment(p.position,end,ground,world.transform,out _))expected++;
                    }
                    rain.Simulate(.30f,false,false,false);weather.Advance(0,tier,false,Vector2.right);
                    Assert.AreEqual(expected,weather.ContactCount,"Each actual crossing must create exactly one contact.");
                }
                Assert.Greater(expected,0);Assert.LessOrEqual(weather.Impacts.ActiveCount,weather.Impacts.Budget);
                int contacts=weather.ContactCount;
                weather.Advance(0,tier,true,Vector2.zero);
                Assert.AreEqual(0,rain.particleCount);Assert.AreEqual(0,weather.Impacts.ActiveCount);
                weather.Advance(0,tier,false,Vector2.zero);
                Assert.AreEqual(contacts,weather.ContactCount,"Cleared rain must not leave delayed phantom hits.");
                Object.Destroy(owner);yield return null;
            }
            Object.Destroy(world);yield return null;
        }
        [UnityTest]
        public IEnumerator FabricRunoffCrossesSeamsDownhillAndStopsAtHem()
        {
            int cached=RainSurface.CachedMeshCount;
            var roof=new GameObject("hard-seam-roof",typeof(MeshFilter),typeof(MeshRenderer));
            var mesh=new Mesh();
            // Two triangles with duplicated hard-edge vertices, as in authored tents.
            mesh.vertices=new[]{new Vector3(-1,0,-1),new Vector3(1,1,-1),new Vector3(-1,0,1),
                new Vector3(1,1,-1),new Vector3(1,1,1),new Vector3(-1,0,1)};
            mesh.triangles=new[]{0,2,1,3,5,4};mesh.RecalculateNormals();mesh.RecalculateBounds();
            roof.GetComponent<MeshFilter>().sharedMesh=mesh;
            var mat=new Material(Shader.Find("QuietCamp/TentCloth"));mat.SetVector("_MeshMin",mesh.bounds.min);mat.SetVector("_MeshSize",mesh.bounds.size);
            roof.GetComponent<Renderer>().sharedMaterial=mat;RainSurface.Attach(roof);
            Shader.SetGlobalFloat("_AtmosWindStrength",0);RainSurface.CaptureWind();
            var contact=RainSurface.Cast(new Ray(new Vector3(.8f,4,0),Vector3.down),0,roof.transform);
            Assert.IsTrue(contact.IsFabric);int triangle=contact.triangle;float previous=contact.point.y;bool crossed=false,stopped=false;
            for(int i=0;i<80;i++)
            {
                bool moved=contact.Slide(.04f);Assert.IsTrue(contact.Resolve(out var point,out _));
                Assert.LessOrEqual(point.y,previous+.00001f);previous=point.y;
                Assert.That(point.x,Is.InRange(-1.001f,1.001f));Assert.That(point.z,Is.InRange(-1.001f,1.001f));
                crossed|=contact.triangle!=triangle;
                if(!moved){stopped=true;break;}
            }
            Assert.IsTrue(crossed,"Runoff must cross a duplicated-vertex authoring seam.");Assert.IsTrue(stopped,"Water must stop at an open hem.");
            Assert.That(contact.point.x,Is.EqualTo(-1).Within(.001f));
            var marks=new GameObject("runoff-budget").AddComponent<RainImpacts>();marks.Configure();marks.SetQuality(0,false);
            marks.Emit(RainSurface.Cast(new Ray(new Vector3(.8f,4,0),Vector3.down),0,roof.transform),.085f);marks.Advance(.4f);
            var ground=new RainSurface.Contact{triangle=-1,point=Vector3.zero,normal=Vector3.up};
            for(int i=0;i<4;i++)marks.Emit(ground,.085f);
            marks.Advance(0);Assert.AreEqual(1,marks.RunoffCount,"Ground splashes erased the flowing fabric before it reached the hem.");
            Assert.AreEqual(3,marks.ActiveCount);Object.Destroy(marks.gameObject);
            Object.Destroy(roof);Object.Destroy(mesh);Object.Destroy(mat);yield return null;
            Assert.AreEqual(cached,RainSurface.CachedMeshCount);
        }
        [UnityTest]
        public IEnumerator RealTentRunoffFollowsWindAndReleasesItsDraw()
        {
            int cached=RainSurface.CachedMeshCount;float strength=Shader.GetGlobalFloat("_AtmosWindStrength"),time=Shader.GetGlobalFloat("_AtmosWindTime");
            Vector4 wind=Shader.GetGlobalVector("_AtmosWindXZ");
            foreach(string id in new[]{"tent_smallOpen","tent_detailedOpen"})
            {
                var tent=Object.Instantiate(AssetCatalog.Load().Prefab(id));RainSurface.Attach(tent);TentCloth.Apply(tent);
                foreach(var t in tent.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
                Shader.SetGlobalVector("_AtmosWindXZ",new Vector4(1,0,0,0));Shader.SetGlobalFloat("_AtmosWindStrength",.18f);
                Shader.SetGlobalFloat("_AtmosWindTime",0);RainSurface.CaptureWind();
                var bounds=tent.GetComponentInChildren<Renderer>().bounds;
                var marks=new GameObject("runoff",typeof(RainImpacts)).GetComponent<RainImpacts>();marks.gameObject.layer=30;marks.Configure();marks.SetQuality(2,false);
                for(int x=2;x<8;x++)for(int z=2;z<8;z++)
                {
                    var origin=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,x/10f),bounds.max.y+2,Mathf.Lerp(bounds.min.z,bounds.max.z,z/10f));
                    var hit=RainSurface.Cast(new Ray(origin,Vector3.down),0,tent.transform);
                    if(hit.IsFabric&&hit.normal.y>.2f&&hit.normal.y<.95f&&hit.point.y>.2f)marks.Emit(hit,.085f);
                }
                Assert.Greater(marks.RunoffCount,0,id);
                for(int frame=0;frame<18;frame++)
                {
                    if(frame==9)tent.transform.position+=new Vector3(.4f,0,.2f);
                    Shader.SetGlobalFloat("_AtmosWindTime",frame*.06f);RainSurface.CaptureWind();marks.Advance(.06f);
                    Capture(tent,marks,id,bounds,frame);
                    var vertices=marks.GetComponent<MeshFilter>().sharedMesh.vertices;var colors=marks.GetComponent<MeshFilter>().sharedMesh.colors;
                    int wet=0;
                    for(int v=0;v<vertices.Length;v++)if(colors[v].a>.015f)
                    {
                        var point=marks.transform.TransformPoint(vertices[v]);
                        var hit=RainSurface.Cast(new Ray(point+Vector3.up*.1f,Vector3.down),0,tent.transform);
                        Assert.IsNotNull(hit.surface,"A wet trace floated off its tent.");
                        Assert.Less(Vector3.Distance(hit.point,point),.045f,"Trace did not follow the deformed roof.");wet++;
                    }
                    if(frame==8)Assert.Greater(wet,0);
                }
                marks.Advance(3);Assert.AreEqual(0,marks.RunoffCount);Assert.IsFalse(marks.GetComponent<Renderer>().enabled);
                Object.Destroy(marks.gameObject);Object.Destroy(tent);yield return null;
            }
            Shader.SetGlobalFloat("_AtmosWindStrength",strength);Shader.SetGlobalFloat("_AtmosWindTime",time);Shader.SetGlobalVector("_AtmosWindXZ",wind);
            RainSurface.CaptureWind();Assert.AreEqual(cached,RainSurface.CachedMeshCount);
        }
        static void Capture(GameObject tent,RainImpacts marks,string id,Bounds bounds,int frame)
        {
            var go=new GameObject("roof-runoff-camera");var camera=go.AddComponent<Camera>();camera.cullingMask=1<<30;
            camera.orthographic=true;camera.orthographicSize=Mathf.Max(bounds.size.x,bounds.size.z)*.7f;
            camera.transform.position=bounds.center+new Vector3(-3,4,3);camera.transform.LookAt(bounds.center);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.17f,.23f,.21f);
            var sunlight=new GameObject("rain-roof-light").AddComponent<Light>();sunlight.type=LightType.Directional;
            sunlight.transform.rotation=Quaternion.Euler(45,30,0);sunlight.intensity=1.1f;sunlight.color=new Color(1,.91f,.79f);sunlight.cullingMask=1<<30;
            var target=RenderTexture.GetTemporary(640,640,24);var previous=RenderTexture.active;camera.targetTexture=target;
            camera.Render();RenderTexture.active=target;
            var image=new Texture2D(640,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();
            var folder=Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/RainRunoff");Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder,id+"-runoff-"+frame.ToString("00")+".png"),image.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);Object.Destroy(image);Object.Destroy(go);Object.Destroy(sunlight.gameObject);
        }
    }
}
