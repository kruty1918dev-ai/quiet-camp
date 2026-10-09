using System;
using System.Collections.Generic;
using QuietCamp.Domain;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>One bounded mesh per level. Source models shared with the baked library;
    /// detail identities never enter roadmap generation. No per-prop objects or Update.</summary>
    public sealed class EnvironmentalStoryVisual : MonoBehaviour
    {
        LevelData _level;
        bool _cared;
        Material _material;
        public static void Attach(Transform parent, LevelData level)
        {
            if (level.environmentalStory == null) return;
            var issues = EnvironmentalStoryValidator.Validate(level.environmentalStory);
            if (issues.Count > 0) { Debug.LogError("[EnvironmentalStory] " + string.Join("; ", issues)); return; }
            var root = new GameObject("Environmental story details");
            root.transform.SetParent(parent, false); root.layer = BoardRenderer.DecorLayer;
            root.AddComponent<MeshFilter>();
            root.AddComponent<OwnedEnvironmentMesh>();
            var visual = root.AddComponent<EnvironmentalStoryVisual>(); visual._level = level;
            var shader = Resources.Load<Shader>("QuietCamp/FoliageLit");
            if (shader == null) { Destroy(root); return; }
            visual._material = new Material(shader) { name = "Environmental story shared detail" };
            visual._material.SetColor("_BaseColor", Color.white); visual._material.SetFloat("_VertexTint", 1);
            visual._material.SetFloat("_SnowCover", SeasonPalette.For(level).SnowCoverage);
            visual._material.SetFloat("_ClusterWind", 1);
            visual._material.SetFloat("_SwayAmp", .04f); visual._material.SetFloat("_FlutterAmp", .003f);
            var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = visual._material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            visual.Rebuild();
        }

        public void ApplyCare()
        {
            if (_cared) return;
            _cared = true; Rebuild();
        }

        void Rebuild()
        {
            var next = Compose(_level, _cared);
            var filter = GetComponent<MeshFilter>(); var previous = filter.sharedMesh;
            filter.sharedMesh = next; GetComponent<OwnedEnvironmentMesh>().Mesh = next;
            GetComponent<MeshRenderer>().enabled = next != null;
            if (previous != null) Destroy(previous);
        }

        public static Mesh Compose(LevelData level, bool cared)
        {
            if (level?.environmentalStory == null) return null;
            var models = RoadmapModelLibrary.Load();
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var colors = new List<Color>(); var roots = new List<Vector4>(); var indices = new List<int>(); var occupied = new List<Bounds>();
            foreach (var beat in level.environmentalStory.beats)
            {
                if (!EnvironmentalStoryPolicy.Applies(beat, level.id)) continue;
                foreach (var prop in beat.props)
                {
                    if (!EnvironmentalStoryPolicy.Appears(prop.appearance, cared)
                        || EnvironmentalStoryPolicy.Visibility(beat, prop, false) == "hidden") continue;
                    var view = prop.level; var model = models.Get(view.assetId);
                    if (model == null) { Debug.LogError("[EnvironmentalStory] Missing detail model: " + view.assetId); continue; }
                    var rotation = Quaternion.Euler(0, view.yaw, 0);
                    var origin = new Vector3(view.x, view.elevation, view.z);
                    var bounds = new Bounds(origin + rotation * model.Positions[0] * view.height, Vector3.zero);
                    foreach (var point in model.Positions) bounds.Encapsulate(origin + rotation * point * view.height);
                    // No automatic repositioning: authored clues stay where the designer placed them.
                    if (!EnvironmentComposer.CanScenicBounds(level, bounds) || occupied.Exists(b => b.Intersects(bounds)))
                    { Debug.LogError("[EnvironmentalStory] Unsafe/overlapping detail: " + prop.id); continue; }
                    if (vertices.Count + model.Positions.Length > 60000)
                    { Debug.LogError("[EnvironmentalStory] Detail vertex budget exceeded"); break; }
                    occupied.Add(bounds);
                    for (int i = 0; i < model.Positions.Length; i++)
                    {
                        indices.Add(vertices.Count); vertices.Add(origin + rotation * model.Positions[i] * view.height);
                        normals.Add(rotation * model.Normals[i]); colors.Add(model.Colors[i / 3]);
                        roots.Add(new Vector4(origin.x, origin.z, origin.y, view.sway ? view.height : -1));
                    }
                }
            }
            if (vertices.Count == 0) return null;
            var mesh = new Mesh { name = "Environmental story details" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetUVs(1, roots); mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds(); var padded = mesh.bounds; padded.Expand(.5f); mesh.bounds = padded; return mesh;
        }

        void OnDestroy() { if (_material != null) Destroy(_material); }
    }
}
