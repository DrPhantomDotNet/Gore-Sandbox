using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Content Update Goredoll types: doctor, prisoner, construction worker, chef, clown, astronaut, cop (armed),
    /// ninja (katana), plus the Giant (3x size) and Skeleton variants. Everything is carried in the preset string so
    /// multiplayer clients build the same thing: "~t&lt;type&gt;" type, "~g" giant, "~s" skeleton (all before any "~m").
    /// </summary>
    public static class NpcTypes
    {
        public sealed class TypeDef
        {
            public string Id, Name, Desc, Brain = "Normal", Weapon;
            public Color Shirt, Pants, Shoes = new Color(0.08f, 0.08f, 0.09f);
            public bool Sleeves = true;
            public int Hair = -1, Hat;
            public Color HatColor = Color.white;
            public string Mask;
            public bool Blade;
        }

        public static readonly List<TypeDef> Types = new List<TypeDef>
        {
            new TypeDef { Id = "doctor", Name = "Doctor", Desc = "White coat, surgical mask. Panics easily.", Brain = "Coward", Shirt = new Color(0.94f, 0.95f, 0.96f), Pants = new Color(0.35f, 0.55f, 0.6f) },
            new TypeDef { Id = "prisoner", Name = "Prisoner", Desc = "Orange jumpsuit, buzz cut and a bad attitude.", Brain = "Aggressive", Shirt = new Color(0.95f, 0.45f, 0.08f), Pants = new Color(0.95f, 0.45f, 0.08f), Hair = 1 },
            new TypeDef { Id = "worker", Name = "Construction Worker", Desc = "Hi-vis vest and a hard hat.", Brain = "Brave", Shirt = new Color(0.95f, 0.85f, 0.1f), Pants = new Color(0.2f, 0.28f, 0.45f), Sleeves = false, Hat = 6, HatColor = new Color(1f, 0.8f, 0.1f) },
            new TypeDef { Id = "chef", Name = "Chef", Desc = "Chef's whites, a tall hat and a meat cleaver he isn't afraid to use.", Brain = "Aggressive", Shirt = new Color(0.97f, 0.97f, 0.95f), Pants = new Color(0.12f, 0.12f, 0.13f), Hat = 14, Weapon = "cleaver", Blade = true },
            new TypeDef { Id = "clown", Name = "Clown", Desc = "Honk honk. Colourful, creepy and aggressive.", Brain = "Aggressive", Shirt = new Color(0.85f, 0.15f, 0.6f), Pants = new Color(0.2f, 0.45f, 0.95f), Hat = 9, HatColor = new Color(0.2f, 0.9f, 0.3f), Mask = "m_clown" },
            new TypeDef { Id = "astronaut", Name = "Astronaut", Desc = "Spacesuit, glass helmet and an oxygen pack.", Brain = "Passive", Shirt = new Color(0.92f, 0.92f, 0.93f), Pants = new Color(0.92f, 0.92f, 0.93f), Shoes = new Color(0.7f, 0.7f, 0.72f) },
            new TypeDef { Id = "cop", Name = "Police Officer", Desc = "Armed with a pistol and shoots back.", Brain = "Gunman", Shirt = new Color(0.12f, 0.2f, 0.4f), Pants = new Color(0.08f, 0.1f, 0.18f), Hat = 1, HatColor = new Color(0.1f, 0.15f, 0.3f) },
            new TypeDef { Id = "ninja", Name = "Ninja", Desc = "All black, masked, with a katana that cuts anything it touches.", Brain = "Aggressive", Shirt = new Color(0.05f, 0.05f, 0.06f), Pants = new Color(0.05f, 0.05f, 0.06f), Mask = "m_ski", Weapon = "katana", Blade = true },
        };

        public static TypeDef Get(string id) => Types.Find(t => t.Id == id);

        /// <summary>Preset string for a type (with a random face), optionally giant / skeleton.</summary>
        public static string Preset(string id, bool giant = false, bool skeleton = false)
        {
            var t = Get(id);
            var tokens = (giant ? "~g" : "") + (skeleton ? "~s" : "");
            if (t == null) return "Normal" + tokens;
            var look = CustomLook.FromAppearance(Appearance.Random(-1));
            look.Shirt = t.Shirt; look.Pants = t.Pants; look.Shoes = t.Shoes; look.LongSleeves = t.Sleeves;
            if (t.Hair >= 0) look.HairStyle = t.Hair;
            look.Name = t.Name;
            look.Role = 0;
            return t.Brain + "~c" + look.Encode() + "~t" + t.Id + tokens;
        }

        public static string TypeOf(string preset)
        {
            if (string.IsNullOrEmpty(preset)) return null;
            int i = preset.IndexOf("~t", System.StringComparison.Ordinal);
            if (i < 0) return null;
            int end = preset.IndexOf('~', i + 2);
            return end < 0 ? preset.Substring(i + 2) : preset.Substring(i + 2, end - i - 2);
        }

        static bool Flag(string preset, string token)
        {
            if (string.IsNullOrEmpty(preset)) return false;
            int m = preset.IndexOf("~m", System.StringComparison.Ordinal);
            var head = m >= 0 ? preset.Substring(0, m) : preset;
            int c = head.IndexOf("~c", System.StringComparison.Ordinal);
            // Don't look inside the custom look code.
            if (c >= 0) { int end = head.IndexOf('~', c + 2); head = head.Substring(0, c) + (end >= 0 ? head.Substring(end) : ""); }
            return head.Contains(token);
        }

        public static bool IsGiant(string preset) => Flag(preset, "~g");
        public static bool IsSkeleton(string preset) => Flag(preset, "~s");

        /// <summary>Before the body is built (GoredollFactory.Spawn): skeleton colours, giant size.</summary>
        public static void ApplyLook(Appearance look, string preset)
        {
            if (IsSkeleton(preset))
            {
                var bone = new Color(0.9f, 0.87f, 0.78f);
                look.Skin = bone; look.Shirt = bone; look.Pants = bone; look.Shoes = bone * 0.9f;
                look.HairStyle = 2; look.Beard = false; look.Eyes = Color.black; look.Lips = bone * 0.8f;
                look.Name = "Skeleton";
            }
            if (IsGiant(preset)) look.Height *= 3f;
        }

        /// <summary>After the body is built: props, giant physics, skeleton (no blood), blade AI.</summary>
        public static void Attach(Goredoll doll, string preset)
        {
            if (doll == null) return;
            int layer = doll.gameObject.layer;
            var t = Get(TypeOf(preset));
            var head = doll.Parts.TryGetValue(PartType.Head, out var hp) && hp != null ? hp.transform : null;
            var chest = doll.Parts.TryGetValue(PartType.Chest, out var cp) && cp != null ? cp.transform : null;
            if (t != null)
            {
                doll.DisplayName = t.Name;
                if (head != null && t.Hat > 0) Hats.Attach(head, t.Hat, t.HatColor, layer);
                if (head != null && t.Mask != null) OWOutfit.Attach(head, null, null, ";;;;;" + t.Mask + ";", layer);
                switch (t.Id)
                {
                    case "doctor": if (head != null) Piece(head, "Surgical Mask", mb => { mb.Color = new Color(0.55f, 0.8f, 0.85f); mb.BevelBox(new Vector3(0, HumanModel.HeadCenterY - 0.045f, 0.085f), new Vector3(0.11f, 0.06f, 0.03f), 0.01f); }, layer); break;
                    case "worker":
                        if (chest != null) Piece(chest, "Hi-vis", mb => { mb.Color = new Color(0.85f, 0.88f, 0.9f); mb.Gloss = 0.9f; mb.Box(new Vector3(0, 0.12f, 0.115f), new Vector3(0.3f, 0.025f, 0.01f)); mb.Box(new Vector3(0, 0.2f, 0.115f), new Vector3(0.3f, 0.025f, 0.01f)); }, layer);
                        break;
                    case "astronaut":
                        if (head != null) Piece(head, "Helmet", mb => { mb.Color = new Color(0.75f, 0.85f, 0.95f); mb.Gloss = 1f; mb.Sphere(new Vector3(0, HumanModel.HeadCenterY, 0.01f), new Vector3(0.16f, 0.17f, 0.16f), 10, 14); }, layer, Mats.Glass);
                        if (chest != null) Piece(chest, "Oxygen Pack", mb => { mb.Color = new Color(0.9f, 0.9f, 0.92f); mb.BevelBox(new Vector3(0, 0.16f, -0.17f), new Vector3(0.28f, 0.34f, 0.14f), 0.03f); mb.SetColor(new Color(0.9f, 0.3f, 0.1f), 0.5f); mb.Box(new Vector3(0.08f, 0.26f, -0.245f), new Vector3(0.05f, 0.05f, 0.01f)); }, layer);
                        break;
                    case "cop":
                        if (chest != null) Piece(chest, "Badge", mb => { mb.Color = new Color(1f, 0.82f, 0.3f); mb.Gloss = 0.9f; mb.Box(new Vector3(-0.08f, 0.2f, 0.115f), new Vector3(0.04f, 0.05f, 0.01f)); }, layer);
                        break;
                }
                if (t.Weapon != null && doll.Parts.TryGetValue(PartType.HandR, out var hand) && hand != null)
                {
                    var w = ArmedDolls.MountGun(hand.transform, t.Weapon, layer);
                    if (w != null && t.Blade) doll.gameObject.AddComponent<NpcBlade>().Init(doll, w, t.Id == "ninja" ? 45f : 32f);
                }
            }
            if (IsSkeleton(preset))
            {
                doll.DisplayName = t != null ? "Skeleton " + t.Name : "Skeleton";
                if (doll.Bleeding != null) doll.Bleeding.enabled = false;
            }
            if (IsGiant(preset))
            {
                doll.DisplayName = "Giant " + doll.DisplayName;
                foreach (var p in doll.Parts.Values)
                {
                    if (p == null) continue;
                    if (p.Body != null) p.Body.mass *= 9f;
                    p.MaxHealth *= 4f; p.Health = p.MaxHealth;
                }
                if (doll.Ragdoll != null) doll.Ragdoll.StrengthMul = 26f;
            }
        }

        static void Piece(Transform parent, string name, System.Action<MeshBuilder> build, int layer, Material mat = null)
        {
            var mb = new MeshBuilder { Color = Color.white };
            build(mb);
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat != null ? mat : Mats.Lit;
        }

        /// <summary>Spawn menu entries (AI tab): the 8 types plus Giant / Skeleton Goredolls.</summary>
        public static void RegisterSpawnables(List<SpawnableDef> ai)
        {
            foreach (var t in Types)
            {
                var id = t.Id;
                ai.Add(new SpawnableDef { Id = "npc_" + id, Name = t.Name, Category = "AI", Glyph = "person", Description = t.Desc, Spawn = (p, r) => GoredollFactory.Spawn(p, r.eulerAngles.y, Preset(id)).gameObject });
            }
            ai.Add(new SpawnableDef { Id = "npc_giant", Name = "Giant Goredoll", Category = "AI", Glyph = "person", Description = "Three times the size, nine times the weight, four times the health.", Spawn = (p, r) => GoredollFactory.Spawn(p + Vector3.up * 0.5f, r.eulerAngles.y, "Normal~g").gameObject });
            ai.Add(new SpawnableDef { Id = "npc_skeleton", Name = "Skeleton", Category = "AI", Glyph = "skull", Description = "A walking skeleton. No blood, just bones.", Spawn = (p, r) => GoredollFactory.Spawn(p, r.eulerAngles.y, "Aggressive~s").gameObject });
            ai.Add(new SpawnableDef { Id = "npc_giant_ninja", Name = "Giant Ninja", Category = "AI", Glyph = "person", Description = "A 5 metre ninja with a 3 metre katana. Good luck.", Spawn = (p, r) => GoredollFactory.Spawn(p + Vector3.up * 0.5f, r.eulerAngles.y, Preset("ninja", true)).gameObject });
        }
    }

    /// <summary>NPC blade (ninja katana, chef cleaver): cuts whoever the swinging blade touches (host / singleplayer).</summary>
    public sealed class NpcBlade : MonoBehaviour
    {
        Goredoll doll; WeaponModel blade; float damage, cooldown;
        Vector3 lastTip;

        public void Init(Goredoll d, WeaponModel w, float dmg) { doll = d; blade = w; damage = dmg; }

        void Update()
        {
            if (doll == null || blade == null || doll.IsDead || doll.NetProxy || !NetState.Authority) return;
            var tip = blade.Muzzle != null ? blade.Muzzle.position : blade.transform.position;
            float speed = (tip - lastTip).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            var mid = Vector3.Lerp(blade.transform.position, tip, 0.6f);
            lastTip = tip;
            cooldown -= Time.deltaTime;
            if (speed < 3f || cooldown > 0f) return;
            float scale = doll.transform.localScale.x;
            var hits = Physics.OverlapSphere(mid, 0.3f * scale, Layers.ShootMask | (1 << Layers.LocalHitbox), QueryTriggerInteraction.Ignore);
            foreach (var c in hits)
            {
                var part = c.GetComponent<BodyPart>();
                if (part != null && part.Doll != null && part.Doll != doll)
                {
                    cooldown = 0.35f;
                    var dir = (part.transform.position - doll.Position).normalized;
                    part.TakeDamage(new DamageInfo { Amount = damage * scale, Type = DamageType.Slash, Point = c.ClosestPoint(mid), Direction = dir, Normal = -dir, Force = dir * 80f, Source = doll, Weapon = doll.DisplayName });
                    if (part.IsLimb && part.CanSever && Random.value < 0.35f && part.Doll.Dismember != null) part.Doll.Dismember.Slice(part, c.ClosestPoint(mid), Vector3.Cross(dir, Vector3.up).normalized);
                    AudioManager.Play("slice", mid, 0.9f);
                    return;
                }
                var pt = c.GetComponentInParent<IPlayerTarget>();
                if (pt != null && !pt.IsDead && pt.Damageable != null)
                {
                    cooldown = 0.6f;
                    var dir = (pt.Position - doll.Position).normalized;
                    pt.Damageable.TakeDamage(new DamageInfo { Amount = damage * 0.6f * scale, Type = DamageType.Slash, Point = mid, Direction = dir, Normal = -dir, Force = dir * 4f, Source = doll, Weapon = doll.DisplayName });
                    AudioManager.Play("slice", mid, 0.9f);
                    return;
                }
            }
        }
    }
}
