using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Kruty1918.UiFoundation
{
    /// <summary>Visual styles a scene cover→reveal transition can play.</summary>
    public enum SceneTransitionStyle
    {
        /// <summary>Seven vertical stripes scaling up with a stagger.</summary>
        Stripes,
        /// <summary>Plain full-screen alpha fade.</summary>
        Fade,
        /// <summary>Opaque circle grows from the centre (cover) / shrinks out (reveal).</summary>
        Iris,
        /// <summary>Two panels slide shut from the left and right edges.</summary>
        Doors,
        /// <summary>Single panel slides up from the bottom edge.</summary>
        Curtain
    }

    public interface ISceneTransitionService
    {
        /// <summary>Style used by the parameterless Cover/Reveal overloads.</summary>
        SceneTransitionStyle Style { get; set; }

        Task CoverAsync(CancellationToken ct = default);
        Task RevealAsync(CancellationToken ct = default);
        Task CoverAsync(SceneTransitionStyle style, CancellationToken ct = default);
        Task RevealAsync(SceneTransitionStyle style, CancellationToken ct = default);

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
        Task CoverAsync(SceneTransitionStyle style, CancellationToken ct = default);
        Task RevealAsync(SceneTransitionStyle style, CancellationToken ct = default);
    }

    public sealed class SceneTransitionService : ISceneTransitionService
    {
        private const int StripeCount = 7;
        private const float Duration = 0.32f;

        // Instance seam for tests: 0 makes animations complete synchronously
        // without needing a frame loop.
        internal float AnimationDurationSeconds = Duration;

        public SceneTransitionStyle Style { get; set; } = SceneTransitionStyle.Stripes;

        private CanvasGroup _group;
        private RectTransform[] _stripes;
        private Image _fade;
        private RectTransform _iris;
        private RectTransform _doorL, _doorR;
        private RectTransform _curtain;
        private readonly Dictionary<SceneTransitionStyle, GameObject> _styleRoots =
            new Dictionary<SceneTransitionStyle, GameObject>();

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
            => CoverAsync(Style, ct);

        public Task RevealAsync(CancellationToken ct = default)
            => RevealAsync(Style, ct);

        public Task CoverAsync(SceneTransitionStyle style, CancellationToken ct = default)
            => EnqueueAnimation(covered: true, style, ct);

        public Task RevealAsync(SceneTransitionStyle style, CancellationToken ct = default)
            => EnqueueAnimation(covered: false, style, ct);

        // All cover/reveal requests chain onto one tail: a second call while an
        // animation is in flight queues behind it instead of interleaving
        // raycast blocks and progress writes on the same overlay.
        private Task EnqueueAnimation(bool covered, SceneTransitionStyle style, CancellationToken ct)
        {
            lock (_animationSync)
            {
                Task tail = _animationTail;
                var done = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _animationTail = done.Task;
                _ = RunAnimationAfter(tail, covered, style, ct, done);
                return done.Task;
            }
        }

        private async Task RunAnimationAfter(
            Task previous, bool covered, SceneTransitionStyle style,
            CancellationToken ct, TaskCompletionSource<bool> done)
        {
            try { await previous; }
            catch { /* a cancelled/failed predecessor must not stall the queue */ }

            try
            {
                ct.ThrowIfCancellationRequested();
                // Already in the target state — complete without re-animating.
                if (_covered != covered || _group == null)
                    await AnimateAsync(covered, style, ct);
                _covered = covered;
                done.TrySetResult(true);
            }
            catch (Exception exception)
            {
                done.TrySetException(exception);
            }
        }

        private async Task AnimateAsync(bool covered, SceneTransitionStyle style, CancellationToken ct)
        {
            EnsureOverlay();
            SetActiveStyle(style);
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
                ApplyProgress(style, covered ? eased : 1f - eased);
                await Task.Yield();
            }

            ApplyProgress(style, covered ? 1f : 0f);
            _group.blocksRaycasts = covered;
            if (!covered)
                _group.gameObject.SetActive(false);
        }

        private void ApplyProgress(SceneTransitionStyle style, float progress)
        {
            switch (style)
            {
                case SceneTransitionStyle.Fade:
                    if (_fade != null)
                    {
                        var c = _fade.color;
                        c.a = progress;
                        _fade.color = c;
                    }
                    break;
                case SceneTransitionStyle.Iris:
                    if (_iris != null)
                        _iris.localScale = Vector3.one * Mathf.Max(0.001f, progress);
                    break;
                case SceneTransitionStyle.Doors:
                    if (_doorL != null) _doorL.localScale = new Vector3(progress, 1f, 1f);
                    if (_doorR != null) _doorR.localScale = new Vector3(progress, 1f, 1f);
                    break;
                case SceneTransitionStyle.Curtain:
                    if (_curtain != null)
                        _curtain.localScale = new Vector3(1f, progress, 1f);
                    break;
                default: // Stripes
                    if (_stripes == null) break;
                    for (int i = 0; i < _stripes.Length; i++)
                    {
                        var stripe = _stripes[i];
                        if (stripe == null)
                            continue;
                        float delay = i * 0.045f;
                        float local = Mathf.Clamp01((progress - delay) / (1f - delay));
                        stripe.localScale = new Vector3(1f, local, 1f);
                    }
                    break;
            }
        }

        /// <summary>Shows only the visuals of the requested transition style.</summary>
        private void SetActiveStyle(SceneTransitionStyle style)
        {
            EnsureStyleVisual(style);
            foreach (var kv in _styleRoots)
                kv.Value.SetActive(kv.Key == style);
        }

        private void EnsureStyleVisual(SceneTransitionStyle style)
        {
            if (_styleRoots.ContainsKey(style))
                return;

            switch (style)
            {
                case SceneTransitionStyle.Fade:
                    _fade = Panel("Fade", _group.transform,
                        Vector2.zero, Vector2.one, DarkA);
                    _styleRoots[style] = _fade.gameObject;
                    break;

                case SceneTransitionStyle.Iris:
                    var go = new GameObject("Iris", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(_group.transform, false);
                    _iris = (RectTransform)go.transform;
                    _iris.anchorMin = new Vector2(0.5f, 0.5f);
                    _iris.anchorMax = new Vector2(0.5f, 0.5f);
                    _iris.pivot = new Vector2(0.5f, 0.5f);
                    // Opaque circle must reach past the screen diagonal.
                    _iris.sizeDelta = new Vector2(4800f, 4800f);
                    _iris.localScale = Vector3.one * 0.001f;
                    var img = go.GetComponent<Image>();
                    img.sprite = IrisSprite();
                    img.color = DarkA;
                    _styleRoots[style] = go;
                    break;

                case SceneTransitionStyle.Doors:
                    var rootGo = new GameObject("Doors", typeof(RectTransform));
                    rootGo.transform.SetParent(_group.transform, false);
                    var root = (RectTransform)rootGo.transform;
                    StretchRect(root);
                    _doorL = Half(rootGo.transform, "Door_L", true);
                    _doorR = Half(rootGo.transform, "Door_R", false);
                    _styleRoots[style] = rootGo;
                    break;

                case SceneTransitionStyle.Curtain:
                    var cgo = new GameObject("Curtain", typeof(RectTransform), typeof(Image));
                    cgo.transform.SetParent(_group.transform, false);
                    _curtain = (RectTransform)cgo.transform;
                    _curtain.anchorMin = Vector2.zero;
                    _curtain.anchorMax = Vector2.one;
                    _curtain.offsetMin = Vector2.zero;
                    _curtain.offsetMax = Vector2.zero;
                    _curtain.pivot = new Vector2(0.5f, 0f); // grows from bottom
                    _curtain.localScale = new Vector3(1f, 0f, 1f);
                    cgo.GetComponent<Image>().color = DarkB;
                    _styleRoots[style] = cgo;
                    break;

                case SceneTransitionStyle.Stripes:
                    var stripesRoot = new GameObject("Stripes", typeof(RectTransform));
                    stripesRoot.transform.SetParent(_group.transform, false);
                    StretchRect((RectTransform)stripesRoot.transform);
                    _stripes = new RectTransform[StripeCount];
                    for (int i = 0; i < StripeCount; i++)
                    {
                        var stripeGo = new GameObject($"Stripe_{i:00}",
                            typeof(RectTransform), typeof(Image));
                        stripeGo.transform.SetParent(stripesRoot.transform, false);
                        var rect = stripeGo.GetComponent<RectTransform>();
                        rect.anchorMin = new Vector2(i / (float)StripeCount, 0f);
                        rect.anchorMax = new Vector2((i + 1) / (float)StripeCount, 1f);
                        rect.offsetMin = Vector2.zero;
                        rect.offsetMax = Vector2.zero;
                        rect.pivot = new Vector2(0.5f, i % 2 == 0 ? 0f : 1f);
                        rect.localScale = new Vector3(1f, 0f, 1f);
                        stripeGo.GetComponent<Image>().color = i % 2 == 0 ? DarkA : DarkB;
                        _stripes[i] = rect;
                    }
                    _styleRoots[style] = stripesRoot;
                    break;
            }
        }

        static readonly Color DarkA = new Color(0.02f, 0.028f, 0.035f, 1f);
        static readonly Color DarkB = new Color(0.055f, 0.07f, 0.08f, 1f);

        static RectTransform Half(Transform parent, string name, bool left)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = left ? new Vector2(0f, 0f) : new Vector2(0.5f, 0f);
            r.anchorMax = left ? new Vector2(0.5f, 1f) : new Vector2(1f, 1f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            r.pivot = new Vector2(left ? 0f : 1f, 0.5f);
            r.localScale = new Vector3(0f, 1f, 1f);
            go.GetComponent<Image>().color = left ? DarkA : DarkB;
            return r;
        }

        static Image Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = min;
            r.anchorMax = max;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            var c = img.color; c.a = 0f; img.color = c;
            return img;
        }

        static void StretchRect(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        static Sprite _irisSprite;

        /// <summary>Radial sprite: fully opaque inside r<0.7, fades to edge.</summary>
        static Sprite IrisSprite()
        {
            if (_irisSprite != null) return _irisSprite;
            const int s = 256;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var px = new Color32[s * s];
            for (var y = 0; y < s; y++)
            {
                for (var x = 0; x < s; x++)
                {
                    var dx = x - s / 2f;
                    var dy = y - s / 2f;
                    var r = Mathf.Sqrt(dx * dx + dy * dy) / (s / 2f);
                    var a = r < 0.7f ? 1f : Mathf.Clamp01((1f - r) / 0.3f);
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            _irisSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
            return _irisSprite;
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

            public Task CoverAsync(SceneTransitionStyle style, CancellationToken ct = default)
                => _owner?.CoverAsync(style, ct) ?? Task.CompletedTask;

            public Task RevealAsync(SceneTransitionStyle style, CancellationToken ct = default)
                => _owner?.RevealAsync(style, ct) ?? Task.CompletedTask;

            public void Dispose()
            {
                SceneTransitionService owner = _owner;
                _owner = null;
                owner?.ReleaseLease(this);
            }
        }
    }
}
