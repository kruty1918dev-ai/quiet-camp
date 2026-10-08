using System;
using UnityEngine;
using UnityEngine.UI;

namespace Kruty1918.AgentVerify
{
    /// <summary>
    /// Structured expectations for agents — each check returns a
    /// CheckResult {ok, detail} instead of throwing, so a whole verification
    /// script runs to completion and reports every outcome at once.
    /// </summary>
    [Serializable]
    public class CheckResult
    {
        public string name;
        public bool ok;
        public string detail;
    }

    public static class AgentCheck
    {
        /// <summary>
        /// Evaluate a named check:
        ///   exists | missing | active | inactive | visible | interactable | no-errors
        ///   text-contains   (target = Text/TMP object, contains = substring)
        ///   position        (target found, x/y/z = expected within tolerance)
        /// </summary>
        public static CheckResult Expect(string check, string target = null,
            string contains = null)
        {
            var r = new CheckResult { name = check + (target != null ? " " + target : "") };
            switch ((check ?? "").Trim())
            {
                case "exists":
                    r.ok = AgentProbe.Exists(target);
                    r.detail = r.ok ? "found" : "not found";
                    break;
                case "missing":
                    r.ok = !AgentProbe.Exists(target);
                    r.detail = r.ok ? "absent" : "still present";
                    break;
                case "active":
                    r.ok = AgentProbe.IsActive(target);
                    r.detail = r.ok ? "active" : "inactive or missing";
                    break;
                case "inactive":
                    r.ok = !AgentProbe.IsActive(target);
                    r.detail = r.ok ? "inactive" : "active";
                    break;
                case "visible":
                    r.ok = AgentProbe.IsVisible(target);
                    r.detail = r.ok ? "visible" : "not visible";
                    break;
                case "interactable":
                    var go = AgentProbe.Find(target);
                    var s = go != null ? go.GetComponent<Selectable>() : null;
                    r.ok = s != null && go.activeInHierarchy && s.interactable;
                    r.detail = r.ok ? "interactable" : "not interactable";
                    break;
                case "text-contains":
                    r = TextContains(target, contains);
                    r.name = $"text-contains {target}";
                    break;
                case "no-errors":
                    var n = AgentLog.ErrorCount();
                    r.ok = n == 0;
                    r.detail = n == 0 ? "clean" : $"{n} error entries";
                    break;
                default:
                    r.ok = false;
                    r.detail = $"unknown check '{check}'";
                    break;
            }
            return r;
        }

        static CheckResult TextContains(string target, string contains)
        {
            var r = new CheckResult();
            var go = AgentProbe.Find(target);
            if (go == null) { r.ok = false; r.detail = "not found"; return r; }
            string text = null;
            var legacy = go.GetComponent<Text>();
            if (legacy != null) text = legacy.text;
            if (text == null)
            {
                // TMP_Text and other text components via reflection — no hard package dep.
                foreach (var c in go.GetComponents<Component>())
                {
                    if (c == null) continue;
                    var n = c.GetType().Name;
                    if (!n.StartsWith("TMP") && !n.StartsWith("TextMeshPro")) continue;
                    var prop = c.GetType().GetProperty("text");
                    if (prop != null) { text = prop.GetValue(c) as string; break; }
                }
            }
            if (text == null) { r.ok = false; r.detail = "no text component"; return r; }
            r.ok = contains == null || text.Contains(contains);
            r.detail = r.ok ? "ok" : $"text '{Truncate(text)}' lacks '{contains}'";
            return r;
        }

        static string Truncate(string s) => s != null && s.Length > 60 ? s.Substring(0, 60) + "…" : s;
    }
}
