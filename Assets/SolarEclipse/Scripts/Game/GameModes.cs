using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SolarEclipse
{
    /// <summary>
    /// Content Update game modes, each on its own map: Zombie Survival (waves + points shop), Gore Golf (launch a
    /// Goredoll as far as you can), Tournament (1v1 duels; champion bots in singleplayer) and Hide &amp; Seek
    /// (multiplayer; find the hidden Goredolls in singleplayer). The host / singleplayer runs the rules; players get
    /// updates through the Open World event channel (OWEv.ModeEvent / OWReq.ModeReq).
    /// </summary>
    public static class GameModes
    {
        public static IEnumerable<MapDef> Defs()
        {
            yield return new MapDef
            {
                Id = "zombie_survival", Name = "Zombie Survival", Size = "Mode - town square", Glyph = "skull", DefaultTime = TimeOfDay.Night, Ambience = "amb_drone",
                Description = "Hold the town square against endless waves of zombies. Kills earn points; spend them in the shop between waves (B).",
                Features = new[] { "Waves", "Points shop", "Barricades", "Co-op" }, Tags = new[] { "Game Mode" },
                ThemeTop = new Color(0.05f, 0.12f, 0.05f), ThemeBottom = new Color(0.45f, 0.6f, 0.2f), Accent = new Color(0.6f, 1f, 0.3f), Build = b => ModeMaps.ZombieTown(b),
            };
            yield return new MapDef
            {
                Id = "gore_golf", Name = "Gore Golf", Size = "Mode - 300 m fairway", Glyph = "flag", DefaultTime = TimeOfDay.Day, Ambience = "amb_wind",
                Description = "Load a Goredoll into the cannon and launch it down the fairway. Distance, rings and gibs score points. 5 shots a round.",
                Features = new[] { "Cannon", "Bonus rings", "Distance markers", "Best score saved" }, Tags = new[] { "Game Mode" },
                ThemeTop = new Color(0.3f, 0.6f, 0.95f), ThemeBottom = new Color(0.3f, 0.7f, 0.3f), Accent = new Color(1f, 1f, 1f), Build = b => ModeMaps.Golf(b),
            };
            yield return new MapDef
            {
                Id = "tournament", Name = "Tournament", Size = "Mode - arena pit", Glyph = "trophy", DefaultTime = TimeOfDay.BloodMoon, Ambience = "amb_drone",
                Description = "Multiplayer: 1v1 duels in the pit, winner stays on, everyone else watches from the stands. Singleplayer: beat the champions one by one.",
                Features = new[] { "1v1 duels", "Winner stays", "Champion bots", "Stands" }, Tags = new[] { "Game Mode" },
                ThemeTop = new Color(0.25f, 0f, 0.02f), ThemeBottom = new Color(0.8f, 0.6f, 0.2f), Accent = new Color(1f, 0.8f, 0.2f), Build = b => ModeMaps.Tournament(b),
            };
            yield return new MapDef
            {
                Id = "hide_seek", Name = "Hide & Seek", Size = "Mode - mansion", Glyph = "eye", DefaultTime = TimeOfDay.Night, Indoor = true, Ambience = "amb_hum",
                Description = "Multiplayer: one seeker, everyone else hides in the mansion. Get close and press E to catch. Singleplayer: find the 6 hidden Goredolls.",
                Features = new[] { "Seeker vs hiders", "Closets + rooms", "3 minute rounds", "Singleplayer hunt" }, Tags = new[] { "Game Mode" },
                ThemeTop = new Color(0.1f, 0.06f, 0.15f), ThemeBottom = new Color(0.5f, 0.35f, 0.25f), Accent = new Color(0.8f, 0.6f, 1f), Build = b => ModeMaps.Mansion(b),
            };
        }

        static bool ach;
        public static void EnsureAchievements()
        {
            if (ach) return;
            ach = true;
            void Add(string id, string name, string desc, string stat, float t) { if (!Stats.Achievements.Exists(a => a.Id == id)) Stats.Achievements.Add(new Achievement(id, name, desc, stat, t)); }
            Add("zs_wave10", "Last Stand", "Reach wave 10 in Zombie Survival.", "zs_best", 10);
            Add("golf_250", "Hole in One", "Launch a Goredoll 250 m in Gore Golf.", "golf_best_shot", 250);
            Add("duel_5", "Champion", "Win 5 duels in the Tournament.", "duel_wins", 5);
            Add("seek_10", "Found You", "Catch 10 hiders in Hide & Seek.", "seek_catches", 10);
            Add("daily_10", "Daily Grinder", "Complete 10 daily challenges.", "daily_done", 10);
        }

        // ---------------- network glue
        public static void Send(byte to, string s, long n = 0, Vector3 v = default) => NetManager.I.OWEvent(to, OWEv.ModeEvent, 255, s, n, v);
        public static void Request(string s, long n = 0, Vector3 v = default) => NetManager.I.OWRequest(OWReq.ModeReq, s, n, v);
        public static void HostRequest(byte pid, string s, long n, Vector3 v) { if (ModeBase.Current != null) ModeBase.Current.HostRequest(pid, s, n, v); }
        public static void OnEvent(string s, long n, Vector3 v)
        {
            if (s.StartsWith("turf:")) { OWTerritories.OnKill(s.Substring(5)); return; } // Open World gang territories
            if (ModeBase.Current != null) ModeBase.Current.OnEvent(s, n, v);
        }
    }

    /// <summary>Shared bits: current mode, banner, HUD hook, local loadout.</summary>
    public abstract class ModeBase : MonoBehaviour
    {
        public static ModeBase Current { get; private set; }
        protected string banner, bannerSub; protected float bannerUntil;
        protected static byte Me => OWDirector.MyId;
        protected static bool Host => !NetState.IsClient;

        protected virtual void OnEnable() { Current = this; UIRoot.HudLayer += DrawHud; Goredoll.Died += OnDollDied; GameModes.EnsureAchievements(); }
        protected virtual void OnDisable() { if (Current == this) Current = null; UIRoot.HudLayer -= DrawHud; Goredoll.Died -= OnDollDied; }

        public virtual void HostRequest(byte pid, string s, long n, Vector3 v) { }
        public virtual void OnEvent(string s, long n, Vector3 v) { }
        protected virtual void OnDollDied(Goredoll d, object attacker) { }
        protected abstract void DrawMode(float W, float H);

        protected void Banner(string t, string sub, float secs) { banner = t; bannerSub = sub; bannerUntil = Time.time + secs; }

        void DrawHud()
        {
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing || gm.HudHidden) return;
            float W = UI.VW, H = UI.VH;
            DrawMode(W, H);
            if (Time.time < bannerUntil && banner != null)
            {
                float a = Mathf.Clamp01((bannerUntil - Time.time) / 0.5f);
                UI.Label(new Rect(0, H * 0.3f, W, 90), banner, 76, Color.white.WithAlpha(a), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
                if (!string.IsNullOrEmpty(bannerSub)) UI.Label(new Rect(0, H * 0.3f + 86, W, 40), bannerSub, 24, UI.Accent2.WithAlpha(a), TextAnchor.MiddleCenter);
            }
        }

        protected static void Panel(Rect r, string text, int size = 22, Color? col = null)
        {
            UI.Rounded(r, new Color(0, 0, 0, 0.55f), true);
            UI.Label(new Rect(r.x + 14, r.y, r.width - 20, r.height), text, size, col ?? Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        protected static void Give(params string[] ids)
        {
            var p = PlayerController.Instance;
            if (p == null || p.Inventory == null) return;
            foreach (var id in ids) p.Inventory.Give(id, -1, false);
        }

        protected static byte Killer(object attacker) => OWDirector.PlayerOf(attacker);
    }

    // ====================================================================== Zombie Survival

    public sealed class ZombieSurvival : ModeBase
    {
        public readonly List<Vector3> Gates = new List<Vector3>();
        public static int Points;
        int wave, toSpawn, alive; bool fighting; float breakUntil, nextSpawn, overUntil = -1f;
        public int Wave => wave;
        public float BreakLeft => fighting ? 0f : Mathf.Max(0f, breakUntil - Time.time);

        protected override void OnEnable() { base.OnEnable(); Points = 0; }

        void Start()
        {
            Banner("ZOMBIE SURVIVAL", "Kills earn points. B (or the SHOP button) opens the shop between waves.", 5f);
            if (Host) { wave = 0; fighting = false; breakUntil = Time.time + 12f; }
        }

        bool given;
        void Update()
        {
            if (!given && PlayerController.Instance != null) { given = true; Give("knife", "glock"); }
            if (Input.GetKeyDown(KeyCode.B) && !UIRoot.AnyBlocking && !fighting && !UIRoot.DevPanelCapturing) UIRoot.Push(new ZombieShopScreen());
            if (!Host) return;
            if (overUntil > 0f) { if (Time.time > overUntil) { overUntil = -1f; wave = 0; Points = 0; breakUntil = Time.time + 10f; fighting = false; GameModes.Send(255, "reset"); } return; }
            bool allDead = PlayerTargets.All.Count > 0;
            foreach (var p in PlayerTargets.All) if (p != null && !p.IsDead) { allDead = false; break; }
            if (!fighting)
            {
                if (Time.time >= breakUntil) StartWave(wave + 1);
                return;
            }
            alive = 0;
            foreach (var g in Goredoll.All) if (g != null && !g.IsDead && g.GetComponent<ZombieWaveMember>() != null) alive++;
            if (allDead) { GameOver(); return; }
            if (toSpawn > 0 && alive < Mathf.Min(14, Settings.Data.maxRagdolls - 4) && Time.time > nextSpawn)
            {
                nextSpawn = Time.time + Mathf.Max(0.4f, 1.6f - wave * 0.1f);
                toSpawn--;
                var kinds = new List<ZombieKind> { ZombieKind.Walker, ZombieKind.Walker };
                if (wave >= 2) kinds.Add(ZombieKind.Runner);
                if (wave >= 3) kinds.Add(ZombieKind.Crawler);
                if (wave >= 4) kinds.Add(ZombieKind.Brute);
                if (wave >= 6) kinds.Add(ZombieKind.Screamer);
                var gate = Gates.Count > 0 ? Gates[Random.Range(0, Gates.Count)] : Vector3.zero;
                var z = GoredollFactory.Spawn(gate + new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f)), Random.Range(0f, 360f), Zombies.PresetPrefix + kinds[Random.Range(0, kinds.Count)]);
                if (z != null) z.gameObject.AddComponent<ZombieWaveMember>();
            }
            if (toSpawn <= 0 && alive == 0)
            {
                fighting = false;
                breakUntil = Time.time + 25f;
                GameModes.Send(255, "break", wave);
                if (wave > PlayerPrefs.GetInt("zs_best_wave", 0)) PlayerPrefs.SetInt("zs_best_wave", wave);
            }
            if (Time.frameCount % 30 == 0) GameModes.Send(255, "left", alive + toSpawn);
        }

        void StartWave(int n)
        {
            wave = n; fighting = true; toSpawn = 4 + n * 3; nextSpawn = Time.time + 1f;
            GameModes.Send(255, "wave", n);
        }

        void GameOver()
        {
            overUntil = Time.time + 9f;
            foreach (var g in Goredoll.All.ToArray()) if (g != null && g.GetComponent<ZombieWaveMember>() != null) g.Despawn();
            GameModes.Send(255, "over", wave);
        }

        protected override void OnDollDied(Goredoll d, object attacker)
        {
            if (!Host || d == null || d.GetComponent<ZombieWaveMember>() == null) return;
            byte k = Killer(attacker);
            if (k != 255) GameModes.Send(k, "pts", 60 + wave * 5);
        }

        int clientLeft;
        public override void OnEvent(string s, long n, Vector3 v)
        {
            switch (s)
            {
                case "pts": Points += (int)n; Stats.Add("zs_kills"); break;
                case "wave": wave = (int)n; fighting = true; Banner("WAVE " + n, n == 1 ? "Here they come." : "", 3f); AudioManager.Play("bell", PlayerController.Instance != null ? PlayerController.Instance.transform.position : Vector3.zero, 0.8f, 0.6f, 0f); if (UIRoot.Find<ZombieShopScreen>() is UIScreen sc) UIRoot.Remove(sc); break;
                case "break": wave = (int)n; fighting = false; breakUntil = Time.time + 25f; Banner("WAVE " + n + " CLEARED", "25 seconds: press B to shop", 4f); Give("knife", "glock"); var pc = PlayerController.Instance; if (pc != null && !pc.IsDead) pc.Health.Current = pc.Health.Max; Stats.Add("zs_best", Mathf.Max(0, (int)n - (int)Stats.Get("zs_best"))); break;
                case "left": clientLeft = (int)n; break;
                case "over": Banner("GAME OVER", "You survived " + (Mathf.Max(0, (int)n - 1)) + " waves. Restarting...", 8f); fighting = false; break;
                case "reset": Points = 0; wave = 0; fighting = false; breakUntil = Time.time + 10f; break;
            }
        }

        protected override void DrawMode(float W, float H)
        {
            int left = Host ? alive + toSpawn : clientLeft;
            Panel(new Rect(W / 2 - 230, 150, 460, 46), fighting ? "WAVE " + wave + "  ·  " + left + " zombies left" : "Next wave in " + Mathf.CeilToInt(BreakLeft) + "s  ·  B: shop", 22);
            Panel(new Rect(W - 300, 150, 270, 46), Points + " points", 24, new Color(0.6f, 1f, 0.35f));
            if (!fighting && Settings.TouchControlsActive && UI.Button(new Rect(W - 300, 204, 270, 60), "SHOP", ButtonKind.Primary, IconFactory.Get("gift"), 22)) UIRoot.Push(new ZombieShopScreen());
        }
    }

    public sealed class ZombieWaveMember : MonoBehaviour { }

    public sealed class ZombieShopScreen : UIScreen
    {
        static readonly (string id, string name, int cost)[] Items =
        {
            ("health", "Full health", 250), ("refill", "Refill ammo", 150), ("frag", "Frag grenades", 300), ("katana", "Katana", 500),
            ("shotgun", "Shotgun", 450), ("ak47", "AK-47", 800), ("bighammer", "Giant Hammer", 900), ("turret", "Sentry Turret", 1200),
            ("m249", "M249 SAW", 1500), ("flamer", "Flamethrower", 2000), ("laser", "Laser Cutter", 2500), ("minigun", "Minigun", 3500),
        };
        public override bool IsModal => true;
        public override void OnOpen() => AudioManager.Ui("ui_open");
        public override void Tick() { if (Input.GetKeyDown(KeyCode.B) && Age > 0.1f) UIRoot.Remove(this); }
        public override void Draw()
        {
            UI.Dim(0.6f);
            var r = UI.Centered(Mathf.Min(1100, UI.VW - 60), Mathf.Min(760, UI.VH - 40));
            UI.Panel(r, "Survivor Shop");
            UI.Label(new Rect(r.xMax - 330, r.y + 18, 300, 44), ZombieSurvival.Points + " pts", 30, new Color(0.6f, 1f, 0.35f), TextAnchor.MiddleRight, FontStyle.Normal, true, false, UI.HeavyFont);
            float cw = (r.width - 48 - 30) / 4f, ch = 140;
            for (int i = 0; i < Items.Length; i++)
            {
                var it = Items[i];
                var cr = new Rect(r.x + 24 + (i % 4) * (cw + 10), r.y + 90 + (i / 4) * (ch + 12), cw, ch);
                UI.Rounded(cr, UI.PanelLight, true);
                var def = WeaponCatalog.Get(it.id);
                var icon = def != null ? Hud.WeaponIcon(def) : IconFactory.Get(it.id == "health" ? "heart" : "ammo");
                if (icon != null) UI.Tex(new Rect(cr.x + 10, cr.y + 8, cr.width - 20, 56), icon, Color.white, ScaleMode.ScaleToFit);
                UI.Label(new Rect(cr.x + 10, cr.y + 64, cr.width - 20, 26), it.name, 17, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                if (UI.Button(new Rect(cr.x + 10, cr.yMax - 50, cr.width - 20, 42), it.cost + " pts", ButtonKind.Primary, null, 17, ZombieSurvival.Points >= it.cost)) Buy(it.id, it.cost);
            }
            if (UI.Button(new Rect(r.x + 24, r.yMax - 76, 220, 56), "DONE", ButtonKind.Normal, IconFactory.Get("check"))) UIRoot.Remove(this);
        }
        static void Buy(string id, int cost)
        {
            var p = PlayerController.Instance;
            if (p == null || ZombieSurvival.Points < cost) return;
            ZombieSurvival.Points -= cost;
            if (id == "health") p.Health.Current = p.Health.Max;
            else if (id == "refill") { foreach (var w in p.Inventory.Slots) if (w is Firearm f) f.Refill(); else if (w is IRefillable rf) rf.Refill(); }
            else p.Inventory.Give(id, -1, true);
            AudioManager.Ui("cash");
        }
    }

    // ====================================================================== Gore Golf

    public sealed class GoreGolf : ModeBase, IInteractable
    {
        public Transform Barrel; public Vector3 Tee; public readonly List<(Vector3 pos, float r)> Rings = new List<(Vector3, float)>();
        Goredoll ball; float launchedAt = -1f, still, power, best; bool charging; int shot, total; float farthest;
        readonly HashSet<int> ringsHit = new HashSet<int>();
        string last = "";
        public string Prompt => ball != null ? "" : charging ? "FIRE!" : "Load a Goredoll and aim";

        void Start() { Banner("GORE GOLF", "Press E at the cannon to start the power meter, E again to fire. 5 shots.", 6f); best = PlayerPrefs.GetFloat("golf_best", 0f); }

        public void Interact(GameObject user)
        {
            if (ball != null) return;
            if (!charging) { charging = true; power = 0f; AudioManager.Play("button", Barrel.position, 1f); return; }
            charging = false;
            Fire(Mathf.PingPong(power, 1f));
        }

        void Fire(float p)
        {
            if (!Host) return;
            var aim = Barrel.forward;
            var pc = PlayerController.Instance;
            if (pc != null) { var f = MathX.Flat(pc.AimDirection).normalized; float yaw = Mathf.Clamp(Vector3.SignedAngle(Vector3.forward, f, Vector3.up), -25f, 25f); aim = Quaternion.Euler(-28f, yaw, 0) * Vector3.forward; }
            ball = GoredollFactory.Spawn(Barrel.position + aim * 1.2f, 0f, "Coward");
            if (ball == null) return;
            ball.DisplayName = "Golf Ball";
            ball.Ragdoll.Launch(aim * Mathf.Lerp(18f, 46f, p));
            launchedAt = Time.time; still = 0f; farthest = 0f; ringsHit.Clear();
            FX.Flash(Barrel.position, new Color(1f, 0.7f, 0.3f), 10f, 6f, 0.15f);
            FX.Smoke(Barrel.position, aim * 3f, 1.5f, new Color(0.6f, 0.6f, 0.6f, 0.8f), 2.5f);
            AudioManager.Play("explosion_far", Barrel.position, 1f, 1.4f);
            CameraFX.Shake(0.3f);
        }

        void Update()
        {
            if (charging) power += Time.deltaTime * 1.1f;
            if (ball == null || !Host) return;
            var pos = ball.Position;
            float d = MathX.Flat(pos - Tee).magnitude;
            farthest = Mathf.Max(farthest, d);
            for (int i = 0; i < Rings.Count; i++) if (!ringsHit.Contains(i) && Vector3.Distance(pos, Rings[i].pos) < Rings[i].r) { ringsHit.Add(i); AudioManager.Play("chime", pos, 1f); }
            var pel = ball.Parts.TryGetValue(PartType.Pelvis, out var pp) && pp != null ? pp.Body : null;
            still = pel != null && pel.velocity.magnitude < 0.6f ? still + Time.deltaTime : 0f;
            if ((still > 1.2f && Time.time - launchedAt > 1.5f) || Time.time - launchedAt > 12f)
            {
                int gibs = 0; foreach (var part in ball.Parts.Values) if (part != null && (part.Gibbed || part.Severed)) gibs++;
                int score = Mathf.RoundToInt(farthest) + ringsHit.Count * 100 + gibs * 40;
                shot++; total += score;
                Stats.Add("golf_shots");
                if (farthest > Stats.Get("golf_best_shot")) Stats.Add("golf_best_shot", farthest - Stats.Get("golf_best_shot"));
                last = Mathf.RoundToInt(farthest) + " m  +" + ringsHit.Count + " rings  +" + gibs + " gibs  =  " + score;
                GameModes.Send(255, "shot:" + last, score);
                if (shot >= 5)
                {
                    if (total > best) { best = total; PlayerPrefs.SetFloat("golf_best", best); }
                    GameModes.Send(255, "round:" + total, total);
                    shot = 0; total = 0;
                }
                ball = null;
            }
        }

        public override void OnEvent(string s, long n, Vector3 v)
        {
            if (s.StartsWith("shot:")) { last = s.Substring(5); Banner(n + " POINTS", last, 4f); }
            else if (s.StartsWith("round:")) { Banner("ROUND OVER", "Total " + s.Substring(6) + "  ·  best " + Mathf.RoundToInt(Mathf.Max(best, n)), 6f); }
        }

        protected override void DrawMode(float W, float H)
        {
            Panel(new Rect(W / 2 - 260, 150, 520, 46), "Shot " + (shot + 1) + "/5  ·  round " + total + "  ·  best " + Mathf.RoundToInt(best), 21);
            if (charging)
            {
                float p = Mathf.PingPong(power, 1f);
                var r = new Rect(W / 2 - 200, H - 220, 400, 26);
                UI.Fill(r, new Color(0, 0, 0, 0.6f));
                UI.Fill(new Rect(r.x, r.y, r.width * p, r.height), Color.Lerp(UI.Good, UI.Bad, p));
                UI.Label(new Rect(r.x, r.y - 34, r.width, 30), "POWER - press E to fire", 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            if (ball != null) UI.Label(new Rect(W / 2 - 200, 200, 400, 40), Mathf.RoundToInt(farthest) + " m", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
        }
    }

    // ====================================================================== Tournament

    public sealed class Tournament : ModeBase
    {
        public Vector3 SpotA, SpotB, Stands;
        readonly List<byte> queue = new List<byte>();
        byte a = 255, b = 255; float roundStart = -1f, nextCheck;
        // singleplayer champions
        static readonly (string name, string preset)[] Champions =
        {
            ("Brawler", "Aggressive"), ("Gunslinger", "Gunman"), ("The Chef", null), ("Ninja", null), ("SWAT", "Soldier"), ("Giant", "Aggressive~g"), ("Giant Ninja", null),
        };
        int champ; Goredoll champion; string status = "";

        void Start()
        {
            if (NetState.Active) Banner("TOURNAMENT", "1v1 in the pit. Winner stays on." + (NetManager.I != null && !NetManager.I.Server.PvP && NetState.IsHost ? " (Turn on PvP!)" : ""), 6f);
            else Banner("TOURNAMENT", "Beat the champions one by one.", 5f);
        }

        bool given;

        void Update()
        {
            if (!given && PlayerController.Instance != null && !NetState.Active) { given = true; Give("katana", "glock", "shotgun"); }
            if (!Host || Time.time < nextCheck) return;
            nextCheck = Time.time + 0.5f;
            if (!NetState.Active) { SoloTick(); return; }
            foreach (var id in OWDirector.PlayerIds()) if (!queue.Contains(id) && id != a && id != b) queue.Add(id);
            queue.RemoveAll(id => !NetManager.I.OWHasPlayer(id));
            if (a != 255 && !NetManager.I.OWHasPlayer(a)) a = 255;
            if (b != 255 && !NetManager.I.OWHasPlayer(b)) b = 255;
            if (a == 255 && queue.Count > 0) { a = queue[0]; queue.RemoveAt(0); GameModes.Send(a, "tp", 0, SpotA); roundStart = Time.time; }
            if (b == 255 && queue.Count > 0) { b = queue[0]; queue.RemoveAt(0); GameModes.Send(b, "tp", 0, SpotB); roundStart = Time.time; }
            status = (a != 255 ? NetManager.I.OWName(a) : "?") + "  vs  " + (b != 255 ? NetManager.I.OWName(b) : "waiting");
            GameModes.Send(255, "status:" + status);
            if (a == 255 || b == 255 || Time.time - roundStart < 3f) return;
            bool da = OWDirector.IsDead(a), db = OWDirector.IsDead(b);
            if (da || db)
            {
                byte win = da ? b : a, lose = da ? a : b;
                GameModes.Send(255, "win:" + NetManager.I.OWName(win) + " beats " + NetManager.I.OWName(lose));
                GameModes.Send(win, "won");
                queue.Add(lose);
                if (da) a = 255; else b = 255;
                GameModes.Send(win, "tp", 0, da ? SpotB : SpotA);
            }
        }

        void SoloTick()
        {
            var p = PlayerController.Instance;
            if (p == null) return;
            if (champion == null && champ < Champions.Length)
            {
                var c = Champions[champ];
                string preset = c.preset ?? (c.name == "The Chef" ? NpcTypes.Preset("chef") : c.name == "Ninja" ? NpcTypes.Preset("ninja") : NpcTypes.Preset("ninja", true));
                champion = GoredollFactory.Spawn(SpotB, 0f, preset);
                if (champion != null) { champion.DisplayName = c.name; if (champion.Brain != null) champion.Brain.Hostile = true; }
                p.Teleport(SpotA);
                Banner("ROUND " + (champ + 1), "vs " + c.name, 3f);
            }
            status = champ < Champions.Length ? "Champion " + (champ + 1) + "/" + Champions.Length + ": " + (champion != null ? champion.DisplayName : "") : "You are the champion!";
            if (champion != null && champion.IsDead) { champion = null; champ++; Stats.Add("duel_wins"); Banner(champ >= Champions.Length ? "CHAMPION!" : "VICTORY", champ >= Champions.Length ? "You beat every champion." : "Next challenger incoming...", 4f); p.Health.Current = p.Health.Max; }
            if (p.IsDead) { champ = 0; if (champion != null) champion.Despawn(); champion = null; }
        }

        public override void OnEvent(string s, long n, Vector3 v)
        {
            if (s == "tp") { var p = PlayerController.Instance; if (p != null) { p.Teleport(v); if (!p.IsDead) p.Health.Current = p.Health.Max; } }
            else if (s == "won") Stats.Add("duel_wins");
            else if (s.StartsWith("status:")) status = s.Substring(7);
            else if (s.StartsWith("win:")) Banner("WINNER", s.Substring(4), 4f);
        }

        protected override void DrawMode(float W, float H) { if (!string.IsNullOrEmpty(status)) Panel(new Rect(W / 2 - 300, 150, 600, 46), status, 22); }
    }

    // ====================================================================== Hide & Seek

    public sealed class HideSeek : ModeBase
    {
        public Vector3 SeekerRoom, HideStart; public readonly List<Vector3> HidingSpots = new List<Vector3>();
        readonly HashSet<byte> seekers = new HashSet<byte>();
        float phaseUntil; int phase; // 0 lobby, 1 hide, 2 seek
        bool iSeek; int hidersLeft; float timeLeft;
        readonly List<Goredoll> soloHidden = new List<Goredoll>(); int soloFound;

        void Start()
        {
            if (!NetState.Active) { Banner("HIDE & SEEK", "6 Goredolls are hiding in the mansion. Find them (press E near them) in 3 minutes.", 6f); SoloSetup(); }
            else Banner("HIDE & SEEK", "A round starts when 2+ players are here.", 5f);
            phaseUntil = Time.time + 10f;
        }

        void SoloSetup()
        {
            soloHidden.Clear(); soloFound = 0;
            var spots = new List<Vector3>(HidingSpots);
            for (int i = 0; i < 6 && spots.Count > 0; i++)
            {
                int k = Random.Range(0, spots.Count);
                var d = GoredollFactory.Spawn(spots[k], Random.Range(0f, 360f), "Coward");
                spots.RemoveAt(k);
                if (d != null) { d.DisplayName = "Hider"; soloHidden.Add(d); }
            }
            phase = 2; timeLeft = 180f;
        }

        void Update()
        {
            var p = PlayerController.Instance;
            if (p == null) return;
            if (!NetState.Active)
            {
                if (phase != 2) return;
                timeLeft -= Time.deltaTime;
                foreach (var d in soloHidden)
                    if (d != null && !d.IsDead && d.DisplayName == "Hider" && Vector3.Distance(d.Position, p.transform.position) < 2.5f && PlayerInput.Frame.Use)
                    { d.DisplayName = "Found"; soloFound++; AudioManager.Ui("ui_success"); Stats.Add("seek_catches"); Banner("FOUND " + soloFound + "/" + soloHidden.Count, "", 2f); }
                if (soloFound >= soloHidden.Count) { Banner("YOU FOUND THEM ALL", Mathf.RoundToInt(180f - timeLeft) + " seconds", 6f); phase = 0; Invoke(nameof(SoloSetup), 8f); }
                else if (timeLeft <= 0f) { Banner("TIME'S UP", "You found " + soloFound + "/" + soloHidden.Count, 6f); phase = 0; foreach (var d in soloHidden) if (d != null) d.Despawn(); Invoke(nameof(SoloSetup), 8f); }
                return;
            }
            // Seeker: catch hiders by getting close and pressing E.
            if (iSeek && phase == 2 && PlayerInput.Frame.Use && NetManager.I != null)
                foreach (var np in NetManager.I.Players.Values)
                    if (np.Avatar != null && np.Id != Me && Vector3.Distance(np.Avatar.transform.position, p.transform.position) < 2.6f) { GameModes.Request("catch", np.Id); break; }
            if (iSeek && phase == 1 && Vector3.Distance(p.transform.position, SeekerRoom) > 3f) p.Teleport(SeekerRoom);
            timeLeft = Mathf.Max(0f, timeLeft - Time.deltaTime);
            if (!Host) return;
            var ids = OWDirector.PlayerIds();
            if (phase == 0 && ids.Count >= 2 && Time.time > phaseUntil)
            {
                seekers.Clear(); seekers.Add(ids[Random.Range(0, ids.Count)]);
                phase = 1; phaseUntil = Time.time + 40f;
                foreach (var id in ids) GameModes.Send(id, seekers.Contains(id) ? "seeker" : "hider", 40, seekers.Contains(id) ? SeekerRoom : HideStart);
            }
            else if (phase == 1 && Time.time > phaseUntil) { phase = 2; phaseUntil = Time.time + 180f; GameModes.Send(255, "seek", 180, HideStart); }
            else if (phase == 2)
            {
                int left = 0; foreach (var id in ids) if (!seekers.Contains(id)) left++;
                if (Time.frameCount % 20 == 0) GameModes.Send(255, "left", left);
                if (left == 0 || Time.time > phaseUntil) { GameModes.Send(255, left == 0 ? "end:SEEKERS WIN" : "end:HIDERS WIN"); phase = 0; phaseUntil = Time.time + 12f; }
            }
        }

        public override void HostRequest(byte pid, string s, long n, Vector3 v)
        {
            if (s != "catch" || phase != 2 || !seekers.Contains(pid)) return;
            byte target = (byte)n;
            if (seekers.Contains(target)) return;
            if (!NetManager.I.OWPos(pid, out var a) || !NetManager.I.OWPos(target, out var b) || Vector3.Distance(a, b) > 3.5f) return;
            seekers.Add(target);
            GameModes.Send(target, "caught", 0);
            GameModes.Send(pid, "gotone", 0);
            GameModes.Send(255, "msg:" + NetManager.I.OWName(pid) + " found " + NetManager.I.OWName(target) + "!");
        }

        public override void OnEvent(string s, long n, Vector3 v)
        {
            var p = PlayerController.Instance;
            switch (s)
            {
                case "seeker": iSeek = true; phase = 1; timeLeft = n; if (p != null) p.Teleport(v); Banner("YOU ARE THE SEEKER", "Wait " + n + " seconds...", 5f); break;
                case "hider": iSeek = false; phase = 1; timeLeft = n; if (p != null) p.Teleport(v); Banner("HIDE!", n + " seconds to find a spot", 5f); break;
                case "seek": phase = 2; timeLeft = n; if (iSeek && p != null) p.Teleport(v); Banner(iSeek ? "GO FIND THEM" : "THE SEEKER IS COMING", iSeek ? "Get close and press E" : "", 4f); break;
                case "caught": iSeek = true; Banner("YOU WERE FOUND", "Now you're a seeker too.", 4f); break;
                case "gotone": Stats.Add("seek_catches"); AudioManager.Ui("ui_success"); break;
                case "left": hidersLeft = (int)n; break;
                default:
                    if (s.StartsWith("end:")) { Banner(s.Substring(4), "Next round soon", 6f); phase = 0; iSeek = false; }
                    else if (s.StartsWith("msg:")) GameEvents.ShowToast(s.Substring(4));
                    break;
            }
        }

        protected override void DrawMode(float W, float H)
        {
            if (!NetState.Active) { if (phase == 2) Panel(new Rect(W / 2 - 260, 150, 520, 46), "Found " + soloFound + "/" + soloHidden.Count + "  ·  " + Mathf.CeilToInt(timeLeft) + "s", 22); return; }
            if (phase == 1 && iSeek) { UI.Fill(new Rect(-10, -10, W + 20, H + 20), new Color(0, 0, 0, 0.92f)); UI.Label(new Rect(0, H * 0.45f, W, 60), "COUNTING... " + Mathf.CeilToInt(timeLeft), 48, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont); }
            if (phase >= 1) Panel(new Rect(W / 2 - 260, 150, 520, 46), (iSeek ? "SEEKER" : "HIDER") + "  ·  " + Mathf.CeilToInt(timeLeft) + "s" + (phase == 2 ? "  ·  " + hidersLeft + " hiding" : ""), 22, iSeek ? UI.Bad : UI.Good);
        }
    }

    // ====================================================================== mode maps

    public static class ModeMaps
    {
        static Material T(Texture2D t) => MapBuilder.Tex(t);

        public static void ZombieTown(MapBuilder m)
        {
            m.Options.StartingNpcs = 0;
            m.Block("Square", new Vector3(-60, -1, -60), new Vector3(60, 0, 60), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.45f, 0.45f, 0.45f), 0.3f);
            var tints = new[] { new Color(0.5f, 0.42f, 0.38f), new Color(0.45f, 0.48f, 0.52f), new Color(0.55f, 0.5f, 0.42f) };
            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(0, i * 90f, 0);
                m.Building(rot * new Vector3(-30, 0, 24) , rot * new Vector3(-8, 14, 40), TextureFactory.Brick, tints[i % 3], true);
                m.Building(rot * new Vector3(8, 0, 24), rot * new Vector3(30, 18, 40), TextureFactory.Plaster, tints[(i + 1) % 3], true);
            }
            var mode = m.Root.gameObject.AddComponent<ZombieSurvival>();
            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(0, i * 90f, 0);
                mode.Gates.Add(rot * new Vector3(0, 0.1f, 40));
                for (int k = 0; k < 4; k++) PropFactory.Spawn("crate", rot * new Vector3(-5 + k * 3.2f, 0.5f, 18), rot);
                m.StreetLamp(rot * new Vector3(10, 0, 12), i * 90f, new Color(1f, 0.8f, 0.5f), 6f, 18f, 2f);
            }
            m.Cylinder("Fountain", Vector3.zero, 3.5f, 0.8f, T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.6f, 0.6f, 0.6f), 20);
            for (int k = 0; k < 4; k++) PropFactory.Spawn("explosive_barrel", Quaternion.Euler(0, k * 90 + 45, 0) * new Vector3(0, 0.5f, 9), Quaternion.identity);
            m.PlayerSpawn = new Vector3(0, 0.1f, -6f);
            m.Playable = new Bounds(new Vector3(0, 3, 0), new Vector3(90, 8, 90));
        }

        public static void Golf(MapBuilder m)
        {
            m.Options.StartingNpcs = 0;
            m.Block("Rough", new Vector3(-150, -1, -40), new Vector3(150, 0, 360), T(TextureFactory.Grass), SurfaceType.Grass, new Color(0.35f, 0.55f, 0.28f), 0.1f);
            m.Block("Fairway", new Vector3(-25, 0, 0), new Vector3(25, 0.02f, 340), T(TextureFactory.Grass), SurfaceType.Grass, new Color(0.45f, 0.75f, 0.35f), 0.1f, false);
            for (int d = 25; d <= 325; d += 25)
            {
                m.Box("Marker", new Vector3(-26, 1f, d), Quaternion.identity, new Vector3(0.3f, 2f, 0.3f), Mats.Lit, SurfaceType.Wood, d % 100 == 0 ? new Color(0.9f, 0.2f, 0.2f) : Color.white);
                m.Box("Marker", new Vector3(26, 1f, d), Quaternion.identity, new Vector3(0.3f, 2f, 0.3f), Mats.Lit, SurfaceType.Wood, d % 100 == 0 ? new Color(0.9f, 0.2f, 0.2f) : Color.white);
                m.Box("Line", new Vector3(0, 0.03f, d), Quaternion.identity, new Vector3(50f, 0.01f, 0.25f), Mats.Lit, SurfaceType.Grass, new Color(1f, 1f, 1f, 0.6f), 1f, false);
            }
            for (int i = 0; i < 5; i++) m.Tree(new Vector3(-40 - i * 7, 0, 60 + i * 55), 1.4f, i % 2 == 0);
            for (int i = 0; i < 5; i++) m.Tree(new Vector3(40 + i * 6, 0, 30 + i * 60), 1.4f, i % 2 == 1);
            // Tee: platform + cannon
            m.Block("Tee", new Vector3(-6, 0, -12), new Vector3(6, 1f, 0), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.7f, 0.7f, 0.7f));
            var cannon = new GameObject("Cannon"); cannon.layer = Layers.Props; cannon.transform.SetParent(m.Root, false); cannon.transform.position = new Vector3(0, 1f, -4f);
            var mb = new MeshBuilder { Color = new Color(0.15f, 0.15f, 0.17f), Gloss = 0.7f };
            mb.Cylinder(new Vector3(0, 1.2f, -0.8f), new Vector3(0, 2.3f, 1.4f), 0.55f, 0.45f, 16);
            mb.SetColor(GunKit.DarkWood, 0.3f); mb.Box(new Vector3(0, 0.6f, -0.3f), new Vector3(1.6f, 1.2f, 2f));
            mb.SetColor(new Color(0.4f, 0.4f, 0.42f), 0.6f); mb.Torus(new Vector3(0.85f, 0.6f, -0.3f), 0.6f, 0.08f, 16, 4, Vector3.right); mb.Torus(new Vector3(-0.85f, 0.6f, -0.3f), 0.6f, 0.08f, 16, 4, Vector3.right);
            cannon.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Cannon"); cannon.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            var bc = cannon.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 1.2f, 0); bc.size = new Vector3(1.8f, 2.4f, 3f);
            var barrel = new GameObject("Barrel").transform; barrel.SetParent(cannon.transform, false); barrel.localPosition = new Vector3(0, 2.4f, 1.5f); barrel.localRotation = Quaternion.Euler(-28f, 0, 0);
            var golf = cannon.AddComponent<GoreGolf>();
            golf.Barrel = barrel; golf.Tee = cannon.transform.position;
            for (int i = 0; i < 6; i++)
            {
                var p = new Vector3(Random.Range(-15f, 15f), Random.Range(4f, 14f), 60 + i * 42);
                float r = 4f;
                m.Box("Ring Post", new Vector3(p.x, p.y * 0.5f, p.z), Quaternion.identity, new Vector3(0.2f, p.y, 0.2f), Mats.LitMetal, SurfaceType.Metal, new Color(0.4f, 0.4f, 0.42f), 1f, false);
                var ring = new GameObject("Bonus Ring"); ring.transform.SetParent(m.Root, false); ring.transform.position = p + Vector3.up * r;
                var rmb = new MeshBuilder { Color = Color.white }; rmb.Torus(Vector3.zero, r, 0.15f, 32, 6, Vector3.forward);
                ring.AddComponent<MeshFilter>().sharedMesh = rmb.ToMesh("Ring"); ring.AddComponent<MeshRenderer>().sharedMaterial = Mats.Glow(new Color(1f, 0.85f, 0.2f), 2f);
                golf.Rings.Add((ring.transform.position, r));
            }
            m.PlayerSpawn = new Vector3(3, 1.1f, -8f);
            m.Playable = new Bounds(new Vector3(0, 3, 0), new Vector3(30, 8, 30));
        }

        public static void Tournament(MapBuilder m)
        {
            m.Options.StartingNpcs = 0;
            m.Block("Ground", new Vector3(-60, -1, -60), new Vector3(60, 0, 60), T(TextureFactory.Dirt), SurfaceType.Dirt, new Color(0.3f, 0.2f, 0.15f));
            m.Block("Pit Floor", new Vector3(-14, -3.2f, -14), new Vector3(14, -3f, 14), T(TextureFactory.Sand), SurfaceType.Dirt, new Color(0.7f, 0.55f, 0.4f));
            m.Block("Pit Wall", new Vector3(-14.5f, -3.2f, -14.5f), new Vector3(14.5f, 0, -14), T(TextureFactory.Brick), SurfaceType.Concrete, new Color(0.45f, 0.25f, 0.2f));
            m.Block("Pit Wall", new Vector3(-14.5f, -3.2f, 14), new Vector3(14.5f, 0, 14.5f), T(TextureFactory.Brick), SurfaceType.Concrete, new Color(0.45f, 0.25f, 0.2f));
            m.Block("Pit Wall", new Vector3(-14.5f, -3.2f, -14), new Vector3(-14, 0, 14), T(TextureFactory.Brick), SurfaceType.Concrete, new Color(0.45f, 0.25f, 0.2f));
            m.Block("Pit Wall", new Vector3(14, -3.2f, -14), new Vector3(14.5f, 0, 14), T(TextureFactory.Brick), SurfaceType.Concrete, new Color(0.45f, 0.25f, 0.2f));
            // The ground around the pit (leave the hole open).
            m.Block("Rim", new Vector3(-30, -0.01f, -30), new Vector3(30, 0.1f, -14.5f), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.4f, 0.4f, 0.42f));
            m.Block("Rim", new Vector3(-30, -0.01f, 14.5f), new Vector3(30, 0.1f, 30), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.4f, 0.4f, 0.42f));
            m.Block("Rim", new Vector3(-30, -0.01f, -14.5f), new Vector3(-14.5f, 0.1f, 14.5f), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.4f, 0.4f, 0.42f));
            m.Block("Rim", new Vector3(14.5f, -0.01f, -14.5f), new Vector3(30, 0.1f, 14.5f), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.4f, 0.4f, 0.42f));
            for (int i = 0; i < 4; i++) m.Stairs("Stands", Quaternion.Euler(0, i * 90, 0) * new Vector3(-10, 0.1f, 18f), i * 90f, 20f, 8, 0.5f, 0.9f, T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.35f, 0.3f, 0.3f));
            for (int i = 0; i < 8; i++) m.StreetLamp(Quaternion.Euler(0, i * 45, 0) * new Vector3(0, 0, 16f), i * 45f + 180f, new Color(1f, 0.6f, 0.3f), 7f, 24f, 2.2f);
            var t = m.Root.gameObject.AddComponent<Tournament>();
            t.SpotA = new Vector3(0, -2.9f, -9f); t.SpotB = new Vector3(0, -2.9f, 9f); t.Stands = new Vector3(0, 2.6f, 22f);
            m.PlayerSpawn = new Vector3(0, 2.6f, 22f);
            m.PlayerYaw = 180f;
            m.Playable = new Bounds(new Vector3(0, -1, 0), new Vector3(28, 6, 28));
            m.KillY = -30f;
        }

        public static void Mansion(MapBuilder m)
        {
            m.Options.StartingNpcs = 0;
            var wall = TextureFactory.Wallpaper; var wood = TextureFactory.Wood;
            Color wc = new Color(0.55f, 0.4f, 0.45f);
            m.Block("Ground", new Vector3(-50, -1, -50), new Vector3(50, 0, 50), T(TextureFactory.Grass), SurfaceType.Grass, new Color(0.3f, 0.45f, 0.25f));
            m.Block("Floor", new Vector3(-20, 0, -15), new Vector3(20, 0.1f, 15), T(wood), SurfaceType.Wood, new Color(0.5f, 0.35f, 0.22f));
            m.Block("Upper Floor", new Vector3(-20, 4f, -15), new Vector3(20, 4.2f, 15), T(wood), SurfaceType.Wood, new Color(0.5f, 0.35f, 0.22f));
            m.Block("Roof", new Vector3(-20.3f, 8f, -15.3f), new Vector3(20.3f, 8.3f, 15.3f), T(TextureFactory.Ceiling), SurfaceType.Concrete, Color.white);
            // Outer walls with a front door
            m.Block("Wall", new Vector3(-20.3f, 0, -15.3f), new Vector3(-2, 8, -15), T(wall), SurfaceType.Concrete, wc);
            m.Block("Wall", new Vector3(2, 0, -15.3f), new Vector3(20.3f, 8, -15), T(wall), SurfaceType.Concrete, wc);
            m.Block("Wall", new Vector3(-2, 2.8f, -15.3f), new Vector3(2, 8, -15), T(wall), SurfaceType.Concrete, wc);
            m.Block("Wall", new Vector3(-20.3f, 0, 15), new Vector3(20.3f, 8, 15.3f), T(wall), SurfaceType.Concrete, wc);
            m.Block("Wall", new Vector3(-20.3f, 0, -15), new Vector3(-20, 8, 15), T(wall), SurfaceType.Concrete, wc);
            m.Block("Wall", new Vector3(20, 0, -15), new Vector3(20.3f, 8, 15), T(wall), SurfaceType.Concrete, wc);
            // Grid of rooms on both floors (doorways in every wall segment)
            for (int fl = 0; fl < 2; fl++)
            {
                float y = fl * 4.2f;
                for (int i = -1; i <= 1; i++)
                {
                    float x = i * 7f;
                    m.Block("Room Wall", new Vector3(x - 0.1f, y, -15), new Vector3(x + 0.1f, y + 3.8f, -3f), T(wall), SurfaceType.Concrete, wc);
                    m.Block("Room Wall", new Vector3(x - 0.1f, y, 3f), new Vector3(x + 0.1f, y + 3.8f, 15), T(wall), SurfaceType.Concrete, wc);
                }
                for (int i = -3; i < 3; i++)
                {
                    float x0 = i * 7f + 1.2f;
                    m.Block("Room Front", new Vector3(x0, y, -3.1f), new Vector3(x0 + 4.2f, y + 3.8f, -2.9f), T(wall), SurfaceType.Concrete, wc);
                    m.Block("Room Front", new Vector3(x0, y, 2.9f), new Vector3(x0 + 4.2f, y + 3.8f, 3.1f), T(wall), SurfaceType.Concrete, wc);
                    // Closets + furniture to hide in / behind
                    m.Box("Wardrobe", new Vector3(x0 + 3.5f, y + 1.1f, -14f), Quaternion.identity, new Vector3(1.4f, 2.2f, 0.7f), T(wood), SurfaceType.Wood, new Color(0.35f, 0.22f, 0.13f));
                    m.Box("Bed", new Vector3(x0 + 1f, y + 0.35f, 13f), Quaternion.identity, new Vector3(1.8f, 0.5f, 2.2f), T(TextureFactory.Carpet), SurfaceType.Carpet, new Color(0.6f, 0.15f, 0.2f));
                    m.CeilingLight(new Vector3(x0 + 2f, y + 3.6f, -9f), new Color(1f, 0.85f, 0.6f), 8f, 1f);
                    m.CeilingLight(new Vector3(x0 + 2f, y + 3.6f, 9f), new Color(1f, 0.85f, 0.6f), 8f, 1f);
                }
            }
            m.Stairs("Grand Stairs", new Vector3(-2f, 0.1f, -2.5f), 90f, 2.2f, 14, 0.29f, 0.45f, T(wood), SurfaceType.Wood, new Color(0.45f, 0.3f, 0.18f));
            var hs = m.Root.gameObject.AddComponent<HideSeek>();
            hs.SeekerRoom = new Vector3(0, 0.15f, -25f);
            m.Block("Seeker Booth", new Vector3(-2, 0, -27), new Vector3(2, 0.1f, -23), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.3f, 0.3f, 0.32f));
            hs.HideStart = new Vector3(0, 0.2f, 0);
            for (int fl = 0; fl < 2; fl++) for (int i = -3; i < 3; i++)
            {
                float y = fl * 4.2f + 0.2f, x0 = i * 7f + 1.2f;
                hs.HidingSpots.Add(new Vector3(x0 + 3.5f, y, -12.8f)); hs.HidingSpots.Add(new Vector3(x0 + 3f, y, 13.6f)); hs.HidingSpots.Add(new Vector3(x0 + 0.6f, y, -6f));
            }
            m.PlayerSpawn = new Vector3(0, 0.2f, -20f);
            m.Playable = new Bounds(new Vector3(0, 4, 0), new Vector3(40, 8, 30));
        }
    }

    // ====================================================================== daily challenges

    public static class DailyChallenges
    {
        public sealed class Challenge { public string Id, Text, Stat; public float Target; public long Reward; }

        static readonly Challenge[] Pool =
        {
            new Challenge { Id = "kills", Text = "Kill {0} Goredolls", Stat = Stats.Kills, Target = 30, Reward = 5000 },
            new Challenge { Id = "limbs", Text = "Sever {0} limbs", Stat = Stats.Limbs, Target = 25, Reward = 6000 },
            new Challenge { Id = "decaps", Text = "Decapitate {0} Goredolls", Stat = Stats.Decaps, Target = 8, Reward = 7000 },
            new Challenge { Id = "heads", Text = "Land {0} headshots", Stat = Stats.Headshots, Target = 20, Reward = 6000 },
            new Challenge { Id = "gibs", Text = "Gib {0} body parts", Stat = Stats.Gibs, Target = 15, Reward = 6000 },
            new Challenge { Id = "maps", Text = "Play {0} maps", Stat = "maps_played", Target = 3, Reward = 4000 },
            new Challenge { Id = "shots", Text = "Fire {0} shots", Stat = Stats.ShotsFired, Target = 500, Reward = 3000 },
            new Challenge { Id = "blood", Text = "Spill {0} ml of blood", Stat = Stats.BloodMl, Target = 25000, Reward = 5000 },
            new Challenge { Id = "zombies", Text = "Kill {0} zombies in Zombie Survival", Stat = "zs_kills", Target = 40, Reward = 9000 },
            new Challenge { Id = "golf", Text = "Take {0} Gore Golf shots", Stat = "golf_shots", Target = 5, Reward = 5000 },
            new Challenge { Id = "duels", Text = "Win {0} Tournament duels", Stat = "duel_wins", Target = 3, Reward = 9000 },
            new Challenge { Id = "heists", Text = "Finish {0} Open World heist", Stat = OWDirector.StatHeists, Target = 1, Reward = 10000 },
        };

        static string Day => DateTime.UtcNow.ToString("yyyyMMdd");

        public static List<Challenge> Today()
        {
            var list = new List<Challenge>();
            var rng = new System.Random(Day.GetHashCode());
            var pool = new List<Challenge>(Pool);
            for (int i = 0; i < 3; i++) { int k = rng.Next(pool.Count); list.Add(pool[k]); pool.RemoveAt(k); }
            foreach (var c in list) if (!PlayerPrefs.HasKey(Key(c, "base"))) PlayerPrefs.SetFloat(Key(c, "base"), Stats.Get(c.Stat));
            return list;
        }

        static string Key(Challenge c, string what) => "daily_" + Day + "_" + c.Id + "_" + what;
        public static float Progress(Challenge c) => Mathf.Clamp(Stats.Get(c.Stat) - PlayerPrefs.GetFloat(Key(c, "base"), Stats.Get(c.Stat)), 0f, c.Target);
        public static bool Done(Challenge c) => PlayerPrefs.GetInt(Key(c, "done"), 0) == 1;
        public static string Text(Challenge c) => string.Format(c.Text, c.Target.ToString("N0"));

        public static void Check()
        {
            foreach (var c in Today())
            {
                if (Done(c) || Progress(c) < c.Target) continue;
                PlayerPrefs.SetInt(Key(c, "done"), 1);
                PlayerPrefs.Save();
                Stats.Add("daily_done");
                OWSave.Earn(c.Reward, "Daily challenge");
                Toasts.Show("DAILY CHALLENGE COMPLETE", Text(c) + "  ·  +$" + c.Reward.ToString("N0") + " Open World cash", UI.Good);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("Daily Challenges");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<DailyTicker>();
        }
    }

    public sealed class DailyTicker : MonoBehaviour
    {
        float next;
        void Update() { if (Time.unscaledTime < next) return; next = Time.unscaledTime + 3f; try { DailyChallenges.Check(); } catch (Exception) { } }
    }

    public sealed class DailyScreen : UIScreen
    {
        public override bool IsModal => true;
        public override void OnOpen() => AudioManager.Ui("ui_open");
        public override void Draw()
        {
            UI.Dim(0.7f);
            var r = UI.Centered(Mathf.Min(1000, UI.VW - 60), 640);
            UI.Panel(r, "Daily Challenges");
            UI.Label(new Rect(r.x + 300, r.y + 20, r.width - 330, 40), "New challenges every day (UTC). Rewards are Open World cash.", 16, UI.Muted, TextAnchor.MiddleRight);
            var list = DailyChallenges.Today();
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                var cr = new Rect(r.x + 30, r.y + 90 + i * 140, r.width - 60, 124);
                bool done = DailyChallenges.Done(c);
                UI.Rounded(cr, done ? new Color(0.1f, 0.35f, 0.15f, 0.9f) : UI.PanelLight, true);
                UI.Label(new Rect(cr.x + 20, cr.y + 10, cr.width - 260, 44), DailyChallenges.Text(c), 26, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                UI.Label(new Rect(cr.xMax - 240, cr.y + 10, 220, 44), "+$" + c.Reward.ToString("N0"), 24, new Color(0.5f, 1f, 0.55f), TextAnchor.MiddleRight, FontStyle.Bold);
                float p = DailyChallenges.Progress(c) / c.Target;
                var bar = new Rect(cr.x + 20, cr.y + 72, cr.width - 40, 16);
                UI.Fill(bar, new Color(1, 1, 1, 0.1f));
                UI.Fill(new Rect(bar.x, bar.y, bar.width * p, bar.height), done ? UI.Good : UI.Accent2);
                UI.Label(new Rect(cr.x + 20, cr.y + 90, cr.width - 40, 30), done ? "COMPLETE" : Mathf.FloorToInt(DailyChallenges.Progress(c)).ToString("N0") + " / " + c.Target.ToString("N0"), 16, done ? UI.Good : UI.Muted);
            }
            if (UI.Button(new Rect(r.x + 30, r.yMax - 80, 220, 58), "BACK", ButtonKind.Normal, IconFactory.Get("arrow_left"))) OnBack();
        }
    }

    /// <summary>PLAY MODES (main menu): pick a mode, play it alone or host it.</summary>
    public sealed class ModesScreen : UIScreen
    {
        public override bool IsModal => true;
        public override void OnOpen() => AudioManager.Ui("ui_open");
        public override void Draw()
        {
            UI.Dim(0.75f);
            var r = UI.Centered(Mathf.Min(1300, UI.VW - 40), Mathf.Min(860, UI.VH - 30));
            UI.Panel(r, "Game Modes");
            int i = 0;
            foreach (var def in GameModes.Defs())
            {
                var d = MapCatalog.All.Find(x => x.Id == def.Id) ?? def;
                var cr = new Rect(r.x + 30, r.y + 86 + i * 168, r.width - 60, 154);
                UI.Gradient(cr, d.ThemeTop, d.ThemeBottom);
                UI.Label(new Rect(cr.x + 24, cr.y + 12, cr.width - 520, 50), d.Name.ToUpperInvariant(), 34, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
                UI.Label(new Rect(cr.x + 24, cr.y + 62, cr.width - 520, 84), d.Description, 17, Color.white, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                if (UI.Button(new Rect(cr.xMax - 470, cr.y + 44, 210, 66), "PLAY", ButtonKind.Primary, IconFactory.Get("play"), 24)) GameManager.I.StartMap(d, new MapOptions { Time = d.DefaultTime, StartingNpcs = 0, Loadout = 0 });
                if (UI.Button(new Rect(cr.xMax - 245, cr.y + 44, 220, 66), "HOST", ButtonKind.Normal, IconFactory.Get("globe"), 22, true, "Host it for friends (Multiplayer settings are used)."))
                {
                    var s = NetManager.I.Server;
                    if (string.IsNullOrWhiteSpace(s.Name) || s.Name == "Gore Sandbox Server") s.Name = Settings.Data.playerName + "'s " + d.Name;
                    if (d.Id == "tournament") s.PvP = true;
                    if (!NetManager.I.StartHost(s, d, new MapOptions { Time = d.DefaultTime, StartingNpcs = 0, Loadout = 0 })) UIRoot.Push(new MessageDialog("COULDN'T HOST", NetManager.I.Status));
                }
                i++;
            }
            if (UI.Button(new Rect(r.x + 30, r.yMax - 80, 220, 58), "BACK", ButtonKind.Normal, IconFactory.Get("arrow_left"))) OnBack();
        }
    }
}
