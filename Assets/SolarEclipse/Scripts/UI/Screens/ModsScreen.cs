using UnityEngine;

namespace SolarEclipse
{
    /// <summary>"Coming soon" page for mod support.</summary>
    public sealed class ModsScreen : UIScreen
    {
        bool notified;
        public override bool IsModal => true;
        public override void OnOpen() => AudioManager.Ui("ui_open");

        public override void Draw()
        {
            float a = Intro(0, 0.3f);
            UI.Dim(0.65f * a);
            var r = UI.Centered(900, 560);
            UI.Panel(r, null, a);
            // Hazard stripes header
            var stripe = new Rect(r.x, r.y, r.width, 70);
            UI.Rounded(stripe, new Color(1f, 0.75f, 0.1f, a));
            float off = (Time.unscaledTime * 60f) % 60f;
            for (float x = -60 + off; x < r.width; x += 60)
            {
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(35, new Vector2((r.x + x) * UI.Scale, (r.y + 35) * UI.Scale));
                UI.Fill(new Rect(r.x + x - 10, r.y - 30, 26, 130), new Color(0.08f, 0.08f, 0.08f, 0.85f * a));
                GUI.matrix = m;
            }
            UI.Label(new Rect(r.x, r.y + 90, r.width, 90), "MODS", 80, UI.TextColor.WithAlpha(a), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
            UI.Label(new Rect(r.x, r.y + 175, r.width, 50), "COMING SOON", 40, UI.Accent2.WithAlpha(a * (0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 4))), TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Label(new Rect(r.x + 60, r.y + 240, r.width - 120, 160),
                "Custom maps, weapons, Goredoll outfits and scripted gamemodes are on the roadmap.\nThe spawn system already reads item definitions from code, so mods will plug straight into the spawn menu.",
                22, UI.TextColor.WithAlpha(0.85f * a), TextAnchor.UpperCenter, FontStyle.Normal, true, true);
            if (UI.Button(new Rect(r.x + 60, r.yMax - 100, 340, 64), notified ? "WE'LL TELL YOU!" : "NOTIFY ME", ButtonKind.Primary, IconFactory.Get("bell"), 24, !notified))
            {
                notified = true;
                Toasts.Show("MODS", "You'll be the first to know. (We promise.)", UI.Good);
            }
            if (UI.Button(new Rect(r.xMax - 300, r.yMax - 100, 240, 64), "BACK", ButtonKind.Normal, IconFactory.Get("arrow_left"))) OnBack();
        }

        public override bool OnBack() { AudioManager.Ui("ui_close"); UIRoot.Pop(); return true; }
    }
}
