using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    public enum ZombieKind { Walker, Runner, Crawler, Brute, Screamer }

    /// <summary>
    /// Zombies are ordinary Goredolls built with a "Zombie_*" preset: the look is zombified inside the seeded spawn,
    /// and <see cref="Attach"/> adds the undead body (<see cref="ZombieDoll"/>) and the zombie mind (<see cref="ZombieBrain"/>).
    /// Because the preset travels with the normal SpawnDoll message, zombies replicate to multiplayer clients for free.
    /// Infection: a Goredoll bitten by a zombie that dies rises as one ~3 s later (host only; the corpse is swapped
    /// for a zombie with the same seed/variant, so it is the same person, now green and pale).
    /// </summary>
    public static class Zombies
    {
        public const string PresetPrefix = "Zombie_";
        public const int MaxActive = 40;
        public static readonly ZombieKind[] Kinds = { ZombieKind.Walker, ZombieKind.Runner, ZombieKind.Crawler, ZombieKind.Brute, ZombieKind.Screamer };

        public static string PresetFor(ZombieKind k) => PresetPrefix + k;

        public static ZombieKind? KindOf(string preset)
        {
            if (string.IsNullOrEmpty(preset) || !preset.StartsWith(PresetPrefix)) return null;
            var name = preset.Substring(PresetPrefix.Length);
            foreach (var k in Kinds) if (k.ToString() == name) return k;
            return ZombieKind.Walker;
        }

        public static string Title(ZombieKind k)
        {
            switch (k)
            {
                case ZombieKind.Runner: return "Runner";
                case ZombieKind.Crawler: return "Crawler";
                case ZombieKind.Brute: return "Brute";
                case ZombieKind.Screamer: return "Screamer";
                default: return "Walker";
            }
        }

        public static string Describe(ZombieKind k)
        {
            switch (k)
            {
                case ZombieKind.Runner: return "Fast and frantic. Sprints at anything alive. Only 3 headshots put it down.";
                case ZombieKind.Crawler: return "Legless and low to the ground. Drags itself towards you with its arms.";
                case ZombieKind.Brute: return "Huge and strong: hits hard and knocks you around. Still dies to 3 headshots.";
                case ZombieKind.Screamer: return "Its shriek pulls every zombie nearby towards whatever it has seen.";
                default: return "Slow, groaning, relentless. Bites and eats Goredolls, who rise as zombies.";
            }
        }

        public static int AliveCount
        {
            get { int n = 0; foreach (var g in Goredoll.All) if (g != null && g.Zombie != null && !g.IsDead) n++; return n; }
        }

        // ------------------------------------------------------------------ look

        static readonly Color ZombieSkin = new Color(0.56f, 0.66f, 0.5f);

        /// <summary>Turns a normal look into a zombie one. Runs inside the seeded spawn (deterministic on every machine).</summary>
        public static void Zombify(Appearance a, ZombieKind kind)
        {
            float lum = a.Skin.r * 0.3f + a.Skin.g * 0.59f + a.Skin.b * 0.11f;
            var grey = new Color(lum, lum, lum);
            a.Skin = Color.Lerp(Color.Lerp(a.Skin, grey, 0.6f), ZombieSkin * Mathf.Lerp(0.75f, 1.1f, lum), 0.7f);
            a.Skin = Color.Lerp(a.Skin, new Color(0.62f, 0.7f, 0.55f), Random.Range(0f, 0.25f));
            a.Eyes = new Color(0.07f, 0.05f, 0.04f);
            a.Lips = new Color(0.2f, 0.09f, 0.09f);
            var dirt = new Color(0.25f, 0.2f, 0.15f);
            a.Shirt = Color.Lerp(a.Shirt, dirt, 0.45f) * 0.75f;
            a.Pants = Color.Lerp(a.Pants, dirt, 0.4f) * 0.75f;
            a.Shoes = a.Shoes * 0.6f;
            a.Hair = Color.Lerp(a.Hair, new Color(0.18f, 0.17f, 0.15f), 0.5f);
            a.LongSleeves = false; // ripped sleeves
            switch (kind)
            {
                case ZombieKind.Brute: a.Height = 1.17f; a.Build = 1.35f; a.HairStyle = 2; a.Shirt = new Color(0.2f, 0.18f, 0.16f); break;
                case ZombieKind.Runner: a.Build = Mathf.Min(a.Build, 0.95f); break;
                case ZombieKind.Screamer: a.HairStyle = 3; a.Hair = new Color(0.3f, 0.28f, 0.26f); a.Lips = new Color(0.3f, 0.05f, 0.06f); break;
                case ZombieKind.Crawler: a.Pants = a.Pants * 0.7f; break;
            }
        }

        /// <summary>Adds the undead body and the zombie brain. Called by GoredollFactory on host and clients alike.</summary>
        public static void Attach(Goredoll doll, ZombieKind kind, int seed)
        {
            var z = doll.gameObject.AddComponent<ZombieDoll>();
            z.Init(doll, kind);
            doll.Zombie = z;
            doll.DisplayName = Title(kind) + " Zombie";
            var sp = doll.GetComponent<Spawned>();
            if (sp != null) sp.DisplayName = doll.DisplayName;
            doll.Vitals.Undead = true;
            doll.Voice.Undead = true;
            doll.Ragdoll.ZombieArms = kind != ZombieKind.Crawler;
            doll.Ragdoll.WantsToCrawl = true;
            if (doll.Brain != null) doll.Brain.enabled = false;
            var brain = doll.gameObject.AddComponent<ZombieBrain>();
            brain.Init(doll, kind);
            z.Brain = brain;

            if (kind == ZombieKind.Crawler)
            {
                // Legless: shins and feet are gone (hidden, limp, no collision); the body can only crawl.
                foreach (var t in new[] { PartType.ShinL, PartType.ShinR, PartType.FootL, PartType.FootR })
                {
                    if (!doll.Parts.TryGetValue(t, out var p) || p == null) continue;
                    p.Destroyed = true;
                    p.Health = 0f;
                    if (p.Renderer != null) p.Renderer.enabled = false;
                    if (p.Col != null) p.Col.enabled = false;
                    if (p.Body != null) p.Body.mass *= 0.2f;
                }
            }
            if (Settings.GoreEnabled) AddGrime(doll);
        }

        static void AddGrime(Goredoll doll)
        {
            var parts = new[] { PartType.Chest, PartType.Abdomen, PartType.ForearmL, PartType.ForearmR, PartType.Head, PartType.Neck, PartType.ThighL, PartType.UpperArmR };
            int n = Random.Range(3, 6);
            for (int i = 0; i < n; i++)
            {
                if (!doll.Parts.TryGetValue(parts[Random.Range(0, parts.Length)], out var p) || p == null || p.Col == null || !p.Col.enabled) continue;
                var c = p.Col.bounds.center;
                var outside = c + Random.onUnitSphere * 0.6f;
                var pt = p.Col.ClosestPoint(outside);
                var nrm = (pt - c).sqrMagnitude > 1e-6f ? (pt - c).normalized : Vector3.forward;
                try { Decals.Wound(pt, nrm, Random.value < 0.5f ? 4 : 12, Random.Range(0.08f, 0.15f), p.Col); } catch { }
            }
            // Bloody mouth/chin.
            if (doll.Parts.TryGetValue(PartType.Head, out var head) && head.Col != null)
            {
                var t = head.transform;
                var mouth = t.TransformPoint(new Vector3(0, HumanModel.HeadCenterY - 0.07f, 0.2f));
                var pt = head.Col.ClosestPoint(mouth);
                try { Decals.Wound(pt, t.forward, 7, 0.08f, head.Col); } catch { }
            }
        }

        // ------------------------------------------------------------------ spawning

        public static Goredoll Spawn(ZombieKind kind, Vector3 feet, float yaw, int variant = -1, int seed = 0)
        {
            return GoredollFactory.Spawn(feet, yaw, PresetFor(kind), variant, null, seed);
        }

        public static ZombieKind RandomKind()
        {
            float r = Random.value;
            if (r < 0.36f) return ZombieKind.Walker;
            if (r < 0.62f) return ZombieKind.Runner;
            if (r < 0.76f) return ZombieKind.Crawler;
            if (r < 0.89f) return ZombieKind.Brute;
            return ZombieKind.Screamer;
        }

        static bool registered;

        /// <summary>Adds the 5 zombies to the spawn menu's AI tab (called from the SpawnCatalog static constructor).</summary>
        public static void RegisterSpawnables(List<SpawnableDef> ai)
        {
            if (registered) return;
            registered = true;
            EnsureAchievements();
            foreach (var k in Kinds)
            {
                var kind = k;
                ai.Add(new SpawnableDef
                {
                    Id = "zombie_" + kind.ToString().ToLowerInvariant(), Name = Title(kind) + " Zombie", Category = "AI", Glyph = "skull",
                    Description = Describe(kind),
                    Spawn = (pos, rot) => Spawn(kind, pos, rot.eulerAngles.y).gameObject,
                });
            }
        }

        public static void EnsureAchievements()
        {
            void Add(Achievement a) { if (!Stats.Achievements.Exists(x => x.Id == a.Id)) Stats.Achievements.Add(a); }
            Add(new Achievement("zombie_slayer", "Double Tap", "Put down 50 zombies.", "zombie_kills", 50));
            Add(new Achievement("patient_zero", "Patient Zero", "Watch a bitten Goredoll rise as a zombie.", "zombie_risen", 1));
            Add(new Achievement("horde_survivor", "Survivor", "Reach round 5 in Arena horde mode.", "horde_round5", 1));
            Add(new Achievement("horde_breaker", "Horde Breaker", "Reach round 8 in Arena horde mode.", "horde_round8", 1));
        }

        /// <summary>Renders a spawn-menu portrait of a zombie type.</summary>
        public static Texture2D Portrait(ZombieKind kind)
        {
            string name = "prop_zombie_" + kind.ToString().ToLowerInvariant();
            var state = Random.state;
            Random.InitState(1234 + (int)kind * 77);
            var look = Appearance.Random(-1);
            Zombify(look, kind);
            Random.state = state;
            var root = new GameObject("Portrait");
            foreach (var type in Anatomy.Parts.Keys)
            {
                if (kind == ZombieKind.Crawler && (type == PartType.ShinL || type == PartType.ShinR || type == PartType.FootL || type == PartType.FootR)) continue;
                var spec = Anatomy.Parts[type];
                var go = new GameObject(spec.Name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = spec.Pivot;
                go.AddComponent<MeshFilter>().sharedMesh = HumanModel.Build(type, look);
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                if (type == PartType.Head) FaceRig.Build(go.transform, look);
            }
            root.transform.localScale = Vector3.one * look.Height;
            return IconFactory.Render(name, root, new Vector3(-0.35f, -0.15f, -1f), 192, 1.05f, "skull");
        }

        // ------------------------------------------------------------------ infection

        /// <summary>Called by Goredoll.OnDeath (host / singleplayer only, never for proxies).</summary>
        public static void OnDollDied(Goredoll doll)
        {
            if (doll == null || doll.NetProxy || !NetState.Authority) return;
            if (doll.Zombie != null)
            {
                if (doll.KilledByPlayer) Stats.Add("zombie_kills");
                return;
            }
            if (doll.Bitten && doll.isActiveAndEnabled) doll.StartCoroutine(Rise(doll));
        }

        static IEnumerator Rise(Goredoll corpse)
        {
            float wait = Random.Range(2.6f, 3.2f);
            float t0 = Time.time;
            bool groaned = false;
            while (Time.time - t0 < wait)
            {
                if (corpse == null) yield break;
                if (!groaned && Time.time - t0 > wait - 1.2f && corpse.Parts.TryGetValue(PartType.Head, out var hh) && hh != null && !hh.Severed)
                {
                    groaned = true;
                    AudioManager.Play("z_rise", hh.transform.position, 0.9f, Random.Range(0.9f, 1.05f), 1f, 1.5f, 40f);
                    if (Settings.GoreEnabled) BloodFX.Mouth(corpse);
                }
                yield return null;
            }
            if (corpse == null || !NetState.Authority) yield break;
            if (!corpse.Parts.TryGetValue(PartType.Head, out var head) || head == null || head.Severed || head.Gibbed || corpse.Vitals.Decapitated) yield break;
            if (WorldRoot.Current == null) yield break;

            // Capture the corpse pose, swap it for a zombie of the same person and lay it in the same pose.
            var poses = new Dictionary<PartType, (Vector3 p, Quaternion r)>();
            foreach (var kv in corpse.Parts)
                if (kv.Value != null && !kv.Value.Severed && !kv.Value.Gibbed) poses[kv.Key] = (kv.Value.transform.position, kv.Value.transform.rotation);
            bool legless = LegGone(corpse, PartType.ThighL) || LegGone(corpse, PartType.ThighR) || LegGone(corpse, PartType.ShinL) || LegGone(corpse, PartType.ShinR);
            var kind = legless ? ZombieKind.Crawler : Random.value < 0.3f ? ZombieKind.Runner : ZombieKind.Walker;
            var pelvis = corpse.Parts[PartType.Pelvis].transform.position;
            float yaw = corpse.Parts[PartType.Pelvis].transform.eulerAngles.y;
            var feet = pelvis;
            if (Physics.Raycast(pelvis + Vector3.up * 0.5f, Vector3.down, out var hit, 4f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) feet = hit.point;
            int seed = corpse.Seed, variant = corpse.Variant;
            string name = corpse.DisplayName;
            bool fromHorde = corpse.GetComponent<HordeMember>() != null;
            corpse.Despawn();

            var z = Spawn(kind, feet, yaw, variant, seed);
            if (z == null) yield break;
            z.DisplayName = name + " (Zombie)";
            if (fromHorde) z.gameObject.AddComponent<HordeMember>();
            foreach (var kv in z.Parts)
            {
                if (kv.Value == null || !poses.TryGetValue(kv.Key, out var pose)) continue;
                kv.Value.transform.SetPositionAndRotation(pose.p, pose.r);
                if (kv.Value.Body != null) { kv.Value.Body.position = pose.p; kv.Value.Body.rotation = pose.r; RB.SetVel(kv.Value.Body, Vector3.zero); }
            }
            Physics.SyncTransforms();
            z.Ragdoll.Fall(0.8f);
            z.Voice.Say("z_rise", 1f);
            Stats.Add("zombie_risen");
            GameEvents.Kill(name + " rose as a zombie", new Color(0.55f, 0.9f, 0.4f));
        }

        static bool LegGone(Goredoll d, PartType t) => !d.Parts.TryGetValue(t, out var p) || p == null || p.Severed || p.Gibbed;
    }

    /// <summary>Marks a Goredoll as part of the Arena horde (so infected horde victims stay counted as horde).</summary>
    public sealed class HordeMember : MonoBehaviour { }

    /// <summary>
    /// Undead body: vitals are pinned (see Physiology.Undead), the zombie only dies after 3 headshots, when its head
    /// is destroyed or severed, or from an explicit Kill (Admin Gun / dev command). Also drives the zombie face.
    /// </summary>
    public sealed class ZombieDoll : MonoBehaviour
    {
        public ZombieKind Kind;
        public int Headshots;
        public const int HeadshotsToKill = 3;
        public ZombieBrain Brain;
        Goredoll doll;
        float lastHeadHit = -9f;
        bool wasDead;

        public void Init(Goredoll d, ZombieKind kind) { doll = d; Kind = kind; }

        public void OnPartHit(BodyPart part, DamageInfo info, float dmg)
        {
            if (doll == null || doll.IsDead) return;
            if (Brain != null) Brain.OnHurt(info);
            if (!part.IsHead) return;
            bool counts;
            switch (info.Type)
            {
                case DamageType.Bullet: case DamageType.Buckshot: case DamageType.Admin: case DamageType.Stab: case DamageType.Slash: counts = info.Amount > 0f; break;
                case DamageType.Explosion: counts = dmg > 35f; break;
                default: counts = dmg >= 18f; break;
            }
            if (!counts || Time.time - lastHeadHit < 0.1f) return; // one shotgun blast = one headshot
            lastHeadHit = Time.time;
            Headshots++;
            if (Headshots >= HeadshotsToKill) FinalDeath("Headshot x" + HeadshotsToKill);
            else
            {
                doll.Ragdoll.OnHit(25f, PartType.Head);
                if (Random.value < 0.6f) doll.Voice.Say("z_snarl", 0.9f);
                if (info.Source is PlayerController || info.Source is Weapon) GameEvents.ShowToast("Headshot " + Headshots + "/" + HeadshotsToKill);
            }
        }

        public void FinalDeath(string cause)
        {
            if (doll == null || doll.IsDead) return;
            if (doll.Parts.TryGetValue(PartType.Head, out var head) && head != null)
                AudioManager.Play("z_death", head.transform.position, 1f, Random.Range(0.9f, 1.05f), 1f, 1.5f, 40f);
            doll.Vitals.UndeadMayDie = true;
            doll.Vitals.Die(cause);
        }

        void Update()
        {
            if (doll == null || doll.NetProxy) return;
            if (doll.IsDead) { wasDead = true; return; }
            if (wasDead) { wasDead = false; Headshots = 0; } // revived by the Admin Gun
            if (!doll.Parts.TryGetValue(PartType.Head, out var head) || head == null || head.Gibbed || head.Severed) FinalDeath("Head destroyed");
        }

        void LateUpdate()
        {
            if (doll == null || doll.Face == null) return;
            if (doll.IsDead) { doll.Face.Expression = Expression.Dead; return; }
            doll.Face.Expression = Expression.Angry;
            float chew = Brain != null && Brain.Eating ? 0.35f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * 9f)) : 0.25f + 0.1f * Mathf.Sin(Time.time * 1.3f + Kind.GetHashCode());
            doll.Face.OpenMouth(chew);
        }
    }
}
