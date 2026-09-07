using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hollowmere.EditorTools
{
    /// <summary>
    /// One-click Windows build, plus a CLI entry point for automation:
    ///   Unity.exe -quit -batchmode -projectPath &lt;path&gt; -executeMethod Hollowmere.EditorTools.BuildScript.BuildWindows64
    /// </summary>
    public static class BuildScript
    {
        const string OutputDir = "Build/Windows64";
        const string ExeName = "Hollowmere.exe";

        [MenuItem("Hollowmere/Build Windows x64 %#b", priority = 20)]
        public static void BuildWindows64()
        {
            HollowmereSetup.Configure();

            string dir = Path.GetFullPath(OutputDir);
            Directory.CreateDirectory(dir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { HollowmereSetup.MainScenePath },
                locationPathName = Path.Combine(dir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Hollowmere] Build succeeded: {summary.totalSize / 1048576} MB -> {options.locationPathName}");
            }
            else
            {
                Debug.LogError($"[Hollowmere] Build {summary.result} with {summary.totalErrors} error(s).");
                // Non-zero exit so CI and steamcmd pipelines actually notice.
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("Hollowmere/Open Build Folder", priority = 21)]
        public static void OpenBuildFolder()
        {
            string dir = Path.GetFullPath(OutputDir);
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }
    }
}
