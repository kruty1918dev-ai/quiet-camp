using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using QuietCamp.Domain;
using QuietCamp.Presentation.UI;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>Shared authored footprints protect architecture from procedural vegetation.</summary>
    public static class CampaignScenicFootprints
    {
        static readonly ConditionalWeakTable<LevelData,List<Bounds>> Cache=new ConditionalWeakTable<LevelData,List<Bounds>>();
        public static bool Intersects(LevelData level,Vector3 at,float radius)
        {
            if(level?.environmentalStory==null)return false;
            foreach(var bounds in Cache.GetValue(level,Build))
                if(at.x+radius>bounds.min.x&&at.x-radius<bounds.max.x&&at.z+radius>bounds.min.z&&at.z-radius<bounds.max.z)return true;
            return false;
        }
        static List<Bounds> Build(LevelData level)
        {
            var result=new List<Bounds>();var library=RoadmapModelLibrary.Load();
            foreach(var beat in level.environmentalStory.beats??Array.Empty<EnvironmentalStoryBeat>())
            foreach(var prop in beat.props??Array.Empty<EnvironmentalStoryProp>())
            {
                var view=prop?.level;if(view==null)continue;
                var model=library.Get(view.assetId);if(model==null)continue;
                var origin=new Vector3(view.x,view.elevation,view.z);var rotation=Quaternion.Euler(0,view.yaw,0);
                var bounds=new Bounds(origin+rotation*model.Positions[0]*view.height,Vector3.zero);
                foreach(var point in model.Positions)bounds.Encapsulate(origin+rotation*point*view.height);
                bounds.Expand(.24f);result.Add(bounds);
            }
            return result;
        }
    }
}
