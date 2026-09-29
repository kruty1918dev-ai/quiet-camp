using System;
using System.Collections.Generic;
using System.Linq;
using Kruty1918.UIActions.API;

namespace Kruty1918.UIActions.Runtime
{
    public sealed class UiContextStack : IUiContextStack
    {
        private readonly List<UiContextRegistration> _registrations = new();

        public string ActiveContextId
            => ActiveContexts.Count > 0 ? ActiveContexts[0].ContextId : "Gameplay";

        public IReadOnlyList<UiContextRegistration> ActiveContexts
            => _registrations
                .Where(registration => registration?.IsActive?.Invoke() == true)
                .OrderByDescending(registration => registration.Layer)
                .ThenByDescending(registration => registration.Priority)
                .ToArray();

        public IDisposable Push(UiContextRegistration registration)
        {
            if (registration == null)
                return Disposable.Empty;

            _registrations.Add(registration);
            return new Disposable(() => _registrations.Remove(registration));
        }

        public bool IsActionAllowedByContext(UiActionId actionId)
        {
            IReadOnlyList<UiContextRegistration> active = ActiveContexts;
            for (int i = 0; i < active.Count; i++)
            {
                UiContextRegistration context = active[i];
                if (!context.BlocksLowerHotkeys)
                    continue;

                return context.AllowsHotkey(actionId);
            }

            return true;
        }

        private sealed class Disposable : IDisposable
        {
            public static readonly IDisposable Empty = new Disposable(null);
            private Action _dispose;

            public Disposable(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                Action dispose = _dispose;
                _dispose = null;
                dispose?.Invoke();
            }
        }
    }
}
