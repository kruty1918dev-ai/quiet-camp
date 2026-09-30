using System.Collections.Generic;
using Kruty1918.UIActions.API;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Main-menu scene host: stretches the illustrated camp background
    /// full-bleed under CanvasRoot, wires Menu action ids (continue/play/
    /// levels/album/settings/back) into the shared router and owns the
    /// Menu UI context.
    /// </summary>
    public sealed class MenuSceneHost : MonoBehaviour
    {
        GameServices _services;
        ScreenRouter _router;
        MenuScreens _screens;
        readonly List<System.IDisposable> _leases = new List<System.IDisposable>();
        System.IDisposable _menuContext;

        public static MenuSceneHost Current { get; private set; }

        /// <summary>Set at the end of Start — the readiness signal the
        /// ScreenRouter waits for before revealing the menu.</summary>
        public bool IsReady { get; private set; }

        public void Configure(GameServices services, ScreenRouter router)
        {
            _services = services;
            _router = router;
        }

        void Start()
        {
            Current = this;
            var canvasRoot = ResolveScene("CanvasRoot");
            var safeArea = (RectTransform)ResolveScene("CanvasRoot/SafeArea");
            if (canvasRoot == null || safeArea == null)
            {
                Debug.LogError("[QuietCamp] MainMenu scene lacks CanvasRoot/SafeArea.");
                return;
            }
            // Illustrated full-bleed backdrop behind SafeArea; the static art
            // replaces the old runtime 3D diorama.
            MenuArt.BuildBackground(canvasRoot).transform.SetAsFirstSibling();
            if (safeArea.GetComponent<SafeAreaFitter>() == null)
                safeArea.gameObject.AddComponent<SafeAreaFitter>();
            _screens = new MenuScreens(_services, safeArea);
            RegisterActions();
            _menuContext = _services.ContextStack.Push(new UiContextRegistration(
                "Menu", UiContextLayer.Global, 0, () => true,
                new UiActionId("qc.back")));
            IsReady = true;
        }

        // ─── Actions ─────────────────────────────────────────────────────────

        void RegisterActions()
        {
            var h = _services.Dispatch;
            var levels = LevelLoader.MvpLevelIds();
            _leases.Add(h.Register(new UiActionId("qc.continue"), () =>
            {
                var id = _services.Progression.ContinueTarget(levels) ?? levels[0];
                _router.GoToCamp(id);
                return UiActionResult.Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.play"), req =>
            {
                var id = req.Payload as string ?? req.TargetId;
                if (!string.IsNullOrEmpty(id)) _router.GoToCamp(id);
                return UiActionResult.Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.levels"), () => Show("Levels")));
            _leases.Add(h.Register(new UiActionId("qc.album"), () => Show("Album")));
            _leases.Add(h.Register(new UiActionId("qc.settings"), () => Show("Settings")));
            _leases.Add(h.Register(new UiActionId("qc.back"), () =>
            {
                if (_screens.Current != "Main") _screens.Back();
                return UiActionResult.Performed();
            }));
        }

        UiActionResult Show(string screen)
        {
            if (screen == "DemoComplete") _screens.ShowDemoComplete();
            else _screens.Show(screen);
            return UiActionResult.Performed();
        }

        void OnDestroy()
        {
            foreach (var l in _leases) l.Dispose();
            _leases.Clear();
            _menuContext?.Dispose();
            if (Current == this) Current = null;
        }

        // ─── Scene helpers ───────────────────────────────────────────────────

        static Transform ResolveScene(string path)
        {
            var parts = path.Split('/');
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = root.transform;
                var i = t.name == parts[0] ? 1 : 0;
                for (; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
                if (t != null) return t;
            }
            return null;
        }
    }
}
