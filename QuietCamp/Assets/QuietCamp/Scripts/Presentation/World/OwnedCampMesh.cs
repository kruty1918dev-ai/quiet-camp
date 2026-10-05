using UnityEngine;
namespace QuietCamp.Presentation.World
{
    public sealed class OwnedCampMesh : MonoBehaviour
    {
        public Mesh Mesh;
        void OnDestroy() { if (Mesh != null) Destroy(Mesh); }
    }
}
