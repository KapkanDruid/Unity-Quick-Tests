using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityQuickTests.Editor.Tests
{
    public sealed class QuickTestDiscoveryTests
    {
        [Test]
        public void FindRegistrations_ReturnsSupportedStaticMethods()
        {
            var registrations = QuickTestDiscovery.FindRegistrations(new[] { typeof(ValidFixtures) });

            Assert.That(registrations.Select(registration => registration.Method.Method.Name), Is.EquivalentTo(new[]
            {
                nameof(ValidFixtures.Hotkey),
                "Schedule"
            }));

            QuickTestRegistration hotkeyRegistration = registrations.Single(
                registration => registration.Method.Method.Name == nameof(ValidFixtures.Hotkey)
            );
            QuickTestRegistration scheduleRegistration = registrations.Single(
                registration => registration.Method.Method.Name == "Schedule"
            );

            Assert.That(hotkeyRegistration.HotkeyAttributes.Count, Is.EqualTo(1));
            Assert.That(hotkeyRegistration.ScheduleAttributes, Is.Empty);
            Assert.That(scheduleRegistration.HotkeyAttributes, Is.Empty);
            Assert.That(scheduleRegistration.ScheduleAttributes.Count, Is.EqualTo(1));
        }

        [Test]
        public void FindRegistrations_ReturnsSupportedUnityObjectInstanceMethods()
        {
            var registrations = QuickTestDiscovery.FindRegistrations(new[]
            {
                typeof(ValidMonoBehaviourFixture),
                typeof(ValidScriptableObjectFixture),
                typeof(ValidEditorWindowFixture)
            });

            Assert.That(registrations.Select(registration => registration.Method.Method.Name), Is.EquivalentTo(new[]
            {
                nameof(ValidMonoBehaviourFixture.MonoHotkey),
                "ScriptableHotkey",
                nameof(ValidEditorWindowFixture.WindowHotkey)
            }));

            Assert.That(registrations.All(registration => !registration.Method.Method.IsStatic), Is.True);
        }

        [Test]
        public void FindRegistrations_RejectsUnsupportedStaticMethods()
        {
            ExpectIgnoredWarning(typeof(InvalidFixtures), nameof(InvalidFixtures.WithParameter), "methods must be parameterless");
            ExpectIgnoredWarning(typeof(InvalidFixtures), nameof(InvalidFixtures.WithReturnValue), "only void methods are supported");
            ExpectIgnoredWarning(typeof(InvalidFixtures), nameof(InvalidFixtures.Generic), "generic methods are not supported");

            var registrations = QuickTestDiscovery.FindRegistrations(new[] { typeof(InvalidFixtures) });

            Assert.That(registrations, Is.Empty);
        }

        [Test]
        public void FindRegistrations_RejectsUnsupportedInstanceTargets()
        {
            ExpectIgnoredWarning(
                typeof(PlainCSharpFixture),
                nameof(PlainCSharpFixture.PlainHotkey),
                "plain C# instance methods require the instance registry planned for the next phase"
            );
            ExpectIgnoredWarning(
                typeof(InvalidEditorFixture),
                nameof(InvalidEditorFixture.EditorHotkey),
                "UnityEditor.Editor targets are not supported until their lifecycle is validated"
            );

            var registrations = QuickTestDiscovery.FindRegistrations(new[]
            {
                typeof(PlainCSharpFixture),
                typeof(InvalidEditorFixture)
            });

            Assert.That(registrations, Is.Empty);
        }

        private static void ExpectIgnoredWarning(Type declaringType, string methodName, string reason)
        {
            LogAssert.Expect(
                LogType.Warning,
                $"[UnityQuickTests] {declaringType.FullName}.{methodName} is ignored: {reason}."
            );
        }

        private static class ValidFixtures
        {
            [QuickTestHotkey(KeyCode.T)]
            public static void Hotkey()
            {
            }

            [QuickTestSchedule(1, QuickTestScheduleUnit.Frames)]
            private static void Schedule()
            {
            }
        }

        private static class InvalidFixtures
        {
            [QuickTestHotkey(KeyCode.T)]
            public static void WithParameter(int value)
            {
            }

            [QuickTestHotkey(KeyCode.T)]
            public static int WithReturnValue()
            {
                return 0;
            }

            [QuickTestHotkey(KeyCode.T)]
            public static void Generic<T>()
            {
            }
        }

        private sealed class ValidMonoBehaviourFixture : MonoBehaviour
        {
            [QuickTestHotkey(KeyCode.T)]
            public void MonoHotkey()
            {
            }
        }

        private sealed class ValidScriptableObjectFixture : ScriptableObject
        {
            [QuickTestHotkey(KeyCode.T)]
            private void ScriptableHotkey()
            {
            }
        }

        private sealed class ValidEditorWindowFixture : UnityEditor.EditorWindow
        {
            [QuickTestHotkey(KeyCode.T)]
            public void WindowHotkey()
            {
            }
        }

        private sealed class PlainCSharpFixture
        {
            [QuickTestHotkey(KeyCode.T)]
            public void PlainHotkey()
            {
            }
        }

        private sealed class InvalidEditorFixture : UnityEditor.Editor
        {
            [QuickTestHotkey(KeyCode.T)]
            public void EditorHotkey()
            {
            }
        }
    }
}
