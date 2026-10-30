using System;
using System.Collections.Generic;

namespace SolarEclipse.Mods
{
    /// <summary>What a mod adds to the game.</summary>
    public enum ModKind
    {
        Weapon = 0,          // built in the Weapon Editor (pure data: parts, trigger, bullet source, hold points, stats)
        ScriptedWeapon = 1,  // Advanced Modding: a weapon whose behaviour is a SandScript
        Npc = 2,             // Advanced Modding: a custom NPC (look, stats, SandScript behaviour)
    }

    /// <summary>
    /// One mod as it is saved, shared (share code) and uploaded (Mod board). Mods are DATA ONLY: a JSON definition plus,
    /// for advanced mods, a SandScript source that runs in the game's own sandboxed interpreter. No mod can ever contain
    /// or load real program code, which is what keeps them from carrying viruses.
    /// </summary>
    [Serializable]
    public sealed class ModPackage
    {
        public int formatVersion = 1;
        public string id = "";          // unique (random), stable across updates of the same mod
        public string name = "";
        public string description = "";
        public string author = "";      // display name
        public string authorGuid = "";  // the author's player id (only the author can update/delete it online)
        public int kind;                // ModKind
        public int version = 1;         // bumped by the author on each upload
        public long created, updated;   // unix seconds
        public string data = "";        // kind-specific JSON definition
        public string script = "";      // SandScript source (advanced mods), empty otherwise

        public ModKind Kind => (ModKind)kind;
        public bool Advanced => Kind != ModKind.Weapon;
    }

    /// <summary>How each mod kind plugs into the Modding Hub (registered at startup by the code that owns the kind).</summary>
    public sealed class ModKindHandler
    {
        public ModKind Kind;
        public string Title = "";                 // "Weapon", "Scripted weapon", "NPC"
        public string Icon = "gun";               // IconFactory glyph
        public bool Advanced;                     // listed under ADVANCED MODDING
        public Action<ModPackage> OpenEditor;     // null package = create new
        public Func<ModPackage, string> Validate; // null = OK, otherwise the reason it's rejected (also the anti-cheat limits)
        public Action<ModPackage> Install;        // make it usable in game (spawn menu entries etc.)
        public Action<ModPackage> Uninstall;
    }

    /// <summary>Registry shared by the Modding Hub (storage, share codes, Mod board, UI) and the mod kinds.</summary>
    public static class ModHub
    {
        public static readonly Dictionary<ModKind, ModKindHandler> Kinds = new Dictionary<ModKind, ModKindHandler>();

        public static void Register(ModKindHandler h) => Kinds[h.Kind] = h;

        public static ModKindHandler Handler(ModKind k) => Kinds.TryGetValue(k, out var h) ? h : null;

        /// <summary>Runs the kind's validator (null = valid). Unknown kinds are rejected.</summary>
        public static string Validate(ModPackage p)
        {
            if (p == null) return "Empty mod.";
            var h = Handler(p.Kind);
            if (h == null) return "This mod needs a newer version of the game.";
            return h.Validate != null ? h.Validate(p) : null;
        }
    }
}
