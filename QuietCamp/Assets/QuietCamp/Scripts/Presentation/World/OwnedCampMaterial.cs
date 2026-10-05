using UnityEngine;
namespace QuietCamp.Presentation.World
{
    public sealed class OwnedCampMaterial : MonoBehaviour
    {
        public Material Material;
        void OnDestroy(){if(Material!=null)Destroy(Material);}
    }
}
