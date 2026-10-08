using System;
using System.Collections.Generic;
using System.Globalization;

namespace QuietCamp.Domain
{
    /// <summary>Structural authoring checks, not an automated historical fact checker.</summary>
    public static class EnvironmentalStoryValidator
    {
        public static List<string> Validate(EnvironmentalStoryData data,
            Func<string, bool> levelExists = null, Func<string, bool> assetExists = null)
        {
            var errors = new List<string>();
            if (data == null) return errors; // Old levels/snapshots remain unchanged.
            void Error(string code, string id) => errors.Add("story:" + code + ":" + id);
            if (data.schemaVersion != 1 || string.IsNullOrWhiteSpace(data.id)) Error("schema", data.id);
            if (data.beats == null || data.beats.Length == 0 || data.beats.Length > 12) Error("beat-budget", data.id);
            if (data.references == null || data.references.Length > 8) Error("reference-budget", data.id);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            bool Id(string id)
            {
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) { Error("duplicate-id", id); return false; }
                return true;
            }
            var references = new Dictionary<string, EnvironmentalStoryReference>(StringComparer.Ordinal);
            foreach (var reference in data.references ?? Array.Empty<EnvironmentalStoryReference>())
            {
                if (reference == null) { Error("reference-null", data.id); continue; }
                if (Id(reference.id)) references.Add(reference.id, reference);
                if (reference.basis != "documented" && reference.basis != "fiction-inspired") Error("reference-basis", reference.id);
                if (string.IsNullOrWhiteSpace(reference.reviewer) || !Date(reference.reviewedOn)) Error("reference-review", reference.id);
                if (reference.sources == null || reference.sources.Length == 0 || reference.sources.Length > 8) Error("source-budget", reference.id);
                var sources = new HashSet<string>(StringComparer.Ordinal);
                foreach (var source in reference.sources ?? Array.Empty<EnvironmentalStorySource>())
                {
                    if (source == null) { Error("source-null", reference.id); continue; }
                    if (Id(source.id)) sources.Add(source.id);
                    if (string.IsNullOrWhiteSpace(source.title) || string.IsNullOrWhiteSpace(source.publisher)
                        || !Date(source.accessedOn) || source.authority != "primary" && source.authority != "authoritative"
                        || !Uri.TryCreate(source.url, UriKind.Absolute, out var uri) || uri.Scheme != "https") Error("source", source.id);
                }
                if (reference.claims == null || reference.claims.Length > 16 || reference.basis == "documented" && reference.claims.Length == 0) Error("claim-budget", reference.id);
                foreach (var claim in reference.claims ?? Array.Empty<EnvironmentalStoryClaim>())
                {
                    if (claim == null) { Error("claim-null", reference.id); continue; }
                    Id(claim.id);
                    if (string.IsNullOrWhiteSpace(claim.statement) || claim.sourceIds == null || claim.sourceIds.Length == 0) Error("claim", claim.id);
                    foreach (var source in claim.sourceIds ?? Array.Empty<string>())
                        if (source == null || !sources.Contains(source)) Error("claim-source", claim.id);
                }
            }
            int props = 0;
            var levelCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var beat in data.beats ?? Array.Empty<EnvironmentalStoryBeat>())
            {
                if (beat == null) { Error("beat-null", data.id); continue; }
                Id(beat.id);
                if (string.IsNullOrWhiteSpace(beat.storyBeat) || string.IsNullOrWhiteSpace(beat.worldStateBefore)
                    || string.IsNullOrWhiteSpace(beat.worldStateAfter) || string.IsNullOrWhiteSpace(beat.intendedInference)) Error("beat-intent", beat.id);
                if (EnvironmentalStoryPolicy.Rank(beat.roadmapVisibility, true) < 0
                    || EnvironmentalStoryPolicy.Rank(beat.levelDetailVisibility, false) < 0) Error("visibility", beat.id);
                var levels = new HashSet<string>(StringComparer.Ordinal);
                if (beat.levelIds == null || beat.levelIds.Length == 0 || beat.levelIds.Length > 32) Error("level-binding", beat.id);
                foreach (var level in beat.levelIds ?? Array.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(level) || !levels.Add(level) || levelExists != null && !levelExists(level)) { Error("level-binding", beat.id); continue; }
                    levelCounts.TryGetValue(level, out int count); levelCounts[level] = count + (beat.props?.Length ?? 0);
                }
                references.TryGetValue(beat.historicalReference ?? "", out var reference);
                bool verified = beat.referenceConfidence == "verified" && reference?.basis == "documented";
                if (beat.referenceConfidence == "fictional")
                { if (!string.IsNullOrEmpty(beat.historicalReference)) Error("fictional-reference", beat.id); }
                else if (beat.referenceConfidence == "fiction-inspired")
                { if (reference?.basis != "fiction-inspired") Error("missing-inspired-reference", beat.id); }
                else if (!verified) Error("unverified-reference", beat.id);
                if (beat.props == null || beat.props.Length == 0 || beat.props.Length > 12) Error("prop-budget", beat.id);
                var localProps = new HashSet<string>(StringComparer.Ordinal);
                foreach (var prop in beat.props ?? Array.Empty<EnvironmentalStoryProp>())
                {
                    props++;
                    if (prop == null) { Error("prop-null", beat.id); continue; }
                    Id(prop.id); if (prop.id != null) localProps.Add(prop.id);
                    if (!Motif(prop.category, prop.motif)) Error("motif", prop.id);
                    if(prop.category!="nature"&&(prop.roadmap?.sway==true||prop.level?.sway==true))Error("rigid-prop-wind",prop.id);
                    if (prop.appearance != "always" && prop.appearance != "before-care" && prop.appearance != "after-care") Error("appearance", prop.id);
                    foreach (var tag in prop.contentTags ?? Array.Empty<string>())
                        if (!Tag(tag)) Error("content-tag", prop.id);
                    var claim = Array.Find(reference?.claims ?? Array.Empty<EnvironmentalStoryClaim>(), c => c != null && c.id == prop.claimId);
                    if ((!string.IsNullOrEmpty(prop.claimId) || Array.IndexOf(prop.contentTags ?? Array.Empty<string>(), "technical-marking") >= 0)
                        && (!verified || claim == null)) Error("unsupported-evidence", prop.id);
                    View(beat.roadmapVisibility, prop.roadmapVisibility, prop.roadmap, true, prop.id);
                    View(beat.levelDetailVisibility, prop.levelDetailVisibility, prop.level, false, prop.id);
                }
                if (!string.IsNullOrEmpty(beat.heroLandmark) && !localProps.Contains(beat.heroLandmark)) Error("landmark", beat.id);
                if (!string.IsNullOrEmpty(beat.returningProp) && !Array.Exists(beat.props ?? Array.Empty<EnvironmentalStoryProp>(), p => p != null && p.returningProp == beat.returningProp)) Error("returning-prop", beat.id);
            }
            if (props > 24) Error("region-prop-budget", data.id);
            foreach (var count in levelCounts) if (count.Value > 12) Error("level-prop-budget", count.Key);
            return errors;

            void View(string parent, string child, EnvironmentalStoryView view, bool roadmap, string id)
            {
                int rank = EnvironmentalStoryPolicy.Rank(child == "inherit" ? parent : child, roadmap);
                if (rank < 0 || rank > EnvironmentalStoryPolicy.Rank(parent, roadmap)) Error("visibility-escalation", id);
                if (rank == 0)
                { if (view != null) Error("hidden-view", id); return; } // Hidden evidence cannot enter baked map data.
                if (view == null) { Error("missing-view", id); return; }
                if (string.IsNullOrWhiteSpace(view.assetId) || assetExists != null && !assetExists(view.assetId)) Error("missing-asset", id);
                if (!Finite(view.x) || !Finite(view.z) || !Finite(view.elevation) || !Finite(view.height) || !Finite(view.yaw)
                    || Math.Abs(view.x) > 40 || Math.Abs(view.z) > 40 || view.elevation < 0 || view.elevation > 12 || view.height <= 0 || view.height > 12) Error("view-bounds", id);
                if (roadmap && view.elevation != 0) Error("roadmap-elevation", id);
            }
        }

        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        static bool Date(string x) => DateTime.TryParseExact(x, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        static bool Tag(string value) => value == "technical-marking" || value == "civil-warning" || value == "memorial"
            || value == "fiction-inspired" || value == "repair" || value == "weathering";
        public static bool Motif(string category, string motif)
        {
            string values = category == "nature" ? "|field|shelterbelt|steppe|river|sea|reeds|orchard|chernozem|hills|"
                : category == "civilian" ? "|bus-stop|power-lines|road|greenhouse|fence|outbuilding|railway|pier|grain-storage|dam|rural-infrastructure|"
                : category == "recovery" ? "|repaired-bridge|abandoned-structure|damaged-infrastructure|civilian-debris|memorial-trace|volunteer-repair|temporary-crossing|" : "";
            return !string.IsNullOrEmpty(motif) && motif.IndexOf('|') < 0 && values.Contains("|" + motif + "|");
        }
    }
}
