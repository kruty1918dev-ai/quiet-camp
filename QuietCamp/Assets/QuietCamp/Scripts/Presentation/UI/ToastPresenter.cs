using System;
using DG.Tweening;
using Kruty1918.Notifications.API;
using Kruty1918.Notifications.Runtime;
using UnityEngine;

namespace QuietCamp.Presentation.UI
{
    /// <summary>Non-interactive HTML notification, above the board and below the transition overlay.</summary>
    public sealed class ToastPresenter : IGameplayNotificationPresenter
    {
        readonly RectTransform _layer;
        readonly Func<string, string> _translate;
        readonly Func<GameServices> _services;
        HtmlSurface _current;
        Tween _timer;
        bool _exiting;
        Action _completed;
        public ToastPresenter(RectTransform layer, Func<string, string> translate, Func<GameServices> services = null)
        { _layer = layer; _translate = translate; _services = services; }
        public void Present(GameplayNotificationRequest request, float holdDuration, Action completed)
        {
            ResetPresentation();
            _completed = completed;
            var text = _translate?.Invoke(request.Message) ?? request.Message;
            _current = HtmlSurface.Create(_layer, "ToastHtml", _services?.Invoke(),
                () => "<view class=\"app\"><view id=\"notification\" class=\"toast\" data-motion-role=\"toast\""
                    + (_exiting ? " data-motion=\"exit\"" : "") + ">" + HtmlUi.Text(text) + "</view></view>");
            _current.Motion.ExitFinished += OnExitFinished;
            var group = _current.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            _timer = DOVirtual.DelayedCall(Mathf.Max(.6f, holdDuration), () =>
            {
                _exiting = true;
                _current?.Refresh();
            }, true);
        }
        void OnExitFinished(string id)
        {
            if (id != "notification" || !_exiting) return;
            var completed = _completed;
            ResetPresentation();
            completed?.Invoke();
        }
        public void ResetPresentation()
        {
            _timer?.Kill(); _timer = null;
            _exiting = false; _completed = null;
            if (_current != null)
            {
                _current.Motion.ExitFinished -= OnExitFinished;
                UnityEngine.Object.Destroy(_current.gameObject);
            }
            _current = null;
        }
    }
}
