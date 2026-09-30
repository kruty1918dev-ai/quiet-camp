using System;
using System.Threading.Tasks;
using Kruty1918.Audio;
using Kruty1918.UIActions.API;
using QuietCamp.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation
{
    /// <summary>
    /// Foliage-dive scene transition (atmosphere spec, prompt 04): the camera
    /// sinks into near foliage, large leaves close over the frame and an
    /// opaque forest-dark cover hides the scene swap, then the new clearing
    /// is revealed through green. One persistent overlay owned by the
    /// ScreenRouter — states Idle → Covering → CoveredLoading → Preparing →
    /// Revealing → Idle with Recovery on error; input is gated for the whole
    /// operation. Reduced motion swaps the dive for a calm color dissolve.
    /// </summary>
    public sealed class FoliageDiveTransition : MonoBehaviour
    {
        public enum State { Idle, Covering, CoveredLoading, Preparing, Revealing, Recovery }

        static readonly Color CoverColor = new Color(0.090f, 0.212f, 0.220f); // #173638
        const float CoverIn = .44f, CoverHold = .08f;
        const float RevealOut = .30f;
        const float ReducedIn = .14f, ReducedOut = .18f;
        const float CamDepth = .16f, CamSize = .94f, CamRevealStart = .10f;
        const float HintDelay = 1.5f;

        GameServices _services;
        CanvasGroup _group;
        Image _cover;
        RectTransform _leafRoot;
        Image[] _leaves;
        Vector2[] _leafHome;
        Vector2[] _leafDive;
        float[] _leafScale;
        TextMeshProUGUI _hint;
        IDisposable _gateContext;
        State _state = State.Idle;
        Camera _cam;
        Vector3 _camBasePos;
        Quaternion _camBaseRot;
        float _camBaseSize;
        float _progress;
        float _stateTime;
        bool _audioIn, _audioOut;

        public State Current => _state;
        public bool IsIdle => _state == State.Idle;

        public static FoliageDiveTransition Ensure(GameServices services)
        {
            var existing = FindFirstObjectByType<FoliageDiveTransition>();
            if (existing != null) { existing._services = services; return existing; }
            var go = new GameObject("FoliageDiveTransition");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<FoliageDiveTransition>();
            t._services = services;
            return t;
        }

        // ─── Overlay construction ────────────────────────────────────────────

        void EnsureOverlay()
        {
            if (_group != null) return;
            var canvasGo = new GameObject("Cover", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            _group = canvasGo.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            var coverGo = new GameObject("OpaqueCover", typeof(RectTransform), typeof(Image));
            coverGo.transform.SetParent(canvasGo.transform, false);
            var cr = (RectTransform)coverGo.transform;
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
            cr.offsetMin = cr.offsetMax = Vector2.zero;
            _cover = coverGo.GetComponent<Image>();
            _cover.color = CoverColor;
            _cover.raycastTarget = true;

            var leafRootGo = new GameObject("Leaves", typeof(RectTransform));
            leafRootGo.transform.SetParent(canvasGo.transform, false);
            _leafRoot = (RectTransform)leafRootGo.transform;
            _leafRoot.anchorMin = Vector2.zero; _leafRoot.anchorMax = Vector2.one;
            _leafRoot.offsetMin = _leafRoot.offsetMax = Vector2.zero;

            var sprites = Resources.LoadAll<Sprite>("QuietCamp/Atmosphere/Textures/leaves");
            int count = 4;
            _leaves = new Image[count];
            _leafHome = new Vector2[count];
            _leafDive = new Vector2[count];
            _leafScale = new float[count];
            // Corners inward — leaves converge toward frame centre when diving.
            var homes = new[] { new Vector2(-.12f, .88f), new Vector2(1.12f, .82f),
                                new Vector2(-.14f, .12f), new Vector2(1.14f, .10f) };
            var dives = new[] { new Vector2(.28f, .62f), new Vector2(.72f, .58f),
                                new Vector2(.32f, .30f), new Vector2(.68f, .34f) };
            for (var i = 0; i < count; i++)
            {
                var go = new GameObject("Leaf_" + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_leafRoot, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
                r.pivot = new Vector2(.5f, .5f);
                _leafHome[i] = homes[i];
                _leafDive[i] = dives[i];
                _leafScale[i] = 520f + 140f * i;
                r.sizeDelta = new Vector2(_leafScale[i], _leafScale[i]);
                r.localRotation = Quaternion.Euler(0f, 0f, (i * 47f) % 360f);
                var img = go.GetComponent<Image>();
                img.sprite = sprites.Length > 0 ? sprites[i % sprites.Length] : null;
                img.color = new Color(.35f, .45f, .38f, 1f);
                img.raycastTarget = false;
                _leaves[i] = img;
            }

            var hintGo = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
            hintGo.transform.SetParent(canvasGo.transform, false);
            var hr = (RectTransform)hintGo.transform;
            hr.anchorMin = new Vector2(.1f, .38f);
            hr.anchorMax = new Vector2(.9f, .46f);
            hr.offsetMin = hr.offsetMax = Vector2.zero;
            _hint = hintGo.GetComponent<TextMeshProUGUI>();
            _hint.alignment = TextAlignmentOptions.Center;
            _hint.fontSize = 40f;
            _hint.color = new Color(0.949f, 0.910f, 0.835f);
            _hint.raycastTarget = false;
            _hint.gameObject.SetActive(false);
        }

        void UpdateHint()
        {
            if (_hint == null || _services == null) return;
            _hint.text = _services.Localization != null
                ? _services.Localization.T("transition.loading") : "…";
        }

        // ─── Public contract ─────────────────────────────────────────────────

        /// <summary>Begins the dive: gate input, sink the camera, close leaves
        /// and the opaque cover. Completes when the screen is fully covered.</summary>
        public async Task CoverAsync()
        {
            EnsureOverlay();
            if (_state != State.Idle && _state != State.Recovery) await WaitIdle();
            _state = State.Covering;
            _stateTime = 0f;
            _progress = 0f;
            _audioIn = _audioOut = false;
            _group.gameObject.SetActive(true);
            _group.blocksRaycasts = true;
            _gateContext = _services?.ContextStack?.Push(new UiContextRegistration(
                "FoliageDive", UiContextLayer.Modal, 100, () => _state != State.Idle,
                new UiActionId("qc.back")));
            CaptureCamera();
            bool reduced = _services != null && _services.ReducedMotion;
            float duration = reduced ? ReducedIn : CoverIn + CoverHold;
            while (_stateTime < duration)
            {
                await Task.Yield();
                if (_state == State.Recovery) break;
            }
            _state = State.CoveredLoading;
        }

        /// <summary>Called after the new scene's host has settled: captures the
        /// freshly fitted camera so the reveal can return to it exactly.</summary>
        public void BeginReveal()
        {
            if (_state != State.CoveredLoading && _state != State.Recovery) return;
            _state = State.Preparing;
            _stateTime = 0f;
            CaptureCamera();
        }

        /// <summary>Opens the cover back up and returns the camera to its
        /// fitted pose; releases input when done.</summary>
        public async Task RevealAsync()
        {
            bool reduced = _services != null && _services.ReducedMotion;
            float duration = reduced ? ReducedOut : RevealOut;
            _state = State.Revealing;
            _stateTime = 0f;
            while (_stateTime < duration) await Task.Yield();
            Finish();
        }

        /// <summary>Shows the slow-load hint once the covered wait drags on.</summary>
        public void ShowSlowHint() { EnsureOverlay(); UpdateHint(); if (_hint != null) _hint.gameObject.SetActive(true); }

        /// <summary>Error path: undo whatever the dive touched and unlock.</summary>
        public void Recover()
        {
            _state = State.Recovery;
            RestoreCamera();
            Finish();
        }

        async Task WaitIdle()
        {
            var guard = 0;
            while (!IsIdle && guard++ < 6000) await Task.Yield();
        }

        void Finish()
        {
            RestoreCamera();
            _gateContext?.Dispose();
            _gateContext = null;
            _state = State.Idle;
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.gameObject.SetActive(false);
            if (_hint != null) _hint.gameObject.SetActive(false);
        }

        // ─── Camera ──────────────────────────────────────────────────────────

        void CaptureCamera()
        {
            _cam = Camera.main;
            if (_cam == null) return;
            _camBasePos = _cam.transform.position;
            _camBaseRot = _cam.transform.rotation;
            _camBaseSize = _cam.orthographicSize;
        }

        void RestoreCamera()
        {
            if (_cam == null) return;
            _cam.transform.SetPositionAndRotation(_camBasePos, _camBaseRot);
            _cam.orthographicSize = _camBaseSize;
        }

        /// <summary>Applies the cosmetic dive offset on top of the captured
        /// fitted pose — never changes logical board coordinates.</summary>
        void DriveCamera(float depth01)
        {
            if (_cam == null) return;
            float h = 2f * _camBaseSize;
            var offset = -_cam.transform.up * (depth01 * CamDepth * h);
            _cam.transform.position = _camBasePos + offset;
            _cam.orthographicSize = _camBaseSize * Mathf.Lerp(1f, CamSize, depth01);
        }

        // ─── Frame loop ──────────────────────────────────────────────────────

        void Update()
        {
            if (_group == null || _state == State.Idle) return;
            bool reduced = _services != null && _services.ReducedMotion;
            _stateTime += Time.unscaledDeltaTime;

            switch (_state)
            {
                case State.Covering:
                {
                    float dur = reduced ? ReducedIn : CoverIn + CoverHold;
                    _progress = Mathf.Clamp01(_stateTime / dur);
                    float e = Smooth(_progress);
                    _group.alpha = 1f;
                    _cover.color = Alpha(CoverColor, reduced ? e : Mathf.Clamp01(e * 1.15f));
                    if (!reduced)
                    {
                        DriveCamera(e);
                        if (e > .45f && !_audioIn) { _audioIn = true; Rustle(1f); }
                    }
                    else if (e > .5f && !_audioIn) { _audioIn = true; Rustle(.8f); }
                    break;
                }
                case State.CoveredLoading:
                {
                    _progress = 1f;
                    _cover.color = CoverColor;
                    if (_stateTime > HintDelay) ShowSlowHint();
                    break;
                }
                case State.Preparing:
                {
                    // Freshly loaded scene still hidden; reveal starts via RevealAsync.
                    _cover.color = CoverColor;
                    break;
                }
                case State.Revealing:
                {
                    float dur = reduced ? ReducedOut : RevealOut;
                    float t = Mathf.Clamp01(_stateTime / dur);
                    float e = 1f - Smooth(t);
                    _cover.color = Alpha(CoverColor, e);
                    if (!reduced)
                    {
                        // Camera returns exactly to the new scene's fit.
                        DriveCamera(Mathf.Lerp(0f, CamRevealStart / CamDepth, e));
                        if (!_audioOut) { _audioOut = true; Rustle(.5f); }
                    }
                    else if (!_audioOut) { _audioOut = true; Rustle(.4f); }
                    break;
                }
                case State.Recovery:
                    break;
            }

            if (!reduced && _leafRoot != null) LayoutLeaves(_progress);
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);
        static Color Alpha(Color c, float a) { c.a = Mathf.Clamp01(a); return c; }

        void LayoutLeaves(float progress)
        {
            var canvas = _group.GetComponent<Canvas>();
            var size = ((RectTransform)canvas.transform).rect.size;
            for (var i = 0; i < _leaves.Length; i++)
            {
                var p = Vector2.Lerp(_leafHome[i], _leafDive[i], progress);
                _leaves[i].rectTransform.anchoredPosition =
                    new Vector2((p.x - .5f) * size.x, (p.y - .5f) * size.y);
                var c = _leaves[i].color;
                c.a = Mathf.Clamp01(progress * 1.4f);
                _leaves[i].color = c;
            }
        }

        void Rustle(float scale)
        {
            if (_services?.Audio == null) return;
            var def = _services.Audio.GetSound("sfx.rustle");
            if (def == null) return;
            _services.Audio.Play("sfx.rustle",
                new AudioPlayOptions(volumeScale: scale, pitchOffset: -.05f));
        }
    }
}
