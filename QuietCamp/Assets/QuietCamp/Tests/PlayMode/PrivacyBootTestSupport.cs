using System.Collections;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QuietCamp.Tests
{
    /// <summary>Explicit user action for existing QA scenarios, never a production bypass.</summary>
    internal static class PrivacyBootTestSupport
    {
        internal static IEnumerator EnterGame()
        {
            Assert.IsTrue(UnityEngine.Application.productName.Contains("QA"), "Boot fixtures must use isolated QA saves.");
            float deadline = Time.realtimeSinceStartup + 90;
            while (Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady != true && Time.realtimeSinceStartup < deadline)
            {
                var button = Find("boot-policy-ack");
                if (BootPrivacyPanel.Current?.Accepted == false && button != null && button.interactable) Tap(button);
                yield return null;
            }
            Assert.IsTrue(Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady == true, "Boot did not reach the menu after explicit QA acknowledgement.");
        }
        internal static Button Find(string id) => Object.FindObjectsByType<Button>().FirstOrDefault(b => b.gameObject.activeInHierarchy && b.name == "<button #" + id + ">");
        internal static void Tap(Button button)
        {
            Assert.NotNull(button); Assert.IsTrue(button.interactable);
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsTrue(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "Boot control is covered: " + button.name);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
    }
}
