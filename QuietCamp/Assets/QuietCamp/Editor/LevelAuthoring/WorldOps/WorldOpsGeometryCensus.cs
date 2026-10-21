using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Presentation.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Editor.LevelAuthoring.WorldOps
{
    /// <summary>Read-only scene topology. Counts instances, including disabled ones;
    /// never substitutes unique mesh storage or render-pass counters for a scene budget.</summary>
    public static class WorldOpsGeometryCensus
    {
        [Serializable] public sealed class Entry
        {
            public string path,mesh,category,rendererType;
            public long triangles;
            public bool active,shadowCaster,frustumCandidate;
        }
        [Serializable] public sealed class Snapshot
        {
            public string scope="Indexed MeshRenderer/SkinnedMeshRenderer instances in loaded non-preview scenes, including inactive/disabled objects. UI separate; dynamic particle/trail/terrain geometry not included in mesh totals. No pass multiplication.";
            public long sceneMeshTriangles,activeMeshTriangles,uniqueMeshTriangles,frustumCandidateTriangles,activeShadowCasterTriangles,uiTriangles;
            public int indexedMeshInstances,liveParticles;
            public List<Entry> entries=new List<Entry>();
            public List<string> uncountedRenderers=new List<string>();
        }
        public static long Triangles(Mesh mesh)
        {
            if(mesh==null)return 0;
            long count=0;
            for(int i=0;i<mesh.subMeshCount;i++)
            {
                var topology=mesh.GetTopology(i);
                if(topology==MeshTopology.Triangles)count+=mesh.GetIndexCount(i)/3;
                else if(topology==MeshTopology.Quads)count+=mesh.GetIndexCount(i)/4*2;
            }
            return count;
        }
        public static Snapshot Capture(Camera camera,Renderer[] renderers=null)
        {
            var result=new Snapshot();var meshes=new HashSet<Mesh>();
            var planes=camera!=null?GeometryUtility.CalculateFrustumPlanes(camera):null;
            foreach(var renderer in renderers??UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(renderer==null||!InScene(renderer.gameObject))continue;
                Mesh mesh=null;
                if(renderer is SkinnedMeshRenderer skin)mesh=skin.sharedMesh;
                else if(renderer is MeshRenderer)mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;
                else
                {
                    result.uncountedRenderers.Add(Path(renderer.transform)+" ["+renderer.GetType().Name+"]");
                    if(renderer is ParticleSystemRenderer)result.liveParticles+=renderer.GetComponent<ParticleSystem>()?.particleCount??0;
                    continue;
                }
                if(mesh==null)continue;
                long triangles=Triangles(mesh);bool active=renderer.enabled&&renderer.gameObject.activeInHierarchy;
                bool shadow=active&&renderer.shadowCastingMode!=ShadowCastingMode.Off;
                bool candidate=active&&renderer.shadowCastingMode!=ShadowCastingMode.ShadowsOnly&&camera!=null
                    &&(camera.cullingMask&(1<<renderer.gameObject.layer))!=0&&GeometryUtility.TestPlanesAABB(planes,renderer.bounds);
                result.sceneMeshTriangles+=triangles;result.indexedMeshInstances++;
                if(meshes.Add(mesh))result.uniqueMeshTriangles+=triangles;
                if(active)result.activeMeshTriangles+=triangles;
                if(shadow)result.activeShadowCasterTriangles+=triangles;
                if(candidate)result.frustumCandidateTriangles+=triangles;
                result.entries.Add(new Entry{path=Path(renderer.transform),mesh=mesh.name,category=Category(renderer),rendererType=renderer.GetType().Name,
                    triangles=triangles,active=active,shadowCaster=shadow,frustumCandidate=candidate});
            }
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<CanvasRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(InScene(renderer.gameObject))result.uiTriangles+=Triangles(renderer.GetMesh());
            result.entries=result.entries.OrderByDescending(e=>e.triangles).ThenBy(e=>e.path,StringComparer.Ordinal).ToList();
            return result;
        }
        static bool InScene(GameObject go)=>go.scene.IsValid()&&go.scene.isLoaded&&!EditorSceneManager.IsPreviewScene(go.scene);
        static string Category(Renderer renderer)
        {
            if(renderer.GetComponentInParent<VisibleForestFloor>()!=null)return "forest-floor";
            if(renderer.GetComponentInParent<CampArtView>()!=null)return "authored-world";
            if(renderer.GetComponentInParent<TentCloth>()!=null)return "tent";
            if(renderer.GetComponentInParent<EnvironmentComposer>()!=null)return "environment";
            return "other";
        }
        static string Path(Transform transform)
        {
            var parts=new List<string>();for(var t=transform;t!=null;t=t.parent)parts.Add(t.name);
            parts.Reverse();return transform.gameObject.scene.name+"/"+string.Join("/",parts);
        }
    }
}
