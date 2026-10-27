using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// The dev panel's FUN and MOBILE tabs: big one-tap toys made for fingers. World toys only run for the host
    /// or in singleplayer (clients get a warning), like the console commands they reuse.
    /// </summary>
    public static class DevFunTab
    {
        sealed class Item
        {
            public string Header, Label;
            public Action Run;
            public Func<bool> On;      // non-null = toggle-style button
            public ButtonKind Kind = ButtonKind.Normal;
            public bool Wide;
        }

        static readonly List<Item> items = new List<Item>();
        static int rainbowPrev;

        static void H(string text) => items.Add(new Item { Header = text });
        static void B(string label, Action run, ButtonKind kind = ButtonKind.Normal) => items.Add(new Item { Label = label, Run = run, Kind = kind });
        static void T(string label, Func<bool> on, Action flip) => items.Add(new Item { Label = label, Run = flip, On = on });
        static void C(string label, string cmd, ButtonKind kind = ButtonKind.Normal) => B(label, () => DevConsole.Execute(cmd), kind);

        // ------------------------------------------------------------------ guards

        static bool World()
        {
            if (NetState.IsClient) { Warn("Only the host can do that in multiplayer."); return false; }
            if (PlayerController.Instance == null) { Warn("Load a map first."); return false; }
            return true;
        }

        static void Warn(string s) { Debug.LogWarning(s); GameEvents.ShowToast(s); }

        static Vector3 OnGround(Vector3 pos)
        {
            if (NavGrid.Current != null && NavGrid.Current.Nearest(pos, out int cx, out int cz)) return NavGrid.Current.CellCenter(cx, cz);
            if (Physics.Raycast(pos + Vector3.up * 4f, Vector3.down, out var g, 30f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return g.point;
            return pos;
        }

        // ------------------------------------------------------------------ toys

        static void SpawnRing(int n, float radius, Func<string> preset)
        {
            if (!World()) return;
            var p = PlayerController.Instance.transform.position;
            for (int i = 0; i < n; i++)
            {
                var pos = OnGround(p + Quaternion.Euler(0, i * 360f / n, 0) * Vector3.forward * radius);
                GameManager.I.SpawnGoredollAt(pos, preset());
            }
            Debug.Log("Spawned " + n + " around you.");
        }

        static void ZombieHorde(int n)
        {
            if (!World()) return;
            n = Mathf.Min(n, Mathf.Max(0, Zombies.MaxActive - Zombies.AliveCount));
            if (n <= 0) { Warn("Too many zombies already (" + Zombies.MaxActive + " max)."); return; }
            SpawnRing(n, 14f, () => Zombies.PresetFor(Zombies.RandomKind()));
            GameEvents.ShowToast("Here they come...");
        }

        static void ExplodeNearby(float range)
        {
            if (!World()) return;
            var p = PlayerController.Instance;
            int n = 0;
            foreach (var g in Goredoll.All.ToArray())
            {
                if (g == null || (g.Position - p.transform.position).magnitude > range) continue;
                Explosion.Create(g.Position, 3.5f, 150f, p, 1.2f);
                n++;
            }
            for (int i = 0; i < 6; i++)
            {
                var pos = OnGround(p.transform.position + Quaternion.Euler(0, i * 60f + UnityEngine.Random.Range(-20f, 20f), 0) * Vector3.forward * UnityEngine.Random.Range(9f, 18f));
                Explosion.Create(pos + Vector3.up * 0.3f, 4f, 120f, p, 1.2f);
            }
            CameraFX.Shake(0.8f);
            Debug.Log("Boom. " + n + " Goredoll(s) caught in it.");
        }

        static void Gravity(float scale, string name)
        {
            if (!World()) return;
            Rules.GravityScale = scale;
            Rules.ApplyGravity();
            Debug.Log(name + " gravity (x" + scale.ToString("0.00") + ")");
        }

        static void SetTime(float scale)
        {
            if (NetState.Active) { Warn("Time scale only works in singleplayer."); return; }
            Rules.TimeScale = scale;
            Debug.Log("Time x" + scale.ToString("0.00"));
        }

        static void ForDolls(Action<Goredoll> act, bool includeDead)
        {
            if (!World()) return;
            int n = 0;
            foreach (var g in Goredoll.All.ToArray())
                if (g != null && (includeDead || !g.IsDead)) { act(g); n++; }
            Debug.Log("Affected " + n + " Goredoll(s).");
        }

        static Color Neon()
        {
            // Hard-switching electric colours: cyan, magenta, lime, yellow.
            int i = Mathf.FloorToInt(UnityEngine.Time.time * 3f) & 3;
            return i == 0 ? new Color(0f, 1f, 1f) : i == 1 ? new Color(1f, 0f, 1f) : i == 2 ? new Color(0.4f, 1f, 0f) : new Color(1f, 1f, 0f);
        }

        static bool RainbowOn => Settings.Data.bloodColor == Settings.BloodColorNames.Length - 1;

        // ------------------------------------------------------------------ tab contents

        static void BuildFun()
        {
            H("SPAWN");
            B("10 Goredolls around me", () => SpawnRing(10, 5f, () => GoredollFactory.Presets[UnityEngine.Random.Range(0, GoredollFactory.Presets.Length)]));
            B("Zombie horde (12)", () => ZombieHorde(12));
            B("Zombie horde (25)", () => ZombieHorde(25));
            C("Nextbot", "nextbot grinner");
            C("Rain melons", "rain watermelon 20");
            C("Rain barrels", "rain explosive_barrel 8");

            H("BOOM");
            B("Explode everything nearby", () => ExplodeNearby(35f), ButtonKind.Danger);
            B("Airstrike at aim", () => { if (World()) MobileDev.Airstrike(14); }, ButtonKind.Danger);
            C("NUKE at aim", "nuke", ButtonKind.Danger);
            C("Explosion at aim", "explode");
            C("Explode all Goredolls", "explode_all");
            T("Explosive bullets", () => Rules.ExplosiveBullets, () => DevConsole.Execute("explosive_bullets"));

            H("GRAVITY & TIME");
            B("Moon gravity", () => Gravity(0.165f, "Moon"), Mathf.Approximately(Rules.GravityScale, 0.165f) ? ButtonKind.Primary : ButtonKind.Normal);
            B("Zero-G", () => Gravity(0f, "Zero"), Rules.GravityScale == 0f ? ButtonKind.Primary : ButtonKind.Normal);
            B("Reverse gravity (4 s)", () => { if (World()) MobileDev.ReverseGravity(4f); }, Rules.GravityScale < 0f ? ButtonKind.Primary : ButtonKind.Normal);
            B("Normal gravity", () => Gravity(1f, "Normal"), Mathf.Approximately(Rules.GravityScale, 1f) ? ButtonKind.Primary : ButtonKind.Normal);
            T("Slow-mo (x0.25)", () => Rules.TimeScale < 0.9f, () => SetTime(Rules.TimeScale < 0.9f ? 1f : 0.25f));
            T("2x speed", () => Rules.TimeScale > 1.5f, () => SetTime(Rules.TimeScale > 1.5f ? 1f : 2f));

            H("ME");
            B("Giant mode (x3)", () => MobileDev.SetPlayerScale(3f), MobileDev.PlayerScale > 1.5f ? ButtonKind.Primary : ButtonKind.Normal);
            B("Tiny mode (x0.35)", () => MobileDev.SetPlayerScale(0.35f), MobileDev.PlayerScale < 0.7f ? ButtonKind.Primary : ButtonKind.Normal);
            B("Normal size", () => MobileDev.SetPlayerScale(1f));
            T("God mode", () => Rules.Invincible, () => DevConsole.Execute("god"));
            T("Noclip", () => PlayerController.Instance != null && PlayerController.Instance.Noclip, () => DevConsole.Execute("noclip"));
            C("Launch me", "launch_me 25");

            H("GOREDOLLS");
            B("Ragdoll everyone", () => ForDolls(g => { g.Ragdoll.Fall(4f); g.Launch(Vector3.up * 2f + UnityEngine.Random.insideUnitSphere * 3f); }, false));
            B("Revive all", () => ForDolls(g => g.Revive(), true));
            T("Freeze all AI", () => Rules.FreezeAi, () => DevConsole.Execute("ai_freeze"));
            B("Launch nearby", () => { if (World()) Debug.Log("Launched " + MobileDev.LaunchNearby(30f, 12f)); });
            C("Dance party", "dance_all");
            C("Panic all", "panic_all");
            C("Kill all", "kill_all", ButtonKind.Danger);
            C("Remove all", "despawn_all", ButtonKind.Danger);

            H("BLOOD");
            T("Rainbow blood", () => RainbowOn && Settings.BloodColorOverride == null, () =>
            {
                Settings.BloodColorOverride = null;
                if (RainbowOn) Settings.Data.bloodColor = rainbowPrev;
                else { rainbowPrev = Settings.Data.bloodColor; Settings.Data.bloodColor = Settings.BloodColorNames.Length - 1; }
                Settings.Save();
            });
            T("Neon blood", () => Settings.BloodColorOverride != null, () => Settings.BloodColorOverride = Settings.BloodColorOverride == null ? Neon : (Func<Color>)null);
            C("Gore: unreal", "gore 5");
            C("Clear blood", "clear_decals");
        }

        static readonly float[] Sens = { 0.5f, 0.75f, 1f, 1.5f, 2f, 3f };
        static readonly float[] Zooms = { 1f, 1.15f, 1.3f, 1.5f, 1.7f };

        static void BuildMobile()
        {
            var d = Settings.Data;
            H("GYRO AIM" + (MobileDev.GyroSupported ? "" : "  (no gyroscope on this device)"));
            T("Gyro aim", () => d.gyroAim, () => { d.gyroAim = !d.gyroAim; Settings.Save(); });
            B("Sensitivity: " + d.gyroSensitivity.ToString("0.00") + "x", () =>
            {
                int i = Array.FindIndex(Sens, s => s >= d.gyroSensitivity - 0.01f);
                d.gyroSensitivity = Sens[(i + 1) % Sens.Length];
                Settings.Save();
            });
            T("Invert gyro X", () => d.gyroInvertX, () => { d.gyroInvertX = !d.gyroInvertX; Settings.Save(); });
            T("Invert gyro Y", () => d.gyroInvertY, () => { d.gyroInvertY = !d.gyroInvertY; Settings.Save(); });

            H("SHAKE");
            T("Shake to ragdoll", () => d.shakeToRagdoll, () => { d.shakeToRagdoll = !d.shakeToRagdoll; Settings.Save(); });
            B("Test shake now", () => { if (World()) Debug.Log("Launched " + MobileDev.LaunchNearby(30f, 11f)); });

            H("OVERLAYS");
            T("Touch visualizer", () => MobileDev.TouchVisualizer, () => MobileDev.TouchVisualizer = !MobileDev.TouchVisualizer);
            T("FPS + device overlay", () => MobileDev.PerfOverlay, () => MobileDev.PerfOverlay = !MobileDev.PerfOverlay);
            T("Show FPS (HUD)", () => d.showFps, () => { d.showFps = !d.showFps; Settings.Save(); });

            H("PANEL & CONTROLS");
            B("Panel zoom: " + DevPanel.Zoom.ToString("0.00") + "x", () =>
            {
                int i = Array.FindIndex(Zooms, z => z >= DevPanel.Zoom - 0.01f);
                DevPanel.Zoom = Zooms[(i + 1) % Zooms.Length];
            });
            B("Touch controls: " + (d.touchMode == 0 ? "Auto" : d.touchMode == 1 ? "Always" : "Never"), () => { d.touchMode = (d.touchMode + 1) % 3; Settings.Save(); });
            B("Button size: " + d.touchButtonScale.ToString("0.00") + "x", () => { d.touchButtonScale = d.touchButtonScale >= 1.4f ? 0.8f : d.touchButtonScale + 0.15f; Settings.Save(); });
            C("Screenshot", "screenshot");
            C("System info to log", "sysinfo");
            C("Force GC", "gc");
        }

        // ------------------------------------------------------------------ drawing

        public static void DrawFun(Rect body) { items.Clear(); BuildFun(); DrawItems(body, "devfun"); }

        public static void DrawMobile(Rect body)
        {
            items.Clear();
            BuildMobile();
            string info = "Device " + SystemInfo.deviceModel + "  |  " + Screen.width + "x" + Screen.height + " @ " + Screen.dpi.ToString("0") + " dpi  |  gyro " + (MobileDev.GyroSupported ? "yes" : "no") +
                          "  |  accel " + Input.acceleration.magnitude.ToString("0.00") + " g  |  last shake " + MobileDev.LastShakeG.ToString("0.0") + " g  |  touches " + Input.touchCount +
                          "\nSecret: hold 3 fingers for 1 s anywhere, or tap the version label in the main menu 7 times.";
            UI.Label(new Rect(body.x, body.y, body.width, 56), info, 18, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, false, true);
            DrawItems(new Rect(body.x, body.y + 64, body.width, body.height - 64), "devmobile");
        }

        static void DrawItems(Rect body, string id)
        {
            const float bh = 78f, gap = 12f, headH = 46f;
            float minW = 320f;
            int cols = Mathf.Max(1, Mathf.FloorToInt((body.width - 12 + gap) / (minW + gap)));
            float bw = (body.width - 12 - gap * (cols - 1)) / cols;

            // Layout pass
            var rects = new Rect[items.Count];
            float y = 0;
            int col = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.Header != null)
                {
                    if (col != 0) { y += bh + gap; col = 0; }
                    rects[i] = new Rect(0, y + (i == 0 ? 0 : 8), body.width - 12, headH - 8);
                    y += headH + (i == 0 ? 0 : 8);
                    continue;
                }
                rects[i] = new Rect(col * (bw + gap), y, bw, bh);
                if (++col >= cols) { col = 0; y += bh + gap; }
            }
            if (col != 0) y += bh + gap;
            float content = y;

            float s = UI.BeginScroll(id, body, content);
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var r = rects[i];
                r.y -= s;
                if (r.yMax < 0 || r.y > body.height) continue;
                if (it.Header != null)
                {
                    UI.Label(r, it.Header, 20, UI.Accent2, TextAnchor.LowerLeft, FontStyle.Bold);
                    UI.Fill(new Rect(r.x, r.yMax + 2, r.width, 2), UI.Accent2.WithAlpha(0.35f));
                    continue;
                }
                bool on = it.On != null && it.On();
                string label = it.On != null ? it.Label + (on ? "  [ON]" : "  [OFF]") : it.Label;
                var kind = it.On != null ? (on ? ButtonKind.Primary : ButtonKind.Normal) : it.Kind;
                if (MobileDev.Tap(r, label, kind, 22))
                {
                    try { it.Run(); }
                    catch (Exception e) { Debug.LogError(it.Label + " failed: " + e.Message); }
                }
            }
            UI.EndScroll(id, body, content);
        }
    }
}
