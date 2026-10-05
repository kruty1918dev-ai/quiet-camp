using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QuietCamp.Presentation.UI
{
    /// <summary>Shared, interruptible feedback. Input dispatch never waits for a tween.</summary>
    [DisallowMultipleComponent]
    public sealed class CampControlFeedback : MonoBehaviour, IPointerDownHandler,
        IPointerUpHandler, IPointerExitHandler, ICancelHandler
    {
        HtmlSurface _surface;
        Selectable _control;
        Button _button;
        Toggle _toggle;
        Slider _slider;
        Transform _visual;
        RectTransform _thumb;
        Tween _press, _switch;
        bool _bound;
        bool _layoutDirty;
        bool _lastOn;
        bool Reduced => _surface == null || _surface.ReducedMotion;
        float Pace => _surface != null ? _surface.MotionScale : 1f;
        public bool IsAnimating => (_press != null && _press.IsActive()) || (_switch != null && _switch.IsActive());

        public void Configure(HtmlSurface surface, Selectable control)
        {
            _surface = surface;
            _control = control;
            if (!_bound)
            {
                _button = control as Button;
                _toggle = control as Toggle;
                _slider = control as Slider;
                _visual = _slider != null && _slider.handleRect != null ? _slider.handleRect : transform;
                if (_button != null) _button.onClick.AddListener(Pulse);
                if (_toggle != null)
                {
                    if (transform.Find("Knob") == null) BuildSwitch();
                    _toggle.onValueChanged.AddListener(SwitchChanged);
                }
                _bound = true;
            }
            if (Reduced) Restore();
            if (_thumb != null && (_switch == null || !_switch.IsActive())) PoseSwitch(true);
        }

        void BuildSwitch()
        {
            // Keep the thumb visible in both states. The HTML :checked style
            // owns the track; uGUI's optional checkmark is not a switch thumb.
            _toggle.graphic = null;
            var go = new GameObject("CampSwitchThumb", typeof(RectTransform), typeof(Image));
            _thumb = (RectTransform)go.transform;
            _thumb.SetParent(transform, false);
            _thumb.anchorMin = _thumb.anchorMax = new Vector2(.5f, .5f);
            _thumb.sizeDelta = new Vector2(48f, 48f);
            var image = go.GetComponent<Image>();
            image.sprite = CampUiTheme.Circle;
            image.color = CampUiTheme.Cream;
            image.raycastTarget = false;
            _lastOn = _toggle.isOn;
            PoseSwitch(true);
        }

        void SwitchChanged(bool on)
        {
            _surface?.PlayControlFeedback(sound: true);
            _lastOn = on;
            PoseSwitch(Reduced);
        }

        void PoseSwitch(bool instant)
        {
            if (_thumb == null || _toggle == null) return;
            _lastOn = _toggle.isOn;
            var rect = (RectTransform)transform;
            var x = Mathf.Max(0f, (rect.rect.width - _thumb.sizeDelta.x) * .5f - 8f);
            var position = new Vector2(_lastOn ? x : -x, 0f);
            _switch?.Kill();
            if (instant) _thumb.anchoredPosition = position;
            else _switch = DOTween.To(() => _thumb.anchoredPosition,
                    value => _thumb.anchoredPosition = value, position, CampMotion.Change * Pace)
                .SetEase(CampMotion.Settle).SetUpdate(true).SetLink(gameObject);
        }

        void OnRectTransformDimensionsChange() => _layoutDirty = true;
        void LateUpdate()
        {
            if (Reduced && IsAnimating) Restore();
            if (!_layoutDirty) return;
            _layoutDirty = false;
            if (_thumb != null) PoseSwitch(true);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_control == null || !_control.IsInteractable() || _toggle != null) return;
            Scale(_slider != null ? 1.08f : .965f, CampMotion.Press);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_slider != null && _slider.IsInteractable()) _surface?.PlayControlFeedback(sound: true);
            Scale(1f, CampMotion.Release);
        }
        public void OnPointerExit(PointerEventData eventData) => Scale(1f, CampMotion.Release);
        public void OnCancel(BaseEventData eventData) => Restore();

        public void Pulse()
        {
            if (_control == null || !_control.IsInteractable() || _visual == null) return;
            _surface?.PlayControlFeedback();
            _press?.Kill();
            if (Reduced) { _visual.localScale = Vector3.one; return; }
            _press = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(_visual.DOScale(.965f, CampMotion.Press * .5f * Pace).SetEase(Ease.OutSine))
                .Append(_visual.DOScale(1f, CampMotion.Release * Pace).SetEase(CampMotion.Settle));
        }

        void Scale(float value, float duration)
        {
            if (_visual == null || _toggle != null) return;
            _press?.Kill();
            if (Reduced) { _visual.localScale = Vector3.one; return; }
            _press = _visual.DOScale(value, duration * Pace).SetEase(CampMotion.Settle)
                .SetUpdate(true).SetLink(gameObject);
        }

        void Restore()
        {
            _press?.Kill(); _press = null;
            _switch?.Kill(); _switch = null;
            if (_visual != null) _visual.localScale = Vector3.one;
            if (_thumb != null) PoseSwitch(true);
        }

        void OnDisable() => Restore();
        void OnDestroy()
        {
            _press?.Kill(); _switch?.Kill();
            if (_button != null) _button.onClick.RemoveListener(Pulse);
            if (_toggle != null) _toggle.onValueChanged.RemoveListener(SwitchChanged);
        }
    }
}
