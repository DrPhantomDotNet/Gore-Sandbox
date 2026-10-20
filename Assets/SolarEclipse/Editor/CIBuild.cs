using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SolarEclipse.EditorTools
{
    /// <summary>
    /// Command-line / cloud build entry point (used by .github/workflows/build.yml).
    /// Makes sure the scene and project settings exist, then builds the player to -customBuildPath.
    /// </summary>
    public static class CIBuild
    {
        public static void Build()
        {
            try
            {
                ProjectSetup.EnsureSceneAndSettings();
                string path = Arg("-customBuildPath") ?? "build/StandaloneWindows64/GoreSandbox.exe";
                var target = EditorUserBuildSettings.activeBuildTarget;
                var targetArg = Arg("-buildTarget");
                if (!string.IsNullOrEmpty(targetArg) && Enum.TryParse(targetArg, true, out BuildTarget parsed)) target = parsed;
                if (target == BuildTarget.StandaloneWindows64 && !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) path += ".exe";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));

                Debug.Log($"[CIBuild] Building {target} → {path}");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ProjectSetup.ScenePath },
                    locationPathName = path,
                    target = target,
                    options = BuildOptions.None,
                });
                var s = report.summary;
                Debug.Log($"[CIBuild] Result: {s.result}, {s.totalErrors} errors, {s.totalSize / 1048576f:0.0} MB, {s.totalTime}");
                EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
