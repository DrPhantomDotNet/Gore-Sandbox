using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Generated icons: vector-style glyphs drawn in code (white, tint when drawing) and
    /// 3D "photo" icons rendered from procedural models (items, NPCs, props).
    /// </summary>
    public static class IconFactory
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Action<PixelCanvas>> glyphs = new Dictionary<string, Action<PixelCanvas>>();
        static readonly Color W = Color.white;
        const float L = 10f; // stroke width

        public static int GlyphCount { get { Define(); return glyphs.Count; } }
        public static int RenderedCount { get; private set; }
        public static IEnumerable<string> GlyphNames { get { Define(); return glyphs.Keys; } }

        public static Texture2D Get(string name)
        {
            if (cache.TryGetValue(name, out var t) && t != null) return t;
            Define();
            var c = new PixelCanvas(128, 128, new Color(1, 1, 1, 0));
            if (glyphs.TryGetValue(name, out var draw)) draw(c);
            else { c.Circle(64, 64, 40, W, 2); c.Circle(64, 64, 26, new Color(0, 0, 0, 1), 2); }
            t = c.ToTexture("Icon_" + name, true);
            cache[name] = t;
            return t;
        }

        public static void Register(string name, Texture2D tex) { cache[name] = tex; RenderedCount++; }
        public static bool Has(string name) => cache.ContainsKey(name);

        static void G(string name, Action<PixelCanvas> d) => glyphs[name] = d;
        static Vector2 P(float x, float y) => new Vector2(x, y);

        static void Define()
        {
            if (glyphs.Count > 0) return;
            G("play", c => c.Polygon(new[] { P(40, 24), P(40, 104), P(104, 64) }, W));
            G("pause", c => { c.Rect(36, 26, 20, 76, W); c.Rect(72, 26, 20, 76, W); });
            G("stop", c => c.Rect(32, 32, 64, 64, W));
            G("exit", c => { c.Rect(24, 20, 10, 88, W); c.Line(34, 20, 70, 20, L, W); c.Line(34, 108, 70, 108, L, W); c.Line(52, 64, 108, 64, L, W); c.Polygon(new[] { P(96, 44), P(118, 64), P(96, 84) }, W); });
            G("gear", c => { for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; c.Line(64, 64, 64 + Mathf.Cos(a) * 52, 64 + Mathf.Sin(a) * 52, 18, W); } c.Circle(64, 64, 38, W); c.Circle(64, 64, 16, new Color(1, 1, 1, 0), 1, false); Cut(c, 64, 64, 15); });
            G("info", c => { c.Ring(64, 64, 50, L, W); c.Rect(58, 30, 12, 44, W); c.Circle(64, 88, 8, W); });
            G("warning", c => { c.Polygon(new[] { P(64, 112), P(10, 18), P(118, 18) }, W); CutPoly(c, new[] { P(64, 94), P(28, 28), P(100, 28) }); c.Rect(59, 50, 10, 30, W); c.Circle(64, 40, 6, W); });
            G("check", c => { c.Line(22, 64, 52, 34, 14, W); c.Line(52, 34, 108, 96, 14, W); });
            G("cross", c => { c.Line(28, 28, 100, 100, 14, W); c.Line(28, 100, 100, 28, 14, W); });
            G("plus", c => { c.Line(64, 20, 64, 108, 14, W); c.Line(20, 64, 108, 64, 14, W); });
            G("minus", c => c.Line(20, 64, 108, 64, 14, W));
            G("arrow_left", c => { c.Line(28, 64, 108, 64, L, W); c.Polygon(new[] { P(16, 64), P(52, 96), P(52, 32) }, W); });
            G("arrow_right", c => { c.Line(20, 64, 100, 64, L, W); c.Polygon(new[] { P(112, 64), P(76, 96), P(76, 32) }, W); });
            G("arrow_up", c => { c.Line(64, 20, 64, 100, L, W); c.Polygon(new[] { P(64, 112), P(32, 76), P(96, 76) }, W); });
            G("arrow_down", c => { c.Line(64, 28, 64, 108, L, W); c.Polygon(new[] { P(64, 16), P(32, 52), P(96, 52) }, W); });
            G("star", c => c.Polygon(Star(64, 62, 52, 22, 5), W));
            G("trophy", c => { c.Polygon(new[] { P(30, 104), P(98, 104), P(90, 62), P(64, 48), P(38, 62) }, W); c.Ring(28, 86, 14, 7, W); c.Ring(100, 86, 14, 7, W); c.Rect(58, 28, 12, 22, W); c.Rect(40, 16, 48, 12, W); });
            G("medal", c => { c.Line(44, 112, 58, 70, 12, W); c.Line(84, 112, 70, 70, 12, W); c.Circle(64, 50, 30, W); c.Polygon(Star(64, 50, 20, 9, 5), new Color(0, 0, 0, 0.6f)); });
            G("crown", c => c.Polygon(new[] { P(16, 30), P(16, 96), P(42, 66), P(64, 104), P(86, 66), P(112, 96), P(112, 30) }, W));
            G("skull", c => { c.Circle(64, 74, 40, W); c.Rect(42, 26, 44, 30, W); Cut(c, 48, 74, 11); Cut(c, 80, 74, 11); CutPoly(c, new[] { P(64, 64), P(58, 50), P(70, 50) }); for (int i = 0; i < 3; i++) c.Rect(48 + i * 12, 22, 4, 16, new Color(0, 0, 0, 1)); });
            G("heart", c => { c.Circle(44, 76, 26, W); c.Circle(84, 76, 26, W); c.Polygon(new[] { P(20, 68), P(108, 68), P(64, 18) }, W); });
            G("heartbeat", c => { c.Line(10, 64, 40, 64, 8, W); c.Line(40, 64, 52, 100, 8, W); c.Line(52, 100, 66, 24, 8, W); c.Line(66, 24, 78, 80, 8, W); c.Line(78, 80, 88, 64, 8, W); c.Line(88, 64, 118, 64, 8, W); });
            G("drop", c => { c.Circle(64, 50, 34, W); c.Polygon(new[] { P(64, 118), P(32, 58), P(96, 58) }, W); });
            G("bullet", c => { c.Rect(48, 16, 32, 52, W); c.Circle(64, 68, 16, W); c.Polygon(new[] { P(48, 70), P(80, 70), P(64, 112) }, W); c.Rect(44, 12, 40, 8, W); });
            G("knife", c => { c.Polygon(new[] { P(20, 30), P(30, 24), P(96, 92), P(110, 112), P(88, 104) }, W); c.Line(14, 18, 38, 42, 16, W); });
            G("fist", c => { c.Rect(30, 30, 64, 52, W); for (int i = 0; i < 4; i++) c.Circle(38 + i * 16, 84, 10, W); c.Polygon(new[] { P(30, 60), P(18, 76), P(30, 92) }, W); c.Rect(40, 12, 44, 22, W); });
            G("gun", c => { c.Rect(14, 72, 96, 20, W); c.Rect(104, 84, 10, 8, W); c.Polygon(new[] { P(30, 74), P(54, 74), P(48, 24), P(24, 24) }, W); c.Ring(64, 64, 10, 6, W); });
            G("rifle", c => { c.Rect(8, 68, 112, 14, W); c.Rect(40, 82, 40, 8, W); c.Polygon(new[] { P(8, 70), P(30, 70), P(24, 44), P(4, 50) }, W); c.Rect(60, 38, 14, 30, W); c.Polygon(new[] { P(44, 68), P(56, 68), P(50, 44), P(40, 44) }, W); });
            G("smg", c => { c.Rect(20, 64, 80, 24, W); c.Rect(100, 74, 16, 8, W); c.Rect(48, 22, 16, 42, W); c.Polygon(new[] { P(20, 66), P(34, 66), P(30, 40), P(18, 40) }, W); });
            G("shotgun", c => { c.Rect(6, 72, 116, 12, W); c.Rect(30, 60, 70, 12, W); c.Polygon(new[] { P(6, 74), P(28, 74), P(20, 44), P(0, 50) }, W); });
            G("admin", c => { c.Polygon(Star(64, 64, 56, 24, 8), W); c.Circle(64, 64, 18, new Color(0, 0, 0, 0.7f)); });
            G("spawngun", c => { c.Rect(20, 60, 70, 34, W); c.Circle(96, 77, 18, W); Cut(c, 96, 77, 9); c.Rect(36, 22, 18, 40, W); c.Polygon(Star(108, 108, 14, 5, 4), W); });
            // Explosives & heavy melee
            G("rocket", c => { c.Rect(4, 60, 96, 16, W); c.Polygon(new[] { P(100, 54), P(100, 82), P(124, 68) }, W); c.Polygon(new[] { P(0, 52), P(14, 60), P(14, 76), P(0, 84) }, W); c.Rect(30, 38, 12, 22, W); c.Rect(60, 38, 12, 22, W); });
            G("c4", c => { c.Rect(14, 34, 100, 54, W); CutRect(c, 30, 34, 6, 54); CutRect(c, 92, 34, 6, 54); c.Rect(50, 88, 28, 14, W); c.Circle(64, 108, 6, UI.Bad); });
            G("katana", c => { c.Line(14, 14, 44, 44, 12, W); c.Line(34, 54, 54, 34, 8, W); c.Polygon(new[] { P(46, 40), P(52, 46), P(118, 120), P(108, 116) }, W); });
            G("rope", c => { c.Ring(40, 88, 22, 8, W); c.Line(40, 66, 56, 46, 8, W); c.Line(56, 46, 76, 58, 8, W); c.Line(76, 58, 96, 36, 8, W); c.Line(96, 36, 104, 14, 8, W); c.Circle(104, 14, 8, W); });
            G("gravgun", c => { c.Rect(14, 56, 64, 34, W); c.Rect(30, 22, 18, 36, W); c.Circle(88, 73, 14, W); c.Line(84, 60, 112, 44, 8, W); c.Line(84, 86, 112, 102, 8, W); c.Line(96, 73, 122, 73, 8, W); });
            G("crosshair", c => { c.Ring(64, 64, 36, 8, W); c.Line(64, 4, 64, 40, 8, W); c.Line(64, 88, 64, 124, 8, W); c.Line(4, 64, 40, 64, 8, W); c.Line(88, 64, 124, 64, 8, W); });
            G("target", c => { c.Ring(64, 64, 50, 10, W); c.Ring(64, 64, 30, 10, W); c.Circle(64, 64, 10, W); });
            G("lock", c => { c.Rect(28, 16, 72, 56, W); c.Ring(64, 78, 24, 10, W); CutRect(c, 30, 72, 68, 6); c.Rect(30, 72, 10, 10, W); c.Rect(88, 72, 10, 10, W); Cut(c, 64, 46, 8); });
            G("unlock", c => { c.Rect(28, 16, 72, 56, W); c.Ring(84, 84, 22, 10, W); CutRect(c, 60, 66, 50, 18); Cut(c, 64, 46, 8); });
            G("trash", c => { c.Rect(32, 14, 64, 76, W); c.Rect(22, 92, 84, 10, W); c.Rect(50, 102, 28, 10, W); for (int i = 0; i < 3; i++) CutRect(c, 44 + i * 16, 26, 6, 52); });
            G("undo", c => { c.Ring(70, 58, 34, 10, W); CutRect(c, 30, 20, 40, 38); c.Polygon(new[] { P(14, 70), P(50, 70), P(32, 104) }, W); });
            G("refresh", c => { c.Ring(64, 64, 38, 10, W); CutRect(c, 92, 54, 30, 22); c.Polygon(new[] { P(86, 52), P(118, 52), P(102, 80) }, W); });
            G("snow", c => { for (int i = 0; i < 3; i++) { float a = i * Mathf.PI / 3; c.Line(64 - Mathf.Cos(a) * 52, 64 - Mathf.Sin(a) * 52, 64 + Mathf.Cos(a) * 52, 64 + Mathf.Sin(a) * 52, 8, W); } c.Circle(64, 64, 12, W); });
            G("rotate", c => { c.Ring(64, 64, 40, 10, W); CutRect(c, 20, 56, 30, 30); c.Polygon(new[] { P(10, 60), P(44, 60), P(26, 32) }, W); });
            G("magnet", c => { c.Ring(64, 74, 36, 22, W); CutRect(c, 18, 20, 92, 54); c.Rect(17, 26, 22, 48, W); c.Rect(89, 26, 22, 48, W); c.Rect(17, 18, 22, 12, UI.Bad); c.Rect(89, 18, 22, 12, UI.Bad); });
            G("eye", c => { c.Polygon(new[] { P(8, 64), P(40, 94), P(88, 94), P(120, 64), P(88, 34), P(40, 34) }, W); Cut(c, 64, 64, 22); c.Circle(64, 64, 12, W); });
            G("ear", c => { c.Ring(64, 72, 30, 12, W); CutRect(c, 40, 20, 30, 40); c.Line(46, 50, 46, 26, 12, W); });
            G("brain", c => { c.Circle(48, 72, 30, W); c.Circle(80, 72, 30, W); c.Circle(64, 54, 26, W); c.Line(64, 30, 64, 100, 4, new Color(0, 0, 0, 0.7f)); c.Line(36, 80, 52, 70, 4, new Color(0, 0, 0, 0.6f)); c.Line(78, 70, 94, 82, 4, new Color(0, 0, 0, 0.6f)); });
            G("lungs", c => { c.Polygon(new[] { P(56, 96), P(56, 20), P(18, 26), P(22, 76) }, W); c.Polygon(new[] { P(72, 96), P(72, 20), P(110, 26), P(106, 76) }, W); c.Line(64, 118, 64, 92, 10, W); });
            G("stomach", c => { c.Circle(70, 54, 34, W); c.Line(52, 110, 56, 78, 16, W); c.Line(96, 40, 112, 22, 14, W); });
            G("liver", c => c.Polygon(new[] { P(10, 70), P(50, 96), P(118, 88), P(100, 50), P(40, 40) }, W));
            G("kidney", c => { c.Circle(64, 64, 42, W); Cut(c, 96, 64, 18); });
            G("heart_organ", c => { c.Circle(52, 70, 30, W); c.Circle(78, 70, 30, W); c.Polygon(new[] { P(24, 64), P(104, 64), P(70, 16) }, W); c.Rect(58, 92, 12, 28, W); c.Rect(76, 92, 10, 22, W); });
            G("bone", c => { c.Line(32, 32, 96, 96, 18, W); c.Circle(26, 38, 12, W); c.Circle(38, 26, 12, W); c.Circle(102, 90, 12, W); c.Circle(90, 102, 12, W); });
            G("spine", c => { for (int i = 0; i < 6; i++) c.Rect(48, 10 + i * 19, 32, 14, W); c.Line(64, 6, 64, 122, 4, W); });
            G("bandage", c => { c.Line(28, 28, 100, 100, 40, W); for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) Cut(c, 56 + i * 8, 56 + j * 8, 2.5f); });
            G("syringe", c => { c.Line(30, 30, 92, 92, 16, W); c.Line(92, 92, 110, 110, 4, W); c.Line(14, 46, 46, 14, 8, W); c.Line(22, 22, 8, 8, 8, W); });
            G("person", c => { c.Circle(64, 98, 18, W); c.Polygon(new[] { P(28, 14), P(36, 70), P(92, 70), P(100, 14) }, W); });
            G("users", c => { c.Circle(44, 92, 16, W); c.Circle(86, 92, 16, W); c.Polygon(new[] { P(14, 20), P(20, 70), P(68, 70), P(74, 20) }, W); c.Polygon(new[] { P(56, 20), P(62, 70), P(110, 70), P(116, 20) }, W); });
            G("ghost", c => { c.Circle(64, 76, 40, W); c.Rect(24, 24, 80, 52, W); for (int i = 0; i < 4; i++) Cut(c, 34 + i * 20, 22, 9); Cut(c, 50, 80, 8); Cut(c, 78, 80, 8); });
            G("noclip", c => { c.Circle(64, 76, 36, W); c.Rect(28, 30, 72, 46, W); for (int i = 0; i < 4; i++) Cut(c, 37 + i * 18, 28, 8); Cut(c, 50, 80, 7); Cut(c, 78, 80, 7); c.Line(8, 120, 120, 8, 8, UI.Accent2); });
            G("box", c => { c.Polygon(new[] { P(64, 116), P(116, 90), P(64, 64), P(12, 90) }, W); c.Polygon(new[] { P(12, 84), P(60, 60), P(60, 8), P(12, 34) }, new Color(1, 1, 1, 0.8f)); c.Polygon(new[] { P(68, 60), P(116, 84), P(116, 34), P(68, 8) }, new Color(1, 1, 1, 0.6f)); });
            G("barrel", c => { c.Rect(30, 14, 68, 100, W); c.Circle(64, 112, 34, W); c.Rect(26, 40, 76, 8, new Color(0, 0, 0, 0.5f)); c.Rect(26, 78, 76, 8, new Color(0, 0, 0, 0.5f)); });
            G("map", c => { c.Polygon(new[] { P(10, 20), P(10, 100), P(44, 112), P(44, 32) }, W); c.Polygon(new[] { P(50, 32), P(50, 112), P(80, 100), P(80, 20) }, new Color(1, 1, 1, 0.75f)); c.Polygon(new[] { P(86, 20), P(86, 100), P(118, 112), P(118, 32) }, W); });
            G("bolt", c => c.Polygon(new[] { P(72, 120), P(26, 58), P(58, 58), P(46, 8), P(102, 74), P(68, 74) }, W));
            G("clock", c => { c.Ring(64, 64, 50, 10, W); c.Line(64, 64, 64, 98, 8, W); c.Line(64, 64, 88, 52, 8, W); });
            G("stopwatch", c => { c.Ring(64, 58, 44, 10, W); c.Rect(56, 104, 16, 14, W); c.Line(64, 58, 82, 82, 8, W); });
            G("flag", c => { c.Rect(22, 10, 10, 108, W); c.Polygon(new[] { P(32, 116), P(108, 96), P(32, 70) }, W); });
            G("search", c => { c.Ring(54, 74, 32, 10, W); c.Line(78, 50, 112, 16, 14, W); });
            G("code", c => { c.Line(40, 96, 12, 64, 10, W); c.Line(12, 64, 40, 32, 10, W); c.Line(88, 96, 116, 64, 10, W); c.Line(116, 64, 88, 32, 10, W); c.Line(74, 104, 54, 24, 8, W); });
            G("terminal", c => { c.Rect(8, 18, 112, 92, W); CutRect(c, 16, 26, 96, 72); c.Line(28, 82, 50, 62, 8, W); c.Line(50, 62, 28, 42, 8, W); c.Line(58, 40, 92, 40, 8, W); });
            G("chart", c => { c.Rect(16, 14, 8, 100, W); c.Rect(16, 14, 100, 8, W); c.Rect(36, 28, 16, 40, W); c.Rect(62, 28, 16, 66, W); c.Rect(88, 28, 16, 52, W); });
            G("book", c => { c.Polygon(new[] { P(10, 24), P(10, 104), P(60, 96), P(60, 16) }, W); c.Polygon(new[] { P(68, 16), P(68, 96), P(118, 104), P(118, 24) }, W); });
            G("list", c => { for (int i = 0; i < 4; i++) { c.Circle(22, 26 + i * 25, 7, W); c.Rect(38, 22 + i * 25, 76, 9, W); } });
            G("grid", c => { for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) c.Rect(14 + i * 36, 14 + j * 36, 28, 28, W); });
            G("home", c => { c.Polygon(new[] { P(64, 116), P(10, 64), P(118, 64) }, W); c.Rect(26, 12, 76, 56, W); CutRect(c, 54, 12, 20, 32); });
            G("menu", c => { for (int i = 0; i < 3; i++) c.Rect(16, 28 + i * 30, 96, 12, W); });
            G("speaker", c => { c.Rect(14, 46, 26, 36, W); c.Polygon(new[] { P(36, 46), P(36, 82), P(68, 110), P(68, 18) }, W); c.Ring(70, 64, 26, 7, W); c.Ring(70, 64, 46, 7, W); CutRect(c, 30, 0, 40, 128); c.Rect(14, 46, 26, 36, W); c.Polygon(new[] { P(36, 46), P(36, 82), P(68, 110), P(68, 18) }, W); });
            G("mute", c => { c.Rect(14, 46, 26, 36, W); c.Polygon(new[] { P(36, 46), P(36, 82), P(68, 110), P(68, 18) }, W); c.Line(80, 44, 116, 84, 10, W); c.Line(80, 84, 116, 44, 10, W); });
            G("music", c => { c.Circle(38, 30, 16, W); c.Circle(94, 42, 16, W); c.Rect(48, 30, 9, 76, W); c.Rect(104, 42, 9, 70, W); c.Polygon(new[] { P(48, 100), P(113, 116), P(113, 100), P(48, 86) }, W); });
            G("mobile", c => { c.Rect(36, 8, 56, 112, W); CutRect(c, 42, 22, 44, 84); c.Circle(64, 15, 4, new Color(0, 0, 0, 1)); });
            G("keyboard", c => { c.Rect(6, 32, 116, 64, W); for (int i = 0; i < 6; i++) for (int j = 0; j < 3; j++) CutRect(c, 14 + i * 18, 42 + j * 16, 12, 10); });
            G("mouse", c => { c.Circle(64, 76, 34, W); c.Rect(30, 30, 68, 46, W); c.Circle(64, 30, 34, W); c.Line(64, 110, 64, 76, 4, new Color(0, 0, 0, 1)); });
            G("camera", c => { c.Rect(10, 24, 108, 72, W); c.Rect(40, 96, 40, 14, W); Cut(c, 64, 60, 24); c.Circle(64, 60, 14, W); });
            G("sun", c => { c.Circle(64, 64, 26, W); for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; c.Line(64 + Mathf.Cos(a) * 36, 64 + Mathf.Sin(a) * 36, 64 + Mathf.Cos(a) * 56, 64 + Mathf.Sin(a) * 56, 9, W); } });
            G("moon", c => { c.Circle(60, 64, 46, W); Cut(c, 84, 78, 40); });
            G("eclipse", c => { c.Ring(64, 64, 50, 10, W); c.Circle(64, 64, 40, W); Cut(c, 74, 70, 38); });
            G("duck", c => { c.Circle(52, 46, 34, W); c.Circle(80, 86, 22, W); c.Polygon(new[] { P(98, 90), P(124, 84), P(98, 78) }, UI.Accent2); Cut(c, 86, 92, 4); });
            G("fire", c => { c.Polygon(new[] { P(64, 124), P(30, 70), P(34, 34), P(64, 14), P(94, 34), P(98, 70) }, W); c.Polygon(new[] { P(64, 84), P(48, 50), P(64, 26), P(80, 50) }, new Color(0, 0, 0, 0.5f)); });
            G("explosion", c => c.Polygon(Star(64, 64, 60, 26, 10), W));
            G("hand", c => { c.Rect(32, 16, 60, 58, W); for (int i = 0; i < 4; i++) c.Line(38 + i * 16, 70, 38 + i * 16, 112 - Mathf.Abs(i - 1.5f) * 8, 13, W); c.Line(32, 50, 14, 76, 13, W); });
            G("spawn", c => { c.Circle(64, 64, 18, W); for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; c.Line(64 + Mathf.Cos(a) * 28, 64 + Mathf.Sin(a) * 28, 64 + Mathf.Cos(a) * 52, 64 + Mathf.Sin(a) * 52, 8, W); } });
            G("shield", c => c.Polygon(new[] { P(64, 118), P(16, 100), P(20, 50), P(64, 8), P(108, 50), P(112, 100) }, W));
            G("slow", c => { c.Ring(64, 64, 44, 10, W); c.Polygon(new[] { P(50, 84), P(50, 44), P(76, 64) }, W); c.Line(30, 64, 10, 64, 6, W); });
            G("fast", c => { c.Polygon(new[] { P(14, 24), P(14, 104), P(64, 64) }, W); c.Polygon(new[] { P(64, 24), P(64, 104), P(114, 64) }, W); });
            G("gravity", c => { c.Circle(64, 92, 20, W); c.Line(64, 64, 64, 14, 10, W); c.Polygon(new[] { P(64, 4), P(44, 30), P(84, 30) }, W); c.Rect(10, 4, 108, 6, W); });
            G("fps", c => { c.Ring(64, 54, 46, 10, W); CutRect(c, 0, 0, 128, 54); c.Line(64, 54, 90, 90, 8, W); c.Circle(64, 54, 8, W); });
            G("cpu", c => { c.Rect(30, 30, 68, 68, W); CutRect(c, 44, 44, 40, 40); for (int i = 0; i < 4; i++) { c.Rect(36 + i * 16, 10, 6, 20, W); c.Rect(36 + i * 16, 98, 6, 20, W); c.Rect(10, 36 + i * 16, 20, 6, W); c.Rect(98, 36 + i * 16, 20, 6, W); } });
            G("bug", c => { c.Circle(64, 56, 30, W); c.Circle(64, 92, 16, W); for (int i = 0; i < 3; i++) { c.Line(36, 40 + i * 18, 12, 30 + i * 22, 6, W); c.Line(92, 40 + i * 18, 116, 30 + i * 22, 6, W); } });
            G("wrench", c => { c.Line(28, 28, 82, 82, 18, W); c.Ring(92, 92, 20, 12, W); CutRect(c, 96, 96, 30, 30); });
            G("dice", c => { c.Rect(20, 20, 88, 88, W); Cut(c, 42, 86, 8); Cut(c, 86, 42, 8); Cut(c, 64, 64, 8); Cut(c, 42, 42, 8); Cut(c, 86, 86, 8); });
            G("gift", c => { c.Rect(18, 14, 92, 64, W); c.Rect(12, 78, 104, 20, W); CutRect(c, 58, 14, 12, 84); c.Ring(48, 108, 12, 6, W); c.Ring(80, 108, 12, 6, W); });
            G("egg", c => { c.Circle(64, 54, 40, W); c.Circle(64, 74, 32, W); c.Line(30, 60, 98, 60, 5, UI.Accent2); });
            G("bell", c => { c.Polygon(new[] { P(64, 110), P(28, 80), P(22, 30), P(106, 30), P(100, 80) }, W); c.Rect(12, 22, 104, 10, W); c.Circle(64, 16, 10, W); });
            G("radio", c => { c.Rect(12, 20, 104, 70, W); Cut(c, 44, 55, 22); c.Line(30, 90, 90, 118, 6, W); for (int i = 0; i < 3; i++) CutRect(c, 76, 36 + i * 14, 30, 6); });
            G("tv", c => { c.Rect(10, 28, 108, 76, W); CutRect(c, 18, 36, 92, 60); c.Line(50, 120, 64, 104, 6, W); c.Line(78, 120, 64, 104, 6, W); });
            G("door", c => { c.Rect(30, 8, 68, 112, W); CutRect(c, 38, 8, 52, 104); c.Polygon(new[] { P(38, 8), P(38, 112), P(78, 118), P(78, 2) }, W); c.Circle(70, 62, 5, new Color(0, 0, 0, 1)); });
            G("button", c => { c.Rect(16, 16, 96, 30, W); c.Circle(64, 64, 32, UI.Bad); });
            G("light", c => { c.Circle(64, 76, 34, W); c.Rect(46, 22, 36, 24, W); c.Rect(52, 10, 24, 10, W); });
            G("diamond", c => c.Polygon(new[] { P(64, 118), P(14, 72), P(34, 44), P(94, 44), P(114, 72) }, W));
            G("anchor", c => { c.Ring(64, 104, 12, 6, W); c.Line(64, 92, 64, 16, 10, W); c.Ring(64, 46, 40, 10, W); CutRect(c, 10, 46, 108, 70); c.Line(40, 82, 88, 82, 10, W); });
            G("link", c => { c.Ring(44, 64, 26, 10, W); c.Ring(84, 64, 26, 10, W); });
            G("cut", c => { c.Ring(32, 32, 18, 8, W); c.Ring(32, 96, 18, 8, W); c.Line(44, 44, 116, 96, 8, W); c.Line(44, 84, 116, 32, 8, W); });
            G("save", c => { c.Rect(14, 14, 100, 100, W); CutRect(c, 30, 74, 60, 32); CutRect(c, 34, 22, 60, 36); c.Rect(40, 26, 48, 28, W); });
            G("folder", c => { c.Rect(10, 20, 108, 72, W); c.Rect(10, 90, 46, 14, W); });
            G("globe", c => { c.Ring(64, 64, 50, 8, W); c.Line(14, 64, 114, 64, 6, W); c.Ring(64, 64, 22, 6, W); c.Line(64, 14, 64, 114, 6, W); });
            G("headphones", c => { c.Ring(64, 64, 44, 10, W); CutRect(c, 0, 0, 128, 64); c.Rect(14, 24, 22, 44, W); c.Rect(92, 24, 22, 44, W); });
            G("dev", c => { c.Ring(64, 64, 50, 8, W); c.Line(46, 82, 28, 64, 8, W); c.Line(28, 64, 46, 46, 8, W); c.Line(82, 82, 100, 64, 8, W); c.Line(100, 64, 82, 46, 8, W); });
            G("cake", c => { c.Rect(20, 16, 88, 56, W); c.Rect(30, 72, 68, 8, W); for (int i = 0; i < 3; i++) { c.Rect(42 + i * 20, 80, 6, 22, W); c.Circle(45 + i * 20, 108, 5, UI.Accent2); } });
            G("pill", c => { c.Line(36, 36, 92, 92, 36, W); c.Line(64, 64, 92, 92, 36, UI.Accent); });
            G("wind", c => { c.Line(10, 80, 90, 80, 8, W); c.Ring(92, 92, 12, 6, W); c.Line(10, 56, 106, 56, 8, W); c.Line(10, 32, 70, 32, 8, W); c.Ring(70, 20, 12, 6, W); });
            G("cloud", c => { c.Circle(44, 56, 26, W); c.Circle(76, 68, 32, W); c.Circle(100, 52, 20, W); c.Rect(20, 30, 96, 30, W); });
            G("bloodtrail", c => { for (int i = 0; i < 6; i++) c.Circle(16 + i * 20, 30 + i * 12, 8 + i * 1.5f, W); });
            G("wound", c => { c.Circle(64, 64, 46, new Color(1, 1, 1, 0.35f)); c.Line(24, 40, 104, 88, 14, W); });
            G("organ", c => { c.Circle(50, 74, 28, W); c.Circle(80, 70, 30, W); c.Circle(64, 44, 26, W); });
            G("ammo", c => { for (int i = 0; i < 3; i++) { c.Rect(22 + i * 30, 16, 20, 66, W); c.Polygon(new[] { P(22 + i * 30, 82), P(42 + i * 30, 82), P(32 + i * 30, 112) }, W); } });
            G("reload", c => { c.Ring(64, 64, 42, 10, W); CutRect(c, 64, 64, 64, 30); c.Polygon(new[] { P(96, 60), P(124, 60), P(110, 88) }, W); c.Rect(54, 40, 20, 46, W); });
            G("inspect", c => { c.Ring(54, 74, 32, 10, W); c.Line(78, 50, 112, 16, 14, W); c.Line(40, 74, 68, 74, 6, UI.Accent); c.Line(54, 60, 54, 88, 6, UI.Accent); });
            G("jump", c => { c.Polygon(new[] { P(64, 118), P(24, 70), P(48, 70), P(48, 30), P(80, 30), P(80, 70), P(104, 70) }, W); c.Rect(14, 10, 100, 8, W); });
            G("crouch", c => { c.Polygon(new[] { P(64, 14), P(24, 62), P(48, 62), P(48, 102), P(80, 102), P(80, 62), P(104, 62) }, W); c.Rect(14, 110, 100, 8, W); });
            G("sprint", c => { c.Circle(76, 104, 12, W); c.Line(70, 90, 56, 54, 12, W); c.Line(56, 54, 30, 30, 10, W); c.Line(56, 54, 80, 26, 10, W); c.Line(64, 78, 94, 70, 9, W); c.Line(64, 78, 36, 86, 9, W); });
            G("kick", c => { c.Circle(48, 104, 12, W); c.Line(48, 90, 48, 56, 12, W); c.Line(48, 56, 30, 16, 10, W); c.Line(48, 56, 110, 74, 10, W); c.Line(48, 82, 80, 96, 8, W); });
            G("aim", c => { c.Ring(64, 64, 28, 6, W); c.Circle(64, 64, 6, W); c.Line(64, 100, 64, 124, 6, W); c.Line(64, 4, 64, 28, 6, W); c.Line(4, 64, 28, 64, 6, W); c.Line(100, 64, 124, 64, 6, W); });
            G("fire_btn", c => { c.Ring(64, 64, 48, 8, W); c.Polygon(new[] { P(64, 100), P(40, 60), P(56, 60), P(48, 28), P(86, 72), P(68, 72) }, W); });
            G("scale", c => { c.Rect(18, 18, 40, 40, W); c.Ring(80, 80, 30, 8, W); c.Line(54, 54, 108, 108, 6, W); });
            G("freeze", c => { c.Polygon(Star(64, 64, 54, 16, 6), W); Cut(c, 64, 64, 12); });
            G("hint", c => { c.Circle(64, 76, 34, W); c.Rect(50, 24, 28, 28, W); c.Rect(54, 12, 20, 10, W); });
            G("nextbot", c => { c.Rect(20, 20, 88, 88, W); Cut(c, 46, 76, 10); Cut(c, 82, 76, 10); CutRect(c, 40, 34, 48, 14); for (int i = 0; i < 4; i++) c.Rect(42 + i * 12, 34, 6, 14, W); });

            DefineMore();
        }

        /// <summary>Second icon set (controls, gore, world, weather, UI chrome).</summary>
        static void DefineMore()
        {
            var B = new Color(1, 1, 1, 0);
            G("close", c => { c.Line(30, 30, 98, 98, 16, W); c.Line(30, 98, 98, 30, 16, W); });
            G("power", c => { c.Ring(64, 60, 40, 12, W); CutRect(c, 52, 70, 24, 40); c.Rect(58, 60, 12, 54, W); });
            G("key", c => { c.Ring(40, 64, 22, 10, W); c.Rect(60, 58, 56, 12, W); c.Rect(96, 40, 10, 20, W); c.Rect(80, 44, 10, 16, W); });
            G("run", c => { c.Circle(76, 104, 12, W); c.Line(70, 88, 56, 52, 12, W); c.Line(56, 52, 76, 30, 10, W); c.Line(76, 30, 70, 10, 10, W); c.Line(56, 52, 34, 34, 10, W); c.Line(34, 34, 20, 40, 10, W); c.Line(66, 80, 92, 70, 10, W); c.Line(66, 80, 40, 72, 10, W); });
            G("boot", c => { c.Rect(40, 40, 30, 70, W); c.Polygon(new[] { P(40, 40), P(40, 14), P(112, 14), P(112, 30), P(70, 44) }, W); });
            G("tooth", c => { c.Circle(48, 84, 24, W); c.Circle(80, 84, 24, W); c.Polygon(new[] { P(28, 80), P(100, 80), P(88, 16), P(72, 40), P(56, 40), P(40, 16) }, W); });
            G("eye_closed", c => { c.Line(18, 70, 64, 50, 10, W); c.Line(64, 50, 110, 70, 10, W); for (int i = 0; i < 4; i++) c.Line(34 + i * 20, 56 - (i == 1 || i == 2 ? 4 : 0), 30 + i * 22, 36, 6, W); });
            G("hand_stop", c => { c.Rect(36, 18, 56, 60, W); for (int i = 0; i < 4; i++) c.Rect(36 + i * 15, 70, 11, 44 - Mathf.Abs(i - 1.5f) * 6, W); c.Line(36, 50, 18, 76, 12, W); });
            G("thumb_up", c => { c.Rect(20, 20, 20, 56, W); c.Rect(46, 20, 58, 52, W); c.Polygon(new[] { P(46, 72), P(60, 110), P(74, 104), P(70, 72) }, W); });
            G("thumb_down", c => { c.Rect(20, 52, 20, 56, W); c.Rect(46, 56, 58, 52, W); c.Polygon(new[] { P(46, 56), P(60, 18), P(74, 24), P(70, 56) }, W); });
            G("chat", c => { c.Rect(16, 40, 96, 64, W); c.Polygon(new[] { P(30, 40), P(26, 14), P(56, 40) }, W); for (int i = 0; i < 3; i++) Cut(c, 40 + i * 24, 72, 7); });
            G("quote", c => { c.Circle(40, 60, 18, W); c.Line(52, 64, 40, 100, 10, W); c.Circle(86, 60, 18, W); c.Line(98, 64, 86, 100, 10, W); });
            G("bandaid", c => { c.Rect(16, 44, 96, 40, W); CutRect(c, 44, 50, 40, 28); c.Rect(48, 54, 32, 20, W); });
            G("scalpel", c => { c.Line(18, 24, 70, 76, 10, W); c.Polygon(new[] { P(66, 72), P(112, 112), P(80, 70) }, W); });
            G("stethoscope", c => { c.Line(36, 112, 36, 70, 8, W); c.Line(80, 112, 80, 70, 8, W); c.Ring(58, 60, 22, 8, W); c.Line(58, 38, 90, 24, 8, W); c.Circle(96, 22, 12, W); });
            G("dna", c => { for (int i = 0; i < 9; i++) { float y = 14 + i * 12; float x = Mathf.Sin(i * 0.8f) * 30; c.Circle(64 + x, y, 7, W); c.Circle(64 - x, y, 7, W); c.Line(64 + x, y, 64 - x, y, 4, W); } });
            G("lung_left", c => { c.Circle(56, 54, 34, W); c.Rect(60, 70, 10, 44, W); CutRect(c, 70, 0, 58, 128); });
            G("blood_bag", c => { c.Rect(30, 24, 68, 76, W); c.Rect(56, 100, 16, 18, W); Cut(c, 64, 60, 16); c.Circle(64, 60, 10, W); });
            G("iv", c => { c.Rect(40, 60, 48, 52, W); c.Line(64, 60, 64, 18, 6, W); c.Line(64, 18, 96, 18, 6, W); c.Line(96, 18, 96, 8, 6, W); });
            G("ambulance", c => { c.Rect(12, 30, 80, 56, W); c.Polygon(new[] { P(92, 30), P(118, 30), P(118, 62), P(104, 86), P(92, 86) }, W); Cut(c, 34, 30, 12); Cut(c, 96, 30, 12); CutRect(c, 46, 50, 8, 28); CutRect(c, 36, 60, 28, 8); });
            G("cleaver", c => { c.Rect(26, 52, 74, 50, W); Cut(c, 88, 90, 6); c.Rect(10, 64, 20, 14, W); });
            G("axe", c => { c.Line(30, 14, 84, 114, 10, W); c.Polygon(new[] { P(70, 96), P(112, 112), P(112, 66), P(82, 76) }, W); });
            G("hammer", c => { c.Line(40, 14, 70, 84, 12, W); c.Rect(46, 84, 64, 26, W); });
            G("saw", c => { c.Rect(20, 50, 80, 30, W); for (int i = 0; i < 8; i++) c.Polygon(new[] { P(20 + i * 10, 50), P(25 + i * 10, 40), P(30 + i * 10, 50) }, W); c.Rect(100, 46, 18, 40, W); });
            G("grenade", c => { c.Circle(64, 54, 36, W); c.Rect(52, 88, 24, 18, W); c.Ring(88, 104, 10, 5, W); CutRect(c, 30, 52, 68, 4); CutRect(c, 62, 22, 4, 64); });
            G("mine", c => { c.Circle(64, 50, 40, W); CutRect(c, 0, 0, 128, 50); c.Rect(20, 44, 88, 10, W); c.Rect(58, 90, 12, 14, W); });
            G("bow", c => { c.Ring(30, 64, 60, 8, W); CutRect(c, 0, 0, 30, 128); c.Line(30, 6, 30, 122, 3, W); c.Line(10, 64, 112, 64, 5, W); c.Polygon(new[] { P(112, 64), P(98, 74), P(98, 54) }, W); });
            G("sword", c => { c.Line(24, 24, 100, 100, 10, W); c.Line(30, 50, 50, 30, 10, W); c.Line(14, 14, 26, 26, 12, W); });
            G("chainsaw", c => { c.Rect(14, 40, 44, 44, W); c.Rect(58, 52, 58, 22, W); for (int i = 0; i < 6; i++) c.Circle(64 + i * 9, 50, 3, W); c.Ring(30, 92, 14, 6, W); });
            G("taser", c => { c.Rect(20, 50, 70, 28, W); c.Rect(30, 18, 22, 34, W); c.Line(92, 64, 104, 80, 4, W); c.Line(104, 80, 112, 60, 4, W); c.Line(112, 60, 122, 76, 4, W); });
            G("bat", c => { c.Line(20, 20, 104, 104, 10, W); c.Line(60, 60, 104, 104, 22, W); });
            G("crowbar", c => { c.Line(30, 20, 96, 100, 10, W); c.Line(96, 100, 112, 96, 10, W); c.Line(30, 20, 18, 30, 10, W); });
            G("pistol_silenced", c => { c.Rect(18, 66, 66, 22, W); c.Rect(84, 70, 34, 14, W); c.Polygon(new[] { P(22, 66), P(42, 66), P(36, 24), P(18, 24) }, W); });
            G("sniper", c => { c.Rect(6, 62, 116, 10, W); c.Rect(40, 76, 36, 12, W); c.Rect(6, 46, 30, 18, W); c.Rect(50, 40, 8, 22, W); });
            G("rpg", c => { c.Rect(6, 58, 100, 16, W); c.Polygon(new[] { P(106, 50), P(124, 66), P(106, 82) }, W); c.Rect(40, 40, 10, 18, W); });
            G("magazine", c => { c.Polygon(new[] { P(44, 112), P(84, 112), P(92, 16), P(52, 16) }, W); for (int i = 0; i < 4; i++) CutRect(c, 56 + i * 1.5f, 30 + i * 18, 28, 3); });
            G("shell", c => { c.Rect(48, 36, 32, 76, W); c.Rect(44, 16, 40, 20, W); CutRect(c, 44, 34, 40, 3); });
            G("casing", c => { c.Rect(50, 16, 28, 70, W); c.Polygon(new[] { P(50, 86), P(78, 86), P(70, 108), P(58, 108) }, W); });
            G("scope", c => { c.Ring(64, 64, 46, 8, W); c.Line(64, 10, 64, 118, 3, W); c.Line(10, 64, 118, 64, 3, W); Cut(c, 64, 64, 6); });
            G("silencer", c => { c.Rect(14, 48, 100, 32, W); for (int i = 0; i < 4; i++) CutRect(c, 30 + i * 20, 48, 4, 32); });
            G("fire_mode", c => { for (int i = 0; i < 3; i++) { c.Rect(24 + i * 30, 28, 18, 56, W); c.Polygon(new[] { P(24 + i * 30, 84), P(42 + i * 30, 84), P(33 + i * 30, 104) }, W); } });
            G("ricochet", c => { c.Line(10, 100, 64, 30, 8, W); c.Line(64, 30, 118, 100, 8, W); c.Rect(30, 18, 68, 10, W); });
            G("impact", c => { c.Polygon(Star(64, 64, 56, 22, 8), W); Cut(c, 64, 64, 14); });
            G("splatter", c => { c.Circle(64, 64, 30, W); for (int i = 0; i < 9; i++) { float a = i * 0.7f; c.Circle(64 + Mathf.Cos(a) * 46, 64 + Mathf.Sin(a) * 46, 6 + (i % 3) * 3, W); } });
            G("gib", c => { c.Circle(44, 44, 20, W); c.Circle(84, 52, 16, W); c.Circle(62, 86, 22, W); c.Line(40, 90, 22, 110, 6, W); });
            G("intestine", c => { for (int i = 0; i < 4; i++) c.Ring(40 + (i % 2) * 48, 36 + (i / 2) * 50, 22, 10, W); });
            G("eyeball", c => { c.Circle(64, 64, 48, W); Cut(c, 64, 64, 22); c.Circle(64, 64, 12, W); c.Line(100, 30, 122, 10, 5, W); });
            G("skull_x", c => { c.Circle(64, 74, 40, W); c.Rect(44, 22, 40, 26, W); Cut(c, 48, 76, 10); Cut(c, 80, 76, 10); c.Line(10, 10, 118, 118, 6, W); });
            G("bones", c => { c.Line(24, 24, 104, 104, 10, W); c.Line(24, 104, 104, 24, 10, W); foreach (var p in new[] { P(24, 24), P(104, 104), P(24, 104), P(104, 24) }) { c.Circle(p.x - 6, p.y + 6, 9, W); c.Circle(p.x + 6, p.y - 6, 9, W); } });
            G("ragdoll", c => { c.Circle(64, 104, 13, W); c.Rect(54, 54, 20, 40, W); c.Line(54, 88, 24, 70, 8, W); c.Line(74, 88, 104, 106, 8, W); c.Line(58, 56, 40, 14, 9, W); c.Line(70, 56, 96, 20, 9, W); });
            G("paralysis", c => { c.Rect(56, 10, 16, 108, W); for (int i = 0; i < 6; i++) CutRect(c, 56, 20 + i * 17, 16, 3); c.Line(20, 20, 108, 108, 8, W); });
            G("lightning", c => c.Polygon(new[] { P(72, 120), P(30, 60), P(60, 60), P(46, 8), P(98, 72), P(66, 72) }, W));
            G("rain", c => { c.Circle(48, 90, 22, W); c.Circle(76, 94, 26, W); c.Rect(30, 70, 70, 22, W); for (int i = 0; i < 4; i++) c.Line(34 + i * 18, 56, 28 + i * 18, 30, 5, W); });
            G("fog", c => { for (int i = 0; i < 4; i++) c.Rect(14 + (i % 2) * 10, 24 + i * 22, 100 - (i % 2) * 20, 10, W); });
            G("tree", c => { c.Polygon(new[] { P(64, 120), P(20, 50), P(108, 50) }, W); c.Polygon(new[] { P(64, 90), P(14, 24), P(114, 24) }, W); c.Rect(56, 4, 16, 22, W); });
            G("building", c => { c.Rect(30, 8, 68, 112, W); for (int y = 0; y < 5; y++) for (int x = 0; x < 3; x++) CutRect(c, 38 + x * 20, 24 + y * 18, 12, 10); });
            G("car", c => { c.Rect(10, 34, 108, 30, W); c.Polygon(new[] { P(30, 64), P(44, 92), P(86, 92), P(100, 64) }, W); Cut(c, 34, 34, 13); Cut(c, 94, 34, 13); c.Circle(34, 34, 8, W); c.Circle(94, 34, 8, W); });
            G("crate", c => { c.Rect(18, 18, 92, 92, W); CutRect(c, 28, 28, 72, 72); c.Line(28, 28, 100, 100, 10, W); c.Line(28, 100, 100, 28, 10, W); });
            G("cone", c => { c.Polygon(new[] { P(64, 116), P(30, 20), P(98, 20) }, W); c.Rect(18, 10, 92, 12, W); CutRect(c, 40, 52, 48, 10); });
            G("melon", c => { c.Circle(64, 64, 52, W); CutRect(c, 0, 64, 128, 64); for (int i = 0; i < 5; i++) Cut(c, 34 + i * 15, 44 - Mathf.Abs(i - 2) * 6, 4); });
            G("ball", c => { c.Circle(64, 64, 50, W); Cut(c, 64, 64, 6); c.Ring(64, 64, 30, 4, B); for (int i = 0; i < 3; i++) CutPoly(c, new[] { P(64, 64), P(64 + Mathf.Cos(i * 2.1f) * 60, 64 + Mathf.Sin(i * 2.1f) * 60), P(64 + Mathf.Cos(i * 2.1f + 0.15f) * 60, 64 + Mathf.Sin(i * 2.1f + 0.15f) * 60) }); });
            G("pin", c => { c.Circle(64, 96, 16, W); c.Circle(64, 44, 26, W); c.Rect(54, 60, 20, 30, W); c.Rect(46, 14, 36, 10, W); });
            G("chair", c => { c.Rect(30, 60, 60, 10, W); c.Rect(30, 70, 10, 50, W); c.Rect(30, 10, 10, 50, W); c.Rect(80, 10, 10, 50, W); });
            G("table", c => { c.Rect(10, 80, 108, 12, W); c.Rect(20, 14, 12, 66, W); c.Rect(96, 14, 12, 66, W); });
            G("tire", c => { c.Ring(64, 64, 44, 22, W); for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; Cut(c, 64 + Mathf.Cos(a) * 52, 64 + Mathf.Sin(a) * 52, 4); } });
            G("can", c => { c.Rect(38, 14, 52, 100, W); CutRect(c, 38, 50, 52, 6); c.Rect(46, 112, 36, 6, W); });
            G("bottle", c => { c.Rect(42, 10, 44, 70, W); c.Rect(54, 80, 20, 30, W); c.Rect(52, 108, 24, 10, W); CutRect(c, 46, 30, 36, 24); });
            G("dumbbell", c => { c.Rect(16, 40, 22, 48, W); c.Rect(90, 40, 22, 48, W); c.Rect(38, 58, 52, 12, W); });
            G("mattress", c => { c.Rect(10, 40, 108, 46, W); for (int i = 0; i < 5; i++) Cut(c, 24 + i * 20, 63, 4); });
            G("pallet", c => { for (int i = 0; i < 5; i++) c.Rect(10 + i * 22, 64, 16, 40, W); c.Rect(6, 44, 116, 12, W); c.Rect(6, 24, 116, 12, W); });
            G("propane", c => { c.Rect(36, 14, 56, 80, W); c.Circle(64, 92, 28, W); c.Rect(54, 110, 20, 14, W); CutRect(c, 36, 40, 56, 4); });
            G("trophy_gold", c => { c.Rect(30, 70, 68, 44, W); c.Ring(28, 92, 14, 6, W); c.Ring(100, 92, 14, 6, W); c.Rect(58, 30, 12, 40, W); c.Rect(36, 12, 56, 18, W); });
            G("secret", c => { c.Circle(64, 64, 52, W); Cut(c, 64, 64, 40); c.Rect(54, 22, 20, 14, W); c.Circle(64, 90, 18, W); Cut(c, 64, 90, 8); c.Rect(58, 46, 12, 20, W); });
            G("fps_meter", c => { c.Ring(64, 40, 54, 10, W); CutRect(c, 0, 0, 128, 40); c.Line(64, 40, 98, 80, 8, W); c.Circle(64, 40, 10, W); });
            G("joystick", c => { c.Rect(20, 14, 88, 26, W); c.Rect(58, 40, 12, 46, W); c.Circle(64, 92, 20, W); });
            G("touch", c => { c.Rect(54, 14, 22, 70, W); c.Circle(65, 86, 11, W); c.Ring(65, 92, 28, 5, W); CutRect(c, 30, 60, 70, 20); });
            G("vibrate", c => { c.Rect(42, 14, 44, 100, W); CutRect(c, 48, 26, 32, 76); for (int i = 0; i < 2; i++) { c.Line(26 - i * 12, 44, 26 - i * 12, 84, 5, W); c.Line(102 + i * 12, 44, 102 + i * 12, 84, 5, W); } });
            G("language", c => { c.Ring(64, 64, 50, 8, W); c.Line(14, 64, 114, 64, 5, W); c.Ring(64, 64, 22, 5, W); c.Line(64, 14, 64, 114, 5, W); });
            G("share", c => { c.Circle(30, 64, 14, W); c.Circle(96, 30, 14, W); c.Circle(96, 98, 14, W); c.Line(30, 64, 96, 30, 6, W); c.Line(30, 64, 96, 98, 6, W); });
            G("copy", c => { c.Rect(20, 20, 64, 76, W); CutRect(c, 28, 28, 48, 60); c.Rect(44, 36, 64, 76, W); });
            G("filter", c => c.Polygon(new[] { P(14, 110), P(114, 110), P(74, 60), P(74, 16), P(54, 26), P(54, 60) }, W));
            G("sort", c => { c.Polygon(new[] { P(40, 116), P(16, 84), P(64, 84) }, W); c.Rect(34, 20, 12, 64, W); c.Polygon(new[] { P(88, 12), P(64, 44), P(112, 44) }, W); c.Rect(82, 44, 12, 64, W); });
            G("expand", c => { c.Line(20, 20, 50, 50, 8, W); c.Line(108, 108, 78, 78, 8, W); c.Rect(14, 14, 30, 8, W); c.Rect(14, 14, 8, 30, W); c.Rect(84, 106, 30, 8, W); c.Rect(106, 84, 8, 30, W); });
            G("pin_map", c => { c.Circle(64, 84, 32, W); c.Polygon(new[] { P(36, 70), P(92, 70), P(64, 10) }, W); Cut(c, 64, 86, 12); });
            G("timer", c => { c.Ring(64, 58, 44, 8, W); c.Line(64, 58, 64, 88, 7, W); c.Line(64, 58, 86, 48, 7, W); c.Rect(52, 106, 24, 10, W); });
            G("battery", c => { c.Rect(14, 40, 92, 48, W); CutRect(c, 22, 48, 76, 32); c.Rect(26, 52, 40, 24, W); c.Rect(106, 54, 10, 20, W); });
            G("wifi", c => { c.Ring(64, 24, 90, 9, W); c.Ring(64, 24, 62, 9, W); c.Ring(64, 24, 34, 9, W); CutRect(c, 0, 0, 128, 24); c.Circle(64, 30, 9, W); });
            G("pacifist", c => { c.Ring(64, 64, 50, 9, W); c.Line(64, 14, 64, 114, 8, W); c.Line(64, 64, 30, 30, 8, W); c.Line(64, 64, 98, 30, 8, W); });
            G("clown", c => { c.Circle(64, 64, 46, W); Cut(c, 46, 76, 8); Cut(c, 82, 76, 8); c.Circle(64, 60, 12, new Color(1, 0.2f, 0.2f, 1)); c.Ring(64, 50, 26, 5, B); });
        }

        static Vector2[] Star(float cx, float cy, float outer, float inner, int points)
        {
            var pts = new Vector2[points * 2];
            for (int i = 0; i < pts.Length; i++)
            {
                float a = Mathf.PI / 2 + i * Mathf.PI / points;
                float r = (i % 2 == 0) ? outer : inner;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return pts;
        }

        static void Cut(PixelCanvas c, float cx, float cy, float r)
        {
            for (int y = (int)(cy - r - 2); y <= cy + r + 2; y++)
                for (int x = (int)(cx - r - 2); x <= cx + r + 2; x++)
                {
                    if (x < 0 || y < 0 || x >= c.W || y >= c.H) continue;
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float k = Mathf.Clamp01(d - r + 0.5f);
                    var p = c.Px[y * c.W + x]; p.a *= k; c.Px[y * c.W + x] = p;
                }
        }

        static void CutRect(PixelCanvas c, float x0, float y0, float w, float h)
        {
            for (int y = Mathf.Max(0, (int)y0); y < Mathf.Min(c.H, (int)(y0 + h)); y++)
                for (int x = Mathf.Max(0, (int)x0); x < Mathf.Min(c.W, (int)(x0 + w)); x++)
                {
                    var p = c.Px[y * c.W + x]; p.a = 0; c.Px[y * c.W + x] = p;
                }
        }

        static void CutPoly(PixelCanvas c, Vector2[] pts)
        {
            var mask = new PixelCanvas(c.W, c.H, new Color(0, 0, 0, 0));
            mask.Polygon(pts, Color.white);
            for (int i = 0; i < c.Px.Length; i++) { var p = c.Px[i]; p.a *= 1f - mask.Px[i].a; c.Px[i] = p; }
        }

        // ------------------------------------------------------------------ rendered icons

        const int StudioLayer = 31;

        /// <summary>Renders a procedural model to a transparent icon and caches it under <paramref name="name"/>.</summary>
        public static Texture2D Render(string name, GameObject model, Vector3 viewDir, int size = 192, float padding = 1.15f, string fallbackGlyph = null)
        {
            var studio = new GameObject("IconStudio");
            studio.transform.position = new Vector3(0, -4000, 0);
            model.transform.SetParent(studio.transform, false);
            SetLayer(model, StudioLayer);

            var bounds = new Bounds(model.transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }

            var camGo = new GameObject("IconCam");
            camGo.transform.SetParent(studio.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << StudioLayer;
            cam.orthographic = true;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 50f;
            viewDir = viewDir.normalized;
            float radius = Mathf.Max(bounds.extents.magnitude, 0.01f);
            cam.transform.position = bounds.center - viewDir * (radius * 4f + 1f);
            cam.transform.rotation = Quaternion.LookRotation(viewDir, Vector3.up);
            // Fit the projected bounds.
            float maxExtent = 0.01f;
            var corners = new Vector3[8];
            var e = bounds.extents;
            int k = 0;
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2) corners[k++] = bounds.center + Vector3.Scale(e, new Vector3(x, y, z));
            foreach (var c in corners)
            {
                var local = cam.transform.InverseTransformPoint(c);
                maxExtent = Mathf.Max(maxExtent, Mathf.Abs(local.x), Mathf.Abs(local.y));
            }
            cam.orthographicSize = maxExtent * padding;

            var key = new GameObject("Key").AddComponent<Light>();
            key.transform.SetParent(studio.transform, false);
            key.type = LightType.Directional;
            key.intensity = 1.25f;
            key.cullingMask = 1 << StudioLayer;
            key.transform.rotation = Quaternion.LookRotation(viewDir + Vector3.down * 0.8f + Vector3.right * 0.5f);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(studio.transform, false);
            fill.type = LightType.Directional;
            fill.intensity = 0.55f;
            fill.color = new Color(0.75f, 0.8f, 1f);
            fill.cullingMask = 1 << StudioLayer;
            fill.transform.rotation = Quaternion.LookRotation(-viewDir + Vector3.down * 0.3f - Vector3.right * 0.6f);

            // Phones: no MSAA render targets (some mobile GPUs read them back blank/black). Supersample 2x and
            // average on the CPU instead, which looks the same. Desktop keeps hardware MSAA.
            bool mobile = Application.isMobilePlatform;
            int ss = mobile ? 2 : 1;
            int rtSize = size * ss;
            var desc = new RenderTextureDescriptor(rtSize, rtSize, RenderTextureFormat.ARGB32, 24) { sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear, msaaSamples = 1 };
            if (!mobile)
            {
                desc.msaaSamples = 4;
                try { desc.msaaSamples = Mathf.Clamp(SystemInfo.GetRenderTextureSupportedMSAASampleCount(desc), 1, 4); } catch { desc.msaaSamples = 1; }
            }
            cam.allowHDR = false;   // render straight into our target (an HDR intermediate can drop alpha on mobile)
            cam.allowMSAA = false;
            Texture2D tex = null;
            RenderTexture rt = null;
            var prev = RenderTexture.active;
            var oldAmbient = RenderSettings.ambientLight;
            var oldMode = RenderSettings.ambientMode;
            try
            {
                rt = RenderTexture.GetTemporary(desc);
                cam.targetTexture = rt;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
                cam.Render();
                RenderSettings.ambientLight = oldAmbient;
                RenderSettings.ambientMode = oldMode;

                RenderTexture.active = rt;
                var read = new Texture2D(rtSize, rtSize, TextureFormat.RGBA32, false);
                read.ReadPixels(new Rect(0, 0, rtSize, rtSize), 0, 0);
                read.Apply(false);
                RenderTexture.active = prev;
                var px = read.GetPixels32();
                UnityEngine.Object.Destroy(read);
                if (ss > 1) px = Downsample(px, rtSize, ss);
                if (LooksValid(px))
                {
                    tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Icon_" + name, wrapMode = TextureWrapMode.Clamp };
                    tex.SetPixels32(px);
                    tex.Apply(true);
                }
                else Debug.LogWarning("[IconFactory] Icon '" + name + "' rendered blank on this GPU — using the drawn icon instead.");
            }
            catch (Exception ex) { Debug.LogWarning("[IconFactory] Icon '" + name + "' failed: " + ex.Message); }
            finally
            {
                RenderSettings.ambientLight = oldAmbient;
                RenderSettings.ambientMode = oldMode;
                RenderTexture.active = prev;
                cam.targetTexture = null;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.Destroy(studio);
            }
            if (tex == null)
            {
                if (fallbackGlyph == null) return null; // caller already falls back to a glyph
                tex = Get(fallbackGlyph);
            }
            Register(name, tex);
            return tex;
        }

        static Color32[] Downsample(Color32[] src, int srcSize, int f)
        {
            int size = srcSize / f, n = f * f;
            var dst = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int r = 0, g = 0, b = 0, a = 0;
                    for (int dy = 0; dy < f; dy++)
                        for (int dx = 0; dx < f; dx++)
                        {
                            var c = src[(y * f + dy) * srcSize + x * f + dx];
                            r += c.r; g += c.g; b += c.b; a += c.a;
                        }
                    dst[y * size + x] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), (byte)(a / n));
                }
            return dst;
        }

        /// <summary>A usable icon has some visible pixels and isn't a solid block (no alpha / failed render).</summary>
        static bool LooksValid(Color32[] px)
        {
            int visible = 0, opaque = 0, lit = 0;
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a > 20) visible++;
                if (c.a > 250) opaque++;
                if (c.a > 20 && c.r + c.g + c.b > 30) lit++;
            }
            return visible > px.Length / 200 && opaque < px.Length * 98 / 100 && lit > visible / 10;
        }

        public static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
        }
    }
}
