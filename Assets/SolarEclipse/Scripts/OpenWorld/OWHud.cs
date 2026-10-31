using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>A thing drawn on the minimap / phone map.</summary>
    public struct OWBlip
    {
        public Vector3 Pos; public string Icon, Label; public Color Color; public float Size; public bool Edge;
    }

    /// <summary>
    /// Open World HUD: cash (with +$ popups), wanted stars, the current objective, the minimap (shops, heists,
    /// players, your pin, north up), a marker + distance for where you're going, the call banner and the speedometer.
    /// </summary>
    public static class OWHud
    {
        static readonly List<(long amount, string reason, float time)> popups = new List<(long, string, float)>();
        static float shownCash = -1f;

        public static void Money(long amount, string reason)
        {
            popups.Add((amount, reason, Time.unscaledTime));
            if (popups.Count > 4) popups.RemoveAt(0);
        }

        // ------------------------------------------------------------------ map texture

        static Texture2D mapTex;
        public const float MapMin = -200f, MapSize = 400f;

        /// <summary>Top-down picture of Port Eclipse (512 x 512, row 0 = south).</summary>
        public static Texture2D MapTex
        {
            get
            {
                if (mapTex != null) return mapTex;
                const int N = 512;
                var px = new Color32[N * N];
                float[] lines = { -180, -120, -60, 0, 60, 120, 180 };
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float wx = MapMin + (x + 0.5f) * MapSize / N, wz = MapMin + (y + 0.5f) * MapSize / N;
                        Color c;
                        if (wx > 186f) c = new Color(0.08f, 0.2f, 0.33f);
                        else if (Mathf.Abs(wx) > 186f || Mathf.Abs(wz) > 186f) c = new Color(0.16f, 0.24f, 0.14f);
                        else
                        {
                            bool road = false;
                            foreach (var l in lines) if (Mathf.Abs(wx - l) < 6f || Mathf.Abs(wz - l) < 6f) { road = true; break; }
                            if (road) c = new Color(0.2f, 0.2f, 0.23f);
                            else
                            {
                                int i = Mathf.Clamp(Mathf.FloorToInt((wx + 180f) / 60f), 0, 5), j = Mathf.Clamp(Mathf.FloorToInt((wz + 180f) / 60f), 0, 5);
                                var cen = OWMap.Block(i, j);
                                bool walk = Mathf.Abs(wx - cen.x) > 22f || Mathf.Abs(wz - cen.z) > 22f;
                                c = BlockColor(i, j);
                                if (walk) c = new Color(0.5f, 0.5f, 0.5f);
                            }
                        }
                        px[y * N + x] = c;
                    }
                mapTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "PortEclipseMap", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                mapTex.SetPixels32(px);
                mapTex.Apply(false, true);
                return mapTex;
            }
        }

        static Color BlockColor(int i, int j)
        {
            switch (i * 10 + j)
            {
                case 21: case 31: return new Color(0.24f, 0.45f, 0.22f);
                case 5: case 15: return new Color(0.3f, 0.5f, 0.26f);
                case 55: return new Color(0.42f, 0.36f, 0.26f);
                case 50: case 51: return new Color(0.36f, 0.4f, 0.45f);
                case 54: return new Color(0.28f, 0.28f, 0.3f);
                case 22: case 23: case 32: case 33: return new Color(0.45f, 0.42f, 0.5f);
                default: return new Color(0.34f, 0.34f, 0.37f);
            }
        }

        // ------------------------------------------------------------------ blips

        static readonly Dictionary<string, (string icon, Color color)> PointIcons = new Dictionary<string, (string, Color)>
        {
            { "guns", ("gun", new Color(1f, 0.35f, 0.2f)) }, { "clothes", ("person", new Color(0.3f, 0.8f, 1f)) },
            { "masks", ("clown", new Color(0.7f, 0.4f, 1f)) }, { "boutique", ("diamond", new Color(1f, 0.5f, 0.85f)) },
            { "cars", ("car", new Color(1f, 0.78f, 0.16f)) }, { "customs", ("wrench", new Color(0.3f, 1f, 0.5f)) },
            { "heists", ("flag", new Color(1f, 0.3f, 0.25f)) }, { "apartment", ("home", new Color(1f, 0.85f, 0.5f)) },
            { "race", ("stopwatch", new Color(0.9f, 0.9f, 0.9f)) },
        };

        public static List<OWBlip> Blips(bool everything)
        {
            var list = new List<OWBlip>();
            foreach (var pt in OWMap.Points)
            {
                if (pt == null || pt.transform.position.y < -20f || !PointIcons.TryGetValue(pt.Kind, out var ic)) continue;
                list.Add(new OWBlip { Pos = pt.transform.position, Icon = ic.icon, Color = ic.color, Label = pt.Label, Size = 26f });
            }
            list.Add(new OWBlip { Pos = OWMap.Place("Hospital"), Icon = "heart", Color = new Color(1f, 0.3f, 0.3f), Label = "Hospital", Size = 24f });
            list.Add(new OWBlip { Pos = OWMap.Place("Police Station"), Icon = "shield", Color = new Color(0.35f, 0.55f, 1f), Label = "Police", Size = 24f });
            if (everything) list.Add(new OWBlip { Pos = OWMap.Place("Bank"), Icon = "trophy", Color = new Color(0.4f, 0.95f, 0.5f), Label = "Central Bank", Size = 24f });
            if (OWDirector.BountyActive) list.Add(new OWBlip { Pos = OWDirector.BountyPos, Icon = "skull", Color = UI.Bad, Label = "Bounty: gang boss", Size = 30f, Edge = true });
            var nm = NetManager.I;
            if (NetState.Active && nm != null)
                foreach (var p in nm.Players.Values)
                {
                    if (p.Id == nm.LocalId) continue;
                    var pos = p.Avatar != null ? p.Avatar.transform.position : p.Pos;
                    if (pos.y < -20f) pos = OWMap.ApartmentOutside;
                    bool wanted = OWDirector.Wanted.ContainsKey(p.Id);
                    list.Add(new OWBlip { Pos = pos, Icon = "person", Color = wanted ? UI.Bad : new Color(0.35f, 0.75f, 1f), Label = p.Name, Size = 24f, Edge = everything });
                }
            var target = ObjectivePos;
            if (target.HasValue) list.Add(new OWBlip { Pos = target.Value, Icon = "target", Color = new Color(1f, 0.85f, 0.2f), Label = "Objective", Size = 30f, Edge = true });
            if (OWDirector.Waypoint.HasValue) list.Add(new OWBlip { Pos = OWDirector.Waypoint.Value, Icon = "pin_map", Color = new Color(1f, 0.3f, 0.9f), Label = "Your pin", Size = 30f, Edge = true });
            return list;
        }

        /// <summary>Where the current heist / job wants you.</summary>
        public static Vector3? ObjectivePos
        {
            get
            {
                if (OWHeists.MeInCrew && OWHeists.Phase >= OWHeists.Travel) return OWHeists.Target;
                if (OWJobs.Active != OWJobs.Kind.None) return OWJobs.Target;
                return null;
            }
        }

        static Vector3 Me
        {
            get
            {
                var p = PlayerController.Instance;
                if (p == null) return Vector3.zero;
                var pos = OWCar.LocalDriving != null ? OWCar.LocalDriving.transform.position : p.transform.position;
                return pos.y < -20f ? OWMap.ApartmentOutside : pos;
            }
        }

        static float MyYaw
        {
            get
            {
                if (OWCar.LocalDriving != null) return OWCar.LocalDriving.transform.eulerAngles.y;
                var p = PlayerController.Instance;
                return p != null && p.Cam != null ? p.Cam.transform.eulerAngles.y : 0f;
            }
        }

        /// <summary>Draws the map (north up) centred on a world point, with blips. Returns the world->rect mapping scale.</summary>
        public static void DrawMap(Rect r, Vector3 center, float range, List<OWBlip> blips, bool labels, float alpha = 1f)
        {
            float u0 = (center.x - MapMin) / MapSize, v0 = (center.z - MapMin) / MapSize, h = range / MapSize;
            float ratio = r.width / r.height;
            UI.TexUV(r, MapTex, new Rect(u0 - h * ratio, v0 - h, 2f * h * ratio, 2f * h), new Color(1, 1, 1, alpha));
            foreach (var b in blips)
            {
                var sp = MapToRect(r, center, range, b.Pos, out bool inside);
                if (!inside && !b.Edge) continue;
                if (!inside) sp = new Vector2(Mathf.Clamp(sp.x, r.x + 8, r.xMax - 8), Mathf.Clamp(sp.y, r.y + 8, r.yMax - 8));
                float s = b.Size * (inside ? 1f : 0.8f);
                UI.Tex(new Rect(sp.x - s / 2 - 3, sp.y - s / 2 - 3, s + 6, s + 6), TextureFactory.Circle, new Color(0, 0, 0, 0.6f * alpha));
                UI.Tex(new Rect(sp.x - s / 2, sp.y - s / 2, s, s), IconFactory.Get(b.Icon), b.Color.WithAlpha(alpha));
                if (labels && !string.IsNullOrEmpty(b.Label)) UI.Label(new Rect(sp.x - 110, sp.y + s / 2, 220, 22), b.Label, 14, Color.white.WithAlpha(alpha), TextAnchor.UpperCenter);
            }
            // You (arrow).
            var me = MapToRect(r, center, range, Me, out bool meIn);
            if (meIn)
            {
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(MyYaw, me * UI.Scale);
                UI.Tex(new Rect(me.x - 15, me.y - 15, 30, 30), IconFactory.Get("arrow_up"), Color.white.WithAlpha(alpha));
                GUI.matrix = m;
            }
            UI.Outline(r, new Color(1, 1, 1, 0.35f * alpha), 2f);
        }

        public static Vector2 MapToRect(Rect r, Vector3 center, float range, Vector3 world, out bool inside)
        {
            float k = r.height / 2f / range;
            var p = new Vector2(r.center.x + (world.x - center.x) * k, r.center.y - (world.z - center.z) * k);
            inside = r.Contains(p);
            return p;
        }

        public static Vector3 RectToMap(Rect r, Vector3 center, float range, Vector2 gui)
        {
            float k = r.height / 2f / range;
            return new Vector3(center.x + (gui.x - r.center.x) / k, 0.3f, center.z - (gui.y - r.center.y) / k);
        }

        // ------------------------------------------------------------------ HUD

        public static void Draw()
        {
            if (!OWDirector.Active) return;
            var gm = GameManager.I;
            var p = PlayerController.Instance;
            if (gm == null || gm.State != GameState.Playing || p == null || gm.HudHidden) return;
            if (UIRoot.Contains<PhoneScreen>() && PhoneScreen.FullMap) return;
            float W = UI.VW;
            bool touch = Settings.TouchControlsActive;

            // Minimap (top left; lower on phones, under the touch buttons).
            float ms = touch ? 230f : 250f;
            var mr = new Rect(touch ? 30f : 24f, touch ? 196f : 40f, ms, ms);
            UI.Rounded(new Rect(mr.x - 4, mr.y - 4, mr.width + 8, mr.height + 8), new Color(0, 0, 0, 0.5f), true);
            DrawMap(mr, Me, OWCar.LocalDriving != null ? 95f : 65f, Blips(false), false);
            UI.Label(new Rect(mr.x, mr.yMax - 24, mr.width, 22), OWMap.District(Me).ToUpperInvariant(), 14, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (UI.Hit(mr)) PhoneScreen.Open(PhoneScreen.App.Map);

            // Cash + popups.
            float y = mr.yMax + 10;
            long cash = OWSave.Cash;
            shownCash = shownCash < 0 ? cash : Mathf.MoveTowards(shownCash, cash, Mathf.Max(50f, Mathf.Abs(cash - shownCash) * 4f) * Time.unscaledDeltaTime);
            UI.Rounded(new Rect(mr.x - 4, y, ms + 8, 42), new Color(0, 0, 0, 0.55f), true);
            UI.Label(new Rect(mr.x + 8, y, ms - 8, 42), "$" + ((long)shownCash).ToString("N0"), 26, new Color(0.45f, 1f, 0.5f), TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
            y += 46;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - popups[i].time;
                if (age > 4f) { popups.RemoveAt(i); continue; }
                float a = Mathf.Clamp01((4f - age) / 0.6f);
                UI.Label(new Rect(mr.x + 8, y, ms + 200, 28), "+$" + popups[i].amount.ToString("N0") + "  " + popups[i].reason, 18, new Color(0.5f, 1f, 0.55f, a), TextAnchor.MiddleLeft, FontStyle.Bold);
                y += 28;
            }

            // Wanted stars.
            int stars = OWDirector.MyWanted;
            if (stars > 0)
            {
                bool flash = Mathf.Repeat(Time.unscaledTime, 0.6f) < 0.3f;
                for (int i = 0; i < 5; i++)
                    UI.Tex(new Rect(mr.x + i * 36, y + 4, 30, 30), IconFactory.Get("star"), i < stars ? (flash ? new Color(1f, 0.3f, 0.3f) : new Color(0.4f, 0.6f, 1f)) : new Color(1, 1, 1, 0.2f));
                y += 40;
            }

            // Objective (top centre, under the compass).
            string obj = OWHeists.Objective ?? OWJobs.Objective;
            if (obj != null)
            {
                float tw = Mathf.Min(UI.TextWidth(obj, 20) + 50, W - 600);
                var orr = new Rect((W - tw) / 2f, 160, tw, 40);
                UI.Rounded(orr, new Color(0, 0, 0, 0.6f), true);
                UI.Fill(new Rect(orr.x, orr.y, 5, orr.height), new Color(1f, 0.78f, 0.16f));
                UI.Label(new Rect(orr.x + 16, orr.y, orr.width - 20, orr.height), obj, 20, Color.white);
            }

            // Marker for the objective / your pin.
            var mark = ObjectivePos ?? OWDirector.Waypoint;
            if (mark.HasValue && p.Cam != null) DrawMarker(p, mark.Value, ObjectivePos.HasValue ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.3f, 0.9f));
            if (OWDirector.Waypoint.HasValue && MathX.Flat(Me - OWDirector.Waypoint.Value).magnitude < 6f && !ObjectivePos.HasValue) { OWDirector.Waypoint = null; AudioManager.Ui("ui_tab"); }

            // Calls.
            var line = OWPhone.CallLine;
            if (line != null)
            {
                var cr = new Rect(W / 2f - 260, 210, 520, 50);
                UI.Rounded(cr, OWPhone.Call == OWPhone.CallState.Talking ? new Color(0.1f, 0.45f, 0.2f, 0.85f) : new Color(0.1f, 0.25f, 0.5f, 0.85f), true);
                UI.Tex(new Rect(cr.x + 12, cr.y + 11, 28, 28), IconFactory.Get("mobile"), Color.white);
                string hint = touch ? "" : OWPhone.Call == OWPhone.CallState.Incoming ? "  [P] answer" : "  [P] phone";
                UI.Label(new Rect(cr.x + 50, cr.y, cr.width - 60, cr.height), line + hint, 18, Color.white);
                if (UI.Hit(cr)) PhoneScreen.Open(PhoneScreen.App.Calls);
            }
            int unread = OWPhone.TotalUnread;
            if (unread > 0) UI.Label(new Rect(mr.xMax + 14, mr.y, 220, 28), unread + " new text" + (unread == 1 ? "" : "s") + (touch ? "" : " [P]"), 16, new Color(0.45f, 0.8f, 1f));

            // Driving: speed + car health.
            var car = OWCar.LocalDriving;
            if (car != null)
            {
                var sr = new Rect(W - 300, UI.VH - 240, 260, 110);
                UI.Rounded(sr, new Color(0, 0, 0, 0.5f), true);
                UI.Label(new Rect(sr.x + 16, sr.y + 6, 160, 64), Mathf.RoundToInt(car.Speed * 3.6f).ToString(), 52, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
                UI.Label(new Rect(sr.x + 150, sr.y + 20, 100, 40), "km/h", 18, UI.Muted);
                UI.Label(new Rect(sr.x + 16, sr.y + 66, 230, 24), car.Model != null ? car.Model.Name : "Car", 15, UI.Muted);
                if (!NetState.IsClient && car.MaxHealth > 0)
                {
                    float f = Mathf.Clamp01(car.Health / car.MaxHealth);
                    UI.Fill(new Rect(sr.x + 16, sr.y + 92, 228, 6), new Color(1, 1, 1, 0.15f));
                    UI.Fill(new Rect(sr.x + 16, sr.y + 92, 228 * f, 6), Color.Lerp(UI.Bad, UI.Good, f));
                }
            }
        }

        static void DrawMarker(PlayerController p, Vector3 world, Color col)
        {
            var cam = p.Cam;
            var top = world + Vector3.up * 2.2f;
            var sp = cam.WorldToScreenPoint(top);
            float dist = MathX.Flat(Me - world).magnitude;
            var g = new Vector2(sp.x / UI.Scale, (Screen.height - sp.y) / UI.Scale);
            bool behind = sp.z < 0f;
            if (behind) g = new Vector2(UI.VW - g.x, UI.VH - 40);
            float m = 60f;
            g.x = Mathf.Clamp(g.x, m, UI.VW - m);
            g.y = Mathf.Clamp(g.y, 200f, UI.VH - m);
            float bob = Mathf.Sin(Time.unscaledTime * 4f) * 4f;
            UI.Tex(new Rect(g.x - 18, g.y - 40 + bob, 36, 36), IconFactory.Get("pin_map"), col);
            UI.Label(new Rect(g.x - 80, g.y - 2, 160, 24), Mathf.RoundToInt(dist) + " m", 16, Color.white, TextAnchor.UpperCenter, FontStyle.Bold);
        }
    }
}
