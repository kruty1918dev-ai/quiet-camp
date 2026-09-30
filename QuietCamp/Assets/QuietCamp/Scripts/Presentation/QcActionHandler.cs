using System;
using System.Collections.Generic;
using Kruty1918.UIActions.API;
namespace QuietCamp.Presentation
{
    /// <summary>
    /// Single router-facing handler with a mutable dispatch table. Scene hosts
    /// register their action implementations on load and drop them on unload —
    /// buttons, hotkeys and escape always hit the same ids.
    /// </summary>
    public sealed class QcActionHandler : IUiActionHandler
    {
        readonly Dictionary<UiActionId, Func<UiActionRequest, UiActionResult>> _map
            = new Dictionary<UiActionId, Func<UiActionRequest, UiActionResult>>();
        readonly List<UiActionId> _ids = new List<UiActionId>();

        public IReadOnlyCollection<UiActionId> ActionIds => _ids;

        public IDisposable Register(UiActionId id, Func<UiActionRequest, UiActionResult> action)
        {
            _map[id] = action;
            if (!_ids.Contains(id)) _ids.Add(id);
            return new Lease(() => _map.Remove(id));
        }

        public IDisposable Register(UiActionId id, Func<UiActionResult> action)
            => Register(id, _ => action());

        public UiActionResult Execute(in UiActionRequest request)
        {
            if (_map.TryGetValue(request.ActionId, out var action) && action != null)
                return action(request) ;
            return UiActionResult.Rejected(UiActionReason.ActionUnavailable);
        }

        sealed class Lease : IDisposable
        {
            Action _dispose;
            public Lease(Action dispose) => _dispose = dispose;
            public void Dispose() { _dispose?.Invoke(); _dispose = null; }
        }
    }
}
