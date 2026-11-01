using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Animals (Content Update): dog, pig and cow. Alive they walk with a procedural four-legged gait, wander,
    /// make noise and run from danger (the dog bites). Hit them and they bleed; enough damage turns them into a real
    /// jointed ragdoll; limbs come off, heads burst, explosions gib them. Simulated on the host / in singleplayer.
    /// </summary>
    public sealed class Animal : MonoBehaviour
    {
        public enum Kind { Dog, Pig, Cow }
        public static readonly List<Animal> All = new List<Animal>();

        public sealed class APart
        {
            public string Name; public Transform T; public Rigidbody Rb; public Collider Col; public float Hp, MaxHp;
            public APart Parent; public bool Limb, Severed, IsHead; public Vector3 RestPos; public Quaternion RestRot; public float Bleed;
        }

        public Kind Type;
        public float Health, MaxHealth;
        public bool Dead, Ragdolled;
        public readonly List<APart> Parts = new List<APart>();
        APart body, head;
        readonly APart[] legs = new APart[8]; // upper/lower x FL FR BL BR
        Vector3 target; float speed, phase, nextThink, nextSound, fearUntil, biteCd;
        Color skin, spot;
        float size;

        public string DisplayName => Type == Kind.Dog ? "Dog" : Type == Kind.Pig ? "Pig" : "Cow";

        // ------------------------------------------------------------------ build

        public static Animal Spawn(Kind k, Vector3 pos, float yaw)
        {
            var go = new GameObject(k.ToString());
            go.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var a = go.AddComponent<Animal>();
            a.Type = k;
            a.Build();
            go.AddComponent<Spawned>().DisplayName = a.DisplayName;
            return a;
        }

        void Build()
        {
            // Dimensions: body length / height / width, leg length, head size.
            float L, H, W, leg, hs;
            switch (Type)
            {
                case Kind.Dog: L = 0.75f; H = 0.32f; W = 0.26f; leg = 0.48f; hs = 0.2f; skin = new Color(0.55f, 0.38f, 0.22f); spot = new Color(0.95f, 0.92f, 0.85f); MaxHealth = 120f; speed = 3.5f; break;
                case Kind.Pig: L = 0.95f; H = 0.5f; W = 0.45f; leg = 0.3f; hs = 0.3f; skin = new Color(0.95f, 0.7f, 0.7f); spot = new Color(0.85f, 0.55f, 0.58f); MaxHealth = 220f; speed = 1.8f; break;
                default: L = 1.9f; H = 0.85f; W = 0.7f; leg = 0.75f; hs = 0.42f; skin = new Color(0.95f, 0.94f, 0.92f); spot = new Color(0.1f, 0.1f, 0.1f); MaxHealth = 600f; speed = 1.3f; break;
            }
            size = L;
            Health = MaxHealth;
            float bodyY = leg + H * 0.5f;
            body = AddPart("Body", null, new Vector3(0, bodyY, 0), mb =>
            {
                mb.Color = skin; mb.Gloss = 0.2f;
                mb.Capsule(new Vector3(0, 0, -L * 0.5f + H * 0.5f), new Vector3(0, 0, L * 0.5f - H * 0.5f), H * 0.5f, H * 0.5f, 12, 4);
                if (Type == Kind.Cow) { mb.SetColor(spot, 0.2f); mb.Sphere(new Vector3(W * 0.42f, H * 0.15f, 0.2f), new Vector3(0.05f, 0.22f, 0.3f)); mb.Sphere(new Vector3(-W * 0.42f, H * 0.05f, -0.4f), new Vector3(0.05f, 0.25f, 0.28f)); mb.SetColor(new Color(1f, 0.75f, 0.75f), 0.3f); mb.Sphere(new Vector3(0, -H * 0.5f, -L * 0.25f), new Vector3(0.14f, 0.1f, 0.16f)); }
                if (Type == Kind.Dog) { mb.SetColor(spot, 0.2f); mb.Sphere(new Vector3(0, -H * 0.2f, L * 0.25f), new Vector3(W * 0.42f, H * 0.32f, L * 0.22f)); }
            }, new Vector3(W, H, L), MaxHealth * 0.6f, 25f * L * L);
            head = AddPart("Head", body, new Vector3(0, bodyY + H * 0.35f, L * 0.5f + hs * 0.3f), mb =>
            {
                mb.Color = skin; mb.Gloss = 0.2f;
                mb.Sphere(Vector3.zero, new Vector3(hs * 0.5f, hs * 0.5f, hs * 0.55f));
                switch (Type)
                {
                    case Kind.Dog:
                        mb.Capsule(new Vector3(0, -hs * 0.1f, hs * 0.2f), new Vector3(0, -hs * 0.15f, hs * 0.75f), hs * 0.22f, hs * 0.18f, 8, 2);
                        mb.SetColor(Color.black, 0.8f); mb.Sphere(new Vector3(0, -hs * 0.1f, hs * 0.92f), new Vector3(0.025f, 0.02f, 0.02f));
                        mb.SetColor(skin * 0.7f, 0.2f);
                        mb.Box(new Vector3(hs * 0.32f, hs * 0.2f, -hs * 0.05f), new Vector3(0.03f, hs * 0.55f, hs * 0.3f));
                        mb.Box(new Vector3(-hs * 0.32f, hs * 0.2f, -hs * 0.05f), new Vector3(0.03f, hs * 0.55f, hs * 0.3f));
                        break;
                    case Kind.Pig:
                        mb.SetColor(spot, 0.3f); mb.Cylinder(new Vector3(0, -hs * 0.05f, hs * 0.45f), new Vector3(0, -hs * 0.05f, hs * 0.62f), hs * 0.2f, hs * 0.2f, 12);
                        mb.SetColor(new Color(0.4f, 0.2f, 0.2f), 0.3f); mb.Sphere(new Vector3(hs * 0.07f, -hs * 0.05f, hs * 0.62f), new Vector3(0.015f, 0.02f, 0.01f)); mb.Sphere(new Vector3(-hs * 0.07f, -hs * 0.05f, hs * 0.62f), new Vector3(0.015f, 0.02f, 0.01f));
                        mb.SetColor(skin, 0.2f);
                        mb.Box(new Vector3(hs * 0.3f, hs * 0.42f, 0), new Vector3(0.12f, 0.02f, 0.1f)); mb.Box(new Vector3(-hs * 0.3f, hs * 0.42f, 0), new Vector3(0.12f, 0.02f, 0.1f));
                        break;
                    default:
                        mb.Capsule(new Vector3(0, -hs * 0.1f, hs * 0.2f), new Vector3(0, -hs * 0.2f, hs * 0.65f), hs * 0.32f, hs * 0.28f, 10, 3);
                        mb.SetColor(new Color(1f, 0.75f, 0.75f), 0.3f); mb.Sphere(new Vector3(0, -hs * 0.2f, hs * 0.85f), new Vector3(hs * 0.3f, hs * 0.22f, hs * 0.12f));
                        mb.SetColor(new Color(0.9f, 0.85f, 0.7f), 0.5f);
                        mb.Cylinder(new Vector3(hs * 0.3f, hs * 0.35f, 0), new Vector3(hs * 0.62f, hs * 0.55f, -0.02f), 0.035f, 0.01f, 6);
                        mb.Cylinder(new Vector3(-hs * 0.3f, hs * 0.35f, 0), new Vector3(-hs * 0.62f, hs * 0.55f, -0.02f), 0.035f, 0.01f, 6);
                        break;
                }
                mb.SetColor(Color.black, 0.9f);
                mb.Sphere(new Vector3(hs * 0.25f, hs * 0.12f, hs * 0.38f), new Vector3(0.02f, 0.022f, 0.015f));
                mb.Sphere(new Vector3(-hs * 0.25f, hs * 0.12f, hs * 0.38f), new Vector3(0.02f, 0.022f, 0.015f));
            }, new Vector3(hs, hs, hs * 1.4f), MaxHealth * 0.25f, 2f + hs * 10f);
            head.IsHead = true;
            float lx = W * 0.35f, lz = L * 0.5f - H * 0.4f;
            Vector3[] hips = { new Vector3(-lx, leg, lz), new Vector3(lx, leg, lz), new Vector3(-lx, leg, -lz), new Vector3(lx, leg, -lz) };
            float r = Type == Kind.Cow ? 0.07f : Type == Kind.Pig ? 0.06f : 0.04f;
            for (int i = 0; i < 4; i++)
            {
                var upper = AddPart("Leg" + i, body, hips[i], mb => { mb.Color = skin; mb.Capsule(Vector3.zero, new Vector3(0, -leg * 0.5f, 0), r * 1.2f, r, 8, 2); }, new Vector3(r * 2.4f, leg * 0.5f, r * 2.4f), MaxHealth * 0.12f, 1.5f + r * 20f, true);
                var lower = AddPart("Shin" + i, upper, hips[i] + Vector3.down * leg * 0.5f, mb =>
                {
                    mb.Color = skin; mb.Capsule(Vector3.zero, new Vector3(0, -leg * 0.5f + r, 0), r, r * 0.85f, 8, 2);
                    mb.SetColor(Type == Kind.Dog ? skin * 0.6f : new Color(0.2f, 0.15f, 0.12f), 0.4f); mb.Cylinder(new Vector3(0, -leg * 0.5f + r * 1.2f, 0), new Vector3(0, -leg * 0.5f, 0), r * 1.05f, r * 1.1f, 8);
                }, new Vector3(r * 2.2f, leg * 0.5f, r * 2.2f), MaxHealth * 0.1f, 1f + r * 15f, true);
                legs[i * 2] = upper; legs[i * 2 + 1] = lower;
            }
            AddPart("Tail", body, new Vector3(0, bodyY + H * 0.2f, -L * 0.5f), mb =>
            {
                mb.Color = Type == Kind.Cow ? spot : skin;
                if (Type == Kind.Pig) for (int i = 0; i < 6; i++) { float a = i * 1.1f; mb.Sphere(new Vector3(Mathf.Cos(a) * 0.03f, Mathf.Sin(a) * 0.03f, -i * 0.012f), new Vector3(0.012f, 0.012f, 0.012f)); }
                else mb.Capsule(Vector3.zero, new Vector3(0, -H * (Type == Kind.Cow ? 0.9f : 0.2f), -H * (Type == Kind.Cow ? 0.1f : 0.5f)), 0.025f, 0.015f, 6, 2);
            }, new Vector3(0.05f, 0.05f, 0.2f), MaxHealth * 0.05f, 0.3f, true);
            target = transform.position;
        }

        APart AddPart(string name, APart parent, Vector3 localPivot, System.Action<MeshBuilder> build, Vector3 colSize, float hp, float mass, bool limb = false)
        {
            var go = new GameObject(name);
            go.layer = Layers.Ragdoll;
            go.transform.SetParent(parent != null ? parent.T : transform, false);
            var world = transform.TransformPoint(localPivot);
            go.transform.position = world;
            var mb = new MeshBuilder { Color = Color.white };
            build(mb);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(DisplayName + " " + name);
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = colSize;
            bc.center = limb ? new Vector3(0, -colSize.y * 0.5f, 0) : Vector3.zero;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass; rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None;
            var p = new APart { Name = name, T = go.transform, Rb = rb, Col = bc, Hp = hp, MaxHp = hp, Parent = parent, Limb = limb, RestPos = go.transform.localPosition, RestRot = go.transform.localRotation };
            go.AddComponent<AnimalPart>().Init(this, p);
            Parts.Add(p);
            return p;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);
        void OnDestroy() { foreach (var p in Parts) if (p.T != null && p.T.parent != transform && !p.T.IsChildOf(transform)) Destroy(p.T.gameObject); }

        // ------------------------------------------------------------------ alive: walk, wander, flee, bite

        void Update()
        {
            if (Ragdolled) { BleedTick(); return; }
            float dt = Time.deltaTime;
            bool scared = Time.time < fearUntil;
            if (NetState.Authority && Time.time > nextThink)
            {
                nextThink = Time.time + Random.Range(2f, 5f);
                var threat = NearestThreat(out float td);
                if (scared && threat.HasValue && Type != Kind.Dog) target = transform.position + MathX.Flat(transform.position - threat.Value).normalized * 10f;
                else if (Type == Kind.Dog && scared && threat.HasValue) target = threat.Value;
                else if (Random.value < 0.6f) target = transform.position + new Vector3(Random.Range(-8f, 8f), 0, Random.Range(-8f, 8f));
                else target = transform.position;
            }
            var to = MathX.Flat(target - transform.position);
            float moveSpeed = 0f;
            if (to.magnitude > 0.6f)
            {
                var want = Quaternion.LookRotation(to);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 120f * dt);
                moveSpeed = speed * (scared ? 1.9f : 1f);
                var step = transform.forward * moveSpeed * dt;
                if (Physics.Raycast(transform.position + Vector3.up * 0.4f, transform.forward, size * 0.6f + 0.3f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) { target = transform.position - transform.forward * 4f + transform.right * Random.Range(-4f, 4f); step = Vector3.zero; }
                transform.position += step;
            }
            // Stick to the ground.
            if (Physics.Raycast(transform.position + Vector3.up * 1.5f, Vector3.down, out var g, 4f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                transform.position = new Vector3(transform.position.x, Mathf.Lerp(transform.position.y, g.point.y, 1f - Mathf.Exp(-12f * dt)), transform.position.z);
            // Gait: diagonal pairs swing together.
            phase += moveSpeed * dt * (6f / Mathf.Max(0.4f, size));
            float amp = Mathf.Clamp01(moveSpeed / speed) * 32f;
            for (int i = 0; i < 4; i++)
            {
                if (legs[i * 2] == null || legs[i * 2].Severed) continue;
                float ph = phase + (i == 0 || i == 3 ? 0f : Mathf.PI);
                legs[i * 2].T.localRotation = legs[i * 2].RestRot * Quaternion.Euler(Mathf.Sin(ph) * amp, 0, 0);
                if (legs[i * 2 + 1] != null) legs[i * 2 + 1].T.localRotation = legs[i * 2 + 1].RestRot * Quaternion.Euler(Mathf.Max(0f, Mathf.Cos(ph)) * -amp * 0.8f, 0, 0);
            }
            if (body != null) body.T.localPosition = body.RestPos + Vector3.up * Mathf.Abs(Mathf.Sin(phase)) * 0.02f * Mathf.Clamp01(moveSpeed);
            if (head != null && !head.Severed) head.T.localRotation = head.RestRot * Quaternion.Euler(Mathf.Sin(Time.time * 1.3f) * 8f + (moveSpeed < 0.1f ? 18f * Mathf.Max(0f, Mathf.Sin(Time.time * 0.4f)) : 0f), Mathf.Sin(Time.time * 0.7f) * 15f, 0);
            if (Time.time > nextSound) { nextSound = Time.time + Random.Range(4f, 12f) / (scared ? 3f : 1f); Voice(scared ? 1f : 0.7f); }
            // The dog bites what it chases.
            biteCd -= dt;
            if (Type == Kind.Dog && scared && biteCd <= 0f && NetState.Authority && head != null) Bite();
            BleedTick();
        }

        void Voice(float vol)
        {
            var clip = Type == Kind.Dog ? "bark" : Type == Kind.Pig ? "oink" : "moo";
            AudioManager.Play(clip, head != null ? head.T.position : transform.position, vol, Random.Range(0.9f, 1.1f));
        }

        Vector3? NearestThreat(out float dist)
        {
            dist = 25f; Vector3? best = null;
            foreach (var p in PlayerTargets.All)
            {
                if (p == null || p.IsDead) continue;
                float d = Vector3.Distance(p.Position, transform.position);
                if (d < dist) { dist = d; best = p.Position; }
            }
            foreach (var g in Goredoll.All)
            {
                if (g == null || g.IsDead) continue;
                float d = Vector3.Distance(g.Position, transform.position);
                if (d < dist * 0.7f) { dist = d; best = g.Position; }
            }
            return best;
        }

        void Bite()
        {
            var mouth = head.T.position + head.T.forward * 0.2f;
            foreach (var c in Physics.OverlapSphere(mouth, 0.35f, Layers.ShootMask | (1 << Layers.LocalHitbox), QueryTriggerInteraction.Ignore))
            {
                if (c.transform.IsChildOf(transform)) continue;
                var part = c.GetComponent<BodyPart>();
                if (part != null && part.Doll != null)
                {
                    part.TakeDamage(new DamageInfo { Amount = 14f, Type = DamageType.Stab, Point = c.ClosestPoint(mouth), Direction = transform.forward, Normal = -transform.forward, Source = this, Weapon = "Dog bite" });
                    biteCd = 0.8f; Voice(1f); return;
                }
                var pt = c.GetComponentInParent<IPlayerTarget>();
                if (pt != null && !pt.IsDead && pt.Damageable != null)
                {
                    pt.Damageable.TakeDamage(new DamageInfo { Amount = 9f, Type = DamageType.Stab, Point = mouth, Direction = transform.forward, Normal = -transform.forward, Source = this, Weapon = "Dog bite" });
                    biteCd = 0.9f; Voice(1f); return;
                }
            }
        }

        // ------------------------------------------------------------------ damage + gore

        public void Hit(APart p, DamageInfo info)
        {
            if (p == null || p.Severed && p.Hp <= 0f && !Ragdolled) return;
            float dmg = info.Amount * (info.Type == DamageType.Explosion ? 1.5f : 1f);
            if (!Dead) Health -= dmg * (p.IsHead ? 2.2f : 1f);
            p.Hp -= dmg;
            p.Bleed = Mathf.Min(3f, p.Bleed + dmg / 40f);
            var dir = info.Direction.sqrMagnitude > 0.01f ? info.Direction.normalized : Vector3.up;
            BloodFX.Entry(info.Point, info.Normal.sqrMagnitude > 0.01f ? info.Normal : -dir, dir, Mathf.Clamp(dmg / 30f, 0.3f, 2f));
            if (Random.value < 0.4f) Decals.Blood(info.Point + Vector3.down * 0.5f, Vector3.up, 0.25f, 0, null, 0.3f);
            fearUntil = Time.time + 12f;
            if (!Dead && Random.value < 0.6f) Voice(1f);
            if (!Dead && Health <= 0f) Die(info);
            else if (!Ragdolled && (dmg > MaxHealth * 0.3f || info.Type == DamageType.Explosion)) MakeRagdoll(info.Force * 0.02f);
            if (Ragdolled && p.Rb != null && !p.Rb.isKinematic && info.Force.sqrMagnitude > 0f) p.Rb.AddForceAtPosition(info.Force * 0.25f, info.Point, ForceMode.Impulse);
            if (p.Hp <= 0f)
            {
                if (p.IsHead && dmg > 40f) Gib(p, dir);
                else if (p.Limb && !p.Severed && (info.Type == DamageType.Slash || info.Type == DamageType.Explosion || info.Type == DamageType.Buckshot || dmg > 60f)) Sever(p, dir);
            }
            if (info.Type == DamageType.Explosion && dmg > 150f) foreach (var q in new List<APart>(Parts)) if (q != body && Random.value < 0.4f) { if (q.IsHead) Gib(q, dir); else if (q.Limb) Sever(q, (q.T.position - info.Point).normalized); }
        }

        void Die(DamageInfo info)
        {
            Dead = true;
            Health = 0f;
            MakeRagdoll(info.Force * 0.02f + Vector3.up * 0.5f);
            GameEvents.Kill(DisplayName + "  -  " + (info.Weapon ?? "killed"), new Color(1f, 0.6f, 0.4f));
            Stats.Add(Stats.Kills);
        }

        /// <summary>Unparents every part and joins them with joints: a real ragdoll from here on.</summary>
        public void MakeRagdoll(Vector3 push)
        {
            if (Ragdolled) return;
            Ragdolled = true;
            foreach (var p in Parts)
            {
                p.T.SetParent(transform.parent, true);
                p.Rb.isKinematic = false;
                p.Rb.interpolation = RigidbodyInterpolation.Interpolate;
                p.Rb.velocity = push;
            }
            foreach (var p in Parts)
            {
                if (p.Parent == null || p.Severed) continue;
                var j = p.T.gameObject.AddComponent<ConfigurableJoint>();
                j.connectedBody = p.Parent.Rb;
                j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
                j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Limited;
                j.lowAngularXLimit = new SoftJointLimit { limit = -60f }; j.highAngularXLimit = new SoftJointLimit { limit = 60f };
                j.angularYLimit = new SoftJointLimit { limit = 30f }; j.angularZLimit = new SoftJointLimit { limit = 30f };
            }
            for (int i = 0; i < Parts.Count; i++)
                for (int k = i + 1; k < Parts.Count; k++)
                    Physics.IgnoreCollision(Parts[i].Col, Parts[k].Col, true);
        }

        void Sever(APart p, Vector3 dir)
        {
            if (p.Severed) return;
            if (!Ragdolled) MakeRagdoll(Vector3.zero);
            p.Severed = true;
            foreach (var j in p.T.GetComponents<ConfigurableJoint>()) Destroy(j);
            p.Rb.AddForce((dir + Vector3.up) * 3f, ForceMode.VelocityChange);
            p.Bleed = 3f;
            if (p.Parent != null) p.Parent.Bleed = 3f;
            BloodFX.Spurt(p.T.position, dir + Vector3.up * 0.5f, 2.5f);
            AudioManager.Play("dismember", p.T.position, 0.9f);
            Stats.Add(Stats.Limbs);
            Debris.Register(p.T.gameObject, 90f);
        }

        void Gib(APart p, Vector3 dir)
        {
            if (p.Hp < -9999f) return;
            p.Hp = -10000f;
            var pos = p.T.position;
            BloodFX.Burst(pos, dir + Vector3.up * 0.5f, 3f, new Color(0.5f, 0.02f, 0.02f));
            BloodFX.Mist(pos, dir, 2f);
            AudioManager.Play("gib", pos, 1f);
            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject("Chunk");
                go.layer = Layers.Debris;
                go.transform.position = pos + Random.insideUnitSphere * 0.1f;
                var mb = new MeshBuilder { Color = Random.value < 0.5f ? new Color(0.55f, 0.06f, 0.06f) : skin };
                mb.Sphere(Vector3.zero, Vector3.one * Random.Range(0.03f, 0.07f), 5, 6);
                go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Chunk");
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                go.AddComponent<SphereCollider>().radius = 0.04f;
                var rb = go.AddComponent<Rigidbody>(); rb.mass = 0.2f; rb.velocity = (dir + Random.insideUnitSphere) * Random.Range(3f, 7f);
                Debris.Register(go, 30f);
            }
            if (p.IsHead && !Dead) { Dead = true; Health = 0f; MakeRagdoll(dir); }
            if (!Ragdolled) MakeRagdoll(dir);
            p.Severed = true;
            if (p.Parent != null) p.Parent.Bleed = 3f;
            Destroy(p.T.gameObject);
            Parts.Remove(p);
        }

        void BleedTick()
        {
            foreach (var p in Parts)
            {
                if (p.Bleed <= 0f || p.T == null) continue;
                p.Bleed = Mathf.MoveTowards(p.Bleed, 0f, Time.deltaTime * (Dead ? 0.05f : 0.15f));
                if (Random.value < p.Bleed * Time.deltaTime * 6f) BloodFX.Drip(p.T.position, p.Rb != null && !p.Rb.isKinematic ? p.Rb.velocity : Vector3.zero);
            }
        }

        // ------------------------------------------------------------------ spawn menu

        public static void RegisterSpawnables(List<SpawnableDef> ai)
        {
            ai.Add(new SpawnableDef { Id = "animal_dog", Name = "Dog", Category = "AI", Glyph = "duck", Description = "Wanders, barks and bites when it's scared. Full gore.", Spawn = (p, r) => Spawn(Kind.Dog, p, r.eulerAngles.y).gameObject });
            ai.Add(new SpawnableDef { Id = "animal_pig", Name = "Pig", Category = "AI", Glyph = "duck", Description = "Oinks around and runs from trouble. Full gore.", Spawn = (p, r) => Spawn(Kind.Pig, p, r.eulerAngles.y).gameObject });
            ai.Add(new SpawnableDef { Id = "animal_cow", Name = "Cow", Category = "AI", Glyph = "duck", Description = "Big, slow and very heavy. Full gore.", Spawn = (p, r) => Spawn(Kind.Cow, p, r.eulerAngles.y).gameObject });
        }
    }

    /// <summary>Receives damage for one animal body part.</summary>
    public sealed class AnimalPart : MonoBehaviour, IDamageable
    {
        Animal owner; Animal.APart part;
        public bool IsAlive => owner != null && !owner.Dead;
        public void Init(Animal a, Animal.APart p) { owner = a; part = p; }
        public void TakeDamage(DamageInfo info) { if (owner != null && !NetState.IsClient) owner.Hit(part, info); }
        void OnCollisionEnter(Collision c)
        {
            if (owner == null || !owner.Ragdolled || NetState.IsClient) return;
            float v = c.relativeVelocity.magnitude;
            if (v > 9f) owner.Hit(part, new DamageInfo { Amount = (v - 9f) * 12f, Type = DamageType.Blunt, Point = c.GetContact(0).point, Direction = -c.relativeVelocity.normalized, Normal = c.GetContact(0).normal });
        }
    }

    public static class Animals
    {
        public static void RegisterSpawnables(List<SpawnableDef> ai) => Animal.RegisterSpawnables(ai);
    }
}
