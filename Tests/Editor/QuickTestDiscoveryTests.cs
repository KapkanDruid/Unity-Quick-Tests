using System;
using System.Linq;
using System.Threading.Tasks;
using QuickTestCodegen.Consumer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityQuickTests.Editor.Tests
{
    public sealed class QuickTestDiscoveryTests
    {
        [SetUp]
        public void SetUp()
        {
            QuickTestWarningSettings.WarningsEnabled = true;
        }

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
            Assert.That(
                registrations.Single(registration => registration.Method.Method.Name == "ScriptableHotkey").Method.SupportStatusDescription,
                Does.Contain("AssetDatabase asset loading is intentionally disabled")
            );
        }

        [Test]
        public void FindRegistrations_ReturnsSupportedPlainCSharpInstanceMethods()
        {
            var registrations = QuickTestDiscovery.FindRegistrations(new[] { typeof(PlainCSharpFixture) });

            QuickTestRegistration registration = registrations.Single();

            Assert.That(registration.Method.Method.Name, Is.EqualTo(nameof(PlainCSharpFixture.PlainHotkey)));
            Assert.That(registration.Method.TargetDescription, Is.EqualTo("registered instance"));
            Assert.That(registration.HotkeyAttributes.Count, Is.EqualTo(1));
            Assert.That(registration.ScheduleAttributes, Is.Empty);
        }

        [Test]
        public void ShouldSearchAssemblyForRegistrations_SkipsPackageTestAssemblies()
        {
            Assert.That(QuickTestDiscovery.ShouldSearchAssemblyForRegistrations(typeof(QuickTestDiscoveryTests).Assembly), Is.False);
            Assert.That(QuickTestDiscovery.ShouldSearchAssemblyForRegistrations(typeof(AutoRegisteredPlainTarget).Assembly), Is.False);
            Assert.That(QuickTestDiscovery.ShouldSearchAssemblyForRegistrations(typeof(QuickTestHotkeyAttribute).Assembly), Is.True);
        }

        [Test]
        public void FindRegistrations_RejectsUnsupportedStaticMethods()
        {
            ExpectIgnoredWarning(typeof(InvalidFixtures), nameof(InvalidFixtures.WithParameter), "methods must be parameterless");
            ExpectIgnoredWarning(typeof(InvalidFixtures), nameof(InvalidFixtures.WithReturnValue), "only void methods are supported");
            ExpectIgnoredWarning(typeof(InvalidFixtures), nameof(InvalidFixtures.Generic), "generic methods and generic target types are not supported");

            var registrations = QuickTestDiscovery.FindRegistrations(new[] { typeof(InvalidFixtures) });

            Assert.That(registrations, Is.Empty);
        }

        [Test]
        public void FindRegistrations_RejectsGenericTargetTypes()
        {
            ExpectIgnoredWarning(
                typeof(InvalidGenericTypeFixture<>),
                nameof(InvalidGenericTypeFixture<object>.GenericTypeHotkey),
                "generic methods and generic target types are not supported"
            );

            var registrations = QuickTestDiscovery.FindRegistrations(new[] { typeof(InvalidGenericTypeFixture<>) });

            Assert.That(registrations, Is.Empty);
        }

        [Test]
        public void FindRegistrations_RejectsAsyncAndTaskLikeMethods()
        {
            ExpectIgnoredWarning(typeof(InvalidAsyncFixtures), nameof(InvalidAsyncFixtures.AsyncVoid), "async methods are not supported; use a parameterless void wrapper method");
            ExpectIgnoredWarning(typeof(InvalidAsyncFixtures), nameof(InvalidAsyncFixtures.AsyncTask), "async methods are not supported; use a parameterless void wrapper method");
            ExpectIgnoredWarning(typeof(InvalidAsyncFixtures), nameof(InvalidAsyncFixtures.ReturnsTask), "Task, ValueTask and UniTask return types are not supported");
            ExpectIgnoredWarning(typeof(InvalidAsyncFixtures), nameof(InvalidAsyncFixtures.ReturnsFakeUniTask), "Task, ValueTask and UniTask return types are not supported");

            var registrations = QuickTestDiscovery.FindRegistrations(new[] { typeof(InvalidAsyncFixtures) });

            Assert.That(registrations, Is.Empty);
        }

        [Test]
        public void FindRegistrations_DoesNotInheritAttributedMethods()
        {
            var registrations = QuickTestDiscovery.FindRegistrations(new[] { typeof(DerivedInheritedFixture) });

            Assert.That(registrations, Is.Empty);
        }

        [Test]
        public void FindRegistrations_RejectsUnsupportedInstanceTargets()
        {
            ExpectIgnoredWarning(
                typeof(InvalidEditorFixture),
                nameof(InvalidEditorFixture.EditorHotkey),
                "UnityEditor.Editor targets are not supported until their lifecycle is validated"
            );
            ExpectIgnoredWarning(
                typeof(InvalidValueTypeFixture),
                nameof(InvalidValueTypeFixture.ValueHotkey),
                "value type target types are not supported"
            );

            var registrations = QuickTestDiscovery.FindRegistrations(new[]
            {
                typeof(InvalidEditorFixture),
                typeof(InvalidValueTypeFixture)
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

        private sealed class InvalidGenericTypeFixture<T>
        {
            [QuickTestHotkey(KeyCode.T)]
            public void GenericTypeHotkey()
            {
            }
        }

        private static class InvalidAsyncFixtures
        {
            [QuickTestHotkey(KeyCode.T)]
            public static async void AsyncVoid()
            {
                await Task.Yield();
            }

            [QuickTestHotkey(KeyCode.T)]
            public static async Task AsyncTask()
            {
                await Task.Yield();
            }

            [QuickTestHotkey(KeyCode.T)]
            public static Task ReturnsTask()
            {
                return Task.CompletedTask;
            }

            [QuickTestHotkey(KeyCode.T)]
            public static UniTask ReturnsFakeUniTask()
            {
                return default(UniTask);
            }
        }

        private readonly struct UniTask
        {
        }

        private class BaseInheritedFixture
        {
            [QuickTestHotkey(KeyCode.T)]
            public void BaseHotkey()
            {
            }
        }

        private sealed class DerivedInheritedFixture : BaseInheritedFixture
        {
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

        private struct InvalidValueTypeFixture
        {
            [QuickTestHotkey(KeyCode.T)]
            public void ValueHotkey()
            {
            }
        }
    }
}
