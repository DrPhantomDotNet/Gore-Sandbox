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
                if (Arg("-customBuildPath") == null && target == BuildTarget.Android) path = "build/Android/GoreSandbox.apk";
                if (target == BuildTarget.StandaloneWindows64 && !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) path += ".exe";
                if (target == BuildTarget.Android)
                {
                    if (!path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) path += ".apk";
                    ConfigureAndroid();
                }
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

        /// <summary>
        /// Android: a plain APK (installable by tapping it), OpenGL ES 3 first, IL2CPP + ARM64, signed with the project's
        /// sideload key so every build can install over the previous one, and a version code that grows with the version.
        /// </summary>
        static void ConfigureAndroid()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3, UnityEngine.Rendering.GraphicsDeviceType.Vulkan });
#if UNITY_6000_0_OR_NEWER
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);
#else
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
#endif
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;

            // 0.5.2 -> 502, 1.2.3 -> 10203: Android only installs an update when this number is higher.
            var parts = GameInfo.Version.Split('.');
            int code = 0;
            for (int i = 0; i < 3; i++) code = code * 100 + (i < parts.Length && int.TryParse(parts[i], out var n) ? n : 0);
            PlayerSettings.Android.bundleVersionCode = Math.Max(1, code);

            // Optional signing key from the private repository secrets ANDROID_KEYSTORE_BASE64 + ANDROID_KEYSTORE_PASS
            // (never stored in the repo). With it, each new APK installs over the previous one.
            string b64 = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_BASE64");
            string pass = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
            string alias = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_NAME");
            if (!string.IsNullOrEmpty(b64) && !string.IsNullOrEmpty(pass))
            {
                string keystore = Path.Combine(Path.GetTempPath(), "goresandbox-release.keystore");
                File.WriteAllBytes(keystore, Convert.FromBase64String(b64.Trim()));
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = pass;
                PlayerSettings.Android.keyaliasName = string.IsNullOrEmpty(alias) ? "goresandbox" : alias;
                PlayerSettings.Android.keyaliasPass = pass;
                Debug.Log("[CIBuild] Signing the APK with the release key from the repository secrets.");
            }
            else
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.LogWarning("[CIBuild] No signing key secret: the APK is debug-signed (uninstall the old one before installing a new build).");
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
