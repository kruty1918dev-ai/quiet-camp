using System.Collections.Generic;
using Kruty1918.Atmos;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>A readable, cell-sized fire pit. Logs stay below the flames;
    /// the effect does not inherit arbitrary model normalization scales.</summary>
    public sealed class CampfireSite : MonoBehaviour
    {
        readonly List<Material> _owned=new List<Material>();
        Light _light;int _tier=-1;
        public FireVisual Visual { get; private set; }
        public static CampfireSite Create(AssetCatalog catalog,Transform parent,Vector3 position,int layer)
        {
            var go=new GameObject("CampfireSite");go.transform.SetParent(parent,false);go.transform.localPosition=position+Vector3.up*.016f;
            var site=go.AddComponent<CampfireSite>();
            site.Model(catalog,"campfire_stones",.86f,.20f,0);
            var ash=GameObject.CreatePrimitive(PrimitiveType.Cylinder);ash.name="CharcoalBed";ash.transform.SetParent(go.transform,false);
            ash.transform.localScale=new Vector3(.56f,.004f,.56f);ash.transform.localPosition=Vector3.up*.014f;
            if(ash.GetComponent<Collider>()!=null)Destroy(ash.GetComponent<Collider>());
            var material=new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));material.SetColor("_BaseColor",new Color(.13f,.105f,.075f));
            ash.GetComponent<Renderer>().sharedMaterial=material;site._owned.Add(material);
            site.Model(catalog,"log_stack",.51f,.18f,.020f);
            // The atmosphere owns the adaptive wind-driven ember/smoke pool.
            site.Visual=Campfire.Create(go.transform,new CampfireSpec{flameScale=new Vector3(.62f,.86f,.62f),flameHeight=.43f,
                glowRadius=1.35f,glowIntensity=.8f,emberCount=0,lightIntensity=.9f,lightRange=2.7f,lightHeight=.55f});
            foreach(var renderer in site.Visual.GetComponentsInChildren<Renderer>(true))
                if(renderer.sharedMaterial!=null)site._owned.Add(renderer.sharedMaterial);
            site._light=site.Visual.GetComponentInChildren<Light>(true);
            foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;
            RainSurface.Attach(ash);site.ApplyQuality();return site;
        }
        void Model(AssetCatalog catalog,string id,float width,float height,float floor)
        {
            var prefab=catalog?.Prefab(id);if(prefab==null)return;
            var model=Instantiate(prefab,transform);model.name=id;
            var renderers=model.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            float scale=Mathf.Min(width/Mathf.Max(.001f,Mathf.Max(bounds.size.x,bounds.size.z)),height/Mathf.Max(.001f,bounds.size.y));
            model.transform.localScale*=scale;
            bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            model.transform.position+=new Vector3(transform.position.x-bounds.center.x,transform.position.y+floor-bounds.min.y,transform.position.z-bounds.center.z);
            foreach(var collider in model.GetComponentsInChildren<Collider>())Destroy(collider);
            RainSurface.Attach(model);
        }
        void Update()=>ApplyQuality();
        void ApplyQuality()
        {
            int tier=QuietCampBootstrap.ServicesRef?.EffectiveQuality??1;if(tier==_tier)return;_tier=tier;
            if(_light!=null)_light.enabled=tier>0;
        }
        void OnDestroy(){foreach(var material in _owned)if(material!=null)Destroy(material);}
    }
}
