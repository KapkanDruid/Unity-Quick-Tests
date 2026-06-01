using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityQuickTests.Editor.Tests
{
    public static class QuickTestPlayerBuildSmoke
    {
        private const string TemporaryAssetDirectory = "Assets/UnityQuickTestsPlayerBuildSmoke";
        private const string ScenePath = TemporaryAssetDirectory + "/PlayerBuildSmoke.unity";
        private const string BuildDirectory = "artifacts/PlayerBuildSmoke";
        private const string BuildFileName = "UnityQuickTestsPlayerBuildSmoke.exe";

        public static void Run()
        {
            try
            {
                RunInternal();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                    return;
                }

                throw;
            }
        }

        private static void RunInternal()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("StandaloneWindows64 build support is not installed for this Unity editor.");

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("Unable to resolve Unity project root.");

            string outputDirectory = Path.Combine(projectRoot, BuildDirectory);
            string outputPath = Path.Combine(outputDirectory, BuildFileName);

            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, true);
            }

            Directory.CreateDirectory(outputDirectory);

            try
            {
                CreateSmokeScene(projectRoot);

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);

                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"Player build failed with result {report.summary.result}.");

                string[] leakedEditorAssemblies = Directory
                    .EnumerateFiles(outputDirectory, "UnityQuickTests.Editor*.dll", SearchOption.AllDirectories)
                    .ToArray();

                if (leakedEditorAssemblies.Length > 0)
                {
                    throw new InvalidOperationException(
                        "Editor assembly leaked into player build: " +
                        string.Join(", ", leakedEditorAssemblies.Select(Path.GetFileName))
                    );
                }

                Debug.Log($"[UnityQuickTests] Player build smoke succeeded: {outputPath}");
            }
            finally
            {
                AssetDatabase.DeleteAsset(TemporaryAssetDirectory);
                AssetDatabase.Refresh();
            }
        }

        private static void CreateSmokeScene(string projectRoot)
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, "Assets"));
            AssetDatabase.Refresh();

            if (!AssetDatabase.IsValidFolder(TemporaryAssetDirectory))
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(TemporaryAssetDirectory));
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Unity Quick Tests Player Build Smoke");

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"Unable to save smoke scene at {ScenePath}.");
        }
    }
}
