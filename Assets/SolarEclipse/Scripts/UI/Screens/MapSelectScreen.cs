using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Lists every map, shows details and launch options.</summary>
    public sealed class MapSelectScreen : UIScreen
    {
        int selected;
        readonly MapOptions options = new MapOptions();
        float npcs = 3;
        // GS2: search, favourites, sort and filter (favourites and play counts live in PlayerPrefs on this device).
        string search = "";
        int sortMode, filterMode;
        static readonly string[] Sorts = { "A-Z", "MOST PLAYED", "NEWEST", "FAVOURITES" };
        static readonly string[] Filters = { "ALL", "OUTDOOR", "INDOOR", "GAME MODES", "FAVOURITES" };
        static System.Collections.Generic.HashSet<string> favs;
        static System.Collections.Generic.HashSet<string> Favs
        {
            get
            {
                if (favs == null) { favs = new System.Collections.Generic.HashSet<string>(); foreach (var id in PlayerPrefs.GetString("map_favs", "").Split(',')) if (id.Length > 0) favs.Add(id); }
                return favs;
            }
        }
        static void ToggleFav(string id) { if (!Favs.Remove(id)) Favs.Add(id); PlayerPrefs.SetString("map_favs", string.Join(",", Favs)); }
        static int Plays(string id) => PlayerPrefs.GetInt("map_plays_" + id, 0);

        System.Collections.Generic.List<int> View(System.Collections.Generic.List<MapDef> maps)
        {
            var v = new System.Collections.Generic.List<int>();
            string q = (search ?? "").Trim().ToLowerInvariant();
            for (int i = 0; i < maps.Count; i++)
            {
                var m = maps[i];
                bool mode = System.Array.IndexOf(m.Tags, "Game Mode") >= 0;
                if (filterMode == 1 && (m.Indoor || mode)) continue;
                if (filterMode == 2 && !m.Indoor) continue;
                if (filterMode == 3 && !mode) continue;
                if (filterMode == 4 && !Favs.Contains(m.Id)) continue;
                if (q.Length > 0 && !(m.IsUnlocked() && ((m.Name ?? "").ToLowerInvariant().Contains(q) || (m.Description ?? "").ToLowerInvariant().Contains(q)))) continue;
                v.Add(i);
            }
            if (sortMode == 0) v.Sort((x, y) => string.Compare(maps[x].Name, maps[y].Name, System.StringComparison.OrdinalIgnoreCase));
            else if (sortMode == 1) v.Sort((x, y) => Plays(maps[y].Id).CompareTo(Plays(maps[x].Id)));
            else if (sortMode == 2) v.Reverse(); // catalog order is oldest first
            else v.Sort((x, y) => (Favs.Contains(maps[y].Id) ? 1 : 0).CompareTo(Favs.Contains(maps[x].Id) ? 1 : 0));
            return v;
        }

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
            search = UI.TextField(new Rect(70, 160, 540, 50), search, "mapsearch", false, "Search maps...");
            if (UI.Button(new Rect(70, 216, 266, 48), "SORT: " + Sorts[sortMode], ButtonKind.Ghost, null, 18)) { sortMode = (sortMode + 1) % Sorts.Length; AudioManager.Ui("ui_tab"); }
            if (UI.Button(new Rect(344, 216, 266, 48), "SHOW: " + Filters[filterMode], ButtonKind.Ghost, null, 18)) { filterMode = (filterMode + 1) % Filters.Length; AudioManager.Ui("ui_tab"); }
            var view = View(maps);
            var list = new Rect(70, 274, 540, H - 394);
            float itemH = 128;
            float content = Mathf.Max(1, view.Count) * (itemH + 14);
            float s = UI.BeginScroll("maps", list, content);
            if (view.Count == 0) UI.Label(new Rect(0, 20, list.width - 14, 40), "No maps match.", 22, UI.Muted, TextAnchor.MiddleCenter);
            for (int vi = 0; vi < view.Count; vi++)
            {
                int i = view[vi];
                var m = maps[i];
                bool unlocked = m.IsUnlocked();
                var r = new Rect(0, vi * (itemH + 14) - s, list.width - 14, itemH);
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
                if (unlocked)
                {
                    var star = new Rect(r.xMax - 50, r.y + 10, 40, 40);
                    bool fav = Favs.Contains(m.Id);
                    UI.Tex(star, IconFactory.Get("star"), fav ? new Color(1f, 0.8f, 0.2f) : new Color(1, 1, 1, UI.Hover(star) ? 0.5f : 0.2f));
                    if (UI.Hit(star)) { ToggleFav(m.Id); AudioManager.Ui("ui_tab"); continue; }
                }
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
            if (Weather.Supported(map))
            {
                int wi = (int)options.Weather;
                if (UI.Selector(new Rect(d.x + 44 + colW, y, colW, 56), "Weather", ref wi, MapOptions.Weathers, "Rain, thunderstorms, fog or snow.")) options.Weather = (WeatherKind)wi;
            }

            if (UI.Button(new Rect(70, H - 100, 240, 64), "BACK", ButtonKind.Normal, IconFactory.Get("arrow_left"))) OnBack();
            if (UI.Button(new Rect(W - 410, H - 104, 340, 72), "PLAY " + map.Name.ToUpperInvariant(), ButtonKind.Primary, IconFactory.Get("play"), 26, map.IsUnlocked()))
            { PlayerPrefs.SetInt("map_plays_" + map.Id, Plays(map.Id) + 1); GameManager.I.StartMap(map, options); }
        }

        public override bool OnBack() { AudioManager.Ui("ui_close"); UIRoot.Pop(); return true; }
    }
}
