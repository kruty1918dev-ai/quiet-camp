using System;
using System.Threading.Tasks;
using Kruty1918.Audio;
using Kruty1918.InputRouting.API;
using Kruty1918.UIActions.API;
using QuietCamp.Infrastructure;
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
    /// operation by both a raycast-blocking canvas and the canonical input
    /// policy/context gate. Reduced motion swaps the dive for a calm color
    /// dissolve. All timings/palettes come from Resources/QuietCamp/
    /// transition.json (TransitionConfig) — nothing is duplicated in code.
    /// </summary>
    public sealed class FoliageDiveTransition : MonoBehaviour
    {
        public enum State { Idle, Covering, CoveredLoading, Preparing, Revealing, Recovery }

        GameServices _services;
        TransitionConfig _cfg;
        CanvasGroup _group;
        Image _cover;
        RectTransform _leafRoot;
        Image[] _leaves;
        TextMeshProUGUI _hint;
        IDisposable _gateContext;
        IDisposable _inputBlock;
        IDisposable _backLease;
        State _state = State.Idle;
        Camera _cam;
        Vector3 _camBasePos;
        Quaternion _camBaseRot;
        float _camBaseSize;
        float _progress;
        float _stateTime;
        bool _audioIn, _audioOut;
        Color _coverTint = TransitionConfig.CoverBase;
        Color _leafTint;
        Vector2Int _screenSize;
        int _recaptureFrames;
        int _generation;
        int _leafActive;
        bool _destroyed;

        public State Current => _state;
        public bool IsIdle => _state == State.Idle;
        public TransitionConfig Config => _cfg ?? (_cfg = TransitionConfig.Load());

        public static FoliageDiveTransition Ensure(GameServices services)
        {
            var existing = FindFirstObjectByType<FoliageDiveTransition>();
            if (existing != null) { existing._services = services; return existing; }
            var go = new GameObject("FoliageDiveTransition");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<FoliageDiveTransition>();
            t._services = services;
            // Back during a transition is swallowed by a dedicated consumed
            // action — it must not reach gameplay's qc.back handler.
            t._backLease = services?.Dispatch?.Register(
                new UiActionId("qc.transition.back"), () => UiActionResult.Performed());
            return t;
        }

        /// <summary>Target-scene phase ("morning"/"noon"/"evening"/"night" or
        /// null for the menu): selects the post-cover tint and leaf palette so
        /// the palette swap happens only under the opaque cover.</summary>
        public void SetTargetPhase(string phase)
        {
            _targetPhase = phase;
            _coverTint = Config.CoverTint(phase);
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
            _cover.color = TransitionConfig.CoverBase;
            _cover.raycastTarget = true;

            var leafRootGo = new GameObject("Leaves", typeof(RectTransform));
            leafRootGo.transform.SetParent(canvasGo.transform, false);
            _leafRoot = (RectTransform)leafRootGo.transform;
            _leafRoot.anchorMin = Vector2.zero; _leafRoot.anchorMax = Vector2.one;
            _leafRoot.offsetMin = _leafRoot.offsetMax = Vector2.zero;

            // Stable sprite lookup by atlas name — never LoadAll order.
            var named = new System.Collections.Generic.Dictionary<string, Sprite>();
            foreach (var s in Resources.LoadAll<Sprite>("QuietCamp/Atmosphere/Textures/leaves"))
                if (s != null) named[s.name] = s;
            var byIndex = new[]
            {
                named.TryGetValue("sage", out var a) ? a : null,
                named.TryGetValue("olive", out var b) ? b : null,
                named.TryGetValue("twig", out var c) ? c : null,
                named.TryGetValue("amber", out var d) ? d : null,
            };
            _leaves = new Image[TransitionConfig.MaxLeaves];
            for (var i = 0; i < _leaves.Length; i++)
            {
                var go = new GameObject("Leaf_" + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_leafRoot, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
                r.pivot = new Vector2(.5f, .5f);
                var img = go.GetComponent<Image>();
                img.sprite = byIndex[i % byIndex.Length];
                // Missing leaf asset degrades to the plain opaque dissolve —
                // an Image without a sprite would render a raw rectangle.
                img.enabled = img.sprite != null;
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

        /// <summary>Begins the dive: gates input (raycast + policy + context),
        /// sinks the camera, closes leaves and the opaque cover. Completes when
        /// the screen is fully covered and at least one covered frame ran.</summary>
        public async Task CoverAsync()
        {
            EnsureOverlay();
            if (_state != State.Idle && _state != State.Recovery) await WaitIdle();
            if (_destroyed) return;
            int gen = ++_generation;
            _state = State.Covering;
            _stateTime = 0f;
            _progress = 0f;
            _audioIn = _audioOut = false;
            _group.gameObject.SetActive(true);
            _group.blocksRaycasts = true;
            _gateContext = _services?.ContextStack?.Push(new UiContextRegistration(
                "FoliageDive", UiContextLayer.Modal, 100, () => _state != State.Idle,
                new UiActionId("qc.transition.back"),
                blocksLowerHotkeys: true,
                allowedHotkeyActionIds: Array.Empty<UiActionId>()));
            _inputBlock = _services?.InputPolicy
                ?.AcquireBlock(GameplayInputKind.All, this);
            bool reduced = _services != null && _services.ReducedMotion;
            _leafActive = reduced ? 0 : Config.LeafCount(QualityTier());
            _leafRoot.gameObject.SetActive(_leafActive > 0);
            _leafTint = Config.LeafBase;
            for (var i = 0; i < _leaves.Length; i++)
            {
                _leaves[i].gameObject.SetActive(i < _leafActive);
                _leaves[i].color = _leafTint;
            }
            CaptureCamera();
            _screenSize = new Vector2Int(Screen.width, Screen.height);
            _recaptureFrames = 0;
            // Scoped ambient dip (~−2 dB) for the entry motion only.
            _services?.Audio?.DuckBus(AudioBus.Ambience,
                Config.DuckAmount, Config.DuckAttack,
                Config.CoverDuration + .15f, Config.DuckRelease);
            float duration = reduced ? Config.ReducedIn
                : Config.CoverDuration + Config.CoveredHold;
            while (_stateTime < duration && _state == State.Covering && !_destroyed)
                await Task.Yield();
            // A Recover() during the dive bumped the generation — this
            // continuation is stale and must not resurrect CoveredLoading.
            if (_destroyed || gen != _generation) return;
            if (_state == State.Covering)
            {
                _state = State.CoveredLoading;
                _stateTime = 0f;
                // Guarantee one painted frame under the fully opaque cover
                // before the router unloads the old scene.
                int frame = Time.renderedFrameCount;
                float guard = 0f;
                while (Time.renderedFrameCount == frame && guard < .35f && !_destroyed)
                {
                    guard += Time.unscaledDeltaTime;
                    await Task.Yield();
                }
            }
        }

        /// <summary>Called after the new scene's host has reported ready and one
        /// rendered frame passed: captures the freshly fitted camera so the
        /// reveal can return to it exactly.</summary>
        public void BeginReveal()
        {
            if (_state != State.CoveredLoading && _state != State.Preparing) return;
            _state = State.Preparing;
            _stateTime = 0f;
            CaptureCamera();
        }

        /// <summary>Opens the cover back up and returns the camera to its
        /// fitted pose; releases input when done.</summary>
        public async Task RevealAsync()
        {
            // Only a prepared scene may reveal — a bare CoveredLoading call
            // would skip the new camera's baseline capture, and Recovery
            // must never reopen a half-loaded scene.
            if (_state != State.Preparing && _state != State.CoveredLoading) return;
            _state = State.Revealing;
            _stateTime = 0f;
            bool reduced = _services != null && _services.ReducedMotion;
            float duration = reduced ? Config.ReducedOut : Config.RevealDuration;
            while (_stateTime < duration && _state == State.Revealing && !_destroyed)
                await Task.Yield();
            if (!_destroyed) Finish();
        }

        /// <summary>Shows the slow-load hint once the covered wait drags on.</summary>
        public void ShowSlowHint()
        {
            EnsureOverlay();
            UpdateHint();
            if (_hint != null) _hint.gameObject.SetActive(true);
        }

        /// <summary>Error path: undo whatever the dive touched and unlock.
        /// Safe to call twice — cleanup is idempotent.</summary>
        public void Recover()
        {
            _generation++;
            _state = State.Recovery;
            RestoreCamera();
            Finish();
        }

        async Task WaitIdle()
        {
            // Wall-clock bound, not a frame counter — a wedged transition
            // must not stall navigation forever.
            float elapsed = 0f;
            while (!IsIdle && elapsed < 8f && !_destroyed)
            {
                elapsed += Time.unscaledDeltaTime;
                await Task.Yield();
            }
        }

        void Finish()
        {
            RestoreCamera();
            _gateContext?.Dispose();
            _gateContext = null;
            _inputBlock?.Dispose();
            _inputBlock = null;
            _state = State.Idle;
            if (_group != null)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
                _group.gameObject.SetActive(false);
            }
            if (_hint != null) _hint.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            _destroyed = true;
            _generation++;
            _gateContext?.Dispose();
            _inputBlock?.Dispose();
            _backLease?.Dispose();
            RestoreCamera();
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
        /// fitted pose — the dip slides along the baseline camera's own up
        /// axis (screen-down), never changes logical board coordinates.</summary>
        void DriveCamera(float depth01)
        {
            if (_cam == null) return;
            float h = 2f * _camBaseSize;
            var up = _camBaseRot * Vector3.up;
            _cam.transform.position = _camBasePos - up * (depth01 * Config.CameraDepth * h);
            _cam.transform.rotation = _camBaseRot;
            _cam.orthographicSize = _camBaseSize * Mathf.Lerp(1f, Config.CameraSize, depth01);
        }

        // ─── Frame loop ──────────────────────────────────────────────────────

        void Update()
        {
            if (_destroyed || _group == null || _state == State.Idle) return;
            bool reduced = _services != null && _services.ReducedMotion;
            _stateTime += Time.unscaledDeltaTime;

            var size = new Vector2Int(Screen.width, Screen.height);
            if (size != _screenSize && (_state == State.Covering || _state == State.Revealing))
            {
                _screenSize = size;
                // Let the scene's own fitter write the new baseline for one
                // frame, then recapture — same normalized progress continues.
                RestoreCamera();
                _recaptureFrames = 1;
            }
            if (_recaptureFrames > 0 && --_recaptureFrames == 0)
                CaptureCamera();

            switch (_state)
            {
                case State.Covering:
                {
                    float dur = reduced ? Config.ReducedIn
                        : Config.CoverDuration + Config.CoveredHold;
                    _progress = Mathf.Clamp01(_stateTime / dur);
                    float e = Smooth(_progress);
                    _group.alpha = 1f;
                    _cover.color = Alpha(TransitionConfig.CoverBase,
                        reduced ? e : Mathf.Clamp01(e * 1.15f));
                    if (!reduced)
                    {
                        DriveCamera(e);
                        if (e > Config.CueInMarker && !_audioIn)
                        { _audioIn = true; Rustle(Config.CueInGain, Config.CueInPan); }
                    }
                    break;
                }
                case State.CoveredLoading:
                case State.Preparing:
                {
                    _progress = 1f;
                    // Under full cover the tint may ease toward the target
                    // phase — never under the open frame.
                    _cover.color = Color.Lerp(TransitionConfig.CoverBase, _coverTint,
                        Mathf.Clamp01(_stateTime * 2.5f));
                    _leafTint = Color.Lerp(Config.LeafBase, Config.LeafTint(TargetPhase()),
                        Mathf.Clamp01(_stateTime * 2.5f));
                    if (_state == State.CoveredLoading && _stateTime > Config.SlowHintDelay)
                        ShowSlowHint();
                    break;
                }
                case State.Revealing:
                {
                    float dur = reduced ? Config.ReducedOut : Config.RevealDuration;
                    float t = Mathf.Clamp01(_stateTime / dur);
                    float e = 1f - Smooth(t);
                    _progress = e;
                    _cover.color = Alpha(_coverTint, e);
                    if (!reduced)
                    {
                        // Reveal drives the same normalized progress the
                        // leaves read — one shared timeline, not a parallel
                        // coroutine with a similar duration.
                        DriveCamera(e * (Config.RevealDepth / Config.CameraDepth));
                        if (t > Config.CueOutMarker && !_audioOut)
                        { _audioOut = true; Rustle(Config.CueOutGain, Config.CueOutPan); }
                    }
                    break;
                }
                case State.Recovery:
                    break;
            }

            if (_leafActive > 0 && _leafRoot != null) LayoutLeaves(_progress);
        }

        string _targetPhase;
        string TargetPhase() => _targetPhase;

        static float Smooth(float t) => t * t * (3f - 2f * t);
        static Color Alpha(Color c, float a) { c.a = Mathf.Clamp01(a); return c; }

        /// <summary>Leaves read the shared cover progress with per-leaf
        /// stagger: A enters from the bottom-left first, B trails from the
        /// bottom-right, a thin leaf rides the periphery, the last soft edge
        /// closes the swap. Sizes derive from the screen's short side so the
        /// same scene works on tablet aspect without stretched sprites.</summary>
        void LayoutLeaves(float progress)
        {
            var canvas = _group.GetComponent<Canvas>();
            var size = ((RectTransform)canvas.transform).rect.size;
            float shortSide = Mathf.Min(size.x, size.y);
            var cfg = Config;
            for (var i = 0; i < _leafActive; i++)
            {
                float lp = progress <= cfg.LeafStagger[i] ? 0f
                    : Mathf.Clamp01((progress - cfg.LeafStagger[i]) / (1f - cfg.LeafStagger[i]));
                var p = Vector2.Lerp(cfg.LeafHome[i], cfg.LeafDive[i], Smooth(lp));
                _leaves[i].rectTransform.anchoredPosition =
                    new Vector2((p.x - .5f) * size.x, (p.y - .5f) * size.y);
                float grow = Mathf.Lerp(1f, cfg.LeafGrow[i], lp);
                float side = cfg.LeafFraction[i] * shortSide * grow;
                _leaves[i].rectTransform.sizeDelta = new Vector2(side * cfg.LeafWide[i], side);
                _leaves[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f,
                    cfg.LeafRotation[i] + cfg.LeafSpin[i] * lp);
                var c = _leafTint;
                c.a = Mathf.Clamp01(lp * 1.6f);
                _leaves[i].color = c;
            }
        }

        /// <summary>0–2 = Low/Balanced/High; settings.quality 0 = auto.</summary>
        int QualityTier()
        {
            int q = _services != null ? _services.Settings.quality : 0;
            if (q >= 1 && q <= 3) return q - 1;
            return Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, 2);
        }

        /// <summary>Transition rustle is spatially subtle: a slight pan within
        /// ±.15 matching the leaf crossing side and no doppler/echo.</summary>
        void Rustle(float scale, float pan)
        {
            if (_services?.Audio == null) return;
            var def = _services.Audio.GetSound("sfx.rustle");
            if (def == null) return;
            _services.Audio.Play("sfx.rustle",
                new AudioPlayOptions(volumeScale: scale, pitchOffset: -.05f,
                    panStereo: Mathf.Clamp(pan, -.15f, .15f)));
        }
    }
}
