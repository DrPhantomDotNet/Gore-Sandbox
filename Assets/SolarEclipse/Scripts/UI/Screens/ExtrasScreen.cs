using UnityEngine;

namespace SolarEclipse
{
    /// <summary>EXTRAS: stories (GoreStoryAsset) and screens ([GoreMenuButton]) made in the Unity editor.</summary>
    public sealed class ExtrasScreen : UIScreen
    {
        public override bool IsModal => true;
        public override void OnOpen() => AudioManager.Ui("ui_open");

        public override void Draw()
        {
            float a = Intro(0, 0.25f);
            UI.Dim(0.75f * a);
            var r = UI.Centered(Mathf.Min(1300, UI.VW - 30), Mathf.Min(960, UI.VH - 20));
            UI.Panel(r, "Extras", a);
            var stories = GoreContent.Stories;
            var screens = GoreContent.Screens;
            var view = new Rect(r.x + 30, r.y + 80, r.width - 60, r.height - 180);
            float rowH = 110, content = (stories.Count + screens.Count) * (rowH + 10) + 60 + rowH + 20;
            float s = UI.BeginScroll("extras", view, content);
            float y = -s;
            // SolarEclipse Productions trailer (private build, Trailer/TrailerDirector.cs)
            {
                var tr = new Rect(0, y, view.width - 16, rowH);
                y += rowH + 20;
                UI.Gradient(tr, new Color(0.35f, 0.08f, 0.2f), new Color(0.9f, 0.4f, 0.12f));
                UI.Label(new Rect(tr.x + 24, tr.y + 10, tr.width - 340, 46), "TRAILER", 30, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
                UI.Label(new Rect(tr.x + 24, tr.y + 56, tr.width - 340, 46), "SolarEclipse Productions - 2:00 cinematic. Screen-record it; Esc stops it.", 19, Color.white, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                if (UI.Button(new Rect(tr.xMax - 340, tr.y + 20, 310, 70), "PLAY ANIMATION", ButtonKind.Primary, IconFactory.Get("play"), 24)) { AudioManager.Ui("ui_click"); TrailerDirector.Begin(); }
            }
            if (stories.Count > 0) { UI.Label(new Rect(0, y, view.width, 30), "MORE STORIES", 20, UI.Accent2, TextAnchor.MiddleLeft, FontStyle.Bold); y += 36; }
            foreach (var st in stories)
            {
                var rr = new Rect(0, y, view.width - 16, rowH);
                y += rowH + 10;
                if (rr.yMax < 0 || rr.y > view.height) continue;
                UI.Rounded(rr, UI.PanelLight, false);
                var p = st.story;
                UI.Label(new Rect(rr.x + 24, rr.y + 10, rr.width - 340, 46), (p != null ? p.title : st.name).ToUpperInvariant(), 30, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
                UI.Label(new Rect(rr.x + 24, rr.y + 56, rr.width - 340, 46), p != null ? p.description : "", 19, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                if (UI.Button(new Rect(rr.xMax - 280, rr.y + 20, 250, 70), "PLAY", ButtonKind.Primary, IconFactory.Get("play"), 26))
                {
                    var pack = st.ToPack(out var err);
                    if (err != null) { GameEvents.ShowToast(err); AudioManager.Ui("ui_error"); }
                    else { AudioManager.Ui("ui_click"); StoryRunner.Play(new StoryContext { Pack = pack, Retry = null }); }
                }
            }
            if (screens.Count > 0) { y += 10; UI.Label(new Rect(0, y, view.width, 30), "MORE", 20, UI.Accent2, TextAnchor.MiddleLeft, FontStyle.Bold); y += 36; }
            foreach (var sc in screens)
            {
                var rr = new Rect(0, y, view.width - 16, 80);
                y += 90;
                if (rr.yMax < 0 || rr.y > view.height) continue;
                if (UI.Button(rr, sc.Label, ButtonKind.Normal, IconFactory.Get(sc.Icon), 26, true, sc.Tooltip)) { AudioManager.Ui("ui_click"); GoreContent.Open(sc); }
            }
            UI.EndScroll("extras", view, content);
            if (UI.Button(new Rect(r.x + 30, r.yMax - 86, 220, 66), "BACK", ButtonKind.Normal, IconFactory.Get("arrow_left"), 22)) OnBack();
        }
    }
}
