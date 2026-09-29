using System;
using Kruty1918.Notifications.API;
using UnityEngine;

#if DOTWEEN_ENABLED
using DG.Tweening;
#else
using System.Collections;
#endif

namespace Kruty1918.Notifications.Runtime
{
    public sealed class GameplayNotificationPresenter : MonoBehaviour, IGameplayNotificationPresenter
    {
        private GameplayNotificationSettings _settings;
        private GameplayNotificationView _view;
        private Action _completion;

#if DOTWEEN_ENABLED
        private Sequence _sequence;
#else
        private Coroutine _routine;
#endif

        /// <summary>
        /// Supplies presenter settings. Call from the game's composition root when
        /// the presenter component is created (e.g. via a DI factory).
        /// </summary>
        public void Construct(GameplayNotificationSettings settings)
        {
            _settings = settings ?? new GameplayNotificationSettings();
            _settings.Normalize();
        }

        public void Present(
            GameplayNotificationRequest request,
            float holdDuration,
            Action completed)
        {
            _settings ??= new GameplayNotificationSettings();
            _settings.Normalize();
            EnsureView();
            ResetAnimationOnly();

            _completion = completed;
            _view.SetMessage(request.Message, request.Kind);
            _view.ApplyState(GameplayNotificationAnimation.Evaluate(_settings, 0f, holdDuration));
            gameObject.SetActive(true);

#if DOTWEEN_ENABLED
            _sequence = DOTween.Sequence()
                .SetUpdate(true)
                .Append(_view.RectTransform.DOAnchorPosY(_settings.VisibleOffsetY, _settings.EnterDuration).SetEase(Ease.Linear))
                .Join(_view.CanvasGroup.DOFade(1f, _settings.EnterDuration).SetEase(Ease.Linear))
                .AppendInterval(Mathf.Max(0.01f, holdDuration))
                .Append(_view.RectTransform.DOAnchorPosY(_settings.HiddenOffsetY, _settings.ExitDuration).SetEase(Ease.Linear))
                .Join(_view.CanvasGroup.DOFade(0f, _settings.ExitDuration).SetEase(Ease.Linear))
                .OnComplete(Complete);
#else
            _routine = StartCoroutine(PlayRoutine(Mathf.Max(0.01f, holdDuration)));
#endif
        }

        public void ResetPresentation()
        {
            ResetAnimationOnly();
            _completion = null;
            if (_view != null)
                _view.SetHidden();
        }

        private void OnDestroy()
        {
            ResetPresentation();
        }

        private void Complete()
        {
            Action completion = _completion;
            _completion = null;
            if (_view != null)
                _view.SetHidden();
            completion?.Invoke();
        }

        private void ResetAnimationOnly()
        {
#if DOTWEEN_ENABLED
            if (_sequence != null && _sequence.IsActive())
                _sequence.Kill(false);
            _sequence = null;
#else
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
#endif
        }

        private void EnsureView()
        {
            if (_view != null)
                return;

            Canvas canvas = FindPrimaryCanvas();
            Transform parent = canvas != null
                ? canvas.transform
                : transform;
            _view = GameplayNotificationView.Create(parent);
        }

        private static Canvas FindPrimaryCanvas()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(
                FindObjectsInactive.Include);

            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].name == "Canvas")
                    return canvases[i];
            }

            return canvases.Length > 0 ? canvases[0] : null;
        }

#if !DOTWEEN_ENABLED
        private IEnumerator PlayRoutine(float holdDuration)
        {
            float elapsed = 0f;
            float total = _settings.EnterDuration + holdDuration + _settings.ExitDuration;

            while (elapsed < total)
            {
                _view.ApplyState(GameplayNotificationAnimation.Evaluate(_settings, elapsed, holdDuration));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _view.ApplyState(GameplayNotificationAnimation.Evaluate(_settings, total, holdDuration));
            _routine = null;
            Complete();
        }
#endif
    }
}
