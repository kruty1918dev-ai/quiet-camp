using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>
    /// Frozen foliage-dive transition contract, loaded once from
    /// Resources/QuietCamp/transition.json (schemaVersion 1). JSON is the only
    /// source for these values — no Inspector duplicates. Bounds follow
    /// Design/Atmosphere/04-TRANSITIONS-PROMPT-UA.md §14.
    /// </summary>
    public sealed class TransitionConfig
    {
        public const int MaxLeaves = 6;
        public static readonly Color CoverBase = new Color(0.090f, 0.212f, 0.220f); // #173638

        [Serializable] sealed class Document
        {
            public int schemaVersion;
            public float coverDuration, coveredHold, revealDuration;
            public float cameraDepthFraction, cameraSizeMultiplier, revealDepthFraction;
            public float reducedIn, reducedOut;
            public float slowHintDelay, recoveryThreshold;
            public float cueInMarker, cueOutMarker, cueInGain, cueOutGain;
            public float cueInPan, cueOutPan;
            public float duckAmount, duckAttack, duckRelease;
            public int leafLow = 2, leafBalanced = 4, leafHigh = 6;
            public float[] leafStagger, leafWide, leafGrow, leafSpin, leafFraction, leafRotation;
            public float[][] leafHome, leafDive;
            public Dictionary<string, string> coverTints, leafTints;
            public string leafBase;
            public bool blurHigh;
            public float blurRadius;
        }

        public float CoverDuration { get; }
        public float CoveredHold { get; }
        public float RevealDuration { get; }
        public float CameraDepth { get; }
        public float CameraSize { get; }
        public float RevealDepth { get; }
        public float ReducedIn { get; }
        public float ReducedOut { get; }
        public float SlowHintDelay { get; }
        public float RecoveryThreshold { get; }
        /// <summary>Normalized eased-cover progress at which the entry rustle fires.</summary>
        public float CueInMarker { get; }
        /// <summary>Normalized reveal progress at which the exit rustle fires.</summary>
        public float CueOutMarker { get; }
        public float CueInGain { get; }
        public float CueOutGain { get; }
        public float CueInPan { get; }
        public float CueOutPan { get; }
        /// <summary>Ambience dip multiplier — 0.79 ≈ −2 dB, scoped, never writes user volume.</summary>
        public float DuckAmount { get; }
        public float DuckAttack { get; }
        public float DuckRelease { get; }
        public int LeafLow { get; }
        public int LeafBalanced { get; }
        public int LeafHigh { get; }
        public float[] LeafStagger { get; }
        public float[] LeafWide { get; }
        public float[] LeafGrow { get; }
        public float[] LeafSpin { get; }
        public float[] LeafFraction { get; }
        public float[] LeafRotation { get; }
        public Vector2[] LeafHome { get; }
        public Vector2[] LeafDive { get; }
        public Color LeafBase { get; }
        public bool BlurHigh { get; }
        public float BlurRadius { get; }

        readonly IReadOnlyDictionary<string, Color> _coverTints;
        readonly IReadOnlyDictionary<string, Color> _leafTints;

        TransitionConfig(Document d,
            IReadOnlyDictionary<string, Color> coverTints,
            IReadOnlyDictionary<string, Color> leafTints, Color leafBase)
        {
            CoverDuration = d.coverDuration; CoveredHold = d.coveredHold;
            RevealDuration = d.revealDuration;
            CameraDepth = d.cameraDepthFraction; CameraSize = d.cameraSizeMultiplier;
            RevealDepth = d.revealDepthFraction;
            ReducedIn = d.reducedIn; ReducedOut = d.reducedOut;
            SlowHintDelay = d.slowHintDelay; RecoveryThreshold = d.recoveryThreshold;
            CueInMarker = d.cueInMarker; CueOutMarker = d.cueOutMarker;
            CueInGain = d.cueInGain; CueOutGain = d.cueOutGain;
            CueInPan = Mathf.Clamp(d.cueInPan, -.15f, .15f);
            CueOutPan = Mathf.Clamp(d.cueOutPan, -.15f, .15f);
            DuckAmount = d.duckAmount; DuckAttack = d.duckAttack; DuckRelease = d.duckRelease;
            LeafLow = d.leafLow; LeafBalanced = d.leafBalanced; LeafHigh = d.leafHigh;
            LeafStagger = d.leafStagger; LeafWide = d.leafWide; LeafGrow = d.leafGrow;
            LeafSpin = d.leafSpin; LeafFraction = d.leafFraction; LeafRotation = d.leafRotation;
            LeafHome = ToVec(d.leafHome); LeafDive = ToVec(d.leafDive);
            LeafBase = leafBase;
            BlurHigh = d.blurHigh; BlurRadius = d.blurRadius;
            _coverTints = coverTints; _leafTints = leafTints;
        }

        /// <summary>Cover tint for the target phase; falls back to the base forest colour.</summary>
        public Color CoverTint(string phase)
            => phase != null && _coverTints.TryGetValue(phase, out var c) ? c : CoverBase;

        /// <summary>Leaf tint for the target phase; falls back to the neutral sage.</summary>
        public Color LeafTint(string phase)
            => phase != null && _leafTints.TryGetValue(phase, out var c) ? c : LeafBase;

        /// <summary>Leaf sprite count per quality tier: 0=Low, 1=Balanced, 2=High.</summary>
        public int LeafCount(int tier)
            => Mathf.Clamp(tier <= 0 ? LeafLow : tier == 1 ? LeafBalanced : LeafHigh, 0, MaxLeaves);

        public static TransitionConfig Load()
        {
            var asset = Resources.Load<TextAsset>("QuietCamp/transition");
            if (asset == null) throw new InvalidOperationException("QuietCamp transition.json is missing.");
            return Parse(asset.text);
        }

        public static TransitionConfig Parse(string json)
        {
            var d = JsonConvert.DeserializeObject<Document>(json);
            if (d == null || d.schemaVersion != 1)
                throw new InvalidOperationException("Invalid transition schema.");
            if (!InRange(d.coverDuration, .05f, 3f) || !InRange(d.coveredHold, 0f, 2f)
                || !InRange(d.revealDuration, .05f, 3f)
                || !InRange(d.cameraDepthFraction, 0f, .2f)
                || !InRange(d.cameraSizeMultiplier, .9f, 1f)
                || !InRange(d.revealDepthFraction, 0f, .2f)
                || !InRange(d.reducedIn, .05f, 1f) || !InRange(d.reducedOut, .05f, 1f)
                || !InRange(d.slowHintDelay, .5f, 10f) || !InRange(d.recoveryThreshold, 2f, 60f)
                || !InRange(d.cueInMarker, 0f, 1f) || !InRange(d.cueOutMarker, 0f, 1f)
                || !InRange(d.cueInGain, 0f, 1f) || !InRange(d.cueOutGain, 0f, 1f)
                || !InRange(d.duckAmount, 0f, 1f) || !InRange(d.duckAttack, 0f, 1f)
                || !InRange(d.duckRelease, .05f, 3f)
                || !InRange(d.leafLow, 0, MaxLeaves) || !InRange(d.leafBalanced, 0, MaxLeaves)
                || !InRange(d.leafHigh, 0, MaxLeaves)
                || !InRange(d.blurRadius, 0f, 8f))
                throw new InvalidOperationException("Transition value out of bounds.");
            Require(d.leafStagger, "leafStagger"); Require(d.leafWide, "leafWide");
            Require(d.leafGrow, "leafGrow"); Require(d.leafSpin, "leafSpin");
            Require(d.leafFraction, "leafFraction"); Require(d.leafRotation, "leafRotation");
            if (d.leafHome == null || d.leafHome.Length != MaxLeaves
                || d.leafDive == null || d.leafDive.Length != MaxLeaves)
                throw new InvalidOperationException("leafHome/leafDive need 6 normalized pairs.");
            if (!ColorUtility.TryParseHtmlString(d.leafBase ?? "#597361", out var leafBase))
                throw new InvalidOperationException("Invalid leafBase colour.");
            return new TransitionConfig(d, Tints(d.coverTints), Tints(d.leafTints), leafBase);
        }

        static void Require(float[] a, string name)
        {
            if (a == null || a.Length != MaxLeaves)
                throw new InvalidOperationException(name + " must hold exactly 6 entries.");
            foreach (var v in a)
                if (float.IsNaN(v) || float.IsInfinity(v))
                    throw new InvalidOperationException(name + " contains a non-finite value.");
        }

        static Vector2[] ToVec(float[][] rows)
        {
            var v = new Vector2[rows.Length];
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null || rows[i].Length != 2
                    || !InRange(rows[i][0], -.5f, 1.5f) || !InRange(rows[i][1], -.5f, 1.5f))
                    throw new InvalidOperationException("Bad leaf coordinate at index " + i);
                v[i] = new Vector2(rows[i][0], rows[i][1]);
            }
            return v;
        }

        static IReadOnlyDictionary<string, Color> Tints(Dictionary<string, string> src)
        {
            var map = new Dictionary<string, Color>(StringComparer.Ordinal);
            if (src != null)
                foreach (var p in src)
                {
                    if (!IsPhase(p.Key) || !ColorUtility.TryParseHtmlString(p.Value, out var c))
                        throw new InvalidOperationException("Invalid transition tint: " + p.Key);
                    map[p.Key] = c;
                }
            return map;
        }

        static bool IsPhase(string id)
            => id == "morning" || id == "noon" || id == "evening" || id == "night" || id == "day";

        static bool InRange(float v, float min, float max)
            => !float.IsNaN(v) && !float.IsInfinity(v) && v >= min && v <= max;
    }
}
