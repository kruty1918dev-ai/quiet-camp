using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kruty1918.AgentVerify
{
    /// <summary>
    /// Read-only scene inspection for agents: find objects by name or
    /// hierarchy path ("Canvas/Panel/Play"), check activity/visibility, list
    /// interactables, and dump a compact JSON description of the loaded
    /// scene that fits in an agent's context window.
    /// </summary>
    public static class AgentProbe
    {
        /// <summary>Find by exact name or by root-anchored path ("Root/Child/Leaf").</summary>
        public static GameObject Find(string query)
        {
            if (string.IsNullOrEmpty(query)) return null;
            if (query.Contains("/"))
            {
                var node = FindByPath(query);
                if (node != null) return node;
            }
            foreach (var go in EnumerateAll())
                if (go.name == query) return go;
            return null;
        }

        static GameObject FindByPath(string path)
        {
            var parts = path.Split('/');
            foreach (var scene in LoadedScenes())
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != parts[0]) continue;
                var t = root.transform;
                var ok = true;
                for (var i = 1; i < parts.Length; i++)
                {
                    var next = t.Find(parts[i]);
                    if (next == null) { ok = false; break; }
                    t = next;
                }
                if (ok) return t.gameObject;
            }
            return null;
        }

        public static bool Exists(string query) => Find(query) != null;

        public static bool IsActive(string query)
        {
            var go = Find(query);
            return go != null && go.activeInHierarchy;
        }

        /// <summary>
        /// Visible = active in hierarchy AND actually rendering: a Renderer
        /// that is enabled and on-screen, or a UI Graphic with alpha &gt; 0.01.
        /// Objects with no visual component count as "not visible".
        /// </summary>
        public static bool IsVisible(string query)
        {
            var go = Find(query);
            if (go == null || !go.activeInHierarchy) return false;
            var g = go.GetComponent<Graphic>();
            if (g != null) return g.color.a > 0.01f;
            var r = go.GetComponent<Renderer>();
            if (r != null && !r.enabled) return false;
            var cam = Camera.main;
            if (cam != null && r != null)
            {
                var vp = cam.WorldToViewportPoint(r.bounds.center);
                if (vp.z < 0f || vp.x < -0.2f || vp.x > 1.2f || vp.y < -0.2f || vp.y > 1.2f) return false;
            }
            return r != null;
        }

        /// <summary>Screen-space center of the object's renderer/graphic (pixels).</summary>
        public static Vector2 ScreenPoint(string query)
        {
            var go = Find(query);
            if (go == null) return new Vector2(-1, -1);
            var rt = go.transform as RectTransform;
            if (rt != null)
            {
                var canvas = rt.GetComponentInParent<Canvas>();
                var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera : null;
                return RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
            }
            var r = go.GetComponent<Renderer>();
            var world = r != null ? r.bounds.center : go.transform.position;
            var c = Camera.main;
            return c != null ? (Vector2)c.WorldToScreenPoint(world) : new Vector2(-1, -1);
        }

        /// <summary>Names of all enabled interactable UI Selectables (buttons, toggles...).</summary>
        public static List<string> Interactables()
        {
            var list = new List<string>();
            foreach (var go in EnumerateAll())
            {
                var s = go.GetComponent<Selectable>();
                if (s != null && go.activeInHierarchy && s.interactable) list.Add(PathOf(go.transform));
            }
            return list;
        }

        // ---------- scene dump ----------

        [Serializable] public class ObjInfo
        {
            public string path;
            public bool active;
            public string[] components;
            public float x, y, z;
        }

        [Serializable] public class SceneDump
        {
            public string[] scenes;
            public int objectCount;
            public string[] cameras;
            public string[] interactables;
            public ObjInfo[] objects;
        }

        /// <summary>
        /// Compact JSON description of everything loaded: scene names, cameras,
        /// interactable UI, and one entry per active object (path, components,
        /// position) up to <paramref name="maxObjects"/>.
        /// </summary>
        public static string DescribeJson(int maxObjects = 400)
        {
            var dump = new SceneDump();
            var sceneNames = new List<string>();
            foreach (var s in LoadedScenes()) sceneNames.Add(s.name);
            dump.scenes = sceneNames.ToArray();

            var cams = new List<string>();
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                cams.Add(c.name);
            dump.cameras = cams.ToArray();
            dump.interactables = Interactables().ToArray();

            var objs = new List<ObjInfo>();
            var count = 0;
            foreach (var go in EnumerateAll())
            {
                count++;
                if (!go.activeInHierarchy) continue;
                if (objs.Count >= maxObjects) continue;
                var comps = go.GetComponents<Component>();
                var names = new List<string>();
                foreach (var c in comps) names.Add(c == null ? "<missing>" : c.GetType().Name);
                var p = go.transform.position;
                objs.Add(new ObjInfo
                {
                    path = PathOf(go.transform), active = true,
                    components = names.ToArray(), x = p.x, y = p.y, z = p.z
                });
            }
            dump.objectCount = count;
            dump.objects = objs.ToArray();
            return JsonUtility.ToJson(dump, true);
        }

        /// <summary>Short one-screen summary for quick agent sanity checks.</summary>
        public static string Summary()
        {
            var sb = new StringBuilder();
            sb.Append("scenes: ").Append(string.Join(", ", LoadedSceneNames())).Append('\n');
            var cam = Camera.main;
            sb.Append("mainCamera: ").Append(cam != null ? cam.name : "<none>").Append('\n');
            var inter = Interactables();
            sb.Append("interactables(").Append(inter.Count).Append("): ")
              .Append(string.Join(", ", inter.ToArray())).Append('\n');
            var n = 0;
            foreach (var _ in EnumerateAll()) n++;
            sb.Append("objects: ").Append(n).Append('\n');
            return sb.ToString();
        }

        // ---------- enumeration ----------

        public static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }

        static IEnumerable<string> LoadedSceneNames()
        {
            foreach (var s in LoadedScenes()) yield return s.name;
        }

        static IEnumerable<Scene> LoadedScenes()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded) yield return s;
            }
        }

        /// <summary>Every GameObject in every loaded scene (active or not).</summary>
        public static IEnumerable<GameObject> EnumerateAll()
        {
            foreach (var scene in LoadedScenes())
            foreach (var root in scene.GetRootGameObjects())
            foreach (var go in EnumerateTree(root.transform))
                yield return go;
        }

        static IEnumerable<GameObject> EnumerateTree(Transform t)
        {
            yield return t.gameObject;
            foreach (Transform child in t)
            foreach (var go in EnumerateTree(child))
                yield return go;
        }
    }
}
