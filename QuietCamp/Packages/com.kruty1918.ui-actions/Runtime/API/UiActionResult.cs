namespace Kruty1918.UIActions.API
{
    public readonly struct UiActionResult
    {
        public UiActionResult(
            UiActionStatus status,
            UiActionReason reason = UiActionReason.None,
            bool consumed = true,
            string details = null)
        {
            Status = status;
            Reason = reason;
            Consumed = consumed;
            Details = details;
        }

        public UiActionStatus Status { get; }
        public UiActionReason Reason { get; }
        public bool Consumed { get; }
        public string Details { get; }

        public static UiActionResult Performed()
            => new(UiActionStatus.Performed);

        public static UiActionResult Rejected(
            UiActionReason reason,
            bool consumed = false,
            string details = null)
            => new(UiActionStatus.Rejected, reason, consumed, details);

        public static UiActionResult Rejected(
            UiActionReason reason,
            string details)
            => new(UiActionStatus.Rejected, reason, false, details);

        public static UiActionResult Ignored(
            UiActionReason reason = UiActionReason.None,
            bool consumed = false,
            string details = null)
            => new(UiActionStatus.Ignored, reason, consumed, details);

        public static UiActionResult Ignored(
            UiActionReason reason,
            string details)
            => new(UiActionStatus.Ignored, reason, false, details);

        public static UiActionResult Cancelled(
            UiActionReason reason = UiActionReason.None,
            bool consumed = true,
            string details = null)
            => new(UiActionStatus.Cancelled, reason, consumed, details);
    }
}
