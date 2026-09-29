namespace Kruty1918.UIActions.API
{
    public readonly struct UiActionJournalEntry
    {
        public UiActionJournalEntry(
            double timestampSeconds,
            int frame,
            UiActionId actionId,
            UiActionSource source,
            string contextId,
            string targetId,
            UiActionStatus status,
            UiActionReason reason,
            bool consumed,
            string details)
        {
            TimestampSeconds = timestampSeconds;
            Frame = frame;
            ActionId = actionId;
            Source = source;
            ContextId = contextId;
            TargetId = targetId;
            Status = status;
            Reason = reason;
            Consumed = consumed;
            Details = details;
        }

        public double TimestampSeconds { get; }
        public int Frame { get; }
        public UiActionId ActionId { get; }
        public UiActionSource Source { get; }
        public string ContextId { get; }
        public string TargetId { get; }
        public UiActionStatus Status { get; }
        public UiActionReason Reason { get; }
        public bool Consumed { get; }
        public string Details { get; }
    }
}
