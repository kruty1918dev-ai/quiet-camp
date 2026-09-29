using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Kruty1918.UiFoundation
{
    public interface ISceneTransitionService
    {
        Task CoverAsync(CancellationToken ct = default);
        Task RevealAsync(CancellationToken ct = default);

        /// <summary>True while a cover→load→reveal transition operation is owned
        /// by a lease. Bare CoverAsync/RevealAsync calls still work — they are
        /// serialized behind any in-flight animation.</summary>
        bool IsTransitionActive { get; }

        /// <summary>
        /// Takes exclusive ownership of a full cover→load→reveal transition.
        /// Returns null when another transition already runs (refuse) — the
        /// caller may then await <see cref="WaitForActiveTransitionAsync"/> to
        /// join it before retrying. Dispose the lease to release ownership.
        /// </summary>
        ISceneTransitionLease TryBeginTransition();

        /// <summary>Completes when the currently active transition operation
        /// (if any) is released — the join half of the nested-call contract.</summary>
        Task WaitForActiveTransitionAsync(CancellationToken ct = default);
    }

    /// <summary>Exclusive ownership of one cover→load→reveal sequence.</summary>
    public interface ISceneTransitionLease : IDisposable
    {
        Task CoverAsync(CancellationToken ct = default);
        Task RevealAsync(CancellationToken ct = default);
    }

    public sealed class SceneTransitionService : ISceneTransitionService
    {
        private const int StripeCount = 7;
        private const float Duration = 0.32f;

        // Instance seam for tests: 0 makes animations complete synchronously
        // without needing a frame loop.
        internal float AnimationDurationSeconds = Duration;

        private CanvasGroup _group;
        private RectTransform[] _stripes;

        private readonly object _animationSync = new object();
        private Task _animationTail = Task.CompletedTask;
        private bool _covered;

        private readonly object _leaseSync = new object();
        private SceneTransitionLease _activeLease;
        private TaskCompletionSource<bool> _leaseCompletion;

        public bool IsTransitionActive
        {
            get { lock (_leaseSync) return _activeLease != null; }
        }

        public ISceneTransitionLease TryBeginTransition()
        {
            lock (_leaseSync)
            {
                if (_activeLease != null)
                    return null;
                _leaseCompletion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _activeLease = new SceneTransitionLease(this);
                return _activeLease;
            }
        }

        public Task WaitForActiveTransitionAsync(CancellationToken ct = default)
        {
            Task completion;
            lock (_leaseSync)
                completion = _leaseCompletion?.Task;
            if (completion == null || completion.IsCompleted)
                return Task.CompletedTask;
            if (!ct.CanBeCanceled)
                return completion;
            return AwaitWithCancellation(completion, ct);
        }

        private static async Task AwaitWithCancellation(Task task, CancellationToken ct)
        {
            var gate = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (ct.Register(() => gate.TrySetCanceled(ct)))
            {
                if (await Task.WhenAny(task, gate.Task) != task)
                    await gate.Task; // observe the cancellation
            }
            await task;
        }

        private void ReleaseLease(SceneTransitionLease lease)
        {
            lock (_leaseSync)
            {
                if (!ReferenceEquals(_activeLease, lease))
                    return;
                _activeLease = null;
                _leaseCompletion?.TrySetResult(true);
            }
        }

        public Task CoverAsync(CancellationToken ct = default)
            => EnqueueAnimation(covered: true, ct);

        public Task RevealAsync(CancellationToken ct = default)
            => EnqueueAnimation(covered: false, ct);

        // All cover/reveal requests chain onto one tail: a second call while an
        // animation is in flight queues behind it instead of interleaving
        // raycast blocks and progress writes on the same overlay.
        private Task EnqueueAnimation(bool covered, CancellationToken ct)
        {
            lock (_animationSync)
            {
                Task tail = _animationTail;
                var done = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _animationTail = done.Task;
                _ = RunAnimationAfter(tail, covered, ct, done);
                return done.Task;
            }
        }

        private async Task RunAnimationAfter(
            Task previous, bool covered, CancellationToken ct, TaskCompletionSource<bool> done)
        {
            try { await previous; }
            catch { /* a cancelled/failed predecessor must not stall the queue */ }

            try
            {
                ct.ThrowIfCancellationRequested();
                // Already in the target state — complete without re-animating.
                if (_covered != covered || _group == null)
                    await AnimateAsync(covered, ct);
                _covered = covered;
                done.TrySetResult(true);
            }
            catch (Exception exception)
            {
                done.TrySetException(exception);
            }
        }

        private async Task AnimateAsync(bool covered, CancellationToken ct)
        {
            EnsureOverlay();
            _group.gameObject.SetActive(true);
            _group.blocksRaycasts = true;
            _group.alpha = 1f;

            float elapsed = 0f;
            float duration = Mathf.Max(0f, AnimationDurationSeconds);
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                ApplyProgress(covered ? eased : 1f - eased);
                await Task.Yield();
            }

            ApplyProgress(covered ? 1f : 0f);
            _group.blocksRaycasts = covered;
            if (!covered)
                _group.gameObject.SetActive(false);
        }

        private void ApplyProgress(float progress)
        {
            if (_stripes == null)
                return;

            for (int i = 0; i < _stripes.Length; i++)
            {
                var stripe = _stripes[i];
                if (stripe == null)
                    continue;

                float delay = i * 0.045f;
                float local = Mathf.Clamp01((progress - delay) / (1f - delay));
                stripe.localScale = new Vector3(1f, local, 1f);
            }
        }

        private void EnsureOverlay()
        {
            if (_group != null)
                return;

            var root = new GameObject("MoyvaSceneTransition", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _group = root.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _stripes = new RectTransform[StripeCount];

            for (int i = 0; i < StripeCount; i++)
            {
                var stripeGo = new GameObject($"Stripe_{i:00}", typeof(RectTransform), typeof(Image));
                stripeGo.transform.SetParent(root.transform, false);
                var rect = stripeGo.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(i / (float)StripeCount, 0f);
                rect.anchorMax = new Vector2((i + 1) / (float)StripeCount, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, i % 2 == 0 ? 0f : 1f);
                rect.localScale = new Vector3(1f, 0f, 1f);
                var image = stripeGo.GetComponent<Image>();
                image.color = i % 2 == 0
                    ? new Color(0.02f, 0.028f, 0.035f, 1f)
                    : new Color(0.055f, 0.07f, 0.08f, 1f);
                _stripes[i] = rect;
            }

            root.SetActive(false);
        }

        private sealed class SceneTransitionLease : ISceneTransitionLease
        {
            private SceneTransitionService _owner;

            public SceneTransitionLease(SceneTransitionService owner) => _owner = owner;

            public Task CoverAsync(CancellationToken ct = default)
                => _owner?.CoverAsync(ct) ?? Task.CompletedTask;

            public Task RevealAsync(CancellationToken ct = default)
                => _owner?.RevealAsync(ct) ?? Task.CompletedTask;

            public void Dispose()
            {
                SceneTransitionService owner = _owner;
                _owner = null;
                owner?.ReleaseLease(this);
            }
        }
    }
}
