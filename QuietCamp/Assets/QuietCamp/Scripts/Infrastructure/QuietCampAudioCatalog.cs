using System;
using System.Collections.Generic;
using Kruty1918.Audio;
using UnityEngine;
using UnityEngine.Audio;
namespace QuietCamp.Infrastructure
{
    /// <summary>
    /// IAudioCatalog backed by a generated asset: exact WAV references and bus
    /// routing from audio_catalog.json, built once by MvpContentBuilder.
    /// </summary>
    public sealed class QuietCampAudioCatalog : ScriptableObject, IAudioCatalog, IAudioSceneOverrides
    {
        public const string ResourcePath = "QuietCamp/AudioCatalog";

        [SerializeField] AudioSoundDefinition[] _sounds = new AudioSoundDefinition[0];
        /// <summary>Procedurally generated extras merged in alongside the kit sounds.</summary>
        [SerializeField] AudioSoundDefinition[] _extraSounds = new AudioSoundDefinition[0];
        [SerializeField] AudioChannelDefinition[] _channels = new AudioChannelDefinition[0];
        [SerializeField] AudioBusGroupBinding[] _busGroups = new AudioBusGroupBinding[0];
        [SerializeField] SoundSceneOverride[] _sceneOverrides = new SoundSceneOverride[0];
        [SerializeField] int _defaultPoolSize = 8;
        [SerializeField] bool _persistAcrossScenes = true;

        Dictionary<string, AudioSoundDefinition> _map;

        public AudioSoundDefinition[] Sounds => _sounds;
        public AudioSoundDefinition[] ExtraSounds => _extraSounds;
        public AudioChannelDefinition[] Channels => _channels;
        public int DefaultPoolSize => _defaultPoolSize;
        public bool PersistAcrossScenes => _persistAcrossScenes;
        public IReadOnlyList<SoundSceneOverride> Overrides => _sceneOverrides;

        public static QuietCampAudioCatalog Load()
            => Resources.Load<QuietCampAudioCatalog>(ResourcePath);

        void OnEnable() => _map = null;

        Dictionary<string, AudioSoundDefinition> Map
        {
            get
            {
                if (_map == null)
                {
                    _map = new Dictionary<string, AudioSoundDefinition>(StringComparer.OrdinalIgnoreCase);
                    foreach (var s in _sounds)
                        if (s != null && !string.IsNullOrEmpty(s.Key))
                            _map[s.Key] = s;
                    foreach (var s in _extraSounds)
                        if (s != null && !string.IsNullOrEmpty(s.Key))
                            _map[s.Key] = s;
                }
                return _map;
            }
        }

        public bool TryGet(string key, out AudioSoundDefinition definition)
            => Map.TryGetValue(key ?? string.Empty, out definition);

        public AudioMixerGroup GetBusGroup(AudioBus bus)
        {
            if (_busGroups != null)
                foreach (var b in _busGroups)
                    if (b != null && b.Bus == bus) return b.MixerGroup;
            return null;
        }

        public string[] GetKeys()
        {
            var keys = new string[_sounds.Length];
            for (var i = 0; i < _sounds.Length; i++) keys[i] = _sounds[i]?.Key;
            return keys;
        }

        public bool TryGet(string sceneName, string soundKey, out SoundSceneOverride result)
        {
            result = null;
            foreach (var ov in _sceneOverrides)
                if (ov != null
                    && string.Equals(ov.SoundKey, soundKey, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrEmpty(ov.SceneName)
                        || string.Equals(ov.SceneName, sceneName, StringComparison.OrdinalIgnoreCase)))
                {
                    result = ov;
                    return true;
                }
            return false;
        }
    }
}
