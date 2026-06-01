using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityQuickTests.Editor.Tests
{
    public sealed class QuickTestHotkeyBindingTests
    {
        private sealed class FakeInputSource : IQuickTestInputSource
        {
            private readonly HashSet<KeyCode> _pressedKeys = new HashSet<KeyCode>();

            public bool GetKey(KeyCode key)
            {
                return _pressedKeys.Contains(key);
            }

            public void SetPressed(params KeyCode[] keys)
            {
                _pressedKeys.Clear();

                foreach (KeyCode key in keys)
                {
                    _pressedKeys.Add(key);
                }
            }
        }

        [Test]
        public void Matches_RequiresExpectedTriggerAndModifiers()
        {
            QuickTestHotkeyBinding binding = CreateBinding(
                new QuickTestHotkeyAttribute(KeyCode.LeftControl, KeyCode.T),
                new FakeInputSource()
            );

            Assert.That(binding.Matches(new Event
            {
                type = EventType.KeyDown,
                keyCode = KeyCode.T,
                modifiers = EventModifiers.Control
            }), Is.True);

            Assert.That(binding.Matches(new Event
            {
                type = EventType.KeyDown,
                keyCode = KeyCode.T
            }), Is.False);
        }

        [Test]
        public void ConsumeCurrentInputPress_ReturnsTrueOnlyOnRisingEdge()
        {
            var input = new FakeInputSource();
            QuickTestHotkeyBinding binding = CreateBinding(
                new QuickTestHotkeyAttribute(KeyCode.LeftControl, KeyCode.T),
                input
            );

            Assert.That(binding.ConsumeCurrentInputPress(), Is.False);

            input.SetPressed(KeyCode.LeftControl, KeyCode.T);
            Assert.That(binding.ConsumeCurrentInputPress(), Is.True);
            Assert.That(binding.ConsumeCurrentInputPress(), Is.False);

            input.SetPressed();
            Assert.That(binding.ConsumeCurrentInputPress(), Is.False);

            input.SetPressed(KeyCode.LeftControl, KeyCode.T);
            Assert.That(binding.ConsumeCurrentInputPress(), Is.True);
        }

        [Test]
        public void TryCreate_RejectsMoreThanOneTriggerKey()
        {
            var method = CreateMethod();
            LogAssert.Expect(
                LogType.Warning,
                $"[UnityQuickTests] {method.DisplayName} hotkey is ignored: use modifiers plus one trigger key."
            );

            bool wasCreated = QuickTestHotkeyBinding.TryCreate(
                method,
                new QuickTestHotkeyAttribute(KeyCode.T, KeyCode.Y),
                new FakeInputSource(),
                out _
            );

            Assert.That(wasCreated, Is.False);
        }

        private static QuickTestHotkeyBinding CreateBinding(
            QuickTestHotkeyAttribute attribute,
            IQuickTestInputSource inputSource)
        {
            Assert.That(
                QuickTestHotkeyBinding.TryCreate(CreateMethod(), attribute, inputSource, out QuickTestHotkeyBinding binding),
                Is.True
            );

            return binding;
        }

        private static QuickTestMethod CreateMethod()
        {
            MethodInfo method = typeof(QuickTestHotkeyBindingTests)
                .GetMethod(nameof(NoOp), BindingFlags.Static | BindingFlags.NonPublic);

            return new QuickTestMethod(method);
        }

        private static void NoOp()
        {
        }
    }
}
