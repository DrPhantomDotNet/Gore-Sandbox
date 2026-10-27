using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    public enum ThrowableKind : byte { Rocket = 0, Grenade = 1, C4 = 2 }

    /// <summary>Weapons whose ammo isn't a Firearm magazine but can still be topped up (Inventory.Give, Arena refills).</summary>
    public interface IRefillable { void Refill(); }

    /// <summary>
    /// Shared plumbing for the rocket launcher, frag grenade and C4 (plus the factory for the katana / sledgehammer).
    ///
    /// Multiplayer: the host (or single player) owns every real projectile and calls <see cref="Explosion.Create"/>
    /// itself, so damage, physics and gore stay host-authoritative. A client sends a ReqThrowable request and spawns a
    /// local "visual" copy right away (it flies, bounces and sticks but never damages anything); the host spawns the
    /// real one and broadcasts ThrowableFx so every other client spawns a visual copy too. Explosions reach clients
    /// through the existing ExplosionFx broadcast. C4 detonation: clients send ReqDetonate, the host detonates that
    /// player's real charges and broadcasts C4Detonated so all clients drop the visual copies.
    /// </summary>
    public static class Explosives
    {
        public const float RocketRadius = 6f, RocketDamage = 260f, RocketPush = 1.6f, RocketSpeed = 52f;
        public const float FragRadius = 5.5f, FragDamage = 210f, FragPush = 1.3f, FragFuse = 3.5f;
        public const float C4Radius = 6.5f, C4Damage = 320f, C4Push = 2f;
        public const int MaxC4 = 8;

        public static Weapon CreateWeapon(WeaponDef def)
        {
            switch (def.Id)
            {
                case "rpg": return new RocketLauncher { Def = def };
                case "frag": return new FragGrenade { Def = def };
                case "c4": return new C4Weapon { Def = def };
                case "katana": return new Katana { Def = def };
                case "sledgehammer": return new Sledgehammer { Def = def };
                default: return null;
            }
        }

        public static byte LocalOwner => NetState.Active && NetManager.I != null ? NetManager.I.LocalId : (byte)0;

        static Transform Parent => WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null;

        /// <summary>The local player fires / throws something. Handles single player, host and client.</summary>
        public static void Launch(ThrowableKind kind, Vector3 pos, Vector3 vel, float fuse, object source)
        {
            if (NetState.IsClient)
            {
                if (NetManager.I != null) NetManager.I.RequestThrowable(kind, pos, vel, fuse);
                Spawn(kind, pos, vel, fuse, null, LocalOwner, true);
                return;
            }
            Spawn(kind, pos, vel, fuse, source, LocalOwner, false);
            if (NetState.IsHost && NetManager.I != null) NetManager.I.BroadcastThrowable(LocalOwner, kind, pos, vel, fuse, null);
        }

        /// <summary>Creates a projectile. <paramref name="visual"/> copies never deal damage (client-side mirrors).</summary>
        public static void Spawn(ThrowableKind kind, Vector3 pos, Vector3 vel, float fuse, object source, byte owner, bool visual)
        {
            switch (kind)
            {
                case ThrowableKind.Rocket: RocketProjectile.Create(pos, vel, source, owner, visual, Parent); break;
                case ThrowableKind.Grenade: GrenadeProjectile.Create(pos, vel, fuse, source, visual, Parent); break;
                case ThrowableKind.C4: C4Charge.Create(pos, vel, source, owner, visual, Parent); break;
            }
        }

        /// <summary>The local player presses the detonator.</summary>
        public static void Detonate()
        {
            if (NetState.IsClient)
            {
                if (NetManager.I != null) NetManager.I.RequestDetonate();
                return;
            }
            DetonateOwner(LocalOwner);
        }

        /// <summary>Authority: sets off every charge of one owner (and tells clients to drop their copies).</summary>
        public static void DetonateOwner(byte owner)
        {
            C4Charge.TriggerAll(owner);
            if (NetState.IsHost && NetManager.I != null) NetManager.I.BroadcastDetonate(owner);
        }

        public static int ChargeCount(byte owner) => C4Charge.Count(owner);

        /// <summary>Authority-side blast (Explosion.Create also broadcasts the effect to clients when hosting).</summary>
        public static void Blast(Vector3 pos, float radius, float damage, float push, object source)
        {
            if (NetState.IsClient) return;
            Explosion.Create(pos, radius, damage, source, push);
        }

        public static GameObject MeshObject(string name, Mesh mesh, int layer, Transform parent)
        {
            var go = new GameObject(name);
            go.layer = layer;
            if (parent != null) go.transform.SetParent(parent, true);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            return go;
        }
    }

    // ====================================================================== rocket

    /// <summary>A rocket flying in a straight line (slight drop), swept with raycasts; explodes on the first hit.</summary>
    public sealed class RocketProjectile : MonoBehaviour
    {
        Vector3 vel;
        object source;
        bool visual, done;
        float age;
        Light glow;
        AudioSource loop;

        public static RocketProjectile Create(Vector3 pos, Vector3 vel, object source, byte owner, bool visual, Transform parent)
        {
            var go = Explosives.MeshObject("Rocket", WeaponModels3.RocketMesh, Layers.Effects, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(vel.sqrMagnitude > 0.01f ? vel : Vector3.forward);
            var r = go.AddComponent<RocketProjectile>();
            r.vel = vel;
            r.source = source;
            r.visual = visual;
            var lgo = new GameObject("Flame");
            lgo.transform.SetParent(go.transform, false);
            lgo.transform.localPosition = new Vector3(0, 0, -0.45f);
            r.glow = lgo.AddComponent<Light>();
            r.glow.type = LightType.Point;
            r.glow.color = new Color(1f, 0.6f, 0.25f);
            r.glow.range = 7f;
            r.glow.intensity = 3f;
            r.glow.shadows = LightShadows.None;
            r.loop = AudioManager.CreateLoop("rocket_loop", go.transform, 0.8f, AudioBus.Sfx, 60f);
            return r;
        }

        void Update()
        {
            if (done) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            age += dt;
            vel += Vector3.down * 1.2f * dt;
            var pos = transform.position;
            var step = vel * dt;
            // Don't blow up on the shooter's own avatar right at launch.
            int mask = age < 0.15f ? Layers.ShootMask & ~(1 << Layers.RemotePlayer) : Layers.ShootMask;
            if (Physics.Raycast(pos, step.normalized, out var hit, step.magnitude + 0.05f, mask, QueryTriggerInteraction.Ignore))
            {
                Impact(hit);
                return;
            }
            transform.position = pos + step;
            transform.rotation = Quaternion.LookRotation(vel);
            var back = transform.position - transform.forward * 0.42f;
            FX.Fire(back, -vel * 0.05f + Random.insideUnitSphere * 0.3f, Random.Range(0.12f, 0.2f));
            for (int i = 0; i < 2; i++)
                FX.Smoke(back - step * (i * 0.5f), Random.insideUnitSphere * 0.4f + Vector3.up * 0.2f, Random.Range(0.25f, 0.45f), new Color(0.75f, 0.74f, 0.72f, 0.55f), Random.Range(1.2f, 2.4f));
            if (glow != null) glow.intensity = 2.6f + Random.value * 1.2f;
            if (age > 9f) Explode(transform.position, null);
        }

        void Impact(RaycastHit hit)
        {
            // A direct hit hurts a lot more than the blast alone.
            if (!visual)
            {
                var part = hit.collider.GetComponent<BodyPart>();
                var info = new DamageInfo { Amount = 120f, Force = vel.normalized * 40f, Point = hit.point, Normal = hit.normal, Direction = vel.normalized, Type = DamageType.Explosion, Source = source, Weapon = "Rocket Launcher" };
                if (part != null) part.TakeDamage(info);
                else hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(info);
            }
            Explode(hit.point + hit.normal * 0.2f, hit.collider);
        }

        void Explode(Vector3 at, Collider hit)
        {
            if (done) return;
            done = true;
            if (!visual) Explosives.Blast(at, Explosives.RocketRadius, Explosives.RocketDamage, Explosives.RocketPush, source);
            Destroy(gameObject);
        }
    }

    // ====================================================================== frag grenade

    /// <summary>A physical grenade (Debris layer: doesn't hit players) that bounces around and explodes when the fuse runs out.</summary>
    public sealed class GrenadeProjectile : MonoBehaviour
    {
        float fuse, lastBounce;
        object source;
        bool visual, done;
        Rigidbody rb;
#if UNITY_6000_0_OR_NEWER
        static PhysicsMaterial bouncy;
#else
        static PhysicMaterial bouncy;
#endif

        public static GrenadeProjectile Create(Vector3 pos, Vector3 vel, float fuse, object source, bool visual, Transform parent)
        {
            var go = Explosives.MeshObject("Frag Grenade", WeaponModels3.GrenadeMesh, Layers.Debris, parent);
            go.transform.position = pos;
            go.transform.rotation = Random.rotation;
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.034f;
#if UNITY_6000_0_OR_NEWER
            if (bouncy == null) bouncy = new PhysicsMaterial("Grenade") { bounciness = 0.35f, dynamicFriction = 0.6f, staticFriction = 0.7f, bounceCombine = PhysicsMaterialCombine.Average };
#else
            if (bouncy == null) bouncy = new PhysicMaterial("Grenade") { bounciness = 0.35f, dynamicFriction = 0.6f, staticFriction = 0.7f, bounceCombine = PhysicMaterialCombine.Average };
#endif
            col.sharedMaterial = bouncy;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            RB.SetDamping(rb, 0.05f, 1.5f);
            RB.SetVel(rb, vel);
            rb.angularVelocity = Random.insideUnitSphere * 12f;
            var g = go.AddComponent<GrenadeProjectile>();
            g.rb = rb;
            g.fuse = Mathf.Max(0.02f, fuse);
            g.source = source;
            g.visual = visual;
            if (fuse > 0.1f) AudioManager.Play("grenade_spoon", pos, 0.5f, Random.Range(0.95f, 1.05f));
            return g;
        }

        void Update()
        {
            if (done) return;
            fuse -= Time.deltaTime;
            if (fuse <= 0f)
            {
                done = true;
                var at = transform.position + Vector3.up * 0.1f;
                var c = GetComponent<Collider>();
                if (c != null) c.enabled = false;
                if (!visual) Explosives.Blast(at, Explosives.FragRadius, Explosives.FragDamage, Explosives.FragPush, source);
                Destroy(gameObject);
            }
        }

        void OnCollisionEnter(Collision c)
        {
            float v = c.relativeVelocity.magnitude;
            if (v < 1.2f || Time.time - lastBounce < 0.08f) return;
            lastBounce = Time.time;
            AudioManager.Play("grenade_bounce", transform.position, Mathf.Clamp01(v / 10f) * 0.8f, Random.Range(0.9f, 1.1f), 1f, 1f, 30f);
        }
    }

    // ====================================================================== C4

    /// <summary>A sticky charge: flies until it touches something, then glues itself to it (follows props and Goredoll limbs).</summary>
    public sealed class C4Charge : MonoBehaviour
    {
        static readonly List<C4Charge> all = new List<C4Charge>();
        public byte Owner;
        object source;
        bool visual, stuck, triggered, done;
        float blink, triggerT, delay;
        int beeps;
        Rigidbody rb;
        Renderer led;
        float born;

        public static int Count(byte owner)
        {
            int n = 0;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (all[i] == null) { all.RemoveAt(i); continue; }
                if (all[i].Owner == owner && !all[i].triggered) n++;
            }
            return n;
        }

        public static C4Charge Create(Vector3 pos, Vector3 vel, object source, byte owner, bool visual, Transform parent)
        {
            // Too many: the oldest one quietly disappears.
            if (Count(owner) >= Explosives.MaxC4)
                foreach (var old in all) if (old != null && old.Owner == owner && !old.triggered) { old.done = true; Destroy(old.gameObject); all.Remove(old); break; }
            var go = Explosives.MeshObject("C4", WeaponModels3.C4Mesh, Layers.Debris, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(vel.sqrMagnitude > 0.01f ? vel : Vector3.forward) * Quaternion.Euler(-90f, 0, 0);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.022f, 0);
            col.size = new Vector3(0.1f, 0.044f, 0.15f);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            RB.SetVel(rb, vel);
            rb.angularVelocity = Random.insideUnitSphere * 4f;
            var c = go.AddComponent<C4Charge>();
            c.rb = rb;
            c.Owner = owner;
            c.source = source;
            c.visual = visual;
            c.born = Time.time;
            c.led = WeaponModels3.Led(go.transform, new Vector3(0.012f, 0.06f, -0.015f), new Color(1f, 0.1f, 0.05f), 0.005f);
            all.Add(c);
            return c;
        }

        /// <summary>Starts the beep-beep → boom sequence on every live charge of an owner (staggered a little).</summary>
        public static void TriggerAll(byte owner)
        {
            int k = 0;
            foreach (var c in all.ToArray())
                if (c != null && c.Owner == owner && !c.triggered) { c.triggered = true; c.triggerT = 0f; c.delay = 0.42f + k++ * 0.09f; }
        }

        void Update()
        {
            if (done) return;
            float dt = Time.deltaTime;
            if (triggered)
            {
                triggerT += dt;
                if (beeps == 0 || (beeps == 1 && triggerT > 0.18f))
                {
                    beeps++;
                    AudioManager.Play("c4_beep", transform.position, 0.9f, 1.15f, 1f, 2f, 40f);
                }
                if (led != null) led.enabled = Mathf.Repeat(triggerT, 0.1f) < 0.05f;
                if (triggerT >= delay) Explode();
                return;
            }
            blink += dt;
            if (led != null) led.enabled = Mathf.Repeat(blink, 1f) < 0.12f;
            if (!stuck && Time.time - born > 20f && rb != null && rb.IsSleeping()) stuck = true;
            if (transform.position.y < -200f) { done = true; Destroy(gameObject); }
        }

        void Explode()
        {
            done = true;
            if (!visual) Explosives.Blast(transform.position + transform.up * 0.08f, Explosives.C4Radius, Explosives.C4Damage, Explosives.C4Push, source);
            Destroy(gameObject);
        }

        void OnCollisionEnter(Collision c)
        {
            if (stuck || done || c.contactCount == 0) return;
            var cp = c.GetContact(0);
            Stick(cp.point, cp.normal, c.collider);
        }

        void Stick(Vector3 point, Vector3 normal, Collider target)
        {
            stuck = true;
            // Leave physics entirely: a loose rigidbody/collider under a ragdoll limb would become part of its compound body.
            var col = GetComponent<Collider>();
            if (col != null) Destroy(col);
            if (rb != null) { rb.isKinematic = true; rb.detectCollisions = false; Destroy(rb); rb = null; }
            var fwd = Vector3.ProjectOnPlane(transform.forward, normal);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(Vector3.forward, normal);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.right;
            transform.SetPositionAndRotation(point, Quaternion.LookRotation(fwd.normalized, normal));
            if (target != null && target.gameObject.activeInHierarchy) transform.SetParent(target.transform, true);
            AudioManager.Play("c4_place", point, 0.7f, Random.Range(0.95f, 1.05f), 1f, 1f, 30f);
        }

        void OnDestroy() => all.Remove(this);
    }
}
