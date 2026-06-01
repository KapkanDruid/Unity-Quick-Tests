using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityQuickTests.Codegen.Editor;

namespace UnityQuickTests.Editor.Tests
{
    public static class QuickTestPlayerBuildSmoke
    {
        private const string TemporaryAssetDirectory = "Assets/UnityQuickTestsPlayerBuildSmoke";
        private const string ScenePath = TemporaryAssetDirectory + "/PlayerBuildSmoke.unity";
        private const string BuildDirectory = "artifacts/PlayerBuildSmoke";
        private const string BuildFileName = "UnityQuickTestsPlayerBuildSmoke.exe";
        private const string RuntimeAssemblyFileName = "UnityQuickTests.Runtime.dll";

        private static readonly string[] ForbiddenPlayerAssemblyPatterns =
        {
            "UnityQuickTests.Editor*.dll",
            "Unity.UrbanDruids.UnityQuickTests.CodeGen*.dll",
            "UnityQuickTests.Codegen*.dll",
            "Mono.Cecil*.dll",
            "Unity.CompilationPipeline.Common.dll"
        };

        private static readonly string[] ForbiddenManagedTypeNames =
        {
            "UnityQuickTests.QuickTestInputPoller"
        };

        private static readonly string[] RequiredRuntimeMetadataTypeNames =
        {
            "UnityQuickTests.QuickTestHotkeyAttribute",
            "UnityQuickTests.QuickTestScheduleAttribute"
        };

        private static readonly string[] IgnoredManagedAssemblyPrefixes =
        {
            "mscorlib",
            "netstandard",
            "System",
            "Microsoft",
            "Mono.",
            "UnityEngine"
        };

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

                AssertPlayerBuildSafety(outputDirectory);

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

        private static void AssertPlayerBuildSafety(string outputDirectory)
        {
            AssertForbiddenPlayerAssembliesAbsent(outputDirectory);

            string managedDirectory = ResolveManagedDirectory(outputDirectory);
            string[] managedAssemblies = Directory
                .EnumerateFiles(managedDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                .ToArray();
            string[] playerScriptAssemblies = managedAssemblies
                .Where(IsPlayerScriptAssembly)
                .ToArray();

            AssertManagedTypesAbsent(playerScriptAssemblies);
            AssertNoRegistryRegisterCallSites(playerScriptAssemblies);
            AssertRuntimeAttributesRemainMetadata(managedAssemblies);
        }

        private static void AssertForbiddenPlayerAssembliesAbsent(string outputDirectory)
        {
            var leakedAssemblies = new List<string>();

            foreach (string pattern in ForbiddenPlayerAssemblyPatterns)
            {
                leakedAssemblies.AddRange(Directory
                    .EnumerateFiles(outputDirectory, pattern, SearchOption.AllDirectories)
                    .Select(Path.GetFileName)
                );
            }

            if (leakedAssemblies.Count > 0)
            {
                throw new InvalidOperationException(
                    "Editor-only assembly leaked into player build: " +
                    string.Join(", ", leakedAssemblies.Distinct().OrderBy(name => name))
                );
            }
        }

        private static string ResolveManagedDirectory(string outputDirectory)
        {
            string[] candidates = Directory
                .EnumerateDirectories(outputDirectory, "Managed", SearchOption.AllDirectories)
                .Where(directory => File.Exists(Path.Combine(directory, RuntimeAssemblyFileName)))
                .ToArray();

            if (candidates.Length != 1)
                throw new InvalidOperationException($"Unable to resolve player Managed directory for {RuntimeAssemblyFileName}.");

            return candidates[0];
        }

        private static bool IsPlayerScriptAssembly(string assemblyPath)
        {
            string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);

            return !IgnoredManagedAssemblyPrefixes.Any(prefix =>
                assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            );
        }

        private static void AssertManagedTypesAbsent(string[] managedAssemblies)
        {
            var leakedTypes = new List<string>();

            foreach (string assemblyPath in managedAssemblies)
            {
                foreach (string typeName in ForbiddenManagedTypeNames)
                {
                    if (QuickTestPlayerBuildAssemblyInspector.ContainsType(assemblyPath, typeName))
                    {
                        leakedTypes.Add($"{typeName} in {Path.GetFileName(assemblyPath)}");
                    }
                }
            }

            if (leakedTypes.Count > 0)
            {
                throw new InvalidOperationException(
                    "Editor-only type leaked into player build: " +
                    string.Join(", ", leakedTypes)
                );
            }
        }

        private static void AssertNoRegistryRegisterCallSites(string[] managedAssemblies)
        {
            string[] callSites = managedAssemblies
                .SelectMany(QuickTestPlayerBuildAssemblyInspector.FindRegistryRegisterCallSites)
                .ToArray();

            if (callSites.Length > 0)
            {
                throw new InvalidOperationException(
                    "Editor-only registry registration call leaked into player build: " +
                    string.Join(", ", callSites)
                );
            }
        }

        private static void AssertRuntimeAttributesRemainMetadata(string[] managedAssemblies)
        {
            string runtimeAssembly = managedAssemblies.FirstOrDefault(path =>
                string.Equals(Path.GetFileName(path), RuntimeAssemblyFileName, StringComparison.Ordinal)
            );

            if (runtimeAssembly == null)
                throw new InvalidOperationException($"{RuntimeAssemblyFileName} was not found in player build output.");

            string[] missingAttributeTypes = RequiredRuntimeMetadataTypeNames
                .Where(typeName => !QuickTestPlayerBuildAssemblyInspector.ContainsType(runtimeAssembly, typeName))
                .ToArray();

            if (missingAttributeTypes.Length > 0)
            {
                throw new InvalidOperationException(
                    "Runtime quick-test attributes were unexpectedly stripped from player build: " +
                    string.Join(", ", missingAttributeTypes)
                );
            }
        }
    }
}
