using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Lists every map, shows details and launch options.</summary>
    public sealed class MapSelectScreen : UIScreen
    {
        int selected;
        readonly MapOptions options = new MapOptions();
        float npcs = 3;

        public override void OnOpen()
        {
            AudioManager.Ui("ui_open");
            var maps = MapCatalog.All;
            for (int i = 0; i < maps.Count; i++) if (maps[i].IsUnlocked()) { selected = i; break; }
            options.Time = maps[selected].DefaultTime;
        }

        public override void Draw()
        {
            float W = UI.VW, H = UI.VH;
            float a = Intro(0, 0.3f);
            UI.Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.55f * a));
            UI.Label(new Rect(70, 40, 800, 80), "SELECT MAP", 64, UI.TextColor.WithAlpha(a), TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
            UI.Label(new Rect(74, 108, 900, 36), MapCatalog.All.Count + " maps · choose your playground", 22, UI.Muted.WithAlpha(a));

            var maps = MapCatalog.All;
            var list = new Rect(70, 170, 540, H - 290);
            float itemH = 128;
            float content = maps.Count * (itemH + 14);
            float s = UI.BeginScroll("maps", list, content);
            for (int i = 0; i < maps.Count; i++)
            {
                var m = maps[i];
                bool unlocked = m.IsUnlocked();
                var r = new Rect(0, i * (itemH + 14) - s, list.width - 14, itemH);
                bool sel = i == selected;
                bool hover = UI.Hover(r);
                UI.Rounded(r, sel ? new Color(0.5f, 0.06f, 0.08f, 0.95f) : new Color(0.1f, 0.1f, 0.13f, hover ? 0.98f : 0.9f));
                if (sel) UI.Outline(r, UI.Accent, 2);
                var thumb = new Rect(r.x + 10, r.y + 10, 190, itemH - 20);
                UI.Tex(thumb, MapThumbs.Get(m), unlocked ? Color.white : new Color(0.2f, 0.2f, 0.2f));
                if (!unlocked) UI.Tex(new Rect(thumb.center.x - 26, thumb.center.y - 26, 52, 52), IconFactory.Get("lock"), Color.white);
                if (unlocked) MapTagChips.Draw(thumb.x + 5, thumb.y + 5, m.Tags, 12, 1f, thumb.width - 10);
                UI.Label(new Rect(r.x + 216, r.y + 12, r.width - 230, 40), unlocked ? m.Name : "???", 28, UI.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                UI.Label(new Rect(r.x + 216, r.y + 50, r.width - 230, 70), unlocked ? m.Size + " · " + MapOptions.Times[(int)m.DefaultTime] : m.UnlockHint, 19, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                if (m.Id == BossRealm.MapId && UI.Hit(r)) { AudioManager.Ui("ui_click"); UIRoot.Push(new OrbieScreen(m)); continue; } // Orbie guards the boss realm
                if (UI.Hit(r) && i != selected)
                {
                    if (unlocked) { selected = i; options.Time = m.DefaultTime; AudioManager.Ui("ui_tab"); }
                    else AudioManager.Ui("ui_error");
                }
            }
            UI.EndScroll("maps", list, content);

            // Details
            var map = maps[selected];
            var d = new Rect(650, 170, W - 720, H - 290);
            UI.Panel(d, null, a);
            var hero = new Rect(d.x + 24, d.y + 24, d.width - 48, Mathf.Min(320, (d.width - 48) * 0.5f));
            UI.Tex(hero, MapThumbs.Get(map), Color.white, ScaleMode.ScaleAndCrop);
            UI.Gradient(new Rect(hero.x, hero.yMax - 110, hero.width, 110), new Color(0, 0, 0, 0), new Color(0, 0, 0, 0.85f));
            MapTagChips.Draw(hero.x + 14, hero.y + 14, map.Tags, 16);
            UI.Label(new Rect(hero.x + 22, hero.yMax - 76, hero.width, 60), map.Name.ToUpperInvariant(), 46, UI.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
            float y = hero.yMax + 18;
            UI.Label(new Rect(d.x + 28, y, d.width - 56, 90), map.Description, 22, UI.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            y += UI.TextHeight(map.Description, 22, d.width - 56) + 14;
            float fx = d.x + 28;
            foreach (var f in map.Features)
            {
                float fw = UI.TextWidth(f, 18) + 26;
                if (fx + fw > d.xMax - 28) { fx = d.x + 28; y += 40; }
                UI.Rounded(new Rect(fx, y, fw, 32), new Color(1, 1, 1, 0.08f), true);
                UI.Label(new Rect(fx + 13, y, fw, 32), f, 18, UI.Accent2);
                fx += fw + 8;
            }
            y += 50;

            float colW = (d.width - 72) / 2f;
            int t = (int)options.Time;
            if (UI.Selector(new Rect(d.x + 28, y, colW, 56), "Time", ref t, MapOptions.Times)) options.Time = (TimeOfDay)t;
            UI.Selector(new Rect(d.x + 44 + colW, y, colW, 56), "Loadout", ref options.Loadout, MapOptions.Loadouts);
            y += 66;
            UI.Slider(new Rect(d.x + 28, y, colW, 56), "Goredolls", ref npcs, 0, 20, "0");
            options.StartingNpcs = Mathf.RoundToInt(npcs);
            UI.Toggle(new Rect(d.x + 44 + colW, y, colW, 56), "AI ignores you", ref options.AiIgnore, "Goredolls won't react to you at all.");
            y += 66;
            if (Stats.HasEasterEgg("Nextbots"))
                UI.Toggle(new Rect(d.x + 28, y, colW, 56), "Nextbots roam", ref options.Nextbots, "Unlocked secret: nextbots hunt you on this map.");

            if (UI.Button(new Rect(70, H - 100, 240, 64), "BACK", ButtonKind.Normal, IconFactory.Get("arrow_left"))) OnBack();
            if (UI.Button(new Rect(W - 410, H - 104, 340, 72), "PLAY " + map.Name.ToUpperInvariant(), ButtonKind.Primary, IconFactory.Get("play"), 26, map.IsUnlocked()))
                GameManager.I.StartMap(map, options);
        }

        public override bool OnBack() { AudioManager.Ui("ui_close"); UIRoot.Pop(); return true; }
    }
}
