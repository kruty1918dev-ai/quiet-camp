using Kruty1918.UIActions.API;
using TMPro;
using UnityEngine.EventSystems;

namespace Kruty1918.UIActions.Runtime
{
    public sealed class UiEscapeRouter : IUiEscapeRouter
    {
        private readonly IUiContextStack _contexts;
        private readonly IUiActionRouter _actions;
        private readonly IUiActionJournal _journal;
        private readonly UiEscapeRoutingOptions _options;

        public UiEscapeRouter(
            IUiContextStack contexts,
            IUiActionRouter actions,
            IUiActionJournal journal,
            UiEscapeRoutingOptions options = null)
        {
            _contexts = contexts;
            _actions = actions;
            _journal = journal;
            _options = options ?? new UiEscapeRoutingOptions();
        }

        public bool TryHandleEscape()
        {
            if (UiActionId.IsValid(_options.EscapeAction.Value))
            {
                _journal?.Record(
                    new UiActionRequest(_options.EscapeAction, UiActionSource.Escape, _contexts?.ActiveContextId),
                    UiActionResult.Performed());
            }

            if (TryUnfocusTextInput())
                return true;

            if (IsExpandedDropdownSelected())
                return true;

            var active = _contexts.ActiveContexts;
            for (int i = 0; i < active.Count; i++)
            {
                UiContextRegistration context = active[i];
                if (!UiActionId.IsValid(context.EscapeActionId.Value))
                    continue;

                UiActionResult result = _actions.Execute(new UiActionRequest(
                    context.EscapeActionId,
                    UiActionSource.Escape,
                    context.ContextId));
                if (result.Consumed)
                    return true;

                if (result.Status == UiActionStatus.Rejected
                    && result.Reason != UiActionReason.WrongContext)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryUnfocusTextInput()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
                return false;

            var input = eventSystem.currentSelectedGameObject.GetComponent<TMP_InputField>();
            if (input == null || !input.isFocused)
                return false;

            eventSystem.SetSelectedGameObject(null);
            if (UiActionId.IsValid(_options.TextUnfocusAction.Value))
            {
                _journal?.Record(
                    new UiActionRequest(_options.TextUnfocusAction, UiActionSource.Escape, _options.TextEditingContextId),
                    UiActionResult.Performed());
            }
            return true;
        }

        // A shown dropdown claims Escape through the input module's Cancel
        // action on the same press; routing a context action here too would
        // close two layers for one keypress. IsExpanded stays true while the
        // list fades out, so the shield holds regardless of tick ordering.
        private bool IsExpandedDropdownSelected()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
                return false;

            TMP_Dropdown dropdown = eventSystem.currentSelectedGameObject.GetComponentInParent<TMP_Dropdown>();
            return dropdown != null && dropdown.IsExpanded;
        }
    }
}
