using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    public sealed class CampLandmarkVisual : MonoBehaviour
    {
        LevelData _level;
        bool _cared;
        public void Configure(LevelData level) => _level = level;
        public void ApplyCare()
        {
            if (_cared || _level == null) return;
            var mesh = CampStoryComposer.Compose(_level, true);
            if (mesh == null) return;
            var owner = GetComponent<OwnedEnvironmentMesh>();
            var filter = GetComponent<MeshFilter>();
            if (owner == null || filter == null) { Destroy(mesh); return; }
            var previous = filter.sharedMesh;
            filter.sharedMesh = mesh; owner.Mesh = mesh; _cared = true;
            if (previous != null) Destroy(previous);
        }
    }
}
