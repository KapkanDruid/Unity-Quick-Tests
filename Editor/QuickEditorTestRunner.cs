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
        private static readonly List<string> _diagnosticWarnings = new List<string>();

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
            Debug.Log(QuickTestDiagnostics.BuildRegisteredTestsReport(
                _hotkeyBindings,
                _scheduleBindings,
                _diagnosticWarnings
            ));
        }

        internal static int HotkeyBindingCountForTests => _hotkeyBindings.Count;
        internal static int ScheduleBindingCountForTests => _scheduleBindings.Count;

        internal static void SetRegistrationsForTests(
            IEnumerable<QuickTestRegistration> registrations,
            IQuickTestInputSource inputSource,
            int currentFrame,
            double currentTime)
        {
            _hotkeyBindings.Clear();
            _scheduleBindings.Clear();
            _inputPoller = null;
            _editorFrame = currentFrame;

            IQuickTestInputSource resolvedInputSource = inputSource ?? UnityQuickTestInputSource.Instance;

            foreach (QuickTestRegistration registration in registrations)
            {
                RegisterHotkeys(registration, resolvedInputSource);
                RegisterSchedules(registration, currentTime);
            }

            RefreshDiagnostics();
        }

        internal static void TickEditorUpdateForTests(double currentTime)
        {
            OnEditorUpdate(currentTime, false);
        }

        internal static void HandleRuntimeUpdateForTests(bool isPlaying)
        {
            HandlePlayModeHotkeys(isPlaying);
        }

        internal static void HandlePlayModeStateChangedForTests(PlayModeStateChange stateChange)
        {
            OnPlayModeStateChanged(stateChange);
        }

        internal static void HandleSceneGuiEventForTests(Event currentEvent)
        {
            HandleSceneGuiEvent(currentEvent);
        }

        internal static void ReloadForTests()
        {
            ReloadInternal(false);
        }

        private static void ReloadInternal(bool shouldLogSummary)
        {
            _hotkeyBindings.Clear();
            _scheduleBindings.Clear();

            double currentTime = EditorApplication.timeSinceStartup;
            IReadOnlyList<QuickTestRegistration> registrations = QuickTestDiscovery.FindRegistrations();

            foreach (QuickTestRegistration registration in registrations)
            {
                RegisterHotkeys(registration, UnityQuickTestInputSource.Instance);
                RegisterSchedules(registration, currentTime);
            }

            RefreshDiagnostics();

            if (shouldLogSummary)
            {
                Debug.Log(
                    $"[UnityQuickTests] Registered {_hotkeyBindings.Count} hotkey(s) and " +
                    $"{_scheduleBindings.Count} schedule(s)."
                );

                LogDiagnosticWarnings();
            }
        }

        private static void RegisterHotkeys(QuickTestRegistration registration, IQuickTestInputSource inputSource)
        {
            foreach (QuickTestHotkeyAttribute attribute in registration.HotkeyAttributes)
            {
                if (QuickTestHotkeyBinding.TryCreate(
                    registration.Method,
                    attribute,
                    inputSource,
                    out QuickTestHotkeyBinding binding))
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

        private static void RefreshDiagnostics()
        {
            _diagnosticWarnings.Clear();
            _diagnosticWarnings.AddRange(QuickTestDiagnostics.FindHotkeyWarnings(_hotkeyBindings));
        }

        private static void LogDiagnosticWarnings()
        {
            foreach (string warning in _diagnosticWarnings)
            {
                Debug.LogWarning(warning);
            }
        }

        private static void OnEditorUpdate()
        {
            OnEditorUpdate(EditorApplication.timeSinceStartup, true);
        }

        private static void OnEditorUpdate(double currentTime, bool shouldEnsurePlayModeInputPoller)
        {
            _editorFrame++;

            if (shouldEnsurePlayModeInputPoller)
            {
                EnsurePlayModeInputPoller();
            }

            if (_scheduleBindings.Count == 0)
                return;

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
            HandlePlayModeHotkeys(EditorApplication.isPlaying);
        }

        private static void HandlePlayModeHotkeys(bool isPlaying)
        {
            if (!isPlaying || _hotkeyBindings.Count == 0)
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
            if (Application.isBatchMode || !EditorApplication.isPlaying)
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
            if (stateChange == PlayModeStateChange.ExitingEditMode ||
                stateChange == PlayModeStateChange.ExitingPlayMode)
            {
                QuickTestInstanceRegistry.Clear();
            }

            if (stateChange == PlayModeStateChange.ExitingPlayMode)
            {
                ResetHotkeyInputState();
                _inputPoller = null;
            }
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            HandleSceneGuiEvent(Event.current);
        }

        private static void HandleSceneGuiEvent(Event currentEvent)
        {
            if (_hotkeyBindings.Count == 0)
                return;

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
