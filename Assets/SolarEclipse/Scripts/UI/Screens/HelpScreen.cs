using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Controls reference.</summary>
    public sealed class HelpScreen : UIScreen
    {
        static readonly (string key, string action)[] Keys =
        {
            ("W A S D", "Move"), ("Mouse", "Look"), ("Shift", "Sprint"), ("Ctrl / C", "Crouch"), ("Space", "Jump"),
            ("LMB", "Fire / punch / slash / grab (Spawn Gun)"), ("RMB", "Aim / stab / throw held object"), ("MMB", "Spawn selected (Spawn Gun)"),
            ("R", "Reload · rotate held object (Spawn Gun)"), ("F", "Kick (dropkick in the air) · freeze held object"),
            ("E", "Inspect wounds / use"), ("Q", "Inspect weapon"), ("B", "Fire mode / Admin Gun mode"),
            ("1 - 9 / Wheel", "Switch items"), ("Wheel (holding)", "Move held object nearer / further"), ("+ / -", "Scale held object"),
            ("Delete / X", "Delete looked-at object (Spawn Gun)"), ("Z", "Undo last spawn"), ("V", "Noclip (when enabled)"),
            ("Esc", "Game menu · spawn menu while holding the Spawn Gun"), ("F5", "Developer panel"), ("F12", "Screenshot"),
            ("B / K", "Bullet time (singleplayer; K if your weapon uses B)"), ("P", "Photo mode (free camera, filters, Enter: take photo)"),
            ("Rope Tool", "LMB end A, LMB end B | RMB cut rope | R length"), ("Gravity Gun", "RMB pull / drop | LMB punt"),
        };

        public override bool IsModal => true;

        public override void Draw()
        {
            float a = Intro(0, 0.25f);
            UI.Dim(0.6f * a);
            var r = UI.Centered(980, Mathf.Min(900, UI.VH - 60));
            UI.Panel(r, "How to play", a);
            var view = new Rect(r.x + 30, r.y + 84, r.width - 60, r.height - 180);
            float content = Keys.Length * 44 + 10;
            float s = UI.BeginScroll("help", view, content);
            for (int i = 0; i < Keys.Length; i++)
            {
                var rr = new Rect(0, i * 44 - s, view.width - 16, 40);
                UI.Rounded(new Rect(rr.x, rr.y, 240, 38), new Color(1, 1, 1, 0.08f), true);
                UI.Label(new Rect(rr.x, rr.y, 240, 38), Keys[i].key, 20, UI.Accent2, TextAnchor.MiddleCenter, FontStyle.Bold);
                UI.Label(new Rect(rr.x + 260, rr.y, rr.width - 260, 38), Keys[i].action, 21, UI.TextColor);
            }
            UI.EndScroll("help", view, content);
            if (UI.Button(new Rect(r.xMax - 270, r.yMax - 82, 240, 60), "GOT IT", ButtonKind.Primary)) OnBack();
        }
    }
}
