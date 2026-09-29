using System.Collections.Generic;

namespace Kruty1918.UIActions.API
{
    public interface IUiHotkeyService
    {
        IReadOnlyList<UiHotkeyBinding> Bindings { get; }
        string GetBindingLabel(UiActionId actionId);
        bool SetBinding(UiHotkeyBinding binding);
        void ResetDefaults();
        IReadOnlyList<string> DetectConflicts();
    }
}
