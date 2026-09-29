using System;
using System.Collections.Generic;
using Kruty1918.Notifications.API;

namespace Kruty1918.Notifications.Runtime
{
    public sealed class GameplayNotificationService : IGameplayNotificationService, IDisposable
    {
        private readonly Queue<GameplayNotificationRequest> _queue = new();
        private readonly HashSet<string> _dedupKeys = new(StringComparer.Ordinal);
        private readonly GameplayNotificationSettings _settings;
        private readonly IGameplayNotificationPresenter _presenter;

        private GameplayNotificationRequest _activeRequest;
        private string _activeDedupKey;
        private bool _hasActive;

        public GameplayNotificationService(
            GameplayNotificationSettings settings,
            IGameplayNotificationPresenter presenter = null)
        {
            _settings = settings ?? new GameplayNotificationSettings();
            _settings.Normalize();
            _presenter = presenter;
        }

        internal int QueuedCount => _queue.Count;
        internal int PendingCount => _queue.Count + (_hasActive ? 1 : 0);
        internal bool HasActive => _hasActive;
        internal string ActiveMessage => _hasActive ? _activeRequest.Message : null;

        public void Show(
            string message,
            GameplayNotificationKind kind = GameplayNotificationKind.Info,
            float? holdDuration = null,
            string dedupKey = null)
        {
            Show(new GameplayNotificationRequest(
                message,
                kind,
                holdDuration,
                dedupKey));
        }

        public void Show(GameplayNotificationRequest request)
        {
            TryShow(request);
        }

        public void Clear()
        {
            _queue.Clear();
            _dedupKeys.Clear();
            _activeDedupKey = null;
            _hasActive = false;
            _presenter?.ResetPresentation();
        }

        public void Dispose()
        {
            Clear();
        }

        internal bool TryShow(GameplayNotificationRequest request)
        {
            var normalized = request.Normalized();
            if (string.IsNullOrWhiteSpace(normalized.Message))
                return false;

            if (normalized.HasDedupKey && _dedupKeys.Contains(normalized.DedupKey))
                return false;

            if (PendingCount >= _settings.MaxQueueSize)
                return false;

            if (normalized.HasDedupKey)
                _dedupKeys.Add(normalized.DedupKey);

            if (!_hasActive)
            {
                Activate(normalized);
                return true;
            }

            _queue.Enqueue(normalized);
            return true;
        }

        internal void CompleteActiveForTests()
        {
            OnActiveCompleted();
        }

        private void Activate(GameplayNotificationRequest request)
        {
            _activeRequest = request;
            _activeDedupKey = request.HasDedupKey ? request.DedupKey : null;
            _hasActive = true;
            GameplayNotificationStream.Publish(request);

            if (_presenter != null)
            {
                _presenter.Present(
                    request,
                    _settings.ResolveHoldDuration(request.HoldDuration),
                    OnActiveCompleted);
            }
            else
            {
                OnActiveCompleted();
            }
        }

        private void OnActiveCompleted()
        {
            if (!_hasActive)
                return;

            if (!string.IsNullOrWhiteSpace(_activeDedupKey))
                _dedupKeys.Remove(_activeDedupKey);

            _activeDedupKey = null;
            _hasActive = false;
            _activeRequest = default;

            if (_queue.Count > 0)
                Activate(_queue.Dequeue());
        }
    }
}
