using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Secrets. Each one is counted once per profile (see Stats.FoundEasterEgg). Hints show in the dev
    /// console ('eggs') and the stats screen without spoiling the trigger.
    /// </summary>
    public static class EasterEggs
    {
        public sealed class Egg { public string Id, Hint; }

        public static readonly List<Egg> All = new List<Egg>
        {
            new Egg { Id = "Konami", Hint = "An old cheat code on the main menu opens a door that shouldn't exist." },
            new Egg { Id = "Nextbots", Hint = "Type their name on the main menu." },
            new Egg { Id = "Total Eclipse", Hint = "Type the studio's last name on the main menu." },
            new Egg { Id = "Bleeding Title", Hint = "The title can only take so many clicks." },
            new Egg { Id = "You're Welcome", Hint = "Give yourself some credit. Three times." },
            new Egg { Id = "Menu Bully", Hint = "The guy on the menu has a limit." },
            new Egg { Id = "Owner", Hint = "Say hello to the owner in the dev console." },
            new Egg { Id = "Developer", Hint = "Say hello to the developer in the dev console." },
            new Egg { Id = "Nuke", Hint = "The dev console has a very big button." },
            new Egg { Id = "Quack", Hint = "Type a bird on the main menu." },
            new Egg { Id = "Gore", Hint = "Type the obvious word on the main menu." },
            new Egg { Id = "Unbreakable", Hint = "Type a certain movie-inspired word on the main menu." },
            new Egg { Id = "Duck Collector", Hint = "Rubber ducks hide on every map. Find five." },
            new Egg { Id = "Pacifist", Hint = "Calm a Goredoll down with the Admin Gun." },
            new Egg { Id = "Surgeon", Hint = "Stop someone's bleeding with a tourniquet." },
            new Egg { Id = "Lazarus", Hint = "Bring the same Goredoll back from the dead three times." },
        };

        static readonly Dictionary<string, System.Action> Words = new Dictionary<string, System.Action>
        {
            { "nextbot", () => { if (Found("Nextbots", "Nextbots unlocked! Toggle them in map options, or spawn them from the Entities tab.")) AudioManager.Ui("jumpscare", 0.4f); } },
            { "eclipse", () => { Found("Total Eclipse", "Sometimes, now, the sun goes dark."); AudioManager.Ui("logo_impact"); } },
            { "quack", () => { Found("Quack", "Quack."); AudioManager.Ui("duck"); } },
            { "duck", () => AudioManager.Ui("duck") },
            { "gore", () => { Found("Gore", "Yes. That's the game."); AudioManager.Ui("splat"); } },
            { "drphantom", () => Toasts.Show("HELLO, BOSS", "Quibbo/DrPhantom — Owner-Developer. The F5 panel is waiting.", UI.Accent2) },
            { "alexkiller", () => Toasts.Show("HEY ALEX", "AlexKiller — Developer. Back to fixing ragdolls?", UI.Accent2) },
            { "unbreakable", () => { Found("Unbreakable", "Invincibility will now start ON in every map."); PlayerPrefs.SetInt("start_god", 1); } },
            { "boing", () => AudioManager.Ui("boing") },
            { "bell", () => AudioManager.Ui("bell") },
            { "scream", () => AudioManager.Play2D("v_scream", 0.6f, 1f, AudioBus.Voice) },
        };

        static readonly Dictionary<Goredoll, int> revives = new Dictionary<Goredoll, int>();

        public static void Init() => revives.Clear();

        /// <summary>Called when a Goredoll is brought back from the dead.</summary>
        public static void OnRevived(Goredoll g)
        {
            revives.TryGetValue(g, out var n);
            revives[g] = ++n;
            if (n >= 3) Found("Lazarus", g.DisplayName + " has died and come back three times. Let them rest.");
        }

        /// <summary>Marks an egg as found and pops a toast. Returns false if it was already found.</summary>
        public static bool Found(string id, string description)
        {
            if (!Stats.FoundEasterEgg(id)) return false;
            Toasts.Show("EASTER EGG: " + id.ToUpperInvariant(), description, UI.Warn);
            AudioManager.Ui("ui_achievement");
            return true;
        }

        public static void Konami()
        {
            AudioManager.Ui("logo_impact");
            if (Found("Konami", "Up Up Down Down Left Right Left Right B A - The lights in the Backrooms buzz a little louder now. Watch your back."))
                CameraFX.Flash(new Color(1f, 0.9f, 0.3f), 0.6f);
            else Toasts.Show("CHEAT CODE", "You already know this one.", UI.Muted);
        }

        public static void CheckTyped(ref string typed)
        {
            foreach (var kv in Words)
            {
                if (!typed.EndsWith(kv.Key)) continue;
                kv.Value();
                typed = "";
                return;
            }
        }

        /// <summary>Applied when a map starts (e.g. "unbreakable").</summary>
        public static void OnMapStarted()
        {
            if (PlayerPrefs.GetInt("start_god", 0) == 1) Rules.Invincible = true;
        }
    }
}
