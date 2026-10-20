using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SolarEclipse.EditorTools
{
    /// <summary>
    /// One-time project setup that runs when the project is first opened in Unity 6:
    /// creates Assets/Scenes/Main.unity (empty — the game builds itself from code), adds it to the build,
    /// sets company/product/version, linear colour space, input handling, landscape orientation for mobile,
    /// and the physics layer names. Also adds a "SolarEclipse" menu with build shortcuts.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        const string DoneKey = "SolarEclipse.GoreSandbox.SetupDone.v1";

        static readonly (int index, string name)[] LayerNames =
        {
            (8, "Player"), (9, "PlayerNoclip"), (10, "Ragdoll"), (11, "Props"), (12, "Debris"), (13, "Boundaries"),
            (14, "Viewmodel"), (15, "Effects"), (16, "Nextbot"), (17, "Trigger"), (18, "RemotePlayer"), (19, "LocalHitbox"), (31, "IconStudio"),
        };

        static ProjectSetup()
        {
            if (Application.isBatchMode) return; // cloud/command-line builds call EnsureSceneAndSettings() themselves
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath) || !SessionState.GetBool(DoneKey, false) && !EditorPrefs.GetBool(DoneKey + Application.dataPath, false))
                    Run(false);
            };
        }

        [MenuItem("SolarEclipse/Set Up Project", priority = 0)]
        public static void RunFromMenu() => Run(true);

        /// <summary>Creates the scene if needed and applies all project settings (no dialogs, no scene switching).</summary>
        public static void EnsureSceneAndSettings() => Apply(false, false);

        static void Run(bool verbose) => Apply(verbose, true);

        static void Apply(bool verbose, bool openScene)
        {
            // Scene
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
                Debug.Log("[SolarEclipse] Created " + ScenePath + " (empty on purpose: GameManager builds everything at runtime).");
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (openScene && EditorSceneManager.GetActiveScene().path != ScenePath && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorSceneManager.OpenScene(ScenePath);

            // Player settings
            PlayerSettings.companyName = "SolarEclipse Games";
            PlayerSettings.productName = "Gore Sandbox";
            PlayerSettings.bundleVersion = GameInfo.Version;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.runInBackground = true; // multiplayer hosts must keep running when alt-tabbed
            PlayerSettings.iOS.microphoneUsageDescription = "Voice chat with other players in multiplayer.";
            PlayerSettings.Android.forceInternetPermission = true;
#if UNITY_6000_0_OR_NEWER
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.solareclipse.goresandbox");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.solareclipse.goresandbox");
#else
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.solareclipse.goresandbox");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.solareclipse.goresandbox");
#endif
            SetInputHandling();
            SetLayers();

            AssetDatabase.SaveAssets();
            SessionState.SetBool(DoneKey, true);
            EditorPrefs.SetBool(DoneKey + Application.dataPath, true);
            if (verbose) EditorUtility.DisplayDialog("Gore Sandbox", "Project set up.\n\nPress Play in Assets/Scenes/Main.unity.", "OK");
        }

        /// <summary>The game uses the classic Input Manager (Input.GetKey / mouse axes). Make sure it isn't set to "new only".</summary>
        static void SetInputHandling()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop != null && prop.intValue == 1)
            {
                prop.intValue = 2;
                so.ApplyModifiedProperties();
                Debug.LogWarning("[SolarEclipse] Active Input Handling set to 'Both' (the game uses the classic Input Manager). Unity may ask to restart the editor — please accept.");
            }
        }

        static void SetLayers()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("layers");
            if (layers == null) return;
            foreach (var (index, name) in LayerNames)
            {
                var el = layers.GetArrayElementAtIndex(index);
                if (el != null && string.IsNullOrEmpty(el.stringValue)) el.stringValue = name;
            }
            so.ApplyModifiedProperties();
        }

        // ------------------------------------------------------------------ build menu

        [MenuItem("SolarEclipse/Build/Windows (x64)", priority = 20)]
        static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/GoreSandbox.exe");

        [MenuItem("SolarEclipse/Build/macOS", priority = 21)]
        static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/GoreSandbox.app");

        [MenuItem("SolarEclipse/Build/Linux", priority = 22)]
        static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/GoreSandbox.x86_64");

        [MenuItem("SolarEclipse/Build/Android (APK)", priority = 23)]
        static void BuildAndroid() => Build(BuildTarget.Android, "Builds/Android/GoreSandbox.apk");

        static void Build(BuildTarget target, string path)
        {
            Run(false);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var options = new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = path, target = target, options = BuildOptions.None };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[SolarEclipse] Build succeeded: {path} ({report.summary.totalSize / 1048576f:0.0} MB)");
                EditorUtility.RevealInFinder(path);
            }
            else Debug.LogError("[SolarEclipse] Build " + report.summary.result + " — see the Console for details.");
        }

        [MenuItem("SolarEclipse/Open Save Data Folder", priority = 40)]
        static void OpenData() => EditorUtility.RevealInFinder(Application.persistentDataPath);

        [MenuItem("SolarEclipse/Reset Settings, Stats and Achievements", priority = 41)]
        static void ResetPrefs()
        {
            if (EditorUtility.DisplayDialog("Reset?", "Deletes saved settings, stats, achievements and easter eggs (PlayerPrefs).", "Reset", "Cancel"))
                PlayerPrefs.DeleteAll();
        }
    }
}
