using System.Collections.Generic;
using UnityEngine;
namespace QuietCamp.Presentation.World
{
    public sealed class DecorDensity : MonoBehaviour
    {
        readonly List<GameObject> _decor=new List<GameObject>();int _tier=-1;
        public void Capture(Transform root)
        {
            foreach(Transform t in root)
            {
                if(t.name=="Meadow"||t.name.IndexOf("sign",System.StringComparison.OrdinalIgnoreCase)>=0)continue;
                if(t.name=="ForestSurround") foreach(Transform tree in t)_decor.Add(tree.gameObject);
                else _decor.Add(t.gameObject);
            }
        }
        void Update()
        {
            var quality=QuietCampBootstrap.ServicesRef?.EffectiveQuality??1;if(_tier==quality)return;_tier=quality;
            // Profile changes cannot remove the forest silhouette. Rendering
            // and peripheral effect budgets provide the saving instead.
            for(int i=0;i<_decor.Count;i++)if(_decor[i]!=null)_decor[i].SetActive(true);
        }
    }
}
