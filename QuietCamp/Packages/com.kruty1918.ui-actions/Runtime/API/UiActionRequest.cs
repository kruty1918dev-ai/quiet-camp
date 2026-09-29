namespace Kruty1918.UIActions.API
{
    public readonly struct UiActionRequest
    {
        public UiActionRequest(
            UiActionId actionId,
            UiActionSource source = UiActionSource.Programmatic,
            string contextId = null,
            string targetId = null,
            object payload = null)
        {
            ActionId = actionId;
            Source = source;
            ContextId = contextId;
            TargetId = targetId;
            Payload = payload;
        }

        public UiActionId ActionId { get; }
        public UiActionSource Source { get; }
        public string ContextId { get; }
        public string TargetId { get; }
        public object Payload { get; }
    }
}
