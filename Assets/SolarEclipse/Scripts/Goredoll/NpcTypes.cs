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
            public int Armor = -1;      // Gore Sandbox 2: built-in armour level ("~aN")
            public bool Gloves, Wraps;  // boxing gloves; mummy bandages
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
            // ---- Gore Sandbox 2 types
            new TypeDef { Id = "businessman", Name = "Businessman", Desc = "Suit, tie and a terrible day ahead.", Brain = "Coward", Shirt = new Color(0.16f, 0.17f, 0.2f), Pants = new Color(0.14f, 0.15f, 0.18f), Hair = 0 },
            new TypeDef { Id = "surgeon", Name = "Surgeon", Desc = "Scrubs, cap and mask, with a scalpel-sharp temper.", Brain = "Aggressive", Shirt = new Color(0.3f, 0.5f, 0.55f), Pants = new Color(0.3f, 0.5f, 0.55f), Hat = 3, HatColor = new Color(0.3f, 0.5f, 0.55f), Weapon = "knife", Blade = true },
            new TypeDef { Id = "swat", Name = "SWAT Officer", Desc = "Heavy armour and a rifle. Shoots back.", Brain = "Gunman", Shirt = new Color(0.1f, 0.11f, 0.13f), Pants = new Color(0.1f, 0.11f, 0.13f), Hat = 7, HatColor = new Color(0.12f, 0.12f, 0.14f), Armor = 3, Weapon = "m4a1" },
            new TypeDef { Id = "firefighter", Name = "Firefighter", Desc = "Turnout coat with reflective stripes and a helmet.", Brain = "Brave", Shirt = new Color(0.75f, 0.6f, 0.25f), Pants = new Color(0.75f, 0.6f, 0.25f), Hat = 6, HatColor = new Color(0.8f, 0.1f, 0.08f) },
            new TypeDef { Id = "paramedic", Name = "Paramedic", Desc = "Green uniform. Runs toward danger.", Brain = "Brave", Shirt = new Color(0.2f, 0.45f, 0.25f), Pants = new Color(0.15f, 0.3f, 0.18f) },
            new TypeDef { Id = "boxer", Name = "Boxer", Desc = "Shorts, gloves and a mean right hand.", Brain = "Aggressive", Shirt = new Color(0.85f, 0.65f, 0.55f), Pants = new Color(0.75f, 0.08f, 0.08f), Sleeves = false, Gloves = true, Hair = 1 },
            new TypeDef { Id = "wrestler", Name = "Wrestler", Desc = "Singlet and a masked face. Big and angry.", Brain = "Aggressive", Shirt = new Color(0.2f, 0.25f, 0.8f), Pants = new Color(0.2f, 0.25f, 0.8f), Sleeves = false, Mask = "m_ski" },
            new TypeDef { Id = "biker", Name = "Biker", Desc = "Black leather, bandana and a short fuse.", Brain = "Aggressive", Shirt = new Color(0.07f, 0.07f, 0.08f), Pants = new Color(0.14f, 0.2f, 0.35f), Mask = "m_bandana" },
            new TypeDef { Id = "gangster", Name = "Gang Member", Desc = "Red colours, backwards cap, a pistol in his belt.", Brain = "Gunman", Shirt = new Color(0.75f, 0.1f, 0.1f), Pants = new Color(0.12f, 0.12f, 0.14f), Hat = 2, HatColor = new Color(0.75f, 0.1f, 0.1f), Weapon = "glock" },
            new TypeDef { Id = "scientist", Name = "Scientist", Desc = "Lab coat and safety glasses. Very curious, not very brave.", Brain = "Coward", Shirt = new Color(0.95f, 0.95f, 0.96f), Pants = new Color(0.3f, 0.32f, 0.36f) },
            new TypeDef { Id = "hazmat", Name = "Hazmat Worker", Desc = "Yellow suit and a gas mask.", Brain = "Passive", Shirt = new Color(0.95f, 0.85f, 0.15f), Pants = new Color(0.95f, 0.85f, 0.15f), Shoes = new Color(0.1f, 0.1f, 0.1f), Mask = "m_hazmat" },
            new TypeDef { Id = "bodybuilder", Name = "Bodybuilder", Desc = "Tank top, huge arms, protein breath.", Brain = "Brave", Shirt = new Color(0.95f, 0.95f, 0.95f), Pants = new Color(0.1f, 0.1f, 0.5f), Sleeves = false },
            new TypeDef { Id = "oldman", Name = "Old Man", Desc = "Cardigan, slippers, grey hair. Walks slowly.", Brain = "Passive", Shirt = new Color(0.55f, 0.45f, 0.35f), Pants = new Color(0.4f, 0.38f, 0.35f), Hat = 11, HatColor = new Color(0.5f, 0.45f, 0.38f) },
            new TypeDef { Id = "tourist", Name = "Tourist", Desc = "Hawaiian shirt, shorts and a bucket hat.", Brain = "Normal", Shirt = new Color(0.1f, 0.6f, 0.75f), Pants = new Color(0.85f, 0.78f, 0.6f), Sleeves = false, Hat = 11, HatColor = new Color(0.9f, 0.85f, 0.7f) },
            new TypeDef { Id = "jogger", Name = "Jogger", Desc = "Running gear and a headband. Fast.", Brain = "Coward", Shirt = new Color(0.95f, 0.3f, 0.5f), Pants = new Color(0.1f, 0.1f, 0.12f), Sleeves = false, Hat = 10, HatColor = new Color(0.95f, 0.95f, 0.95f) },
            new TypeDef { Id = "office", Name = "Office Worker", Desc = "Shirt and tie, ID badge, coffee breath.", Brain = "Normal", Shirt = new Color(0.75f, 0.82f, 0.92f), Pants = new Color(0.2f, 0.22f, 0.26f) },
            new TypeDef { Id = "homeless", Name = "Homeless Man", Desc = "Layers of worn clothes and a beanie.", Brain = "Passive", Shirt = new Color(0.35f, 0.3f, 0.22f), Pants = new Color(0.28f, 0.26f, 0.22f), Hat = 3, HatColor = new Color(0.3f, 0.32f, 0.28f) },
            new TypeDef { Id = "richman", Name = "Rich Man", Desc = "White suit and a top hat. Thinks he's untouchable.", Brain = "Coward", Shirt = new Color(0.95f, 0.94f, 0.9f), Pants = new Color(0.95f, 0.94f, 0.9f), Hat = 5, HatColor = new Color(0.1f, 0.1f, 0.12f) },
            new TypeDef { Id = "mascot", Name = "Pig Mascot", Desc = "A sweaty person in a pig costume head.", Brain = "Passive", Shirt = new Color(0.95f, 0.65f, 0.7f), Pants = new Color(0.95f, 0.65f, 0.7f), Mask = "m_pig" },
            new TypeDef { Id = "knight", Name = "Knight", Desc = "Plate armour and a sword.", Brain = "Brave", Shirt = new Color(0.6f, 0.62f, 0.66f), Pants = new Color(0.55f, 0.57f, 0.6f), Shoes = new Color(0.5f, 0.52f, 0.55f), Hat = 7, HatColor = new Color(0.6f, 0.62f, 0.66f), Armor = 3, Weapon = "katana", Blade = true },
            new TypeDef { Id = "viking", Name = "Viking", Desc = "Horned helmet, fur and an axe-happy attitude.", Brain = "Aggressive", Shirt = new Color(0.45f, 0.32f, 0.2f), Pants = new Color(0.3f, 0.22f, 0.15f), Hat = 15, Weapon = "cleaver", Blade = true },
            new TypeDef { Id = "cowboy", Name = "Cowboy", Desc = "Hat, boots and a revolver.", Brain = "Gunman", Shirt = new Color(0.6f, 0.35f, 0.2f), Pants = new Color(0.2f, 0.3f, 0.5f), Shoes = new Color(0.35f, 0.2f, 0.1f), Hat = 4, HatColor = new Color(0.45f, 0.3f, 0.15f), Weapon = "revolver" },
            new TypeDef { Id = "pirate", Name = "Pirate", Desc = "Bandana, striped shirt and a blade.", Brain = "Aggressive", Shirt = new Color(0.9f, 0.9f, 0.9f), Pants = new Color(0.15f, 0.15f, 0.2f), Mask = "m_bandana", Weapon = "knife", Blade = true },
            new TypeDef { Id = "samurai", Name = "Samurai", Desc = "Armour, topknot and a katana.", Brain = "Brave", Shirt = new Color(0.55f, 0.1f, 0.08f), Pants = new Color(0.1f, 0.1f, 0.12f), Armor = 2, Weapon = "katana", Blade = true },
            new TypeDef { Id = "gladiator", Name = "Gladiator", Desc = "Helmet, armour and a short sword. Born for the arena.", Brain = "Brave", Shirt = new Color(0.75f, 0.6f, 0.4f), Pants = new Color(0.55f, 0.15f, 0.1f), Sleeves = false, Hat = 7, HatColor = new Color(0.8f, 0.65f, 0.3f), Armor = 2, Weapon = "knife", Blade = true },
            new TypeDef { Id = "mummy", Name = "Mummy", Desc = "Wrapped head to toe in old bandages.", Brain = "Aggressive", Shirt = new Color(0.82f, 0.78f, 0.65f), Pants = new Color(0.82f, 0.78f, 0.65f), Shoes = new Color(0.75f, 0.7f, 0.58f), Wraps = true },
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
            return t.Brain + (t.Armor >= 0 ? "~a" + t.Armor : "") + "~c" + look.Encode() + "~t" + t.Id + tokens;
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
        public static bool IsMini(string preset) => Flag(preset, "~n");

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
            if (IsMini(preset)) look.Height *= 0.5f;
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
                if (t.Gloves)
                {
                    var parts = new Dictionary<PartType, Transform>();
                    foreach (var kv in doll.Parts) if (kv.Value != null) parts[kv.Key] = kv.Value.transform;
                    FightAnim.Gloves(parts, layer);
                }
                if (t.Wraps)
                    foreach (var kv in doll.Parts)
                    {
                        if (kv.Value == null) continue;
                        var col = kv.Value.GetComponent<Collider>();
                        float len = col != null ? Mathf.Max(col.bounds.size.y, 0.1f) / Mathf.Max(0.1f, kv.Value.transform.lossyScale.y) : 0.2f;
                        Piece(kv.Value.transform, "Wraps", mb =>
                        {
                            mb.Color = new Color(0.62f, 0.58f, 0.46f);
                            for (int i = 0; i < 4; i++) mb.Box(new Vector3(0, -len * (0.15f + i * 0.2f) + (kv.Key == PartType.Head ? HumanModel.HeadCenterY + len * 0.4f : 0f), 0), new Vector3(0.13f, 0.012f, 0.13f));
                        }, layer);
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
            if (IsMini(preset))
            {
                doll.DisplayName = "Mini " + doll.DisplayName;
                foreach (var p in doll.Parts.Values) if (p != null && p.Body != null) p.Body.mass *= 0.25f;
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
            ai.Add(new SpawnableDef { Id = "npc_mini", Name = "Mini Goredoll", Category = "AI", Glyph = "person", Description = "Half the size, a quarter of the weight. Flies a long way.", Spawn = (p, r) => GoredollFactory.Spawn(p, r.eulerAngles.y, "Normal~n").gameObject });
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
