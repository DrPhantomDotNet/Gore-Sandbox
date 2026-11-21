using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// More Gore Sandbox 2 modes: Last Man Standing, Boss Rush, Parkour Race and Escape the Maze.
    /// Same pattern as GameModes2 (map built in code with a ModeBase on its root), added through GameModes2.Defs.
    /// </summary>
    public static class GameModes3
    {
        static Material T(Texture2D t) => MapBuilder.Tex(t);
        static MapDef Mode(string id, string name, string size, string glyph, TimeOfDay time, string desc, string[] features, Color top, Color bottom, Color accent, System.Action<MapBuilder> build, bool indoor = false)
            => new MapDef { Id = id, Name = name, Size = size, Glyph = glyph, DefaultTime = time, Indoor = indoor, Ambience = indoor ? "amb_hum" : "amb_wind", Description = desc, Features = features, Tags = new[] { "Game Mode" }, ThemeTop = top, ThemeBottom = bottom, Accent = accent, Build = build };

        public static IEnumerable<MapDef> Defs()
        {
            yield return Mode("lms", "Last Man Standing", "Mode - ruined arena", "skull", TimeOfDay.Night,
                "One life. Waves of armed Goredolls, each wave bigger and better armed than the last. How many waves can you survive?",
                new[] { "One life", "Endless waves", "Best wave saved" }, new Color(0.08f, 0.05f, 0.05f), new Color(0.45f, 0.3f, 0.25f), new Color(1f, 0.35f, 0.25f), BuildRuins);
            yield return Mode("bossrush", "Boss Rush", "Mode - colosseum pit", "skull", TimeOfDay.BloodMoon,
                "Giant after giant: the Wrestler, the Viking, the Samurai, the Bodybuilder, the Gladiator and the Knight. Beat all six as fast as you can.",
                new[] { "6 giant bosses", "Boss health bar", "Best time saved" }, new Color(0.25f, 0.02f, 0.02f), new Color(0.6f, 0.45f, 0.3f), new Color(1f, 0.2f, 0.15f), BuildPit);
            yield return Mode("parkour", "Parkour Race", "Mode - sky course", "jump", TimeOfDay.Day,
                "Platforms floating in the sky. Jump, sprint and climb to the finish. Fall and you go back to the last checkpoint.",
                new[] { "40 platforms", "Checkpoints", "Best time saved" }, new Color(0.45f, 0.7f, 1f), new Color(0.9f, 0.95f, 1f), new Color(0.3f, 1f, 0.6f), BuildSky);
            yield return Mode("maze", "Escape the Maze", "Mode - hedge maze", "grid", TimeOfDay.Night,
                "A new random maze every time. Find the exit before the things hunting you in the dark find you.",
                new[] { "Random maze", "Hunters", "Best time saved" }, new Color(0.05f, 0.08f, 0.05f), new Color(0.2f, 0.35f, 0.18f), new Color(0.5f, 1f, 0.4f), BuildMaze);
            yield return Mode("ffa", "Free-for-all", "Mode - ruined arena", "target", TimeOfDay.Sunset,
                "Sixteen Goredolls, every one for themselves, and you in the middle. Be the last one standing.",
                new[] { "Everyone fights everyone", "16 brawlers", "Wins saved" }, new Color(0.3f, 0.12f, 0.05f), new Color(0.5f, 0.38f, 0.25f), new Color(1f, 0.6f, 0.2f), m => BuildRuinsFor(m, 1));
            yield return Mode("oitc", "One in the Chamber", "Mode - ruined arena", "pistol_silenced", TimeOfDay.Night,
                "A pistol with one bullet and a knife. Every kill gives you a bullet back (up to three). Miss, and it's knife work.",
                new[] { "One bullet", "Kills refill", "Best score saved" }, new Color(0.1f, 0.08f, 0.1f), new Color(0.45f, 0.3f, 0.3f), new Color(1f, 0.3f, 0.3f), m => BuildRuinsFor(m, 2));
            yield return Mode("fightnight", "Fight Night", "Mode - boxing ring", "fist", TimeOfDay.Night,
                "A ring, a cheering crowd and five opponents, each tougher than the last. Fists and fighting styles only. Knock them out.",
                new[] { "5 opponents", "Crowd", "Best run saved" }, new Color(0.15f, 0.05f, 0.05f), new Color(0.55f, 0.15f, 0.12f), new Color(1f, 0.85f, 0.3f), BuildRing, true);
            yield return Mode("infection", "Infection", "Mode - town square", "skull", TimeOfDay.Sunset,
                "Two zombies in a crowded town square. Everyone they kill gets back up as one of them. Stop the outbreak before the whole town turns.",
                new[] { "Spreading infection", "Crowd of 24", "Survivors saved" }, new Color(0.3f, 0.15f, 0.1f), new Color(0.35f, 0.4f, 0.25f), new Color(0.6f, 1f, 0.3f), BuildSquare);
        }

        // ------------------------------------------------------------------ maps

        static void BuildRuins(MapBuilder m) => BuildRuinsFor(m, 0);

        static void BuildRuinsFor(MapBuilder m, int kind)
        {
            m.Options.StartingNpcs = 0;
            m.Block("Floor", new Vector3(-32, -1, -32), new Vector3(32, 0, 32), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.35f, 0.33f, 0.32f), 0.2f);
            var brick = T(TextureFactory.Brick); Color b = new Color(0.5f, 0.38f, 0.32f);
            m.Block("Wall", new Vector3(-32, 0, 32), new Vector3(32, 6, 32.5f), brick, SurfaceType.Concrete, b);
            m.Block("Wall", new Vector3(-32, 0, -32.5f), new Vector3(32, 6, -32), brick, SurfaceType.Concrete, b);
            m.Block("Wall", new Vector3(32, 0, -32), new Vector3(32.5f, 6, 32), brick, SurfaceType.Concrete, b);
            m.Block("Wall", new Vector3(-32.5f, 0, -32), new Vector3(-32, 6, 32), brick, SurfaceType.Concrete, b);
            for (int i = 0; i < 22; i++)
            {
                var p = new Vector3(Random.Range(-27f, 27f), 0, Random.Range(-27f, 27f));
                if (p.magnitude < 5f) continue;
                float h = Random.Range(0.8f, 3.2f);
                m.Box("Ruin", p + Vector3.up * h / 2f, Quaternion.Euler(0, Random.Range(0f, 90f), 0), new Vector3(Random.Range(2f, 5f), h, 0.6f), brick, SurfaceType.Concrete, b * Random.Range(0.8f, 1.1f));
            }
            for (int i = 0; i < 4; i++) m.StreetLamp(new Vector3(i < 2 ? -20 : 20, 0, i % 2 == 0 ? -20 : 20), 45f + i * 90f, new Color(1f, 0.6f, 0.35f), 6f, 26f, 2f);
            if (kind == 1) m.Root.gameObject.AddComponent<FreeForAll>(); else if (kind == 2) m.Root.gameObject.AddComponent<OneInTheChamber>(); else m.Root.gameObject.AddComponent<LastManStanding>();
            m.PlayerSpawn = new Vector3(0, 0.1f, 0);
            for (int i = 0; i < 8; i++) m.NpcSpawns.Add(Quaternion.Euler(0, i * 45f, 0) * new Vector3(0, 0.1f, 28f));
            m.Playable = new Bounds(new Vector3(0, 4, 0), new Vector3(64, 10, 64));
        }

        static void BuildPit(MapBuilder m)
        {
            m.Options.StartingNpcs = 0;
            m.Block("Sand", new Vector3(-30, -1, -30), new Vector3(30, 0, 30), T(TextureFactory.Sand), SurfaceType.Dirt, new Color(0.8f, 0.68f, 0.5f), 0.2f);
            Color stone = new Color(0.7f, 0.62f, 0.5f);
            for (int i = 0; i < 28; i++)
            {
                float a = i * Mathf.PI * 2f / 28f; var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                m.Box("Pit Wall", dir * 22f + Vector3.up * 3f, Quaternion.LookRotation(dir), new Vector3(5.2f, 6f, 1f), T(TextureFactory.Brick), SurfaceType.Concrete, stone);
            }
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f; var p = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 19f;
                var l = new GameObject("Brazier").AddComponent<Light>(); l.transform.SetParent(m.Root, false); l.transform.position = p + Vector3.up * 2.2f;
                l.type = LightType.Point; l.range = 14f; l.intensity = 1.8f; l.color = new Color(1f, 0.45f, 0.15f); l.shadows = LightShadows.None; m.Lamps.Add(l);
                FlickerLight.Maybe(l, null, 1f);
                m.Cylinder("Brazier", p, 0.5f, 1.6f, Mats.LitMetal, SurfaceType.Metal, new Color(0.25f, 0.2f, 0.18f), 10);
                m.Box("Coals", p + Vector3.up * 1.65f, Quaternion.identity, new Vector3(0.7f, 0.1f, 0.7f), Mats.Glow(new Color(1f, 0.35f, 0.05f), 1.2f), SurfaceType.Wood, null, 1f, false);
            }
            m.Root.gameObject.AddComponent<BossRush>();
            m.PlayerSpawn = new Vector3(0, 0.1f, -14f);
            m.NpcSpawns.Add(new Vector3(0, 0.1f, 12f));
            m.Playable = new Bounds(new Vector3(0, 5, 0), new Vector3(46, 20, 46));
        }

        public static readonly List<Vector3> SkyCheckpoints = new List<Vector3>();

        static void BuildSky(MapBuilder m)
        {
            m.Options.StartingNpcs = 0;
            SkyCheckpoints.Clear();
            var cols = new[] { new Color(0.95f, 0.95f, 0.97f), new Color(0.3f, 0.75f, 1f), new Color(1f, 0.75f, 0.3f) };
            Vector3 p = new Vector3(0, 20, 0);
            m.Box("Start", p - Vector3.up * 0.25f, Quaternion.identity, new Vector3(8f, 0.5f, 8f), T(TextureFactory.Concrete), SurfaceType.Concrete, cols[0]);
            SkyCheckpoints.Add(p + Vector3.up * 0.1f);
            var rnd = new System.Random(4242);
            float F(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            for (int i = 1; i <= 40; i++)
            {
                bool cp = i % 10 == 0;
                p += new Vector3(F(-3f, 3f), F(-0.8f, 1.3f), F(3.5f, 5.5f));
                float s = cp ? 5f : F(1.4f, 2.6f);
                m.Box(cp ? "Checkpoint" : "Platform", p - Vector3.up * 0.2f, Quaternion.Euler(0, F(-20f, 20f), 0), new Vector3(s, 0.4f, s), Mats.LitMetal, SurfaceType.Metal, cp ? new Color(0.3f, 1f, 0.5f) : cols[i % 3]);
                if (cp) { SkyCheckpoints.Add(p + Vector3.up * 0.1f); m.Box("Beacon", p + Vector3.up * 2f, Quaternion.identity, new Vector3(0.15f, 4f, 0.15f), Mats.Glow(new Color(0.3f, 1f, 0.5f), 1f), SurfaceType.Metal, null, 1f, false); }
                if (i % 7 == 3) { var q = p + new Vector3(0, 0, 3f); m.Box("Wall Run", q + Vector3.up * 1.5f, Quaternion.identity, new Vector3(0.4f, 3f, 4f), Mats.LitMetal, SurfaceType.Metal, cols[1]); }
            }
            m.Root.gameObject.AddComponent<ParkourRace>();
            m.PlayerSpawn = SkyCheckpoints[0];
            m.Playable = new Bounds(new Vector3(0, 20, 100), new Vector3(120, 80, 260));
            m.KillY = -200f; // falling is handled by the mode (back to the checkpoint)
        }

        public static readonly List<Goredoll> RingCrowd = new List<Goredoll>();

        static void BuildRing(MapBuilder m)
        {
            m.Options.StartingNpcs = 0;
            Maps2.Room(m, new Vector3(-22, 0, -22), new Vector3(22, 12, 22), TextureFactory.Concrete, new Color(0.18f, 0.16f, 0.17f));
            m.Block("Ring", new Vector3(-6, 0, -6), new Vector3(6, 1f, 6), T(TextureFactory.Carpet), SurfaceType.Carpet, new Color(0.25f, 0.3f, 0.6f));
            for (int c = 0; c < 4; c++) m.Box("Corner Post", new Vector3(c < 2 ? -6 : 6, 1.9f, c % 2 == 0 ? -6 : 6), Quaternion.identity, new Vector3(0.25f, 1.8f, 0.25f), Mats.LitMetal, SurfaceType.Metal, c % 2 == 0 ? new Color(0.8f, 0.1f, 0.1f) : new Color(0.1f, 0.2f, 0.8f));
            for (int k = 0; k < 3; k++)
                for (int sd = 0; sd < 4; sd++)
                {
                    var rot = Quaternion.Euler(0, sd * 90f, 0);
                    m.Box("Rope", rot * new Vector3(0, 1.5f + k * 0.45f, 6f), rot, new Vector3(12f, 0.05f, 0.05f), Mats.Lit, SurfaceType.Carpet, Color.white, 1f, true);
                }
            m.Ramp("Ring Steps", new Vector3(0, 0, -9f), 0f, 1.5f, 1f, 20f, T(TextureFactory.Wood), SurfaceType.Wood);
            for (int t = 0; t < 4; t++)
                for (int sd = 0; sd < 4; sd++)
                {
                    var rot = Quaternion.Euler(0, sd * 90f, 0);
                    m.Box("Seats", rot * new Vector3(0, 0.4f + t * 0.8f, 11f + t * 2.2f), rot, new Vector3(22f + t * 4f, 0.8f + t * 1.6f, 2.2f), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.3f, 0.12f, 0.12f));
                }
            var spot = new GameObject("Ring Light").AddComponent<Light>(); spot.transform.SetParent(m.Root, false); spot.transform.position = new Vector3(0, 11.5f, 0); spot.transform.rotation = Quaternion.Euler(90, 0, 0);
            spot.type = LightType.Spot; spot.spotAngle = 70f; spot.range = 20f; spot.intensity = 4f; spot.color = new Color(1f, 0.97f, 0.9f); spot.shadows = LightShadows.Soft; m.Lamps.Add(spot);
            m.CeilingLight(new Vector3(0, 11.7f, 14), new Color(1f, 0.7f, 0.5f), 18f, 0.8f);
            m.Root.gameObject.AddComponent<FightNight>();
            m.PlayerSpawn = new Vector3(0, 1.1f, -4f);
            m.Playable = new Bounds(new Vector3(0, 6, 0), new Vector3(44, 12, 44));
        }

        static void BuildSquare(MapBuilder m)
        {
            m.Options.StartingNpcs = 0;
            m.Block("Square", new Vector3(-40, -1, -40), new Vector3(40, 0, 40), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.5f, 0.48f, 0.45f), 0.2f);
            var tints = new[] { new Color(0.75f, 0.65f, 0.5f), new Color(0.6f, 0.45f, 0.4f), new Color(0.55f, 0.6f, 0.65f) };
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f; var c = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 30f;
                m.Building(c - new Vector3(5, 0, 5), c + new Vector3(5, 9 + (i % 3) * 3, 5), i % 2 == 0 ? TextureFactory.Brick : TextureFactory.Plaster, tints[i % 3], true);
            }
            m.Cylinder("Fountain", Vector3.zero, 3f, 0.7f, T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.6f, 0.6f, 0.62f), 24);
            for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3f + 0.4f; m.Box("Bench", new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 9f + Vector3.up * 0.25f, Quaternion.Euler(0, a * Mathf.Rad2Deg, 0), new Vector3(2f, 0.5f, 0.6f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.5f, 0.35f, 0.2f)); }
            for (int i = 0; i < 6; i++) m.StreetLamp(Quaternion.Euler(0, i * 60f, 0) * new Vector3(0, 0, 16f), i * 60f + 180f, new Color(1f, 0.75f, 0.45f), 5f, 18f, 2f);
            m.Root.gameObject.AddComponent<Infection>();
            m.PlayerSpawn = new Vector3(0, 0.1f, -20f);
            m.Playable = new Bounds(new Vector3(0, 5, 0), new Vector3(80, 20, 80));
            m.Boundaries(new Bounds(new Vector3(0, 10, 0), new Vector3(52, 20, 52)));
        }

        public static Vector3 MazeExit;

        static void BuildMaze(MapBuilder m)
        {
            m.Options.StartingNpcs = 0;
            const int N = 12; const float C = 4f; float half = N * C / 2f;
            m.Block("Ground", new Vector3(-half - 4, -1, -half - 4), new Vector3(half + 4, 0, half + 4), T(TextureFactory.Grass), SurfaceType.Grass, new Color(0.22f, 0.32f, 0.18f), 0.2f);
            // Recursive backtracker.
            var wallE = new bool[N, N]; var wallN = new bool[N, N]; var seen = new bool[N, N];
            for (int x = 0; x < N; x++) for (int z = 0; z < N; z++) { wallE[x, z] = true; wallN[x, z] = true; }
            var stack = new Stack<Vector2Int>(); stack.Push(new Vector2Int(0, 0)); seen[0, 0] = true;
            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            while (stack.Count > 0)
            {
                var c = stack.Peek(); var opts = new List<Vector2Int>();
                foreach (var d in dirs) { var n = c + d; if (n.x >= 0 && n.y >= 0 && n.x < N && n.y < N && !seen[n.x, n.y]) opts.Add(d); }
                if (opts.Count == 0) { stack.Pop(); continue; }
                var dd = opts[Random.Range(0, opts.Count)]; var nn = c + dd;
                if (dd.x == 1) wallE[c.x, c.y] = false; else if (dd.x == -1) wallE[nn.x, nn.y] = false;
                else if (dd.y == 1) wallN[c.x, c.y] = false; else wallN[nn.x, nn.y] = false;
                seen[nn.x, nn.y] = true; stack.Push(nn);
            }
            var hedge = T(TextureFactory.Grass); Color hc = new Color(0.18f, 0.3f, 0.14f); const float H = 3.2f;
            Vector3 Cell(int x, int z) => new Vector3(-half + x * C + C / 2f, 0, -half + z * C + C / 2f);
            for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                {
                    var c = Cell(x, z);
                    if (wallE[x, z] && !(x == N - 1 && z == N - 1)) m.Box("Hedge", c + new Vector3(C / 2f, H / 2f, 0), Quaternion.identity, new Vector3(0.8f, H, C + 0.8f), hedge, SurfaceType.Grass, hc);
                    if (wallN[x, z]) m.Box("Hedge", c + new Vector3(0, H / 2f, C / 2f), Quaternion.identity, new Vector3(C + 0.8f, H, 0.8f), hedge, SurfaceType.Grass, hc);
                }
            m.Box("Hedge", new Vector3(0, H / 2f, -half), Quaternion.identity, new Vector3(N * C + 0.8f, H, 0.8f), hedge, SurfaceType.Grass, hc);
            m.Box("Hedge", new Vector3(-half, H / 2f, 0), Quaternion.identity, new Vector3(0.8f, H, N * C + 0.8f), hedge, SurfaceType.Grass, hc);
            MazeExit = Cell(N - 1, N - 1) + new Vector3(C, 0, 0);
            m.Box("Exit Light", MazeExit + Vector3.up * 0.05f, Quaternion.identity, new Vector3(2.5f, 0.05f, 2.5f), Mats.Glow(new Color(0.4f, 1f, 0.4f), 1.2f), SurfaceType.Grass, null, 1f, false);
            var l = new GameObject("Exit Lamp").AddComponent<Light>(); l.transform.SetParent(m.Root, false); l.transform.position = MazeExit + Vector3.up * 2f;
            l.type = LightType.Point; l.range = 10f; l.intensity = 2f; l.color = new Color(0.4f, 1f, 0.4f); l.shadows = LightShadows.None; m.Lamps.Add(l);
            m.Root.gameObject.AddComponent<MazeEscape>();
            m.PlayerSpawn = Cell(0, 0) + Vector3.up * 0.1f;
            for (int i = 0; i < 4; i++) m.NpcSpawns.Add(Cell(Random.Range(5, N), Random.Range(5, N)) + Vector3.up * 0.1f);
            m.Playable = new Bounds(new Vector3(0, 3, 0), new Vector3(N * C + 10, 10, N * C + 10));
        }
    }

    // ====================================================================== Last Man Standing

    public sealed class LastManStanding : ModeBase
    {
        int wave, kills, best; float nextWave; bool dead, given;
        static readonly string[] Guns = { null, null, "glock", "uzi", "shotgun", "ak47" };

        void Start() { Banner("LAST MAN STANDING", "One life. Survive the waves.", 5f); nextWave = Time.time + 4f; best = PlayerPrefs.GetInt("lms_best", 0); }

        void Update()
        {
            var p = PlayerController.Instance;
            if (!given && p != null) { given = true; Give("glock", "shotgun", "machete"); }
            if (p == null || dead) return;
            if (p.IsDead)
            {
                dead = true;
                if (wave - 1 > best) { best = wave - 1; PlayerPrefs.SetInt("lms_best", best); }
                Banner("YOU DIED", "Survived " + Mathf.Max(0, wave - 1) + " waves, " + kills + " kills. Restart the map to try again.", 10f);
                return;
            }
            if (!Host || Time.time < nextWave) return;
            int alive = 0;
            foreach (var d in Goredoll.All) if (d != null && !d.IsDead) alive++;
            if (alive > 0) return;
            wave++;
            int n = 2 + wave * 2;
            for (int i = 0; i < n; i++)
            {
                var at = GameModes2.SpawnPoint(Quaternion.Euler(0, Random.Range(0f, 360f), 0) * new Vector3(0, 0.1f, 27f));
                bool armed = Random.value < Mathf.Clamp01(0.15f + wave * 0.1f);
                GoredollFactory.Spawn(at, Random.Range(0f, 360f), armed ? "Gunman" : (wave > 3 && Random.value < 0.3f ? NpcTypes.Preset(Random.value < 0.5f ? "boxer" : "biker") : "Aggressive"));
            }
            Banner("WAVE " + wave, n + " enemies", 3f);
            AudioManager.Ui("ui_tab");
            if (wave % 3 == 0 && p.Health != null) p.Health.Current = Mathf.Min(p.Health.Max, p.Health.Current + 40f);
            nextWave = Time.time + 3f;
        }

        protected override void OnDollDied(Goredoll d, object attacker) { if (GameModes2.ByMe(attacker)) kills++; }

        protected override void DrawMode(float W, float H)
        {
            Panel(new Rect(W / 2 - 220, 20, 440, 50), "WAVE " + wave + "    KILLS " + kills + "    BEST " + best);
        }
    }

    // ====================================================================== Boss Rush

    public sealed class BossRush : ModeBase
    {
        static readonly string[] Bosses = { "wrestler", "viking", "samurai", "bodybuilder", "gladiator", "knight" };
        int index = -1; Goredoll boss; float start, nextBoss, best; bool won, given;

        void Start() { Banner("BOSS RUSH", "Six giants. One at a time.", 5f); start = Time.time; nextBoss = Time.time + 4f; best = PlayerPrefs.GetFloat("bossrush_best", 0f); }

        void Update()
        {
            if (!given && PlayerController.Instance != null) { given = true; Give("shotgun", "ak47", "rpg", "sledgehammer"); }
            if (won || !Host) return;
            if (boss != null && !boss.IsDead) return;
            if (Time.time < nextBoss) return;
            index++;
            if (index >= Bosses.Length)
            {
                won = true; float t = Time.time - start;
                if (best <= 0f || t < best) { best = t; PlayerPrefs.SetFloat("bossrush_best", t); }
                Banner("ALL BOSSES DOWN", t.ToString("0.0") + " s", 10f); AudioManager.Ui("ui_success");
                return;
            }
            boss = GoredollFactory.Spawn(new Vector3(0, 0.3f, 12f), 180f, NpcTypes.Preset(Bosses[index], true));
            var t2 = NpcTypes.Get(Bosses[index]);
            Banner("BOSS " + (index + 1) + "/" + Bosses.Length, "The Giant " + (t2 != null ? t2.Name : Bosses[index]), 3f);
            AudioManager.Play2D("bell", 0.6f, 0.6f, AudioBus.Sfx);
        }

        protected override void OnDollDied(Goredoll d, object attacker)
        {
            if (d == boss && !won) { nextBoss = Time.time + 4f; Banner("BOSS DOWN", (Bosses.Length - index - 1) + " to go", 3f); AudioManager.Ui("ui_success"); }
        }

        protected override void DrawMode(float W, float H)
        {
            Panel(new Rect(W / 2 - 220, 20, 440, 50), "BOSS " + Mathf.Clamp(index + 1, 0, Bosses.Length) + "/" + Bosses.Length + "    " + (won ? "DONE" : (Time.time - start).ToString("0") + " s") + "    BEST " + (best > 0 ? best.ToString("0.0") + " s" : "-"));
            if (boss != null && !boss.IsDead && boss.Vitals != null)
            {
                float k = Mathf.Clamp01(1f - boss.Vitals.BloodLossFraction / 0.45f);
                var r = new Rect(W / 2 - 300, 80, 600, 22);
                UI.Rounded(r, new Color(0, 0, 0, 0.6f), true);
                UI.Rounded(new Rect(r.x + 3, r.y + 3, (r.width - 6) * k, r.height - 6), new Color(0.85f, 0.12f, 0.1f), true);
            }
        }
    }

    // ====================================================================== Parkour Race

    public sealed class ParkourRace : ModeBase
    {
        int checkpoint, falls; float start = -1f, best; bool done;

        void Start() { Banner("PARKOUR RACE", "Reach the last green platform. Falling sends you back.", 5f); best = PlayerPrefs.GetFloat("parkour_best", 0f); }

        void Update()
        {
            var p = PlayerController.Instance;
            var cps = GameModes3.SkyCheckpoints;
            if (p == null || cps.Count == 0) return;
            var pos = p.transform.position;
            if (start < 0f && !done && (pos - cps[0]).sqrMagnitude > 16f) start = Time.time;
            for (int i = checkpoint + 1; i < cps.Count; i++)
                if ((pos - cps[i]).sqrMagnitude < 9f)
                {
                    checkpoint = i;
                    if (i == cps.Count - 1 && !done)
                    {
                        done = true; float t = Time.time - Mathf.Max(0f, start);
                        if (t < 60f) EasterEggs.Found("Free Runner", "Finished the Parkour Race in under a minute.");
                        if (best <= 0f || t < best) { best = t; PlayerPrefs.SetFloat("parkour_best", t); Banner("NEW RECORD", t.ToString("0.00") + " s", 8f); }
                        else Banner("FINISH", t.ToString("0.00") + " s", 8f);
                        AudioManager.Ui("ui_success");
                    }
                    else { Banner("CHECKPOINT", i + " of " + (cps.Count - 1), 1.5f); AudioManager.Ui("ui_tab"); }
                }
            if (pos.y < cps[checkpoint].y - 12f || p.IsDead)
            {
                falls++;
                if (p.IsDead) p.Respawn();
                p.Teleport(cps[checkpoint]);
            }
        }

        protected override void DrawMode(float W, float H)
        {
            string t = done ? "DONE" : start >= 0f ? (Time.time - start).ToString("0.00") + " s" : "GO!";
            Panel(new Rect(W / 2 - 240, 20, 480, 50), t + "    CHECKPOINT " + checkpoint + "    FALLS " + falls + "    BEST " + (best > 0 ? best.ToString("0.00") + " s" : "-"));
        }
    }

    // ====================================================================== Escape the Maze

    public sealed class MazeEscape : ModeBase
    {
        float start, best, nextHunter; bool done; int hunters;

        void Start() { Banner("ESCAPE THE MAZE", "Find the green light. Something is in here with you.", 5f); start = Time.time; nextHunter = Time.time + 20f; best = PlayerPrefs.GetFloat("maze_best", 0f); }

        void Update()
        {
            var p = PlayerController.Instance;
            if (p == null || done) return;
            var pos = p.transform.position;
            if ((new Vector2(pos.x, pos.z) - new Vector2(GameModes3.MazeExit.x, GameModes3.MazeExit.z)).sqrMagnitude < 4f)
            {
                done = true; float t = Time.time - start;
                if (t < 90f) EasterEggs.Found("Minotaur Who?", "Escaped the maze in under 90 seconds.");
                if (best <= 0f || t < best) { best = t; PlayerPrefs.SetFloat("maze_best", t); Banner("ESCAPED - NEW RECORD", t.ToString("0.0") + " s", 8f); }
                else Banner("ESCAPED", t.ToString("0.0") + " s", 8f);
                AudioManager.Ui("ui_success");
                return;
            }
            if (p.IsDead) { done = true; Banner("THEY GOT YOU", "Restart the map for a new maze.", 8f); return; }
            if (!Host || Time.time < nextHunter || hunters >= 6) return;
            nextHunter = Time.time + 25f; hunters++;
            var at = GameModes2.SpawnPoint(GameModes3.MazeExit - new Vector3(4f, 0, 2f));
            Zombies.Spawn(hunters % 3 == 0 ? ZombieKind.Runner : ZombieKind.Walker, at, Random.Range(0f, 360f));
            AudioManager.Play2D("bell", 0.25f, 0.45f, AudioBus.Sfx);
        }

        protected override void DrawMode(float W, float H)
        {
            Panel(new Rect(W / 2 - 220, 20, 440, 50), (done ? "DONE" : (Time.time - start).ToString("0") + " s") + "    HUNTERS " + hunters + "    BEST " + (best > 0 ? best.ToString("0.0") + " s" : "-"));
        }
    }

    // ====================================================================== Infection

    public sealed class Infection : ModeBase
    {
        int saved, turned, zombieKills; float start; bool started, over, given;

        void Start() { Banner("INFECTION", "Kill the zombies before they turn the whole town.", 5f); start = Time.time; }

        void Update()
        {
            if (!given && PlayerController.Instance != null) { given = true; Give("shotgun", "glock", "machete"); }
            if (!Host || over) return;
            if (!started && Time.time - start > 2f)
            {
                started = true;
                for (int i = 0; i < 24; i++)
                {
                    var at = new Vector3(Random.Range(-20f, 20f), 0.1f, Random.Range(-20f, 20f));
                    if (at.magnitude < 4f) at += at.normalized * 5f;
                    GoredollFactory.Spawn(at, Random.Range(0f, 360f), Random.value < 0.2f ? "Coward" : "Passive");
                }
                Zombies.Spawn(ZombieKind.Walker, new Vector3(18f, 0.1f, 18f), 225f);
                Zombies.Spawn(ZombieKind.Runner, new Vector3(-18f, 0.1f, 18f), 135f);
                return;
            }
            if (!started) return;
            int humans = 0, zombies = 0;
            foreach (var d in Goredoll.All) { if (d == null || d.IsDead) continue; if (d.Zombie != null) zombies++; else humans++; }
            if (zombies == 0 && Time.time - start > 6f) { over = true; saved = humans; Banner("OUTBREAK STOPPED", saved + " survivors saved", 10f); AudioManager.Ui("ui_success"); if (saved > PlayerPrefs.GetInt("infection_best", 0)) PlayerPrefs.SetInt("infection_best", saved); }
            else if (humans == 0 && Time.time - start > 6f) { over = true; Banner("THE TOWN IS GONE", "Everyone turned. Restart the map to try again.", 10f); }
        }

        protected override void OnDollDied(Goredoll d, object attacker)
        {
            if (d == null) return;
            if (d.Zombie != null) { if (GameModes2.ByMe(attacker)) zombieKills++; return; }
            var z = attacker as Goredoll;
            if (z == null || z.Zombie == null || !Host || over) return;
            turned++;
            StartCoroutine(Turn(d));
        }

        System.Collections.IEnumerator Turn(Goredoll victim)
        {
            yield return new WaitForSeconds(Random.Range(3f, 6f));
            if (victim == null || over) yield break;
            var pos = victim.Position; pos.y = 0.1f;
            victim.Despawn();
            Zombies.Spawn(Random.value < 0.3f ? ZombieKind.Runner : ZombieKind.Walker, pos, Random.Range(0f, 360f));
            Banner("SOMEONE TURNED", turned + " infected", 2f);
        }

        protected override void DrawMode(float W, float H)
        {
            int humans = 0, zombies = 0;
            foreach (var d in Goredoll.All) { if (d == null || d.IsDead) continue; if (d.Zombie != null) zombies++; else humans++; }
            Panel(new Rect(W / 2 - 260, 20, 520, 50), "HUMANS " + humans + "   ZOMBIES " + zombies + "   TURNED " + turned + "   BEST " + PlayerPrefs.GetInt("infection_best", 0));
        }
    }

    // ====================================================================== Free-for-all

    public sealed class FreeForAll : ModeBase
    {
        bool spawned, over, given, prevInfighting; int kills; float start;

        void Start() { Banner("FREE-FOR-ALL", "Everyone fights everyone. Be the last one standing.", 5f); start = Time.time; prevInfighting = Rules.NpcInfighting; Rules.NpcInfighting = true; }
        protected override void OnDisable() { base.OnDisable(); Rules.NpcInfighting = prevInfighting; }

        void Update()
        {
            var p = PlayerController.Instance;
            if (!given && p != null) { given = true; Give("boxing", "bat", "glock"); }
            if (over || p == null) return;
            if (Host && !spawned && Time.time - start > 2.5f)
            {
                spawned = true;
                var types = new[] { "boxer", "wrestler", "biker", "gangster", "gladiator", "viking", "samurai", "bodybuilder" };
                for (int i = 0; i < 16; i++)
                {
                    var at = Quaternion.Euler(0, i * 22.5f, 0) * new Vector3(0, 0.1f, Random.Range(10f, 26f));
                    GoredollFactory.Spawn(at, Random.Range(0f, 360f), Random.value < 0.6f ? NpcTypes.Preset(types[Random.Range(0, types.Length)]) : "Aggressive");
                }
                return;
            }
            if (!spawned) return;
            if (p.IsDead) { over = true; Banner("ELIMINATED", kills + " kills. Restart the map to try again.", 10f); return; }
            int alive = 0;
            foreach (var d in Goredoll.All) if (d != null && !d.IsDead && d.Vitals.Conscious) alive++;
            if (alive == 0) { over = true; EasterEggs.Found("Last One Standing", "Won a Free-for-all."); PlayerPrefs.SetInt("ffa_wins", PlayerPrefs.GetInt("ffa_wins", 0) + 1); Banner("LAST ONE STANDING", kills + " kills in " + (Time.time - start).ToString("0") + " s", 10f); AudioManager.Ui("ui_success"); }
        }

        protected override void OnDollDied(Goredoll d, object attacker) { if (GameModes2.ByMe(attacker)) kills++; }

        protected override void DrawMode(float W, float H)
        {
            int alive = 0;
            foreach (var d in Goredoll.All) if (d != null && !d.IsDead && d.Vitals.Conscious) alive++;
            Panel(new Rect(W / 2 - 230, 20, 460, 50), "STANDING " + (alive + 1) + "   KILLS " + kills + "   WINS " + PlayerPrefs.GetInt("ffa_wins", 0));
        }
    }

    // ====================================================================== Fight Night

    public sealed class FightNight : ModeBase
    {
        static readonly string[] Opponents = { "boxer", "brawler2", "wrestler", "bodybuilder", "sumo2" };
        static readonly string[] OppTypes = { "boxer", "biker", "wrestler", "bodybuilder", "gladiator" };
        int round = -1, best; Goredoll opp; float next; bool over, given, crowdSpawned; float cheerNext;

        void Start() { Banner("FIGHT NIGHT", "Five opponents. Fists only. Knock them out.", 5f); next = Time.time + 5f; best = PlayerPrefs.GetInt("fightnight_best", 0); }

        void Update()
        {
            var p = PlayerController.Instance;
            if (!given && p != null) { given = true; Give("boxing", "karate", "muaythai", "wrestling"); }
            if (p == null || over) return;
            if (Host && !crowdSpawned)
            {
                crowdSpawned = true;
                for (int i = 0; i < 16; i++)
                {
                    float a = i / 16f * Mathf.PI * 2f; var at = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 12.5f + Vector3.up * 0.9f;
                    var c = GoredollFactory.Spawn(at, a * Mathf.Rad2Deg + 180f, "Passive");
                    if (c != null) { if (c.Brain != null) c.Brain.enabled = false; c.DisplayName = "Fan"; GameModes3.RingCrowd.Add(c); }
                }
            }
            if (Time.time > cheerNext) { cheerNext = Time.time + Random.Range(1.5f, 4f); foreach (var c in GameModes3.RingCrowd) if (c != null && !c.IsDead && Random.value < 0.15f) c.Voice?.Say(Random.value < 0.5f ? "v_hey" : "v_whoa", 0.5f, false); }
            if (p.IsDead) { over = true; Banner("KNOCKED OUT", "You won " + Mathf.Max(0, round) + " fights. Restart the map to go again.", 10f); return; }
            if (!Host) return;
            bool oppDown = opp != null && (opp.IsDead || opp.Vitals.State == LifeState.Unconscious);
            if (opp != null && !oppDown) return;
            if (opp != null && oppDown && next <= 0f)
            {
                next = Time.time + 4f;
                Banner("K.O.!", "Round " + (round + 1) + " won", 3f); AudioManager.Ui("ui_success"); AudioManager.Play2D("bell", 0.8f, 1f, AudioBus.Sfx);
                if (round + 1 > best) { best = round + 1; PlayerPrefs.SetInt("fightnight_best", best); }
                if (round + 1 >= Opponents.Length) { over = true; Banner("CHAMPION", "You beat all five. The crowd goes wild.", 10f); return; }
            }
            if (Time.time < next) return;
            if (opp != null) opp.Despawn();
            round++; next = 0f;
            opp = GoredollFactory.Spawn(new Vector3(0, 1.1f, 4f), 180f, NpcTypes.Preset(OppTypes[round]));
            if (opp != null && opp.Brain != null) { opp.Brain.Hostile = true; opp.Brain.Aggression = 1f; }
            if (p.Health != null) p.Health.Current = Mathf.Min(p.Health.Max, p.Health.Current + 35f);
            var t = NpcTypes.Get(OppTypes[round]);
            Banner("ROUND " + (round + 1), "vs. the " + (t != null ? t.Name : "Fighter"), 3f);
            AudioManager.Play2D("bell", 0.9f, 1.1f, AudioBus.Sfx);
        }

        protected override void OnDisable() { base.OnDisable(); GameModes3.RingCrowd.Clear(); }

        protected override void DrawMode(float W, float H)
        {
            Panel(new Rect(W / 2 - 220, 20, 440, 50), "ROUND " + Mathf.Clamp(round + 1, 1, Opponents.Length) + "/" + Opponents.Length + "    BEST " + best + " wins");
        }
    }

    // ====================================================================== One in the Chamber

    public sealed class OneInTheChamber : ModeBase
    {
        int bullets = 1, kills, best, lastAmmo = -1; bool given, over; float nextSpawn;

        void Start() { Banner("ONE IN THE CHAMBER", "One bullet. Every kill gives you one back.", 5f); best = PlayerPrefs.GetInt("oitc_best", 0); nextSpawn = Time.time + 3f; }

        void Update()
        {
            var p = PlayerController.Instance;
            if (p == null || over) return;
            if (!given) { given = true; p.Inventory.Strip(); Give("glock", "cknife"); Rules.InfiniteAmmo = false; }
            // Keep the pistol's magazine to the bullets you've earned (reloading can't conjure more).
            foreach (var w in p.Inventory.Slots)
                if (w is Firearm f && f.Def.Id == "glock")
                {
                    if (lastAmmo >= 0 && f.Ammo < lastAmmo) bullets = Mathf.Max(0, bullets - (lastAmmo - f.Ammo));
                    f.Ammo = Mathf.Min(f.Ammo, bullets);
                    if (f.Ammo < bullets) f.Ammo = bullets;
                    lastAmmo = f.Ammo;
                }
            if (p.IsDead)
            {
                over = true;
                if (kills > best) { best = kills; PlayerPrefs.SetInt("oitc_best", best); }
                Banner("DEAD", kills + " kills. Restart the map to try again.", 10f);
                return;
            }
            if (!Host || Time.time < nextSpawn) return;
            nextSpawn = Time.time + Mathf.Max(2f, 6f - kills * 0.15f);
            int alive = 0; foreach (var d in Goredoll.All) if (d != null && !d.IsDead) alive++;
            if (alive >= 6) return;
            var at = GameModes2.SpawnPoint(Quaternion.Euler(0, Random.Range(0f, 360f), 0) * new Vector3(0, 0.1f, 27f));
            GoredollFactory.Spawn(at, Random.Range(0f, 360f), kills > 8 && Random.value < 0.3f ? NpcTypes.Preset("samurai") : "Aggressive");
        }

        protected override void OnDollDied(Goredoll d, object attacker)
        {
            if (over || !GameModes2.ByMe(attacker)) return;
            kills++;
            if (bullets < 3) { bullets++; Banner("+1 BULLET", bullets + " in the gun", 1.2f); AudioManager.Ui("ui_tab"); }
        }

        protected override void DrawMode(float W, float H)
        {
            Panel(new Rect(W / 2 - 220, 20, 440, 50), "BULLETS " + bullets + "    KILLS " + kills + "    BEST " + best);
        }
    }
}
