using System;
using System.Collections.Generic;
namespace QuietCamp.Application
{
    /// <summary>
    /// Shows tutorial.<n> instruction keys from the level's tutorialKey.
    /// An instruction closes only after the matching real player action,
    /// never on a timer.
    /// </summary>
    public sealed class TutorialDirector
    {
        static readonly Dictionary<string, string> RequiredAction = new Dictionary<string, string>
        {
            ["tutorial.1"] = "commit",
            ["tutorial.2"] = "move",
            ["tutorial.3"] = "commit",
            ["tutorial.4"] = "commit",
            ["tutorial.5"] = "rotate",
            ["tutorial.6"] = "check",
            ["tutorial.7"] = "commit",
            ["tutorial.8"] = "commit",
        };

        public string ActiveKey { get; private set; }
        public bool Finished { get; private set; }

        public event Action Changed;

        public TutorialDirector(string tutorialKey)
        {
            ActiveKey = string.IsNullOrWhiteSpace(tutorialKey) ? null : tutorialKey;
            Finished = ActiveKey == null;
        }

        /// <summary>Reports a real gameplay action; returns true when it dismissed the hint.</summary>
        public bool ReportAction(string actionId)
        {
            if (Finished || ActiveKey == null || actionId == null) return false;
            if (!RequiredAction.TryGetValue(ActiveKey, out var required) || required != actionId)
                return false;
            Finished = true;
            ActiveKey = null;
            Changed?.Invoke();
            return true;
        }
    }
}
