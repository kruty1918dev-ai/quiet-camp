#if UNITY_EDITOR
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    // Only authoring prefabs carry this marker. Export consumes IDs and geometry, not this component.
    public sealed class LevelAuthoringAsset : MonoBehaviour
    {
        public string stableId;
        public bool packedModel, smallPlant;
        public Vector3 baseScale = Vector3.one;
        public string instanceId;
        [System.NonSerialized] public QuietCamp.Infrastructure.CampArtDefinition.Instance sourcePose;
        [System.NonSerialized] public Vector3 capturedPosition, capturedScale;
        [System.NonSerialized] public Quaternion capturedRotation;
    }
}

#endif
