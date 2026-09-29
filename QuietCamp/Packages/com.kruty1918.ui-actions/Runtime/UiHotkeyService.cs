using System;
using System.Collections.Generic;
using System.Linq;
using Kruty1918.InputRouting.API;
using Kruty1918.UIActions.API;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Kruty1918.UIActions.Runtime
{
    public sealed class UiHotkeyService : IUiHotkeyService
    {
        private readonly IUiActionRouter _actions;
        private readonly IUiContextStack _contexts;
        private readonly IGameplayInputPolicy _inputPolicy;
        private readonly Func<int> _ignoredModifierMask;
        private readonly IReadOnlyList<UiHotkeyBinding> _defaultBindings;
        private readonly List<UiHotkeyBinding> _bindings = new();
        private readonly HashSet<UiActionId> _heldActionsTriggered = new();

        /// <param name="ignoredModifierMask">
        /// Optional provider returning modifier bits (1=Ctrl, 2=Shift, 4=Alt) that must not gate
        /// hotkey matching — e.g. the camera-sprint modifier held during navigation.
        /// </param>
        public UiHotkeyService(
            IUiActionRouter actions,
            IUiContextStack contexts,
            IGameplayInputPolicy inputPolicy = null,
            IReadOnlyList<UiHotkeyBinding> defaultBindings = null,
            Func<int> ignoredModifierMask = null)
        {
            _actions = actions;
            _contexts = contexts;
            _inputPolicy = inputPolicy;
            _ignoredModifierMask = ignoredModifierMask;
            _defaultBindings = defaultBindings ?? Array.Empty<UiHotkeyBinding>();
            ResetDefaults();
        }

        public IReadOnlyList<UiHotkeyBinding> Bindings => _bindings;

        public void Tick()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            Vector2 pointerPosition = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            if (!(_inputPolicy?.CanProcess(GameplayInputKind.KeyboardNavigation, pointerPosition) ?? true))
                return;

            if (IsTextInputFocused())
            {
                // A text field owns the keyboard: no binding may fire, including
                // remapped Escape chords (UiEscapeRouter owns that press). Drop
                // held latches so a release mid-typing cannot wedge the action.
                _heldActionsTriggered.Clear();
                return;
            }

            int ignoredModifiers = _ignoredModifierMask?.Invoke() ?? 0;

            for (int i = 0; i < _bindings.Count; i++)
            {
                UiHotkeyBinding binding = _bindings[i];
                if (!binding.HasBinding || !_contexts.IsActionAllowedByContext(binding.ActionId))
                    continue;

                if (!IsContextAllowed(binding))
                    continue;

                if (!WasTriggered(keyboard, binding, ignoredModifiers))
                    continue;

                _actions.Execute(binding.ActionId, UiActionSource.Hotkey, _contexts.ActiveContextId);
            }
        }

        public string GetBindingLabel(UiActionId actionId)
        {
            UiHotkeyBinding binding = _bindings.FirstOrDefault(x => x.ActionId == actionId);
            return binding.HasBinding ? KeyLabel(binding.PrimaryKey, binding) : string.Empty;
        }

        public bool SetBinding(UiHotkeyBinding binding)
        {
            if (!UiActionId.IsValid(binding.ActionId.Value))
                return false;

            int index = _bindings.FindIndex(x => x.ActionId == binding.ActionId);
            UiHotkeyBinding previous = index >= 0 ? _bindings[index] : default;
            if (index >= 0)
                _bindings[index] = binding;
            else
                _bindings.Add(binding);

            if (DetectConflicts().Count == 0)
                return true;
            if (index >= 0)
                _bindings[index] = previous;
            else
                _bindings.RemoveAt(_bindings.Count - 1);
            return false;
        }

        public void ResetDefaults()
        {
            _bindings.Clear();
            _bindings.AddRange(_defaultBindings);
        }

        public IReadOnlyList<string> DetectConflicts()
        {
            var conflicts = new List<string>();
            for (int i = 0; i < _bindings.Count; i++)
            {
                for (int j = i + 1; j < _bindings.Count; j++)
                {
                    if (!SameChord(_bindings[i], _bindings[j]))
                        continue;

                    if (ContextsOverlap(_bindings[i].AllowedContexts, _bindings[j].AllowedContexts))
                    {
                        conflicts.Add(
                            $"{_bindings[i].ActionId} conflicts with {_bindings[j].ActionId} on {KeyLabel(_bindings[i].PrimaryKey, _bindings[i])}");
                    }
                }
            }

            return conflicts;
        }

        private bool WasTriggered(
            Keyboard keyboard,
            UiHotkeyBinding binding,
            int ignoredModifiers = 0)
        {
            if (!ModifiersMatch(keyboard, binding, ignoredModifiers))
            {
                _heldActionsTriggered.Remove(binding.ActionId);
                return false;
            }

            bool pressed = KeyPressedThisFrame(keyboard, binding.PrimaryKey)
                || KeyPressedThisFrame(keyboard, binding.SecondaryKey);

            if (binding.TriggerMode == UiHotkeyTriggerMode.TriggeredOnce)
                return pressed;

            bool held = KeyHeld(keyboard, binding.PrimaryKey)
                || KeyHeld(keyboard, binding.SecondaryKey);
            if (!held)
            {
                _heldActionsTriggered.Remove(binding.ActionId);
                return false;
            }

            if (binding.TriggerMode == UiHotkeyTriggerMode.Held)
                return true;

            return _heldActionsTriggered.Add(binding.ActionId);
        }

        private bool IsContextAllowed(UiHotkeyBinding binding)
        {
            if (binding.AllowedContexts == null || binding.AllowedContexts.Count == 0)
                return true;

            IReadOnlyList<UiContextRegistration> active = _contexts.ActiveContexts;
            for (int i = 0; i < active.Count; i++)
            {
                if (binding.AllowedContexts.Contains(active[i].ContextId))
                    return true;
            }

            return binding.AllowedContexts.Contains("Gameplay");
        }

        private static bool IsTextInputFocused()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
                return false;

            TMP_InputField input = eventSystem.currentSelectedGameObject.GetComponentInParent<TMP_InputField>();
            return input != null && input.isFocused;
        }

        private static bool ModifiersMatch(Keyboard keyboard, UiHotkeyBinding binding, int ignoredModifiers = 0)
        {
            bool ctrl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            bool shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            bool alt = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
            return ((ignoredModifiers & 1) != 0 || ctrl == binding.Ctrl)
                && ((ignoredModifiers & 2) != 0 || shift == binding.Shift)
                && ((ignoredModifiers & 4) != 0 || alt == binding.Alt);
        }

        private static bool KeyPressedThisFrame(Keyboard keyboard, Key key)
            => key != Key.None && keyboard[key].wasPressedThisFrame;

        private static bool KeyHeld(Keyboard keyboard, Key key)
            => key != Key.None && keyboard[key].isPressed;

        // A chord is key + the binding's modifiers; conflicts must consider
        // primary/secondary cross-pairs — remapping A.Secondary onto B.Primary
        // would otherwise let both actions fire on one press. Unbound slots
        // (Key.None) never conflict.
        private static bool SameChord(UiHotkeyBinding left, UiHotkeyBinding right)
        {
            if (left.Ctrl != right.Ctrl || left.Shift != right.Shift || left.Alt != right.Alt)
                return false;

            return SameKey(left.PrimaryKey, right.PrimaryKey)
                || SameKey(left.PrimaryKey, right.SecondaryKey)
                || SameKey(left.SecondaryKey, right.PrimaryKey)
                || SameKey(left.SecondaryKey, right.SecondaryKey);
        }

        private static bool SameKey(Key left, Key right)
            => left != Key.None && left == right;

        private static bool ContextsOverlap(
            IReadOnlyCollection<string> left,
            IReadOnlyCollection<string> right)
        {
            if (left == null || left.Count == 0 || right == null || right.Count == 0)
                return true;

            return left.Any(right.Contains);
        }

        private static string KeyLabel(Key key, UiHotkeyBinding binding)
        {
            if (key == Key.None)
                return string.Empty;

            var parts = new List<string>();
            if (binding.Ctrl)
                parts.Add("Ctrl");
            if (binding.Shift)
                parts.Add("Shift");
            if (binding.Alt)
                parts.Add("Alt");
            parts.Add(key.ToString());
            return string.Join("+", parts);
        }
    }
}
