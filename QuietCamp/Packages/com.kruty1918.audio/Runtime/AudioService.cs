using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

namespace Kruty1918.Audio
{

    public sealed class AudioService : IAudioService, IDisposable
    {
        private const string RootName = "AudioPool";

        private sealed class BusDuck
        {
            public AudioBus Bus;
            public float Target;
            public float Attack;
            public float Hold;
            public float Release;
            public float Elapsed;
            public float StartMultiplier;
        }

        private readonly IAudioCatalog _registry;
        private readonly IAudioSceneOverrides _sceneOverrides;
        private readonly Queue<AudioSource> _available = new Queue<AudioSource>();
        private readonly List<AudioSource> _active = new List<AudioSource>();
        private readonly Dictionary<AudioSource, string> _activeKeys = new Dictionary<AudioSource, string>();
        private readonly Dictionary<string, int> _activeCountByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<AudioSource, AudioBus> _activeBusBySource = new Dictionary<AudioSource, AudioBus>();
        private readonly Dictionary<AudioSource, float> _activeBaseVolumeBySource = new Dictionary<AudioSource, float>();
        private readonly Dictionary<AudioSource, string> _activeChannelBySource = new Dictionary<AudioSource, string>();
        private readonly Dictionary<AudioSource, float> _activeScaleBySource = new Dictionary<AudioSource, float>();
        private readonly Dictionary<string, float> _channelVolumes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly List<BusDuck> _ducks = new List<BusDuck>();
        private readonly HashSet<string> _warnedMissingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<AudioSource> _awakeSources = new List<AudioSource>();
        private readonly float[] _busVolumes = { 1f, 1f, 1f, 1f, 1f };

        private GameObject _root;
        private UnityEngine.SceneManagement.SceneHandle _lastProcessedSceneHandle = default;

        public AudioService(IAudioCatalog catalog, IAudioSceneOverrides sceneOverrides)
        {
            _registry = catalog;
            _sceneOverrides = sceneOverrides;
        }

        public void Initialize()
        {
            _root = new GameObject(RootName);
            if (Application.isPlaying && (_registry == null || _registry.PersistAcrossScenes))
                UnityEngine.Object.DontDestroyOnLoad(_root);

            int poolSize = _registry != null ? _registry.DefaultPoolSize : 12;
            if (_registry != null && _registry.Sounds != null)
            {
                for (int i = 0; i < _registry.Sounds.Length; i++)
                {
                    var sound = _registry.Sounds[i];
                    if (sound != null)
                        poolSize += Mathf.Max(0, sound.PoolWarmup);
                }
            }

            for (int i = 0; i < poolSize; i++)
                _available.Enqueue(CreateSource());

            if (_registry != null && _registry.Channels != null)
            {
                for (int i = 0; i < _registry.Channels.Length; i++)
                {
                    var channel = _registry.Channels[i];
                    if (channel != null && !string.IsNullOrWhiteSpace(channel.Key))
                        _channelVolumes[channel.Key.Trim()] = Mathf.Clamp01(channel.Volume);
                }
            }

            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            // Перша сцена вже завантажена до Initialize() — запустити auto-play вручну.
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.IsValid())
                OnSceneLoaded(activeScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        public void Tick()
        {
            RefreshAutoPlayForCurrentScene();
            TickDucks(Time.unscaledDeltaTime);

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var source = _active[i];
                if (source == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }

                if (source.loop || source.isPlaying)
                    continue;

                Release(source);
            }
        }

        public void Dispose()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

            if (_root != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_root);
                else
                    UnityEngine.Object.DestroyImmediate(_root);
            }

            _available.Clear();
            _active.Clear();
            _activeKeys.Clear();
            _activeCountByKey.Clear();
            _activeChannelBySource.Clear();
            _activeScaleBySource.Clear();
            _ducks.Clear();
            _awakeSources.Clear();
        }

        public bool TryGetSound(string key, out AudioSoundDefinition sound)
        {
            sound = null;
            return _registry != null && _registry.TryGet(key, out sound);
        }

        public AudioSoundDefinition GetSound(string key)
        {
            TryGetSound(key, out var sound);
            return sound;
        }

        public AudioHandle Play(string key)
            => PlayInternal(key, AudioPlayOptions.Default, null);

        public AudioHandle PlayAt(string key, Vector3 position, float volumeScale = 1f)
            => PlayInternal(key, new AudioPlayOptions(position, null, volumeScale), null);

        public AudioHandle Play(string key, AudioPlayOptions options)
            => PlayInternal(key, options, null);

        public void SetBusVolume(AudioBus bus, float volume)
        {
            int busIndex = GetBusIndex(bus);
            float clampedVolume = Mathf.Clamp01(volume);
            if (Mathf.Approximately(_busVolumes[busIndex], clampedVolume))
                return;

            _busVolumes[busIndex] = clampedVolume;
            ApplyBusVolumeToActiveSources(bus);

            // Master впливає на всі bus — перерахувати решту активних сорсів.
            if (bus == AudioBus.Master)
            {
                for (int i = 0; i < _active.Count; i++)
                {
                    var src = _active[i];
                    if (src == null)
                        continue;
                    if (_activeBusBySource.TryGetValue(src, out var srcBus)
                        && srcBus != AudioBus.Master
                        && _activeBaseVolumeBySource.TryGetValue(src, out float srcBase))
                        ApplyBusVolume(src, srcBus, srcBase);
                }
            }
        }

        public float GetBusVolume(AudioBus bus)
            => _busVolumes[GetBusIndex(bus)];

        public void SetChannelVolume(string channelKey, float volume)
        {
            if (string.IsNullOrWhiteSpace(channelKey))
                return;

            string key = channelKey.Trim();
            float clamped = Mathf.Clamp01(volume);
            if (_channelVolumes.TryGetValue(key, out float existing) && Mathf.Approximately(existing, clamped))
                return;

            _channelVolumes[key] = clamped;

            for (int i = 0; i < _active.Count; i++)
            {
                var source = _active[i];
                if (source == null)
                    continue;

                if (!_activeChannelBySource.TryGetValue(source, out var channel)
                    || !string.Equals(channel, key, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!_activeBusBySource.TryGetValue(source, out var bus)
                    || !_activeBaseVolumeBySource.TryGetValue(source, out float baseVolume))
                    continue;

                ApplyBusVolume(source, bus, baseVolume);
            }
        }

        public float GetChannelVolume(string channelKey)
        {
            if (string.IsNullOrWhiteSpace(channelKey))
                return 1f;

            return _channelVolumes.TryGetValue(channelKey.Trim(), out float volume) ? volume : 1f;
        }

        public void DuckBus(AudioBus bus, float targetVolume, float attack, float hold, float release)
        {
            _ducks.Add(new BusDuck
            {
                Bus = bus,
                Target = Mathf.Clamp01(targetVolume),
                Attack = Mathf.Max(0f, attack),
                Hold = Mathf.Max(0f, hold),
                Release = Mathf.Max(0.01f, release),
                Elapsed = 0f,
                StartMultiplier = GetBusDuckMultiplier(bus),
            });
        }

        public float GetBusDuckMultiplier(AudioBus bus)
        {
            float multiplier = 1f;
            for (int i = 0; i < _ducks.Count; i++)
            {
                var duck = _ducks[i];
                if (duck.Bus != bus)
                    continue;

                float value = EvaluateDuck(duck);
                if (value < multiplier)
                    multiplier = value;
            }
            return multiplier;
        }

        public void SetPlaybackScale(AudioHandle handle, float scale)
        {
            var source = handle.Source;
            if (source == null || !_active.Contains(source))
                return;

            float clamped = Mathf.Clamp01(scale);
            if (_activeScaleBySource.TryGetValue(source, out float existing)
                && Mathf.Approximately(existing, clamped))
                return;

            _activeScaleBySource[source] = clamped;
            if (_activeBusBySource.TryGetValue(source, out var bus)
                && _activeBaseVolumeBySource.TryGetValue(source, out float baseVolume))
                ApplyBusVolume(source, bus, baseVolume);
        }

        public float GetPlaybackScale(AudioHandle handle)
        {
            var source = handle.Source;
            if (source == null)
                return 1f;

            return _activeScaleBySource.TryGetValue(source, out float scale) ? scale : 1f;
        }

        private void TickDucks(float dt)
        {
            if (_ducks.Count == 0)
                return;

            bool changed = false;
            for (int i = _ducks.Count - 1; i >= 0; i--)
            {
                var duck = _ducks[i];
                duck.Elapsed += dt;
                if (duck.Elapsed >= duck.Attack + duck.Hold + duck.Release)
                    _ducks.RemoveAt(i);
                changed = true;
            }

            if (!changed)
                return;

            // Оновити гучність усіх активних сорсів (duck-множники змінилися).
            for (int i = 0; i < _active.Count; i++)
            {
                var source = _active[i];
                if (source == null)
                    continue;

                if (_activeBusBySource.TryGetValue(source, out var bus)
                    && _activeBaseVolumeBySource.TryGetValue(source, out float baseVolume))
                {
                    ApplyBusVolume(source, bus, baseVolume);
                }
            }
        }

        private static float EvaluateDuck(BusDuck duck)
        {
            float t = duck.Elapsed;
            if (t < duck.Attack)
            {
                float k = duck.Attack <= 0f ? 1f : t / duck.Attack;
                return Mathf.Lerp(duck.StartMultiplier, duck.Target, k);
            }

            t -= duck.Attack;
            if (t < duck.Hold)
                return duck.Target;

            t -= duck.Hold;
            float releaseK = duck.Release <= 0f ? 1f : Mathf.Clamp01(t / duck.Release);
            return Mathf.Lerp(duck.Target, 1f, releaseK);
        }

        private AudioHandle PlayInternal(string key, AudioPlayOptions options, string sceneNameForOverrides)
        {
            if (!TryGetSound(key, out var sound))
            {
                if (!string.IsNullOrWhiteSpace(key) && _warnedMissingKeys.Add(key))
                    Debug.LogWarning($"[AudioService] Unknown sound key '{key}'.");
                return default;
            }

            var clip = ResolveClip(sound);
            if (clip == null)
            {
                return default;
            }

            if (!CanPlay(sound))
            {
                return default;
            }

            var source = GetConfiguredSourceInternal(sound, options.Parent);
            source.clip = clip;
            float baseVolume = Mathf.Clamp01(ResolveVolume(sound) * options.VolumeScale);
            source.volume = baseVolume;
            source.pitch = Mathf.Clamp(ResolvePitch(sound) + options.PitchOffset, -3f, 3f);
            source.loop = options.LoopOverride ?? sound.Loop;

            if (options.Position.HasValue)
            {
                source.transform.position = options.Position.Value;
                source.spatialBlend = Mathf.Max(source.spatialBlend, 1f);
            }
            else if (options.Parent != null)
            {
                source.transform.localPosition = Vector3.zero;
            }
            else
            {
                source.transform.position = Vector3.zero;
            }

            if (_sceneOverrides != null)
            {
                string scn = !string.IsNullOrWhiteSpace(sceneNameForOverrides)
                    ? sceneNameForOverrides
                    : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (_sceneOverrides.TryGet(scn, sound.Key, out var ov))
                {
                    if (ov.OverrideVolume)       baseVolume                  = Mathf.Clamp01(ov.Volume * options.VolumeScale);
                    if (ov.OverridePitch)        source.pitch                 = Mathf.Clamp(ov.Pitch + options.PitchOffset, -3f, 3f);
                    if (ov.OverrideMixerGroup)   source.outputAudioMixerGroup = ov.MixerGroup;
                    if (ov.OverrideLoop)         source.loop                  = ov.Loop;
                    if (ov.OverrideSpatialBlend) source.spatialBlend          = Mathf.Clamp01(ov.SpatialBlend);
                    if (ov.OverridePriority)     source.priority              = Mathf.Clamp(ov.Priority, 0, 256);
                }
            }

            ApplyBusVolume(source, sound.Bus, baseVolume);
            RegisterActive(source, sound.Key, sound.Bus, baseVolume, sound.Channel);

            var duck = sound.Duck;
            if (duck != null && duck.Enabled)
                DuckBus(duck.TargetBus, duck.Amount, duck.Attack, duck.Hold, duck.Release);

            source.Play();
            return new AudioHandle(source);
        }

        private void RefreshAutoPlayForCurrentScene()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
                return;

            if (activeScene.handle.Equals(_lastProcessedSceneHandle))
                return;

            RefreshAutoPlayForScene(activeScene);
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
            => RefreshAutoPlayForScene(scene);

        private void RefreshAutoPlayForScene(UnityEngine.SceneManagement.Scene scene)
        {
            _lastProcessedSceneHandle = scene.handle;

            for (int i = _awakeSources.Count - 1; i >= 0; i--)
            {
                StopAndReleaseSource(_awakeSources[i]);
            }

            _awakeSources.Clear();

            if (_sceneOverrides == null) return;

            string sceneName = scene.name;
            foreach (var ov in _sceneOverrides.Overrides)
            {
                if (ov == null || !ov.PlayOnAwake) continue;
                if (!string.IsNullOrEmpty(ov.SceneName)
                    && !string.Equals(ov.SceneName, sceneName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var handle = PlayInternal(ov.SoundKey, AudioPlayOptions.Default, sceneName);
                if (handle.IsValid && handle.Source != null)
                    _awakeSources.Add(handle.Source);
            }
        }

        private void StopAndReleaseSource(AudioSource source)
        {
            if (source == null)
                return;

            if (_active.Contains(source))
            {
                source.Stop();
                Release(source);
                return;
            }

            source.Stop();
        }

        public AudioSource GetConfiguredSource(string key, Transform parent = null)
        {
            if (!TryGetSound(key, out var sound))
                return null;

            return GetConfiguredSourceInternal(sound, parent);
        }

        public void StopByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var source = _active[i];
                if (source == null)
                    continue;

                if (_activeKeys.TryGetValue(source, out var sourceKey)
                    && string.Equals(sourceKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    source.Stop();
                    Release(source);
                }
            }
        }

        public void StopAll(AudioBus? bus = null)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var source = _active[i];
                if (source == null)
                    continue;

                if (bus.HasValue && _activeKeys.TryGetValue(source, out var key) && TryGetSound(key, out var sound) && sound.Bus != bus.Value)
                    continue;

                source.Stop();
                Release(source);
            }
        }

        public string[] GetKeys()
            => _registry != null ? _registry.GetKeys() : Array.Empty<string>();

        private void ApplyBusVolumeToActiveSources(AudioBus bus)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var source = _active[i];
                if (source == null)
                    continue;

                if (!_activeBusBySource.TryGetValue(source, out AudioBus sourceBus) || sourceBus != bus)
                    continue;

                if (!_activeBaseVolumeBySource.TryGetValue(source, out float baseVolume))
                    continue;

                ApplyBusVolume(source, sourceBus, baseVolume);
            }
        }

        private AudioSource GetConfiguredSourceInternal(AudioSoundDefinition sound, Transform parent)
        {
            var source = _available.Count > 0 ? _available.Dequeue() : CreateSource();
            source.gameObject.SetActive(true);
            source.transform.SetParent(parent != null ? parent : _root.transform, false);
            ConfigureSource(source, sound);
            return source;
        }

        private AudioSource CreateSource()
        {
            var go = new GameObject("AudioSource");
            go.transform.SetParent(_root != null ? _root.transform : null, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            ResetSourceForPool(source);
            source.gameObject.SetActive(false);
            return source;
        }

        private void ConfigureSource(AudioSource source, AudioSoundDefinition sound)
        {
            source.outputAudioMixerGroup = sound.MixerGroup != null
                ? sound.MixerGroup
                : (_registry != null ? _registry.GetBusGroup(sound.Bus) : null);
            source.priority = Mathf.Clamp(sound.Priority, 0, 256);
            source.spatialBlend = Mathf.Clamp01(sound.SpatialBlend);
            source.dopplerLevel = Mathf.Max(0f, sound.DopplerLevel);
            source.reverbZoneMix = Mathf.Clamp(sound.ReverbZoneMix, 0f, 1.1f);

            if (sound.SpatialBlend > 0f)
            {
                source.rolloffMode = sound.RolloffMode;
                if (sound.MinDistance > 0f) source.minDistance = sound.MinDistance;
                if (sound.MaxDistance > 0f) source.maxDistance = sound.MaxDistance;
            }

            ApplyEffects(source.gameObject, sound.Effects);
        }

        private static void ApplyEffects(GameObject go, AudioEffectSettings effects)
        {
            if (effects == null)
                effects = new AudioEffectSettings();

            ConfigureLowPass(go, effects);
            ConfigureHighPass(go, effects);
            ConfigureEcho(go, effects);
            ConfigureReverb(go, effects);
            ConfigureDistortion(go, effects);
            ConfigureChorus(go, effects);
        }

        private static void ConfigureLowPass(GameObject go, AudioEffectSettings effects)
        {
            var filter = go.GetComponent<AudioLowPassFilter>();
            if (!effects.EnableLowPass)
            {
                if (filter != null) filter.enabled = false;
                return;
            }

            filter ??= go.AddComponent<AudioLowPassFilter>();
            filter.enabled = true;
            filter.cutoffFrequency = effects.LowPassCutoff;
            filter.lowpassResonanceQ = effects.LowPassResonance;
        }

        private static void ConfigureHighPass(GameObject go, AudioEffectSettings effects)
        {
            var filter = go.GetComponent<AudioHighPassFilter>();
            if (!effects.EnableHighPass)
            {
                if (filter != null) filter.enabled = false;
                return;
            }

            filter ??= go.AddComponent<AudioHighPassFilter>();
            filter.enabled = true;
            filter.cutoffFrequency = effects.HighPassCutoff;
            filter.highpassResonanceQ = effects.HighPassResonance;
        }

        private static void ConfigureEcho(GameObject go, AudioEffectSettings effects)
        {
            var filter = go.GetComponent<AudioEchoFilter>();
            if (!effects.EnableEcho)
            {
                if (filter != null) filter.enabled = false;
                return;
            }

            filter ??= go.AddComponent<AudioEchoFilter>();
            filter.enabled = true;
            filter.delay = effects.EchoDelay;
            filter.decayRatio = effects.EchoDecayRatio;
            filter.wetMix = effects.EchoWetMix;
            filter.dryMix = effects.EchoDryMix;
        }

        private static void ConfigureReverb(GameObject go, AudioEffectSettings effects)
        {
            var filter = go.GetComponent<AudioReverbFilter>();
            if (!effects.EnableReverb)
            {
                if (filter != null) filter.enabled = false;
                return;
            }

            filter ??= go.AddComponent<AudioReverbFilter>();
            filter.enabled = true;
            filter.reverbPreset = effects.ReverbPreset;
        }

        private static void ConfigureDistortion(GameObject go, AudioEffectSettings effects)
        {
            var filter = go.GetComponent<AudioDistortionFilter>();
            if (!effects.EnableDistortion)
            {
                if (filter != null) filter.enabled = false;
                return;
            }

            filter ??= go.AddComponent<AudioDistortionFilter>();
            filter.enabled = true;
            filter.distortionLevel = effects.DistortionLevel;
        }

        private static void ConfigureChorus(GameObject go, AudioEffectSettings effects)
        {
            var filter = go.GetComponent<AudioChorusFilter>();
            if (!effects.EnableChorus)
            {
                if (filter != null) filter.enabled = false;
                return;
            }

            filter ??= go.AddComponent<AudioChorusFilter>();
            filter.enabled = true;
            filter.dryMix = effects.ChorusDryMix;
            filter.wetMix1 = effects.ChorusWetMix1;
            filter.wetMix2 = effects.ChorusWetMix2;
            filter.wetMix3 = effects.ChorusWetMix3;
            filter.delay = effects.ChorusDelay;
            filter.rate = effects.ChorusRate;
            filter.depth = effects.ChorusDepth;
        }

        private bool CanPlay(AudioSoundDefinition sound)
        {
            int max = Mathf.Max(1, sound.MaxSimultaneous);
            return !_activeCountByKey.TryGetValue(sound.Key, out int count) || count < max;
        }

        private static AudioClip ResolveClip(AudioSoundDefinition sound)
        {
            if (sound.Variants != null && sound.Variants.Length > 0)
            {
                var valid = new List<AudioClip>();
                for (int i = 0; i < sound.Variants.Length; i++)
                    if (sound.Variants[i] != null)
                        valid.Add(sound.Variants[i]);

                if (valid.Count > 0)
                    return valid[UnityEngine.Random.Range(0, valid.Count)];
            }

            return sound.Clip;
        }

        private static float ResolveVolume(AudioSoundDefinition sound)
        {
            float delta = sound.VolumeRandom > 0f ? UnityEngine.Random.Range(-sound.VolumeRandom, sound.VolumeRandom) : 0f;
            return Mathf.Clamp01(sound.Volume + delta);
        }

        private static float ResolvePitch(AudioSoundDefinition sound)
        {
            float delta = sound.PitchRandom > 0f ? UnityEngine.Random.Range(-sound.PitchRandom, sound.PitchRandom) : 0f;
            return Mathf.Clamp(sound.Pitch + delta, -3f, 3f);
        }

        private void ApplyBusVolume(AudioSource source, AudioBus bus, float baseVolume)
        {
            if (source == null)
                return;

            float channelVolume = 1f;
            if (_activeChannelBySource.TryGetValue(source, out var channel)
                && !string.IsNullOrEmpty(channel))
            {
                channelVolume = GetChannelVolume(channel);
            }

            float playbackScale = _activeScaleBySource.TryGetValue(source, out float scale)
                ? scale
                : 1f;

            float masterScale = bus == AudioBus.Master ? 1f : GetBusVolume(AudioBus.Master);

            source.volume = Mathf.Clamp01(
                baseVolume * masterScale * GetBusVolume(bus)
                * GetBusDuckMultiplier(bus) * channelVolume * playbackScale);
        }

        private int GetBusIndex(AudioBus bus)
            => Mathf.Clamp((int)bus, 0, _busVolumes.Length - 1);

        private void RegisterActive(AudioSource source, string key, AudioBus bus, float baseVolume, string channel = null)
        {
            if (!_active.Contains(source))
                _active.Add(source);

            _activeKeys[source] = key;
            _activeBusBySource[source] = bus;
            _activeBaseVolumeBySource[source] = Mathf.Clamp01(baseVolume);
            if (string.IsNullOrWhiteSpace(channel))
                _activeChannelBySource.Remove(source);
            else
                _activeChannelBySource[source] = channel.Trim();
            _activeCountByKey.TryGetValue(key, out int count);
            _activeCountByKey[key] = count + 1;
            _activeScaleBySource[source] = 1f;
        }

        private void Release(AudioSource source)
        {
            if (source == null)
                return;

            _active.Remove(source);
            if (_activeKeys.TryGetValue(source, out var key))
            {
                _activeKeys.Remove(source);
                if (_activeCountByKey.TryGetValue(key, out int count))
                {
                    count--;
                    if (count <= 0) _activeCountByKey.Remove(key);
                    else _activeCountByKey[key] = count;
                }
            }

            _activeBusBySource.Remove(source);
            _activeBaseVolumeBySource.Remove(source);
            _activeChannelBySource.Remove(source);
            _activeScaleBySource.Remove(source);

            source.Stop();
            source.clip = null;
            ResetSourceForPool(source);
            source.transform.SetParent(_root != null ? _root.transform : null, false);
            source.gameObject.SetActive(false);
            _available.Enqueue(source);
        }

        private static void ResetSourceForPool(AudioSource source)
        {
            if (source == null)
                return;

            source.volume = 1f;
            source.pitch = 1f;
            source.loop = false;
            source.mute = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.reverbZoneMix = 1f;
            source.priority = 128;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1f;
            source.maxDistance = 500f;
            source.outputAudioMixerGroup = null;
        }
    }

}
