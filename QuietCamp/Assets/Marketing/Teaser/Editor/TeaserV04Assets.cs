using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QuietCamp.Marketing
{
    public static class TeaserV04Assets
    {
        public const string Root="Assets/Marketing/TeaserV04";
        public static string ConfigPath(string version)=>Root+"/"+version+"/Settings/"+version+".asset";
        [MenuItem("Tools/Marketing/Teaser/V04/Open")]
        static void Open()
        {
            TeaserPipeline.SelectVersion("v04");Create("v04");
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(Root+"/v04/Scenes/v04.unity");
        }
        public static TeaserConfig Create(string version)
        {
            var existing=AssetDatabase.LoadAssetAtPath<TeaserConfig>(ConfigPath(version));if(existing!=null)return existing;
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            string root=Root+"/"+version;
            foreach(var name in new[]{"Settings","Scenes","Cameras","Timelines","Animations","Profiles"})Directory.CreateDirectory(root+"/"+name);
            AssetDatabase.Refresh();var config=ScriptableObject.CreateInstance<TeaserConfig>();config.revision=version;
            config.captureQuality=3;config.musicGain=.32f;config.exportFolder="Marketing/Exports/v04/"+version;
            config.music=AssetDatabase.LoadAssetAtPath<AudioClip>(TeaserV03Assets.Root+"/Audio/quietcamp_v03_score.wav");
            if(config.music==null)throw new FileNotFoundException("Owned score missing.");
            bool pilot=version=="v04-pilot";config.totalFrames=pilot?180:480;
            config.videoName=pilot?"QuietCamp_V04_SceneStudy":"QuietCamp_V04_Review";
            config.blackAt=pilot?6:15.8f;config.fadeAt=pilot?6:15.6f;config.fadeSeconds=.2f;
            config.shots=pilot?new[]{S("clearing-study","QC003",180,new Vector3(-.9f,.6f,.1f),44,225,4.2f,0,-1,C(8,"place-0"),C(65,"preview-1"),C(82,"place-1"),C(83,"preview-off"),C(116,"preview-2"),C(136,"place-2"),C(137,"preview-off"))}:new[]{
                S("01-turn-and-welcome","QC003",96,new Vector3(-1,.6f,.1f),44,225,3.6f,1,0,C(8,"place-0"),C(32,"preview-1"),C(48,"place-1"),C(49,"preview-off")),
                S("02-complete-the-clearing","QC003",72,new Vector3(-.8f,.6f,-.5f),55,225,3.7f,2,-1,C(0,"preview-2"),C(20,"place-2"),C(21,"preview-off")),
                S("03-a-quiet-rest","QC003",72,new Vector3(-.6f,.7f,0),37,225,4.1f,3,-1,C(0,"route-2"),C(24,"route-off"),C(26,"complete-camp")),
                S("04-shelter-in-the-rain","QC009",96,new Vector3(-1,.7f,0),45,225,4.6f,0,-1,C(0,"preview-0"),C(24,"place-0"),C(25,"preview-off"),C(56,"preview-1"),C(80,"place-1"),C(81,"preview-off")),
                S("05-another-dry-place","QC009",60,new Vector3(0,.6f,.5f),42,225,3.8f,2,-1,C(0,"preview-2"),C(18,"place-2"),C(19,"preview-off")),
                S("06-rest-in-the-rain","QC009",84,new Vector3(0,.7f,0),42,225,4.6f,3,-1,C(8,"place-3"),C(38,"complete-camp"))};
            return TeaserV02Assets.Build(config,root,ConfigPath(version),root+"/Scenes/"+version+".unity");
        }
        static TeaserCue C(int frame,string command)=>new TeaserCue{frame=frame,command=command};
        static TeaserShot S(string id,string level,int frames,Vector3 target,float pitch,float yaw,float ortho,int initial,int alternate,params TeaserCue[] cues)
            =>new TeaserShot{id=id,levelId=level,purpose=id,frames=frames,target=target,endTarget=target,pitch=pitch,yaw=yaw,endYaw=yaw,ortho=ortho,endOrtho=ortho,movement="Locked",initialTentCount=initial,alternateGuest=alternate,cues=cues,gameplay=true};
    }
}
