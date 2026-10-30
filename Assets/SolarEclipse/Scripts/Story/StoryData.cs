using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>What a story step does. Values are stored in saved stories: only ever append.</summary>
    public enum StepType
    {
        Say = 0,        // who: speaker, text: line
        Choice = 1,     // who, text, options[] + jumps[] (label to go to per option, "" = carry on)
        Chat = 2,       // who: sender, text: message (phone text in the corner + chat log)
        Objective = 3,  // text
        Banner = 4,     // who: big title, text: subtitle
        Cutscene = 5,   // camera from pos/look to pos2/look2 over time seconds, text: caption
        Wait = 6,       // time seconds
        GoTo = 7,       // reach pos (radius), text: objective
        Talk = 8,       // target: actor id, text: objective (talk to them to carry on)
        Spawn = 9,      // target: actor id
        Kill = 10,      // target: actor id or "enemies", text: objective
        Remove = 11,    // target: actor id (they leave)
        Give = 12,      // target: weapon id
        Music = 13,     // target: "fight", "calm", "tense" or "" (stop)
        Jump = 14,      // target: label
        SetFlag = 15,   // target: flag name
        IfFlag = 16,    // target: flag name, text: label to go to when it's set
        Fade = 17,      // time: seconds of black (fade out, hold, fade in), text: optional caption on black
        End = 18,       // target: "pass", "fail" or "end"; who: title / ending name; text: subtitle / reason / ending text
        Unlock = 19,    // built-in stories only: target achievement id
        Heal = 20,      // full health
    }

    /// <summary>Who an actor is.</summary>
    public enum ActorRole
    {
        Talker = 0,     // can't be hurt (like Big Marcus); stands, turns to you, talks
        Civilian = 1,   // a normal person (can be hurt), can talk
        Gunman = 2,     // enemy with a pistol
        Soldier = 3,    // enemy with a rifle and armour
        Brawler = 4,    // enemy that fights with fists
        Zombie = 5,     // enemy zombie
        Boss = 6,       // enemy with a big health bar (health), rifle and armour
    }

    [Serializable]
    public sealed class StoryStep
    {
        public int type;
        public string label = "";
        public string who = "", text = "", target = "";
        public string[] options = new string[0];
        public string[] jumps = new string[0];
        public Vector3 pos, look, pos2, look2;
        public float time = 3f, radius = 3f;

        public StepType Type => (StepType)type;
        public StoryStep Clone() => JsonUtility.FromJson<StoryStep>(JsonUtility.ToJson(this));
    }

    [Serializable]
    public sealed class StoryActorDef
    {
        public string id = "", name = "";
        public int role;
        public string look = "";        // CustomLook code; empty = made from the seed
        public int seed = 1;
        public Vector3 pos;
        public float yaw;
        public bool atStart = true;     // spawned when the story starts (otherwise by a Spawn step)
        public float health = 1200f;    // boss health

        public ActorRole Role => (ActorRole)role;
        public bool Hostile => Role >= ActorRole.Gunman;
    }

    /// <summary>A whole story: map, cast and the list of steps. Stored as JSON (story mods, built-in chapter missions).</summary>
    [Serializable]
    public sealed class StoryPack
    {
        public int formatVersion = 1;
        public string title = "My Story", description = "";
        public string map = "flatgrass";
        public int time = -1;           // TimeOfDay, -1 = the map's default
        public List<string> weapons = new List<string> { "fists", "knife", "glock" };
        public List<StoryActorDef> actors = new List<StoryActorDef>();
        public List<StoryStep> steps = new List<StoryStep>();
        [NonSerialized] public bool builtIn;

        public StoryActorDef Actor(string id) => actors.Find(a => a.id == id);
        public StoryPack Clone() { var c = JsonUtility.FromJson<StoryPack>(JsonUtility.ToJson(this)); c.builtIn = builtIn; return c; }
    }

    /// <summary>Limits and cleaning for player-made stories (the same rules run on save, upload, install and play).</summary>
    public static class StoryRules
    {
        public const int MaxSteps = 250, MaxActors = 40, MaxOptions = 4, MaxText = 300, MaxName = 32;
        public static readonly string[] Music = { "", "calm", "tense", "fight" };
        public static readonly string[] MusicNames = { "Stop music", "Calm", "Tense", "Fight" };

        static string Clip(string s, int n, bool filter)
        {
            s = (s ?? "").Replace("\r", "");
            var sb = new System.Text.StringBuilder(Math.Min(s.Length, n));
            foreach (char c in s)
            {
                if (sb.Length >= n) break;
                if (c == '\n' || (c >= ' ' && c < 127) || c == '•' || c == '—') sb.Append(c);
            }
            var r = sb.ToString().Replace("<", "(").Replace(">", ")"); // no rich-text tags
            return filter ? ChatFilter.Censor(r) : r;
        }

        static Vector3 V(Vector3 v) => MathX.IsFinite(v) ? new Vector3(Mathf.Clamp(v.x, -2000, 2000), Mathf.Clamp(v.y, -500, 2000), Mathf.Clamp(v.z, -2000, 2000)) : Vector3.zero;

        public static string Id(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in (s ?? "").ToLowerInvariant()) if (sb.Length < 24 && ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>Parses and cleans a player-made story. Null on success (pack set), otherwise why it's rejected.</summary>
        public static string Parse(string json, out StoryPack pack)
        {
            pack = null;
            if (string.IsNullOrEmpty(json)) return "The story is empty.";
            if (json.Length > 200 * 1024) return "The story is too big.";
            try { pack = JsonUtility.FromJson<StoryPack>(json); }
            catch (Exception) { return "The story data is broken."; }
            if (pack == null) return "The story data is broken.";
            return Sanitize(pack, true);
        }

        public static string Sanitize(StoryPack p, bool playerMade)
        {
            if (p.formatVersion != 1) return "This story was made with a newer version of the game.";
            p.title = Clip(p.title, MaxName, playerMade);
            if (p.title.Length == 0) p.title = "Untitled story";
            p.description = Clip(p.description, MaxText, playerMade);
            if (MapCatalog.All.Find(m => m.Id == p.map) == null || p.map == BossRealm.MapId) p.map = "flatgrass";
            p.time = Mathf.Clamp(p.time, -1, Enum.GetValues(typeof(TimeOfDay)).Length - 1);
            if (p.weapons == null) p.weapons = new List<string>();
            p.weapons.RemoveAll(w => !WeaponAllowed(w));
            if (p.weapons.Count > 8) p.weapons.RemoveRange(8, p.weapons.Count - 8);
            if (p.actors == null) p.actors = new List<StoryActorDef>();
            if (p.steps == null) p.steps = new List<StoryStep>();
            if (p.actors.Count > MaxActors) return "Too many characters (max " + MaxActors + ").";
            if (p.steps.Count > MaxSteps) return "Too many steps (max " + MaxSteps + ").";
            var ids = new HashSet<string>();
            foreach (var a in p.actors)
            {
                a.id = Id(a.id);
                if (a.id.Length == 0 || !ids.Add(a.id)) return "Every character needs its own id.";
                a.name = Clip(a.name, 24, playerMade);
                if (a.name.Length == 0) a.name = "Someone";
                a.role = Mathf.Clamp(a.role, 0, (int)ActorRole.Boss);
                if (CustomLook.Decode(a.look) == null) a.look = "";
                a.pos = V(a.pos);
                a.yaw = Mathf.Repeat(a.yaw, 360f);
                a.health = Mathf.Clamp(a.health, 100f, 5000f);
            }
            foreach (var s in p.steps)
            {
                if (s == null) return "The story data is broken.";
                if (!Enum.IsDefined(typeof(StepType), s.type)) return "This story needs a newer version of the game.";
                if (playerMade && s.Type == StepType.Unlock) s.type = (int)StepType.Wait; // achievements belong to the game
                s.label = Id(s.label);
                s.who = Clip(s.who, MaxName, playerMade);
                s.text = Clip(s.text, MaxText, playerMade);
                s.target = Clip(s.target, 40, false);
                if (s.Type == StepType.Give && !WeaponAllowed(s.target)) s.target = "glock";
                if (s.options == null) s.options = new string[0];
                if (s.options.Length > MaxOptions) Array.Resize(ref s.options, MaxOptions);
                for (int i = 0; i < s.options.Length; i++) s.options[i] = Clip(s.options[i], 40, playerMade);
                if (s.jumps == null || s.jumps.Length != s.options.Length) Array.Resize(ref s.jumps, s.options.Length);
                for (int i = 0; i < s.jumps.Length; i++) s.jumps[i] = Id(s.jumps[i]);
                s.pos = V(s.pos); s.look = V(s.look); s.pos2 = V(s.pos2); s.look2 = V(s.look2);
                s.time = Mathf.Clamp(float.IsNaN(s.time) ? 3f : s.time, 0.2f, 60f);
                s.radius = Mathf.Clamp(float.IsNaN(s.radius) ? 3f : s.radius, 1f, 30f);
            }
            return null;
        }

        /// <summary>Weapons a story may hand out (no Spawn Gun / admin tools / mods).</summary>
        public static bool WeaponAllowed(string id)
        {
            var d = WeaponCatalog.Get(id);
            if (d == null || d.Kind == WeaponKind.SpawnGun || d.Kind == WeaponKind.Admin) return false;
            return !(d.Id ?? "").StartsWith("mod_");
        }
    }

    /// <summary>Small helpers so built-in missions read like a script.</summary>
    public sealed class StoryWriter
    {
        public readonly StoryPack Pack;
        public StoryWriter(string title, string map, int time = -1) { Pack = new StoryPack { title = title, map = map, time = time, builtIn = true }; }

        StoryStep Add(StepType t) { var s = new StoryStep { type = (int)t }; Pack.steps.Add(s); return s; }

        public StoryWriter Weapons(params string[] ids) { Pack.weapons = new List<string>(ids); return this; }

        public StoryActorDef Actor(string id, string name, ActorRole role, Vector3 pos, float yaw, bool atStart = true, CustomLook look = null, int seed = 1)
        {
            if (look != null) look.Name = name;
            var a = new StoryActorDef { id = id, name = name, role = (int)role, pos = pos, yaw = yaw, atStart = atStart, look = look != null ? look.Encode() : "", seed = seed };
            Pack.actors.Add(a);
            return a;
        }

        public StoryWriter Label(string l) { Add(StepType.Wait).time = 0.01f; Pack.steps[Pack.steps.Count - 1].label = l; return this; }
        public StoryWriter Say(string who, string text) { var s = Add(StepType.Say); s.who = who; s.text = text; return this; }
        public StoryWriter Choice(string who, string text, params string[] optionsAndLabels)
        {
            var s = Add(StepType.Choice); s.who = who; s.text = text;
            int n = optionsAndLabels.Length / 2;
            s.options = new string[n]; s.jumps = new string[n];
            for (int i = 0; i < n; i++) { s.options[i] = optionsAndLabels[i * 2]; s.jumps[i] = optionsAndLabels[i * 2 + 1]; }
            return this;
        }
        public StoryWriter Chat(string who, string text) { var s = Add(StepType.Chat); s.who = who; s.text = text; return this; }
        public StoryWriter Objective(string text) { Add(StepType.Objective).text = text; return this; }
        public StoryWriter Banner(string title, string sub) { var s = Add(StepType.Banner); s.who = title; s.text = sub; return this; }
        public StoryWriter Shot(Vector3 from, Vector3 lookFrom, Vector3 to, Vector3 lookTo, float seconds, string caption = "")
        { var s = Add(StepType.Cutscene); s.pos = from; s.look = lookFrom; s.pos2 = to; s.look2 = lookTo; s.time = seconds; s.text = caption; return this; }
        public StoryWriter Wait(float seconds) { Add(StepType.Wait).time = seconds; return this; }
        public StoryWriter GoTo(Vector3 pos, float radius, string objective) { var s = Add(StepType.GoTo); s.pos = pos; s.radius = radius; s.text = objective; return this; }
        public StoryWriter Talk(string actor, string objective = "") { var s = Add(StepType.Talk); s.target = actor; s.text = objective; return this; }
        public StoryWriter Spawn(string actor) { Add(StepType.Spawn).target = actor; return this; }
        public StoryWriter Kill(string actorOrEnemies, string objective) { var s = Add(StepType.Kill); s.target = actorOrEnemies; s.text = objective; return this; }
        public StoryWriter Remove(string actor) { Add(StepType.Remove).target = actor; return this; }
        public StoryWriter Give(string weapon) { Add(StepType.Give).target = weapon; return this; }
        public StoryWriter Music(string track) { Add(StepType.Music).target = track; return this; }
        public StoryWriter Jump(string label) { Add(StepType.Jump).target = label; return this; }
        public StoryWriter Flag(string flag) { Add(StepType.SetFlag).target = flag; return this; }
        public StoryWriter IfFlag(string flag, string label) { var s = Add(StepType.IfFlag); s.target = flag; s.text = label; return this; }
        public StoryWriter Fade(float seconds, string caption = "") { var s = Add(StepType.Fade); s.time = seconds; s.text = caption; return this; }
        public StoryWriter Unlock(string achievement) { Add(StepType.Unlock).target = achievement; return this; }
        public StoryWriter Heal() { Add(StepType.Heal); return this; }
        public StoryWriter Pass(string subtitle) { var s = Add(StepType.End); s.target = "pass"; s.who = "MISSION PASSED"; s.text = subtitle; return this; }
        public StoryWriter Fail(string reason) { var s = Add(StepType.End); s.target = "fail"; s.text = reason; return this; }
        public StoryWriter TheEnd(string endingName, string text) { var s = Add(StepType.End); s.target = "end"; s.who = endingName; s.text = text; return this; }
    }
}
