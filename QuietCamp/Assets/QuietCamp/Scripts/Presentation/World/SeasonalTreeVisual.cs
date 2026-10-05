using System.Collections.Generic;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Owns seasonal replacements for the few authored trees. Colliders and rule data stay intact.</summary>
    public sealed class SeasonalTreeVisual : MonoBehaviour
    {
        Mesh _mesh;
        Vector3 _leafAt;
        public bool HasLeaves {get;private set;}
        public Vector3 LeafSource=>transform.TransformPoint(_leafAt);
        readonly List<Material> _materials = new List<Material>();
        public static void Apply(GameObject root, LevelData level, string assetId)
        {
            if(root==null||level==null||root.GetComponent<SeasonalTreeVisual>()!=null)return;
            var profile=SeasonProfile.For(level);bool conifer=assetId!=null&&assetId.Contains("pine");
            var shader=Resources.Load<Shader>("QuietCamp/FoliageLit");if(shader==null)return;
            bool bare=profile.BareTree(root.transform.localPosition,level.decorSeed,conifer);
            var geometry=SeasonalTreeGeometry.Read(root);if(geometry.Vertices.Length==0)return;
            var owner=root.AddComponent<SeasonalTreeVisual>();
            owner.HasLeaves=!bare&&!conifer;owner._leafAt=geometry.Bounds.center+Vector3.up*geometry.Height*.18f;
            if(!bare)
            {
                float variation=SeasonProfile.Variation(root.transform.localPosition,level.decorSeed);
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    var slots=renderer.sharedMaterials;
                    for(int i=0;i<slots.Length;i++)
                    {
                        var source=slots[i];if(source==null||!source.HasProperty("_BaseColor"))continue;
                        var color=source.GetColor("_BaseColor");
                        var copy=new Material(source){shader=shader,name=source.name+" ("+profile.Id+")"};owner._materials.Add(copy);
                        CampFoliageResponse.Apply(copy);
                        copy.SetColor("_BaseColor",profile.Palette.Plant(color,variation,conifer));
                        if(copy.HasProperty("_SnowCover"))copy.SetFloat("_SnowCover",profile.Palette.SnowCoverage);
                        slots[i]=copy;
                    }
                    renderer.sharedMaterials=slots;
                    CampFoliageResponse.BindRenderer(renderer);CampFoliageResponse.ExpandBounds(renderer);
                }
                return;
            }
            var branches=SeasonalTreeGeometry.Bare(geometry.Vertices,geometry.Normals,geometry.Indices,geometry.Colors);
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())renderer.enabled=false;
            var child=new GameObject("Seasonal bare branches");child.transform.SetParent(root.transform,false);child.layer=root.layer;
            owner._mesh=new Mesh{name="Bare deciduous branches"};owner._mesh.vertices=branches.Vertices;
            owner._mesh.normals=branches.Normals;owner._mesh.colors=branches.Colors;
            var anchors=new List<Vector4>();foreach(var p in branches.Vertices)anchors.Add(new Vector4(0,0,geometry.Bounds.min.y,geometry.Height));
            owner._mesh.SetUVs(1,anchors);owner._mesh.triangles=branches.Indices;owner._mesh.RecalculateBounds();
            var bounds=owner._mesh.bounds;bounds.Expand(geometry.Height*.5f);owner._mesh.bounds=bounds;
            child.AddComponent<MeshFilter>().sharedMesh=owner._mesh;
            var material=new Material(shader){name="Bare seasonal wood"};owner._materials.Add(material);
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_VertexTint",1);material.SetFloat("_ClusterWind",1);
            material.SetFloat("_TreeWind",1);material.SetFloat("_SwayAmp",.08f);material.SetFloat("_SnowCover",profile.Palette.SnowCoverage);
            var r=child.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
            RainSurface.Attach(child);
        }
        void OnDestroy(){if(_mesh!=null)Destroy(_mesh);foreach(var material in _materials)if(material!=null)Destroy(material);}
    }
}
