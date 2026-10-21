// Copy into Assets/Editor of the authorized QA project only. No builds or save changes.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Reflection;
using Newtonsoft.Json;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEditor;
using UnityEngine.Profiling;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
static class LosslessPerformanceProbe
{
    static readonly string Control = Path.GetFullPath("Temp/LosslessPerformance");
    static LosslessPerformanceProbe() { EditorApplication.update += Tick; }
    static void Tick()
    {
        var request = Path.Combine(Control, "request.txt");
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        if (PlayerSettings.productName != "Quiet Camp QA") return;
        string round = File.ReadAllText(request).Trim(); File.Delete(request);
        if (round != "baseline" && round != "optimized" && round != "repeat" && round != "baseline-timing" && round != "optimized-timing") return;
        var results = new List<object>();
        try
        {
            using(var calibration=Allocations())
            {
                var allocated=new byte[12345];GC.KeepAlive(allocated);calibration.Stop();
                if(Bytes(calibration)<12345)throw new InvalidOperationException("Mono heap measurement failed its allocation calibration.");
            }
            var art = CampArtDefinition.Load(LevelLoader.Load("QC007"));
            foreach (string id in new[] { "spring_morning", "summer_rain", "autumn_golden", "winter_snowfall" })
            {
                var preset = SeasonAtmosphereCatalog.Get(id);
                var resolved = art.models.Select(m => SeasonModelAppearance.Resolve(m, preset)).ToArray();
                string geometry = Hash(JsonConvert.SerializeObject(resolved));
                // Warm-up and measure the real transformation independently of serialization.
                var timings = new List<double>(); var allocations = new List<long>();
                int iterations=round.EndsWith("-timing")?20:3;
                for (int sample = 0; sample < 9; sample++)
                {
                    using var recorder=Allocations();var watch = Stopwatch.StartNew();
                    for (int i = 0; i < iterations; i++) foreach (var model in art.models) SeasonModelAppearance.Resolve(model, preset);
                    watch.Stop();recorder.Stop();timings.Add(watch.Elapsed.TotalMilliseconds / iterations);
                    allocations.Add(Bytes(recorder)<0?-1:Bytes(recorder) / iterations);
                }
                results.Add(new { kind = "geometry", id, geometry, models = resolved.Length,
                    vertices = resolved.Sum(m => m.vertices.Length / 3), milliseconds = timings, bytes = allocations });
                if(!round.EndsWith("-timing"))
                    foreach (int tier in new[] { 0, 1, 2 }) foreach (bool reduced in new[] { false, true })
                        results.Add(Weather(preset, tier, reduced));
            }
            File.WriteAllText(Path.Combine(Control, round + ".json"), JsonConvert.SerializeObject(new
            { unity = UnityEngine.Application.unityVersion, utc = DateTime.UtcNow,
                filterIlBytes=typeof(SeasonModelAppearance).GetMethod("Filter",BindingFlags.Static|BindingFlags.NonPublic).GetMethodBody().GetILAsByteArray().Length,
                scope = "Editor CPU; calibrated Mono used-heap growth, -1 if collection occurred during a sample; fixed-step native particles; not device FPS or per-thread allocation totals", results }, Formatting.Indented));
            File.Delete(Path.Combine(Control, round + "-error.txt"));
            UnityEngine.Debug.Log("[QC-LOSSLESS] Completed " + round);
        }
        catch (Exception error)
        { File.WriteAllText(Path.Combine(Control, round + "-error.txt"), error.ToString()); UnityEngine.Debug.LogException(error); }
    }
    static object Weather(SeasonAtmospherePreset preset, int tier, bool reduced)
    {
        var root = new GameObject("Lossless weather QA"); var cameraRoot = new GameObject("Lossless QA camera");
        try
        {
            var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false;
            CameraFitter.Configure(camera); camera.orthographicSize = 8; camera.aspect = .45f;
            var art = new CampArtDefinition { width = 6, height = 6, streamCentre = 100,
                models = new[]{new CampArtDefinition.Model { id="qa-tree",seasonalRole="deciduous",vertices=new float[]{0,0,0,1,5,0,0,5,1},
                    normals=new float[]{0,1,0,0,1,0,0,1,0},colors=new[]{0x557744,0x557744,0x557744},seasonalLeafTriangles=new[]{0} }},
                instances = new[]{new CampArtDefinition.Instance{modelId="qa-tree",x=5,z=4},
                    new CampArtDefinition.Instance{motion="waterwheel",motionPivot=new float[]{0,0,0},x=-5,z=-4}} };
            var view = new GameObject("Weather").AddComponent<SeasonAtmosphereView>();
            var setup=Stopwatch.StartNew();
            view.Configure(preset, art, camera, root.transform);
            setup.Stop();
            view.SetFire(new Vector3(4,0,4),true);
            for (int i = 0; i < 60; i++) view.Advance(1f / 60, reduced, tier);
            using var recorder=Allocations();var watch = Stopwatch.StartNew();
            for (int i = 0; i < 180; i++) view.Advance(1f / 60, reduced, tier);
            watch.Stop();recorder.Stop();long allocated=Bytes(recorder);
            var particles = new List<object>(); var buffer = new ParticleSystem.Particle[4096];
            foreach (var system in root.GetComponentsInChildren<ParticleSystem>())
            {
                int count = system.GetParticles(buffer);
                particles.Add(new { system.name, count, values = buffer.Take(count).Select(p => new
                { p.position, p.velocity, p.startSize, p.startColor, p.rotation3D, p.remainingLifetime, p.randomSeed }).ToArray() });
            }
            // Unity Vector serialization must avoid recursive normalized properties.
            string snapshot = JsonConvert.SerializeObject(particles, new JsonSerializerSettings
            { Converters = new List<JsonConverter> { new VectorConverter() } });
            var textures=root.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials)
                .Where(m=>m!=null&&(m.HasProperty("_BaseMap")||m.HasProperty("_MainTex")))
                .Select(m=>m.GetTexture(m.HasProperty("_BaseMap")?"_BaseMap":"_MainTex")).OfType<Texture2D>().Distinct().ToArray();
            var textureHashes=textures.Select(t=>Hash(Convert.ToBase64String(t.GetRawTextureData()))).Distinct().OrderBy(h=>h).ToArray();
            return new { kind = "weather", id = preset.id, tier, reduced, milliseconds = watch.Elapsed.TotalMilliseconds / 180,
                bytes = allocated<0?-1:allocated / 180, particleHash = Hash(snapshot), particles = view.LiveParticles, view.WindStrength,
                view.RainContactCount, view.FabricRainContacts, view.VisibleShafts,setupMilliseconds=setup.Elapsed.TotalMilliseconds,
                textureCount=textures.Length,textureHashes };
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(cameraRoot); }
    }
    sealed class VectorConverter : JsonConverter
    {
        public override bool CanConvert(Type type) => type == typeof(Vector3);
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        { var v = (Vector3)value; writer.WriteStartArray(); writer.WriteValue(v.x); writer.WriteValue(v.y); writer.WriteValue(v.z); writer.WriteEndArray(); }
        public override object ReadJson(JsonReader reader, Type type, object value, JsonSerializer serializer) => throw new NotSupportedException();
    }
    static string Hash(string value)
    { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant(); }
    sealed class HeapSample:IDisposable
    {
        readonly long before=Profiler.GetMonoUsedSizeLong();
        readonly int collections=GC.CollectionCount(0);
        public long Bytes {get;private set;}
        public void Stop(){long growth=Profiler.GetMonoUsedSizeLong()-before;Bytes=GC.CollectionCount(0)==collections&&growth>=0?growth:-1;}
        public void Dispose(){}
    }
    static HeapSample Allocations()=>new HeapSample();
    static long Bytes(HeapSample sample)=>sample.Bytes;
}
