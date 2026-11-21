using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Developer debug overlay, styled like an engine's internal debug HUD. Only for developers logged into the dev
    /// panel: F7 or the "debug" console command toggles it, "debug &lt;layer&gt;" toggles a layer.
    /// Layers: perf (frame graph, memory, GC, object counts, build info), bones (every Goredoll's skeleton), colliders
    /// (hit volumes coloured by health), physics (velocity vectors, centres of mass, sleep state), ai (action, thought,
    /// fear/anger), vitals (heart, pressure, oxygen, blood), xray (organs through skin), probe (what the crosshair is
    /// on), noise (sound events the AI hears), player, net and log (live engine log).
    /// Everything is drawn with IMGUI on top of the frame, nothing touches the game state.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        public static bool Enabled;
        public static readonly Dictionary<string, bool> Layers = new Dictionary<string, bool>
        {
            { "perf", true }, { "bones", true }, { "colliders", false }, { "physics", false }, { "ai", true }, { "vitals", true },
            { "probe", true }, { "noise", true }, { "player", true }, { "net", true }, { "log", true },
        };
        public static bool On(string layer) => Enabled && Layers.TryGetValue(layer, out var v) && v;

        static DebugOverlay inst;
        readonly float[] frameMs = new float[180];
        int frameHead; long frame;
        float nextCount; int bodies, awake, lights, objects, gc0, gc1;
        struct LogLine { public string Text; public LogType Type; public float Time; }
        static readonly List<LogLine> log = new List<LogLine>();
        struct Noise { public Vector3 Pos; public float Radius, Threat, Time; }
        static readonly List<Noise> noises = new List<Noise>();

        static readonly Color Green = new Color(0.35f, 1f, 0.45f), Amber = new Color(1f, 0.78f, 0.2f), Red = new Color(1f, 0.3f, 0.25f),
            Cyan = new Color(0.35f, 0.9f, 1f), Panel = new Color(0f, 0.02f, 0.01f, 0.72f), Dim = new Color(0.6f, 0.75f, 0.65f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (inst != null) return;
            var go = new GameObject("Debug Overlay"); DontDestroyOnLoad(go);
            inst = go.AddComponent<DebugOverlay>();
            Application.logMessageReceived += (msg, stack, type) =>
            {
                lock (log) { log.Add(new LogLine { Text = msg.Length > 140 ? msg.Substring(0, 140) + "..." : msg, Type = type, Time = Time.realtimeSinceStartup }); if (log.Count > 60) log.RemoveAt(0); }
            };
            GameEvents.Noise += e => { if (!Enabled) return; noises.Add(new Noise { Pos = e.Position, Radius = e.Radius, Threat = e.Threat, Time = Time.time }); if (noises.Count > 24) noises.RemoveAt(0); };
            UIRoot.OverlayLayer += () => { if (Enabled && inst != null) inst.Draw(); };
        }

        public static void Toggle()
        {
            if (!DevPanel.LoggedIn) { GameEvents.ShowToast("Debug overlay: log in to the developer panel first (F5)."); return; }
            Enabled = !Enabled;
            GameEvents.ShowToast("Debug overlay " + (Enabled ? "ON  (F7, or 'debug <layer>' in the console)" : "OFF"));
        }

        void Update()
        {
            frameMs[frameHead] = Time.unscaledDeltaTime * 1000f; frameHead = (frameHead + 1) % frameMs.Length; frame++;
            if (Input.GetKeyDown(KeyCode.F7)) Toggle();
            if (!Enabled) return;
            if (Time.unscaledTime >= nextCount)
            {
                nextCount = Time.unscaledTime + 1f;
                bodies = 0; awake = 0;
                foreach (var rb in FindObjectsOfType<Rigidbody>()) { bodies++; if (!rb.isKinematic && !rb.IsSleeping()) awake++; }
                lights = FindObjectsOfType<Light>().Length;
                objects = FindObjectsOfType<Transform>().Length;
                gc0 = GC.CollectionCount(0); gc1 = GC.CollectionCount(1);
            }
        }

        // ------------------------------------------------------------------ drawing helpers

        static Camera Cam => PlayerController.Instance != null && PlayerController.Instance.Cam != null ? PlayerController.Instance.Cam : Camera.main;

        static bool Project(Vector3 world, out Vector2 g)
        {
            g = default;
            var c = Cam; if (c == null) return false;
            var sp = c.WorldToScreenPoint(world);
            if (sp.z < 0.05f) return false;
            g = new Vector2(sp.x / UI.Scale, (Screen.height - sp.y) / UI.Scale);
            return true;
        }

        static void Line(Vector2 a, Vector2 b, Color c, float w = 2f)
        {
            var d = b - a; float len = d.magnitude;
            if (len < 0.5f || len > 4000f) return;
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a * UI.Scale);
            UI.Fill(new Rect(a.x, a.y - w * 0.5f, len, w), c);
            GUI.matrix = m;
        }

        static void Line3(Vector3 a, Vector3 b, Color c, float w = 2f) { if (Project(a, out var pa) && Project(b, out var pb)) Line(pa, pb, c, w); }
        static void Dot(Vector3 p, Color c, float s = 6f) { if (Project(p, out var g)) UI.Fill(new Rect(g.x - s / 2, g.y - s / 2, s, s), c); }

        static void Box3(Bounds b, Color c)
        {
            var mn = b.min; var mx = b.max;
            var v = new[] { new Vector3(mn.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mx.z), new Vector3(mn.x, mn.y, mx.z), new Vector3(mn.x, mx.y, mn.z), new Vector3(mx.x, mx.y, mn.z), new Vector3(mx.x, mx.y, mx.z), new Vector3(mn.x, mx.y, mx.z) };
            for (int i = 0; i < 4; i++) { Line3(v[i], v[(i + 1) % 4], c, 1f); Line3(v[i + 4], v[(i + 1) % 4 + 4], c, 1f); Line3(v[i], v[i + 4], c, 1f); }
        }

        static void Circle3(Vector3 center, float r, Color c)
        {
            Vector3 prev = center + new Vector3(r, 0, 0);
            for (int i = 1; i <= 24; i++) { float a = i / 24f * Mathf.PI * 2f; var p = center + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); Line3(prev, p, c, 1.5f); prev = p; }
        }

        static float PanelBox(float x, float y, float w, string title, List<(string k, string v, Color c)> rows)
        {
            float h = 30 + rows.Count * 19 + 8;
            UI.Fill(new Rect(x, y, w, h), Panel);
            UI.Fill(new Rect(x, y, w, 2), Green);
            UI.Label(new Rect(x + 8, y + 4, w - 16, 22), title, 14, Green, TextAnchor.MiddleLeft, FontStyle.Bold);
            for (int i = 0; i < rows.Count; i++)
            {
                float ry = y + 28 + i * 19;
                UI.Label(new Rect(x + 10, ry, w * 0.48f, 19), rows[i].k, 13, Dim, TextAnchor.MiddleLeft);
                UI.Label(new Rect(x + w * 0.45f, ry, w * 0.55f - 10, 19), rows[i].v, 13, rows[i].c, TextAnchor.MiddleRight, FontStyle.Bold);
            }
            return y + h + 8;
        }

        // ------------------------------------------------------------------ the overlay

        void Draw()
        {
            float W = UI.VW, H = UI.VH;
            // Header strip.
            UI.Fill(new Rect(0, 0, W, 24), new Color(0, 0, 0, 0.8f));
            float fps = 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            UI.Label(new Rect(10, 1, W - 20, 22), $"SOLARECLIPSE ENGINE  //  DEBUG BUILD  //  {GameInfo.Title} v{GameInfo.Version}  //  Unity {Application.unityVersion}  //  {SystemInfo.graphicsDeviceType}  //  frame {frame}  //  t {Time.time:0.0}s  //  {fps:0} fps",
                13, Green, TextAnchor.MiddleLeft, FontStyle.Bold);
            string layers = ""; foreach (var kv in Layers) layers += (kv.Value ? kv.Key.ToUpperInvariant() : kv.Key) + "  ";
            UI.Label(new Rect(10, 1, W - 20, 22), "F7  " + layers + (Rules.Xray ? " XRAY" : ""), 12, Dim, TextAnchor.MiddleRight);

            var cam = Cam;
            if (cam != null)
            {
                if (On("noise")) DrawNoise();
                foreach (var d in Goredoll.All)
                {
                    if (d == null || !d.Parts.ContainsKey(PartType.Pelvis)) continue;
                    if ((d.Position - cam.transform.position).sqrMagnitude > 60f * 60f) continue;
                    if (On("colliders")) DrawColliders(d);
                    if (On("bones")) DrawBones(d);
                    if (On("physics")) DrawPhysics(d);
                    if (On("ai") || On("vitals")) DrawLabel(d);
                }
                if (On("physics")) DrawProps();
            }

            float y = 32;
            if (On("perf")) y = DrawPerf(10, y);
            if (On("player")) y = DrawPlayer(10, y);
            float ry = 32;
            if (On("probe")) ry = DrawProbe(W - 370, ry);
            if (On("net")) ry = DrawNet(W - 370, ry);
            if (On("log")) DrawLog(10, H - 220, Mathf.Min(900, W - 20));
            if (On("probe")) { UI.Fill(new Rect(W / 2 - 9, H / 2, 18, 1), Cyan); UI.Fill(new Rect(W / 2, H / 2 - 9, 1, 18), Cyan); }
        }

        float DrawPerf(float x, float y)
        {
            float w = 360;
            // Frame-time graph (16.7 ms / 33.3 ms guides).
            var g = new Rect(x, y, w, 90);
            UI.Fill(g, Panel); UI.Fill(new Rect(g.x, g.y, w, 2), Green);
            float maxMs = 50f; float min = 999, max = 0, sum = 0;
            foreach (var ms in frameMs) { if (ms <= 0) continue; min = Mathf.Min(min, ms); max = Mathf.Max(max, ms); sum += ms; }
            float avg = sum / frameMs.Length;
            foreach (var guide in new[] { 16.7f, 33.3f }) { float gy = g.yMax - guide / maxMs * (g.height - 22); UI.Fill(new Rect(g.x, gy, w, 1), new Color(1, 1, 1, 0.15f)); UI.Label(new Rect(g.x + 4, gy - 14, 80, 14), guide.ToString("0.0") + "ms", 10, Dim); }
            float bw = w / frameMs.Length;
            for (int i = 0; i < frameMs.Length; i++)
            {
                float ms = frameMs[(frameHead + i) % frameMs.Length];
                float bh = Mathf.Clamp(ms / maxMs, 0f, 1f) * (g.height - 22);
                UI.Fill(new Rect(g.x + i * bw, g.yMax - bh, Mathf.Max(1f, bw), bh), ms > 33.3f ? Red : ms > 16.7f ? Amber : Green);
            }
            UI.Label(new Rect(g.x + 8, g.y + 3, w - 16, 18), $"FRAME  avg {avg:0.0}ms  min {min:0.0}  max {max:0.0}  ({1000f / Mathf.Max(0.1f, avg):0} fps)", 13, Green, TextAnchor.MiddleLeft, FontStyle.Bold);
            y = g.yMax + 8;
            int alive = 0, dead = 0; foreach (var d in Goredoll.All) if (d != null) { if (d.IsDead) dead++; else alive++; }
            var rows = new List<(string, string, Color)>
            {
                ("GC heap", (GC.GetTotalMemory(false) / 1048576f).ToString("0.0") + " MB", Green),
                ("Unity alloc / reserved", (UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f).ToString("0") + " / " + (UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1048576f).ToString("0") + " MB", Green),
                ("GC collections (gen0/1)", gc0 + " / " + gc1, Green),
                ("Time scale / fixed dt", Time.timeScale.ToString("0.00") + " / " + (Time.fixedDeltaTime * 1000f).ToString("0.0") + "ms", Time.timeScale < 0.99f ? Amber : Green),
                ("Rigidbodies awake / total", awake + " / " + bodies, awake > 300 ? Amber : Green),
                ("Goredolls alive / dead", alive + " / " + dead, Green),
                ("Decals / debris", Decals.ActiveCount + " / " + Debris.Count, Green),
                ("Audio voices / lights", AudioManager.ActiveVoices + " / " + lights, Green),
                ("Scene objects", objects.ToString(), Green),
                ("Quality / shadows", QualitySettings.names.Length > 0 ? QualitySettings.names[QualitySettings.GetQualityLevel()] + " / " + QualitySettings.shadows : "-", Dim),
                ("Screen / render", Screen.width + "x" + Screen.height + "  cap " + (Application.targetFrameRate > 0 ? Application.targetFrameRate.ToString() : "vsync"), Dim),
            };
            return PanelBox(x, y, w, "ENGINE", rows);
        }

        float DrawPlayer(float x, float y)
        {
            var p = PlayerController.Instance;
            if (p == null) return y;
            var w = p.Inventory != null ? p.Inventory.Current : null;
            var rows = new List<(string, string, Color)>
            {
                ("Position", p.transform.position.ToString("F1"), Green),
                ("Speed / grounded", p.Speed.ToString("0.0") + " m/s / " + (p.Grounded ? "yes" : "no"), Green),
                ("Health / bleed", (p.Health != null ? p.Health.Current.ToString("0") : "-") + " / " + (p.Health != null ? p.Health.Bleed.ToString("0.0") + " hp/s" : "-"), p.Health != null && p.Health.Bleed > 0 ? Red : Green),
                ("Stamina", (PlayerFeel.Stamina * 100f).ToString("0") + "%", PlayerFeel.CanSprint ? Green : Amber),
                ("Weapon", w != null ? w.Def.Id + "  " + w.ModeText : "-", Green),
                ("Ammo", w != null ? w.AmmoText : "-", Green),
                ("Gravity", Physics.gravity.y.ToString("0.00"), Mathf.Abs(Physics.gravity.y + 9.81f) > 0.01f ? Amber : Green),
                ("Map / seed", (GameManager.I != null && GameManager.I.CurrentMap != null ? GameManager.I.CurrentMap.Id : "-") + " / " + (GameManager.I != null ? GameManager.I.CurrentSeed.ToString() : "-"), Dim),
            };
            return PanelBox(x, y, 360, "PLAYER", rows);
        }

        float DrawProbe(float x, float y)
        {
            var c = Cam; if (c == null) return y;
            var rows = new List<(string, string, Color)>();
            if (Physics.Raycast(c.transform.position, c.transform.forward, out var hit, 200f, ~0, QueryTriggerInteraction.Collide))
            {
                var go = hit.collider.gameObject;
                string path = go.name; var t = go.transform.parent; int depth = 0;
                while (t != null && depth++ < 2) { path = t.name + "/" + path; t = t.parent; }
                rows.Add(("Object", path.Length > 34 ? "..." + path.Substring(path.Length - 32) : path, Cyan));
                rows.Add(("Layer / surface", LayerMask.LayerToName(go.layer) + " / " + Surface.Of(hit.collider), Green));
                rows.Add(("Collider", hit.collider.GetType().Name + (hit.collider.isTrigger ? " (trigger)" : ""), Green));
                rows.Add(("Distance / normal", hit.distance.ToString("0.00") + "m / " + hit.normal.ToString("F1"), Green));
                if (hit.rigidbody != null) rows.Add(("Rigidbody", hit.rigidbody.mass.ToString("0.0") + "kg  v " + RB.Vel(hit.rigidbody).magnitude.ToString("0.0") + (hit.rigidbody.IsSleeping() ? " (asleep)" : "") + (hit.rigidbody.isKinematic ? " kin" : ""), Amber));
                var bp = hit.collider.GetComponent<BodyPart>();
                if (bp != null)
                {
                    rows.Add(("Body part", bp.Spec.Medical + (bp.Severed ? " (severed)" : "") + (bp.Gibbed ? " (gibbed)" : ""), Amber));
                    rows.Add(("Health / function", bp.Health.ToString("0") + "/" + bp.MaxHealth.ToString("0") + " / " + (bp.Function * 100f).ToString("0") + "%", bp.Health < 0 ? Red : Green));
                    rows.Add(("Wounds / bleeding", bp.Wounds.Count + " / " + bp.BleedRate.ToString("0.0") + " ml/s", bp.BleedRate > 5 ? Red : Green));
                    if (bp.Fractured || bp.TendonCut || bp.NerveDamaged) rows.Add(("Damage", (bp.Fractured ? "fracture " : "") + (bp.TendonCut ? "tendon " : "") + (bp.NerveDamaged ? "nerve" : ""), Red));
                }
                Dot(hit.point, Cyan, 8f);
                Line3(hit.point, hit.point + hit.normal * 0.4f, Cyan, 2f);
            }
            else rows.Add(("Probe", "nothing within 200 m", Dim));
            return PanelBox(x, y, 360, "PROBE (crosshair)", rows);
        }

        float DrawNet(float x, float y)
        {
            var rows = new List<(string, string, Color)>();
            rows.Add(("Role", !NetState.Active ? "offline (singleplayer)" : NetState.IsHost ? "HOST" : "CLIENT", NetState.Active ? Cyan : Dim));
            if (NetState.Active && NetManager.I != null)
            {
                rows.Add(("Server", NetManager.I.ServerName ?? "-", Green));
                foreach (var pl in NetManager.I.Players.Values) rows.Add(("  " + pl.Name, pl.IsHost ? "host" : pl.PingMs + " ms", pl.PingMs > 150 ? Amber : Green));
            }
            return PanelBox(x, y, 360, "NETWORK", rows);
        }

        void DrawLog(float x, float y, float w)
        {
            List<LogLine> lines; lock (log) lines = new List<LogLine>(log);
            int n = Mathf.Min(10, lines.Count);
            UI.Fill(new Rect(x, y, w, 24 + n * 18), Panel);
            UI.Fill(new Rect(x, y, w, 2), Green);
            UI.Label(new Rect(x + 8, y + 2, w, 20), "LOG", 13, Green, TextAnchor.MiddleLeft, FontStyle.Bold);
            for (int i = 0; i < n; i++)
            {
                var l = lines[lines.Count - n + i];
                var col = l.Type == LogType.Error || l.Type == LogType.Exception || l.Type == LogType.Assert ? Red : l.Type == LogType.Warning ? Amber : Dim;
                UI.Label(new Rect(x + 8, y + 22 + i * 18, w - 16, 18), $"[{l.Time,7:0.00}] {l.Text.Replace('\n', ' ')}", 12, col, TextAnchor.MiddleLeft);
            }
        }

        // ------------------------------------------------------------------ world layers

        static void DrawBones(Goredoll d)
        {
            foreach (var p in d.Parts.Values)
            {
                if (p == null || p.Gibbed) continue;
                var c = p.Severed ? Red : p.Fractured ? Amber : p.NerveDamaged ? new Color(0.8f, 0.4f, 1f) : Green;
                var a = p.transform.position;
                Vector3 tip = p.Col != null ? p.Col.bounds.center + (p.Col.bounds.center - a) : a;
                if (p.Parent != null && !p.Severed) Line3(p.Parent.transform.position, a, c, 2.5f);
                else Line3(a, tip, c, 2f);
                Dot(a, c, p.Joint != null ? 6f : 4f);
            }
            if (d.Parts.TryGetValue(PartType.Head, out var hd) && hd != null && !hd.Gibbed)
            {
                var t = hd.transform; var o = t.TransformPoint(new Vector3(0, HumanModel.HeadCenterY, 0));
                Line3(o, o + t.forward * 0.18f, new Color(0.3f, 0.5f, 1f), 2f); Line3(o, o + t.up * 0.18f, Green, 2f); Line3(o, o + t.right * 0.18f, Red, 2f);
            }
        }

        static void DrawColliders(Goredoll d)
        {
            foreach (var p in d.Parts.Values)
            {
                if (p == null || p.Gibbed || p.Col == null) continue;
                float hpk = Mathf.Clamp01(p.Health / Mathf.Max(1f, p.MaxHealth));
                Box3(p.Col.bounds, Color.Lerp(Red, Cyan, hpk).WithAlpha(0.8f));
            }
        }

        static void DrawPhysics(Goredoll d)
        {
            foreach (var p in d.Parts.Values)
            {
                if (p == null || p.Body == null || p.Gibbed) continue;
                var com = p.Body.worldCenterOfMass;
                Dot(com, p.Body.IsSleeping() ? Dim : Amber, 5f);
                var v = RB.Vel(p.Body);
                if (v.sqrMagnitude > 0.04f) Line3(com, com + v * 0.12f, Amber, 1.5f);
            }
        }

        static void DrawProps()
        {
            var c = Cam; if (c == null) return;
            int n = 0;
            foreach (var hit in Physics.OverlapSphere(c.transform.position, 25f, global::SolarEclipse.Layers.Mask(global::SolarEclipse.Layers.Props, global::SolarEclipse.Layers.Debris), QueryTriggerInteraction.Ignore))
            {
                var rb = hit.attachedRigidbody; if (rb == null || n++ > 60) continue;
                Box3(hit.bounds, (rb.IsSleeping() ? Dim : Amber).WithAlpha(0.6f));
                var v = RB.Vel(rb); if (v.sqrMagnitude > 0.04f) Line3(rb.worldCenterOfMass, rb.worldCenterOfMass + v * 0.15f, Amber, 1.5f);
            }
        }

        static void DrawLabel(Goredoll d)
        {
            Vector3 top = d.Parts.TryGetValue(PartType.Head, out var hd) && hd != null && !hd.Gibbed ? hd.transform.position + Vector3.up * 0.4f : d.Position + Vector3.up * 1.2f;
            if (!Project(top, out var g)) return;
            var lines = new List<(string, Color)>();
            lines.Add((d.DisplayName + "  #" + d.GetInstanceID().ToString("X").Substring(0, 4) + "  [" + d.Preset.Split('~')[0] + "]", Cyan));
            var b = d.Brain;
            if (On("ai"))
            {
                if (b != null) { lines.Add(("AI " + b.Action + (b.Hostile ? " HOSTILE" : "") + "  F" + b.Fear.ToString("0.00") + " A" + b.Anger.ToString("0.00"), b.Hostile || b.Action == AiAction.Fight ? Red : b.Running ? Amber : Green)); if (!string.IsNullOrEmpty(b.Thought)) lines.Add(("\"" + b.Thought + "\"", Dim)); }
                else if (d.Zombie != null) lines.Add(("AI zombie", Red));
                if (d.Ragdoll != null) lines.Add(("Body " + d.Ragdoll.State + " / " + d.Ragdoll.Pose + "  bal " + d.Ragdoll.Balance.ToString("0.00"), Dim));
            }
            if (On("vitals"))
            {
                var v = d.Vitals;
                lines.Add(($"HR {v.HeartRate:0}  BP {v.Systolic:0}/{v.Diastolic:0}  SpO2 {v.SpO2:0}%  RR {v.RespRate:0}", v.State == LifeState.Dead ? Red : Green));
                lines.Add(($"Blood -{v.BloodLost:0}ml ({v.BloodLossFraction * 100f:0}%)  bleed {d.TotalBleed:0.0}+{v.InternalBleed:0.0}ml/s  {v.Temperature:0.0}C", v.BloodLossFraction > 0.25f ? Red : Amber));
                lines.Add(($"GCS {v.GCS}  pain {v.Pain:0}  shock {v.Shock:0.00}  {v.State}" + (v.Paralysis != Paralysis.None ? "  " + v.Paralysis : ""), v.State == LifeState.Alive ? Dim : Red));
            }
            float w = 330, h = lines.Count * 16 + 6;
            var r = new Rect(g.x - w / 2, g.y - h, w, h);
            UI.Fill(r, new Color(0, 0, 0, 0.6f));
            for (int i = 0; i < lines.Count; i++) UI.Label(new Rect(r.x + 6, r.y + 3 + i * 16, w - 12, 16), lines[i].Item1, 11, lines[i].Item2, TextAnchor.MiddleLeft);
        }

        static void DrawNoise()
        {
            for (int i = noises.Count - 1; i >= 0; i--)
            {
                var n = noises[i]; float age = Time.time - n.Time;
                if (age > 2f) { noises.RemoveAt(i); continue; }
                var c = (n.Threat >= 0.9f ? Red : n.Threat >= 0.5f ? Amber : Cyan).WithAlpha(1f - age / 2f);
                Circle3(n.Pos + Vector3.up * 0.05f, Mathf.Min(n.Radius, 40f) * Mathf.Clamp01(age / 0.6f + 0.1f), c);
            }
        }
    }
}
