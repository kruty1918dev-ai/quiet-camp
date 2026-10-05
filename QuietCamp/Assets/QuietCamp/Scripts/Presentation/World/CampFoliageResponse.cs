using System.Collections.Generic;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>Camp-scale response, calibrated for silhouettes viewed from the board camera.</summary>
    public static class CampFoliageResponse
    {
        static readonly Dictionary<Mesh, float[]> CrownBases = new Dictionary<Mesh, float[]>();
        public static void Apply(Material material)
        {
            var amplitude = material.GetFloat("_SwayAmp");
            bool tree = amplitude < .01f && material.GetFloat("_SwayFreq") < .26f;
            bool trunk = tree && amplitude < .0045f;
            // Trunk and crown share the same primary bend. Only the crown has
            // secondary flex, starting above its actual attachment to the trunk.
            material.SetFloat("_TreeWind", tree ? 1 : 0);
            material.SetFloat("_SwayAmp", tree ? (trunk ? 0 : .14f)
                : amplitude < .02f ? .26f : amplitude < .04f ? .27f : .32f);
            material.SetFloat("_FlutterAmp", trunk ? 0 : tree ? .004f : .006f);
            if (tree) material.SetFloat("_SwayFreq", .20f);
            // Flowers use one phase for stems and heads so they stay connected.
            material.SetFloat("_PhaseLag", 0);
        }
        public static void BindRenderer(Renderer renderer)
        {
            var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) return;
            var bounds = mesh.bounds;
            var materials = renderer.sharedMaterials;
            for (int slot = 0; slot < materials.Length; slot++)
            {
                var material = materials[slot];
                if (material == null || material.shader.name != "QuietCamp/FoliageLit") continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, slot);
                if (block.isEmpty) renderer.GetPropertyBlock(block);
                block.SetFloat("_MeshMinY", bounds.min.y);
                block.SetFloat("_MeshTopY", bounds.max.y);
                if (material.GetFloat("_TreeWind") > .5f && mesh.isReadable && mesh.subMeshCount > 0)
                {
                    if (!CrownBases.TryGetValue(mesh, out var bases))
                    {
                        bases = new float[mesh.subMeshCount];
                        var vertices = mesh.vertices;
                        for (int sub = 0; sub < bases.Length; sub++)
                        {
                            float bottom = bounds.max.y;
                            foreach (int index in mesh.GetTriangles(sub)) bottom = Mathf.Min(bottom, vertices[index].y);
                            bases[sub] = Mathf.Clamp01((bottom - bounds.min.y) / Mathf.Max(.0001f, bounds.size.y));
                        }
                        CrownBases.Add(mesh, bases);
                    }
                    block.SetFloat("_CrownStart", bases[Mathf.Min(slot, bases.Length - 1)]);
                }
                renderer.SetPropertyBlock(block, slot);
            }
        }
        public static void ExpandBounds(Renderer renderer)
        {
            var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) return;
            var bounds = mesh.bounds;
            float worldHeight = renderer.transform.TransformVector(Vector3.up * bounds.size.y).magnitude;
            // Conservative envelope for every wind direction, including
            // narrow/nonuniformly scaled plants. Re-registration never grows it.
            var x = renderer.transform.InverseTransformVector(Vector3.right);
            var z = renderer.transform.InverseTransformVector(Vector3.forward);
            var y = renderer.transform.InverseTransformVector(Vector3.up);
            var envelope = new Vector3(Mathf.Sqrt(x.x*x.x+z.x*z.x),
                Mathf.Sqrt(x.y*x.y+z.y*z.y),Mathf.Sqrt(x.z*x.z+z.z*z.z)) * (worldHeight * .36f);
            envelope += new Vector3(Mathf.Abs(y.x),Mathf.Abs(y.y),Mathf.Abs(y.z)) * (worldHeight * .12f);
            bounds.Expand(envelope * 2);
            renderer.localBounds = bounds;
        }
    }
}
