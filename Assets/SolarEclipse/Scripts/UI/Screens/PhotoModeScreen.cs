using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Photo mode panel: FOV, roll, exposure, filter, take photo. Pauses time in singleplayer (like a menu);
    /// multiplayer ignores PausesTime so the world keeps going. Esc / P closes it and restores the player camera.
    /// </summary>
    public sealed class PhotoModeScreen : UIScreen
    {
        public static bool PanelHidden;
        int openedFrame;

        public override bool BlocksGameplay => true;
        public override bool PausesTime => !PhotoMode.LiveTime && Time.unscaledTime > PhotoMode.PoseRunUntil;
        public override bool ShowCursor => !PhotoCamera.Looking;

        public override void OnOpen()
        {
            openedFrame = Time.frameCount;
            PanelHidden = false;
        }

        public override void OnClose()
        {
            PanelHidden = false;
            PhotoMode.Exit();
        }

        public override bool OnBack()
        {
            UIRoot.Remove(this);
            return true;
        }

        public override void Tick()
        {
            if (UIRoot.Top != this || UIRoot.DevPanelCapturing || GUIUtility.keyboardControl != 0) return;
            if (Time.frameCount != openedFrame && Input.GetKeyDown(KeyCode.P)) { UIRoot.Remove(this); return; }
            if (Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.Tab)) PanelHidden = !PanelHidden;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.F)) PhotoMode.TakePhoto();
            if (Input.GetKeyDown(KeyCode.R)) { PhotoMode.Roll = 0f; PhotoMode.Fov = Settings.Data.fov; PhotoMode.Exposure = 0f; PhotoMode.Filter = 0; PhotoMode.Letterbox = false; }
            if (Input.GetKeyDown(KeyCode.G)) PhotoMode.Grid = !PhotoMode.Grid;
            if (Input.GetKeyDown(KeyCode.L)) PhotoMode.Letterbox = !PhotoMode.Letterbox;
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f && PhotoCamera.Looking) PhotoMode.Fov = Mathf.Clamp(PhotoMode.Fov - scroll * 3f, 10f, 120f);
        }

        public override void Draw()
        {
            var full = new Rect(0, 0, UI.VW, UI.VH);
            if (!PhotoMode.Capturing)
            {
                DrawPreview(full);
                if (!PanelHidden) DrawPanel();
                else if (Settings.TouchControlsActive)
                {
                    var b = HiddenBar;
                    float bw = (b.width - 24) / 3f;
                    if (UI.Button(new Rect(b.x, b.y, bw, b.height), "PHOTO", ButtonKind.Primary, IconFactory.Get("camera"), 20)) PhotoMode.TakePhoto();
                    if (UI.Button(new Rect(b.x + bw + 12, b.y, bw, b.height), "PANEL", ButtonKind.Normal, null, 20)) PanelHidden = false;
                    if (UI.Button(new Rect(b.x + (bw + 12) * 2, b.y, bw, b.height), "EXIT", ButtonKind.Normal, IconFactory.Get("close"), 20)) UIRoot.Remove(this);
                }
                else UI.Label(new Rect(20, UI.VH - 50, 800, 36), "H: show panel  |  Enter / F: take photo  |  P / Esc: exit", 18, new Color(1, 1, 1, 0.5f));
            }
            if (PhotoMode.CountdownEnd > 0f && !PhotoMode.Capturing)
            {
                int left = Mathf.CeilToInt(PhotoMode.CountdownEnd - Time.unscaledTime);
                UI.Label(new Rect(0, UI.VH * 0.5f - 120, UI.VW, 240), left.ToString(), 180, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
            }
            if (PhotoMode.FlashT > 0f) UI.Fill(full, new Color(1, 1, 1, PhotoMode.FlashT * 0.8f));
        }

        /// <summary>Approximate live preview of the exposure and filters (the saved PNG gets the exact processing).</summary>
        static void DrawPreview(Rect full)
        {
            float e = PhotoMode.Exposure;
            if (e > 0.001f) UI.Fill(full, new Color(1, 1, 1, Mathf.Clamp01(e * 0.3f)));
            else if (e < -0.001f) UI.Fill(full, new Color(0, 0, 0, Mathf.Clamp01(-e * 0.45f)));
            switch (PhotoMode.Filter)
            {
                case 1: UI.Fill(full, new Color(0.5f, 0.5f, 0.5f, 0.55f)); break;
                case 2: UI.Fill(full, new Color(0.55f, 0.38f, 0.18f, 0.4f)); break;
                case 3: UI.Tex(full, TextureFactory.Vignette, new Color(0, 0, 0, 0.35f)); break;
                case 4: UI.Tex(full, TextureFactory.Vignette, new Color(0, 0, 0, 0.9f)); break;
                case 5: UI.Fill(full, new Color(0.6f, 0.02f, 0.02f, 0.35f)); break;
                case 6: UI.Fill(full, new Color(0.3f, 0.3f, 0.3f, 0.6f)); UI.Tex(full, TextureFactory.Vignette, new Color(0, 0, 0, 0.8f)); break;
                case 7: UI.Fill(full, new Color(0.6f, 0.1f, 0.8f, 0.2f)); break;
                case 8: UI.Fill(full, new Color(0.1f, 0.8f, 0.15f, 0.4f)); break;
                case 9: UI.Fill(full, new Color(1f, 0.6f, 0.2f, 0.15f)); break;
                case 10: UI.Fill(full, new Color(0.2f, 0.5f, 1f, 0.15f)); break;
                case 11: UI.Fill(full, new Color(1f, 0.9f, 0.3f, 0.08f)); break;
                case 12: UI.Fill(full, new Color(0.5f, 0.4f, 0.25f, 0.2f)); UI.Tex(full, TextureFactory.Vignette, new Color(0, 0, 0, 0.45f)); break;
            }
            if (PhotoMode.Letterbox)
            {
                float bar = Mathf.Max(0f, (full.height - full.width / 2.39f) * 0.5f);
                UI.Fill(new Rect(0, 0, full.width, bar), Color.black);
                UI.Fill(new Rect(0, full.height - bar, full.width, bar), Color.black);
            }
            if (PhotoMode.Grid)
                for (int i = 1; i < 3; i++)
                {
                    UI.Fill(new Rect(full.width * i / 3f - 1, 0, 2, full.height), new Color(1, 1, 1, 0.35f));
                    UI.Fill(new Rect(0, full.height * i / 3f - 1, full.width, 2), new Color(1, 1, 1, 0.35f));
                }
        }

        /// <summary>Panel position (top-right on touch screens so the left side stays free for the move stick).</summary>
        static Rect PanelRect => Settings.TouchControlsActive ? new Rect(UI.VW - 644, 16, 620, 900) : new Rect(24, UI.VH - 924, 620, 900);
        static Rect HiddenBar => new Rect(UI.VW / 2f - 330, UI.VH - 96, 660, 72);

        /// <summary>Is this virtual-canvas point over the photo UI (so touches there don't move the camera)?</summary>
        public static bool OverUi(Vector2 v) => PanelHidden ? Settings.TouchControlsActive && HiddenBar.Contains(v) : PanelRect.Contains(v);

        void DrawPanel()
        {
            var r = PanelRect;
            UI.Panel(r, "Photo mode", 0.92f);
            float y = r.y + 80, x = r.x + 20, w = r.width - 40;
            UI.Slider(new Rect(x, y, w, 52), "FOV", ref PhotoMode.Fov, 10f, 120f, "0", null, " deg"); y += 60;
            UI.Slider(new Rect(x, y, w, 52), "Roll", ref PhotoMode.Roll, -45f, 45f, "0", null, " deg"); y += 60;
            UI.Slider(new Rect(x, y, w, 52), "Exposure", ref PhotoMode.Exposure, -2f, 2f, "+0.0;-0.0;0.0"); y += 60;
            UI.Selector(new Rect(x, y, w, 52), "Filter", ref PhotoMode.Filter, PhotoMode.FilterNames); y += 60;
            UI.Selector(new Rect(x, y, w, 52), "Self-timer", ref PhotoMode.Timer, PhotoMode.TimerNames); y += 60;
            float hw = (w - 12) / 2f;
            UI.Toggle(new Rect(x, y, hw, 48), "Cinema bars (L)", ref PhotoMode.Letterbox);
            UI.Toggle(new Rect(x + hw + 12, y, hw, 48), "Grid (G)", ref PhotoMode.Grid); y += 54;
            if (!NetState.Active) { UI.Toggle(new Rect(x, y, w, 48), "Let time run (action shots)", ref PhotoMode.LiveTime); y += 56; }
            UI.Selector(new Rect(x, y, w - 200, 52), "Pose", ref PhotoMode.PoseIndex, PhotoMode.PoseNames);
            if (UI.Button(new Rect(x + w - 188, y, 188, 52), "POSE DOLLS", ButtonKind.Normal, null, 17, !NetState.IsClient))
            {
                int n = PhotoMode.PoseNearby();
                GameEvents.ShowToast(n > 0 ? n + " Goredoll" + (n == 1 ? "" : "s") + " posing for the camera." : "No standing Goredolls within 25 m.");
            }
            y += 58;
            if (UI.Button(new Rect(x, y, hw, 46), "RELEASE POSES", ButtonKind.Ghost, null, 16)) PhotoMode.ReleasePoses();
            if (UI.Button(new Rect(x + hw + 12, y, hw, 46), "GALLERY", ButtonKind.Ghost, IconFactory.Get("camera"), 16)) UIRoot.Push(new PhotoGalleryScreen());
            y += 54;
            UI.Label(new Rect(x, y, w, 60), "Hold RMB to look | WASD move | Q/E down/up | Shift fast | Ctrl slow | Wheel (looking) zoom | H hide panel | R reset", 15, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            if (UI.Button(new Rect(x, r.yMax - 84, w * 0.44f, 62), "TAKE PHOTO", ButtonKind.Primary, IconFactory.Get("camera"), 22)) PhotoMode.TakePhoto();
            if (UI.Button(new Rect(x + w * 0.44f + 12, r.yMax - 84, w * 0.26f - 12, 62), "HIDE (H)", ButtonKind.Ghost, null, 18)) PanelHidden = true;
            if (UI.Button(new Rect(x + w * 0.70f + 12, r.yMax - 84, w * 0.30f - 12, 62), "EXIT (P)", ButtonKind.Normal, IconFactory.Get("close"), 18)) UIRoot.Remove(this);
        }
    }
}
