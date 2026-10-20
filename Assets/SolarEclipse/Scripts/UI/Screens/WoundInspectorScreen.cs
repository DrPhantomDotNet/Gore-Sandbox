using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// E on a Goredoll: a live patient chart. Body map coloured by injury, a vitals monitor with an ECG trace
    /// that follows the real heart rate (or flatlines), every wound with its medical description, and first-aid
    /// actions (pressure, tourniquet, adrenaline) that actually change the bleeding/physiology simulation.
    /// Time keeps running while it is open, so you can watch the patient deteriorate.
    /// </summary>
    public sealed class WoundInspectorScreen : UIScreen
    {
        readonly Goredoll doll;
        PartType? selected;
        float ecgPhase;
        readonly float[] ecg = new float[160];
        int ecgHead;

        public override bool PausesTime => false;
        public override bool IsModal => true;

        public WoundInspectorScreen(Goredoll doll, BodyPart part)
        {
            this.doll = doll;
            selected = part != null ? part.Type : (PartType?)null;
        }

        public override void OnOpen() { AudioManager.Ui("ui_open"); Stats.Add("inspections"); }
        public override bool OnBack() { AudioManager.Ui("ui_close"); UIRoot.Pop(); return true; }

        public override void Tick()
        {
            if (doll == null) { UIRoot.Remove(this); return; }
            // ECG: P-QRS-T shape driven by the simulated heart rate.
            var v = doll.Vitals;
            float hr = v.State == LifeState.Dead ? 0f : v.HeartRate;
            for (int k = 0; k < 2; k++)
            {
                ecgPhase += Time.unscaledDeltaTime / 2f * hr / 60f;
                float ph = Mathf.Repeat(ecgPhase, 1f);
                float y = 0f;
                if (hr > 0f)
                {
                    y += Gauss(ph, 0.12f, 0.025f) * 0.15f;
                    y -= Gauss(ph, 0.2f, 0.008f) * 0.15f;
                    y += Gauss(ph, 0.22f, 0.01f) * 1f;
                    y -= Gauss(ph, 0.245f, 0.01f) * 0.3f;
                    y += Gauss(ph, 0.42f, 0.04f) * 0.25f;
                    if (v.Shock > 0.6f) y += (Random.value - 0.5f) * 0.1f;
                }
                ecg[ecgHead] = y;
                ecgHead = (ecgHead + 1) % ecg.Length;
            }
        }

        static float Gauss(float x, float mu, float s) => Mathf.Exp(-(x - mu) * (x - mu) / (2 * s * s));

        public override void Draw()
        {
            if (doll == null) return;
            float a = Intro(0, 0.2f);
            UI.Dim(0.5f * a);
            var r = UI.Centered(Mathf.Min(1500, UI.VW - 60), Mathf.Min(940, UI.VH - 60));
            UI.Panel(r, "Patient: " + doll.DisplayName, a);
            var v = doll.Vitals;
            UI.Label(new Rect(r.x + 420, r.y + 22, r.width - 450, 36), doll.StatusLine, 20, doll.IsDead ? UI.Bad : v.State == LifeState.Unconscious ? UI.Warn : UI.Good, TextAnchor.MiddleRight);

            DrawBodyMap(new Rect(r.x + 24, r.y + 80, 340, r.height - 170));
            DrawVitals(new Rect(r.x + 384, r.y + 80, r.width - 408, 250));
            DrawWounds(new Rect(r.x + 384, r.y + 344, r.width - 408, r.height - 434));
            DrawActions(new Rect(r.x + 24, r.yMax - 78, r.width - 48, 60));
        }

        // ------------------------------------------------------------------ body map

        static readonly Dictionary<PartType, Rect> Map = new Dictionary<PartType, Rect>
        {
            { PartType.Head, new Rect(140, 0, 60, 66) }, { PartType.Neck, new Rect(155, 66, 30, 20) },
            { PartType.Chest, new Rect(115, 86, 110, 90) }, { PartType.Abdomen, new Rect(120, 176, 100, 60) }, { PartType.Pelvis, new Rect(118, 236, 104, 46) },
            // Patient faces us: their right is on our left.
            { PartType.UpperArmR, new Rect(78, 90, 36, 90) }, { PartType.ForearmR, new Rect(66, 180, 34, 84) }, { PartType.HandR, new Rect(58, 264, 34, 40) },
            { PartType.UpperArmL, new Rect(226, 90, 36, 90) }, { PartType.ForearmL, new Rect(240, 180, 34, 84) }, { PartType.HandL, new Rect(248, 264, 34, 40) },
            { PartType.ThighR, new Rect(120, 282, 48, 110) }, { PartType.ShinR, new Rect(122, 392, 42, 100) }, { PartType.FootR, new Rect(112, 492, 52, 24) },
            { PartType.ThighL, new Rect(172, 282, 48, 110) }, { PartType.ShinL, new Rect(176, 392, 42, 100) }, { PartType.FootL, new Rect(176, 492, 52, 24) },
        };

        void DrawBodyMap(Rect area)
        {
            UI.Rounded(area, new Color(0, 0, 0, 0.3f), false);
            float scale = Mathf.Min(area.width / 340f, (area.height - 60) / 520f);
            float ox = area.x + (area.width - 340f * scale) / 2f, oy = area.y + 20;
            foreach (var kv in Map)
            {
                doll.Parts.TryGetValue(kv.Key, out var part);
                var rr = new Rect(ox + kv.Value.x * scale, oy + kv.Value.y * scale, kv.Value.width * scale, kv.Value.height * scale);
                Color c;
                if (part == null || part.Gibbed) c = new Color(0.2f, 0.2f, 0.2f, 0.4f);
                else if (part.Severed) c = new Color(0.35f, 0.05f, 0.05f, 0.6f);
                else
                {
                    float h = Mathf.Clamp01(part.Health / Mathf.Max(1f, part.MaxHealth));
                    c = Color.Lerp(new Color(0.85f, 0.1f, 0.1f), new Color(0.35f, 0.75f, 0.4f), h);
                    if (part.Wounds.Count == 0) c = new Color(0.45f, 0.55f, 0.5f);
                }
                bool sel = selected == kv.Key;
                UI.Rounded(rr, c.WithAlpha(sel ? 1f : 0.8f), true);
                if (sel) UI.Outline(rr, Color.white, 2f);
                if (part != null && part.BleedRate > 0.05f && Mathf.Repeat(Time.unscaledTime * 2f, 1f) < 0.5f) UI.Outline(rr, UI.Bad, 2f);
                if (part != null && part.Wounds.Count > 0) UI.Label(new Rect(rr.x, rr.y, rr.width, rr.height), part.Wounds.Count.ToString(), 14, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                if (UI.Hit(rr)) { selected = sel ? (PartType?)null : kv.Key; AudioManager.Ui("ui_click"); }
            }
            UI.Label(new Rect(area.x, area.yMax - 40, area.width, 30), selected.HasValue ? Anatomy.Parts[selected.Value].Medical + " — click again for all" : "Click a body part to filter", 14, UI.Muted, TextAnchor.MiddleCenter);
        }

        // ------------------------------------------------------------------ vitals

        void DrawVitals(Rect area)
        {
            var v = doll.Vitals;
            UI.Rounded(area, new Color(0.02f, 0.04f, 0.03f, 0.9f), false);
            // ECG trace
            var trace = new Rect(area.x + 16, area.y + 16, area.width * 0.55f, area.height - 32);
            UI.Fill(new Rect(trace.x, trace.center.y, trace.width, 1), new Color(0.2f, 0.5f, 0.3f, 0.3f));
            float step = trace.width / ecg.Length;
            for (int i = 0; i < ecg.Length; i++)
            {
                int idx = (ecgHead + i) % ecg.Length;
                float y = trace.center.y - ecg[idx] * trace.height * 0.42f;
                float fade = i / (float)ecg.Length;
                UI.Fill(new Rect(trace.x + i * step, y - 1, Mathf.Max(1.5f, step), 3), new Color(0.3f, 1f, 0.45f, fade));
            }
            UI.Label(new Rect(trace.x + 4, trace.y, 200, 22), doll.IsDead ? "ASYSTOLE" : v.HeartRate > 100 ? "SINUS TACHYCARDIA" : v.HeartRate < 60 ? "SINUS BRADYCARDIA" : "NORMAL SINUS RHYTHM", 13, new Color(0.3f, 1f, 0.45f));

            float x = trace.xMax + 24, w = area.xMax - x - 16;
            void Vital(float yy, string label, string value, Color c)
            {
                UI.Label(new Rect(x, area.y + yy, w * 0.5f, 26), label, 14, UI.Muted);
                UI.Label(new Rect(x + w * 0.42f, area.y + yy - 4, w * 0.58f, 32), value, 24, c, TextAnchor.MiddleRight, FontStyle.Bold);
            }
            bool dead = doll.IsDead;
            Vital(14, "HEART RATE", dead ? "0" : Mathf.RoundToInt(v.HeartRate) + " bpm", new Color(0.3f, 1f, 0.45f));
            Vital(46, "BLOOD PRESSURE", dead ? "0/0" : Mathf.RoundToInt(v.Systolic) + "/" + Mathf.RoundToInt(v.Diastolic), new Color(1f, 0.35f, 0.35f));
            Vital(78, "RESP. RATE", dead ? "0" : Mathf.RoundToInt(v.RespRate) + " /min", Color.white);
            Vital(110, "SpO2", dead ? "--" : Mathf.RoundToInt(v.SpO2) + " %", new Color(0.4f, 0.8f, 1f));
            Vital(142, "BLOOD LOST", Mathf.RoundToInt(v.BloodLost) + " ml (" + Mathf.RoundToInt(v.BloodLossFraction * 100) + "%)", UI.Bad);
            Vital(174, "HAEMORRHAGE", v.HemorrhageClass, UI.Warn);
            Vital(206, "GCS / AVPU", v.GCS + " · " + v.AVPU, Color.white);
        }

        // ------------------------------------------------------------------ wounds

        void DrawWounds(Rect area)
        {
            var list = new List<Wound>();
            foreach (var w in doll.Wounds) if (!selected.HasValue || w.Part == selected.Value) list.Add(w);
            var v = doll.Vitals;
            var sb = new StringBuilder();
            sb.Append($"Pain {Mathf.RoundToInt(v.Pain)}/100 · Shock {Mathf.RoundToInt(v.Shock * 100)}% · Consciousness {Mathf.RoundToInt(v.Consciousness * 100)}%");
            if (v.Paralysis != Paralysis.None) sb.Append(" · PARALYSIS: " + (v.Paralysis == Paralysis.Total ? "complete (spinal cord)" : "lower body"));
            if (v.AirwayBlood > 0.05f) sb.Append($" · Airway blood {Mathf.RoundToInt(v.AirwayBlood * 100)}% (choking)");
            if (v.LungDamage > 0.05f) sb.Append(" · Lung injury");
            if (doll.IsDead) sb.Append(" · Cause of death: " + v.CauseOfDeath);
            UI.Label(new Rect(area.x, area.y, area.width, 44), sb.ToString(), 15, UI.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, true, true);

            var view = new Rect(area.x, area.y + 50, area.width, area.height - 50);
            if (list.Count == 0)
            {
                UI.Label(view, selected.HasValue ? "No wounds on the " + Anatomy.Parts[selected.Value].Medical.ToLowerInvariant() + "." : "No wounds. A perfectly healthy Goredoll... for now.", 20, UI.Muted, TextAnchor.UpperLeft);
                return;
            }
            float content = 0;
            var heights = new float[list.Count];
            for (int i = 0; i < list.Count; i++) { heights[i] = Mathf.Max(96, UI.TextHeight(list[i].Describe(), 15, view.width - 60) + 60); content += heights[i] + 10; }
            float s = UI.BeginScroll("wounds", view, content);
            float y = -s;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var w = list[i];
                var rr = new Rect(0, y, view.width - 16, heights[i]);
                y += heights[i] + 10;
                if (rr.yMax < 0 || rr.y > view.height) continue;
                UI.Rounded(rr, UI.PanelLight, false);
                UI.Fill(new Rect(rr.x, rr.y, 6, rr.height), w.SeverityColor);
                UI.Label(new Rect(rr.x + 20, rr.y + 8, rr.width - 300, 28), w.KindName + " — " + w.Location, 19, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                UI.Label(new Rect(rr.xMax - 290, rr.y + 8, 270, 28), w.Severity.ToUpperInvariant(), 16, w.SeverityColor, TextAnchor.MiddleRight, FontStyle.Bold);
                UI.Label(new Rect(rr.x + 20, rr.y + 36, rr.width - 40, 22), w.BleedingText + (string.IsNullOrEmpty(w.Weapon) ? "" : " · " + w.Weapon) + $" · {Time.time - w.Time:0}s ago", 14, w.Arterial ? UI.Bad : UI.Muted);
                UI.Label(new Rect(rr.x + 20, rr.y + 58, rr.width - 40, rr.height - 62), w.Describe(), 15, UI.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            }
            UI.EndScroll("wounds", view, content);
        }

        // ------------------------------------------------------------------ first aid

        void DrawActions(Rect area)
        {
            float x = area.x, bw = 250;
            bool alive = !doll.IsDead;
            bool limb = selected.HasValue && (Anatomy.Parts[selected.Value].IsArm || Anatomy.Parts[selected.Value].IsLeg);
            if (UI.Button(new Rect(x, area.y, bw, area.height), "APPLY PRESSURE", ButtonKind.Normal, IconFactory.Get("hand"), 18, alive && selected.HasValue, "Direct pressure on the selected part: venous bleeding mostly stops, arterial bleeding slows."))
            {
                foreach (var w in doll.Parts[selected.Value].Wounds) w.BleedRate *= w.Arterial ? 0.5f : 0.15f;
                if (doll.NetProxy) NetManager.I.RequestDollCommand(doll, 3, selected.Value);
                AudioManager.Play("cloth", doll.Position, 0.6f);
                GameEvents.ShowToast("Pressure applied to the " + Anatomy.Parts[selected.Value].Medical.ToLowerInvariant());
            }
            x += bw + 12;
            if (UI.Button(new Rect(x, area.y, bw, area.height), "TOURNIQUET", ButtonKind.Normal, IconFactory.Get("link"), 18, alive && limb, "Stops all bleeding from the selected limb (arms and legs only)."))
            {
                foreach (var p in doll.Parts.Values)
                    if (p != null && (p.Type == selected.Value || IsDistal(p.Type, selected.Value)))
                        foreach (var w in p.Wounds) { w.BleedRate = 0f; w.Arterial = false; }
                doll.Vitals.AddPain(15f);
                if (doll.NetProxy) NetManager.I.RequestDollCommand(doll, 4, selected.Value);
                AudioManager.Play("cloth", doll.Position, 0.8f);
                GameEvents.ShowToast("Tourniquet applied");
                Stats.Add("tourniquets");
                EasterEggs.Found("Surgeon", "Tourniquet applied. Somewhere, a medic nods approvingly.");
            }
            x += bw + 12;
            if (UI.Button(new Rect(x, area.y, bw, area.height), "ADRENALINE", ButtonKind.Normal, IconFactory.Get("bolt"), 18, alive, "Raises heart rate and wakes an unconscious (but alive) patient for a while."))
            {
                if (doll.NetProxy) NetManager.I.RequestDollCommand(doll, 5);
                doll.Vitals.Adrenaline = 1f;
                doll.Vitals.Consciousness = Mathf.Max(doll.Vitals.Consciousness, 0.7f);
                if (doll.Vitals.State == LifeState.Unconscious) doll.Vitals.State = LifeState.Alive;
                doll.Voice.Say("v_gasp", 1f, true);
            }
            x += bw + 12;
            if (UI.Button(new Rect(x, area.y, 200, area.height), "MERCY", ButtonKind.Danger, IconFactory.Get("skull"), 18, alive, "End it."))
            {
                doll.Kill("Euthanised");
                OnBack();
            }
            if (UI.Button(new Rect(area.xMax - 200, area.y, 200, area.height), "CLOSE", ButtonKind.Primary)) OnBack();
        }

        static bool IsDistal(PartType p, PartType of)
        {
            var spec = Anatomy.Parts[p];
            while (spec.Parent.HasValue)
            {
                if (spec.Parent.Value == of) return true;
                spec = Anatomy.Parts[spec.Parent.Value];
            }
            return false;
        }
    }

    /// <summary>Tab: session stats and a roster of every Goredoll on the map.</summary>
    public sealed class ScoreboardScreen : UIScreen
    {
        public override bool PausesTime => false;
        public override bool IsModal => true;
        public override bool ShowCursor => true;

        public override void Tick()
        {
            if (Age > 0.2f && Input.GetKeyUp(KeyCode.Tab)) UIRoot.Remove(this);
        }

        public override void Draw()
        {
            float a = Intro(0, 0.15f);
            UI.Dim(0.4f * a);
            var r = UI.Centered(Mathf.Min(1200, UI.VW - 80), Mathf.Min(800, UI.VH - 80));
            UI.Panel(r, "Session", a);
            float t = GameManager.I != null ? Time.realtimeSinceStartup - GameManager.I.SessionStart : 0f;
            string[,] stats =
            {
                { "Kills", Stats.Get(Stats.Kills).ToString("0") }, { "Headshots", Stats.Get(Stats.Headshots).ToString("0") },
                { "Limbs severed", Stats.Get(Stats.Limbs).ToString("0") }, { "Shots fired", Stats.Get(Stats.ShotsFired).ToString("0") },
                { "Hits", Stats.Get(Stats.Hits).ToString("0") }, { "Accuracy", Stats.Get(Stats.ShotsFired) > 0 ? (Stats.Get(Stats.Hits) / Stats.Get(Stats.ShotsFired) * 100f).ToString("0") + "%" : "-" },
                { "Things spawned", Stats.Get("spawned").ToString("0") }, { "Distance walked", Stats.Get(Stats.Distance).ToString("0") + " m" },
                { "Play time", Mathf.FloorToInt(t / 60) + "m " + Mathf.FloorToInt(t % 60) + "s" }, { "Achievements", Stats.UnlockedCount() + " / " + Stats.Achievements.Count },
            };
            for (int i = 0; i < stats.GetLength(0); i++)
            {
                var rr = new Rect(r.x + 30 + (i % 2) * 260, r.y + 90 + (i / 2) * 64, 240, 56);
                UI.Rounded(rr, UI.PanelLight, true);
                UI.Label(new Rect(rr.x + 12, rr.y + 2, rr.width - 24, 22), stats[i, 0], 13, UI.Muted);
                UI.Label(new Rect(rr.x + 12, rr.y + 20, rr.width - 24, 34), stats[i, 1], 24, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            }
            var view = new Rect(r.x + 570, r.y + 90, r.width - 600, r.height - 120);
            if (NetState.Active)
            {
                UI.Label(new Rect(view.x, view.y - 4, view.width, 30), "PLAYERS — " + NetManager.I.ServerName, 18, UI.Accent2, TextAnchor.MiddleLeft, FontStyle.Bold);
                PlayersPanel.Draw(new Rect(view.x, view.y + 30, view.width, view.height - 60));
                UI.Label(new Rect(r.x, r.yMax - 40, r.width, 30), "Release Tab to close", 14, UI.Muted, TextAnchor.MiddleCenter);
                return;
            }
            UI.Label(new Rect(view.x, view.y - 4, view.width, 30), "GOREDOLLS", 18, UI.Accent2, TextAnchor.MiddleLeft, FontStyle.Bold);
            view.y += 30; view.height -= 30;
            float content = Goredoll.All.Count * 48;
            float s = UI.BeginScroll("roster", view, content);
            for (int i = 0; i < Goredoll.All.Count; i++)
            {
                var g = Goredoll.All[i];
                if (g == null) continue;
                var rr = new Rect(0, i * 48 - s, view.width - 16, 42);
                UI.Rounded(rr, new Color(1, 1, 1, 0.05f), true);
                UI.Label(new Rect(rr.x + 12, rr.y, 220, 42), g.DisplayName, 18, Color.white);
                UI.Label(new Rect(rr.x + 230, rr.y, rr.width - 240, 42), g.StatusLine + " · " + g.Wounds.Count + " wounds · " + Mathf.RoundToInt(g.Vitals.BloodLost) + " ml lost", 15, g.IsDead ? UI.Bad : UI.Muted);
            }
            UI.EndScroll("roster", view, content);
            UI.Label(new Rect(r.x, r.yMax - 40, r.width, 30), "Release Tab to close", 14, UI.Muted, TextAnchor.MiddleCenter);
        }
    }
}
