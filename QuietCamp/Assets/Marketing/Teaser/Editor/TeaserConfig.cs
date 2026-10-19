using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Timeline;

namespace QuietCamp.Marketing
{
    [CreateAssetMenu(menuName = "Marketing/Teaser configuration")]
    public sealed class TeaserConfig : ScriptableObject
    {
        public const int Fps = 30;
        public int seed = 1918;
        public int width = 1920, height = 1080;
        public string exportFolder = "Marketing/Exports";
        public string revision = "v01";
        public float blackAt = 41.4f, fadeAt = 41.4f, fadeSeconds = .12f;
        public float musicGain = .24f, ambienceGain = .8f, effectsGain = .8f;
        public AudioClip music;
        public VolumeProfile profile;
        public TimelineAsset timeline;
        public GameObject cameraRig;
        public TeaserShot[] shots;
    }

    [Serializable]
    public sealed class TeaserShot
    {
        public string id, levelId, purpose;
        public string scene = "Camp";
        public int frames;
        [Tooltip("Hard cuts preserve real multi-scene events and exact duration.")]
        public string transition = "Cut";
        public float transitionSeconds;
        public bool gameplay, startWithTents, complete;
        public Vector3 target, start, end;
        public float pitch = 47, yaw = 225, ortho = 7;
        public AnimationClip motion;
        public int initialTentCount = -1, alternateGuest = -1;
        public float endOrtho, endYaw, travel;
        public Vector3 endTarget;
        public string movement = "Slide";
        public float sunMultiplier = 1, ambienceMultiplier = 1, musicMultiplier = 1;
        public VolumeProfile look;
        public TeaserCue[] cues = Array.Empty<TeaserCue>();
    }

    [Serializable] public sealed class TeaserCue
    {
        public int frame;
        public string command;
    }
}
