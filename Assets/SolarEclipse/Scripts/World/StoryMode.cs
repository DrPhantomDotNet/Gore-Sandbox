using System.Collections;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Story mode, chapter 1 (singleplayer only). Lives on the Alleyway map's world root when the map was started with
    /// <see cref="Begin"/>, so unloading the map (menu, retry) destroys it and <see cref="OnDestroy"/> cleans up.
    /// Flow: talk to Big Marcus -> YES: the glowing wall at the corner fades / NO: MISSION FAILED -> walk round the
    /// corner -> Z: "You think you can kill me ashore?!" -> boss fight (stage 1 brawler, stage 2 enraged gunman + 2 thugs)
    /// -> Z down -> MISSION PASSED -> main menu -> Orbie's ending line. Dying = MISSION FAILED (RETRY / MENU).
    /// </summary>
    public sealed class StoryMode : MonoBehaviour
    {
        public static StoryMode I { get; private set; }
        public static bool Active => I != null;
        /// <summary>Set by <see cref="Begin"/>; consumed by AlleywayMap.Build.</summary>
        public static bool Pending;
        static float endedAt = -99f;

        enum Phase { Intro, TalkMarcus, GoCorner, MeetZ, Fight, Won, Failed }
        Phase phase = Phase.Intro;
        bool setup, talking;
        BigMarcus marcus;
        StoryBarrier barrier;
        ZBoss z;
        StoryTalk zTalk;
        string objective = "";
        float objectiveAt, bannerUntil, bannerAt;
        string banner, bannerSub;
        Color bannerColor;
        float shownPool = ZBoss.MaxPool;

        // Layout (set by AlleywayMap.Build).
        public Vector3 MarcusPos, ZPos, BarrierPos, ZTrigger;
        public float MarcusYaw, ZYaw, BarrierYaw, BarrierWidth = 7.2f;
        public Vector3[] ThugSpawns = new Vector3[0];

        /// <summary>Main menu "PLAY STORY".</summary>
        public static void Begin()
        {
            if (NetState.Active) { GameEvents.ShowToast("Story mode is singleplayer only."); AudioManager.Ui("ui_error"); return; }
            Pending = true;
            GameManager.I.StartMap(MapCatalog.Get(AlleywayMap.MapId), new MapOptions { Time = TimeOfDay.Night, StartingNpcs = 0, Loadout = 0, Nextbots = false, Weather = WeatherKind.Clear });
        }

        /// <summary>AlleywayMap.Build: is this load a story run? (Begin, or the pause menu's restart of a story run.)</summary>
        public static bool ConsumePending()
        {
            bool story = !NetState.Active && (Pending || Time.unscaledTime - endedAt < 1.5f);
            Pending = false;
            return story;
        }

        // ================================================================== lifecycle

        void Awake()
        {
            I = this;
            Rules.ResetAll();
            UIRoot.HudLayer += DrawHud;
        }

        void OnDestroy()
        {
            UIRoot.HudLayer -= DrawHud;
            if (I == this) I = null;
            // A world teardown followed by a rebuild within a moment = the pause menu's "restart": stay in story mode.
            endedAt = phase == Phase.Won || phase == Phase.Failed ? -99f : Time.unscaledTime;
            Rules.ResetAll();
            if (AudioManager.I != null) AudioManager.StopMusic();
        }

        /// <summary>Called by AlleywayMap.Build after the geometry: spawns the cast.</summary>
        public void SpawnCast(Transform root)
        {
            marcus = BigMarcus.Spawn(root, MarcusPos, MarcusYaw);
            marcus.Talk.CanTalk = () => !talking && (phase == Phase.TalkMarcus || phase == Phase.GoCorner);
            marcus.Talk.OnTalk = () => StartCoroutine(phase == Phase.GoCorner ? MarcusAgain() : MarcusTalk());
            barrier = StoryBarrier.Create(root, BarrierPos, BarrierYaw, BarrierWidth, 4.2f);
        }

        void SpawnZ()
        {
            if (z != null) return;
            try
            {
                z = ZBoss.Spawn(ZPos, ZYaw);
                z.OnEnraged = OnEnraged;
                z.OnDefeated = OnZDefeated;
                zTalk = z.gameObject.AddComponent<StoryTalk>();
                zTalk.Label = "Talk to Z";
                zTalk.CanTalk = () => !talking && phase == Phase.GoCorner;
                zTalk.OnTalk = () => StartCoroutine(ZTalk());
            }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        void SetupPlayer(PlayerController p)
        {
            setup = true;
            if (p.Noclip) p.SetNoclip(false);
            var inv = p.Inventory;
            inv.Strip();
            inv.Give("fists", 0, false);
            inv.Give("knife", 1, false);
            inv.Give("glock", 2, false);
            inv.Give("uzi", 3, false);
            if (WeaponCatalog.Get("mac11") != null) inv.Give("mac11", 4, false); // added by another update; only if it exists
            inv.Select(0, true);
            // Z spawns after the map is built (Goredolls need the NavGrid + a settled world).
            SpawnZ();
            phase = Phase.TalkMarcus;
            SetObjective("Talk to Big Marcus");
            ShowBanner("CHAPTER 1", "THE ALLEY", new Color(1f, 0.78f, 0.16f), 3.5f);
        }

        void Update()
        {
            var gm = GameManager.I;
            var p = PlayerController.Instance;
            if (gm == null) return;
            // Lockdown: no noclip, no spawn menu, no dev panel, no cheat rules.
            Rules.NoclipAllowed = false;
            Rules.Invincible = Rules.OneHitKill = Rules.AlwaysDismember = Rules.FreezeAi = Rules.SuperKick = Rules.ExplosiveBullets = false;
            Rules.DamageMultiplier = Rules.NpcHealthMultiplier = Rules.PlayerSpeedMultiplier = 1f;
            if (phase != Phase.Won) Rules.AiIgnorePlayer = false;
            if (Rules.TimeScale != 1f) Rules.TimeScale = 1f;
            if (p != null && p.Noclip) p.SetNoclip(false);
            var sm = UIRoot.Find<SpawnMenuScreen>();
            if (sm != null) { UIRoot.Remove(sm); AudioManager.Ui("ui_error"); GameEvents.ShowToast("No spawning in story mode."); }
            if (DevPanel.Open) DevPanel.Toggle();

            if (!setup && p != null && p.Cam != null && gm.State == GameState.Playing) SetupPlayer(p);
            if (!setup || p == null) return;
            // Keep the Spawn Gun away (pause menu loadouts etc.).
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                var w = p.Inventory.Slots[i];
                if (w != null && w.Def != null && (w.Def.Kind == WeaponKind.SpawnGun || w.Def.Kind == WeaponKind.Admin)) p.Inventory.Remove(i);
            }

            if (phase != Phase.Won && phase != Phase.Failed && p.IsDead) { Fail("You died.", 1.4f); return; }

            if (phase == Phase.GoCorner && !talking && z != null)
            {
                var to = MathX.Flat(z.Doll.Position - p.Position);
                if (to.magnitude < 9f || MathX.Flat(p.Position - ZTrigger).magnitude < 3f) StartCoroutine(ZTalk());
            }
            if (z != null) shownPool = Mathf.MoveTowards(shownPool, z.Pool, Time.deltaTime * ZBoss.MaxPool * 0.6f);
        }

        // ================================================================== story

        IEnumerator Say(string who, string text, Color? col = null)
        {
            var d = UIRoot.Push(new StoryDialog(who, text, null, null, col));
            while (!d.Closed) yield return null;
            yield return new WaitForSecondsRealtime(0.15f);
        }

        IEnumerator MarcusTalk()
        {
            if (talking || phase != Phase.TalkMarcus) yield break;
            talking = true;
            int c = -1;
            UIRoot.Push(new StoryDialog(BigMarcus.Name, "You think you got what it takes to kill this guy?", new[] { "YES", "NO" }, i => c = i));
            while (c < 0) yield return null;
            if (c == 0)
            {
                yield return Say(BigMarcus.Name, "Okay then, he's right there by the corner.");
                if (barrier != null) barrier.Dissolve();
                phase = Phase.GoCorner;
                SetObjective("Go to the corner");
            }
            else
            {
                yield return Say(BigMarcus.Name, "Then fuck off.");
                talking = false;
                Fail("Big Marcus told you to get lost.", 0.2f);
                yield break;
            }
            talking = false;
        }

        IEnumerator MarcusAgain()
        {
            if (talking) yield break;
            talking = true;
            yield return Say(BigMarcus.Name, "Okay then, he's right there by the corner.");
            talking = false;
        }

        IEnumerator ZTalk()
        {
            if (talking || phase != Phase.GoCorner || z == null) yield break;
            talking = true;
            phase = Phase.MeetZ;
            var red = new Color(1f, 0.3f, 0.25f);
            int c = -1;
            UIRoot.Push(new StoryDialog(ZBoss.Name, "You think you can kill me ashore?!", new[] { "YES", "NO" }, i => c = i, red));
            while (c < 0) yield return null;
            if (c == 1) yield return Say(ZBoss.Name, "Too late.", red);
            talking = false;
            StartFight();
        }

        void StartFight()
        {
            phase = Phase.Fight;
            if (zTalk != null) Destroy(zTalk);
            zTalk = null;
            if (marcus != null && marcus.Talk != null) Destroy(marcus.Talk); // nothing more to say
            if (z != null) z.BeginFight();
            SetObjective("Kill Z");
            ShowBanner("KILL Z", null, new Color(1f, 0.25f, 0.2f), 2.2f);
            AudioManager.PlayMusic("story_fight", 0.7f);
            AudioManager.Play2D("boss_slam", 0.6f, 1.1f, AudioBus.Sfx);
            CameraFX.Shake(0.3f);
        }

        void OnEnraged()
        {
            ShowBanner("Z IS ENRAGED!", "He pulled out a gun", new Color(1f, 0.15f, 0.1f), 3f);
            AudioManager.Play2D("story_enraged", 1f, 1f, AudioBus.Sfx);
            CameraFX.Shake(0.6f);
            CameraFX.Flash(new Color(0.6f, 0f, 0f), 0.45f);
            // He calls two of his boys.
            var p = PlayerController.Instance;
            foreach (var s in ThugSpawns)
            {
                try
                {
                    var yaw = p != null ? Quaternion.LookRotation(MathX.Flat(p.Position - s) + Vector3.forward * 0.001f).eulerAngles.y : 0f;
                    var thug = ZBoss.SpawnThug(s, yaw);
                    if (thug != null) thug.DisplayName = "Z's thug";
                }
                catch (System.Exception e) { Debug.LogWarning("Thug spawn failed: " + e.Message); }
            }
            if (ThugSpawns.Length > 0) GameEvents.ShowToast("Z called his boys!");
        }

        void OnZDefeated()
        {
            if (phase == Phase.Won || phase == Phase.Failed) return;
            phase = Phase.Won;
            SetObjective("");
            Rules.AiIgnorePlayer = true; // the thugs stop shooting: it's over
            AudioManager.StopMusic();
            if (GameManager.I != null) GameManager.I.SlowMo(0.3f, 1.6f);
            Stats.UnlockById("story_ch1");
            StartCoroutine(PassedLater());
        }

        IEnumerator PassedLater()
        {
            yield return new WaitForSecondsRealtime(2f);
            while (UIRoot.Find<PauseMenuScreen>() != null) yield return null;
            UIRoot.Push(new MissionPassedScreen("Z is dead."));
        }

        void Fail(string reason, float delay)
        {
            if (phase == Phase.Won || phase == Phase.Failed) return;
            phase = Phase.Failed;
            AudioManager.StopMusic();
            StartCoroutine(FailLater(reason, delay));
        }

        IEnumerator FailLater(string reason, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            UIRoot.Push(new MissionFailedScreen(reason, Begin));
        }

        void SetObjective(string text) { objective = text; objectiveAt = Time.unscaledTime; if (!string.IsNullOrEmpty(text)) AudioManager.Ui("ui_success", 0.4f); }

        void ShowBanner(string title, string sub, Color col, float seconds)
        {
            banner = title; bannerSub = sub; bannerColor = col;
            bannerAt = Time.unscaledTime;
            bannerUntil = Time.unscaledTime + seconds;
        }

        // ================================================================== HUD

        void DrawHud()
        {
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing || !setup || gm.HudHidden) return;
            if (UIRoot.Find<MissionPassedScreen>() != null || UIRoot.Find<MissionFailedScreen>() != null) return;
            float W = UI.VW, t = Time.unscaledTime;

            // Objective (top-left).
            if (!string.IsNullOrEmpty(objective))
            {
                float a = Mathf.Clamp01((t - objectiveAt) / 0.3f);
                float flash = Mathf.Clamp01(1f - (t - objectiveAt) / 1.2f);
                float tw = Mathf.Max(300f, UI.TextWidth(objective, 30, FontStyle.Bold) + 60f);
                var r = new Rect(24 - (1f - a) * 40f, 22, tw, 84);
                UI.Rounded(r, new Color(0f, 0f, 0f, 0.6f * a), true);
                UI.Fill(new Rect(r.x, r.y + 8, 5, r.height - 16), new Color(1f, 0.78f, 0.16f, a));
                UI.Label(new Rect(r.x + 22, r.y + 6, r.width - 30, 26), "OBJECTIVE", 17, new Color(1f, 0.78f, 0.16f, a), TextAnchor.MiddleLeft, FontStyle.Bold);
                UI.Label(new Rect(r.x + 22, r.y + 34, r.width - 30, 40), objective, 30, Color.Lerp(UI.TextColor, Color.white, flash).WithAlpha(a), TextAnchor.MiddleLeft, FontStyle.Bold);
            }

            // Name tags.
            var p = PlayerController.Instance;
            if (p != null && p.Cam != null)
            {
                if (marcus != null) NameTag(p, marcus.HeadPosition, BigMarcus.Name, UI.Accent2, phase == Phase.TalkMarcus);
                if (z != null && !z.Defeated && z.Doll != null && z.Doll.Parts.TryGetValue(PartType.Head, out var zh) && zh != null)
                    NameTag(p, zh.transform.position + Vector3.up * 0.32f, ZBoss.Name, new Color(1f, 0.3f, 0.25f), false);
            }

            // Boss bar (top-centre).
            if (z != null && (phase == Phase.Fight || phase == Phase.Won))
            {
                float bw = Mathf.Min(900f, W - 700f);
                var r = new Rect(W / 2f - bw / 2f, 26, bw, 74);
                UI.Rounded(r, new Color(0f, 0f, 0f, 0.65f), true);
                UI.Label(new Rect(r.x + 18, r.y + 4, 200, 34), ZBoss.Name, 30, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
                string stage = z.Stage >= 2 ? "ENRAGED" : "STAGE 1";
                UI.Label(new Rect(r.xMax - 220, r.y + 4, 200, 34), stage, 18, z.Stage >= 2 ? new Color(1f, 0.3f, 0.25f) : UI.Muted, TextAnchor.MiddleRight, FontStyle.Bold);
                var bar = new Rect(r.x + 18, r.y + 44, r.width - 36, 18);
                UI.Fill(bar, new Color(1f, 1f, 1f, 0.1f));
                float k = Mathf.Clamp01(z.Pool / ZBoss.MaxPool), ks = Mathf.Clamp01(shownPool / ZBoss.MaxPool);
                UI.Fill(new Rect(bar.x, bar.y, bar.width * ks, bar.height), new Color(1f, 0.85f, 0.6f, 0.6f));
                bool hit = Time.time - z.LastHitTime < 0.08f;
                UI.Fill(new Rect(bar.x, bar.y, bar.width * k, bar.height), hit ? Color.white : z.Stage >= 2 ? new Color(0.9f, 0.08f, 0.06f) : new Color(0.85f, 0.2f, 0.12f));
                UI.Fill(new Rect(bar.x + bar.width * 0.5f - 1, bar.y - 3, 2, bar.height + 6), new Color(1f, 1f, 1f, 0.6f)); // stage 2 mark
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
            if (d > 16f) return;
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0.1f) return;
            if (Physics.Linecast(cam.transform.position, world, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return;
            float x = sp.x / UI.Scale, y = (Screen.height - sp.y) / UI.Scale;
            float a = Mathf.Clamp01((16f - d) / 4f);
            float tw = UI.TextWidth(name, 24, FontStyle.Bold) + 30f;
            var r = new Rect(x - tw / 2f, y - 44, tw, 36);
            UI.Rounded(r, new Color(0f, 0f, 0f, 0.55f * a), true);
            UI.Label(r, name, 24, col.WithAlpha(a), TextAnchor.MiddleCenter, FontStyle.Bold);
            if (interactHint && d < 6f)
            {
                string hint = Settings.TouchControlsActive ? "Look at him + tap INTERACT" : "Look at him + press E";
                UI.Label(new Rect(x - 200, y - 76, 400, 30), hint, 18, new Color(1f, 1f, 1f, 0.8f * a), TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }
    }
}
