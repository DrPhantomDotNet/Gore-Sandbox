using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    public sealed class Achievement
    {
        public string Id, Name, Description, Stat;
        public float Target;
        public bool Secret;
        public Achievement(string id, string name, string desc, string stat, float target, bool secret = false)
        { Id = id; Name = name; Description = desc; Stat = stat; Target = target; Secret = secret; }
    }

    /// <summary>Lifetime statistics and achievements, persisted in PlayerPrefs.</summary>
    public static class Stats
    {
        public const string ShotsFired = "shots_fired", Hits = "hits", Headshots = "headshots", Kills = "kills", Limbs = "limbs_severed",
            Decaps = "decapitations", BloodMl = "blood_ml", Spawned = "npcs_spawned", PropsSpawned = "props_spawned", Gibs = "gibs",
            Inspections = "wounds_inspected", Slices = "slices", Punches = "punches", Kicks = "kicks", Explosions = "explosions",
            PlaySeconds = "play_seconds", Deaths = "player_deaths", Noclips = "noclip_toggles", Paralyzed = "paralyzed",
            NeckShots = "neck_shots", Ducks = "ducks_found", EasterEggs = "easter_eggs", Nextbots = "nextbots_spawned",
            Revives = "revives", Throws = "throws", Reloads = "reloads", DevLogins = "dev_logins", MapsPlayed = "maps_played", Distance = "distance_m";

        public static readonly List<Achievement> Achievements = new List<Achievement>
        {
            new Achievement("first_blood", "First Blood", "Hurt a Goredoll for the first time.", Hits, 1),
            new Achievement("trigger_happy", "Trigger Happy", "Fire 1,000 rounds.", ShotsFired, 1000),
            new Achievement("marksman", "Marksman", "Land 50 headshots.", Headshots, 50),
            new Achievement("reaper", "The Reaper", "Kill 100 Goredolls.", Kills, 100),
            new Achievement("butcher", "Butcher", "Sever 50 limbs.", Limbs, 50),
            new Achievement("headless", "Headless Horseman", "Decapitate 10 Goredolls.", Decaps, 10),
            new Achievement("blood_bank", "Blood Bank", "Spill 100 litres of blood.", BloodMl, 100000),
            new Achievement("creator", "Creator", "Spawn 100 Goredolls.", Spawned, 100),
            new Achievement("surgeon", "Field Surgeon", "Inspect 25 wounds.", Inspections, 25),
            new Achievement("chef", "Chef", "Make 25 knife slices.", Slices, 25),
            new Achievement("boxer", "Boxer", "Throw 200 punches.", Punches, 200),
            new Achievement("dropkick", "Sparta", "Kick 50 things.", Kicks, 50),
            new Achievement("demolition", "Demolition Expert", "Cause 25 explosions.", Explosions, 25),
            new Achievement("pulp", "Pulped", "Gib 20 body parts.", Gibs, 20),
            new Achievement("still", "Lie Still", "Paralyze a Goredoll.", Paralyzed, 1),
            new Achievement("neck", "Pain in the Neck", "Shoot 10 Goredolls in the neck.", NeckShots, 10),
            new Achievement("ghost", "Ghost", "Toggle noclip 10 times.", Noclips, 10),
            new Achievement("medic", "Miracle Worker", "Revive a Goredoll with the Admin Gun.", Revives, 1),
            new Achievement("veteran", "Veteran", "Play for one hour.", PlaySeconds, 3600),
            new Achievement("hiker", "Hiker", "Walk 10 kilometres.", Distance, 10000),
            new Achievement("tourist", "Tourist", "Load 10 maps.", MapsPlayed, 10),
            new Achievement("duck", "Duck Hunter", "Find a hidden rubber duck.", Ducks, 1, true),
            new Achievement("ducks_all", "Quack Commander", "Find 5 hidden rubber ducks.", Ducks, 5, true),
            new Achievement("eggs", "Egg Hunter", "Discover 5 easter eggs.", EasterEggs, 5, true),
            new Achievement("run", "RUN", "Spawn a nextbot.", Nextbots, 1, true),
            new Achievement("dev", "Insider", "Log in to the developer panel.", DevLogins, 1, true),
            new Achievement("death", "Gravity Wins", "Die 10 times.", Deaths, 10),
            new Achievement("scaredy_cat", "Scaredy Cat", "This badge is given to the people that are too scared to play the bossfight.", "boss_scaredy", 1),
            new Achievement("now_angry", "Now it's angry", "Survive five minutes in the realm.", "boss_angry", 1, true),
            new Achievement("realm_owner", "I'm the owner of this realm now", "Finish it off.", "boss_owner", 1, true),
        };

        static readonly Dictionary<string, float> cache = new Dictionary<string, float>();
        static readonly HashSet<string> sessionEggs = new HashSet<string>();
        public static event Action<Achievement> Unlocked;

        public static float Get(string stat)
        {
            if (!cache.TryGetValue(stat, out var v)) { v = PlayerPrefs.GetFloat("stat_" + stat, 0f); cache[stat] = v; }
            return v;
        }

        public static void Add(string stat, float amount = 1f)
        {
            float before = Get(stat);
            float after = before + amount;
            cache[stat] = after;
            PlayerPrefs.SetFloat("stat_" + stat, after);
            foreach (var a in Achievements)
                if (a.Stat == stat && before < a.Target && after >= a.Target && !IsUnlocked(a)) Unlock(a);
        }

        /// <summary>Manually unlocks an achievement by id (once). Returns true if it was newly unlocked.</summary>
        public static bool UnlockById(string id)
        {
            var a = Achievements.Find(x => x.Id == id);
            if (a == null || IsUnlocked(a)) return false;
            if (a.Stat != null && Get(a.Stat) < a.Target) { cache[a.Stat] = a.Target; PlayerPrefs.SetFloat("stat_" + a.Stat, a.Target); }
            Unlock(a);
            return true;
        }

        public static bool IsUnlocked(Achievement a) => PlayerPrefs.GetInt("ach_" + a.Id, 0) == 1;

        public static int UnlockedCount()
        {
            int n = 0;
            foreach (var a in Achievements) if (IsUnlocked(a)) n++;
            return n;
        }

        public static void Unlock(Achievement a)
        {
            PlayerPrefs.SetInt("ach_" + a.Id, 1);
            PlayerPrefs.Save();
            Unlocked?.Invoke(a);
        }

        /// <summary>Counts each named easter egg once per profile.</summary>
        public static bool FoundEasterEgg(string id)
        {
            if (sessionEggs.Contains(id) || PlayerPrefs.GetInt("egg_" + id, 0) == 1) return false;
            sessionEggs.Add(id);
            PlayerPrefs.SetInt("egg_" + id, 1);
            Add(EasterEggs);
            GameEvents.ShowToast("Easter egg found: " + id);
            return true;
        }

        public static bool HasEasterEgg(string id) => PlayerPrefs.GetInt("egg_" + id, 0) == 1;

        public static void ResetAll()
        {
            foreach (var a in Achievements) PlayerPrefs.DeleteKey("ach_" + a.Id);
            foreach (var k in new List<string>(cache.Keys)) PlayerPrefs.DeleteKey("stat_" + k);
            cache.Clear();
            PlayerPrefs.Save();
        }

        public static void Hook()
        {
            GameEvents.StatIncrement += s => Add(s);
        }
    }
}
