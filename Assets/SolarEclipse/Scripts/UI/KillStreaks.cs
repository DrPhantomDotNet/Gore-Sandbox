using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Kill streak announcer. Counts the local player's kills (Goredolls, zombies: anything that bumps Stats.Kills;
    /// on a multiplayer client, kill-feed lines that start with our own name) and shows a big punchy banner:
    /// multi-kills within 3 s (DOUBLE KILL ... MONSTER KILL) and streaks without dying (KILLING SPREE at 5 ...
    /// GODLIKE at 25), with a synthesized stinger per tier, plus a small HEADSHOT popup on headshot kills.
    /// Self-contained: polls stats and listens to GameEvents, draws on UIRoot.HudLayer. Toggle: Settings "Kill streak announcer".
    /// </summary>
    public sealed class KillStreaks : MonoBehaviour
    {
        public static KillStreaks I { get; private set; }

        const float MultiWindow = 3f;
        static readonly string[] MultiNames = { null, null, "DOUBLE KILL", "TRIPLE KILL", "MULTI KILL", "MEGA KILL", "ULTRA KILL", "MONSTER KILL", "LUDICROUS KILL", "WICKED SICK" };
        static readonly int[] StreakAt = { 5, 10, 15, 20, 25 };
        static readonly string[] StreakNames = { "KILLING SPREE", "RAMPAGE", "UNSTOPPABLE", "DOMINATING", "GODLIKE" };

        public int Streak { get; private set; }
        int multi;
        float lastKillTime = -99f;
        float lastKillStat, lastHeadStat, lastHeadTime = -99f;
        PlayerController trackedPlayer;
        bool wasDead;

        // Banner state
        string bannerText, bannerSub;
        Color bannerColor;
        float bannerTime = -99f;
        float headshotTime = -99f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("KillStreaks");
            DontDestroyOnLoad(go);
            go.AddComponent<KillStreaks>();
        }

        void Awake()
        {
            I = this;
            EnsureAchievements();
            lastKillStat = Stats.Get(Stats.Kills);
            lastHeadStat = Stats.Get(Stats.Headshots);
        }

        void OnEnable() { UIRoot.HudLayer += DrawHud; GameEvents.KillFeed += OnKillFeed; }
        void OnDisable() { UIRoot.HudLayer -= DrawHud; GameEvents.KillFeed -= OnKillFeed; }

        static bool achievementsAdded;
        static void EnsureAchievements()
        {
            if (achievementsAdded) return;
            achievementsAdded = true;
            Stats.Achievements.Add(new Achievement("ks_spree", "Killing Spree", "Kill 5 in a row without dying.", "ks_spree", 1));
            Stats.Achievements.Add(new Achievement("ks_unstoppable", "Unstoppable", "Kill 15 in a row without dying.", "ks_unstoppable", 1));
            Stats.Achievements.Add(new Achievement("ks_godlike", "Godlike", "Kill 25 in a row without dying.", "ks_godlike", 1));
            Stats.Achievements.Add(new Achievement("ks_ultra", "Ultra Kill", "Get 5 kills within a few seconds.", "ks_ultra", 1));
        }

        void Update()
        {
            // Stat-driven kills (singleplayer + host): Goredoll.OnDeath adds Stats.Kills when the local player killed it.
            float k = Stats.Get(Stats.Kills);
            float h = Stats.Get(Stats.Headshots);
            if (h > lastHeadStat) lastHeadTime = Time.unscaledTime;
            lastHeadStat = h;
            if (k < lastKillStat) lastKillStat = k; // stats reset
            int newKills = Mathf.Min(10, Mathf.FloorToInt(k - lastKillStat));
            if (newKills > 0)
            {
                lastKillStat += newKills;
                bool head = Time.unscaledTime - lastHeadTime < 0.6f;
                for (int i = 0; i < newKills; i++) RegisterKill(head && i == 0);
            }

            // Streak resets when the local player dies or a new player (new map) is spawned.
            var p = PlayerController.Instance;
            if (p != trackedPlayer) { trackedPlayer = p; ResetStreak(); }
            bool dead = p != null && p.IsDead;
            if (dead && !wasDead) ResetStreak();
            wasDead = dead;
        }

        void ResetStreak() { Streak = 0; multi = 0; lastKillTime = -99f; }

        /// <summary>Multiplayer clients don't run the AI, so their kills only arrive as kill-feed lines "Name  >  Doll - cause".</summary>
        void OnKillFeed(string text, Color color)
        {
            if (!NetState.IsClient || NetManager.I == null || string.IsNullOrEmpty(text)) return;
            var me = NetManager.I.LocalName;
            if (string.IsNullOrEmpty(me) || !text.StartsWith(me + "  >  ")) return;
            RegisterKill(false);
        }

        void RegisterKill(bool headshot)
        {
            float now = Time.unscaledTime;
            multi = now - lastKillTime <= MultiWindow ? multi + 1 : 1;
            lastKillTime = now;
            Streak++;
            if (headshot) headshotTime = now;
            if (!Settings.Data.killStreakAnnouncer) { Achievements(); return; }

            int streakTier = System.Array.IndexOf(StreakAt, Streak);
            if (streakTier >= 0)
            {
                // Streak milestones win over multi-kills (they're rarer); the multi-kill goes into the subtitle.
                string sub = multi >= 2 ? MultiNames[Mathf.Min(multi, MultiNames.Length - 1)] : Streak + " kills without dying";
                Show(StreakNames[streakTier], sub, StreakColor(streakTier), 3 + streakTier);
            }
            else if (multi >= 2)
            {
                int idx = Mathf.Min(multi, MultiNames.Length - 1);
                Show(MultiNames[idx], Streak >= 5 ? Streak + " kill streak" : null, MultiColor(multi), Mathf.Min(multi - 2, 7));
            }
            Achievements();
        }

        void Achievements()
        {
            if (Streak >= 5) Stats.UnlockById("ks_spree");
            if (Streak >= 15) Stats.UnlockById("ks_unstoppable");
            if (Streak >= 25) Stats.UnlockById("ks_godlike");
            if (multi >= 5) Stats.UnlockById("ks_ultra");
        }

        static Color MultiColor(int m) => m <= 2 ? new Color(1f, 0.85f, 0.3f) : m == 3 ? new Color(1f, 0.6f, 0.2f) : m == 4 ? new Color(1f, 0.35f, 0.2f) : new Color(1f, 0.2f, 0.45f);
        static Color StreakColor(int t) => t == 0 ? new Color(0.4f, 0.85f, 1f) : t == 1 ? new Color(1f, 0.45f, 0.25f) : t == 2 ? new Color(0.85f, 0.35f, 1f) : t == 3 ? new Color(1f, 0.25f, 0.25f) : new Color(1f, 0.9f, 0.35f);

        void Show(string text, string sub, Color color, int soundTier)
        {
            bannerText = text;
            bannerSub = sub;
            bannerColor = color;
            bannerTime = Time.unscaledTime;
            AudioManager.Play2D("ks_" + Mathf.Clamp(soundTier, 0, 7), 0.9f, 1f, AudioBus.Sfx);
            CameraFX.Shake(0.08f);
        }

        // ------------------------------------------------------------------ drawing

        void DrawHud()
        {
            if (GameManager.I == null || GameManager.I.State != GameState.Playing || GameManager.I.HudHidden) return;
            if (!Settings.Data.killStreakAnnouncer) return;
            float now = Time.unscaledTime;
            float cx = UI.VW * 0.5f;

            // HEADSHOT popup, just under the crosshair.
            float ha = now - headshotTime;
            if (ha < 1.1f)
            {
                float a = Mathf.Clamp01((1.1f - ha) * 3f);
                float pop = 1f + 0.5f * Mathf.Clamp01(1f - ha * 8f);
                int size = Mathf.RoundToInt(22 * pop);
                float y = UI.VH * 0.5f + 46 - ha * 12f;
                UI.Label(new Rect(cx - 200 + 2, y + 2, 400, 40), "HEADSHOT", size, new Color(0, 0, 0, 0.6f * a), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
                UI.Label(new Rect(cx - 200, y, 400, 40), "HEADSHOT", size, new Color(1f, 0.3f, 0.25f, a), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
            }

            if (bannerText == null) return;
            float age = now - bannerTime;
            const float life = 2.4f;
            if (age > life) { bannerText = null; return; }
            // Scale punch: starts big, snaps to 1 with a small overshoot, then fades out.
            float t = Mathf.Clamp01(age / 0.22f);
            float scale = age < 0.22f ? Mathf.Lerp(2.1f, 0.92f, t * t) : 1f - 0.08f * Mathf.Exp(-(age - 0.22f) * 10f) * Mathf.Cos((age - 0.22f) * 30f);
            float alpha = Mathf.Clamp01(age * 8f) * Mathf.Clamp01((life - age) / 0.5f);
            float by = UI.VH * 0.24f - Mathf.Clamp01((age - (life - 0.5f)) / 0.5f) * 20f;
            int fs = Mathf.Clamp(Mathf.RoundToInt(72 * scale), 20, 180);
            float bw = 1400, bh = 120;
            var r = new Rect(cx - bw / 2, by - bh / 2, bw, bh);
            // Soft dark band behind the text + accent bars.
            float tw = Mathf.Min(UI.TextWidth(bannerText, 72) * 1.15f + 80, bw);
            UI.Rounded(new Rect(cx - tw / 2, by - 50, tw, 100), new Color(0, 0, 0, 0.45f * alpha), true);
            UI.Fill(new Rect(cx - tw / 2 * Mathf.Clamp01(age * 4f), by + 46, tw * Mathf.Clamp01(age * 4f), 4), bannerColor.WithAlpha(alpha));
            UI.Label(new Rect(r.x + 4, r.y + 4, r.width, r.height), bannerText, fs, new Color(0, 0, 0, 0.7f * alpha), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
            UI.Label(r, bannerText, fs, bannerColor.WithAlpha(alpha), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
            // White flash on the text right at the hit.
            if (age < 0.15f) UI.Label(r, bannerText, fs, new Color(1, 1, 1, (1f - age / 0.15f) * 0.8f), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
            if (!string.IsNullOrEmpty(bannerSub))
                UI.Label(new Rect(cx - 400, by + 52, 800, 36), bannerSub, 24, UI.TextColor.WithAlpha(alpha), TextAnchor.MiddleCenter, FontStyle.Bold);
        }
    }
}
