using System;
using System.Collections;
using System.Collections.Generic;
using Kruty1918.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kruty1918.Audio
{
    // ─── Public API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Параметри активного треку що можна змінити в runtime.
    /// </summary>
    public sealed class RuntimeMusicParams
    {
        public float Volume;
        public float Pitch;
        public bool? LoopOverride;

        public RuntimeMusicParams(float volume = 1f, float pitch = 1f, bool? loopOverride = null)
        {
            Volume = Mathf.Clamp01(volume);
            Pitch = Mathf.Clamp(pitch, -3f, 3f);
            LoopOverride = loopOverride;
        }
    }

    /// <summary>
    /// Публічний API музичного сервісу.
    ///
    /// Пункти ТЗ: 6, 7, 8, 9, 10, 11, 12.
    /// </summary>
    public interface IMusicService
    {
        // ── Стан ───────────────────────────────────────────────────────────

        /// <summary>Чи зараз активний epic-режим для локального гравця.</summary>
        bool IsEpicActive { get; }

        /// <summary>Назва поточного активного AudioClip (або null).</summary>
        string CurrentClipName { get; }

        // ── Сцена ──────────────────────────────────────────────────────────

        /// <summary>
        /// Явно активувати профіль для поточної сцени.
        /// Викликається автоматично при зміні сцени, але може бути викликаний вручну.
        /// </summary>
        void ApplyProfile(IMusicSceneProfile profile);

        // ── Epic Music ─────────────────────────────────────────────────────

        /// <summary>Увімкнути epic music для локального клієнта з плавним переходом.</summary>
        void EnableEpicMusic();

        /// <summary>Вимкнути epic music, повернутися до default music з плавним переходом.</summary>
        void DisableEpicMusic();

        /// <summary>Toggle epic mode.</summary>
        void ToggleEpicMusic();

        // ── Runtime control ────────────────────────────────────────────────

        /// <summary>Змінити гучність default-треку в реальному часі.</summary>
        void SetDefaultVolume(float volume);

        /// <summary>Змінити гучність epic-треку в реальному часі.</summary>
        void SetEpicVolume(float volume);

        /// <summary>Тимчасово змінити будь-які параметри активного треку.</summary>
        void ApplyRuntimeParams(RuntimeMusicParams p);

        /// <summary>Зупинити всю музику з fade-out.</summary>
        void Stop(float fadeDuration = -1f);

        /// <summary>Програти довільний трек поверх поточних налаштувань сцени.</summary>
        void PlayCustom(MusicTrackSettings track, float overrideFadeDuration = -1f);

        /// <summary>Відновити профіль поточної сцени (скасувати custom-трек).</summary>
        void RestoreSceneMusic(float overrideFadeDuration = -1f);

        // ── Mixer exposed params (пункт 12) ─────────────────────────────

        /// <summary>Встановити exposed AudioMixer float-параметр за іменем.</summary>
        bool TrySetMixerParam(string paramName, float value);
    }

    // ─── Implementation ──────────────────────────────────────────────────────────

    /// <summary>
    /// Реалізація IMusicService.
    ///
    /// Особливості:
    /// - Два AudioSource (_sourceA / _sourceB) для crossfade.
    /// - DontDestroyOnLoad на root-об'єкті → безперервне відтворення між сценами.
    /// - SceneManager.sceneLoaded → автоматичне застосування профілю.
    /// - Підтримує preserve-between-scenes (той самий clip → не перезапускати).
    /// - Epic music тільки для локального гравця.
    /// - Всі переходи через корутину Coroutine(FadeTransition).
    /// </summary>
    public sealed class MusicService : IMusicService, IDisposable
    {
        // ── Injected ───────────────────────────────────────────────────────

        private readonly List<IMusicSceneProfile> _profiles;

        // ── State ──────────────────────────────────────────────────────────

        private GameObject _root;
        private AudioSource _sourceA;     // primary
        private AudioSource _sourceB;     // crossfade secondary
        private AudioSource _activeSource;
        private AudioSource _fadingSource;

        private IMusicSceneProfile _currentProfile;
        private MusicTrackSettings _currentTrackSettings; // default чи epic
        private MusicTrackSettings _runtimeDefaultSettings; // mutable copy
        private MusicTrackSettings _runtimeEpicSettings;   // mutable copy
        private bool _isEpicActive;
        private CoroutineRunner _runner;

        public bool IsEpicActive => _isEpicActive;
        public string CurrentClipName => _activeSource != null && _activeSource.clip != null ? _activeSource.clip.name : null;

        // ─────────────────────────────────────────────────────────────────────

        public MusicService(List<IMusicSceneProfile> profiles)
        {
            _profiles = profiles ?? new List<IMusicSceneProfile>();
        }

        public void Initialize()
        {
            _root = new GameObject("[MoyvaMusic]");
            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(_root);

            _sourceA = AddSource(_root);
            _sourceB = AddSource(_root);
            _activeSource = _sourceA;
            _fadingSource = _sourceB;

            _runner = _root.AddComponent<CoroutineRunner>();

            SceneManager.sceneLoaded += OnSceneLoaded;

            // Застосувати профіль для поточної сцени одразу
            var profile = FindProfileForScene(SceneManager.GetActiveScene().name);
            if (profile != null)
                ApplyProfileInternal(profile, force: true);
        }

        public void Dispose()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_root != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_root);
                else
                    UnityEngine.Object.DestroyImmediate(_root);
            }
        }

        // ── IMusicService ──────────────────────────────────────────────────

        public void ApplyProfile(IMusicSceneProfile profile)
        {
            if (profile == null) return;
            ApplyProfileInternal(profile, force: false);
        }

        public void EnableEpicMusic()
        {
            if (_isEpicActive) return;
            _isEpicActive = true;
            TransitionTo(_runtimeEpicSettings ?? _currentProfile?.EpicMusic);
        }

        public void DisableEpicMusic()
        {
            if (!_isEpicActive) return;
            _isEpicActive = false;
            TransitionTo(_runtimeDefaultSettings ?? _currentProfile?.DefaultMusic);
        }

        public void ToggleEpicMusic()
        {
            if (_isEpicActive) DisableEpicMusic();
            else EnableEpicMusic();
        }

        public void SetDefaultVolume(float volume)
        {
            if (_runtimeDefaultSettings == null && _currentProfile != null)
                _runtimeDefaultSettings = Clone(_currentProfile.DefaultMusic);
            if (_runtimeDefaultSettings != null)
                _runtimeDefaultSettings.Volume = Mathf.Clamp01(volume);

            if (!_isEpicActive && _activeSource != null)
                _activeSource.volume = Mathf.Clamp01(volume);
        }

        public void SetEpicVolume(float volume)
        {
            if (_runtimeEpicSettings == null && _currentProfile != null)
                _runtimeEpicSettings = Clone(_currentProfile.EpicMusic);
            if (_runtimeEpicSettings != null)
                _runtimeEpicSettings.Volume = Mathf.Clamp01(volume);

            if (_isEpicActive && _activeSource != null)
                _activeSource.volume = Mathf.Clamp01(volume);
        }

        public void ApplyRuntimeParams(RuntimeMusicParams p)
        {
            if (p == null || _activeSource == null) return;
            _activeSource.volume = Mathf.Clamp01(p.Volume);
            _activeSource.pitch = Mathf.Clamp(p.Pitch, -3f, 3f);
            if (p.LoopOverride.HasValue)
                _activeSource.loop = p.LoopOverride.Value;
        }

        public void Stop(float fadeDuration = -1f)
        {
            float dur = fadeDuration >= 0f ? fadeDuration : GetFadeDuration(_currentTrackSettings, false);
            _runner.StopAllCoroutines();
            _runner.StartCoroutine(FadeOut(_activeSource, dur, () => { }));
        }

        public void PlayCustom(MusicTrackSettings track, float overrideFadeDuration = -1f)
        {
            if (track == null) return;
            TransitionTo(track, overrideFadeDuration);
        }

        public void RestoreSceneMusic(float overrideFadeDuration = -1f)
        {
            _isEpicActive = false;
            var settings = _runtimeDefaultSettings ?? _currentProfile?.DefaultMusic;
            TransitionTo(settings, overrideFadeDuration);
        }

        public bool TrySetMixerParam(string paramName, float value)
        {
            if (_activeSource == null || _activeSource.outputAudioMixerGroup == null) return false;
            var mixer = _activeSource.outputAudioMixerGroup.audioMixer;
            if (mixer == null) return false;
            return mixer.SetFloat(paramName, value);
        }

        // ── Private helpers ────────────────────────────────────────────────

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Additive-загрузка не змінює музику
            if (mode == LoadSceneMode.Additive) return;

            var profile = FindProfileForScene(scene.name);
            ApplyProfileInternal(profile, force: false);
        }

        private void ApplyProfileInternal(IMusicSceneProfile profile, bool force)
        {
            _currentProfile = profile;

            if (profile == null)
            {
                Stop();
                return;
            }

            // Зробити mutable copies з SO
            _runtimeDefaultSettings = Clone(profile.DefaultMusic);
            _runtimeEpicSettings    = Clone(profile.EpicMusic);

            var desired = _isEpicActive ? _runtimeEpicSettings : _runtimeDefaultSettings;

            if (!force && profile.PreserveMusicBetweenScenes && IsSameClip(desired))
            {
                // Той самий трек — лише плавно скоригувати параметри
                ApplyParamsToActive(desired);
                return;
            }

            TransitionTo(desired, profile.SceneTransitionDuration);
        }

        private void TransitionTo(MusicTrackSettings settings, float overrideDuration = -1f)
        {
            _currentTrackSettings = settings;

            if (settings == null || settings.Clip == null)
            {
                Stop(overrideDuration >= 0f ? overrideDuration : 1f);
                return;
            }

            float duration = overrideDuration >= 0f
                ? overrideDuration
                : settings.FadeOutDuration;

            bool useCross = settings.UseCrossfade;

            _runner.StopAllCoroutines();

            if (useCross)
                _runner.StartCoroutine(CrossfadeRoutine(settings, duration));
            else
                _runner.StartCoroutine(FadeSwapRoutine(settings, duration, settings.FadeInDuration));
        }

        private IEnumerator CrossfadeRoutine(MusicTrackSettings settings, float duration)
        {
            // Підготувати fading source
            Swap();
            ConfigureSource(_activeSource, settings);
            _activeSource.volume = 0f;
            if (settings.StartDelay > 0f)
                yield return new WaitForSeconds(settings.StartDelay);
            _activeSource.Play();

            float start = Time.unscaledTime;
            float prevVol = _fadingSource.volume;

            while (Time.unscaledTime - start < duration)
            {
                float t = (Time.unscaledTime - start) / Mathf.Max(duration, 0.01f);
                _activeSource.volume = Mathf.Lerp(0f, settings.Volume, t);
                _fadingSource.volume = Mathf.Lerp(prevVol, 0f, t);
                yield return null;
            }

            _activeSource.volume = settings.Volume;
            _fadingSource.Stop();
            _fadingSource.volume = 0f;
        }

        private IEnumerator FadeSwapRoutine(MusicTrackSettings settings, float fadeOut, float fadeIn)
        {
            yield return FadeOut(_fadingSource ?? _activeSource, fadeOut, () => { });

            Swap();
            ConfigureSource(_activeSource, settings);
            _activeSource.volume = 0f;
            if (settings.StartDelay > 0f)
                yield return new WaitForSeconds(settings.StartDelay);
            _activeSource.Play();
            yield return FadeIn(_activeSource, settings.Volume, fadeIn);
        }

        private IEnumerator FadeOut(AudioSource source, float duration, Action onDone)
        {
            if (source == null || !source.isPlaying)
            {
                onDone?.Invoke();
                yield break;
            }

            float start = source.volume;
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < duration)
            {
                source.volume = Mathf.Lerp(start, 0f, (Time.unscaledTime - t0) / Mathf.Max(duration, 0.01f));
                yield return null;
            }

            source.Stop();
            source.volume = 0f;
            onDone?.Invoke();
        }

        private IEnumerator FadeIn(AudioSource source, float targetVolume, float duration)
        {
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < duration)
            {
                source.volume = Mathf.Lerp(0f, targetVolume, (Time.unscaledTime - t0) / Mathf.Max(duration, 0.01f));
                yield return null;
            }
            source.volume = targetVolume;
        }

        private static float GetFadeDuration(MusicTrackSettings settings, bool out_)
            => settings != null ? (out_ ? settings.FadeOutDuration : settings.FadeInDuration) : 1f;

        private bool IsSameClip(MusicTrackSettings settings)
            => settings != null && _activeSource != null && _activeSource.isPlaying
               && _activeSource.clip != null && settings.Clip != null
               && _activeSource.clip == settings.Clip;

        private void ApplyParamsToActive(MusicTrackSettings settings)
        {
            if (_activeSource == null || settings == null) return;
            _activeSource.volume = settings.Volume;
            _activeSource.loop = settings.Loop;
        }

        private void Swap()
        {
            var tmp = _activeSource;
            _activeSource = _fadingSource;
            _fadingSource = tmp;
        }

        private static void ConfigureSource(AudioSource source, MusicTrackSettings settings)
        {
            source.clip = settings.Clip;
            source.outputAudioMixerGroup = settings.MixerGroup;
            source.loop = settings.Loop;
            source.volume = settings.Volume;
            source.pitch = 1f;
            source.spatialBlend = 0f;
            source.playOnAwake = false;
        }

        private static MusicTrackSettings Clone(MusicTrackSettings src)
        {
            if (src == null) return new MusicTrackSettings();
            return new MusicTrackSettings
            {
                Clip            = src.Clip,
                MixerGroup      = src.MixerGroup,
                Volume          = src.Volume,
                Loop            = src.Loop,
                StartDelay      = src.StartDelay,
                FadeInDuration  = src.FadeInDuration,
                FadeOutDuration = src.FadeOutDuration,
                UseCrossfade    = src.UseCrossfade,
            };
        }

        private IMusicSceneProfile FindProfileForScene(string sceneName)
        {
            IMusicSceneProfile globalFallback = null;
            for (int i = 0; i < _profiles.Count; i++)
            {
                var p = _profiles[i];
                if (p == null) continue;
                if (!p.IsGlobal && p.MatchesScene(sceneName))
                    return p;
                if (p.IsGlobal && globalFallback == null)
                    globalFallback = p;
            }
            return globalFallback;
        }

        private static AudioSource AddSource(GameObject root)
        {
            var s = root.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.loop = true;
            return s;
        }
    }

    /// <summary>
    /// Допоміжний MonoBehaviour для запуску корутин від MusicService.
    /// </summary>
    internal sealed class CoroutineRunner : MonoBehaviour { }

}
