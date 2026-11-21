using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 traps and props, part 2: lava pool, stocks, rack, trapdoor, furnace, freezer, claymore mine,
    /// tripwire grenade, bouncy castle, ball pit, bowling set, basketball hoop, football goal, traffic light,
    /// breakable street lamp, prop spawner.
    /// </summary>
    public static class Traps3
    {
        public static void Register(Action<string, string, string, string, Func<Vector3, Quaternion, GameObject>> add)
        {
            add("lavapool", "Lava Pool", "Molten rock. Sets anything that touches it on fire and burns it to the bone.", "fire", LavaPool.Create);
            add("stocks", "Stocks", "Wooden pillory. Put a Goredoll's head and hands in and press E to lock it.", "warning", Stocks.Create);
            add("rack", "Rack", "A medieval stretching rack. Lay a Goredoll on it and press E to turn the wheel.", "warning", Rack.Create);
            add("trapdoor", "Trapdoor", "Press E to drop the floor out from under whoever's standing on it.", "arrow_down", Trapdoor.Create);
            add("furnace", "Furnace", "An industrial furnace. Anything thrown in burns.", "fire", Furnace.Create);
            add("freezer", "Freezer", "A walk-in freezer box. Goredolls inside freeze solid.", "snow", Freezer.Create);
            add("claymore", "Claymore Mine", "Directional mine with a tripwire laser. Faces the way you placed it.", "explosion", Claymore.Create);
            add("tripwire", "Tripwire Grenade", "A wire across the floor tied to a grenade.", "explosion", Tripwire.Create);
            add("bouncycastle", "Bouncy Castle", "An inflatable castle. Everything bounces.", "jump", BouncyCastle.Create);
            add("ballpit", "Ball Pit", "A pit full of plastic balls.", "globe", BallPit.Create);
            add("bowlingset", "Bowling Set", "Ten pins in a triangle and a heavy ball.", "flag", BowlingSet.Create);
            add("hoop", "Basketball Hoop", "A hoop on a pole, with a ball.", "ball", Hoop.Create);
            add("goal", "Football Goal", "A goal with a net, and a ball.", "ball", Goal.Create);
            add("trafficlight", "Traffic Light", "A working traffic light.", "warning", TrafficLight.Create);
            add("streetlamp", "Street Lamp", "A tall lamp that lights up the night. Shoot the bulb out.", "bulb", StreetLampProp.Create);
            add("propspawner", "Prop Spawner", "Spits out a prop every few seconds. Press E to change which prop.", "plus", PropSpawner.Create);
            Traps4.Register(add); // part 3 (World/Traps4.cs)
        }

        internal static readonly Color Wood = new Color(0.45f, 0.3f, 0.18f);
    }

    public sealed class LavaPool : MonoBehaviour
    {
        TriggerZone zone; readonly Dictionary<BodyPart, float> hit = new Dictionary<BodyPart, float>(); float nextFx;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Lava Pool", pos, rot);
            Machines.Part(go.transform, "Rim", mb =>
            {
                mb.Color = new Color(0.15f, 0.12f, 0.11f);
                for (int i = 0; i < 12; i++) { float a = i * 30f * Mathf.Deg2Rad; mb.BevelBox(new Vector3(Mathf.Cos(a) * 1.7f, 0.2f, Mathf.Sin(a) * 1.7f), new Vector3(0.7f, 0.4f, 0.5f), 0.08f); }
            });
            Machines.Part(go.transform, "Lava", mb => { mb.Color = Color.white; mb.Cylinder(new Vector3(0, 0.18f, 0), new Vector3(0, 0.22f, 0), 1.6f, 1.6f, 24); }, Mats.Glow(new Color(1f, 0.35f, 0.05f), 3f), false);
            var l = go.AddComponent<LavaPool>();
            l.zone = Machines.Zone(go.transform, new Vector3(0, 0.3f, 0), new Vector3(3.1f, 0.6f, 3.1f));
            var lg = new GameObject("Glow").AddComponent<Light>(); lg.transform.SetParent(go.transform, false); lg.transform.localPosition = Vector3.up;
            lg.type = LightType.Point; lg.color = new Color(1f, 0.4f, 0.1f); lg.range = 7f; lg.intensity = 2f;
            return go;
        }
        void Update()
        {
            if (Time.time > nextFx) { nextFx = Time.time + 0.2f; FX.Fire(transform.TransformPoint(new Vector3(Random.Range(-1.3f, 1.3f), 0.25f, Random.Range(-1.3f, 1.3f))), Vector3.up * 1.5f, 0.25f); }
            if (!Machines.Authority) return;
            foreach (var rb in zone.Live())
            {
                RB.SetVel(rb, RB.Vel(rb) * 0.85f);
                rb.AddForce(Vector3.down * 4f, ForceMode.Acceleration); // sinks
                var p = TriggerZone.PartOf(rb);
                if (p == null || p.Doll == null || !Traps2.Cool(hit, p, 0.4f)) continue;
                Machines.Hurt(p, 18f, DamageType.Explosion, Vector3.up, this, "Lava");
                GoreKit.Burn(p, 0.5f);
                Burning.Apply(p.Doll, 15f, this);
            }
        }
    }

    public sealed class Stocks : MonoBehaviour, IInteractable
    {
        TriggerZone zone; bool locked; readonly List<FixedJoint> joints = new List<FixedJoint>(); Transform bar;
        public string Prompt => locked ? "Unlock the stocks" : "Lock the stocks";
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Stocks", pos, rot);
            Machines.Part(go.transform, "Posts", mb => { mb.Color = Traps3.Wood; mb.Box(new Vector3(-0.7f, 0.75f, 0), new Vector3(0.15f, 1.5f, 0.15f)); mb.Box(new Vector3(0.7f, 0.75f, 0), new Vector3(0.15f, 1.5f, 0.15f)); mb.Box(new Vector3(0, 1.1f, 0), new Vector3(1.55f, 0.18f, 0.12f)); });
            var s = go.AddComponent<Stocks>();
            s.bar = Machines.Part(go.transform, "Top Bar", mb => { mb.Color = Traps3.Wood; mb.Box(Vector3.zero, new Vector3(1.55f, 0.18f, 0.12f)); }, null, true, new Vector3(0, 1.6f, 0)).transform;
            s.zone = Machines.Zone(go.transform, new Vector3(0, 1.25f, 0), new Vector3(1.4f, 0.35f, 0.5f));
            return go;
        }
        public void Interact(GameObject user)
        {
            locked = !locked;
            AudioManager.Play("creak", transform.position + Vector3.up, 0.8f);
            foreach (var j in joints) if (j != null) Destroy(j);
            joints.Clear();
            if (!locked || !Machines.Authority) return;
            var root = GetComponent<Rigidbody>();
            foreach (var rb in zone.Live())
            {
                var p = TriggerZone.PartOf(rb);
                if (p == null || (p.Type != PartType.Neck && p.Type != PartType.Head && p.Type != PartType.HandL && p.Type != PartType.HandR && p.Type != PartType.ForearmL && p.Type != PartType.ForearmR)) continue;
                var j = rb.gameObject.AddComponent<FixedJoint>(); j.connectedBody = root; joints.Add(j);
                if (p.Doll.Vitals.Conscious) p.Doll.Voice.Say("v_no");
            }
        }
        void Update() { bar.localPosition = Vector3.Lerp(bar.localPosition, new Vector3(0, locked ? 1.38f : 1.6f, 0), Time.deltaTime * 8f); }
    }

    public sealed class Rack : MonoBehaviour, IInteractable
    {
        TriggerZone head, feet; float turnUntil; Transform wheel; readonly Dictionary<BodyPart, float> hit = new Dictionary<BodyPart, float>();
        public string Prompt => "Turn the wheel";
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Rack", pos, rot);
            Machines.Part(go.transform, "Frame", mb =>
            {
                mb.Color = Traps3.Wood;
                foreach (var x in new[] { -0.4f, 0.4f }) foreach (var z in new[] { -1.1f, 1.1f }) mb.Box(new Vector3(x, 0.4f, z), new Vector3(0.12f, 0.8f, 0.12f));
                mb.Box(new Vector3(-0.4f, 0.8f, 0), new Vector3(0.12f, 0.12f, 2.4f)); mb.Box(new Vector3(0.4f, 0.8f, 0), new Vector3(0.12f, 0.12f, 2.4f));
                mb.SetColor(Traps3.Wood * 0.8f); mb.Box(new Vector3(0, 0.75f, 0), new Vector3(0.7f, 0.05f, 2.2f));
            });
            var r = go.AddComponent<Rack>();
            r.wheel = Machines.Part(go.transform, "Wheel", mb => { mb.Color = Traps3.Wood; mb.Torus(Vector3.zero, 0.3f, 0.03f, 16, 4, Vector3.right); for (int i = 0; i < 4; i++) mb.Box(Vector3.zero, Quaternion.Euler(i * 45f, 0, 0) * new Vector3(0.04f, 0.6f, 0.04f)); }, null, false, new Vector3(0.55f, 0.9f, 1.1f)).transform;
            r.head = Machines.Zone(go.transform, new Vector3(0, 0.95f, 1.0f), new Vector3(0.7f, 0.4f, 0.5f));
            r.feet = Machines.Zone(go.transform, new Vector3(0, 0.95f, -1.0f), new Vector3(0.7f, 0.4f, 0.5f));
            return go;
        }
        public void Interact(GameObject user) { turnUntil = Time.time + 3f; AudioManager.Play("creak", transform.position, 1f, 0.7f); }
        void FixedUpdate()
        {
            if (Time.time > turnUntil) return;
            wheel.Rotate(120f * Time.fixedDeltaTime, 0, 0, Space.Self);
            if (!Machines.Authority) return;
            foreach (var rb in head.Live()) rb.AddForce(transform.forward * 60f, ForceMode.Acceleration);
            foreach (var rb in feet.Live())
            {
                rb.AddForce(-transform.forward * 60f, ForceMode.Acceleration);
                var p = TriggerZone.PartOf(rb);
                if (p != null && p.Doll != null && Traps2.Cool(hit, p, 0.8f)) { Machines.Hurt(p, 10f, DamageType.Blunt, -transform.forward, this, "Rack"); AudioManager.Play("bone_crack", rb.position, 0.5f); if (p.Doll.Vitals.Conscious) p.Doll.Voice.Say("v_scream"); }
            }
        }
    }

    public sealed class Trapdoor : MonoBehaviour, IInteractable
    {
        Transform leafL, leafR; bool open; float closeAt;
        public string Prompt => open ? "" : "Open the trapdoor";
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Trapdoor", pos, rot);
            Machines.Part(go.transform, "Frame", mb => { mb.Color = Machines.Dark; mb.Box(new Vector3(0, 0.05f, 1.05f), new Vector3(2.2f, 0.1f, 0.1f)); mb.Box(new Vector3(0, 0.05f, -1.05f), new Vector3(2.2f, 0.1f, 0.1f)); mb.Box(new Vector3(1.05f, 0.05f, 0), new Vector3(0.1f, 0.1f, 2.2f)); mb.Box(new Vector3(-1.05f, 0.05f, 0), new Vector3(0.1f, 0.1f, 2.2f)); }, null, false);
            var t = go.AddComponent<Trapdoor>();
            foreach (var side in new[] { -1f, 1f })
            {
                var h = new GameObject("Hinge").transform; h.SetParent(go.transform, false); h.localPosition = new Vector3(side * 1f, 0.08f, 0);
                Machines.Part(h, "Leaf", mb => { mb.Color = Traps3.Wood; mb.Box(new Vector3(-side * 0.5f, 0, 0), new Vector3(1f, 0.06f, 2f)); });
                if (side < 0) t.leafL = h; else t.leafR = h;
            }
            return go;
        }
        public void Interact(GameObject user) { if (open) return; open = true; closeAt = Time.time + 4f; AudioManager.Play("creak", transform.position, 1f, 0.8f); }
        void Update()
        {
            if (open && Time.time > closeAt) open = false;
            float a = open ? 90f : 0f;
            leafL.localRotation = Quaternion.Slerp(leafL.localRotation, Quaternion.Euler(0, 0, a), Time.deltaTime * (open ? 14f : 3f));
            leafR.localRotation = Quaternion.Slerp(leafR.localRotation, Quaternion.Euler(0, 0, -a), Time.deltaTime * (open ? 14f : 3f));
        }
    }

    public sealed class Furnace : MonoBehaviour
    {
        TriggerZone mouth; float nextFx; readonly Dictionary<BodyPart, float> hit = new Dictionary<BodyPart, float>();
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Furnace", pos, rot);
            Machines.Part(go.transform, "Body", mb =>
            {
                mb.Color = new Color(0.25f, 0.22f, 0.2f);
                mb.Box(new Vector3(0, 1.4f, -0.6f), new Vector3(2.2f, 2.8f, 0.3f)); mb.Box(new Vector3(-1f, 1.4f, 0.2f), new Vector3(0.3f, 2.8f, 1.6f)); mb.Box(new Vector3(1f, 1.4f, 0.2f), new Vector3(0.3f, 2.8f, 1.6f));
                mb.Box(new Vector3(0, 2.65f, 0.2f), new Vector3(2.2f, 0.3f, 1.6f)); mb.Box(new Vector3(0, 0.1f, 0.2f), new Vector3(2.2f, 0.2f, 1.6f));
                mb.SetColor(Machines.Dark); mb.Cylinder(new Vector3(0, 2.8f, -0.3f), new Vector3(0, 4.5f, -0.3f), 0.25f, 0.22f, 12);
            });
            Machines.Part(go.transform, "Coals", mb => { mb.Color = Color.white; mb.Box(new Vector3(0, 0.25f, 0.1f), new Vector3(1.6f, 0.1f, 1.2f)); }, Mats.Glow(new Color(1f, 0.4f, 0.1f), 3f), false);
            var f = go.AddComponent<Furnace>();
            f.mouth = Machines.Zone(go.transform, new Vector3(0, 1.2f, 0.2f), new Vector3(1.6f, 2f, 1.3f));
            var l = new GameObject("Glow").AddComponent<Light>(); l.transform.SetParent(go.transform, false); l.transform.localPosition = new Vector3(0, 1f, 1.2f);
            l.type = LightType.Point; l.color = new Color(1f, 0.45f, 0.1f); l.range = 6f; l.intensity = 2.5f;
            AudioManager.CreateLoop("flamer", go.transform, 0.25f, AudioBus.Sfx, 20f);
            return go;
        }
        void Update()
        {
            if (Time.time > nextFx) { nextFx = Time.time + 0.1f; FX.Fire(transform.TransformPoint(new Vector3(Random.Range(-0.6f, 0.6f), 0.4f, Random.Range(-0.3f, 0.6f))), Vector3.up * 2f, 0.35f); }
            if (!Machines.Authority) return;
            foreach (var rb in mouth.Live())
            {
                var p = TriggerZone.PartOf(rb);
                if (p != null && p.Doll != null) { if (Traps2.Cool(hit, p, 0.5f)) { Machines.Hurt(p, 14f, DamageType.Explosion, Vector3.up, this, "Furnace"); GoreKit.Burn(p, 0.4f); Burning.Apply(p.Doll, 20f, this); } }
                else if (rb.GetComponent<Spawned>() != null && Random.value < 0.01f) { FX.Fire(rb.position, Vector3.up, 0.5f); Destroy(rb.gameObject); }
            }
        }
    }

    public sealed class Freezer : MonoBehaviour
    {
        TriggerZone inside; readonly HashSet<Goredoll> done = new HashSet<Goredoll>(); readonly Dictionary<Goredoll, float> since = new Dictionary<Goredoll, float>();
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Freezer", pos, rot);
            var ice = new Color(0.75f, 0.85f, 0.92f);
            Machines.Part(go.transform, "Walls", mb =>
            {
                mb.Color = ice; mb.Gloss = 0.7f;
                mb.Box(new Vector3(0, 1.2f, -1.2f), new Vector3(2.5f, 2.4f, 0.1f)); mb.Box(new Vector3(-1.2f, 1.2f, 0), new Vector3(0.1f, 2.4f, 2.5f)); mb.Box(new Vector3(1.2f, 1.2f, 0), new Vector3(0.1f, 2.4f, 2.5f));
                mb.Box(new Vector3(0, 2.45f, 0), new Vector3(2.5f, 0.1f, 2.5f)); mb.Box(new Vector3(0, 0.03f, 0), new Vector3(2.5f, 0.06f, 2.5f));
            });
            var f = go.AddComponent<Freezer>();
            f.inside = Machines.Zone(go.transform, new Vector3(0, 1.2f, 0), new Vector3(2.2f, 2.2f, 2.2f));
            var l = new GameObject("Cold").AddComponent<Light>(); l.transform.SetParent(go.transform, false); l.transform.localPosition = new Vector3(0, 2.2f, 0);
            l.type = LightType.Point; l.color = new Color(0.6f, 0.8f, 1f); l.range = 4f; l.intensity = 1.5f;
            return go;
        }
        void Update()
        {
            if (Random.value < 0.3f) FX.Dust(transform.TransformPoint(new Vector3(Random.Range(-1f, 1f), Random.Range(0.2f, 2.2f), Random.Range(-1f, 1f))), 0.02f, new Color(0.9f, 0.95f, 1f, 0.4f), 1);
            if (!Machines.Authority) return;
            foreach (var rb in inside.Live())
            {
                var p = TriggerZone.PartOf(rb);
                if (p == null || p.Doll == null || done.Contains(p.Doll)) continue;
                if (!since.TryGetValue(p.Doll, out var t)) { since[p.Doll] = Time.time; continue; }
                if (Time.time - t > 6f) { done.Add(p.Doll); ContentFx.Freeze(p.Doll); }
            }
        }
    }

    public sealed class Claymore : MonoBehaviour
    {
        bool armed = true;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Claymore Mine", pos, rot);
            Machines.Part(go.transform, "Mine", mb =>
            {
                mb.Color = new Color(0.3f, 0.35f, 0.22f); mb.BevelBox(new Vector3(0, 0.12f, 0), new Vector3(0.24f, 0.14f, 0.05f), 0.01f);
                mb.SetColor(Machines.Dark); foreach (var x in new[] { -0.09f, 0.09f }) mb.Cylinder(new Vector3(x, 0.05f, 0), new Vector3(x - 0.02f, 0f, 0.02f), 0.006f, 0.006f, 4);
            }, null, false);
            var c = go.AddComponent<Claymore>();
            Machines.Part(go.transform, "Laser", mb => { mb.Color = Color.white; mb.Box(new Vector3(0, 0.12f, 1.5f), new Vector3(0.006f, 0.006f, 3f)); }, Mats.Glow(new Color(1f, 0.1f, 0.05f), 2f), false);
            var z = Machines.Zone(go.transform, new Vector3(0, 0.6f, 1.5f), new Vector3(0.4f, 1.2f, 3f));
            z.Enter = col => c.Trip(col);
            return go;
        }
        void Trip(Collider col)
        {
            if (!armed || !Machines.Authority) return;
            if (col.GetComponent<BodyPart>() == null && col.GetComponentInParent<PlayerController>() == null && col.GetComponentInParent<RemotePlayer>() == null) return;
            armed = false;
            AudioManager.Play("grenade_pin", transform.position, 1f);
            var at = transform.position + Vector3.up * 0.2f;
            // Ball bearings in a forward cone.
            foreach (var d in Goredoll.All)
            {
                if (d == null) continue;
                var to = d.Position - at;
                if (to.magnitude > 9f || Vector3.Angle(transform.forward, to) > 35f) continue;
                foreach (var pt in new[] { PartType.Chest, PartType.ThighL, PartType.ThighR, PartType.Abdomen })
                    if (d.Parts.TryGetValue(pt, out var bp) && bp != null && bp.Body != null)
                        bp.TakeDamage(new DamageInfo { Amount = 55f, Type = DamageType.Bullet, Caliber = 8f, Point = bp.Body.position, Direction = to.normalized, Normal = -to.normalized, Force = to.normalized * 40f, Source = this, Weapon = "Claymore" });
            }
            Explosion.Create(at, 2.5f, 120f, this, 1.4f);
            Destroy(gameObject);
        }
    }

    public sealed class Tripwire : MonoBehaviour
    {
        bool armed = true;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Tripwire Grenade", pos, rot);
            Machines.Part(go.transform, "Pegs", mb =>
            {
                mb.Color = Tripwire_Peg; mb.Box(new Vector3(-1.2f, 0.1f, 0), new Vector3(0.04f, 0.2f, 0.04f)); mb.Box(new Vector3(1.2f, 0.1f, 0), new Vector3(0.04f, 0.2f, 0.04f));
                mb.SetColor(new Color(0.75f, 0.75f, 0.7f)); mb.Box(new Vector3(0, 0.12f, 0), new Vector3(2.4f, 0.004f, 0.004f));
                mb.SetColor(new Color(0.25f, 0.3f, 0.2f)); mb.Sphere(new Vector3(1.25f, 0.12f, 0), new Vector3(0.05f, 0.06f, 0.05f), 6, 8);
            }, null, false);
            var t = go.AddComponent<Tripwire>();
            var z = Machines.Zone(go.transform, new Vector3(0, 0.15f, 0), new Vector3(2.4f, 0.3f, 0.2f));
            z.Enter = c => t.Trip(c);
            return go;
        }
        static readonly Color Tripwire_Peg = new Color(0.4f, 0.3f, 0.2f);
        void Trip(Collider c)
        {
            if (!armed || !Machines.Authority) return;
            if (c.GetComponent<BodyPart>() == null && c.GetComponentInParent<PlayerController>() == null && c.GetComponentInParent<RemotePlayer>() == null) return;
            armed = false;
            AudioManager.Play("grenade_pin", transform.position, 1f);
            Invoke(nameof(Boom), 1.2f);
        }
        void Boom() { Explosion.Create(transform.TransformPoint(new Vector3(1.25f, 0.12f, 0)), 5f, 220f, this, 1.3f); Destroy(gameObject); }
    }

    public sealed class BouncyCastle : MonoBehaviour
    {
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Bouncy Castle", pos, rot);
            Machines.Part(go.transform, "Castle", mb =>
            {
                mb.Color = new Color(0.9f, 0.2f, 0.2f); mb.Gloss = 0.6f;
                mb.BevelBox(new Vector3(0, 0.25f, 0), new Vector3(4f, 0.5f, 4f), 0.2f);
                mb.SetColor(new Color(0.2f, 0.4f, 0.95f), 0.6f);
                foreach (var x in new[] { -1.85f, 1.85f }) foreach (var z in new[] { -1.85f, 1.85f }) mb.Cylinder(new Vector3(x, 0.4f, z), new Vector3(x, 2.6f, z), 0.3f, 0.3f, 12);
                mb.SetColor(new Color(0.95f, 0.85f, 0.15f), 0.6f);
                mb.BevelBox(new Vector3(0, 1.3f, -1.9f), new Vector3(3.6f, 1.6f, 0.25f), 0.12f); mb.BevelBox(new Vector3(-1.9f, 1.3f, 0), new Vector3(0.25f, 1.6f, 3.6f), 0.12f); mb.BevelBox(new Vector3(1.9f, 1.3f, 0), new Vector3(0.25f, 1.6f, 3.6f), 0.12f);
            }, Mats.Lit);
            var z0 = Machines.Zone(go.transform, new Vector3(0, 0.75f, 0), new Vector3(3.6f, 0.5f, 3.6f));
            z0.Enter = c => Bounce(c, go.transform);
            go.AddComponent<BouncyCastle>();
            return go;
        }
        static void Bounce(Collider c, Transform t)
        {
            var p = c.GetComponentInParent<PlayerController>();
            if (p != null) { p.Launch(Vector3.up * Random.Range(9f, 12f)); AudioManager.Play("boing", t.position, 0.7f, Random.Range(0.8f, 1f)); return; }
            var rb = c.attachedRigidbody;
            if (rb == null || rb.isKinematic || !Machines.Authority) return;
            var part = rb.GetComponent<BodyPart>();
            if (part != null && part.Doll != null) part.Doll.Launch(Vector3.up * Random.Range(7f, 10f) + Random.insideUnitSphere * 1.5f);
            else RB.SetVel(rb, new Vector3(RB.Vel(rb).x, Random.Range(6f, 9f), RB.Vel(rb).z));
            AudioManager.Play("boing", t.position, 0.6f, Random.Range(0.8f, 1.1f));
        }
    }

    public sealed class BallPit : MonoBehaviour
    {
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Ball Pit", pos, rot);
            Machines.Part(go.transform, "Walls", mb =>
            {
                mb.Color = new Color(0.2f, 0.6f, 0.95f); mb.Gloss = 0.5f;
                mb.Box(new Vector3(0, 0.35f, 1.5f), new Vector3(3.1f, 0.7f, 0.1f)); mb.Box(new Vector3(0, 0.35f, -1.5f), new Vector3(3.1f, 0.7f, 0.1f));
                mb.Box(new Vector3(1.5f, 0.35f, 0), new Vector3(0.1f, 0.7f, 3.1f)); mb.Box(new Vector3(-1.5f, 0.35f, 0), new Vector3(0.1f, 0.7f, 3.1f));
            });
            go.AddComponent<BallPit>();
            if (!Machines.Authority) return go;
            var colors = new[] { new Color(0.95f, 0.2f, 0.2f), new Color(0.2f, 0.6f, 0.95f), new Color(0.95f, 0.85f, 0.15f), new Color(0.25f, 0.85f, 0.3f), new Color(0.9f, 0.4f, 0.9f) };
            for (int i = 0; i < 70; i++)
            {
                var p = go.transform.TransformPoint(new Vector3(Random.Range(-1.3f, 1.3f), 0.2f + (i / 25) * 0.18f, Random.Range(-1.3f, 1.3f)));
                var rb = PropFactory.SimpleBody("Ball", p, Quaternion.identity, Vector3.one * 0.14f, colors[i % colors.Length], 0.15f, SurfaceType.Carpet, Layers.Props);
                var bc = rb.GetComponent<BoxCollider>(); if (bc != null) Destroy(bc);
                var sc = rb.gameObject.AddComponent<SphereCollider>(); sc.radius = 0.07f;
                rb.transform.SetParent(go.transform, true);
            }
            return go;
        }
    }

    public sealed class BowlingSet : MonoBehaviour
    {
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Bowling Set", pos, rot);
            Machines.Part(go.transform, "Lane", mb => { mb.Color = new Color(0.75f, 0.58f, 0.38f); mb.Gloss = 0.8f; mb.Box(new Vector3(0, 0.01f, 3f), new Vector3(1.1f, 0.02f, 8f)); }, Mats.LitTextured(TextureFactory.Wood));
            go.AddComponent<BowlingSet>();
            if (!Machines.Authority) return go;
            int k = 0;
            for (int row = 0; row < 4; row++)
                for (int i = 0; i <= row; i++, k++)
                {
                    var p = go.transform.TransformPoint(new Vector3((i - row / 2f) * 0.3f, 0.25f, 6f + row * 0.26f));
                    PropFactory.Spawn("bowling", p, go.transform.rotation);
                }
            var ball = PropFactory.SimpleBody("Bowling Ball", go.transform.TransformPoint(new Vector3(0, 0.2f, -0.5f)), Quaternion.identity, Vector3.one * 0.22f, new Color(0.15f, 0.1f, 0.4f), 7f, SurfaceType.Metal, Layers.Props);
            var bc2 = ball.GetComponent<BoxCollider>(); if (bc2 != null) Destroy(bc2);
            ball.gameObject.AddComponent<SphereCollider>().radius = 0.11f;
            return go;
        }
    }

    public sealed class Hoop : MonoBehaviour
    {
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Basketball Hoop", pos, rot);
            Machines.Part(go.transform, "Pole", mb =>
            {
                mb.Color = Machines.Dark; mb.Cylinder(Vector3.zero, Vector3.up * 3.2f, 0.07f, 0.07f, 10); mb.Box(new Vector3(0, 3.1f, 0.4f), new Vector3(0.08f, 0.08f, 0.8f));
                mb.SetColor(Color.white, 0.5f); mb.Box(new Vector3(0, 3.35f, 0.8f), new Vector3(1.8f, 1.05f, 0.05f));
                mb.SetColor(new Color(0.95f, 0.4f, 0.1f), 0.6f); mb.Torus(new Vector3(0, 3.05f, 1.05f), 0.23f, 0.015f, 16, 4, Vector3.up);
            });
            go.AddComponent<Hoop>();
            if (Machines.Authority)
            {
                var ball = PropFactory.SimpleBody("Basketball", go.transform.TransformPoint(new Vector3(0.5f, 0.3f, 1.4f)), Quaternion.identity, Vector3.one * 0.24f, new Color(0.9f, 0.45f, 0.12f), 0.6f, SurfaceType.Carpet, Layers.Props);
                var bc = ball.GetComponent<BoxCollider>(); if (bc != null) Destroy(bc);
                var sc = ball.gameObject.AddComponent<SphereCollider>(); sc.radius = 0.12f; PhysMat.Apply(sc, "Basketball", 0.8f, 0.6f);
            }
            return go;
        }
    }

    public sealed class Goal : MonoBehaviour
    {
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Football Goal", pos, rot);
            Machines.Part(go.transform, "Frame", mb =>
            {
                mb.Color = Color.white; mb.Gloss = 0.6f;
                mb.Cylinder(new Vector3(-2.4f, 0, 0), new Vector3(-2.4f, 2.2f, 0), 0.05f, 0.05f, 8); mb.Cylinder(new Vector3(2.4f, 0, 0), new Vector3(2.4f, 2.2f, 0), 0.05f, 0.05f, 8);
                mb.Cylinder(new Vector3(-2.4f, 2.2f, 0), new Vector3(2.4f, 2.2f, 0), 0.05f, 0.05f, 8);
                mb.SetColor(new Color(0.85f, 0.85f, 0.85f), 0.2f); mb.Box(new Vector3(0, 1.1f, -1.2f), new Vector3(4.8f, 2.2f, 0.02f)); mb.Box(new Vector3(0, 2.2f, -0.6f), new Vector3(4.8f, 0.02f, 1.2f));
            });
            go.AddComponent<Goal>();
            if (Machines.Authority)
            {
                var ball = PropFactory.SimpleBody("Football", go.transform.TransformPoint(new Vector3(0, 0.3f, 4f)), Quaternion.identity, Vector3.one * 0.22f, Color.white, 0.45f, SurfaceType.Carpet, Layers.Props);
                var bc = ball.GetComponent<BoxCollider>(); if (bc != null) Destroy(bc);
                var sc = ball.gameObject.AddComponent<SphereCollider>(); sc.radius = 0.11f; PhysMat.Apply(sc, "Football", 0.6f, 0.5f);
            }
            return go;
        }
    }

    public sealed class TrafficLight : MonoBehaviour
    {
        readonly Renderer[] lamps = new Renderer[3]; Material off;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Traffic Light", pos, rot);
            Machines.Part(go.transform, "Pole", mb => { mb.Color = Machines.Dark; mb.Cylinder(Vector3.zero, Vector3.up * 3f, 0.07f, 0.06f, 10); mb.BevelBox(new Vector3(0, 3.4f, 0), new Vector3(0.35f, 0.95f, 0.3f), 0.04f); });
            var t = go.AddComponent<TrafficLight>();
            t.off = Mats.Lit;
            var cols = new[] { new Color(1f, 0.1f, 0.05f), new Color(1f, 0.75f, 0.1f), new Color(0.2f, 1f, 0.3f) };
            for (int i = 0; i < 3; i++)
            {
                var c = cols[i];
                t.lamps[i] = Machines.Part(go.transform, "Lamp", mb => { mb.Color = c; mb.Sphere(Vector3.zero, new Vector3(0.1f, 0.1f, 0.03f), 8, 10); }, Mats.Lit, false, new Vector3(0, 3.7f - i * 0.3f, 0.16f)).GetComponent<Renderer>();
            }
            return go;
        }
        static readonly Material[] lit = new Material[3];
        void Update()
        {
            float t = Mathf.Repeat(Time.time, 14f);
            int on = t < 6f ? 2 : t < 8f ? 1 : 0;
            var cols = new[] { new Color(1f, 0.1f, 0.05f), new Color(1f, 0.75f, 0.1f), new Color(0.2f, 1f, 0.3f) };
            for (int i = 0; i < 3; i++)
            {
                if (lit[i] == null) lit[i] = Mats.Glow(cols[i], 3f);
                if (lamps[i] != null) lamps[i].sharedMaterial = i == on ? lit[i] : off;
            }
        }
    }

    public sealed class StreetLampProp : MonoBehaviour, IDamageable
    {
        Light light; Renderer bulb; bool broken;
        public bool IsAlive => !broken;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Street Lamp", pos, rot);
            Machines.Part(go.transform, "Pole", mb => { mb.Color = Machines.Dark; mb.Cylinder(Vector3.zero, Vector3.up * 5f, 0.08f, 0.06f, 10); mb.Box(new Vector3(0, 5f, 0.5f), new Vector3(0.08f, 0.08f, 1.1f)); mb.BevelBox(new Vector3(0, 4.95f, 1.0f), new Vector3(0.35f, 0.12f, 0.5f), 0.03f); });
            var s = go.AddComponent<StreetLampProp>();
            var b = Machines.Part(go.transform, "Bulb", mb => { mb.Color = Color.white; mb.Box(Vector3.zero, new Vector3(0.28f, 0.03f, 0.4f)); }, Mats.Glow(new Color(1f, 0.85f, 0.6f), 3f), true, new Vector3(0, 4.87f, 1.0f));
            s.bulb = b.GetComponent<Renderer>();
            var bd = b.AddComponent<StreetLampBulb>(); bd.Lamp = s;
            s.light = new GameObject("Light").AddComponent<Light>(); s.light.transform.SetParent(go.transform, false); s.light.transform.localPosition = new Vector3(0, 4.7f, 1f);
            s.light.type = LightType.Spot; s.light.transform.localRotation = Quaternion.LookRotation(Vector3.down); s.light.spotAngle = 100f; s.light.range = 14f; s.light.intensity = 2.5f; s.light.color = new Color(1f, 0.85f, 0.6f);
            return go;
        }
        public void TakeDamage(DamageInfo info) { }
        public void Break()
        {
            if (broken) return;
            broken = true;
            if (light != null) light.enabled = false;
            if (bulb != null) bulb.enabled = false;
            FX.Sparks(transform.TransformPoint(new Vector3(0, 4.87f, 1f)), Vector3.down, 20, 5f);
            AudioManager.Play("glass_break", transform.position + Vector3.up * 4.8f, 0.9f);
        }
    }

    /// <summary>The bulb is its own collider: shooting it breaks the lamp.</summary>
    public sealed class StreetLampBulb : MonoBehaviour, IDamageable
    {
        public StreetLampProp Lamp;
        public bool IsAlive => Lamp != null && Lamp.IsAlive;
        public void TakeDamage(DamageInfo info) { if (Lamp != null) Lamp.Break(); }
    }

    public sealed class PropSpawner : MonoBehaviour, IInteractable
    {
        static readonly string[] Ids = { "crate", "barrel", "explosive_barrel", "watermelon", "ball", "tire", "cinder", "mattress", "bowling" };
        int idx; float next;
        public string Prompt => "Spawning: " + Ids[idx] + " (E to change)";
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Prop Spawner", pos, rot);
            Machines.Part(go.transform, "Machine", mb => { mb.Color = Machines.Dark; mb.BevelBox(new Vector3(0, 0.8f, 0), new Vector3(1f, 1.6f, 1f), 0.06f); mb.SetColor(Machines.Yellow); mb.Box(new Vector3(0, 1.65f, 0), new Vector3(1.05f, 0.1f, 1.05f)); mb.SetColor(Color.black); mb.Box(new Vector3(0, 0.9f, 0.51f), new Vector3(0.7f, 0.7f, 0.02f)); });
            go.AddComponent<PropSpawner>();
            return go;
        }
        public void Interact(GameObject user) { idx = (idx + 1) % Ids.Length; AudioManager.Ui("ui_tab"); GameEvents.ShowToast("Prop Spawner: " + Ids[idx]); }
        void Update()
        {
            if (!Machines.Authority || Time.time < next) return;
            next = Time.time + 3f;
            int alive = 0;
            foreach (Transform c in transform) if (c.GetComponent<Spawned>() != null) alive++;
            if (alive >= 12) return;
            var rb = PropFactory.Spawn(Ids[idx], transform.TransformPoint(new Vector3(0, 1.2f, 1.1f)), transform.rotation);
            if (rb != null) { rb.transform.SetParent(transform, true); RB.SetVel(rb, transform.forward * 4f + Vector3.up * 2f); AudioManager.Play("sg_spawn", transform.position, 0.5f); }
        }
    }
}
