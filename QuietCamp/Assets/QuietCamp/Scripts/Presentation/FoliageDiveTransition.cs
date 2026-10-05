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
    /// Persistent, input-gated leaf curtain. Independently animated folded leaves
    /// gather over the screen, sway during loading, then fly offscreen.
    /// Reduced motion uses a short dissolve. The public router contract and
    /// recovery lifecycle are shared by both presentations.
    /// </summary>
    public sealed class FoliageDiveTransition : MonoBehaviour
    {
        public enum State { Idle, Covering, CoveredLoading, Preparing, Revealing, Recovery }

        GameServices _services;
        TransitionConfig _cfg;
        CanvasGroup _group;
        Image _cover;
        RectTransform _leafRoot;
        LeafCurtainGraphic _canopy;
        TextMeshProUGUI _hint;
        IDisposable _gateContext;
        IDisposable _inputBlock;
        IDisposable _backLease;
        State _state = State.Idle;
        float _progress;
        float _stateTime;
        float _leafTime;
        bool _audioIn, _audioOut;
        AudioHandle _entryRustle, _exitRustle;
        Color _leafTint, _sourceTint, _destinationTint;
        Color CoverTone=>new Color(_leafTint.r*.62f,_leafTint.g*.62f,_leafTint.b*.62f,1);
        public Color LeafTint => _leafTint;
        string _targetPhase;
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

        /// <summary>Select a palette once, before the curtain enters.</summary>
        public void SetTargetPhase(string phase) => _targetPhase = phase;

        // ─── Overlay construction ────────────────────────────────────────────

        void EnsureOverlay()
        {
            if (_group != null) return;
            var canvasGo = new GameObject("Cover", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup), typeof(GraphicRaycaster));
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

            _canopy = leafRootGo.AddComponent<LeafCurtainGraphic>();
            _canopy.raycastTarget = false;

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

        /// <summary>Gates input and sweeps the canopy over the scene. Completes when
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
            _leafTime = 0f;
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
            _sourceTint = CaptureLighting();
            _leafTint = _sourceTint;
            _canopy.ConfigureQuality(QualityTier());
            _canopy.SetFrame(0f, _leafTint);
            _cover.color = Alpha(CoverTone, 0f);
            _group.alpha = 1f;
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

        /// <summary>Called after the new scene is ready under full cover.</summary>
        public void BeginReveal()
        {
            if (_state != State.CoveredLoading && _state != State.Preparing) return;
            _destinationTint = CaptureLighting();
            _state = State.Preparing;
            _stateTime = 0f;
        }

        /// <summary>Continues the canopy offscreen and releases input.</summary>
        public async Task RevealAsync()
        {
            // Recovery must never reopen a half-loaded scene.
            if (_state != State.Preparing && _state != State.CoveredLoading) return;
            if (_hint != null) _hint.gameObject.SetActive(false);
            if (_state == State.CoveredLoading) BeginReveal();
            float paletteDuration = _services?.ReducedMotion == true ? .08f : .24f;
            while (_state == State.Preparing && _stateTime < paletteDuration && !_destroyed) await Task.Yield();
            if (_destroyed || _state != State.Preparing) return;
            _leafTint = _destinationTint;
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
            StopRustles();
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
            StopRustles();
            _destroyed = true;
            _generation++;
            _gateContext?.Dispose();
            _inputBlock?.Dispose();
            _backLease?.Dispose();
        }

        // ─── Frame loop ──────────────────────────────────────────────────────

        void Update()
        {
            if (_destroyed || _group == null || _state == State.Idle) return;
            bool reduced = _services != null && _services.ReducedMotion;
            _stateTime += Time.unscaledDeltaTime;
            _leafTime += Time.unscaledDeltaTime;

            switch (_state)
            {
                case State.Covering:
                {
                    float dur = reduced ? Config.ReducedIn : Config.CoverDuration;
                    _progress = Mathf.Clamp01(_stateTime / dur);
                    float e = Smooth(_progress);
                    _group.alpha = 1f;
                    _cover.color = Alpha(CoverTone,
                        reduced ? e : Smooth((_progress-.70f)/.30f));
                    if (!reduced)
                    {
                        _canopy.SetFrame(_progress, _leafTint, _leafTime);
                        if (e > Config.CueInMarker && !_audioIn)
                        { _audioIn = true; _entryRustle = Rustle("sfx.transition.in", Config.CueInGain, Config.CueInPan); }
                    }
                    break;
                }
                case State.CoveredLoading:
                case State.Preparing:
                {
                    _progress = 1f;
                    if (_state == State.Preparing)
                        _leafTint = Color.Lerp(_sourceTint,_destinationTint,Smooth(_stateTime/(_services?.ReducedMotion==true?.08f:.24f)));
                    _cover.color = CoverTone;
                    if (!reduced) _canopy.SetFrame(1f, _leafTint, _leafTime);
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
                    _cover.color = Alpha(CoverTone, reduced ? e : 1-Smooth(t/.30f));
                    if (!reduced)
                    {
                        // Continue in the same direction, like a passing canopy.
                        _canopy.SetFrame(1f + t, _leafTint, _leafTime);
                        if (t > Config.CueOutMarker && !_audioOut)
                        { _audioOut = true; _exitRustle = Rustle("sfx.transition.out", Config.CueOutGain, Config.CueOutPan); }
                    }
                    break;
                }
                case State.Recovery:
                    break;
            }

        }

        public static Color CaptureLighting()
        {
            var ambient=RenderSettings.ambientLight;
            var sun=RenderSettings.sun;
            if(sun==null) foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.type==LightType.Directional){sun=light;break;}
            var lit=ambient+(sun!=null?sun.color*sun.intensity*.38f:Color.black);
            var dim=World.MenuSceneHost.Current?.VisibleDimming??0;
            return new Color(.35f*lit.r*(1-dim),.48f*lit.g*(1-dim),.30f*lit.b*(1-dim),1);
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
        static Color Alpha(Color c, float a) { c.a = Mathf.Clamp01(a); return c; }

        /// <summary>0–2 = Low/Balanced/High; settings.quality 0 = auto.</summary>
        int QualityTier()
        {
            int q = _services != null ? _services.Settings.quality : 0;
            if (q >= 1 && q <= 3) return q - 1;
            return Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, 2);
        }

        /// <summary>Transition rustle is spatially subtle: a slight pan within
        /// ±.15 matching the leaf crossing side and no doppler/echo.</summary>
        AudioHandle Rustle(string key, float scale, float pan)
        {
            if (_services?.Audio == null || _services.Audio.GetSound(key) == null) return default;
            return _services.Audio.Play(key,
                new AudioPlayOptions(volumeScale: scale,
                    panStereo: Mathf.Clamp(pan, -.15f, .15f)));
        }

        void StopRustles()
        {
            _entryRustle.Stop();
            _exitRustle.Stop();
            _entryRustle = _exitRustle = default;
        }
    }
}
