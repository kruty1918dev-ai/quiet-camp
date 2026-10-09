using QuietCamp.Presentation.UI.Prepared;
using RoadmapStreamingPlayModeTests = QuietCamp.Tests.Prepared.RoadmapStreamingPlayModeTests;
#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QuietCamp.Tests
{
    public sealed class EnvironmentalStoryPlayModeTests
    {
        bool _async;
        [SetUp] public void Setup()
        { _async = UnityEditor.ShaderUtil.allowAsyncCompilation; UnityEditor.ShaderUtil.allowAsyncCompilation = false; QuietCampBootstrap.EditorDisableAudio = true; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            QuietCampBootstrap.EditorDisableAudio = false; UnityEditor.ShaderUtil.allowAsyncCompilation = _async;
            var harness = Object.FindAnyObjectByType<RoadmapBenchmarkHarness>(); if (harness != null) Object.Destroy(harness.gameObject);
            yield return null;
        }
        static Mesh NativeMesh(RoadmapWorldRenderer renderer, int node)
        {
            // Read-only diagnostic: verify the real cached buffer, not just its source Scene.
            var slots = (System.Array)typeof(RoadmapWorldRenderer).GetField("_glades", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(renderer);
            foreach (var slot in slots)
                if ((int)slot.GetType().GetField("Key").GetValue(slot) == node)
                    return (Mesh)slot.GetType().GetField("Mesh").GetValue(slot);
            Assert.Fail("Native node mesh missing"); return null;
        }
        static IEnumerator Shot(string name)
        {
            yield return RoadmapStreamingPlayModeTests.Shot("story_" + name);
            var root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../TestResults"));
            var output = Path.Combine(root, "environmental-story-2026-10-07", name + ".png");
            File.Delete(output); File.Move(Path.Combine(root, "roadmap-production-2026-10-07", "story_" + name + ".png"), output);
        }
        [UnityTest, Timeout(150000)] public IEnumerator CareRefreshesNativeBufferAndKeepsHiddenDetailOutOfMap()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Requires rendered Game View");
            RoadmapStreamingPlayModeTests.Size(720, 1600);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/QuietCamp/Tests/Fixtures/Roadmap/RoadmapBenchmark.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var harness = Object.FindAnyObjectByType<RoadmapBenchmarkHarness>(); harness.autoRun = false;
            harness.fixtureName = "environmental-story-baked"; harness.summaryName = "environmental-story-summaries";
            harness.completedLevels = 1;
            harness.outputDirectoryOverride = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../TestResults/environmental-story-2026-10-07"));
            harness.Build(); var map = harness.Map; yield return RoadmapStreamingPlayModeTests.Ready(map);
            int node = map.Data.LevelIndex("QC002");
            Assert.AreEqual(.6f, map.Scenes[node].Props.Single(p => p.Asset == "log_stack" && p.Position.x == -13).Height);
            Assert.IsFalse(map.Data.Nodes[0].world.props.Any(p => p.storyId == "wayside:board"));
            float before = NativeMesh(map.WorldRenderer, node).vertices.Sum(v => v.y);
            yield return Shot("before_care");
            harness.Services.Progression.MarkCompleted("QC002"); yield return null;
            yield return RoadmapStreamingPlayModeTests.Ready(map);
            Assert.AreEqual(.8f, map.Scenes[node].Props.Single(p => p.Asset == "log_stack" && p.Position.x == -13).Height);
            Assert.AreNotEqual(before, NativeMesh(map.WorldRenderer, node).vertices.Sum(v => v.y), "Native cached mesh retained the old care state");
            Assert.IsFalse(map.Data.Nodes[2].world.props.Any(p => p.storyId == "garden:flowers"));
            Assert.LessOrEqual(map.ActiveChunks, 3); Assert.AreEqual(1, map.WorldRenderer.MaterialCount); Assert.AreEqual(1, map.WorldRenderer.TextureCount);
            while(map.RevealAnimating)yield return null;
            yield return RoadmapStreamingPlayModeTests.Ready(map);
            yield return Shot("after_care");
        }
    }
}
#endif
