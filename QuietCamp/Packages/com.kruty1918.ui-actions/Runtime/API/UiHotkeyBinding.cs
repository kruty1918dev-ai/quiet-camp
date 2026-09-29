using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Kruty1918.UIActions.API
{
    public enum UiHotkeyTriggerMode
    {
        TriggeredOnce = 0,
        Held = 1,
        Repeatable = 2,
    }

    public readonly struct UiHotkeyBinding
    {
        public UiHotkeyBinding(
            UiActionId actionId,
            Key primaryKey,
            Key secondaryKey = Key.None,
            bool ctrl = false,
            bool shift = false,
            bool alt = false,
            UiHotkeyTriggerMode triggerMode = UiHotkeyTriggerMode.TriggeredOnce,
            IReadOnlyCollection<string> allowedContexts = null)
        {
            ActionId = actionId;
            PrimaryKey = primaryKey;
            SecondaryKey = secondaryKey;
            Ctrl = ctrl;
            Shift = shift;
            Alt = alt;
            TriggerMode = triggerMode;
            AllowedContexts = allowedContexts;
        }

        public UiActionId ActionId { get; }
        public Key PrimaryKey { get; }
        public Key SecondaryKey { get; }
        public bool Ctrl { get; }
        public bool Shift { get; }
        public bool Alt { get; }
        public UiHotkeyTriggerMode TriggerMode { get; }
        public IReadOnlyCollection<string> AllowedContexts { get; }

        public bool HasBinding => PrimaryKey != Key.None || SecondaryKey != Key.None;
    }
}
