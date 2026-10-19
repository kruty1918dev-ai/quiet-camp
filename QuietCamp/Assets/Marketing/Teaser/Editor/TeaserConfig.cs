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
    }
}
