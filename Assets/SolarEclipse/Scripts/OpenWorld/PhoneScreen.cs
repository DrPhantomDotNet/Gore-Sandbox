using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// The phone (P on PC, PHONE button on phones): MAP (tap to pin, take a taxi there, see every player),
    /// HEISTS, JOBS, GARAGE, TEXTS, BANK (send money), CALLS (voice calls with other players), EMOTES and STATS.
    /// </summary>
    public sealed class PhoneScreen : UIScreen
    {
        public enum App { Home, Map, Heists, Jobs, Garage, Messages, Bank, Calls, Emotes, Stats }
        static readonly (App app, string name, string icon, Color color)[] Apps =
        {
            (App.Map, "MAP", "map", new Color(0.25f, 0.65f, 0.35f)), (App.Heists, "HEISTS", "flag", new Color(0.85f, 0.25f, 0.2f)),
            (App.Jobs, "JOBS", "list", new Color(0.95f, 0.6f, 0.15f)), (App.Garage, "GARAGE", "car", new Color(0.95f, 0.78f, 0.15f)),
            (App.Messages, "TEXTS", "chat", new Color(0.25f, 0.6f, 1f)), (App.Bank, "BANK", "gift", new Color(0.3f, 0.8f, 0.45f)),
            (App.Calls, "CALLS", "mobile", new Color(0.3f, 0.85f, 0.4f)), (App.Emotes, "EMOTES", "hand", new Color(0.7f, 0.4f, 1f)),
            (App.Stats, "STATS", "chart", new Color(0.55f, 0.55f, 0.65f)),
        };

        App app;
        public static byte OpenThread = 255;
        public static bool FullMap => UIRoot.Find<PhoneScreen>() is PhoneScreen ps && ps.app == App.Map;
        string draft = "", amount = "1000";
        byte bankTo = 255;
        float mapRange = 200f;
        Vector3 mapCenter;
        bool mapFollow;
        int openedFrame;

        public PhoneScreen(App start = App.Home) { app = start; }

        public override bool IsModal => true;
        public override bool BlocksGameplay => true;

        public static void Open(App a = App.Home)
        {
            var cur = UIRoot.Find<PhoneScreen>();
            if (cur != null) { cur.Go(a); return; }
            if (!OWDirector.Active) return;
            if (a == App.Home && OWPhone.Call == OWPhone.CallState.Incoming) a = App.Calls;
            UIRoot.Push(new PhoneScreen(a));
        }

        /// <summary>P / the PHONE button.</summary>
        public static void Toggle()
        {
            var cur = UIRoot.Find<PhoneScreen>();
            if (cur != null) { UIRoot.Remove(cur); AudioManager.Ui("ui_close"); return; }
            Open();
        }

        void Go(App a)
        {
            app = a;
            if (a == App.Map) { mapCenter = new Vector3(0, 0, 0); mapRange = 200f; mapFollow = false; }
            if (a != App.Messages) OpenThread = 255;
            AudioManager.Ui("ui_tab");
        }

        public override void OnOpen()
        {
            openedFrame = Time.frameCount;
            AudioManager.Ui("ui_open");
            Go(app);
        }

        public override void OnClose() { OpenThread = 255; }

        public override bool OnBack()
        {
            if (app != App.Home && !(app == App.Messages && OpenThread != 255)) { Go(App.Home); return true; }
            if (app == App.Messages && OpenThread != 255) { OpenThread = 255; return true; }
            UIRoot.Remove(this);
            AudioManager.Ui("ui_close");
            return true;
        }

        public override void Tick()
        {
            if (Time.frameCount == openedFrame || UIRoot.DevPanelCapturing) return;
            bool typing = GUIUtility.keyboardControl != 0;
            if (!typing && Input.GetKeyDown(KeyCode.P)) { UIRoot.Remove(this); AudioManager.Ui("ui_close"); return; }
            if (!typing && Input.GetKeyDown(KeyCode.M)) { if (app == App.Map) UIRoot.Remove(this); else Go(App.Map); }
        }

        // ------------------------------------------------------------------ frame

        public override void Draw()
        {
            if (app == App.Map) { DrawMapApp(); return; }
            float a = Intro(0, 0.2f);
            bool touch = Settings.TouchControlsActive;
            float h = Mathf.Min(UI.VH - 40, 960), w = touch ? 640f : 560f;
            var phone = new Rect(UI.VW - w - (touch ? 60 : 90), (UI.VH - h) / 2f + (1f - a) * 80f, w, h);
            UI.Shadow(phone, 30f, 0.7f * a);
            UI.Rounded(phone, new Color(0.04f, 0.04f, 0.05f, 0.98f));
            var screen = new Rect(phone.x + 16, phone.y + 16, phone.width - 32, phone.height - 32);
            UI.Gradient(screen, new Color(0.1f, 0.08f, 0.2f), new Color(0.02f, 0.02f, 0.05f));

            // Status bar
            UI.Label(new Rect(screen.x + 16, screen.y + 4, 120, 30), System.DateTime.Now.ToString("HH:mm"), 17, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Label(new Rect(screen.xMax - 260, screen.y + 4, 244, 30), "$" + OWSave.Cash.ToString("N0") + "   " + (NetState.Active ? "5G" : "WIFI"), 16, new Color(0.5f, 1f, 0.55f), TextAnchor.MiddleRight);
            var body = new Rect(screen.x + 16, screen.y + 40, screen.width - 32, screen.height - 120);

            if (OWPhone.Call != OWPhone.CallState.None && app != App.Calls)
            {
                var cb = new Rect(body.x, body.y, body.width, 44);
                UI.Rounded(cb, new Color(0.1f, 0.45f, 0.2f, 0.9f), true);
                UI.Label(new Rect(cb.x + 12, cb.y, cb.width - 24, cb.height), OWPhone.CallLine, 16, Color.white);
                if (UI.Hit(cb)) Go(App.Calls);
                body.y += 52; body.height -= 52;
            }

            switch (app)
            {
                case App.Home: DrawHome(body); break;
                case App.Heists: Title(body, "HEISTS"); HeistBoardScreen.DrawList(Inner(body)); break;
                case App.Jobs: DrawJobs(body); break;
                case App.Garage: Title(body, "GARAGE"); GarageScreen.DrawBody(Inner(body), true); break;
                case App.Messages: DrawMessages(body); break;
                case App.Bank: DrawBank(body); break;
                case App.Calls: DrawCalls(body); break;
                case App.Emotes: DrawEmotes(body); break;
                case App.Stats: DrawStats(body); break;
            }

            // Home bar
            var hb = new Rect(screen.center.x - 90, screen.yMax - 66, 180, 50);
            if (UI.Button(hb, app == App.Home ? "CLOSE" : "HOME", ButtonKind.Ghost, null, 18)) { if (app == App.Home) { UIRoot.Remove(this); AudioManager.Ui("ui_close"); } else Go(App.Home); }
            if (!touch) UI.Label(new Rect(screen.x, screen.yMax - 20, screen.width, 18), "P: close phone  ·  M: map", 13, UI.Muted, TextAnchor.MiddleCenter);
        }

        static void Title(Rect body, string t) => UI.Label(new Rect(body.x, body.y, body.width, 44), t, 26, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
        static Rect Inner(Rect body) => new Rect(body.x, body.y + 54, body.width, body.height - 54);

        void DrawHome(Rect body)
        {
            UI.Label(new Rect(body.x, body.y + 10, body.width, 60), "PORT ECLIPSE", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
            UI.Label(new Rect(body.x, body.y + 64, body.width, 26), OWMap.District(PlayerController.Instance != null ? PlayerController.Instance.transform.position : Vector3.zero), 17, UI.Muted, TextAnchor.MiddleCenter);
            float cell = (body.width - 40) / 3f;
            for (int i = 0; i < Apps.Length; i++)
            {
                var ap = Apps[i];
                var r = new Rect(body.x + (i % 3) * (cell + 20), body.y + 120 + (i / 3) * (cell + 40), cell, cell);
                bool hover = UI.Hover(r);
                UI.Rounded(r, ap.color * (hover ? 1.15f : 0.9f));
                UI.Tex(new Rect(r.center.x - cell * 0.22f, r.center.y - cell * 0.26f, cell * 0.44f, cell * 0.44f), IconFactory.Get(ap.icon), Color.white);
                UI.Label(new Rect(r.x, r.yMax + 2, r.width, 30), ap.name, 16, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                int badge = ap.app == App.Messages ? OWPhone.TotalUnread : ap.app == App.Calls && OWPhone.Call == OWPhone.CallState.Incoming ? 1 : 0;
                if (badge > 0)
                {
                    UI.Tex(new Rect(r.xMax - 30, r.y - 8, 38, 38), TextureFactory.Circle, UI.Bad);
                    UI.Label(new Rect(r.xMax - 30, r.y - 8, 38, 38), badge.ToString(), 16, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                }
                if (UI.Hit(r)) Go(ap.app);
            }
        }

        // ------------------------------------------------------------------ map

        void DrawMapApp()
        {
            UI.Dim(0.75f);
            var full = UI.Centered(UI.VW - 60, UI.VH - 40);
            float side = Mathf.Min(480f, full.width * 0.32f);
            var mr = new Rect(full.x, full.y, full.width - side - 20, full.height);
            var me = PlayerController.Instance != null ? PlayerController.Instance.transform.position : Vector3.zero;
            if (me.y < -20f) me = OWMap.ApartmentOutside;
            if (mapFollow) mapCenter = me;
            UI.Rounded(new Rect(mr.x - 4, mr.y - 4, mr.width + 8, mr.height + 8), new Color(0, 0, 0, 0.6f));
            var blips = OWHud.Blips(true);
            OWHud.DrawMap(mr, mapCenter, mapRange, blips, mapRange < 120f);
            if (UI.Hit(mr))
            {
                var gui = Event.current.mousePosition / UI.Scale;
                var w = OWHud.RectToMap(mr, mapCenter, mapRange, gui);
                if (Mathf.Abs(w.x) < 190f && Mathf.Abs(w.z) < 190f) { OWDirector.Waypoint = w; AudioManager.Ui("ui_click"); }
            }
            UI.Label(new Rect(mr.x + 14, mr.y + 8, mr.width - 28, 30), "Tap the map to drop a pin. The minimap and the marker on screen guide you there.", 16, Color.white);

            // Side panel
            var sp = new Rect(mr.xMax + 20, full.y, side, full.height);
            UI.Rounded(sp, UI.PanelColor);
            float y = sp.y + 16, bw = side - 32;
            UI.Label(new Rect(sp.x + 16, y, bw, 40), "MAP", 28, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont); y += 48;
            float half = (bw - 10) / 2f;
            if (UI.Button(new Rect(sp.x + 16, y, half, 50), "ZOOM +", ButtonKind.Normal, null, 17)) { mapRange = Mathf.Max(40f, mapRange * 0.7f); if (mapRange < 199f && mapCenter == Vector3.zero) mapFollow = true; }
            if (UI.Button(new Rect(sp.x + 26 + half, y, half, 50), "ZOOM -", ButtonKind.Normal, null, 17)) { mapRange = Mathf.Min(200f, mapRange / 0.7f); if (mapRange >= 199f) { mapFollow = false; mapCenter = Vector3.zero; } }
            y += 60;
            if (UI.Button(new Rect(sp.x + 16, y, half, 50), "ON ME", ButtonKind.Normal, IconFactory.Get("person"), 16)) { mapFollow = true; mapRange = Mathf.Min(mapRange, 90f); }
            if (UI.Button(new Rect(sp.x + 26 + half, y, half, 50), "WHOLE CITY", ButtonKind.Normal, null, 16)) { mapFollow = false; mapCenter = Vector3.zero; mapRange = 200f; }
            y += 64;
            var pin = OWDirector.Waypoint;
            UI.Label(new Rect(sp.x + 16, y, bw, 26), pin.HasValue ? "Pin: " + OWMap.District(pin.Value) + " · " + Mathf.RoundToInt(MathX.Flat(pin.Value - me).magnitude) + " m away" : "No pin yet.", 16, pin.HasValue ? new Color(1f, 0.5f, 0.95f) : UI.Muted); y += 32;
            bool canTaxi = pin.HasValue && OWDirector.MyWanted == 0 && !OWHeists.InActiveCrew(OWDirector.MyId) && OWJobs.Active == OWJobs.Kind.None && OWCar.LocalDriving == null;
            if (UI.Button(new Rect(sp.x + 16, y, half, 54), "CLEAR PIN", ButtonKind.Normal, null, 16, pin.HasValue)) OWDirector.Waypoint = null;
            if (UI.Button(new Rect(sp.x + 26 + half, y, half, 54), "TAXI $" + TaxiCost, ButtonKind.Primary, IconFactory.Get("car"), 16, canTaxi && OWSave.Cash >= TaxiCost,
                "Go straight to your pin (not while wanted, on a heist, on a job or driving).")) Taxi(pin.Value);
            y += 66;

            UI.Label(new Rect(sp.x + 16, y, bw, 26), "PLACES (tap to pin)", 15, UI.Muted); y += 30;
            var view = new Rect(sp.x + 16, y, bw, sp.yMax - y - 90);
            var places = new List<OWBlip>();
            foreach (var b in blips) if (b.Label != null && b.Icon != "arrow_up") places.Add(b);
            float rowH = 50f, content = places.Count * (rowH + 6);
            float s = UI.BeginScroll("ow_places", view, content);
            for (int i = 0; i < places.Count; i++)
            {
                var b = places[i];
                var rr = new Rect(0, i * (rowH + 6) - s, view.width - 14, rowH);
                if (rr.yMax < 0 || rr.y > view.height) continue;
                UI.Rounded(rr, UI.Hover(rr) ? new Color(1, 1, 1, 0.12f) : UI.PanelLight, true);
                UI.Tex(new Rect(rr.x + 10, rr.y + 10, 30, 30), IconFactory.Get(b.Icon), b.Color);
                UI.Label(new Rect(rr.x + 50, rr.y, rr.width - 140, rowH), b.Label, 16, Color.white);
                UI.Label(new Rect(rr.xMax - 90, rr.y, 80, rowH), Mathf.RoundToInt(MathX.Flat(b.Pos - me).magnitude) + " m", 14, UI.Muted, TextAnchor.MiddleRight);
                if (UI.Hit(rr)) { OWDirector.Waypoint = b.Pos; mapCenter = b.Pos; mapFollow = false; mapRange = Mathf.Min(mapRange, 80f); AudioManager.Ui("ui_click"); }
            }
            UI.EndScroll("ow_places", view, content);
            if (UI.Button(new Rect(sp.x + 16, sp.yMax - 74, bw, 58), "BACK TO PHONE", ButtonKind.Normal, IconFactory.Get("arrow_left"), 18)) Go(App.Home);
        }

        const int TaxiCost = 250;

        void Taxi(Vector3 pin)
        {
            var p = PlayerController.Instance;
            if (p == null || !OWSave.Spend(TaxiCost)) return;
            if (!OWDirector.RoadSpotNear(pin, out var spot, out _)) spot = pin;
            p.Teleport(spot + Vector3.up * 0.4f);
            UIRoot.Remove(this);
            AudioManager.Ui("car_horn", 0.5f);
            GameEvents.ShowToast("Taxi: -$" + TaxiCost + ". Here you are.");
        }

        // ------------------------------------------------------------------ jobs

        void DrawJobs(Rect body)
        {
            Title(body, "JOBS");
            float y = body.y + 60, bw = body.width;
            if (OWJobs.Active != OWJobs.Kind.None)
            {
                UI.Rounded(new Rect(body.x, y, bw, 90), new Color(0.3f, 0.22f, 0.05f, 0.8f), true);
                UI.Label(new Rect(body.x + 14, y + 6, bw - 28, 80), OWJobs.Objective, 17, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, true);
                y += 100;
                if (UI.Button(new Rect(body.x, y, bw, 56), "CANCEL JOB", ButtonKind.Danger, null, 18)) OWJobs.Cancel();
                y += 72;
            }
            if (Job(ref y, body, "DELIVERY", "box", "Pick up a package across town and drop it off before the timer ends. $2,000 - $6,000.", OWJobs.Active == OWJobs.Kind.None))
            { OWJobs.StartDelivery(); UIRoot.Remove(this); }
            if (Job(ref y, body, "STREET RACE", "stopwatch", "Drive a car onto the checkered line in Industrial (stopwatch on the map). Beat par: $9,000.", true))
            { OWDirector.Waypoint = OWMap.Place("Race Start"); GameEvents.ShowToast("Race start pinned on your map."); UIRoot.Remove(this); }
            if (Job(ref y, body, "BOUNTY", "skull", OWDirector.BountyActive ? "A gang boss is out there right now (skull on the map). Kill him: $12,000." : "Put out a hit on a gang boss and his two guards. Whoever kills him gets $12,000.", !OWDirector.BountyActive))
            { NetManager.I.OWRequest(OWReq.Bounty); }
            if (Job(ref y, body, "HEISTS", "flag", "Big money with your crew. Go to the heist board at the Southside hangout, or open the HEISTS app.", true)) Go(App.Heists);
        }

        static bool Job(ref float y, Rect body, string name, string icon, string desc, bool enabled)
        {
            var r = new Rect(body.x, y, body.width, 116);
            UI.Rounded(r, UI.PanelLight, true);
            UI.Tex(new Rect(r.x + 14, r.y + 14, 44, 44), IconFactory.Get(icon), UI.Accent2);
            UI.Label(new Rect(r.x + 70, r.y + 8, r.width - 220, 34), name, 20, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Label(new Rect(r.x + 70, r.y + 42, r.width - 220, 70), desc, 14, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            bool go = UI.Button(new Rect(r.xMax - 138, r.y + 30, 124, 56), "START", ButtonKind.Primary, null, 17, enabled);
            y += 126;
            return go;
        }

        // ------------------------------------------------------------------ texts

        void DrawMessages(Rect body)
        {
            var contacts = OWPhone.Contacts();
            foreach (var id in OWPhone.Threads.Keys) if (!contacts.Contains(id)) contacts.Add(id);
            if (OpenThread == 255)
            {
                Title(body, "TEXTS");
                if (!NetState.Active) { UI.Label(Inner(body), "Texting works in multiplayer Open World: host or join a Port Eclipse server and message anyone on it.", 18, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true); return; }
                if (contacts.Count == 0) { UI.Label(Inner(body), "Nobody else is on the server yet.", 18, UI.Muted, TextAnchor.UpperLeft); return; }
                float y = body.y + 60;
                foreach (var id in contacts)
                {
                    var r = new Rect(body.x, y, body.width, 70);
                    UI.Rounded(r, UI.Hover(r) ? new Color(1, 1, 1, 0.12f) : UI.PanelLight, true);
                    UI.Tex(new Rect(r.x + 12, r.y + 15, 40, 40), IconFactory.Get("person"), new Color(0.35f, 0.75f, 1f));
                    UI.Label(new Rect(r.x + 64, r.y + 4, r.width - 140, 34), OWPhone.NameOf(id), 19, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                    var last = OWPhone.Threads.TryGetValue(id, out var th) && th.Count > 0 ? th[th.Count - 1].Body : "Say hi";
                    UI.Label(new Rect(r.x + 64, r.y + 36, r.width - 140, 28), last, 14, UI.Muted);
                    if (OWPhone.Unread.TryGetValue(id, out var u) && u > 0) { UI.Tex(new Rect(r.xMax - 56, r.y + 17, 36, 36), TextureFactory.Circle, UI.Bad); UI.Label(new Rect(r.xMax - 56, r.y + 17, 36, 36), u.ToString(), 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold); }
                    if (UI.Hit(r)) { OpenThread = id; OWPhone.Unread.Remove(id); draft = ""; }
                    y += 78;
                    if (y > body.yMax - 70) break;
                }
                return;
            }
            OWPhone.Unread.Remove(OpenThread);
            Title(body, OWPhone.NameOf(OpenThread));
            if (UI.Button(new Rect(body.xMax - 130, body.y + 2, 130, 42), "CALL", ButtonKind.Normal, IconFactory.Get("mobile"), 15, OWPhone.Call == OWPhone.CallState.None)) { OWPhone.StartCall(OpenThread); Go(App.Calls); return; }
            var list = OWPhone.Threads.TryGetValue(OpenThread, out var t) ? t : new List<OWPhone.Text>();
            var view = new Rect(body.x, body.y + 54, body.width, body.height - 54 - 70);
            float yy = view.yMax;
            for (int i = list.Count - 1; i >= 0 && yy > view.y + 40; i--)
            {
                var m = list[i];
                float tw = Mathf.Min(UI.TextWidth(m.Body, 17) + 30, view.width * 0.8f);
                float th2 = Mathf.Max(40f, UI.TextHeight(m.Body, 17, tw - 24) + 18);
                yy -= th2 + 22;
                var br = new Rect(m.Mine ? view.xMax - tw : view.x, yy, tw, th2);
                UI.Rounded(br, m.Mine ? new Color(0.2f, 0.5f, 1f, 0.9f) : new Color(0.25f, 0.25f, 0.3f, 0.95f), true);
                UI.Label(new Rect(br.x + 12, br.y + 4, br.width - 24, br.height - 8), m.Body, 17, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, false, true);
                UI.Label(new Rect(br.x, br.yMax, br.width, 18), m.Time, 12, UI.Muted, m.Mine ? TextAnchor.UpperRight : TextAnchor.UpperLeft);
            }
            var ir = new Rect(body.x, body.yMax - 60, body.width - 130, 56);
            draft = UI.TextField(ir, draft, "ow_text", false, "Message");
            if (draft.Length > 160) draft = draft.Substring(0, 160);
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == "ow_text";
            if (UI.Button(new Rect(ir.xMax + 10, ir.y, 120, 56), "SEND", ButtonKind.Primary, null, 18, draft.Trim().Length > 0) || (enter && draft.Trim().Length > 0))
            {
                OWPhone.SendText(OpenThread, draft);
                draft = "";
            }
        }

        // ------------------------------------------------------------------ bank

        void DrawBank(Rect body)
        {
            Title(body, "BANK OF PORT ECLIPSE");
            float y = body.y + 60;
            UI.Rounded(new Rect(body.x, y, body.width, 100), new Color(0.1f, 0.35f, 0.18f, 0.9f), true);
            UI.Label(new Rect(body.x + 18, y + 6, body.width - 36, 30), "Balance", 16, new Color(0.8f, 1f, 0.85f));
            UI.Label(new Rect(body.x + 18, y + 36, body.width - 36, 56), "$" + OWSave.Cash.ToString("N0"), 38, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
            y += 116;
            UI.Label(new Rect(body.x, y, body.width, 28), "Earned in total: $" + OWSave.D.totalEarned.ToString("N0"), 15, UI.Muted); y += 36;
            if (!NetState.Active) { UI.Label(new Rect(body.x, y, body.width, 120), "Sending money works in multiplayer Open World.", 17, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true); return; }
            UI.Label(new Rect(body.x, y, body.width, 30), "SEND MONEY TO", 16, UI.Muted); y += 34;
            var contacts = OWPhone.Contacts();
            if (contacts.Count == 0) { UI.Label(new Rect(body.x, y, body.width, 40), "Nobody else is on the server.", 16, UI.Muted); return; }
            if (!contacts.Contains(bankTo)) bankTo = contacts[0];
            int idx = contacts.IndexOf(bankTo);
            var names = contacts.ConvertAll(OWPhone.NameOf).ToArray();
            UI.Selector(new Rect(body.x, y, body.width, 56), "Player", ref idx, names);
            bankTo = contacts[Mathf.Clamp(idx, 0, contacts.Count - 1)];
            y += 66;
            UI.Label(new Rect(body.x, y, 120, 56), "Amount $", 18, Color.white);
            amount = UI.TextField(new Rect(body.x + 120, y, body.width - 120, 56), amount, "ow_amount", false, "0");
            amount = Digits(amount);
            y += 66;
            long[] quick = { 100, 1000, 10000, 50000 };
            float qw = (body.width - 30) / 4f;
            for (int i = 0; i < quick.Length; i++)
                if (UI.Button(new Rect(body.x + i * (qw + 10), y, qw, 48), "$" + (quick[i] >= 1000 ? quick[i] / 1000 + "K" : quick[i].ToString()), ButtonKind.Normal, null, 16)) amount = quick[i].ToString();
            y += 60;
            long.TryParse(amount, out long n);
            if (UI.Button(new Rect(body.x, y, body.width, 64), "SEND $" + n.ToString("N0") + " TO " + names[Mathf.Clamp(idx, 0, names.Length - 1)].ToUpperInvariant(), ButtonKind.Primary, IconFactory.Get("gift"), 18,
                n > 0 && n <= OWSave.Cash && n <= OWPhone.MaxTransfer, "Up to $" + OWPhone.MaxTransfer.ToString("N0") + " per transfer."))
                OWPhone.SendMoney(bankTo, n);
        }

        static string Digits(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in s) if (char.IsDigit(c) && sb.Length < 9) sb.Append(c);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ calls

        void DrawCalls(Rect body)
        {
            Title(body, "CALLS");
            float y = body.y + 60;
            var st = OWPhone.Call;
            if (st != OWPhone.CallState.None)
            {
                var r = new Rect(body.x, y, body.width, 250);
                UI.Rounded(r, new Color(0.08f, 0.3f, 0.16f, 0.9f), true);
                UI.Tex(new Rect(r.center.x - 40, r.y + 20, 80, 80), IconFactory.Get("person"), Color.white);
                UI.Label(new Rect(r.x, r.y + 108, r.width, 40), OWPhone.NameOf(OWPhone.Peer), 28, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                UI.Label(new Rect(r.x, r.y + 146, r.width, 30), OWPhone.CallLine, 16, new Color(0.8f, 1f, 0.85f), TextAnchor.MiddleCenter);
                float bw = (r.width - 60) / 2f;
                if (st == OWPhone.CallState.Incoming)
                {
                    if (UI.Button(new Rect(r.x + 20, r.yMax - 70, bw, 56), "ACCEPT", ButtonKind.Primary, IconFactory.Get("check"), 18)) OWPhone.Accept();
                    if (UI.Button(new Rect(r.x + 40 + bw, r.yMax - 70, bw, 56), "DECLINE", ButtonKind.Danger, IconFactory.Get("cross"), 18)) OWPhone.HangUp();
                }
                else
                {
                    if (st == OWPhone.CallState.Talking && UI.Button(new Rect(r.x + 20, r.yMax - 70, bw, 56), OWPhone.Muted ? "UNMUTE" : "MUTE", ButtonKind.Normal, IconFactory.Get(OWPhone.Muted ? "mute" : "speaker"), 18)) OWPhone.Muted = !OWPhone.Muted;
                    if (UI.Button(new Rect(r.x + 40 + bw, r.yMax - 70, bw, 56), "HANG UP", ButtonKind.Danger, IconFactory.Get("cross"), 18)) OWPhone.HangUp();
                }
                y += 266;
                if (st == OWPhone.CallState.Talking) UI.Label(new Rect(body.x, y, body.width, 50), "You hear each other anywhere in the city. Your mic is on while the call lasts" + (OWPhone.Muted ? " (muted now)." : "."), 15, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                return;
            }
            if (!NetState.Active) { UI.Label(Inner(body), "Voice calls work in multiplayer Open World. Call anyone on the server: if they accept, you talk through the phone wherever you both are.", 18, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true); return; }
            if (Settings.Data.voiceMode == 2) { UI.Label(new Rect(body.x, y, body.width, 60), "Voice chat is off (Settings > Voice). Turn it on to make calls.", 16, UI.Warn, TextAnchor.UpperLeft, FontStyle.Normal, true, true); y += 64; }
            var contacts = OWPhone.Contacts();
            if (contacts.Count == 0) { UI.Label(new Rect(body.x, y, body.width, 40), "Nobody else is on the server yet.", 17, UI.Muted); return; }
            foreach (var id in contacts)
            {
                var r = new Rect(body.x, y, body.width, 70);
                UI.Rounded(r, UI.PanelLight, true);
                UI.Tex(new Rect(r.x + 12, r.y + 15, 40, 40), IconFactory.Get("person"), new Color(0.35f, 0.75f, 1f));
                UI.Label(new Rect(r.x + 64, r.y, r.width - 260, r.height), OWPhone.NameOf(id), 19, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                if (UI.Button(new Rect(r.xMax - 186, r.y + 10, 80, 50), "TEXT", ButtonKind.Normal, null, 14)) { app = App.Messages; OpenThread = id; }
                if (UI.Button(new Rect(r.xMax - 96, r.y + 10, 86, 50), "CALL", ButtonKind.Primary, null, 15, Settings.Data.voiceMode != 2)) OWPhone.StartCall(id);
                y += 78;
                if (y > body.yMax - 70) break;
            }
        }

        // ------------------------------------------------------------------ emotes + stats

        void DrawEmotes(Rect body)
        {
            Title(body, "EMOTES");
            UI.Label(new Rect(body.x, body.y + 46, body.width, 26), Settings.TouchControlsActive ? "Tap to play. Buy more at the Eclipse Boutique." : "Tap to play. J plays your last emote. More at the Eclipse Boutique.", 14, UI.Muted);
            var list = OWItems.In(OWCat.Emote);
            float cw = (body.width - 20) / 2f, y = body.y + 84;
            for (int i = 0; i < list.Count; i++)
            {
                var it = list[i];
                bool own = OWSave.Owns(it.Id);
                var r = new Rect(body.x + (i % 2) * (cw + 20), y + (i / 2) * 84, cw, 74);
                if (UI.Button(r, own ? it.Name.ToUpperInvariant() : it.Name + " ($" + it.Price.ToString("N0") + ")", own ? (OWSave.D.emote == it.Id ? ButtonKind.Primary : ButtonKind.Normal) : ButtonKind.Ghost, IconFactory.Get(own ? "hand" : "lock"), 16, own))
                { OWDirector.PlayEmote(it.Id); UIRoot.Remove(this); }
            }
        }

        void DrawStats(Rect body)
        {
            Title(body, "STATS");
            var d = OWSave.D;
            int owned = 0; foreach (var it in OWItems.All) if (it.Cat != OWCat.Gun && OWSave.Owns(it.Id)) owned++;
            int guns = 0; foreach (var it in OWItems.In(OWCat.Gun)) if (OWSave.Owns(it.Id)) guns++;
            var lines = new[]
            {
                ("Cash", "$" + d.cash.ToString("N0")), ("Earned in total", "$" + d.totalEarned.ToString("N0")),
                ("Heists done", d.heists.ToString()), ("Jobs done", d.jobs.ToString()), ("Races won", d.races.ToString()),
                ("Collection", owned + " / " + OWItems.CollectionCount + " items"), ("Guns", guns + " / " + OWItems.In(OWCat.Gun).Count),
                ("Car", string.IsNullOrEmpty(d.car) ? "none" : OWItems.Get(d.car)?.Name ?? "none"), ("Wanted", OWDirector.MyWanted + " stars"),
            };
            float y = body.y + 60;
            foreach (var (k, v) in lines)
            {
                UI.Fill(new Rect(body.x, y + 47, body.width, 1), new Color(1, 1, 1, 0.08f));
                UI.Label(new Rect(body.x, y, body.width / 2, 48), k, 18, UI.Muted);
                UI.Label(new Rect(body.x + body.width / 2, y, body.width / 2, 48), v, 18, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
                y += 50;
            }
        }
    }
}
