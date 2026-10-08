using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kruty1918.AgentVerify
{
    /// <summary>
    /// Simulated user input for agents: click a named UI element, tap a
    /// screen position through the real EventSystem pipeline (down → click →
    /// up), or SendMessage any component method as an escape hatch.
    /// Works headless — no OS-level input required.
    /// </summary>
    public static class AgentInput
    {
        /// <summary>Click a UI element found by name/path. Returns false if not found or not interactable.</summary>
        public static bool Click(string query)
        {
            var go = AgentProbe.Find(query);
            if (go == null) return false;
            var s = go.GetComponent<Selectable>();
            if (s != null && (!go.activeInHierarchy || !s.interactable)) return false;
            var ped = new PointerEventData(CurrentES())
            {
                position = AgentProbe.ScreenPoint(AgentProbe.PathOf(go.transform))
            };
            ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerUpHandler);
            return true;
        }

        /// <summary>EventSystem.current, or any EventSystem in the scene (edit-mode safe).</summary>
        static EventSystem CurrentES()
        {
            var es = EventSystem.current;
            if (es == null) es = Object.FindAnyObjectByType<EventSystem>();
            return es;
        }

        /// <summary>
        /// Tap at a screen-space pixel position through the real EventSystem
        /// raycast — drives whatever UI or world object sits there.
        /// </summary>
        public static bool Tap(Vector2 screenPos)
        {
            var es = CurrentES();
            if (es == null) return false;
            var ped = new PointerEventData(es) { position = screenPos };
            var hits = new List<RaycastResult>();
            es.RaycastAll(ped, hits);
            if (hits.Count == 0) return false;
            var target = hits[0].gameObject;
            ped.pointerCurrentRaycast = hits[0];
            ExecuteEvents.Execute(target, ped, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, ped, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(target, ped, ExecuteEvents.pointerUpHandler);
            return true;
        }

        /// <summary>
        /// Drag from one screen point to another over <paramref name="steps"/> synthetic moves —
        /// fires beginDrag/drag/endDrag on the hit target.
        /// </summary>
        public static bool Drag(Vector2 from, Vector2 to, int steps = 8)
        {
            var es = CurrentES();
            if (es == null) return false;
            var ped = new PointerEventData(es) { position = from };
            var hits = new List<RaycastResult>();
            es.RaycastAll(ped, hits);
            if (hits.Count == 0) return false;
            var target = hits[0].gameObject;
            ped.pointerCurrentRaycast = hits[0];
            ExecuteEvents.Execute(target, ped, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, ped, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(target, ped, ExecuteEvents.beginDragHandler);
            for (var i = 1; i <= steps; i++)
            {
                ped.position = Vector2.Lerp(from, to, i / (float)steps);
                ExecuteEvents.Execute(target, ped, ExecuteEvents.dragHandler);
            }
            ExecuteEvents.Execute(target, ped, ExecuteEvents.endDragHandler);
            ExecuteEvents.Execute(target, ped, ExecuteEvents.pointerUpHandler);
            return true;
        }

        /// <summary>Escape hatch: SendMessage(method) on a named object. Returns false if not found.</summary>
        public static bool Invoke(string query, string method)
        {
            var go = AgentProbe.Find(query);
            if (go == null || string.IsNullOrEmpty(method)) return false;
            go.SendMessage(method, SendMessageOptions.DontRequireReceiver);
            return true;
        }
    }
}
