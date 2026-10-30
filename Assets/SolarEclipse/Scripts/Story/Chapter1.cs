using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Story chapter 1, "The East Side": four missions.
    ///   1 The Alley     (World/AlleywayMap.cs + World/StoryMode.cs) - Big Marcus, kill Z.
    ///   2 The Docks     (World/DocksMap.cs) - Rico, the container yard, the ambush, Vex's ledger.
    ///   3 The Warehouse (Warehouse 13) - quiet or loud, Dez the bookkeeper (spare him or not).
    ///   4 The Rooftop   (Rooftops) - Vex on the helipad: join him or fight; three endings.
    /// Then THE END, END OF CHAPTER 1, the credits and the achievement. Progress and choices are saved.
    /// </summary>
    public static class Chapter1
    {
        public const int MissionCount = 4;
        public static readonly string[] Names = { "The Alley", "The Docks", "The Warehouse", "The Rooftop" };
        public static readonly string[] Blurbs =
        {
            "Big Marcus has a job for you: Z, round the corner.",
            "Z's phone had one address. Somebody's running guns through the docks.",
            "Vex's bookkeeper is inside. Go in quiet, or go in loud.",
            "Vex is on the roof of the Meridian Tower. Finish this.",
        };
        const string KProgress = "story_ch1_progress", KFlags = "story_ch1_flags";

        /// <summary>Missions finished (0..4). Mission n is playable when n &lt;= Progress + 1.</summary>
        public static int Progress
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KProgress, IsZDead() ? 1 : 0), 0, MissionCount);
            set { PlayerPrefs.SetInt(KProgress, Mathf.Clamp(value, 0, MissionCount)); PlayerPrefs.Save(); }
        }

        static bool IsZDead()
        {
            EnsureAchievements();
            var a = Stats.Achievements.Find(x => x.Id == "story_ch1");
            return a != null && Stats.IsUnlocked(a);
        }

        public static bool Unlocked(int mission) => mission <= Progress + 1;
        public static int NextMission => Mathf.Clamp(Progress + 1, 1, MissionCount);

        static HashSet<string> LoadFlags()
        {
            var set = new HashSet<string>();
            foreach (var f in PlayerPrefs.GetString(KFlags, "").Split(',')) if (f.Length > 0) set.Add(f);
            return set;
        }

        static void SaveFlags(HashSet<string> flags) { PlayerPrefs.SetString(KFlags, string.Join(",", new List<string>(flags).ToArray())); PlayerPrefs.Save(); }

        public static void EnsureAchievements()
        {
            AlleywayMap.EnsureAchievement();
            void Add(string id, string name, string desc, bool secret = false)
            {
                if (!Stats.Achievements.Exists(a => a.Id == id)) Stats.Achievements.Add(new Achievement(id, name, desc, id, 1, secret));
            }
            Add("story_ch1_end", "End of Chapter 1", "Finish all four missions of story chapter 1.");
            Add("story_end_join", "The New Right Hand", "Chapter 1 ending: take Vex's offer.", true);
            Add("story_end_king", "King of the East Side", "Chapter 1 ending: take over the east side.", true);
            Add("story_end_walk", "Walk Away", "Chapter 1 ending: walk away from it all.", true);
        }

        // ------------------------------------------------------------------ flow

        public static void Play(int mission)
        {
            EnsureAchievements();
            mission = Mathf.Clamp(mission, 1, MissionCount);
            if (mission == 1) { StoryMode.Begin(); return; }
            var ctx = new StoryContext { Pack = Mission(mission), Mission = mission, OnPassed = c => Completed(c.Mission, c.Flags), OnTheEnd = Finish };
            int m = mission;
            ctx.Retry = () => Play(m); // fresh flags from the save, not the failed run's choices
            foreach (var f in LoadFlags()) ctx.Flags.Add(f);
            if (mission == 3) { ctx.Flags.Remove("spared_dez"); ctx.Flags.Remove("killed_dez"); }
            StoryRunner.Play(ctx);
        }

        /// <summary>A mission was passed: save, then straight into the next one.</summary>
        public static void Completed(int mission, HashSet<string> flags = null)
        {
            if (flags != null) SaveFlags(flags);
            if (mission > Progress) Progress = mission;
            if (mission < MissionCount) Play(mission + 1);
            else GameManager.I.ReturnToMenu();
        }

        static void Finish(StoryContext ctx, StoryStep ending)
        {
            SaveFlags(ctx.Flags);
            Progress = MissionCount;
            UIRoot.Push(new TheEndScreen(ending.who, ending.text, "END OF CHAPTER 1", () =>
                UIRoot.Push(new CreditsRollScreen(Credits, () =>
                {
                    EnsureAchievements();
                    GameManager.I.ReturnToMenu();
                    Stats.UnlockById("story_ch1_end");
                    UIRoot.Push(new StoryOrbieScreen("That's the end of chapter 1! Chapter 2 is coming... it takes a lot of time to make this, so hang tight."));
                }))));
        }

        static readonly string[] Credits =
        {
            "#GORE SANDBOX", "*CHAPTER 1", "THE EAST SIDE", "", "",
            "*A " + GameInfo.Studio.ToUpperInvariant() + " PRODUCTION", "", "",
            "*OWNER - DEVELOPER", "Quibbo / DrPhantom", "",
            "*DEVELOPER", "AlexKiller", "", "",
            "*STARRING", "Big Marcus", "Z", "Rico", "Dez", "Vex", "Orbie", "", "*AND", "You", "", "",
            "*MAPS", "The Alley", "The Docks", "Warehouse 13", "The Meridian Tower", "", "",
            "*ENGINE", "Unity 6", "",
            "*EVERY SOUND", "Synthesised live by the game", "",
            "*EVERY MODEL AND TEXTURE", "Generated procedurally at runtime", "", "",
            "*SPECIAL THANKS", "Everyone in the Discord", "Every Early Access player", "Everyone who reported a bug", "", "", "",
            "#THANKS FOR PLAYING", "*CHAPTER 2 IS COMING", "", "",
        };

        // ------------------------------------------------------------------ missions

        public static StoryPack Mission(int n)
        {
            switch (n)
            {
                case 2: return Docks();
                case 3: return Warehouse();
                default: return Rooftop();
            }
        }

        const string M = BigMarcus.Name;

        static CustomLook Look(Color skin, Color shirt, Color pants, Color hair, int hairStyle, bool beard, bool sleeves, bool glasses, float height, float build, int armor = 0)
            => new CustomLook { Skin = skin, Shirt = shirt, Pants = pants, Shoes = new Color(0.08f, 0.08f, 0.08f), Hair = hair, Eyes = new Color(0.2f, 0.13f, 0.07f), HairStyle = hairStyle, Beard = beard, LongSleeves = sleeves, Glasses = glasses, Height = height, Build = build, Armor = armor };

        static StoryPack Docks()
        {
            var w = new StoryWriter("The Docks", DocksMap.MapId, (int)TimeOfDay.Night).Weapons("fists", "knife", "glock", "uzi", "mac11", "shotgun");
            var rico = Look(new Color(0.72f, 0.52f, 0.38f), new Color(0.95f, 0.5f, 0.1f), new Color(0.15f, 0.2f, 0.35f), new Color(0.08f, 0.06f, 0.05f), 0, true, false, false, 0.98f, 1.1f);
            var a = w.Actor("rico", "Rico", ActorRole.Talker, new Vector3(-21.5f, 0, 11f), 270f, true, rico);
            w.Actor("guard1", "Dock Guard", ActorRole.Gunman, new Vector3(14f, 0.05f, 40f), 250f, false, null, 11);
            w.Actor("guard2", "Dock Guard", ActorRole.Gunman, new Vector3(-1f, 0.05f, 24f), 200f, false, null, 12);
            w.Actor("crew1", "Z's Crew", ActorRole.Gunman, new Vector3(46f, 0.05f, 54f), 220f, false, null, 21);
            w.Actor("crew2", "Z's Crew", ActorRole.Gunman, new Vector3(50f, 0.05f, 48f), 240f, false, null, 22);
            w.Actor("crew3", "Z's Crew", ActorRole.Brawler, new Vector3(30f, 0.05f, 30f), 60f, false, null, 23);
            w.Actor("crewboss", "Crew Boss", ActorRole.Soldier, new Vector3(44f, 0.05f, 58f), 200f, false,
                Look(new Color(0.91f, 0.72f, 0.58f), new Color(0.12f, 0.12f, 0.13f), new Color(0.1f, 0.1f, 0.11f), new Color(0.6f, 0.55f, 0.5f), 1, true, true, false, 1.08f, 1.3f, 2));

            w.Shot(new Vector3(-58, 16, -4), new Vector3(0, 4, 30), new Vector3(-36, 9, 4), new Vector3(30, 2, 40), 6f, "THE DOCKS  -  2:14 AM")
             .Shot(new Vector3(10, 12, -2), new Vector3(-20, 8, -17), new Vector3(-10, 10, -1), new Vector3(-36, 12, -17), 4f, "")
             .Banner("MISSION 2", "THE DOCKS")
             .Chat(M, "Z had one address in his phone. The docks. Somebody's moving guns through there.")
             .Chat(M, "Find Rico at the dock office. He sees everything that comes in.")
             .Talk("rico", "Find Rico at the dock office")
             .Say("Rico", "Whoa, whoa. You're the one who dropped Z? Man, you're crazy.")
             .Choice("Rico", "So what do you want from me?", "PAY HIM $200", "rico_pay", "THREATEN HIM", "rico_threat", "FORGET IT", "rico_leave")
             .Label("rico_pay").Flag("paid_rico")
             .Say("Rico", "Container seven. Blue one, end of the yard. They load at three. You didn't hear it from me.").Jump("yard")
             .Label("rico_threat").Flag("scared_rico")
             .Say("Rico", "Okay, okay! Container seven! The blue one! Put that away, man!").Jump("yard")
             .Label("rico_leave")
             .Say("Rico", "Suit yourself. You'll never find it.")
             .Say("You", "...I'll check the containers myself.")
             .Label("yard")
             .Spawn("guard1").Spawn("guard2")
             .Kill("enemies", "Clear the container yard")
             .GoTo(DocksMap.Container7 + new Vector3(0, 0, -5f), 3.5f, "Find container 7")
             .Shot(DocksMap.Container7 + new Vector3(-7, 3, -9), DocksMap.Container7 + new Vector3(0, 1.5f, -2), DocksMap.Container7 + new Vector3(-4, 4, -12), DocksMap.Container7 + new Vector3(6, 1, 14), 3f, "Headlights...")
             .Spawn("crew1").Spawn("crew2").Spawn("crew3").Spawn("crewboss")
             .Say("Crew Boss", "That's the guy who killed Z! Light him up!")
             .Music("fight")
             .Banner("AMBUSH!", "Z's crew wants payback")
             .Kill("enemies", "Survive the ambush")
             .Music("")
             .Wait(1f)
             .Say("You", "A ledger... payments, shipments, names. And one name on every page: VEX.")
             .Chat(M, "VEX?? Man... Vex runs the whole east side. Z was just his dog.")
             .Chat(M, "His people do business at Warehouse 13. Meet me there.")
             .Pass("You found Vex's ledger.");
            return w.Pack;
        }

        static StoryPack Warehouse()
        {
            var w = new StoryWriter("The Warehouse", "warehouse", (int)TimeOfDay.Day).Weapons("fists", "knife", "glock", "uzi", "mac11", "shotgun", "ar15");
            w.Actor("marcus", M, ActorRole.Talker, new Vector3(3f, 0, -41f), 200f).look = "@marcus";
            var dez = Look(new Color(0.98f, 0.86f, 0.78f), new Color(0.85f, 0.85f, 0.8f), new Color(0.3f, 0.3f, 0.32f), new Color(0.3f, 0.2f, 0.1f), 0, false, true, true, 0.95f, 0.85f);
            w.Actor("dez", "Dez", ActorRole.Civilian, new Vector3(-22f, 0.05f, 44f), 135f, true, dez);
            // Quiet: two on the floor, one on the catwalk. Loud: everybody.
            w.Actor("g1", "Vex's Guard", ActorRole.Gunman, new Vector3(0.5f, 0.05f, -6f), 180f, false, null, 31);
            w.Actor("g2", "Vex's Guard", ActorRole.Gunman, new Vector3(-17.5f, 0.05f, 12f), 160f, false, null, 32);
            w.Actor("g3", "Vex's Guard", ActorRole.Gunman, new Vector3(27f, 4.35f, 2f), 200f, false, null, 33);
            w.Actor("l1", "Vex's Soldier", ActorRole.Soldier, new Vector3(14f, 0.05f, 10f), 200f, false, null, 34);
            w.Actor("l2", "Vex's Guard", ActorRole.Brawler, new Vector3(-8.5f, 0.05f, 2f), 180f, false, null, 35);
            w.Actor("l3", "Vex's Soldier", ActorRole.Soldier, new Vector3(16f, 0.05f, 40f), 220f, false, null, 36);

            w.Shot(new Vector3(-26, 8, -46), new Vector3(0, 2, 0), new Vector3(22, 7, -40), new Vector3(0, 2, 12), 6f, "WAREHOUSE 13  -  3:30 AM")
             .Banner("MISSION 3", "THE WAREHOUSE")
             .Talk("marcus", "Talk to Big Marcus")
             .Say(M, "Vex's bookkeeper is in that office at the back. He knows where Vex sleeps.")
             .Choice(M, "So. We going in quiet, or loud?", "QUIET", "quiet", "LOUD", "loud")
             .Label("quiet")
             .Say(M, "Smart. Take them out one at a time. I'll watch the door.")
             .Spawn("g1").Spawn("g2")
             .Kill("enemies", "Take out the guards")
             .Spawn("g3")
             .Banner("ONE MORE", "On the catwalk")
             .Kill("enemies", "Take out the guard on the catwalk")
             .Jump("office")
             .Label("loud")
             .Say(M, "My kind of plan.")
             .Music("fight")
             .Spawn("g1").Spawn("g2").Spawn("g3").Spawn("l1").Spawn("l2").Spawn("l3")
             .Banner("GO LOUD", "Six of Vex's men")
             .Kill("enemies", "Kill Vex's men")
             .Music("")
             .Label("office")
             .Talk("dez", "Find the bookkeeper in the office")
             .Say("Dez", "Please! Don't shoot! I just count the money!")
             .Choice("Dez", "I'll tell you anything! Anything!", "WHERE IS VEX?", "")
             .Say("Dez", "The Meridian Tower! The roof! He's flying out at sunrise!")
             .Choice("Dez", "That's all I know, I swear! Please...", "LET HIM GO", "spare", "FINISH HIM", "finish")
             .Label("spare").Flag("spared_dez")
             .Say("Dez", "Thank you... thank you. I'll disappear, I swear.")
             .Say("You", "Get out of here before I change my mind.")
             .Fade(1.2f, "Dez runs for the back door...")
             .Remove("dez")
             .Jump("after")
             .Label("finish").Flag("killed_dez")
             .Kill("dez", "Finish Dez")
             .Label("after")
             .Wait(1f)
             .Chat(M, "Meridian Tower. I'll get us a ride. Tonight we finish this.")
             .Pass("Vex is on the roof.");
            return w.Pack;
        }

        static StoryPack Rooftop()
        {
            var w = new StoryWriter("The Rooftop", RooftopsMap.MapId, (int)TimeOfDay.Night).Weapons("fists", "knife", "glock", "deagle", "shotgun", "ar15", "sniper");
            var helipad = new Vector3(16f, 52f, 15f);
            var vex = Look(new Color(0.91f, 0.72f, 0.58f), new Color(0.1f, 0.1f, 0.12f), new Color(0.06f, 0.06f, 0.07f), new Color(0.05f, 0.04f, 0.03f), 0, false, true, true, 1.06f, 1.15f, 2);
            var b = w.Actor("vex", "Vex", ActorRole.Boss, new Vector3(16f, 52.05f, 19f), 180f, true, vex);
            b.health = 2400f;
            w.Actor("r1", "Vex's Soldier", ActorRole.Soldier, new Vector3(8f, 44.05f, -20f), 250f, true, null, 41);
            w.Actor("r2", "Vex's Guard", ActorRole.Gunman, new Vector3(11f, 44.05f, -25f), 270f, true, null, 42);
            w.Actor("r3", "Vex's Guard", ActorRole.Gunman, new Vector3(8f, 44.05f, -13f), 250f, true, null, 43);
            w.Actor("h1", "Vex's Soldier", ActorRole.Soldier, new Vector3(9f, 52.05f, 9f), 150f, false, null, 44);
            w.Actor("h2", "Vex's Soldier", ActorRole.Soldier, new Vector3(21f, 52.05f, 21f), 220f, false, null, 45);
            w.Actor("marcus", M, ActorRole.Talker, new Vector3(12f, 52.05f, 7f), 30f, false).look = "@marcus";

            w.Shot(new Vector3(-60, 26, -64), helipad + Vector3.up, new Vector3(-22, 58, -14), helipad + Vector3.up, 7f, "MERIDIAN TOWER  -  4:52 AM")
             .Banner("MISSION 4", "THE ROOFTOP")
             .Chat(M, "Elevators are locked, so I hope you like heights. I'm holding the stairs. Go.")
             .Kill("enemies", "Clear the rooftop")
             .Heal()
             .Chat(M, "Patch yourself up. He's on the helipad. Up the bridge.")
             .GoTo(new Vector3(12f, 52f, 6.5f), 3f, "Get to the helipad")
             .Shot(new Vector3(12, 53.8f, 6), new Vector3(16, 53.6f, 19), new Vector3(14, 53.7f, 11), new Vector3(16, 53.6f, 19), 3f, "")
             .Say("Vex", "So you're the one breaking my business. Z. The docks. My warehouse.")
             .IfFlag("spared_dez", "vex_soft")
             .IfFlag("killed_dez", "vex_cold")
             .Jump("vex_offer")
             .Label("vex_soft").Say("Vex", "And Dez is still breathing. Soft. Don't worry, I'll find him.").Jump("vex_offer")
             .Label("vex_cold").Say("Vex", "You even did Dez. Cold. I like that.")
             .Label("vex_offer")
             .Choice("Vex", "Here's the deal. Work for me. Be my right hand. Or go over that edge.", "JOIN HIM", "join", "NEVER", "fight")
             .Label("join")
             .Say("Vex", "Smart kid. Let's go for a ride.")
             .Fade(2.5f, "The helicopter lifts off into the dawn...")
             .Unlock("story_end_join")
             .TheEnd("The New Right Hand", "You took Vex's offer. The east side never found out who brought down Z... because now you work for the man who sent him. Big Marcus stopped answering your calls.")
             .Label("fight")
             .Say("Vex", "Then you go over the edge.")
             .Music("fight")
             .Spawn("h1").Spawn("h2")
             .Banner("KILL VEX", "The man who runs the east side")
             .Kill("vex", "Kill Vex")
             .Kill("enemies", "Finish his guards")
             .Music("")
             .Shot(new Vector3(16, 56, 12), new Vector3(-40, 44, -60), new Vector3(10, 55, 4), new Vector3(-60, 40, -50), 5f, "Sunrise.")
             .Spawn("marcus")
             .Talk("marcus", "Talk to Big Marcus")
             .Say(M, "It's over. Vex is done.")
             .Choice(M, "Nobody's running the east side now... unless it's you.", "TAKE OVER", "king", "WALK AWAY", "walk")
             .Label("king")
             .Say(M, "Ha! That's what I'm talking about. Partner.")
             .Unlock("story_end_king")
             .TheEnd("King of the East Side", "You and Big Marcus took over everything Vex owned. The streets are quiet now. Quiet, because everyone knows your name.")
             .Label("walk")
             .Say(M, "...Yeah. Yeah, I get it. Take care of yourself, man.")
             .Unlock("story_end_walk")
             .TheEnd("Walk Away", "You dropped the gun off the roof and walked down 52 floors. The sun came up. For the first time in a long time, you felt free.");
            return w.Pack;
        }
    }
}
