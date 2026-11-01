using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Story chapter 2, "Cold Blood" (Content Update): four missions on the new maps.
    ///   1 St. Mercy   (Hospital)       - Big Marcus was shot; the doctors aren't doctors. The Surgeon escapes.
    ///   2 Last Train  (Subway)         - catch the Surgeon's courier and his cooler.
    ///   3 Meat        (Slaughterhouse) - free the captives, kill the Butcher; burn it or take the money.
    ///   4 Blackgate   (Prison)         - riot, the Warden, the Surgeon. Three endings.
    /// The opening changes with the chapter 1 ending you reached. Progress and choices are saved.
    /// </summary>
    public static class Chapter2
    {
        public const int MissionCount = 4;
        public static readonly string[] Names = { "St. Mercy", "Last Train", "Meat", "Blackgate" };
        public static readonly string[] Blurbs =
        {
            "Big Marcus took a bullet. He's at St. Mercy... and the doctors there aren't doctors.",
            "The Surgeon's courier is running for the last train. Don't let him reach it.",
            "The cooler had an address: a slaughterhouse. People go in there. Nobody comes out.",
            "The Surgeon is hiding in Blackgate Prison. The Warden is on his payroll.",
        };
        public static readonly string[] EndingIds = { "story2_end_chair", "story2_end_justice", "story2_end_partner" };
        public static readonly string[] EndingNames = { "Cold Blood", "Justice", "The Surgeon's Partner" };
        const string KProgress = "story_ch2_progress", KFlags = "story_ch2_flags";

        public static int Progress
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KProgress, 0), 0, MissionCount);
            set { PlayerPrefs.SetInt(KProgress, Mathf.Clamp(value, 0, MissionCount)); PlayerPrefs.Save(); }
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
            Chapter1.EnsureAchievements();
            void Add(string id, string name, string desc, bool secret = false) { if (!Stats.Achievements.Exists(a => a.Id == id)) Stats.Achievements.Add(new Achievement(id, name, desc, id, 1, secret)); }
            Add("story_ch2_end", "End of Chapter 2", "Finish all four missions of story chapter 2.");
            Add("story2_end_chair", "Cold Blood", "Chapter 2 ending: the electric chair.", true);
            Add("story2_end_justice", "Justice", "Chapter 2 ending: hand the Surgeon over.", true);
            Add("story2_end_partner", "The Surgeon's Partner", "Chapter 2 ending: take his deal.", true);
        }

        static bool Has(string ach) { var a = Stats.Achievements.Find(x => x.Id == ach); return a != null && Stats.IsUnlocked(a); }

        public static void Play(int mission)
        {
            EnsureAchievements();
            mission = Mathf.Clamp(mission, 1, MissionCount);
            var ctx = new StoryContext { Pack = Mission(mission), Mission = mission, OnPassed = c => Completed(c.Mission, c.Flags), OnTheEnd = Finish };
            int m = mission;
            ctx.Retry = () => Play(m);
            foreach (var f in LoadFlags()) ctx.Flags.Add(f);
            // Chapter 1 carries over.
            if (Has("story_end_king")) ctx.Flags.Add("ch1_king");
            else if (Has("story_end_join")) ctx.Flags.Add("ch1_join");
            else if (Has("story_end_walk")) ctx.Flags.Add("ch1_walk");
            StoryRunner.Play(ctx);
        }

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
            UIRoot.Push(new TheEndScreen(ending.who, ending.text, "END OF CHAPTER 2", () =>
                UIRoot.Push(new CreditsRollScreen(Credits, () =>
                {
                    EnsureAchievements();
                    GameManager.I.ReturnToMenu();
                    Stats.UnlockById("story_ch2_end");
                    UIRoot.Push(new StoryOrbieScreen("That's the end of chapter 2! Chapter 3 is coming one day. Thanks for playing!"));
                }))));
        }

        static readonly string[] Credits =
        {
            "#GORE SANDBOX", "*CHAPTER 2", "COLD BLOOD", "", "",
            "*A " + GameInfo.Studio.ToUpperInvariant() + " PRODUCTION", "", "",
            "*OWNER - DEVELOPER", "Quibbo / DrPhantom", "",
            "*DEVELOPER", "AlexKiller", "", "",
            "*STARRING", "Big Marcus", "Nurse Jo", "The Courier", "The Butcher", "The Warden", "The Surgeon", "", "*AND", "You", "", "",
            "*MAPS", "St. Mercy Hospital", "Subway Station", "The Slaughterhouse", "Blackgate Prison", "", "",
            "*EVERY SOUND", "Synthesised live by the game", "",
            "*EVERY MODEL AND TEXTURE", "Generated procedurally at runtime", "", "",
            "*SPECIAL THANKS", "Everyone in the Discord", "Every Early Access player", "", "", "",
            "#THANKS FOR PLAYING", "*CHAPTER 3 IS COMING", "", "",
        };

        public static StoryPack Mission(int n)
        {
            switch (n)
            {
                case 1: return Hospital();
                case 2: return Subway();
                case 3: return Slaughterhouse();
                default: return Prison();
            }
        }

        const string M = BigMarcus.Name;

        static CustomLook Look(Color shirt, Color pants, bool sleeves = true, bool glasses = false, float height = 1f, float build = 1f, int armor = 0, int hair = 0, Color? skin = null)
            => new CustomLook { Skin = skin ?? new Color(0.88f, 0.7f, 0.56f), Shirt = shirt, Pants = pants, Shoes = new Color(0.08f, 0.08f, 0.08f), Hair = new Color(0.12f, 0.09f, 0.07f), Eyes = new Color(0.2f, 0.13f, 0.07f), HairStyle = hair, LongSleeves = sleeves, Glasses = glasses, Height = height, Build = build, Armor = armor };

        static readonly Color Scrubs = new Color(0.35f, 0.6f, 0.62f), White = new Color(0.94f, 0.95f, 0.96f);

        // ------------------------------------------------------------------ 1 St. Mercy

        static StoryPack Hospital()
        {
            var w = new StoryWriter("St. Mercy", "hospital", (int)TimeOfDay.Night).Weapons("fists", "knife", "glock", "deagle", "shotgun", "nailgun");
            w.Actor("jo", "Nurse Jo", ActorRole.Civilian, new Vector3(3f, 0.05f, -3f), 200f, true, Look(new Color(0.55f, 0.75f, 0.9f), new Color(0.55f, 0.75f, 0.9f), false, false, 0.95f, 0.9f, 0, 3, new Color(0.7f, 0.5f, 0.38f)));
            w.Actor("marcus_ward", M, ActorRole.Talker, new Vector3(-2.5f, 0.05f, 8f), 180f, false).look = "@marcus";
            w.Actor("marcus_slab", M, ActorRole.Talker, new Vector3(12f, 0.05f, -7f), 180f, false).look = "@marcus";
            w.Actor("d1", "\"Doctor\"", ActorRole.Gunman, new Vector3(9f, 0.05f, -11f), 0f, false, Look(Scrubs, Scrubs), 51);
            w.Actor("d2", "\"Doctor\"", ActorRole.Gunman, new Vector3(16f, 0.05f, -12f), 0f, false, Look(Scrubs, Scrubs), 52);
            w.Actor("d3", "Orderly", ActorRole.Brawler, new Vector3(12f, 0.05f, -4.5f), 180f, false, Look(White, White, false, false, 1.1f, 1.3f), 53);
            w.Actor("s1", "Surgeon's Guard", ActorRole.Soldier, new Vector3(-10f, 4.55f, -5f), 90f, false, Look(Scrubs, Scrubs, true, false, 1.05f, 1.2f, 2), 54);
            w.Actor("s2", "Surgeon's Guard", ActorRole.Soldier, new Vector3(0f, 4.55f, 6f), 180f, false, Look(Scrubs, Scrubs, true, false, 1.05f, 1.2f, 2), 55);
            w.Actor("s3", "Surgeon's Guard", ActorRole.Gunman, new Vector3(-14f, 4.55f, 8f), 120f, false, Look(Scrubs, Scrubs), 56);
            w.Actor("s4", "Surgeon's Guard", ActorRole.Gunman, new Vector3(5f, 4.55f, -8f), 270f, false, Look(Scrubs, Scrubs), 57);
            w.Actor("surgeon", "The Surgeon", ActorRole.Talker, new Vector3(-16f, 4.55f, 0f), 90f, false, Look(White, new Color(0.15f, 0.15f, 0.18f), true, true, 1.02f, 0.95f, 0, 2, new Color(0.95f, 0.85f, 0.78f)));

            w.Shot(new Vector3(-17, 3.6f, -13), new Vector3(0, 1, 2), new Vector3(16, 3.6f, -12), new Vector3(0, 1, 6), 6f, "ST. MERCY HOSPITAL  -  1:40 AM")
             .Banner("CHAPTER 2", "COLD BLOOD")
             .IfFlag("ch1_king", "open_king").IfFlag("ch1_join", "open_join").IfFlag("ch1_walk", "open_walk").Jump("open_any")
             .Label("open_king").Chat(M, "Somebody's hitting our corners, boss. Took a bullet finding out who. I'm at St. Mercy.").Jump("open_any")
             .Label("open_join").Chat(M, "I know you work for Vex now. Don't care. I got shot and I need help. St. Mercy.").Jump("open_any")
             .Label("open_walk").Chat(M, "I know you walked away. I'm sorry. I got shot... I'm at St. Mercy. Something's wrong here.").Jump("open_any")
             .Label("open_any")
             .Chat(M, "These doctors... they keep talking about 'harvest night'. Get me out of here.")
             .Talk("jo", "Talk to the nurse at reception")
             .Say("Nurse Jo", "Oh thank god, someone who isn't one of THEM. They moved patients to the morgue. Living patients.")
             .Say("Nurse Jo", "Your friend too. Ward three... or maybe the morgue already. I don't know!")
             .Choice("Nurse Jo", "Please, hurry.", "GO TO THE MORGUE", "morgue", "CHECK THE WARD", "ward")
             .Label("ward").Flag("checked_ward")
             .Spawn("marcus_ward")
             .Talk("marcus_ward", "Find Big Marcus in the ward")
             .Say(M, "You came. Good. They came for me twice already. The morgue... that's where they do it.")
             .Say(M, "I can walk. Barely. Go clear it out, I'll follow you.")
             .Remove("marcus_ward")
             .Label("morgue")
             .Music("fight")
             .Spawn("d1").Spawn("d2").Spawn("d3")
             .Banner("THE MORGUE", "Those aren't doctors")
             .Kill("enemies", "Clear the morgue")
             .Music("")
             .IfFlag("checked_ward", "morgue_after")
             .Spawn("marcus_slab")
             .Talk("marcus_slab", "Get Big Marcus off the slab")
             .Say(M, "Ten more minutes and I'd be in jars. Thanks.")
             .Label("morgue_after")
             .Chat(M, "Their boss is upstairs. They call him the Surgeon. Take the lift.")
             .GoTo(new Vector3(6f, 4.6f, 0f), 7f, "Take the lift or the stairs to the second floor")
             .Spawn("s1").Spawn("s2").Spawn("s3").Spawn("s4").Spawn("surgeon")
             .Shot(new Vector3(-8, 7.5f, -6), new Vector3(-16, 5.8f, 0), new Vector3(-11, 6.4f, -2), new Vector3(-16, 5.7f, 0), 3f, "")
             .Say("The Surgeon", "Such a waste. You have such healthy organs.")
             .Say("The Surgeon", "Gentlemen? Keep them fresh.")
             .Fade(1f, "The Surgeon slips away down the back stairs...")
             .Remove("surgeon")
             .Music("fight")
             .Banner("FLOOR 2", "His guards")
             .Kill("enemies", "Kill the Surgeon's guards")
             .Music("")
             .Wait(1f)
             .Say("You", "A phone on the guard... 'Courier leaving with the cooler. Last train, 3 AM.'")
             .Chat(M, "The subway. Go. I'll be fine. Go!")
             .Pass("The Surgeon got away. His courier didn't. Yet.");
            return w.Pack;
        }

        // ------------------------------------------------------------------ 2 Last Train

        static StoryPack Subway()
        {
            var w = new StoryWriter("Last Train", "subway", (int)TimeOfDay.Night).Weapons("fists", "knife", "glock", "mp5", "shotgun", "crossbow");
            w.Actor("courier", "The Courier", ActorRole.Civilian, new Vector3(34f, 0.05f, -7f), 270f, false, Look(new Color(0.15f, 0.35f, 0.2f), new Color(0.2f, 0.2f, 0.25f), true, false, 0.95f, 0.9f));
            w.Actor("g1", "Courier Guard", ActorRole.Gunman, new Vector3(20f, 0.05f, 7f), 180f, false, null, 61);
            w.Actor("g2", "Courier Guard", ActorRole.Gunman, new Vector3(28f, 0.05f, 8f), 200f, false, null, 62);
            w.Actor("g3", "Courier Guard", ActorRole.Gunman, new Vector3(12f, 0.05f, -8f), 90f, false, null, 63);
            w.Actor("b1", "Tunnel Rat", ActorRole.Brawler, new Vector3(-30f, 0.05f, -6f), 90f, false, null, 64);
            w.Actor("b2", "Tunnel Rat", ActorRole.Brawler, new Vector3(-28f, 0.05f, -8f), 90f, false, null, 65);
            w.Actor("b3", "Tunnel Rat", ActorRole.Brawler, new Vector3(-32f, 0.05f, -9f), 90f, false, null, 66);
            w.Actor("b4", "Surgeon's Guard", ActorRole.Soldier, new Vector3(-26f, 0.05f, 7f), 180f, false, Look(Scrubs, Scrubs, true, false, 1.05f, 1.2f, 2), 67);

            w.Shot(new Vector3(-36, 4.5f, -9), new Vector3(10, 0.5f, 0), new Vector3(30, 4f, -9), new Vector3(0, 0.5f, 0), 6f, "SUBWAY STATION  -  2:55 AM")
             .Banner("MISSION 2", "LAST TRAIN")
             .Chat(M, "The courier's got a red cooler. Whatever's in it, the Surgeon wants it badly.")
             .Chat(M, "And watch the tracks. Third rail's live and the trains don't brake.")
             .Spawn("g1").Spawn("g2").Spawn("g3")
             .Music("fight")
             .Kill("enemies", "Kill the courier's guards")
             .Music("")
             .Spawn("courier")
             .GoTo(new Vector3(32f, 0.05f, -7f), 4f, "Catch the courier at the end of the platform")
             .Talk("courier", "Grab the courier")
             .Say("The Courier", "Don't! Don't! I just carry the box! I don't even look inside!")
             .Choice("The Courier", "Take it. Just take it and let me go.", "TAKE THE COOLER", "spare", "KILL HIM", "kill")
             .Label("spare").Flag("spared_courier")
             .Say("You", "Run. And don't come back to this city.")
             .Remove("courier").Jump("cooler")
             .Label("kill").Flag("killed_courier")
             .Kill("courier", "Kill the courier")
             .Label("cooler")
             .Say("You", "Ice... and a heart. A human heart. And a label: 'Delivery - Hollis Meat Packing'.")
             .Chat(M, "Hollis... the old slaughterhouse on the river. Oh man.")
             .Spawn("b1").Spawn("b2").Spawn("b3").Spawn("b4")
             .Music("fight")
             .Banner("AMBUSH", "From the tunnel")
             .Kill("enemies", "Survive the ambush")
             .Music("")
             .Pass("Next stop: the slaughterhouse.");
            return w.Pack;
        }

        // ------------------------------------------------------------------ 3 Meat

        static StoryPack Slaughterhouse()
        {
            var w = new StoryWriter("Meat", "slaughterhouse", (int)TimeOfDay.Night).Weapons("fists", "cleaver", "glock", "ak47", "shotgun", "flamer");
            var captive = Look(new Color(0.6f, 0.6f, 0.62f), new Color(0.45f, 0.45f, 0.48f), false);
            w.Actor("c1", "Captive", ActorRole.Civilian, new Vector3(13.5f, 0.05f, 12.5f), 180f, true, captive, 71);
            w.Actor("c2", "Captive", ActorRole.Civilian, new Vector3(17.5f, 0.05f, 14f), 200f, true, captive, 72);
            w.Actor("c3", "Captive", ActorRole.Civilian, new Vector3(21f, 0.05f, 11.5f), 160f, true, captive, 73);
            var apron = Look(new Color(0.85f, 0.85f, 0.82f), new Color(0.2f, 0.2f, 0.22f), false, false, 1.05f, 1.25f);
            w.Actor("w1", "Butcher's Boy", ActorRole.Gunman, new Vector3(-5f, 0.05f, -10f), 90f, false, apron, 74);
            w.Actor("w2", "Butcher's Boy", ActorRole.Gunman, new Vector3(5f, 0.05f, 10f), 200f, false, apron, 75);
            w.Actor("w3", "Butcher's Boy", ActorRole.Brawler, new Vector3(0f, 0.05f, -2f), 270f, false, apron, 76);
            w.Actor("w4", "Butcher's Boy", ActorRole.Brawler, new Vector3(17f, 0.05f, -12f), 270f, false, apron, 77);
            w.Actor("w5", "Butcher's Boy", ActorRole.Gunman, new Vector3(-12f, 0.05f, 10f), 135f, false, apron, 78);
            var butcher = w.Actor("butcher", "The Butcher", ActorRole.Boss, new Vector3(2f, 0.05f, 2f), 270f, false, Look(new Color(0.85f, 0.8f, 0.75f), new Color(0.15f, 0.12f, 0.1f), false, false, 1.18f, 1.45f, 2, 2));
            butcher.health = 1800f;

            w.Shot(new Vector3(-22, 6, -14), new Vector3(0, 2, 4), new Vector3(20, 5, -12), new Vector3(0, 2, 6), 6f, "HOLLIS MEAT PACKING  -  3:40 AM")
             .Banner("MISSION 3", "MEAT")
             .Chat(M, "The cooler came from here. If there are people inside... get them out.")
             .Spawn("w1").Spawn("w2").Spawn("w3").Spawn("w4").Spawn("w5")
             .Music("fight")
             .Kill("enemies", "Clear the processing floor")
             .Music("")
             .GoTo(new Vector3(17.5f, 0.05f, 9.8f), 3f, "Get into the freezer")
             .Talk("c1", "Free the first captive")
             .Say("Captive", "You're real? You're real. Thank you.").Remove("c1")
             .Talk("c2", "Free the second captive")
             .Say("Captive", "He takes one of us every night. I could hear the grinder...").Remove("c2")
             .Talk("c3", "Free the last captive")
             .Say("Captive", "The Butcher's coming. I hear him. RUN!").Remove("c3")
             .Spawn("butcher")
             .Shot(new Vector3(8, 3, -3), new Vector3(2, 1.8f, 2), new Vector3(6, 2.6f, -1), new Vector3(2, 1.8f, 2), 3f, "")
             .Say("The Butcher", "Fresh meat walked in by itself. Saves me a trip.")
             .Music("fight")
             .Banner("THE BUTCHER", "Mind the grinder")
             .Kill("butcher", "Kill the Butcher")
             .Music("")
             .Wait(1f)
             .Say("You", "His ledger. 'Blackgate - medical wing - Warden takes 30%'. The Surgeon's in the prison.")
             .Choice("You", "And a safe full of cash...", "BURN IT ALL DOWN", "burn", "TAKE THE MONEY", "money")
             .Label("burn").Flag("burned_plant")
             .Fade(2f, "The slaughterhouse burns behind you.")
             .Jump("done")
             .Label("money").Flag("took_money")
             .Say("You", "Nobody needs to know.")
             .Label("done")
             .Chat(M, "Blackgate? The Warden's in on it? ...Man. Okay. Let's end this.")
             .Pass("The Surgeon is hiding in Blackgate.");
            return w.Pack;
        }

        // ------------------------------------------------------------------ 4 Blackgate

        static StoryPack Prison()
        {
            var w = new StoryWriter("Blackgate", "prison", (int)TimeOfDay.Night).Weapons("fists", "katana", "glock", "m4a1", "aa12", "freezeray");
            var orange = Look(new Color(0.95f, 0.45f, 0.08f), new Color(0.95f, 0.45f, 0.08f), false, false, 1f, 1.1f, 0, 1);
            for (int i = 0; i < 5; i++) w.Actor("p" + i, "Rioter", ActorRole.Brawler, new Vector3(-10 + i * 5f, 0.05f, -32f), 0f, false, orange, 81 + i);
            var guardLook = Look(new Color(0.15f, 0.2f, 0.35f), new Color(0.1f, 0.12f, 0.2f), true, false, 1.03f, 1.15f, 1);
            w.Actor("g1", "Corrupt Guard", ActorRole.Gunman, new Vector3(-8f, 0.05f, 0f), 180f, false, guardLook, 91);
            w.Actor("g2", "Corrupt Guard", ActorRole.Gunman, new Vector3(8f, 0.05f, 2f), 180f, false, guardLook, 92);
            w.Actor("g3", "Corrupt Guard", ActorRole.Soldier, new Vector3(0f, 4.3f, 6f), 180f, false, guardLook, 93);
            w.Actor("g4", "Corrupt Guard", ActorRole.Gunman, new Vector3(-12f, 0.05f, 2f), 90f, false, guardLook, 94);
            var warden = w.Actor("warden", "The Warden", ActorRole.Boss, new Vector3(-29f, 0.05f, -3f), 90f, false, Look(new Color(0.12f, 0.14f, 0.25f), new Color(0.08f, 0.08f, 0.12f), true, false, 1.08f, 1.3f, 2, 2));
            warden.health = 1600f;
            w.Actor("surgeon", "The Surgeon", ActorRole.Talker, new Vector3(-27.5f, 0.05f, 2.5f), 120f, false, Look(White, new Color(0.15f, 0.15f, 0.18f), true, true, 1.02f, 0.95f, 0, 2, new Color(0.95f, 0.85f, 0.78f)));
            w.Actor("marcus", M, ActorRole.Talker, new Vector3(-21.5f, 0.05f, -1.5f), 270f, false).look = "@marcus";

            w.Shot(new Vector3(28, 12, -48), new Vector3(0, 2, -15), new Vector3(-28, 9, -46), new Vector3(0, 3, -10), 7f, "BLACKGATE PRISON  -  4:30 AM")
             .Banner("MISSION 4", "BLACKGATE")
             .Chat(M, "I paid a guy to open the cells. In about ten seconds... it's gonna get loud.")
             .Wait(1.5f)
             .Spawn("p0").Spawn("p1").Spawn("p2").Spawn("p3").Spawn("p4")
             .Music("fight")
             .Banner("RIOT!", "Everybody's out")
             .Kill("enemies", "Survive the riot in the yard")
             .Heal()
             .GoTo(new Vector3(0f, 0.05f, -11f), 3.5f, "Get into the cell block")
             .Spawn("g1").Spawn("g2").Spawn("g3").Spawn("g4")
             .Kill("enemies", "Kill the Warden's guards")
             .Music("")
             .GoTo(new Vector3(-21f, 0.05f, 0f), 3f, "Go to the execution room")
             .Spawn("warden").Spawn("surgeon")
             .Shot(new Vector3(-22, 3.2f, 4), new Vector3(-28, 1.6f, 0), new Vector3(-23, 2.4f, -4), new Vector3(-28, 1.6f, 0), 3f, "")
             .Say("The Warden", "You should've stayed in your cell, whoever you are. Doctor, get behind me.")
             .Say("The Surgeon", "Do be careful with the face, Warden. I have a buyer for the eyes.")
             .Music("fight")
             .Banner("THE WARDEN", "Thirty percent of every life")
             .Kill("warden", "Kill the Warden")
             .Music("")
             .Spawn("marcus")
             .Talk("surgeon", "Corner the Surgeon")
             .Say("The Surgeon", "Wait. Wait. Let's be civilised. Everyone has a price.")
             .IfFlag("took_money", "deal_money").Jump("deal")
             .Label("deal_money").Say("The Surgeon", "You took the Butcher's money. You're already one of us. Admit it.")
             .Label("deal")
             .Say("The Surgeon", "Ten million. A new face. A new name. Partners.")
             .Say(M, "Your call. After everything... it's your call.")
             .Choice("The Surgeon", "Well?", "THE ELECTRIC CHAIR", "chair", "HAND HIM TO THE POLICE", "police", "TAKE HIS DEAL", "deal_yes")
             .Label("chair")
             .Say("You", "You like taking things out of people. Let's see what comes out of you.")
             .Fade(3f, "The lights in Blackgate flicker. Once. Twice. Then go dark.")
             .Unlock("story2_end_chair")
             .TheEnd("Cold Blood", "The Surgeon sat in his own chair. The prison lights flickered for a whole minute. Nobody in Port Eclipse ever went missing on harvest night again.")
             .Label("police")
             .Say("You", "No. You're going to court. Everyone's going to hear what you did.")
             .Say(M, "...Good. That's good. Let them all see.")
             .Unlock("story2_end_justice")
             .TheEnd("Justice", "The trial lasted three weeks. Every name in the Surgeon's books went down with him: guards, doctors, the Warden's friends. For once, the city got it right.")
             .Label("deal_yes")
             .Say("You", "Ten million. And I pick the face.")
             .Say(M, "...Wow. Okay. I never knew you at all, did I?")
             .Unlock("story2_end_partner")
             .TheEnd("The Surgeon's Partner", "Big Marcus walked out of Blackgate alone. You walked out with a new face and a very rich business partner. Somewhere in the city, the harvest starts again.");
            return w.Pack;
        }
    }
}
