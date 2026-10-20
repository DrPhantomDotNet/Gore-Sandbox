using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Every console command available in the F5 dev panel.</summary>
    public static class DevCommands
    {
        static PlayerController P
        {
            get
            {
                var p = PlayerController.Instance;
                if (p == null) throw new Exception("No player — load a map first.");
                return p;
            }
        }

        static float F(string[] a, int i, float def) => a.Length > i && float.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
        static int N(string[] a, int i, int def) => a.Length > i && int.TryParse(a[i], out var v) ? v : def;
        static string S(string[] a, int i, string def = "") => a.Length > i ? a[i] : def;
        static void Log(string s) => Debug.Log(s);

        static bool Flip(ref bool b, string name, string[] a)
        {
            if (a.Length > 0) b = a[0] == "1" || a[0].ToLowerInvariant() == "on" || a[0].ToLowerInvariant() == "true";
            else b = !b;
            Log(name + ": " + (b ? "ON" : "OFF"));
            return b;
        }

        static Vector3 AimPoint(out Vector3 normal)
        {
            var p = P;
            if (Physics.Raycast(p.EyePosition, p.AimDirection, out var hit, 300f, Layers.ShootMask, QueryTriggerInteraction.Ignore)) { normal = hit.normal; return hit.point; }
            normal = Vector3.up;
            return p.EyePosition + p.AimDirection * 10f;
        }

        static Goredoll AimDoll()
        {
            var p = P;
            if (Physics.Raycast(p.EyePosition, p.AimDirection, out var hit, 300f, Layers.ShootMask, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<Goredoll>();
            return null;
        }

        static void ForDolls(Action<Goredoll> act, bool includeDead = false)
        {
            int n = 0;
            foreach (var g in Goredoll.All.ToArray())
                if (g != null && (includeDead || !g.IsDead)) { act(g); n++; }
            Log("Affected " + n + " Goredoll(s).");
        }

        static Vector3 InFront(float dist)
        {
            var p = P;
            var pos = p.transform.position + MathX.Flat(p.AimDirection).normalized * dist;
            if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out var g, 20f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) pos = g.point;
            return pos;
        }

        public static void RegisterAll()
        {
            var R = (Action<string, string, string, Action<string[]>, string>)DevConsole.Register;

            // ------------------------------------------------------------ general
            R("help", "General", "Lists commands (optionally filtered).", a =>
            {
                var f = S(a, 0).ToLowerInvariant();
                foreach (var g in DevConsole.Commands.Values.Where(c => f == "" || c.Name.Contains(f) || c.Category.ToLowerInvariant() == f).GroupBy(c => c.Category))
                    Log("<" + g.Key + ">  " + string.Join("  ", g.Select(c => c.Name)));
                Log(DevConsole.Commands.Count + " commands. 'help <word>' filters, Tab completes, Up/Down for history, ';' chains commands.");
            }, "[filter]");
            R("about", "General", "Explains one command.", a => { if (DevConsole.Commands.TryGetValue(S(a, 0).ToLowerInvariant(), out var c)) Log(c.Name + " " + c.Usage + " — " + c.Help); else Log("No such command."); }, "<command>");
            R("clear", "General", "Clears the console output.", a => DevConsole.Clear(), "");
            R("echo", "General", "Prints text.", a => Log(string.Join(" ", a)), "<text>");
            R("history", "General", "Shows command history.", a => Log(string.Join("\n", DevConsole.History)), "");
            R("version", "General", "Game version.", a => Log($"{GameInfo.Title} v{GameInfo.Version} ({GameInfo.Access}) by {GameInfo.Studio}, Unity {Application.unityVersion}"), "");
            R("sysinfo", "General", "Hardware and runtime info.", a => Log($"{SystemInfo.operatingSystem}\nCPU {SystemInfo.processorType} ×{SystemInfo.processorCount}\nGPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}, {SystemInfo.graphicsMemorySize} MB)\nRAM {SystemInfo.systemMemorySize} MB\nScreen {Screen.width}×{Screen.height} (display {Screen.currentResolution})"), "");
            R("mem", "General", "Managed memory use.", a => Log($"Managed heap: {GC.GetTotalMemory(false) / 1048576f:0.0} MB · Unity allocated: {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f:0.0} MB"), "");
            R("gc", "General", "Forces garbage collection.", a => { GC.Collect(); Resources.UnloadUnusedAssets(); Log("GC done."); }, "");
            R("quit", "General", "Quits the game.", a => GameManager.I.Quit(), "");
            R("menu", "General", "Back to the main menu.", a => GameManager.I.ReturnToMenu(), "");
            R("restart", "General", "Restarts the current map.", a => GameManager.I.RestartMap(), "");
            R("maps", "General", "Lists maps.", a => Log(string.Join("\n", MapCatalog.All.Select(m => m.Id + " — " + m.Name + (m.IsUnlocked() ? "" : " (locked)")))), "");
            R("map", "General", "Loads a map by id.", a =>
            {
                var m = MapCatalog.All.Find(x => x.Id == S(a, 0));
                if (m == null) { Log("Unknown map. Try 'maps'."); return; }
                GameManager.I.StartMap(m, new MapOptions { Time = m.DefaultTime, StartingNpcs = N(a, 1, 3) });
            }, "<id> [npcs]");
            R("screenshot", "General", "Saves a screenshot.", a => GameManager.I.Screenshot(), "");
            R("fps", "General", "Sets the FPS cap (-1 = unlimited).", a => { Settings.Data.targetFps = N(a, 0, 144); Settings.Data.vsync = false; Settings.Save(); Log("FPS cap " + Settings.Data.targetFps); }, "<cap>");
            R("vsync", "General", "Toggles V-Sync.", a => { Flip(ref Settings.Data.vsync, "V-Sync", a); Settings.Save(); }, "[on|off]");
            R("quality", "General", "Sets the quality level.", a => { Settings.Data.qualityLevel = N(a, 0, 3); Settings.Save(); Log("Quality " + QualitySettings.names[QualitySettings.GetQualityLevel()]); }, "<level>");
            R("hud", "General", "Toggles the HUD.", a => GameManager.I.HudHidden = !GameManager.I.HudHidden, "");
            R("toast", "General", "Shows a notification.", a => GameEvents.ShowToast(string.Join(" ", a)), "<text>");
            R("log_test", "General", "Writes a log, a warning and an error.", a => { Debug.Log("Test log"); Debug.LogWarning("Test warning"); Debug.LogError("Test error"); }, "");
            R("exception_test", "General", "Throws a handled exception into the log.", a => Debug.LogException(new InvalidOperationException("Test exception from the dev console")), "");

            // ------------------------------------------------------------ multiplayer
            R("net", "General", "Multiplayer status.", a =>
            {
                var nm = NetManager.I;
                if (nm == null || !NetState.Active) { Log("Not in multiplayer. Lobby: " + (nm != null ? nm.LobbyStatus : "-")); return; }
                Log($"{NetState.Role} · '{nm.ServerName}' · port {nm.Port} · code {nm.ServerCode} · {(nm.ViaRelay ? "relay" : "direct")} · public {(nm.PublicEndpoint != null ? nm.PublicEndpoint.ToString() : "unknown")} · {nm.Players.Count} players · in {nm.BytesIn / 1024f:0} KB / out {nm.BytesOut / 1024f:0} KB · {NetEntity.All.Count} networked objects · creative {NetState.LocalCreative}");
            }, "");
            R("players", "General", "Lists players in the session.", a =>
            {
                var nm = NetManager.I;
                if (nm == null || !NetState.Active) { Log("Not in multiplayer."); return; }
                foreach (var p in nm.Players.Values) Log($"#{p.Id} {p.Name}{(p.IsHost ? " [host]" : "")}{(p.Creative ? " [creative]" : "")} · {p.PingMs} ms · {p.Address}");
            }, "");
            R("kick", "General", "Host: kicks a player by name.", a =>
            {
                if (!NetState.IsHost) { Log("Only the host can kick."); return; }
                foreach (var p in NetManager.I.Players.Values) if (p.Name.ToLowerInvariant() == string.Join(" ", a).ToLowerInvariant()) { NetManager.I.Kick(p); return; }
                Log("No player with that name.");
            }, "<name>");
            R("creative", "General", "Host: toggles Creative mode for a player by name.", a =>
            {
                if (!NetState.IsHost) { Log("Only the host can do that."); return; }
                foreach (var p in NetManager.I.Players.Values) if (p.Name.ToLowerInvariant() == string.Join(" ", a).ToLowerInvariant()) { NetManager.I.SetCreative(p, !p.Creative); return; }
                Log("No player with that name.");
            }, "<name>");

            // ------------------------------------------------------------ time & world
            R("timescale", "World", "Sets the time scale.", a => { Rules.TimeScale = F(a, 0, 1f); Log("Time ×" + Rules.TimeScale); }, "<0.05-3>");
            R("slowmo", "World", "Toggles 0.25× time.", a => { Rules.TimeScale = Rules.TimeScale < 0.9f ? 1f : 0.25f; Log("Time ×" + Rules.TimeScale); }, "");
            R("gravity", "World", "Gravity multiplier.", a => { Rules.GravityScale = F(a, 0, 1f); Rules.ApplyGravity(); Log("Gravity ×" + Rules.GravityScale); }, "<x>");
            R("moon", "World", "Moon gravity.", a => { Rules.GravityScale = 0.165f; Rules.ApplyGravity(); Log("Moon gravity."); }, "");
            R("lighting", "World", "Changes the time of day: day, sunset, night, bloodmoon.", a =>
            {
                var t = S(a, 0, "day").ToLowerInvariant();
                var tod = t == "sunset" ? TimeOfDay.Sunset : t == "night" ? TimeOfDay.Night : t == "bloodmoon" ? TimeOfDay.BloodMoon : TimeOfDay.Day;
                if (WorldRoot.Current != null) MapLighting.Apply(tod, WorldRoot.Current.Def.Indoor, WorldRoot.Current.transform);
                Log("Lighting: " + tod);
            }, "<day|sunset|night|bloodmoon>");
            R("explode", "World", "Explosion where you aim.", a => { var p = AimPoint(out var n); Explosion.Create(p + n * 0.2f, F(a, 0, 4f), F(a, 1, 150f), P, 1f); }, "[radius] [damage]");
            R("nuke", "World", "A very large explosion where you aim.", a => { var p = AimPoint(out var n); Explosion.Create(p + n * 0.5f, 18f, 800f, P, 3f); CameraFX.Flash(Color.white, 1f); Stats.FoundEasterEgg("Nuke"); }, "");
            R("lightning", "World", "Lightning strike where you aim (Admin Gun smite).", a => { var p = AimPoint(out _); var d = AimDoll(); if (d != null) d.Kill("Struck by lightning"); Explosion.Create(p, 2f, 80f, P, 0.6f); FX.Flash(p, new Color(0.7f, 0.85f, 1f), 30f, 8f, 0.25f); }, "");
            R("tracers", "World", "Toggles bullet tracers.", a => Flip(ref Rules.BulletTracers, "Tracers", a), "[on|off]");
            R("explosive_bullets", "World", "Every bullet explodes.", a => Flip(ref Rules.ExplosiveBullets, "Explosive bullets", a), "[on|off]");
            R("superkick", "World", "Kicks launch things.", a => Flip(ref Rules.SuperKick, "Super kick", a), "[on|off]");
            R("force", "World", "Bullet force multiplier.", a => { Rules.BulletForceMultiplier = F(a, 0, 1f); Log("Bullet force ×" + Rules.BulletForceMultiplier); }, "<x>");
            R("damage", "World", "Damage multiplier.", a => { Rules.DamageMultiplier = F(a, 0, 1f); Log("Damage ×" + Rules.DamageMultiplier); }, "<x>");
            R("colliders", "World", "Shows physics colliders.", a => Flip(ref Rules.ShowColliders, "Colliders", a), "[on|off]");
            R("wireframe", "World", "Wireframe rendering.", a => Flip(ref CameraOverlays.Wireframe, "Wireframe", a), "[on|off]");
            R("xray", "World", "See organs through skin.", a => Flip(ref Rules.Xray, "X-ray", a), "[on|off]");
            R("fog", "World", "Toggles fog.", a => { RenderSettings.fog = !RenderSettings.fog; Log("Fog " + RenderSettings.fog); }, "");
            R("clear_decals", "World", "Removes blood and bullet holes.", a => { Decals.ClearAll(); BloodFX.Clear(); }, "");
            R("clear_debris", "World", "Removes casings, mags and gibs.", a => Debris.ClearAll(), "");
            R("clear_all", "World", "Removes everything spawned.", a => WorldRoot.Current?.ClearDynamic(), "");
            R("solver", "World", "Physics solver iterations.", a => { Physics.defaultSolverIterations = N(a, 0, 6); Physics.defaultSolverVelocityIterations = N(a, 1, 1); Log("Solver " + Physics.defaultSolverIterations + "/" + Physics.defaultSolverVelocityIterations); }, "<pos> [vel]");
            R("physics_rate", "World", "Physics steps per second.", a => { float hz = Mathf.Clamp(F(a, 0, 60f), 20f, 240f); Time.fixedDeltaTime = 1f / hz; Log("Physics at " + hz + " Hz"); }, "<hz>");

            // ------------------------------------------------------------ player
            R("god", "Player", "Invincibility.", a => Flip(ref Rules.Invincible, "God mode", a), "[on|off]");
            R("nofall", "Player", "No fall damage.", a => Flip(ref Rules.NoFallDamage, "No fall damage", a), "[on|off]");
            R("noclip", "Player", "Toggles noclip.", a => P.SetNoclip(!P.Noclip), "");
            R("heal", "Player", "Full health.", a => P.Health.Revive(), "");
            R("hurt", "Player", "Damages you.", a => P.Health.TakeDamage(new DamageInfo { Amount = F(a, 0, 25f), Type = DamageType.Generic, Weapon = "Console", Point = P.transform.position, Direction = Vector3.down }), "<amount>");
            R("kill_me", "Player", "Kills you.", a => P.Health.Die("Typed kill_me"), "");
            R("respawn", "Player", "Respawns you.", a => P.Respawn(), "");
            R("pos", "Player", "Prints your position.", a => Log("Position " + P.transform.position.ToString("F2") + " yaw " + P.transform.eulerAngles.y.ToString("0")), "");
            R("tp", "Player", "Teleports to coordinates.", a => P.Teleport(new Vector3(F(a, 0, 0), F(a, 1, 1), F(a, 2, 0))), "<x> <y> <z>");
            R("tp_aim", "Player", "Teleports to where you aim.", a => { var p = AimPoint(out var n); P.Teleport(p + n * 0.5f); }, "");
            R("tp_spawn", "Player", "Teleports to the map spawn.", a => P.Teleport(WorldRoot.Current.PlayerSpawn, WorldRoot.Current.PlayerYaw), "");
            R("launch_me", "Player", "Launches you upward.", a => P.Launch(Vector3.up * F(a, 0, 20f) + P.AimDirection * 6f), "[speed]");
            R("speed", "Player", "Movement speed multiplier.", a => { Rules.PlayerSpeedMultiplier = F(a, 0, 1f); Log("Speed ×" + Rules.PlayerSpeedMultiplier); }, "<x>");
            R("jump", "Player", "Jump multiplier.", a => { Rules.JumpMultiplier = F(a, 0, 1f); Log("Jump ×" + Rules.JumpMultiplier); }, "<x>");
            R("fov", "Player", "Field of view.", a => { Settings.Data.fov = Mathf.Clamp(F(a, 0, 80f), 50f, 130f); Settings.Save(); }, "<deg>");
            R("sens", "Player", "Mouse sensitivity.", a => { Settings.Data.mouseSensitivity = F(a, 0, 2f); Settings.Save(); }, "<x>");
            R("give", "Player", "Gives a weapon: " + string.Join(", ", WeaponCatalog.All.Select(w => w.Id)), a => { if (P.Inventory.Give(S(a, 0)) < 0) Log("Unknown weapon."); }, "<weapon>");
            R("give_all", "Player", "Gives every weapon.", a => P.Inventory.GiveAll(), "");
            R("strip", "Player", "Removes all weapons except fists and the Spawn Gun.", a => { for (int i = 2; i < Inventory.SlotCount; i++) P.Inventory.Remove(i); }, "");
            R("ammo", "Player", "Infinite ammo.", a => Flip(ref Rules.InfiniteAmmo, "Infinite ammo", a), "[on|off]");
            R("fast_reload", "Player", "Instant reloads.", a => Flip(ref Rules.NoReload, "Instant reload", a), "[on|off]");
            R("refill", "Player", "Refills every gun.", a => { foreach (var w in P.Inventory.Slots) if (w is Firearm f) f.Refill(); }, "");
            R("weapons", "Player", "Lists weapons.", a => Log(string.Join("\n", WeaponCatalog.All.Select(w => w.Id + " — " + w.Name + ": " + w.Description))), "");
            foreach (var w in WeaponCatalog.All) { var id = w.Id; R("give_" + id, "Player", "Gives the " + w.Name + ".", a => P.Inventory.Give(id), ""); }

            // ------------------------------------------------------------ spawning
            R("spawnables", "Spawn", "Lists everything spawnable.", a => Log(string.Join("\n", SpawnCatalog.AI.Concat(SpawnCatalog.Items).Concat(SpawnCatalog.Props).Concat(SpawnCatalog.Entities).Select(d => d.Category + ": " + d.Id + " — " + d.Name))), "");
            R("spawn", "Spawn", "Spawns something in front of you.", a =>
            {
                var d = SpawnCatalog.Find(S(a, 0));
                if (d == null) { Log("Unknown id. Try 'spawnables'."); return; }
                int n = Mathf.Clamp(N(a, 1, 1), 1, 50);
                for (int i = 0; i < n; i++) SpawnCatalog.Spawn(d, InFront(3f) + new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f) * (n > 1 ? 1 : 0), d.Category == "Props" ? 1f + i * 0.6f : 0, UnityEngine.Random.Range(-1.5f, 1.5f) * (n > 1 ? 1 : 0)), Quaternion.LookRotation(-MathX.Flat(P.AimDirection) + Vector3.forward * 0.001f));
            }, "<id> [count]");
            R("goredoll", "Spawn", "Spawns Goredolls: preset Normal/Coward/Brave/Aggressive/Passive.", a =>
            {
                var preset = GoredollFactory.Presets.FirstOrDefault(p => p.ToLowerInvariant() == S(a, 0, "normal").ToLowerInvariant()) ?? "Normal";
                int n = Mathf.Clamp(N(a, 1, 1), 1, 30);
                for (int i = 0; i < n; i++) GameManager.I.SpawnGoredollAt(InFront(3f + i * 0.9f) + MathX.Flat(Quaternion.Euler(0, 90, 0) * P.AimDirection).normalized * (i % 2 == 0 ? 1 : -1) * (i * 0.4f), preset);
            }, "[preset] [count]");
            R("horde", "Spawn", "Spawns a ring of Goredolls around you.", a =>
            {
                int n = Mathf.Clamp(N(a, 0, 10), 1, 40);
                for (int i = 0; i < n; i++)
                {
                    var pos = P.transform.position + Quaternion.Euler(0, i * 360f / n, 0) * Vector3.forward * F(a, 1, 8f);
                    if (NavGrid.Current != null && NavGrid.Current.Nearest(pos, out int cx, out int cz)) pos = NavGrid.Current.CellCenter(cx, cz);
                    GameManager.I.SpawnGoredollAt(pos, "Aggressive");
                }
            }, "[count] [radius]");
            R("nextbot", "Spawn", "Spawns a nextbot: " + string.Join(", ", Nextbots.Defs.Select(d => d.Id)), a => { Nextbots.Spawn(S(a, 0, "grinner"), InFront(15f)); Stats.FoundEasterEgg("Nextbots"); }, "<id>");
            R("rain", "Spawn", "Rains props from the sky.", a =>
            {
                var d = SpawnCatalog.Find(S(a, 0, "watermelon")) ?? SpawnCatalog.Find("watermelon");
                int n = Mathf.Clamp(N(a, 1, 15), 1, 60);
                for (int i = 0; i < n; i++) SpawnCatalog.Spawn(d, P.transform.position + new Vector3(UnityEngine.Random.Range(-8f, 8f), 15f + i * 0.5f, UnityEngine.Random.Range(-8f, 8f)), UnityEngine.Random.rotation);
            }, "[prop] [count]");
            R("select", "Spawn", "Arms the Spawn Gun with an id.", a => { var d = SpawnCatalog.Find(S(a, 0)); if (d != null) { SpawnCatalog.Selected = d; Log("Selected " + d.Name); } else Log("Unknown id."); }, "<id>");
            R("undo", "Spawn", "Removes the last thing you spawned.", a =>
            {
                for (int i = SpawnGun.UndoStack.Count - 1; i >= 0; i--) { var go = SpawnGun.UndoStack[i]; SpawnGun.UndoStack.RemoveAt(i); if (go != null) { SpawnGun.DeleteObject(go); return; } }
            }, "");
            R("freeze_spawns", "Spawn", "Props spawn frozen.", a => Flip(ref SpawnCatalog.SpawnFrozen, "Spawn frozen", a), "[on|off]");
            foreach (var d in PropFactory.Props) { var id = d.Id; R("spawn_" + id, "Spawn", "Spawns a " + d.Name + ".", a => SpawnCatalog.Spawn(SpawnCatalog.Find(id), InFront(2.5f) + Vector3.up * 1f, Quaternion.identity), ""); }

            // ------------------------------------------------------------ AI
            R("ai_ignore", "AI", "AI ignores you.", a => Flip(ref Rules.AiIgnorePlayer, "AI ignore", a), "[on|off]");
            R("ai_freeze", "AI", "Freezes all brains.", a => Flip(ref Rules.FreezeAi, "AI frozen", a), "[on|off]");
            R("ai_thoughts", "AI", "Shows what the AI is thinking.", a => Flip(ref Rules.ShowAiThoughts, "AI thoughts", a), "[on|off]");
            R("infighting", "AI", "Goredolls fight each other.", a => Flip(ref Rules.NpcInfighting, "Infighting", a), "[on|off]");
            R("aggression", "AI", "Global aggression bias (-1..1).", a => { Rules.AiAggression = Mathf.Clamp(F(a, 0, 0f), -1f, 1f); Log("Aggression " + Rules.AiAggression); }, "<-1..1>");
            R("npc_speed", "AI", "Goredoll speed multiplier.", a => { Rules.NpcSpeedMultiplier = F(a, 0, 1f); }, "<x>");
            R("npc_health", "AI", "Goredoll toughness multiplier.", a => { Rules.NpcHealthMultiplier = F(a, 0, 1f); }, "<x>");
            R("npcs", "AI", "Lists Goredolls and their state.", a => { foreach (var g in Goredoll.All) Log(g.DisplayName + " — " + g.StatusLine + " · HR " + g.Vitals.HeartRate.ToString("0") + " · lost " + g.Vitals.BloodLost.ToString("0") + " ml · " + (g.Brain != null ? g.Brain.Action + ": " + g.Brain.Thought : "")); }, "");
            R("kill_all", "AI", "Kills every Goredoll.", a => ForDolls(g => g.Kill("Console: kill_all")), "");
            R("revive_all", "AI", "Revives every Goredoll that still has a head.", a => ForDolls(g => g.Revive(), true), "");
            R("gib_all", "AI", "Gibs every Goredoll's head.", a => ForDolls(g => { if (g.Parts.TryGetValue(PartType.Head, out var h) && h.Gibbable) g.Dismember.Gib(h, Vector3.up); }, true), "");
            R("decap_all", "AI", "Decapitates every Goredoll.", a => ForDolls(g => { if (g.Parts.TryGetValue(PartType.Head, out var h) && h.CanSever) g.Dismember.Sever(h, h.transform.position, Vector3.down, true); }, true), "");
            R("disarm_all", "AI", "Removes everyone's arms.", a => ForDolls(g => { foreach (var t in new[] { PartType.UpperArmL, PartType.UpperArmR }) if (g.Parts.TryGetValue(t, out var p) && p.CanSever) g.Dismember.Sever(p, p.transform.position, Vector3.up, true); }, true), "");
            R("legless_all", "AI", "Removes everyone's legs (they'll crawl).", a => ForDolls(g => { foreach (var t in new[] { PartType.ThighL, PartType.ThighR }) if (g.Parts.TryGetValue(t, out var p) && p.CanSever) g.Dismember.Sever(p, p.transform.position, Vector3.up, true); }, true), "");
            R("launch_all", "AI", "Launches everyone into the air.", a => ForDolls(g => g.Launch(Vector3.up * F(a, 0, 14f) + UnityEngine.Random.insideUnitSphere * 4f), true), "[speed]");
            R("explode_all", "AI", "An explosion on every Goredoll.", a => ForDolls(g => Explosion.Create(g.Position, 3f, 120f, P, 1f), true), "");
            R("dance_all", "AI", "Everyone dances.", a => ForDolls(g => { if (g.Brain != null) g.Brain.ForceDance = true; }), "");
            R("panic_all", "AI", "Everyone panics.", a => ForDolls(g => { if (g.Brain != null) g.Brain.Fear = 1f; }), "");
            R("calm_all", "AI", "Everyone calms down.", a => ForDolls(g => { if (g.Brain != null) { g.Brain.Fear = 0f; g.Brain.Anger = 0f; g.Brain.ForceDance = false; } }), "");
            R("anger_all", "AI", "Everyone gets angry.", a => ForDolls(g => { if (g.Brain != null) g.Brain.Anger = 1f; }), "");
            R("preset_all", "AI", "Applies a personality preset to everyone.", a => ForDolls(g => g.Brain?.ApplyPreset(S(a, 0, "Normal"))), "<preset>");
            R("paralyze_all", "AI", "Severs everyone's spinal cord (still conscious and breathing).", a => ForDolls(g => { g.Vitals.Paralysis = Paralysis.Total; g.Ragdoll.Fall(999f); }), "");
            R("trip_all", "AI", "Knocks everyone over.", a => ForDolls(g => g.Ragdoll.Fall(3f)), "");
            R("despawn_dead", "AI", "Removes dead Goredolls.", a => { foreach (var g in Goredoll.All.ToArray()) if (g != null && g.IsDead) g.Despawn(); }, "");
            R("despawn_all", "AI", "Removes every Goredoll.", a => { foreach (var g in Goredoll.All.ToArray()) g?.Despawn(); }, "");
            R("inspect", "AI", "Opens the wound inspector on the Goredoll you aim at.", a => { var d = AimDoll(); if (d != null) UIRoot.Push(new WoundInspectorScreen(d, null)); else Log("Aim at a Goredoll."); }, "");
            R("say", "AI", "Makes the Goredoll you aim at say a voice line (v_*).", a => { var d = AimDoll(); if (d != null) d.Voice.Say(S(a, 0, "v_hey"), 1f, true); }, "<clip>");

            // ------------------------------------------------------------ gore
            R("gore", "Gore", "Gore level 0-5 (none, minimum, low, medium, high, unreal).", a => { Settings.Data.goreLevel = Mathf.Clamp(N(a, 0, 4), 0, 5); Settings.Save(); Log("Gore: " + Settings.GoreLevels[Settings.Data.goreLevel]); }, "<0-5>");
            R("blood_color", "Gore", "Blood colour: " + string.Join(", ", Settings.BloodColorNames.Select((n, i) => i + "=" + n)), a => { Settings.Data.bloodColor = Mathf.Clamp(N(a, 0, 0), 0, Settings.BloodColorNames.Length - 1); Settings.Save(); }, "<index>");
            R("blood", "Gore", "Blood amount multiplier.", a => { Settings.Data.bloodAmount = F(a, 0, 1f); Settings.Save(); }, "<x>");
            R("bleed", "Gore", "Bleeding speed multiplier.", a => { Rules.BleedMultiplier = F(a, 0, 1f); }, "<x>");
            R("always_dismember", "Gore", "Every severing hit removes the limb.", a => Flip(ref Rules.AlwaysDismember, "Always dismember", a), "[on|off]");
            R("no_bleeding", "Gore", "Disables bleeding.", a => Flip(ref Rules.NoBleeding, "No bleeding", a), "[on|off]");
            R("no_pain", "Gore", "Disables pain reactions.", a => Flip(ref Rules.NoPain, "No pain", a), "[on|off]");
            R("onehit", "Gore", "Any damage kills.", a => Flip(ref Rules.OneHitKill, "One-hit kill", a), "[on|off]");
            R("headshot_kills", "Gore", "Headshots always kill.", a => Flip(ref Rules.InstantDeathOnHeadshot, "Headshots kill", a), "[on|off]");
            R("bigheads", "Gore", "Big heads for new spawns.", a => Flip(ref Rules.BigHeads, "Big heads", a), "[on|off]");
            R("slowmo_kills", "Gore", "Slow motion on every kill.", a => Flip(ref Rules.SlowMoOnKill, "Slow-mo kills", a), "[on|off]");

            // ------------------------------------------------------------ audio
            R("sounds", "Audio", "Lists every synthesised sound.", a => Log(SoundBank.SoundCount + " sounds, " + SoundBank.ClipCount + " clips:\n" + string.Join("  ", SoundBank.Names)), "");
            R("play", "Audio", "Plays a sound by name.", a => { if (SoundBank.Has(S(a, 0))) AudioManager.Play2D(S(a, 0), F(a, 1, 1f), F(a, 2, 1f), AudioBus.Sfx); else Log("No such sound. Try 'sounds'."); }, "<name> [volume] [pitch]");
            R("volume", "Audio", "Master volume 0-1.", a => { Settings.Data.masterVolume = Mathf.Clamp01(F(a, 0, 1f)); Settings.Save(); }, "<0-1>");
            R("music", "Audio", "Plays or stops menu music.", a => { if (S(a, 0) == "off") AudioManager.StopMusic(); else AudioManager.PlayMusic("music_menu"); }, "[on|off]");
            R("voices", "Audio", "Number of playing sounds.", a => Log("Active voices: " + AudioManager.ActiveVoices), "");

            // ------------------------------------------------------------ stats & secrets
            R("stats", "Stats", "Prints lifetime stats.", a => Log($"Kills {Stats.Get(Stats.Kills)} · Headshots {Stats.Get(Stats.Headshots)} · Limbs {Stats.Get(Stats.Limbs)} · Shots {Stats.Get(Stats.ShotsFired)} · Hits {Stats.Get(Stats.Hits)} · Eggs {Stats.Get(Stats.EasterEggs)}"), "");
            R("achievements", "Stats", "Lists achievements.", a => { foreach (var x in Stats.Achievements) Log((Stats.IsUnlocked(x) ? "[x] " : "[ ] ") + x.Name + " — " + x.Description); }, "");
            R("unlock_all", "Stats", "Unlocks every achievement.", a => { foreach (var x in Stats.Achievements) if (!Stats.IsUnlocked(x)) Stats.Unlock(x); }, "");
            R("reset_stats", "Stats", "Resets stats and achievements.", a => Stats.ResetAll(), "");
            R("eggs", "Stats", "Lists the easter eggs you've found.", a => Log(string.Join("\n", EasterEggs.All.Select(e => (Stats.HasEasterEgg(e.Id) ? "[x] " : "[?] ") + (Stats.HasEasterEgg(e.Id) ? e.Id + " — " + e.Hint : e.Hint)))), "");
            R("unlock_secret_map", "Stats", "Marks the Konami easter egg as found (the Backrooms are always open now).", a => { Stats.FoundEasterEgg("Konami"); Log("Konami egg found."); }, "");
            R("drphantom", "Stats", "Hello, owner.", a => { Log("Welcome back, Quibbo/DrPhantom. Everything is on fire, as intended."); Stats.FoundEasterEgg("Owner"); }, "");
            R("alexkiller", "Stats", "Hello, developer.", a => { Log("AlexKiller was here. Probably fixing a ragdoll."); Stats.FoundEasterEgg("Developer"); }, "");
        }
    }
}
