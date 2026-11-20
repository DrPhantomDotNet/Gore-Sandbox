using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 game modes (Main menu > MODES): Gun Game, Ragdoll Olympics, Doctor Mode, Trap Gauntlet,
    /// Assassin and King of the Hill. They follow the GameModes pattern: a map built in code with a ModeBase on its
    /// root. Built for singleplayer first; the host runs them in multiplayer and the score HUD is local.
    /// </summary>
    public static class GameModes2
    {
        static Material T(Texture2D t) => MapBuilder.Tex(t);
        static MapDef Mode(string id, string name, string size, string glyph, TimeOfDay time, string desc, string[] features, Color top, Color bottom, Color accent, System.Action<MapBuilder> build, bool indoor = false)
            => new MapDef { Id = id, Name = name, Size = size, Glyph = glyph, DefaultTime = time, Indoor = indoor, Ambience = indoor ? "amb_hum" : "amb_wind", Description = desc, Features = features, Tags = new[] { "Game Mode" }, ThemeTop = top, ThemeBottom = bottom, Accent = accent, Build = build };

        public static IEnumerable<MapDef> Defs()
        {
            yield return Mode("gun_game", "Gun Game", "Mode - warehouse arena", "target", TimeOfDay.Night,
                "Every kill swaps your gun for the next one on the ladder: pistol, SMG, shotgun, rifles, sniper, rocket launcher... and the last kill has to be with a knife.",
                new[] { "8 weapon ladder", "Armed Goredolls", "Best time saved" }, new Color(0.1f, 0.1f, 0.12f), new Color(0.6f, 0.4f, 0.2f), new Color(1f, 0.6f, 0.2f), BuildArena);
            yield return Mode("olympics", "Ragdoll Olympics", "Mode - stadium field", "trophy", TimeOfDay.Day,
                "A Goredoll stands on the launch pad. Send it as far as you can: kick it, throw it, blow it up. Distance is measured where it lands. 5 attempts.",
                new[] { "Launch pad", "Distance markers", "Best distance saved" }, new Color(0.3f, 0.6f, 0.95f), new Color(0.35f, 0.6f, 0.3f), new Color(1f, 0.9f, 0.3f), BuildField);
            yield return Mode("doctor", "Doctor Mode", "Mode - emergency room", "heart", TimeOfDay.Night,
                "Wounded patients are rushed in. Press E on a patient to treat them: pressure, tourniquets, chest seals, transfusions, the defibrillator. Keep them alive for 60 seconds.",
                new[] { "Medical kit", "Bleeding patients", "Patients saved" }, new Color(0.08f, 0.15f, 0.15f), new Color(0.6f, 0.8f, 0.8f), new Color(0.4f, 1f, 0.6f), BuildER, true);
            yield return Mode("gauntlet", "Trap Gauntlet", "Mode - 90 m death corridor", "warning", TimeOfDay.Night,
                "Run the corridor of pendulums, blade pillars, rail saws, spike pits and flame jets. Reach the end alive. Best time saved.",
                new[] { "12 traps", "Timer", "Best time saved" }, new Color(0.15f, 0.05f, 0.05f), new Color(0.5f, 0.35f, 0.2f), new Color(1f, 0.3f, 0.2f), BuildGauntlet);
            yield return Mode("assassin", "Assassin", "Mode - crowded plaza", "eye", TimeOfDay.Sunset,
                "Your target is somewhere in the crowd. Read the description, find them, take them out. Every innocent you kill costs you.",
                new[] { "Crowd of 20", "One target", "Score streak" }, new Color(0.35f, 0.2f, 0.25f), new Color(0.8f, 0.5f, 0.3f), new Color(0.9f, 0.2f, 0.2f), BuildPlaza);
            yield return Mode("koth", "King of the Hill", "Mode - hill arena", "flag", TimeOfDay.BloodMoon,
                "Stand on the hill. Angry Goredolls keep coming to knock you off it. Every second on top scores. 2 minute rounds.",
                new[] { "Hill zone", "Endless brawlers", "Best score saved" }, new Color(0.25f, 0.02f, 0.02f), new Color(0.5f, 0.3f, 0.15f), new Color(1f, 0.85f, 0.3f), BuildHill);
        }

        // ------------------------------------------------------------------ maps

        static void Floor(MapBuilder m, float half, Texture2D tex, Color c) => m.Block("Floor", new Vector3(-half, -1, -half), new Vector3(half, 0, half), T(tex), SurfaceType.Concrete, c, 0.3f);
        static void Walls(MapBuilder m, float half, float h, Texture2D tex, Color c)
        {
            m.Block("Wall", new Vector3(-half, 0, half), new Vector3(half, h, half + 0.4f), T(tex), SurfaceType.Concrete, c);
            m.Block("Wall", new Vector3(-half, 0, -half - 0.4f), new Vector3(half, h, -half), T(tex), SurfaceType.Concrete, c);
            m.Block("Wall", new Vector3(half, 0, -half), new Vector3(half + 0.4f, h, half), T(tex), SurfaceType.Concrete, c);
            m.Block("Wall", new Vector3(-half - 0.4f, 0, -half), new Vector3(-half, h, half), T(tex), SurfaceType.Concrete, c);
        }

        static void BuildArena(MapBuilder m)
        {
            m.Options.StartingNpcs = 0;
            Floor(m, 30, TextureFactory.Concrete, new Color(0.4f, 0.4f, 0.42f));
            Walls(m, 30, 9, TextureFactory.Metal, new Color(0.45f, 0.47f, 0.5f));
            for (int i = 0; i < 18; i++)
            {
                var p = new Vector3(Random.Range(-24f, 24f), 0, Random.Range(-24f, 24f));
                if (p.magnitude < 6f) continue;
                m.Box("Cover", p + Vector3.up * 0.75f, Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0), new Vector3(Random.Range(1.5f, 3.5f), 1.5f, 1.2f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.55f, 0.4f, 0.25f));
            }
            for (int i = 0; i < 4; i++) m.CeilingLight(new Vector3(-15 + i * 10, 8.5f, 0), new Color(1f, 0.9f, 0.75f), 22f, 1.8f);
            m.Root.gameObject.AddComponent<GunGame>();
            m.PlayerSpawn = new Vector3(0, 0.1f, -2f);
            for (int i = 0; i < 8; i++) m.NpcSpawns.Add(Quaternion.Euler(0, i * 45f, 0) * new Vector3(0, 0.1f, 24f));
            m.Playable = new Bounds(new Vector3(0, 4, 0), new Vector3(60, 10, 60));
        }

        static void BuildField(MapBuilder m)
        {
            m.Options.StartingNpcs = 0;
            m.Block("Field", new Vector3(-80, -1, -30), new Vector3(80, 0, 260), T(TextureFactory.Grass), SurfaceType.Grass, new Color(0.38f, 0.62f, 0.3f), 0.1f);
            m.Cylinder("Launch Pad", new Vector3(0, 0, 0), 2f, 0.3f, Mats.LitMetal, SurfaceType.Metal, new Color(0.9f, 0.75f, 0.15f), 24);
            for (int d = 10; d <= 250; d += 10)
            {
                bool big = d % 50 == 0;
                m.Box("Line", new Vector3(0, 0.02f, d), Quaternion.identity, new Vector3(big ? 60f : 40f, 0.02f, big ? 0.4f : 0.15f), Mats.Lit, SurfaceType.Grass, Color.white, 1f, false);
            }
            for (int i = 0; i < 6; i++) m.StreetLamp(new Vector3(i % 2 == 0 ? -35 : 35, 0, 20 + i * 40), i % 2 == 0 ? 90f : -90f, new Color(1f, 0.95f, 0.85f), 8f, 30f, 2f);
            m.Root.gameObject.AddComponent<Olympics>();
            m.PlayerSpawn = new Vector3(0, 0.1f, -6f);
            m.Playable = new Bounds(new Vector3(0, 10, 110), new Vector3(160, 40, 300));
        }

        static void BuildER(MapBuilder m)
        {
            m.Options.StartingNpcs = 0;
            Floor(m, 12, TextureFactory.Tiles, new Color(0.8f, 0.85f, 0.83f));
            Walls(m, 12, 4.5f, TextureFactory.Tiles, new Color(0.75f, 0.85f, 0.82f));
            m.Block("Ceiling", new Vector3(-12, 4.5f, -12), new Vector3(12, 4.8f, 12), T(TextureFactory.Tiles), SurfaceType.Concrete, new Color(0.7f, 0.72f, 0.72f));
            var er = m.Root.gameObject.AddComponent<DoctorMode>();
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(-7.5f + i * 5f, 0, 5f);
                HospitalBed.Create(p, Quaternion.identity).transform.SetParent(m.Root, true);
                er.Beds.Add(p + new Vector3(0, 0.95f, 0));
                m.CeilingLight(p + new Vector3(0, 4.3f, 0), new Color(0.9f, 1f, 0.97f), 8f, 1.5f);
            }
            m.PlayerSpawn = new Vector3(0, 0.1f, -6f);
            m.Playable = new Bounds(new Vector3(0, 2.5f, 0), new Vector3(24, 6, 24));
        }

        static void BuildGauntlet(MapBuilder m)
        {
            m.Options.StartingNpcs = 0;
            m.Block("Floor", new Vector3(-4, -1, -6), new Vector3(4, 0, 96), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.35f, 0.33f, 0.32f), 0.3f);
            m.Block("Wall", new Vector3(-4.4f, 0, -6), new Vector3(-4, 7, 96), T(TextureFactory.Brick), SurfaceType.Concrete, new Color(0.4f, 0.28f, 0.25f));
            m.Block("Wall", new Vector3(4, 0, -6), new Vector3(4.4f, 7, 96), T(TextureFactory.Brick), SurfaceType.Concrete, new Color(0.4f, 0.28f, 0.25f));
            m.Block("Finish", new Vector3(-4, 0, 92), new Vector3(4, 0.05f, 96), Mats.Glow(new Color(0.2f, 1f, 0.3f), 1.5f), SurfaceType.Concrete, null, 1f, false);
            var traps = new System.Func<Vector3, Quaternion, GameObject>[] { Pendulum.Create, BladePillar.Create, RailSaw.Create, FlameJet.Create, SpikePit.Create, Pendulum.Create, ElectricFloor.Create, BladePillar.Create, FlameJet.Create, RailSaw.Create, Pendulum.Create, LaserGrid.Create };
            for (int i = 0; i < traps.Length; i++)
            {
                var rot = traps[i] == RailSaw.Create || traps[i] == LaserGrid.Create ? Quaternion.Euler(0, 0, 0) : Quaternion.Euler(0, 90, 0);
                var go = traps[i](new Vector3(i % 2 == 0 ? -0.5f : 0.5f, 0, 6f + i * 7f), rot);
                if (go != null) go.transform.SetParent(m.Root, true);
            }
            for (int i = 0; i < 9; i++) m.CeilingLight(new Vector3(0, 6.7f, i * 11f), new Color(1f, 0.55f, 0.4f), 14f, 1.4f);
            m.Root.gameObject.AddComponent<Gauntlet>();
            m.PlayerSpawn = new Vector3(0, 0.1f, -3f);
            m.Playable = new Bounds(new Vector3(0, 4, 45), new Vector3(9, 10, 104));
        }

        static void BuildPlaza(MapBuilder m)
        {
            m.Options.StartingNpcs = 0;
            Floor(m, 35, TextureFactory.Tiles, new Color(0.7f, 0.65f, 0.6f));
            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(0, i * 90f, 0);
                m.Building(rot * new Vector3(-30, 0, 28), rot * new Vector3(30, 16, 40), TextureFactory.Plaster, new Color(0.7f, 0.6f, 0.5f), true);
            }
            m.Cylinder("Fountain", Vector3.zero, 3f, 0.9f, T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.7f, 0.7f, 0.72f), 20);
            for (int i = 0; i < 8; i++) PropFactory.Spawn("chair", Quaternion.Euler(0, i * 45f, 0) * new Vector3(0, 0.4f, 6f), Quaternion.identity);
            m.Root.gameObject.AddComponent<Assassin>();
            m.PlayerSpawn = new Vector3(0, 0.1f, -20f);
            m.Playable = new Bounds(new Vector3(0, 4, 0), new Vector3(60, 10, 60));
        }

        static void BuildHill(MapBuilder m)
        {
            m.Options.StartingNpcs = 0;
            Floor(m, 30, TextureFactory.Dirt, new Color(0.35f, 0.25f, 0.18f));
            Walls(m, 30, 4, TextureFactory.Brick, new Color(0.35f, 0.22f, 0.2f));
            m.Cylinder("Hill", Vector3.zero, 6f, 1.2f, T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.55f, 0.5f, 0.45f), 24);
            m.Cylinder("Ring", new Vector3(0, 1.2f, 0), 4.5f, 0.02f, Mats.Glow(new Color(1f, 0.8f, 0.2f), 1.2f), SurfaceType.Concrete, null, 24, false);
            for (int k = 0; k < 4; k++) m.Box("Ramp", Quaternion.Euler(0, k * 90f, 0) * new Vector3(0, 0.55f, 7f), Quaternion.Euler(0, k * 90f, 0) * Quaternion.Euler(-12f, 0, 0), new Vector3(3f, 0.3f, 4f), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.5f, 0.46f, 0.42f));
            m.Root.gameObject.AddComponent<KingOfHill>();
            m.PlayerSpawn = new Vector3(0, 1.4f, 0f);
            for (int i = 0; i < 8; i++) m.NpcSpawns.Add(Quaternion.Euler(0, i * 45f, 0) * new Vector3(0, 0.1f, 25f));
            m.Playable = new Bounds(new Vector3(0, 3, 0), new Vector3(60, 8, 60));
        }

        internal static bool ByMe(object attacker) => attacker is PlayerController || attacker is Weapon || (NetState.Active && OWDirector.PlayerOf(attacker) == OWDirector.MyId);

        internal static Vector3 SpawnPoint(Vector3 fallback)
        {
            var w = WorldRoot.Current;
            var p = PlayerController.Instance;
            if (NavGrid.Current != null && p != null && NavGrid.Current.RandomPoint(p.transform.position, 26f, out var at) && (at - p.transform.position).magnitude > 10f) return at;
            return fallback;
        }
    }

    // ====================================================================== Gun Game

    public sealed class GunGame : ModeBase
    {
        static readonly string[] Ladder = { "glock", "mp5", "shotgun", "ak47", "m4a1", "awp", "rpg", "knife" };
        int level; float start, nextSpawn; bool won; string current;

        void Start() { Banner("GUN GAME", "Every kill: next gun. Last one is the knife.", 5f); start = Time.time; }

        void Update()
        {
            var p = PlayerController.Instance;
            if (p != null && current != Ladder[Mathf.Min(level, Ladder.Length - 1)] && !won)
            {
                current = Ladder[Mathf.Min(level, Ladder.Length - 1)];
                p.Inventory.Strip();
                p.Inventory.Give(current, 0, true);
            }
            if (!Host || won || Time.time < nextSpawn) return;
            nextSpawn = Time.time + 2.5f;
            int alive = 0;
            foreach (var d in Goredoll.All) if (d != null && !d.IsDead) alive++;
            if (alive >= 6) return;
            var at = GameModes2.SpawnPoint(new Vector3(Random.Range(-20f, 20f), 0.1f, 22f));
            GoredollFactory.Spawn(at, Random.Range(0f, 360f), Random.value < 0.45f ? "Gunman" : "Aggressive");
        }

        protected override void OnDollDied(Goredoll d, object attacker)
        {
            if (won || !GameModes2.ByMe(attacker)) return;
            if (level == Ladder.Length - 1) { won = true; float t = Time.time - start; Banner("YOU WIN", "Gun Game in " + t.ToString("0.0") + " s", 8f); if (t < PlayerPrefs.GetFloat("gg_best", 9999f)) PlayerPrefs.SetFloat("gg_best", t); AudioManager.Ui("ui_success"); return; }
            level++;
            Banner(Ladder[level].ToUpperInvariant(), (Ladder.Length - level) + " kills to go", 1.5f);
            AudioManager.Ui("ui_tab");
        }

        protected override void DrawMode(float W, float H)
        {
            Panel(new Rect(W / 2 - 220, 20, 440, 50), "LEVEL " + (level + 1) + "/" + Ladder.Length + "   " + Ladder[Mathf.Min(level, Ladder.Length - 1)].ToUpperInvariant() + "   " + (won ? "DONE" : (Time.time - start).ToString("0") + " s"));
            float best = PlayerPrefs.GetFloat("gg_best", 0f);
            if (best > 0f && best < 9000f) Panel(new Rect(W / 2 - 160, 76, 320, 36), "BEST " + best.ToString("0.0") + " s", 17, UI.Accent2);
        }
    }

    // ====================================================================== Ragdoll Olympics

    public sealed class Olympics : ModeBase
    {
        Goredoll current; bool launched; float settleT, best, nextSpawn; int attempts; readonly List<float> scores = new List<float>();

        void Start() { Banner("RAGDOLL OLYMPICS", "Send the Goredoll as far as you can. 5 attempts.", 5f); best = PlayerPrefs.GetFloat("olympics_best", 0f); }

        bool given;
        void Update()
        {
            if (!given && PlayerController.Instance != null) { given = true; Give("rpg", "sledgehammer", "boxing"); Rules.SuperKick = true; }
            if (!Host) return;
            if (current == null || !current)
            {
                if (attempts >= 5 || Time.time < nextSpawn) return;
                current = GoredollFactory.Spawn(new Vector3(0, 0.35f, 0), 180f, "Passive");
                if (current != null && current.Brain != null) current.Brain.enabled = false;
                launched = false; settleT = 0f;
                return;
            }
            var pos = current.Position;
            float flat = new Vector2(pos.x, pos.z).magnitude;
            if (!launched && flat > 2.5f) launched = true;
            if (!launched) return;
            float speed = current.Parts.TryGetValue(PartType.Pelvis, out var pv) && pv != null && pv.Body != null ? RB.Vel(pv.Body).magnitude : 0f;
            settleT = speed < 0.6f ? settleT + Time.deltaTime : 0f;
            if (settleT > 1.2f)
            {
                float dist = Mathf.Max(0f, pos.z);
                scores.Add(dist); attempts++;
                if (dist > best) { best = dist; PlayerPrefs.SetFloat("olympics_best", best); Banner("NEW RECORD!", dist.ToString("0.0") + " m", 4f); AudioManager.Ui("ui_success"); }
                else Banner(dist.ToString("0.0") + " m", "Attempt " + attempts + " of 5", 3f);
                current.Despawn(); current = null; nextSpawn = Time.time + 2f;
                if (attempts >= 5) Banner("FINAL: " + Max().ToString("0.0") + " m", "Best ever " + best.ToString("0.0") + " m. Restart the map to go again.", 8f);
            }
        }

        float Max() { float m = 0f; foreach (var s in scores) m = Mathf.Max(m, s); return m; }

        protected override void DrawMode(float W, float H)
        {
            Panel(new Rect(W / 2 - 230, 20, 460, 50), "ATTEMPT " + Mathf.Min(attempts + 1, 5) + "/5   BEST TODAY " + Max().ToString("0.0") + " m   RECORD " + best.ToString("0.0") + " m");
        }
    }

    // ====================================================================== Doctor Mode

    public sealed class DoctorMode : ModeBase
    {
        public readonly List<Vector3> Beds = new List<Vector3>();
        sealed class Patient { public Goredoll Doll; public float Since; }
        readonly List<Patient> patients = new List<Patient>();
        int saved, lost; float nextPatient;

        void Start() { Banner("DOCTOR MODE", "Press E on a patient to treat them. Keep them alive for 60 seconds.", 6f); nextPatient = Time.time + 4f; }

        void Update()
        {
            if (!Host) return;
            for (int i = patients.Count - 1; i >= 0; i--)
            {
                var pt = patients[i];
                if (pt.Doll == null || pt.Doll.IsDead) { lost++; Banner("PATIENT LOST", lost + " lost", 2.5f); AudioManager.Ui("ui_error"); if (pt.Doll != null) pt.Doll.StartCoroutine(Remove(pt.Doll, 6f)); patients.RemoveAt(i); continue; }
                if (Time.time - pt.Since > 60f && pt.Doll.TotalBleed < 2f) { saved++; Banner("PATIENT SAVED", saved + " saved", 2.5f); AudioManager.Ui("ui_success"); pt.Doll.Despawn(); patients.RemoveAt(i); }
            }
            if (Time.time >= nextPatient && patients.Count < Beds.Count)
            {
                nextPatient = Time.time + Random.Range(18f, 30f);
                var used = new HashSet<int>();
                foreach (var pt in patients) for (int b = 0; b < Beds.Count; b++) if ((pt.Doll.Position - Beds[b]).sqrMagnitude < 2f) used.Add(b);
                int bed = -1; for (int b = 0; b < Beds.Count; b++) if (!used.Contains(b)) { bed = b; break; }
                if (bed < 0) return;
                var d = GoredollFactory.Spawn(Beds[bed] + Vector3.up * 0.1f, 0f, "Passive");
                if (d == null) return;
                if (d.Brain != null) d.Brain.enabled = false;
                d.Ragdoll.Fall(999f);
                Injure(d);
                patients.Add(new Patient { Doll = d, Since = Time.time });
                Banner("INCOMING", d.DisplayName + " - " + (d.Wounds.Count) + " wounds", 3f);
                AudioManager.Play2D("siren", 0.4f, 1f, AudioBus.Sfx);
            }
        }

        static System.Collections.IEnumerator Remove(Goredoll d, float t) { yield return new WaitForSeconds(t); if (d != null) d.Despawn(); }

        static void Injure(Goredoll d)
        {
            var parts = new[] { PartType.ThighL, PartType.ThighR, PartType.UpperArmL, PartType.UpperArmR, PartType.Abdomen, PartType.Chest, PartType.ForearmL, PartType.ShinR };
            int n = Random.Range(2, 4);
            for (int i = 0; i < n; i++)
            {
                if (!d.Parts.TryGetValue(parts[Random.Range(0, parts.Length)], out var bp) || bp == null) continue;
                var dir = Random.onUnitSphere;
                bp.TakeDamage(new DamageInfo { Amount = Random.Range(18f, 32f), Type = Random.value < 0.6f ? DamageType.Bullet : DamageType.Slash, Caliber = 9f, Point = bp.transform.position, Direction = dir, Normal = -dir, Weapon = "Street violence" });
            }
        }

        protected override void DrawMode(float W, float H)
        {
            Panel(new Rect(W / 2 - 200, 20, 400, 50), "SAVED " + saved + "    LOST " + lost + "    PATIENTS " + patients.Count);
            float y = 80;
            foreach (var pt in patients)
            {
                if (pt.Doll == null) continue;
                var v = pt.Doll.Vitals;
                Panel(new Rect(20, y, 460, 40), pt.Doll.DisplayName + "  " + Mathf.RoundToInt(v.HeartRate) + " bpm  SpO2 " + Mathf.RoundToInt(v.SpO2) + "%  blood -" + Mathf.RoundToInt(v.BloodLossFraction * 100) + "%  " + Mathf.Max(0, 60 - (int)(Time.time - pt.Since)) + " s", 16, v.BloodLossFraction > 0.3f ? UI.Bad : Color.white);
                y += 46;
            }
        }
    }

    // ====================================================================== Trap Gauntlet

    public sealed class Gauntlet : ModeBase
    {
        float start = -1f, best; bool done; int deaths;

        void Start() { Banner("TRAP GAUNTLET", "Reach the green floor at the end. Don't stop.", 5f); best = PlayerPrefs.GetFloat("gauntlet_best", 0f); }

        void Update()
        {
            var p = PlayerController.Instance;
            if (p == null) return;
            var z = p.transform.position.z;
            if (p.IsDead) { if (start >= 0f) { deaths++; start = -1f; } return; }
            if (start < 0f && z > -1f && !done) start = Time.time;
            if (start >= 0f && !done && z > 92f)
            {
                done = true;
                float t = Time.time - start;
                if (best <= 0f || t < best) { best = t; PlayerPrefs.SetFloat("gauntlet_best", t); Banner("NEW RECORD", t.ToString("0.00") + " s", 6f); }
                else Banner("FINISHED", t.ToString("0.00") + " s", 6f);
                AudioManager.Ui("ui_success");
            }
            if (done && z < -1f) { done = false; start = -1f; }
        }

        protected override void DrawMode(float W, float H)
        {
            string t = done ? "DONE" : start >= 0f ? (Time.time - start).ToString("0.00") + " s" : "GO!";
            Panel(new Rect(W / 2 - 210, 20, 420, 50), t + "    DEATHS " + deaths + "    BEST " + (best > 0f ? best.ToString("0.00") + " s" : "-"));
        }
    }

    // ====================================================================== Assassin

    public sealed class Assassin : ModeBase
    {
        Goredoll target; string clue = ""; int score, streak; float nextRound;

        void Start() { Banner("ASSASSIN", "Find the target in the crowd. Innocents cost points.", 5f); nextRound = Time.time + 1f; }

        bool given;
        void Update()
        {
            if (!given && PlayerController.Instance != null) { given = true; Give("cknife", "glock"); }
            if (!Host || target != null || Time.time < nextRound) return;
            // New round: clear the plaza and fill it with a crowd, one of them the target.
            foreach (var d in new List<Goredoll>(Goredoll.All)) if (d != null) d.Despawn();
            var types = new List<NpcTypes.TypeDef>(NpcTypes.Types.FindAll(t => t.Weapon == null));
            var pick = types[Random.Range(0, types.Count)];
            for (int i = 0; i < 20; i++)
            {
                var at = new Vector3(Random.Range(-24f, 24f), 0.1f, Random.Range(-24f, 24f));
                if (at.magnitude < 4f) at += at.normalized * 5f;
                string preset = i < 14 ? "Passive" : NpcTypes.Preset(types[Random.Range(0, types.Count)].Id);
                if (preset.Contains("~t" + pick.Id)) preset = "Passive";
                GoredollFactory.Spawn(at, Random.Range(0f, 360f), preset);
            }
            var tp = new Vector3(Random.Range(-22f, 22f), 0.1f, Random.Range(-22f, 22f));
            target = GoredollFactory.Spawn(tp, Random.Range(0f, 360f), NpcTypes.Preset(pick.Id));
            clue = "TARGET: the " + pick.Name + ". " + pick.Desc;
            Banner("NEW TARGET", clue, 6f);
        }

        protected override void OnDollDied(Goredoll d, object attacker)
        {
            if (!GameModes2.ByMe(attacker)) return;
            if (d == target)
            {
                score += 100 + streak * 25; streak++; target = null; nextRound = Time.time + 4f;
                Banner("TARGET DOWN", "+" + (100 + (streak - 1) * 25) + "  streak " + streak, 3f); AudioManager.Ui("ui_success");
                if (score > PlayerPrefs.GetInt("assassin_best", 0)) PlayerPrefs.SetInt("assassin_best", score);
            }
            else { score -= 50; streak = 0; Banner("INNOCENT", "-50", 1.5f); AudioManager.Ui("ui_error"); }
        }

        protected override void DrawMode(float W, float H)
        {
            Panel(new Rect(W / 2 - 220, 20, 440, 50), "SCORE " + score + "   STREAK " + streak + "   BEST " + PlayerPrefs.GetInt("assassin_best", 0));
            if (!string.IsNullOrEmpty(clue)) Panel(new Rect(W / 2 - 420, 76, 840, 40), clue, 16, UI.Accent2);
        }
    }

    // ====================================================================== King of the Hill

    public sealed class KingOfHill : ModeBase
    {
        float score, roundStart, best, nextSpawn; bool over;

        void Start() { Banner("KING OF THE HILL", "Stay inside the gold ring. 2 minutes.", 5f); roundStart = Time.time; best = PlayerPrefs.GetFloat("koth_best", 0f); }

        bool given;
        void Update()
        {
            var p = PlayerController.Instance;
            if (!given && p != null) { given = true; Give("boxing", "bat"); }
            if (over || p == null) return;
            if (Time.time - roundStart > 120f)
            {
                over = true;
                if (score > best) { best = score; PlayerPrefs.SetFloat("koth_best", best); Banner("NEW RECORD", score.ToString("0") + " points", 8f); }
                else Banner("TIME!", score.ToString("0") + " points", 8f);
                return;
            }
            var fp = p.transform.position;
            if (!p.IsDead && new Vector2(fp.x, fp.z).magnitude < 4.5f && fp.y > 0.8f) score += Time.deltaTime * 10f;
            if (!Host || Time.time < nextSpawn) return;
            nextSpawn = Time.time + Mathf.Max(1.5f, 5f - (Time.time - roundStart) / 30f);
            int alive = 0;
            foreach (var d in Goredoll.All) if (d != null && !d.IsDead) alive++;
            if (alive >= 8) return;
            var at = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * new Vector3(0, 0.1f, 24f);
            GoredollFactory.Spawn(at, Random.Range(0f, 360f), Random.value < 0.3f ? NpcTypes.Preset("wrestler") : "Aggressive");
        }

        protected override void DrawMode(float W, float H)
        {
            Panel(new Rect(W / 2 - 220, 20, 440, 50), "POINTS " + score.ToString("0") + "   TIME " + Mathf.Max(0, 120 - (int)(Time.time - roundStart)) + " s   BEST " + best.ToString("0"));
        }
    }
}
