using System.Collections.Generic;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>One cached refinement of fabric triangles; rigid submeshes and colliders retain authored geometry.</summary>
    internal sealed class FabricGeometry
    {
        static readonly Dictionary<(Mesh, ulong), FabricGeometry> Cache = new Dictionary<(Mesh, ulong), FabricGeometry>();
        readonly (Mesh, ulong) _key;
        int _owners;
        public Mesh Mesh { get; private set; }
        public Bounds FabricBounds { get; }
        public static int CachedCount => Cache.Count;
        FabricGeometry((Mesh, ulong) key, Mesh mesh, Bounds fabricBounds) { _key = key; Mesh = mesh; FabricBounds = fabricBounds; }
        public static FabricGeometry Acquire(Mesh source, ulong fabricSlots)
        {
            if (!source.isReadable || fabricSlots == 0 || source.subMeshCount > 64) return null;
            var key = (source, fabricSlots);
            if (!Cache.TryGetValue(key, out var geometry))
            {
                int triangles = 0;
                for (int slot = 0; slot < source.subMeshCount; slot++)
                {
                    if (source.GetTopology(slot) != MeshTopology.Triangles) return null;
                    if ((fabricSlots & (1UL << slot)) != 0) triangles += (int)source.GetIndexCount(slot) / 3;
                }
                // A fixed, small startup budget. Dense authored meshes already have enough movable vertices.
                if (triangles == 0 || triangles > 1024) return null;
                var vertices = new List<Vector3>(source.vertices);
                var normals = new List<Vector3>(source.normals);
                var uv = new List<Vector2>(source.uv);
                var tangents = new List<Vector4>(source.tangents);
                var colors = new List<Color>(source.colors);
                bool hasNormals = normals.Count == vertices.Count, hasUv = uv.Count == vertices.Count;
                bool hasTangents = tangents.Count == vertices.Count, hasColors = colors.Count == vertices.Count;
                var edges = new Dictionary<long, int>();
                var fabricBounds = new Bounds(); bool hasFabricBounds = false;
                int Midpoint(int a, int b)
                {
                    long edge = ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
                    if (edges.TryGetValue(edge, out int index)) return index;
                    index = vertices.Count; vertices.Add((vertices[a] + vertices[b]) * .5f);
                    if (hasNormals) normals.Add((normals[a] + normals[b]).normalized);
                    if (hasUv) uv.Add((uv[a] + uv[b]) * .5f);
                    if (hasTangents) tangents.Add((tangents[a] + tangents[b]) * .5f);
                    if (hasColors) colors.Add(Color.Lerp(colors[a], colors[b], .5f));
                    edges.Add(edge, index); return index;
                }
                var parts = new int[source.subMeshCount][];
                for (int slot = 0; slot < parts.Length; slot++)
                {
                    var original = source.GetTriangles(slot);
                    if ((fabricSlots & (1UL << slot)) == 0) { parts[slot] = original; continue; }
                    foreach (int index in original)
                    {
                        if (!hasFabricBounds) { fabricBounds = new Bounds(vertices[index], Vector3.zero); hasFabricBounds = true; }
                        else fabricBounds.Encapsulate(vertices[index]);
                    }
                    var refined = new int[original.Length * 4]; int next = 0;
                    void Triangle(int a, int b, int c) { refined[next++] = a; refined[next++] = b; refined[next++] = c; }
                    for (int i = 0; i < original.Length; i += 3)
                    {
                        int a = original[i], b = original[i+1], c = original[i+2];
                        int ab = Midpoint(a,b), bc = Midpoint(b,c), ca = Midpoint(c,a);
                        Triangle(a,ab,ca); Triangle(ab,b,bc); Triangle(ca,bc,c); Triangle(ab,bc,ca);
                    }
                    parts[slot] = refined;
                }
                var mesh = new Mesh { name = source.name + " (wind fabric)", indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : source.indexFormat };
                mesh.SetVertices(vertices);
                if (hasNormals) mesh.SetNormals(normals);
                if (hasUv) mesh.SetUVs(0, uv);
                if (hasTangents) mesh.SetTangents(tangents);
                if (hasColors) mesh.SetColors(colors);
                mesh.subMeshCount = parts.Length;
                for (int slot = 0; slot < parts.Length; slot++) mesh.SetTriangles(parts[slot], slot, false);
                if (!hasNormals) mesh.RecalculateNormals();
                mesh.bounds = source.bounds;
                geometry = new FabricGeometry(key, mesh, fabricBounds); Cache.Add(key, geometry);
            }
            geometry._owners++; return geometry;
        }
        public void Release()
        {
            if (--_owners != 0) return;
            Cache.Remove(_key);
            if (UnityEngine.Application.isPlaying) Object.Destroy(Mesh); else Object.DestroyImmediate(Mesh);
        }
    }
}
