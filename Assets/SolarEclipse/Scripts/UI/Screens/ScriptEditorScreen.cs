using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse.Mods
{
    /// <summary>
    /// Touch-friendly SandScript editor. Line mode: tap a line to edit it, big buttons to add / delete / indent / move
    /// lines, and a command palette that inserts ready-made lines (no typing needed). Text mode: one multi-line box.
    /// The script is checked live; errors show with their line number and the line is highlighted.
    /// </summary>
    public sealed class ScriptEditorScreen : UIScreen
    {
        readonly string title;
        readonly ScriptTarget target;
        readonly Action<string> onDone;
        readonly List<string> lines = new List<string>();
        int sel;
        bool textMode;
        string text;
        int category;
        string error;
        int errorLine;
        float nextCheck;
        bool dirty = true;
        static readonly string[] Cats = { "EVENTS", "LOGIC", "MOVE", "FIGHT", "TALK", "WEAPON", "ASK" };

        public ScriptEditorScreen(string title, string source, ScriptTarget target, Action<string> onDone)
        {
            this.title = title; this.target = target; this.onDone = onDone;
            SetText(source ?? "");
            if (target == ScriptTarget.Weapon) category = 5;
        }

        void SetText(string s)
        {
            lines.Clear();
            lines.AddRange(s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
            while (lines.Count > 1 && lines[lines.Count - 1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
            if (lines.Count == 0) lines.Add("");
            sel = Mathf.Clamp(sel, 0, lines.Count - 1);
            dirty = true;
        }

        string Source => string.Join("\n", lines.ToArray()) + "\n";

        void Check()
        {
            dirty = false;
            error = null; errorLine = -1;
            try { SandProgram.Parse(Source, target); }
            catch (SandError e) { error = e.Pretty; errorLine = e.Line - 1; }
            catch (Exception e) { error = e.Message; }
        }

        public override bool OnBack() { Finish(); return true; }

        void Finish()
        {
            if (textMode) SetText(text);
            UIRoot.Pop();
            onDone?.Invoke(Source);
        }

        static int IndentOf(string l) { int n = 0; foreach (char c in l) { if (c == ' ') n++; else if (c == '\t') n += 4; else break; } return n; }

        void Insert(string snippet)
        {
            // Indent: one level deeper after a line ending with ':', events always at the left edge.
            string cur = lines.Count > 0 ? lines[sel] : "";
            int indent = IndentOf(cur);
            if (cur.TrimEnd().EndsWith(":")) indent += 2;
            if (cur.Trim().Length == 0 && sel > 0) { indent = IndentOf(lines[sel - 1]); if (lines[sel - 1].TrimEnd().EndsWith(":")) indent += 2; }
            bool isEvent = snippet.StartsWith("on ") || snippet.StartsWith("every ");
            if (isEvent) indent = 0;
            var parts = snippet.Split('\n');
            int at = cur.Trim().Length == 0 ? sel : sel + 1;
            if (cur.Trim().Length == 0) lines.RemoveAt(sel);
            if (isEvent && at > 0 && lines.Count > 0 && at <= lines.Count && lines[at - 1].Trim().Length > 0) { lines.Insert(at, ""); at++; }
            for (int i = 0; i < parts.Length; i++)
            {
                int extra = parts[i].StartsWith("  ") ? 0 : 0;
                lines.Insert(Mathf.Min(at + i, lines.Count), new string(' ', indent + extra) + parts[i]);
            }
            sel = Mathf.Clamp(at + parts.Length - 1, 0, lines.Count - 1);
            if (lines.Count > SandApi.MaxLines) lines.RemoveRange(SandApi.MaxLines, lines.Count - SandApi.MaxLines);
            dirty = true;
            AudioManager.Ui("ui_click");
        }

        List<KeyValuePair<string, string>> Palette()
        {
            var list = new List<KeyValuePair<string, string>>();
            void Add(string label, string snippet) => list.Add(new KeyValuePair<string, string>(label, snippet));
            switch (category)
            {
                case 0:
                    foreach (var e in SandApi.Events)
                        if (SandApi.Allowed(target, e.Target)) Add(e.Example.TrimEnd(':'), e.Example);
                    break;
                case 1:
                    Add("if ... :", "if health < 50:\n  say \"Ouch\"");
                    Add("if / else", "if chance 50:\n  say \"Heads\"\nelse:\n  say \"Tails\"");
                    Add("repeat N", "repeat 3:\n  say \"Hey\"\n  wait 1");
                    Add("wait", "wait 1");
                    Add("set variable", "set count = count + 1");
                    if (target == ScriptTarget.Npc) { Add("set speed", "set speed 5"); Add("set team", "set team hostile"); Add("set color", "set color red"); Add("set max_health", "set max_health 200"); }
                    Add("while", "while has_target:\n  wait 0.5");
                    Add("return (stop event)", "return");
                    Add("print (log)", "print health");
                    break;
                default:
                    {
                        string cat = category == 2 ? "Move" : category == 3 ? "Fight" : category == 4 ? "Talk" : category == 5 ? "Weapon" : null;
                        foreach (var c in SandApi.Commands)
                        {
                            if (!SandApi.Allowed(target, c.Target)) continue;
                            if (cat != null ? (c.Category != cat || c.IsFunc) : !c.IsFunc) continue;
                            string snip = c.IsFunc ? (c.Example.StartsWith("if ") || c.Example.StartsWith("set ") ? c.Example : "set x = " + c.Example) : c.Example;
                            if (snip.EndsWith(":")) snip += "\n  say \"!\"";
                            Add(c.Name, snip);
                        }
                        break;
                    }
            }
            return list;
        }

        public override void Draw()
        {
            if (dirty && Time.unscaledTime >= nextCheck) { nextCheck = Time.unscaledTime + 0.25f; Check(); }
            UI.Fill(new Rect(0, 0, UI.VW, UI.VH), new Color(0.03f, 0.03f, 0.04f, 0.97f));
            float W = UI.VW, H = UI.VH;
            UI.Label(new Rect(30, 14, W - 700, 56), title.ToUpperInvariant(), 34, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
            if (UI.Button(new Rect(W - 250, 14, 220, 60), "DONE", ButtonKind.Primary, IconFactory.Get("check"), 24)) { Finish(); return; }
            if (UI.Button(new Rect(W - 520, 14, 250, 60), textMode ? "LINE MODE" : "TEXT MODE", ButtonKind.Ghost, null, 22))
            {
                if (textMode) SetText(text); else text = Source;
                textMode = !textMode;
            }
            // status
            var status = new Rect(30, 80, W - 60, 44);
            UI.Rounded(status, error == null ? new Color(0.1f, 0.3f, 0.12f, 0.9f) : new Color(0.4f, 0.08f, 0.08f, 0.9f), true);
            UI.Label(new Rect(status.x + 14, status.y, status.width - 28, status.height), error == null ? "Script OK  -  " + lines.Count + " lines" : error, 20, Color.white);

            float top = 136;
            float paletteW = Mathf.Min(560, W * 0.38f);
            var editor = new Rect(30, top, W - paletteW - 80, H - top - 20);
            var pal = new Rect(editor.xMax + 20, top, paletteW, H - top - 20);

            if (textMode)
            {
                var r = UI.R(editor);
                var st = new GUIStyle(GUI.skin.textArea) { fontSize = UI.FontPx(22), wordWrap = false, font = UI.BuiltinFont };
                var nt = GUI.TextArea(r, text ?? "", SandApi.MaxSourceBytes, st);
                if (nt != text) { text = nt; SetTextKeepMode(nt); }
            }
            else DrawLines(editor);
            DrawPalette(pal);
        }

        void SetTextKeepMode(string s)
        {
            var saveSel = sel;
            SetText(s);
            sel = Mathf.Clamp(saveSel, 0, lines.Count - 1);
        }

        void DrawLines(Rect area)
        {
            const float rowH = 54, toolsH = 140;
            var view = new Rect(area.x, area.y, area.width, area.height - toolsH - 10);
            UI.Rounded(view, new Color(0.08f, 0.08f, 0.1f, 1f));
            float content = lines.Count * rowH + 20;
            float s = UI.BeginScroll("sse_lines", view, content);
            for (int i = 0; i < lines.Count; i++)
            {
                var r = new Rect(0, 10 + i * rowH - s, view.width - 14, rowH - 4);
                if (r.yMax < 0 || r.y > view.height) continue;
                bool selected = i == sel;
                var bg = i == errorLine ? new Color(0.45f, 0.1f, 0.1f, 0.8f) : selected ? new Color(0.22f, 0.22f, 0.3f, 1f) : new Color(1, 1, 1, 0.03f);
                UI.Rounded(r, bg, true);
                UI.Label(new Rect(r.x + 8, r.y, 54, r.height), (i + 1).ToString(), 18, UI.Muted, TextAnchor.MiddleRight);
                int ind = IndentOf(lines[i]);
                var textRect = new Rect(r.x + 74 + ind * 12, r.y + 2, r.width - 84 - ind * 12, r.height - 4);
                if (selected)
                {
                    var nv = UI.TextField(textRect, lines[i].TrimStart(), "sse_line");
                    if (nv != lines[i].TrimStart()) { lines[i] = new string(' ', ind) + nv.Replace("\t", "  "); dirty = true; }
                }
                else
                {
                    var t = lines[i].TrimStart();
                    var col = t.StartsWith("#") ? UI.Muted : (t.StartsWith("on ") || t.StartsWith("every ")) ? UI.Accent2 : Color.white;
                    UI.Label(new Rect(textRect.x + 14, textRect.y, textRect.width, textRect.height), t, 22, col);
                    if (UI.Hit(r)) { sel = i; GUI.FocusControl(null); }
                }
            }
            UI.EndScroll("sse_lines", view, content);

            // line tools (two rows of big buttons)
            float y = view.yMax + 10, bw = (area.width - 50) / 6f;
            int b = 0;
            Rect Btn() => new Rect(area.x + (b++) * (bw + 10), y, bw, 60);
            if (UI.Button(Btn(), "+ LINE", ButtonKind.Normal, null, 20)) { lines.Insert(sel + 1, new string(' ', IndentOf(lines[sel]) + (lines[sel].TrimEnd().EndsWith(":") ? 2 : 0))); sel++; dirty = true; }
            if (UI.Button(Btn(), "DELETE", ButtonKind.Danger, null, 20)) { if (lines.Count > 1) { lines.RemoveAt(sel); sel = Mathf.Clamp(sel, 0, lines.Count - 1); } else lines[0] = ""; dirty = true; }
            if (UI.Button(Btn(), "INDENT >", ButtonKind.Normal, null, 20)) { lines[sel] = "  " + lines[sel]; dirty = true; }
            if (UI.Button(Btn(), "< OUTDENT", ButtonKind.Normal, null, 20)) { if (lines[sel].StartsWith("  ")) lines[sel] = lines[sel].Substring(2); else lines[sel] = lines[sel].TrimStart(); dirty = true; }
            if (UI.Button(Btn(), "UP", ButtonKind.Ghost, null, 20) && sel > 0) { var t = lines[sel]; lines[sel] = lines[sel - 1]; lines[sel - 1] = t; sel--; dirty = true; }
            if (UI.Button(Btn(), "DOWN", ButtonKind.Ghost, null, 20) && sel < lines.Count - 1) { var t = lines[sel]; lines[sel] = lines[sel + 1]; lines[sel + 1] = t; sel++; dirty = true; }
            y += 70; b = 0;
            if (UI.Button(Btn(), "PREV", ButtonKind.Ghost, null, 20) && sel > 0) { sel--; GUI.FocusControl(null); }
            if (UI.Button(Btn(), "NEXT", ButtonKind.Ghost, null, 20) && sel < lines.Count - 1) { sel++; GUI.FocusControl(null); }
            if (UI.Button(Btn(), "\"TEXT\"", ButtonKind.Ghost, null, 20)) { lines[sel] += " \"\""; dirty = true; }
            if (UI.Button(Btn(), "ADD :", ButtonKind.Ghost, null, 20)) { if (!lines[sel].TrimEnd().EndsWith(":")) lines[sel] = lines[sel].TrimEnd() + ":"; dirty = true; }
            if (errorLine >= 0 && UI.Button(Btn(), "GO TO ERROR", ButtonKind.Danger, null, 18)) { sel = Mathf.Clamp(errorLine, 0, lines.Count - 1); UI.SetScroll("sse_lines", Mathf.Max(0, sel * rowH - 100)); }
            else b++;
            if (UI.Button(Btn(), "CLEAR ALL", ButtonKind.Ghost, null, 18))
                UIRoot.Push(new ConfirmDialog("CLEAR SCRIPT", "Delete every line of this script?", () => { SetText(""); sel = 0; }, "CLEAR", ButtonKind.Danger));
        }

        void DrawPalette(Rect area)
        {
            UI.Rounded(area, new Color(0.08f, 0.08f, 0.1f, 1f));
            UI.Label(new Rect(area.x + 16, area.y + 6, area.width - 32, 34), "TAP TO INSERT BELOW THE SELECTED LINE", 16, UI.Muted);
            float tabW = (area.width - 20) / 4f;
            for (int i = 0; i < Cats.Length; i++)
            {
                if (target == ScriptTarget.Weapon && (i == 2 || i == 3)) continue;
                if (target == ScriptTarget.Npc && i == 5) continue;
                var r = new Rect(area.x + 10 + (i % 4) * tabW, area.y + 44 + (i / 4) * 54, tabW - 6, 48);
                if (UI.Button(r, Cats[i], ButtonKind.Tab, null, 16, true, null, category == i)) { category = i; UI.ResetScroll("sse_pal"); }
            }
            var items = Palette();
            var view = new Rect(area.x + 10, area.y + 160, area.width - 20, area.height - 170);
            const float rowH = 76;
            float content = items.Count * rowH + 10;
            float s = UI.BeginScroll("sse_pal", view, content);
            for (int i = 0; i < items.Count; i++)
            {
                var r = new Rect(0, i * rowH - s, view.width - 12, rowH - 8);
                if (r.yMax < 0 || r.y > view.height) continue;
                var help = Help(items[i].Key);
                if (UI.Button(r, "", ButtonKind.Normal, null, 18)) Insert(items[i].Value);
                UI.Label(new Rect(r.x + 14, r.y + 4, r.width - 28, 32), items[i].Key, 22, Color.white);
                UI.Label(new Rect(r.x + 14, r.y + 36, r.width - 28, 28), help, 15, UI.Muted);
            }
            UI.EndScroll("sse_pal", view, content);
        }

        static string Help(string key)
        {
            var c = SandApi.FindCmd(key);
            if (c != null) return c.Help;
            foreach (var e in SandApi.Events) if (e.Example.TrimEnd(':') == key) return e.Help;
            return "";
        }
    }
}
