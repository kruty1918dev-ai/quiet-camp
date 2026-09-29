using System;
using System.Collections.Generic;

namespace Kruty1918.UIActions.API
{
    public sealed class UiContextRegistration
    {
        public UiContextRegistration(
            string contextId,
            UiContextLayer layer,
            int priority,
            Func<bool> isActive,
            UiActionId escapeActionId = default,
            bool blocksLowerHotkeys = false,
            IEnumerable<UiActionId> allowedHotkeyActionIds = null,
            Func<UiActionId, bool> hotkeyAllowance = null)
        {
            ContextId = string.IsNullOrWhiteSpace(contextId) ? "Unknown" : contextId;
            Layer = layer;
            Priority = priority;
            IsActive = isActive ?? (() => true);
            EscapeActionId = escapeActionId;
            BlocksLowerHotkeys = blocksLowerHotkeys;
            _allowedHotkeyActionIds = allowedHotkeyActionIds != null
                ? new HashSet<UiActionId>(allowedHotkeyActionIds)
                : null;
            HotkeyAllowance = hotkeyAllowance;
        }

        private readonly HashSet<UiActionId> _allowedHotkeyActionIds;

        public string ContextId { get; }
        public UiContextLayer Layer { get; }
        public int Priority { get; }
        public Func<bool> IsActive { get; }
        public UiActionId EscapeActionId { get; }
        public bool BlocksLowerHotkeys { get; }
        public IReadOnlyCollection<UiActionId> AllowedHotkeyActionIds => _allowedHotkeyActionIds;

        /// <summary>Optional dynamic gate applied after the static allow-list —
        /// lets a context suspend its pass-through (e.g. while it is closing).</summary>
        public Func<UiActionId, bool> HotkeyAllowance { get; }

        public bool AllowsHotkey(UiActionId actionId)
            => _allowedHotkeyActionIds != null
               && _allowedHotkeyActionIds.Contains(actionId)
               && (HotkeyAllowance == null || HotkeyAllowance(actionId));
    }
}
