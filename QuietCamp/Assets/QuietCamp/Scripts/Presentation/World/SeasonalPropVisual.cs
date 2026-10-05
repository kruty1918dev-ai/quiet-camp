using System.Collections.Generic;
using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>Snow on exposed rock and wood faces, with stable geometry and scene-owned materials.</summary>
    public sealed class SeasonalPropVisual:MonoBehaviour
    {
        readonly List<Material> _materials=new List<Material>();
        public static void Apply(GameObject root,LevelData level,string assetId)
        {
            if(root==null||!SeasonProfile.For(level).Winter||root.GetComponent<SeasonalPropVisual>()!=null)return;
            if(assetId==null||!(assetId.StartsWith("stone")||assetId.StartsWith("stump")||assetId.StartsWith("log")))return;
            var shader=Resources.Load<Shader>("QuietCamp/FoliageLit");if(shader==null)return;
            var owner=root.AddComponent<SeasonalPropVisual>();var copies=new Dictionary<Material,Material>();
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int slot=0;slot<materials.Length;slot++)
                {
                    var source=materials[slot];if(source==null||!source.HasProperty("_BaseColor"))continue;
                    if(!copies.TryGetValue(source,out var copy))
                    {
                        copy=new Material(shader){name=source.name+" (snow-capped prop)"};
                        copy.SetColor("_BaseColor",source.GetColor("_BaseColor"));copy.SetFloat("_SnowCover",1);
                        copy.SetFloat("_SwayAmp",0);copy.SetFloat("_FlutterAmp",0);copy.SetFloat("_InnerShade",0);
                        copies.Add(source,copy);owner._materials.Add(copy);
                    }
                    materials[slot]=copy;
                }
                renderer.sharedMaterials=materials;
            }
        }
        void OnDestroy(){foreach(var material in _materials)if(material!=null)Destroy(material);}
    }
}
