using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>The map editor's overlay: top toolbar, palette (left), inspector (right), status bar and help.</summary>
    public sealed class MapEditorScreen : UIScreen
    {
        int tab;
        static readonly string[] Tabs = { "BUILD", "PROPS", "ITEMS", "OTHER" };
        List<CustomMapKit.Entry> props;
        float inspectorContent = 400f;
        bool collect;

        public override bool BlocksGameplay => true;
        public override bool ShowCursor => !MapEditor.MouseLook;

        public override bool OnBack()
        {
            var ed = MapEditor.Current;
            if (ed == null) { UIRoot.Pop(); return true; }
            if (ed.HandleEscape()) return true;
            UIRoot.Push(new MapEditorMenu());
            return true;
        }

        void Block(Rect r)
        {
            if (collect && MapEditor.Current != null) MapEditor.Current.UiRects.Add(r);
        }

        public override void Draw()
        {
            var ed = MapEditor.Current;
            if (ed == null) return;
            collect = Event.current.type == EventType.Layout;
            if (collect) ed.UiRects.Clear();
            float W = UI.VW, H = UI.VH;

            if (MapEditor.MouseLook)
            {
                var c = new Vector2(W / 2, H / 2);
                UI.Fill(new Rect(c.x - 10, c.y - 1, 20, 2), new Color(1, 1, 1, 0.8f));
                UI.Fill(new Rect(c.x - 1, c.y - 10, 2, 20), new Color(1, 1, 1, 0.8f));
            }

            DrawTopBar(ed, W);
            if (ed.ShowHelp) { DrawHelp(ed); return; } // nothing clickable underneath the help panel
            DrawPalette(ed, H);
            if (ed.Mode == MapEditor.Tool.Place || ed.SelectedObj != null) DrawInspector(ed, W, H);
            DrawStatus(ed, W, H);
        }

        // ------------------------------------------------------------------ top bar

        void DrawTopBar(MapEditor ed, float W)
        {
            var bar = new Rect(0, 0, W, 70);
            UI.Fill(bar, new Color(0.04f, 0.04f, 0.05f, 0.88f));
            UI.Fill(new Rect(0, 69, W, 2), UI.Accent.WithAlpha(0.7f));
            Block(bar);
            float x = 12, y = 10, h = 50;
            if (UI.Button(new Rect(x, y, 100, h), "EXIT", ButtonKind.Danger, null, 20)) AskExit(ed);
            x += 112;
            if (UI.Button(new Rect(x, y, 120, h), "SELECT", ButtonKind.Tab, null, 20, true, "Select, drag and edit objects (1)", ed.Mode == MapEditor.Tool.Select)) { ed.Mode = MapEditor.Tool.Select; ed.CancelGrab(); }
            x += 122;
            if (UI.Button(new Rect(x, y, 120, h), "PLACE", ButtonKind.Tab, null, 20, ed.Brush != null, "Place the item picked in the palette (2)", ed.Mode == MapEditor.Tool.Place)) { ed.Mode = MapEditor.Tool.Place; ed.Selected = -1; ed.CancelGrab(); }
            x += 132;
            if (UI.Button(new Rect(x, y, 130, h), ed.Snap ? "SNAP ON" : "SNAP OFF", ButtonKind.Tab, null, 20, true, "Grid snapping (Tab)", ed.Snap)) ed.Snap = !ed.Snap;
            x += 132;
            if (UI.Button(new Rect(x, y, 110, h), MapEditor.GridNames[ed.GridIndex], ButtonKind.Ghost, null, 20, true, "Grid size (click to change)")) ed.GridIndex = (ed.GridIndex + 1) % MapEditor.GridSteps.Length;
            x += 122;
            if (UI.Button(new Rect(x, y, 90, h), "UNDO", ButtonKind.Normal, null, 18, ed.CanUndo, "Ctrl+Z")) ed.Undo();
            x += 92;
            if (UI.Button(new Rect(x, y, 90, h), "REDO", ButtonKind.Normal, null, 18, ed.CanRedo, "Ctrl+Y")) ed.Redo();

            float rx = W - 12;
            rx -= 170; if (UI.Button(new Rect(rx, y, 170, h), "TEST PLAY", ButtonKind.Primary, IconFactory.Get("play"), 20, true, "Play the map right now (Ctrl+P). Esc comes back here.")) ed.TestPlay();
            rx -= 128; if (UI.Button(new Rect(rx, y, 120, h), ed.Dirty ? "SAVE*" : "SAVE", ButtonKind.Normal, IconFactory.Get("save"), 20, true, "Ctrl+S")) ed.Save();
            rx -= 108; if (UI.Button(new Rect(rx, y, 100, h), "HELP", ButtonKind.Ghost, null, 20, true, "Controls (H / F1)", ed.ShowHelp)) ed.ShowHelp = !ed.ShowHelp;
            rx -= 158; if (UI.Button(new Rect(rx, y, 150, h), "SETTINGS", ButtonKind.Ghost, IconFactory.Get("gear"), 20, true, "Name, time of day, ground, fog, Goredolls")) UIRoot.Push(new MapSettingsDialog());
        }

        public static void AskExit(MapEditor ed)
        {
            if (!ed.Dirty) { ed.Exit(); return; }
            UIRoot.Push(new EditorExitDialog());
        }

        // ------------------------------------------------------------------ palette

        void DrawPalette(MapEditor ed, float H)
        {
            var p = new Rect(10, 80, 300, H - 80 - 62);
            UI.Rounded(p, UI.PanelColor);
            Block(p);
            UI.Label(new Rect(p.x + 16, p.y + 8, p.width - 32, 34), ed.Data.Name + (ed.Dirty ? " *" : ""), 24, UI.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Label(new Rect(p.x + 16, p.y + 40, p.width - 32, 24), ed.Data.Objects.Count + " / " + CustomMapData.MaxObjects + " objects", 17, UI.Muted);
            tab = UI.Tabs(new Rect(p.x + 8, p.y + 72, p.width - 16, 42), tab, Tabs, 16);
            List<CustomMapKit.Entry> list;
            switch (tab)
            {
                case 1: list = props ??= CustomMapKit.PropEntries(); break;
                case 2: list = CustomMapKit.Items; break;
                case 3: list = CustomMapKit.Specials; break;
                default: list = CustomMapKit.Shapes; break;
            }
            var view = new Rect(p.x + 8, p.y + 124, p.width - 16, p.height - 132);
            float itemH = 52;
            float content = list.Count * (itemH + 6);
            float s = UI.BeginScroll("ed_palette_" + tab, view, content);
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                bool sel = ed.Mode == MapEditor.Tool.Place && ed.Brush != null && ed.Brush.K == e.Kind && ed.Brush.Id == (e.Id ?? "");
                var r = new Rect(0, i * (itemH + 6) - s, view.width - 10, itemH);
                if (UI.Button(r, e.Name, sel ? ButtonKind.Tab : ButtonKind.Normal, IconFactory.Get(e.Glyph), 19, true, null, sel))
                {
                    ed.SetBrush(e);
                    ed.Say("Placing " + e.Name + " - click to place, Q/E or wheel to rotate, Esc to stop.");
                }
            }
            UI.EndScroll("ed_palette_" + tab, view, content);
        }

        // ------------------------------------------------------------------ inspector

        void DrawInspector(MapEditor ed, float W, float H)
        {
            bool placing = ed.Mode == MapEditor.Tool.Place;
            var o = placing ? ed.Brush : ed.SelectedObj;
            if (o == null) return;
            var p = new Rect(W - 470, 80, 460, H - 80 - 62);
            UI.Rounded(p, UI.PanelColor);
            Block(p);
            UI.Label(new Rect(p.x + 18, p.y + 8, p.width - 36, 30), placing ? "PLACING" : "SELECTED", 16, UI.Accent2, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Label(new Rect(p.x + 18, p.y + 34, p.width - 36, 36), CustomMapKit.DisplayName(o), 26, UI.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);

            var view = new Rect(p.x + 10, p.y + 78, p.width - 20, p.height - 86);
            float s = UI.BeginScroll("ed_inspector", view, inspectorContent);
            float w = view.width - 12;
            float y = -s;

            if (!placing)
            {
                UI.Label(new Rect(8, y, w, 30), string.Format("Position  x {0:0.##}   y {1:0.##}   z {2:0.##}", o.Px, o.Py, o.Pz), 18, UI.Muted);
                y += 34;
            }

            // rotation
            bool shape = MapEditor.IsShape(o.K);
            RotRow(ed, "Turn", o.Ry, 1, ref y, w);
            if (shape)
            {
                RotRow(ed, "Tilt", o.Rx, 0, ref y, w);
                RotRow(ed, "Roll", o.Rz, 2, ref y, w);
            }

            // size
            if (shape || (o.K == "item" && o.Id == "spikes"))
            {
                SizeSlider(ed, "Width", o.Sx, v => o2 => o2.Sx = v, ref y, w, 0.1f, 40f);
                if (shape) SizeSlider(ed, "Height", o.Sy, v => o2 => o2.Sy = v, ref y, w, 0.05f, 30f);
                SizeSlider(ed, "Depth", o.Sz, v => o2 => o2.Sz = v, ref y, w, 0.1f, 40f);
            }
            else if (o.K == "prop")
            {
                float v = o.Sx;
                if (UI.Slider(new Rect(0, y, w, 48), "Scale", ref v, 0.25f, 4f, "0.00", null, "x"))
                {
                    v = Mathf.Round(v * 20f) / 20f;
                    ed.Modify(x => { x.Sx = x.Sy = x.Sz = v; }, "scale");
                }
                y += 54;
            }

            if (shape)
            {
                int m = Mathf.Clamp(o.Mat, 0, CustomMapKit.MatNames.Length - 1);
                if (UI.Selector(new Rect(0, y, w, 50), "Material", ref m, CustomMapKit.MatNames)) { int mm = m; ed.Modify(x => x.Mat = mm, "mat"); }
                y += 56;
            }

            if (shape || o.K == "light" || (o.K == "item" && o.Id == "vending"))
            {
                UI.Label(new Rect(8, y, w, 26), o.K == "light" ? "Light colour" : "Colour tint", 18, UI.Muted);
                y += 28;
                var sw = CustomMapKit.Swatches;
                float sz = Mathf.Min(32, (w - 8) / sw.Length - 4);
                for (int i = 0; i < sw.Length; i++)
                {
                    var r = new Rect(8 + i * (sz + 4), y, sz, sz);
                    bool cur = Mathf.Abs(o.Cr - sw[i].r) < 0.01f && Mathf.Abs(o.Cg - sw[i].g) < 0.01f && Mathf.Abs(o.Cb - sw[i].b) < 0.01f;
                    UI.Rounded(r, sw[i], true);
                    if (cur) UI.Outline(r, UI.Accent2, 3);
                    if (UI.Hit(r))
                    {
                        var c = sw[i];
                        ed.Modify(x => { x.Cr = Mathf.Round(c.r * 1000) / 1000; x.Cg = Mathf.Round(c.g * 1000) / 1000; x.Cb = Mathf.Round(c.b * 1000) / 1000; }, "color");
                        AudioManager.Ui("ui_click", 0.5f);
                    }
                }
                y += sz + 12;
            }

            if (o.K == "light")
            {
                float range = o.Range, power = o.Power;
                if (UI.Slider(new Rect(0, y, w, 48), "Range", ref range, 2f, 40f, "0", null, " m")) { float v = Mathf.Round(range * 2) / 2; ed.Modify(x => x.Range = v, "range"); }
                y += 54;
                if (UI.Slider(new Rect(0, y, w, 48), "Brightness", ref power, 0.1f, 6f, "0.0")) { float v = Mathf.Round(power * 20) / 20; ed.Modify(x => x.Power = v, "power"); }
                y += 54;
            }

            if (o.K == "spawn") { UI.Label(new Rect(8, y, w, 60), "Where players start. The arrow shows the facing direction.", 18, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true); y += 64; }
            if (o.K == "npc") { UI.Label(new Rect(8, y, w, 60), "Goredolls spawn here when the map starts (set how many in Settings).", 18, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true); y += 64; }

            if (!placing)
            {
                y += 6;
                float bw = (w - 10) / 2;
                if (UI.Button(new Rect(0, y, bw, 50), "DUPLICATE", ButtonKind.Normal, IconFactory.Get("copy"), 18, true, "Ctrl+D")) ed.DuplicateSelected();
                if (UI.Button(new Rect(bw + 10, y, bw, 50), "DELETE", ButtonKind.Danger, IconFactory.Get("trash"), 18, true, "Del")) ed.DeleteSelected();
                y += 58;
                if (UI.Button(new Rect(0, y, bw, 50), "MOVE (G)", ButtonKind.Ghost, IconFactory.Get("hand"), 18, true, "Follow the mouse, click to drop. Or just drag the object.")) ed.BeginGrabPublic();
                if (UI.Button(new Rect(bw + 10, y, bw, 50), "FOCUS (F)", ButtonKind.Ghost, IconFactory.Get("eye"), 18)) ed.Focus();
                y += 58;
                UI.Label(new Rect(8, y, w, 50), "Arrows: move  PgUp/PgDn: up/down  Shift: x4", 16, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                y += 40;
            }
            inspectorContent = y + s + 10;
            UI.EndScroll("ed_inspector", view, inspectorContent);
        }

        static void RotRow(MapEditor ed, string label, float value, int axis, ref float y, float w)
        {
            UI.Rounded(new Rect(0, y, w, 48), new Color(1, 1, 1, 0.04f));
            UI.Label(new Rect(14, y, 150, 48), label + "  " + Mathf.RoundToInt(value), 20, UI.TextColor);
            float bx = w - 3 * 76;
            if (UI.Button(new Rect(bx, y + 4, 70, 40), "-15", ButtonKind.Ghost, null, 18)) ed.Rotate(-15, axis);
            if (UI.Button(new Rect(bx + 76, y + 4, 70, 40), "+15", ButtonKind.Ghost, null, 18)) ed.Rotate(15, axis);
            if (UI.Button(new Rect(bx + 152, y + 4, 70, 40), "+90", ButtonKind.Ghost, null, 18)) ed.Rotate(90, axis);
            y += 54;
        }

        static void SizeSlider(MapEditor ed, string label, float value, Func<float, Action<CustomMapObj>> setter, ref float y, float w, float min, float max)
        {
            float v = Mathf.Clamp(value, min, max);
            if (UI.Slider(new Rect(0, y, w, 48), label, ref v, min, max, "0.0#", null, " m"))
            {
                v = Mathf.Max(min, Mathf.Round(v * 20f) / 20f);
                ed.Modify(setter(v), "size" + label);
            }
            y += 54;
        }

        // ------------------------------------------------------------------ status bar + help

        void DrawStatus(MapEditor ed, float W, float H)
        {
            var bar = new Rect(0, H - 54, W, 54);
            UI.Fill(bar, new Color(0.04f, 0.04f, 0.05f, 0.85f));
            Block(bar);
            string hint;
            if (Time.unscaledTime - ed.StatusAt < 4f && !string.IsNullOrEmpty(ed.Status)) hint = ed.Status;
            else if (MapEditor.MouseLook) hint = "Flying: mouse to look, WASD move, E/Q or Space/C up/down, Shift faster";
            else if (ed.Grabbing) hint = "Moving: release / click to drop, Esc to cancel";
            else if (ed.Mode == MapEditor.Tool.Place) hint = "LMB place   Q/E or wheel rotate   R +90   +/- size   Esc stop placing   Hold RMB + WASD to fly";
            else if (ed.SelectedObj != null) hint = "Drag to move   G move   Arrows/PgUp/PgDn nudge   Q/E rotate   Ctrl+D duplicate   Del delete";
            else hint = "Pick something in the palette to place it   LMB select   Hold RMB + WASD to fly   H help";
            UI.Label(new Rect(18, bar.y, W * 0.66f, 54), hint, 19, UI.TextColor);
            string info = "Spawns: " + ed.CountKind("spawn") + "   Goredoll spawns: " + ed.CountKind("npc");
            if (ed.CountKind("spawn") == 0) info = "No player spawn placed!   " + info;
            UI.Label(new Rect(W - 620, bar.y, 600, 54), info, 18, ed.CountKind("spawn") == 0 ? UI.Warn : UI.Muted, TextAnchor.MiddleRight);
        }

        static readonly string[,] Help =
        {
            { "Hold RMB + mouse", "Look around" },
            { "WASD (Shift = fast)", "Fly" },
            { "Space / C  (E / Q while RMB)", "Up / down" },
            { "Palette (left)", "Pick an object to place" },
            { "LMB", "Place / select" },
            { "Drag  or  G", "Move the selection" },
            { "Q / E  or  wheel", "Rotate 15 (Shift: 5)" },
            { "R", "Rotate 90" },
            { "+ / -", "Bigger / smaller" },
            { "Arrows, PgUp / PgDn", "Nudge by the grid step" },
            { "Ctrl+D", "Duplicate" },
            { "Del", "Delete" },
            { "Ctrl+Z / Ctrl+Y", "Undo / redo" },
            { "Tab", "Grid snapping on/off" },
            { "F", "Focus the selection" },
            { "1 / 2", "Select tool / place tool" },
            { "Ctrl+S", "Save" },
            { "Ctrl+P", "Test play (Esc returns)" },
            { "Esc", "Stop placing / deselect / menu" },
            { "H or F1", "This help" },
        };

        void DrawHelp(MapEditor ed)
        {
            var r = UI.Centered(1040, 700);
            Block(r);
            UI.Panel(r, "Map editor controls");
            int n = Help.GetLength(0);
            int half = (n + 1) / 2;
            for (int i = 0; i < n; i++)
            {
                float cx = r.x + 30 + (i >= half ? 500 : 0);
                float cy = r.y + 84 + (i % half) * 50;
                UI.Rounded(new Rect(cx, cy, 480, 44), new Color(1, 1, 1, 0.04f), true);
                UI.Label(new Rect(cx + 12, cy, 250, 44), Help[i, 0], 19, UI.Accent2, TextAnchor.MiddleLeft, FontStyle.Bold);
                UI.Label(new Rect(cx + 262, cy, 214, 44), Help[i, 1], 19, UI.TextColor);
            }
            UI.Label(new Rect(r.x + 30, r.yMax - 76, r.width - 300, 60), "Tip: place a Player Spawn and some Goredoll Spawns, then Test Play. Saved maps show up in Play with a Custom tag and can be hosted online.", 17, UI.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, true, true);
            if (UI.Button(new Rect(r.xMax - 230, r.yMax - 76, 200, 56), "GOT IT", ButtonKind.Primary)) ed.ShowHelp = false;
        }
    }

    // ====================================================================== Esc menu inside the editor

    public sealed class MapEditorMenu : UIScreen
    {
        public override bool IsModal => true;

        public override void Draw()
        {
            var ed = MapEditor.Current;
            if (ed == null) { UIRoot.Pop(); return; }
            float a = Intro(0, 0.2f);
            UI.Dim(0.5f * a);
            var r = UI.Centered(460, 520);
            UI.Panel(r, "Map editor", a);
            float y = r.y + 86, x = r.x + 30, w = r.width - 60;
            if (UI.Button(new Rect(x, y, w, 58), "RESUME", ButtonKind.Primary, IconFactory.Get("play"))) UIRoot.Pop();
            y += 68;
            if (UI.Button(new Rect(x, y, w, 58), ed.Dirty ? "SAVE*" : "SAVE", ButtonKind.Normal, IconFactory.Get("save"))) { ed.Save(); UIRoot.Pop(); }
            y += 68;
            if (UI.Button(new Rect(x, y, w, 58), "TEST PLAY", ButtonKind.Normal, IconFactory.Get("run"))) { UIRoot.Pop(); ed.TestPlay(); return; }
            y += 68;
            if (UI.Button(new Rect(x, y, w, 58), "MAP SETTINGS", ButtonKind.Normal, IconFactory.Get("gear"))) { UIRoot.Pop(); UIRoot.Push(new MapSettingsDialog()); return; }
            y += 68;
            if (UI.Button(new Rect(x, y, w, 58), "CONTROLS", ButtonKind.Normal, IconFactory.Get("keyboard"))) { UIRoot.Pop(); ed.ShowHelp = true; return; }
            y += 68;
            if (UI.Button(new Rect(x, y, w, 58), "EXIT EDITOR", ButtonKind.Danger, IconFactory.Get("exit"))) { UIRoot.Pop(); MapEditorScreen.AskExit(ed); }
        }
    }

    public sealed class EditorExitDialog : UIScreen
    {
        public override bool IsModal => true;

        public override void Draw()
        {
            var ed = MapEditor.Current;
            if (ed == null) { UIRoot.Pop(); return; }
            float a = Intro(0, 0.2f);
            UI.Dim(0.55f * a);
            var r = UI.Centered(760, 280);
            UI.Panel(r, "Unsaved changes", a);
            UI.Label(new Rect(r.x + 30, r.y + 84, r.width - 60, 80), "Save \"" + ed.Data.Name + "\" before leaving the editor?", 24, UI.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            float bw = (r.width - 80) / 3;
            if (UI.Button(new Rect(r.x + 30, r.yMax - 86, bw, 60), "CANCEL")) UIRoot.Pop();
            if (UI.Button(new Rect(r.x + 40 + bw, r.yMax - 86, bw, 60), "DISCARD", ButtonKind.Danger)) { UIRoot.Pop(); ed.Exit(); }
            if (UI.Button(new Rect(r.x + 50 + bw * 2, r.yMax - 86, bw, 60), "SAVE & EXIT", ButtonKind.Primary)) { UIRoot.Pop(); if (ed.Save() == null) ed.Exit(); }
        }
    }

    // ====================================================================== map settings

    public sealed class MapSettingsDialog : UIScreen
    {
        CustomMapData before;
        string beforeJson;
        static readonly string[] FogNames = { "Default", "Off", "Thin", "Thick" };
        static string[] npcNames, groundMats;

        public override bool IsModal => true;

        public override void OnOpen()
        {
            var ed = MapEditor.Current;
            if (ed == null) return;
            before = ed.Data.Clone();
            beforeJson = before.ToJson();
            if (npcNames == null)
            {
                npcNames = new string[22];
                npcNames[0] = "Map select";
                for (int i = 0; i <= 20; i++) npcNames[i + 1] = i.ToString();
            }
            if (groundMats == null)
            {
                groundMats = new string[CustomMapKit.MatGlass];
                Array.Copy(CustomMapKit.MatNames, groundMats, groundMats.Length);
            }
        }

        public override void OnClose()
        {
            GUIUtility.keyboardControl = 0;
            var ed = MapEditor.Current;
            if (ed == null || before == null) return;
            if (ed.Data.ToJson() != beforeJson) { ed.AddUndoSnapshot(before); ed.Say("Map settings changed."); }
        }

        public override void Draw()
        {
            var ed = MapEditor.Current;
            if (ed == null) { UIRoot.Pop(); return; }
            var d = ed.Data;
            float a = Intro(0, 0.2f);
            UI.Dim(0.45f * a);
            var r = UI.Centered(900, Mathf.Min(UI.VH - 40, 860));
            UI.Panel(r, "Map settings", a);
            float x = r.x + 30, w = r.width - 60, y = r.y + 84;
            float half = (w - 20) / 2;

            UI.Label(new Rect(x, y, 200, 50), "Name", 22, UI.TextColor);
            var nn = UI.TextField(new Rect(x + 180, y, w - 180, 50), d.Name, "ed_name");
            if (nn != d.Name) d.Name = nn.Length > 40 ? nn.Substring(0, 40) : nn;
            y += 60;
            UI.Label(new Rect(x, y, 200, 50), "Description", 22, UI.TextColor);
            var nd = UI.TextField(new Rect(x + 180, y, w - 180, 50), d.Description, "ed_desc", false, "Shown on the map select screen");
            if (nd != d.Description) d.Description = nd.Length > 300 ? nd.Substring(0, 300) : nd;
            y += 70;

            int t = d.Time;
            if (UI.Selector(new Rect(x, y, half, 54), "Time of day", ref t, MapOptions.Times)) { d.Time = t; ed.ApplyLighting(); }
            int f = d.Fog;
            if (UI.Selector(new Rect(x + half + 20, y, half, 54), "Sky fog", ref f, FogNames)) { d.Fog = f; ed.ApplyLighting(); }
            y += 64;
            bool indoor = d.Indoor;
            if (UI.Toggle(new Rect(x, y, half, 54), "Indoor lighting", ref indoor, "Dimmer sun, for maps with a roof.")) { d.Indoor = indoor; ed.ApplyLighting(); }
            bool walls = d.Walls;
            if (UI.Toggle(new Rect(x + half + 20, y, half, 54), "Boundary walls", ref walls, "Invisible walls around the area you built, so nobody falls off the world.")) d.Walls = walls;
            y += 64;
            bool ground = d.Ground;
            if (UI.Toggle(new Rect(x, y, half, 54), "Ground plane", ref ground)) { d.Ground = ground; ed.RebuildGround(); }
            int gm = Mathf.Clamp(d.GroundMat, 0, groundMats.Length - 1);
            if (UI.Selector(new Rect(x + half + 20, y, half, 54), "Ground", ref gm, groundMats)) { d.GroundMat = gm; ed.RebuildGround(); }
            y += 64;
            float gs = d.GroundSize;
            if (UI.Slider(new Rect(x, y, w, 54), "Ground size", ref gs, 20f, 400f, "0", null, " m")) { d.GroundSize = Mathf.Round(gs / 5f) * 5f; ed.RebuildGround(); }
            y += 64;
            int npc = Mathf.Clamp(d.Npcs + 1, 0, npcNames.Length - 1);
            if (UI.Selector(new Rect(x, y, w, 54), "Starting Goredolls", ref npc, npcNames, "How many Goredolls spawn at the start. 'Map select' uses the slider on the map select screen.")) d.Npcs = npc - 1;
            y += 70;
            UI.Label(new Rect(x, y, w, 60), "Goredolls appear at your Goredoll Spawn points (or near the player if there are none). The AI walks on anything you build.", 18, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);

            if (UI.Button(new Rect(r.xMax - 250, r.yMax - 80, 220, 58), "DONE", ButtonKind.Primary)) UIRoot.Pop();
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return) { Event.current.Use(); UIRoot.Pop(); }
        }

        public override bool OnBack() { UIRoot.Pop(); return true; }
    }

    // ====================================================================== text prompt

    public sealed class TextPromptDialog : UIScreen
    {
        readonly string title, label, ok;
        readonly Action<string> onOk;
        string value;
        bool focused;

        public TextPromptDialog(string title, string label, string initial, string ok, Action<string> onOk)
        { this.title = title; this.label = label; value = initial ?? ""; this.ok = ok; this.onOk = onOk; }

        public override bool IsModal => true;

        public override void OnClose() { GUIUtility.keyboardControl = 0; }

        public override void Draw()
        {
            float a = Intro(0, 0.2f);
            UI.Dim(0.55f * a);
            var r = UI.Centered(680, 300);
            UI.Panel(r, title, a);
            UI.Label(new Rect(r.x + 30, r.y + 78, r.width - 60, 34), label, 20, UI.Muted);
            value = UI.TextField(new Rect(r.x + 30, r.y + 114, r.width - 60, 54), value, "prompt_field");
            if (value.Length > 40) value = value.Substring(0, 40);
            if (!focused) { GUI.FocusControl("prompt_field"); focused = true; }
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            if (UI.Button(new Rect(r.x + 30, r.yMax - 86, 240, 60), "CANCEL")) UIRoot.Pop();
            bool valid = CustomMapData.CleanText(value, 40).Length > 0;
            if ((UI.Button(new Rect(r.xMax - 270, r.yMax - 86, 240, 60), ok, ButtonKind.Primary, null, 24, valid) || (enter && valid)))
            {
                if (enter) Event.current.Use();
                UIRoot.Pop();
                onOk?.Invoke(CustomMapData.CleanText(value, 40));
            }
        }
    }

    // ====================================================================== hub: list of your maps

    public sealed class MapEditorHubScreen : UIScreen
    {
        int selected;

        public override void OnOpen()
        {
            AudioManager.Ui("ui_open");
            CustomMaps.Refresh();
            selected = 0;
        }

        public override bool OnBack() { AudioManager.Ui("ui_close"); UIRoot.Pop(); return true; }

        public override void Draw()
        {
            float W = UI.VW, H = UI.VH;
            float a = Intro(0, 0.3f);
            UI.Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.6f * a));
            UI.Label(new Rect(70, 40, 900, 80), "MAP EDITOR", 64, UI.TextColor.WithAlpha(a), TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
            UI.Label(new Rect(74, 108, W - 150, 36), "Build your own maps, test them, play them and host them online.", 22, UI.Muted.WithAlpha(a));

            var maps = CustomMaps.Local;
            if (selected >= maps.Count) selected = maps.Count - 1;
            var list = new Rect(70, 170, 560, H - 290);
            float y0 = 0;
            if (UI.Button(new Rect(list.x, list.y, list.width - 14, 64), "NEW MAP", ButtonKind.Primary, IconFactory.Get("plus"), 26))
                UIRoot.Push(new TextPromptDialog("New map", "Name your map", CustomMaps.UniqueName("My Map"), "CREATE", name =>
                {
                    var d = CustomMaps.NewMap(name);
                    var err = CustomMaps.Save(d);
                    if (err != null) GameEvents.ShowToast("Couldn't create the map file: " + err);
                    MapEditor.Open(d.Clone());
                }));
            y0 += 74;
            var pending = MapEditor.PendingSession;
            if (pending != null && MapEditor.PendingDirty)
            {
                if (UI.Button(new Rect(list.x, list.y + y0, list.width - 14, 56), "RESUME UNSAVED: " + pending.Name, ButtonKind.Normal, IconFactory.Get("undo"), 20))
                    MapEditor.Open(pending, true);
                y0 += 66;
            }
            var view = new Rect(list.x, list.y + y0, list.width, list.height - y0);
            float itemH = 92;
            float content = Mathf.Max(1, maps.Count) * (itemH + 10);
            float s = UI.BeginScroll("ed_hub", view, content);
            if (maps.Count == 0)
                UI.Label(new Rect(10, 10, view.width - 30, 80), "No maps yet. Press NEW MAP to start building.", 22, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            for (int i = 0; i < maps.Count; i++)
            {
                var m = maps[i];
                var r = new Rect(0, i * (itemH + 10) - s, view.width - 14, itemH);
                bool sel = i == selected;
                UI.Rounded(r, sel ? new Color(0.5f, 0.06f, 0.08f, 0.95f) : new Color(0.1f, 0.1f, 0.13f, UI.Hover(r) ? 0.98f : 0.9f));
                if (sel) UI.Outline(r, UI.Accent, 2);
                var th = new Rect(r.x + 10, r.y + 10, 128, itemH - 20);
                UI.Tex(th, MapThumbs.Get(m.Def), Color.white, ScaleMode.ScaleAndCrop);
                UI.Label(new Rect(r.x + 152, r.y + 10, r.width - 166, 36), m.Data.Name, 26, UI.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                UI.Label(new Rect(r.x + 152, r.y + 48, r.width - 166, 30), m.Data.Objects.Count + " objects  -  edited " + m.Modified.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), 18, UI.Muted);
                if (UI.Hit(r) && i != selected) { selected = i; AudioManager.Ui("ui_tab"); }
            }
            UI.EndScroll("ed_hub", view, content);

            // details + actions
            var d = new Rect(670, 170, W - 740, H - 290);
            UI.Panel(d, null, a);
            if (selected >= 0 && selected < maps.Count)
            {
                var e = maps[selected];
                var hero = new Rect(d.x + 24, d.y + 24, d.width - 48, Mathf.Min(260, (d.width - 48) * 0.45f));
                UI.Tex(hero, MapThumbs.Get(e.Def), Color.white, ScaleMode.ScaleAndCrop);
                UI.Label(new Rect(hero.x + 20, hero.yMax - 70, hero.width, 60), e.Data.Name.ToUpperInvariant(), 42, UI.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
                float y = hero.yMax + 16;
                UI.Label(new Rect(d.x + 28, y, d.width - 56, 80), e.Def.Description, 21, UI.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                y += 84;
                UI.Label(new Rect(d.x + 28, y, d.width - 56, 30), string.Join("   ", e.Def.Features), 18, UI.Accent2);
                y += 50;
                float bw = (d.width - 56 - 30) / 4;
                float bx = d.x + 28;
                if (UI.Button(new Rect(bx, y, bw, 60), "EDIT", ButtonKind.Primary, IconFactory.Get("wrench"), 22)) { MapEditor.Open(e.Data.Clone()); return; }
                if (UI.Button(new Rect(bx + (bw + 10), y, bw, 60), "PLAY", ButtonKind.Normal, IconFactory.Get("play"), 22))
                {
                    GameManager.I.StartMap(e.Def, new MapOptions { Time = e.Def.DefaultTime, StartingNpcs = 3 });
                    return;
                }
                if (UI.Button(new Rect(bx + (bw + 10) * 2, y, bw, 60), "RENAME", ButtonKind.Normal, null, 22))
                {
                    var data = e.Data.Clone();
                    UIRoot.Push(new TextPromptDialog("Rename map", "New name", data.Name, "RENAME", n => { data.Name = n; var err = CustomMaps.Save(data); if (err != null) GameEvents.ShowToast("Rename failed: " + err); }));
                }
                if (UI.Button(new Rect(bx + (bw + 10) * 3, y, bw, 60), "DELETE", ButtonKind.Danger, IconFactory.Get("trash"), 22))
                    UIRoot.Push(new ConfirmDialog("Delete map?", "\"" + e.Data.Name + "\" will be gone for good.", () => CustomMaps.Delete(e), "DELETE", ButtonKind.Danger));
                y += 76;
                if (UI.Button(new Rect(bx, y, bw * 2 + 10, 54), "DUPLICATE", ButtonKind.Ghost, IconFactory.Get("copy"), 20))
                {
                    var copy = e.Data.Clone();
                    copy.Id = CustomMapData.NewId();
                    copy.Name = CustomMaps.UniqueName(e.Data.Name + " copy");
                    CustomMaps.Save(copy);
                    selected = 0;
                }
                UI.Label(new Rect(d.x + 28, d.yMax - 60, d.width - 56, 40), "Saved maps also appear in PLAY (tagged Custom) and can be hosted in multiplayer.", 17, UI.Muted);
            }
            else UI.Label(new Rect(d.x + 30, d.y + 30, d.width - 60, 80), "Create a map to get started.", 24, UI.Muted);

            UI.Label(new Rect(330, H - 100, W - 400, 64), "Folder: " + CustomMaps.Dir, 15, UI.Muted.WithAlpha(0.7f));
            if (UI.Button(new Rect(70, H - 100, 240, 64), "BACK", ButtonKind.Normal, IconFactory.Get("arrow_left"))) OnBack();
        }
    }
}
