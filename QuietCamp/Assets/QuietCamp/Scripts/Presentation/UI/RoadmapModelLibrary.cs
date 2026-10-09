using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace QuietCamp.Presentation.UI
{
    /// <summary>Small Editor-baked meshes of the game's actual models. No prefab
    /// instances, cameras, render textures or runtime mesh read/write requirement.</summary>
    public sealed class RoadmapModelLibrary
    {
        [Serializable, UnityEngine.Scripting.Preserve] public sealed class Model
        {
            public string id;
            public float[] data; // triangle stream: xyz, normal xyz; normalized height = 1
            public float[] uvs;public float sourceHeight=1;
            [JsonIgnore] public Vector2[] UVs;
            public int[] colors; // packed source material RGB per triangle
            [JsonIgnore] public Vector3[] Positions, Normals;
            [JsonIgnore] public Color[] Colors;
            [JsonIgnore] public float SourceHeight=1;
            [JsonIgnore] public Bounds Bounds;
            internal void Expand()
            {
                SourceHeight=sourceHeight;Positions=new Vector3[data.Length/6];Normals=new Vector3[Positions.Length];Colors=new Color[colors.Length];
                for(int i=0;i<Positions.Length;i++)
                {
                    int j=i*6;Positions[i]=new Vector3(data[j],data[j+1],data[j+2]);
                    Normals[i]=new Vector3(data[j+3],data[j+4],data[j+5]);
                }
                for(int i=0;i<colors.Length;i++)Colors[i]=new Color(((colors[i]>>16)&255)/255f,((colors[i]>>8)&255)/255f,(colors[i]&255)/255f);
                if(uvs!=null&&uvs.Length==Positions.Length*2){UVs=new Vector2[Positions.Length];for(int i=0;i<UVs.Length;i++)UVs[i]=new Vector2(uvs[i*2],uvs[i*2+1]);}uvs=null;
                Bounds=new Bounds(Positions[0],Vector3.zero);foreach(var p in Positions)Bounds.Encapsulate(p);
                data=null;colors=null; // expanded arrays are the runtime representation
            }
        }
        static RoadmapModelLibrary _shared;
        public static bool IsLoaded=>_shared!=null;
        readonly Dictionary<string,Model> _models=new Dictionary<string,Model>(StringComparer.Ordinal);
        readonly Dictionary<string,Model> _stories=new Dictionary<string,Model>(StringComparer.Ordinal);
        readonly Dictionary<string,Model> _bareTrees=new Dictionary<string,Model>(StringComparer.Ordinal);
        bool _cultureLoaded;
        public int Count=>_models.Count;
        RoadmapModelLibrary()
        {
            var asset=Resources.Load<TextAsset>("QuietCamp/roadmap_models");
            if(asset==null)throw new InvalidOperationException("Refresh procedural roadmap in the Content menu before shipping.");
            var stories=Resources.Load<TextAsset>("QuietCamp/roadmap_story_models");
            if(stories!=null)foreach(var model in JsonConvert.DeserializeObject<Model[]>(stories.text)){model.Expand();_models.Add(model.id,model);}
            foreach(var model in JsonConvert.DeserializeObject<Model[]>(asset.text))
            { model.Expand();_models.Add(model.id,model); }
        }
        public static void Reset()=>_shared=null;
        public static RoadmapModelLibrary Load()=>_shared??(_shared=new RoadmapModelLibrary());
        /// <summary>Call once while opening a cultural map, never from a scroll callback.</summary>
        public void PrepareCulture()
        {
            #if UNITY_EDITOR
            PrepareEnvironment();
            #endif
            if(_cultureLoaded)return;
            var asset=Resources.Load<TextAsset>("QuietCamp/roadmap_culture_models");
            if(asset==null)throw new InvalidOperationException("Roadmap rural models have not been exported.");
            foreach(var model in JsonConvert.DeserializeObject<Model[]>(asset.text))
            {model.Expand();_models.Add(model.id,model);}
            BareTree("ua_orchard_tree");_cultureLoaded=true;
        }
        #if UNITY_EDITOR
        bool _environmentLoaded;
        /// <summary>Authoring-only model kit. Player uses verified native baked meshes.</summary>
        public void PrepareEnvironment()
        {
            if(_environmentLoaded)return;
            LoadAuthoringModels("EnvironmentKit");_environmentLoaded=true;
        }
        public void PrepareStaging()
        {
            PrepareEnvironment();LoadAuthoringModels("StagingLandmarks");
        }
        void LoadAuthoringModels(string folder)
        {
            string path=System.IO.Path.Combine(UnityEngine.Application.dataPath,"QuietCamp/Authoring/Roadmap/Models",folder,"models.json");
            if(!System.IO.File.Exists(path))throw new InvalidOperationException("Missing roadmap authoring models: "+path);
            foreach(var model in JsonConvert.DeserializeObject<Model[]>(System.IO.File.ReadAllText(path)))if(!_models.ContainsKey(model.id)){model.Expand();_models.Add(model.id,model);}
        }
#endif
        public void PrepareSeasons(){BareTree("tree_default");BareTree("tree_pineRoundA");}
        public Model Get(string id)=>id!=null&&_models.TryGetValue(id,out var model)?model:null;
        public static bool IsOrchardFruit(Color color)=>Mathf.Abs(color.r-183/255f)<.002f&&Mathf.Abs(color.g-134/255f)<.002f&&Mathf.Abs(color.b-87/255f)<.002f;
        public Model BareTree(string id)
        {
            if(_bareTrees.TryGetValue(id,out var cached))return cached;
            var source=Get(id);if(source==null)return null;
            var colors=new Color[source.Positions.Length];var indices=new int[colors.Length];
            for(int i=0;i<indices.Length;i++)
            {indices[i]=i;colors[i]=source.Colors[i/3];if(id=="ua_orchard_tree"&&IsOrchardFruit(colors[i]))colors[i]=new Color(.3f,.5f,.2f);}
            var geometry=World.SeasonalTreeGeometry.Bare(source.Positions,source.Normals,indices,colors);
            var model=new Model{id=id+":bare",Positions=new Vector3[geometry.Indices.Length],Normals=new Vector3[geometry.Indices.Length],Colors=new Color[geometry.Indices.Length/3],SourceHeight=source.SourceHeight,Bounds=geometry.Bounds};
            for(int i=0;i<geometry.Indices.Length;i++)
            {
                int index=geometry.Indices[i];model.Positions[i]=geometry.Vertices[index];model.Normals[i]=geometry.Normals[index];
                if(i%3==0)model.Colors[i/3]=geometry.Colors[index];
            }
            _bareTrees.Add(id,model);return model;
        }
        public Model Story(string[] motifs)
        {
            if(motifs==null||motifs.Length==0)return null;
            string id=string.Join(",",motifs);if(_stories.TryGetValue(id,out var cached))return cached;
            var source=World.CampStoryComposer.Compose(new Domain.LevelData
                {environment=new Domain.EnvironmentCompositionData{storyMotifs=motifs}});
            if(source==null)return null;
            try
            {
                var v=source.vertices;var n=source.normals;var colors=source.colors;var indices=source.triangles;
                float height=Mathf.Max(.01f,source.bounds.size.y);
                var origin=new Vector3(source.bounds.center.x,source.bounds.min.y,source.bounds.center.z);
                var model=new Model{id="story:"+id,SourceHeight=height,Positions=new Vector3[indices.Length],Normals=new Vector3[indices.Length],Colors=new Color[indices.Length/3]};
                for(int i=0;i<indices.Length;i++)
                {
                    int index=indices[i];model.Positions[i]=(v[index]-origin)/height;model.Normals[i]=n[index];
                    if(i%3==0)model.Colors[i/3]=colors[index];
                }
                model.Bounds=new Bounds((source.bounds.center-origin)/height,source.bounds.size/height);
                if(_stories.Count<64)_stories.Add(id,model);
                return model;
            }
            finally
            {
                if(UnityEngine.Application.isPlaying)UnityEngine.Object.Destroy(source);else UnityEngine.Object.DestroyImmediate(source);
            }
        }
    }
}
