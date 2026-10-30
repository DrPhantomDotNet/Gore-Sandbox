using UnityEngine;

namespace SolarEclipse
{
    /// <summary>PLAY STORY: chapter 1's four missions (finished / next / locked), CONTINUE, and the endings you've seen.</summary>
    public sealed class StoryMenuScreen : UIScreen
    {
        public override bool IsModal => true;

        public override void OnOpen() { AudioManager.Ui("ui_open"); Chapter1.EnsureAchievements(); }

        public override void Draw()
        {
            float a = Intro(0, 0.25f);
            UI.Dim(0.75f * a);
            var r = UI.Centered(Mathf.Min(1500, UI.VW - 30), Mathf.Min(980, UI.VH - 20));
            UI.Panel(r, "Story", a);
            int progress = Chapter1.Progress;

            UI.Label(new Rect(r.x + 40, r.y + 80, r.width - 80, 70), "CHAPTER 1:  THE EAST SIDE", 52, new Color(1f, 0.78f, 0.16f), TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
            UI.Label(new Rect(r.x + 40, r.y + 146, r.width - 80, 36), progress >= Chapter1.MissionCount ? "Chapter complete. Replay any mission to find the other endings." : "Mission " + Chapter1.NextMission + " of " + Chapter1.MissionCount + " is next.", 22, UI.Muted);

            float top = r.y + 200, rowH = 140, gap = 14;
            for (int i = 1; i <= Chapter1.MissionCount; i++)
            {
                var rr = new Rect(r.x + 40, top + (i - 1) * (rowH + gap), r.width - 80, rowH);
                bool done = i <= progress, unlocked = Chapter1.Unlocked(i), next = i == Chapter1.NextMission && progress < Chapter1.MissionCount;
                UI.Rounded(rr, next ? new Color(0.3f, 0.2f, 0.05f, 0.9f) : UI.PanelLight, false);
                if (next) UI.Outline(rr, new Color(1f, 0.78f, 0.16f, 0.8f), 2);
                UI.Label(new Rect(rr.x + 24, rr.y + 10, 120, rowH - 20), i.ToString(), 90, unlocked ? new Color(1f, 0.78f, 0.16f) : UI.Muted, TextAnchor.MiddleCenter, FontStyle.Normal, false, false, UI.HeavyFont);
                UI.Label(new Rect(rr.x + 160, rr.y + 16, rr.width - 520, 50), unlocked ? Chapter1.Names[i - 1].ToUpperInvariant() : "LOCKED", 36, unlocked ? Color.white : UI.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
                UI.Label(new Rect(rr.x + 160, rr.y + 68, rr.width - 520, 60), unlocked ? Chapter1.Blurbs[i - 1] : "Finish mission " + (i - 1) + " first.", 21, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                if (done) UI.Label(new Rect(rr.xMax - 360, rr.y + 10, 90, 40), "DONE", 18, UI.Good, TextAnchor.MiddleCenter, FontStyle.Bold);
                if (unlocked && UI.Button(new Rect(rr.xMax - 300, rr.y + (rowH - 90) / 2f, 270, 90), done ? "REPLAY" : "PLAY", next ? ButtonKind.Primary : ButtonKind.Normal, IconFactory.Get("play"), 30))
                {
                    AudioManager.Ui("ui_click");
                    Chapter1.Play(i);
                    return;
                }
            }

            // Endings found.
            float by = r.yMax - 92;
            string[] ids = { "story_end_join", "story_end_king", "story_end_walk" };
            string[] names = { "The New Right Hand", "King of the East Side", "Walk Away" };
            int found = 0;
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k < ids.Length; k++)
            {
                var ach = Stats.Achievements.Find(x => x.Id == ids[k]);
                bool got = ach != null && Stats.IsUnlocked(ach);
                if (got) found++;
                sb.Append(k > 0 ? "   -   " : "").Append(got ? names[k] : "???");
            }
            UI.Label(new Rect(r.x + 280, by, r.width - 620, 30), "ENDINGS FOUND: " + found + " / 3", 18, UI.Accent2, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Label(new Rect(r.x + 280, by + 30, r.width - 620, 34), sb.ToString(), 18, UI.Muted, TextAnchor.MiddleLeft);

            if (GoreContent.Stories.Count > 0 && UI.Button(new Rect(r.xMax - 650, by, 300, 66), "MORE STORIES (" + GoreContent.Stories.Count + ")", ButtonKind.Normal, IconFactory.Get("book"), 22))
                UIRoot.Push(new ExtrasScreen());
            if (UI.Button(new Rect(r.x + 30, by, 220, 66), "BACK", ButtonKind.Normal, IconFactory.Get("arrow_left"), 22)) OnBack();
            if (progress < Chapter1.MissionCount && UI.Button(new Rect(r.xMax - 330, by, 300, 66), "CONTINUE", ButtonKind.Primary, IconFactory.Get("play"), 26))
            {
                AudioManager.Ui("ui_click");
                Chapter1.Play(Chapter1.NextMission);
            }
        }
    }
}
