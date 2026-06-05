using UnityEditor;
using UnityEngine;

namespace UnityQuickTests.Editor
{
    internal static class QuickTestWarningSettings
    {
        private const string WarningsEnabledKey = "UnityQuickTests.WarningsEnabled";

        internal static bool WarningsEnabled
        {
            get => EditorPrefs.GetBool(WarningsEnabledKey, true);
            set => EditorPrefs.SetBool(WarningsEnabledKey, value);
        }

        internal static void LogWarning(string message)
        {
            if (WarningsEnabled)
            {
                Debug.LogWarning(message);
            }
        }
    }

    internal sealed class QuickTestWarningSettingsWindow : EditorWindow
    {
        [MenuItem("Tools/Unity Quick Tests/Warning Settings")]
        public static void Open()
        {
            GetWindow<QuickTestWarningSettingsWindow>("Unity Quick Tests");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Warnings", EditorStyles.boldLabel);

            bool warningsEnabled = EditorGUILayout.ToggleLeft(
                "Enable Unity Quick Tests warnings",
                QuickTestWarningSettings.WarningsEnabled
            );

            if (warningsEnabled != QuickTestWarningSettings.WarningsEnabled)
            {
                QuickTestWarningSettings.WarningsEnabled = warningsEnabled;
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload"))
                {
                    QuickEditorTestRunner.Reload();
                }

                if (GUILayout.Button("List Registered Tests"))
                {
                    QuickEditorTestRunner.ListRegisteredTests();
                }
            }
        }
    }
}
