using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using QuietCamp.Editor.LevelAuthoring;
using QuietCamp.Infrastructure;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class FreeWorldAssetsTests
    {
        [Test] public void FortyMetricModelsAreDecorativeAndMatchTheirCatalog()
        {
            var catalog = JObject.Parse(File.ReadAllText(LevelAuthoringPalette.FreeCatalogPath));
            var source = JObject.Parse(File.ReadAllText(LevelAuthoringPalette.FreeModelPath));
            Assert.AreEqual(40, catalog["entries"].Count());
            Assert.AreEqual(40, source["models"].Count());
            Assert.AreEqual(40, catalog["entries"].Select(e => (string)e["id"]).Distinct().Count());
            foreach (var item in catalog["entries"])
            {
                var entry = LevelAuthoringPalette.Get((string)item["id"]);
                Assert.AreEqual("decor", (string)item["role"]);
                Assert.AreEqual((string)item["label"], entry.label);
                Assert.AreEqual((string)item["category"], entry.category);
                Assert.IsNotNull(entry.prefab);
                var mesh = entry.prefab.GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual(mesh.vertexCount, mesh.colors.Length);
                Assert.AreEqual(mesh.vertexCount, mesh.normals.Length);
                Assert.Greater(mesh.triangles.Length, 0);
                var measured = (string)item["sizingAxis"] == "height" ? mesh.bounds.size.y
                    : Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z);
                Assert.That(measured, Is.EqualTo((float)item["targetMetres"]).Within(.0001f), entry.id);
                Assert.That(mesh.bounds.min.y, Is.EqualTo(0).Within(.0001f), entry.id);
                Assert.IsFalse(entry.model.vertices.Any(v => float.IsNaN(v) || float.IsInfinity(v)));
            }
        }

        [Test] public void FreeModelsRoundTripWithoutChangingThePuzzle()
        {
            var d = LevelAuthoringDocument.Create(true, "QC007");
            var puzzle = JsonConvert.SerializeObject(d.level);
            var added = LevelAuthoringPalette.Entries.Where(e => e.id.StartsWith("qc_kenney_", StringComparison.Ordinal)).ToArray();
            d.art.models = d.art.models.Concat(added.Select(e => e.model)).GroupBy(m => m.id).Select(g => g.First()).ToArray();
            d.art.instances = added.Select((e,i) => new CampArtDefinition.Instance
            { id="free/"+i, modelId=e.id, x=60+i*20, z=60, scale=1, surfaceAttached=false }).ToArray();
            d.art.cover = Array.Empty<CampArtDefinition.CoverPoint>();
            Assert.IsEmpty(d.Validate(true));
            var copy = JsonConvert.DeserializeObject<LevelAuthoringDocument>(JsonConvert.SerializeObject(d));
            Assert.AreEqual(puzzle, JsonConvert.SerializeObject(copy.level));
            Assert.AreEqual(JsonConvert.SerializeObject(d.art), JsonConvert.SerializeObject(copy.art));
        }

        [Test] public void OfficialPackagesAndEditorOnlyLibrariesAreLoaded()
        {
            var packages = PackageInfo.GetAllRegisteredPackages();
            Assert.AreEqual("6.1.3", packages.Single(p => p.name == "com.unity.probuilder").version);
            Assert.AreEqual("1.2.1", packages.Single(p => p.name == "com.unity.polybrush").version);
            Assert.IsNotNull(UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies().Select(a => a.GetType("UnityMeshSimplifier.MeshSimplifier")).FirstOrDefault(t => t != null));
            Assert.IsNotNull(UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies().Select(a => a.GetType("EzySlice.Slicer")).FirstOrDefault(t => t != null));
            var players = CompilationPipeline.GetAssemblies(AssembliesType.Player).Select(a => a.name).ToArray();
            Assert.IsFalse(players.Any(n => n.Contains("UnityMeshSimplifier") || n.Contains("EzySlice")));
        }

        [Test] public void PolybrushOwnershipKeepsFullEntityIdsAndDetectsDuplicatedMeshes()
        {
            var type = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies().Select(a => a.GetType("UnityEditor.Polybrush.EditableObject")).First(t => t != null);
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var prefix = (string)type.GetField("k_MeshInstancePrefix", flags).GetValue(null);
            var getId = type.GetMethod("GetMeshId", flags);
            var matches = type.GetMethod("MeshInstanceMatchesGameObject", flags);
            var owner = new GameObject("Polybrush owner");
            var duplicate = new GameObject("Polybrush duplicate");
            var mesh = new Mesh();
            try
            {
                mesh.name = prefix + EntityId.ToULong(owner.GetEntityId());
                Assert.AreEqual(owner.GetEntityId(), getId.Invoke(null, new object[] { mesh }));
                Assert.IsTrue((bool)matches.Invoke(null, new object[] { mesh, owner }));
                Assert.IsFalse((bool)matches.Invoke(null, new object[] { mesh, duplicate }));
                mesh.name = prefix + "-12345"; // Legacy name: no ownership claim and no mutation of the shared source.
                Assert.AreEqual(EntityId.None, getId.Invoke(null, new object[] { mesh }));
                Assert.IsFalse((bool)matches.Invoke(null, new object[] { mesh, duplicate }));
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(duplicate); }
        }
    }
}
