using System.Collections;
using UnityEngine;

namespace SolarEclipse
{
    public enum GameState { Boot, Splash, Menu, Loading, Playing }

    /// <summary>
    /// The one persistent object. Boots every system, runs splash → menu → loading → playing, owns global
    /// hotkeys (Esc, F5 dev panel, F2 screenshot, F3 hide HUD, F11 fullscreen), time scale / slow-mo,
    /// cursor locking and per-frame housekeeping (input sampling, debris expiry).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }
        public GameState State { get; private set; } = GameState.Boot;
        public MapDef CurrentMap { get; private set; }
        public MapOptions CurrentOptions { get; private set; }
        /// <summary>Seed of the current map build (shared with clients so everyone builds the same map).</summary>
        public int CurrentSeed { get; private set; }
        public bool HudHidden;
        public float Fps { get; private set; } = 60f;
        public float SessionStart { get; private set; }

        float slowMoUntil, slowMoScale = 1f;
        Coroutine loading;

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 144;
            SessionStart = Time.realtimeSinceStartup;

            // Disable any camera that came with the scene: the game makes its own.
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (cam.transform.root != transform) cam.gameObject.SetActive(false);

            DevConsole.Hook();
            Settings.Load();
            Layers.ConfigureCollisionMatrix();
            Rules.ResetAll();
            Stats.Hook();
            AudioManager.Create(transform);
            UIRoot.Create(transform);
            Hud.Create(transform);
            MobileControls.Create(transform);
            DevPanel.Create(transform);
            NetManager.Create(transform);
            VoiceChat.Create(transform);
            EasterEggs.Init();
            Debug.Log($"[{GameInfo.Studio}] {GameInfo.Title} v{GameInfo.Version} ({GameInfo.Access}) booting on {Application.platform}, Unity {Application.unityVersion}");
        }

        void Start()
        {
            JointCalibration.Run();
            StartCoroutine(SoundBank.GenerateAll());
            MenuBackdrop.Create();
            State = GameState.Splash;
            UIRoot.Push(new SplashScreen());
        }

        // ------------------------------------------------------------------ flow

        public void OnSplashFinished()
        {
            State = GameState.Menu;
            UIRoot.Replace(new MainMenuScreen());
            AudioManager.PlayMusic("music_menu", 0.8f);
            AudioManager.PlayAmbience("amb_drone", 0.4f);
            if (Updater.LastUpdateFailed) UIRoot.Push(new MessageDialog("UPDATE FAILED", "The new version couldn't be copied into the game folder (a file may have been in use). It will try again next time you start the game."));
            // First launch: the setup screen (gore, voice, age, chat) comes first, then the update check.
            if (!Settings.Data.setupDone) UIRoot.Push(new SetupScreen(CheckForUpdates));
            else CheckForUpdates();
        }

        /// <summary>Auto-update: looks for a newer version in the background; the update screen takes over if there is one.</summary>
        void CheckForUpdates()
            => StartCoroutine(Updater.Check(() => { if (State == GameState.Menu && !UIRoot.Contains<UpdateScreen>() && !UIRoot.Contains<SetupScreen>()) UIRoot.Push(new UpdateScreen()); }));

        /// <param name="seed">0 = random. Multiplayer passes the host's seed so every machine builds the same map.</param>
        /// <param name="networkClient">Joining someone else's game: the host supplies Goredolls, props and nextbots.</param>
        public void StartMap(MapDef map, MapOptions options, int seed = 0, bool networkClient = false)
        {
            if (loading != null) { StopCoroutine(loading); loading = null; }
            if (!networkClient && !map.IsUnlocked()) { AudioManager.Ui("ui_error"); return; }
            if (!networkClient && !NetState.IsHost && NetState.Active) NetManager.I.Shutdown("left");
            // Maps may adjust their options (Arena turns off starting NPCs): give them a copy, not the menu's object.
            options = options == null ? new MapOptions() : new MapOptions { Time = options.Time, StartingNpcs = options.StartingNpcs, Loadout = options.Loadout, Nextbots = options.Nextbots, AiIgnore = options.AiIgnore, Weather = options.Weather };
            loading = StartCoroutine(LoadMap(map, options, seed == 0 ? Random.Range(1, int.MaxValue) : seed, networkClient));
        }

        public void RestartMap()
        {
            if (CurrentMap == null) return;
            if (NetState.IsClient) { GameEvents.ShowToast("Only the host can restart the map."); return; }
            if (NetState.IsHost)
            {
                // Clients are rebuilt by re-joining; simplest robust approach is a fresh session on the same settings.
                var s = NetManager.I.Server; var m = CurrentMap; var o = CurrentOptions;
                NetManager.I.Shutdown("restart");
                NetManager.I.StartHost(s, m, o);
                return;
            }
            StartMap(CurrentMap, CurrentOptions);
        }

        IEnumerator LoadMap(MapDef def, MapOptions opt, int seed, bool networkClient)
        {
            State = GameState.Loading;
            AudioManager.Ui("ui_success");
            var ls = UIRoot.Replace(new LoadingScreen { MapName = def.Name, Thumb = MapThumbs.Get(def), Status = "Clearing old world...", Progress = 0.02f });
            AudioManager.StopMusic();
            yield return null;
            UnloadWorld();
            MenuBackdrop.DestroyBackdrop();
            slowMoUntil = 0f;
            Rules.AiIgnorePlayer = opt.AiIgnore;
            CurrentMap = def;
            CurrentOptions = opt;
            CurrentSeed = seed;
            yield return null;

            ls.Status = "Building " + def.Name + "..."; ls.Progress = 0.08f;
            yield return null;
            var world = WorldRoot.Create(def, opt);
            // Seeded so every player in a multiplayer game builds an identical map (same props, same ids).
            var randomState = Random.state;
            Random.InitState(seed);
            MapLighting.Apply(opt.Time, def.Indoor, world.transform);
            Random.InitState(seed); // re-seed: lighting may use randomness that differs per player (easter eggs)
            var b = new MapBuilder(world.transform, def, opt);
            try { def.Build(b); }
            catch (System.Exception e) { Debug.LogException(e); }
            Random.state = randomState;
            b.Finish();
            if (NetState.Active) NetManager.I.OnMapBuilt(world.transform);
            else StoryRunner.OnMapBuilt(world.transform); // Story/StoryRunner.cs: chapter missions + stories
            world.Builder = b;
            world.PlayerSpawn = b.PlayerSpawn;
            world.PlayerYaw = b.PlayerYaw;
            world.KillY = b.KillY;
            world.Playable = b.Playable;
            Physics.SyncTransforms();
            ls.Progress = 0.25f;
            yield return null;

            if (!networkClient)
            {
                ls.Status = "Mapping walkable ground for the AI...";
                NavGrid.Current = new NavGrid(b.Playable, Mathf.Max(0.25f, def.NavCell));
                yield return NavGrid.Current.Build(b.Playable.max.y, b.Playable.min.y, p => ls.Progress = 0.25f + p * 0.5f);
            }

            ls.Status = networkClient ? "Syncing with the host..." : "Waking up the Goredolls..."; ls.Progress = 0.78f;
            yield return null;
            for (int i = 0; i < (networkClient ? 0 : opt.StartingNpcs); i++)
            {
                Vector3 pos;
                if (b.NpcSpawns.Count > 0) pos = b.NpcSpawns[i % b.NpcSpawns.Count] + (i >= b.NpcSpawns.Count ? new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f)) : Vector3.zero);
                else if (!NavGrid.Current.RandomPoint(b.PlayerSpawn, 30f, out pos)) pos = b.PlayerSpawn + new Vector3(Random.Range(-8f, 8f), 0, Random.Range(6f, 14f));
                GoredollFactory.Spawn(pos, Random.Range(0f, 360f));
                ls.Progress = 0.78f + 0.15f * (i + 1) / Mathf.Max(1, opt.StartingNpcs);
                if (i % 2 == 1) yield return null;
            }

            ls.Status = "Spawning you..."; ls.Progress = 0.95f;
            yield return null;
            var spawnPos = b.PlayerSpawn;
            if (NetState.Active && NetManager.I.LocalId > 1) spawnPos += Quaternion.Euler(0, NetManager.I.LocalId * 47f, 0) * Vector3.forward * 1.6f; // don't stack players
            var player = PlayerController.Create(spawnPos, b.PlayerYaw, world.transform);
            if (opt.Loadout == 1 && !NetState.Active) { player.Inventory.GiveAll(); player.Inventory.Select(0, true); }
            if (NetState.IsHost && NetManager.I.Server.Loadout == 1) foreach (var id in new[] { "knife", "glock", "uzi", "ar15", "ar17", "shotgun", "sniper" }) player.Inventory.Give(id, -1, false);
            if (NetState.Active) player.AddLocalHitbox();
            EasterEggs.OnMapStarted();
            if (opt.Nextbots && !networkClient)
            {
                for (int i = 0; i < 2; i++)
                    if (NavGrid.Current.RandomPoint(b.PlayerSpawn, 60f, out var p) && Vector3.Distance(p, b.PlayerSpawn) > 20f)
                        Nextbots.Spawn(Nextbots.Defs[Random.Range(0, Nextbots.Defs.Count)].Id, p);
            }
            AudioManager.PlayAmbience(def.Ambience, 0.7f);
            Weather.Begin(def, opt, world.transform); // World/Weather.cs (rain, snow, fog, thunder)
            ls.Progress = 1f;
            yield return new WaitForSecondsRealtime(0.25f);

            while (UIRoot.Count > 0) UIRoot.Pop();
            State = GameState.Playing;
            Stats.Add("maps_played");
            Hud.ShowMapTitle(def.Name, def.Description);
            if (NetState.Active)
            {
                NetManager.I.OnMapLoaded();
                if (Settings.Data.tutorialHints) GameEvents.ShowToast(NetState.IsHost ? "You're hosting. Esc > Players to kick, ban or give Creative mode. T: chat · " + VoiceChat.KeyHint : "T: chat · " + VoiceChat.KeyHint + " · Tab: players");
            }
            else if (Settings.Data.tutorialHints && def.Id != BossRealm.MapId && !StoryMode.Active && !StoryRunner.Active) GameEvents.ShowToast("Esc: game settings · Esc with the Spawn Gun (2): spawn menu · V: noclip · F5: dev panel");
            Debug.Log($"Loaded map '{def.Id}' (seed {seed}) with {opt.StartingNpcs} Goredolls{(NavGrid.Current != null ? ", " + NavGrid.Current.WalkableCount + " walkable cells" : "")}.");
            loading = null;
        }

        void UnloadWorld()
        {
            NetEntity.SuppressDespawn = true; // tearing down the whole world is not "despawning" things for clients
            if (WorldRoot.Current != null)
            {
                WorldRoot.Current.ClearDynamic();
                DestroyImmediate(WorldRoot.Current.gameObject);
            }
            NetEntity.ClearAll();
            NetEntity.SuppressDespawn = false;
            foreach (var d in Goredoll.All.ToArray()) if (d != null) d.Despawn();
            Goredoll.All.Clear();
            NavGrid.Current = null;
            Decals.ClearAll();
            Debris.ClearAll();
            FX.Clear();
            BloodFX.Clear();
            SpawnGun.UndoStack.Clear();
        }

        public void ReturnToMenu()
        {
            if (loading != null) { StopCoroutine(loading); loading = null; }
            if (NetState.Active) NetManager.I.Shutdown("left");
            UnloadWorld();
            CurrentMap = null;
            Rules.ResetAll();
            State = GameState.Menu;
            MenuBackdrop.Create();
            UIRoot.Replace(new MainMenuScreen());
            AudioManager.PlayMusic("music_menu", 0.8f);
            AudioManager.PlayAmbience("amb_drone", 0.4f);
        }

        public void Quit()
        {
            UIRoot.Replace(new QuitScreen());
        }

        // ------------------------------------------------------------------ helpers used by maps/UI

        public Goredoll SpawnGoredollAt(Vector3 pos, string preset = "Normal", int variant = -1)
        {
            if (NetState.ClientIntercepts)
            {
                if (NetState.LocalCreative) NetManager.I.RequestSpawn(SpawnCatalog.AI[0], pos, Quaternion.identity);
                return null;
            }
            var yaw = PlayerController.Instance != null ? Quaternion.LookRotation(MathX.Flat(PlayerController.Instance.transform.position - pos) + Vector3.forward * 0.001f).eulerAngles.y : Random.Range(0f, 360f);
            var d = GoredollFactory.Spawn(pos, yaw, preset, variant);
            AudioManager.Play("sg_spawn", pos, 0.6f);
            return d;
        }

        /// <summary>Temporarily slows time (kill cam style). Ignored while paused.</summary>
        public void SlowMo(float scale, float durationRealtime)
        {
            slowMoScale = Mathf.Clamp(scale, 0.05f, 1f);
            slowMoUntil = Time.unscaledTime + durationRealtime;
        }

        public void Screenshot()
        {
            var name = "GoreSandbox_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
            var path = System.IO.Path.Combine(Application.persistentDataPath, name);
            ScreenCapture.CaptureScreenshot(path);
            AudioManager.Ui("ui_success");
            GameEvents.ShowToast("Screenshot saved: " + path);
            Stats.Add("screenshots");
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            Fps = Mathf.Lerp(Fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.05f);
            Debris.Tick();

            // ----- global hotkeys
            if (Input.GetKeyDown(KeyCode.F5)) DevPanel.Toggle();
            if (!UIRoot.DevPanelCapturing)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) OnEscape();
                if (Input.GetKeyDown(KeyCode.F2)) Screenshot();
                if (Input.GetKeyDown(KeyCode.F3)) { HudHidden = !HudHidden; GameEvents.ShowToast(HudHidden ? "HUD hidden (F3)" : "HUD visible"); }
                if (Input.GetKeyDown(KeyCode.F11)) { Settings.Data.fullscreen = !Settings.Data.fullscreen; Settings.Save(); }
                if (State == GameState.Playing && !UIRoot.AnyBlocking)
                {
                    if (Input.GetKeyDown(KeyCode.Tab)) UIRoot.Push(new ScoreboardScreen());
                    if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.Return)) UIRoot.Push(new ChatScreen()); // singleplayer too (type /commands)
                    if (Input.GetKeyDown(KeyCode.F1)) UIRoot.Push(new HelpScreen());
                    if (Input.GetKeyDown(KeyCode.Q) && Input.GetKey(KeyCode.LeftControl)) UIRoot.Push(new SpawnMenuScreen());
                    BulletTime.HandleKeys(); // B / K bullet time
                    if (Input.GetKeyDown(KeyCode.P)) { if (OWDirector.Active) PhoneScreen.Toggle(); else PhotoMode.Toggle(); } // Open World: P = phone
                }
            }

            // ----- time scale (pause > slow-mo > rules)
            float scale = Rules.TimeScale;
            if (Time.unscaledTime < slowMoUntil) scale *= Mathf.Lerp(slowMoScale, 1f, Mathf.Pow(1f - (slowMoUntil - Time.unscaledTime) / 1.2f, 3f));
            scale *= BulletTime.Step(); // 1 unless bullet time (singleplayer only)
            // Multiplayer never pauses: other players keep playing while your menu is open.
            bool paused = !NetState.Active && (UIRoot.AnyPausesTime || State == GameState.Loading);
            Time.timeScale = paused ? 0f : scale;
            if (!paused) Time.fixedDeltaTime = (1f / 60f) * Mathf.Min(1f, scale);
            AudioListener.pause = paused && State == GameState.Playing;

            // ----- cursor
            bool wantCursor = (State != GameState.Playing && !MapEditor.MouseLook) || UIRoot.CursorWanted || DevPanel.Open || Settings.TouchControlsActive;
            Cursor.lockState = wantCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = wantCursor;
        }

        /// <summary>Same as pressing Esc (used by the touch MENU button).</summary>
        public void Back() => OnEscape();

        void OnEscape()
        {
            if (DevPanel.Open) { DevPanel.Toggle(); return; }
            if (UIRoot.Count > 0) { UIRoot.HandleBack(); return; }
            if (State != GameState.Playing) return;
            if (MapEditor.TryReturnFromTest()) return; // Esc while test-playing a map from the editor
            var p = PlayerController.Instance;
            if (p != null && p.Inventory != null && p.Inventory.Current is SpawnGun) UIRoot.Push(new SpawnMenuScreen());
            else UIRoot.Push(new PauseMenuScreen());
        }

        void OnApplicationQuit()
        {
            Settings.Save();
            PlayerPrefs.Save();
        }
    }
}
