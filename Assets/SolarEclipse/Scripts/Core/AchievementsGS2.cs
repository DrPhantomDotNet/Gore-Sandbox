using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 achievement pack (about 200): five ranks (I-V) for every lifetime stat, ranks for the new GS2
    /// stats (bandages, disarms, finishers, tendons, internal bleeding, lightning, jams, fight hits, blade wipes, time stops),
    /// extra ranks for the gore stats, and one for visiting each GS2 map. Registered from GoreKit.EnsureAchievements.
    /// </summary>
    public static class AchievementsGS2
    {
        static readonly string[] Roman = { "I", "II", "III", "IV", "V" };
        static bool done;

        static void Add(string id, string name, string desc, string stat, float target)
        {
            if (Stats.Achievements.Exists(a => a.Id == id)) return;
            var ach = new Achievement(id, name, desc, stat, target);
            Stats.Achievements.Add(ach);
            // Players who already passed this rank before the update get it now, quietly (no toast flood).
            if (!Stats.IsUnlocked(ach) && Stats.Get(stat) >= target) PlayerPrefs.SetInt("ach_" + id, 1);
        }

        /// <summary>Five ranks. desc is a format string: {0} is the target (shown with the given divisor and unit).</summary>
        static void Tiers(string id, string name, string desc, string stat, float[] targets, float shownDiv = 1f)
        {
            for (int i = 0; i < targets.Length && i < Roman.Length; i++)
            {
                float shown = targets[i] / shownDiv;
                string n = shown >= 1000f ? shown.ToString("#,0") : shown.ToString(shown % 1f == 0f ? "0" : "0.#");
                Add("gs2_" + id + "_" + (i + 1), name + " " + Roman[i], string.Format(desc, n), stat, targets[i]);
            }
        }

        public static void Register()
        {
            if (done) return;
            done = true;
            Tiers("kills", "Body Count", "Kill {0} Goredolls.", Stats.Kills, new[] { 10f, 250f, 1000f, 5000f, 20000f });
            Tiers("heads", "Sharpshooter", "Land {0} headshots.", Stats.Headshots, new[] { 10f, 100f, 500f, 2000f, 10000f });
            Tiers("shots", "Lead Rain", "Fire {0} rounds.", Stats.ShotsFired, new[] { 500f, 5000f, 25000f, 100000f, 500000f });
            Tiers("hits", "Connected", "Hit Goredolls {0} times.", Stats.Hits, new[] { 100f, 1000f, 10000f, 50000f, 200000f });
            Tiers("limbs", "Limb Collector", "Sever {0} limbs.", Stats.Limbs, new[] { 10f, 100f, 500f, 2000f, 10000f });
            Tiers("decaps", "Off With Their Heads", "Decapitate {0} Goredolls.", Stats.Decaps, new[] { 5f, 50f, 250f, 1000f, 5000f });
            Tiers("blood", "Blood Tide", "Spill {0} litres of blood.", Stats.BloodMl, new[] { 10000f, 500000f, 2000000f, 10000000f, 50000000f }, 1000f);
            Tiers("spawned", "Population", "Spawn {0} Goredolls.", Stats.Spawned, new[] { 50f, 500f, 2500f, 10000f, 50000f });
            Tiers("props", "Builder", "Spawn {0} props.", Stats.PropsSpawned, new[] { 50f, 500f, 2500f, 10000f, 50000f });
            Tiers("gibs", "Mincemeat", "Gib {0} body parts.", Stats.Gibs, new[] { 10f, 250f, 1000f, 5000f, 20000f });
            Tiers("inspect", "Doctor", "Inspect {0} wounds.", Stats.Inspections, new[] { 10f, 100f, 500f, 2000f, 5000f });
            Tiers("slices", "Slicer", "Make {0} slices.", Stats.Slices, new[] { 10f, 100f, 1000f, 5000f, 20000f });
            Tiers("punches", "Iron Fist", "Throw {0} punches.", Stats.Punches, new[] { 100f, 1000f, 5000f, 25000f, 100000f });
            Tiers("kicks", "Boot", "Kick {0} things.", Stats.Kicks, new[] { 25f, 250f, 1000f, 5000f, 20000f });
            Tiers("boom", "Boom", "Cause {0} explosions.", Stats.Explosions, new[] { 10f, 100f, 1000f, 5000f, 20000f });
            Tiers("time", "Dedicated", "Play for {0} hours.", Stats.PlaySeconds, new[] { 7200f, 36000f, 180000f, 720000f, 1800000f }, 3600f);
            Tiers("walk", "Marathon", "Walk {0} km.", Stats.Distance, new[] { 5000f, 42195f, 200000f, 1000000f, 5000000f }, 1000f);
            Tiers("maps", "Globetrotter", "Load {0} maps.", Stats.MapsPlayed, new[] { 25f, 100f, 500f, 2000f, 5000f });
            Tiers("throws", "Yeet", "Throw {0} things.", Stats.Throws, new[] { 10f, 100f, 1000f, 5000f, 20000f });
            Tiers("reloads", "Mag Dump", "Reload {0} times.", Stats.Reloads, new[] { 50f, 500f, 5000f, 25000f, 100000f });
            Tiers("neck", "Jugular", "Shoot {0} Goredolls in the neck.", Stats.NeckShots, new[] { 25f, 100f, 500f, 2000f, 5000f });
            Tiers("revive", "Lazarus", "Revive {0} Goredolls.", Stats.Revives, new[] { 5f, 25f, 100f, 500f, 2000f });
            Tiers("paralyze", "Spine Doctor", "Paralyse {0} Goredolls.", Stats.Paralyzed, new[] { 5f, 25f, 100f, 500f, 2000f });
            Tiers("deaths", "Respawner", "Die {0} times.", Stats.Deaths, new[] { 25f, 100f, 500f, 2000f, 5000f });
            // New in Gore Sandbox 2
            Tiers("bandage", "Patch Up", "Bandage your own wounds {0} times.", "bandages", new[] { 1f, 10f, 50f, 200f, 1000f });
            Tiers("disarm", "Disarmer", "Knock {0} weapons out of Goredolls' hands.", "disarms", new[] { 1f, 10f, 50f, 250f, 1000f });
            Tiers("finisher", "Finisher", "Land {0} finishers on dazed Goredolls.", "finishers", new[] { 1f, 10f, 50f, 250f, 1000f });
            Tiers("tendon", "Tendon Snapper", "Cut through {0} wrist or ankle tendons.", "tendons", new[] { 1f, 10f, 50f, 250f, 1000f });
            Tiers("internal", "Internal Affairs", "Cause {0} internal bleeds with blunt force.", "internal_bleeds", new[] { 1f, 10f, 50f, 250f, 1000f });
            Tiers("lightning", "Thunderstruck", "Watch lightning strike {0} Goredolls.", "lightning", new[] { 1f, 5f, 25f, 100f, 500f });
            Tiers("jam", "Clear the Jam", "Clear {0} jammed guns.", "jams", new[] { 1f, 10f, 50f, 200f, 1000f });
            Tiers("fight", "Heavy Hands", "Land {0} fighting-style hits.", "fight_landed", new[] { 50f, 500f, 2500f, 10000f, 50000f });
            Tiers("wipe", "Clean Blade", "Wipe the blood off your weapon {0} times.", "wipes", new[] { 1f, 10f, 50f, 200f, 1000f });
            Tiers("timestop", "The World", "Stop time {0} times.", "timestops", new[] { 1f, 10f, 50f, 200f, 1000f });
            // Higher ranks for the Gore Update stats (Gore/GoreKit.cs has rank I).
            Tiers("teeth", "Dentist", "Knock out {0} teeth.", GoreKit.StatTeeth, new[] { 100f, 500f, 2000f, 8000f, 25000f });
            Tiers("pops", "Pop Goes the Head", "Pop {0} heads.", GoreKit.StatHeads, new[] { 50f, 200f, 1000f, 4000f, 15000f });
            Tiers("pools", "Blood Bath", "Leave {0} pools of blood.", GoreKit.StatPools, new[] { 100f, 500f, 2000f, 8000f, 25000f });
            Tiers("charred", "Well Done", "Burn {0} body parts.", GoreKit.StatCharred, new[] { 100f, 1000f, 5000f });
            // Story (UI/Screens/StoryScreens.cs mission ranks)
            Add("gs2_story_s1", "Flawless", "Finish a story mission with an S rank.", "missions_rank_S", 1f);
            Add("gs2_story_s5", "Untouchable", "Finish 5 story missions with an S rank.", "missions_rank_S", 5f);
            Add("gs2_story_a3", "Professional", "Finish 3 story missions with an A rank.", "missions_rank_A", 3f);
            Add("gs2_story_c1", "Barely Made It", "Finish a story mission with a C rank.", "missions_rank_C", 1f);
            // Visit every Gore Sandbox 2 map.
            foreach (var m in new[] { ("morgue", "The Morgue"), ("skyline", "Skyline"), ("sewers", "The Sewers"), ("junkyard", "Junkyard"), ("lab", "Laboratory"), ("colosseum", "Colosseum"),
                ("village", "Medieval Village"), ("containerport", "Container Port"), ("flatgrass2", "Flatgrass 2"), ("farm", "Farm"), ("gasstation", "Desert Gas Station"), ("cabin", "Forest Cabin") })
                Add("gs2_map_" + m.Item1, "Visitor: " + m.Item2, "Play on " + m.Item2 + ".", "map_" + m.Item1, 1f);
        }
    }
}
