using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using Kruty1918.ScenePlacement.Unity;

namespace Kruty1918.ScenePlacement.EditorTools
{
    [Serializable] public sealed class Reservation{public string id;public Box box;public Pose pose;}
    /// <summary>Explicit inputs and immutable receipt-gated publication. Source importers are never changed.</summary>
    public static class BatchFiles
    {
        public static PlacementContext Context(SurfaceRecipe surface,Reservation[] reservations)
        {var context=new PlacementContext(surface.Create());foreach(var r in reservations)context.Reserve(r.id,new OrientedBox(r.box,r.pose));return context;}
        [Serializable] public sealed class NamedMesh{public string id;public MeshData data;}
        [Serializable] public sealed class Bundle{public string id,metadataJson;public NamedMesh[] meshes;public PlacedInstance[] instances;public Diagnostic[] diagnostics;}
        [Serializable] public sealed class Dependency{public string path,sha256;}
        [Serializable] public sealed class Receipt
        {
            public string id,recipeHash,resultHash,toolVersion;
            public Dependency[] dependencies;public string[] meshAssets;
            public int instances,triangles;public bool playerBuild;
        }
        public static string Argument(string key)
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);if(i<0||i+1==args.Length)throw new ArgumentException("Required command argument "+key);return args[i+1];}
        public static T Request<T>()
        {var value=JsonUtility.FromJson<T>(File.ReadAllText(Argument("--authoring-request")));if(value==null)throw new ArgumentException("Empty authoring request");return value;}
        public static string Hash(string text){using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
        public static void Save(string id,MeshData mesh,IEnumerable<Diagnostic> diagnostics=null,string metadataJson=null,Dependency[] inputs=null)
        {SaveBundle(id,new[]{new NamedMesh{id=id,data=mesh}},Array.Empty<PlacedInstance>(),diagnostics,metadataJson,inputs);}
        public static void SaveInstances(string id,PlacedInstance[] instances,IEnumerable<Diagnostic> diagnostics=null,string metadataJson=null)
        {SaveBundle(id,Array.Empty<NamedMesh>(),instances,diagnostics,metadataJson);}
        public static void SaveBundle(string id,NamedMesh[] meshes,PlacedInstance[] instances,IEnumerable<Diagnostic> diagnostics=null,string metadataJson=null,Dependency[] inputs=null)
        {
            var issues=(diagnostics??Array.Empty<Diagnostic>()).ToArray();
            if(issues.Any(d=>d.error))throw new InvalidOperationException(string.Join("; ",issues.Select(d=>d.ToString())));
            foreach(var mesh in meshes)mesh.data.Validate();
            string root=Argument("--authoring-output").Replace('\\','/').TrimEnd('/');
            if(!root.StartsWith("Assets/",StringComparison.Ordinal)||root.Split('/').Any(p=>p==".."||p=="."||p.Length==0))throw new ArgumentException("Output must be a project Assets subfolder");
            var bundle=new Bundle{id=id,meshes=meshes,instances=instances,diagnostics=issues,metadataJson=metadataJson};
            string resultJson=JsonUtility.ToJson(bundle,true),recipe=File.ReadAllText(Argument("--authoring-request"));
            var dependencies=Sources().Concat(inputs??Array.Empty<Dependency>()).OrderBy(d=>d.path,StringComparer.Ordinal).ToArray();string version=string.Join(";",dependencies.Where(d=>d.path.EndsWith("/package.json",StringComparison.Ordinal)).Select(d=>d.path+"@"+d.sha256));
            string hash=Hash(Hash(recipe)+Hash(resultJson)+JsonUtility.ToJson(new DependencyList{items=dependencies}));
            string finalFolder=root+"/"+hash;
            if(File.Exists(finalFolder+"/receipt.json"))
            {
                var prior=JsonUtility.FromJson<Receipt>(File.ReadAllText(finalFolder+"/receipt.json"));
                if(prior.resultHash!=Hash(resultJson)||Hash(File.ReadAllText(finalFolder+"/result.json"))!=prior.resultHash)throw new InvalidOperationException("Immutable bake was modified: "+finalFolder);
                Debug.Log("[Authoring] Reused "+finalFolder);return;
            }
            if(Directory.Exists(finalFolder))throw new InvalidOperationException("Incomplete output requires explicit cleanup: "+finalFolder);
            string stage=root+"/__AuthoringPending-"+Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(stage);AssetDatabase.Refresh();var paths=new List<string>();
                for(int i=0;i<meshes.Length;i++)
                {
                    string filename="mesh-"+i.ToString("D4")+".asset";
                    AssetDatabase.CreateAsset(UnityBridge.CreateMesh(meshes[i].data,meshes[i].id),stage+"/"+filename);
                    paths.Add(finalFolder+"/"+filename);
                }
                File.WriteAllText(stage+"/result.json",resultJson);File.WriteAllText(stage+"/recipe.json",recipe);
                var receipt=new Receipt{id=id,recipeHash=Hash(recipe),resultHash=Hash(resultJson),toolVersion=version,dependencies=dependencies,meshAssets=paths.ToArray(),instances=instances.Length,triangles=meshes.Sum(m=>m.data.TriangleCount),playerBuild=false};
                File.WriteAllText(stage+"/receipt.json",JsonUtility.ToJson(receipt,true)+"\n");
                AssetDatabase.SaveAssets();AssetDatabase.Refresh();
                string error=AssetDatabase.MoveAsset(stage,finalFolder);if(error.Length>0)throw new IOException(error);
                Debug.Log("[Authoring] Published "+finalFolder);
            }
            catch{if(Directory.Exists(stage)){if(!AssetDatabase.DeleteAsset(stage))Directory.Delete(stage,true);}throw;}
        }
        [Serializable] sealed class DependencyList{public Dependency[] items;}
        static Dependency[] Sources()
        {
            var list=new List<Dependency>();var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name.StartsWith("Kruty1918.",StringComparison.Ordinal)).OrderBy(a=>a.GetName().Name,StringComparer.Ordinal))
            {
                var package=UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);if(package==null||!seen.Add(package.resolvedPath))continue;
                foreach(string file in Directory.GetFiles(package.resolvedPath,"*",SearchOption.AllDirectories).Where(f=>!f.Contains("Tests~")&&!f.Contains("Samples~")&&(f.EndsWith(".cs",StringComparison.Ordinal)||f.EndsWith(".asmdef",StringComparison.Ordinal)||Path.GetFileName(f)=="package.json")).OrderBy(f=>f,StringComparer.Ordinal))
                    list.Add(new Dependency{path=package.name+"/"+file.Substring(package.resolvedPath.Length+1).Replace('\\','/'),sha256=Hash(File.ReadAllText(file))});
            }
            return list.OrderBy(d=>d.path,StringComparer.Ordinal).ToArray();
        }
    }
    public static class PlacementBatch
    {
        [Serializable] public sealed class Recipe{public PlacementRequest[] requests=Array.Empty<PlacementRequest>();public SurfaceRecipe surface=new SurfaceRecipe();}
        public static void Run(){var recipe=BatchFiles.Request<Recipe>();var result=PlacementSolver.Ensemble(recipe.requests,new PlacementContext(recipe.surface.Create()));BatchFiles.SaveInstances("placement",result.instances.ToArray(),result.diagnostics);}
    }
}
