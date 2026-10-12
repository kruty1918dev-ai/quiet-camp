#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using QuietCamp.Presentation;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace QuietCamp.Editor
{
    [InitializeOnLoad]
    public static class QC007ArtCapture
    {
        sealed class Request { public string mode, output, name, levelId, profile; public int width = 1536, height = 1024, seed = 12007; }
        static readonly string RequestPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Temp/ai/qc007-art-request.json"));
        static readonly string ResultPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Temp/ai/qc007-art-result.json"));
        static readonly string GamePath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Temp/ai/qc007-game-session.json"));
        static QC007ArtCapture() => EditorApplication.update += Poll;
        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists(GamePath)) PollGame();
            if (!File.Exists(RequestPath)) return;
            try
            {
                var request = JsonConvert.DeserializeObject<Request>(File.ReadAllText(RequestPath));
                File.Delete(RequestPath);
                if (request.mode == "capture") Capture(request);
                else if (request.mode == "tests") RunTests();
                else if (request.mode == "game-start") StartGame();
                else if (request.mode == "game-shot") GameShot(request);
                else if (request.mode == "game-stop") { SetGameStage("stopping"); EditorApplication.ExitPlaymode(); }
                else File.WriteAllText(ResultPath, JsonConvert.SerializeObject(new { status = "ready", playing = EditorApplication.isPlaying, unity = UnityEngine.Application.unityVersion }, Formatting.Indented));
            }
            catch (Exception error) { File.WriteAllText(ResultPath, JsonConvert.SerializeObject(new { status = "error", error = error.ToString() })); }
        }
        sealed class GameState { public string stage, saveRoot, backupRoot; public string[] files; }
        static void StartGame()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("The existing Play session must be left intact.");
            string saves = Path.Combine(UnityEngine.Application.persistentDataPath, "saves");
            string backup = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Temp/ai/qc007-save-backup"));
            Directory.CreateDirectory(backup);
            var files = Directory.Exists(saves) ? Directory.GetFiles(saves).Select(Path.GetFileName).ToArray() : Array.Empty<string>();
            foreach (var file in files) File.Copy(Path.Combine(saves, file), Path.Combine(backup, file), true);
            File.WriteAllText(GamePath, JsonConvert.SerializeObject(new GameState { stage = "boot", saveRoot = saves, backupRoot = backup, files = files }));
            File.WriteAllText(ResultPath, "{\"status\":\"starting-game\"}");
            EditorApplication.EnterPlaymode();
        }
        static void SetGameStage(string stage)
        {
            var state = JsonConvert.DeserializeObject<GameState>(File.ReadAllText(GamePath)); state.stage = stage;
            File.WriteAllText(GamePath, JsonConvert.SerializeObject(state));
        }
        static void PollGame()
        {
            var state = JsonConvert.DeserializeObject<GameState>(File.ReadAllText(GamePath));
            if (!EditorApplication.isPlaying)
            {
                if (state.stage != "stopping") return;
                Directory.CreateDirectory(state.saveRoot);
                foreach (var file in Directory.GetFiles(state.saveRoot))
                    if (!state.files.Contains(Path.GetFileName(file))) File.Delete(file);
                foreach (var file in state.files) File.Copy(Path.Combine(state.backupRoot, file), Path.Combine(state.saveRoot, file), true);
                File.Delete(GamePath); File.WriteAllText(ResultPath, "{\"status\":\"game-stopped-saves-restored\"}"); return;
            }
            if (state.stage == "boot")
            {
                var services = QuietCampBootstrap.ServicesRef;
                if (services == null) return;
                if (MenuSceneHost.Current == null || !MenuSceneHost.Current.UiReady) return;
                services.Progression.Restore(LevelLoader.MvpLevelIds().Take(6), "QC006", services.Progression.CosmeticFlags);
                services.Save.Session = new QuietCamp.Application.SessionSaveData();
                services.PendingLevelId = "QC007"; services.PendingMenuScreen = null;
                SetGameStage("camp"); SceneManager.LoadSceneAsync("Camp");
            }
            else if (state.stage == "camp" && CampSceneHost.Current != null && CampSceneHost.Current.UiReady)
            {
                SetGameStage("ready");
                File.WriteAllText(ResultPath, JsonConvert.SerializeObject(new { status = "game-ready", level = CampSceneHost.Current.Session.Level.id,
                    art = UnityEngine.Object.FindAnyObjectByType<CampArtView>() != null, scene = SceneManager.GetActiveScene().name }, Formatting.Indented));
            }
        }
        static void GameShot(Request request)
        {
            if (!EditorApplication.isPlaying || CampSceneHost.Current?.Session?.Level?.id != "QC007") throw new InvalidOperationException("QC007 Gameplay is not ready.");
            var host = CampSceneHost.Current; var camera = UnityEngine.Object.FindAnyObjectByType<CampAtmosphere>().WorldCamera;
            var view = camera.GetComponent<CampArtView>();
            var board = typeof(CampSceneHost).GetField("_renderer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(host) as BoardRenderer;
            board.SyncPlacements(host.Session.Level.witness, host.Session.Level, 0, true);
            board.ShowShade(false); board.HidePath();
            var overlays = typeof(BoardRenderer).GetField("_overlayRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(board) as Transform;
            bool overlayVisible = overlays.gameObject.activeSelf; overlays.gameObject.SetActive(false);
            var previousTarget = camera.targetTexture; float previousAspect = camera.aspect; var previousActive = RenderTexture.active;
            var texture = new RenderTexture(request.width, request.height, 24, RenderTextureFormat.ARGB32); texture.Create();
            var image = new Texture2D(request.width, request.height, TextureFormat.RGB24, false);
            try
            {
                camera.aspect = (float)request.width / request.height; view.FitCamera(new Rect(0, 0, 1, 1));
                camera.targetTexture = texture;
                var render = new UniversalRenderPipeline.SingleCameraRequest { destination = texture };
                RenderPipeline.SubmitRenderRequest(camera, render); RenderPipeline.SubmitRenderRequest(camera, render);
                RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, request.width, request.height), 0, 0); image.Apply();
                var bytes = image.EncodeToPNG(); Directory.CreateDirectory(request.output);
                string output = Path.Combine(request.output, request.name + ".png"); File.WriteAllBytes(output, bytes);
                var result = new { status = "game-captured", kind = "native-live-gameplay-camera", output, imageSha256 = Hash(bytes),
                    level = host.Session.Level.id, contentHash = host.Session.Level.contentHash, scene = SceneManager.GetActiveScene().name,
                    width = request.width, height = request.height, artSha256 = Hash(File.ReadAllBytes("Assets/QuietCamp/Resources/QuietCamp/ArtCompositions/QC007.json")),
                    profile = view.Definition.generation, seed = view.Definition.seed, sourceReceipt = SourceReceipt(view.Definition),
                    cameraEuler = view.Definition.cameraEuler, camera.orthographicSize, capturedUtc = DateTime.UtcNow.ToString("o"), playerBuild = false };
                string serialized = JsonConvert.SerializeObject(result, Formatting.Indented);
                File.WriteAllText(Path.Combine(request.output, request.name + ".json"), serialized); File.WriteAllText(ResultPath, serialized);
            }
            finally
            {
                camera.targetTexture = previousTarget; camera.aspect = previousAspect; RenderTexture.active = previousActive;
                overlays.gameObject.SetActive(overlayVisible);
                texture.Release(); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(image);
            }
        }
        static void RunTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new TestReport(api));
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
                testNames = new[] { "QuietCamp.Tests.CampArtCompositionTests", "QuietCamp.Tests.RulesTests", "QuietCamp.Tests.LiveCampContentTests" } }));
        }
        sealed class TestReport : ICallbacks
        {
            readonly TestRunnerApi _api;
            readonly System.Collections.Generic.List<object> _failures = new System.Collections.Generic.List<object>();
            public TestReport(TestRunnerApi api) => _api = api;
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Failed) _failures.Add(new { name = result.Test.FullName, result.Message, result.StackTrace });
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                File.WriteAllText(ResultPath, JsonConvert.SerializeObject(new { status = "tests", outcome = result.TestStatus.ToString(),
                    passed = result.PassCount, failed = result.FailCount, skipped = result.SkipCount, duration = result.Duration, failures = _failures }, Formatting.Indented));
                UnityEngine.Object.DestroyImmediate(_api);
            }
        }
        static string Hash(byte[] bytes) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static object SourceReceipt(CampArtDefinition art)
        {
            var paths = new[] { "Assets/QuietCamp/Scripts/Infrastructure/CampArtDefinition.cs", "Assets/QuietCamp/Scripts/Infrastructure/CampCompositionGenerator.cs",
                "Assets/QuietCamp/Scripts/Presentation/World/CampArtView.cs", "Assets/QuietCamp/Scripts/Presentation/World/BoardRenderer.cs",
                "Assets/QuietCamp/Scripts/Presentation/World/CampArtEdgeBlur.cs", "Assets/QuietCamp/Scripts/Presentation/World/CampAtmosphere.cs",
                "Assets/QuietCamp/Resources/QuietCamp/CampArtLit.shader", "Assets/QuietCamp/Resources/QuietCamp/CampArtEdgeBlur.shader",
                "Assets/QuietCamp/Resources/QuietCamp/ArtCompositions/Profiles/" + art.generation + ".json",
                "Assets/QuietCamp/Settings/URP/QC_Renderer.asset" }
                .Concat(new[] { "scene-placement", "vegetation-placement", "rock-placement" }.SelectMany(id => Directory.GetFiles("Packages/com.kruty1918." + id + "/Runtime/Core", "*.cs")))
                .Concat(AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(Resources.Load<Material>(art.waterMaterial)), true))
                .Concat(AssetDatabase.GetDependencies("Assets/QuietCamp/Resources/QuietCamp/CozyVegetationLibrary.asset", true))
                .Where(File.Exists).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();
            return new { resolvedArtSha256 = Hash(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(art))),
                sources = paths.Select(path => new { path, sha256 = Hash(File.ReadAllBytes(path)) }).ToArray() };
        }
        static void Capture(Request request)
        {
            if (request.width < 640 || request.width > 2560 || request.height < 480 || request.height > 2560)
                throw new InvalidDataException("Capture dimensions out of range.");
            if (string.IsNullOrEmpty(request.output) || string.IsNullOrEmpty(request.name)
                || request.name.Any(c => !(char.IsLetterOrDigit(c) || c == '-' || c == '_')))
                throw new InvalidDataException("Invalid capture destination.");
            Directory.CreateDirectory(request.output);
            var level = LevelLoader.Load(request.levelId ?? "QC007");
            var art = string.IsNullOrEmpty(request.profile) ? CampArtDefinition.Load(level) : CampCompositionGenerator.Generate(level, request.profile, request.seed, out _);
            var catalog = AssetCatalog.Load();
            var oldAsync = ShaderUtil.allowAsyncCompilation;
            var oldSun = RenderSettings.sun; var oldAmbient = RenderSettings.ambientLight;
            var oldMode = RenderSettings.ambientMode; var oldFog = RenderSettings.fog;
            var oldSunDir = Shader.GetGlobalVector("_CampSunDirW");
            var oldSunColor = Shader.GetGlobalColor("_CampSunColor");
            var oldCampAmbient = Shader.GetGlobalColor("_CampAmbient");
            var preview = new PreviewRenderUtility(true);
            GameObject root = null; CampArtView view = null;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                root = new GameObject("QC007_NativeComposition");
                Transform Child(string name) { var go = new GameObject(name); go.transform.SetParent(root.transform, false); return go.transform; }
                var board = new BoardRenderer(level, catalog, Child("Base"), Child("Grid"), Child("Obstacles"), Child("Tents"), Child("Overlay"), art);
                board.SyncPlacements(level.witness, level, 0, true);
                var decor = Child("Decor");
                var camera = preview.camera;
                camera.cameraType = CameraType.Game; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = CampArtDefinition.Color(art.groundLow); camera.aspect = (float)request.width / request.height;
                camera.allowHDR = true; camera.allowMSAA = true;
                view = camera.gameObject.AddComponent<CampArtView>(); view.Configure(art, level, decor, catalog);
                view.FitCamera(new Rect(0, 0, 1, 1));
                preview.lights[0].type = LightType.Directional; view.ApplyLighting(preview.lights[0]);
                preview.lights[1].intensity = 0;
                preview.ambientColor = CampArtDefinition.Color(art.ambientColor) * .60f;
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = preview.ambientColor; RenderSettings.sun = preview.lights[0]; RenderSettings.fog = false;
                Shader.SetGlobalVector("_CampSunDirW", -preview.lights[0].transform.forward);
                Shader.SetGlobalColor("_CampSunColor", preview.lights[0].color * preview.lights[0].intensity);
                Shader.SetGlobalColor("_CampAmbient", preview.ambientColor);
                foreach (var transform in root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 31;
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << 31;
                preview.AddSingleGO(root);
                for (int frame = 0; frame < 3; frame++)
                { preview.BeginStaticPreview(new Rect(0, 0, request.width, request.height)); preview.Render(true); UnityEngine.Object.DestroyImmediate(preview.EndStaticPreview()); }
                preview.BeginStaticPreview(new Rect(0, 0, request.width, request.height)); preview.Render(true);
                var image = preview.EndStaticPreview(); var bytes = image.EncodeToPNG(); UnityEngine.Object.DestroyImmediate(image);
                string output = Path.Combine(request.output, request.name + ".png"); File.WriteAllBytes(output, bytes);
                var errors = new[] { Resources.Load<Shader>("QuietCamp/CampArtLit"), Resources.Load<Shader>("QuietCamp/CampArtEdgeBlur"), Resources.Load<Material>(art.waterMaterial).shader }
                    .SelectMany(ShaderUtil.GetShaderMessages).Where(message => message.severity.ToString() == "Error").Select(message => message.message).ToArray();
                if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors));
                var receipt = new { status = "captured", kind = "native-unity-production-world-components", output,
                    unity = UnityEngine.Application.unityVersion, graphics = SystemInfo.graphicsDeviceType.ToString(), device = SystemInfo.graphicsDeviceName,
                    width = request.width, height = request.height, levelId = level.id, contentHash = level.contentHash,
                    imageSha256 = Hash(bytes), artSha256 = Hash(File.ReadAllBytes("Assets/QuietCamp/Resources/QuietCamp/ArtCompositions/QC007.json")),
                    cameraEuler = art.cameraEuler, cameraPosition = new[] { camera.transform.position.x, camera.transform.position.y, camera.transform.position.z }, camera.orthographicSize,
                    sourceMeshes = art.models.Select(model => model.id).ToArray(), instances = art.instances.Length, triangles = view.TriangleCount,
                    profile = art.generation, generationAttempt = art.generationAttempt, seed = art.seed, sourceReceipt = SourceReceipt(art),
                    sunEuler = art.sunEuler, art.sunIntensity, art.blurPixels, warmupFrames = 3, capturedUtc = DateTime.UtcNow.ToString("o"), playerBuild = false, shaderErrors = errors };
                string serialized = JsonConvert.SerializeObject(receipt, Formatting.Indented);
                File.WriteAllText(Path.Combine(request.output, request.name + ".json"), serialized);
                File.WriteAllText(ResultPath, serialized);
            }
            finally
            {
                if (view != null) { view.Release(); UnityEngine.Object.DestroyImmediate(view); }
                preview.Cleanup();
                RenderSettings.sun = oldSun; RenderSettings.ambientLight = oldAmbient; RenderSettings.ambientMode = oldMode; RenderSettings.fog = oldFog;
                Shader.SetGlobalVector("_CampSunDirW", oldSunDir); Shader.SetGlobalColor("_CampSunColor", oldSunColor); Shader.SetGlobalColor("_CampAmbient", oldCampAmbient);
                ShaderUtil.allowAsyncCompilation = oldAsync;
            }
        }
    }
}
#endif

