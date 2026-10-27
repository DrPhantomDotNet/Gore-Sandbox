using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Endless horde mode for the Arena map. Round N has 2^(N-1) enemies (1, 2, 4, 8 ...), all zombies (random mix of
    /// the 5 types) or all hostile Goredolls, picked at random each round. They enter through the gates, at most
    /// <see cref="MaxAlive"/> at once; the rest come in as others die. Clear the round for a short break (ammo and
    /// health refilled), die and the run restarts from round 1 (best run saved in PlayerPrefs).
    /// The host / singleplayer runs everything; clients get the round info from "[HORDE]" system messages and count
    /// the living enemies themselves for the HUD.
    /// </summary>
    public sealed class HordeMode : MonoBehaviour
    {
        enum Phase { Starting, Break, Fighting, GameOver }

        public static HordeMode Current { get; private set; }
        public readonly List<Vector3> Spawns = new List<Vector3>();
        public const string BestKey = "horde_best_round";
        const string Tag = "[HORDE] ";
        static readonly string[] Loadout = { "knife", "glock", "uzi", "ar15", "ar17", "shotgun", "sniper" };

        public int Round;
        public bool ZombieRound;
        public int Total, Spawned;
        Phase phase = Phase.Starting;
        float phaseUntil, nextSpawn, bannerUntil, lastChatTime;
        string banner = "", bannerSub = "";
        bool started, loadoutGiven;
        int clientAlive;

        static int MaxAlive => Mathf.Clamp(Settings.Data.maxRagdolls - 4, 1, Zombies.MaxActive);
        public static int Best => PlayerPrefs.GetInt(BestKey, 0);

        void OnEnable() { Current = this; UIRoot.HudLayer += DrawHud; }
        void OnDisable() { if (Current == this) Current = null; UIRoot.HudLayer -= DrawHud; }

        int AliveEnemies()
        {
            int n = 0;
            foreach (var g in Goredoll.All)
                if (g != null && !g.IsDead && (g.GetComponent<HordeMember>() != null || NetState.IsClient)) n++;
            return n;
        }

        void Update()
        {
            if (GameManager.I == null || GameManager.I.State != GameState.Playing) return;
            if (!started)
            {
                started = true;
                lastChatTime = Time.unscaledTime;
                if (NetState.Authority)
                {
                    // No company but the horde.
                    foreach (var g in Goredoll.All.ToArray()) if (g != null && g.GetComponent<HordeMember>() == null) g.Despawn();
                    phase = Phase.Starting;
                    phaseUntil = Time.time + 4f;
                    ShowBanner("HORDE MODE", "Survive as long as you can. Round 1 starts soon...", 4f);
                }
                else ShowBanner("HORDE MODE", "The host runs the waves. Good luck.", 4f);
            }
            if (!loadoutGiven && PlayerController.Instance != null) { loadoutGiven = true; GiveLoadout(true); }

            if (!NetState.Authority) { ClientTick(); return; }

            bool anyPlayer = PlayerTargets.All.Count > 0, allDead = anyPlayer;
            foreach (var p in PlayerTargets.All) if (p != null && !p.IsDead) { allDead = false; break; }

            switch (phase)
            {
                case Phase.Starting:
                case Phase.Break:
                    if (Time.time >= phaseUntil) StartRound(Round + 1);
                    break;
                case Phase.Fighting:
                {
                    if (allDead) { GameOver(); break; }
                    int alive = AliveEnemies();
                    if (Spawned < Total && alive < MaxAlive && Time.time >= nextSpawn)
                    {
                        nextSpawn = Time.time + Random.Range(0.25f, 0.6f);
                        SpawnEnemy();
                    }
                    if (Spawned >= Total && alive == 0) RoundCleared();
                    break;
                }
                case Phase.GameOver:
                    if (Time.time >= phaseUntil)
                    {
                        Round = 0;
                        phase = Phase.Break;
                        phaseUntil = Time.time + 2f;
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ host

        void StartRound(int n)
        {
            Round = n;
            Total = n >= 20 ? 1 << 19 : 1 << (n - 1);
            Spawned = 0;
            ZombieRound = Random.value < 0.5f;
            phase = Phase.Fighting;
            nextSpawn = Time.time + 0.5f;
            string kind = ZombieRound ? "ZOMBIES" : "GOREDOLLS";
            ShowBanner("ROUND " + n, kind + " INCOMING  -  " + Total + (Total == 1 ? " ENEMY" : " ENEMIES"), 3.5f);
            AudioManager.Ui("ui_success");
            Announce("ROUND " + n + " - " + kind + " - " + Total + " enemies");
            GiveLoadout(false);
            if (n >= 5) Stats.UnlockById("horde_survivor");
            if (n >= 8) Stats.UnlockById("horde_breaker");
        }

        void SpawnEnemy()
        {
            if (Spawns.Count == 0) Spawns.Add(new Vector3(0, 0.05f, 20f));
            var gate = Spawns[Random.Range(0, Spawns.Count)];
            var side = Vector3.Cross(Vector3.up, -gate.normalized) * Random.Range(-1.6f, 1.6f);
            var pos = gate + side + gate.normalized * Random.Range(-1f, 0.5f);
            pos.y = 0.05f;
            var to = -new Vector3(gate.x, 0, gate.z);
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            Goredoll d;
            if (ZombieRound) d = Zombies.Spawn(Round == 1 ? ZombieKind.Walker : Zombies.RandomKind(), pos, yaw);
            else d = GoredollFactory.Spawn(pos, yaw, "Hostile");
            Spawned++;
            if (d == null) return;
            d.gameObject.AddComponent<HordeMember>();
        }

        void RoundCleared()
        {
            int best = Best;
            if (Round > best) { PlayerPrefs.SetInt(BestKey, Round); PlayerPrefs.Save(); }
            phase = Phase.Break;
            float pause = Random.Range(5.5f, 7.5f);
            phaseUntil = Time.time + pause;
            ShowBanner("ROUND " + Round + " CLEARED", "Ammo and health refilled. Next round in a moment...", 3f);
            Announce("Round " + Round + " cleared! Round " + (Round + 1) + " in " + Mathf.RoundToInt(pause) + " s");
            GiveLoadout(false);
            // Clear old corpses so the next wave has room.
            foreach (var g in Goredoll.All.ToArray()) if (g != null && g.IsDead && Time.time - g.Vitals.TimeOfDeath > 4f) g.Despawn();
        }

        void GameOver()
        {
            int survived = Mathf.Max(0, Round - 1);
            if (survived > Best) { PlayerPrefs.SetInt(BestKey, survived); PlayerPrefs.Save(); }
            phase = Phase.GameOver;
            phaseUntil = Time.time + 6f;
            ShowBanner("YOU SURVIVED " + survived + (survived == 1 ? " ROUND" : " ROUNDS"), "Best: " + Best + "   -   Restarting from round 1...", 6f);
            Announce("GAME OVER - survived " + survived + " rounds. Restarting from round 1.");
            foreach (var g in Goredoll.All.ToArray()) if (g != null) g.Despawn();
        }

        void Announce(string text)
        {
            if (NetState.IsHost && NetManager.I != null) NetManager.I.SystemMessage(Tag + text);
        }

        // ------------------------------------------------------------------ client

        void ClientTick()
        {
            if (Time.frameCount % 15 == 0) clientAlive = AliveEnemies();
            var nm = NetManager.I;
            if (nm == null) return;
            foreach (var entry in nm.ChatLog)
            {
                if (entry.time <= lastChatTime) continue;
                lastChatTime = entry.time;
                var text = entry.text;
                if (string.IsNullOrEmpty(text) || !text.StartsWith(Tag)) continue;
                text = text.Substring(Tag.Length);
                if (text.StartsWith("ROUND "))
                {
                    var parts = text.Split('-');
                    if (parts.Length >= 2 && int.TryParse(parts[0].Substring(6).Trim(), out var n))
                    {
                        Round = n;
                        ZombieRound = parts[1].Trim().StartsWith("ZOMBIES");
                        phase = Phase.Fighting;
                        ShowBanner("ROUND " + n, parts[1].Trim() + " INCOMING", 3.5f);
                        GiveLoadout(false);
                        if (n >= 5) Stats.UnlockById("horde_survivor");
                        if (n >= 8) Stats.UnlockById("horde_breaker");
                        if (n - 1 > Best) { PlayerPrefs.SetInt(BestKey, n - 1); PlayerPrefs.Save(); }
                    }
                }
                else if (text.StartsWith("Round ")) { phase = Phase.Break; ShowBanner("ROUND " + Round + " CLEARED", "Next round in a moment...", 3f); GiveLoadout(false); }
                else if (text.StartsWith("GAME OVER")) { phase = Phase.GameOver; ShowBanner("GAME OVER", text.Substring(9).Trim(' ', '-'), 6f); }
            }
        }

        // ------------------------------------------------------------------ player

        void GiveLoadout(bool select)
        {
            var pc = PlayerController.Instance;
            if (pc == null || pc.Inventory == null) return;
            foreach (var id in Loadout) pc.Inventory.Give(id, -1, false); // gives, or refills the magazine if already owned
            if (pc.Health != null && !pc.Health.Dead) pc.Health.Current = pc.Health.Max;
            if (select)
                for (int i = 0; i < Inventory.SlotCount; i++)
                    if (pc.Inventory.Slots[i] != null && pc.Inventory.Slots[i].Def.Id == "ar15") { pc.Inventory.Select(i, true); break; }
        }

        void ShowBanner(string title, string sub, float seconds)
        {
            banner = title; bannerSub = sub; bannerUntil = Time.time + seconds;
        }

        // ------------------------------------------------------------------ HUD

        void DrawHud()
        {
            if (GameManager.I == null || GameManager.I.State != GameState.Playing || GameManager.I.HudHidden) return;
            float cx = UI.VW * 0.5f;
            var box = new Rect(cx - 260, 14, 520, 62);
            UI.Rounded(box, new Color(0f, 0f, 0f, 0.55f), true);
            int left = NetState.Authority ? Mathf.Max(0, Total - Spawned) + AliveEnemies() : clientAlive;
            string roundText = Round > 0 ? "ROUND " + Round : "HORDE MODE";
            string typeText = Round > 0 ? (ZombieRound ? "ZOMBIES" : "GOREDOLLS") : "";
            UI.Label(new Rect(box.x + 18, box.y + 4, 220, 54), roundText, 30, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
            UI.Label(new Rect(box.x + 180, box.y + 4, 150, 54), typeText, 20, ZombieRound ? new Color(0.55f, 0.95f, 0.45f) : UI.Accent2, TextAnchor.MiddleCenter);
            string right = phase == Phase.Fighting ? "Enemies left: " + left : phase == Phase.GameOver ? "Game over" : NetState.Authority && Round >= 0 ? "Next round: " + Mathf.CeilToInt(Mathf.Max(0f, phaseUntil - Time.time)) + " s" : "Get ready";
            UI.Label(new Rect(box.x + 320, box.y + 6, 186, 30), right, 18, Color.white, TextAnchor.MiddleRight);
            UI.Label(new Rect(box.x + 320, box.y + 32, 186, 24), "Best: " + Best + " rounds", 14, UI.Muted, TextAnchor.MiddleRight);

            if (Time.time < bannerUntil && !string.IsNullOrEmpty(banner))
            {
                float a = Mathf.Clamp01((bannerUntil - Time.time) / 0.6f);
                var r = new Rect(cx - 520, UI.VH * 0.26f, 1040, 120);
                UI.Label(r, banner, 84, new Color(1f, 0.85f, 0.6f, a), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
                UI.Label(new Rect(r.x, r.yMax, r.width, 40), bannerSub, 24, new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter);
            }
        }
    }
}
