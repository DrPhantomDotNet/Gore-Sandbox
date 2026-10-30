using SolarEclipse.Mods;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// A weapon made in the Unity editor: Assets > Create > Gore Sandbox > Weapon (or SolarEclipse > Dev Hub).
    /// Put it anywhere under a Resources/GoreContent folder and it's in the game: spawn menu, give lists, everything.
    /// It's the same data as the in-game Weapon Editor, so every field shows in the Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "New Weapon", menuName = "Gore Sandbox/Weapon", order = 0)]
    public sealed class GoreWeaponAsset : ScriptableObject
    {
        [Tooltip("Name in the spawn menu and the HUD.")] public string displayName = "My Weapon";
        [TextArea(2, 4)] public string description = "";
        [Tooltip("Untick to keep it out of the game without deleting it.")] public bool includeInGame = true;
        [Tooltip("Parts, special points (bullet source, hold points, sights), trigger and stats. Values outside the fair limits are clamped.")]
        public WeaponDesign design = WeaponDesign.Template(0);

        /// <summary>Stable id from the asset name, so saves and multiplayer see the same weapon every build.</summary>
        public string ContentId => "gw" + StableHash(name);

        public ModPackage ToPackage() => new ModPackage
        {
            id = ContentId, name = string.IsNullOrWhiteSpace(displayName) ? name : displayName, description = description ?? "",
            author = GameInfo.Studio, authorGuid = "official", kind = (int)ModKind.Weapon, version = 1,
            data = JsonUtility.ToJson(design ?? new WeaponDesign()),
        };

        public static string StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in s ?? "") { h ^= c; h *= 16777619; }
                return h.ToString("x8");
            }
        }
    }
}
