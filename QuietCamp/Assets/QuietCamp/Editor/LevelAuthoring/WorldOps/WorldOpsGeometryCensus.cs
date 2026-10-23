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
            public string scope="Indexed MeshRenderer/SkinnedMeshRenderer instances in loaded non-preview scenes, including inactive/disabled objects. UI separate. Particle capacities count worst-case quads/meshes; unsupported dynamic renderers remain explicit. No pass multiplication.";
            public long sceneMeshTriangles,activeMeshTriangles,uniqueMeshTriangles,frustumCandidateTriangles,activeShadowCasterTriangles,uiTriangles;
            public int indexedMeshInstances,liveParticles;
            public long particleTriangleCapacity;
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
                    if(renderer is ParticleSystemRenderer particles)
                    {
                        var system=renderer.GetComponent<ParticleSystem>();
                        if(system==null)continue;
                        result.liveParticles+=system.particleCount;
                        long perParticle=2;
                        if(particles.renderMode==ParticleSystemRenderMode.None)perParticle=0;
                        else if(particles.renderMode==ParticleSystemRenderMode.Mesh)
                        {
                            var variants=new Mesh[particles.meshCount];particles.GetMeshes(variants);
                            perParticle=variants.Select(Triangles).DefaultIfEmpty(0).Max();
                        }
                        result.particleTriangleCapacity+=(long)system.main.maxParticles*perParticle;
                        if(system.trails.enabled)result.uncountedRenderers.Add(Path(renderer.transform)+" [particle trails]");
                    }
                    else result.uncountedRenderers.Add(Path(renderer.transform)+" ["+renderer.GetType().Name+"]");
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
            foreach(var terrain in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(InScene(terrain.gameObject))result.uncountedRenderers.Add(Path(terrain.transform)+" [Terrain tessellation]");
            result.entries=result.entries.OrderByDescending(e=>e.triangles).ThenBy(e=>e.path,StringComparer.Ordinal).ToList();
            return result;
        }
        public static List<ValidationFinding> CheckBudget(Snapshot census,int limit,string levelId)
        {
            var findings=new List<ValidationFinding>();if(limit<=0)return findings;
            long total=census.sceneMeshTriangles+census.uiTriangles+census.particleTriangleCapacity;
            if(total>limit)findings.Add(new ValidationFinding{code="scene.triangle-budget",levelId=levelId,measured=total,limit=limit,
                message="Scene mesh instances + UI + maximum particle geometry exceed the triangle budget (including inactive objects)."});
            if(census.uncountedRenderers.Count>0)findings.Add(new ValidationFinding{code="scene.unbounded-geometry",levelId=levelId,
                message="Cannot certify the full scene budget while dynamic geometry is uncounted: "+string.Join(", ",census.uncountedRenderers)});
            return findings;
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
