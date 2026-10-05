using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Kruty1918.InputRouting.API;
using Kruty1918.InputRouting.Runtime;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    public class PlacementPlayModeTests
    {
        GameObject _root;
        Camera _camera;
        Mouse _mouse;
        Mouse[] _otherMice;
        CampSession _session;
        PlacementController _controller;
        BoardRenderer _renderer;
        Transform _overlays;
        PlacementController[] _otherControllers;
        Collider[] _otherTentColliders;
        UnityEngine.UI.GraphicRaycaster[] _otherRaycasters;
        InputSettings.BackgroundBehavior _background;
        InputSettings.EditorInputBehaviorInPlayMode _editorInput;

        [SetUp]
        public void SetUp()
        {
            _background=InputSystem.settings.backgroundBehavior;
            _editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            // Other fixtures can leave a live camp behind. Its input and tents
            // must not receive or intercept this fixture's synthetic gestures.
            _otherControllers = Object.FindObjectsByType<PlacementController>().Where(c => c.enabled).ToArray();
            foreach (var controller in _otherControllers) controller.enabled = false;
            _otherTentColliders = Object.FindObjectsByType<Collider>()
                .Where(c => c.enabled && c.gameObject.layer == BoardRenderer.TentSelectableLayer).ToArray();
            foreach (var collider in _otherTentColliders) collider.enabled = false;
            _otherRaycasters = Object.FindObjectsByType<UnityEngine.UI.GraphicRaycaster>().Where(r => r.enabled).ToArray();
            foreach (var raycaster in _otherRaycasters) raycaster.enabled = false;
            _root = new GameObject("placement-test");
            Transform Child(string name)
            {
                var go = new GameObject(name);
                go.transform.SetParent(_root.transform, false);
                return go.transform;
            }
            _session = new CampSession(LevelLoader.Load("QC001"));
            _overlays = Child("overlays");
            var catalog = AssetCatalog.Load();
            _renderer = new BoardRenderer(_session.Level, catalog,
                Child("base"), Child("grid"), Child("obstacles"), Child("tents"), _overlays);
            _session.Evented += e =>
            {
                if (e.Kind == CampEventKind.BoardCommitted)
                    _renderer.SyncPlacements(_session.State.Placements, _session.Level, 1f, true);
            };
            _camera = Child("camera").gameObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 5f;
            _camera.transform.SetPositionAndRotation(Vector3.up * 20f, Quaternion.Euler(90f, 0f, 0f));
            _controller = Child("controller").gameObject.AddComponent<PlacementController>();
            _controller.Configure(_session, _renderer, null, _camera, catalog, () => true);
            _session.Select("g1");
            _otherMice = InputSystem.devices.OfType<Mouse>().Where(m => m.enabled).ToArray();
            foreach (var mouse in _otherMice) InputSystem.DisableDevice(mouse);
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_controller.gameObject);
            _renderer.ClearAll();
            _session.Dispose();
            Object.DestroyImmediate(_root);
            InputSystem.RemoveDevice(_mouse);
            foreach (var mouse in _otherMice) InputSystem.EnableDevice(mouse);
            foreach (var controller in _otherControllers) if (controller != null) controller.enabled = true;
            foreach (var collider in _otherTentColliders) if (collider != null) collider.enabled = true;
            foreach (var raycaster in _otherRaycasters) if (raycaster != null) raycaster.enabled = true;
            InputSystem.settings.backgroundBehavior=_background;
            InputSystem.settings.editorInputBehaviorInPlayMode=_editorInput;
        }

        Vector2 At(int x, int z) => _camera.WorldToScreenPoint(BoardMath.CellCenter(_session.Level, x, z));
        void MouseAt(Vector2 screen, bool down)
        {
            _mouse.MakeCurrent();
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = screen }.WithButton(MouseButton.Left, down));
        }

        static void AssertAuthoredTentColours(GameObject root,string asset)
        {
            var original=AssetCatalog.Load().Prefab(asset).GetComponentInChildren<MeshRenderer>().sharedMaterials;
            var renderer=root.GetComponentInChildren<MeshRenderer>(true);
            var current=renderer.sharedMaterials;
            Assert.AreEqual(original.Length,current.Length);
            for(int i=0;i<current.Length;i++)
                Assert.That(Vector4.Distance(current[i].GetColor("_BaseColor"),original[i].GetColor("_BaseColor")),Is.LessThan(.00001f),"Tent material recoloured at slot "+i);
            var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
            Assert.IsTrue(block.isEmpty,"Rule tint overrides the tent renderer");
        }

        [UnityTest]
        public IEnumerator WarningDragHintsAndRainKeepTentAndSupportColours()
        {
            foreach(var guest in _session.Level.guests)
            {
                Assert.IsTrue(_controller.BeginCardDrag(guest.id,0,At(-3,-3)));
                var ghost=(GameObject)typeof(PlacementController).GetField("_ghost",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(_controller);
                foreach(var cell in new[]{new Vector2Int(0,0),new Vector2Int(-1,0),new Vector2Int(5,5)})
                {
                    MouseAt(At(cell.x,cell.y),true);yield return null;
                    AssertAuthoredTentColours(ghost,guest.assetId);
                    ghost.GetComponent<TentCloth>().SetShelterWarmth(1);
                    AssertAuthoredTentColours(ghost,guest.assetId);
                }
                _controller.Cancel();MouseAt(At(0,0),false);yield return null;
                _renderer.ShowHintGhost(new Placement{guestId=guest.id,x=0,z=0},guest.assetId);
                var hint=_overlays.GetComponentInChildren<TentCloth>().gameObject;
                AssertAuthoredTentColours(hint,guest.assetId);
                Assert.IsTrue(hint.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"Hint intercepts the tent drag");
                _renderer.HideHint();yield return null;
            }
            Assert.AreEqual(0,_session.State.Count,"Cosmetic previews committed a placement");
        }

        [UnityTest]
        public IEnumerator CardDropCommitsOnceAndCancelPreservesLayout()
        {
            Assert.IsTrue(_controller.BeginCardDrag("g1",0,At(-3,-3)));
            Assert.IsFalse(_controller.BeginCardDrag("g2",1,At(0,0)),"A second pointer cannot steal a tent");
            MouseAt(At(0,0),true);yield return null;
            var dot=_overlays.Find("PreviewDoorDot");Assert.IsNotNull(dot);
            Assert.AreEqual(RuleEvaluator.Door(new Placement{x=0,z=0,rotation=0}),BoardMath.CellOf(_session.Level,dot.position));
            MouseAt(At(1,0),true);yield return null;
            Assert.AreEqual(RuleEvaluator.Door(new Placement{x=1,z=0,rotation=0}),BoardMath.CellOf(_session.Level,dot.position));
            MouseAt(At(0,0),true);yield return null;
            MouseAt(At(0,0),false);yield return null;
            Assert.AreEqual(1,_session.State.Count);Assert.IsTrue(_session.Undo());Assert.AreEqual(0,_session.State.Count);
            Assert.IsFalse(_session.Undo(),"One drop must create one command");Assert.IsTrue(_session.Redo());
            var revision=_session.State.Revision;
            Assert.IsTrue(_controller.BeginCardDrag("g1",0,At(0,0)));_controller.Cancel();yield return null;
            Assert.IsFalse(dot.gameObject.activeSelf,"Cancel must remove the moving door marker.");
            Assert.IsTrue(_renderer.Tents["g1"].Root.transform.Find("DoorMarker").gameObject.activeSelf);
            Assert.AreEqual(revision,_session.State.Revision);Assert.AreEqual(0,_session.State.Find("g1").x);
        }

        [UnityTest]
        public IEnumerator UiCardCapturesItsOwnTouchAndIgnoresAnotherFinger()
        {
            var touch=InputSystem.AddDevice<Touchscreen>();
            var cardGo=new GameObject("guest-card-test");cardGo.transform.SetParent(_root.transform,false);
            var card=cardGo.AddComponent<QuietCamp.Presentation.UI.GuestCardInput>();card.Configure(_controller,"g1");
            try
            {
                var outside=new Vector2(-1000,-1000);
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=17,phase=UnityEngine.InputSystem.TouchPhase.Began,position=outside});yield return null;
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=18,phase=UnityEngine.InputSystem.TouchPhase.Began,position=outside});yield return null;
                var owner=new UnityEngine.InputSystem.UI.ExtendedPointerEventData(UnityEngine.EventSystems.EventSystem.current)
                    {pointerType=UnityEngine.InputSystem.UI.UIPointerType.Touch,touchId=18,pointerId=2018,position=outside};
                var other=new UnityEngine.InputSystem.UI.ExtendedPointerEventData(UnityEngine.EventSystems.EventSystem.current)
                    {pointerType=UnityEngine.InputSystem.UI.UIPointerType.Touch,touchId=17,pointerId=2017,position=outside};
                card.OnPointerDown(owner);card.OnPointerDown(other);card.OnPointerUp(other);
                yield return new WaitForSecondsRealtime(.23f);yield return null;
                Assert.IsTrue(_controller.HasPreview,"The unrelated finger release canceled the hold");
                var destination=(Vector2)_camera.WorldToScreenPoint(BoardMath.TentCenter(_session.Level,0,0))
                    -Vector2.up*_controller.TouchLiftPixels;
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=18,phase=UnityEngine.InputSystem.TouchPhase.Moved,position=destination});yield return null;
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=18,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=destination});yield return null;
                card.OnPointerUp(owner);
                Assert.AreEqual(1,_session.State.Count,"The card followed primaryTouch rather than its owner");
                Assert.AreEqual(0,_session.State.Find("g1").x);Assert.IsTrue(_session.Undo());Assert.IsFalse(_session.Undo());
            }
            finally{_controller.Cancel();InputSystem.RemoveDevice(touch);}
        }

        [UnityTest]
        public IEnumerator TouchAimIsAboveFingerAndReleaseUsesLastPositionExactlyOnce()
        {
            var touch=InputSystem.AddDevice<Touchscreen>();
            try
            {
                var outside=new Vector2(Screen.width*.5f,32f);
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=27,phase=UnityEngine.InputSystem.TouchPhase.Began,position=outside});yield return null;
                Assert.IsTrue(_controller.BeginCardDrag("g1",27,outside));
                Assert.GreaterOrEqual(_controller.TouchLiftPixels,96f*PlacementTargetProjector.ScreenDpScale());
                Vector2 Finger(int x,int z)=>(Vector2)_camera.WorldToScreenPoint(BoardMath.TentCenter(_session.Level,x,z))
                    -Vector2.up*_controller.TouchLiftPixels;
                var raw=Finger(0,0);
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=27,phase=UnityEngine.InputSystem.TouchPhase.Moved,position=raw});yield return null;
                Assert.That(_controller.AimScreen.y-raw.y,Is.EqualTo(_controller.TouchLiftPixels).Within(.01));
                var dot=_overlays.Find("PreviewDoorDot");Assert.IsNotNull(dot);
                Assert.AreEqual(new Cell(1,2),BoardMath.CellOf(_session.Level,dot.position));
                Assert.IsNull(_root.GetComponentInChildren<TentDragCard>()?.GetComponentInChildren<Camera>(),"Holding must not render a second scene");
                // No preceding Moved event at the final coordinate: release must still retarget.
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=27,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=Finger(1,0)});yield return null;
                Assert.AreEqual(1,_session.State.Find("g1").x);
                Assert.IsTrue(_session.Undo());Assert.IsFalse(_session.Undo());Assert.IsTrue(_session.Redo());
                Assert.IsFalse(_controller.IsPlacementActive);
            }
            finally{_controller.Cancel();InputSystem.RemoveDevice(touch);}
        }

        [UnityTest]
        public IEnumerator ReleasingOutsideScreenDoesNotCommitLastValidTarget()
        {
            Assert.IsTrue(_controller.BeginCardDrag("g1",0,At(-3,-3)));
            MouseAt(At(0,0),true);yield return null;
            MouseAt(new Vector2(-20,-20),false);yield return null;
            Assert.AreEqual(0,_session.State.Count);Assert.IsFalse(_session.CanUndo);
            Assert.IsFalse(_controller.IsPlacementActive);
        }

        [UnityTest]
        public IEnumerator BoardTapPlacesButTentTapAndSmallJitterPreserveHistory()
        {
            var point = At(0, 0);
            MouseAt(point, true); yield return null;
            Assert.IsTrue(_controller.HasPreview);
            Assert.AreEqual(0, _session.State.Count, "Preview is not a committed move");
            MouseAt(point, false); yield return null;
            Assert.AreEqual(1, _session.State.Count);
            Assert.IsFalse(_controller.HasPreview);
            Physics.SyncTransforms();
            var revision = _session.State.Revision;
            MouseAt(point, true); yield return null;
            MouseAt(point + Vector2.one, true); yield return null;
            Assert.IsFalse(_controller.HasPreview, "A selection tap must not lift the tent");
            MouseAt(point + Vector2.one, false); yield return null;
            Assert.AreEqual(revision, _session.State.Revision);
            Assert.IsTrue(_session.Undo());
            Assert.AreEqual(0, _session.State.Count, "One undo should remove the only real placement");
        }

        [UnityTest]
        public IEnumerator InvalidDragRestoresTentAndReleasesPreview()
        {
            Assert.IsTrue(_session.TryCommit(PlacementCommand.Place("g1", 0, 0, 0), out _));
            Physics.SyncTransforms();
            string failure = null;
            _controller.PlacementFailed += key => failure = key;
            var revision = _session.State.Revision;
            MouseAt(At(0, 0), true); yield return null;
            MouseAt(At(-2, 0), true); yield return null;
            Assert.IsTrue(_controller.HasPreview);
            MouseAt(At(-2, 0), false); yield return null;
            Assert.AreEqual("rule.bounds", failure);
            Assert.AreEqual(revision, _session.State.Revision);
            Assert.IsFalse(_controller.HasPreview);
            Assert.AreEqual(BoardMath.TentCenter(_session.Level, 0, 0), _renderer.Tents["g1"].Root.transform.position);
            Assert.IsFalse(_overlays.Find("PlacementPreview").gameObject.activeSelf);
            _session.Select("g2");
            // The first lesson now reserves (2,4) as its entrance. Use a
            // disjoint footprint that leaves the authored entrance free.
            MouseAt(At(3, 0), true); yield return null;
            MouseAt(At(3, 0), false); yield return null;
            Assert.AreEqual(2, _session.State.Count, "The pointer must remain usable after a rejected drag");
        }

        [UnityTest]
        public IEnumerator PreviewPathIncludesEntryAndAvoidsCandidateFootprint()
        {
            MouseAt(At(0, 0), true); yield return null;
            var path = _overlays.Find("PathOverlay");
            Assert.IsTrue(path.gameObject.activeSelf, "A valid route must be visible");
            var footprint = new HashSet<Cell>(RuleEvaluator.Footprint(new Placement { x = 0, z = 0 }));
            var cells = path.Cast<Transform>().Where(t => t.gameObject.activeSelf)
                .Select(t => BoardMath.CellOf(_session.Level, t.position)).ToArray();
            Assert.Contains(new Cell(_session.Level.entry[0], _session.Level.entry[1]), cells);
            Assert.IsFalse(cells.Any(footprint.Contains), "Preview path must never walk through the new tent");
            var nodes = path.Cast<Transform>().ToArray();
            MouseAt(At(1, 0), true); yield return null;
            Assert.AreEqual(0, _session.State.Count);
            Assert.IsTrue(nodes.All(t => t != null && t.parent == path), "Dragging must reuse path tiles");
            _controller.Cancel();
            MouseAt(At(1, 0), false); yield return null;
            Assert.IsFalse(path.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator SecondTouchCannotStealDragAndCanceledTouchNeverCommits()
        {
            var touch = InputSystem.AddDevice<Touchscreen>();
            try
            {
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 17,
                    phase = UnityEngine.InputSystem.TouchPhase.Began, position = At(0, 0) });
                yield return null;
                Assert.IsTrue(_controller.HasPreview);
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 18,
                    phase = UnityEngine.InputSystem.TouchPhase.Began, position = At(2, 3) });
                yield return null;
                Assert.AreEqual(0, _session.State.Count);
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 17,
                    phase = UnityEngine.InputSystem.TouchPhase.Ended, position = At(0, 0) });
                yield return null;
                Assert.AreEqual(1, _session.State.Count);
                Assert.AreEqual(0, _session.State.Find("g1").x, "The second finger stole the first finger's anchor");
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 18,
                    phase = UnityEngine.InputSystem.TouchPhase.Ended, position = At(2, 3) });
                yield return null;
                _session.Select("g2");
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 19,
                    phase = UnityEngine.InputSystem.TouchPhase.Began, position = At(2, 3) });
                yield return null;
                Assert.IsTrue(_controller.HasPreview);
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 19,
                    phase = UnityEngine.InputSystem.TouchPhase.Canceled, position = At(2, 3) });
                yield return null;
                Assert.AreEqual(1, _session.State.Count, "An interrupted OS gesture must not place a tent");
                Assert.IsFalse(_controller.HasPreview);
            }
            finally { InputSystem.RemoveDevice(touch); }
        }

        [UnityTest]
        public IEnumerator SuccessfulMoveDismissesTutorialOnlyAfterRelease()
        {
            _session.TryCommit(PlacementCommand.Place("g1", 0, 0, 0), out _);
            Physics.SyncTransforms();
            var moved = 0;
            _controller.PlacementMoved += () => moved++;
            MouseAt(At(0, 0), true); yield return null;
            MouseAt(At(1, 1), true); yield return null;
            Assert.IsTrue(_controller.HasPreview);
            Assert.AreEqual(0, moved);
            Assert.AreEqual(0, _session.State.Find("g1").x);
            MouseAt(At(1, 1), false); yield return null;
            Assert.AreEqual(1, moved);
            Assert.AreEqual(1, _session.State.Find("g1").x);
            Assert.IsTrue(_session.Undo());
            Assert.AreEqual(0, _session.State.Find("g1").x);
        }

        [UnityTest]
        public IEnumerator GlobalBlockCancelsCapturedDragWithoutCommit()
        {
            // Replace only the policy, keeping the fully built board and real pointer.
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var policy = new GameplayInputPolicy();
            typeof(PlacementController).GetField("_policy", flags).SetValue(_controller, policy);
            MouseAt(At(0, 0), true); yield return null;
            Assert.IsTrue(_controller.HasPreview);
            using (policy.AcquireBlock(GameplayInputKind.AllPointer, this))
            {
                yield return null;
                Assert.IsFalse(_controller.HasPreview);
                MouseAt(At(0, 0), false); yield return null;
                Assert.AreEqual(0, _session.State.Count);
            }
            _session.Select("g1");
            MouseAt(At(0, 0), true); yield return null;
            MouseAt(At(0, 0), false); yield return null;
            Assert.AreEqual(1, _session.State.Count);
        }
    }
}
