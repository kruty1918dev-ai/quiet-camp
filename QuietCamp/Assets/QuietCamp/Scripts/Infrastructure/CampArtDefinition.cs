using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>Resolved presentation data. Puzzle data and saved revisions stay separate.</summary>
    public sealed class CampArtDefinition
    {
        // Load-only envelope fields. Defaults are omitted so authored documents
        // and composition hashes retain their existing serialization.
        [JsonProperty("absent", DefaultValueHandling=DefaultValueHandling.Ignore, NullValueHandling=NullValueHandling.Ignore)] bool _absent;
        [JsonProperty("profile", DefaultValueHandling=DefaultValueHandling.Ignore)] string _profile;
        public SeasonAtmospherePreset atmosphere;
        [JsonProperty(DefaultValueHandling=DefaultValueHandling.Ignore)] public bool mobileGeometryBudget;
        [JsonProperty(DefaultValueHandling=DefaultValueHandling.Ignore)] public bool mobileMeadow;
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)] public GroundSurfaceSettings groundSurface;
        public sealed class GroundSurfaceSettings
        {
            public float patchScale=.34f,facetScale=1.1f,relief=.12f,stoneScale=1.65f,snowDepth=.28f;
            public void Validate()
            {
                if(!new[]{patchScale,facetScale,relief,stoneScale,snowDepth}.All(Finite)
                    ||patchScale<.15f||patchScale>.7f||facetScale<.5f||facetScale>2
                    ||relief<0||relief>.2f||stoneScale<.5f||stoneScale>2||snowDepth<0||snowDepth>.4f)
                    throw new InvalidDataException("Invalid natural ground surface settings.");
            }
        }
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)] public FieldCoverSettings fieldCover;
        public sealed class FieldCoverSettings
        {
#if UNITY_EDITOR
            [JsonProperty(NullValueHandling=NullValueHandling.Ignore)] public MeadowHeightFieldPatch[] heightFields;
#endif
            public bool continuousMeadow=true;
            public float density=28,maximumHeight=.14f,maximumSpan=.28f,fringe=6,broadLeafShare=.04f,flowerCoverage=.06f,softnessPixels=2.2f;
            public void Validate()
            {
#if UNITY_EDITOR
                if(heightFields!=null)
                {
                    if(!continuousMeadow||heightFields.Length==0||heightFields.Length>256)throw new InvalidDataException("Height fields require a nonempty continuous-meadow preview.");
                    foreach(var patch in heightFields)(patch??throw new InvalidDataException("Missing height-field patch.")).Validate();
                }
#endif
                if(!new[]{density,maximumHeight,maximumSpan,fringe,broadLeafShare,flowerCoverage,softnessPixels}.All(Finite)||density<2||density>28||maximumHeight<.06f||maximumHeight>.2f||maximumSpan<.12f||maximumSpan>.45f||fringe<1||fringe>8||broadLeafShare<0||broadLeafShare>1||flowerCoverage<0||flowerCoverage>1||softnessPixels<0||softnessPixels>4)
                    throw new InvalidDataException("Invalid natural field-cover settings.");
            }
        }
        public string levelId;
        public int width, height, seed;
        public float[] cameraEuler, cameraTarget, sunEuler;
        public float orthographicSize, cameraAspect, sunIntensity, streamCentre, streamWidth, streamSlope;
        public bool frameGameplayBoard;
        public float gameplayCameraMargin = 1.12f;
        public float streamWidthVariation;
        public float streamBend = .32f, frameYaw = 225, bridgeFar = -1, landingRadius = .9f, meadowRelief = .13f, waterDepth = .64f, surroundExtent = 17.6f;
        public string waterMaterial = "QuietCamp/ArtCompositions/QC007Water", generation;
        // Optional authoring provenance. Decorative identity never changes puzzle rules.
        public string primaryLandmarkId, primaryLandmarkInstanceId;
        public FrozenLandscape landscape;
        public CampArtDefinition EditingCopy()
        {
            var copy = (CampArtDefinition)MemberwiseClone();
            copy.cameraEuler = cameraEuler?.ToArray(); copy.cameraTarget = cameraTarget?.ToArray(); copy.sunEuler = sunEuler?.ToArray();
            copy.atmosphere=atmosphere==null?null:SeasonAtmosphereCatalog.Copy(atmosphere);
            copy.groundSurface=groundSurface==null?null:new GroundSurfaceSettings{patchScale=groundSurface.patchScale,
                facetScale=groundSurface.facetScale,relief=groundSurface.relief,stoneScale=groundSurface.stoneScale,snowDepth=groundSurface.snowDepth};
            return copy;
        }
        public int generationAttempt;
        public int generationSeed;
        public Kruty1918.ScenePlacement.Composition.CompositionReview review;
        public Kruty1918.ScenePlacement.Composition.CompositionAttempt[] attempts;
        public int sunColor, ambientColor, groundLow, groundHigh, soilColor;
        public float blurPixels, exposure, saturation, contrast;
        public Model[] models;
        public Instance[] instances;
        public CoverPoint[] cover = Array.Empty<CoverPoint>();
        public sealed class CoverPoint
        {
            public int kind; public float x, y, z, height, maximumSpan, yaw, variation;
        }

        public sealed class Model
        {
            public string id; public string shading;
            public string sourceModelId;
            public string authoringRecipe;
            public string seasonalRole;
            public int[] seasonalLeafTriangles;
            public float[] vertices, normals;
            public float[] uv, tangents;
            public int[] colors;
            [JsonProperty(NullValueHandling=NullValueHandling.Ignore)] public float[] weatherExposure;
            [JsonProperty(NullValueHandling=NullValueHandling.Ignore)] public float[] seasonalGrowth;
            [JsonProperty(NullValueHandling=NullValueHandling.Ignore)] public float[] mossCoverage;
        }
        public sealed class Instance
        {
            public bool? surfaceAttached;
            public float surfaceOffset;
            public string id, modelId, assetId;
            public string motion;
            public float[] motionPivot;
            public float[] rotationEuler, scaleXYZ;
            public float x, y, z, yaw, scale = 1;
        }
        public static CampArtDefinition Load(LevelData level)
        {
#if UNITY_EDITOR
            if (level != null && EditorAuthoringPreview.MatchesArt(level.id))
            {
                var preview = EditorAuthoringPreview.Art(level.id);
                preview.Validate(level);
                return preview;
            }
#endif
            if (level == null || string.IsNullOrEmpty(level.id)) return null;
            var revision = string.IsNullOrEmpty(level.contentHash) ? null : Resources.Load<TextAsset>("QuietCamp/ArtRevisions/" + level.contentHash);
            var current = Resources.Load<TextAsset>("QuietCamp/ArtCompositions/" + level.id.Replace(':', '_'));
            var asset = revision ?? current;
            if (asset == null) return null;
            var definition = ReadDocument(asset.text, out var profile);
            if (definition == null) return null;
            if (!string.IsNullOrEmpty(profile)) return CampCompositionGenerator.Generate(level, profile, definition.seed, out _);
            definition.Validate(level);
            return CampGeometryBudget.DenseMeadow(CampGeometryBudget.Prepare(definition));
        }
        public static CampArtDefinition ReadDocument(string json, out string profile)
        {
            var definition = JsonConvert.DeserializeObject<CampArtDefinition>(json);
            if (definition == null) throw new InvalidDataException("Camp art document must be an object.");
            profile = definition._profile;
            return definition._absent ? null : definition;
        }
        public void Validate(LevelData level)
        {
            atmosphere?.Validate();
            fieldCover?.Validate();
            groundSurface?.Validate();
            landscape?.Validate();
            bool Vector(float[] value) => value != null && value.Length == 3 && value.All(Finite);
            if (levelId != level.id || width != level.width || height != level.height
                || !Vector(cameraEuler) || !Vector(cameraTarget) || !Vector(sunEuler)
                || !Finite(orthographicSize) || orthographicSize <= 0 || cameraAspect <= 0
                || frameGameplayBoard && (!Finite(gameplayCameraMargin) || gameplayCameraMargin < 1 || gameplayCameraMargin > 1.6f)
                || streamWidth <= 0 || !Finite(streamSlope) || blurPixels < 0 || blurPixels > 12 || models == null || instances == null)
                throw new InvalidDataException("Invalid camp art definition: " + levelId);
            var ids = models.Select(m => m.id).ToArray();
            if (ids.Distinct().Count() != ids.Length) throw new InvalidDataException("Duplicate camp art model ID.");
            foreach (var model in models)
                if (string.IsNullOrEmpty(model.id) || model.vertices == null || model.normals == null || model.colors == null
                    || model.vertices.Length == 0 || model.vertices.Length % 9 != 0
                    || model.normals.Length != model.vertices.Length || model.colors.Length * 3 != model.vertices.Length
                    || !model.vertices.All(Finite) || !model.normals.All(Finite)
                    || model.uv!=null&&(model.uv.Length!=model.colors.Length*2||!model.uv.All(Finite))
                    || model.mossCoverage!=null&&(model.mossCoverage.Length!=model.colors.Length||!model.mossCoverage.All(v=>Finite(v)&&v>=0&&v<=1))
                    || model.weatherExposure!=null&&(model.weatherExposure.Length!=model.colors.Length||!model.weatherExposure.All(v=>Finite(v)&&v>=0&&v<=1))
                    || model.seasonalGrowth!=null&&(model.seasonalGrowth.Length!=4||!model.seasonalGrowth.All(v=>Finite(v)&&v>=0&&v<=2))
                    || model.tangents!=null&&(model.tangents.Length!=model.colors.Length*4||!model.tangents.All(Finite)))
                    throw new InvalidDataException("Invalid camp art mesh: " + model.id);
            foreach (var item in instances)
            {
                if(item.motion!=null&&item.motion!="boat"&&item.motion!="waterwheel"&&item.motion!="cloth")throw new InvalidDataException("Unknown authored motion.");
                if((item.motion=="waterwheel"||item.motion=="cloth")&&!Vector(item.motionPivot)||item.motionPivot!=null&&!Vector(item.motionPivot))throw new InvalidDataException("Invalid motion pivot.");
                if (item.rotationEuler != null && !Vector(item.rotationEuler) || item.scaleXYZ != null && (!Vector(item.scaleXYZ) || item.scaleXYZ.Any(v => v <= 0))) throw new InvalidDataException("Invalid authored transform.");
                if (!Finite(item.x) || !Finite(item.y) || !Finite(item.z) || !Finite(item.yaw)
                    || !Finite(item.scale) || item.scale <= 0
                    || (string.IsNullOrEmpty(item.assetId) && !ids.Contains(item.modelId)))
                    throw new InvalidDataException("Invalid camp art instance.");
                if (Mathf.Abs(item.x) < width * .5f + .2f && Mathf.Abs(item.z) < height * .5f + .2f
                    && (!string.IsNullOrEmpty(item.assetId) || !IsCompressibleCover(item, models.First(m => m.id == item.modelId)) && MeshTouchesBoard(item, models.First(m => m.id == item.modelId))))
                    throw new InvalidDataException("Camp art instance enters the gameplay board: " + (item.id ?? item.modelId ?? item.assetId));
            }
            if (cover == null || cover.Length > 180000 || cover.Any(p => p == null || p.kind < 0 || p.kind > 3
                || !Finite(p.x) || !Finite(p.y) || !Finite(p.z) || !Finite(p.yaw) || !Finite(p.height) || p.height <= 0
                || !Finite(p.maximumSpan) || p.maximumSpan <= 0 || !Finite(p.variation)
                || Mathf.Abs(p.x) < width * .5f + .2f && Mathf.Abs(p.z) < height * .5f + .2f))
                throw new InvalidDataException("Invalid generated groundcover.");
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public bool IsCompressibleCover(Instance item,Model model)
        {
            if(model.seasonalRole!="groundcover"||item.surfaceAttached!=true)return false;
            var scale=item.scaleXYZ==null?Vector3.one*item.scale:Vector(item.scaleXYZ);
            var rotation=Quaternion.Euler(item.rotationEuler==null?new Vector3(0,item.yaw,0):Vector(item.rotationEuler));
            for(int i=0;i<model.vertices.Length;i+=3)
            {
                var p=new Vector3(item.x,item.y,item.z)+rotation*Vector3.Scale(new Vector3(model.vertices[i],model.vertices[i+1],model.vertices[i+2]),scale);
                if(p.y-GroundHeight(p.x,p.z)>.24f)return false;
            }
            return true;
        }
        bool MeshTouchesBoard(Instance item, Model model)
        {
            var matrix = Matrix4x4.TRS(new Vector3(item.x, item.y, item.z),
                Quaternion.Euler(item.rotationEuler == null ? new Vector3(0, item.yaw, 0) : Vector(item.rotationEuler)),
                item.scaleXYZ == null ? Vector3.one * item.scale : Vector(item.scaleXYZ));
            for (int i = 0; i < model.vertices.Length; i += 9)
            {
                Vector3 Point(int offset) => matrix.MultiplyPoint3x4(new Vector3(model.vertices[offset], model.vertices[offset + 1], model.vertices[offset + 2]));
                var a = Point(i); var b = Point(i + 3); var c = Point(i + 6);
                if (Mathf.Min(a.x, b.x, c.x) < width * .5f + .2f && Mathf.Max(a.x, b.x, c.x) > -width * .5f - .2f
                    && Mathf.Min(a.z, b.z, c.z) < height * .5f + .2f && Mathf.Max(a.z, b.z, c.z) > -height * .5f - .2f) return true;
            }
            return false;
        }
        public float GroundHeight(float x,float z)
        {
            if (landscape != null) return landscape.SampleHeight(x, z);
            float yaw=frameYaw*Mathf.Deg2Rad;
            var uv=new Vector2(x*Mathf.Cos(yaw)-z*Mathf.Sin(yaw),x*Mathf.Sin(yaw)+z*Mathf.Cos(yaw));
            float centre=streamCentre+Kruty1918.ScenePlacement.Composition.RiverGeometry.Centre(uv.y,streamSlope,streamBend);
            float width=Kruty1918.ScenePlacement.Composition.RiverGeometry.Width(uv.y,streamWidth,streamWidthVariation);
            float bank=Mathf.Abs(uv.x-centre);
            float bed=Kruty1918.ScenePlacement.Composition.RiverGeometry.Bed(bank,width,uv.y,uv.x-centre);
            float margin=Mathf.Max(Mathf.Abs(x)-this.width*.5f,Mathf.Abs(z)-height*.5f);
            float relief=Mathf.SmoothStep(0,1,margin*.5f)*meadowRelief*(.5f+.5f*Mathf.Sin(x*.55f)*Mathf.Cos(z*.68f));
            return -.045f+relief-bed*waterDepth;
        }
        public static Vector3 Vector(float[] value) => new Vector3(value[0], value[1], value[2]);
        public static Color Color(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
