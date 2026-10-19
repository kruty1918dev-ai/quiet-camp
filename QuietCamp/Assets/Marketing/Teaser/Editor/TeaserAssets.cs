using System;
using System.IO;
using System.Linq;
using QuietCamp.Editor.LevelAuthoring;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;

namespace QuietCamp.Marketing
{
    public static class TeaserAssets
    {
        public const string Root = "Assets/Marketing/Teaser";
        public const string ConfigPath = Root + "/Settings/TeaserConfig.asset";
        public const string ScenePath = Root + "/Scenes/Teaser.unity";
        public static TeaserConfig Load() => AssetDatabase.LoadAssetAtPath<TeaserConfig>(ConfigPath);
        public static void Reframe(TeaserConfig config,int index)
        {
            if(TeaserPipeline.Busy||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop the capture before changing cameras.");
            var shot=config.shots[index];var q=Quaternion.Euler(shot.pitch,shot.yaw,0);
            var centre=shot.target-q*Vector3.forward*28;
            shot.start=centre-q*Vector3.right*.4f;shot.end=centre+q*Vector3.right*.4f;
            string[] xyz={"x","y","z"},xyzw={"x","y","z","w"};
            for(int i=0;i<3;i++)AnimationUtility.SetEditorCurve(shot.motion,EditorCurveBinding.FloatCurve("",typeof(Transform),"m_LocalPosition."+xyz[i]),AnimationCurve.EaseInOut(0,shot.start[i],shot.frames/30f,shot.end[i]));
            for(int i=0;i<4;i++)AnimationUtility.SetEditorCurve(shot.motion,EditorCurveBinding.FloatCurve("",typeof(Transform),"m_LocalRotation."+xyzw[i]),AnimationCurve.Constant(0,shot.frames/30f,q[i]));
            AnimationUtility.SetEditorCurve(shot.motion,EditorCurveBinding.FloatCurve("",typeof(CinemachineCamera),"Lens.OrthographicSize"),AnimationCurve.EaseInOut(0,shot.ortho,shot.frames/30f,shot.ortho*(index==6?1.25f:1)));
            EditorUtility.SetDirty(shot.motion);EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Marketing/Teaser/Apply Camera Framing")]
        static void ReframeAll(){var config=Load();for(int i=0;i<config.shots.Length;i++)Reframe(config,i);}

        [MenuItem("Tools/Marketing/Teaser/Apply Configuration")]
        static void ApplyConfiguration()
        {
            if(TeaserPipeline.Busy||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop the capture first.");
            var config=Load();
            if(config.shots.Length!=7||config.shots.Sum(s=>s.frames)!=1260||config.shots.Any(s=>s.frames<30)||config.shots.Select(s=>s.id).Distinct().Count()!=7)throw new InvalidOperationException("Seven unique shots must total 1260 frames.");
            var cameras=config.timeline.GetOutputTracks().OfType<CinemachineTrack>().Single();
            var old=cameras.GetClips().ToDictionary(c=>c.displayName,c=>new{c.start,c.duration});
            if(config.shots.Any(s=>!old.ContainsKey(s.id)))throw new InvalidOperationException("Keep stable shot IDs when changing order.");
            var cues=config.timeline.GetOutputTracks().OfType<SignalTrack>().SelectMany(t=>t.GetMarkers().OfType<SignalEmitter>()).Select(s=>new{marker=s,time=s.time,owner=old.First(p=>s.time>=p.Value.start&&s.time<p.Value.start+p.Value.duration).Key}).ToArray();
            double start=0;
            foreach(var shot in config.shots)
            {
                var clip=cameras.GetClips().Single(c=>c.displayName==shot.id);clip.start=start;clip.duration=shot.frames/30.0;
                var motion=config.timeline.GetOutputTracks().OfType<AnimationTrack>().Single(t=>t.name==shot.id+" motion");
                foreach(var animation in motion.GetClips()){animation.start=start;animation.duration=clip.duration;}
                foreach(var cue in cues.Where(c=>c.owner==shot.id)){cue.marker.time=start+(cue.time-old[shot.id].start)*clip.duration/old[shot.id].duration;EditorUtility.SetDirty(cue.marker);}
                EditorUtility.SetDirty(motion);start+=clip.duration;
            }
            foreach(var track in config.timeline.GetOutputTracks().OfType<AudioTrack>())foreach(var clip in track.GetClips())((AudioPlayableAsset)clip.asset).clip=config.music;
            EditorUtility.SetDirty(cameras);EditorUtility.SetDirty(config.timeline);ReframeAll();AssetDatabase.SaveAssets();
        }

        public static TeaserConfig Create()
        {
            if (Load() != null) { EnsureRotations(Load()); return Load(); }
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create assets outside Play Mode.");
            foreach (var folder in new[] { "Scenes", "Timelines", "Cameras", "Animations", "Profiles", "Settings", "Documentation" })
                Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            var config = ScriptableObject.CreateInstance<TeaserConfig>();
            config.music = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/QuietCamp/Audio/Generated/Cozy/clearing_music.wav");
            config.shots = new[] {
                Shot("01-hook", "gen:qc_camp:57", 150, new Vector3(13,1,1),47,175,3.4f,"Overgrown airframe detail; no invented crash event"),
                Shot("02-world", "QC003", 180, new Vector3(-2,.6f,1),47,225,8.0f,"Water and a place to rest"),
                Shot("03-play", "QC001", 180, new Vector3(0,.5f,0),52,225,5.5f,"Real legal tent placement; preparation, action, consequence",true),
                Shot("04-care", "QC003", 180, new Vector3(-1,.6f,1),47,225,6.0f,"Complete a real camp; doors close and care props appear",true,true),
                Shot("05-rain", "QC009", 180, new Vector3(0,.6f,0),48,225,6.8f,"Rain meets tent cloth and ground; existing weather",false,true),
                Shot("06-traces", "gen:qc_camp:57", 180, new Vector3(6,.7f,0),51,170,8.5f,"Occupied space between wreck sections",false,true),
                Shot("07-reveal", "gen:qc_camp:57", 210, new Vector3(6,1,0),58,175,12.5f,"Reveal an inhabited clearing; unanswered history",false,true)
            };
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, Root + "/Profiles/Teaser.asset");
            var vignette = profile.Add<Vignette>(true); vignette.intensity.Override(.1f); vignette.smoothness.Override(.5f);
            AssetDatabase.AddObjectToAsset(vignette, profile);
            config.profile = profile;
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.editorSettings.frameRate = 30;
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength; timeline.fixedDuration = 42;
            AssetDatabase.CreateAsset(timeline, Root + "/Timelines/Master42.playable");
            var rig = new GameObject("Teaser cameras — real Camp replay");
            var director = rig.AddComponent<PlayableDirector>(); director.playOnAwake = false; director.playableAsset = timeline;
            director.timeUpdateMode = DirectorUpdateMode.Manual;
            var cameraTrack = timeline.CreateTrack<CinemachineTrack>(null, "Seven real game shots");
            double start = 0;
            foreach (var shot in config.shots)
            {
                var camera = new GameObject(shot.id).AddComponent<CinemachineCamera>();
                camera.transform.SetParent(rig.transform, false); camera.transform.position = shot.start;
                camera.transform.rotation = Quaternion.Euler(shot.pitch, shot.yaw, 0);
                camera.Lens = LensSettings.Default; camera.Lens.OrthographicSize = shot.ortho;
                camera.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
                camera.gameObject.AddComponent<Animator>();
                var clip = cameraTrack.CreateClip<CinemachineShot>(); clip.start = start; clip.duration = shot.frames / 30.0; clip.displayName = shot.id;
                var cmShot = (CinemachineShot)clip.asset; cmShot.VirtualCamera.exposedName = shot.id;
                director.SetReferenceValue(shot.id, camera);
                var animation = new AnimationClip { name = shot.id + " motion", frameRate = 30 };
                float duration = shot.frames / 30f;
                string[] axes = { "x", "y", "z" };
                for (int i = 0; i < 3; i++) animation.SetCurve("", typeof(Transform), "localPosition." + axes[i], AnimationCurve.EaseInOut(0, shot.start[i], duration, shot.end[i]));
                AssetDatabase.CreateAsset(animation, Root + "/Animations/" + shot.id + ".anim"); shot.motion = animation;
                var animationTrack = timeline.CreateTrack<AnimationTrack>(null, shot.id + " motion");
                animationTrack.trackOffset = TrackOffset.ApplyTransformOffsets;
                var move = animationTrack.CreateClip(animation); move.start = start; move.duration = clip.duration;
                ((AnimationPlayableAsset)move.asset).removeStartOffset=false;
                director.SetGenericBinding(animationTrack, camera.GetComponent<Animator>());
                start += clip.duration;
            }
            if (config.music != null)
            {
                var source = rig.AddComponent<AudioSource>(); source.playOnAwake = false; source.volume = config.musicGain;
                var audio = timeline.CreateTrack<AudioTrack>(null, "Original game score");
                var clip = audio.CreateClip<AudioPlayableAsset>(); clip.start = 0; clip.duration = 42;
                var asset = (AudioPlayableAsset)clip.asset; asset.clip = config.music; asset.loop = true;
                director.SetGenericBinding(audio, source);
            }
            timeline.CreateTrack<SignalTrack>(null, "Replay events documented in config");
            config.timeline = timeline;
            config.cameraRig = PrefabUtility.SaveAsPrefabAsset(rig, Root + "/Cameras/TeaserRig.prefab");
            AssetDatabase.CreateAsset(config, ConfigPath);
            var previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var stage = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rig, stage);
            var output = new GameObject("Preview output — runtime binds actual Camp camera").AddComponent<Camera>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(output.gameObject, stage);
            var brain = output.gameObject.AddComponent<CinemachineBrain>(); director.SetGenericBinding(cameraTrack, brain);
            EditorSceneManager.SaveScene(stage, ScenePath); EditorSceneManager.CloseScene(stage, true);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
            EditorUtility.SetDirty(timeline); EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
            EnsureRotations(config);
            return config;
        }

        static void EnsureRotations(TeaserConfig config)
        {
            var signals=config.timeline.GetOutputTracks().OfType<SignalTrack>().First();
            if(!config.timeline.GetOutputTracks().OfType<ActivationTrack>().Any())
            {
                var activation=config.timeline.CreateTrack<ActivationTrack>(null,"Weather before the final cut");
                var clip=activation.CreateDefaultClip();clip.start=0;clip.duration=41.4;activation.postPlaybackState=ActivationTrack.PostPlaybackState.Revert;
                EditorUtility.SetDirty(config.timeline);
            }
            if(!signals.GetMarkers().Any())
            {
                foreach(var cue in new[]{("place-0",11+25/30.0),("place-1",11+67/30.0),("complete-camp",18.5)})
                {
                    var signal=ScriptableObject.CreateInstance<SignalAsset>();signal.name=cue.Item1;AssetDatabase.AddObjectToAsset(signal,config.timeline);
                    var emitter=signals.CreateMarker<SignalEmitter>(cue.Item2);emitter.asset=signal;
                }
                EditorUtility.SetDirty(signals);EditorUtility.SetDirty(config.timeline);
            }
            var reveal=config.shots.Last();
            if(!AnimationUtility.GetCurveBindings(reveal.motion).Any(b=>b.propertyName=="Lens.OrthographicSize"))
                AnimationUtility.SetEditorCurve(reveal.motion,EditorCurveBinding.FloatCurve("",typeof(CinemachineCamera),"Lens.OrthographicSize"),AnimationCurve.EaseInOut(0,reveal.ortho,reveal.frames/30f,reveal.ortho*1.25f));
            foreach(var track in config.timeline.GetOutputTracks().OfType<AnimationTrack>())
                foreach(var clip in track.GetClips())if(clip.asset is AnimationPlayableAsset animation){animation.removeStartOffset=false;EditorUtility.SetDirty(animation);}
            foreach(var shot in config.shots)
            {
                if(AnimationUtility.GetCurveBindings(shot.motion).Any(b=>b.propertyName.StartsWith("m_LocalRotation")))continue;
                var q=Quaternion.Euler(shot.pitch,shot.yaw,0);string[] axes={"x","y","z","w"};
                for(int i=0;i<4;i++)AnimationUtility.SetEditorCurve(shot.motion,EditorCurveBinding.FloatCurve("",typeof(Transform),"m_LocalRotation."+axes[i]),AnimationCurve.Constant(0,shot.frames/30f,q[i]));
                EditorUtility.SetDirty(shot.motion);
            }
            AssetDatabase.SaveAssets();
        }

        static TeaserShot Shot(string id,string level,int frames,Vector3 target,float pitch,float yaw,float ortho,string purpose,bool gameplay=false,bool tents=false)
        {
            var rotation = Quaternion.Euler(pitch,yaw,0);
            var position = target - rotation * Vector3.forward * 28;
            return new TeaserShot { id=id,levelId=level,frames=frames,target=target,start=position-rotation*Vector3.right*.4f,
                end=position+rotation*Vector3.right*.4f, pitch=pitch,yaw=yaw,ortho=ortho,purpose=purpose,gameplay=gameplay,startWithTents=tents,complete=id=="04-care" };
        }
    }
}
