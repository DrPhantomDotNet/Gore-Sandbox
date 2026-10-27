using System;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Player preferences, saved to PlayerPrefs as JSON.</summary>
    [Serializable]
    public sealed class SettingsData
    {
        // Audio
        public float masterVolume = 0.9f, sfxVolume = 1f, voiceVolume = 1f, musicVolume = 0.55f, uiVolume = 0.8f, ambienceVolume = 0.7f;
        // Graphics
        public int qualityLevel = -1;
        public bool fullscreen = true, vsync = true, fog = true, showFps = true, detailedStats = false;
        public int targetFps = 144, shadowQuality = 2, antiAliasing = 2;
        public float fov = 80f, viewmodelFov = 58f, shadowDistance = 70f, renderDistance = 400f, brightness = 1f, uiScale = 1f;
        public bool builtinFont = false;   // desktop only: use the game font instead of the OS font
        // Controls
        public float mouseSensitivity = 2f, adsSensitivity = 0.65f, touchSensitivity = 1f, touchButtonScale = 1f, touchOpacity = 0.55f;
        public bool invertY = false, aimToggle = false, sprintToggle = false, crouchToggle = false, autoReload = true;
        public int touchMode = 0; // 0 auto, 1 always, 2 never
        // Mobile dev toys (Dev/MobileDev.cs)
        public bool gyroAim = false, gyroInvertX = false, gyroInvertY = false, shakeToRagdoll = false;
        public float gyroSensitivity = 1f;
        // Gameplay / gore
        public int goreLevel = 4; // 0 none, 1 minimum, 2 low, 3 medium, 4 high, 5 unreal
        public int goreScale = 2;  // 1 = old 4-step scale (migrated on load), 2 = current 6-step scale
        public bool setupDone = false; // first-launch setup screen finished
        public int bloodColor = 0;
        public float bloodAmount = 1f;
        public int maxDecals = 450, maxRagdolls = 40, maxCasings = 80, maxGibs = 120;
        public int crosshairStyle = 0, crosshairColor = 0;
        public float crosshairSize = 1f, screenShake = 1f;
        public bool headBob = true, hitmarkers = true, damageNumbers = false, subtitles = true, npcNames = true, viewmodel = true, killFeed = true, tutorialHints = true;
        public bool killStreakAnnouncer = true; // DOUBLE KILL / KILLING SPREE banners + stingers (UI/KillStreaks.cs)
        // Multiplayer + voice chat
        public string playerName = "";
        public string micDevice = "";          // empty = system default microphone
        public int voiceMode = 0;              // 0 push-to-talk, 1 open mic (voice activation), 2 off
        public int pushToTalkKey = (int)KeyCode.G;
        public float micGain = 1f, voiceThreshold = 0.02f, incomingVoiceVolume = 1f;
        public bool proximityVoice = true;
        // Updates
        public bool autoUpdate = true;          // check GitHub for a newer version on startup and install it
        // Chat
        public bool chatFilter = true;          // hide profanity in chat (forced on when ageGroup == 1)
        public int ageGroup = 0;                // 0 not set, 1 under 13, 2 13-17, 3 18+ (only stored on this device)
        public bool showChat = true;
        // Network
        public bool restrictedNetwork = false;  // school/work mode: no open ports, everything through the relay
        public string netProxy = "";            // optional HTTP proxy "host:port" (only if the network needs one)
        public string customRelay = "";         // optional own MQTT broker URL, e.g. wss://example.com/mqtt
    }

    public static class Settings
    {
        const string Key = "seg_settings_v1";
        public static SettingsData Data { get; private set; } = new SettingsData();
        public static event Action Changed;

        public static readonly string[] GoreLevels = { "None", "Minimum", "Low", "Medium", "High", "Unreal" };
        public static readonly string[] GoreDescriptions =
        {
            "No blood, no wounds you can see, no dismemberment.",
            "A little blood. No dismemberment.",
            "Blood and wounds. No dismemberment.",
            "Blood, wounds and dismemberment.",
            "Everything: dismemberment, organs, lots of blood. (Default)",
            "Over the top: extra blood, extra gore, everything breaks.",
        };
        public static readonly string[] BloodColorNames = { "Red", "Dark Red", "Alien Green", "Robot Blue", "Black Oil", "Bubblegum", "Rainbow" };
        static readonly Color[] BloodColors =
        {
            new Color(0.55f, 0.02f, 0.02f), new Color(0.32f, 0.01f, 0.015f), new Color(0.25f, 0.55f, 0.05f),
            new Color(0.05f, 0.25f, 0.65f), new Color(0.05f, 0.04f, 0.04f), new Color(0.95f, 0.35f, 0.65f), Color.red,
        };
        public static readonly string[] CrosshairStyles = { "Cross", "Dot", "Circle", "Cross + Dot", "T-Shape", "None" };
        public static readonly string[] CrosshairColors = { "White", "Green", "Yellow", "Cyan", "Red", "Magenta" };
        static readonly Color[] CrosshairColorValues = { Color.white, new Color(0.3f, 1f, 0.3f), new Color(1f, 0.9f, 0.2f), new Color(0.2f, 1f, 1f), new Color(1f, 0.25f, 0.25f), new Color(1f, 0.3f, 1f) };

        /// <summary>Session-only blood colour override (dev panel "neon blood"); null = use the setting.</summary>
        public static Func<Color> BloodColorOverride;

        public static Color BloodColor
        {
            get
            {
                if (BloodColorOverride != null) return BloodColorOverride();
                int i = Mathf.Clamp(Data.bloodColor, 0, BloodColors.Length - 1);
                if (i == BloodColors.Length - 1) return Color.HSVToRGB(Mathf.Repeat(Time.time * 0.15f, 1f), 0.9f, 0.7f);
                return BloodColors[i];
            }
        }

        public static Color CrosshairColor => CrosshairColorValues[Mathf.Clamp(Data.crosshairColor, 0, CrosshairColorValues.Length - 1)];

        public static bool GoreEnabled => Data.goreLevel > 0;
        public static bool Dismemberment => Data.goreLevel >= 3;
        public static bool Organs => Data.goreLevel >= 4;
        public static bool Unreal => Data.goreLevel >= 5;
        static readonly float[] bloodByLevel = { 0f, 0.2f, 0.45f, 0.75f, 1f, 1.8f };
        public static float BloodScale => bloodByLevel[UnityEngine.Mathf.Clamp(Data.goreLevel, 0, 5)] * Data.bloodAmount;

        public static bool TouchControlsActive
            => Data.touchMode == 1 || (Data.touchMode == 0 && (Application.isMobilePlatform || Input.touchSupported && Input.touchCount > 0 && !Input.mousePresent));

        public static void Load()
        {
            try
            {
                if (PlayerPrefs.HasKey(Key))
                {
                    string json = PlayerPrefs.GetString(Key);
                    Data = JsonUtility.FromJson<SettingsData>(json) ?? new SettingsData();
                    // Saves from before v0.3.2 used Off/Reduced/Full/Extreme (0-3) and have no goreScale field.
                    if (!json.Contains("\"goreScale\"")) { Data.goreLevel = new[] { 0, 2, 4, 5 }[Mathf.Clamp(Data.goreLevel, 0, 3)]; Data.goreScale = 2; }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Settings reset: " + e.Message);
                Data = new SettingsData();
            }
            Apply();
        }

        public static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
            Apply();
        }

        public static void ResetToDefaults()
        {
            Data = new SettingsData();
            Save();
        }

        public static void Apply()
        {
            var d = Data;
            if (d.qualityLevel >= 0 && d.qualityLevel < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != d.qualityLevel)
                QualitySettings.SetQualityLevel(d.qualityLevel, true);
            QualitySettings.vSyncCount = d.vsync ? 1 : 0;
            Application.targetFrameRate = d.vsync ? -1 : d.targetFps;
            QualitySettings.shadows = d.shadowQuality == 0 ? ShadowQuality.Disable : d.shadowQuality == 1 ? ShadowQuality.HardOnly : ShadowQuality.All;
            QualitySettings.shadowDistance = d.shadowDistance;
            QualitySettings.antiAliasing = d.antiAliasing == 0 ? 0 : d.antiAliasing == 1 ? 2 : d.antiAliasing == 2 ? 4 : 8;
            if (!Application.isMobilePlatform && Screen.fullScreen != d.fullscreen)
                Screen.fullScreenMode = d.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            AudioListener.volume = d.masterVolume;
            Changed?.Invoke();
        }
    }
}
