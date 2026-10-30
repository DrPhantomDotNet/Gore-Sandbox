using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SolarEclipse.EditorTools
{
    /// <summary>
    /// Sets up VR for Windows builds: XR Plug-in Management with the OpenXR loader (works with SteamVR, Oculus/Meta
    /// Link, Windows Mixed Reality), "Initialize XR on Startup" OFF (the game starts VR itself only when SteamVR is
    /// running, see Scripts/VR/VRManager.cs) and the common controller profiles enabled (Index, Vive, Quest Touch,
    /// WMR, Reverb G2, simple controller). Everything goes through reflection, so the project still compiles if the
    /// XR packages are missing. Runs from ProjectSetup and before every cloud build.
    /// </summary>
    public static class VRSetup
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        static readonly string[] Profiles =
        {
            "ValveIndexControllerProfile", "HTCViveControllerProfile", "OculusTouchControllerProfile", "MetaQuestTouchProControllerProfile",
            "MetaQuestTouchPlusControllerProfile", "MicrosoftMotionControllerProfile", "HPReverbG2ControllerProfile", "KHRSimpleControllerProfile",
        };

        [MenuItem("SolarEclipse/VR/Set Up VR (OpenXR)", priority = 30)]
        static void Menu() { Ensure(true); EditorUtility.DisplayDialog("VR", "VR set up for Windows (OpenXR). See the Console for details.", "OK"); }

        static Type FindType(string fullName)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        public static bool Ensure(bool verbose)
        {
            try
            {
                var perBtType = FindType("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget");
                if (perBtType == null) { Debug.Log("[VRSetup] XR Plug-in Management isn't installed; skipping VR setup."); return false; }
                var grp = BuildTargetGroup.Standalone;

                // 1) XR General Settings for Standalone with a manager.
                var perBt = perBtType.GetMethod("GetOrCreate", Any)?.Invoke(null, null);
                if (perBt == null) { Debug.LogWarning("[VRSetup] Couldn't create XR settings."); return false; }
                bool hasMgr = (bool)perBtType.GetMethod("HasManagerSettingsForBuildTarget", Any).Invoke(perBt, new object[] { grp });
                if (!hasMgr) perBtType.GetMethod("CreateDefaultManagerSettingsForBuildTarget", Any).Invoke(perBt, new object[] { grp });
                var gs = perBtType.GetMethod("SettingsForBuildTarget", Any).Invoke(perBt, new object[] { grp });
                if (gs == null) { Debug.LogWarning("[VRSetup] No Standalone XR settings."); return false; }
                var gsType = gs.GetType();
                gsType.GetProperty("InitManagerOnStart", Any)?.SetValue(gs, false); // the game decides when to start VR
                var mgr = gsType.GetProperty("Manager", Any)?.GetValue(gs);
                if (mgr == null) { Debug.LogWarning("[VRSetup] No XR manager."); return false; }

                // 2) The OpenXR loader.
                bool hasLoader = false;
                if (mgr.GetType().GetProperty("activeLoaders", Any)?.GetValue(mgr) is IEnumerable loaders)
                    foreach (var l in loaders) if (l != null && l.GetType().FullName == "UnityEngine.XR.OpenXR.OpenXRLoader") hasLoader = true;
                if (!hasLoader)
                {
                    var store = FindType("UnityEditor.XR.Management.Metadata.XRPackageMetadataStore");
                    var assign = store?.GetMethod("AssignLoader", BindingFlags.Public | BindingFlags.Static);
                    bool ok = assign != null && (bool)assign.Invoke(null, new object[] { mgr, "UnityEngine.XR.OpenXR.OpenXRLoader", grp });
                    Debug.Log("[VRSetup] OpenXR loader assigned: " + ok);
                }
                if (gs is Object gso) EditorUtility.SetDirty(gso);
                if (mgr is Object mo) EditorUtility.SetDirty(mo);
                if (perBt is Object pbo) EditorUtility.SetDirty(pbo);

                // 3) OpenXR settings + controller profiles.
                var pkgSettingsType = FindType("UnityEditor.XR.OpenXR.OpenXRPackageSettings");
                pkgSettingsType?.GetMethod("GetOrCreateInstance", Any)?.Invoke(null, null);
                FindType("UnityEditor.XR.OpenXR.Features.FeatureHelpers")?.GetMethod("RefreshFeatures", Any)?.Invoke(null, new object[] { grp });
                var oxSettingsType = FindType("UnityEngine.XR.OpenXR.OpenXRSettings");
                var ox = oxSettingsType?.GetMethod("GetSettingsForBuildTargetGroup", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { grp });
                int enabled = 0;
                if (ox != null)
                {
                    var featuresField = ox.GetType().GetField("features", Any) ?? ox.GetType().BaseType?.GetField("features", Any);
                    if (featuresField?.GetValue(ox) is Array features)
                        foreach (var f in features)
                        {
                            if (f == null || Array.IndexOf(Profiles, f.GetType().Name) < 0) continue;
                            var en = f.GetType().GetProperty("enabled", Any);
                            if (en != null && !(bool)en.GetValue(f)) { en.SetValue(f, true); if (f is Object fo) EditorUtility.SetDirty(fo); }
                            enabled++;
                        }
                    // Multi-pass stereo: the game's own unlit shaders (blood, particles, sky) have no single-pass
                    // instancing support, so they would only show in one eye.
                    var rmField = ox.GetType().GetField("m_renderMode", Any) ?? ox.GetType().BaseType?.GetField("m_renderMode", Any);
                    if (rmField != null && rmField.FieldType.IsEnum)
                    {
                        var multi = Enum.Parse(rmField.FieldType, "MultiPass");
                        if (!Equals(rmField.GetValue(ox), multi)) { rmField.SetValue(ox, multi); Debug.Log("[VRSetup] OpenXR render mode: Multi Pass"); }
                    }
                    if (ox is Object oxo) EditorUtility.SetDirty(oxo);
                }
                AssetDatabase.SaveAssets();
                if (verbose || !hasLoader) Debug.Log("[VRSetup] VR ready: OpenXR loader on Windows, start-up init off, " + enabled + " controller profiles on.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VRSetup] VR setup failed (the game still works without VR): " + e);
                return false;
            }
        }
    }
}
