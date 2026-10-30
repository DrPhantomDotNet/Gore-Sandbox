using System;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// The game's UI in VR. Everything UIRoot draws (menus, HUD, toasts, dev panel) goes into a texture shown on a
    /// panel in the world: menus float in front of you where you were looking when they opened (point with the right
    /// hand's laser, pull the trigger to click, right stick up/down to scroll, Y or the menu button to go back); during
    /// play the HUD rides in front of your eyes. The main menu's camera follows your head too.
    /// </summary>
    public static class VRUI
    {
        static RenderTexture rt;
        static GameObject panel, laser;
        static Material mat;
        static Vector2 laserPx;
        static bool laserOn, interactive, placed, pendingDown, pendingUp;
        static float pendingScroll;
        static Vector3 menuHead0;
        static bool menuHead0Set;
        static Camera menuCam;
        static Vector3 menuCamPos;
        static Quaternion menuCamRot;
        static bool menuCamOverridden;
        const float PanelDist = 1.5f, PanelWidth = 1.9f, HudDist = 1.6f, HudWidth = 1.9f;

        public static bool Active => VRManager.Active;

        // ------------------------------------------------------------------ drawing (called by UIRoot.OnGUI)

        public static void OnGUI(Action draw)
        {
            var e = Event.current;
            if (laserOn && (e.isMouse || e.type == EventType.Repaint || e.type == EventType.Layout)) e.mousePosition = laserPx;
            if (e.type == EventType.Layout)
            {
                // Turn the frame's layout event into the laser's click / scroll (same control order as a real click).
                if (pendingDown) { pendingDown = false; e.type = EventType.MouseDown; e.button = 0; e.clickCount = 1; e.mousePosition = laserPx; }
                else if (pendingUp) { pendingUp = false; e.type = EventType.MouseUp; e.button = 0; e.clickCount = 1; e.mousePosition = laserPx; }
                else if (Mathf.Abs(pendingScroll) > 0.01f) { e.type = EventType.ScrollWheel; e.delta = new Vector2(0f, pendingScroll); e.mousePosition = laserPx; pendingScroll = 0f; }
            }
            if (e.type != EventType.Repaint) { draw(); return; }
            EnsureTexture();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, new Color(0, 0, 0, 0));
            draw();
            if (laserOn) DrawCursor();
            RenderTexture.active = prev;
            // The desktop window mirrors the headset; show the UI there too for anyone watching.
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), rt, ScaleMode.StretchToFill, true);
        }

        static void DrawCursor()
        {
            float s = Mathf.Max(10f, Screen.height / 60f);
            GUI.color = new Color(1f, 0.78f, 0.16f, 0.95f);
            GUI.DrawTexture(new Rect(laserPx.x - s / 2f, laserPx.y - s / 2f, s, s), TextureFactory.SoftDot);
            GUI.color = Color.white;
        }

        static void EnsureTexture()
        {
            int w = Mathf.Max(640, Screen.width), h = Mathf.Max(360, Screen.height);
            if (rt != null && rt.width == w && rt.height == h) return;
            if (rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "VR UI", useMipMap = false };
            rt.Create();
            if (mat != null) mat.mainTexture = rt;
        }

        // ------------------------------------------------------------------ per frame (VRManager.LateUpdate)

        public static void Tick()
        {
            EnsureTexture();
            EnsurePanel();
            var gm = GameManager.I;
            var player = PlayerController.Instance;
            bool playing = gm != null && gm.State == GameState.Playing && player != null && VRRig.I != null;
            interactive = !playing || UIRoot.AnyBlocking || DevPanel.Open;

            // Tracking origin for this context (player rig, or the menu camera).
            Vector3 headPos; Quaternion headRot;
            Func<Vector3, Vector3> toWorld; Func<Quaternion, Quaternion> rotToWorld;
            if (playing)
            {
                var origin = VRRig.I.Origin;
                headPos = player.Cam.transform.position; headRot = player.Cam.transform.rotation;
                toWorld = origin.TransformPoint; rotToWorld = r => origin.rotation * r;
                ReleaseMenuCam();
            }
            else
            {
                var cam = MenuBackdrop.Cam != null ? MenuBackdrop.Cam : Camera.main;
                if (cam == null) { panel.SetActive(false); laser.SetActive(false); return; }
                if (!menuHead0Set) { menuHead0 = VRManager.Head.Pos; menuHead0Set = true; }
                var baseRot = Quaternion.Euler(0, cam.transform.eulerAngles.y, 0);
                var basePos = cam.transform.position - baseRot * menuHead0;
                toWorld = v => basePos + baseRot * v; rotToWorld = r => baseRot * r;
                headPos = toWorld(VRManager.Head.Pos); headRot = rotToWorld(VRManager.Head.Rot);
                menuCam = cam;
            }

            // Panel placement.
            var flatFwd = Vector3.ProjectOnPlane(headRot * Vector3.forward, Vector3.up);
            if (flatFwd.sqrMagnitude < 0.01f) flatFwd = Vector3.forward;
            flatFwd.Normalize();
            float aspect = rt.height / (float)rt.width;
            panel.SetActive(true);
            if (interactive)
            {
                // Re-place when opened, or when you've turned away from it.
                bool lost = placed && Vector3.Angle(flatFwd, Vector3.ProjectOnPlane(panel.transform.position - headPos, Vector3.up)) > 70f;
                if (!placed || lost)
                {
                    panel.transform.position = headPos + flatFwd * PanelDist + Vector3.down * 0.12f;
                    panel.transform.rotation = Quaternion.LookRotation(flatFwd);
                    panel.transform.localScale = new Vector3(PanelWidth, PanelWidth * aspect, 1f);
                    placed = true;
                }
                mat.color = Color.white;
            }
            else
            {
                placed = false;
                panel.transform.position = headPos + headRot * Vector3.forward * HudDist;
                panel.transform.rotation = headRot;
                panel.transform.localScale = new Vector3(HudWidth, HudWidth * aspect, 1f);
                mat.color = new Color(1, 1, 1, 0.92f);
            }

            // Laser from the right hand (left if the right isn't tracked).
            laserOn = false;
            var hand = VRManager.Right.Valid ? VRManager.Right : VRManager.Left;
            if (interactive && hand.Valid)
            {
                var origin = toWorld(hand.Pos + (playing ? Vector3.up * Settings.Data.vrHeightOffset : Vector3.zero));
                var dir = rotToWorld(hand.Rot) * Vector3.forward;
                var plane = new Plane(-panel.transform.forward, panel.transform.position);
                float len = 4f;
                if (plane.Raycast(new Ray(origin, dir), out float dist) && dist > 0f)
                {
                    var hit = origin + dir * dist;
                    var local = panel.transform.InverseTransformPoint(hit); // quad is -0.5..0.5
                    if (Mathf.Abs(local.x) <= 0.5f && Mathf.Abs(local.y) <= 0.5f)
                    {
                        laserOn = true;
                        len = dist;
                        laserPx = new Vector2((local.x + 0.5f) * Screen.width, (0.5f - local.y) * Screen.height);
                    }
                }
                laser.SetActive(true);
                laser.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(dir));
                laser.transform.localScale = new Vector3(1f, 1f, len);
                if (laserOn)
                {
                    if (hand.TriggerDown) { pendingDown = true; VRManager.Haptic(hand == VRManager.Right, 0.2f, 0.02f); }
                    if (hand.TriggerUp) pendingUp = true;
                    float sy = VRManager.Right.Stick.y;
                    if (Mathf.Abs(sy) > 0.25f) pendingScroll += -sy * Time.unscaledDeltaTime * 18f;
                }
            }
            else laser.SetActive(false);

            // Y / menu button: back out of menus, or open the pause / spawn menu.
            var L = VRManager.Left;
            if (L.SecondaryDown || L.MenuDown || VRManager.Right.MenuDown)
            {
                if (DevPanel.Open) DevPanel.Toggle();
                else if (UIRoot.Count > 0 && (playing || UIRoot.Count > 1)) UIRoot.HandleBack();
                else if (playing)
                {
                    if (player.Inventory.Current is SpawnGun) UIRoot.Push(new SpawnMenuScreen());
                    else UIRoot.Push(new PauseMenuScreen());
                }
            }
        }

        // ------------------------------------------------------------------ menu camera follows the head

        /// <summary>Application.onBeforeRender: the main menu camera gets your head pose on top of its own.</summary>
        public static void BeforeRender()
        {
            if (!VRManager.Active || menuCam == null || PlayerController.Instance != null) return;
            if (!VRManager.HeadPose(out var hp, out var hr)) return;
            if (!menuCamOverridden) { menuCamPos = menuCam.transform.position; menuCamRot = menuCam.transform.rotation; menuCamOverridden = true; }
            var baseRot = Quaternion.Euler(0, menuCamRot.eulerAngles.y, 0);
            menuCam.transform.SetPositionAndRotation(menuCamPos + baseRot * (hp - menuHead0), baseRot * hr);
        }

        /// <summary>Called early each frame: give the menu camera back its own pose before its script moves it.</summary>
        public static void RestoreMenuCam()
        {
            if (menuCamOverridden && menuCam != null) menuCam.transform.SetPositionAndRotation(menuCamPos, menuCamRot);
            menuCamOverridden = false;
        }

        static void ReleaseMenuCam()
        {
            RestoreMenuCam();
            menuCam = null;
            menuHead0Set = false;
        }

        // ------------------------------------------------------------------ objects

        static void EnsurePanel()
        {
            if (panel != null) return;
            panel = new GameObject("VR UI Panel");
            UnityEngine.Object.DontDestroyOnLoad(panel);
            panel.layer = Layers.Effects;
            var m = new Mesh { name = "VRPanel" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 100f); // never culled
            panel.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = panel.AddComponent<MeshRenderer>();
            mat = new Material(Mats.Find("SolarEclipse/UIPanel", "Sprites/Default")) { name = "VR UI", mainTexture = rt };
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            laser = new GameObject("VR Laser");
            UnityEngine.Object.DontDestroyOnLoad(laser);
            laser.layer = Layers.Effects;
            var lb = new MeshBuilder { Color = Color.white };
            lb.Box(new Vector3(0, 0, 0.5f), new Vector3(0.004f, 0.004f, 1f));
            laser.AddComponent<MeshFilter>().sharedMesh = lb.ToMesh("VRLaser");
            var lr = laser.AddComponent<MeshRenderer>();
            lr.sharedMaterial = Mats.Glow(new Color(1f, 0.78f, 0.16f), 1.5f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laser.SetActive(false);
        }

        public static void Shutdown()
        {
            ReleaseMenuCam();
            if (panel != null) panel.SetActive(false);
            if (laser != null) laser.SetActive(false);
            laserOn = false; placed = false;
        }
    }
}
