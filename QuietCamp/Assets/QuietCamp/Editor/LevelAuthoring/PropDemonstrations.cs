using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    // Separate authoring examples. Never installs content into the campaign.
    public static class PropDemonstrations
    {
        public static LevelAuthoringDocument Create(string kind)
        {
            if (kind != "aircraft" && kind != "windmill" && kind != "watermill") throw new ArgumentException("Невідомий приклад.");
            var d = LevelAuthoringDocument.Create(true, "QC007");
            d.level.id = "QC_PROPS_" + kind.ToUpperInvariant(); d.art.levelId = d.level.id;
            d.art.orthographicSize = 9.7f; d.art.cameraTarget = new[] { 1.4f, 0f, -.8f };
            var models = d.art.models.ToDictionary(m => m.id);
            var instances = d.art.instances.ToList();
            float yaw = d.art.frameYaw * Mathf.Deg2Rad;
            Vector3 Point(float u, float v) => new Vector3(u*Mathf.Cos(yaw)+v*Mathf.Sin(yaw), 0, -u*Mathf.Sin(yaw)+v*Mathf.Cos(yaw));
            void Clear(float u, float v, float radius)
            {
                var p = Point(u,v);
                instances.RemoveAll(i => new Vector2(i.x-p.x,i.z-p.z).magnitude < radius);
                d.art.cover = d.art.cover.Where(i => new Vector2(i.x-p.x,i.z-p.z).magnitude >= radius).ToArray();
            }
            void Add(string id, float u, float v, float scale = 1, float rotation = 0, float? height = null)
            {
                var entry = LevelAuthoringPalette.Get(id); if (entry.model != null) models[id] = entry.model;
                var p = Point(u,v); float y = height ?? d.art.GroundHeight(p.x,p.z);
                instances.Add(new CampArtDefinition.Instance { id = "demo/"+kind+"/"+instances.Count, modelId = entry.model == null ? null : id,
                    assetId = entry.model == null ? id : null, x = p.x, y = y, z = p.z, scale = scale,
                    yaw = d.art.frameYaw + rotation, surfaceAttached = !height.HasValue, surfaceOffset = y-d.art.GroundHeight(p.x,p.z) });
            }
            if (kind == "aircraft")
            {
                Clear(4,9,5.5f); Add("qc_aircraft_ruin",4,9,.8f,95);
                Add("qc_bent_sheet",6.7f,6,.9f,25); Add("qc_broken_beam",3,9,1,70);
                Add("qc_backpack",1.5f,5.3f); Add("qc_lantern",1.2f,5.1f);
                Add("qc_picnic_table",5.5f,3.8f,1,-10); Add("qc_mug",5.5f,3.8f,1,0,.83f);
            }
            else if (kind == "windmill")
            {
                Clear(1,8,3.6f); Add("qc_windmill_ruin",1,8,1,180);
                Add("qc_millstone",2.5f,6.8f); Add("qc_grain_sack",1.7f,5.7f);
                Add("qc_grain_sack",2.2f,5.8f,.82f,40); Add("qc_broken_beam",.5f,5.4f,1,30);
                Add("qc_haystack",4,6,1.2f); Add("qc_wheelbarrow",3.4f,5.2f,1,35);
            }
            else
            {
                Clear(-9.3f,2.2f,3.5f); Add("qc_watermill_ruin",-9.3f,2.2f,1,0,-.01f);
                Add("qc_rowboat",-7.2f,-1.8f,1,15,-.11f); Add("qc_oar",-8.5f,-1.3f,1,35);
                Add("qc_dock_section",-8.1f,-1.5f,1,0,-.4f);
                Add("qc_fishing_basket",-8.1f,-1.5f,1,0,.23f);
                Add("qc_rope_coil",-8.3f,-1.7f,1,0,.23f); Add("qc_fish_trap",-9.7f,.3f);
            }
            // Props near the foreground explain the camping scale in every example.
            Add("qc_bench",3,-5.5f,1,10); Add("qc_cooking_pot",2.6f,-5.1f);
            d.art.instances = instances.ToArray(); d.art.models = models.Values.ToArray();
            return d;
        }
    }
}
