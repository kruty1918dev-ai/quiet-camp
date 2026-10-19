#if UNITY_EDITOR
using System;
using System.IO;
using Newtonsoft.Json;
using QuietCamp.Application;
using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    // Only explicit authoring requests activate this boundary. Never included in a player.
    public static class EditorAuthoringPreview
    {
        [Serializable] public sealed class Request { public LevelData level; public CampArtDefinition art; public bool campaign,controlledCapture,learning; public string campaignArtDirectory; }
        public static bool CaptureClockRunning {get;set;}
        public static bool ControlledCapture=>Active&&Current.controlledCapture;
        public static float CaptureDelta=>CaptureClockRunning?1f/30:0;
        public static void SwitchCampaignCapture(LevelData level,CampArtDefinition art,string directory,Action<string,string> atomicWrite=null)
        {
            if(!CampaignPreview||!ControlledCapture||!Path.IsPathRooted(directory)||!Directory.Exists(directory))throw new InvalidOperationException("Only the controlled memory preview can switch capture inputs.");
            var next=new Request{level=level,art=art,campaign=true,controlledCapture=true,campaignArtDirectory=directory};
            string json=JsonConvert.SerializeObject(next);
            if(atomicWrite!=null)atomicWrite(RequestPath,json);
            else{string pending=RequestPath+".pending";File.WriteAllText(pending,json);File.Replace(pending,RequestPath,null);}
            _request=next;CaptureClockRunning=false;
        }
        public static string RequestPath => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Temp/ai/level-authoring-preview.json"));
        static Request _request;
        const string EnabledKey = "QuietCamp.Authoring.PreviewEnabled";
        public static Request Current => _request ?? (_request = File.Exists(RequestPath) ? JsonConvert.DeserializeObject<Request>(File.ReadAllText(RequestPath)) : null);
        public static bool Active => UnityEditor.SessionState.GetBool(EnabledKey, false) && Current?.level != null && Current.art != null;
        public static void Enable(bool enabled) => UnityEditor.SessionState.SetBool(EnabledKey, enabled);
        public static bool CampaignPreview => Active && Current.campaign;
        public static bool Matches(string id) => Active && !Current.campaign && Current.level.id == id;
        public static bool MatchesArt(string id) => Matches(id) || Active && Current.campaign
            && !string.IsNullOrEmpty(Current.campaignArtDirectory)
            && File.Exists(Path.Combine(Current.campaignArtDirectory, id.Replace(':', '_') + ".json"));
        public static LevelData Level(string id) => Matches(id) ? JsonConvert.DeserializeObject<LevelData>(JsonConvert.SerializeObject(Current.level)) : null;
        public static CampArtDefinition Art(string id)
        {
            if (Matches(id)) return JsonConvert.DeserializeObject<CampArtDefinition>(JsonConvert.SerializeObject(Current.art));
            if (!MatchesArt(id)) return null;
            return JsonConvert.DeserializeObject<CampArtDefinition>(File.ReadAllText(Path.Combine(Current.campaignArtDirectory, id.Replace(':', '_') + ".json")));
        }
        public static void Reset(){_request=null;CaptureClockRunning=false;}
        public static SaveAdapter CreateSave()
        {
            var save = new SaveAdapter(memoryOnly: true);
            save.Progress.tutorial = Current?.learning==true ? new TutorialSaveData { initialized=true, curriculumVersion=2 }
                : new TutorialSaveData { initialized = true, existingPlayer = true, controlsUnlocked = true, introductionSeen = true, menuIntroSeen = true };
            return save;
        }
    }
}
#endif
