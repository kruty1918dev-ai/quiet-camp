using UnityEngine;
namespace QuietCamp.Presentation.World
{
    [ExecuteAlways] public sealed class OwnedCampMesh : MonoBehaviour
    {
        public Mesh Mesh;
        void OnDestroy() { if (Mesh != null) { if (UnityEngine.Application.isPlaying) Destroy(Mesh); else DestroyImmediate(Mesh); } }
    }
}
