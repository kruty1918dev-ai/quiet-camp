using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Editor.LevelAuthoring.WorldOps;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace QuietCamp.Tests
{
    public sealed class MeadowHeightFieldTests
    {
        [Test] public void EveryProxyAndRetainedFlowerReceivesAdvancingWeatherAndReducedMotion()
        {
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false,true);string png=Convert.ToBase64String(texture.EncodeToPNG());Object.DestroyImmediate(texture);
            var patch=new MeadowHeightFieldPatch{name="wind test",rect=new[]{-4f,-4,8,8},heights=new[]{0f,1},resolution=64,albedoPng=png,normalHeightPng=png,
                flowers=new MeadowRetainedMesh{vertices=new[]{0f,0,0,0,1,0,1,0,0},normals=new[]{0f,0,1,0,0,1,0,0,1},
                    colours=new[]{1f,0,1,1,1,0,1,1,1,0,1,1},response=new[]{0f,1,1,1,0,1}}};
            var art=new CampArtDefinition{width=2,height=2,streamCentre=20,ambientColor=0x334422,
                atmosphere=new SeasonAtmospherePreset{season="spring",wind=.7f,rain=.3f,windHeading=90},
                fieldCover=new CampArtDefinition.FieldCoverSettings{heightFields=new[]{patch}}};
            var level=new LevelData{width=2,height=2,entry=new[]{0,0},blocked=Array.Empty<int[]>(),noise=Array.Empty<int[]>()};
            var root=new GameObject("Meadow weather binding test");CampGroundDetails details=null;
            try
            {
                details=root.AddComponent<CampGroundDetails>();details.Configure(level,null,art);bool reduced=false;details.Bind(null,reduced:()=>reduced);
                var materials=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial.shader.name=="QuietCamp/MeadowHeightField"||r.name.EndsWith("retained flowers")).Select(r=>r.sharedMaterial).Distinct().ToArray();
                Assert.AreEqual(2,materials.Length);details.Advance(.1f,.3f,0);
                foreach(var material in materials)
                {
                    var weather=material.GetVector("_QCWeather");Assert.That(weather.x,Is.EqualTo(.1f).Within(.0001f));Assert.AreEqual(.7f,weather.y);Assert.AreEqual(.3f,weather.z);
                    Assert.That(material.GetVector("_QCWind").x,Is.EqualTo(1).Within(.0001f));
                }
                reduced=true;details.Advance(.1f,.3f,0);
                foreach(var material in materials){Assert.AreEqual(0,material.GetVector("_QCWeather").y);Assert.That(material.GetVector("_QCWeather").x,Is.GreaterThan(.19f));}
            }
            finally{if(details!=null)details.Dispose();Object.DestroyImmediate(root);}
        }
        [Test] public void RetainedFlowersKeepExactGeometryLinearColourAndWindAfterSerialization()
        {
            var source=new Mesh{vertices=new[]{Vector3.zero,Vector3.up,Vector3.right},triangles=new[]{0,1,2},
                colors=new[]{new Color(.13f,.37f,.61f,.8f),Color.magenta,Color.green},uv2=new[]{Vector2.up,Vector2.one,new Vector2(.7f,1)}};
            source.RecalculateNormals();Mesh copy=null;
            try
            {
                var data=WorldOpsMeadowHeightFields.ReadChannels(source,new[]{0,1,2});
                data=JsonConvert.DeserializeObject<MeadowRetainedMesh>(JsonConvert.SerializeObject(data));
                copy=MeadowHeightFieldView.CreateMesh(data);
                CollectionAssert.AreEqual(source.vertices,copy.vertices);CollectionAssert.AreEqual(source.normals,copy.normals);
                CollectionAssert.AreEqual(source.colors,copy.colors);CollectionAssert.AreEqual(source.uv2,copy.uv2);
                CollectionAssert.AreEqual(source.triangles,copy.triangles);
            }
            finally{Object.DestroyImmediate(source);if(copy!=null)Object.DestroyImmediate(copy);}
        }
        [Test] public void ExperimentalOperationCannotEnterAPublishableRecipe()
        {
            var recipe=new WorldRecipe{id="test",operations=new[]{new WorldOperation{id="test",kind="vegetation.heightfield-preview"}}};
            Assert.Throws<ArgumentException>(()=>WorldOpsOperations.Validate(recipe));
            recipe.previewOnly=true;recipe.autoApply=false;recipe.capture=true;
            Assert.DoesNotThrow(()=>WorldOpsOperations.Validate(recipe));
        }
        [Test] public void RejectsInvalidSurfaceBoundsAndMissingSamples()
        {
            var patch=new MeadowHeightFieldPatch();Assert.Throws<InvalidDataException>(()=>patch.Validate());
            patch.rect=new[]{0f,0,1,1};patch.heights=new[]{0f,1};patch.resolution=64;patch.albedoPng="x";patch.normalHeightPng="x";
            Assert.DoesNotThrow(()=>patch.Validate());patch.rect[0]=float.NaN;Assert.Throws<InvalidDataException>(()=>patch.Validate());
        }
        [Test] public void BakesLinearColourNormalsHeightAndWindInWorldCoordinates()
        {
            var mesh=new Mesh{vertices=new[]{new Vector3(0,.2f,0),new Vector3(1,.2f,0),new Vector3(1,.6f,1),new Vector3(0,.6f,1)},
                triangles=new[]{0,2,1,0,3,2}};
            mesh.colors=Enumerable.Repeat(new Color(.2f,.45f,.7f,1),4).ToArray();
            mesh.uv2=new[]{Vector2.up,Vector2.up,Vector2.one,Vector2.one};mesh.RecalculateNormals();mesh.RecalculateBounds();
            Texture2D colour=null,normal=null;
            try
            {
                var patch=WorldOpsMeadowHeightFields.BakePatch(mesh,128);patch.Validate();
                colour=Decode(patch.albedoPng);normal=Decode(patch.normalHeightPng);
                int x=Mathf.RoundToInt((.5f-patch.rect[0])/patch.rect[2]*127);
                int z=Mathf.RoundToInt((.2f-patch.rect[1])/patch.rect[3]*127);
                var c=colour.GetPixel(x,z);var n=normal.GetPixel(x,z);
                Assert.That(c.r,Is.EqualTo(.2f).Within(.01f));Assert.That(c.g,Is.EqualTo(.45f).Within(.01f));
                Assert.That(c.b,Is.EqualTo(.7f).Within(.01f));Assert.That(c.a*3,Is.EqualTo(.2f).Within(.04f));
                Assert.That(Mathf.Lerp(patch.heights[0],patch.heights[1],n.a),Is.EqualTo(.28f).Within(.015f));
                Assert.That(Vector3.Dot(new Vector3(n.r*2-1,n.g*2-1,n.b*2-1).normalized,mesh.normals[0]),Is.GreaterThan(.999f));
                Assert.That(normal.GetPixel(0,0),Is.EqualTo(Color.clear));
            }
            finally{Object.DestroyImmediate(mesh);if(colour!=null)Object.DestroyImmediate(colour);if(normal!=null)Object.DestroyImmediate(normal);}
        }
        [Test] public void ProxyHasTwoTrianglesAndOwnsItsTextureLifetime()
        {
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false,true);string png=Convert.ToBase64String(texture.EncodeToPNG());Object.DestroyImmediate(texture);
            var patch=new MeadowHeightFieldPatch{name="test",rect=new[]{0f,0,8,8},heights=new[]{0f,1},resolution=64,albedoPng=png,normalHeightPng=png};
            var root=new GameObject("Height-field lifetime test");Mesh mesh=null;Texture sample=null;
            try
            {
                var view=root.AddComponent<MeadowHeightFieldView>();
                view.Configure(new[]{patch},new CampArtDefinition{ambientColor=0x334422},_=>{});
                mesh=root.GetComponentInChildren<MeshFilter>().sharedMesh;sample=root.GetComponentInChildren<Renderer>().sharedMaterial.GetTexture("_FieldAlbedo");
                Assert.AreEqual(2,WorldOpsGeometryCensus.Triangles(mesh));Assert.AreEqual(64*64*8,view.TextureBytes);
            }
            finally{Object.DestroyImmediate(root);}
            Assert.IsTrue(mesh==null);Assert.IsTrue(sample==null);
        }
        [Test] public void BothSurfaceShadersHaveNoCompileErrors()
        {
            WorldOpsMeadowHeightFields.ValidateRenderingShader();
            foreach(var shader in new[]{Resources.Load<Shader>("QuietCamp/CampArtLit"),Shader.Find("QuietCamp/MeadowHeightField"),Shader.Find("Hidden/QuietCamp/WorldOpsMeadowBake")})
            {
                Assert.IsNotNull(shader);Assert.IsTrue(shader.isSupported,shader.name);
                Assert.IsEmpty(ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray(),shader.name);
            }
        }
        static Texture2D Decode(string png)
        {var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,true);texture.LoadImage(Convert.FromBase64String(png));return texture;}
    }
}
