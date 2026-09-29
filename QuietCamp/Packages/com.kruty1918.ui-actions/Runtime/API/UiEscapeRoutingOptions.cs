namespace Kruty1918.UIActions.API
{
    public sealed class UiEscapeRoutingOptions
    {
        public UiActionId EscapeAction { get; set; }
        public UiActionId TextUnfocusAction { get; set; }
        public string TextEditingContextId { get; set; } = "TextEditing";
    }
}
