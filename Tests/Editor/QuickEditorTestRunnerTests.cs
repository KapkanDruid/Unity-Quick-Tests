using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityQuickTests.Editor.Tests
{
    public sealed class QuickEditorTestRunnerTests
    {
        private const BindingFlags MethodFlags = BindingFlags.Static | BindingFlags.NonPublic;

        private static int _invocationCount;

        [SetUp]
        public void SetUp()
        {
            _invocationCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            QuickEditorTestRunner.ReloadForTests();
        }

        [Test]
        public void SceneViewHotkey_InvokesStaticMethodAndConsumesEvent()
        {
            QuickEditorTestRunner.SetRegistrationsForTests(
                new[]
                {
                    CreateRegistration(
                        nameof(Increment),
                        hotkeyAttributes: new[] { new QuickTestHotkeyAttribute(KeyCode.LeftControl, KeyCode.T) })
                },
                inputSource: null,
                currentFrame: 0,
                currentTime: 0d
            );

            var keyDown = new Event
            {
                type = EventType.KeyDown,
                keyCode = KeyCode.T,
                modifiers = EventModifiers.Control
            };

            QuickEditorTestRunner.HandleSceneGuiEventForTests(keyDown);

            Assert.That(_invocationCount, Is.EqualTo(1));
            Assert.That(keyDown.type, Is.EqualTo(EventType.Used));
        }

        [Test]
        public void EditorUpdate_OnceFrameSchedule_InvokesOnceAndCompletes()
        {
            QuickEditorTestRunner.SetRegistrationsForTests(
                new[]
                {
                    CreateRegistration(
                        nameof(Increment),
                        scheduleAttributes: new[]
                        {
                            new QuickTestScheduleAttribute(2, QuickTestScheduleUnit.Frames)
                        })
                },
                inputSource: null,
                currentFrame: 0,
                currentTime: 0d
            );

            QuickEditorTestRunner.TickEditorUpdateForTests(0d);
            Assert.That(_invocationCount, Is.Zero);

            QuickEditorTestRunner.TickEditorUpdateForTests(0d);
            QuickEditorTestRunner.TickEditorUpdateForTests(0d);

            Assert.That(_invocationCount, Is.EqualTo(1));
            Assert.That(QuickEditorTestRunner.ScheduleBindingCountForTests, Is.Zero);
        }

        [Test]
        public void EditorUpdate_RepeatingSecondsSchedule_ReschedulesAfterEachInvocation()
        {
            QuickEditorTestRunner.SetRegistrationsForTests(
                new[]
                {
                    CreateRegistration(
                        nameof(Increment),
                        scheduleAttributes: new[]
                        {
                            new QuickTestScheduleAttribute(
                                0.25,
                                QuickTestScheduleUnit.Seconds,
                                QuickTestRepeatMode.Repeat)
                        })
                },
                inputSource: null,
                currentFrame: 0,
                currentTime: 10d
            );

            QuickEditorTestRunner.TickEditorUpdateForTests(10.24d);
            QuickEditorTestRunner.TickEditorUpdateForTests(10.25d);
            QuickEditorTestRunner.TickEditorUpdateForTests(10.49d);
            QuickEditorTestRunner.TickEditorUpdateForTests(10.5d);

            Assert.That(_invocationCount, Is.EqualTo(2));
            Assert.That(QuickEditorTestRunner.ScheduleBindingCountForTests, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedPlayModeExits_ResetRuntimeHotkeyInputState()
        {
            var input = new FakeInputSource();
            QuickEditorTestRunner.SetRegistrationsForTests(
                new[]
                {
                    CreateRegistration(
                        nameof(Increment),
                        hotkeyAttributes: new[] { new QuickTestHotkeyAttribute(KeyCode.T) })
                },
                input,
                currentFrame: 0,
                currentTime: 0d
            );

            input.SetPressed(KeyCode.T);

            QuickEditorTestRunner.HandleRuntimeUpdateForTests(isPlaying: true);
            QuickEditorTestRunner.HandleRuntimeUpdateForTests(isPlaying: true);
            QuickEditorTestRunner.HandlePlayModeStateChangedForTests(PlayModeStateChange.ExitingPlayMode);
            QuickEditorTestRunner.HandleRuntimeUpdateForTests(isPlaying: true);
            QuickEditorTestRunner.HandlePlayModeStateChangedForTests(PlayModeStateChange.ExitingPlayMode);
            QuickEditorTestRunner.HandleRuntimeUpdateForTests(isPlaying: true);

            Assert.That(_invocationCount, Is.EqualTo(3));
        }

        private static QuickTestRegistration CreateRegistration(
            string methodName,
            IReadOnlyList<QuickTestHotkeyAttribute> hotkeyAttributes = null,
            IReadOnlyList<QuickTestScheduleAttribute> scheduleAttributes = null)
        {
            MethodInfo method = typeof(QuickEditorTestRunnerTests).GetMethod(methodName, MethodFlags);

            return new QuickTestRegistration(
                new QuickTestMethod(method),
                hotkeyAttributes ?? Array.Empty<QuickTestHotkeyAttribute>(),
                scheduleAttributes ?? Array.Empty<QuickTestScheduleAttribute>()
            );
        }

        private static void Increment()
        {
            _invocationCount++;
        }

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
    }
}
