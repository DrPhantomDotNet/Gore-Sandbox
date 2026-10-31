using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// In-game HUD (IMGUI, HudLayer): item bar at the top with rendered weapon icons, compass, crosshair that
    /// follows weapon spread, hit/kill markers, damage numbers, health + damage direction, ammo + fire mode,
    /// Goredoll name tags with status, subtitles and AI thoughts, look hints, rule chips, death screen,
    /// map title card, FPS and a detailed stats overlay.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        static Hud I;
        static string mapTitle, mapDesc;
        static float mapTitleTime = -99f;

        float hitTime = -9f, killTime = -9f, hitDamage;
        struct Number { public Vector3 World; public float Amount, Time; }
        readonly List<Number> numbers = new List<Number>();
        readonly Queue<string> iconQueue = new Queue<string>();
        float lastHealth = 100f;

        public static void Create(Transform parent)
        {
            var go = new GameObject("Hud");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<Hud>();
            UIRoot.HudLayer += I.Draw;
            foreach (var w in WeaponCatalog.All) I.iconQueue.Enqueue(w.Id);
        }

        public static void ShowMapTitle(string title, string desc)
        {
            mapTitle = title; mapDesc = desc; mapTitleTime = Time.unscaledTime;
        }

        void OnEnable()
        {
            GameEvents.HitMarker += OnHit;
            GameEvents.KillFeed += OnKill;
        }

        void OnDisable()
        {
            GameEvents.HitMarker -= OnHit;
            GameEvents.KillFeed -= OnKill;
        }

        void OnHit(Vector3 p, float dmg)
        {
            hitTime = Time.unscaledTime;
            hitDamage = dmg;
            if (Settings.Data.damageNumbers) { numbers.Add(new Number { World = p, Amount = dmg, Time = Time.unscaledTime }); if (numbers.Count > 30) numbers.RemoveAt(0); }
        }

        void OnKill(string text, Color c) => killTime = Time.unscaledTime;

        /// <summary>Weapon icons are photographed from the real viewmodel meshes, one per frame after boot.</summary>
        void LateUpdate()
        {
            if (iconQueue.Count > 0 && SoundBank.Ready)
            {
                var id = iconQueue.Dequeue();
                if (id != "fists")
                {
                    var model = WeaponModels.Build(id, 31);
                    IconFactory.Render("weapon_" + id, model.gameObject, new Vector3(-1f, -0.15f, 0.05f), 160, 1.05f);
                }
            }
        }

        public static Texture WeaponIcon(WeaponDef d)
        {
            if (IconFactory.Has("weapon_" + d.Id)) return IconFactory.Get("weapon_" + d.Id);
            return IconFactory.Get(d.Glyph);
        }

        static Vector2 ToGui(Vector3 screen) => new Vector2(screen.x / UI.Scale, (Screen.height - screen.y) / UI.Scale);

        void Draw()
        {
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing) return;
            var p = PlayerController.Instance;
            if (p == null) return;
            float W = UI.VW, H = UI.VH;
            var d = Settings.Data;
            bool repaint = Event.current.type == EventType.Repaint;

            DrawDamageVignette(p, W, H);
            if (gm.HudHidden) { DrawDeath(p, W, H); return; }

            p.Inventory?.Current?.DrawOverlay(W, H); // e.g. sniper scope
            DrawNameTags(p);
            if (NetState.Active) { DrawPlayerTags(p); DrawVoice(W); }
            if (Settings.Data.showChat || UIRoot.Contains<ChatScreen>()) DrawChat(H);
            if (repaint) DrawNumbers(p);
            DrawHotbar(p, W);
            DrawCompass(p, W);
            if (!VRManager.Active) DrawCrosshair(p, W, H); // VR: you aim down the real sights
            DrawHealth(p, H);
            DrawAmmo(p, W, H);
            DrawHints(p, W, H);
            DrawChips(p, H);
            DrawMapTitle(W, H);
            if (d.showFps) UI.Label(new Rect(14, 8, 300, 26), Mathf.RoundToInt(gm.Fps) + " FPS", 18, gm.Fps < 50 ? UI.Warn : UI.Good);
            if (d.detailedStats) DrawStats(gm, 14, 36);
            DrawDeath(p, W, H);
        }

        // ------------------------------------------------------------------ item bar (top)

        void DrawHotbar(PlayerController p, float W)
        {
            var inv = p.Inventory;
            if (inv == null) return;
            const float slot = 78f, gap = 8f;
            float total = Inventory.SlotCount * slot + (Inventory.SlotCount - 1) * gap;
            float x0 = (W - total) / 2f, y = 14f;
            float sinceSwitch = Time.time - inv.LastSwitchTime;
            float alpha = sinceSwitch < 2.5f ? 1f : Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(sinceSwitch - 2.5f));
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                var r = new Rect(x0 + i * (slot + gap), y, slot, slot);
                var w = inv.Slots[i];
                bool sel = i == inv.CurrentIndex;
                UI.Rounded(r, sel ? new Color(UI.Accent.r, UI.Accent.g, UI.Accent.b, 0.85f * alpha) : new Color(0.05f, 0.05f, 0.07f, 0.65f * alpha), true);
                if (sel) UI.Outline(r, new Color(1f, 0.85f, 0.8f, alpha), 2f);
                UI.Label(new Rect(r.x + 6, r.y + 2, 20, 20), (i + 1).ToString(), 15, new Color(1, 1, 1, 0.75f * alpha));
                if (w != null)
                {
                    var icon = WeaponIcon(w.Def);
                    if (icon != null) UI.Tex(new Rect(r.x + 8, r.y + 10, slot - 16, slot - 22), icon, new Color(1, 1, 1, alpha), ScaleMode.ScaleToFit);
                    if (w is Firearm fa && !fa.InfAmmo)
                    {
                        float frac = fa.Ammo / (float)Mathf.Max(1, fa.Capacity);
                        UI.Fill(new Rect(r.x + 6, r.yMax - 7, (slot - 12) * Mathf.Clamp01(frac), 3), frac < 0.25f ? UI.Bad : new Color(1, 1, 1, 0.7f * alpha));
                    }
                    if (UI.Hit(r) && UIRoot.CursorWanted == false && Settings.TouchControlsActive) inv.Select(i);
                }
            }
            var cur = inv.Current;
            if (cur != null && sinceSwitch < 2.5f)
            {
                float a = 1f - Mathf.Clamp01((sinceSwitch - 1.8f) / 0.7f);
                UI.Label(new Rect(0, y + slot + 4, W, 30), cur.Def.Name.ToUpperInvariant() + Mods.ModWeapons.Suffix(cur.Def), 22, new Color(1, 1, 1, a), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
            }
        }

        void DrawCompass(PlayerController p, float W)
        {
            float yaw = p.Cam != null ? p.Cam.transform.eulerAngles.y : 0f;
            float cx = W / 2f, y = 14f + 78f + 34f, half = 220f;
            UI.Fill(new Rect(cx - half, y + 14, half * 2, 1), new Color(1, 1, 1, 0.15f));
            string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f;
                float delta = Mathf.DeltaAngle(yaw, a);
                if (Mathf.Abs(delta) > 60f) continue;
                float x = cx + delta / 60f * half;
                float fade = 1f - Mathf.Abs(delta) / 60f;
                if (i % 3 == 0) UI.Label(new Rect(x - 20, y - 6, 40, 20), names[i / 3], 14, new Color(1, 1, 1, fade * (i % 6 == 0 ? 0.95f : 0.6f)), TextAnchor.MiddleCenter);
                else UI.Fill(new Rect(x, y + 10, 1, 6), new Color(1, 1, 1, fade * 0.4f));
            }
            UI.Fill(new Rect(cx - 1, y + 8, 2, 12), UI.Accent2);
        }

        // ------------------------------------------------------------------ crosshair & markers

        void DrawCrosshair(PlayerController p, float W, float H)
        {
            if (p.Health.Dead) return;
            var cur = p.Inventory != null ? p.Inventory.Current : null;
            float aim = cur != null ? cur.AimWeight : 0f;
            var d = Settings.Data;
            var col = Settings.CrosshairColor;
            float cx = W / 2f, cy = H / 2f;
            if (aim < 0.6f && d.crosshairStyle != 5)
            {
                float gap = 6f;
                if (cur is Firearm fa) gap = 4f + fa.Spread * 9f;
                gap *= d.crosshairSize;
                float len = 8f * d.crosshairSize, th = 2f;
                var c = col.WithAlpha(1f - aim / 0.6f);
                int s = d.crosshairStyle;
                if (cur is SpawnGun sg && sg.Holding) { c = new Color(0.4f, 1f, 0.6f); }
                if (s == 0 || s == 3 || s == 4)
                {
                    if (s != 4) UI.Fill(new Rect(cx - th / 2, cy - gap - len, th, len), c);
                    UI.Fill(new Rect(cx - th / 2, cy + gap, th, len), c);
                    UI.Fill(new Rect(cx - gap - len, cy - th / 2, len, th), c);
                    UI.Fill(new Rect(cx + gap, cy - th / 2, len, th), c);
                }
                if (s == 1 || s == 3) UI.Rounded(new Rect(cx - 2.5f, cy - 2.5f, 5, 5), c, true);
                if (s == 2) UI.Tex(new Rect(cx - gap - 6, cy - gap - 6, (gap + 6) * 2, (gap + 6) * 2), TextureFactory.Ring, c);
            }
            // Hit marker (white), kill marker (red, bigger).
            if (Settings.Data.hitmarkers)
            {
                float ht = Time.unscaledTime - hitTime, kt = Time.unscaledTime - killTime;
                if (ht < 0.25f || kt < 0.5f)
                {
                    bool kill = kt < 0.5f;
                    float a = kill ? 1f - kt / 0.5f : 1f - ht / 0.25f;
                    float sz = kill ? 16f : 11f + Mathf.Clamp(hitDamage / 20f, 0, 6);
                    var mc = (kill ? UI.Bad : Color.white).WithAlpha(a);
                    DrawDiagonal(cx, cy, sz, mc);
                }
            }
        }

        static void DrawDiagonal(float cx, float cy, float size, Color c)
        {
            var m = GUI.matrix;
            for (int i = 0; i < 4; i++)
            {
                GUIUtility.RotateAroundPivot(45f + i * 90f, new Vector2(cx, cy) * UI.Scale);
                UI.Fill(new Rect(cx - 1, cy - size - 4, 2.5f, size * 0.6f), c);
                GUI.matrix = m;
            }
        }

        void DrawNumbers(PlayerController p)
        {
            if (p.Cam == null) return;
            float now = Time.unscaledTime;
            for (int i = numbers.Count - 1; i >= 0; i--)
            {
                var n = numbers[i];
                float age = now - n.Time;
                if (age > 1.2f) { numbers.RemoveAt(i); continue; }
                var sp = p.Cam.WorldToScreenPoint(n.World + Vector3.up * age * 0.6f);
                if (sp.z < 0) continue;
                var g = ToGui(sp);
                UI.Label(new Rect(g.x - 40, g.y - 15, 80, 30), Mathf.RoundToInt(n.Amount).ToString(), n.Amount > 60 ? 26 : 20, (n.Amount > 60 ? UI.Bad : UI.Warn).WithAlpha(1f - age / 1.2f), TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }

        // ------------------------------------------------------------------ name tags, subtitles, thoughts

        void DrawNameTags(PlayerController p)
        {
            if (p.Cam == null) return;
            var d = Settings.Data;
            var camPos = p.Cam.transform.position;
            foreach (var doll in Goredoll.All)
            {
                if (doll == null || !doll.Parts.TryGetValue(PartType.Head, out var head) || head == null) continue;
                var top = head.Gibbed || head.Severed ? doll.Position + Vector3.up * 0.9f : head.transform.position + Vector3.up * 0.32f;
                float dist = Vector3.Distance(camPos, top);
                if (dist > 22f) continue;
                var sp = p.Cam.WorldToScreenPoint(top);
                if (sp.z < 0.1f) continue;
                if (Physics.Linecast(camPos, top, Layers.SightMask, QueryTriggerInteraction.Ignore)) continue;
                var g = ToGui(sp);
                float a = Mathf.Clamp01((22f - dist) / 6f);
                float y = g.y - 40;
                if (d.npcNames && dist < 12f)
                {
                    var status = doll.IsDead ? "DEAD" : doll.Vitals.State == LifeState.Unconscious ? "UNCONSCIOUS" : doll.Vitals.Paralysis == Paralysis.Total ? "PARALYSED" : null;
                    UI.Label(new Rect(g.x - 150, y, 300, 22), doll.DisplayName, 17, Color.white.WithAlpha(a), TextAnchor.MiddleCenter);
                    if (status != null) UI.Label(new Rect(g.x - 150, y + 18, 300, 20), status, 14, (doll.IsDead ? UI.Bad : UI.Warn).WithAlpha(a), TextAnchor.MiddleCenter);
                    else
                    {
                        // Blood volume bar.
                        float bv = 1f - doll.Vitals.BloodLossFraction;
                        UI.Fill(new Rect(g.x - 30, y + 22, 60, 4), new Color(0, 0, 0, 0.5f * a));
                        UI.Fill(new Rect(g.x - 30, y + 22, 60 * bv, 4), Color.Lerp(UI.Bad, UI.Good, (bv - 0.6f) / 0.4f).WithAlpha(a));
                    }
                }
                if (Rules.ShowAiThoughts && doll.Brain != null && !doll.IsDead)
                    UI.Label(new Rect(g.x - 200, y - 44, 400, 40), "<" + doll.Brain.Action + "> " + doll.Brain.Thought + $"\nfear {doll.Brain.Fear:0.00}  anger {doll.Brain.Anger:0.00}", 13, new Color(0.6f, 0.9f, 1f, a), TextAnchor.LowerCenter, FontStyle.Normal, true, true);
                if (d.subtitles && doll.Voice != null && Time.time < doll.Voice.SubtitleUntil && !string.IsNullOrEmpty(doll.Voice.Subtitle) && dist < 18f)
                {
                    string text = doll.Voice.Subtitle;
                    float tw = Mathf.Min(UI.TextWidth(text, 16) + 20, 420);
                    var r = new Rect(g.x - tw / 2, y - 76, tw, 28);
                    UI.Rounded(r, new Color(0, 0, 0, 0.55f * a), true);
                    UI.Label(r, text, 16, new Color(1f, 0.95f, 0.8f, a), TextAnchor.MiddleCenter, FontStyle.Italic);
                }
            }
        }

        // ------------------------------------------------------------------ multiplayer

        void DrawPlayerTags(PlayerController p)
        {
            var nm = NetManager.I;
            if (p.Cam == null || nm == null) return;
            var camPos = p.Cam.transform.position;
            foreach (var np in nm.Players.Values)
            {
                if (np.Avatar == null) continue;
                var top = np.Avatar.EyePosition + Vector3.up * 0.42f;
                float dist = Vector3.Distance(camPos, top);
                if (dist > 60f) continue;
                var sp = p.Cam.WorldToScreenPoint(top);
                if (sp.z < 0.1f) continue;
                bool visible = !Physics.Linecast(camPos, top, Layers.SightMask, QueryTriggerInteraction.Ignore);
                var g = ToGui(sp);
                float a = visible ? 1f : 0.35f;
                bool talking = VoiceChat.I != null && VoiceChat.I.IsTalking(np.Id);
                string label = np.Name + ((np.Flags & NetPlayer.FDead) != 0 ? "  (dead)" : "");
                // Profile icon + name + compact badge chips (Core/PlayerProfile.cs, UI/BadgeChips.cs)
                var prof = nm.ProfileOf(np);
                int badgeMask = prof != null ? prof.BadgeMask & ~1 : 0;
                float chipsW = badgeMask != 0 ? BadgeChips.Width(badgeMask, 22, true, 3) + 6 : 0;
                float tw = UI.TextWidth(label, 17) + (talking ? 34 : 18) + 26 + chipsW;
                var r = new Rect(g.x - tw / 2, g.y - 30, tw, 28);
                UI.Rounded(r, new Color(0, 0, 0, 0.45f * a), true);
                PlayerProfile.DrawIcon(new Rect(r.x + 3, r.y + 2, 24, 24), prof, a);
                float lx = r.x + 26;
                if (talking) { UI.Tex(new Rect(lx + 4, r.y + 4, 20, 20), IconFactory.Get("speaker"), UI.Good.WithAlpha(a)); lx += 26; }
                UI.Label(new Rect(lx, r.y, r.xMax - lx - chipsW, r.height), label, 17, (np.IsHost ? UI.Warn : Color.white).WithAlpha(a), TextAnchor.MiddleCenter);
                if (badgeMask != 0) BadgeChips.Draw(new Rect(r.xMax - chipsW, r.y + 3, chipsW - 4, 22), badgeMask, true, a, 3);
                if (dist < 15f && (np.Flags & NetPlayer.FDead) == 0)
                {
                    float hp = np.Health / 100f;
                    UI.Fill(new Rect(g.x - 30, r.yMax + 3, 60, 4), new Color(0, 0, 0, 0.5f * a));
                    UI.Fill(new Rect(g.x - 30, r.yMax + 3, 60 * Mathf.Clamp01(hp), 4), Color.Lerp(UI.Bad, UI.Good, hp).WithAlpha(a));
                }
            }
        }

        void DrawChat(float H)
        {
            var log = NetManager.I.ChatLog;
            bool open = UIRoot.Contains<ChatScreen>();
            float y = H - 250;
            int shown = 0;
            for (int i = log.Count - 1; i >= 0 && shown < 8; i--)
            {
                var (text, color, time) = log[i];
                float age = Time.unscaledTime - time;
                if (!open && age > 12f) break;
                float a = open ? 1f : Mathf.Clamp01((12f - age) / 2f);
                y -= 28;
                text = ChatFilter.Apply(text);
                float tw = Mathf.Min(UI.TextWidth(text, 17) + 20, 760);
                UI.Rounded(new Rect(30, y, tw, 26), new Color(0, 0, 0, 0.45f * a), true);
                UI.Label(new Rect(40, y, tw - 10, 26), text, 17, color.WithAlpha(a));
                shown++;
            }
            if (!open && log.Count == 0) UI.Label(new Rect(30, H - 270, 400, 22), NetState.Active ? "T: chat" : "T: chat (type /help for commands)", 14, UI.Muted.WithAlpha(0.6f));
        }

        void DrawVoice(float W)
        {
            var vc = VoiceChat.I;
            var nm = NetManager.I;
            if (vc == null || nm == null) return;
            float y = 170;
            if (vc.Transmitting)
            {
                UI.Rounded(new Rect(W - 230, y, 200, 34), new Color(0.1f, 0.5f, 0.2f, 0.75f), true);
                UI.Tex(new Rect(W - 222, y + 5, 24, 24), IconFactory.Get("speaker"), Color.white);
                UI.Label(new Rect(W - 192, y, 160, 34), "Talking", 17, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                UI.Fill(new Rect(W - 120, y + 14, 80 * vc.InputLevel, 6), UI.Good);
                y += 40;
            }
            else if (!string.IsNullOrEmpty(vc.MicError) && Settings.Data.voiceMode != 2)
            {
                UI.Label(new Rect(W - 420, y, 390, 30), "Mic: " + vc.MicError, 14, UI.Warn, TextAnchor.MiddleRight);
                y += 32;
            }
            foreach (var p in nm.Players.Values)
            {
                if (p.Id == nm.LocalId || !vc.IsTalking(p.Id)) continue;
                UI.Rounded(new Rect(W - 230, y, 200, 32), new Color(0, 0, 0, 0.55f), true);
                UI.Tex(new Rect(W - 222, y + 5, 22, 22), IconFactory.Get("speaker"), UI.Good);
                UI.Label(new Rect(W - 194, y, 160, 32), p.Name, 16, Color.white);
                y += 36;
            }
        }

        // ------------------------------------------------------------------ health, ammo, hints

        void DrawHealth(PlayerController p, float H)
        {
            var h = p.Health;
            lastHealth = Mathf.Lerp(lastHealth, h.Current, 0.08f);
            var r = new Rect(30, H - 92, 340, 62);
            UI.Rounded(r, new Color(0, 0, 0, 0.45f), true);
            UI.Tex(new Rect(r.x + 12, r.y + 14, 34, 34), IconFactory.Get("heart"), Rules.Invincible ? UI.Warn : UI.Bad);
            var bar = new Rect(r.x + 58, r.y + 34, 260, 10);
            UI.Fill(bar, new Color(1, 1, 1, 0.1f));
            UI.Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(lastHealth / h.Max), bar.height), new Color(1f, 0.85f, 0.85f, 0.6f));
            float f = Mathf.Clamp01(h.Current / h.Max);
            UI.Fill(new Rect(bar.x, bar.y, bar.width * f, bar.height), Color.Lerp(UI.Bad, UI.Good, f));
            UI.Label(new Rect(r.x + 58, r.y + 4, 200, 30), Mathf.CeilToInt(h.Current).ToString(), 26, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
            if (Rules.Invincible) UI.Label(new Rect(r.x + 130, r.y + 4, 190, 30), "INVINCIBLE", 14, UI.Warn, TextAnchor.MiddleRight);

            // Damage direction indicator.
            float since = Time.time - h.LastDamageTime;
            if (since < 1.2f && p.Cam != null && h.LastDamageDir.sqrMagnitude > 0.01f)
            {
                var local = p.Cam.transform.InverseTransformDirection(-h.LastDamageDir);
                float ang = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                var m = GUI.matrix;
                var c = new Vector2(UI.VW / 2f, UI.VH / 2f);
                GUIUtility.RotateAroundPivot(ang, c * UI.Scale);
                UI.Tex(new Rect(c.x - 40, c.y - 170, 80, 40), TextureFactory.Corona, UI.Bad.WithAlpha(1f - since / 1.2f));
                GUI.matrix = m;
            }
        }

        void DrawAmmo(PlayerController p, float W, float H)
        {
            var cur = p.Inventory != null ? p.Inventory.Current : null;
            if (cur == null) return;
            var r = new Rect(W - 370, H - 110, 340, 80);
            UI.Rounded(r, new Color(0, 0, 0, 0.45f), true);
            var icon = WeaponIcon(cur.Def);
            if (icon != null) UI.Tex(new Rect(r.x + 10, r.y + 8, 110, 64), icon, Color.white, ScaleMode.ScaleToFit);
            string ammo = cur.AmmoText;
            bool low = cur is Firearm fa && !fa.InfAmmo && fa.Ammo <= Mathf.Max(1, fa.Capacity / 4);
            if (cur is Firearm f2 && f2.Reloading) ammo = "RELOADING";
            UI.Label(new Rect(r.x + 120, r.y + 4, 210, 44), ammo, ammo.Length > 10 ? 22 : 34, low ? UI.Bad : Color.white, TextAnchor.MiddleRight, FontStyle.Normal, true, false, UI.HeavyFont);
            UI.Label(new Rect(r.x + 120, r.y + 46, 210, 26), cur.ModeText, 15, UI.Accent2, TextAnchor.MiddleRight);
            if (low && !(cur is Firearm f3 && f3.Reloading) && Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f)
                UI.Label(new Rect(0, H / 2f + 60, W, 30), "[R] RELOAD", 20, UI.Bad, TextAnchor.MiddleCenter);
        }

        void DrawHints(PlayerController p, float W, float H)
        {
            string hint = p.LookHint;
            if (p.Inventory != null && p.Inventory.Current is SpawnGun sg)
            {
                if (!string.IsNullOrEmpty(sg.TargetName) && string.IsNullOrEmpty(hint)) hint = sg.TargetName;
                if (sg.Holding) hint = "Scroll: distance · hold R: rotate (Shift = roll) · F: freeze · RMB: throw · X: delete";
            }
            if (!string.IsNullOrEmpty(hint))
            {
                float tw = Mathf.Min(UI.TextWidth(hint, 18) + 30, W - 100);
                var r = new Rect((W - tw) / 2f, H / 2f + 90, tw, 34);
                UI.Rounded(r, new Color(0, 0, 0, 0.5f), true);
                UI.Label(r, hint, 18, Color.white, TextAnchor.MiddleCenter);
            }
        }

        void DrawChips(PlayerController p, float H)
        {
            float x = 30, y = H - 128;
            void Chip(bool on, string text, Color c)
            {
                if (!on) return;
                float w = UI.TextWidth(text, 13) + 18;
                UI.Rounded(new Rect(x, y, w, 24), c.WithAlpha(0.8f), true);
                UI.Label(new Rect(x, y, w, 24), text, 13, Color.black, TextAnchor.MiddleCenter, FontStyle.Bold, false);
                x += w + 6;
            }
            Chip(p.Noclip, "NOCLIP", new Color(0.5f, 0.8f, 1f));
            Chip(Rules.NoFallDamage, "NO FALL DMG", UI.Good);
            Chip(Rules.AiIgnorePlayer, "AI IGNORE", new Color(0.8f, 0.6f, 1f));
            Chip(Rules.InfiniteAmmo, "INF AMMO", UI.Warn);
            Chip(Rules.FreezeAi, "AI FROZEN", new Color(0.6f, 0.9f, 1f));
            Chip(!Mathf.Approximately(Rules.TimeScale, 1f), "TIME ×" + Rules.TimeScale.ToString("0.00"), UI.Accent2);
            Chip(Rules.ExplosiveBullets, "EXPLOSIVE ROUNDS", UI.Bad);
        }

        void DrawDamageVignette(PlayerController p, float W, float H)
        {
            var h = p.Health;
            float low = 1f - Mathf.Clamp01(h.Current / (h.Max * 0.4f));
            float pulse = low > 0 ? (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)) * low * 0.5f : 0f;
            float a = Mathf.Clamp01(h.DamageFlash + pulse);
            if (a > 0.01f) UI.Tex(new Rect(0, 0, W, H), TextureFactory.Vignette, new Color(0.7f, 0f, 0f, a));
            if (CameraFX.FlashAlpha > 0.01f) UI.Fill(new Rect(0, 0, W, H), CameraFX.FlashColor.WithAlpha(CameraFX.FlashAlpha));
        }

        void DrawDeath(PlayerController p, float W, float H)
        {
            if (!p.Health.Dead) return;
            float t = Mathf.Clamp01((p.Health.RespawnAt - Time.time) / 4f);
            UI.Fill(new Rect(0, 0, W, H), new Color(0.15f, 0f, 0f, 0.55f));
            UI.Label(new Rect(0, H / 2 - 120, W, 120), "YOU DIED", 110, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
            UI.Label(new Rect(0, H / 2, W, 40), p.Health.DeathCause, 24, Color.white, TextAnchor.MiddleCenter);
            UI.Label(new Rect(0, H / 2 + 44, W, 30), "Respawning in " + Mathf.Max(0f, p.Health.RespawnAt - Time.time).ToString("0.0") + "s", 18, UI.Muted, TextAnchor.MiddleCenter);
        }

        void DrawMapTitle(float W, float H)
        {
            float age = Time.unscaledTime - mapTitleTime;
            if (age > 5f || string.IsNullOrEmpty(mapTitle)) return;
            float a = Mathf.Clamp01(age / 0.5f) * (1f - Mathf.Clamp01((age - 4f) / 1f));
            UI.Label(new Rect(0, H * 0.25f, W, 90), mapTitle.ToUpperInvariant(), 72, Color.white.WithAlpha(a), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
            UI.Fill(new Rect(W / 2 - 160 * a, H * 0.25f + 92, 320 * a, 3), UI.Accent.WithAlpha(a));
            UI.Label(new Rect(W / 2 - 400, H * 0.25f + 100, 800, 60), mapDesc, 20, UI.Muted.WithAlpha(a), TextAnchor.UpperCenter, FontStyle.Normal, true, true);
        }

        void DrawStats(GameManager gm, float x, float y)
        {
            int alive = 0, dead = 0;
            foreach (var g in Goredoll.All) if (g != null) { if (g.IsDead) dead++; else alive++; }
            string s = $"Goredolls {alive} alive / {dead} dead\nDecals {Decals.ActiveCount} · Debris {Debris.Count}\nVoices {AudioManager.ActiveVoices} · Time ×{Time.timeScale:0.00}\nPos {PlayerController.Instance.transform.position:F1}";
            UI.Rounded(new Rect(x - 4, y, 300, 92), new Color(0, 0, 0, 0.45f), true);
            UI.Label(new Rect(x + 4, y + 4, 290, 86), s, 14, UI.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
        }
    }
}
