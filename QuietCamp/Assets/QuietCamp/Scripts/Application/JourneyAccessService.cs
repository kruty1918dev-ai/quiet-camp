using System;
using System.Collections.Generic;
using System.Linq;

namespace QuietCamp.Application
{
    [Serializable] public sealed class JourneyDefinition
    {
        public string id, titleKey, descriptionKey, entitlementId, previewLevelId;
        public int revision = 1, currencyCost;
        public string subscriptionEntitlementId;
        public string[] requiredLevelIds = Array.Empty<string>();
        public bool published;
        /// <summary>Main-path completions required before this branch opens —
        /// the "unlocked by playing" gate; 0 keeps the journey ungated.</summary>
        public int requiredCompletions;
        public string[] levelIds = Array.Empty<string>(), storyKeys = Array.Empty<string>();
        public string StoryKey(string levelId)
        {
            int index = Array.IndexOf(levelIds, levelId);
            return index >= 0 && index < (storyKeys?.Length ?? 0) && !string.IsNullOrEmpty(storyKeys[index]) ? storyKeys[index] : null;
        }
    }
    [Serializable] public sealed class EntitlementSaveData
    {
        public int version = 1;
        public string[] ownedIds = Array.Empty<string>(), compatibilityIds = Array.Empty<string>();
        public bool Has(string id) => string.IsNullOrEmpty(id) || Array.IndexOf(ownedIds ?? Array.Empty<string>(), id) >= 0
            || Array.IndexOf(compatibilityIds ?? Array.Empty<string>(), id) >= 0;
    }
    public sealed class JourneyCatalog
    {
        readonly JourneyDefinition[] _journeys;
        public IReadOnlyList<JourneyDefinition> Journeys => _journeys;
        public JourneyCatalog(IEnumerable<JourneyDefinition> journeys)
        {
            _journeys = journeys?.ToArray() ?? throw new ArgumentNullException(nameof(journeys));
            var issues = Validate(_journeys);
            if (issues.Count > 0) throw new ArgumentException(string.Join(", ", issues));
        }
        public JourneyDefinition ForLevel(string id) => _journeys.FirstOrDefault(j => Array.IndexOf(j.levelIds, id) >= 0);
        public JourneyDefinition Find(string id) => _journeys.FirstOrDefault(j => j.id == id);
        public static List<string> Validate(IEnumerable<JourneyDefinition> journeys)
        {
            var issues = new List<string>(); var ids = new HashSet<string>(StringComparer.Ordinal); var levels = new HashSet<string>(StringComparer.Ordinal);
            foreach (var journey in journeys ?? Array.Empty<JourneyDefinition>())
            {
                if (journey == null || string.IsNullOrWhiteSpace(journey.id) || !ids.Add(journey.id)) { issues.Add("journey.id"); continue; }
                if (journey.revision < 1 || journey.currencyCost < 0 || journey.levelIds == null || journey.published && journey.levelIds.Length == 0) issues.Add("journey.content");
                // Main/qa are the campaign itself, not side products: a revisit
                // journey may reuse main levels without claiming them.
                if (journey.id == "main" || journey.id == "qa") continue;
                foreach (var id in journey.levelIds ?? Array.Empty<string>()) if (string.IsNullOrWhiteSpace(id) || !levels.Add(id)) issues.Add("journey.level");
                if (!string.IsNullOrEmpty(journey.previewLevelId) && Array.IndexOf(journey.levelIds ?? Array.Empty<string>(), journey.previewLevelId) < 0) issues.Add("journey.preview");
                foreach(var required in journey.requiredLevelIds??Array.Empty<string>())if(string.IsNullOrWhiteSpace(required)||Array.IndexOf(journey.levelIds??Array.Empty<string>(),required)>=0)issues.Add("journey.prerequisite");
                if (journey.storyKeys?.Length > 0 && journey.storyKeys.Length != journey.levelIds.Length) issues.Add("journey.story");
            }
            return issues;
        }
    }
    public enum JourneyAccessState { Available, MissingContent, Predecessor, PurchaseRequired }
    public readonly struct AccessDecision
    {
        public readonly JourneyAccessState State;
        public readonly JourneyDefinition Journey;
        public bool CanStart => State == JourneyAccessState.Available;
        public AccessDecision(JourneyAccessState state, JourneyDefinition journey) { State = state; Journey = journey; }
    }
    public sealed class JourneyAccessService
    {
        readonly JourneyCatalog _catalog;
        readonly ProgressionService _progression;
        readonly EntitlementSaveData _grants;
        readonly Func<bool> _pro;
        readonly Func<string,bool> _subscription;
        public JourneyAccessService(JourneyCatalog catalog, ProgressionService progression, EntitlementSaveData grants, Func<bool> pro,Func<string,bool> subscription=null)
        { _catalog = catalog; _progression = progression; _grants = grants; _pro = pro; _subscription=subscription; }
        public AccessDecision Evaluate(string levelId)
        {
            var journey = _catalog.ForLevel(levelId);
            if (journey == null || !journey.published) return new AccessDecision(JourneyAccessState.MissingContent, journey);
            // A progress-gated branch reports "keep walking the main path"
            // before any purchase question is even asked.
            if (journey.requiredCompletions > 0 && MainCompleted() < journey.requiredCompletions)
                return new AccessDecision(JourneyAccessState.Predecessor, journey);
            foreach(var id in journey.requiredLevelIds??Array.Empty<string>())if(!_progression.IsCompleted(id))return new AccessDecision(JourneyAccessState.Predecessor,journey);
            if (_pro()) return new AccessDecision(JourneyAccessState.Available, journey);
            if (!_grants.Has(journey.entitlementId)&&!(journey.subscriptionEntitlementId!=null&&(_subscription?.Invoke(journey.subscriptionEntitlementId)??false))) return new AccessDecision(JourneyAccessState.PurchaseRequired, journey);
            return new AccessDecision(_progression.IsCompleted(levelId) || _progression.IsUnlocked(levelId, journey.levelIds)
                ? JourneyAccessState.Available : JourneyAccessState.Predecessor, journey);
        }
        public string ContinueTarget(string journeyId)
        {
            var journey = _catalog.Find(journeyId);
            if (journey == null || !journey.published) return null;
            var next = _progression.ContinueTarget(journey.levelIds);
            return Evaluate(next).CanStart ? next : null;
        }
        public string NextAfter(string levelId)
        {
            var journey = _catalog.ForLevel(levelId);
            if (journey == null) return null;
            var next = _progression.NextAfter(levelId, journey.levelIds);
            return Evaluate(next).CanStart ? next : null;
        }
        /// <summary>Completions on the ordered main path — side-route finishes
        /// never substitute for campaign progress.</summary>
        int MainCompleted()
        {
            var main = _catalog.Find("main");
            if (main?.levelIds == null || main.levelIds.Length == 0) return _progression.CompletedCount;
            var count = 0;
            foreach (var id in main.levelIds) if (_progression.IsCompleted(id)) count++;
            return count;
        }
    }
}
