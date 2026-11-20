using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 progression: everything you do earns XP (kills, headshots, limbs, finishers, building, treating
    /// patients, exploring...). Levels give titles, shown on your profile card in the menu. Purely cosmetic; nothing is
    /// locked behind levels, and nothing is ever bought with real money.
    /// </summary>
    public static class PlayerLevel
    {
        static readonly Dictionary<string, float> Xp = new Dictionary<string, float>
        {
            { Stats.Kills, 10f }, { Stats.Headshots, 5f }, { Stats.Limbs, 4f }, { Stats.Decaps, 8f }, { Stats.Gibs, 3f }, { Stats.Slices, 3f },
            { Stats.Punches, 1f }, { Stats.Kicks, 1f }, { Stats.Throws, 3f }, { Stats.Explosions, 3f }, { Stats.Spawned, 1f }, { Stats.PropsSpawned, 0.5f },
            { Stats.Inspections, 2f }, { Stats.Revives, 10f }, { Stats.MapsPlayed, 15f }, { Stats.Ducks, 50f }, { Stats.EasterEggs, 100f },
            { Stats.Distance, 0.02f }, { Stats.PlaySeconds, 0.05f }, { "headlocks", 5f }, { "neck_snaps", 15f }, { "defibs", 25f }, { "tourniquets", 10f },
            { "photos", 2f }, { "grabs", 1f },
        };

        static readonly string[] Titles = { "Rookie", "Intern", "Brawler", "Butcher", "Field Medic", "Surgeon", "Executioner", "Reaper", "Nightmare", "Legend", "Solar Eclipse" };

        public static float TotalXp => PlayerPrefs.GetFloat("gs2_xp", 0f);
        static float pending;

        public static int LevelFor(float xp) { int l = 1; while (xp >= Need(l)) { xp -= Need(l); l++; if (l > 999) break; } return l; }
        public static float Need(int level) => Mathf.Round(120f * Mathf.Pow(level, 1.35f));
        public static int Level => LevelFor(TotalXp);
        public static string Title => Titles[Mathf.Clamp((Level - 1) / 5, 0, Titles.Length - 1)];

        /// <summary>0..1 progress inside the current level.</summary>
        public static float Progress
        {
            get { float xp = TotalXp; int l = 1; while (xp >= Need(l)) { xp -= Need(l); l++; if (l > 999) break; } return xp / Need(l); }
        }

        /// <summary>Called from Stats.Add for every stat change.</summary>
        public static void OnStat(string stat, float amount)
        {
            if (amount <= 0f || !Xp.TryGetValue(stat, out var per)) return;
            int before = Level;
            pending += per * amount;
            if (pending < 1f) return;
            float add = Mathf.Floor(pending);
            pending -= add;
            PlayerPrefs.SetFloat("gs2_xp", TotalXp + add);
            int after = Level;
            if (after > before)
            {
                Toasts.Show("LEVEL UP", "Level " + after + "  -  " + Title, new Color(1f, 0.78f, 0.16f));
                AudioManager.Ui("ui_success");
            }
        }
    }
}
