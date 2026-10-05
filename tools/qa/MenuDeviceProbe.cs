// Injected only into the isolated device QA project. Not a shipped game asset.
using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public sealed class MenuDeviceProbe : MonoBehaviour
{
    [Serializable] public sealed class Control { public string id, kind; public float value; public bool on; public int x, y, width, height; public bool enabled, visible; }
    [Serializable] public sealed class Target { public int x, y; }
    [Serializable] public sealed class Snapshot
    {
        public string scene, language; public int width, height; public float textScale, master;
        public int orientation; public bool reducedMotion, highContrast, haptics;
        public bool privacyPending, privacyDeclined, privacyAcknowledged, startupReady;
        public int placements, boardRevision; public bool canUndo, canRedo, gameplayActive;
        public List<Target> placementTargets = new List<Target>();
        public List<Control> controls = new List<Control>(); public List<string> headings = new List<string>();
    }
    float _next; string _previous;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (!UnityEngine.Application.identifier.EndsWith(".uiqa", StringComparison.Ordinal)) return;
        var go = new GameObject("MenuDeviceProbe"); DontDestroyOnLoad(go); go.AddComponent<MenuDeviceProbe>();
    }
    void LateUpdate()
    {
        if (Time.unscaledTime < _next) return; _next = Time.unscaledTime + .25f;
        var services = QuietCampBootstrap.ServicesRef;
        var state = new Snapshot { scene = SceneManager.GetActiveScene().name, width = Screen.width, height = Screen.height };
        state.privacyPending = BootPrivacyPanel.Current != null && !BootPrivacyPanel.Current.Accepted;
        state.privacyDeclined = BootPrivacyPanel.Current != null && BootPrivacyPanel.Current.Declined;
        state.startupReady = FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady ?? false;
        if (services != null)
        {
            state.language = services.Localization.CurrentLanguageId; state.textScale = services.Settings.textScale;
            state.orientation = services.Settings.orientation; state.master = services.Settings.master;
            state.reducedMotion = services.Settings.reducedMotion; state.highContrast = services.Settings.highContrast;
            state.haptics = services.Settings.haptics;
            state.privacyAcknowledged = !string.IsNullOrEmpty(services.Settings.privacyAcknowledgementHash);
        }
        var session = CampSceneHost.Current?.Session; var camera = Camera.main;
        state.gameplayActive = CampSceneHost.Current?.GameplayActive ?? false;
        if (session != null && camera != null)
        {
            state.placements = session.State.Count; state.boardRevision = session.State.Revision;
            state.canUndo = session.CanUndo; state.canRedo = session.CanRedo;
            if (session.SelectedGuestId != null)
                for (int z = 0; z < session.Level.height && state.placementTargets.Count < 8; z++)
                    for (int x = 0; x < session.Level.width && state.placementTargets.Count < 8; x++)
                    {
                        var preview = session.Preview(QuietCamp.Application.PlacementCommand.Place(session.SelectedGuestId, x, z, 0));
                        if (!preview.CanCommit) continue;
                        var point = camera.WorldToScreenPoint(BoardMath.CellCenter(session.Level, x, z));
                        if (point.z > 0 && point.x > 0 && point.x < Screen.width && point.y > 0 && point.y < Screen.height)
                            state.placementTargets.Add(new Target { x = Mathf.RoundToInt(point.x), y = Mathf.RoundToInt(Screen.height - point.y) });
                    }
        }
        foreach (var button in FindObjectsByType<Selectable>().OrderBy(b => b.name, StringComparer.Ordinal))
        {
            var marker = button.name.IndexOf("#", StringComparison.Ordinal);
            if (marker < 0 || !button.gameObject.activeInHierarchy) continue;
            var rect = (RectTransform)button.transform; var centre = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var control = new Control { id = button.name.Substring(marker + 1).TrimEnd('>'), kind = button.GetType().Name,
                value = button is Slider slider ? slider.value : 0, on = button is Toggle toggle && toggle.isOn, x = Mathf.RoundToInt(centre.x), y = Mathf.RoundToInt(Screen.height - centre.y),
                width = Mathf.RoundToInt(corners[2].x - corners[0].x), height = Mathf.RoundToInt(corners[2].y - corners[0].y),
                enabled = button.interactable, visible = centre.x > 0 && centre.x < Screen.width && centre.y > 0 && centre.y < Screen.height };
            foreach (var group in button.GetComponentsInParent<CanvasGroup>())
                if (!group.blocksRaycasts || group.alpha < .9f) control.visible = false;
            foreach (var scroll in button.GetComponentsInParent<ScrollRect>())
                if (scroll.viewport != null && !RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, centre)) control.visible = false;
            state.controls.Add(control);
        }
        // Capture only settings/menu headings, never guest names or save content.
        foreach (var surface in FindObjectsByType<HtmlSurface>())
            foreach (var text in surface.GetComponentsInChildren<TMPro.TMP_Text>())
                if (text.name.Contains("settings-title") || text.name.Contains("full-title")) state.headings.Add(text.text);
        var json = JsonUtility.ToJson(state);
        if (json != _previous)
        {
            _previous = json;
            // Android truncates long Unity messages. Frame-tagged chunks keep
            // all 30 roadmap nodes readable without touching game behavior.
            const int chunkSize = 1400;
            var count = (json.Length + chunkSize - 1) / chunkSize;
            for (var offset = 0; offset < json.Length; offset += chunkSize)
                Debug.Log("[MenuDeviceQA:" + Time.frameCount + ":" + offset / chunkSize + ":" + count + "] "
                    + json.Substring(offset, Mathf.Min(chunkSize, json.Length - offset)));
        }
    }
}
