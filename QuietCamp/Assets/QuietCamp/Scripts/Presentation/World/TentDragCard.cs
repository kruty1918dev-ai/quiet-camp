using System;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation.World
{
    /// <summary>Pooled thumbnail before the aim reaches the board. Never adds a rendering camera.</summary>
    public sealed class TentDragCard : MonoBehaviour
    {
        GameObject _card;
        RectTransform _rect;
        Image _picture;
        Canvas _canvas;
        Vector2 _position, _target, _velocity;
        Func<bool> _reduced;
        public void Show(AssetCatalog assets, string asset, Camera world, Vector2 screen, Func<bool> reduced)
        {
            _reduced = reduced;
            if (_card == null)
            {
                _canvas = FindFirstObjectByType<Canvas>();
                if (_canvas == null) return;
                _card = new GameObject("HeldGuestCard", typeof(RectTransform), typeof(Image));
                _rect = (RectTransform)_card.transform; _rect.SetParent(_canvas.transform, false);
                var image = _card.GetComponent<Image>(); image.sprite = CampUiTheme.Card; image.type = Image.Type.Sliced;
                image.color = new Color(.97f,.94f,.84f,.96f); image.raycastTarget = false;
                var shadow = _card.AddComponent<Shadow>(); shadow.effectColor = new Color(.08f,.15f,.1f,.22f);
                shadow.effectDistance = new Vector2(0,-5);
                var picture = new GameObject("Tent", typeof(RectTransform), typeof(Image)); picture.transform.SetParent(_card.transform,false);
                var r = (RectTransform)picture.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
                r.offsetMin = new Vector2(8,8); r.offsetMax = new Vector2(-8,-8);
                _picture = picture.GetComponent<Image>(); _picture.raycastTarget = false; _picture.preserveAspect = true;
            }
            _picture.sprite = Resources.Load<Sprite>("QuietCamp/UI/Tents/" + asset)
                ?? Resources.Load<Sprite>("QuietCamp/UI/Atlas/hex_tent");
            float dp = PlacementTargetProjector.ScreenDpScale();
            _rect.sizeDelta = Vector2.one * (72f * dp / Mathf.Max(.01f, _canvas.scaleFactor));
            _position = _target = screen; _velocity = Vector2.zero; Position(); SetVisible(true);
        }
        public void Follow(Vector2 aim) { _target = aim; }
        public void SetVisible(bool visible) { if (_card != null) _card.SetActive(visible); }
        void LateUpdate()
        {
            if (_card == null || !_card.activeSelf) return;
            if (_reduced?.Invoke() == true) { _position = _target; _velocity = Vector2.zero; }
            else _position = Vector2.SmoothDamp(_position,_target,ref _velocity,.075f,4000,Mathf.Min(.04f,Time.unscaledDeltaTime));
            Position();
        }
        void Position()
        {
            if (_rect == null) return;
            var safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0,0,Screen.width,Screen.height);
            var half = _rect.sizeDelta * _canvas.scaleFactor * .5f;
            var position = new Vector2(Mathf.Clamp(_position.x,safe.xMin+half.x,safe.xMax-half.x),
                Mathf.Clamp(_position.y,safe.yMin+half.y,safe.yMax-half.y));
            RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)_rect.parent,position,
                _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,out var point);
            _rect.position = point;
        }
        public void Hide() => SetVisible(false);
        void OnDestroy() { if (_card != null) Destroy(_card); }
    }
}
