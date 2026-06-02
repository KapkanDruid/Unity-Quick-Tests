using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityQuickTests.Editor
{
    internal sealed class QuickTestHotkeyBinding
    {
        private const EventModifiers SupportedModifierMask =
            EventModifiers.Control |
            EventModifiers.Shift |
            EventModifiers.Alt |
            EventModifiers.Command;

        private readonly QuickTestMethod _method;
        private readonly IQuickTestInputSource _inputSource;
        private readonly EventModifiers _modifiers;
        private readonly KeyCode _triggerKey;
        private bool _wasInputPressed;

        public string Description { get; }
        public string MethodName => _method.DisplayName;
        public QuickTestMethod Method => _method;
        public KeyCode TriggerKey => _triggerKey;
        public bool HasModifiers => _modifiers != EventModifiers.None;
        public string InputSignature => $"{(int)_modifiers}:{_triggerKey}";

        private QuickTestHotkeyBinding(
            QuickTestMethod method,
            IQuickTestInputSource inputSource,
            EventModifiers modifiers,
            KeyCode triggerKey,
            string description)
        {
            _method = method;
            _inputSource = inputSource;
            _modifiers = modifiers;
            _triggerKey = triggerKey;
            Description = description;
        }

        public static bool TryCreate(
            QuickTestMethod method,
            QuickTestHotkeyAttribute attribute,
            out QuickTestHotkeyBinding binding)
        {
            return TryCreate(method, attribute, UnityQuickTestInputSource.Instance, out binding);
        }

        internal static bool TryCreate(
            QuickTestMethod method,
            QuickTestHotkeyAttribute attribute,
            IQuickTestInputSource inputSource,
            out QuickTestHotkeyBinding binding)
        {
            binding = null;

            KeyCode[] keys = attribute.Keys
                .Where(key => key != KeyCode.None)
                .Distinct()
                .ToArray();

            if (keys.Length == 0)
            {
                Debug.LogWarning($"[UnityQuickTests] {method.DisplayName} hotkey is ignored: no keys were provided.");
                return false;
            }

            EventModifiers modifiers = EventModifiers.None;
            var triggerKeys = new List<KeyCode>();

            foreach (KeyCode key in keys)
            {
                if (TryGetModifier(key, out EventModifiers modifier))
                {
                    modifiers |= modifier;
                    continue;
                }

                triggerKeys.Add(key);
            }

            if (triggerKeys.Count != 1)
            {
                Debug.LogWarning(
                    $"[UnityQuickTests] {method.DisplayName} hotkey is ignored: use modifiers plus one trigger key."
                );

                return false;
            }

            string description = BuildDescription(modifiers, triggerKeys[0]);
            binding = new QuickTestHotkeyBinding(method, inputSource, modifiers, triggerKeys[0], description);
            return true;
        }

        public bool Matches(Event currentEvent)
        {
            if (currentEvent == null || currentEvent.type != EventType.KeyDown)
                return false;

            if (currentEvent.keyCode != _triggerKey)
                return false;

            EventModifiers currentModifiers = currentEvent.modifiers & SupportedModifierMask;
            return currentModifiers == _modifiers;
        }

        public bool ConsumeCurrentInputPress()
        {
            bool isInputPressed = _inputSource.GetKey(_triggerKey) && GetCurrentInputModifiers() == _modifiers;
            bool wasPressedThisTick = isInputPressed && !_wasInputPressed;

            _wasInputPressed = isInputPressed;

            return wasPressedThisTick;
        }

        public void ResetInputState()
        {
            _wasInputPressed = false;
        }

        public void Invoke()
        {
            _method.Invoke();
        }

        private EventModifiers GetCurrentInputModifiers()
        {
            EventModifiers modifiers = EventModifiers.None;

            if (_inputSource.GetKey(KeyCode.LeftControl) || _inputSource.GetKey(KeyCode.RightControl))
            {
                modifiers |= EventModifiers.Control;
            }

            if (_inputSource.GetKey(KeyCode.LeftShift) || _inputSource.GetKey(KeyCode.RightShift))
            {
                modifiers |= EventModifiers.Shift;
            }

            if (_inputSource.GetKey(KeyCode.LeftAlt) || _inputSource.GetKey(KeyCode.RightAlt))
            {
                modifiers |= EventModifiers.Alt;
            }

            if (_inputSource.GetKey(KeyCode.LeftCommand) || _inputSource.GetKey(KeyCode.RightCommand))
            {
                modifiers |= EventModifiers.Command;
            }

            return modifiers;
        }

        private static bool TryGetModifier(KeyCode key, out EventModifiers modifier)
        {
            switch (key)
            {
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    modifier = EventModifiers.Control;
                    return true;

                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    modifier = EventModifiers.Shift;
                    return true;

                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                    modifier = EventModifiers.Alt;
                    return true;

                case KeyCode.LeftCommand:
                case KeyCode.RightCommand:
                    modifier = EventModifiers.Command;
                    return true;

                default:
                    modifier = EventModifiers.None;
                    return false;
            }
        }

        private static string BuildDescription(EventModifiers modifiers, KeyCode triggerKey)
        {
            var parts = new List<string>();

            AddModifier(parts, modifiers, EventModifiers.Control, "Ctrl");
            AddModifier(parts, modifiers, EventModifiers.Shift, "Shift");
            AddModifier(parts, modifiers, EventModifiers.Alt, "Alt");
            AddModifier(parts, modifiers, EventModifiers.Command, "Cmd");
            parts.Add(triggerKey.ToString());

            return string.Join("+", parts);
        }

        private static void AddModifier(
            List<string> parts,
            EventModifiers modifiers,
            EventModifiers expectedModifier,
            string label)
        {
            if ((modifiers & expectedModifier) == expectedModifier)
            {
                parts.Add(label);
            }
        }
    }
}
