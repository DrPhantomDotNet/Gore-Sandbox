using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Full settings: graphics, audio, controls, gameplay, gore and interface. Shared by the main and pause menus.</summary>
    public sealed class SettingsScreen : UIScreen
    {
        static readonly string[] TabNames = { "GRAPHICS", "AUDIO", "CONTROLS", "GAMEPLAY", "GORE", "INTERFACE", "VOICE" };
        static readonly string[] VoiceModes = { "Push to talk", "Open mic (voice activation)", "Off" };
        static readonly KeyCode[] PttKeys = { KeyCode.G, KeyCode.CapsLock, KeyCode.LeftAlt, KeyCode.Mouse3, KeyCode.Mouse4, KeyCode.H, KeyCode.Y, KeyCode.LeftShift };
        static readonly string[] PttNames = { "G", "Caps Lock", "Left Alt", "Mouse button 4", "Mouse button 5", "H", "Y", "Left Shift" };
        static readonly string[] Quality = { "Very Low", "Low", "Medium", "High", "Very High", "Ultra" };
        static readonly string[] Shadows = { "Off", "Hard", "Soft" };
        static readonly string[] AA = { "Off", "2x MSAA", "4x MSAA", "8x MSAA" };
        static readonly string[] TouchModes = { "Auto", "Always", "Never" };
        static readonly string[] FpsCaps = { "30", "60", "90", "120", "144", "165", "240", "Unlimited" };
        static readonly int[] FpsValues = { 30, 60, 90, 120, 144, 165, 240, -1 };
        int tab;
        readonly bool inGame;

        public SettingsScreen(bool inGame) { this.inGame = inGame; }
        public override bool IsModal => inGame;
        public override bool PausesTime => inGame;

        public override void OnOpen() => AudioManager.Ui("ui_open");

        public override bool OnBack()
        {
            Settings.Save();
            AudioManager.Ui("ui_close");
            UIRoot.Pop();
            return true;
        }

        public override void Draw()
        {
            float a = Intro(0, 0.25f);
            UI.Dim(0.6f * a);
            var r = UI.Centered(Mathf.Min(1180, UI.VW - 80), Mathf.Min(860, UI.VH - 80));
            UI.Panel(r, "Settings", a);
            tab = UI.Tabs(new Rect(r.x + 24, r.y + 80, r.width - 48, 50), tab, TabNames, 20);
            var view = new Rect(r.x + 24, r.y + 146, r.width - 48, r.height - 240);
            float content = Rows() * 64f + 10;
            float s = UI.BeginScroll("settings" + tab, view, content);
            row = 0; scrollY = -s; width = view.width - 16;
            var d = Settings.Data;
            bool changed = false;
            switch (tab)
            {
                case 0:
                {
                    int q = d.qualityLevel < 0 ? QualitySettings.GetQualityLevel() : d.qualityLevel;
                    string[] names = QualitySettings.names.Length > 0 ? QualitySettings.names : Quality;
                    q = Mathf.Clamp(q, 0, names.Length - 1);
                    if (UI.Selector(Next(), "Quality preset", ref q, names)) { d.qualityLevel = q; changed = true; }
                    changed |= UI.Toggle(Next(), "Fullscreen", ref d.fullscreen);
                    changed |= UI.Toggle(Next(), "V-Sync", ref d.vsync);
                    int fps = System.Array.IndexOf(FpsValues, d.targetFps); if (fps < 0) fps = 4;
                    if (UI.Selector(Next(), "FPS cap (V-Sync off)", ref fps, FpsCaps)) { d.targetFps = FpsValues[fps]; changed = true; }
                    changed |= UI.Selector(Next(), "Shadows", ref d.shadowQuality, Shadows);
                    changed |= UI.Slider(Next(), "Shadow distance", ref d.shadowDistance, 10, 200, "0", null, " m");
                    changed |= UI.Selector(Next(), "Anti-aliasing", ref d.antiAliasing, AA);
                    changed |= UI.Slider(Next(), "Field of view", ref d.fov, 60, 120, "0", null, "°");
                    changed |= UI.Slider(Next(), "Viewmodel FOV", ref d.viewmodelFov, 40, 90, "0", null, "°");
                    changed |= UI.Slider(Next(), "Render distance", ref d.renderDistance, 80, 1000, "0", null, " m");
                    changed |= UI.Slider(Next(), "Brightness", ref d.brightness, 0.5f, 1.6f, "0.00");
                    changed |= UI.Toggle(Next(), "Distance fog", ref d.fog);
                    changed |= UI.Toggle(Next(), "Show FPS", ref d.showFps);
                    changed |= UI.Toggle(Next(), "Detailed stats overlay", ref d.detailedStats);
                    break;
                }
                case 1:
                    changed |= UI.Slider(Next(), "Master volume", ref d.masterVolume, 0, 1);
                    changed |= UI.Slider(Next(), "Effects", ref d.sfxVolume, 0, 1);
                    changed |= UI.Slider(Next(), "Goredoll voices", ref d.voiceVolume, 0, 1);
                    changed |= UI.Slider(Next(), "Music", ref d.musicVolume, 0, 1);
                    changed |= UI.Slider(Next(), "Ambience", ref d.ambienceVolume, 0, 1);
                    changed |= UI.Slider(Next(), "Interface", ref d.uiVolume, 0, 1);
                    if (UI.Button(Next(), "Test sound", ButtonKind.Ghost, IconFactory.Get("speaker"))) AudioManager.Play2D(Rng.Pick(new[] { "v_scream", "gun_glock", "headshot", "bell" }), 1f, 1f, AudioBus.Sfx);
                    break;
                case 2:
                    changed |= UI.Slider(Next(), "Mouse sensitivity", ref d.mouseSensitivity, 0.1f, 8f, "0.00");
                    changed |= UI.Slider(Next(), "Aim sensitivity multiplier", ref d.adsSensitivity, 0.2f, 1.5f, "0.00");
                    changed |= UI.Toggle(Next(), "Invert Y", ref d.invertY);
                    changed |= UI.Toggle(Next(), "Toggle aim (instead of hold)", ref d.aimToggle);
                    changed |= UI.Toggle(Next(), "Toggle sprint", ref d.sprintToggle);
                    changed |= UI.Toggle(Next(), "Toggle crouch", ref d.crouchToggle);
                    changed |= UI.Toggle(Next(), "Auto reload when empty", ref d.autoReload);
                    changed |= UI.Selector(Next(), "Touch controls", ref d.touchMode, TouchModes);
                    changed |= UI.Slider(Next(), "Touch look sensitivity", ref d.touchSensitivity, 0.2f, 3f, "0.00");
                    changed |= UI.Slider(Next(), "Touch button size", ref d.touchButtonScale, 0.6f, 1.6f, "0.00");
                    changed |= UI.Slider(Next(), "Touch button opacity", ref d.touchOpacity, 0.15f, 1f, "0.00");
                    if (UI.Button(Next(), "Show key bindings", ButtonKind.Ghost, IconFactory.Get("keyboard"))) UIRoot.Push(new HelpScreen());
                    break;
                case 3:
                    changed |= UI.Toggle(Next(), "Head bob", ref d.headBob);
                    changed |= UI.Slider(Next(), "Screen shake", ref d.screenShake, 0, 2, "0.00");
                    changed |= UI.Toggle(Next(), "Show viewmodel", ref d.viewmodel);
                    changed |= UI.Toggle(Next(), "Goredoll speech subtitles", ref d.subtitles);
                    changed |= UI.Toggle(Next(), "Goredoll name tags", ref d.npcNames);
                    changed |= UI.Toggle(Next(), "Tutorial hints", ref d.tutorialHints);
                    changed |= UI.Toggle(Next(), "Kill streak announcer", ref d.killStreakAnnouncer, "DOUBLE KILL, KILLING SPREE, GODLIKE... banners and stingers.");
                    int maxR = d.maxRagdolls; float fr = maxR;
                    if (UI.Slider(Next(), "Max ragdolls", ref fr, 5, 100, "0")) { d.maxRagdolls = Mathf.RoundToInt(fr); changed = true; }
                    float fc = d.maxCasings;
                    if (UI.Slider(Next(), "Max shell casings", ref fc, 0, 300, "0")) { d.maxCasings = Mathf.RoundToInt(fc); changed = true; }
                    break;
                case 4:
                    changed |= UI.Selector(Next(), "Gore level", ref d.goreLevel, Settings.GoreLevels, Settings.GoreDescriptions[Mathf.Clamp(d.goreLevel, 0, 5)]);
                    changed |= UI.Selector(Next(), "Blood colour", ref d.bloodColor, Settings.BloodColorNames);
                    changed |= UI.Slider(Next(), "Blood amount", ref d.bloodAmount, 0.1f, 3f, "0.00", null, "x");
                    float fd = d.maxDecals;
                    if (UI.Slider(Next(), "Max blood decals", ref fd, 50, 2000, "0")) { d.maxDecals = Mathf.RoundToInt(fd); changed = true; }
                    float fg = d.maxGibs;
                    if (UI.Slider(Next(), "Max gibs", ref fg, 0, 400, "0")) { d.maxGibs = Mathf.RoundToInt(fg); changed = true; }
                    changed |= UI.Toggle(Next(), "Hit markers", ref d.hitmarkers);
                    changed |= UI.Toggle(Next(), "Damage numbers", ref d.damageNumbers);
                    changed |= UI.Toggle(Next(), "Kill feed", ref d.killFeed);
                    break;
                case 6:
                    changed |= DrawVoice(d);
                    break;
                case 5:
                    changed |= UI.Slider(Next(), "Interface scale", ref d.uiScale, 0.7f, 1.4f, "0.00");
                    if (!Application.isMobilePlatform && UI.Toggle(Next(), "Use built-in game font", ref d.builtinFont, "Turn on if text looks broken. Phones always use it."))
                    { changed = true; UI.ReloadFonts(); }
                    changed |= UI.Toggle(Next(), "Automatic updates", ref d.autoUpdate, "Checks for a new version when the game starts and installs it (you're on v" + GameInfo.Version + ").");
                    changed |= UI.Toggle(Next(), "Show chat", ref d.showChat, "Chat messages in the corner (T or Enter to type, also in singleplayer).");
                    changed |= UI.Selector(Next(), "Your age (for chat)", ref d.ageGroup, ChatFilter.AgeGroups, "Only stored on this device, never sent anywhere. Used to set chat maturity: under 13 always hides profanity.");
                    if (ChatFilter.ForcedOn)
                    {
                        bool on = true;
                        UI.Toggle(Next(), "Profanity filter (always on under 13)", ref on);
                    }
                    else changed |= UI.Toggle(Next(), "Profanity filter", ref d.chatFilter, "Hides swear words in chat with ****.");
                    changed |= UI.Selector(Next(), "Crosshair", ref d.crosshairStyle, Settings.CrosshairStyles);
                    changed |= UI.Selector(Next(), "Crosshair colour", ref d.crosshairColor, Settings.CrosshairColors);
                    changed |= UI.Slider(Next(), "Crosshair size", ref d.crosshairSize, 0.5f, 2f, "0.00");
                    break;
            }
            UI.EndScroll("settings" + tab, view, content);
            if (changed) Settings.Apply();

            if (tab != 6 && VoiceChat.I != null && VoiceChat.I.TestMode) VoiceChat.I.StopTest();

            if (UI.Button(new Rect(r.x + 24, r.yMax - 80, 250, 58), "RESET DEFAULTS", ButtonKind.Danger, IconFactory.Get("refresh"), 20))
                UIRoot.Push(new ConfirmDialog("Reset settings?", "Every option goes back to its default value.", Settings.ResetToDefaults, "RESET", ButtonKind.Danger));
            if (UI.Button(new Rect(r.xMax - 274, r.yMax - 80, 250, 58), "SAVE & BACK", ButtonKind.Primary, IconFactory.Get("check"), 22)) OnBack();
        }

        int row;
        float scrollY, width;
        Rect Next() => new Rect(0, scrollY + (row++) * 64f, width, 56);

        int Rows()
        {
            switch (tab)
            {
                case 0: return 14;
                case 1: return 7;
                case 2: return 12;
                case 3: return 9;
                case 4: return 8;
                case 6: return 12;
                default: return 10;
            }
        }

        /// <summary>Voice chat: pick the exact microphone, how to talk, and test it (you hear yourself).</summary>
        bool DrawVoice(SettingsData d)
        {
            bool changed = false;
            var vc = VoiceChat.I;
            var devs = VoiceChat.Devices;
            var names = new string[devs.Length + 1];
            names[0] = "System default";
            for (int i = 0; i < devs.Length; i++) names[i + 1] = devs[i];
            int cur = string.IsNullOrEmpty(d.micDevice) ? 0 : System.Array.IndexOf(devs, d.micDevice) + 1;
            if (cur < 0) cur = 0;
            if (UI.Selector(Next(), "Microphone", ref cur, names, devs.Length == 0 ? "No microphone detected." : "Choose the exact microphone to use, so it never picks the wrong one."))
            {
                d.micDevice = cur == 0 ? "" : devs[cur - 1];
                vc?.RestartMic();
                changed = true;
            }
            changed |= UI.Selector(Next(), "Voice chat", ref d.voiceMode, VoiceModes);
            int k = System.Array.IndexOf(PttKeys, (KeyCode)d.pushToTalkKey); if (k < 0) k = 0;
            if (UI.Selector(Next(), "Push-to-talk key", ref k, PttNames)) { d.pushToTalkKey = (int)PttKeys[k]; changed = true; }
            changed |= UI.Slider(Next(), "Microphone volume (gain)", ref d.micGain, 0.2f, 4f, "0.00", null, "x");
            changed |= UI.Slider(Next(), "Open mic sensitivity", ref d.voiceThreshold, 0.002f, 0.15f, "0.000", "Lower = picks up quieter speech. Watch the meter below while you talk.");
            changed |= UI.Slider(Next(), "Other players' voice volume", ref d.incomingVoiceVolume, 0f, 2f, "0.00");
            changed |= UI.Toggle(Next(), "Proximity voice (3D)", ref d.proximityVoice, "On: hear players from where they stand. Off: everyone sounds like a radio.");

            // Test: capture + loopback with a live level meter and the open-mic threshold marked.
            var rr = Next();
            if (vc != null)
            {
                bool testing = vc.TestMode;
                if (UI.Button(new Rect(rr.x, rr.y, 300, rr.height), testing ? "STOP TEST" : "TEST MICROPHONE", testing ? ButtonKind.Danger : ButtonKind.Normal, IconFactory.Get("speaker"), 18))
                {
                    if (testing) vc.StopTest(); else vc.TestMode = true;
                }
                var meter = new Rect(rr.x + 320, rr.y + 18, rr.width - 330, 20);
                UI.Rounded(meter, new Color(1, 1, 1, 0.08f), true);
                float lvl = testing || NetState.Active ? vc.InputLevel : 0f;
                float th = Mathf.Clamp01(d.voiceThreshold * 6f);   // same scale as InputLevel (rms × 6)
                UI.Rounded(new Rect(meter.x, meter.y, meter.width * lvl, meter.height), lvl > th ? UI.Good : UI.Muted, true);
                UI.Fill(new Rect(meter.x + meter.width * th - 1, meter.y - 4, 2, meter.height + 8), UI.Warn);
            }
            var info = Next();
            string status = vc == null ? "" : !string.IsNullOrEmpty(vc.MicError) ? vc.MicError : vc.TestMode ? "Testing: talk and you'll hear yourself. The yellow line is the open-mic threshold." : "In a multiplayer game: " + VoiceChat.KeyHint + ".";
            UI.Label(info, status, 17, string.IsNullOrEmpty(vc?.MicError) ? UI.Muted : UI.Warn, TextAnchor.MiddleLeft, FontStyle.Normal, true, true);
            string nm = UI.TextField(Next(), d.playerName, "set_name", false, "Your multiplayer name");
            if (nm != d.playerName) { d.playerName = nm.Length > 20 ? nm.Substring(0, 20) : nm; changed = true; }
            return changed;
        }
    }
}
