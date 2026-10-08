using System.Collections.Generic;

namespace Kruty1918.LevelKit
{
    public enum LevelIssueSeverity { Info, Warning, Error }

    /// <summary>One parse/validation diagnostic with a JSON-ish path hint.</summary>
    public sealed class LevelIssue
    {
        public readonly LevelIssueSeverity Severity;
        public readonly string Path;
        public readonly string Message;

        public LevelIssue(LevelIssueSeverity severity, string path, string message)
        {
            Severity = severity;
            Path = path ?? "";
            Message = message ?? "";
        }

        public override string ToString()
            => $"[{Severity}] {(string.IsNullOrEmpty(Path) ? Message : Path + ": " + Message)}";
    }

    /// <summary>Result of a LevelJson.Parse call: document + diagnostics.</summary>
    public sealed class LevelResult
    {
        public LevelDocument Document;
        public readonly List<LevelIssue> Issues = new List<LevelIssue>();

        public bool Ok => Document != null
            && !Issues.Exists(i => i.Severity == LevelIssueSeverity.Error);

        public void Add(LevelIssueSeverity severity, string path, string message)
            => Issues.Add(new LevelIssue(severity, path, message));
    }
}
