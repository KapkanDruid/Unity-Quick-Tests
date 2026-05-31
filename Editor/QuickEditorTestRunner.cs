using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityQuickTests.Editor
{
    [InitializeOnLoad]
    public static class QuickEditorTestRunner
    {
        private const string PollerObjectName = "[UnityQuickTestsInputPoller]";

        private static readonly List<QuickTestHotkeyBinding> _hotkeyBindings = new List<QuickTestHotkeyBinding>();
        private static readonly List<QuickTestScheduleBinding> _scheduleBindings = new List<QuickTestScheduleBinding>();

        private static QuickTestInputPoller _inputPoller;
        private static int _editorFrame;

        static QuickEditorTestRunner()
        {
            ReloadInternal(false);

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;

            QuickTestInputPoller.Updated -= OnRuntimeUpdate;
            QuickTestInputPoller.Updated += OnRuntimeUpdate;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/Unity Quick Tests/Reload")]
        public static void Reload()
        {
            ReloadInternal(true);
        }

        [MenuItem("Tools/Unity Quick Tests/List Registered Tests")]
        public static void ListRegisteredTests()
        {
            var lines = new List<string>
            {
                "[UnityQuickTests] Registered tests:",
                $"Hotkeys: {_hotkeyBindings.Count}",
                $"Schedules: {_scheduleBindings.Count}"
            };

            lines.AddRange(_hotkeyBindings.Select(binding => $"Hotkey {binding.Description} -> {binding.MethodName}"));
            lines.AddRange(_scheduleBindings.Select(binding => $"Schedule {binding.Description} -> {binding.MethodName}"));

            Debug.Log(string.Join("\n", lines));
        }

        private static void ReloadInternal(bool shouldLogSummary)
        {
            _hotkeyBindings.Clear();
            _scheduleBindings.Clear();

            double currentTime = EditorApplication.timeSinceStartup;
            IReadOnlyList<QuickTestRegistration> registrations = QuickTestDiscovery.FindRegistrations();

            foreach (QuickTestRegistration registration in registrations)
            {
                RegisterHotkeys(registration);
                RegisterSchedules(registration, currentTime);
            }

            if (shouldLogSummary)
            {
                Debug.Log(
                    $"[UnityQuickTests] Registered {_hotkeyBindings.Count} hotkey(s) and " +
                    $"{_scheduleBindings.Count} schedule(s)."
                );
            }
        }

        private static void RegisterHotkeys(QuickTestRegistration registration)
        {
            foreach (QuickTestHotkeyAttribute attribute in registration.HotkeyAttributes)
            {
                if (QuickTestHotkeyBinding.TryCreate(registration.Method, attribute, out QuickTestHotkeyBinding binding))
                {
                    _hotkeyBindings.Add(binding);
                }
            }
        }

        private static void RegisterSchedules(QuickTestRegistration registration, double currentTime)
        {
            foreach (QuickTestScheduleAttribute attribute in registration.ScheduleAttributes)
            {
                _scheduleBindings.Add(new QuickTestScheduleBinding(
                    registration.Method,
                    attribute,
                    _editorFrame,
                    currentTime
                ));
            }
        }

        private static void OnEditorUpdate()
        {
            _editorFrame++;
            EnsurePlayModeInputPoller();

            if (_scheduleBindings.Count == 0)
                return;

            double currentTime = EditorApplication.timeSinceStartup;

            for (int i = _scheduleBindings.Count - 1; i >= 0; i--)
            {
                QuickTestScheduleBinding binding = _scheduleBindings[i];
                binding.Tick(_editorFrame, currentTime);

                if (binding.IsCompleted)
                {
                    _scheduleBindings.RemoveAt(i);
                }
            }
        }

        private static void OnRuntimeUpdate()
        {
            HandlePlayModeHotkeys();
        }

        private static void HandlePlayModeHotkeys()
        {
            if (!EditorApplication.isPlaying || _hotkeyBindings.Count == 0)
            {
                ResetHotkeyInputState();
                return;
            }

            foreach (QuickTestHotkeyBinding binding in _hotkeyBindings)
            {
                if (binding.ConsumeCurrentInputPress())
                {
                    binding.Invoke();
                }
            }
        }

        private static void ResetHotkeyInputState()
        {
            foreach (QuickTestHotkeyBinding binding in _hotkeyBindings)
            {
                binding.ResetInputState();
            }
        }

        private static void EnsurePlayModeInputPoller()
        {
            if (!EditorApplication.isPlaying)
                return;

            if (_inputPoller != null)
                return;

            var pollerObject = new GameObject(PollerObjectName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            Object.DontDestroyOnLoad(pollerObject);
            _inputPoller = pollerObject.AddComponent<QuickTestInputPoller>();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange != PlayModeStateChange.ExitingPlayMode)
                return;

            ResetHotkeyInputState();
            _inputPoller = null;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (_hotkeyBindings.Count == 0)
                return;

            Event currentEvent = Event.current;

            if (currentEvent == null || currentEvent.type != EventType.KeyDown)
                return;

            bool wasHandled = false;

            foreach (QuickTestHotkeyBinding binding in _hotkeyBindings)
            {
                if (!binding.Matches(currentEvent))
                    continue;

                binding.Invoke();
                wasHandled = true;
            }

            if (wasHandled)
            {
                currentEvent.Use();
            }
        }
    }
}
