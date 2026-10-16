using System;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Domain;

namespace QuietCamp.Application
{
    /// <summary>Authored teaching order; existing level geometry, IDs and solutions are retained.</summary>
    public sealed class LearningCurriculum
    {
        public sealed class Lesson { public string levelId, textKey; }
        public int version;
        public int quietFrom, friendsFrom, accessFrom;
        public Lesson[] lessons;
        public static LearningCurriculum Read(string json)
        {
            var value=JsonConvert.DeserializeObject<LearningCurriculum>(json);
            if(value?.version!=2||value.lessons?.Length!=20||value.lessons.Any(l=>l==null||string.IsNullOrEmpty(l.levelId)||string.IsNullOrEmpty(l.textKey))
                ||value.lessons.Select(l=>l.levelId).Distinct().Count()!=20||value.quietFrom<4||value.friendsFrom<=value.quietFrom||value.accessFrom<=value.friendsFrom||value.accessFrom>20)
                throw new ArgumentException("Invalid twenty-camp curriculum.");
            return value;
        }
        /// <summary>Editor-only publication calls this on a copy, then validates and archives through the campaign writer.</summary>
        public void Pace(LevelData level,string sourceLevelId=null)
        {
            int number=Array.FindIndex(lessons,l=>l.levelId==(sourceLevelId??level.id))+1;
            if(number==0)return;
            if(number<quietFrom)foreach(var guest in level.guests)guest.quiet=false;
            if(number<friendsFrom)level.friends=Array.Empty<string[]>();
            if(number<accessFrom)level.accessPoints=Array.Empty<AccessPointData>();
        }
    }
}
