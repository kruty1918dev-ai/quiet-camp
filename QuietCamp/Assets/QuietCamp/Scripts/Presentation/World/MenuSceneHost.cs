using System.Collections.Generic;
using Kruty1918.UIActions.API;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Main-menu scene host: builds a small non-interactive camp diorama behind
    /// the menu canvas, wires Menu action ids (continue/play/levels/album/
    /// settings/back) into the shared router and owns the Menu UI context.
    /// </summary>
    public sealed class MenuSceneHost : MonoBehaviour
    {
        GameServices _services;
        ScreenRouter _router;
        MenuScreens _screens;
        readonly List<System.IDisposable> _leases = new List<System.IDisposable>();
        System.IDisposable _menuContext;
        Transform _diorama;

        public void Configure(GameServices services, ScreenRouter router)
        {
            _services = services;
            _router = router;
        }

        void Start()
        {
            var safeArea = (RectTransform)ResolveScene("CanvasRoot/SafeArea");
            if (safeArea == null)
            {
                Debug.LogError("[QuietCamp] MainMenu scene lacks CanvasRoot/SafeArea.");
                return;
            }
            _screens = new MenuScreens(_services, safeArea);
            BuildDiorama();
            RegisterActions();
            _menuContext = _services.ContextStack.Push(new UiContextRegistration(
                "Menu", UiContextLayer.Global, 0, () => true,
                new UiActionId("qc.back")));
        }

        // ─── Diorama ─────────────────────────────────────────────────────────

        void BuildDiorama()
        {
            _diorama = new GameObject("MenuDiorama").transform;
            var level = TryLoad("QC_TEST");
            if (level == null) return;
            var catalog = _services.Assets;
            if (catalog == null) return;

            // Ground slab, a few trees/rocks and the witness tents — no grid,
            // no overlays, no input.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(_diorama, false);
            ground.transform.localScale = new Vector3(9f, 0.3f, 9f);
            ground.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            TintObject(ground, new Color(0.55f, 0.66f, 0.47f));
            var groundCol = ground.GetComponent<Collider>();
            if (groundCol != null) Destroy(groundCol);

            Spawn("tree_pineRoundA", new Vector3(-3.2f, 0f, 1.6f), 20);
            Spawn("tree_default", new Vector3(3.4f, 0f, -1.2f), 200);
            Spawn("stone_largeA", new Vector3(2.9f, 0f, 2.4f), 90);
            Spawn("stump_round", new Vector3(-2.6f, 0f, -2.8f), 0);
            var fire = Spawn("campfire_stones", new Vector3(0.4f, 0f, -0.4f), 0);
            if (fire != null)
            {
                Spawn("log_stack", Vector3.zero, 45, fire.transform);
                FireFx.Create(fire.transform);
            }

            foreach (var p in level.witness)
            {
                var guest = System.Array.Find(level.guests, g => g.id == p.guestId);
                if (guest == null) continue;
                var pos = new Vector3(
                    p.x + 1.0f - level.width / 2f, 0.02f, p.z + 1.0f - level.height / 2f);
                Spawn(guest.assetId, pos, p.rotation * 90);
            }

            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 7.5f, -7.5f);
                camera.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
                camera.orthographic = true;
                camera.orthographicSize = 4.6f;
            }
            var lighting = new GameObject("MenuLight").transform;
            lighting.SetParent(_diorama, false);
            var light = lighting.gameObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.824f, 0.608f);
            light.intensity = 0.75f;
            lighting.eulerAngles = new Vector3(40f, -35f, 0f);
        }

        LevelData TryLoad(string id)
        {
            try { return LevelLoader.Load(id); }
            catch (System.Exception e) { Debug.LogError($"[QuietCamp] {e.Message}"); return null; }
        }

        GameObject Spawn(string assetId, Vector3 pos, int yaw, Transform parent = null)
        {
            if (!_services.Assets.TryGet(assetId, out var entry) || entry.prefab == null)
                return null;
            var go = Instantiate(entry.prefab, parent ?? _diorama);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
            SetLayerRecursively(go, BoardRenderer.DecorLayer);
            foreach (var c in go.GetComponentsInChildren<Collider>())
                c.enabled = false;
            return go;
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        static void TintObject(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var mat = renderer.material;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
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
            if (_diorama != null) Destroy(_diorama.gameObject);
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
