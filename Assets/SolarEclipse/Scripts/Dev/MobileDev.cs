using System.Collections;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Touch-only developer helpers: secret gestures that open the dev panel without a keyboard (hold three
    /// fingers for a second anywhere, or tap the version label in the main menu 7 times), gyro fine-aim,
    /// shake-to-ragdoll, a finger visualiser, a frame-time overlay with device info, player giant/tiny mode
    /// and the timed toys used by the dev panel's FUN tab. Created by <see cref="DevPanel.Create"/>.
    /// Runs before PlayerController (and after MobileControls, -500) so gyro look is added to the touch look.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public sealed class MobileDev : MonoBehaviour
    {
        public static MobileDev I { get; private set; }

        // Overlays (session only)
        public static bool TouchVisualizer, PerfOverlay;
        // Player scale (1 = normal); reset when a new player is created
        public static float PlayerScale { get; private set; } = 1f;

        // Secret gestures
        const float HoldTime = 1f;
        const int TapsNeeded = 7;
        float holdTimer;
        bool holdFired;
        Vector2 holdCenter;
        int versionTaps;
        float lastTapAt;
        string tapToast;
        float tapToastUntil;

        // Shake
        Vector3 lastAccel;
        float shakeCooldown;
        public static float LastShakeG;

        // Perf overlay
        readonly float[] frameMs = new float[180];
        int frameHead;

        // Reverse gravity pulse
        float reverseUntil = -1f, gravityBefore = 1f;

        PlayerController lastPlayer;

        public static void Create(Transform parent)
        {
            if (I != null) return;
            var go = new GameObject("MobileDev");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<MobileDev>();
            UIRoot.TopLayer += I.Draw;
        }

        static bool Playing => GameManager.I != null && GameManager.I.State == GameState.Playing && PlayerController.Instance != null;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            frameMs[frameHead] = dt * 1000f;
            frameHead = (frameHead + 1) % frameMs.Length;

            SecretHold(dt);
            VersionTaps();
            Gyro(dt);
            Shake(dt);
            KeepPlayerScale();

            if (reverseUntil > 0f && Time.time > reverseUntil)
            {
                reverseUntil = -1f;
                if (NetState.Authority) { Rules.GravityScale = gravityBefore; Rules.ApplyGravity(); Debug.Log("Gravity back to x" + gravityBefore.ToString("0.00")); }
            }
        }

        // ------------------------------------------------------------------ secret gestures

        void SecretHold(float dt)
        {
            if (Input.touchCount >= 3)
            {
                Vector2 c = Vector2.zero;
                for (int i = 0; i < 3; i++) c += Input.GetTouch(i).position;
                holdCenter = c / 3f;
                holdTimer += dt;
                if (!holdFired && holdTimer >= HoldTime)
                {
                    holdFired = true;
                    DevPanel.Toggle();
                }
            }
            else { holdTimer = 0f; holdFired = false; }
        }

        void VersionTaps()
        {
            if (!(UIRoot.Top is MainMenuScreen) || DevPanel.Open) { if (Time.unscaledTime - lastTapAt > 3f) versionTaps = 0; return; }
            bool down = false;
            Vector2 screen = Vector2.zero;
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began) { down = true; screen = t.position; break; }
            }
            if (!down && Input.touchCount == 0 && Input.GetMouseButtonDown(0)) { down = true; screen = Input.mousePosition; }
            if (!down) return;
            var v = new Vector2(screen.x / UI.Scale, (Screen.height - screen.y) / UI.Scale);
            // The version label is drawn by MainMenuScreen at (42, H - 100, 260 x 36).
            var label = new Rect(42 - 24, UI.VH - 100 - 24, 260 + 48, 36 + 48);
            if (!label.Contains(v)) return;
            if (Time.unscaledTime - lastTapAt > 2f) versionTaps = 0;
            lastTapAt = Time.unscaledTime;
            versionTaps++;
            int left = TapsNeeded - versionTaps;
            if (DevPanel.LoggedIn && versionTaps >= 2)
            {
                ShowTapToast("No need, you are already a developer.");
                versionTaps = 0;
                DevPanel.Toggle();
                return;
            }
            if (left <= 0)
            {
                versionTaps = 0;
                ShowTapToast("You are now a developer! (log in to continue)");
                AudioManager.Ui("ui_success");
                DevPanel.Toggle();
            }
            else if (left <= 4)
            {
                ShowTapToast("You are " + left + (left == 1 ? " tap" : " taps") + " away from being a developer.");
                AudioManager.Ui("ui_click", 0.5f);
            }
        }

        void ShowTapToast(string s) { tapToast = s; tapToastUntil = Time.unscaledTime + 2.2f; }

        // ------------------------------------------------------------------ gyro aim

        public static bool GyroSupported => SystemInfo.supportsGyroscope;

        void Gyro(float dt)
        {
            var d = Settings.Data;
            bool want = d.gyroAim && GyroSupported && Playing && !DevPanel.Open && !UIRoot.AnyBlocking && !UIRoot.DevPanelCapturing;
            if (GyroSupported && Input.gyro.enabled != (d.gyroAim)) Input.gyro.enabled = d.gyroAim;
            if (!want) return;
            var rate = Input.gyro.rotationRateUnbiased; // rad/s, device axes: x right, y up (portrait), z out of the screen
            var up = -Input.gyro.gravity;
            if (up.sqrMagnitude < 0.01f) up = new Vector3(0, 1, 0);
            up.Normalize();
            Vector3 right;
            switch (Screen.orientation)
            {
                case ScreenOrientation.LandscapeRight: right = new Vector3(0, 1, 0); break;
                case ScreenOrientation.Portrait: right = new Vector3(1, 0, 0); break;
                case ScreenOrientation.PortraitUpsideDown: right = new Vector3(-1, 0, 0); break;
                default: right = new Vector3(0, -1, 0); break; // LandscapeLeft (the usual game orientation)
            }
            float yawRate = -Vector3.Dot(rate, up);     // turning right = positive yaw
            float pitchRate = Vector3.Dot(rate, right); // tilting the top away = look up
            const float dead = 0.015f;
            yawRate = Mathf.Abs(yawRate) < dead ? 0f : yawRate;
            pitchRate = Mathf.Abs(pitchRate) < dead ? 0f : pitchRate;
            if (d.gyroInvertX) yawRate = -yawRate;
            if (d.gyroInvertY) pitchRate = -pitchRate;
            float k = Mathf.Rad2Deg * dt * Mathf.Clamp(d.gyroSensitivity, 0.1f, 5f) / Mathf.Max(0.05f, d.touchSensitivity);
            PlayerInput.TouchLook += new Vector2(yawRate, pitchRate) * k;
        }

        // ------------------------------------------------------------------ shake to ragdoll

        void Shake(float dt)
        {
            var a = Input.acceleration;
            float jerk = (a - lastAccel).magnitude;
            lastAccel = a;
            shakeCooldown -= dt;
            if (!Settings.Data.shakeToRagdoll || !Playing || !NetState.Authority || shakeCooldown > 0f) return;
            if (a.magnitude > 2.4f || jerk > 1.8f)
            {
                LastShakeG = Mathf.Max(a.magnitude, jerk);
                shakeCooldown = 1.2f;
                int n = LaunchNearby(30f, 11f);
                if (n > 0) { GameEvents.ShowToast("SHAKE! Launched " + n + " Goredoll" + (n == 1 ? "" : "s")); CameraFX.Shake(0.6f); }
            }
        }

        /// <summary>Knocks down and throws every Goredoll within range, away from the player.</summary>
        public static int LaunchNearby(float range, float speed)
        {
            var p = PlayerController.Instance;
            if (p == null) return 0;
            int n = 0;
            foreach (var g in Goredoll.All.ToArray())
            {
                if (g == null) continue;
                var off = g.Position - p.transform.position;
                if (off.magnitude > range) continue;
                var away = MathX.Flat(off).sqrMagnitude > 0.01f ? MathX.Flat(off).normalized : Random.onUnitSphere;
                if (g.Ragdoll != null) g.Ragdoll.Fall(3f);
                g.Launch(Vector3.up * speed + away * speed * 0.6f + Random.insideUnitSphere * 2f);
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ player scale

        public static void SetPlayerScale(float s)
        {
            var p = PlayerController.Instance;
            if (p == null) { Debug.LogWarning("Load a map first."); return; }
            if (NetState.Active) { Debug.LogWarning("Giant/tiny mode is singleplayer only (other players would not see it)."); return; }
            s = Mathf.Clamp(s, 0.2f, 5f);
            float before = p.transform.localScale.y;
            PlayerScale = s;
            p.transform.localScale = Vector3.one * s;
            // Lift a grown player so the bigger capsule does not start inside the floor/ceiling.
            if (s > before) p.Teleport(p.transform.position + Vector3.up * (1.8f * (s - before) + 0.1f));
            Rules.PlayerSpeedMultiplier = Mathf.Sqrt(s);
            Rules.JumpMultiplier = Mathf.Sqrt(s);
            I.lastPlayer = p;
            Debug.Log("Player scale x" + s.ToString("0.00") + " (speed/jump x" + Mathf.Sqrt(s).ToString("0.00") + ")");
        }

        void KeepPlayerScale()
        {
            var p = PlayerController.Instance;
            if (p == lastPlayer) return;
            lastPlayer = p;
            if (!Mathf.Approximately(PlayerScale, 1f)) { PlayerScale = 1f; Rules.PlayerSpeedMultiplier = Rules.JumpMultiplier = 1f; }
        }

        // ------------------------------------------------------------------ timed toys

        public static void ReverseGravity(float seconds)
        {
            if (I == null) return;
            if (I.reverseUntil < 0f) I.gravityBefore = Rules.GravityScale > 0f ? Rules.GravityScale : 1f;
            Rules.GravityScale = -0.6f;
            Rules.ApplyGravity();
            Rules.NoFallDamage = true;
            I.reverseUntil = Time.time + seconds;
            Debug.Log("Reverse gravity for " + seconds + " s (no fall damage switched on).");
        }

        public static void Airstrike(int count)
        {
            if (I != null) I.StartCoroutine(I.AirstrikeRoutine(count));
        }

        IEnumerator AirstrikeRoutine(int count)
        {
            var p = PlayerController.Instance;
            if (p == null) yield break;
            Vector3 center = p.EyePosition + p.AimDirection * 18f;
            if (Physics.Raycast(p.EyePosition, p.AimDirection, out var hit, 300f, Layers.ShootMask, QueryTriggerInteraction.Ignore)) center = hit.point;
            for (int i = 0; i < count; i++)
            {
                if (PlayerController.Instance == null || !NetState.Authority) yield break;
                var pos = center + new Vector3(Random.Range(-9f, 9f), 0f, Random.Range(-9f, 9f));
                if (Physics.Raycast(pos + Vector3.up * 30f, Vector3.down, out var g, 80f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) pos = g.point;
                Explosion.Create(pos + Vector3.up * 0.3f, 4.5f, 180f, PlayerController.Instance, 1.3f);
                yield return new WaitForSeconds(0.18f);
            }
        }

        // ------------------------------------------------------------------ drag guard for touch lists

        static Vector2 downPos;

        /// <summary>Call at the top of an OnGUI draw: remembers where the finger went down.</summary>
        public static void TrackPointer()
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown) downPos = e.mousePosition;
        }

        /// <summary>True when the finger moved too far since it went down (a scroll drag, not a tap).</summary>
        public static bool PointerDragged
        {
            get
            {
                var e = Event.current;
                return e != null && (e.mousePosition - downPos).magnitude > 22f * UI.Scale;
            }
        }

        /// <summary>A big touch button that ignores the release of a scroll drag.</summary>
        public static bool Tap(Rect r, string label, ButtonKind kind = ButtonKind.Normal, int size = 22, bool selected = false, bool enabled = true)
            => UI.Button(r, label, kind, null, size, enabled, null, selected) && !PointerDragged;

        // ------------------------------------------------------------------ overlays

        void Draw()
        {
            if (holdTimer > 0.2f && !holdFired) DrawHoldRing();
            if (tapToast != null && Time.unscaledTime < tapToastUntil) DrawTapToast();
            if (PerfOverlay && !DevPanel.Open) DrawPerfOverlay();
            if (TouchVisualizer) DrawTouches();
        }

        void DrawHoldRing()
        {
            var c = new Vector2(holdCenter.x / UI.Scale, (Screen.height - holdCenter.y) / UI.Scale);
            float t = Mathf.Clamp01(holdTimer / HoldTime);
            float r = 70f + 30f * t;
            UI.Tex(new Rect(c.x - r, c.y - r, r * 2, r * 2), TextureFactory.Ring, UI.Accent2.WithAlpha(0.4f + 0.6f * t));
            UI.Label(new Rect(c.x - 200, c.y + r + 6, 400, 34), t < 1f ? "Hold for dev panel..." : "", 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        void DrawTapToast()
        {
            float w = Mathf.Min(UI.VW - 40, UI.TextWidth(tapToast, 24) + 60);
            var r = new Rect((UI.VW - w) / 2f, UI.VH - 190, w, 64);
            UI.Rounded(r, new Color(0.05f, 0.05f, 0.07f, 0.92f));
            UI.Label(r, tapToast, 24, Color.white, TextAnchor.MiddleCenter);
        }

        void DrawTouches()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                var v = new Vector2(t.position.x / UI.Scale, (Screen.height - t.position.y) / UI.Scale);
                float r = 46f + t.pressure * 10f;
                var col = Color.HSVToRGB(Mathf.Repeat(t.fingerId * 0.17f, 1f), 0.8f, 1f);
                UI.Tex(new Rect(v.x - r, v.y - r, r * 2, r * 2), TextureFactory.Circle, col.WithAlpha(0.25f));
                UI.Tex(new Rect(v.x - r, v.y - r, r * 2, r * 2), TextureFactory.Ring, col.WithAlpha(0.9f));
                UI.Label(new Rect(v.x - 120, v.y - r - 52, 240, 48), "#" + t.fingerId + " " + t.phase + "\n" + Mathf.RoundToInt(t.position.x) + ", " + Mathf.RoundToInt(t.position.y), 16, Color.white, TextAnchor.LowerCenter, FontStyle.Bold, true, true);
            }
            if (Input.touchCount == 0 && Input.mousePresent && Input.GetMouseButton(0))
            {
                var m = Input.mousePosition;
                var v = new Vector2(m.x / UI.Scale, (Screen.height - m.y) / UI.Scale);
                UI.Tex(new Rect(v.x - 40, v.y - 40, 80, 80), TextureFactory.Ring, Color.white.WithAlpha(0.8f));
            }
            UI.Label(new Rect(20, UI.VH - 46, 400, 30), "Touches: " + Input.touchCount, 18, Color.white);
        }

        void DrawPerfOverlay()
        {
            const float w = 600f;
            var r = new Rect((UI.VW - w) / 2f, 10f, w, 282f);
            UI.Rounded(r, new Color(0, 0, 0, 0.72f));
            var graph = new Rect(r.x + 12, r.y + 12, r.width - 24, 80);
            UI.Fill(graph, new Color(1, 1, 1, 0.05f));
            const float maxMs = 50f;
            foreach (var target in new[] { 16.7f, 33.3f })
            {
                float gy = graph.yMax - target / maxMs * graph.height;
                UI.Fill(new Rect(graph.x, gy, graph.width, 1), new Color(1, 1, 1, 0.2f));
            }
            float bw = graph.width / frameMs.Length, worst = 0f, sum = 0f;
            for (int i = 0; i < frameMs.Length; i++)
            {
                float ms = frameMs[(frameHead + i) % frameMs.Length];
                if (ms <= 0f) continue;
                worst = Mathf.Max(worst, ms); sum += ms;
                float h = Mathf.Clamp(ms, 0f, maxMs) / maxMs * graph.height;
                UI.Fill(new Rect(graph.x + i * bw, graph.yMax - h, Mathf.Max(1f, bw - 0.4f), h), ms > 33.4f ? UI.Bad : ms > 16.8f ? UI.Warn : UI.Good);
            }
            float avg = sum / frameMs.Length;
            string battery = SystemInfo.batteryLevel < 0f ? "n/a" : Mathf.RoundToInt(SystemInfo.batteryLevel * 100f) + "% (" + SystemInfo.batteryStatus + ")";
            var sa = Screen.safeArea;
            string s =
                "FPS " + (1000f / Mathf.Max(0.01f, avg)).ToString("0") + "  |  avg " + avg.ToString("0.0") + " ms  |  worst " + worst.ToString("0.0") + " ms  |  x" + Time.timeScale.ToString("0.00") + "\n" +
                SystemInfo.deviceModel + "  |  " + SystemInfo.processorCount + " cores  |  RAM " + SystemInfo.systemMemorySize + " MB\n" +
                "GPU " + SystemInfo.graphicsDeviceName + "\n" +
                "API " + SystemInfo.graphicsDeviceType + "  |  VRAM " + SystemInfo.graphicsMemorySize + " MB\n" +
                "Battery " + battery + "  |  Thermal n/a  |  Heap " + (System.GC.GetTotalMemory(false) / 1048576f).ToString("0") + " MB\n" +
                "Screen " + Screen.width + "x" + Screen.height + " @ " + Screen.dpi.ToString("0") + " dpi  |  " + Screen.orientation + "\n" +
                "Safe area " + sa.x.ToString("0") + "," + sa.y.ToString("0") + " " + sa.width.ToString("0") + "x" + sa.height.ToString("0") + "  |  touches " + Input.touchCount;
            UI.Label(new Rect(r.x + 14, graph.yMax + 6, r.width - 28, r.yMax - graph.yMax - 10), s, 16, Color.white, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
        }
    }

    /// <summary>
    /// Text field that uses the phone's own keyboard (TouchScreenKeyboard) on mobile, and the normal IMGUI
    /// field on desktop. Reports "submitted" when the keyboard's Done/Enter key is pressed.
    /// </summary>
    public static class TouchKeys
    {
        static TouchScreenKeyboard kb;
        static string kbId;

        public static bool Native => Application.isMobilePlatform && TouchScreenKeyboard.isSupported;
        public static bool IsOpen(string id) => kb != null && kbId == id;

        public static void Open(string id, string text, bool secure, string placeholder)
        {
            if (!Native) return;
            kbId = id;
            kb = TouchScreenKeyboard.Open(text ?? "", TouchScreenKeyboardType.Default, false, false, secure, false, placeholder ?? "");
        }

        public static string Field(Rect v, string value, string id, bool secure, string placeholder, out bool submitted)
        {
            submitted = false;
            if (!Native) return UI.TextField(v, value, id, secure, placeholder);
            value = value ?? "";
            bool active = IsOpen(id);
            if (active)
            {
                var st = kb.status;
                if (st == TouchScreenKeyboard.Status.Visible || st == TouchScreenKeyboard.Status.Done) value = kb.text ?? "";
                if (st == TouchScreenKeyboard.Status.Done) { submitted = true; kb = null; active = false; }
                else if (st != TouchScreenKeyboard.Status.Visible) { kb = null; active = false; }
            }
            UI.Rounded(v, active ? new Color(0.2f, 0.2f, 0.25f, 1f) : new Color(0.12f, 0.12f, 0.15f, 1f));
            if (active) UI.Outline(v, UI.Accent.WithAlpha(0.8f), 2);
            string shown = secure ? new string('*', value.Length) : value;
            if (shown.Length == 0) UI.Label(new Rect(v.x + 14, v.y, v.width - 28, v.height), placeholder ?? "", 22, UI.Muted, TextAnchor.MiddleLeft, FontStyle.Italic, false);
            else UI.Label(new Rect(v.x + 14, v.y, v.width - 28, v.height), shown + (active && Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? "|" : ""), 22, UI.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, false);
            if (UI.Hit(v) && !MobileDev.PointerDragged) Open(id, value, secure, placeholder);
            return value;
        }
    }
}
