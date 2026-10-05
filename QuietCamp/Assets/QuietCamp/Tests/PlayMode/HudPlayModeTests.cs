using System.Collections;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace QuietCamp.Tests
{
    public class HudPlayModeTests
    {
        InputSettings.BackgroundBehavior _background;
        InputSettings.EditorInputBehaviorInPlayMode _editorInput;
        [SetUp] public void SyntheticInput()
        {
            _background = InputSystem.settings.backgroundBehavior;
            _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
        [TearDown] public void RestoreInput()
        {
            InputSystem.settings.backgroundBehavior = _background;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
        }
        static Button Button(string id) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .FirstOrDefault(b => b.name == "<button #" + id + ">" && b.gameObject.activeInHierarchy);

        static void Tap(Button button)
        {
            Assert.IsNotNull(button);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pos = RectTransformUtility.WorldToScreenPoint(cam, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = pos };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsTrue(hits.Count > 0 && (hits[0].gameObject.transform == rect || hits[0].gameObject.transform.IsChildOf(rect)),
                "Control is covered: " + string.Join(" | ", hits.ConvertAll(h => h.gameObject.name)));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        static IEnumerator LoadCamp()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            QuietCampBootstrap.ServicesRef.Tutorial.Skip(); // Exercise history controls independently of onboarding.
            // Boot now owns asynchronous initialization and the fade. Do not
            // race its MainMenu load with a fixture's direct scene navigation.
            float deadline = Time.realtimeSinceStartup + 20;
            while (!Object.FindAnyObjectByType<QuietCampBootstrap>().StartupReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<QuietCampBootstrap>().StartupReady);
            yield return SceneManager.LoadSceneAsync("Camp");
            for (var i = 0; i < 600 && (CampSceneHost.Current == null || !CampSceneHost.Current.IsReady); i++) yield return null;
            Assert.IsNotNull(CampSceneHost.Current);
            Assert.IsTrue(CampSceneHost.Current.IsReady);
            for (var i = 0; i < 40; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator GuestCardSelectsAndBoardTapCommits()
        {
            yield return LoadCamp();
            var host = CampSceneHost.Current;
            host.Session.Restore(new Placement[0], null);
            for (var i = 0; i < 5; i++) yield return null;
            Tap(Button("guest-card"));
            yield return null;
            Assert.AreEqual(0, host.Session.State.Count, "Choosing a guest must leave the puzzle decision to the player");
            var selected = host.Session.SelectedGuestId;
            Assert.AreEqual(host.Session.Level.guests[0].id, selected);
            var pose = host.Session.Level.witness.First(p => p.guestId == selected);
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var camera = Camera.main;
                Assert.IsNotNull(camera);
                var point = (Vector2)camera.WorldToScreenPoint(BoardMath.CellCenter(host.Session.Level, pose.x, pose.z));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                yield return null;
                Assert.AreEqual(1, host.Session.State.Count, "A real tap on the board must place the chosen guest");
                for (var i = 0; i < 10; i++) yield return null;
                Tap(Button("undo")); yield return null;
                Assert.AreEqual(0, host.Session.State.Count);
                for (var i = 0; i < 5; i++) yield return null;
                Tap(Button("redo")); yield return null;
                Assert.AreEqual(1, host.Session.State.Count);
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest]
        public IEnumerator CompletedCampWaitsForThePlayer()
        {
            yield return LoadCamp();
            var host = CampSceneHost.Current;
            host.Session.DebugApplyWitness();
            Assert.IsTrue(host.Session.Check().IsSolved);
            yield return new WaitForSecondsRealtime(3.2f);
            Assert.AreSame(host, CampSceneHost.Current, "Completion must not automatically load another level");
            Assert.IsNotNull(Button("next"));
            Assert.IsFalse(host.Session.Undo(), "A finished album layout must remain unchanged");
            Tap(Button("pause"));
            for (var i = 0; i < 20; i++) yield return null;
            Tap(Button("resume"));
            yield return new WaitForSecondsRealtime(.6f);
            Assert.IsNotNull(Button("next"), "Closing pause must return to the finished campsite");
        }
    }
}
