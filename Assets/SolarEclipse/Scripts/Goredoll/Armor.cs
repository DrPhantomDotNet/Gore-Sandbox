using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Armour worn by a Goredoll. Bit flags: 1 = helmet, 2 = vest (3 = full).</summary>
    public static class Armor
    {
        public const int None = 0, Helmet = 1, Vest = 2, Full = 3;
        public static readonly string[] Options = { "None", "Helmet", "Vest", "Full" };
        /// <summary>Spawn-menu choice applied to newly spawned Goredolls (Soldiers always get Full).</summary>
        public static int SpawnLevel;

        public const float HelmetHp = 140f, VestHp = 260f;

        // ------------------------------------------------------------------ preset suffix ("Normal~a3")

        /// <summary>Armour level carried by a preset string ("~aN" token), or -1 when it has none.</summary>
        public static int LevelIn(string preset)
        {
            if (string.IsNullOrEmpty(preset)) return -1;
            int i = preset.IndexOf("~a", System.StringComparison.Ordinal);
            if (i < 0 || i + 2 >= preset.Length) return -1;
            int v = preset[i + 2] - '0';
            return v >= 0 && v <= 3 ? v : -1;
        }

        /// <summary>The preset without any "~..." tokens.</summary>
        public static string BaseOf(string preset)
        {
            if (string.IsNullOrEmpty(preset)) return "Normal";
            int i = preset.IndexOf('~');
            return i < 0 ? preset : preset.Substring(0, i);
        }

        /// <summary>basePreset + the armour chosen in the spawn menu (or the one already in SpawnCatalog.GoredollPreset,
        /// which is the requesting client's choice while the host spawns for a client).</summary>
        public static string WithSpawnArmor(string basePreset)
        {
            int lvl = LevelIn(SpawnCatalog.GoredollPreset);
            if (lvl < 0) lvl = SpawnLevel;
            return BaseOf(basePreset) + "~a" + lvl;
        }

        // ------------------------------------------------------------------ damage hook (BodyPart.TakeDamage)

        static BodyPart stoppedPart;
        static int stoppedFrame = -1;

        internal static void MarkStopped(BodyPart part) { stoppedPart = part; stoppedFrame = Time.frameCount; }

        /// <summary>Ballistics: true if armour just stopped the bullet that hit this part (no exit wound / no over-penetration).</summary>
        public static bool ConsumeStopped(BodyPart part)
        {
            bool s = stoppedPart == part && stoppedFrame == Time.frameCount;
            if (s) stoppedPart = null;
            return s;
        }

        /// <summary>
        /// Called by BodyPart.TakeDamage before wounds are made (host, singleplayer and on clients replaying the host's
        /// damage stream: it is deterministic, so armour wears out identically everywhere). May rewrite the hit.
        /// Returns false if the hit must be ignored entirely.
        /// </summary>
        public static bool Process(BodyPart part, ref DamageInfo info)
        {
            if (part == null || part.Doll == null) return true;
            var brain = part.Doll.GetComponent<ArmedBrain>();
            if (brain != null) brain.OnHurt(info);
            var armor = part.Doll.GetComponent<DollArmor>();
            if (armor == null) return true;
            return armor.Absorb(part, ref info);
        }
    }

    /// <summary>Helmet / vest on one Goredoll: visuals, durability, bullet stopping, breaking off.</summary>
    public sealed class DollArmor : MonoBehaviour
    {
        public int Level;
        public float HelmetHp, VestHp;
        Goredoll doll;
        GameObject helmetGo, vestChestGo, vestAbdGo;

        public bool HasHelmet => helmetGo != null && HelmetHp > 0f;
        public bool HasVest => vestChestGo != null && VestHp > 0f;

        static readonly Color HelmetColor = new Color(0.27f, 0.31f, 0.2f), VestColor = new Color(0.2f, 0.22f, 0.17f), StrapColor = new Color(0.1f, 0.1f, 0.09f);

        public static DollArmor Attach(Goredoll d, int level)
        {
            if (d == null || level <= 0) return null;
            var a = d.gameObject.AddComponent<DollArmor>();
            a.doll = d;
            a.Level = level;
            if ((level & Armor.Helmet) != 0 && d.Parts.TryGetValue(PartType.Head, out var head))
            {
                a.HelmetHp = Armor.HelmetHp;
                a.helmetGo = Visual("Helmet", head.transform, HelmetMesh(), d.gameObject.layer);
            }
            if ((level & Armor.Vest) != 0 && d.Parts.TryGetValue(PartType.Chest, out var chest))
            {
                a.VestHp = Armor.VestHp;
                a.vestChestGo = Visual("Vest", chest.transform, VestChestMesh(d.Look != null ? d.Look.Build : 1f), d.gameObject.layer);
                if (d.Parts.TryGetValue(PartType.Abdomen, out var abd))
                    a.vestAbdGo = Visual("VestLower", abd.transform, VestAbdomenMesh(d.Look != null ? d.Look.Build : 1f), d.gameObject.layer);
            }
            return a;
        }

        static GameObject Visual(string name, Transform parent, Mesh mesh, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            return go;
        }

        // ------------------------------------------------------------------ meshes (part-local space)

        static Mesh helmetMesh;
        public static Mesh HelmetMesh()
        {
            if (helmetMesh != null) return helmetMesh;
            var mb = new MeshBuilder { Color = HelmetColor, Gloss = 0.2f };
            var c = new Vector3(0, HumanModel.HeadCenterY + 0.012f, -0.004f);
            float cutFront = HumanModel.HeadCenterY + 0.042f;
            mb.Sphere(c, new Vector3(0.096f, 0.126f, 0.113f), 12, 18, p =>
            {
                // Open face: the rim sits above the brows at the front and drops lower at the back/sides.
                float back = Mathf.Clamp01(-(p.z - c.z) / 0.11f);
                float cut = cutFront - back * 0.06f;
                return p.y < cut ? new Vector3(p.x, cut, p.z) : p;
            });
            mb.SetColor(StrapColor, 0.1f);
            for (int s = -1; s <= 1; s += 2)
                mb.Box(new Vector3(s * 0.084f, HumanModel.HeadCenterY - 0.02f, 0.03f), new Vector3(0.006f, 0.1f, 0.012f));
            helmetMesh = mb.ToMesh("Helmet");
            return helmetMesh;
        }

        public static Mesh VestChestMesh(float build)
        {
            var mb = new MeshBuilder { Color = VestColor, Gloss = 0.15f };
            mb.BevelBox(new Vector3(0, 0.12f, 0.01f), new Vector3(0.385f * build, 0.24f, 0.3f), 0.03f);
            mb.SetColor(VestColor * 0.85f, 0.1f);
            for (int i = -1; i <= 1; i++)
                mb.BevelBox(new Vector3(i * 0.08f * build, 0.07f, 0.172f), new Vector3(0.065f, 0.075f, 0.035f), 0.008f);
            mb.SetColor(StrapColor, 0.1f);
            for (int s = -1; s <= 1; s += 2)
                mb.Box(new Vector3(s * 0.09f * build, 0.268f, 0.005f), new Vector3(0.055f, 0.03f, 0.25f));
            return mb.ToMesh("Vest");
        }

        public static Mesh VestAbdomenMesh(float build)
        {
            var mb = new MeshBuilder { Color = VestColor, Gloss = 0.15f };
            mb.BevelBox(new Vector3(0, 0.115f, 0.01f), new Vector3(0.335f * build, 0.11f, 0.26f), 0.025f);
            mb.SetColor(StrapColor, 0.1f);
            mb.Box(new Vector3(0, 0.07f, 0.01f), new Vector3(0.34f * build, 0.02f, 0.265f));
            return mb.ToMesh("VestLower");
        }

        // ------------------------------------------------------------------ hits

        bool Covers(BodyPart part, Vector3 worldPoint, out bool helmet)
        {
            helmet = false;
            var local = part.transform.InverseTransformPoint(worldPoint);
            switch (part.Type)
            {
                case PartType.Head:
                    if (!HasHelmet) return false;
                    helmet = true;
                    // Crown, sides and back; the face is open.
                    return local.y > HumanModel.HeadCenterY + 0.035f || (local.y > HumanModel.HeadCenterY - 0.02f && local.z < 0.02f);
                case PartType.Chest: return HasVest;
                case PartType.Abdomen: return HasVest && local.y > 0.06f;
                default: return false;
            }
        }

        public bool Absorb(BodyPart part, ref DamageInfo info)
        {
            if (info.IsExit || info.Amount <= 0f) return true;
            if (!Covers(part, info.Point, out bool helmet)) return true;
            float original = info.Amount;
            float wear;
            bool stopped = false;
            switch (info.Type)
            {
                case DamageType.Bullet:
                case DamageType.Buckshot:
                {
                    // Pistols / SMGs / buckshot are stopped, rifles get through weakened, heavy rounds pierce.
                    bool heavy = original >= 85f || info.Caliber >= 10f;
                    bool rifle = !heavy && (original >= 45f || info.Penetration >= 1f);
                    if (heavy) { info.Amount = original * 0.8f; wear = original * 0.7f; }
                    else if (rifle) { info.Amount = original * 0.35f; info.Penetration *= 0.4f; wear = original * 0.9f; }
                    else { stopped = true; wear = original; }
                    break;
                }
                case DamageType.Slash:
                case DamageType.Stab:
                    info.Amount = original * 0.25f; wear = original * 0.4f; break;
                case DamageType.Blunt:
                    info.Amount = original * 0.7f; wear = original * 0.2f; break;
                case DamageType.Explosion:
                    info.Amount = original * 0.75f; wear = original * 0.5f; break;
                default: return true; // Admin, falls, crush: armour doesn't help
            }

            if (stopped)
            {
                // Blunt trauma behind the plate: a bruise and a shove, no bullet wound.
                info.Type = DamageType.Blunt;
                info.Amount = original * 0.12f;
                info.Caliber = 0f;
                info.Penetration = 0f;
                Armor.MarkStopped(part);
            }
            Ping(info.Point, info.Normal, stopped || info.Amount < original * 0.5f);

            if (helmet) { HelmetHp -= wear; if (HelmetHp <= 0f) BreakOff(true, info.Direction); }
            else { VestHp -= wear; if (VestHp <= 0f) BreakOff(false, info.Direction); }
            return true;
        }

        static void Ping(Vector3 pos, Vector3 normal, bool big)
        {
            FX.Sparks(pos, normal, big ? 12 : 6, big ? 8f : 5f);
            AudioManager.Play("impact_metal", pos, 0.9f, Random.Range(1.1f, 1.3f), 1f, 2f, 60f);
            if (big && Random.value < 0.5f) AudioManager.Play("ricochet", pos, 0.5f, Random.Range(0.95f, 1.1f), 1f, 2f, 40f);
        }

        void BreakOff(bool helmet, Vector3 dir)
        {
            if (helmet) { Drop(helmetGo, dir); helmetGo = null; HelmetHp = 0f; }
            else { Drop(vestChestGo, dir); Drop(vestAbdGo, dir); vestChestGo = vestAbdGo = null; VestHp = 0f; }
            if (doll != null && doll.Parts.TryGetValue(helmet ? PartType.Head : PartType.Chest, out var p))
                AudioManager.Play("impact_metal", p.transform.position, 1f, 0.7f, 1f, 2f, 60f);
        }

        /// <summary>Turns a worn piece into a loose physics prop (local only; purely cosmetic in multiplayer).</summary>
        static void Drop(GameObject go, Vector3 dir)
        {
            if (go == null) return;
            var parentBody = go.GetComponentInParent<Rigidbody>();
            var scale = go.transform.lossyScale;
            go.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, true);
            go.transform.localScale = scale;
            go.layer = Layers.Debris;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = true; // a gibbed part hides its children
            var mf = go.GetComponent<MeshFilter>();
            var bc = go.AddComponent<BoxCollider>();
            if (mf != null && mf.sharedMesh != null) { bc.center = mf.sharedMesh.bounds.center; bc.size = mf.sharedMesh.bounds.size; }
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            if (parentBody != null) RB.SetVel(rb, RB.Vel(parentBody));
            rb.AddForce((dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.up) * 2.5f + Vector3.up * 1.5f, ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * 4f, ForceMode.VelocityChange);
            go.AddComponent<Surface>().Type = SurfaceType.Metal;
            Debris.Register(go, 45f);
        }

        void Update()
        {
            if (doll == null) return;
            // Chest blown apart: the vest falls.
            if (vestChestGo != null && doll.Parts.TryGetValue(PartType.Chest, out var chest) && (chest == null || chest.Gibbed))
            {
                Drop(vestChestGo, Vector3.up); Drop(vestAbdGo, Vector3.up);
                vestChestGo = vestAbdGo = null;
            }
            // Head blown off: the helmet falls.
            if (helmetGo != null && doll.Parts.TryGetValue(PartType.Head, out var head) && (head == null || head.Gibbed))
            {
                Drop(helmetGo, Vector3.up);
                helmetGo = null;
            }
        }
    }
}
