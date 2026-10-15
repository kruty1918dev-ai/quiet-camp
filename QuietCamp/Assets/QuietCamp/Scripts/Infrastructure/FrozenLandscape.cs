using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    // Plain exported geometry. Authoring tools never participate in runtime decisions.
    [Serializable] public sealed class FrozenLandscape
    {
        public int resolution;
        public int riverEnvironmentVersion;
        public float[] riverContacts;
        public float originX, originZ, size;
        public float[] heights;
        public LandscapeMesh[] meshes;
        public float SampleHeight(float x, float z)
        {
            float u = Mathf.Clamp((x - originX) / size * (resolution - 1), 0, resolution - 1);
            float v = Mathf.Clamp((z - originZ) / size * (resolution - 1), 0, resolution - 1);
            int a = Mathf.Min((int)u, resolution - 2), b = Mathf.Min((int)v, resolution - 2);
            return Mathf.Lerp(Mathf.Lerp(heights[b * resolution + a], heights[b * resolution + a + 1], u - a),
                Mathf.Lerp(heights[(b + 1) * resolution + a], heights[(b + 1) * resolution + a + 1], u - a), v - b);
        }
        public void Validate()
        {
            if (resolution < 2 || resolution > 1025 || !Finite(size) || size <= 0 || !Finite(originX) || !Finite(originZ)
                || heights == null || heights.Length != resolution * resolution || heights.Any(v => !Finite(v))
                || meshes == null || meshes.Length == 0 || meshes.Length > 128 || riverEnvironmentVersion<0
                || riverContacts!=null&&(riverContacts.Length%3!=0||riverContacts.Length>4096||riverContacts.Any(v=>!Finite(v)))) throw new InvalidDataException("Некоректний запечений ландшафт.");
            foreach (var mesh in meshes) mesh.Validate();
        }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
    [Serializable] public sealed class LandscapeMesh
    {
        public string name;
        public bool water;
        public bool decoration;
        public float[] vertices, normals, uv, colors;
        public float[] lightmapUv, flow, tangents;
        public int[] triangles;
        public void Validate()
        {
            if (vertices == null || vertices.Length < 9 || vertices.Length % 3 != 0 || vertices.Length > 1200000
                || normals == null || normals.Length != vertices.Length || uv == null || uv.Length != vertices.Length / 3 * 2
                || colors == null || colors.Length != vertices.Length / 3 * 4 || triangles == null || triangles.Length % 3 != 0
                || triangles.Any(i => i < 0 || i >= vertices.Length / 3)
                || vertices.Any(v => !FrozenLandscape.Finite(v)) || normals.Any(v => !FrozenLandscape.Finite(v))
                || uv.Any(v => !FrozenLandscape.Finite(v)) || colors.Any(v => !FrozenLandscape.Finite(v))
                || !OptionalChannel(lightmapUv, vertices.Length / 3 * 2) || !OptionalChannel(flow, vertices.Length)
                || !OptionalChannel(tangents, vertices.Length / 3 * 4))
                throw new InvalidDataException("Некоректна геометрія ландшафту: " + name);
        }
        static bool OptionalChannel(float[] values, int length) => values == null || values.Length == 0
            || values.Length == length && values.All(FrozenLandscape.Finite);
        public Mesh CreateMesh()
        {
            Validate(); int count = vertices.Length / 3;
            var points = new Vector3[count]; var directions = new Vector3[count]; var texture = new Vector2[count]; var tint = new Color[count];
            for (int i = 0; i < count; i++)
            {
                points[i] = new Vector3(vertices[i * 3], vertices[i * 3 + 1], vertices[i * 3 + 2]);
                directions[i] = new Vector3(normals[i * 3], normals[i * 3 + 1], normals[i * 3 + 2]);
                texture[i] = new Vector2(uv[i * 2], uv[i * 2 + 1]);
                var c = new Color(colors[i * 4], colors[i * 4 + 1], colors[i * 4 + 2], colors[i * 4 + 3]);
                tint[i] = water || QualitySettings.activeColorSpace != ColorSpace.Linear ? c : c.linear;
            }
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = points; mesh.normals = directions; mesh.uv = texture; mesh.colors = tint; mesh.triangles = triangles;
            if (lightmapUv?.Length > 0) mesh.uv2 = Enumerable.Range(0, count).Select(i => new Vector2(lightmapUv[i * 2], lightmapUv[i * 2 + 1])).ToArray();
            if (flow?.Length > 0) mesh.SetUVs(2, Enumerable.Range(0, count).Select(i => new Vector3(flow[i * 3], flow[i * 3 + 1], flow[i * 3 + 2])).ToArray());
            if (tangents?.Length > 0) mesh.tangents = Enumerable.Range(0, count).Select(i => new Vector4(tangents[i * 4], tangents[i * 4 + 1], tangents[i * 4 + 2], tangents[i * 4 + 3])).ToArray();
            mesh.RecalculateBounds(); return mesh;
        }
    }
}
