using System.Collections;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Photo mode (P): a free-flying camera detached from the player, with the HUD and viewmodel hidden. In
    /// singleplayer time freezes (the screen pauses time like a menu); in multiplayer the world keeps running.
    /// The UI lives in <see cref="PhotoModeScreen"/>; closing that screen restores everything.
    /// </summary>
    public static class PhotoMode
    {
        public static readonly string[] FilterNames = { "None", "Black & white", "Sepia", "High contrast", "Vignette", "Blood red", "Noir", "Neon", "Night vision", "Warm", "Cold", "Comic", "Film grain" };
        public static readonly string[] TimerNames = { "Off", "3 seconds", "10 seconds" };
        public static readonly string[] PoseNames = { "Wave", "Dance", "Hands up", "Cower", "Fight stance", "Punch", "Brace", "Look around", "Writhe" };
        static readonly PoseMode[] Poses = { PoseMode.Wave, PoseMode.Dance, PoseMode.HandsUp, PoseMode.Cower, PoseMode.Guard, PoseMode.Punch, PoseMode.Brace, PoseMode.LookAround, PoseMode.Writhe };
        public static bool Letterbox, Grid, LiveTime;
        public static int Timer, PoseIndex;
        /// <summary>Unscaled time the self-timer fires at (0 = no countdown).</summary>
        public static float CountdownEnd;
        /// <summary>Unscaled time until which singleplayer time runs so posed Goredolls can move into the pose.</summary>
        public static float PoseRunUntil;
        public static Vector3 CameraPosition => cam != null ? cam.transform.position : Vector3.zero;

        /// <summary>Every standing Goredoll within 25 m of the photo camera takes the pose and turns to face the camera.</summary>
        public static int PoseNearby()
        {
            if (!Active || NetState.IsClient) { if (NetState.IsClient) GameEvents.ShowToast("Only the host can pose Goredolls."); return 0; }
            var at = CameraPosition; int n = 0;
            foreach (var d in Goredoll.All)
            {
                if (d == null || d.Ragdoll == null || d.Vitals == null || d.Vitals.State == LifeState.Dead) continue;
                var to = at - d.Position; to.y = 0f;
                if (to.magnitude > 25f) continue;
                d.Ragdoll.HoldPose = Poses[Mathf.Clamp(PoseIndex, 0, Poses.Length - 1)];
                d.Ragdoll.HoldPoseT = 30f;
                d.Ragdoll.HoldYaw = to.sqrMagnitude > 0.01f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg : d.transform.eulerAngles.y;
                n++;
            }
            if (n > 0) PoseRunUntil = Time.unscaledTime + 1.6f;
            return n;
        }

        public static void ReleasePoses() { foreach (var d in Goredoll.All) if (d != null && d.Ragdoll != null) d.Ragdoll.HoldPoseT = 0f; }

        public static bool Active => cam != null;
        public static float Fov = 60f, Roll, Exposure;
        public static int Filter;
        /// <summary>True while a photo is being captured (the screen hides its panel and preview overlays).</summary>
        public static bool Capturing { get; private set; }
        public static float FlashT;

        static PhotoCamera cam;
        static bool savedHudHidden;

        public static void Toggle()
        {
            if (Active) { var s = UIRoot.Find<PhotoModeScreen>(); if (s != null) UIRoot.Remove(s); else Exit(); }
            else Enter();
        }

        public static void Enter()
        {
            var gm = GameManager.I;
            var p = PlayerController.Instance;
            if (Active || gm == null || gm.State != GameState.Playing || p == null || p.Cam == null) return;
            if (VRManager.Active) { GameEvents.ShowToast("Photo mode isn't available in VR."); return; }
            var go = new GameObject("PhotoModeCamera");
            go.tag = "MainCamera"; // billboards / weather follow Camera.main, which is now this one
            var c = go.AddComponent<Camera>();
            c.CopyFrom(p.Cam);
            c.depth = 10;
            go.transform.SetPositionAndRotation(p.Cam.transform.position, p.Cam.transform.rotation);
            cam = go.AddComponent<PhotoCamera>();
            cam.Cam = c;
            cam.Player = p;
            var e = p.Cam.transform.eulerAngles;
            cam.Yaw = e.y; cam.Pitch = e.x > 180f ? e.x - 360f : e.x;
            Fov = p.Cam.fieldOfView; Roll = 0f;
            savedHudHidden = gm.HudHidden;
            gm.HudHidden = true;
            AudioManager.Ui("ui_open", 0.6f);
            UIRoot.Push(new PhotoModeScreen());
        }

        /// <summary>Called by the screen when it closes.</summary>
        public static void Exit()
        {
            if (cam == null) return;
            var p = cam.Player;
            Object.Destroy(cam.gameObject);
            cam = null;
            Capturing = false;
            CountdownEnd = 0f; PoseRunUntil = 0f;
            ReleasePoses();
            if (p != null) { if (p.Cam != null) p.Cam.enabled = true; if (p.ViewCam != null) p.ViewCam.enabled = Settings.Data.viewmodel; }
            if (GameManager.I != null) GameManager.I.HudHidden = savedHudHidden;
            AudioManager.Ui("ui_close", 0.6f);
        }

        public static void TakePhoto()
        {
            if (!Active || Capturing || GameManager.I == null || CountdownEnd > 0f) return;
            if (Timer > 0) GameManager.I.StartCoroutine(Countdown(Timer == 1 ? 3f : 10f));
            else GameManager.I.StartCoroutine(Capture());
        }

        static IEnumerator Countdown(float secs)
        {
            CountdownEnd = Time.unscaledTime + secs;
            int last = -1;
            while (Active && Time.unscaledTime < CountdownEnd)
            {
                int left = Mathf.CeilToInt(CountdownEnd - Time.unscaledTime);
                if (left != last) { last = left; AudioManager.Ui("ui_tab"); }
                yield return null;
            }
            CountdownEnd = 0f;
            if (Active) yield return Capture();
        }

        static IEnumerator Capture()
        {
            Capturing = true;
            yield return null;                       // one frame without the panel
            yield return new WaitForEndOfFrame();
            Texture2D tex = null;
            string path = null;
            try
            {
                tex = ScreenCapture.CaptureScreenshotAsTexture();
                ApplyFilters(tex);
                var dir = System.IO.Path.Combine(Application.persistentDataPath, "Photos");
                System.IO.Directory.CreateDirectory(dir);
                path = System.IO.Path.Combine(dir, "Photo_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png");
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            catch (System.Exception ex) { Debug.LogException(ex); path = null; }
            finally { if (tex != null) Object.Destroy(tex); }
            Capturing = false;
            FlashT = 1f;
            AudioManager.Play2D("photo_shutter", 0.8f, 1f, AudioBus.Ui);
            if (path != null) { GameEvents.ShowToast("Photo saved: " + path); Stats.Add("photos"); }
            else { GameEvents.ShowToast("Couldn't save the photo."); AudioManager.Ui("ui_error"); }
        }

        /// <summary>Exposure, filter and vignette applied exactly to the saved image (the on-screen preview is an approximation).</summary>
        static void ApplyFilters(Texture2D tex)
        {
            if (Mathf.Abs(Exposure) < 0.001f && Filter == 0 && !Letterbox) return;
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            float gain = Mathf.Pow(2f, Exposure);
            float cx = w * 0.5f, cy = h * 0.5f, inv = 1f / Mathf.Sqrt(cx * cx + cy * cy);
            int bar = Letterbox ? Mathf.Max(0, Mathf.RoundToInt((h - w / 2.39f) * 0.5f)) : 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (y < bar || y >= h - bar) { px[i] = new Color32(0, 0, 0, 255); continue; }
                    var c = px[i];
                    float r = c.r / 255f * gain, g = c.g / 255f * gain, b = c.b / 255f * gain;
                    switch (Filter)
                    {
                        case 1: { float l = r * 0.299f + g * 0.587f + b * 0.114f; r = g = b = l; break; }
                        case 2:
                        {
                            float nr = r * 0.393f + g * 0.769f + b * 0.189f, ng = r * 0.349f + g * 0.686f + b * 0.168f, nb = r * 0.272f + g * 0.534f + b * 0.131f;
                            r = nr; g = ng; b = nb; break;
                        }
                        case 3:
                        {
                            r = (r - 0.5f) * 1.6f + 0.5f; g = (g - 0.5f) * 1.6f + 0.5f; b = (b - 0.5f) * 1.6f + 0.5f;
                            float l = r * 0.299f + g * 0.587f + b * 0.114f;
                            r = l + (r - l) * 1.25f; g = l + (g - l) * 1.25f; b = l + (b - l) * 1.25f; break;
                        }
                        case 4:
                        {
                            float dx = x - cx, dy = y - cy;
                            float d = Mathf.Sqrt(dx * dx + dy * dy) * inv;
                            float v = 1f - 0.8f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.35f) / 0.65f));
                            r *= v; g *= v; b *= v; break;
                        }
                        case 5: { float l = r * 0.299f + g * 0.587f + b * 0.114f; r = Mathf.Max(r * 1.25f, l * 1.1f); g = l * 0.42f + g * 0.12f; b = l * 0.38f + b * 0.1f; break; }
                        case 6:
                        {
                            float l = (r * 0.299f + g * 0.587f + b * 0.114f - 0.5f) * 1.8f + 0.45f;
                            float dx = x - cx, dy = y - cy, d = Mathf.Sqrt(dx * dx + dy * dy) * inv;
                            l *= 1f - 0.7f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.3f) / 0.7f));
                            r = g = b = l; break;
                        }
                        case 7:
                        {
                            float l = r * 0.299f + g * 0.587f + b * 0.114f;
                            r = l + (r - l) * 1.9f; g = l + (g - l) * 1.9f; b = l + (b - l) * 1.9f;
                            r = (r * 1.1f - 0.5f) * 1.2f + 0.5f; g = (g * 0.85f - 0.5f) * 1.2f + 0.5f; b = (b * 1.3f - 0.5f) * 1.2f + 0.5f; break;
                        }
                        case 8:
                        {
                            float l = (r * 0.299f + g * 0.587f + b * 0.114f) * 1.6f + 0.05f + (Hash(x, y) - 0.5f) * 0.18f;
                            if (y % 3 == 0) l *= 0.78f;
                            r = l * 0.25f; g = l * 1.15f; b = l * 0.3f; break;
                        }
                        case 9: r *= 1.12f; g *= 1.03f; b *= 0.84f; break;
                        case 10: r *= 0.84f; g *= 1.0f; b *= 1.16f; break;
                        case 11:
                        {
                            float l = r * 0.299f + g * 0.587f + b * 0.114f;
                            r = l + (r - l) * 1.5f; g = l + (g - l) * 1.5f; b = l + (b - l) * 1.5f;
                            r = Mathf.Round(Mathf.Clamp01(r) * 4f) / 4f; g = Mathf.Round(Mathf.Clamp01(g) * 4f) / 4f; b = Mathf.Round(Mathf.Clamp01(b) * 4f) / 4f; break;
                        }
                        case 12:
                        {
                            float l = r * 0.299f + g * 0.587f + b * 0.114f, n = (Hash(x, y) - 0.5f) * 0.16f;
                            r = Mathf.Lerp(r, l * 1.08f, 0.35f) + n; g = Mathf.Lerp(g, l, 0.35f) + n; b = Mathf.Lerp(b, l * 0.85f, 0.35f) + n;
                            float dx = x - cx, dy = y - cy, d = Mathf.Sqrt(dx * dx + dy * dy) * inv, v = 1f - 0.4f * Mathf.SmoothStep(0f, 1f, d);
                            r *= v; g *= v; b *= v; break;
                        }
                    }
                    px[i] = new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), 255);
                }
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        static float Hash(int x, int y) { float v = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f; return v - Mathf.Floor(v); }
    }

    /// <summary>The free-fly photo camera: WASD, Q/E down/up, Shift faster, Ctrl slower; hold RMB (or hide the panel) to look.</summary>
    public sealed class PhotoCamera : MonoBehaviour
    {
        public Camera Cam;
        public PlayerController Player;
        public float Yaw, Pitch;

        public static bool Looking => !Settings.TouchControlsActive && (Input.GetMouseButton(1) || PhotoModeScreen.PanelHidden);

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            var d = Settings.Data;
            if (Looking && !UIRoot.DevPanelCapturing)
            {
                Yaw += Input.GetAxisRaw("Mouse X") * d.mouseSensitivity;
                Pitch = Mathf.Clamp(Pitch - Input.GetAxisRaw("Mouse Y") * (d.invertY ? -1f : 1f) * d.mouseSensitivity, -89f, 89f);
            }
            var touchMove = TouchInput(d);
            var rot = Quaternion.Euler(Pitch, Yaw, 0f);
            var move = Vector3.zero;
            if (!UIRoot.DevPanelCapturing && GUIUtility.keyboardControl == 0)
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move += Vector3.forward;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move += Vector3.back;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move += Vector3.right;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move += Vector3.left;
                if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) move += Vector3.up;
                if (Input.GetKey(KeyCode.Q)) move += Vector3.down;
            }
            move += touchMove;
            float speed = 4f;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) speed *= 4f;
            if (Input.GetKey(KeyCode.LeftControl)) speed *= 0.25f;
            var worldMove = rot * new Vector3(move.x, 0f, move.z) + Vector3.up * move.y;
            transform.position += Vector3.ClampMagnitude(worldMove, 1.5f) * speed * dt;
            transform.rotation = rot * Quaternion.Euler(0f, 0f, PhotoMode.Roll);
            Cam.fieldOfView = PhotoMode.Fov;
            PhotoMode.FlashT = Mathf.MoveTowards(PhotoMode.FlashT, 0f, dt * 3f);
        }

        // Touch: a drag starting on the left 40% of the screen is a move stick, anywhere else looks around
        // (touches that start on the photo panel or its buttons are ignored).
        readonly System.Collections.Generic.Dictionary<int, (bool stick, Vector2 origin, Vector2 last)> fingers = new System.Collections.Generic.Dictionary<int, (bool, Vector2, Vector2)>();

        Vector3 TouchInput(SettingsData d)
        {
            var move = Vector3.zero;
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                var v = new Vector2(t.position.x / UI.Scale, (Screen.height - t.position.y) / UI.Scale);
                if (t.phase == TouchPhase.Began)
                {
                    if (PhotoModeScreen.OverUi(v)) continue;
                    fingers[t.fingerId] = (v.x < UI.VW * 0.4f, v, v);
                    continue;
                }
                if (!fingers.TryGetValue(t.fingerId, out var f)) continue;
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { fingers.Remove(t.fingerId); continue; }
                if (f.stick)
                {
                    var st = Vector2.ClampMagnitude((v - f.origin) / 90f, 1f);
                    move += new Vector3(st.x, 0f, -st.y);
                }
                else
                {
                    var delta = v - f.last;
                    Yaw += delta.x * 0.15f * d.touchSensitivity;
                    Pitch = Mathf.Clamp(Pitch + delta.y * 0.15f * d.touchSensitivity, -89f, 89f);
                }
                fingers[t.fingerId] = (f.stick, f.origin, v);
            }
            if (Input.touchCount == 0) fingers.Clear();
            return move;
        }

        void LateUpdate()
        {
            // The player controller re-enables its cameras every frame; keep them off while we're live.
            if (Player != null)
            {
                if (Player.Cam != null) Player.Cam.enabled = false;
                if (Player.ViewCam != null) Player.ViewCam.enabled = false;
            }
            else PhotoMode.Toggle(); // player gone (map change): leave photo mode
        }
    }
}
