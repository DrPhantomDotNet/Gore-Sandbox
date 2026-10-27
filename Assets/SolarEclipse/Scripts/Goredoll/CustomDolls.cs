using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>A Goredoll designed in the customizer. Encodes to a short URL-safe base64 code (~50 chars).</summary>
    public sealed class CustomLook
    {
        public string Name = "Custom";
        public Color Skin = new Color(0.91f, 0.72f, 0.58f), Shirt = new Color(0.25f, 0.55f, 0.25f), Pants = new Color(0.2f, 0.3f, 0.5f),
            Shoes = new Color(0.12f, 0.1f, 0.09f), Hair = new Color(0.3f, 0.18f, 0.08f), Eyes = new Color(0.35f, 0.2f, 0.1f);
        public int HairStyle;          // 0 short, 1 buzz, 2 bald, 3 long
        public bool LongSleeves, Beard, Glasses;
        public float Height = 1f, Build = 1f;
        public int Armor;              // Armor.None..Full
        public int Role;               // 0 = spawn-menu personality, 1 = Gunman, 2 = Soldier

        public const float MinHeight = 0.85f, MaxHeight = 1.2f, MinBuild = 0.8f, MaxBuild = 1.4f;
        const byte Version = 1;

        public CustomLook Clone() => Decode(Encode()) ?? new CustomLook();

        public Appearance ToAppearance()
        {
            var a = new Appearance
            {
                Skin = Skin, Shirt = Shirt, Pants = Pants, Shoes = Shoes, Hair = Hair, Eyes = Eyes, HairStyle = Mathf.Clamp(HairStyle, 0, 3),
                LongSleeves = LongSleeves, Beard = Beard, Height = Height, Build = Build, Name = string.IsNullOrEmpty(Name) ? "Custom" : Name,
            };
            a.Lips = Color.Lerp(a.Skin, new Color(0.75f, 0.35f, 0.35f), 0.45f);
            return a;
        }

        public static CustomLook FromAppearance(Appearance a)
        {
            return new CustomLook
            {
                Name = a.Name, Skin = a.Skin, Shirt = a.Shirt, Pants = a.Pants, Shoes = a.Shoes, Hair = a.Hair, Eyes = a.Eyes,
                HairStyle = a.HairStyle, LongSleeves = a.LongSleeves, Beard = a.Beard,
                Height = Mathf.Clamp(a.Height, MinHeight, MaxHeight), Build = Mathf.Clamp(a.Build, MinBuild, MaxBuild),
            };
        }

        public static string CleanName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Custom";
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                if (sb.Length >= 16) break;
                if (c >= 32 && c < 127 && c != '|' && c != '~') sb.Append(c);
            }
            var r = sb.ToString().Trim();
            return r.Length == 0 ? "Custom" : r;
        }

        static void Col(List<byte> b, Color c) { b.Add(Q(c.r)); b.Add(Q(c.g)); b.Add(Q(c.b)); }
        static byte Q(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(v) * 255f), 0, 255);
        static Color Col(byte[] b, ref int i) { var c = new Color(b[i] / 255f, b[i + 1] / 255f, b[i + 2] / 255f); i += 3; return c; }

        public string Encode()
        {
            var b = new List<byte> { Version };
            Col(b, Skin); Col(b, Shirt); Col(b, Pants); Col(b, Shoes); Col(b, Hair); Col(b, Eyes);
            b.Add((byte)Mathf.Clamp(HairStyle, 0, 3));
            b.Add((byte)((LongSleeves ? 1 : 0) | (Beard ? 2 : 0) | (Glasses ? 4 : 0)));
            b.Add(Q(Mathf.InverseLerp(MinHeight, MaxHeight, Height)));
            b.Add(Q(Mathf.InverseLerp(MinBuild, MaxBuild, Build)));
            b.Add((byte)Mathf.Clamp(Armor, 0, 3));
            b.Add((byte)Mathf.Clamp(Role, 0, 2));
            var name = CleanName(Name);
            b.Add((byte)name.Length);
            foreach (char c in name) b.Add((byte)c);
            return Convert.ToBase64String(b.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        public static CustomLook Decode(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length > 120) return null;
            try
            {
                var s = code.Replace('-', '+').Replace('_', '/');
                while (s.Length % 4 != 0) s += "=";
                var b = Convert.FromBase64String(s);
                if (b.Length < 26 || b[0] != Version) return null;
                int i = 1;
                var l = new CustomLook();
                l.Skin = Col(b, ref i); l.Shirt = Col(b, ref i); l.Pants = Col(b, ref i); l.Shoes = Col(b, ref i); l.Hair = Col(b, ref i); l.Eyes = Col(b, ref i);
                l.HairStyle = Mathf.Clamp(b[i++], 0, 3);
                int flags = b[i++];
                l.LongSleeves = (flags & 1) != 0; l.Beard = (flags & 2) != 0; l.Glasses = (flags & 4) != 0;
                l.Height = Mathf.Lerp(MinHeight, MaxHeight, b[i++] / 255f);
                l.Build = Mathf.Lerp(MinBuild, MaxBuild, b[i++] / 255f);
                l.Armor = Mathf.Clamp(b[i++], 0, 3);
                l.Role = Mathf.Clamp(b[i++], 0, 2);
                int n = b[i++];
                var sb = new System.Text.StringBuilder();
                for (int k = 0; k < n && i < b.Length; k++) sb.Append((char)b[i++]);
                l.Name = CleanName(sb.ToString());
                return l;
            }
            catch { return null; }
        }
    }

    /// <summary>Spawn modifiers carried in the preset string: "Normal~a3~c&lt;code&gt;" (armour level, custom look).</summary>
    public struct NpcSpawnMods
    {
        public int Armor;
        public CustomLook Custom;
    }

    public static class NpcMods
    {
        /// <summary>Strips the "~" tokens from the preset (leaving "Normal", "Gunman", "Zombie_Runner"...).</summary>
        public static NpcSpawnMods Parse(ref string preset)
        {
            var m = new NpcSpawnMods { Armor = Mathf.Max(0, Armor.LevelIn(preset)) };
            if (!string.IsNullOrEmpty(preset))
            {
                int ci = preset.IndexOf("~c", StringComparison.Ordinal);
                if (ci >= 0)
                {
                    int end = preset.IndexOf('~', ci + 2);
                    m.Custom = CustomLook.Decode(end < 0 ? preset.Substring(ci + 2) : preset.Substring(ci + 2, end - ci - 2));
                }
            }
            preset = Armor.BaseOf(preset);
            return m;
        }

        /// <summary>Called by GoredollFactory.Spawn after the body is built (host and clients alike).</summary>
        public static void Attach(Goredoll doll, string basePreset, NpcSpawnMods m)
        {
            int armor = m.Armor;
            if (m.Custom != null)
            {
                armor = m.Custom.Armor;
                if (m.Custom.Glasses && doll.Parts.TryGetValue(PartType.Head, out var head) && head != null)
                    CustomDolls.AddGlasses(head.transform, doll.gameObject.layer);
            }
            else if (basePreset == ArmedDolls.Soldier) armor = Armor.Full;
            if (ArmedDolls.IsArmed(basePreset) && doll.Zombie == null) ArmedDolls.Attach(doll, basePreset);
            if (armor > 0) DollArmor.Attach(doll, armor);
        }

        /// <summary>Spawn menu: renders one missing portrait per call.</summary>
        public static void TickIcons()
        {
            if (ArmedDolls.RenderPendingIcon()) return;
            CustomDolls.RenderPendingIcon();
        }
    }

    /// <summary>Saved custom Goredolls (max 12) and their Spawn Gun entries.</summary>
    public static class CustomDolls
    {
        public const int MaxSaved = 12;
        public const string IdPrefix = "custom:";
        static List<string> saved;
        static readonly List<SpawnableDef> defs = new List<SpawnableDef>();

        [Serializable] sealed class Store { public List<string> codes = new List<string>(); }

        static string FilePath => Path.Combine(Application.persistentDataPath, "custom_dolls.json");

        public static List<string> Saved
        {
            get
            {
                if (saved == null) Load();
                return saved;
            }
        }

        public static List<SpawnableDef> Defs { get { if (saved == null) Load(); return defs; } }

        static void Load()
        {
            saved = new List<string>();
            try
            {
                if (File.Exists(FilePath))
                {
                    var st = JsonUtility.FromJson<Store>(File.ReadAllText(FilePath));
                    if (st != null && st.codes != null)
                        foreach (var c in st.codes) if (CustomLook.Decode(c) != null && saved.Count < MaxSaved) saved.Add(c);
                }
            }
            catch (Exception e) { Debug.LogWarning("Custom dolls load failed: " + e.Message); }
            Rebuild();
        }

        static void Persist()
        {
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(new Store { codes = new List<string>(saved) })); }
            catch (Exception e) { Debug.LogWarning("Custom dolls save failed: " + e.Message); }
            Rebuild();
        }

        static void Rebuild()
        {
            defs.Clear();
            foreach (var c in saved) { var d = DefFor(c); if (d != null) defs.Add(d); }
        }

        /// <summary>Saves into a slot (-1 = new). Returns the slot or -1 if full.</summary>
        public static int Save(CustomLook look, int slot)
        {
            var code = look.Encode();
            var list = Saved;
            if (slot >= 0 && slot < list.Count) list[slot] = code;
            else
            {
                if (list.Count >= MaxSaved) return -1;
                list.Add(code);
                slot = list.Count - 1;
            }
            Persist();
            return slot;
        }

        public static void Delete(int slot)
        {
            var list = Saved;
            if (slot < 0 || slot >= list.Count) return;
            list.RemoveAt(slot);
            Persist();
        }

        public static SpawnableDef DefForId(string id)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith(IdPrefix, StringComparison.Ordinal)) return null;
            var code = id.Substring(IdPrefix.Length);
            foreach (var d in Defs) if (d.Id == id) return d;
            return DefFor(code);
        }

        static SpawnableDef DefFor(string code)
        {
            var look = CustomLook.Decode(code);
            if (look == null) return null;
            string role = look.Role == 1 ? " Armed with a pistol." : look.Role == 2 ? " Armed with a rifle." : "";
            return new SpawnableDef
            {
                Id = IdPrefix + code, Name = look.Name, Category = "AI", Glyph = "person",
                Description = "Your custom Goredoll." + role,
                Spawn = (pos, rot) => GoredollFactory.Spawn(pos, rot.eulerAngles.y, PresetFor(code), -1).gameObject,
            };
        }

        /// <summary>The preset string that spawns this look (travels to clients through the SpawnDoll message).</summary>
        public static string PresetFor(string code)
        {
            var look = CustomLook.Decode(code);
            string basePreset = look != null && look.Role == 1 ? ArmedDolls.Gunman : look != null && look.Role == 2 ? ArmedDolls.Soldier : Armor.BaseOf(SpawnCatalog.GoredollPreset);
            return Armor.WithSpawnArmor(basePreset) + "~c" + code;
        }

        public static string IconKey(SpawnableDef d) => "prop_" + d.Id;

        public static void RenderPendingIcon()
        {
            foreach (var d in Defs)
            {
                var key = IconKey(d);
                if (IconFactory.Has(key)) continue;
                var look = CustomLook.Decode(d.Id.Substring(IdPrefix.Length));
                try
                {
                    if (look != null)
                        IconFactory.Render(key, BuildMannequin(look.ToAppearance(), look.Armor, look.Glasses, look.Role == 1 ? "glock" : look.Role == 2 ? "ar15" : null),
                            new Vector3(-0.35f, -0.15f, -1f), 192, 1.05f, "person");
                }
                catch (Exception e) { Debug.LogWarning("Custom doll portrait failed: " + e.Message); }
                if (!IconFactory.Has(key)) IconFactory.Register(key, IconFactory.Get("person"));
                return;
            }
        }

        // ------------------------------------------------------------------ models

        /// <summary>A static (no physics) Goredoll model for icons and the customizer preview.</summary>
        public static GameObject BuildMannequin(Appearance look, int armor, bool glasses, string gunId, int layer = 0)
        {
            var root = new GameObject("Mannequin");
            root.layer = layer;
            foreach (var type in Anatomy.Parts.Keys)
            {
                var spec = Anatomy.Parts[type];
                var go = new GameObject(spec.Name);
                go.layer = layer;
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = spec.Pivot;
                go.AddComponent<MeshFilter>().sharedMesh = HumanModel.Build(type, look);
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                if (type == PartType.Head)
                {
                    FaceRig.Build(go.transform, look);
                    if ((armor & Armor.Helmet) != 0) Piece(go.transform, DollArmor.HelmetMesh(), layer);
                    if (glasses) AddGlasses(go.transform, layer);
                }
                if (type == PartType.Chest && (armor & Armor.Vest) != 0) Piece(go.transform, DollArmor.VestChestMesh(look.Build), layer);
                if (type == PartType.Abdomen && (armor & Armor.Vest) != 0) Piece(go.transform, DollArmor.VestAbdomenMesh(look.Build), layer);
                if (type == PartType.HandR && !string.IsNullOrEmpty(gunId)) ArmedDolls.MountGun(go.transform, gunId, layer);
            }
            // Children (face, eyes) inherit the layer.
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            root.transform.localScale = Vector3.one * look.Height;
            return root;
        }

        static void Piece(Transform parent, Mesh mesh, int layer)
        {
            var go = new GameObject("Armor");
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
        }

        static Mesh glassesMesh;

        public static void AddGlasses(Transform head, int layer)
        {
            if (glassesMesh == null)
            {
                var mb = new MeshBuilder { Color = new Color(0.08f, 0.08f, 0.09f), Gloss = 0.6f };
                float y = HumanModel.HeadCenterY + 0.012f, z = 0.093f;
                for (int s = -1; s <= 1; s += 2)
                {
                    mb.Torus(new Vector3(s * 0.031f, y, z), 0.017f, 0.0025f, 14, 4, Vector3.forward);
                    mb.Box(new Vector3(s * 0.08f, y + 0.004f, z - 0.045f), new Vector3(0.004f, 0.004f, 0.09f));
                    mb.Box(new Vector3(s * 0.064f, y + 0.004f, z - 0.002f), new Vector3(0.034f, 0.004f, 0.004f));
                }
                mb.Box(new Vector3(0, y + 0.006f, z + 0.002f), new Vector3(0.028f, 0.004f, 0.004f));
                glassesMesh = mb.ToMesh("Glasses");
            }
            var go = new GameObject("Glasses");
            go.layer = layer;
            go.transform.SetParent(head, false);
            go.AddComponent<MeshFilter>().sharedMesh = glassesMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
        }
    }
}
