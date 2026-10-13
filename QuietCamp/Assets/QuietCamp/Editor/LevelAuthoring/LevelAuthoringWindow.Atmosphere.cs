using System.Collections.Generic;
using System.Linq;
using Kruty1918.Audio;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    public partial class LevelAuthoringWindow
    {
        int _seasonFilter;
        readonly Dictionary<string,Texture2D> _moodImages=new Dictionary<string,Texture2D>();
        AudioService _moodAudio;
        double _moodTime;
        void DrawAtmosphere()
        {
            EditorGUILayout.LabelField("Допомога з рослинністю",EditorStyles.boldLabel);
            foreach(var preset in VegetationAuthoring.Presets)
                if(GUILayout.Button(new GUIContent(preset.label,preset.description),GUILayout.Height(34)))Try(()=>{StopMoodAudio();var d=CaptureDraft(false);Undo.RecordObject(_state,"Додати живу зелень");VegetationAuthoring.Apply(d,preset,d.art.seed);_state.Write(d);_refreshing=true;try{_workspace?.Dispose();_workspace=new LevelAuthoringWorkspace(d);}finally{_refreshing=false;}_dirty=true;_message="Зелень додана навколо наявних деталей. Ctrl+Z — скасування.";});
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Одне натискання — новий настрій рівня",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Картки показують справжні рендери одного деморівня. Пресет застосовує світло, тіні, сезонні моделі, частинки, звуки й постобробку. Ctrl+Z скасовує застосування.",MessageType.None);
            _seasonFilter=GUILayout.Toolbar(_seasonFilter,new[]{"Весна","Літо","Осінь","Зима"});
            string season=new[]{"spring","summer","autumn","winter"}[_seasonFilter];
            var presets=SeasonAtmosphereCatalog.All.Where(p=>p.season==season).ToArray();
            int columns=Mathf.Max(1,Mathf.FloorToInt((position.width-220)/230));
            var style=new GUIStyle(GUI.skin.button){imagePosition=ImagePosition.ImageAbove,wordWrap=true,fontSize=13,fontStyle=FontStyle.Bold};
            for(int start=0;start<presets.Length;start+=columns)
                using(new EditorGUILayout.HorizontalScope())
                    for(int j=start;j<Mathf.Min(start+columns,presets.Length);j++)
                    {
                        var preset=presets[j];
                        if(!_moodImages.TryGetValue(preset.id,out var preview))_moodImages[preset.id]=preview=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/QuietCamp/Editor/LevelAuthoring/AtmospherePreviews/"+preset.id+".png");
                        var selected=Document.art.atmosphere?.id==preset.id;var previous=GUI.backgroundColor;if(selected)GUI.backgroundColor=new Color(.72f,.88f,.72f);
                        var rect=GUILayoutUtility.GetRect(220,260,GUILayout.Width(220),GUILayout.Height(260));
                        if(GUI.Button(rect,new GUIContent("",preset.description)))Try(()=>{StopMoodAudio();Edit("Застосувати сезонний пресет",d=>SeasonAtmosphereAuthoring.Apply(d,preset));});
                        if(preview!=null)GUI.DrawTexture(new Rect(rect.x+8,rect.y+8,204,204),preview,ScaleMode.ScaleToFit);
                        GUI.Label(new Rect(rect.x+8,rect.y+216,204,40),preset.label,new GUIStyle(EditorStyles.wordWrappedLabel){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold});
                        GUI.backgroundColor=previous;
                    }
            var active=Document.art.atmosphere;
            if(active==null)return;
            EditorGUILayout.Space();EditorGUILayout.LabelField("Застосовано: "+active.label,EditorStyles.boldLabel);EditorGUILayout.HelpBox(active.description,MessageType.Info);
            if(GUILayout.Button(_moodAudio==null?"Послухати звуки пресету":"Зупинити звукове прев’ю"))Try(()=>
            {
                if(_moodAudio!=null){StopMoodAudio();return;}
                var catalog=QuietCampAudioCatalog.Load();_moodAudio=new AudioService(catalog,catalog);_moodAudio.Initialize();_moodAudio.SetBusVolume(AudioBus.Ambience,.5f);_moodAudio.SetBusVolume(AudioBus.Sfx,.3f);_workspace.Mood?.BindAudio(_moodAudio);
            });
            if(GUILayout.Button("Прибрати пресет — повернути базовий вигляд"))Try(()=>Edit("Прибрати сезонний пресет",d=>d.art.atmosphere=null));
        }
        void StopMoodAudio(){_workspace?.Mood?.StopAudio();_moodAudio?.Dispose();_moodAudio=null;}
        void TickMood()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            double now=EditorApplication.timeSinceStartup;float dt=(float)System.Math.Min(.05,System.Math.Max(0,now-_moodTime));_moodTime=now;
            _workspace?.Mood?.Advance(dt);_moodAudio?.Tick();
            _workspace?.AdvanceBaseBreeze((float)now);
            if(_workspace?.Mood!=null && now-_lastMoodRepaint>.10){_lastMoodRepaint=now;SceneView.RepaintAll();}
        }
        double _lastMoodRepaint;
    }
}
