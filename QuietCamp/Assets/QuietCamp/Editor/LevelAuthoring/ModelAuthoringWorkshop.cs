using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityMeshSimplifier;
using Kruty1918.MeshStyle;
using Kruty1918.MeshStyle.Unity;
using EzySlice;
using System.Threading;
using System.Threading.Tasks;
using Kruty1918.ScenePlacement.Unity;

namespace QuietCamp.Editor.LevelAuthoring
{
    [Serializable] public sealed class ModelAuthoringRecipe
    {
        public string sourceId, operation, label, category, backend;
        public float size = 1, amount, density = 1;
        public int seed;
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)] public SurfaceWeatheringSettings weathering;
    }
    [Serializable] public sealed class AuthoredModelVariant
    {
        public CampArtDefinition.Model model;
        public ModelAuthoringRecipe recipe;
        public AuthoringPropDescriptor prop;
        public bool smallPlant;
    }
    public static class ModelAuthoringWorkshop
    {
        public const string CatalogPath = "Assets/QuietCamp/Authoring/ModelVariants/catalog.json";
        sealed class Catalog { public int version = 1; public AuthoredModelVariant[] entries = Array.Empty<AuthoredModelVariant>(); }
        static Catalog _catalog;
        public static bool WritingCatalog { get; private set; }
        public static AuthoredModelVariant[] Entries => (_catalog ?? (_catalog = File.Exists(CatalogPath)
            ? JsonConvert.DeserializeObject<Catalog>(File.ReadAllText(CatalogPath)) : new Catalog())).entries;
        public static void Reset() => _catalog = null;
        public static AuthoredModelVariant Find(string id) => Entries.FirstOrDefault(v => v.model.id == id);
        public sealed class DetailJob : IDisposable
        {
            readonly MeshSimplifier _fast;
            readonly ModelAuthoringRecipe _recipe;
            readonly CampArtDefinition.Model _source;
            readonly CancellationTokenSource _cancel=new CancellationTokenSource();
            readonly Task<StyleResult> _protected;
            readonly Task _fastTask;
            public bool IsDone => _fastTask?.IsCompleted??_protected.IsCompleted;
            public DetailJob(CampArtDefinition.Model source,ModelAuthoringRecipe recipe)
            {
                _source=source;_recipe=LevelAuthoringDocument.Copy(recipe);_recipe.sourceId=source.id;
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32};LevelAuthoringPalette.PopulateMesh(mesh,source);
                try
                {
                    if(recipe.backend=="fast")
                    {
                        _fast=new MeshSimplifier(mesh);var options=SimplificationOptions.Default;options.PreserveBorderEdges=true;options.PreserveUVSeamEdges=true;options.PreserveUVFoldoverEdges=true;_fast.SimplificationOptions=options;
                        _fastTask=Task.Run(()=>{_cancel.Token.ThrowIfCancellationRequested();_fast.SimplifyMesh(_recipe.density);_cancel.Token.ThrowIfCancellationRequested();});
                    }
                    else
                    {
                        var data=UnityBridge.Read(mesh);var profile=DetailProfile(source,recipe);
                        _protected=Task.Run(()=>MeshHarmonizer.Harmonize(data,profile,1,_cancel.Token));
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(mesh);}
            }
            public AuthoredModelVariant Finish()
            {
                _cancel.Token.ThrowIfCancellationRequested();Mesh mesh;
                if(_fastTask!=null){_fastTask.GetAwaiter().GetResult();mesh=_fast.ToMesh();}
                else mesh=MeshStyleAdapter.Create(_protected.GetAwaiter().GetResult());
                try{var model=ReadMesh(mesh,_source.sourceModelId??_source.id);model.shading=_source.shading;return Register(model,_recipe,false);}
                finally{UnityEngine.Object.DestroyImmediate(mesh);}
            }
            // Cancelling the fast backend discards its bounded result; no worker touches scene objects.
            public void Dispose(){_cancel.Cancel();}
        }
        static StyleProfile DetailProfile(CampArtDefinition.Model source,ModelAuthoringRecipe r) => new StyleProfile{referenceTriangles=Mathf.Max(12,Mathf.RoundToInt(source.colors.Length/3*r.density)),referenceArea=1,minimumTriangles=12,maximumInputTriangles=20000,maximumTriangles=20000,maximumCollapses=500,maximumCandidateTests=150000,maximumWorldError=.025f,normals=NormalMode.Preserve};

        public static CampArtDefinition.Model ReadMesh(Mesh mesh, string sourceId)
        {
            var positions = mesh.vertices; var normals = mesh.normals; var colors = mesh.colors;
            var uv = mesh.uv; var tangents = mesh.tangents; var triangles = mesh.triangles;
            if (normals.Length != positions.Length) { mesh.RecalculateNormals(); normals = mesh.normals; }
            var result = new CampArtDefinition.Model { sourceModelId = sourceId, shading = "solid",
                vertices = new float[triangles.Length*3], normals = new float[triangles.Length*3], colors = new int[triangles.Length],
                uv = uv.Length == positions.Length ? new float[triangles.Length*2] : null,
                tangents = tangents.Length == positions.Length ? new float[triangles.Length*4] : null,
                weatherExposure=colors.Length==positions.Length&&colors.Any(c=>c.a<.999f)?new float[triangles.Length]:null };
            for (int k=0;k<triangles.Length;k++)
            {
                int i=triangles[k]; var p=positions[i]; var n=normals[i];
                result.vertices[k*3]=p.x;result.vertices[k*3+1]=p.y;result.vertices[k*3+2]=p.z;
                result.normals[k*3]=n.x;result.normals[k*3+1]=n.y;result.normals[k*3+2]=n.z;
                var c=colors.Length==positions.Length?colors[i]:Color.gray;
                if (QualitySettings.activeColorSpace==ColorSpace.Linear)c=c.gamma;
                result.colors[k]=Pack(c);
                if(result.weatherExposure!=null)result.weatherExposure[k]=Mathf.Clamp01(c.a);
                if(result.uv!=null){result.uv[k*2]=uv[i].x;result.uv[k*2+1]=uv[i].y;}
                if(result.tangents!=null)for(int j=0;j<4;j++)result.tangents[k*4+j]=tangents[i][j];
            }
            return result;
        }
        static int Pack(Color c) => Mathf.RoundToInt(Mathf.Clamp01(c.r)*255)<<16 | Mathf.RoundToInt(Mathf.Clamp01(c.g)*255)<<8 | Mathf.RoundToInt(Mathf.Clamp01(c.b)*255);

        public static AuthoredModelVariant Create(CampArtDefinition.Model original, ModelAuthoringRecipe recipe, bool persist = true)
        {
            if(original==null || recipe==null || recipe.size<=0 || recipe.density<=0 || recipe.density>1)throw new ArgumentException("Некоректні параметри моделі.");
            var model=LevelAuthoringDocument.Copy(original); recipe=LevelAuthoringDocument.Copy(recipe); recipe.sourceId=original.id;
            model.sourceModelId=original.sourceModelId??original.id;
            if(recipe.operation=="scale")for(int i=0;i<model.vertices.Length;i++)model.vertices[i]*=recipe.size;
            else if(recipe.operation=="age")Age(model,recipe);
            else if(recipe.operation=="simplify")model=Simplify(model,recipe);
            else if(recipe.operation!="external")throw new ArgumentException("Невідома операція.");
            return Register(model,recipe,persist);
        }
        public static AuthoredModelVariant Register(CampArtDefinition.Model model, ModelAuthoringRecipe recipe, bool persist = true)
        {
            string hash;
            using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes((JsonConvert.SerializeObject(new{model.vertices,model.normals,model.colors,model.uv,model.tangents,recipe})+(model.weatherExposure==null?"":JsonConvert.SerializeObject(model.weatherExposure))+(model.seasonalGrowth==null?"":JsonConvert.SerializeObject(model.seasonalGrowth)))))).Replace("-","").ToLowerInvariant().Substring(0,14);
            model.id="qc_variant_"+hash;
            model.authoringRecipe=JsonConvert.SerializeObject(recipe);
            var variant=new AuthoredModelVariant{model=model,recipe=recipe};
            var parent=Find(recipe.sourceId); var originalProp=AuthoringPropCatalog.FindBase(recipe.sourceId)??parent?.prop;
            if(originalProp!=null)
            {
                variant.prop=LevelAuthoringDocument.Copy(originalProp);variant.prop.id=model.id;variant.prop.label=recipe.label;
                var bounds=AuthoringPropCatalog.ModelBounds(model);variant.prop.boundsMin=new[]{bounds.min.x,bounds.min.y,bounds.min.z};variant.prop.boundsMax=new[]{bounds.max.x,bounds.max.y,bounds.max.z};
                variant.prop.triangles=model.colors.Length/3;variant.prop.recommendedScale=1;
            }
            if(persist && Find(model.id)==null)
            {
                var list=Entries.Concat(new[]{variant}).ToArray();Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
                File.WriteAllText(CatalogPath,JsonConvert.SerializeObject(new Catalog{entries=list},Formatting.None),new UTF8Encoding(false));
                _catalog=new Catalog{entries=list};
                WritingCatalog=true;
                try { AssetDatabase.ImportAsset(CatalogPath); LevelAuthoringPalette.AddVariant(variant); }
                finally { WritingCatalog=false; }
            }
            return variant;
        }
        static void Age(CampArtDefinition.Model model,ModelAuthoringRecipe r)
        {
            if(r.weathering!=null){SurfaceWeathering.Apply(model,r);return;}
            var b=AuthoringPropCatalog.ModelBounds(model);float h=Mathf.Max(.01f,b.size.y);
            var target= r.backend=="autumn"?new Color(.65f,.47f,.19f):r.backend=="moss"?new Color(.28f,.36f,.16f):r.backend=="rust"?new Color(.43f,.27f,.16f):r.backend=="fade"?new Color(.59f,.55f,.43f):new Color(.29f,.25f,.19f);
            for(int i=0;i<model.colors.Length;i++)
            {
                float x=model.vertices[i*3],y=model.vertices[i*3+1],z=model.vertices[i*3+2];
                float noise=Mathf.PerlinNoise(x*.7f+r.seed*.31f,z*.7f+r.seed*.17f);
                float lower=1-Mathf.Clamp01((y-b.min.y)/h);
                float mask=r.backend=="fade"?.5f+.5f*noise:r.backend=="rust"?Mathf.SmoothStep(0,1,(noise-.42f)*3):lower*lower*(.3f+.7f*noise);
                if(r.backend=="autumn") {var old=CampArtDefinition.Color(model.colors[i]);mask=old.g>old.r*1.04f&&old.g>old.b*1.1f?.8f+.2f*noise:0;}
                model.colors[i]=Pack(Color.Lerp(CampArtDefinition.Color(model.colors[i]),target,Mathf.Clamp01(r.amount)*mask));
            }
        }
        static CampArtDefinition.Model Simplify(CampArtDefinition.Model source,ModelAuthoringRecipe r)
        {
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32}; LevelAuthoringPalette.PopulateMesh(mesh,source); Mesh output=null;
            try
            {
                if(r.backend=="fast")
                {
                    var s=new MeshSimplifier(mesh);var options=SimplificationOptions.Default;options.PreserveBorderEdges=true;options.PreserveUVSeamEdges=true;options.PreserveUVFoldoverEdges=true;s.SimplificationOptions=options;
                    s.SimplifyMesh(r.density);output=s.ToMesh();
                }
                else
                {
                    var p=DetailProfile(source,r);
                    output=MeshStyleAdapter.Create(MeshStyleAdapter.Harmonize(mesh,p,1));
                }
                var result=ReadMesh(output,source.sourceModelId??source.id);result.shading=source.shading;return result;
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh);if(output!=null)UnityEngine.Object.DestroyImmediate(output); }
        }
        public static bool IsConvex(CampArtDefinition.Model model)
        {
            if(model.colors.Length/3>2500)return false;
            var points=Enumerable.Range(0,model.colors.Length).Select(i=>new Vector3(model.vertices[i*3],model.vertices[i*3+1],model.vertices[i*3+2])).Distinct().ToArray();
            for(int k=0;k<model.vertices.Length;k+=9)
            {
                var a=new Vector3(model.vertices[k],model.vertices[k+1],model.vertices[k+2]);var b=new Vector3(model.vertices[k+3],model.vertices[k+4],model.vertices[k+5]);var c=new Vector3(model.vertices[k+6],model.vertices[k+7],model.vertices[k+8]);
                var n=Vector3.Cross(b-a,c-a).normalized;bool positive=false,negative=false;
                foreach(var p in points){float d=Vector3.Dot(n,p-a);positive|=d>.0001f;negative|=d<-.0001f;if(positive&&negative)return false;}
            }
            return points.Length>=4;
        }
        public static AuthoredModelVariant[] Slice(CampArtDefinition.Model source,Vector3 normal,float offset,string label,bool persist=true)
        {
            if(!IsConvex(source))throw new InvalidOperationException("Площиною можна різати лише опуклі моделі. Використайте ProBuilder для цієї геометрії.");
            var go=new GameObject("Temporary authoring cut"){hideFlags=HideFlags.HideAndDontSave};var mesh=new Mesh();LevelAuthoringPalette.PopulateMesh(mesh,source);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>();
            try
            {
                var hull=go.Slice(normal.normalized*offset,normal.normalized);
                if(hull==null)throw new InvalidOperationException("Площина не перетинає модель.");
                var result=new List<AuthoredModelVariant>();
                foreach(var part in new[]{hull.upperHull,hull.lowerHull})
                {
                    var positions=part.vertices;var colors=new Color[positions.Length];
                    // Recover colour at cut edges from the nearest original triangle, using barycentric interpolation.
                    for(int i=0;i<positions.Length;i++)
                    {
                        float distance=float.MaxValue;Color best=Color.gray;
                        for(int k=0;k<source.vertices.Length;k+=9)
                        {
                            Vector3 P(int j)=>new Vector3(source.vertices[j],source.vertices[j+1],source.vertices[j+2]);
                            var a=P(k);var b=P(k+3);var c=P(k+6);var v0=b-a;var v1=c-a;var v2=positions[i]-a;
                            float d00=Vector3.Dot(v0,v0),d01=Vector3.Dot(v0,v1),d11=Vector3.Dot(v1,v1),den=d00*d11-d01*d01;if(Mathf.Abs(den)<1e-12f)continue;
                            float u=(d11*Vector3.Dot(v2,v0)-d01*Vector3.Dot(v2,v1))/den,v=(d00*Vector3.Dot(v2,v1)-d01*Vector3.Dot(v2,v0))/den;
                            u=Mathf.Clamp01(u);v=Mathf.Clamp(v,0,1-u);var q=a+v0*u+v1*v;float error=(q-positions[i]).sqrMagnitude;
                            if(error<distance){distance=error;best=CampArtDefinition.Color(source.colors[k/3])*(1-u-v)+CampArtDefinition.Color(source.colors[k/3+1])*u+CampArtDefinition.Color(source.colors[k/3+2])*v;}
                        }
                        colors[i]=QualitySettings.activeColorSpace==ColorSpace.Linear?best.linear:best;
                    }
                    part.colors=colors;var model=ReadMesh(part,source.sourceModelId??source.id);model.shading=source.shading;
                    result.Add(Register(model,new ModelAuthoringRecipe{sourceId=source.id,operation="external",backend="ezyslice",label=label+" "+(result.Count+1),category="Декор"},persist));UnityEngine.Object.DestroyImmediate(part);
                }
                return result.ToArray();
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
