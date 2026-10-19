using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QuietCamp.Marketing
{
    /// <summary>Fresh direction: one readable puzzle, not a tour assembled from V02 shots.</summary>
    public static class TeaserV03Assets
    {
        public const string Root="Assets/Marketing/TeaserV03";
        public static string ConfigPath(string version)=>Root+"/"+version+"/Settings/"+version+".asset";
        [MenuItem("Tools/Marketing/Teaser/V03/Open Review Timeline")]
        static void Open()
        {
            TeaserPipeline.SelectVersion("v03");Create("v03");
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(Root+"/v03/Scenes/v03.unity");
        }
        [MenuItem("Tools/Marketing/Teaser/V03/Record Review")]
        static void Record(){TeaserPipeline.SelectVersion("v03");TeaserPipeline.Start(true);}
        [MenuItem("Tools/Marketing/Teaser/V03/Preview Review")]
        static void Preview(){TeaserPipeline.SelectVersion("v03");TeaserPipeline.Start(false);}
        public static TeaserConfig Create(string version)
        {
            var existing=AssetDatabase.LoadAssetAtPath<TeaserConfig>(ConfigPath(version));if(existing!=null)return existing;
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Create outside Play Mode.");
            string root=Root+"/"+version;
            foreach(var folder in new[]{"Settings","Scenes","Cameras","Timelines","Animations","Profiles"})Directory.CreateDirectory(root+"/"+folder);
            AssetDatabase.Refresh();var config=ScriptableObject.CreateInstance<TeaserConfig>();config.revision=version;
            config.exportFolder="Marketing/Exports/v03/"+version;config.musicGain=.20f;
            config.music=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/quietcamp_v03_score.wav");
            if(config.music==null)throw new FileNotFoundException("Generate and import the owned V03 score first.");
            config.totalFrames=version.Contains("-a")||version.Contains("-b")||version.Contains("-c")?180:480;
            config.blackAt=config.totalFrames/30f;config.fadeAt=config.blackAt-.14f;config.fadeSeconds=.14f;
            config.videoName=version=="v03-a"?"QuietCamp_Hook_A":version=="v03-b"?"QuietCamp_Hook_B":version=="v03-c"?"QuietCamp_Hook_C":version=="v03-rough"?"QuietCamp_V03_RoughCut":"QuietCamp_V03_Review";
            if(version=="v03-a")config.shots=new[]{
                S("A-one-small-turn","QC003",180,new Vector3(-1,.55f,0),52,225,4.5f,1,0,
                    C(8,"place-0"),C(30,"route-0"),C(52,"preview-1"),C(74,"place-1"),C(75,"preview-off"),C(108,"route-off"),C(120,"preview-2"),C(146,"place-2"),C(147,"preview-off"))};
            else if(version=="v03-b")config.shots=new[]{
                S("B-living-among-traces","gen:qc_camp:57",180,new Vector3(3,.7f,0),48,165,6.8f,0,-1,
                    C(8,"place-0"),C(54,"place-1"),C(100,"place-2"))};
            else if(version=="v03-c")config.shots=new[]{
                S("C-a-dry-place","QC009",180,new Vector3(0,.6f,0),38,225,4.8f,3,-1,
                    C(8,"place-3"),C(68,"complete-camp"))};
            else config.shots=new[]{
                S("01-turn-makes-room","QC003",75,new Vector3(-2,.6f,-1.5f),38,215,2.8f,1,0,C(8,"place-0"),C(25,"route-0")),
                S("02-welcome-another","QC003",75,new Vector3(-1,.6f,0),54,225,4.6f,1,-1,C(0,"preview-1"),C(20,"place-1"),C(21,"preview-off")),
                S("03-a-place-in-shade","QC003",60,new Vector3(-2,.65f,-1.5f),35,215,3.1f,2,-1,C(0,"shade-on"),C(52,"shade-off")),
                S("04-fit-the-last-tent","QC003",90,new Vector3(0,.6f,0),59,225,4.6f,2,-1,C(0,"preview-2"),C(24,"place-2"),C(25,"preview-off")),
                S("05-leave-a-way-through","QC003",60,new Vector3(0,.3f,0),55,225,4.5f,3,-1,C(0,"route-2"),C(53,"route-off")),
                S("06-everybody-can-rest","QC003",120,new Vector3(0,.65f,0),36,215,4.2f,3,-1,C(24,"complete-camp"))
            };
            if(!version.Contains("-a")&&!version.Contains("-b")&&!version.Contains("-c"))
            {config.blackAt=15.8f;config.fadeAt=15.6f;config.fadeSeconds=.2f;}
            // Fine cut responds to the actual hook/rough review; rough remains intact.
            if(version=="v03")
            {
                config.musicGain=.38f;
                config.shots[0].ortho=config.shots[0].endOrtho=2.35f;
                config.shots[1].ortho=config.shots[1].endOrtho=3.6f;
                config.shots[2].ortho=config.shots[2].endOrtho=2.7f;
                config.shots[3].ortho=config.shots[3].endOrtho=3.6f;
                config.shots[4].ortho=config.shots[4].endOrtho=3.4f;
                config.shots[5].ortho=config.shots[5].endOrtho=3.6f;
                config.shots[0].yaw=config.shots[0].endYaw=225;
                config.shots[2].yaw=config.shots[2].endYaw=225;
                config.shots[5].yaw=config.shots[5].endYaw=225;
                config.shots[3].cues=new[]{C(0,"preview-2"),C(24,"place-2"),C(25,"preview-off"),C(48,"route-2")};
                config.shots[4].cues=new[]{C(0,"route-2"),C(58,"route-off")};
                config.shots[5].cues=new[]{C(0,"route-2"),C(20,"route-off"),C(24,"complete-camp")};
            }
            return TeaserV02Assets.Build(config,root,ConfigPath(version),root+"/Scenes/"+version+".unity");
        }
        static TeaserCue C(int frame,string command)=>new TeaserCue{frame=frame,command=command};
        static TeaserShot S(string id,string level,int frames,Vector3 target,float pitch,float yaw,float ortho,int initial,int alternate,params TeaserCue[] cues)
            =>new TeaserShot{id=id,levelId=level,frames=frames,purpose=id,target=target,endTarget=target,pitch=pitch,yaw=yaw,endYaw=yaw,ortho=ortho,endOrtho=ortho,movement="Locked",initialTentCount=initial,alternateGuest=alternate,cues=cues,gameplay=true};
    }
}
