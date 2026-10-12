using UnityEngine;
namespace QuietCamp.Presentation.World
{
    [ExecuteAlways] public sealed class OwnedCampMaterial : MonoBehaviour
    {
        public Material Material;
        void OnDestroy(){if(Material!=null){if(UnityEngine.Application.isPlaying)Destroy(Material);else DestroyImmediate(Material);}}
    }
}
