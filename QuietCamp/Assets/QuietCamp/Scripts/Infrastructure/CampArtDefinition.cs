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
        public string levelId;
        public int width, height, seed;
        public float[] cameraEuler, cameraTarget, sunEuler;
        public float orthographicSize, cameraAspect, sunIntensity, streamCentre, streamWidth, streamSlope;
        public float streamBend = .32f, frameYaw = 225, bridgeFar = -1, landingRadius = .9f, meadowRelief = .13f, waterDepth = .64f, surroundExtent = 17.6f;
        public string waterMaterial = "QuietCamp/ArtCompositions/QC007Water", generation;
        public int generationAttempt;
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
            public string id;
            public float[] vertices, normals;
            public int[] colors;
        }
        public sealed class Instance
        {
            public string id, modelId, assetId;
            public float x, y, z, yaw, scale = 1;
        }
        public static CampArtDefinition Load(LevelData level)
        {
            if (level == null || string.IsNullOrEmpty(level.id) || !level.id.StartsWith("QC", StringComparison.Ordinal)) return null;
            var asset = Resources.Load<TextAsset>("QuietCamp/ArtCompositions/" + level.id);
            if (asset == null) return null;
            var reference = JsonConvert.DeserializeObject<ProfileReference>(asset.text);
            if (!string.IsNullOrEmpty(reference.profile)) return CampCompositionGenerator.Generate(level, reference.profile, reference.seed, out _);
            var definition = JsonConvert.DeserializeObject<CampArtDefinition>(asset.text);
            definition.Validate(level);
            return definition;
        }
        sealed class ProfileReference { public string profile; public int seed; }
        public void Validate(LevelData level)
        {
            bool Vector(float[] value) => value != null && value.Length == 3 && value.All(Finite);
            if (levelId != level.id || width != level.width || height != level.height
                || !Vector(cameraEuler) || !Vector(cameraTarget) || !Vector(sunEuler)
                || !Finite(orthographicSize) || orthographicSize <= 0 || cameraAspect <= 0
                || streamWidth <= 0 || !Finite(streamSlope) || blurPixels < 0 || blurPixels > 12 || models == null || instances == null)
                throw new InvalidDataException("Invalid camp art definition: " + levelId);
            var ids = models.Select(m => m.id).ToArray();
            if (ids.Distinct().Count() != ids.Length) throw new InvalidDataException("Duplicate camp art model ID.");
            foreach (var model in models)
                if (string.IsNullOrEmpty(model.id) || model.vertices == null || model.normals == null || model.colors == null
                    || model.vertices.Length == 0 || model.vertices.Length % 9 != 0
                    || model.normals.Length != model.vertices.Length || model.colors.Length * 3 != model.vertices.Length
                    || !model.vertices.All(Finite) || !model.normals.All(Finite))
                    throw new InvalidDataException("Invalid camp art mesh: " + model.id);
            foreach (var item in instances)
            {
                if (!Finite(item.x) || !Finite(item.y) || !Finite(item.z) || !Finite(item.yaw)
                    || !Finite(item.scale) || item.scale <= 0
                    || (string.IsNullOrEmpty(item.assetId) && !ids.Contains(item.modelId)))
                    throw new InvalidDataException("Invalid camp art instance.");
                if (Mathf.Abs(item.x) < width * .5f + .2f && Mathf.Abs(item.z) < height * .5f + .2f)
                    throw new InvalidDataException("Camp art instance enters the gameplay board.");
            }
            if (cover == null || cover.Length > 180000 || cover.Any(p => p == null || p.kind < 0 || p.kind > 3
                || !Finite(p.x) || !Finite(p.y) || !Finite(p.z) || !Finite(p.yaw) || !Finite(p.height) || p.height <= 0
                || !Finite(p.maximumSpan) || p.maximumSpan <= 0 || !Finite(p.variation)
                || Mathf.Abs(p.x) < width * .5f + .2f && Mathf.Abs(p.z) < height * .5f + .2f))
                throw new InvalidDataException("Invalid generated groundcover.");
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static Vector3 Vector(float[] value) => new Vector3(value[0], value[1], value[2]);
        public static Color Color(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
