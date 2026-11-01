using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SolarEclipse
{
    /// <summary>
    /// Content Update machines (20, all in the spawn menu's Props tab): wood chipper, shredder, piston crusher,
    /// conveyor belt, saw wall, tesla coil, bear trap, guillotine, rocket chair, trampoline, catapult, wrecking ball,
    /// pinball flippers, wind tunnel, magnet crane, washing machine, microwave, can cannon vending machine, meat hook,
    /// electric chair. Moving parts animate everywhere; the gore runs on the host / in singleplayer.
    /// </summary>
    public static class Machines
    {
        static bool registered;

        public static void Register(Action<string, string, string, string, Func<Vector3, Quaternion, GameObject>> add)
        {
            if (registered) return;
            registered = true;
            add("woodchipper", "Wood Chipper", "Feed it anything. It eats Goredolls feet first and sprays them out the chute.", "saw", WoodChipper.Create);
            add("shredder", "Industrial Shredder", "Twin spiked rollers in an open box. Drop things in.", "saw", Shredder.Create);
            add("piston", "Piston Crusher", "A hydraulic ram that slams down every few seconds. Flattens what's underneath.", "hammer", Piston.Create);
            add("conveyor", "Conveyor Belt", "Carries everything along. Point it at a machine.", "arrow_right", Conveyor.Create);
            add("sawwall", "Saw Wall", "A wall of spinning circular saws. Limbs come off.", "saw", SawWall.Create);
            add("tesla", "Tesla Coil", "Arcs lightning into anything that comes close. Fries and chars Goredolls.", "lightning", Tesla.Create);
            add("beartrap", "Bear Trap", "Steel jaws snap shut on the first leg to step in, and hold it.", "bone", BearTrap.Create);
            add("guillotine", "Guillotine", "Press E to drop the blade. Heads roll.", "cut", Guillotine.Create);
            add("rocketchair", "Rocket Chair", "Sit a Goredoll in it and press E. 3... 2... 1...", "rocket", RocketChair.Create);
            add("trampoline", "Trampoline", "Bounces everything (and you) high into the air.", "jump", Trampoline.Create);
            add("catapult", "Catapult", "Put something in the bucket and press E to fling it across the map.", "arrow_up", Catapult.Create);
            add("wreckingball", "Wrecking Ball", "A two-tonne ball on a chain, swinging back and forth.", "ball", WreckingBall.Create);
            add("flippers", "Pinball Flippers", "Giant flippers that smack whatever lands on them. Press E (or they flip on their own).", "refresh", Flippers.Create);
            add("windtunnel", "Wind Tunnel", "A huge fan blowing everything up into the air.", "wind", WindTunnel.Create);
            add("magnet", "Magnet Crane", "Press E to switch the electromagnet on or off. Picks up bodies and props.", "magnet", MagnetCrane.Create);
            add("washer", "Washing Machine", "Put a Goredoll in and press E for a 1400 RPM spin cycle.", "refresh", Washer.Create);
            add("microwave", "Microwave", "Put something in, press E. Do not microwave heads.", "tv", Microwave.Create);
            add("cancannon", "Can Cannon", "A broken vending machine that fires cans at 30 m/s. Press E for a burst.", "can", CanCannon.Create);
            add("meathook", "Meat Hook", "A hanging hook on a chain. Bodies that touch it get hooked.", "anchor", MeatHook.Create);
            add("electricchair", "Electric Chair", "Sit a Goredoll in it, press E.", "bolt", ElectricChair.Create);
        }

        // ------------------------------------------------------------------ building helpers

        public static GameObject Root(string name, Vector3 pos, Quaternion rot, bool kinematic = true, float mass = 200f)
        {
            var go = new GameObject(name);
            go.layer = Layers.Props;
            go.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = kinematic; rb.mass = mass;
            go.AddComponent<Surface>().Type = SurfaceType.Metal;
            return go;
        }

        public static GameObject Part(Transform parent, string name, Action<MeshBuilder> build, Material mat = null, bool collide = true, Vector3? at = null)
        {
            var mb = new MeshBuilder { Color = Color.white, Gloss = 0.4f };
            build(mb);
            var go = new GameObject(name);
            go.layer = Layers.Props;
            go.transform.SetParent(parent, false);
            if (at.HasValue) go.transform.localPosition = at.Value;
            var mesh = mb.ToMesh(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat != null ? mat : Mats.LitMetal;
            if (collide) { var bc = go.AddComponent<BoxCollider>(); bc.center = mesh.bounds.center; bc.size = mesh.bounds.size; }
            return go;
        }

        public static TriggerZone Zone(Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Zone");
            go.layer = Layers.Trigger;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var bc = go.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = size;
            return go.AddComponent<TriggerZone>();
        }

        public static bool Authority => !NetState.IsClient;

        public static void Hurt(BodyPart p, float amount, DamageType type, Vector3 dir, object src, string weapon)
            => p.TakeDamage(new DamageInfo { Amount = amount, Type = type, Point = p.transform.position, Direction = dir, Normal = -dir, Force = dir * amount * 0.5f, Source = src, Weapon = weapon });

        public static readonly Color Yellow = new Color(0.95f, 0.75f, 0.1f), Steel = new Color(0.55f, 0.56f, 0.6f), Dark = new Color(0.18f, 0.18f, 0.2f), Red = new Color(0.75f, 0.1f, 0.08f);
    }

    /// <summary>Trigger volume that reports what is inside it.</summary>
    public sealed class TriggerZone : MonoBehaviour
    {
        public readonly HashSet<Rigidbody> Bodies = new HashSet<Rigidbody>();
        public Action<Collider> Enter;
        void OnTriggerEnter(Collider c) { if (c.attachedRigidbody != null) Bodies.Add(c.attachedRigidbody); Enter?.Invoke(c); }
        void OnTriggerExit(Collider c) { if (c.attachedRigidbody != null) Bodies.Remove(c.attachedRigidbody); }
        public IEnumerable<Rigidbody> Live() { Bodies.RemoveWhere(b => b == null); return Bodies; }
        public static BodyPart PartOf(Rigidbody rb) => rb != null ? rb.GetComponent<BodyPart>() : null;
    }

    // ====================================================================== grinders

    public sealed class WoodChipper : MonoBehaviour
    {
        TriggerZone mouth; Transform chute; float nextChew;
        readonly Dictionary<BodyPart, float> inside = new Dictionary<BodyPart, float>();

        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Wood Chipper", pos, rot);
            Machines.Part(go.transform, "Body", mb => { mb.Color = Machines.Yellow; mb.BevelBox(new Vector3(0, 0.6f, 0), new Vector3(1.2f, 0.9f, 1.6f), 0.08f); mb.SetColor(Machines.Dark); mb.Cylinder(new Vector3(-0.7f, 0.35f, -0.4f), new Vector3(0.7f, 0.35f, -0.4f), 0.35f, 0.35f, 14); });
            Machines.Part(go.transform, "Hopper", mb => { mb.Color = Machines.Yellow; mb.Box(new Vector3(0, 1.3f, 0.95f), new Vector3(1.1f, 0.06f, 0.7f)); mb.Box(new Vector3(0.55f, 1.35f, 1.2f), new Vector3(0.06f, 0.5f, 0.9f)); mb.Box(new Vector3(-0.55f, 1.35f, 1.2f), new Vector3(0.06f, 0.5f, 0.9f)); mb.SetColor(Color.black, 0.2f); mb.Box(new Vector3(0, 1.1f, 0.62f), new Vector3(0.9f, 0.4f, 0.05f)); }, null, false);
            var ch = Machines.Part(go.transform, "Chute", mb => { mb.Color = Machines.Yellow; mb.Cylinder(new Vector3(0, 1f, -0.6f), new Vector3(0, 2f, -1.4f), 0.18f, 0.16f, 10); });
            var c = go.AddComponent<WoodChipper>();
            c.chute = new GameObject("ChuteEnd").transform; c.chute.SetParent(go.transform, false); c.chute.localPosition = new Vector3(0, 2.05f, -1.45f);
            c.mouth = Machines.Zone(go.transform, new Vector3(0, 1.3f, 1.05f), new Vector3(0.95f, 0.6f, 0.9f));
            AudioManager.CreateLoop("grind", go.transform, 0.5f, AudioBus.Sfx, 25f);
            return go;
        }

        void FixedUpdate()
        {
            if (!Machines.Authority) return;
            var pull = transform.TransformPoint(new Vector3(0, 0.9f, 0.6f));
            foreach (var rb in mouth.Live())
            {
                if (rb.isKinematic) continue;
                rb.AddForce((pull - rb.worldCenterOfMass).normalized * 40f, ForceMode.Acceleration);
                var part = TriggerZone.PartOf(rb);
                if (part != null && !inside.ContainsKey(part)) inside[part] = Time.time;
                else if (part == null && rb.GetComponent<Spawned>() != null && Time.time > nextChew) { nextChew = Time.time + 0.5f; FX.Chips(rb.position, Vector3.up, new Color(0.6f, 0.45f, 0.3f), 20); Destroy(rb.gameObject); }
            }
            foreach (var part in new List<BodyPart>(inside.Keys))
            {
                if (part == null || part.Gibbed) { inside.Remove(part); continue; }
                if (Time.time - inside[part] < 0.25f) continue;
                inside.Remove(part);
                var d = part.Doll;
                if (d != null && d.Dismember != null) { if (part.Gibbable) d.Dismember.Gib(part, -transform.forward); else Machines.Hurt(part, 120f, DamageType.Crush, Vector3.down, this, "Wood Chipper"); }
                BloodFX.Spurt(chute.position, -transform.forward + Vector3.up * 0.8f, 4f);
                BloodFX.Burst(chute.position, -transform.forward + Vector3.up, 2.5f, new Color(0.5f, 0.02f, 0.02f));
                AudioManager.Play("gib", transform.position + Vector3.up, 1f);
                CameraFX.Shake(0.1f);
            }
        }
    }

    public sealed class Shredder : MonoBehaviour
    {
        TriggerZone zone; Transform r1, r2; float next;

        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Shredder", pos, rot);
            Machines.Part(go.transform, "Box", mb =>
            {
                mb.Color = Machines.Steel;
                mb.Box(new Vector3(0, 0.6f, 1f), new Vector3(2f, 1.2f, 0.1f)); mb.Box(new Vector3(0, 0.6f, -1f), new Vector3(2f, 1.2f, 0.1f));
                mb.Box(new Vector3(1f, 0.6f, 0), new Vector3(0.1f, 1.2f, 2f)); mb.Box(new Vector3(-1f, 0.6f, 0), new Vector3(0.1f, 1.2f, 2f));
                mb.SetColor(Machines.Red); mb.Box(new Vector3(0, 1.22f, 1f), new Vector3(2.05f, 0.05f, 0.12f)); mb.Box(new Vector3(0, 1.22f, -1f), new Vector3(2.05f, 0.05f, 0.12f));
            }, null, false);
            // Walls as separate colliders so things can fall in.
            foreach (var (c, s) in new[] { (new Vector3(0, 0.6f, 1f), new Vector3(2f, 1.2f, 0.1f)), (new Vector3(0, 0.6f, -1f), new Vector3(2f, 1.2f, 0.1f)), (new Vector3(1f, 0.6f, 0), new Vector3(0.1f, 1.2f, 2f)), (new Vector3(-1f, 0.6f, 0), new Vector3(0.1f, 1.2f, 2f)), (new Vector3(0, 0.05f, 0), new Vector3(2f, 0.1f, 2f)) })
            { var bc = go.AddComponent<BoxCollider>(); bc.center = c; bc.size = s; }
            var sh = go.AddComponent<Shredder>();
            sh.r1 = Machines.Part(go.transform, "Roller", Roller, null, false, new Vector3(0, 0.35f, 0.3f)).transform;
            sh.r2 = Machines.Part(go.transform, "Roller", Roller, null, false, new Vector3(0, 0.35f, -0.3f)).transform;
            sh.zone = Machines.Zone(go.transform, new Vector3(0, 0.45f, 0), new Vector3(1.8f, 0.6f, 1.6f));
            AudioManager.CreateLoop("grind", go.transform, 0.6f, AudioBus.Sfx, 25f);
            return go;
        }

        static void Roller(MeshBuilder mb)
        {
            mb.Color = Machines.Dark;
            mb.Cylinder(new Vector3(-0.9f, 0, 0), new Vector3(0.9f, 0, 0), 0.22f, 0.22f, 12);
            mb.SetColor(Machines.Steel, 0.8f);
            for (int i = 0; i < 9; i++) for (int k = 0; k < 6; k++) { var d = Quaternion.Euler(k * 60f, 0, 0) * Vector3.up; var x = -0.8f + i * 0.2f; mb.Cylinder(new Vector3(x, 0, 0) + d * 0.2f, new Vector3(x, 0, 0) + d * 0.32f, 0.03f, 0.005f, 4); }
        }

        void Update()
        {
            if (r1 != null) r1.Rotate(-600f * Time.deltaTime, 0, 0, Space.Self);
            if (r2 != null) r2.Rotate(600f * Time.deltaTime, 0, 0, Space.Self);
            if (!Machines.Authority || Time.time < next) return;
            next = Time.time + 0.18f;
            foreach (var rb in new List<Rigidbody>(zone.Live()))
            {
                var part = TriggerZone.PartOf(rb);
                if (part != null && part.Doll != null && part.Doll.Dismember != null)
                {
                    if (part.Gibbable && !part.Gibbed) part.Doll.Dismember.Gib(part, Vector3.up);
                    else Machines.Hurt(part, 90f, DamageType.Crush, Vector3.up, this, "Shredder");
                    BloodFX.Spurt(transform.position + Vector3.up * 1.2f, Vector3.up + Random.insideUnitSphere * 0.5f, 3f);
                    AudioManager.Play("gib", transform.position + Vector3.up, 0.9f);
                }
                else if (rb.GetComponent<Spawned>() != null) { FX.Chips(rb.position, Vector3.up, new Color(0.5f, 0.45f, 0.4f), 25); Destroy(rb.gameObject); }
            }
        }
    }

    public sealed class Piston : MonoBehaviour
    {
        Rigidbody head; float t; bool slammed;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Piston Crusher", pos, rot);
            Machines.Part(go.transform, "Frame", mb =>
            {
                mb.Color = Machines.Dark;
                mb.Box(new Vector3(-1.1f, 2f, 0), new Vector3(0.25f, 4f, 0.25f)); mb.Box(new Vector3(1.1f, 2f, 0), new Vector3(0.25f, 4f, 0.25f));
                mb.Box(new Vector3(0, 4.1f, 0), new Vector3(2.5f, 0.3f, 0.5f)); mb.SetColor(Machines.Steel); mb.Box(new Vector3(0, 0.08f, 0), new Vector3(2f, 0.16f, 1.6f));
                mb.SetColor(Machines.Yellow); for (int i = 0; i < 6; i++) mb.Box(new Vector3(-0.9f + i * 0.36f, 0.17f, 0.75f), new Vector3(0.18f, 0.01f, 0.1f));
            });
            var h = new GameObject("Ram"); h.layer = Layers.Props; h.transform.SetParent(go.transform, false); h.transform.localPosition = new Vector3(0, 3.2f, 0);
            Machines.Part(h.transform, "RamMesh", mb => { mb.Color = Machines.Red; mb.Box(Vector3.zero, new Vector3(1.8f, 0.5f, 1.4f)); mb.SetColor(Machines.Steel); mb.Cylinder(Vector3.up * 0.25f, Vector3.up * 1.2f, 0.18f, 0.18f, 10); });
            var p = go.AddComponent<Piston>();
            p.head = h.AddComponent<Rigidbody>(); p.head.isKinematic = true;
            return go;
        }

        void FixedUpdate()
        {
            t += Time.fixedDeltaTime;
            float cycle = Mathf.Repeat(t, 4f);
            float y = cycle < 2.6f ? Mathf.Lerp(0.45f, 3.2f, Mathf.Clamp01(cycle / 2.6f)) : Mathf.Lerp(3.2f, 0.45f, Mathf.Clamp01((cycle - 2.6f) / 0.18f));
            head.MovePosition(transform.TransformPoint(new Vector3(0, y, 0)));
            if (cycle > 2.78f && !slammed)
            {
                slammed = true;
                AudioManager.Play("press_slam", transform.position + Vector3.up, 1f);
                CameraFX.Shake(0.15f);
                if (Machines.Authority)
                    foreach (var c in Physics.OverlapBox(transform.TransformPoint(new Vector3(0, 0.5f, 0)), new Vector3(0.9f, 0.35f, 0.7f), transform.rotation, Layers.ShootMask, QueryTriggerInteraction.Ignore))
                    {
                        var part = c.GetComponent<BodyPart>();
                        if (part == null || part.Doll == null) continue;
                        if (part.Gibbable && part.Doll.Dismember != null) part.Doll.Dismember.Gib(part, Vector3.down + Random.insideUnitSphere);
                        else Machines.Hurt(part, 400f, DamageType.Crush, Vector3.down, this, "Piston Crusher");
                        BloodFX.Burst(c.transform.position, Vector3.up + Random.insideUnitSphere * 0.6f, 3f, new Color(0.5f, 0.02f, 0.02f));
                    }
            }
            if (cycle < 2.6f) slammed = false;
        }
    }

    public sealed class Conveyor : MonoBehaviour
    {
        TriggerZone zone; Transform stripes; float off;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Conveyor Belt", pos, rot);
            Machines.Part(go.transform, "Frame", mb => { mb.Color = Machines.Steel; mb.Box(new Vector3(0, 0.45f, 0), new Vector3(1.2f, 0.12f, 5f)); mb.SetColor(Machines.Dark); for (int i = 0; i < 4; i++) { mb.Box(new Vector3(0.55f, 0.2f, -2f + i * 1.33f), new Vector3(0.08f, 0.4f, 0.08f)); mb.Box(new Vector3(-0.55f, 0.2f, -2f + i * 1.33f), new Vector3(0.08f, 0.4f, 0.08f)); } });
            var c = go.AddComponent<Conveyor>();
            c.stripes = Machines.Part(go.transform, "Belt", mb => { mb.Color = new Color(0.12f, 0.12f, 0.13f); for (int i = 0; i < 20; i++) mb.Box(new Vector3(0, 0.52f, -2.4f + i * 0.25f), new Vector3(1.1f, 0.02f, 0.12f)); }, Mats.Lit, false).transform;
            c.zone = Machines.Zone(go.transform, new Vector3(0, 0.8f, 0), new Vector3(1.2f, 0.6f, 5f));
            return go;
        }
        void Update() { off = Mathf.Repeat(off + Time.deltaTime * 1.5f, 0.25f); if (stripes != null) stripes.localPosition = new Vector3(0, 0, off); }
        void FixedUpdate()
        {
            if (!Machines.Authority) return;
            foreach (var rb in zone.Live())
            {
                if (rb.isKinematic) continue;
                var v = rb.velocity; var fwd = transform.forward * 2.2f;
                rb.velocity = new Vector3(Mathf.Lerp(v.x, fwd.x, 0.2f), v.y, Mathf.Lerp(v.z, fwd.z, 0.2f));
            }
        }
    }

    public sealed class SawWall : MonoBehaviour
    {
        readonly List<Transform> saws = new List<Transform>();
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Saw Wall", pos, rot);
            Machines.Part(go.transform, "Wall", mb => { mb.Color = Machines.Dark; mb.Box(new Vector3(0, 1.4f, -0.15f), new Vector3(3.6f, 2.8f, 0.2f)); mb.SetColor(Machines.Yellow); mb.Box(new Vector3(0, 2.85f, -0.15f), new Vector3(3.7f, 0.1f, 0.25f)); });
            var s = go.AddComponent<SawWall>();
            for (int i = 0; i < 3; i++)
            {
                var saw = Machines.Part(go.transform, "Saw", mb =>
                {
                    mb.Color = Machines.Steel; mb.Gloss = 0.9f;
                    mb.Cylinder(new Vector3(0, 0, -0.01f), new Vector3(0, 0, 0.01f), 0.5f, 0.5f, 24);
                    for (int k = 0; k < 16; k++) { var d = Quaternion.Euler(0, 0, k * 22.5f) * Vector3.up; mb.Cylinder(d * 0.49f, d * 0.58f + Quaternion.Euler(0, 0, 90) * d * 0.05f, 0.02f, 0.002f, 3); }
                }, null, false, new Vector3(-1.2f + i * 1.2f, i % 2 == 0 ? 1f : 1.9f, 0.05f));
                var zone = Machines.Zone(saw.transform, Vector3.zero, new Vector3(1.1f, 1.1f, 0.25f));
                zone.Enter = c => s.Cut(c, saw.transform.position);
                s.saws.Add(saw.transform);
            }
            AudioManager.CreateLoop("fan_loop", go.transform, 0.7f, AudioBus.Sfx, 25f);
            return go;
        }
        void Update() { foreach (var t in saws) if (t != null) t.Rotate(0, 0, 1400f * Time.deltaTime, Space.Self); }
        void Cut(Collider c, Vector3 at)
        {
            if (!Machines.Authority) return;
            var part = c.GetComponent<BodyPart>();
            if (part == null || part.Doll == null) { if (c.attachedRigidbody != null) { FX.Sparks(c.ClosestPoint(at), Vector3.up, 12, 6f); AudioManager.Play("metal_impact", at, 0.6f); } return; }
            Machines.Hurt(part, 70f, DamageType.Slash, (part.transform.position - at).normalized, this, "Saw Wall");
            if (part.CanSever && part.Doll.Dismember != null) part.Doll.Dismember.Sever(part, c.ClosestPoint(at), transform.forward, true);
            BloodFX.Spurt(c.ClosestPoint(at), transform.forward + Vector3.up * 0.5f, 2.5f);
            AudioManager.Play("slice", at, 1f);
        }
    }

    // ====================================================================== electric

    public sealed class Tesla : MonoBehaviour
    {
        Transform ball; float next; readonly List<GameObject> bolts = new List<GameObject>(); float boltUntil;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Tesla Coil", pos, rot);
            Machines.Part(go.transform, "Coil", mb =>
            {
                mb.Color = Machines.Dark; mb.Cylinder(Vector3.zero, Vector3.up * 0.4f, 0.6f, 0.5f, 16);
                mb.SetColor(new Color(0.75f, 0.45f, 0.2f), 0.9f); for (int i = 0; i < 22; i++) mb.Torus(Vector3.up * (0.5f + i * 0.1f), 0.22f, 0.025f, 16, 4, Vector3.up);
                mb.SetColor(Machines.Steel, 0.9f); mb.Torus(Vector3.up * 2.85f, 0.5f, 0.12f, 20, 8, Vector3.up);
            });
            var t = go.AddComponent<Tesla>();
            t.ball = Machines.Part(go.transform, "Ball", mb => { mb.Color = Color.white; mb.Sphere(Vector3.zero, Vector3.one * 0.28f, 10, 12); }, Mats.Glow(new Color(0.55f, 0.75f, 1f), 3f), false, new Vector3(0, 3.1f, 0)).transform;
            var l = t.ball.gameObject.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(0.55f, 0.75f, 1f); l.range = 6f; l.intensity = 2f;
            return go;
        }

        void Update()
        {
            if (Time.time > boltUntil) { foreach (var b in bolts) if (b != null) Destroy(b); bolts.Clear(); }
            if (Time.time < next) return;
            next = Time.time + Random.Range(0.6f, 1.3f);
            Vector3? target = null; BodyPart victim = null;
            float best = 7f;
            foreach (var c in Physics.OverlapSphere(ball.position, 7f, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                if (c.transform.IsChildOf(transform)) continue;
                float d = Vector3.Distance(c.transform.position, ball.position);
                if (d < best) { best = d; target = c.ClosestPoint(ball.position); victim = c.GetComponent<BodyPart>(); }
            }
            if (!target.HasValue) { if (Random.value < 0.4f) target = ball.position + Random.onUnitSphere * 1.8f; else return; }
            Bolt(ball.position, target.Value);
            AudioManager.Play("zap", ball.position, 1f, Random.Range(0.85f, 1.15f));
            FX.Flash(target.Value, new Color(0.6f, 0.8f, 1f), 6f, 4f, 0.08f);
            if (victim != null && victim.Doll != null && Machines.Authority)
            {
                Machines.Hurt(victim, 45f, DamageType.Generic, (target.Value - ball.position).normalized, this, "Tesla Coil");
                GoreKit.Burn(victim, 0.3f);
                if (victim.Doll.Ragdoll != null) victim.Doll.Ragdoll.Fall(2f);
                FX.Smoke(target.Value, Vector3.up, 0.4f, new Color(0.2f, 0.2f, 0.2f, 0.7f), 1.5f);
            }
        }

        void Bolt(Vector3 a, Vector3 b)
        {
            boltUntil = Time.time + 0.12f;
            var mat = Mats.Glow(new Color(0.7f, 0.85f, 1f), 6f);
            var prev = a;
            int n = 8;
            for (int i = 1; i <= n; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)n) + (i < n ? Random.insideUnitSphere * 0.25f : Vector3.zero);
                var go = new GameObject("Bolt");
                var mb = new MeshBuilder { Color = Color.white };
                mb.Box(new Vector3(0, 0, 0.5f), Vector3.one);
                go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Bolt");
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                go.transform.SetPositionAndRotation(prev, Quaternion.LookRotation(p - prev));
                go.transform.localScale = new Vector3(0.03f, 0.03f, (p - prev).magnitude);
                bolts.Add(go);
                prev = p;
            }
        }
    }

    public sealed class ElectricChair : MonoBehaviour, IInteractable
    {
        TriggerZone seat; float until, next;
        public string Prompt => until > Time.time ? "" : "Throw the switch";
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Electric Chair", pos, rot);
            Machines.Part(go.transform, "Chair", mb =>
            {
                mb.Color = GunKit.DarkWood; mb.Gloss = 0.3f;
                mb.Box(new Vector3(0, 0.5f, 0), new Vector3(0.7f, 0.1f, 0.7f)); mb.Box(new Vector3(0, 1.1f, -0.33f), new Vector3(0.7f, 1.2f, 0.1f));
                mb.Box(new Vector3(0.35f, 0.7f, 0), new Vector3(0.08f, 0.08f, 0.7f)); mb.Box(new Vector3(-0.35f, 0.7f, 0), new Vector3(0.08f, 0.08f, 0.7f));
                for (int i = 0; i < 4; i++) mb.Box(new Vector3(i < 2 ? 0.3f : -0.3f, 0.25f, i % 2 == 0 ? 0.3f : -0.3f), new Vector3(0.08f, 0.5f, 0.08f));
                mb.SetColor(Machines.Steel, 0.8f); mb.Cylinder(new Vector3(0, 1.85f, -0.1f), new Vector3(0, 2f, -0.1f), 0.14f, 0.12f, 10);
            });
            var c = go.AddComponent<ElectricChair>();
            c.seat = Machines.Zone(go.transform, new Vector3(0, 0.9f, 0.05f), new Vector3(0.7f, 1.0f, 0.7f));
            return go;
        }
        public void Interact(GameObject user) { if (Time.time > until) { until = Time.time + 6f; AudioManager.Play("button", transform.position, 1f); } }
        void Update()
        {
            if (Time.time > until || Time.time < next) return;
            next = Time.time + 0.15f;
            AudioManager.Play("zap", transform.position + Vector3.up, 0.8f, Random.Range(0.8f, 1.2f));
            FX.Sparks(transform.TransformPoint(new Vector3(0, 1.95f, -0.1f)), Vector3.up, 8, 5f);
            FX.Flash(transform.position + Vector3.up * 1.5f, new Color(0.6f, 0.8f, 1f), 6f, 4f, 0.06f);
            if (!Machines.Authority) return;
            foreach (var rb in seat.Live())
            {
                var part = TriggerZone.PartOf(rb);
                if (part == null || part.Doll == null) continue;
                Machines.Hurt(part, 7f, DamageType.Generic, Vector3.up, this, "Electric Chair");
                GoreKit.Burn(part, 0.06f);
                rb.AddForce(Random.insideUnitSphere * 6f, ForceMode.VelocityChange);
                if (part.IsHead && Time.time > until - 0.2f && part.Doll.Dismember != null) part.Doll.Dismember.Gib(part, Vector3.up);
                if (Random.value < 0.3f) FX.Smoke(part.transform.position, Vector3.up, 0.3f, new Color(0.2f, 0.2f, 0.2f, 0.7f), 1.2f);
            }
        }
    }

    // ====================================================================== traps

    public sealed class BearTrap : MonoBehaviour
    {
        Transform jawA, jawB; bool shut; float shutAt;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Bear Trap", pos, rot);
            Machines.Part(go.transform, "Base", mb => { mb.Color = Machines.Dark; mb.Cylinder(Vector3.zero, Vector3.up * 0.04f, 0.28f, 0.28f, 16); mb.SetColor(Machines.Steel); mb.Box(new Vector3(0, 0.05f, 0), new Vector3(0.12f, 0.03f, 0.12f)); });
            var t = go.AddComponent<BearTrap>();
            t.jawA = Machines.Part(go.transform, "Jaw", Jaw, null, false, new Vector3(0, 0.04f, 0)).transform;
            t.jawB = Machines.Part(go.transform, "Jaw", Jaw, null, false, new Vector3(0, 0.04f, 0)).transform;
            t.jawB.localRotation = Quaternion.Euler(0, 180, 0);
            t.jawA.localRotation = Quaternion.Euler(0, 0, 0) * Quaternion.Euler(-80, 0, 0);
            t.jawB.localRotation = Quaternion.Euler(0, 180, 0) * Quaternion.Euler(-80, 0, 0);
            var z = Machines.Zone(go.transform, new Vector3(0, 0.15f, 0), new Vector3(0.4f, 0.3f, 0.4f));
            z.Enter = c => t.Snap(c);
            return go;
        }
        static void Jaw(MeshBuilder mb)
        {
            mb.Color = Machines.Steel; mb.Gloss = 0.8f;
            mb.Torus(new Vector3(0, 0, 0.0f), 0.26f, 0.015f, 16, 4, Vector3.up);
            for (int i = 0; i < 9; i++) { float a = (i - 4) * 18f * Mathf.Deg2Rad; var p = new Vector3(Mathf.Sin(a) * 0.26f, 0, Mathf.Cos(a) * 0.26f); mb.Cylinder(p, p + Vector3.up * 0.07f, 0.015f, 0.002f, 4); }
        }
        void Snap(Collider c)
        {
            if (shut) return;
            var part = c.GetComponent<BodyPart>();
            bool player = c.GetComponentInParent<PlayerController>() != null;
            if (part == null && !player && c.attachedRigidbody == null) return;
            shut = true; shutAt = Time.time;
            AudioManager.Play("metal_heavy", transform.position, 1f, 1.3f);
            AudioManager.Play("bone_crack", transform.position, 1f);
            if (part != null && part.Doll != null && Machines.Authority)
            {
                Machines.Hurt(part, 85f, DamageType.Stab, Vector3.down, this, "Bear Trap");
                BloodFX.Spurt(part.transform.position, Vector3.up, 2f);
                if (part.Body != null && !part.Body.isKinematic) { var j = part.gameObject.AddComponent<FixedJoint>(); j.connectedBody = GetComponent<Rigidbody>(); j.breakForce = 6000f; }
                if (part.Doll.Voice != null) part.Doll.Voice.Say("v_scream", 1f);
            }
            if (player) { var p = c.GetComponentInParent<PlayerController>(); p.Health.TakeDamage(new DamageInfo { Amount = 35f, Type = DamageType.Stab, Point = transform.position, Direction = Vector3.up, Weapon = "Bear Trap" }); }
        }
        void Update()
        {
            float k = shut ? Mathf.Clamp01((Time.time - shutAt) / 0.06f) : 0f;
            jawA.localRotation = Quaternion.Euler(Mathf.Lerp(-80f, -2f, k), 0, 0);
            jawB.localRotation = Quaternion.Euler(0, 180, 0) * Quaternion.Euler(Mathf.Lerp(-80f, -2f, k), 0, 0);
        }
    }

    public sealed class Guillotine : MonoBehaviour, IInteractable
    {
        Transform blade; float dropAt = -1f, resetAt; TriggerZone neck;
        public string Prompt => dropAt < 0f ? "Drop the blade" : "";
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Guillotine", pos, rot);
            Machines.Part(go.transform, "Frame", mb =>
            {
                mb.Color = GunKit.DarkWood; mb.Gloss = 0.3f;
                mb.Box(new Vector3(-0.45f, 1.9f, 0), new Vector3(0.14f, 3.8f, 0.14f)); mb.Box(new Vector3(0.45f, 1.9f, 0), new Vector3(0.14f, 3.8f, 0.14f));
                mb.Box(new Vector3(0, 3.85f, 0), new Vector3(1.1f, 0.16f, 0.2f)); mb.Box(new Vector3(0, 0.3f, 0.9f), new Vector3(0.5f, 0.12f, 1.8f));
                mb.Box(new Vector3(0, 0.55f, 0), new Vector3(0.9f, 0.18f, 0.2f));
            });
            var g = go.AddComponent<Guillotine>();
            g.blade = Machines.Part(go.transform, "Blade", mb => { mb.Color = Machines.Steel; mb.Gloss = 0.95f; mb.Prism(new[] { new Vector2(-0.38f, 0.3f), new Vector2(0.38f, 0.3f), new Vector2(0.38f, -0.05f), new Vector2(-0.38f, -0.3f) }, 0.02f, Vector3.zero); mb.SetColor(Machines.Dark); mb.Box(new Vector3(0, 0.4f, 0), new Vector3(0.8f, 0.2f, 0.06f)); }, null, false, new Vector3(0, 3.3f, 0)).transform;
            g.neck = Machines.Zone(go.transform, new Vector3(0, 0.75f, 0.05f), new Vector3(0.8f, 0.3f, 0.6f));
            return go;
        }
        public void Interact(GameObject user) { if (dropAt < 0f) { dropAt = Time.time; AudioManager.Play("creak", transform.position + Vector3.up * 3f, 0.8f); } }
        void Update()
        {
            if (dropAt < 0f) { blade.localPosition = Vector3.Lerp(blade.localPosition, new Vector3(0, 3.3f, 0), Time.deltaTime * 2f); return; }
            float t = Time.time - dropAt;
            float y = Mathf.Max(0.95f, 3.3f - 0.5f * 9.81f * t * t * 1.4f);
            bool landed = blade.localPosition.y > 0.96f && y <= 0.96f;
            blade.localPosition = new Vector3(0, y, 0);
            if (landed)
            {
                AudioManager.Play("sledge_world", transform.position + Vector3.up, 1f, 1.2f);
                resetAt = Time.time + 2.5f;
                if (Machines.Authority)
                    foreach (var rb in new List<Rigidbody>(neck.Live()))
                    {
                        var part = TriggerZone.PartOf(rb);
                        if (part == null || part.Doll == null || part.Doll.Dismember == null) continue;
                        var target = part.Type == PartType.Neck || part.Type == PartType.Head ? part.Doll.Parts[PartType.Head] : part;
                        if (target.CanSever) part.Doll.Dismember.Sever(target, target.transform.position, Vector3.down, true);
                        BloodFX.Spurt(part.transform.position, Vector3.up + transform.forward * 0.3f, 3f);
                        AudioManager.Play("dismember", part.transform.position, 1f);
                    }
            }
            if (y <= 0.96f && Time.time > resetAt && resetAt > 0f) { dropAt = -1f; resetAt = 0f; }
        }
    }

    public sealed class MeatHook : MonoBehaviour
    {
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Meat Hook", pos + Vector3.up * 3.5f, rot);
            Machines.Part(go.transform, "Mount", mb => { mb.Color = Machines.Dark; mb.Box(Vector3.zero, new Vector3(0.6f, 0.15f, 0.6f)); });
            var hook = new GameObject("Hook"); hook.layer = Layers.Props; hook.transform.SetParent(go.transform, false); hook.transform.localPosition = Vector3.zero;
            Machines.Part(hook.transform, "Chain", mb =>
            {
                mb.Color = Machines.Steel; mb.Gloss = 0.7f;
                for (int i = 0; i < 12; i++) mb.Torus(new Vector3(0, -0.1f - i * 0.1f, 0), 0.035f, 0.01f, 8, 3, i % 2 == 0 ? Vector3.forward : Vector3.right);
                for (int i = 0; i < 8; i++) { float a = i * 22f * Mathf.Deg2Rad; mb.Cylinder(new Vector3(0, -1.3f - Mathf.Sin(a) * 0.15f, Mathf.Cos(a) * 0.15f - 0.15f), new Vector3(0, -1.3f - Mathf.Sin(a + 0.38f) * 0.15f, Mathf.Cos(a + 0.38f) * 0.15f - 0.15f), 0.018f, 0.016f, 6); }
            }, null, false);
            var rb = hook.AddComponent<Rigidbody>(); rb.mass = 8f; rb.angularDrag = 0.4f;
            var j = hook.AddComponent<HingeJoint>(); j.connectedBody = go.GetComponent<Rigidbody>(); j.anchor = Vector3.zero; j.axis = Vector3.right;
            var col = hook.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, -0.7f, 0); col.height = 1.4f; col.radius = 0.05f;
            var z = Machines.Zone(hook.transform, new Vector3(0, -1.45f, -0.15f), new Vector3(0.25f, 0.3f, 0.3f));
            var mh = go.AddComponent<MeatHook>();
            z.Enter = c => mh.Hook(c, rb);
            return go;
        }
        int hooked;
        void Hook(Collider c, Rigidbody hookRb)
        {
            if (!Machines.Authority || hooked >= 1) return;
            var part = c.GetComponent<BodyPart>();
            if (part == null || part.Doll == null || part.Body == null || part.Body.isKinematic) return;
            hooked++;
            var j = part.gameObject.AddComponent<FixedJoint>(); j.connectedBody = hookRb; j.breakForce = 12000f;
            Machines.Hurt(part, 60f, DamageType.Stab, Vector3.up, this, "Meat Hook");
            BloodFX.Spurt(part.transform.position, Vector3.down, 2f);
            AudioManager.Play("stab", part.transform.position, 1f);
            if (part.Doll.Ragdoll != null) part.Doll.Ragdoll.Fall(10f);
        }
    }

    // ====================================================================== launchers

    public sealed class RocketChair : MonoBehaviour, IInteractable
    {
        float lit = -1f; Rigidbody rb; Transform nozzle;
        public string Prompt => lit < 0f ? "Light the rocket" : "";
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Rocket Chair", pos, rot, false, 30f);
            Machines.Part(go.transform, "Chair", mb =>
            {
                mb.Color = Machines.Red; mb.Box(new Vector3(0, 0.5f, 0), new Vector3(0.6f, 0.08f, 0.6f)); mb.Box(new Vector3(0, 0.9f, -0.28f), new Vector3(0.6f, 0.8f, 0.08f));
                for (int i = 0; i < 4; i++) mb.Box(new Vector3(i < 2 ? 0.26f : -0.26f, 0.25f, i % 2 == 0 ? 0.26f : -0.26f), new Vector3(0.05f, 0.5f, 0.05f));
                mb.SetColor(Machines.Steel, 0.8f); mb.Cylinder(new Vector3(0, 0.15f, -0.1f), new Vector3(0, 0.45f, -0.1f), 0.14f, 0.12f, 10);
                mb.SetColor(Machines.Red); mb.Capsule(new Vector3(0, -0.25f, -0.1f), new Vector3(0, 0.15f, -0.1f), 0.12f, 0.12f, 10, 2);
            });
            var c = go.AddComponent<RocketChair>();
            c.rb = go.GetComponent<Rigidbody>();
            c.nozzle = new GameObject("Nozzle").transform; c.nozzle.SetParent(go.transform, false); c.nozzle.localPosition = new Vector3(0, -0.4f, -0.1f);
            return go;
        }
        public void Interact(GameObject user) { if (lit < 0f) { lit = Time.time; AudioManager.Play("hiss", transform.position, 1f); } }
        void FixedUpdate()
        {
            if (lit < 0f) return;
            float t = Time.time - lit;
            if (t < 1.5f) { if (Random.value < 0.5f) FX.Sparks(nozzle.position, Vector3.down, 3, 3f); return; }
            if (t < 4.5f)
            {
                if (Machines.Authority && !rb.isKinematic) rb.AddForce(transform.up * 30f, ForceMode.Acceleration);
                FX.Fire(nozzle.position, -transform.up * 8f + Random.insideUnitSphere, 0.5f);
                FX.Smoke(nozzle.position, -transform.up * 3f + Random.insideUnitSphere, 0.8f, new Color(0.6f, 0.6f, 0.6f, 0.7f), 2f);
                if (Mathf.Repeat(t, 0.5f) < Time.fixedDeltaTime) AudioManager.Play("rocket_loop", transform.position, 1f);
                return;
            }
            if (Machines.Authority) Explosion.Create(transform.position, 5f, 250f, this, 1.5f);
            Destroy(gameObject);
        }
    }

    public sealed class Trampoline : MonoBehaviour
    {
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Trampoline", pos, rot);
            Machines.Part(go.transform, "Frame", mb =>
            {
                mb.Color = new Color(0.2f, 0.4f, 0.9f); mb.Torus(new Vector3(0, 0.7f, 0), 1.6f, 0.08f, 28, 6, Vector3.up);
                mb.SetColor(Color.black, 0.1f); mb.Cylinder(new Vector3(0, 0.66f, 0), new Vector3(0, 0.7f, 0), 1.55f, 1.55f, 28);
                mb.SetColor(Machines.Steel); for (int i = 0; i < 6; i++) { float a = i * 60f * Mathf.Deg2Rad; mb.Cylinder(new Vector3(Mathf.Cos(a) * 1.6f, 0f, Mathf.Sin(a) * 1.6f), new Vector3(Mathf.Cos(a) * 1.6f, 0.7f, Mathf.Sin(a) * 1.6f), 0.04f, 0.04f, 6); }
            }, null, false);
            var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.65f, 0); bc.size = new Vector3(3f, 0.1f, 3f);
            var z = Machines.Zone(go.transform, new Vector3(0, 0.9f, 0), new Vector3(3f, 0.4f, 3f));
            z.Enter = c => Bounce(c, go.transform);
            return go;
        }
        static void Bounce(Collider c, Transform t)
        {
            var p = c.GetComponentInParent<PlayerController>();
            if (p != null) { p.Launch(Vector3.up * 14f); AudioManager.Play("boing", t.position, 1f); return; }
            var rb = c.attachedRigidbody;
            if (rb == null || rb.isKinematic || !Machines.Authority) return;
            var part = rb.GetComponent<BodyPart>();
            if (part != null && part.Doll != null) { part.Doll.Launch(Vector3.up * 11f); }
            else rb.velocity = new Vector3(rb.velocity.x, Mathf.Max(11f, -rb.velocity.y * 1.1f), rb.velocity.z);
            AudioManager.Play("boing", t.position, 0.8f, Random.Range(0.9f, 1.2f));
        }
    }

    public sealed class Catapult : MonoBehaviour, IInteractable
    {
        Transform arm; TriggerZone bucket; float fireAt = -1f;
        public string Prompt => fireAt < 0f ? "Fire the catapult" : "";
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Catapult", pos, rot);
            Machines.Part(go.transform, "Base", mb => { mb.Color = GunKit.Wood; mb.Box(new Vector3(0.6f, 0.2f, 0), new Vector3(0.15f, 0.4f, 3f)); mb.Box(new Vector3(-0.6f, 0.2f, 0), new Vector3(0.15f, 0.4f, 3f)); mb.Box(new Vector3(0, 0.9f, 0.2f), new Vector3(1.4f, 0.15f, 0.15f)); mb.Box(new Vector3(0.6f, 0.6f, 0.2f), new Vector3(0.12f, 0.8f, 0.12f)); mb.Box(new Vector3(-0.6f, 0.6f, 0.2f), new Vector3(0.12f, 0.8f, 0.12f)); });
            var pivot = new GameObject("Arm"); pivot.layer = Layers.Props; pivot.transform.SetParent(go.transform, false); pivot.transform.localPosition = new Vector3(0, 0.9f, 0.2f);
            Machines.Part(pivot.transform, "ArmMesh", mb => { mb.Color = GunKit.DarkWood; mb.Box(new Vector3(0, 0, -1.2f), new Vector3(0.18f, 0.12f, 2.6f)); mb.Box(new Vector3(0, 0.15f, -2.4f), new Vector3(0.9f, 0.08f, 0.9f)); mb.Box(new Vector3(0, 0.3f, -2.85f), new Vector3(0.9f, 0.3f, 0.06f)); mb.Box(new Vector3(0, 0.3f, -1.95f), new Vector3(0.9f, 0.3f, 0.06f)); }, null, true);
            var c = go.AddComponent<Catapult>();
            c.arm = pivot.transform;
            c.arm.localRotation = Quaternion.Euler(-15f, 0, 0);
            c.bucket = Machines.Zone(pivot.transform, new Vector3(0, 0.45f, -2.4f), new Vector3(1f, 0.8f, 1f));
            return go;
        }
        public void Interact(GameObject user) { if (fireAt < 0f) { fireAt = Time.time; AudioManager.Play("creak", transform.position, 1f, 0.8f); } }
        void Update()
        {
            if (fireAt < 0f) { arm.localRotation = Quaternion.Slerp(arm.localRotation, Quaternion.Euler(-15f, 0, 0), Time.deltaTime * 2f); return; }
            float t = Time.time - fireAt;
            float a = Mathf.Lerp(-15f, 95f, Mathf.Clamp01(t / 0.25f));
            arm.localRotation = Quaternion.Euler(a, 0, 0);
            if (t >= 0.2f && t - Time.deltaTime < 0.2f)
            {
                AudioManager.Play("whoosh_big", transform.position + Vector3.up * 2f, 1f);
                if (Machines.Authority)
                    foreach (var rb in new List<Rigidbody>(bucket.Live()))
                    {
                        if (rb.isKinematic) continue;
                        var v = (transform.forward * -1f + Vector3.up * 1.1f).normalized * 24f;
                        var part = rb.GetComponent<BodyPart>();
                        if (part != null && part.Doll != null) part.Doll.Launch(v); else rb.velocity = v;
                    }
            }
            if (t > 3f) fireAt = -1f;
        }
    }

    public sealed class WreckingBall : MonoBehaviour
    {
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Wrecking Ball", pos, rot);
            Machines.Part(go.transform, "Gantry", mb => { mb.Color = Machines.Yellow; mb.Box(new Vector3(-2.5f, 3.5f, 0), new Vector3(0.35f, 7f, 0.35f)); mb.Box(new Vector3(2.5f, 3.5f, 0), new Vector3(0.35f, 7f, 0.35f)); mb.Box(new Vector3(0, 7.1f, 0), new Vector3(5.4f, 0.4f, 0.5f)); });
            var ball = new GameObject("Ball"); ball.layer = Layers.Props; ball.transform.SetParent(go.transform, false); ball.transform.localPosition = new Vector3(0, 7f, 0);
            Machines.Part(ball.transform, "BallMesh", mb => { mb.Color = Machines.Steel; mb.Gloss = 0.7f; for (int i = 0; i < 10; i++) mb.Torus(new Vector3(0, -0.25f - i * 0.45f, 0), 0.08f, 0.025f, 8, 3, i % 2 == 0 ? Vector3.forward : Vector3.right); mb.SetColor(Machines.Dark, 0.6f); mb.Sphere(new Vector3(0, -5f, 0), Vector3.one * 0.75f, 12, 14); }, null, false);
            var col = ball.AddComponent<SphereCollider>(); col.center = new Vector3(0, -5f, 0); col.radius = 0.75f;
            var rb = ball.AddComponent<Rigidbody>(); rb.mass = 2000f; rb.angularDrag = 0.02f; rb.drag = 0f;
            var j = ball.AddComponent<HingeJoint>(); j.connectedBody = go.GetComponent<Rigidbody>(); j.anchor = Vector3.zero; j.axis = Vector3.right;
            rb.angularVelocity = new Vector3(1.4f, 0, 0);
            ball.AddComponent<WreckingBallHit>();
            var wb = go.AddComponent<WreckingBall>();
            wb.body = rb;
            return go;
        }
        Rigidbody body;
        void FixedUpdate() { if (Machines.Authority && body != null && !body.isKinematic && body.angularVelocity.magnitude < 1.2f) body.AddTorque(transform.right * Mathf.Sign(Vector3.Dot(body.angularVelocity, transform.right) + 0.001f) * 3000f, ForceMode.Force); }
    }

    public sealed class WreckingBallHit : MonoBehaviour
    {
        void OnCollisionEnter(Collision c)
        {
            if (!Machines.Authority) return;
            float v = c.relativeVelocity.magnitude;
            var part = c.collider.GetComponent<BodyPart>();
            if (part == null || part.Doll == null || v < 3f) return;
            Machines.Hurt(part, v * 45f, DamageType.Blunt, -c.relativeVelocity.normalized, this, "Wrecking Ball");
            if (v > 6f && part.Gibbable && part.Doll.Dismember != null && Random.value < 0.6f) part.Doll.Dismember.Gib(part, -c.relativeVelocity.normalized);
            part.Doll.Launch(-c.relativeVelocity.normalized * v * 0.9f + Vector3.up * 3f);
            AudioManager.Play("sledge_flesh", c.GetContact(0).point, 1f, 0.7f);
        }
    }

    public sealed class Flippers : MonoBehaviour, IInteractable
    {
        Rigidbody left, right; float flipAt = -10f, next;
        public string Prompt => "Flip";
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Pinball Flippers", pos, rot);
            Machines.Part(go.transform, "Base", mb => { mb.Color = new Color(0.1f, 0.1f, 0.3f); mb.Box(new Vector3(0, 0.05f, 0), new Vector3(4.4f, 0.1f, 2.4f)); });
            var f = go.AddComponent<Flippers>();
            f.left = Flipper(go.transform, new Vector3(-1.9f, 0.35f, 0), 1);
            f.right = Flipper(go.transform, new Vector3(1.9f, 0.35f, 0), -1);
            return go;
        }
        static Rigidbody Flipper(Transform parent, Vector3 at, int side)
        {
            var p = new GameObject("Flipper"); p.layer = Layers.Props; p.transform.SetParent(parent, false); p.transform.localPosition = at;
            Machines.Part(p.transform, "Paddle", mb => { mb.Color = Machines.Red; mb.Gloss = 0.8f; mb.Capsule(Vector3.zero, new Vector3(side * 1.6f, 0, 0.2f), 0.22f, 0.12f, 12, 3); mb.SetColor(Color.white); mb.Cylinder(Vector3.down * 0.25f, Vector3.up * 0.25f, 0.1f, 0.1f, 8); });
            var rb = p.AddComponent<Rigidbody>(); rb.isKinematic = true;
            return rb;
        }
        public void Interact(GameObject user) => flipAt = Time.time;
        void FixedUpdate()
        {
            if (Time.time > next) { next = Time.time + Random.Range(2.5f, 4.5f); flipAt = Time.time; }
            float t = Time.time - flipAt;
            float a = t < 0.12f ? Mathf.Lerp(-25f, 35f, t / 0.12f) : Mathf.Lerp(35f, -25f, Mathf.Clamp01((t - 0.3f) / 0.4f));
            left.MoveRotation(transform.rotation * Quaternion.Euler(0, -a, 0));
            right.MoveRotation(transform.rotation * Quaternion.Euler(0, a, 0));
            if (t < Time.fixedDeltaTime * 1.5f)
            {
                AudioManager.Play("button", transform.position, 1f, 0.6f);
                if (!Machines.Authority) return;
                foreach (var c in Physics.OverlapBox(transform.TransformPoint(new Vector3(0, 0.6f, 0.5f)), new Vector3(2.2f, 0.6f, 1.2f), transform.rotation, Layers.ShootMask, QueryTriggerInteraction.Ignore))
                {
                    var rb = c.attachedRigidbody;
                    if (rb == null || rb.isKinematic || rb.transform.IsChildOf(transform)) continue;
                    var v = (transform.forward + Vector3.up * 0.7f).normalized * 16f;
                    var part = rb.GetComponent<BodyPart>();
                    if (part != null && part.Doll != null) part.Doll.Launch(v); else rb.velocity = v;
                }
            }
        }
    }

    public sealed class WindTunnel : MonoBehaviour
    {
        TriggerZone zone; Transform rotor;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Wind Tunnel", pos, rot);
            Machines.Part(go.transform, "Housing", mb => { mb.Color = Machines.Dark; mb.Torus(new Vector3(0, 0.4f, 0), 1.6f, 0.15f, 28, 6, Vector3.up); mb.Cylinder(Vector3.zero, new Vector3(0, 0.3f, 0), 1.75f, 1.75f, 28); mb.SetColor(Machines.Yellow); mb.Torus(new Vector3(0, 0.5f, 0), 1.6f, 0.04f, 28, 4, Vector3.up); });
            var w = go.AddComponent<WindTunnel>();
            w.rotor = Machines.Part(go.transform, "Rotor", mb => { mb.Color = Machines.Steel; for (int i = 0; i < 5; i++) { mb.Push(Vector3.zero, Quaternion.Euler(0, i * 72f, 0)); mb.Push(new Vector3(0, 0, 0.7f), Quaternion.Euler(0, 0, 25f)); mb.Box(Vector3.zero, new Vector3(0.35f, 0.03f, 1.3f)); mb.Pop(); mb.Pop(); } }, null, false, new Vector3(0, 0.45f, 0)).transform;
            w.zone = Machines.Zone(go.transform, new Vector3(0, 5f, 0), new Vector3(3f, 9f, 3f));
            AudioManager.CreateLoop("fan_loop", go.transform, 1f, AudioBus.Sfx, 30f);
            return go;
        }
        void Update() { if (rotor != null) rotor.Rotate(0, 900f * Time.deltaTime, 0, Space.Self); }
        void FixedUpdate()
        {
            var p = PlayerController.Instance;
            if (p != null && p.gameObject.activeInHierarchy)
            {
                var local = transform.InverseTransformPoint(p.transform.position);
                if (Mathf.Abs(local.x) < 1.5f && Mathf.Abs(local.z) < 1.5f && local.y > -0.5f && local.y < 9f && Time.frameCount % 6 == 0) p.Launch(transform.up * 8f);
            }
            if (!Machines.Authority) return;
            foreach (var rb in zone.Live()) if (!rb.isKinematic) rb.AddForce(transform.up * 22f, ForceMode.Acceleration);
        }
    }

    public sealed class MagnetCrane : MonoBehaviour, IInteractable
    {
        Transform magnet; bool on; Light glow;
        public string Prompt => on ? "Magnet OFF" : "Magnet ON";
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Magnet Crane", pos, rot);
            Machines.Part(go.transform, "Crane", mb => { mb.Color = Machines.Yellow; mb.Box(new Vector3(0, 3f, -2f), new Vector3(0.5f, 6f, 0.5f)); mb.Box(new Vector3(0, 6.1f, 0), new Vector3(0.4f, 0.4f, 4.6f)); mb.SetColor(Machines.Dark); mb.Box(new Vector3(0, 0.15f, -2f), new Vector3(2f, 0.3f, 2f)); mb.Cylinder(new Vector3(0, 6f, 2f), new Vector3(0, 4.6f, 2f), 0.02f, 0.02f, 4); });
            var m = go.AddComponent<MagnetCrane>();
            m.magnet = Machines.Part(go.transform, "Magnet", mb => { mb.Color = Machines.Red; mb.Cylinder(Vector3.zero, Vector3.up * 0.3f, 0.7f, 0.7f, 18); mb.SetColor(Machines.Steel); mb.Cylinder(Vector3.down * 0.02f, Vector3.zero, 0.65f, 0.65f, 18); }, null, true, new Vector3(0, 4.3f, 2f)).transform;
            m.glow = m.magnet.gameObject.AddComponent<Light>(); m.glow.type = LightType.Point; m.glow.color = new Color(0.6f, 0.7f, 1f); m.glow.range = 4f; m.glow.intensity = 0f;
            return go;
        }
        public void Interact(GameObject user) { on = !on; AudioManager.Play(on ? "zap" : "button", magnet.position, 1f); }
        void FixedUpdate()
        {
            if (glow != null) glow.intensity = on ? 1.5f : 0f;
            if (!on || !Machines.Authority) return;
            var c = magnet.position + Vector3.down * 0.4f;
            foreach (var col in Physics.OverlapSphere(c + Vector3.down * 2f, 3f, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                var rb = col.attachedRigidbody;
                if (rb == null || rb.isKinematic || rb.transform.IsChildOf(transform)) continue;
                var d = c - rb.worldCenterOfMass;
                rb.AddForce(d.normalized * Mathf.Clamp(60f / Mathf.Max(0.3f, d.magnitude), 4f, 40f), ForceMode.Acceleration);
                if (d.magnitude < 0.6f) rb.velocity *= 0.8f;
            }
        }
    }

    // ====================================================================== household

    public sealed class Washer : MonoBehaviour, IInteractable
    {
        TriggerZone drum; float until, next; Transform door;
        public string Prompt => until > Time.time ? "" : "Start the spin cycle";
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Washing Machine", pos, rot);
            Machines.Part(go.transform, "Body", mb =>
            {
                mb.Color = new Color(0.95f, 0.95f, 0.96f);
                mb.Box(new Vector3(0, 0.05f, 0), new Vector3(0.9f, 0.1f, 0.9f)); mb.Box(new Vector3(0, 1.75f, 0), new Vector3(0.9f, 0.1f, 0.9f));
                mb.Box(new Vector3(0.43f, 0.9f, 0), new Vector3(0.05f, 1.7f, 0.9f)); mb.Box(new Vector3(-0.43f, 0.9f, 0), new Vector3(0.05f, 1.7f, 0.9f)); mb.Box(new Vector3(0, 0.9f, -0.43f), new Vector3(0.9f, 1.7f, 0.05f));
                mb.SetColor(Machines.Dark); mb.Box(new Vector3(0.25f, 1.6f, 0.44f), new Vector3(0.3f, 0.12f, 0.02f));
            }, null, false);
            foreach (var (c, s) in new[] { (new Vector3(0, 0.05f, 0), new Vector3(0.9f, 0.1f, 0.9f)), (new Vector3(0, 1.75f, 0), new Vector3(0.9f, 0.1f, 0.9f)), (new Vector3(0.43f, 0.9f, 0), new Vector3(0.05f, 1.7f, 0.9f)), (new Vector3(-0.43f, 0.9f, 0), new Vector3(0.05f, 1.7f, 0.9f)), (new Vector3(0, 0.9f, -0.43f), new Vector3(0.9f, 1.7f, 0.05f)) })
            { var bc = go.AddComponent<BoxCollider>(); bc.center = c; bc.size = s; }
            var w = go.AddComponent<Washer>();
            w.drum = Machines.Zone(go.transform, new Vector3(0, 0.9f, 0), new Vector3(0.8f, 1.5f, 0.8f));
            w.door = Machines.Part(go.transform, "Door", mb => { mb.Color = Machines.Steel; mb.Torus(Vector3.zero, 0.3f, 0.05f, 20, 5, Vector3.forward); }, null, false, new Vector3(0, 0.9f, 0.46f)).transform;
            return go;
        }
        public void Interact(GameObject user) { if (Time.time > until) { until = Time.time + 8f; AudioManager.Play("button", transform.position, 1f); } }
        void FixedUpdate()
        {
            bool spinning = Time.time < until;
            if (door != null) door.localPosition = new Vector3(0, 0.9f, 0.46f) + (spinning ? Random.insideUnitSphere * 0.01f : Vector3.zero);
            if (!spinning) return;
            if (Time.time > next) { next = Time.time + 0.8f; AudioManager.Play("fan_loop", transform.position, 0.8f, 1.6f); }
            if (!Machines.Authority) return;
            var c = transform.TransformPoint(new Vector3(0, 0.9f, 0));
            foreach (var rb in drum.Live())
            {
                if (rb.isKinematic) continue;
                var r = rb.worldCenterOfMass - c;
                var tangent = Vector3.Cross(transform.forward, r).normalized;
                rb.velocity = tangent * 9f - r * 2f;
                var part = rb.GetComponent<BodyPart>();
                if (part != null && Random.value < 0.05f) { Machines.Hurt(part, 12f, DamageType.Blunt, tangent, this, "Washing Machine"); if (Random.value < 0.3f) BloodFX.Mist(rb.position, tangent, 0.6f); }
            }
        }
    }

    public sealed class Microwave : MonoBehaviour, IInteractable
    {
        TriggerZone inside; float until = -1f; Light glow;
        public string Prompt => until > 0f ? "" : "Microwave it (30 s... well, 4 s)";
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Microwave", pos, rot, false, 15f);
            Machines.Part(go.transform, "Body", mb => { mb.Color = new Color(0.9f, 0.9f, 0.9f); mb.Box(new Vector3(0, 0.02f, 0), new Vector3(0.6f, 0.04f, 0.45f)); mb.Box(new Vector3(0, 0.38f, 0), new Vector3(0.6f, 0.04f, 0.45f)); mb.Box(new Vector3(0.28f, 0.2f, 0), new Vector3(0.04f, 0.36f, 0.45f)); mb.Box(new Vector3(-0.28f, 0.2f, 0), new Vector3(0.04f, 0.36f, 0.45f)); mb.Box(new Vector3(0, 0.2f, -0.21f), new Vector3(0.6f, 0.36f, 0.04f)); mb.SetColor(Machines.Dark); mb.Box(new Vector3(0.2f, 0.2f, 0.225f), new Vector3(0.14f, 0.3f, 0.01f)); }, null, false);
            var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.02f, 0); bc.size = new Vector3(0.6f, 0.04f, 0.45f);
            var m = go.AddComponent<Microwave>();
            m.inside = Machines.Zone(go.transform, new Vector3(-0.05f, 0.2f, 0), new Vector3(0.45f, 0.32f, 0.38f));
            m.glow = new GameObject("Glow").AddComponent<Light>(); m.glow.transform.SetParent(go.transform, false); m.glow.transform.localPosition = new Vector3(0, 0.2f, 0); m.glow.type = LightType.Point; m.glow.color = new Color(1f, 0.85f, 0.5f); m.glow.range = 1.2f; m.glow.intensity = 0f;
            return go;
        }
        public void Interact(GameObject user) { if (until < 0f) { until = Time.time + 4f; AudioManager.Play("button", transform.position, 1f); } }
        void Update()
        {
            if (until < 0f) { glow.intensity = 0f; return; }
            glow.intensity = 1.5f;
            if (Random.value < 0.05f) FX.Sparks(transform.TransformPoint(new Vector3(0, 0.3f, 0)), Vector3.up, 3, 2f);
            if (Time.time < until) return;
            until = -1f;
            AudioManager.Play("chime", transform.position, 1f);
            if (!Machines.Authority) return;
            bool any = false;
            foreach (var rb in new List<Rigidbody>(inside.Live()))
            {
                var part = TriggerZone.PartOf(rb);
                if (part != null && part.Doll != null && part.Doll.Dismember != null) { if (part.Gibbable) part.Doll.Dismember.Gib(part, Vector3.up); any = true; }
                else if (rb != GetComponent<Rigidbody>()) { any = true; }
            }
            if (any) Explosion.Create(transform.position + Vector3.up * 0.2f, 1.5f, 60f, this, 0.8f);
        }
    }

    public sealed class CanCannon : MonoBehaviour, IInteractable
    {
        int burst; float next;
        public string Prompt => "Fire cans";
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Can Cannon", pos, rot);
            Machines.Part(go.transform, "Machine", mb => { mb.Color = Machines.Red; mb.BevelBox(new Vector3(0, 0.95f, 0), new Vector3(1f, 1.9f, 0.8f), 0.05f); mb.SetColor(new Color(0.2f, 0.25f, 0.3f), 0.9f); mb.Box(new Vector3(-0.15f, 1.15f, 0.41f), new Vector3(0.55f, 1.1f, 0.02f)); mb.SetColor(Color.black); mb.Box(new Vector3(0.3f, 0.35f, 0.41f), new Vector3(0.25f, 0.15f, 0.04f)); });
            go.AddComponent<CanCannon>();
            return go;
        }
        public void Interact(GameObject user) { burst = 12; }
        void Update()
        {
            if (burst <= 0 || Time.time < next) return;
            next = Time.time + 0.12f;
            burst--;
            var at = transform.TransformPoint(new Vector3(0.3f, 0.4f, 0.55f));
            AudioManager.Play("thump_light", at, 0.8f);
            if (!Machines.Authority) return;
            var can = new GameObject("Can"); can.layer = Layers.Props;
            can.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, false);
            can.transform.SetPositionAndRotation(at, Random.rotation);
            var mb = new MeshBuilder { Color = new Color(Random.value, Random.value * 0.5f, 0.1f), Gloss = 0.8f };
            mb.Cylinder(Vector3.down * 0.06f, Vector3.up * 0.06f, 0.033f, 0.033f, 10);
            can.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Can");
            can.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            var cc = can.AddComponent<CapsuleCollider>(); cc.radius = 0.033f; cc.height = 0.12f;
            var rb = can.AddComponent<Rigidbody>(); rb.mass = 0.35f; rb.velocity = (transform.forward + Random.insideUnitSphere * 0.08f).normalized * 30f; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            can.AddComponent<CanHit>();
            Debris.Register(can, 20f);
        }
    }

    public sealed class CanHit : MonoBehaviour
    {
        bool done;
        void OnCollisionEnter(Collision c)
        {
            if (done || !Machines.Authority) return;
            var part = c.collider.GetComponent<BodyPart>();
            if (part == null || c.relativeVelocity.magnitude < 8f) return;
            done = true;
            Machines.Hurt(part, c.relativeVelocity.magnitude * 1.4f, DamageType.Blunt, -c.relativeVelocity.normalized, this, "Can");
            AudioManager.Play("pan_bonk", c.GetContact(0).point, 0.6f, 1.6f);
        }
    }
}
