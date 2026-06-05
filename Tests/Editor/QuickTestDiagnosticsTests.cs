using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace UnityQuickTests.Editor.Tests
{
    public sealed class QuickTestDiagnosticsTests
    {
        private const BindingFlags StaticMethodFlags = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags InstanceMethodFlags = BindingFlags.Instance | BindingFlags.Public;

        [Test]
        public void FindHotkeyWarnings_DetectsDuplicateHotkeys()
        {
            QuickTestHotkeyBinding[] bindings =
            {
                CreateHotkeyBinding(nameof(First), new QuickTestHotkeyAttribute(KeyCode.LeftControl, KeyCode.T)),
                CreateHotkeyBinding(nameof(Second), new QuickTestHotkeyAttribute(KeyCode.LeftControl, KeyCode.T))
            };

            string[] warnings = QuickTestDiagnostics.FindHotkeyWarnings(bindings).ToArray();

            Assert.That(warnings, Has.Some.Contains("Hotkey Ctrl+T is registered for multiple tests"));
            Assert.That(warnings.Single(warning => warning.Contains("multiple tests")), Does.Contain(nameof(First)));
            Assert.That(warnings.Single(warning => warning.Contains("multiple tests")), Does.Contain(nameof(Second)));
        }

        [Test]
        public void FindHotkeyWarnings_RecommendsModifiersForSceneViewShortcuts()
        {
            QuickTestHotkeyBinding[] bindings =
            {
                CreateHotkeyBinding(nameof(First), new QuickTestHotkeyAttribute(KeyCode.W))
            };

            string[] warnings = QuickTestDiagnostics.FindHotkeyWarnings(bindings).ToArray();

            Assert.That(warnings.Single(), Does.Contain("may conflict with Unity Scene View shortcuts"));
            Assert.That(warnings.Single(), Does.Contain("Prefer Ctrl/Shift/Alt/Cmd plus one trigger key"));
        }

        [Test]
        public void BuildRegisteredTestsReport_IncludesTriggerTargetScopeAndStatus()
        {
            QuickTestHotkeyBinding hotkey = CreateHotkeyBinding(
                nameof(First),
                new QuickTestHotkeyAttribute(KeyCode.LeftControl, KeyCode.T)
            );
            var schedule = new QuickTestScheduleBinding(
                CreatePlainInstanceMethod(),
                new QuickTestScheduleAttribute(3, QuickTestScheduleUnit.Frames),
                currentFrame: 0,
                currentTime: 0d
            );

            string report = QuickTestDiagnostics.BuildRegisteredTestsReport(
                new[] { hotkey },
                new[] { schedule },
                Array.Empty<string>()
            );

            Assert.That(report, Does.Contain("Hotkey: Ctrl+T"));
            Assert.That(report, Does.Contain("Schedule: Once after 3 frame(s)"));
            Assert.That(report, Does.Contain($"method signature: {typeof(QuickTestDiagnosticsTests).FullName}.First()"));
            Assert.That(report, Does.Contain($"method signature: {typeof(PlainFixture).FullName}.Run()"));
            Assert.That(report, Does.Contain($"declaring type: {typeof(QuickTestDiagnosticsTests).FullName}"));
            Assert.That(report, Does.Contain("target scope: static method"));
            Assert.That(report, Does.Contain("status: supported: direct invocation"));
            Assert.That(report, Does.Contain("target scope: weak-registered plain C# instances"));
            Assert.That(report, Does.Contain("status: supported: ILPP auto-registration or manual QuickTestInstanceRegistry.Register"));
        }

        private static QuickTestHotkeyBinding CreateHotkeyBinding(
            string methodName,
            QuickTestHotkeyAttribute attribute)
        {
            Assert.That(
                QuickTestHotkeyBinding.TryCreate(
                    CreateStaticMethod(methodName),
                    attribute,
                    UnityQuickTestInputSource.Instance,
                    out QuickTestHotkeyBinding binding),
                Is.True
            );

            return binding;
        }

        private static QuickTestMethod CreateStaticMethod(string methodName)
        {
            MethodInfo method = typeof(QuickTestDiagnosticsTests).GetMethod(methodName, StaticMethodFlags);
            return new QuickTestMethod(method);
        }

        private static QuickTestMethod CreatePlainInstanceMethod()
        {
            MethodInfo method = typeof(PlainFixture).GetMethod(nameof(PlainFixture.Run), InstanceMethodFlags);
            return new QuickTestMethod(method);
        }

        private static void First()
        {
        }

        private static void Second()
        {
        }

        private sealed class PlainFixture
        {
            public void Run()
            {
            }
        }
    }
}
