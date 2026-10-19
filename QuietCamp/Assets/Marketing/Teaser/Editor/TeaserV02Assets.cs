using System;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Splines;
using UnityEngine.Timeline;

namespace QuietCamp.Marketing
{
    /// <summary>Separate authored revision. Never overwrites V01 assets or campaign content.</summary>
    public static class TeaserV02Assets
    {
        public const string Root = TeaserAssets.Root + "/V02";
        public const string ConfigPath = Root + "/Settings/Teaser_v02.asset";
        public const string ScenePath = Root + "/Scenes/Teaser_v02.unity";

        [MenuItem("Tools/Marketing/Teaser/V02/Create or Open")]
        static void Open()
        {
            TeaserPipeline.SelectVersion("v02"); Create();
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(ScenePath);
        }
        [MenuItem("Tools/Marketing/Teaser/V02/Record")]
        static void Record(){TeaserPipeline.SelectVersion("v02");TeaserPipeline.Start(true);}
        [MenuItem("Tools/Marketing/Teaser/V02/Preview")]
        static void Preview(){TeaserPipeline.SelectVersion("v02");TeaserPipeline.Start(false);}
        [MenuItem("Tools/Marketing/Teaser/V02/Bake Edited Splines")]
        static void BakeSplines()
        {
            TeaserPipeline.SelectVersion("v02");var config=TeaserAssets.Load();
            if(TeaserPipeline.Busy||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop capture first.");
            foreach(var shot in config.shots)
            {
                var path=config.cameraRig.transform.Find(shot.id+" path").GetComponent<SplineContainer>();
                Bake(shot,path.Spline);EditorUtility.SetDirty(shot.motion);
            }
            AssetDatabase.SaveAssets();
        }

        public static TeaserConfig Create()
        {
            var existing=AssetDatabase.LoadAssetAtPath<TeaserConfig>(ConfigPath);if(existing!=null)return existing;
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Create outside Play Mode.");
            foreach(var name in new[]{"Settings","Scenes","Cameras","Timelines","Animations","Profiles","Documentation"})Directory.CreateDirectory(Root+"/"+name);
            AssetDatabase.Refresh();
            var config=ScriptableObject.CreateInstance<TeaserConfig>();config.revision="v02";
            config.exportFolder="Marketing/Exports/v02";config.blackAt=41.5f;config.fadeAt=40.8f;config.fadeSeconds=.7f;
            config.musicGain=.25f;config.music=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/QuietCamp/Audio/Generated/Cozy/clearing_music.wav");
            config.shots=new[]{
                S("01-fracture","gen:qc_camp:57",90,new Vector3(8,.9f,0),33,170,3.2f,"Slide",.45f,"Torn airframe: an unanswered trace"),
                S("02-human-scale","gen:qc_camp:57",90,new Vector3(5,.7f,0),54,155,6.3f,"Reveal",1.2f,"Wreck and belongings belong to the same inhabited landscape"),
                S("03-a-place","QC003",90,new Vector3(-2,.5f,1),42,225,8.2f,"Locked",0,"A sheltered clearing, water and the well"),
                S("04-place","QC001",90,new Vector3(-1.5f,.5f,-1.5f),61,225,3.9f,"Push",0,"One actual tent placement",0,0,new TeaserCue{frame=24,command="place-alternate-0"}),
                S("05-turn","QC001",90,new Vector3(-1.5f,.65f,-1.5f),32,215,2.2f,"Locked",0,"Actual rotation and its settling animation",1,0,new TeaserCue{frame=25,command="place-0"}),
                S("06-settle","QC001",90,new Vector3(-1.5f,.5f,0),54,230,4.2f,"Slide",.4f,"Second tent and canonical completion",1,-1,new TeaserCue{frame=10,command="place-1"},new TeaserCue{frame=43,command="complete-camp"}),
                S("07-the-route","gen:qc_camp:29",90,new Vector3(-10,1,1.5f),36,220,4.3f,"Slide",1.4f,"Timber crossing: the journey continues"),
                S("08-water","QC003",90,new Vector3(-13.5f,.1f,-1),68,225,2.6f,"Locked",0,"The actual moving water surface and planted banks"),
                S("09-weather-turns","QC009",90,new Vector3(0,.5f,0),61,225,5.3f,"Locked",0,"Rain changes the mood, not the rules",2),
                S("10-rain-on-cloth","QC009",90,new Vector3(-2,.6f,-2),30,215,2.5f,"Slide",.4f,"Rain contacts cloth and ground; wind moves the fabric",2),
                S("11-make-room","QC009",120,new Vector3(0,.5f,0),48,235,5.7f,"Slide",1.8f,"Two more real placements, then shelter for everyone",2,-1,new TeaserCue{frame=15,command="place-2"},new TeaserCue{frame=48,command="place-3"},new TeaserCue{frame=79,command="complete-camp"}),
                S("12-beyond-the-garden","gen:qc_camp:122",120,new Vector3(10.5f,1,5.3f),38,225,5,"Reveal",1,"Reveal the existing Haven shelter",-2),
                S("13-haven","gen:qc_camp:122",120,new Vector3(4,.8f,2),52,215,8.3f,"Reveal",1,"A cared-for camp is a destination; the route still continues",-2,-1,new TeaserCue{frame=15,command="complete-camp"})
            };
            config.shots[1].endTarget=new Vector3(3,.7f,0);config.shots[1].endOrtho=6.8f;
            config.shots[3].endOrtho=3.6f;
            config.shots[11].endTarget=new Vector3(4,.8f,2);config.shots[11].endOrtho=8.3f;config.shots[11].endYaw=215;
            config.shots[12].endTarget=new Vector3(3,.6f,1.5f);config.shots[12].endOrtho=9.6f;config.shots[12].endYaw=205;
            var timeline=ScriptableObject.CreateInstance<TimelineAsset>();timeline.editorSettings.frameRate=30;timeline.durationMode=TimelineAsset.DurationMode.FixedLength;timeline.fixedDuration=42;
            AssetDatabase.CreateAsset(timeline,Root+"/Timelines/Master42_v02.playable");config.timeline=timeline;
            var rig=new GameObject("Teaser V02 — curiosity, action, weather, destination");
            var director=rig.AddComponent<PlayableDirector>();director.playOnAwake=false;director.playableAsset=timeline;director.timeUpdateMode=DirectorUpdateMode.Manual;
            var cuts=timeline.CreateTrack<CinemachineTrack>(null,"13 editorial shots");
            var signals=timeline.CreateTrack<SignalTrack>(null,"Canonical gameplay and shot lighting cues");
            double start=0;
            foreach(var shot in config.shots)
            {
                var cam=new GameObject(shot.id).AddComponent<CinemachineCamera>();cam.transform.SetParent(rig.transform,false);
                cam.Lens=LensSettings.Default;cam.Lens.ModeOverride=LensSettings.OverrideModes.Orthographic;cam.Lens.OrthographicSize=shot.ortho;
                var animator=cam.gameObject.AddComponent<Animator>();
                var path=new GameObject(shot.id+" path").AddComponent<SplineContainer>();path.transform.SetParent(rig.transform,false);path.Spline=Path(shot);
                var animation=new AnimationClip{name=shot.id+" framing",frameRate=30};AssetDatabase.CreateAsset(animation,Root+"/Animations/"+shot.id+".anim");shot.motion=animation;Bake(shot,path.Spline);
                cam.transform.position=shot.start;cam.transform.rotation=Quaternion.Euler(shot.pitch,shot.yaw,0);
                var clip=cuts.CreateClip<CinemachineShot>();clip.displayName=shot.id;clip.start=start;clip.duration=shot.frames/30.0;
                ((CinemachineShot)clip.asset).VirtualCamera.exposedName=shot.id;director.SetReferenceValue(shot.id,cam);
                var motion=timeline.CreateTrack<AnimationTrack>(null,shot.id+" motion");motion.trackOffset=TrackOffset.ApplyTransformOffsets;
                var move=motion.CreateClip(animation);move.start=start;move.duration=clip.duration;((AnimationPlayableAsset)move.asset).removeStartOffset=false;director.SetGenericBinding(motion,animator);
                shot.look=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(shot.look,Root+"/Profiles/"+shot.id+".asset");
                var vignette=shot.look.Add<Vignette>(true);vignette.intensity.Override(.055f);AssetDatabase.AddObjectToAsset(vignette,shot.look);
                var grade=shot.look.Add<ColorAdjustments>(true);grade.postExposure.Override(0);AssetDatabase.AddObjectToAsset(grade,shot.look);
                shot.sunMultiplier=shot.id.Contains("rain")||shot.id.Contains("weather")?1.08f:1;
                shot.musicMultiplier=shot.id.Contains("rain")?.35f:shot.id.Contains("water")?.5f:1;
                foreach(var cue in shot.cues)AddSignal(timeline,signals,start+cue.frame/30.0,cue.command);
                AddSignal(timeline,signals,start,"look:"+shot.id);
                start+=clip.duration;
            }
            config.profile=config.shots[0].look;
            var source=rig.AddComponent<AudioSource>();source.playOnAwake=false;
            var score=timeline.CreateTrack<AudioTrack>(null,"Continuous original score — recorded in engine");var music=score.CreateClip<AudioPlayableAsset>();music.start=0;music.duration=42;
            ((AudioPlayableAsset)music.asset).clip=config.music;((AudioPlayableAsset)music.asset).loop=true;director.SetGenericBinding(score,source);
            var weather=timeline.CreateTrack<ActivationTrack>(null,"Native weather until final darkness");var active=weather.CreateDefaultClip();active.duration=config.blackAt;weather.postPlaybackState=ActivationTrack.PostPlaybackState.Revert;
            config.cameraRig=PrefabUtility.SaveAsPrefabAsset(rig,Root+"/Cameras/TeaserRig_v02.prefab");AssetDatabase.CreateAsset(config,ConfigPath);
            var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rig,scene);
            var output=new GameObject("Preview output — WorldOps binds real Camp camera").AddComponent<Camera>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(output.gameObject,scene);
            director.SetGenericBinding(cuts,output.gameObject.AddComponent<CinemachineBrain>());
            EditorSceneManager.SaveScene(scene,ScenePath);EditorSceneManager.CloseScene(scene,true);UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
            EditorUtility.SetDirty(timeline);EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();return config;
        }

        static void AddSignal(TimelineAsset timeline,SignalTrack track,double time,string name)
        {var signal=ScriptableObject.CreateInstance<SignalAsset>();signal.name=name;AssetDatabase.AddObjectToAsset(signal,timeline);track.CreateMarker<SignalEmitter>(time).asset=signal;}

        static TeaserShot S(string id,string level,int frames,Vector3 target,float pitch,float yaw,float ortho,string movement,float travel,string purpose,int initial=0,int alternate=-1,params TeaserCue[] cues)
            =>new TeaserShot{id=id,levelId=level,frames=frames,target=target,endTarget=target,pitch=pitch,yaw=yaw,ortho=ortho,endOrtho=ortho,endYaw=yaw,movement=movement,travel=travel,purpose=purpose,initialTentCount=initial,alternateGuest=alternate,cues=cues,gameplay=cues.Length>0};

        static Spline Path(TeaserShot shot)
        {
            var q=Quaternion.Euler(shot.pitch,shot.yaw,0);var endQ=Quaternion.Euler(shot.pitch,shot.endYaw,0);
            var start=shot.target-q*Vector3.forward*28;var end=shot.endTarget-endQ*Vector3.forward*28;
            if(shot.movement=="Slide"){start-=q*Vector3.right*shot.travel*.5f;end+=q*Vector3.right*shot.travel*.5f;}
            var mid=Vector3.Lerp(start,end,.5f);
            if(shot.movement=="Reveal")mid+=q*Vector3.right*shot.travel*.3f;
            return new Spline(new[]{(float3)start,(float3)mid,(float3)end},TangentMode.AutoSmooth);
        }

        public static void Reframe(TeaserConfig config,int index)
        {
            var shot=config.shots[index];
            var rig=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(config.cameraRig));
            try
            {
                var path=rig.transform.Find(shot.id+" path").GetComponent<SplineContainer>();path.Spline=Path(shot);Bake(shot,path.Spline);
                PrefabUtility.SaveAsPrefabAsset(rig,AssetDatabase.GetAssetPath(config.cameraRig));
            }
            finally{PrefabUtility.UnloadPrefabContents(rig);}
            EditorUtility.SetDirty(shot.motion);EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
        }

        static void Bake(TeaserShot shot,Spline path)
        {
            string[] axes={"x","y","z","w"};var pos=new AnimationCurve[3];var rot=new AnimationCurve[4];for(int i=0;i<3;i++)pos[i]=new AnimationCurve();for(int i=0;i<4;i++)rot[i]=new AnimationCurve();
            var lens=new AnimationCurve();
            for(int k=0;k<=16;k++)
            {
                float t=k/16f,e=shot.movement=="Locked"?0:t*t*(3-2*t),seconds=t*shot.frames/30f;
                Vector3 p=path.EvaluatePosition(e);var q=Quaternion.Euler(shot.pitch,Mathf.LerpAngle(shot.yaw,shot.endYaw,e),0);
                for(int a=0;a<3;a++)pos[a].AddKey(seconds,p[a]);for(int a=0;a<4;a++)rot[a].AddKey(seconds,q[a]);
                lens.AddKey(seconds,Mathf.Lerp(shot.ortho,shot.endOrtho,e));
            }
            foreach(var curve in pos.Concat(rot).Append(lens))for(int k=0;k<curve.length;k++){AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.ClampedAuto);}
            for(int a=0;a<3;a++)AnimationUtility.SetEditorCurve(shot.motion,EditorCurveBinding.FloatCurve("",typeof(Transform),"m_LocalPosition."+axes[a]),pos[a]);
            for(int a=0;a<4;a++)AnimationUtility.SetEditorCurve(shot.motion,EditorCurveBinding.FloatCurve("",typeof(Transform),"m_LocalRotation."+axes[a]),rot[a]);
            AnimationUtility.SetEditorCurve(shot.motion,EditorCurveBinding.FloatCurve("",typeof(CinemachineCamera),"Lens.OrthographicSize"),lens);
            shot.start=path.EvaluatePosition(0);shot.end=path.EvaluatePosition(1);
        }
    }
}
