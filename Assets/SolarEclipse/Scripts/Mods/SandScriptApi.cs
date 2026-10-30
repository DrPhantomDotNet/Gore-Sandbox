using System;
using System.Collections.Generic;

namespace SolarEclipse.Mods
{
    /// <summary>Which kind of mod a SandScript belongs to (decides which commands it may use).</summary>
    [Flags]
    public enum ScriptTarget { Npc = 1, Weapon = 2, Both = 3 }

    /// <summary>
    /// Everything a SandScript is allowed to touch. There is NO other way out of the sandbox: the interpreter can only call
    /// the commands listed here, and the game side (ISandHost) clamps every number to the caps below (anti-cheat).
    /// This file has no Unity dependency so it can be tested offline.
    /// </summary>
    public static class SandApi
    {
        // ------------------------------------------------------------------ sandbox limits
        public const int MaxSourceBytes = 8 * 1024;
        public const int MaxLines = 400;
        public const int MaxLineLength = 200;
        public const int MaxVariables = 64;
        public const int MaxStringLength = 200;
        public const int MaxNesting = 12;            // nested blocks (if inside repeat inside ...)
        public const int MaxExprDepth = 32;          // nested expression depth (also the call-depth limit)
        public const int StepsPerFrame = 2000;       // all threads of one script together, per frame
        public const int MaxStepsWithoutWait = 20000;// one event that never waits gets stopped
        public const int MaxRepeat = 1000;           // repeat N is capped
        public const int MaxLoopIterations = 10000;  // while loops
        public const int MaxThreads = 8;             // events running at the same time (others are skipped)
        public const int MaxErrors = 5;              // after this many runtime errors the script is switched off
        public const double MaxWait = 60, MinEvery = 0.1;

        /// <summary>A built-in command or function. Arg kinds: n number, s name or text, e entity (player/target/...), a any.
        /// A '?' after an arg makes it (and the rest) optional.</summary>
        public sealed class Cmd
        {
            public string Name, Args, Help, Example, Category;
            public ScriptTarget Target;
            public bool IsFunc;
            public int MinArgs { get { int i = Args.IndexOf('?'); return i < 0 ? Args.Length : i - 1; } }
            public int MaxArgs => Args.Replace("?", "").Length;
            public char Kind(int i) { var a = Args.Replace("?", ""); return i < a.Length ? a[i] : 'a'; }
        }

        public sealed class Prop
        {
            public string Name, Help;
            public ScriptTarget Target;
            public bool Settable, IsText;
            public double Min, Max;
        }

        public sealed class EventSpec
        {
            public string Name, Help, Example;   // Name is the words after "on" (e.g. "see player"), Example is the full header
            public ScriptTarget Target;
            public bool HasNumber;              // "every 2", "near player 5"
            public double NumMin, NumMax;
        }

        public static readonly List<Cmd> Commands = new List<Cmd>();
        public static readonly List<Prop> Props = new List<Prop>();
        public static readonly List<EventSpec> Events = new List<EventSpec>();
        static readonly Dictionary<string, Cmd> cmdMap = new Dictionary<string, Cmd>();
        static readonly Dictionary<string, Prop> propMap = new Dictionary<string, Prop>();

        public static readonly string[] EntityWords = { "player", "target", "npc", "attacker", "self" };
        public static readonly string[] Sounds = { "scream", "yell", "laugh", "hey", "no", "argh", "growl", "shriek", "beep", "zap", "boom", "punch", "slash", "gunshot", "click", "whoosh" };
        public static readonly string[] Effects = { "blood", "sparks", "smoke", "fire", "dust" };
        public static readonly string[] Projectiles = { "bullet", "pellets", "fireball", "spark", "rocket" };
        public static readonly string[] Teams = { "neutral", "hostile", "friendly" };
        public static readonly string[] ColorNames = { "red", "green", "blue", "yellow", "orange", "purple", "white", "black", "grey", "pink", "cyan", "brown" };

        // Anti-cheat caps for command arguments (the host clamps to these as well).
        public const double CapDamage = 60, CapKnockback = 30, CapExplode = 4, CapHeal = 50, CapSpeed = 7, CapRagdoll = 8, CapIgnite = 8, CapJump = 8;

        static SandApi()
        {
            const ScriptTarget N = ScriptTarget.Npc, W = ScriptTarget.Weapon, B = ScriptTarget.Both;
            // --- movement
            C("chase", "e", N, "Move", "Run after someone.", "chase player");
            C("flee", "e?", N, "Move", "Run away (from someone, or from the target).", "flee player");
            C("move_to", "e", N, "Move", "Walk to someone's position.", "move_to npc");
            C("wander", "", N, "Move", "Stroll around randomly.", "wander");
            C("stop", "", N, "Move", "Stand still.", "stop");
            C("look_at", "e", N, "Move", "Turn to face someone.", "look_at player");
            C("jump", "n?", N, "Move", "Jump (strength 1-8).", "jump 4");
            C("crouch", "n?", N, "Move", "Cower / crouch for N seconds.", "crouch 2");
            C("target", "e", N, "Move", "Pick who 'target' means.", "target player");
            // --- combat
            C("attack", "e?", N, "Fight", "Hit or shoot the target if in reach.", "attack");
            C("punch", "e?", N, "Fight", "Throw a punch.", "punch");
            C("kick", "e?", N, "Fight", "Throw a kick (stronger, slower).", "kick");
            C("shoot", "e?", N, "Fight", "Fire the NPC's gun at the target.", "shoot player");
            C("give_weapon", "s", N, "Fight", "Hold a weapon (built-in id: glock, ar15, knife...).", "give_weapon knife");
            C("explode", "n", B, "Fight", "Explosion (radius up to 4).", "explode 3");
            C("ragdoll", "n", N, "Fight", "Go limp for N seconds (max 8).", "ragdoll 2");
            C("heal", "n", N, "Fight", "Heal N health (max 50 per call).", "heal 20");
            // --- talk / fx
            C("say", "a", B, "Talk", "Show a speech bubble.", "say \"Hello!\"");
            C("play_sound", "s", B, "Talk", "Play a sound: " + string.Join(", ", Sounds), "play_sound scream");
            C("spawn_effect", "s", B, "Talk", "Effect: " + string.Join(", ", Effects), "spawn_effect sparks");
            C("print", "a", B, "Talk", "Write to the script log (for testing).", "print health");
            // --- weapon
            C("spawn_projectile", "s", W, "Weapon", "Extra shot: " + string.Join(", ", Projectiles), "spawn_projectile spark");
            C("damage_target", "n", W, "Weapon", "Extra damage to what was hit (max 60).", "damage_target 10");
            C("knockback", "n", W, "Weapon", "Push what was hit (max 30).", "knockback 12");
            C("ignite", "n", W, "Weapon", "Set what was hit on fire (seconds, max 8).", "ignite 3");
            C("effect", "s", W, "Weapon", "Effect at the hit point / muzzle.", "effect sparks");
            C("sound", "s", W, "Weapon", "Sound at the hit point / muzzle.", "sound zap");
            // --- functions (used inside expressions)
            F("random", "nn", B, "Math", "Random whole number from a to b.", "set x = random 1 10");
            F("chance", "n", B, "Math", "True N percent of the time.", "if chance 25:");
            F("abs", "n", B, "Math", "Absolute value.", "abs x");
            F("min", "nn", B, "Math", "Smaller of two.", "min a b");
            F("max", "nn", B, "Math", "Bigger of two.", "max a b");
            F("round", "n", B, "Math", "Round to a whole number.", "round x");
            F("distance", "e", B, "Ask", "Distance in metres to someone (999 if nobody).", "if distance player < 3:");
            F("is_dead", "e", B, "Ask", "True if that someone is dead (or missing).", "if is_dead target:");
            F("health_of", "e", B, "Ask", "Health of someone (0-100 for players).", "health_of target");
            F("can_see", "e", N, "Ask", "True if this NPC can see them.", "if can_see player:");

            P("speed", N, true, 0.5, CapSpeed, "Walk/run speed (0.5-7).");
            P("health", B, true, 0, 500, "Current health (read). Setting it is capped by max_health.");
            P("max_health", N, true, 10, 500, "Maximum health (10-500).");
            P("damage", B, true, 1, CapDamage, "Melee / weapon damage (1-60).");
            P("team", N, true, 0, 0, "neutral, hostile or friendly.", true);
            P("color", N, true, 0, 0, "Shirt colour: " + string.Join(", ", ColorNames), true);
            P("time", B, false, 0, 0, "Seconds since the game started.");
            P("has_target", B, false, 0, 0, "True if there is a target.");
            P("ammo", B, false, 0, 0, "Bullets left (weapons / armed NPCs).");
            P("damage_taken", N, false, 0, 0, "In 'on hurt': how much damage was taken.");
            P("hit_damage", W, false, 0, 0, "In 'on hit': the damage the shot did.");

            E("spawn", N, "When the NPC appears.", "on spawn:");
            E("see player", N, "When it first sees a player (target = that player).", "on see player:");
            E("see npc", N, "When it first sees another NPC (target = it).", "on see npc:");
            E("lose target", N, "When the target is gone / out of sight.", "on lose target:");
            E("hurt", N, "When it takes damage (attacker, damage_taken).", "on hurt:");
            E("death", N, "When it dies.", "on death:");
            E("every", B, "Repeats every N seconds.", "every 2:", true, MinEvery, 600);
            E("near player", N, "When a player comes within N metres.", "on near player 3:", true, 0.5, 50);
            E("tick", B, "Every frame (keep it short!).", "on tick:");
            E("fire", W, "When the weapon fires.", "on fire:");
            E("hit", W, "When the weapon hits something (target = it).", "on hit:");
            E("equip", W, "When the weapon is taken out.", "on equip:");
        }

        static void C(string n, string a, ScriptTarget t, string cat, string help, string ex)
        {
            var c = new Cmd { Name = n, Args = a, Target = t, Category = cat, Help = help, Example = ex };
            Commands.Add(c); cmdMap[n] = c;
        }

        static void F(string n, string a, ScriptTarget t, string cat, string help, string ex)
        {
            var c = new Cmd { Name = n, Args = a, Target = t, Category = cat, Help = help, Example = ex, IsFunc = true };
            Commands.Add(c); cmdMap[n] = c;
        }

        static void P(string n, ScriptTarget t, bool settable, double min, double max, string help, bool text = false)
        {
            var p = new Prop { Name = n, Target = t, Settable = settable, Min = min, Max = max, Help = help, IsText = text };
            Props.Add(p); propMap[n] = p;
        }

        static void E(string n, ScriptTarget t, string help, string ex, bool num = false, double min = 0, double max = 0)
            => Events.Add(new EventSpec { Name = n, Target = t, Help = help, Example = ex, HasNumber = num, NumMin = min, NumMax = max });

        public static Cmd FindCmd(string n) => n != null && cmdMap.TryGetValue(n, out var c) ? c : null;
        public static Prop FindProp(string n) => n != null && propMap.TryGetValue(n, out var p) ? p : null;
        public static bool IsEntity(string n) => Array.IndexOf(EntityWords, n) >= 0;
        public static bool Allowed(ScriptTarget have, ScriptTarget need) => (have & need) != 0;

        public static readonly string[] Keywords = { "on", "every", "if", "else", "elif", "repeat", "while", "wait", "set", "and", "or", "not", "true", "false", "return" };
        public static bool IsKeyword(string n) => Array.IndexOf(Keywords, n) >= 0;

        /// <summary>Clamps a number to [min, max] and turns NaN/Infinity into min (scripts can never pass absurd values).</summary>
        public static double Clamp(double v, double min, double max)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return min;
            return v < min ? min : v > max ? max : v;
        }
    }
}
