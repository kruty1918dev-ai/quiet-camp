using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    public class PlacementCameraPlayModeTests
    {
        Mouse _mouse;
        InputSettings.BackgroundBehavior _background;
        InputSettings.EditorInputBehaviorInPlayMode _editorInput;

        [SetUp] public void SetUp()
        {
            _background = InputSystem.settings.backgroundBehavior;
            _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        [TearDown] public void TearDown()
        {
            InputSystem.RemoveDevice(_mouse);
            InputSystem.settings.backgroundBehavior = _background;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
        }

        static IEnumerator Frames(int count) { for (var i = 0; i < count; i++) yield return null; }
        static void Size(int width, int height) => typeof(ScreenshotPlayModeTest)
            .GetMethod("SetGameViewSize", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { width, height });
        void Pointer(Vector2 at, bool down)
        {
            _mouse.MakeCurrent();
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = at }.WithButton(MouseButton.Left, down));
        }

        // Observe every rendered frame, including the frame in which the HTML
        // remount and atmosphere layout refit run. A settle-only check misses jolts.
        static IEnumerator Still(Camera camera, Vector3 position, Quaternion rotation, float size, string action, int frames = 8)
        {
            for (var i = 0; i < frames; i++)
            {
                yield return null;
                Assert.Less(Vector3.Distance(position, camera.transform.position), .00001f, action + ": camera moved");
                Assert.Less(Quaternion.Angle(rotation, camera.transform.rotation), .00001f, action + ": camera rotated");
                Assert.AreEqual(size, camera.orthographicSize, .00001f, action + ": camera zoomed");
            }
        }

        [UnityTest] public IEnumerator CardPreviewDropAndHistoryKeepCameraStillOnPhoneAndTablet()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Requires rendered Game View");
            Size(720, 1600);
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            var deadline = Time.realtimeSinceStartup + 20;
            while (Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady != true && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<QuietCampBootstrap>().StartupReady);
            var services = QuietCampBootstrap.ServicesRef;
            services.Tutorial.Skip();
            services.Save.Session = new SessionSaveData();
            services.ReducedMotion = false;
            services.Settings.textScale = 1.3f;
            services.Localization.TrySetLanguage("de");
            services.PendingLevelId = "QC001";
            yield return SceneManager.LoadSceneAsync("Camp");
            deadline = Time.realtimeSinceStartup + 20;
            while (CampSceneHost.Current?.IsReady != true && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(CampSceneHost.Current.IsReady);
            yield return Frames(30);
            var session = CampSceneHost.Current.Session;
            var controller = Object.FindAnyObjectByType<PlacementController>();
            var hud = Object.FindObjectsByType<HtmlSurface>().First(s => s.name == "CampHtml");
            var camera = Camera.main;
            // This fixture isolates layout/board-command jolts. Wind motion and
            // its gesture freeze have separate EnvironmentPresentation coverage.
            var atmosphereMotion = CampSceneHost.Current.Atmosphere.CameraMotion;
            if (atmosphereMotion != null) atmosphereMotion.enabled = false;

            foreach (var dimensions in new[] { new Vector2Int(720, 1600), new Vector2Int(2560, 1600) })
            {
                Size(dimensions.x, dimensions.y);
                session.Restore(new Placement[0], "g1");
                yield return Frames(20);
                yield return (IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Screenshots/PlacementCamera"), "before_" + dimensions.x + "x" + dimensions.y });
                yield return Frames(8);
                Assert.AreEqual(dimensions.x, Screen.width); Assert.AreEqual(dimensions.y, Screen.height);
                var position = camera.transform.position;
                var rotation = camera.transform.rotation;
                var size = camera.orthographicSize;
                var outside = new Vector2(-100, -100);
                Pointer(outside, true); yield return null;
                Assert.IsTrue(controller.BeginCardDrag("g1", 0, outside));
                var first = (Vector2)camera.WorldToScreenPoint(BoardMath.CellCenter(session.Level, 0, 0));
                Pointer(first, true);
                yield return Still(camera, position, rotation, size, "card preview");
                Assert.IsTrue(controller.HasPreview);
                Assert.NotNull(hud.Element("placement-status"));
                Pointer(outside, true);
                yield return Still(camera, position, rotation, size, "invalid preview");
                controller.Cancel(); Pointer(outside, false);
                yield return Still(camera, position, rotation, size, "cancel");
                Assert.AreEqual(0, session.State.Count);

                Pointer(outside, true); yield return Still(camera, position, rotation, size, "card press", 1);
                Assert.IsTrue(controller.BeginCardDrag("g1", 0, outside));
                Pointer(first, true); yield return Still(camera, position, rotation, size, "valid card preview");
                Pointer(first, false); yield return Still(camera, position, rotation, size, "card drop");
                Assert.AreEqual(1, session.State.Count);
                Assert.IsTrue(session.Undo());
                yield return Still(camera, position, rotation, size, "undo card drop");
                Assert.IsFalse(session.Undo(), "One drop must produce one command");
                session.Select("g1");
                yield return Still(camera, position, rotation, size, "guest selection");
                // A normal board tap uses the same camera projection as the drag.
                Pointer(first, true); yield return Still(camera, position, rotation, size, "board press", 1);
                Pointer(first, false); yield return Still(camera, position, rotation, size, "drop");
                Assert.AreEqual(1, session.State.Count);
                var ghost = Object.FindObjectsByType<TentCloth>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(c => c.name == "Ghost_g1");
                var source = services.Assets.Prefab("tent_smallOpen").GetComponentInChildren<MeshRenderer>().sharedMaterials;
                var preview = ghost.GetComponentInChildren<MeshRenderer>(true).sharedMaterials;
                for (var slot = 0; slot < source.Length; slot++)
                    Assert.Less(Vector4.Distance(source[slot].GetColor("_BaseColor"), preview[slot].GetColor("_BaseColor")), .00001f, "Preview recoloured the tent");
                Pointer(outside, true); yield return null;
                Assert.IsTrue(controller.BeginCardDrag("g1", 0, outside));
                var moved = (Vector2)camera.WorldToScreenPoint(BoardMath.CellCenter(session.Level, 1, 0));
                Pointer(moved, true); yield return Still(camera, position, rotation, size, "moving placed tent");
                Pointer(moved, false); yield return Still(camera, position, rotation, size, "move drop");
                Assert.AreEqual(1, session.State.Find("g1").x);
                Assert.IsTrue(session.TryCommit(PlacementCommand.Place("g2", 0, 3, 1), out _));
                yield return Still(camera, position, rotation, size, "all placed card");
                Assert.IsNull(hud.Element("guest-card"));
                Assert.IsTrue(session.Undo());
                yield return Still(camera, position, rotation, size, "undo");
                Assert.IsTrue(session.Redo());
                yield return Still(camera, position, rotation, size, "redo");
                session.Select("g2");
                Assert.IsTrue(session.TryCommit(PlacementCommand.Place("g2", 0, 3, 2), out _));
                yield return Still(camera, position, rotation, size, "rotate");
                Assert.IsTrue(session.TryCommit(PlacementCommand.Remove("g2"), out _));
                yield return Still(camera, position, rotation, size, "remove");
                Debug.Log($"[PlacementCameraQA] Stable at {dimensions.x}x{dimensions.y}, position={position}, size={size}");
            }
            services.Settings.textScale = 1f;
            services.Localization.TrySetLanguage("uk");
        }
    }
}
