using UnityEngine;

namespace SolarEclipse
{
    public sealed class StatsScreen : UIScreen
    {
        int tab;
        static readonly (string stat, string label, string icon, float scale, string unit)[] Rows =
        {
            (Stats.PlaySeconds, "Time played", "clock", 1 / 60f, " min"),
            (Stats.Kills, "Goredolls killed", "skull", 1, ""),
            (Stats.Spawned, "Goredolls spawned", "person", 1, ""),
            (Stats.ShotsFired, "Rounds fired", "bullet", 1, ""),
            (Stats.Hits, "Hits", "target", 1, ""),
            (Stats.Headshots, "Headshots", "crosshair", 1, ""),
            (Stats.NeckShots, "Neck shots", "drop", 1, ""),
            (Stats.Limbs, "Limbs severed", "cut", 1, ""),
            (Stats.Decaps, "Decapitations", "skull", 1, ""),
            (Stats.Gibs, "Body parts gibbed", "explosion", 1, ""),
            (Stats.Slices, "Knife slices", "knife", 1, ""),
            (Stats.BloodMl, "Blood spilled", "drop", 0.001f, " L"),
            (Stats.Paralyzed, "Goredolls paralysed", "spine", 1, ""),
            (Stats.Punches, "Punches thrown", "fist", 1, ""),
            (Stats.Kicks, "Kicks", "kick", 1, ""),
            (Stats.Explosions, "Explosions", "explosion", 1, ""),
            (Stats.Inspections, "Wounds inspected", "inspect", 1, ""),
            (Stats.PropsSpawned, "Props spawned", "box", 1, ""),
            (Stats.Reloads, "Reloads", "reload", 1, ""),
            (Stats.Deaths, "Your deaths", "skull", 1, ""),
            (Stats.Distance, "Distance travelled", "sprint", 0.001f, " km"),
            (Stats.Ducks, "Rubber ducks found", "duck", 1, ""),
            (Stats.EasterEggs, "Easter eggs found", "egg", 1, ""),
            (Stats.MapsPlayed, "Maps loaded", "map", 1, ""),
        };

        public override bool IsModal => true;
        public override void OnOpen() => AudioManager.Ui("ui_open");

        public override void Draw()
        {
            float a = Intro(0, 0.25f);
            UI.Dim(0.6f * a);
            var r = UI.Centered(1000, Mathf.Min(880, UI.VH - 80));
            UI.Panel(r, "Stats & Achievements", a);
            tab = UI.Tabs(new Rect(r.x + 24, r.y + 80, 420, 48), tab, new[] { "STATISTICS", "ACHIEVEMENTS" });
            var view = new Rect(r.x + 24, r.y + 144, r.width - 48, r.height - 240);
            if (tab == 0)
            {
                float rowH = 52, content = Mathf.Ceil(Rows.Length / 2f) * (rowH + 8);
                float s = UI.BeginScroll("stats", view, content);
                float cw = (view.width - 30) / 2f;
                for (int i = 0; i < Rows.Length; i++)
                {
                    var row = Rows[i];
                    var rr = new Rect((i % 2) * (cw + 14), (i / 2) * (rowH + 8) - s, cw, rowH);
                    UI.Rounded(rr, new Color(1, 1, 1, 0.05f));
                    UI.Tex(new Rect(rr.x + 12, rr.y + 10, 32, 32), IconFactory.Get(row.icon), UI.Accent2);
                    UI.Label(new Rect(rr.x + 56, rr.y, cw - 200, rowH), row.label, 21, UI.TextColor);
                    float v = Stats.Get(row.stat) * row.scale;
                    UI.Label(new Rect(rr.xMax - 180, rr.y, 166, rowH), (row.scale < 1 ? v.ToString("0.0") : v.ToString("0")) + row.unit, 22, UI.TextColor, TextAnchor.MiddleRight, FontStyle.Bold);
                }
                UI.EndScroll("stats", view, content);
            }
            else
            {
                float rowH = 84, content = Stats.Achievements.Count * (rowH + 8);
                float s = UI.BeginScroll("ach", view, content);
                for (int i = 0; i < Stats.Achievements.Count; i++)
                {
                    var ach = Stats.Achievements[i];
                    bool un = Stats.IsUnlocked(ach);
                    var rr = new Rect(0, i * (rowH + 8) - s, view.width - 16, rowH);
                    UI.Rounded(rr, un ? new Color(0.35f, 0.25f, 0.05f, 0.5f) : new Color(1, 1, 1, 0.04f));
                    UI.Tex(new Rect(rr.x + 14, rr.y + 14, 56, 56), IconFactory.Get(un ? "trophy" : "lock"), un ? UI.Warn : UI.Muted);
                    bool hidden = ach.Secret && !un;
                    UI.Label(new Rect(rr.x + 86, rr.y + 8, rr.width - 300, 36), hidden ? "Secret achievement" : ach.Name, 24, un ? UI.Warn : UI.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                    UI.Label(new Rect(rr.x + 86, rr.y + 42, rr.width - 300, 34), hidden ? "Keep exploring..." : ach.Description, 19, UI.Muted);
                    float p = Mathf.Clamp01(Stats.Get(ach.Stat) / ach.Target);
                    var bar = new Rect(rr.xMax - 200, rr.y + rowH / 2 - 6, 170, 12);
                    UI.Rounded(bar, new Color(1, 1, 1, 0.1f), true);
                    UI.Rounded(new Rect(bar.x, bar.y, bar.width * p, bar.height), un ? UI.Warn : UI.Accent, true);
                }
                UI.EndScroll("ach", view, content);
            }
            if (UI.Button(new Rect(r.x + 24, r.yMax - 82, 240, 60), "RESET", ButtonKind.Danger, IconFactory.Get("trash"), 20))
                UIRoot.Push(new ConfirmDialog("Reset all progress?", "Statistics and achievements will be wiped.", Stats.ResetAll, "WIPE", ButtonKind.Danger));
            if (UI.Button(new Rect(r.xMax - 264, r.yMax - 82, 240, 60), "CLOSE", ButtonKind.Primary)) OnBack();
        }

        public override bool OnBack() { AudioManager.Ui("ui_close"); UIRoot.Pop(); return true; }
    }
}
