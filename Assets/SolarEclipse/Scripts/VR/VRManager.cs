using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR;

namespace SolarEclipse
{
    /// <summary>One controller (or the headset) this frame, with button edges.</summary>
    public sealed class VRHandState
    {
        public bool Valid;
        public Vector3 Pos, Vel;            // tracking space
        public Quaternion Rot = Quaternion.identity;
        public float Trigger, Grip;
        public Vector2 Stick;
        public bool Primary, Secondary, StickClick, Menu;
        bool pTrig, pGrip, pPrim, pSec, pClick, pMenu;
        public bool TriggerHeld => Trigger > 0.55f;
        public bool GripHeld => Grip > 0.55f;
        public bool TriggerDown, TriggerUp, GripDown, GripUp, PrimaryDown, SecondaryDown, StickClickDown, MenuDown;

        public void Read(InputDevice d)
        {
            Valid = d.isValid;
            if (!Valid) { Trigger = Grip = 0; Stick = Vector2.zero; Primary = Secondary = StickClick = Menu = false; Edges(); return; }
            d.TryGetFeatureValue(CommonUsages.devicePosition, out Pos);
            d.TryGetFeatureValue(CommonUsages.deviceRotation, out Rot);
            d.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vel);
            d.TryGetFeatureValue(CommonUsages.trigger, out Trigger);
            d.TryGetFeatureValue(CommonUsages.grip, out Grip);
            d.TryGetFeatureValue(CommonUsages.primary2DAxis, out Stick);
            d.TryGetFeatureValue(CommonUsages.primaryButton, out Primary);
            d.TryGetFeatureValue(CommonUsages.secondaryButton, out Secondary);
            d.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out StickClick);
            d.TryGetFeatureValue(CommonUsages.menuButton, out Menu);
            // Some controllers only report the buttons (no analog value).
            if (Trigger <= 0f && d.TryGetFeatureValue(CommonUsages.triggerButton, out bool tb) && tb) Trigger = 1f;
            if (Grip <= 0f && d.TryGetFeatureValue(CommonUsages.gripButton, out bool gb) && gb) Grip = 1f;
            if (Rot.x == 0 && Rot.y == 0 && Rot.z == 0 && Rot.w == 0) Rot = Quaternion.identity;
            Edges();
        }

        void Edges()
        {
            bool t = TriggerHeld, g = GripHeld;
            TriggerDown = t && !pTrig; TriggerUp = !t && pTrig;
            GripDown = g && !pGrip; GripUp = !g && pGrip;
            PrimaryDown = Primary && !pPrim; SecondaryDown = Secondary && !pSec;
            StickClickDown = StickClick && !pClick; MenuDown = Menu && !pMenu;
            pTrig = t; pGrip = g; pPrim = Primary; pSec = Secondary; pClick = StickClick; pMenu = Menu;
        }
    }

    /// <summary>
    /// VR on Windows through OpenXR (SteamVR, Meta/Oculus Link, Windows Mixed Reality). The game starts XR itself at
    /// launch when a VR runtime is running (Settings > VR: Auto) or always (Always) or never (Off); "-vr" / "-novr" on
    /// the command line force it. While <see cref="Active"/>, the player becomes a VR rig (VR/VRRig.cs) and the UI is
    /// drawn on a panel in the world (VR/VRUI.cs). Controller state for this frame is in <see cref="Left"/>,
    /// <see cref="Right"/> and <see cref="Head"/>. The XR packages are reached through reflection only here.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class VRManager : MonoBehaviour
    {
        public static VRManager I { get; private set; }
        public static bool Active { get; private set; }
        public static string Status { get; private set; } = "VR is off.";
        public static readonly VRHandState Left = new VRHandState(), Right = new VRHandState(), Head = new VRHandState();
        public static event Action Changed;

        static object manager; // XRManagerSettings
        static bool starting;
        static readonly List<XRInputSubsystem> inputs = new List<XRInputSubsystem>();

        public static bool Supported => Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (I != null) return;
            var go = new GameObject("VRManager");
            DontDestroyOnLoad(go);
            I = go.AddComponent<VRManager>();
            I.StartCoroutine(I.AutoStart());
        }

        IEnumerator AutoStart()
        {
            yield return null; // settings are loaded by GameManager on the first frame
            yield return null;
            if (ShouldAutoStart()) yield return StartXR(false);
        }

        static bool ShouldAutoStart()
        {
            if (!Supported) return false;
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-novr") >= 0) return false;
            if (Array.IndexOf(args, "-vr") >= 0) return true;
            int mode = Settings.Data.vrMode;
            if (mode == 2) return false;
            if (mode == 1) return true;
            return RuntimeRunning();
        }

        /// <summary>Is a VR runtime running (SteamVR, Oculus/Meta, WMR)? Only then does "Auto" start VR.</summary>
        public static bool RuntimeRunning()
        {
            try
            {
                foreach (var n in new[] { "vrserver", "vrcompositor", "vrmonitor", "OVRServer_x64", "MixedRealityPortal" })
                    if (System.Diagnostics.Process.GetProcessesByName(n).Length > 0) return true;
            }
            catch (Exception) { }
            return false;
        }

        // ------------------------------------------------------------------ start / stop

        public static void TryStart() { if (I != null && !Active && !starting) I.StartCoroutine(I.StartXR(true)); }

        IEnumerator StartXR(bool manual)
        {
            if (starting || Active) yield break;
            starting = true;
            Status = "Starting VR...";
            bool ok = false;
            try
            {
                var gsType = Type.GetType("UnityEngine.XR.Management.XRGeneralSettings, Unity.XR.Management");
                var gs = gsType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                manager = gs?.GetType().GetProperty("Manager")?.GetValue(gs);
                if (manager == null) Status = "VR isn't available in this build.";
                else
                {
                    manager.GetType().GetMethod("InitializeLoaderSync")?.Invoke(manager, null);
                    var loader = manager.GetType().GetProperty("activeLoader")?.GetValue(manager);
                    if (loader == null) Status = "No VR headset found. Start SteamVR and connect your headset, then press START VR.";
                    else { manager.GetType().GetMethod("StartSubsystems")?.Invoke(manager, null); ok = true; }
                }
            }
            catch (Exception e) { Status = "VR failed to start: " + (e.InnerException ?? e).Message; Debug.LogWarning("[VR] " + e); }

            if (ok)
            {
                // Wait for the headset to be up.
                float until = Time.realtimeSinceStartup + 5f;
                while (!XRSettings.isDeviceActive && Time.realtimeSinceStartup < until) yield return null;
                if (!XRSettings.isDeviceActive) { Status = "The VR headset didn't start."; StopXR(); ok = false; }
            }
            starting = false;
            if (!ok)
            {
                Debug.Log("[VR] " + Status);
                if (manual) GameEvents.ShowToast(Status);
                yield break;
            }
            Active = true;
            Status = "VR is on: " + XRSettings.loadedDeviceName;
            Debug.Log("[VR] " + Status);
            QualitySettings.vSyncCount = 0;          // the headset paces frames
            Application.targetFrameRate = -1;
            SetFloorOrigin();
            Application.onBeforeRender -= VRUI.BeforeRender;
            Application.onBeforeRender += VRUI.BeforeRender;
            Changed?.Invoke();
            // Already in a map: rebuild the player as a VR rig.
            var p = PlayerController.Instance;
            if (p != null && p.GetComponent<VRRig>() == null) p.gameObject.AddComponent<VRRig>();
        }

        public static void StopXR()
        {
            try
            {
                if (manager != null)
                {
                    manager.GetType().GetMethod("StopSubsystems")?.Invoke(manager, null);
                    manager.GetType().GetMethod("DeinitializeLoader")?.Invoke(manager, null);
                }
            }
            catch (Exception e) { Debug.LogWarning("[VR] stop: " + e.Message); }
            bool was = Active;
            Active = false;
            Application.onBeforeRender -= VRUI.BeforeRender;
            VRUI.Shutdown();
            Status = "VR is off.";
            if (was)
            {
                var rig = VRRig.I;
                if (rig != null) Destroy(rig);
                Settings.Apply();
                Changed?.Invoke();
            }
        }

        void OnApplicationQuit() { if (Active) StopXR(); }

        static void SetFloorOrigin()
        {
            try
            {
                inputs.Clear();
                SubsystemManager.GetSubsystems(inputs);
                foreach (var s in inputs) s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
            }
            catch (Exception e) { Debug.LogWarning("[VR] origin: " + e.Message); }
        }

        /// <summary>Recenter the view (the headset's own recenter also works).</summary>
        public static void Recenter()
        {
            try
            {
                inputs.Clear();
                SubsystemManager.GetSubsystems(inputs);
                foreach (var s in inputs) s.TryRecenter();
            }
            catch (Exception) { }
            VRRig.I?.ResetRoomOffset();
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!Active) return;
            VRUI.RestoreMenuCam();
            Poll();
            if (!XRSettings.isDeviceActive && !starting) { Debug.Log("[VR] headset lost"); StopXR(); }
        }

        void LateUpdate()
        {
            if (Active) VRUI.Tick();
        }

        public static void Poll()
        {
            Head.Read(InputDevices.GetDeviceAtXRNode(XRNode.Head));
            Left.Read(InputDevices.GetDeviceAtXRNode(XRNode.LeftHand));
            Right.Read(InputDevices.GetDeviceAtXRNode(XRNode.RightHand));
        }

        /// <summary>Just the head pose (for the camera right before rendering).</summary>
        public static bool HeadPose(out Vector3 pos, out Quaternion rot)
        {
            var d = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            pos = Head.Pos; rot = Head.Rot;
            if (!d.isValid) return false;
            if (d.TryGetFeatureValue(CommonUsages.devicePosition, out var p)) pos = p;
            if (d.TryGetFeatureValue(CommonUsages.deviceRotation, out var r) && (r.x != 0 || r.y != 0 || r.z != 0 || r.w != 0)) rot = r;
            return true;
        }

        /// <summary>Controller rumble (0..1 strength, seconds).</summary>
        public static void Haptic(bool right, float amplitude, float seconds)
        {
            if (!Active) return;
            try
            {
                var d = InputDevices.GetDeviceAtXRNode(right ? XRNode.RightHand : XRNode.LeftHand);
                if (d.isValid && d.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
                    d.SendHapticImpulse(0, Mathf.Clamp01(amplitude), Mathf.Clamp(seconds, 0.005f, 1f));
            }
            catch (Exception) { }
        }
    }
}
