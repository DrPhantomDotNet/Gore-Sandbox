using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>How a story run started and what happens when it ends (kept across RETRY).</summary>
    public sealed class StoryContext
    {
        public StoryPack Pack;
        public int Mission = -1;                    // chapter mission number, -1 = a player-made story
        public readonly HashSet<string> Flags = new HashSet<string>();
        public Action<StoryContext> OnPassed;       // "pass" ending (null = back to the menu)
        public Action<StoryContext, StoryStep> OnTheEnd; // "end" ending (null = THE END screen, then the menu)
        public Action OnQuit;                       // after a failed run's MENU
        public Action Retry;                        // RETRY after MISSION FAILED (null = replay this context)
        public string Subtitle = "";
    }

    /// <summary>
    /// Plays a <see cref="StoryPack"/> on its map: spawns the cast, runs the steps (dialogue, choices, phone chats,
    /// cutscenes, objectives, fights, endings) and draws the story HUD. Lives on the world root, so leaving the map
    /// ends it. Singleplayer only.
    /// </summary>
    public sealed class StoryRunner : MonoBehaviour
    {
        public static StoryRunner I { get; private set; }
        public static bool Active => I != null;
        static StoryContext pending, last;
        static float endedAt = -99f;

        public StoryContext Ctx { get; private set; }
        StoryPack pack;
        bool setup, ended;
        readonly Dictionary<string, Cast> cast = new Dictionary<string, Cast>();

        sealed class Cast
        {
            public StoryActorDef Def;
            public GameObject Go;
            public StoryStatic Static;
            public Goredoll Doll;
            public StoryBoss Boss;
            public StoryTalk Talk;
            public bool Talked;
            public bool Alive => Go != null && (Doll == null || (!Doll.IsDead && (Boss == null || !Boss.Defeated)));
            public Vector3 Head
            {
                get
                {
                    if (Static != null) return Static.HeadPosition;
                    if (Doll != null && Doll.Parts.TryGetValue(PartType.Head, out var h) && h != null) return h.transform.position + Vector3.up * 0.3f;
                    return Go != null ? Go.transform.position + Vector3.up * 2f : Vector3.zero;
                }
            }
        }

        // HUD state
        string objective = "", banner, bannerSub;
        float objectiveAt, bannerAt, bannerUntil;
        Color bannerColor;
        readonly List<(string who, string text, float at)> texts = new List<(string, string, float)>();
        Vector3? marker;
        float markerRadius;
        GameObject beacon;
        List<Cast> killTargets;
        int killTotal;
        StoryBoss shownBoss;
        float shownPool;

        // Cutscene
        Camera cutCam;
        CutsceneScreen cutScreen;
        bool savedHud;

        // ================================================================== starting

        /// <summary>Loads the story's map and runs it.</summary>
        public static void Play(StoryContext ctx)
        {
            if (ctx == null || ctx.Pack == null) return;
            if (NetState.Active) { GameEvents.ShowToast("Stories are singleplayer only."); AudioManager.Ui("ui_error"); return; }
            pending = ctx;
            var map = MapCatalog.Get(ctx.Pack.map);
            var time = ctx.Pack.time >= 0 ? (TimeOfDay)ctx.Pack.time : map.DefaultTime;
            GameManager.I.StartMap(map, new MapOptions { Time = time, StartingNpcs = 0, Loadout = 0, Nextbots = false, Weather = WeatherKind.Clear });
        }

        /// <summary>Called by GameManager right after a map is built.</summary>
        public static void OnMapBuilt(Transform world)
        {
            var ctx = pending;
            pending = null;
            if (ctx == null && last != null && Time.unscaledTime - endedAt < 1.5f) ctx = last; // pause menu "restart"
            if (ctx == null || NetState.Active || world == null) return;
            if (GameManager.I.CurrentMap == null || GameManager.I.CurrentMap.Id != ctx.Pack.map) return;
            var r = world.gameObject.AddComponent<StoryRunner>();
            r.Ctx = ctx;
            r.pack = ctx.Pack;
            last = ctx;
        }

        /// <summary>Starting NPCs and nextbots are off for story loads (GameManager asks before spawning them).</summary>
        public static bool Loading => pending != null;

        void Awake()
        {
            I = this;
            Rules.ResetAll();
            UIRoot.HudLayer += DrawHud;
        }

        void OnDestroy()
        {
            UIRoot.HudLayer -= DrawHud;
            EndCutscene();
            if (beacon != null) Destroy(beacon);
            if (I == this) I = null;
            endedAt = ended ? -99f : Time.unscaledTime;
            Rules.ResetAll();
            if (AudioManager.I != null) AudioManager.StopMusic();
        }

        void Update()
        {
            var gm = GameManager.I;
            var p = PlayerController.Instance;
            if (gm == null) return;
            Rules.NoclipAllowed = false;
            Rules.Invincible = Rules.OneHitKill = Rules.AlwaysDismember = Rules.FreezeAi = Rules.SuperKick = Rules.ExplosiveBullets = false;
            Rules.DamageMultiplier = Rules.NpcHealthMultiplier = Rules.PlayerSpeedMultiplier = 1f;
            if (Rules.TimeScale != 1f) Rules.TimeScale = 1f;
            // Nobody shoots you during cutscenes and conversations (or after the end).
            Rules.AiIgnorePlayer = ended || cutCam != null || UIRoot.Find<StoryDialog>() != null || UIRoot.Find<StoryFadeScreen>() != null;
            if (p != null && p.Noclip) p.SetNoclip(false);
            var sm = UIRoot.Find<SpawnMenuScreen>();
            if (sm != null) { UIRoot.Remove(sm); AudioManager.Ui("ui_error"); GameEvents.ShowToast("No spawning in story mode."); }
            if (DevPanel.Open) DevPanel.Toggle();

            if (!setup && p != null && p.Cam != null && gm.State == GameState.Playing) Setup(p);
            if (!setup || p == null) return;
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                var w = p.Inventory.Slots[i];
                if (w != null && w.Def != null && (w.Def.Kind == WeaponKind.SpawnGun || w.Def.Kind == WeaponKind.Admin)) p.Inventory.Remove(i);
            }
            if (!ended && p.IsDead) Fail("You died.", 1.4f);
            if (shownBoss != null) shownPool = Mathf.MoveTowards(shownPool, shownBoss.Pool, Time.deltaTime * shownBoss.MaxPool * 0.6f);
            if (beacon != null) beacon.transform.Rotate(0, 60f * Time.deltaTime, 0);
        }

        void Setup(PlayerController p)
        {
            setup = true;
            if (p.Noclip) p.SetNoclip(false);
            var inv = p.Inventory;
            inv.Strip();
            int slot = 0;
            foreach (var w in pack.weapons) if (StoryRules.WeaponAllowed(w) && slot < Inventory.SlotCount) inv.Give(w, slot++, false);
            if (slot == 0) inv.Give("fists", 0, false);
            inv.Select(0, true);
            foreach (var a in pack.actors) if (a.atStart) SpawnActor(a);
            StartCoroutine(Run());
        }

        // ================================================================== cast

        Cast SpawnActor(StoryActorDef a)
        {
            if (a == null) return null;
            if (cast.TryGetValue(a.id, out var old) && old.Go != null) return old;
            var c = new Cast { Def = a };
            try
            {
                var look = CustomLook.Decode(a.look) ?? LookFromSeed(a);
                look.Name = CustomLook.CleanName(a.name);
                switch (a.Role)
                {
                    case ActorRole.Talker:
                        if (a.look == "@marcus")
                        {
                            var bm = BigMarcus.Spawn(transform, a.pos, a.yaw);
                            c.Go = bm.gameObject; c.Talk = bm.Talk;
                        }
                        else
                        {
                            c.Static = StoryStatic.Spawn(transform, a.name, look, a.pos, a.yaw);
                            c.Go = c.Static.gameObject; c.Talk = c.Static.Talk;
                        }
                        break;
                    case ActorRole.Boss:
                        c.Boss = StoryBoss.Spawn(a.name, look, a.pos, a.yaw, a.health);
                        c.Doll = c.Boss.Doll; c.Go = c.Doll.gameObject;
                        break;
                    default:
                    {
                        string preset;
                        switch (a.Role)
                        {
                            case ActorRole.Gunman: preset = ArmedDolls.Gunman; break;
                            case ActorRole.Soldier: preset = ArmedDolls.Soldier; look.Armor = Mathf.Max(look.Armor, 2); break;
                            case ActorRole.Brawler: preset = "Aggressive"; break;
                            case ActorRole.Zombie: preset = Zombies.PresetPrefix + "Walker"; break;
                            default: preset = "Passive"; break;
                        }
                        bool custom = a.Role != ActorRole.Zombie && !string.IsNullOrEmpty(a.look);
                        var d = GoredollFactory.Spawn(a.pos, a.yaw, custom ? preset + "~c" + look.Encode() : preset, -1, null, Mathf.Max(1, a.seed));
                        d.DisplayName = a.name;
                        var sp = d.GetComponent<Spawned>();
                        if (sp != null) sp.DisplayName = a.name;
                        if (a.Role == ActorRole.Civilian && d.Brain != null) d.Brain.enabled = false; // stays put for the story
                        c.Doll = d; c.Go = d.gameObject;
                        break;
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[Story] couldn't spawn '" + a.name + "': " + e.Message); }
            cast[a.id] = c;
            return c;
        }

        static CustomLook LookFromSeed(StoryActorDef a)
        {
            var r = new System.Random(a.seed * 7919 + a.id.GetHashCode());
            Color C(float lo, float hi) => new Color(lo + (float)r.NextDouble() * (hi - lo), lo + (float)r.NextDouble() * (hi - lo), lo + (float)r.NextDouble() * (hi - lo));
            Color[] skins = { new Color(0.98f, 0.86f, 0.78f), new Color(0.91f, 0.72f, 0.58f), new Color(0.72f, 0.52f, 0.38f), new Color(0.5f, 0.34f, 0.24f), new Color(0.33f, 0.22f, 0.15f) };
            return new CustomLook
            {
                Skin = skins[r.Next(skins.Length)], Shirt = C(0.05f, 0.85f), Pants = C(0.05f, 0.45f), Shoes = C(0.02f, 0.3f), Hair = C(0.02f, 0.4f),
                HairStyle = r.Next(4), Beard = r.NextDouble() < 0.3, LongSleeves = r.NextDouble() < 0.5, Glasses = r.NextDouble() < 0.15,
                Height = 0.92f + (float)r.NextDouble() * 0.16f, Build = 0.85f + (float)r.NextDouble() * 0.4f,
            };
        }

        Cast Find(string id) => id != null && cast.TryGetValue(id, out var c) ? c : null;

        Color SpeakerColor(string who)
        {
            foreach (var c in cast.Values)
                if (c.Def.name == who) return c.Def.Hostile ? new Color(1f, 0.3f, 0.25f) : c.Def.Role == ActorRole.Talker ? UI.Accent2 : new Color(0.55f, 0.8f, 1f);
            return who == "You" ? new Color(0.6f, 0.9f, 0.6f) : UI.Accent2;
        }

        // ================================================================== running

        int FindLabel(string label, int fallback)
        {
            if (string.IsNullOrEmpty(label)) return fallback;
            for (int i = 0; i < pack.steps.Count; i++) if (pack.steps[i].label == label) return i;
            Debug.LogWarning("[Story] no label '" + label + "'");
            return fallback;
        }

        IEnumerator Run()
        {
            int i = 0, guard = 0;
            while (!ended && i < pack.steps.Count)
            {
                var s = pack.steps[i];
                int next = i + 1;
                if (++guard > 5000) { Debug.LogWarning("[Story] too many jumps, stopping"); break; }
                if (s.Type != StepType.Cutscene && s.Type != StepType.Say && s.Type != StepType.Choice && s.Type != StepType.Chat && s.Type != StepType.Wait
                    && s.Type != StepType.Fade && s.Type != StepType.Banner && s.Type != StepType.Music && s.Type != StepType.Spawn && s.Type != StepType.Remove
                    && s.Type != StepType.Jump && s.Type != StepType.SetFlag && s.Type != StepType.IfFlag && s.Type != StepType.Unlock)
                    EndCutscene(); // gameplay steps hand the camera back
                yield return Exec(s, n => next = n);
                // Wait while the pause menu or another screen is up (time may be paused anyway).
                while (UIRoot.Find<PauseMenuScreen>() != null) yield return null;
                i = next;
            }
            if (!ended) End(new StoryStep { type = (int)StepType.End, target = "pass", who = "MISSION PASSED", text = pack.title });
        }

        IEnumerator Exec(StoryStep s, Action<int> jump)
        {
            var p = PlayerController.Instance;
            switch (s.Type)
            {
                case StepType.Say:
                {
                    var d = UIRoot.Push(new StoryDialog(string.IsNullOrEmpty(s.who) ? "..." : s.who, s.text, null, null, SpeakerColor(s.who), 2.6f));
                    while (!d.Closed) yield return null;
                    yield return new WaitForSecondsRealtime(0.12f);
                    break;
                }
                case StepType.Choice:
                {
                    if (s.options == null || s.options.Length == 0) break;
                    int c = -1;
                    UIRoot.Push(new StoryDialog(string.IsNullOrEmpty(s.who) ? "..." : s.who, s.text, s.options, k => c = k, SpeakerColor(s.who)));
                    while (c < 0) yield return null;
                    if (s.jumps != null && c < s.jumps.Length && !string.IsNullOrEmpty(s.jumps[c])) jump(FindLabel(s.jumps[c], pack.steps.IndexOf(s) + 1));
                    yield return new WaitForSecondsRealtime(0.12f);
                    break;
                }
                case StepType.Chat:
                    texts.Add((s.who, ChatFilter.Active ? ChatFilter.Apply(s.text) : s.text, Time.unscaledTime));
                    if (texts.Count > 3) texts.RemoveAt(0);
                    AudioManager.Ui("ui_notify", 0.9f);
                    try { NetManager.I?.AddChat("[" + s.who + "] " + s.text, new Color(0.55f, 0.85f, 1f)); } catch { }
                    yield return new WaitForSecondsRealtime(Mathf.Clamp(s.text.Length * 0.035f, 1.2f, 4f));
                    break;
                case StepType.Objective:
                    SetObjective(s.text);
                    break;
                case StepType.Banner:
                    ShowBanner(s.who, s.text, new Color(1f, 0.78f, 0.16f), 3.2f);
                    yield return new WaitForSecondsRealtime(1f);
                    break;
                case StepType.Cutscene:
                    yield return Shot(s);
                    break;
                case StepType.Wait:
                {
                    float until = Time.time + s.time;
                    while (Time.time < until) yield return null;
                    break;
                }
                case StepType.GoTo:
                {
                    SetObjective(string.IsNullOrEmpty(s.text) ? "Go to the marker" : s.text);
                    ShowMarker(s.pos, s.radius);
                    while (p != null && !ended)
                    {
                        var d = p.Position - s.pos;
                        if (MathX.Flat(d).magnitude < s.radius && Mathf.Abs(d.y) < 4f) break;
                        yield return null;
                    }
                    HideMarker();
                    break;
                }
                case StepType.Talk:
                {
                    var c = Find(s.target) ?? SpawnActor(pack.Actor(s.target));
                    if (c == null || c.Go == null) break;
                    if (c.Talk == null) { c.Talk = c.Go.AddComponent<StoryTalk>(); c.Talk.Label = "Talk to " + c.Def.name; }
                    c.Talked = false;
                    var cc = c;
                    c.Talk.CanTalk = () => !cc.Talked && !UIRoot.AnyBlocking;
                    c.Talk.OnTalk = () => cc.Talked = true;
                    SetObjective(string.IsNullOrEmpty(s.text) ? "Talk to " + c.Def.name : s.text);
                    while (!c.Talked && c.Alive && !ended) yield return null;
                    if (c.Talk != null) c.Talk.CanTalk = () => false;
                    SetObjective("");
                    break;
                }
                case StepType.Spawn:
                    SpawnActor(pack.Actor(s.target));
                    break;
                case StepType.Kill:
                {
                    killTargets = new List<Cast>();
                    if (s.target == "enemies") { foreach (var c in cast.Values) if (c.Def.Hostile && c.Alive && (c.Boss == null || c.Boss.IsAwake)) killTargets.Add(c); }
                    else { var c = Find(s.target) ?? SpawnActor(pack.Actor(s.target)); if (c != null && c.Alive) killTargets.Add(c); }
                    killTotal = killTargets.Count;
                    if (killTotal == 0) { killTargets = null; break; }
                    foreach (var c in killTargets)
                    {
                        if (c.Boss != null) { c.Boss.Wake(); shownBoss = c.Boss; shownPool = c.Boss.Pool; }
                        if (c.Static != null) { Destroy(c.Static.gameObject); } // talkers can't be hurt: they leave
                    }
                    SetObjective(string.IsNullOrEmpty(s.text) ? (killTotal == 1 ? "Kill " + killTargets[0].Def.name : "Kill them all") : s.text);
                    while (!ended && killTargets.Exists(c => c.Alive)) yield return null;
                    killTargets = null;
                    SetObjective("");
                    if (shownBoss != null && shownBoss.Defeated) { if (GameManager.I != null) GameManager.I.SlowMo(0.3f, 1.4f); yield return new WaitForSecondsRealtime(1.5f); }
                    shownBoss = null;
                    break;
                }
                case StepType.Remove:
                {
                    var c = Find(s.target);
                    if (c != null && c.Go != null)
                    {
                        if (c.Doll != null) c.Doll.Despawn();
                        else Destroy(c.Go);
                    }
                    break;
                }
                case StepType.Give:
                    if (p != null && StoryRules.WeaponAllowed(s.target)) { int slot = p.Inventory.Give(s.target, -1, true); GameEvents.ShowToast("Got: " + (WeaponCatalog.Get(s.target)?.Name ?? s.target)); }
                    break;
                case StepType.Music:
                    switch (s.target)
                    {
                        case "fight": AudioManager.PlayMusic("story_fight", 0.7f); break;
                        case "calm": AudioManager.PlayMusic("music_menu", 0.5f); break;
                        case "tense": AudioManager.PlayMusic("intro_score", 0.6f); break;
                        default: AudioManager.StopMusic(); break;
                    }
                    break;
                case StepType.Jump:
                    jump(FindLabel(s.target, pack.steps.IndexOf(s) + 1));
                    break;
                case StepType.SetFlag:
                    if (!string.IsNullOrEmpty(s.target)) Ctx.Flags.Add(s.target);
                    break;
                case StepType.IfFlag:
                    if (Ctx.Flags.Contains(s.target)) jump(FindLabel(s.text, pack.steps.IndexOf(s) + 1));
                    break;
                case StepType.Fade:
                {
                    var f = UIRoot.Push(new StoryFadeScreen(s.time, ChatFilter.Active ? ChatFilter.Apply(s.text) : s.text));
                    while (!f.Done) yield return null;
                    break;
                }
                case StepType.End:
                    End(s);
                    break;
                case StepType.Unlock:
                    if (pack.builtIn) { Chapter1.EnsureAchievements(); Stats.UnlockById(s.target); }
                    break;
                case StepType.Heal:
                    if (p != null && p.Health != null) p.Health.Current = p.Health.Max;
                    break;
            }
        }

        // ================================================================== cutscenes

        void BeginCutscene()
        {
            var p = PlayerController.Instance;
            if (cutCam != null || p == null || p.Cam == null) return;
            var go = new GameObject("StoryCutsceneCamera");
            go.transform.SetParent(transform, false);
            cutCam = go.AddComponent<Camera>();
            cutCam.CopyFrom(p.Cam);
            cutCam.cullingMask = p.Cam.cullingMask & ~(1 << Layers.Viewmodel) & ~(1 << Layers.Player);
            go.tag = "MainCamera";
            p.Cam.enabled = false;
            if (p.ViewCam != null) p.ViewCam.enabled = false;
            var gm = GameManager.I;
            savedHud = gm.HudHidden;
            gm.HudHidden = true;
            cutScreen = UIRoot.Push(new CutsceneScreen());
        }

        void EndCutscene()
        {
            if (cutCam == null) return;
            Destroy(cutCam.gameObject);
            cutCam = null;
            var p = PlayerController.Instance;
            if (p != null) { if (p.Cam != null) p.Cam.enabled = true; if (p.ViewCam != null) p.ViewCam.enabled = Settings.Data.viewmodel; }
            if (GameManager.I != null) GameManager.I.HudHidden = savedHud;
            if (cutScreen != null) { UIRoot.Remove(cutScreen); cutScreen = null; }
        }

        IEnumerator Shot(StoryStep s)
        {
            if (VRManager.Active)
            {
                // VR: moving your view for you makes people sick; show the caption instead.
                if (!string.IsNullOrEmpty(s.text)) ShowBanner("", ChatFilter.Active ? ChatFilter.Apply(s.text) : s.text, Color.white, Mathf.Max(2f, s.time));
                yield return new WaitForSecondsRealtime(Mathf.Min(s.time, 2f));
                yield break;
            }
            BeginCutscene();
            if (cutCam == null) yield break;
            cutScreen.Caption = ChatFilter.Active ? ChatFilter.Apply(s.text) : s.text;
            cutScreen.CaptionAt = Time.unscaledTime;
            cutScreen.SkipShot = false;
            float t0 = Time.unscaledTime, dur = Mathf.Max(0.2f, s.time);
            while (true)
            {
                float k = MathX.SmoothStep01((Time.unscaledTime - t0) / dur);
                var pos = Vector3.Lerp(s.pos, s.pos2, k);
                var look = Vector3.Lerp(s.look, s.look2, k);
                var dir = look - pos;
                cutCam.transform.SetPositionAndRotation(pos, dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : cutCam.transform.rotation);
                if (k >= 1f || cutScreen == null || cutScreen.SkipShot) break;
                yield return null;
            }
            if (cutScreen != null) cutScreen.Caption = null;
        }

        // ================================================================== endings

        void End(StoryStep s)
        {
            if (ended) return;
            ended = true;
            SetObjective("");
            HideMarker();
            AudioManager.StopMusic();
            Rules.AiIgnorePlayer = true;
            switch (s.target)
            {
                case "fail":
                    EndCutscene();
                    StartCoroutine(Later(0.3f, () => UIRoot.Push(new MissionFailedScreen(string.IsNullOrEmpty(s.text) ? "Mission failed." : s.text, RetryAction) { OnMenu = Ctx.OnQuit })));
                    break;
                case "end":
                    StartCoroutine(Later(0.6f, () =>
                    {
                        EndCutscene();
                        if (Ctx.OnTheEnd != null) Ctx.OnTheEnd(Ctx, s);
                        else UIRoot.Push(new TheEndScreen(s.who, s.text, null, () => { GameManager.I.ReturnToMenu(); Ctx.OnQuit?.Invoke(); }));
                    }));
                    break;
                default:
                    StartCoroutine(Later(0.8f, () =>
                    {
                        EndCutscene();
                        var ctx = Ctx;
                        UIRoot.Push(new MissionPassedScreen(string.IsNullOrEmpty(s.text) ? pack.title : s.text, ctx.Mission > 0 ? "MISSION " + ctx.Mission + " OF " + Chapter1.MissionCount + " COMPLETE" : "STORY COMPLETE",
                            () =>
                            {
                                if (ctx.OnPassed != null) ctx.OnPassed(ctx);
                                else { GameManager.I.ReturnToMenu(); ctx.OnQuit?.Invoke(); }
                            }));
                    }));
                    break;
            }
        }

        IEnumerator Later(float seconds, Action a)
        {
            yield return new WaitForSecondsRealtime(seconds);
            while (UIRoot.Find<PauseMenuScreen>() != null) yield return null;
            a();
        }

        Action RetryAction => Ctx.Retry ?? (() => Play(Ctx));

        void Fail(string reason, float delay)
        {
            if (ended) return;
            ended = true;
            AudioManager.StopMusic();
            EndCutscene();
            StartCoroutine(Later(delay, () => UIRoot.Push(new MissionFailedScreen(reason, RetryAction) { OnMenu = Ctx.OnQuit })));
        }

        // ================================================================== HUD

        void SetObjective(string text) { if (text == objective) return; objective = text ?? ""; objectiveAt = Time.unscaledTime; if (!string.IsNullOrEmpty(text)) AudioManager.Ui("ui_success", 0.4f); }

        void ShowBanner(string title, string sub, Color col, float seconds)
        {
            banner = title; bannerSub = sub; bannerColor = col;
            bannerAt = Time.unscaledTime;
            bannerUntil = Time.unscaledTime + seconds;
            AudioManager.Ui("logo_shimmer", 0.5f);
        }

        void ShowMarker(Vector3 pos, float radius)
        {
            marker = pos; markerRadius = radius;
            if (beacon == null)
            {
                beacon = new GameObject("Story Marker");
                beacon.transform.SetParent(transform, false);
                var mb = new MeshBuilder { Color = Color.white };
                mb.Cylinder(Vector3.zero, new Vector3(0, 0.05f, 0), 1f, 1f, 32);
                mb.Cylinder(new Vector3(0, 0.05f, 0), new Vector3(0, 4f, 0), 0.08f, 0.02f, 8);
                beacon.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("StoryMarker");
                var mr = beacon.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Mats.Glow(new Color(1f, 0.78f, 0.16f), 2f);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            beacon.transform.position = pos + Vector3.up * 0.03f;
            beacon.transform.localScale = new Vector3(Mathf.Min(radius, 4f) * 0.6f, 1f, Mathf.Min(radius, 4f) * 0.6f);
            beacon.SetActive(true);
        }

        void HideMarker() { marker = null; if (beacon != null) beacon.SetActive(false); }

        void DrawHud()
        {
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing || !setup) return;
            float W = UI.VW, H = UI.VH, t = Time.unscaledTime;
            bool hud = !gm.HudHidden && cutCam == null && UIRoot.Find<MissionPassedScreen>() == null && UIRoot.Find<MissionFailedScreen>() == null;

            // Phone texts (top-right), also during cutscenes.
            for (int i = texts.Count - 1; i >= 0; i--) if (t - texts[i].at > 7f) texts.RemoveAt(i);
            float ty = 120;
            foreach (var m in texts)
            {
                float a = Mathf.Clamp01((t - m.at) / 0.25f) * Mathf.Clamp01((7f - (t - m.at)) / 0.5f);
                var r = new Rect(W - 560 + (1f - Mathf.Clamp01((t - m.at) / 0.25f)) * 80f, ty, 530, 120);
                UI.Rounded(r, new Color(0.06f, 0.08f, 0.12f, 0.92f * a));
                UI.Outline(r, new Color(0.45f, 0.75f, 1f, 0.5f * a), 2);
                UI.Tex(new Rect(r.x + 16, r.y + 16, 34, 34), IconFactory.Get("chat"), new Color(0.55f, 0.85f, 1f, a), ScaleMode.ScaleToFit);
                UI.Label(new Rect(r.x + 60, r.y + 10, r.width - 80, 40), m.who.ToUpperInvariant(), 22, new Color(0.55f, 0.85f, 1f, a), TextAnchor.MiddleLeft, FontStyle.Bold);
                UI.Label(new Rect(r.x + 18, r.y + 50, r.width - 36, 66), m.text, 22, new Color(1, 1, 1, a), TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                ty += 132;
            }

            if (!hud) return;
            // Objective (top-left).
            if (!string.IsNullOrEmpty(objective))
            {
                string text = objective;
                if (killTargets != null && killTotal > 1) text += "  (" + killTargets.FindAll(c => c.Alive).Count + " left)";
                float a = Mathf.Clamp01((t - objectiveAt) / 0.3f);
                float flash = Mathf.Clamp01(1f - (t - objectiveAt) / 1.2f);
                float tw = Mathf.Max(300f, UI.TextWidth(text, 30, FontStyle.Bold) + 60f);
                var r = new Rect(24 - (1f - a) * 40f, 22, Mathf.Min(tw, W * 0.5f), 84);
                UI.Rounded(r, new Color(0f, 0f, 0f, 0.6f * a), true);
                UI.Fill(new Rect(r.x, r.y + 8, 5, r.height - 16), new Color(1f, 0.78f, 0.16f, a));
                UI.Label(new Rect(r.x + 22, r.y + 6, r.width - 30, 26), "OBJECTIVE", 17, new Color(1f, 0.78f, 0.16f, a), TextAnchor.MiddleLeft, FontStyle.Bold);
                UI.Label(new Rect(r.x + 22, r.y + 34, r.width - 30, 40), text, 30, Color.Lerp(UI.TextColor, Color.white, flash).WithAlpha(a), TextAnchor.MiddleLeft, FontStyle.Bold);
            }

            var p = PlayerController.Instance;
            if (p != null && p.Cam != null)
            {
                // Name tags of the cast.
                foreach (var c in cast.Values)
                {
                    if (!c.Alive || (c.Def.Hostile && c.Boss == null && (killTargets == null || !killTargets.Contains(c)))) continue;
                    bool talk = c.Talk != null && c.Talk.CanTalk != null && c.Talk.CanTalk();
                    NameTag(p, c.Head, c.Def.name, c.Def.Hostile ? new Color(1f, 0.3f, 0.25f) : c.Def.Role == ActorRole.Talker ? UI.Accent2 : new Color(0.55f, 0.8f, 1f), talk);
                }
                // Marker arrow / distance.
                if (marker.HasValue)
                {
                    var sp = p.Cam.WorldToScreenPoint(marker.Value + Vector3.up * 1.5f);
                    float dist = Vector3.Distance(p.Position, marker.Value);
                    float x = sp.x / UI.Scale, y = (Screen.height - sp.y) / UI.Scale;
                    if (sp.z < 0) { x = W - x; y = H - 80; }
                    x = Mathf.Clamp(x, 60, W - 60); y = Mathf.Clamp(y, 120, H - 120);
                    float pulse = 0.75f + 0.25f * Mathf.Sin(t * 5f);
                    UI.Rounded(new Rect(x - 18, y - 18, 36, 36), new Color(1f, 0.78f, 0.16f, pulse), true);
                    UI.Label(new Rect(x - 100, y + 20, 200, 30), Mathf.RoundToInt(dist) + " m", 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                }
            }

            // Boss bar (top-centre).
            if (shownBoss != null && shownBoss.Doll != null)
            {
                float bw = Mathf.Min(900f, W - 700f);
                var r = new Rect(W / 2f - bw / 2f, 26, bw, 74);
                UI.Rounded(r, new Color(0f, 0f, 0f, 0.65f), true);
                UI.Label(new Rect(r.x + 18, r.y + 4, bw - 260, 34), shownBoss.Doll.DisplayName, 30, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
                UI.Label(new Rect(r.xMax - 220, r.y + 4, 200, 34), shownBoss.Enraged ? "ENRAGED" : "BOSS", 18, shownBoss.Enraged ? new Color(1f, 0.3f, 0.25f) : UI.Muted, TextAnchor.MiddleRight, FontStyle.Bold);
                var bar = new Rect(r.x + 18, r.y + 44, r.width - 36, 18);
                UI.Fill(bar, new Color(1f, 1f, 1f, 0.1f));
                float k = Mathf.Clamp01(shownBoss.Pool / shownBoss.MaxPool), ks = Mathf.Clamp01(shownPool / shownBoss.MaxPool);
                UI.Fill(new Rect(bar.x, bar.y, bar.width * ks, bar.height), new Color(1f, 0.85f, 0.6f, 0.6f));
                bool hit = Time.time - shownBoss.LastHitTime < 0.08f;
                UI.Fill(new Rect(bar.x, bar.y, bar.width * k, bar.height), hit ? Color.white : shownBoss.Enraged ? new Color(0.9f, 0.08f, 0.06f) : new Color(0.85f, 0.2f, 0.12f));
            }

            // Banner (centre).
            if (!string.IsNullOrEmpty(banner) && t < bannerUntil)
            {
                float age = t - bannerAt;
                float a = Mathf.Clamp01(age / 0.2f) * Mathf.Clamp01((bannerUntil - t) / 0.5f);
                int size = Mathf.RoundToInt(Mathf.Lerp(150f, 110f, MathX.SmoothStep01(age / 0.3f)));
                float y = UI.VH * 0.26f;
                UI.Label(new Rect(4, y + 5, W, 150), banner, size, new Color(0, 0, 0, 0.75f * a), TextAnchor.MiddleCenter, FontStyle.Normal, false, false, UI.HeavyFont);
                UI.Label(new Rect(0, y, W, 150), banner, size, bannerColor.WithAlpha(a), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
                if (!string.IsNullOrEmpty(bannerSub)) UI.Label(new Rect(0, y + 140, W, 50), bannerSub, 34, new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }

        static void NameTag(PlayerController p, Vector3 world, string name, Color col, bool interactHint)
        {
            var cam = p.Cam;
            float d = Vector3.Distance(cam.transform.position, world);
            if (d > 18f) return;
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0.1f) return;
            if (Physics.Linecast(cam.transform.position, world, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return;
            float x = sp.x / UI.Scale, y = (Screen.height - sp.y) / UI.Scale;
            float a = Mathf.Clamp01((18f - d) / 4f);
            float tw = UI.TextWidth(name, 24, FontStyle.Bold) + 30f;
            var r = new Rect(x - tw / 2f, y - 44, tw, 36);
            UI.Rounded(r, new Color(0f, 0f, 0f, 0.55f * a), true);
            UI.Label(r, name, 24, col.WithAlpha(a), TextAnchor.MiddleCenter, FontStyle.Bold);
            if (interactHint && d < 6f)
            {
                string hint = Settings.TouchControlsActive ? "Look at them + tap INTERACT" : "Look at them + press E";
                UI.Label(new Rect(x - 220, y - 76, 440, 30), hint, 18, new Color(1f, 1f, 1f, 0.8f * a), TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }
    }
}
