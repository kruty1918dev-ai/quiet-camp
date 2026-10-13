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
            d.art.orthographicSize = 9.15f;
            d.art.sunEuler = new[] { 54f,258f,0f }; d.art.sunIntensity = 1.2f; d.art.blurPixels = 1.35f;
            var models = d.art.models.ToDictionary(m => m.id);
            var instances = d.art.instances.ToList();
            float yaw = d.art.frameYaw * Mathf.Deg2Rad;
            Vector3 Point(float u, float v) => new Vector3(u*Mathf.Cos(yaw)+v*Mathf.Sin(yaw), 0, -u*Mathf.Sin(yaw)+v*Mathf.Cos(yaw));
            var target = Point(-.8f,1.8f); d.art.cameraTarget = new[] { target.x,0,target.z };
            void Clear(float u, float v, float radius)
            {
                var p = Point(u,v);
                instances.RemoveAll(i => !(i.modelId ?? "").StartsWith("qc_",StringComparison.Ordinal) && new Vector2(i.x-p.x,i.z-p.z).magnitude < radius);
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
            void Green(float u, float v, float radius, int seed)
            {
                var random=new System.Random(seed); var plants=d.art.cover.ToList();
                for(int i=0;i<48;i++)
                {
                    float angle=(float)random.NextDouble()*Mathf.PI*2, distance=Mathf.Sqrt((float)random.NextDouble())*radius;
                    distance*=.7f+.3f*Mathf.Sin(angle*3+seed);
                    var p=Point(u+Mathf.Cos(angle)*distance*1.25f,v+Mathf.Sin(angle)*distance*.85f);
                    if(Mathf.Abs(p.x)<d.level.width*.5f+.3f&&Mathf.Abs(p.z)<d.level.height*.5f+.3f)continue;
                    float ground=d.art.GroundHeight(p.x,p.z);if(ground<-.11f)continue;
                    int plantKind=i%37==0?2:i%7==0?0:1;
                    plants.Add(new CampArtDefinition.CoverPoint { x=p.x,y=ground,z=p.z,kind=plantKind,
                        height=plantKind==2?.09f:.06f+(float)random.NextDouble()*.13f,maximumSpan=.24f,yaw=(float)random.NextDouble()*360,variation=(float)random.NextDouble() });
                }
                d.art.cover=plants.ToArray();
            }
            if (kind == "aircraft")
            {
                Clear(4,9,6.3f); Add("qc_aircraft_ruin",4,9,.8f,95);
                Add("qc_bent_sheet",4.8f,7,.9f,25); Add("qc_broken_beam",3,9,1,70);
                Add("qc_backpack",1.5f,5.3f); Add("qc_lantern",1.2f,5.1f);
                Clear(5.5f,3.8f,1.5f); Add("qc_picnic_table",5.5f,3.8f,1,-10); Add("qc_mug",5.5f,3.8f,1,0,.83f);
                Green(.7f,8,1.1f,103); Green(6.8f,10.8f,1.3f,104); Green(6.1f,6.1f,1.2f,105);
                Green(1.4f,11.8f,1.3f,106); Green(4,12.4f,1.1f,107);
                Add("ua_young_willow",-.8f,9.9f,.85f,-30); Green(2.4f,7.2f,.75f,108);
            }
            else if (kind == "windmill")
            {
                Clear(1,8,3.6f); Add("qc_windmill_ruin",1,8,1,155);
                Add("qc_millstone",2.5f,6.8f); Add("qc_grain_sack",1.7f,5.7f);
                Add("qc_grain_sack",1.35f,5.9f,.82f,40); Add("qc_broken_beam",.5f,5.4f,1,60);
                Add("qc_haystack",4,6,1.2f); Add("qc_wheelbarrow",3.7f,5.4f,1,20);
                Green(-.9f,8.9f,.95f,201); Green(2.8f,8.3f,.8f,202); Green(.2f,6.5f,.7f,203);
                Add("ua_picket_fence_damaged",-2,7,.7f,15); Add("qc_barrel",3.4f,7.5f,.8f);
            }
            else
            {
                Clear(-10,2.2f,3.5f); Add("qc_watermill_ruin",-10,2.2f,1,35);
                float riverU=d.art.streamCentre+Kruty1918.ScenePlacement.Composition.RiverGeometry.Centre(-4.5f,d.art.streamSlope,d.art.streamBend);
                Clear(riverU,-4.5f,1.65f); Clear(riverU-1.6f,-4.5f,1.4f);
                Add("qc_rowboat",riverU,-4.5f,1,-10,-.11f); Add("qc_oar",riverU-2.4f,-4.2f,1,35);
                Add("qc_dock_section",riverU-1.6f,-4.5f,1,0,-.4f);
                Add("qc_fishing_basket",riverU-1.45f,-4.2f,1,0,.225f);
                Add("qc_rope_coil",riverU-1.95f,-4.65f,1,0,.225f); Add("qc_fish_trap",-10.1f,.3f);
                Add("qc_bucket",riverU-1.4f,-4.85f,.8f,0,.225f); Green(riverU-2.4f,-3.8f,.75f,304);
                Add("ua_mooring_post",riverU-1.15f,-4.95f,.45f,0,.225f);
                Green(-10.7f,3.3f,1,301); Green(-10.7f,1.2f,1,302); Green(-9.8f,-.2f,.7f,303);
            }
            // Props near the foreground explain the camping scale in every example.
            Clear(3,-4.2f,2.6f); Add("qc_bench",3,-4.2f,1,10); Add("qc_cooking_pot",2.1f,-4.2f);
            Green(4,-5.5f,.7f,401); Green(2.2f,-5.6f,.6f,402);
            d.art.instances = instances.ToArray(); d.art.models = models.Values.ToArray();
            return d;
        }
    }
}
