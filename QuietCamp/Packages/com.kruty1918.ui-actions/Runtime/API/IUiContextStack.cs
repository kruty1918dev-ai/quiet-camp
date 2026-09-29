using System;
using System.Collections.Generic;

namespace Kruty1918.UIActions.API
{
    public interface IUiContextStack
    {
        IDisposable Push(UiContextRegistration registration);
        string ActiveContextId { get; }
        IReadOnlyList<UiContextRegistration> ActiveContexts { get; }
        bool IsActionAllowedByContext(UiActionId actionId);
    }
}
