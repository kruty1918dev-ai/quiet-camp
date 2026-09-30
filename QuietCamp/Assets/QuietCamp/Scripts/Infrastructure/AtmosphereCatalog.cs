using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>Validated presentation settings, independent of level content hashes and saves.</summary>
    public sealed class AtmosphereCatalog
    {
        [Serializable] sealed class Document
        {
            public int schemaVersion;
            public Dictionary<string, string> levelPhases;
            public ProfileData[] profiles;
        }
        [Serializable] sealed class ProfileData
        {
            public string id, sun, ambient, foreground;
            public float sunIntensity, elevation, wind, birdMin, birdMax, crickets;
            public bool fire;
        }

        public sealed class Profile
        {
            public string Id { get; }
            public Color Sun { get; }
            public Color Ambient { get; }
            public Color Foreground { get; }
            public float SunIntensity { get; }
            public float Elevation { get; }
            public float Wind { get; }
            public float BirdMin { get; }
            public float BirdMax { get; }
            public float Crickets { get; }
            public bool Fire { get; }

            internal Profile(string id, Color sun, Color ambient, Color foreground, float intensity,
                float elevation, float wind, float birdMin, float birdMax, float crickets, bool fire)
            {
                Id = id; Sun = sun; Ambient = ambient; Foreground = foreground;
                SunIntensity = intensity; Elevation = elevation; Wind = wind;
                BirdMin = birdMin; BirdMax = birdMax; Crickets = crickets; Fire = fire;
            }
        }

        readonly Dictionary<string, Profile> _profiles;
        readonly Dictionary<string, string> _levels;

        AtmosphereCatalog(Dictionary<string, Profile> profiles, Dictionary<string, string> levels)
        { _profiles = profiles; _levels = levels; }

        public static AtmosphereCatalog Load()
        {
            var asset = Resources.Load<TextAsset>("QuietCamp/atmosphere");
            if (asset == null) throw new InvalidOperationException("QuietCamp atmosphere.json is missing.");
            return Parse(asset.text);
        }

        public static AtmosphereCatalog Parse(string json)
        {
            var doc = JsonConvert.DeserializeObject<Document>(json);
            if (doc == null || doc.schemaVersion != 1 || doc.profiles == null)
                throw new InvalidOperationException("Invalid atmosphere schema.");
            var profiles = new Dictionary<string, Profile>(StringComparer.Ordinal);
            foreach (var p in doc.profiles)
            {
                if (p == null || !IsPhase(p.id) || profiles.ContainsKey(p.id)
                    || !InRange(p.sunIntensity, 0, 2) || !InRange(p.elevation, 0, 90)
                    || !InRange(p.wind, 0, 1) || !InRange(p.crickets, 0, 1)
                    || !InRange(p.birdMin, 0, 300) || !InRange(p.birdMax, p.birdMin, 300)
                    || (p.birdMin == 0 && p.birdMax != 0))
                    throw new InvalidOperationException("Invalid or duplicate atmosphere profile.");
                if (!ColorUtility.TryParseHtmlString(p.sun, out var sun)
                    || !ColorUtility.TryParseHtmlString(p.ambient, out var ambient)
                    || !ColorUtility.TryParseHtmlString(p.foreground, out var foreground))
                    throw new InvalidOperationException("Invalid atmosphere color: " + p.id);
                profiles.Add(p.id, new Profile(p.id, sun, ambient, foreground, p.sunIntensity,
                    p.elevation, p.wind, p.birdMin, p.birdMax, p.crickets, p.fire));
            }
            if (profiles.Count != 4) throw new InvalidOperationException("All four atmosphere phases are required.");
            var levels = new Dictionary<string, string>(StringComparer.Ordinal);
            if (doc.levelPhases != null)
                foreach (var pair in doc.levelPhases)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || !profiles.ContainsKey(pair.Value))
                        throw new InvalidOperationException("Invalid level atmosphere override: " + pair.Key);
                    levels.Add(pair.Key, pair.Value);
                }
            return new AtmosphereCatalog(profiles, levels);
        }

        public Profile Resolve(string levelId, string legacyLighting)
        {
            if (levelId != null && _levels.TryGetValue(levelId, out var id)) return _profiles[id];
            return Get(legacyLighting);
        }

        public Profile Get(string phase)
        {
            if (phase == "day") phase = "noon";
            return phase != null && _profiles.TryGetValue(phase, out var profile)
                ? profile : _profiles["noon"];
        }

        static bool IsPhase(string id) => id == "morning" || id == "noon" || id == "evening" || id == "night";
        static bool InRange(float value, float min, float max)
            => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
    }
}

