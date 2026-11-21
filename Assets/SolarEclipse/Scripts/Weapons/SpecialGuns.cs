using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Rounds for the Gore Sandbox 2 special guns (defined in ArsenalPack.DefineSpecial):
    /// gl = 40mm grenade that bursts on impact, rail = penetrating hypersonic slug with a beam,
    /// taser = drops and twitches the target, paint = coloured splats, water = push, put out fires, wash blood,
    /// flare = a burning flare that lights up the area and ignites Goredolls.
    /// World changes only happen with authority (host / singleplayer); the visuals play everywhere.
    /// </summary>
    public static class SpecialGuns
    {
        static readonly Color[] PaintColors = { new Color(0.1f, 0.6f, 1f), new Color(1f, 0.25f, 0.6f), new Color(0.4f, 1f, 0.2f), new Color(1f, 0.85f, 0.1f), new Color(0.65f, 0.3f, 1f), new Color(1f, 0.5f, 0.1f) };
        static int paintIndex;

        public static bool Fire(Weapon w, string kind, Vector3 origin, Vector3 dir, Vector3 muzzle, bool authority)
        {
            switch (kind)
            {
                case "gl": Grenade40.Launch(muzzle + dir * 0.3f, dir * 32f + Vector3.up * 1.5f, w, authority); return true;
                case "rail": Rail(w, origin, dir, muzzle, authority); return true;
                case "taser": Taser(w, origin, dir, muzzle, authority); return true;
                case "paint": Paint(origin, dir, authority); return true;
                case "water": Water(origin, dir, muzzle, authority); return true;
                case "flare": Flare.Launch(muzzle + dir * 0.3f, dir * 45f, w, authority); return true;
            }
            return false;
        }

        static Goredoll DollOf(Collider c) { var bp = c != null ? c.GetComponent<BodyPart>() : null; return bp != null ? bp.Doll : null; }

        // ------------------------------------------------------------------ railgun

        static void Rail(Weapon w, Vector3 origin, Vector3 dir, Vector3 muzzle, bool authority)
        {
            var hits = Physics.RaycastAll(origin, dir, 400f, Layers.ShootMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            Vector3 end = origin + dir * 400f;
            int walls = 0;
            foreach (var h in hits)
            {
                var bp = h.collider.GetComponent<BodyPart>();
                if (bp != null)
                {
                    if (authority) bp.TakeDamage(new DamageInfo { Amount = 220f, Type = DamageType.Bullet, Caliber = 20f, Point = h.point, Direction = dir, Normal = h.normal, Weapon = "Railgun", Source = w });
                    if (bp.Body != null && authority) bp.Body.AddForce(dir * 30f, ForceMode.VelocityChange);
                    BloodFX.Burst(h.point, dir, 2.5f, new Color(0.45f, 0.02f, 0.02f));
                }
                else
                {
                    Decals.Scorch(h.point, h.normal, 0.25f, h.collider);
                    FX.Sparks(h.point, h.normal, 14);
                    if (h.rigidbody != null && authority) h.rigidbody.AddForceAtPosition(dir * 40f, h.point, ForceMode.VelocityChange);
                    if (++walls >= 4) { end = h.point; break; }
                }
            }
            Beam(muzzle, end, new Color(0.3f, 0.8f, 1f), 0.06f, 0.35f);
            CameraFX.Shake(0.2f);
        }

        public static void Beam(Vector3 a, Vector3 b, Color c, float width, float life)
        {
            var go = new GameObject("Beam");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2; lr.SetPosition(0, a); lr.SetPosition(1, b);
            lr.widthMultiplier = width; lr.sharedMaterial = Mats.Glow(c, 3f); lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<BeamFade>().Init(lr, life);
        }

        sealed class BeamFade : MonoBehaviour
        {
            LineRenderer lr; float life, t, w0;
            public void Init(LineRenderer l, float lifeSecs) { lr = l; life = lifeSecs; w0 = l.widthMultiplier; }
            void Update() { t += Time.deltaTime; if (lr != null) lr.widthMultiplier = w0 * (1f - t / life); if (t >= life) Destroy(gameObject); }
        }

        // ------------------------------------------------------------------ taser

        static void Taser(Weapon w, Vector3 origin, Vector3 dir, Vector3 muzzle, bool authority)
        {
            if (!Physics.SphereCast(origin, 0.06f, dir, out var h, 9f, Layers.ShootMask, QueryTriggerInteraction.Ignore)) { Wire(muzzle, origin + dir * 9f); return; }
            Wire(muzzle, h.point);
            FX.Sparks(h.point, h.normal, 10);
            AudioManager.Play("zap", h.point, 0.8f, Random.Range(0.95f, 1.05f));
            var d = DollOf(h.collider);
            if (d != null) Machines.ElectricBurn(h.collider.GetComponent<BodyPart>(), h.point); // barb marks
            if (d == null || !authority) return;
            d.Ragdoll.Fall(5f);
            d.Vitals.Sedate(4f);
            d.gameObject.AddComponent<Tased>();
            if (d.Voice != null) d.Voice.Say("v_scream");
        }

        static void Wire(Vector3 a, Vector3 b) => Beam(a, b, new Color(0.9f, 0.9f, 0.7f), 0.006f, 1.2f);

        /// <summary>Muscles lock up and twitch for a few seconds.</summary>
        sealed class Tased : MonoBehaviour
        {
            Goredoll d; float until, next;
            void Start() { d = GetComponent<Goredoll>(); until = Time.time + 4f; }
            void Update()
            {
                if (d == null || Time.time > until) { Destroy(this); return; }
                if (Time.time < next) return;
                next = Time.time + 0.06f;
                foreach (var p in d.Parts.Values)
                    if (p != null && p.Body != null && Random.value < 0.5f) p.Body.AddTorque(Random.insideUnitSphere * 2.5f, ForceMode.VelocityChange);
                if (Random.value < 0.15f) FX.Sparks(d.Position + Random.insideUnitSphere * 0.3f, Vector3.up, 3);
            }
        }

        // ------------------------------------------------------------------ paintball

        static void Paint(Vector3 origin, Vector3 dir, bool authority)
        {
            dir = (dir + Random.insideUnitSphere * 0.012f).normalized;
            if (!Physics.Raycast(origin, dir, out var h, 80f, Layers.ShootMask, QueryTriggerInteraction.Ignore)) return;
            var c = PaintColors[paintIndex = (paintIndex + 1) % PaintColors.Length];
            Decals.Paint(h.point, h.normal, Random.Range(0.18f, 0.32f), c, h.collider);
            var bp = h.collider.GetComponent<BodyPart>();
            if (authority && bp != null) bp.TakeDamage(new DamageInfo { Amount = 1f, Type = DamageType.Blunt, Point = h.point, Direction = dir, Normal = h.normal, Weapon = "Paintball", Source = null });
            else if (authority && h.rigidbody != null) h.rigidbody.AddForceAtPosition(dir * 1.5f, h.point, ForceMode.Impulse);
        }

        // ------------------------------------------------------------------ water

        static ParticleSystem spray;

        static void Water(Vector3 origin, Vector3 dir, Vector3 muzzle, bool authority)
        {
            if (spray == null) spray = ParticleKit.Create("Water Spray", null, Mats.Particle, 600, 1.2f);
            for (int i = 0; i < 3; i++)
                ParticleKit.Emit(spray, muzzle, (dir + Random.insideUnitSphere * 0.04f) * Random.Range(13f, 16f), Random.Range(0.04f, 0.08f), 0.9f, new Color(0.75f, 0.85f, 1f, 0.6f));
            if (!Physics.SphereCast(origin, 0.15f, dir, out var h, 14f, Layers.ShootMask, QueryTriggerInteraction.Ignore)) return;
            Decals.Wash(h.point, 0.6f);
            var d = DollOf(h.collider);
            if (!authority) return;
            if (d != null)
            {
                Burning.Extinguish(d);
                var bp = h.collider.GetComponent<BodyPart>();
                if (bp != null && bp.Body != null) bp.Body.AddForce(dir * 1.2f, ForceMode.VelocityChange);
            }
            else if (h.rigidbody != null) h.rigidbody.AddForceAtPosition(dir * 0.8f, h.point, ForceMode.VelocityChange);
        }
    }

    /// <summary>40mm grenade: flies on a ballistic arc and explodes on the first hit (after 4 m arming distance it explodes harder).</summary>
    public sealed class Grenade40 : MonoBehaviour
    {
        object src; Vector3 start; bool done, real; float born;

        /// <param name="authority">False on machines that only draw it: it flies the same arc but the host's explosion is the real one.</param>
        public static void Launch(Vector3 pos, Vector3 vel, object source, bool authority = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "40mm Grenade";
            go.transform.position = pos; go.transform.localScale = Vector3.one * 0.08f;
            go.GetComponent<Renderer>().sharedMaterial = Mats.LitMetal;
            go.layer = Layers.Debris;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.25f; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            RB.SetVel(rb, vel);
            var g = go.AddComponent<Grenade40>(); g.src = source; g.start = pos; g.born = Time.time; g.real = authority;
        }

        void OnCollisionEnter(Collision c)
        {
            if (done || Time.time - born < 0.03f) return;
            done = true;
            bool armed = (transform.position - start).sqrMagnitude > 16f;
            if (!real) { Destroy(gameObject); return; } // the host's explosion reaches everyone
            if (armed) Explosion.Create(transform.position, 5f, 120f, src, 1.2f);
            else { AudioManager.Play("thump_heavy", transform.position, 0.8f, 1f); FX.Sparks(transform.position, Vector3.up, 6); }
            Destroy(gameObject);
        }

        void Update() { if (Time.time - born > 12f) Destroy(gameObject); }
    }

    /// <summary>Flare round: a burning red light that ignites Goredolls it hits and burns for 25 seconds.</summary>
    public sealed class Flare : MonoBehaviour
    {
        object src; float born; bool stuck, real; Light glow;

        public static void Launch(Vector3 pos, Vector3 vel, object source, bool authority = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Flare";
            go.transform.position = pos; go.transform.localScale = new Vector3(0.04f, 0.06f, 0.04f);
            go.GetComponent<Renderer>().sharedMaterial = Mats.Glow(new Color(1f, 0.15f, 0.08f), 4f);
            go.layer = Layers.Debris;
            var rb = go.AddComponent<Rigidbody>(); rb.mass = 0.1f; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            RB.SetVel(rb, vel);
            var f = go.AddComponent<Flare>(); f.src = source; f.born = Time.time; f.real = authority;
            f.glow = new GameObject("Flare Light").AddComponent<Light>();
            f.glow.transform.SetParent(go.transform, false);
            f.glow.type = LightType.Point; f.glow.color = new Color(1f, 0.2f, 0.1f); f.glow.range = 18f; f.glow.intensity = 3f; f.glow.shadows = LightShadows.None;
        }

        void OnCollisionEnter(Collision c)
        {
            if (stuck) return;
            var bp = c.collider.GetComponent<BodyPart>();
            if (bp != null && bp.Doll != null && real && !NetState.IsClient)
            {
                Burning.Apply(bp.Doll, 10f, src);
                bp.TakeDamage(new DamageInfo { Amount = 12f, Type = DamageType.Generic, Point = transform.position, Direction = Vector3.down, Normal = Vector3.up, Weapon = "Flare" });
            }
            if (c.relativeVelocity.magnitude < 6f) stuck = true;
        }

        void Update()
        {
            float age = Time.time - born;
            if (Random.value < 0.6f) FX.Fire(transform.position, Vector3.up * 0.6f + Random.insideUnitSphere * 0.3f, 0.12f);
            if (glow != null) glow.intensity = (3f + Mathf.PerlinNoise(Time.time * 12f, 0f) * 1.5f) * Mathf.Clamp01((25f - age) / 3f);
            if (age > 25f) Destroy(gameObject);
        }
    }
}
