using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// F5 developer panel, locked behind a login. After login: live Unity console output with filters and
    /// stack traces, a command line (Tab completion, history, ';' chaining), and tabs of testing tools:
    /// quick actions, spawning, AI, player, rules, a sound board for every synthesised sound, a performance
    /// graph and system info.
    /// </summary>
    public sealed class DevPanel : MonoBehaviour
    {
        static DevPanel I;
        public static bool Open { get; private set; }
        public static bool LoggedIn { get; private set; }
        public static int QuickActionCount => Quick.Length;

        // Credentials are compared as SHA-256 hashes (username is case-insensitive).
        const string UserHash = "8e6352a433843006b883755f46675107ca995aca114432425af4c1c8693a481a";
        const string PassHash = "98c8e8a335e4ca7593513a225dc12fa6d61e49acee09aa3dcb48f623d7162d22";

        string user = "", pass = "", error = "", command = "", soundSearch = "", logSearch = "";
        int failed, historyIndex = -1, tab;
        float lockedUntil, shake, openedAt;
        bool showLog = true, showWarn = true, showError = true, collapse, autoScroll = true, focusCommand;
        int expanded = -1;
        readonly float[] fpsHistory = new float[240];
        int fpsHead;
        static readonly string[] Tabs = { "CONSOLE", "QUICK", "SPAWN", "AI", "PLAYER", "RULES", "SOUNDS", "PERF", "INFO", "FUN", "MOBILE" };
        bool showHistory;

        /// <summary>True on phones/tablets: bigger rows, the native keyboard and a full-width panel.</summary>
        static bool Touch => Settings.TouchControlsActive || Application.isMobilePlatform;
        static float zoom = -1f;
        /// <summary>Extra magnification of the whole panel (GUI matrix), default 1.3x on touch devices.</summary>
        public static float Zoom
        {
            get
            {
                if (zoom < 0f)
                {
                    try { zoom = PlayerPrefs.GetFloat("dev_panel_zoom", Touch ? 1.3f : 1f); } catch { zoom = 1f; }
                }
                return zoom;
            }
            set
            {
                zoom = Mathf.Clamp(value, 0.8f, 2f);
                try { PlayerPrefs.SetFloat("dev_panel_zoom", zoom); PlayerPrefs.Save(); } catch { }
            }
        }
        float W => UI.VW / Zoom;
        float H => UI.VH / Zoom;
        int rulesTab;

        static readonly (string label, string cmd)[] Quick =
        {
            ("God mode", "god"), ("Noclip", "noclip"), ("No fall damage", "nofall"), ("AI ignore me", "ai_ignore"), ("Freeze AI", "ai_freeze"),
            ("AI thoughts", "ai_thoughts"), ("Infinite ammo", "ammo"), ("Instant reload", "fast_reload"), ("Give all weapons", "give_all"), ("Refill ammo", "refill"),
            ("Heal me", "heal"), ("Respawn", "respawn"), ("TP to aim", "tp_aim"), ("TP to spawn", "tp_spawn"), ("Launch me", "launch_me"),
            ("Spawn Goredoll", "goredoll"), ("Spawn 5 cowards", "goredoll coward 5"), ("Spawn 5 aggressive", "goredoll aggressive 5"), ("Horde (10)", "horde 10"), ("Horde (25)", "horde 25 12"),
            ("Kill all", "kill_all"), ("Revive all", "revive_all"), ("Gib all heads", "gib_all"), ("Decapitate all", "decap_all"), ("Disarm all", "disarm_all"),
            ("Legless all", "legless_all"), ("Launch all", "launch_all"), ("Explode all", "explode_all"), ("Dance party", "dance_all"), ("Panic all", "panic_all"),
            ("Calm all", "calm_all"), ("Anger all", "anger_all"), ("Paralyse all", "paralyze_all"), ("Trip all", "trip_all"), ("Remove dead", "despawn_dead"),
            ("Remove all NPCs", "despawn_all"), ("Infighting", "infighting"), ("Explosion at aim", "explode"), ("NUKE", "nuke"), ("Lightning", "lightning"),
            ("Explosive bullets", "explosive_bullets"), ("Super kick", "superkick"), ("Always dismember", "always_dismember"), ("One-hit kill", "onehit"), ("No bleeding", "no_bleeding"),
            ("Big heads", "bigheads"), ("X-ray organs", "xray"), ("Colliders", "colliders"), ("Wireframe", "wireframe"), ("Tracers", "tracers"),
            ("Slow-mo", "slowmo"), ("Time ×0.1", "timescale 0.1"), ("Time ×1", "timescale 1"), ("Time ×2", "timescale 2"), ("Moon gravity", "moon"),
            ("Zero gravity", "gravity 0"), ("Normal gravity", "gravity 1"), ("Heavy gravity", "gravity 3"), ("Day", "lighting day"), ("Sunset", "lighting sunset"),
            ("Night", "lighting night"), ("Blood moon", "lighting bloodmoon"), ("Toggle fog", "fog"), ("Rain melons", "rain watermelon 20"), ("Rain barrels", "rain explosive_barrel 8"),
            ("Rain balls", "rain ball 30"), ("Nextbot", "nextbot grinner"), ("Clear decals", "clear_decals"), ("Clear debris", "clear_debris"), ("Clear everything", "clear_all"),
            ("Undo spawn", "undo"), ("Screenshot", "screenshot"), ("Toggle HUD", "hud"), ("List NPCs", "npcs"), ("Inspect aimed", "inspect"),
            ("Gore: extreme", "gore 3"), ("Gore: full", "gore 2"), ("Gore: off", "gore 0"), ("Green blood", "blood_color 2"), ("Red blood", "blood_color 0"),
            ("Log test", "log_test"), ("Exception test", "exception_test"), ("Memory", "mem"), ("Force GC", "gc"), ("System info", "sysinfo"),
            ("Solver 12", "solver 12 2"), ("Physics 120 Hz", "physics_rate 120"), ("Physics 60 Hz", "physics_rate 60"), ("Unlock all", "unlock_all"), ("Restart map", "restart"),
        };

        public static void Create(Transform parent)
        {
            var go = new GameObject("DevPanel");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<DevPanel>();
            UIRoot.TopLayer += I.Draw;
            MobileDev.Create(parent);
        }

        public static void Toggle()
        {
            Open = !Open;
            UIRoot.DevPanelCapturing = Open;
            if (I != null) { I.openedAt = Time.unscaledTime; I.focusCommand = true; }
            AudioManager.Ui(Open ? "ui_open" : "ui_close");
        }

        static string Hash(string s)
        {
            using (var sha = SHA256.Create())
            {
                var b = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
                var sb = new StringBuilder(64);
                foreach (var x in b) sb.Append(x.ToString("x2"));
                return sb.ToString();
            }
        }

        void Update()
        {
            fpsHistory[fpsHead] = 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            fpsHead = (fpsHead + 1) % fpsHistory.Length;
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 3f);
            if (Open && Input.GetKeyDown(KeyCode.Escape)) Toggle();
            if (Open) UIRoot.DevPanelCapturing = true;
        }

        void TryLogin()
        {
            if (Time.unscaledTime < lockedUntil) return;
            if (Hash(user.Trim().ToLowerInvariant()) == UserHash && Hash(pass) == PassHash)
            {
                LoggedIn = true;
                error = "";
                pass = "";
                failed = 0;
                AudioManager.Ui("ui_success");
                Stats.Add(Stats.DevLogins);
                Debug.Log("[DevPanel] Access granted. Welcome, " + user.Trim() + ".");
                focusCommand = true;
            }
            else
            {
                failed++;
                shake = 1f;
                pass = "";
                AudioManager.Ui("ui_error");
                Debug.LogWarning("[DevPanel] Failed login attempt #" + failed + " for '" + user + "'.");
                if (failed >= 5) { lockedUntil = Time.unscaledTime + 30f; failed = 0; error = "Too many attempts. Locked for 30 seconds."; }
                else error = "Wrong username or password. (" + (5 - failed) + " tries left)";
            }
        }

        void Draw()
        {
            if (!Open) return;
            GUI.enabled = true;
            UI.Dim(0.35f);
            var oldMatrix = GUI.matrix;
            if (!Mathf.Approximately(Zoom, 1f)) GUI.matrix = oldMatrix * Matrix4x4.Scale(new Vector3(Zoom, Zoom, 1f));
            MobileDev.TrackPointer();
            try { if (!LoggedIn) DrawLogin(); else DrawPanel(); }
            finally { GUI.matrix = oldMatrix; }
        }

        // ------------------------------------------------------------------ login

        void DrawLogin()
        {
            var e = Event.current;
            float sx = Mathf.Sin(Time.unscaledTime * 60f) * 14f * shake;
            var r = new Rect((W - 560) / 2f, Mathf.Max(10f, (H - 480) / 2f - (TouchScreenKeyboard.visible ? H * 0.22f : 0f)), 560, 480);
            r.x += sx;
            UI.Panel(r, "Developer Access");
            UI.Tex(new Rect(r.x + 28, r.y + 84, 56, 56), IconFactory.Get("lock"), UI.Accent2);
            UI.Label(new Rect(r.x + 100, r.y + 80, r.width - 130, 64), "This panel is restricted to " + GameInfo.Studio + " staff.\nLog in to continue.", 17, UI.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, true, true);
            UI.Label(new Rect(r.x + 28, r.y + 158, 200, 26), "USERNAME", 14, UI.Muted);
            user = TouchKeys.Field(new Rect(r.x + 28, r.y + 184, r.width - 56, 52), user, "dev_user", false, "username", out bool userDone);
            if (userDone) TouchKeys.Open("dev_pass", pass, true, "password");
            UI.Label(new Rect(r.x + 28, r.y + 246, 200, 26), "PASSWORD", 14, UI.Muted);
            pass = TouchKeys.Field(new Rect(r.x + 28, r.y + 272, r.width - 56, 52), pass, "dev_pass", true, "password", out bool passDone);
            if (passDone && pass.Length > 0) TryLogin();
            bool locked = Time.unscaledTime < lockedUntil;
            if (!string.IsNullOrEmpty(error)) UI.Label(new Rect(r.x + 28, r.y + 330, r.width - 56, 30), locked ? "Locked — try again in " + Mathf.CeilToInt(lockedUntil - Time.unscaledTime) + "s" : error, 15, UI.Bad);
            if (focusCommand && e.type == EventType.Repaint && !TouchKeys.Native) { GUI.FocusControl(string.IsNullOrEmpty(user) ? "dev_user" : "dev_pass"); focusCommand = false; }
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                if (GUI.GetNameOfFocusedControl() == "dev_user") GUI.FocusControl("dev_pass"); else TryLogin();
                e.Use();
            }
            if (UI.Button(new Rect(r.x + 28, r.yMax - 92, 240, 62), "LOG IN", ButtonKind.Primary, IconFactory.Get("key"), 22, !locked)) TryLogin();
            if (UI.Button(new Rect(r.xMax - 228, r.yMax - 92, 200, 62), "CANCEL", ButtonKind.Normal)) Toggle();
        }

        // ------------------------------------------------------------------ main panel

        void DrawPanel()
        {
            float left = Touch ? 12f : Mathf.Max(20, W * 0.32f);
            var r = Touch ? new Rect(left, 8, W - left * 2, H - 16) : new Rect(left, 20, W - left - 20, H - 40);
            UI.Panel(r, "Dev Panel");
            UI.Label(new Rect(r.x + 260, r.y + 22, r.width - 290 - (Touch ? 170 : 0), 34), $"{DevConsole.Commands.Count} commands | {Quick.Length} quick actions | {SoundBank.SoundCount} sounds | " + (Touch ? "hold 3 fingers to close" : "F5/Esc to close"), Touch ? 17 : 15, UI.Muted, TextAnchor.MiddleRight);
            if (Touch && MobileDev.Tap(new Rect(r.xMax - 170, r.y + 14, 150, 50), "CLOSE", ButtonKind.Danger, 20)) { Toggle(); return; }
            int nt = UI.Tabs(new Rect(r.x + 20, r.y + 72, r.width - 40, Touch ? 56 : 44), tab, Tabs, Touch ? 17 : 15);
            if (nt != tab) { tab = nt; AudioManager.Ui("ui_tab"); }
            var body = Touch ? new Rect(r.x + 20, r.y + 140, r.width - 40, r.height - 152) : new Rect(r.x + 20, r.y + 128, r.width - 40, r.height - 148);
            switch (tab)
            {
                case 0: DrawConsole(body); break;
                case 1: DrawButtons(body, "quick", Quick); break;
                case 2: DrawSpawn(body); break;
                case 3: DrawAi(body); break;
                case 4: DrawPlayer(body); break;
                case 5:
                    rulesTab = UI.Tabs(new Rect(body.x, body.y, body.width, 40), rulesTab, new[] { "PLAYER", "GOREDOLLS", "WORLD", "VISUAL" }, 14);
                    GameRulesPanel.Draw(new Rect(body.x, body.y + 52, body.width, body.height - 52), rulesTab);
                    break;
                case 6: DrawSounds(body); break;
                case 7: DrawPerf(body); break;
                case 8: DrawInfo(body); break;
                case 9: DevFunTab.DrawFun(body); break;
                case 10: DevFunTab.DrawMobile(body); break;
            }
        }

        void DrawConsole(Rect body)
        {
            var e = Event.current;
            float x = body.x;
            UI.Toggle(new Rect(x, body.y, 200, 40), "Log " + DevConsole.Logs, ref showLog); x += 210;
            UI.Toggle(new Rect(x, body.y, 210, 40), "Warn " + DevConsole.Warnings, ref showWarn); x += 220;
            UI.Toggle(new Rect(x, body.y, 210, 40), "Error " + DevConsole.Errors, ref showError); x += 220;
            UI.Toggle(new Rect(x, body.y, 180, 40), "Collapse", ref collapse); x += 190;
            UI.Toggle(new Rect(x, body.y, 200, 40), "Auto-scroll", ref autoScroll);
            if (UI.Button(new Rect(body.xMax - 220, body.y, 100, 40), "COPY", ButtonKind.Ghost, null, 15))
                GUIUtility.systemCopyBuffer = string.Join("\n", DevConsole.Entries.Select(en => "[" + en.Type + "] " + en.Message));
            if (UI.Button(new Rect(body.xMax - 110, body.y, 110, 40), "CLEAR", ButtonKind.Ghost, null, 15)) DevConsole.Clear();
            logSearch = TouchKeys.Field(new Rect(body.x, body.y + 48, body.width, Touch ? 48 : 40), logSearch, "dev_logsearch", false, "Filter output...", out _);

            float bottomH = Touch ? 66f : 52f;
            float top = Touch ? 104f : 96f;
            var view = new Rect(body.x, body.y + top, body.width, body.height - top - bottomH - 8);
            if (showHistory)
            {
                var hist = new Rect(view.xMax - Mathf.Min(560, view.width * 0.42f), view.y, Mathf.Min(560, view.width * 0.42f), view.height);
                view.width -= hist.width + 10;
                DrawHistory(hist);
            }
            UI.Rounded(view, new Color(0, 0, 0, 0.5f), false);
            var list = new List<DevConsole.Entry>();
            foreach (var en in DevConsole.Entries)
            {
                bool err = en.Type == LogType.Error || en.Type == LogType.Exception || en.Type == LogType.Assert;
                if (err && !showError || en.Type == LogType.Warning && !showWarn || en.Type == LogType.Log && !showLog) continue;
                if (logSearch.Length > 0 && en.Message.IndexOf(logSearch, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (collapse) { int idx = list.FindIndex(o => o.Message == en.Message); if (idx >= 0) { var o = list[idx]; o.Count += en.Count; list[idx] = o; continue; } }
                list.Add(en);
            }
            float lineH = Touch ? 36f : 26f;
            int logSize = Touch ? 18 : 14;
            float content = 0;
            var heights = new float[list.Count];
            for (int i = 0; i < list.Count; i++) { heights[i] = i == expanded ? lineH + Mathf.Min(300, UI.TextHeight(list[i].Stack ?? "", logSize - 1, view.width - 40)) + 10 : lineH; content += heights[i]; }
            string sid = "devlog";
            if ((e.type == EventType.ScrollWheel || e.type == EventType.MouseDrag) && UI.R(view).Contains(e.mousePosition)) autoScroll = false;
            if (autoScroll) UI.SetScroll(sid, Mathf.Max(0, content - view.height));
            float s = UI.BeginScroll(sid, view, content);
            float y = -s;
            for (int i = 0; i < list.Count; i++)
            {
                var en = list[i];
                float h = heights[i];
                if (y + h >= 0 && y <= view.height)
                {
                    var row = new Rect(0, y, view.width - 12, h);
                    bool err = en.Type == LogType.Error || en.Type == LogType.Exception || en.Type == LogType.Assert;
                    var c = err ? UI.Bad : en.Type == LogType.Warning ? UI.Warn : en.Message.StartsWith("> ") ? UI.Accent2 : UI.TextColor;
                    if (i % 2 == 0) UI.Fill(row, new Color(1, 1, 1, 0.03f));
                    string first = en.Message.Split('\n')[0];
                    string time = en.Time >= 0 ? "[" + en.Time.ToString("0.0") + "] " : "";
                    UI.Label(new Rect(8, y, row.width - 70, lineH), time + first, logSize, c, TextAnchor.MiddleLeft, FontStyle.Normal, false);
                    if (en.Count > 1) UI.Label(new Rect(row.width - 60, y, 56, lineH), "x" + en.Count, logSize - 1, UI.Muted, TextAnchor.MiddleRight);
                    if (i == expanded)
                    {
                        string detail = en.Message.Contains("\n") ? en.Message + "\n" : "";
                        UI.Label(new Rect(24, y + lineH, row.width - 40, h - lineH), detail + (string.IsNullOrEmpty(en.Stack) ? "(no stack trace)" : en.Stack), logSize - 1, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, false, true);
                    }
                    if (UI.Hit(row) && !MobileDev.PointerDragged) { expanded = expanded == i ? -1 : i; autoScroll = false; }
                }
                y += h;
            }
            UI.EndScroll(sid, view, content);

            // Command line
            var input = new Rect(body.x, body.yMax - bottomH, body.width - 140 - 170, bottomH);
            if (e.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == "dev_cmd")
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { Run(); e.Use(); }
                else if (e.keyCode == KeyCode.UpArrow && DevConsole.History.Count > 0)
                {
                    historyIndex = historyIndex < 0 ? DevConsole.History.Count - 1 : Mathf.Max(0, historyIndex - 1);
                    command = DevConsole.History[historyIndex]; e.Use();
                }
                else if (e.keyCode == KeyCode.DownArrow && historyIndex >= 0)
                {
                    historyIndex++;
                    if (historyIndex >= DevConsole.History.Count) { historyIndex = -1; command = ""; } else command = DevConsole.History[historyIndex];
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Tab || e.character == '\t') { command = DevConsole.Complete(command); e.Use(); MoveCursorToEnd(); }
            }
            command = TouchKeys.Field(input, command, "dev_cmd", false, TouchKeys.Native ? "Tap to type a command (try 'help')..." : "Type a command (try 'help')...", out bool submitted);
            if (submitted) Run();
            if (focusCommand && e.type == EventType.Repaint) { if (!TouchKeys.Native) GUI.FocusControl("dev_cmd"); focusCommand = false; }
            if (MobileDev.Tap(new Rect(input.xMax + 10, input.y, 160, bottomH), showHistory ? "HIDE HIST" : "HISTORY", ButtonKind.Normal, 18, showHistory)) showHistory = !showHistory;
            if (UI.Button(new Rect(input.xMax + 180, input.y, 130, bottomH), "RUN", ButtonKind.Primary, null, 18)) Run();
        }

        /// <summary>Recent commands, newest first: tap to run again, EDIT to put it in the command line.</summary>
        void DrawHistory(Rect view)
        {
            UI.Rounded(view, new Color(0, 0, 0, 0.5f), false);
            var hist = DevConsole.History;
            float rowH = Touch ? 64f : 48f;
            var head = new Rect(view.x + 10, view.y + 4, view.width - 20, 32);
            UI.Label(head, hist.Count == 0 ? "No commands yet" : "HISTORY - tap to run", 16, UI.Accent2, TextAnchor.MiddleLeft, FontStyle.Bold);
            var list = new Rect(view.x, view.y + 40, view.width, view.height - 40);
            float content = hist.Count * (rowH + 6);
            float s = UI.BeginScroll("devhistory", list, content);
            string run = null, edit = null;
            for (int k = 0; k < hist.Count; k++)
            {
                string cmd = hist[hist.Count - 1 - k];
                var r = new Rect(6, k * (rowH + 6) - s, list.width - 18, rowH);
                if (r.yMax < 0 || r.y > list.height) continue;
                if (MobileDev.Tap(new Rect(r.x, r.y, r.width - 96, r.height), cmd, ButtonKind.Normal, Touch ? 18 : 15)) run = cmd;
                if (MobileDev.Tap(new Rect(r.xMax - 90, r.y, 90, r.height), "EDIT", ButtonKind.Ghost, 15)) edit = cmd;
            }
            UI.EndScroll("devhistory", list, content);
            if (run != null) { DevConsole.Execute(run); autoScroll = true; AudioManager.Ui("ui_click"); }
            if (edit != null) { command = edit; if (TouchKeys.Native) TouchKeys.Open("dev_cmd", command, false, "command"); }
        }

        static void MoveCursorToEnd()
        {
            var te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            if (te != null) te.MoveTextEnd();
        }

        void Run()
        {
            if (string.IsNullOrWhiteSpace(command)) return;
            DevConsole.Execute(command);
            command = "";
            historyIndex = -1;
            autoScroll = true;
            focusCommand = true;
            AudioManager.Ui("ui_click");
        }

        void DrawButtons(Rect body, string id, (string label, string cmd)[] buttons)
        {
            float bw = Touch ? 290 : 230, bh = Touch ? 72 : 50, gap = Touch ? 12 : 10;
            int cols = Mathf.Max(1, Mathf.FloorToInt((body.width - 12 + gap) / (bw + gap)));
            float content = Mathf.CeilToInt(buttons.Length / (float)cols) * (bh + gap);
            float s = UI.BeginScroll(id, body, content);
            for (int i = 0; i < buttons.Length; i++)
            {
                var r = new Rect((i % cols) * (bw + gap), (i / cols) * (bh + gap) - s, bw, bh);
                if (r.yMax < 0 || r.y > body.height) continue;
                if (UI.Button(r, buttons[i].label, ButtonKind.Normal, null, Touch ? 19 : 15, true, buttons[i].cmd) && !MobileDev.PointerDragged) { DevConsole.Execute(buttons[i].cmd); AudioManager.Ui("ui_click"); }
            }
            UI.EndScroll(id, body, content);
        }

        void DrawSpawn(Rect body)
        {
            var list = new List<(string, string)>();
            foreach (var p in GoredollFactory.Presets) list.Add(("Goredoll: " + p, "goredoll " + p));
            foreach (var d in SpawnCatalog.Items) list.Add(("Item: " + d.Name, "spawn " + d.Id));
            foreach (var d in SpawnCatalog.Props) list.Add((d.Name, "spawn " + d.Id));
            foreach (var d in SpawnCatalog.Entities) list.Add(("Nextbot: " + d.Name, "spawn " + d.Id));
            foreach (var w in WeaponCatalog.All) list.Add(("Give " + w.Name, "give " + w.Id));
            DrawButtons(body, "devspawn", list.ToArray());
        }

        void DrawAi(Rect body)
        {
            var top = new Rect(body.x, body.y, body.width, 240);
            DrawButtons(top, "devai", Quick.Skip(15).Take(25).ToArray());
            var view = new Rect(body.x, body.y + 252, body.width, body.height - 252);
            float content = Goredoll.All.Count * 56f;
            float s = UI.BeginScroll("devroster", view, content);
            for (int i = 0; i < Goredoll.All.Count; i++)
            {
                var g = Goredoll.All[i];
                if (g == null) continue;
                var r = new Rect(0, i * 56f - s, view.width - 12, 50);
                if (r.yMax < 0 || r.y > view.height) continue;
                UI.Rounded(r, new Color(1, 1, 1, 0.05f), true);
                UI.Label(new Rect(10, r.y, 360, 50), g.DisplayName + " — " + g.StatusLine, 15, g.IsDead ? UI.Bad : UI.TextColor);
                UI.Label(new Rect(370, r.y, 300, 50), (g.Brain != null ? g.Brain.Action + ": " + g.Brain.Thought : ""), 13, UI.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, false, true);
                float bx = r.xMax - 5 * 106;
                if (UI.Button(new Rect(bx, r.y + 5, 100, 40), "Inspect", ButtonKind.Ghost, null, 14)) { Toggle(); UIRoot.Push(new WoundInspectorScreen(g, null)); }
                if (UI.Button(new Rect(bx + 106, r.y + 5, 100, 40), "TP to", ButtonKind.Ghost, null, 14) && PlayerController.Instance != null) PlayerController.Instance.Teleport(g.Position + Vector3.back * 2f + Vector3.up * 0.2f);
                if (UI.Button(new Rect(bx + 212, r.y + 5, 100, 40), "Revive", ButtonKind.Ghost, null, 14)) g.Revive();
                if (UI.Button(new Rect(bx + 318, r.y + 5, 100, 40), "Kill", ButtonKind.Ghost, null, 14)) g.Kill("Dev panel");
                if (UI.Button(new Rect(bx + 424, r.y + 5, 100, 40), "Remove", ButtonKind.Danger, null, 14)) g.Despawn();
            }
            UI.EndScroll("devroster", view, content);
        }

        void DrawPlayer(Rect body)
        {
            var p = PlayerController.Instance;
            if (p == null) { UI.Label(body, "Load a map to use player tools.", 20, UI.Muted, TextAnchor.UpperLeft); return; }
            float y = body.y;
            UI.Label(new Rect(body.x, y, body.width, 30), $"Health {p.Health.Current:0}/{p.Health.Max:0} · Position {p.transform.position:F1} · Speed {p.Speed:0.0} m/s · Grounded {p.Grounded} · Noclip {p.Noclip}", 16, UI.TextColor); y += 40;
            float hp = p.Health.Current;
            if (UI.Slider(new Rect(body.x, y, 640, 56), "Health", ref hp, 1, p.Health.Max, "0")) p.Health.Current = hp; y += 64;
            UI.Slider(new Rect(body.x, y, 640, 56), "Move speed", ref Rules.PlayerSpeedMultiplier, 0.25f, 5f, "0.00", null, "×"); y += 64;
            UI.Slider(new Rect(body.x, y, 640, 56), "Jump", ref Rules.JumpMultiplier, 0.25f, 5f, "0.00", null, "×"); y += 64;
            UI.Toggle(new Rect(body.x, y, 640, 56), "Invincible", ref Rules.Invincible); y += 64;
            UI.Toggle(new Rect(body.x, y, 640, 56), "No fall damage", ref Rules.NoFallDamage); y += 64;
            bool nc = p.Noclip;
            if (UI.Toggle(new Rect(body.x, y, 640, 56), "Noclip", ref nc)) p.SetNoclip(nc); y += 74;
            var bts = new List<(string, string)> { ("Heal", "heal"), ("Respawn", "respawn"), ("Kill me", "kill_me"), ("TP to aim", "tp_aim"), ("TP to spawn", "tp_spawn"), ("Launch me", "launch_me 25"), ("Give all", "give_all"), ("Strip weapons", "strip"), ("Refill", "refill"), ("Print position", "pos") };
            DrawButtons(new Rect(body.x, y, body.width, body.yMax - y), "devplayer", bts.ToArray());
        }

        void DrawSounds(Rect body)
        {
            soundSearch = UI.TextField(new Rect(body.x, body.y, 420, 44), soundSearch, "dev_sound", false, "Search sounds...");
            var names = SoundBank.Names.Where(n => soundSearch.Length == 0 || n.Contains(soundSearch.ToLowerInvariant())).OrderBy(n => n).Select(n => (n, "play " + n)).ToArray();
            UI.Label(new Rect(body.x + 440, body.y, 400, 44), names.Length + " sounds (all synthesised at startup)", 15, UI.Muted);
            DrawButtons(new Rect(body.x, body.y + 56, body.width, body.height - 56), "devsounds", names);
        }

        void DrawPerf(Rect body)
        {
            var graph = new Rect(body.x, body.y, body.width, 220);
            UI.Rounded(graph, new Color(0, 0, 0, 0.5f), false);
            float max = 240f;
            foreach (var target in new[] { 30f, 60f, 144f })
            {
                float gy = graph.yMax - target / max * graph.height;
                UI.Fill(new Rect(graph.x, gy, graph.width, 1), new Color(1, 1, 1, 0.15f));
                UI.Label(new Rect(graph.x + 6, gy - 18, 80, 18), target + " fps", 12, UI.Muted);
            }
            float bw = graph.width / fpsHistory.Length;
            float min = float.MaxValue, avg = 0;
            for (int i = 0; i < fpsHistory.Length; i++)
            {
                float f = fpsHistory[(fpsHead + i) % fpsHistory.Length];
                if (f <= 0) continue;
                min = Mathf.Min(min, f); avg += f;
                float h = Mathf.Clamp(f, 0, max) / max * graph.height;
                UI.Fill(new Rect(graph.x + i * bw, graph.yMax - h, Mathf.Max(1, bw - 0.5f), h), f < 30 ? UI.Bad : f < 60 ? UI.Warn : UI.Good);
            }
            avg /= fpsHistory.Length;
            int alive = Goredoll.All.Count(g => g != null && !g.IsDead);
            string s = $"FPS now {GameManager.I.Fps:0} · avg {avg:0} · min {min:0}  ({1000f / Mathf.Max(1, GameManager.I.Fps):0.0} ms)\n" +
                       $"Goredolls {Goredoll.All.Count} ({alive} alive) · Nextbots {Nextbot.All.Count} · Decals {Decals.ActiveCount} · Debris {Debris.Count} · Audio voices {AudioManager.ActiveVoices}\n" +
                       $"Time scale {Time.timeScale:0.00} · Fixed step {Time.fixedDeltaTime * 1000f:0.0} ms · Solver {Physics.defaultSolverIterations}/{Physics.defaultSolverVelocityIterations} · Gravity {Physics.gravity.y:0.00}\n" +
                       $"Managed heap {System.GC.GetTotalMemory(false) / 1048576f:0.0} MB · Quality {QualitySettings.names[QualitySettings.GetQualityLevel()]} · Screen {Screen.width}×{Screen.height}\n" +
                       $"Nav grid {(NavGrid.Current != null ? NavGrid.Current.W + "×" + NavGrid.Current.H + ", " + NavGrid.Current.WalkableCount + " walkable" : "none")} · Icons {IconFactory.GlyphCount} glyphs + {IconFactory.RenderedCount} rendered";
            UI.Label(new Rect(body.x, graph.yMax + 16, body.width, 200), s, 16, UI.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
        }

        void DrawInfo(Rect body)
        {
            string s = $"{GameInfo.Title}  ·  Version {GameInfo.Version}  ·  Access {GameInfo.Access}\n{GameInfo.Studio}\n\n" +
                       $"Unity {Application.unityVersion} · {Application.platform} · {SystemInfo.graphicsDeviceType}\n" +
                       $"OS: {SystemInfo.operatingSystem}\nCPU: {SystemInfo.processorType} ({SystemInfo.processorCount} threads)\nGPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB)\nRAM: {SystemInfo.systemMemorySize} MB\n" +
                       $"Data path: {Application.persistentDataPath}\n\n" +
                       $"Logged in as: {user.Trim()}\nSession time: {(Time.realtimeSinceStartup - GameManager.I.SessionStart) / 60f:0.0} min\n" +
                       $"Achievements: {Stats.UnlockedCount()}/{Stats.Achievements.Count} · Easter eggs found: {EasterEggs.All.Count(x => Stats.HasEasterEgg(x.Id))}/{EasterEggs.All.Count}\n\n" +
                       "Owner-Developer: Quibbo/DrPhantom · Developer: AlexKiller";
            UI.Label(body, s, 17, UI.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            if (UI.Button(new Rect(body.x, body.yMax - 60, 220, 56), "LOG OUT", ButtonKind.Danger, IconFactory.Get("lock"), 18)) { LoggedIn = false; Debug.Log("[DevPanel] Logged out."); }
        }
    }
}
