using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Harpoon projectile (Harpoon Gun). Flies with a little drop, impales Goredoll body parts (joint between the part
    /// and the harpoon), carries them along and pins them to the wall / prop it sticks into behind them.
    /// Authority (offline / host) does damage, joints and forces; clients spawn a visual-only harpoon that sticks to
    /// whatever it hits and see the pinned bodies through the synced ragdolls.
    /// </summary>
    public sealed class Harpoon : MonoBehaviour
    {
        public const int MaxHarpoons = 14;
        const float Speed = 70f, CarrySpeed = 20f, MaxCarry = 4f, ShaftAnchor = -0.28f;
        static readonly List<Harpoon> live = new List<Harpoon>();
        static Mesh mesh;

        WeaponDef def;
        object source;
        bool authority, flying = true;
        Vector3 vel;
        float life, carried, anchorZ;
        Rigidbody rb;
        readonly List<BodyPart> impaled = new List<BodyPart>();
        readonly List<Collider> impaledCols = new List<Collider>();
        readonly List<ConfigurableJoint> joints = new List<ConfigurableJoint>();

        public static void ClearAll()
        {
            for (int i = live.Count - 1; i >= 0; i--) if (live[i] != null) Destroy(live[i].gameObject);
            live.Clear();
        }

        /// <param name="pos">Start position of the tip.</param>
        public static Harpoon Launch(Vector3 pos, Vector3 dir, WeaponDef def, object source, bool authority)
        {
            if (dir.sqrMagnitude < 1e-6f) return null;
            dir.Normalize();
            var go = new GameObject("Harpoon");
            go.layer = Layers.Debris;
            go.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));

            if (mesh == null) mesh = WeaponModels2.HarpoonMesh(0.8f);
            var vis = new GameObject("Mesh");
            vis.layer = Layers.Debris;
            vis.transform.SetParent(go.transform, false);
            vis.transform.localPosition = new Vector3(0f, 0f, -0.8f); // pivot = tip
            vis.AddComponent<MeshFilter>().sharedMesh = mesh;
            vis.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;

            var h = go.AddComponent<Harpoon>();
            h.def = def;
            h.source = source;
            h.authority = authority;
            h.vel = dir * Speed;
            h.rb = go.AddComponent<Rigidbody>();
            h.rb.isKinematic = true;
            h.rb.mass = 1.5f;
            h.rb.interpolation = RigidbodyInterpolation.Interpolate;

            live.Add(h);
            while (live.Count > MaxHarpoons)
            {
                var old = live[0];
                live.RemoveAt(0);
                if (old != null) Destroy(old.gameObject);
            }
            Debris.Register(go, 300f); // cleaned up with the rest of the debris (map change / clear)
            return h;
        }

        void OnDestroy()
        {
            live.Remove(this);
            // Never leave a joint whose connected body vanished (it would nail the limb to thin air).
            foreach (var j in joints) if (j != null) Destroy(j);
            joints.Clear();
        }

        bool Ignored(Collider c)
        {
            if (c == null || c.isTrigger) return true;
            if (impaledCols.Contains(c)) return true;
            if (source is Component sc && sc != null && c.transform.IsChildOf(sc.transform)) return true;
            return false;
        }

        bool Cast(Vector3 from, Vector3 dir, float dist, out RaycastHit best)
        {
            best = default;
            int mask = source is RemotePlayer ? Layers.RemoteShootMask : Layers.ShootMask;
            var hits = Physics.RaycastAll(from, dir, dist, mask, QueryTriggerInteraction.Ignore);
            float bestD = float.MaxValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.distance >= bestD || Ignored(h.collider)) continue;
                var part = h.collider.GetComponent<BodyPart>();
                if (part != null && impaled.Count > 0 && impaled[0] != null && part.Doll == impaled[0].Doll) continue; // already skewering this body
                bestD = h.distance; best = h; found = true;
            }
            return found;
        }

        void FixedUpdate()
        {
            if (!flying) return;
            float dt = Time.fixedDeltaTime;
            life += dt;
            if (life > 4f) { Drop(); return; }

            // Slide skewered bodies down the shaft as it passes through them.
            if (joints.Count > 0)
            {
                anchorZ = Mathf.MoveTowards(anchorZ, ShaftAnchor, dt * 5f);
                foreach (var j in joints) if (j != null) j.connectedAnchor = new Vector3(0f, 0f, anchorZ);
            }

            vel += Physics.gravity * dt * (impaled.Count > 0 ? 0.6f : 0.12f);
            var pos = rb.position;
            var step = vel * dt;
            float dist = step.magnitude;
            if (dist < 1e-5f) return;
            var dir = step / dist;

            if (Cast(pos, dir, dist + 0.02f, out var hit))
            {
                var part = hit.collider.GetComponent<BodyPart>();
                if (part != null && part.Doll != null && !part.Gibbed) { HitBody(part, hit, dir); return; }
                var dmg = hit.collider.GetComponentInParent<IDamageable>();
                if (hit.collider.GetComponentInParent<RemotePlayer>() != null || hit.collider.gameObject.layer == Layers.LocalHitbox)
                {
                    if (authority && dmg != null)
                        dmg.TakeDamage(new DamageInfo { Amount = def.Damage, Force = dir * 20f, Point = hit.point, Normal = hit.normal, Direction = dir, Type = DamageType.Stab, Source = source, Weapon = def.Name, Caliber = def.Caliber });
                    BloodFX.Entry(hit.point, hit.normal, dir, 1.2f);
                    Stick(hit, dir, false);
                    return;
                }
                HitSurface(hit, dir, dmg);
                return;
            }

            rb.MovePosition(pos + step);
            rb.MoveRotation(Quaternion.LookRotation(vel));
            if (impaled.Count > 0)
            {
                carried += dist;
                if (carried > MaxCarry) Drop();
            }
        }

        void HitBody(BodyPart part, RaycastHit hit, Vector3 dir)
        {
            AudioManager.Play("stab", hit.point, 0.9f, Random.Range(0.85f, 1f), 1f, 2f, 40f);
            BloodFX.Entry(hit.point, hit.normal, dir, 1.6f);
            if (!authority)
            {
                // Visual only: stay in the (synced) body part.
                transform.SetParent(part.transform, true);
                transform.position = hit.point + dir * 0.15f;
                Stop();
                return;
            }

            part.TakeDamage(new DamageInfo { Amount = def.Damage, Force = dir * 15f, Point = hit.point, Normal = hit.normal, Direction = dir, Type = DamageType.Stab, Source = source, Weapon = def.Name, Caliber = def.Caliber, Penetration = 1f });
            BloodFX.Exit(hit.point + dir * 0.12f, dir, 1.2f);
            if (part == null || part.Gibbed || part.Body == null || impaled.Count >= 3) return; // keep flying through

            // Move the tip to the hit point first so both joint anchors start on the same spot (no snap).
            rb.position = hit.point;
            transform.position = hit.point;
            var j = part.Body.gameObject.AddComponent<ConfigurableJoint>();
            j.autoConfigureConnectedAnchor = false;
            j.connectedBody = rb;
            j.anchor = part.Body.transform.InverseTransformPoint(hit.point);
            j.connectedAnchor = Vector3.zero;
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
            j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Free;
            j.enableCollision = false;
            j.breakForce = 9000f;
            joints.Add(j);
            anchorZ = 0f;
            impaled.Add(part);
            if (part.Col != null) impaledCols.Add(part.Col);
            if (impaled.Count == 1)
            {
                part.Doll.Launch(dir * 8f);
                // A body slows the harpoon down; it carries it for a few metres looking for something to pin it to.
                vel = dir * Mathf.Min(vel.magnitude, CarrySpeed);
                carried = 0f;
            }
        }

        void HitSurface(RaycastHit hit, Vector3 dir, IDamageable dmg)
        {
            if (hit.collider.gameObject.layer == Layers.Boundaries) { Drop(); return; }
            var surface = Surface.Of(hit.collider);
            FX.Impact(hit.point, hit.normal, surface);
            AudioManager.Play("harpoon_hit", hit.point, 0.9f, Random.Range(0.92f, 1.08f), 1f, 2f, 60f);
            if (impaled.Count > 0)
            {
                BloodFX.Burst(hit.point - dir * 0.15f, -hit.normal, 1.5f, new Color(0.45f, 0.02f, 0.02f));
                Decals.Blood(hit.point, hit.normal, 0.6f, 0, hit.collider);
            }
            if (authority)
            {
                dmg?.TakeDamage(new DamageInfo { Amount = def.Damage * 0.5f, Force = dir * 10f, Point = hit.point, Normal = hit.normal, Direction = dir, Type = DamageType.Stab, Source = source, Weapon = def.Name, Caliber = def.Caliber });
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic) hit.rigidbody.AddForceAtPosition(dir * 25f, hit.point, ForceMode.Impulse);
            }
            Stick(hit, dir, true);
        }

        /// <summary>Embeds the tip in what it hit and stops (pinning anything skewered to it).</summary>
        void Stick(RaycastHit hit, Vector3 dir, bool embed)
        {
            var p = hit.point + dir * (embed ? 0.07f : 0.12f);
            rb.position = p;
            transform.position = p;
            transform.rotation = Quaternion.LookRotation(dir);
            var body = hit.rigidbody;
            if (body != null && !body.isKinematic)
            {
                if (authority)
                {
                    // Dynamic prop: the harpoon becomes a physical part of it (and drags pinned bodies along).
                    rb.isKinematic = false;
                    AddCollider();
                    Physics.IgnoreCollision(GetComponentInChildren<Collider>(), hit.collider, true);
                    var fj = gameObject.AddComponent<FixedJoint>();
                    fj.connectedBody = body;
                    fj.breakForce = 20000f;
                }
                else transform.SetParent(hit.collider.transform, true);
            }
            else if (body != null) transform.SetParent(body.transform, true); // kinematic / frozen prop: follow it
            else if (hit.collider.GetComponentInParent<RemotePlayer>() != null || hit.collider.gameObject.layer == Layers.LocalHitbox)
                transform.SetParent(hit.collider.transform, true);
            Stop();
        }

        void Stop()
        {
            flying = false;
            anchorZ = joints.Count > 0 ? ShaftAnchor : anchorZ;
            foreach (var j in joints) if (j != null) j.connectedAnchor = new Vector3(0f, 0f, anchorZ);
            if (rb.isKinematic && transform.parent != null && transform.parent.GetComponentInParent<Rigidbody>() != null && joints.Count == 0)
            {
                // Riding on something that moves: drop our own body so the transform follows the parent.
                Destroy(rb);
            }
        }

        /// <summary>No target to pin to: becomes a loose physical object (still dangling from skewered bodies).</summary>
        void Drop()
        {
            flying = false;
            rb.isKinematic = false;
            AddCollider();
            RB.SetVel(rb, vel * 0.3f);
        }

        void AddCollider()
        {
            if (GetComponent<CapsuleCollider>() != null) return;
            var c = gameObject.AddComponent<CapsuleCollider>();
            c.direction = 2;
            c.center = new Vector3(0f, 0f, -0.4f);
            c.height = 0.8f;
            c.radius = 0.012f;
            foreach (var ic in impaledCols) if (ic != null) Physics.IgnoreCollision(c, ic, true);
        }
    }
}
