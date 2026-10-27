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
        public static readonly string[] FilterNames = { "None", "Black & white", "Sepia", "High contrast", "Vignette" };

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
            if (p != null) { if (p.Cam != null) p.Cam.enabled = true; if (p.ViewCam != null) p.ViewCam.enabled = Settings.Data.viewmodel; }
            if (GameManager.I != null) GameManager.I.HudHidden = savedHudHidden;
            AudioManager.Ui("ui_close", 0.6f);
        }

        public static void TakePhoto()
        {
            if (!Active || Capturing || GameManager.I == null) return;
            GameManager.I.StartCoroutine(Capture());
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
            if (Mathf.Abs(Exposure) < 0.001f && Filter == 0) return;
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            float gain = Mathf.Pow(2f, Exposure);
            float cx = w * 0.5f, cy = h * 0.5f, inv = 1f / Mathf.Sqrt(cx * cx + cy * cy);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
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
                    }
                    px[i] = new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), 255);
                }
            tex.SetPixels32(px);
            tex.Apply(false);
        }
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
