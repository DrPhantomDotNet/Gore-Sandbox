using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    public enum ButtonKind { Normal, Primary, Danger, Ghost, Tab, Toggle }

    /// <summary>
    /// Immediate-mode UI toolkit on top of IMGUI. All layout uses a virtual 1080-unit-tall canvas,
    /// converted to crisp pixel sizes (no matrix scaling, so text stays sharp at any resolution).
    /// </summary>
    public static class UI
    {
        public static float Scale { get; private set; } = 1f;
        public static float VW { get; private set; } = 1920f;
        public static float VH { get; private set; } = 1080f;

        public static readonly Color Accent = new Color(0.86f, 0.11f, 0.13f);
        public static readonly Color Accent2 = new Color(1f, 0.55f, 0.16f);
        public static readonly Color PanelColor = new Color(0.06f, 0.06f, 0.08f, 0.9f);
        public static readonly Color PanelLight = new Color(0.13f, 0.13f, 0.16f, 0.95f);
        public static readonly Color TextColor = new Color(0.95f, 0.94f, 0.92f);
        public static readonly Color Muted = new Color(0.62f, 0.62f, 0.66f);
        public static readonly Color Good = new Color(0.35f, 0.85f, 0.4f);
        public static readonly Color Warn = new Color(1f, 0.8f, 0.25f);
        public static readonly Color Bad = new Color(1f, 0.3f, 0.3f);

        static Font font, heavyFont;
        static readonly Dictionary<long, GUIStyle> styles = new Dictionary<long, GUIStyle>();
        static readonly Dictionary<int, float> anim = new Dictionary<int, float>();
        static readonly Dictionary<string, float> scroll = new Dictionary<string, float>();
        static GUIStyle rounded, roundedSmall, invisible, textField, sliderTrack, sliderThumb, shadowStyle;
        static int hoverId = -1, prevHoverId = -1;
        static string tooltip;
        static string dragId;
        static Vector2 dragStart;
        static float dragStartScroll;
        static bool dragging;
        static float lastScale = -1;

        public static Font Font => font ??= LoadFont(new[] { "Segoe UI", "Roboto", "Helvetica Neue", "Arial" });
        public static Font HeavyFont => heavyFont ??= LoadFont(new[] { "Impact", "Arial Black", "Roboto Condensed", "Arial" });

        /// <summary>
        /// Desktop: the nicest installed OS font from the list. Phones/tablets/WebGL: Unity's built-in font, because
        /// OS font lookup there is unreliable (Android 12+ variable fonts and iOS font collections can come out
        /// blank, as boxes or garbled), and the requested names usually don't exist anyway.
        /// </summary>
        static Font LoadFont(string[] names)
        {
            Font f = null;
            if (CanUseOSFonts)
            {
                try
                {
                    var installed = new HashSet<string>(Font.GetOSInstalledFontNames(), System.StringComparer.OrdinalIgnoreCase);
                    var found = new List<string>();
                    foreach (var n in names) if (installed.Contains(n)) found.Add(n);
                    if (found.Count > 0) f = Font.CreateDynamicFontFromOSFont(found.ToArray(), 32);
                }
                catch { f = null; }
            }
            return f != null ? f : BuiltinFont;
        }

        static Font builtinFont;
        public static Font BuiltinFont
        {
            get
            {
                if (builtinFont != null) return builtinFont;
                try { builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (builtinFont == null) try { builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                return builtinFont; // null = IMGUI's default skin font, which still works
            }
        }

        static bool CanUseOSFonts
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.WindowsPlayer: case RuntimePlatform.WindowsEditor:
                    case RuntimePlatform.OSXPlayer: case RuntimePlatform.OSXEditor:
                    case RuntimePlatform.LinuxPlayer: case RuntimePlatform.LinuxEditor:
                        return !Settings.Data.builtinFont;
                    default: return false;
                }
            }
        }

        /// <summary>Re-picks fonts (after the font setting changes).</summary>
        public static void ReloadFonts() { font = null; heavyFont = null; styles.Clear(); rounded = null; }

        /// <summary>
        /// Phones: the layout is designed for a 1080-unit-tall monitor, which makes text tiny on a ~7 cm tall phone
        /// screen. Small screens get a modest boost (layouts already clamp to the visible height).
        /// </summary>
        static float phoneBoost = -1;
        static float PhoneBoost
        {
            get
            {
                if (phoneBoost > 0) return phoneBoost;
                phoneBoost = 1f;
                if (Application.isMobilePlatform)
                {
                    float dpi = Screen.dpi > 0 ? Screen.dpi : 300f;
                    float inchesTall = Mathf.Min(Screen.width, Screen.height) / dpi;
                    phoneBoost = inchesTall < 3.2f ? 1.15f : 1.05f; // phone vs tablet
                }
                return phoneBoost;
            }
        }

        // Dynamic fonts keep one glyph atlas per font. Every distinct pixel size adds glyphs, and when the atlas has to
        // grow mid-frame IMGUI can draw garbled or invisible text, which mobile GPUs show most. On phones/tablets text
        // sizes snap to a small fixed set, and each set is pre-rendered once with all ASCII characters.
        static readonly int[] SizeSteps = { 8, 9, 10, 11, 12, 13, 14, 15, 16, 18, 20, 22, 24, 26, 28, 30, 32, 36, 40, 44, 48, 54, 60, 68, 76, 84, 96, 112, 128 };
        static readonly HashSet<long> warmed = new HashSet<long>();
        const string Ascii = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\u00b7\u2022\u2014\u00d7\u00b0\u00a9";

        /// <summary>Pixel font size for a virtual size (snapped to <see cref="SizeSteps"/> on mobile).</summary>
        public static int FontPx(float size)
        {
            int px = Mathf.Clamp(Px(size), 6, 160);
            if (!Application.isMobilePlatform) return px;
            int best = SizeSteps[0];
            foreach (var st in SizeSteps) if (Mathf.Abs(st - px) < Mathf.Abs(best - px)) best = st;
            return best;
        }

        static void Warm(Font f, int px, FontStyle fs)
        {
            if (f == null || !f.dynamic || !Application.isMobilePlatform) return;
            long key = ((long)f.GetInstanceID() << 32) ^ ((long)px << 4) ^ (long)fs;
            if (!warmed.Add(key)) return;
            try { f.RequestCharactersInTexture(Ascii, px, fs); } catch { }
        }

        /// <summary>Call first in OnGUI.</summary>
        public static void BeginFrame()
        {
            float uiScale = Mathf.Clamp(Settings.Data.uiScale, 0.6f, 1.6f);
            Scale = Mathf.Max(0.35f, Screen.height / 1080f * uiScale * PhoneBoost);
            VW = Screen.width / Scale;
            VH = Screen.height / Scale;
            if (!Mathf.Approximately(lastScale, Scale)) { styles.Clear(); lastScale = Scale; rounded = null; }
            GUI.matrix = Matrix4x4.identity;
            if (Event.current.type == EventType.Repaint) { prevHoverId = hoverId; hoverId = -1; tooltip = null; }
            var e = Event.current;
            if (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp) { dragId = null; dragging = false; }
            EnsureStyles();
        }

        static void EnsureStyles()
        {
            if (rounded != null) return;
            rounded = new GUIStyle { normal = { background = TextureFactory.RoundRect }, border = new RectOffset(16, 16, 16, 16) };
            roundedSmall = new GUIStyle { normal = { background = TextureFactory.RoundRectSmall }, border = new RectOffset(7, 7, 7, 7) };
            invisible = new GUIStyle();
            textField = new GUIStyle
            {
                font = Font, fontSize = FontPx(24), alignment = TextAnchor.MiddleLeft, padding = new RectOffset(Px(14), Px(14), 0, 0),
                normal = { textColor = TextColor }, focused = { textColor = Color.white }, hover = { textColor = Color.white }, clipping = TextClipping.Clip,
            };
            sliderTrack = new GUIStyle { fixedHeight = 0 };
            sliderThumb = new GUIStyle { fixedWidth = Px(28), fixedHeight = Px(28) };
        }

        public static int Px(float v) => Mathf.Max(1, Mathf.RoundToInt(v * Scale));
        public static Rect R(float x, float y, float w, float h) => new Rect(x * Scale, y * Scale, w * Scale, h * Scale);
        public static Rect R(Rect v) => R(v.x, v.y, v.width, v.height);
        public static Vector2 Mouse => Event.current.mousePosition / Scale;
        public static bool Hover(Rect v) => !dragging && R(v).Contains(Event.current.mousePosition);
        public static bool Dragging => dragging;

        // ------------------------------------------------------------------ drawing

        static bool Repaint => Event.current.type == EventType.Repaint;

        public static void Fill(Rect v, Color c)
        {
            if (!Repaint) return;
            var old = GUI.color; GUI.color = c * old;
            GUI.DrawTexture(R(v), TextureFactory.White);
            GUI.color = old;
        }

        public static void Rounded(Rect v, Color c, bool small = false)
        {
            if (!Repaint) return;
            EnsureStyles();
            var old = GUI.color; GUI.color = c * old;
            var style = small || v.height * Scale < 34 ? roundedSmall : rounded;
            style.Draw(R(v), GUIContent.none, false, false, false, false);
            GUI.color = old;
        }

        public static void Outline(Rect v, Color c, float thickness = 2f)
        {
            Fill(new Rect(v.x, v.y, v.width, thickness), c);
            Fill(new Rect(v.x, v.yMax - thickness, v.width, thickness), c);
            Fill(new Rect(v.x, v.y, thickness, v.height), c);
            Fill(new Rect(v.xMax - thickness, v.y, thickness, v.height), c);
        }

        public static void Tex(Rect v, Texture t, Color tint, ScaleMode mode = ScaleMode.StretchToFill)
        {
            if (!Repaint || t == null) return;
            var old = GUI.color; GUI.color = tint * old;
            GUI.DrawTexture(R(v), t, mode, true);
            GUI.color = old;
        }

        public static void TexUV(Rect v, Texture t, Rect uv, Color tint)
        {
            if (!Repaint || t == null) return;
            var old = GUI.color; GUI.color = tint * old;
            GUI.DrawTextureWithTexCoords(R(v), t, uv, true);
            GUI.color = old;
        }

        /// <summary>Vertical gradient from top colour to bottom colour.</summary>
        public static void Gradient(Rect v, Color top, Color bottom)
        {
            Fill(v, bottom);
            Tex(v, FlipGradient, top);
        }

        static Texture2D flipGradient;
        static Texture2D FlipGradient
        {
            get
            {
                if (flipGradient != null) return flipGradient;
                flipGradient = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 64; y++) flipGradient.SetPixel(0, y, new Color(1, 1, 1, y / 63f));
                flipGradient.Apply();
                return flipGradient;
            }
        }

        public static void Shadow(Rect v, float spread = 24f, float alpha = 0.6f)
        {
            if (!Repaint) return;
            var old = GUI.color; GUI.color = new Color(0, 0, 0, alpha) * old;
            shadowStyle ??= new GUIStyle { normal = { background = TextureFactory.Shadow }, border = new RectOffset(22, 22, 22, 22) };
            shadowStyle.Draw(R(new Rect(v.x - spread, v.y - spread * 0.6f, v.width + spread * 2, v.height + spread * 2)), GUIContent.none, false, false, false, false);
            GUI.color = old;
        }

        /// <summary>Standard window panel with drop shadow and accent top bar.</summary>
        public static void Panel(Rect v, string title = null, float alpha = 1f)
        {
            Shadow(v, 26f, 0.55f * alpha);
            Rounded(v, PanelColor.WithAlpha(PanelColor.a * alpha));
            if (title != null)
            {
                Rounded(new Rect(v.x, v.y, v.width, 64), PanelLight.WithAlpha(alpha));
                Fill(new Rect(v.x + 16, v.y + 62, v.width - 32, 2), Accent.WithAlpha(0.8f * alpha));
                Label(new Rect(v.x + 26, v.y, v.width - 52, 64), title.ToUpperInvariant(), 28, TextColor.WithAlpha(alpha), TextAnchor.MiddleLeft, FontStyle.Bold, true, false, HeavyFont);
            }
        }

        // ------------------------------------------------------------------ text

        static GUIStyle Style(int size, TextAnchor align, FontStyle fs, bool wrap, Font f)
        {
            long key = ((long)size << 32) ^ ((long)align << 24) ^ ((long)fs << 20) ^ (wrap ? 1L << 19 : 0) ^ (f == HeavyFont ? 1L << 18 : 0);
            if (!styles.TryGetValue(key, out var s))
            {
                s = new GUIStyle
                {
                    font = f ?? Font, fontSize = FontPx(size), alignment = align, fontStyle = fs, wordWrap = wrap, richText = true,
                    clipping = TextClipping.Overflow, normal = { textColor = Color.white },
                };
                styles[key] = s;
                Warm(s.font, s.fontSize, fs);
            }
            return s;
        }

        public static void Label(Rect v, string text, int size = 22, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft,
            FontStyle fs = FontStyle.Normal, bool shadow = true, bool wrap = false, Font f = null)
        {
            if (!Repaint || string.IsNullOrEmpty(text)) return;
            var style = Style(size, align, fs, wrap, f ?? Font);
            var col = color ?? TextColor;
            var r = R(v);
            var old = GUI.color;
            if (shadow)
            {
                GUI.color = new Color(0, 0, 0, 0.6f * col.a) * old;
                GUI.Label(new Rect(r.x + Px(2), r.y + Px(2), r.width, r.height), text, style);
            }
            GUI.color = col * old;
            GUI.Label(r, text, style);
            GUI.color = old;
        }

        public static float TextWidth(string text, int size, FontStyle fs = FontStyle.Normal, Font f = null)
            => Style(size, TextAnchor.MiddleLeft, fs, false, f ?? Font).CalcSize(new GUIContent(text)).x / Scale;

        public static float TextHeight(string text, int size, float width)
            => Style(size, TextAnchor.UpperLeft, FontStyle.Normal, true, Font).CalcHeight(new GUIContent(text), width * Scale) / Scale;

        // ------------------------------------------------------------------ interaction helpers

        static int Id(Rect v, string salt) => (salt ?? "").GetHashCode() ^ (int)(v.x * 73856093) ^ (int)(v.y * 19349663) ^ (int)(v.width * 83492791);

        /// <summary>0..1 hover animation value for a control.</summary>
        public static float HoverAnim(int id, bool hover)
        {
            anim.TryGetValue(id, out var a);
            if (Repaint)
            {
                a = Mathf.MoveTowards(a, hover ? 1f : 0f, Time.unscaledDeltaTime * 7f);
                anim[id] = a;
            }
            return a;
        }

        static void TrackHover(int id, bool hover, string tip)
        {
            if (!hover || !Repaint) return;
            hoverId = id;
            if (tip != null) tooltip = tip;
            if (prevHoverId != id && prevHoverId != -2) AudioManager.Ui("ui_hover", 0.5f);
        }

        public static bool Button(Rect v, string text, ButtonKind kind = ButtonKind.Normal, Texture icon = null, int size = 24, bool enabled = true, string tip = null, bool selected = false)
        {
            int id = Id(v, text);
            bool hover = enabled && Hover(v);
            TrackHover(id, hover, tip);
            float a = HoverAnim(id, hover || selected);

            Color bg, fg = TextColor;
            switch (kind)
            {
                case ButtonKind.Primary: bg = Color.Lerp(Accent * 0.85f, Accent * 1.15f, a); bg.a = 1; break;
                case ButtonKind.Danger: bg = Color.Lerp(new Color(0.35f, 0.06f, 0.07f), new Color(0.7f, 0.1f, 0.12f), a); break;
                case ButtonKind.Ghost: bg = new Color(1, 1, 1, 0.04f + 0.1f * a); break;
                case ButtonKind.Tab: bg = selected ? Accent.WithAlpha(0.9f) : new Color(1, 1, 1, 0.05f + 0.1f * a); break;
                default: bg = Color.Lerp(new Color(0.16f, 0.16f, 0.2f, 0.92f), new Color(0.26f, 0.24f, 0.28f, 0.98f), a); break;
            }
            if (!enabled) { bg.a *= 0.4f; fg = Muted; }
            var draw = v;
            if (kind != ButtonKind.Tab) draw = new Rect(v.x + a * 4f, v.y, v.width, v.height);
            Rounded(draw, bg);
            if (kind == ButtonKind.Normal || kind == ButtonKind.Ghost) Fill(new Rect(draw.x, draw.y + 8, 4 * a, draw.height - 16), Accent);

            float tx = draw.x + 18;
            if (icon != null)
            {
                float isz = Mathf.Min(draw.height - 16, 44);
                Tex(new Rect(tx, draw.y + (draw.height - isz) / 2, isz, isz), icon, fg);
                tx += isz + 12;
            }
            var align = icon == null && kind != ButtonKind.Normal && kind != ButtonKind.Ghost ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            var textRect = align == TextAnchor.MiddleCenter ? draw : new Rect(tx, draw.y, draw.xMax - tx - 10, draw.height);
            Label(textRect, text, size, fg, align, FontStyle.Bold);

            if (!enabled) return false;
            bool clicked = GUI.Button(R(v), GUIContent.none, invisible) && !dragging;
            if (clicked) AudioManager.Ui(kind == ButtonKind.Danger ? "ui_back" : "ui_click", 0.8f);
            return clicked;
        }

        /// <summary>Invisible click area (for custom-drawn widgets).</summary>
        public static bool Hit(Rect v) => GUI.Button(R(v), GUIContent.none, invisible) && !dragging;

        /// <summary>On/off switch row. Returns true when the value changed.</summary>
        public static bool Toggle(Rect v, string label, ref bool value, string tip = null)
        {
            int id = Id(v, label);
            bool hover = Hover(v);
            TrackHover(id, hover, tip);
            float a = HoverAnim(id, hover);
            Rounded(v, new Color(1, 1, 1, 0.03f + 0.06f * a));
            Label(new Rect(v.x + 16, v.y, v.width - 120, v.height), label, 22, TextColor);
            var sw = new Rect(v.xMax - 86, v.y + v.height / 2 - 15, 64, 30);
            int kid = Id(sw, "knob" + label);
            anim.TryGetValue(kid, out var k);
            if (Repaint) { k = Mathf.MoveTowards(k, value ? 1 : 0, Time.unscaledDeltaTime * 8f); anim[kid] = k; }
            Rounded(sw, Color.Lerp(new Color(0.25f, 0.25f, 0.3f), Accent, k), true);
            Tex(new Rect(sw.x + 3 + k * 34, sw.y + 3, 24, 24), TextureFactory.Circle, Color.white);
            if (GUI.Button(R(v), GUIContent.none, invisible) && !dragging)
            {
                value = !value;
                AudioManager.Ui(value ? "ui_toggle_on" : "ui_toggle_off", 0.8f);
                return true;
            }
            return false;
        }

        public static bool Slider(Rect v, string label, ref float value, float min, float max, string format = "0.00", string tip = null, string suffix = "")
        {
            int id = Id(v, label);
            bool hover = Hover(v);
            TrackHover(id, hover, tip);
            float a = HoverAnim(id, hover);
            Rounded(v, new Color(1, 1, 1, 0.03f + 0.06f * a));
            Label(new Rect(v.x + 16, v.y, v.width * 0.42f, v.height), label, 22, TextColor);
            Label(new Rect(v.xMax - 110, v.y, 96, v.height), value.ToString(format) + suffix, 22, Accent2, TextAnchor.MiddleRight, FontStyle.Bold);
            var track = new Rect(v.x + v.width * 0.44f, v.y + v.height / 2 - 4, v.width * 0.56f - 130, 8);
            float t = Mathf.InverseLerp(min, max, value);
            Rounded(track, new Color(1, 1, 1, 0.12f), true);
            Rounded(new Rect(track.x, track.y, track.width * t, track.height), Accent, true);
            Tex(new Rect(track.x + track.width * t - 13, track.y - 9, 26, 26), TextureFactory.Circle, Color.Lerp(Color.white, Accent2, a));
            float nv = GUI.HorizontalSlider(R(new Rect(track.x, v.y, track.width, v.height)), value, min, max, sliderTrack, sliderThumb);
            if (!Mathf.Approximately(nv, value))
            {
                if (Mathf.Abs(nv - value) > (max - min) * 0.02f) AudioManager.Ui("ui_slider", 0.6f);
                value = nv;
                return true;
            }
            return false;
        }

        /// <summary>Touch-friendly option cycler: ‹ value ›.</summary>
        public static bool Selector(Rect v, string label, ref int index, string[] options, string tip = null)
        {
            int id = Id(v, label);
            bool hover = Hover(v);
            TrackHover(id, hover, tip);
            float a = HoverAnim(id, hover);
            Rounded(v, new Color(1, 1, 1, 0.03f + 0.06f * a));
            Label(new Rect(v.x + 16, v.y, v.width * 0.45f, v.height), label, 22, TextColor);
            var area = new Rect(v.x + v.width * 0.45f, v.y + 6, v.width * 0.55f - 12, v.height - 12);
            bool changed = false;
            if (Button(new Rect(area.x, area.y, 44, area.height), "<", ButtonKind.Ghost, null, 28)) { index = (index - 1 + options.Length) % options.Length; changed = true; }
            if (Button(new Rect(area.xMax - 44, area.y, 44, area.height), ">", ButtonKind.Ghost, null, 28)) { index = (index + 1) % options.Length; changed = true; }
            index = Mathf.Clamp(index, 0, options.Length - 1);
            Label(new Rect(area.x + 44, area.y, area.width - 88, area.height), options[index], 22, Accent2, TextAnchor.MiddleCenter, FontStyle.Bold);
            return changed;
        }

        public static string TextField(Rect v, string value, string controlName, bool password = false, string placeholder = null)
        {
            EnsureStyles();
            textField.fontSize = FontPx(24);
            textField.padding = new RectOffset(Px(14), Px(14), 0, 0);
            bool focused = GUI.GetNameOfFocusedControl() == controlName;
            Rounded(v, focused ? new Color(0.2f, 0.2f, 0.25f, 1f) : new Color(0.12f, 0.12f, 0.15f, 1f));
            if (focused) Outline(v, Accent.WithAlpha(0.8f), 2);
            GUI.SetNextControlName(controlName);
            string result = password ? GUI.PasswordField(R(v), value ?? "", '*', textField) : GUI.TextField(R(v), value ?? "", textField);
            if (string.IsNullOrEmpty(result) && !focused && placeholder != null)
                Label(new Rect(v.x + 14, v.y, v.width - 28, v.height), placeholder, 22, Muted, TextAnchor.MiddleLeft, FontStyle.Italic, false);
            if (result != value) AudioManager.Ui("ui_type", 0.4f);
            return result;
        }

        public static int Tabs(Rect v, int current, string[] names, int size = 22)
        {
            float w = v.width / names.Length;
            for (int i = 0; i < names.Length; i++)
                if (Button(new Rect(v.x + i * w + 2, v.y, w - 4, v.height), names[i], ButtonKind.Tab, null, size, true, null, i == current) && i != current)
                {
                    AudioManager.Ui("ui_tab");
                    current = i;
                }
            return current;
        }

        // ------------------------------------------------------------------ scrolling

        /// <summary>Begins a clipped, wheel- and drag-scrollable region. Returns the scroll offset to subtract from content y.</summary>
        public static float BeginScroll(string id, Rect view, float contentHeight)
        {
            scroll.TryGetValue(id, out var s);
            float max = Mathf.Max(0, contentHeight - view.height);
            var e = Event.current;
            var screen = R(view);
            if (e.type == EventType.ScrollWheel && screen.Contains(e.mousePosition)) { s += e.delta.y * 40f; e.Use(); }
            if (e.type == EventType.MouseDown && screen.Contains(e.mousePosition)) { dragId = id; dragStart = e.mousePosition; dragStartScroll = s; }
            if (e.type == EventType.MouseDrag && dragId == id)
            {
                float dy = (e.mousePosition.y - dragStart.y) / Scale;
                if (Mathf.Abs(dy) > 10f) dragging = true;
                if (dragging) { s = dragStartScroll - dy; e.Use(); }
            }
            s = Mathf.Clamp(s, 0, max);
            scroll[id] = s;
            GUI.BeginGroup(screen);
            // Scrollbar indicator (drawn after content in EndScroll would be on top; here it is under, fine)
            return s;
        }

        public static void EndScroll(string id, Rect view, float contentHeight)
        {
            GUI.EndGroup();
            float max = contentHeight - view.height;
            if (max <= 1) return;
            scroll.TryGetValue(id, out var s);
            float frac = view.height / contentHeight;
            float barH = Mathf.Max(30, view.height * frac);
            float y = view.y + (view.height - barH) * (s / max);
            Rounded(new Rect(view.xMax - 6, y, 4, barH), new Color(1, 1, 1, 0.25f), true);
        }

        public static void ResetScroll(string id) => scroll.Remove(id);
        public static void SetScroll(string id, float value) => scroll[id] = value;

        /// <summary>Converts a virtual rect inside a scroll group to group-relative virtual coords.</summary>
        public static Rect InGroup(Rect view, float x, float y, float w, float h) => new Rect(x, y, w, h);

        // ------------------------------------------------------------------ tooltip

        public static void DrawTooltip()
        {
            if (string.IsNullOrEmpty(tooltip) || Settings.TouchControlsActive) return;
            var m = Mouse;
            float w = Mathf.Min(520, TextWidth(tooltip, 20) + 30);
            float h = TextHeight(tooltip, 20, w - 24) + 18;
            var r = new Rect(Mathf.Min(m.x + 18, VW - w - 8), Mathf.Min(m.y + 22, VH - h - 8), w, h);
            Rounded(r, new Color(0.02f, 0.02f, 0.03f, 0.95f));
            Outline(r, Accent.WithAlpha(0.6f), 1);
            Label(new Rect(r.x + 12, r.y + 9, w - 24, h - 18), tooltip, 20, TextColor, TextAnchor.UpperLeft, FontStyle.Normal, false, true);
        }

        /// <summary>Full-screen dim used behind modal windows; also eats clicks.</summary>
        public static void Dim(float alpha = 0.6f)
        {
            Fill(new Rect(0, 0, VW, VH), new Color(0, 0, 0, alpha));
        }

        public static Rect Centered(float w, float h) => new Rect((VW - w) / 2f, (VH - h) / 2f, w, h);
    }
}
