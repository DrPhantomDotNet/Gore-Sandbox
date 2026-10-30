using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse.Mods
{
    /// <summary>
    /// Bundled example Advanced mods, so ADVANCED MODDING is never empty. The Modding Hub lists AdvancedExamples.All and
    /// installs one with ModHub.Handler(p.Kind).Install(p) (or saves a copy to edit). Fresh copies on every call.
    /// </summary>
    public static class AdvancedExamples
    {
        public const string Author = "Gore Sandbox";

        public static List<ModPackage> All
        {
            get
            {
                var chefLook = new CustomLook
                {
                    Name = "Angry Chef", Shirt = new Color(0.95f, 0.95f, 0.95f), Pants = new Color(0.15f, 0.15f, 0.17f), Shoes = new Color(0.1f, 0.1f, 0.1f),
                    Hair = new Color(0.12f, 0.08f, 0.05f), HairStyle = 1, Beard = true, LongSleeves = true, Build = 1.25f, Height = 1.05f,
                };
                var cowardLook = new CustomLook
                {
                    Name = "Coward", Shirt = new Color(0.85f, 0.8f, 0.4f), Pants = new Color(0.45f, 0.35f, 0.25f), Hair = new Color(0.55f, 0.4f, 0.2f),
                    HairStyle = 0, Glasses = true, Build = 0.85f, Height = 0.95f,
                };
                return new List<ModPackage>
                {
                    Npc("example_angry_chef", "Angry Chef", "Chases anyone near his kitchen with a cleaver and yells about it.",
                        new NpcModData { look = chefLook.Encode(), health = 160, speed = 4.5f, strength = 2.2f, bravery = 1f, aggression = 1f, team = 1, weapon = "knife" },
                        AdvancedScripts.AngryChef),
                    Npc("example_coward", "Coward", "Runs from everyone and begs for his life.",
                        new NpcModData { look = cowardLook.Encode(), health = 70, speed = 5f, strength = 0.5f, bravery = 0f, aggression = 0f, team = 0 },
                        AdvancedScripts.Coward),
                    new ModPackage
                    {
                        id = "example_lightning_pistol", name = "Lightning Pistol", description = "Every shot crackles with sparks and knocks targets back.",
                        author = Author, kind = (int)ModKind.ScriptedWeapon, version = 1,
                        data = JsonUtility.ToJson(new ScriptedWeaponData { baseWeapon = "glock", r = 0.55f, g = 0.75f, b = 1f, damageMul = 0.9f }),
                        script = AdvancedScripts.LightningPistol,
                    },
                };
            }
        }

        static ModPackage Npc(string id, string name, string desc, NpcModData d, string script) => new ModPackage
        {
            id = id, name = name, description = desc, author = Author, kind = (int)ModKind.Npc, version = 1, data = JsonUtility.ToJson(d), script = script,
        };
    }
}
