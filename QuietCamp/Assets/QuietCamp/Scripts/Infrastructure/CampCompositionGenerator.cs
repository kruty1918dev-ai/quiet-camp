using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Domain;
using UnityEngine;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Composition;
using Kruty1918.VegetationPlacement;
using Kruty1918.RockPlacement;

namespace QuietCamp.Infrastructure
{
    public sealed class CampCompositionProfile
    {
        public CompositionStyle layout;
        public float[] sunEuler;
        public float sunIntensity, blurPixels, exposure, saturation, contrast;
        public int sunColor, ambientColor, groundLow, groundHigh, soilColor;
        public string waterMaterial;
        public string bridge;
        public string boundary;
        public float boundaryLengthRatio = .3f;
        public Habitat trees, reeds, flowers;
        public Rocks rocks;
        public GroundcoverStyle groundcover;
        public CompositionReviewProfile quality;
        public sealed class Habitat
        {
            public string id; public float cellSize, density; public int minimumCount;
            public Plant[] species;
            public CompositionHabitatStyle fields;
        }
        public sealed class Plant
        {
            public string id; public float weight = 1, minimumScale = 1, maximumScale = 1, spacing = .5f, minimumYaw, maximumYaw = 360;
        }
        public sealed class Rocks
        {
            public string[] assets; public float interval, minimumScale, maximumScale;
        }
    }
    public static class CampCompositionGenerator
    {
        sealed class Library { public CampArtDefinition.Model[] models; }
        public static CampArtDefinition Generate(LevelData level, string profileId, int seed, out CompositionPlan plan)
        {
            if (level == null || string.IsNullOrWhiteSpace(profileId) || profileId.Any(c => !(char.IsLetterOrDigit(c) || c == '-')))
                throw new ArgumentException("Invalid composition request.");
            T Read<T>(string path)
            {
                var text = Resources.Load<TextAsset>(path);
                if (text == null) throw new InvalidDataException("Composition resource missing: " + path);
                return JsonConvert.DeserializeObject<T>(text.text);
            }
            var profile = Read<CampCompositionProfile>("QuietCamp/ArtCompositions/Profiles/" + profileId);
            if (profile == null || profile.layout == null || profile.sunEuler == null || profile.sunEuler.Length != 3
                || profile.sunEuler.Any(v => !Numbers.Finite(v)) || !Numbers.Finite(profile.sunIntensity) || profile.sunIntensity <= 0
                || string.IsNullOrWhiteSpace(profile.waterMaterial) || profile.trees == null || profile.reeds == null || profile.flowers == null || profile.rocks == null)
                throw new InvalidDataException("Invalid composition presentation profile.");
            var library = Read<Library>("QuietCamp/ArtCompositions/CampModelLibrary");
            var catalog = AssetCatalog.Load();
            var shapes = new Dictionary<string, AssetShape>(StringComparer.Ordinal);
            foreach (var model in library.models)
            {
                var points = Enumerable.Range(0, model.vertices.Length / 3).Select(i => new V3(model.vertices[i * 3], model.vertices[i * 3 + 1], model.vertices[i * 3 + 2])).ToArray();
                shapes.Add(model.id, Shape(model.id, Box.FromPoints(points)));
            }
            AssetShape Resolve(string id)
            {
                if (shapes.TryGetValue(id, out var shape)) return shape;
                var prefab = catalog?.Prefab(id);
                if (prefab == null) throw new InvalidDataException("Composition asset missing: " + id);
                var vertices = new List<V3>();
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    var bounds = filter.sharedMesh.bounds;
                    var matrix = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    foreach (float x in new[] { -1f, 1f }) foreach (float y in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                    {
                        var p = matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                        vertices.Add(new V3(p.x, p.y, p.z));
                    }
                }
                shape = Shape(id, Box.FromPoints(vertices)); shapes.Add(id, shape); return shape;
            }
            Species[] Plants(CampCompositionProfile.Habitat habitat) => habitat.species.Select(p => new Species { id = p.id, shape = Resolve(p.id),
                weight = p.weight, minimumScale = p.minimumScale, maximumScale = p.maximumScale, spacing = p.spacing,
                minimumYaw = p.minimumYaw, maximumYaw = p.maximumYaw, maximumSlope = 65 }).ToArray();
            CompositionVegetationStage PlantsStage(CampCompositionProfile.Habitat h, CompositionHabitat habitat) =>
                new CompositionVegetationStage(h.id, habitat, Plants(h), h.cellSize, h.density, h.minimumCount, h.fields);
            var bridge = Resolve(profile.bridge);
            var bounds = bridge.volumes[0];
            // The contacts belong to the source model's two ends, with both banks required.
            bool longX = bounds.half.x > bounds.half.z;
            var end = longX ? new V3(bounds.half.x * .92f, 0, 0) : new V3(0, 0, bounds.half.z * .92f);
            var contact = new V3(bounds.center.x, bounds.center.y - bounds.half.y, bounds.center.z);
            bridge.contacts = new[] { contact - end, contact + end };
            var stages = new List<ICompositionStage> { new CrossingStage(bridge, longX ? 0 : 90) };
            if (!string.IsNullOrEmpty(profile.boundary)) stages.Add(new BankBoundaryStage(Resolve(profile.boundary), profile.boundaryLengthRatio));
            stages.Add(new ShorelineRockStage(profile.rocks.assets.Select(Resolve).ToArray(), profile.rocks.interval, profile.rocks.minimumScale, profile.rocks.maximumScale));
            stages.Add(PlantsStage(profile.trees, CompositionHabitat.WoodlandFrame));
            stages.Add(PlantsStage(profile.reeds, CompositionHabitat.Shoreline));
            stages.Add(PlantsStage(profile.flowers, CompositionHabitat.WoodlandFrame));
            if (profile.quality == null) throw new InvalidDataException("A composition quality profile is required.");
            plan = CompositionGenerator.Generate(new CompositionRequest { id = level.id, seed = seed, width = level.width, depth = level.height, style = profile.layout }, stages,
                critic: new CompositionCritic(profile.quality));
            if (!plan.Ok) throw new InvalidDataException("Composition rejected: " + string.Join("; ", plan.diagnostics.Where(d => d.error)));
            var layout = plan.layout;
            var result = new CampArtDefinition { levelId = level.id, width = level.width, height = level.height, seed = seed,
                cameraEuler = new[] { profile.layout.pitch, profile.layout.yaw, 0 }, cameraTarget = new[] { layout.target.x, layout.target.y, layout.target.z },
                cameraAspect = profile.layout.aspect, orthographicSize = layout.orthoSize, sunEuler = profile.sunEuler, sunIntensity = profile.sunIntensity,
                sunColor = profile.sunColor, ambientColor = profile.ambientColor, groundLow = profile.groundLow, groundHigh = profile.groundHigh, soilColor = profile.soilColor,
                streamCentre = layout.riverCentre, streamWidth = layout.riverWidth, streamSlope = profile.layout.riverSlope,
                streamBend = profile.layout.riverBend, frameYaw = profile.layout.yaw, bridgeFar = layout.bridgeFar, landingRadius = profile.layout.landingRadius,
                meadowRelief = profile.layout.meadowRelief, waterDepth = profile.layout.waterDepth,
                surroundExtent = layout.extent,
                waterMaterial = profile.waterMaterial, blurPixels = profile.blurPixels, exposure = profile.exposure, saturation = profile.saturation, contrast = profile.contrast,
                models = library.models, generation = profileId, generationAttempt = plan.attempt,
                generationSeed = plan.effectiveSeed, review = plan.review, attempts = plan.attempts,
                cover = CompositionGroundcover.Generate(layout, profile.groundcover, plan.effectiveSeed, plan.instances).Select(p => new CampArtDefinition.CoverPoint {
                    x = p.point.x, y = p.point.y, z = p.point.z, kind = (int)p.kind, height = p.height, maximumSpan = p.maximumSpan, yaw = p.yaw, variation = p.variation }).ToArray(),
                instances = plan.instances.Select(p => new CampArtDefinition.Instance { id = p.id,
                    modelId = library.models.Any(m => m.id == p.asset) ? p.asset : null,
                    assetId = library.models.Any(m => m.id == p.asset) ? null : p.asset,
                    x = p.pose.position.x, y = p.pose.position.y, z = p.pose.position.z, scale = p.pose.scale.x,
                    yaw = Mathf.Atan2(p.pose.forward.x, p.pose.forward.z) * Mathf.Rad2Deg }).ToArray() };
            result.Validate(level); return result;
        }
        static AssetShape Shape(string id, Box bounds) => new AssetShape { id = id, volumes = new[] { bounds },
            contacts = new[] { new V3(bounds.center.x, bounds.center.y - bounds.half.y, bounds.center.z) } };
    }
}
