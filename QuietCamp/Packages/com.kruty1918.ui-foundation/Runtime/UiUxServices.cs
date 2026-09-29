using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kruty1918.UiFoundation
{
    public readonly struct UiTooltipRequest
    {
        public UiTooltipRequest(
            object owner,
            string text,
            Vector2 screenPosition,
            RectTransform anchor = null,
            bool immediate = false)
        {
            Owner = owner;
            Text = text;
            ScreenPosition = screenPosition;
            Anchor = anchor;
            Immediate = immediate;
        }

        public object Owner { get; }
        public string Text { get; }
        public Vector2 ScreenPosition { get; }
        public RectTransform Anchor { get; }
        public bool Immediate { get; }
    }

    public interface IUiTooltipService
    {
        void Request(UiTooltipRequest request);
        void Hide(object owner);
        void Clear();
    }

    public interface IUiMotionService
    {
        bool ReducedMotion { get; set; }

        void SetPanelVisible(
            CanvasGroup canvasGroup,
            RectTransform panel,
            bool visible,
            float duration = 0.14f,
            Vector2? hiddenOffset = null);

        void Cancel(object target);
    }

    /// <summary>External source for the persisted reduced-motion setting.
    /// Implemented on the application side (player control settings) so this
    /// package stays free of project dependencies.</summary>
    public interface IUiReducedMotionSource
    {
        bool ReduceMotion { get; }
        void SetReduceMotion(bool value);
    }

    public sealed class UiMotionService : IUiMotionService
    {
        private readonly List<Motion> _motions = new List<Motion>();
        private readonly Dictionary<RectTransform, Vector2> _shownPositions = new Dictionary<RectTransform, Vector2>();
        private readonly IUiReducedMotionSource _reducedMotionSource;
        private bool _sessionReducedMotion;

        public UiMotionService(
            IUiReducedMotionSource reducedMotionSource = null)
            => _reducedMotionSource = reducedMotionSource;

        /// <summary>Single persistent reduced-motion gate — reads through to the
        /// player's control settings when bound, falls back to a session flag.
        /// Writing persists via the settings service (OnSettingsChanged fans out
        /// to camera/marker/HTML consumers).</summary>
        public bool ReducedMotion
        {
            get => _reducedMotionSource?.ReduceMotion ?? _sessionReducedMotion;
            set
            {
                if (_reducedMotionSource != null)
                    _reducedMotionSource.SetReduceMotion(value);
                else
                    _sessionReducedMotion = value;
            }
        }

        public void SetPanelVisible(
            CanvasGroup canvasGroup,
            RectTransform panel,
            bool visible,
            float duration = 0.14f,
            Vector2? hiddenOffset = null)
        {
            if (canvasGroup == null)
                throw new ArgumentNullException(nameof(canvasGroup));

            Cancel(canvasGroup);
            Vector2 offset = hiddenOffset ?? new Vector2(0f, -8f);
            Vector2 shownPosition = ResolveShownPosition(panel, offset);
            if (!visible && canvasGroup.gameObject.activeSelf == false)
                return;

            if (visible)
                canvasGroup.gameObject.SetActive(true);

            float effectiveDuration = ReducedMotion ? 0f : Mathf.Max(0f, duration);
            if (effectiveDuration <= 0f)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
                if (panel != null)
                    panel.anchoredPosition = shownPosition;
                if (!visible)
                    canvasGroup.gameObject.SetActive(false);
                return;
            }

            _motions.Add(new Motion
            {
                CanvasGroup = canvasGroup,
                Panel = panel,
                Elapsed = 0f,
                Duration = effectiveDuration,
                StartAlpha = canvasGroup.alpha,
                EndAlpha = visible ? 1f : 0f,
                StartPosition = panel != null
                    ? (visible ? shownPosition + offset : shownPosition)
                    : Vector2.zero,
                EndPosition = panel != null
                    ? (visible ? shownPosition : shownPosition + offset)
                    : Vector2.zero,
                VisibleAtEnd = visible,
            });

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            if (panel != null && visible)
                panel.anchoredPosition = shownPosition + offset;
        }

        public void Cancel(object target)
        {
            for (int index = _motions.Count - 1; index >= 0; index--)
            {
                Motion motion = _motions[index];
                if (ReferenceEquals(motion.CanvasGroup, target) || ReferenceEquals(motion.Panel, target))
                    _motions.RemoveAt(index);
            }
        }

        private Vector2 ResolveShownPosition(RectTransform panel, Vector2 offset)
        {
            if (panel == null)
                return Vector2.zero;

            if (_shownPositions.TryGetValue(panel, out var stored))
                return stored;

            var position = panel.anchoredPosition;
            if (!panel.gameObject.activeSelf)
                position -= offset;

            _shownPositions[panel] = position;
            return position;
        }

        public void Tick()
        {
            float deltaTime = Time.unscaledDeltaTime;
            for (int index = _motions.Count - 1; index >= 0; index--)
            {
                Motion motion = _motions[index];
                if (motion.CanvasGroup == null)
                {
                    _motions.RemoveAt(index);
                    continue;
                }

                motion.Elapsed += deltaTime;
                float normalized = Mathf.Clamp01(motion.Elapsed / motion.Duration);
                float eased = normalized * normalized * (3f - 2f * normalized);
                motion.CanvasGroup.alpha = Mathf.LerpUnclamped(motion.StartAlpha, motion.EndAlpha, eased);
                if (motion.Panel != null)
                    motion.Panel.anchoredPosition = Vector2.LerpUnclamped(motion.StartPosition, motion.EndPosition, eased);

                if (normalized < 1f)
                    continue;

                motion.CanvasGroup.interactable = motion.VisibleAtEnd;
                motion.CanvasGroup.blocksRaycasts = motion.VisibleAtEnd;
                if (!motion.VisibleAtEnd)
                    motion.CanvasGroup.gameObject.SetActive(false);
                _motions.RemoveAt(index);
            }
        }

        private sealed class Motion
        {
            public CanvasGroup CanvasGroup;
            public RectTransform Panel;
            public float Elapsed;
            public float Duration;
            public float StartAlpha;
            public float EndAlpha;
            public Vector2 StartPosition;
            public Vector2 EndPosition;
            public bool VisibleAtEnd;
        }
    }

    public sealed class UiTooltipService : IUiTooltipService, IDisposable
    {
        private const float DefaultDelay = 0.42f;

        private UiTooltipRequest _pending;
        private object _visibleOwner;
        private float _showAt;
        private bool _hasPending;
        private UiTooltipPresenter _presenter;

        public void Request(UiTooltipRequest request)
        {
            if (request.Owner == null || string.IsNullOrWhiteSpace(request.Text))
                return;

            bool replacingVisible = _visibleOwner != null && !ReferenceEquals(_visibleOwner, request.Owner);
            _pending = request;
            _hasPending = true;
            _showAt = Time.unscaledTime + (request.Immediate || replacingVisible ? 0f : DefaultDelay);
        }

        public void Hide(object owner)
        {
            if (owner == null)
                return;
            if (_hasPending && ReferenceEquals(_pending.Owner, owner))
                _hasPending = false;
            if (!ReferenceEquals(_visibleOwner, owner))
                return;

            _visibleOwner = null;
            _presenter?.Hide();
        }

        public void Clear()
        {
            _hasPending = false;
            _visibleOwner = null;
            _presenter?.Hide();
        }

        public void Tick()
        {
            if (!_hasPending || Time.unscaledTime < _showAt)
                return;

            _hasPending = false;
            _presenter ??= UiTooltipPresenter.Create();
            if (_presenter == null)
                return;

            _visibleOwner = _pending.Owner;
            _presenter.Show(_pending.Text, ResolveScreenPosition(_pending));
        }

        public void Dispose()
        {
            if (_presenter != null)
                UnityEngine.Object.Destroy(_presenter.gameObject);
            _presenter = null;
        }

        private static Vector2 ResolveScreenPosition(UiTooltipRequest request)
        {
            if (request.Anchor == null)
                return request.ScreenPosition;

            var corners = new Vector3[4];
            request.Anchor.GetWorldCorners(corners);
            Canvas canvas = request.Anchor.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return topRight + new Vector2(10f, 8f);
        }
    }

    public sealed class UiTooltipPresenter : MonoBehaviour
    {
        private const float MaxWidth = 360f;

        /// <summary>Applied to the runtime tooltip canvas so hosts control scaling policy.</summary>
        public static Action<Canvas, CanvasScaler> CanvasScaleApplier;
        private RectTransform _rect;
        private TMP_Text _label;

        public static UiTooltipPresenter Create()
        {
            TMP_FontAsset font = ResolveFont();
            if (font == null)
            {
                Debug.LogError("[UiFoundation] Cannot create runtime tooltip: no TMP default font asset is configured.");
                return null;
            }

            var canvasObject = new GameObject(
                "TooltipCanvas (Runtime)",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            DontDestroyOnLoad(canvasObject);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            canvasObject.GetComponent<GraphicRaycaster>().enabled = false;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            CanvasScaleApplier?.Invoke(canvas, scaler);

            var panel = new GameObject("Tooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ContentSizeFitter), typeof(UiTooltipPresenter));
            panel.transform.SetParent(canvasObject.transform, false);
            Image image = panel.GetComponent<Image>();
            image.color = new Color(0.105f, 0.09f, 0.075f, 0.97f);
            image.raycastTarget = false;
            ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var labelObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(panel.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 8f);
            labelRect.offsetMax = new Vector2(-12f, -8f);
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 14f;
            label.color = new Color(0.96f, 0.91f, 0.82f);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;

            UiTooltipPresenter presenter = panel.GetComponent<UiTooltipPresenter>();
            presenter._rect = (RectTransform)panel.transform;
            presenter._label = label;
            panel.SetActive(false);
            return presenter;
        }

        public void Show(string text, Vector2 screenPosition)
        {
            gameObject.SetActive(true);
            _label.text = text;
            _label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, MaxWidth - 24f);
            Canvas.ForceUpdateCanvases();
            float width = Mathf.Min(MaxWidth, _label.preferredWidth + 24f);
            float height = _label.GetPreferredValues(text, width - 24f, 0f).y + 16f;
            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            float x = Mathf.Clamp(screenPosition.x, 8f, Screen.width - width - 8f);
            float y = Mathf.Clamp(screenPosition.y, 8f, Screen.height - height - 8f);
            _rect.position = new Vector3(x, y, 0f);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private static TMP_FontAsset ResolveFont()
        {
            if (TMP_Settings.defaultFontAsset != null)
                return TMP_Settings.defaultFontAsset;

            TMP_FontAsset[] loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            return loaded.Length > 0 ? loaded[0] : null;
        }
    }
}
