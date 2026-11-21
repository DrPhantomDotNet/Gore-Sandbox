using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Gore Sandbox 2: every fighting style's moves, the input for each and how hard it hits (pause menu > MOVE LIST).</summary>
    public sealed class MoveListScreen : UIScreen
    {
        int tab;
        public override bool IsModal => true;
        public override void OnOpen() => AudioManager.Ui("ui_open");
        public override bool OnBack() { AudioManager.Ui("ui_close"); UIRoot.Pop(); return true; }

        public override void Draw()
        {
            float a = Intro(0, 0.25f);
            UI.Dim(0.6f * a);
            var r = UI.Centered(Mathf.Min(1180, UI.VW - 80), Mathf.Min(860, UI.VH - 80));
            UI.Panel(r, "Move List", a);
            var styles = FightingStyles.Styles;
            var names = new string[styles.Count];
            for (int i = 0; i < styles.Count; i++) names[i] = styles[i].Name.ToUpperInvariant();
            tab = UI.Tabs(new Rect(r.x + 24, r.y + 80, r.width - 48, 50), Mathf.Clamp(tab, 0, styles.Count - 1), names, 16);
            var st = styles[tab];
            UI.Label(new Rect(r.x + 28, r.y + 140, r.width - 56, 56), st.Desc, 17, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            UI.Label(new Rect(r.x + 28, r.y + 196, r.width - 56, 26), "Damage x" + st.Damage.ToString("0.00") + "   Speed x" + st.Speed.ToString("0.00") + (st.Gloves ? "   Gloves" : "   Bare hands"), 16, UI.Accent2);

            var rows = new System.Collections.Generic.List<(string input, FightMove m)>();
            for (int i = 0; i < st.Combo.Length; i++) rows.Add(("LMB combo " + (i + 1), st.Combo[i]));
            rows.Add(("Crouch + LMB", st.CrouchLmb)); rows.Add(("Sprint + LMB", st.SprintLmb));
            rows.Add(("F (kick)", st.Kick)); rows.Add(("Crouch + F", st.CrouchKick)); rows.Add(("Sprint + F", st.SprintKick)); rows.Add(("F in the air", st.AirKick));
            rows.Add(("R (special)", st.R)); rows.Add(("Q (special)", st.Q)); rows.Add(("R in a headlock", st.LockThrow));

            var view = new Rect(r.x + 24, r.y + 232, r.width - 48, r.height - 320);
            float rowH = 44, content = rows.Count * rowH + 10;
            float s = UI.BeginScroll("movelist", view, content);
            for (int i = 0; i < rows.Count; i++)
            {
                var spec = FightingStyles.Spec(rows[i].m);
                var rr = new Rect(0, i * rowH - s, view.width - 16, rowH - 6);
                UI.Rounded(rr, new Color(1, 1, 1, i % 2 == 0 ? 0.06f : 0.03f), true);
                UI.Label(new Rect(rr.x + 14, rr.y, 260, rr.height), rows[i].input, 17, UI.Muted);
                UI.Label(new Rect(rr.x + 280, rr.y, 300, rr.height), spec != null ? spec.Name : rows[i].m.ToString(), 19, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                if (spec != null)
                {
                    string zone = spec.Zone == 0 ? "head" : spec.Zone == 1 ? "body" : "legs";
                    UI.Label(new Rect(rr.x + 590, rr.y, rr.width - 604, rr.height), Mathf.RoundToInt(spec.Dmg * st.Damage) + " dmg · " + zone + (spec.Knock > 0f ? " · knockdown" : "") + (spec.Kick ? " · kick" : ""), 16, UI.Accent2, TextAnchor.MiddleRight);
                }
            }
            UI.EndScroll("movelist", view, content);
            UI.Label(new Rect(r.x + 28, r.yMax - 80, r.width - 330, 58), "Also: hold RMB to guard, E for a headlock (then hold RMB to choke, Q to snap the neck), MMB to grab and throw. Hits in a row build a combo; heavy blows on a dazed Goredoll are finishers.", 14, UI.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, true, true);
            if (UI.Button(new Rect(r.xMax - 274, r.yMax - 80, 250, 58), "BACK", ButtonKind.Primary, IconFactory.Get("arrow_left"), 22)) OnBack();
        }
    }
}
