using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Kruty1918.Audio
{
    /// <summary>Read-side catalog of sound/channel/bus definitions consumed by AudioService.</summary>
    public interface IAudioCatalog
    {
        AudioSoundDefinition[] Sounds { get; }
        AudioChannelDefinition[] Channels { get; }
        int DefaultPoolSize { get; }
        bool PersistAcrossScenes { get; }
        bool TryGet(string key, out AudioSoundDefinition definition);
        AudioMixerGroup GetBusGroup(AudioBus bus);
        string[] GetKeys();
    }

    /// <summary>Per-scene sound overrides consumed by AudioService.</summary>
    public interface IAudioSceneOverrides
    {
        IReadOnlyList<SoundSceneOverride> Overrides { get; }
        bool TryGet(string sceneName, string soundKey, out SoundSceneOverride result);
    }

    /// <summary>Music profile surface consumed by MusicService.</summary>
    public interface IMusicSceneProfile
    {
        bool IsGlobal { get; }
        MusicTrackSettings DefaultMusic { get; }
        MusicTrackSettings EpicMusic { get; }
        bool PreserveMusicBetweenScenes { get; }
        float SceneTransitionDuration { get; }
        bool MatchesScene(string sceneName);
    }

    /// <summary>
    /// Per-scene override для окремого звуку.
    /// SceneName = "" означає глобальний override (всі сцени).
    /// </summary>
    [Serializable]
    public sealed class SoundSceneOverride
    {
        [HideInInspector] public string SceneName;
        [HideInInspector] public string SoundKey;

        public bool OverrideVolume;
        [Range(0f, 1f)] public float Volume = 1f;

        public bool OverridePitch;
        [Range(-3f, 3f)] public float Pitch = 1f;

        public bool OverrideMixerGroup;
        public AudioMixerGroup MixerGroup;

        public bool OverrideLoop;
        public bool Loop;

        public bool OverrideSpatialBlend;
        [Range(0f, 1f)] public float SpatialBlend;

        public bool OverridePriority;
        [Range(0, 256)] public int Priority = 128;

        /// <summary>
        /// Автоматично відтворити цей звук при завантаженні сцени.
        /// Не вимагає окремого OverrideXxx — просто встановіть true.
        /// </summary>
        public bool PlayOnAwake;
    }

    // ─── Data ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Налаштування одного музичного треку (стартові значення — можна змінити в runtime).
    /// </summary>
    [Serializable]
    public sealed class MusicTrackSettings
    {
        [Tooltip("AudioClip що грає. null = тиша.")]
        public AudioClip Clip;

        [Tooltip("AudioMixerGroup для маршрутизації (Music-група).")]
        public AudioMixerGroup MixerGroup;

        [Range(0f, 1f), Tooltip("Стартова гучність.")]
        public float Volume = 0.7f;

        [Tooltip("Чи грати в циклі.")]
        public bool Loop = true;

        [Min(0f), Tooltip("Затримка (сек) перед першим запуском.")]
        public float StartDelay = 0f;

        [Tooltip("Тривалість fade-in при запуску (сек).")]
        [Min(0f)] public float FadeInDuration = 1.5f;

        [Tooltip("Тривалість fade-out при зупинці (сек).")]
        [Min(0f)] public float FadeOutDuration = 1.5f;

        [Tooltip("Якщо true — при переході до нового треку (або сцени) crossfade, інакше: fade-out → fade-in.")]
        public bool UseCrossfade = true;
    }

    /// <summary>
    /// Посилання на сцену за іменем і шляхом.
    /// </summary>
    [Serializable]
    public sealed class SceneReference
    {
        [Tooltip("Відображуване ім'я сцени (має збігатися із зареєстрованою сценою в Build Settings або бути просто ідентифікатором).")]
        public string SceneName;

        [Tooltip("Повний шлях до .unity-файлу (опційно, для зручності).")]
        public string ScenePath;
    }
}
