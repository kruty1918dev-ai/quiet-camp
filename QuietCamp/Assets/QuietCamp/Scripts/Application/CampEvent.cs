using QuietCamp.Domain;
namespace QuietCamp.Application
{
    /// <summary>Ordered session event stream; listeners unsubscribe on dispose.</summary>
    public enum CampEventKind
    {
        SelectionChanged,
        BoardCommitted,
        RulesChanged,
        LevelCompleted,
        SettingsChanged,
    }

    public readonly struct CampEvent
    {
        public CampEvent(CampEventKind kind, string guestId, RuleReport report, int revision)
        {
            Kind = kind;
            GuestId = guestId;
            Report = report;
            Revision = revision;
        }

        public CampEventKind Kind { get; }
        public string GuestId { get; }
        public RuleReport Report { get; }
        public int Revision { get; }
    }
}
