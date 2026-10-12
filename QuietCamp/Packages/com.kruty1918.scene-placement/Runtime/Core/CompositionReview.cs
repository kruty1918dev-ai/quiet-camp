using System;
using System.Collections.Generic;
using System.Linq;

namespace Kruty1918.ScenePlacement.Composition
{
    [Serializable] public sealed class RoleQualityRule
    {
        public string stage;
        public int minimumVisible = 1, targetVisible = 8, maximumVisible = 32, minimumQuadrants = 1;
        public float weight = 1, minimumArea, maximumArea = .8f, minimumVisibleFraction = .15f;
        public float minimumSideFraction;
        public float countWeight = .7f, spreadWeight = .3f, balanceWeight;
        public RoleQualityRule Snapshot() => (RoleQualityRule)MemberwiseClone();
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(stage) || minimumVisible < 0 || targetVisible < minimumVisible || maximumVisible < targetVisible
                || maximumVisible > 4096 || minimumQuadrants < 0 || minimumQuadrants > 4 || !Numbers.Finite(weight) || weight <= 0
                || !Numbers.Finite(minimumArea) || !Numbers.Finite(maximumArea) || minimumArea < 0 || maximumArea > 1 || maximumArea < minimumArea
                || !Numbers.Finite(minimumVisibleFraction) || minimumVisibleFraction < 0 || minimumVisibleFraction > 1
                || !Numbers.Finite(minimumSideFraction) || minimumSideFraction < 0 || minimumSideFraction > .5f
                || !Numbers.Finite(countWeight) || !Numbers.Finite(spreadWeight) || !Numbers.Finite(balanceWeight)
                || countWeight < 0 || spreadWeight < 0 || balanceWeight < 0 || countWeight + spreadWeight + balanceWeight <= 0)
                throw new ArgumentException("Finite, ordered role quality thresholds required.");
        }
    }
    [Serializable] public sealed class CompositionReviewProfile
    {
        public float minimumScore = .5f;
        public RoleQualityRule[] roles = Array.Empty<RoleQualityRule>();
        public void Validate()
        {
            if (!Numbers.Finite(minimumScore) || minimumScore < 0 || minimumScore > 1 || roles == null || roles.Length == 0
                || roles.Any(r => r == null) || roles.Select(r => r.stage).Distinct(StringComparer.Ordinal).Count() != roles.Length)
                throw new ArgumentException("A quality profile needs unique role rules and a finite score threshold.");
            foreach (var role in roles) role.Validate();
        }
    }
    [Serializable] public sealed class RoleMetric
    {
        public string stage; public int visible, quadrants, left, right;
        public float projectedArea, score; public bool passed;
        public string[] failures = Array.Empty<string>();
    }
    [Serializable] public sealed class CompositionReview
    {
        public bool passed; public float score;
        public RoleMetric[] metrics = Array.Empty<RoleMetric>();
    }
    [Serializable] public sealed class CompositionAttempt
    {
        public int attempt, seed; public bool geometryPassed, qualityPassed;
        public float score; public string[] failures = Array.Empty<string>();
        public RoleMetric[] metrics = Array.Empty<RoleMetric>();
    }
    public interface ICompositionCritic { CompositionReview Evaluate(CompositionPlan plan); }

    /// <summary>Measures composition geometry; it does not claim to grade materials, light or artistic beauty.</summary>
    public sealed class CompositionCritic : ICompositionCritic
    {
        readonly RoleQualityRule[] roles; readonly float minimumScore;
        public CompositionCritic(CompositionReviewProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile)); profile.Validate();
            roles = profile.roles.Select(r => r.Snapshot()).ToArray(); minimumScore = profile.minimumScore;
        }
        public CompositionReview Evaluate(CompositionPlan plan)
        {
            if (plan == null || plan.layout == null) throw new ArgumentException("Resolved plan required.");
            var metrics = new List<RoleMetric>(); float total = 0, weights = 0;
            foreach (var rule in roles)
            {
                var sectors = new HashSet<int>(); int visible = 0, left = 0, right = 0; float area = 0;
                if (plan.byStage.TryGetValue(rule.stage, out var instances)) foreach (var item in instances)
                {
                    float loX = float.PositiveInfinity, loY = float.PositiveInfinity, hiX = float.NegativeInfinity, hiY = float.NegativeInfinity;
                    foreach (var b in item.volumes)
                    foreach (float x in new[] { -1f, 1f }) foreach (float y in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                    {
                        var p = plan.layout.Project(b.center + b.x * (b.half.x * x) + b.y * (b.half.y * y) + b.z * (b.half.z * z));
                        loX = Math.Min(loX, p.x); hiX = Math.Max(hiX, p.x); loY = Math.Min(loY, p.y); hiY = Math.Max(hiY, p.y);
                    }
                    float fullArea = (hiX - loX) * (hiY - loY);
                    if (!Numbers.Finite(fullArea) || fullArea <= 1e-8f) continue;
                    float clipped = Math.Max(0, Math.Min(1, hiX) - Math.Max(0, loX)) * Math.Max(0, Math.Min(1, hiY) - Math.Max(0, loY));
                    if (clipped <= 0 || clipped / fullArea < rule.minimumVisibleFraction) continue;
                    visible++; area += clipped;
                    if ((loX + hiX) * .5f < .5f) left++; else right++;
                    sectors.Add(((loX + hiX) * .5f >= .5f ? 1 : 0) + ((loY + hiY) * .5f >= .5f ? 2 : 0));
                }
                var failures = new List<string>();
                if (!plan.byStage.ContainsKey(rule.stage)) failures.Add("missing-stage");
                if (visible < rule.minimumVisible) failures.Add("too-few-visible");
                if (visible > rule.maximumVisible) failures.Add("too-many-visible");
                if (sectors.Count < rule.minimumQuadrants) failures.Add("unbalanced-framing");
                if (area < rule.minimumArea) failures.Add("insufficient-projected-area");
                if (area > rule.maximumArea) failures.Add("excessive-projected-area");
                float sideFraction = Math.Min(left, right) / (float)Math.Max(1, visible);
                if (sideFraction < rule.minimumSideFraction) failures.Add("one-sided-framing");
                float countScore = rule.targetVisible == 0 ? visible == 0 ? 1 : 0
                    : Math.Max(0, 1 - Math.Abs(visible - rule.targetVisible) / (float)Math.Max(1, rule.targetVisible));
                float spreadScore = rule.minimumQuadrants == 0 ? 1 : Math.Min(1, sectors.Count / (float)rule.minimumQuadrants);
                float balanceScore = rule.minimumSideFraction == 0 ? 1 : Math.Min(1, sideFraction / rule.minimumSideFraction);
                float score = (rule.countWeight * countScore + rule.spreadWeight * spreadScore + rule.balanceWeight * balanceScore)
                    / (rule.countWeight + rule.spreadWeight + rule.balanceWeight);
                metrics.Add(new RoleMetric { stage = rule.stage, visible = visible, quadrants = sectors.Count, left = left, right = right, projectedArea = area,
                    score = score, passed = failures.Count == 0, failures = failures.ToArray() });
                total += score * rule.weight; weights += rule.weight;
            }
            float result = total / weights;
            return new CompositionReview { passed = metrics.All(m => m.passed) && result >= minimumScore, score = result, metrics = metrics.ToArray() };
        }
    }
}
