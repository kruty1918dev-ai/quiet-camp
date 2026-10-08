using QuietCamp.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security;
using System.Text.RegularExpressions;
using UnityEngine.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting;
using UnityHTML.Runtime;
using DG.Tweening;

namespace QuietCamp.Presentation.UI
{
    /// <summary>One owned HTML document. Reconcile after input dispatch, never inside a JS callback.</summary>
    public sealed class HtmlSurface : MonoBehaviour
    {
        readonly UnityHtmlHost _host = new UnityHtmlHost();
        readonly HtmlCallbacks _callbacks = new HtmlCallbacks();
        GameServices _services;
        Func<string> _render;
        bool _dirty;
        string _css;
        string _lastHtml;
        Slider[] _sliders = Array.Empty<Slider>();
        int _themeFrames;
        Tween _settingsSave;
        bool _savePending;
        int _mountFailures;
        float _retryAt;
        public event Action Mounted;
        public event Action LayoutChanged;
        public UnityHtmlViewport Viewport => _host.Viewport;
        public bool IsMounted { get; private set; }
        public bool IsUsable => IsMounted || transform.Find("UiRecovery") != null;
        public string LastMountError { get; private set; }
        public bool ReducedMotion => _services?.ReducedMotion ?? true;
        public float MotionScale => _services?.MotionScale ?? 1f;
        public HtmlCallbacks Callbacks => _callbacks;
        /// <summary>Declarative motion bridge — ExitFinished lets close flows
        /// stay mounted until their out-animation settles.</summary>
        public IUnityHtmlMotion Motion => _host.Motion;
        /// <summary>Extra rules appended after the base stylesheet on every
        /// mount — used for layout constants computed in C# (e.g. safe-area
        /// insets of the owning canvas).</summary>
        public string ExtraCss;

        public static HtmlSurface Create(Transform parent, string name, GameServices services, Func<string> render)
        {
            var root = QcUi.Stretch(parent, name);
            var surface = root.gameObject.AddComponent<HtmlSurface>();
            surface._services = services;
            surface._host.ViewportChanged += _ => surface.OnLayoutChanged();
            surface._render = render;
            surface._css = Resources.Load<TextAsset>("QuietCamp/Html/Camp.css")?.text ?? "";
            // Quiet Camp documents use a small C# callback allow-list, not JS programs.
            // Keep CSS/Yoga/uGUI rendering, but avoid Android native VM/AOT binding.
            surface._host.NativeEventResolver = surface._callbacks.ResolveNativeEvent;
            if (services != null) services.Localization.LanguageChanged += surface.Refresh;
            surface.Refresh();
            return surface;
        }

        public RectTransform Element(string id)
        {
            foreach (var rect in GetComponentsInChildren<RectTransform>(true))
                if (rect.name.Contains("#" + id + ">")) return rect;
            return null;
        }

        public void Refresh() => _dirty = true;

        public void PlayControlFeedback(bool sound = false)
        {
            _services?.PlayHaptic(Kruty1918.Haptics.HapticCue.Selection);
            if (sound) _services?.Audio?.Play("sfx.toggle",
                new Kruty1918.Audio.AudioPlayOptions(volumeScale: .4f));
        }
        public void ScheduleSettingsSave()
        {
            _savePending = true;
            _settingsSave?.Kill();
            _settingsSave = DOVirtual.DelayedCall(.25f, FlushSettingsSave, true).SetLink(gameObject);
        }
        public void FlushSettingsSave()
        {
            _settingsSave?.Kill(); _settingsSave = null;
            if (!_savePending) return;
            _savePending = false;
            _services?.Save.Save();
        }
        void OnRectTransformDimensionsChange() { if (!IsMounted) Refresh(); }
        void LateUpdate()
        {
            if (_themeFrames > 0) { ApplyNativeControlTheme(); _themeFrames--; }
            if (!_dirty || _render == null) return;
            if (Time.unscaledTime < _retryAt) return;
            var root = (RectTransform)transform;
            // Android can report a zero-sized Canvas on its first frame, especially
            // without the Unity splash. Wait for the actual display/safe-area layout.
            if (root.rect.width < 1f || root.rect.height < 1f) return;
            _dirty = false;
            try { MountDocument(); }
            catch (Exception exception) { MountFailed(exception.ToString()); }
        }

        void MountDocument()
        {
            using var audit = PerformanceAudit.Measure("QC.HtmlSurface.MountDocument");
            _callbacks.Clear();
            var html = _render();
            html = CampMotion.Apply(html, MotionScale);
            _lastHtml = html;
            var settings = _services?.Settings;
            _host.Motion.ReducedMotion = settings?.reducedMotion ?? true;
            _host.ScrollSettings = UnityHtmlScrollSettings.Default
                .WithReducedMotion(settings?.reducedMotion ?? true)
                .WithWheelSensitivity((settings?.scrollSensitivity ?? 12f) / 12f);
            var scale = settings?.textScale ?? 1f;
            var css = Regex.Replace(_css, @"font-size:\s*([0-9.]+)px", match =>
                "font-size: " + HtmlUi.Number(float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * scale) + "px");
            css += "\ntext, button, label { font-size: " + HtmlUi.Number(28f * scale) + "px; }";
            if (settings?.highContrast == true)
                css += "\n.surface, .chip, .app button { background-color: #f6f0df; color: #243e35; } .app .primary { background-color: #243e35; color: #f6f0df; }";
            if (!string.IsNullOrEmpty(ExtraCss)) css += "\n" + ExtraCss;
            var result = _host.Mount((RectTransform)transform,
                new UnityHtmlDocument(html, css, name), new Dictionary<string, object>
                {
                    ["campUi"] = _callbacks,
                    ["campFont"] = Resources.Load<TMP_FontAsset>("Fonts/DejaVuSans SDF"),
                    ["moyvaFont"] = Resources.Load<TMP_FontAsset>("Fonts/DejaVuSans SDF")
                });
            if (!result.Succeeded) { MountFailed(result.ErrorMessage); return; }
            IsMounted = true;
            LastMountError = null;
            _mountFailures = 0;
            _retryAt = 0f;
            _sliders = GetComponentsInChildren<Slider>(true);
            ApplyNativeControlTheme();
            _themeFrames = 2;
            foreach (var control in GetComponentsInChildren<Selectable>(true))
            {
                // Scrims are lighting layers; scaling one exposes the edges.
                if (control is Scrollbar || control.name == "<button #dim>") continue;
                var feedback = control.GetComponent<CampControlFeedback>();
                if (feedback == null) feedback = control.gameObject.AddComponent<CampControlFeedback>();
                feedback.Configure(this, control);
            }
            Mounted?.Invoke();
        }

        void MountFailed(string error)
        {
            IsMounted = false;
            LastMountError = error;
            Debug.LogError("[QuietCamp HTML] " + error);
            // A failed mount must not emit Mounted or permanently clear the render
            // request. Retry transient display/font initialization on later frames.
            _mountFailures++;
            if (_mountFailures < 3)
            { _retryAt = Time.unscaledTime + .2f; _dirty = true; }
            else HtmlRecoveryControls.Show(this, _lastHtml, _callbacks);
        }

        // UnityHTML's slider internals are generated uGUI outside its CSS tree:
        // adapt colors/sprites here while HTML still owns layout and events.
        // uGUI Slider anchors the handle stretched vertically (y 0..1) on every
        // UpdateVisuals, so the handle is sized via vertical offsets relative to
        // the slide area — fixed 56 px instead of a stretched blob.
        const float Knob = 56f;
        const float Track = 18f;
        const float ValueWidth = 120f;
        const float ValueInset = ValueWidth + Knob * .5f + 16f;
        void ApplyNativeControlTheme()
        {
            using var audit = PerformanceAudit.Measure("QC.HtmlSurface.ApplyNativeControlTheme");
            foreach (var slider in _sliders)
            {
                if (slider == null) continue;
                var slide = slider.GetComponent<RectTransform>();
                var height = slide != null && slide.rect.height > 4f ? slide.rect.height : 68f;
                var inset = Mathf.Max(0f, (height - Track) * 0.5f);
                var knobPad = Mathf.Max(0f, (height - Knob) * 0.5f);
                // Flat-color bars: Kenney slide sprites ship a built-in round cap
                // that distorts when stretched; solid colors stay crisp on phones.
                if (slider.fillRect != null)
                {
                    var fill = slider.fillRect.GetComponent<Image>();
                    fill.sprite = null;
                    fill.color = CampUiTheme.Forest;
                    fill.raycastTarget = false;
                    slider.fillRect.offsetMin = new Vector2(10f, inset);
                    slider.fillRect.offsetMax = new Vector2(-ValueInset, -inset);
                }
                var background = slider.transform.Find("Background") as RectTransform;
                if (background != null)
                {
                    var bg = background.GetComponent<Image>();
                    bg.sprite = null;
                    bg.color = new Color(0.14f, 0.24f, 0.21f, 0.34f);
                    bg.raycastTarget = false;
                    background.offsetMin = new Vector2(10f, inset);
                    background.offsetMax = new Vector2(-ValueInset, -inset);
                }
                if (slider.handleRect != null)
                {
                    var image = slider.handleRect.GetComponent<Image>();
                    // Round knob — the Kenney pentagon reads as a "home" icon.
                    image.sprite = CampUiTheme.Circle;
                    image.type = Image.Type.Simple;
                    image.color = Color.white;
                    slider.handleRect.offsetMin = new Vector2(-Knob * 0.5f, knobPad);
                    slider.handleRect.offsetMax = new Vector2(Knob * 0.5f, -knobPad);
                }
                var handleArea = slider.transform.Find("Handle Slide Area") as RectTransform;
                if (handleArea != null)
                {
                    handleArea.offsetMin = new Vector2(Knob * 0.5f, 0f);
                    handleArea.offsetMax = new Vector2(-ValueInset, 0f);
                }
                var value = slider.GetComponentInChildren<TMP_Text>();
                if (value != null)
                {
                    value.color = MenuArt.Forest;
                    value.fontSize = 30f * (_services?.Settings.textScale ?? 1f);
                    // 58px strips "100%" to "100" — give the value room.
                    var vr = value.GetComponent<RectTransform>();
                    if (vr != null) vr.sizeDelta = new Vector2(ValueWidth, vr.sizeDelta.y);
                }
            }
        }

        void OnLayoutChanged()
        {
            using var audit = PerformanceAudit.Measure("QC.HtmlSurface.OnLayoutChanged");
            ApplyNativeControlTheme();
            LayoutChanged?.Invoke();
        }

        void OnDestroy()
        {
            FlushSettingsSave();
            if (_services != null) _services.Localization.LanguageChanged -= Refresh;
            _host.Dispose();
            _callbacks.Clear();
        }
    }

    // The sole script allow-list: markup receives callbacks, never mutable game services.
    [Preserve]
    public sealed class HtmlCallbacks
    {
        static readonly Regex NativeExpression = new Regex(
            @"^Globals\.campUi\.(Click|Number|Toggle)\('([^']*)'(?:,\s*event)?\)$");
        readonly Dictionary<string, Action> _clicks = new Dictionary<string, Action>();
        readonly Dictionary<string, Action<float>> _numbers = new Dictionary<string, Action<float>>();
        readonly Dictionary<string, Action<bool>> _toggles = new Dictionary<string, Action<bool>>();
        public void Clear() { _clicks.Clear(); _numbers.Clear(); _toggles.Clear(); }
        public void Bind(string id, Action callback) => _clicks[id] = callback;
        public void BindNumber(string id, Action<float> callback) => _numbers[id] = callback;
        public void BindToggle(string id, Action<bool> callback) => _toggles[id] = callback;
        [Preserve] public void Click(string id) { if (_clicks.TryGetValue(id, out var action)) action(); }
        [Preserve] public void Number(string id, float value) { if (_numbers.TryGetValue(id, out var action)) action(value); }
        [Preserve] public void Toggle(string id, bool value) { if (_toggles.TryGetValue(id, out var action)) action(value); }
        public Delegate ResolveNativeEvent(string expression)
        {
            var match = NativeExpression.Match(expression ?? "");
            if (!match.Success) throw new InvalidOperationException("Unsupported UI event: " + expression);
            var id = match.Groups[2].Value;
            switch (match.Groups[1].Value)
            {
                case "Click": return new Action<object, object>((_, __) => Click(id));
                case "Number": return new Action<object, object>((value, _) =>
                    Number(id, Convert.ToSingle(value, CultureInfo.InvariantCulture)));
                case "Toggle": return new Action<object, object>((value, _) =>
                    Toggle(id, Convert.ToBoolean(value, CultureInfo.InvariantCulture)));
                default: throw new InvalidOperationException(expression);
            }
        }
    }

    public static class HtmlUi
    {
        public static string Escape(string value) => SecurityElement.Escape(value ?? "") ?? "";
        public static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        public static string Text(string value, string cls = "") => "<text class=\"" + cls + "\">" + Escape(value) + "</text>";
        public static string Button(HtmlSurface surface, string id, string title, Action click, string cls = "", bool enabled = true, string tooltip = null)
        {
            surface.Callbacks.Bind(id, click);
            return "<button id=\"" + Escape(id) + "\" class=\"" + cls + "\" onClick=\"Globals.campUi.Click('" + Escape(id) + "')\"" +
                (enabled ? "" : " disabled=\"true\"") + (tooltip == null ? "" : " data-tooltip=\"" + Escape(tooltip) + "\"") + ">" + Escape(title) + "</button>";
        }
        public static string Template(string name, params string[] pairs)
        {
            var html = Resources.Load<TextAsset>("QuietCamp/Html/" + name).text;
            for (var i = 0; i < pairs.Length; i += 2) html = html.Replace("{{" + pairs[i] + "}}", pairs[i + 1]);
            return html;
        }
        /// <summary>CSS rule pinning .sheet-zone to the safe area inside a
        /// full-screen overlay surface — sheet content stays notch-safe while
        /// the scrim layer bleeds over the whole display.</summary>
        public static string SheetPadCss(RectTransform safeArea, RectTransform canvasRoot)
        {
            var c = new Vector3[4];
            safeArea.GetWorldCorners(c);
            var bl = canvasRoot.InverseTransformPoint(c[0]);
            var tr = canvasRoot.InverseTransformPoint(c[2]);
            var r = canvasRoot.rect;
            return ".sheet-zone { padding: " + Number(r.yMax - tr.y) + "px "
                + Number(r.xMax - tr.x) + "px " + Number(bl.y - r.yMin) + "px "
                + Number(bl.x - r.xMin) + "px; }";
        }
    }
}
