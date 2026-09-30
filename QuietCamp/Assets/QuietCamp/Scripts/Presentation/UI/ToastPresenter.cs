using System;
using DG.Tweening;
using Kruty1918.Notifications.API;
using Kruty1918.Notifications.Runtime;
using TMPro;
using UnityEngine;
namespace QuietCamp.Presentation.UI
{
    /// <summary>
    /// Toast-layer presenter for the notification service: one short message at
    /// a time above the bottom panel, never blocking board cells.
    /// </summary>
    public sealed class ToastPresenter : IGameplayNotificationPresenter
    {
        readonly RectTransform _layer;
        readonly Func<string, string> _translate;
        GameObject _current;
        Sequence _seq;

        public ToastPresenter(RectTransform layer, Func<string, string> translate)
        {
            _layer = layer;
            _translate = translate;
        }

        public void Present(GameplayNotificationRequest request, float holdDuration, Action completed)
        {
            ResetPresentation();
            var rt = QcUi.Anchor(_layer, "Toast",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-400f, 160f), new Vector2(400f, 250f));
            var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.14f, 0.11f, 0.09f, 0.92f);
            img.raycastTarget = false;
            var tmp = QcUi.PlainText(rt, _translate?.Invoke(request.Message) ?? request.Message,
                QcUi.TextSmall, TextAlignmentOptions.Center, QcUi.Cream);
            _current = rt.gameObject;
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            float hold = Mathf.Max(0.6f, holdDuration);
            _seq = DOTween.Sequence();
            _seq.Append(DOTween.To(() => group.alpha, v => group.alpha = v, 1f, 0.15f));
            _seq.AppendInterval(hold);
            _seq.Append(DOTween.To(() => group.alpha, v => group.alpha = v, 0f, 0.2f));
            _seq.OnComplete(() =>
            {
                if (_current != null) UnityEngine.Object.Destroy(_current);
                _current = null;
                completed?.Invoke();
            });
        }

        public void ResetPresentation()
        {
            _seq?.Kill();
            _seq = null;
            if (_current != null) UnityEngine.Object.Destroy(_current);
            _current = null;
        }
    }
}
