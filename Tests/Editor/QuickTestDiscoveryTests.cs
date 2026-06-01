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

            Assert.That(hotkeyRegistration.HotkeyAttributes, Has.Count.EqualTo(1));
            Assert.That(hotkeyRegistration.ScheduleAttributes, Is.Empty);
            Assert.That(scheduleRegistration.HotkeyAttributes, Is.Empty);
            Assert.That(scheduleRegistration.ScheduleAttributes, Has.Count.EqualTo(1));
        }

        [Test]
        public void FindRegistrations_RejectsUnsupportedStaticMethods()
        {
            ExpectIgnoredWarning(nameof(InvalidFixtures.WithParameter), "methods must be parameterless");
            ExpectIgnoredWarning(nameof(InvalidFixtures.WithReturnValue), "only void methods are supported");
            ExpectIgnoredWarning(nameof(InvalidFixtures.Generic), "generic methods are not supported");

            var registrations = QuickTestDiscovery.FindRegistrations(new[] { typeof(InvalidFixtures) });

            Assert.That(registrations, Is.Empty);
        }

        private static void ExpectIgnoredWarning(string methodName, string reason)
        {
            LogAssert.Expect(
                LogType.Warning,
                $"[UnityQuickTests] {typeof(InvalidFixtures).FullName}.{methodName} is ignored: {reason}."
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
    }
}
