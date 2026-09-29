using System.Collections.Generic;

namespace Kruty1918.UIActions.API
{
    public interface IUiActionHandler
    {
        IReadOnlyCollection<UiActionId> ActionIds { get; }
        UiActionResult Execute(in UiActionRequest request);
    }
}
