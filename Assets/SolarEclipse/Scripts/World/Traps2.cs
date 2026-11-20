using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 traps and props (spawn menu > Props): spike pit, closing spike wall, pendulum blade, blade pillar,
    /// rail saw, flame jet, acid pool, electric floor, laser grid, crusher ceiling, iron maiden, land mine, punching
    /// bag, hospital bed, wheelchair, office chair, fire hydrant, jet engine. Built with the Machines helpers; damage
    /// and forces run on the host / in singleplayer only (Machines.Authority).
    /// </summary>
    public static class Traps2
    {
        public static void Register(Action<string, string, string, string, Func<Vector3, Quaternion, GameObject>> add)
        {
            add("spikepit", "Spike Pit", "A pit lined with steel spikes. Falling in is a bad idea.", "warning", SpikePit.Create);
            add("spikewall", "Closing Spike Wall", "A wall of spikes that grinds forward and back. Don't get pinned.", "warning", SpikeWall.Create);
            add("pendulum", "Pendulum Blade", "A huge blade swinging in an arc. Cuts through anything in its path.", "cut", Pendulum.Create);
            add("bladepillar", "Blade Pillar", "A spinning pillar with four blades.", "saw", BladePillar.Create);
            add("railsaw", "Rail Saw", "A buzz saw racing back and forth along a rail.", "saw", RailSaw.Create);
            add("flamejet", "Flame Jet", "A floor nozzle that blasts fire every few seconds.", "fire", FlameJet.Create);
            add("acidpool", "Acid Pool", "Bubbling green acid. Eats skin, then flesh.", "drop", AcidPool.Create);
            add("electricfloor", "Electric Floor", "A shock plate that zaps whoever stands on it.", "bolt", ElectricFloor.Create);
            add("lasergrid", "Laser Grid", "Red cutting lasers. Walk through and come out in pieces.", "cut", LaserGrid.Create);
            add("crusher", "Crusher Ceiling", "A slab that slowly comes down, crushes, and goes back up.", "hammer", CrusherCeiling.Create);
            add("ironmaiden", "Iron Maiden", "Put a Goredoll inside and press E to close the spiked doors.", "warning", IronMaiden.Create);
            add("landmine", "Land Mine", "Step on it. Or don't.", "explosion", LandMine.Create);
            add("punchingbag", "Punching Bag", "A heavy bag on a chain. Practise your Boxing.", "fist", PunchingBag.Create);
            add("hospitalbed", "Hospital Bed", "A bed with restraint straps and rails.", "heart", HospitalBed.Create);
            add("wheelchair", "Wheelchair", "Rolls. Fast, downhill.", "refresh", Wheelchair.Create);
            add("officechair", "Office Chair", "A rolling desk chair that spins.", "refresh", OfficeChair.Create);
            add("hydrant", "Fire Hydrant", "Shoot it or hit it hard and it blasts a jet of water.", "drop", Hydrant.Create);
            add("jetengine", "Jet Engine", "A running jet engine. It sucks in everything in front of it.", "wind", JetEngine.Create);
        }

        internal static readonly Color Spike = new Color(0.5f, 0.5f, 0.52f), Rusty = new Color(0.45f, 0.28f, 0.18f);

        internal static void Spikes(MeshBuilder mb, Vector3 origin, Vector3 dir, Vector3 right, Vector3 up, int nx, int ny, float spacing, float len)
        {
            for (int x = 0; x < nx; x++)
                for (int y = 0; y < ny; y++)
                {
                    var p = origin + right * ((x - (nx - 1) / 2f) * spacing) + up * ((y - (ny - 1) / 2f) * spacing);
                    mb.Cylinder(p, p + dir * len, 0.025f, 0.002f, 5);
                }
        }

        /// <summary>Damage a Goredoll part touching a trap, with a cooldown per part.</summary>
        internal static bool Cool(Dictionary<BodyPart, float> map, BodyPart p, float every)
        {
            if (map.TryGetValue(p, out var t) && Time.time - t < every) return false;
            map[p] = Time.time;
            return true;
        }
    }

    // ====================================================================== spikes

    public sealed class SpikePit : MonoBehaviour
    {
        TriggerZone zone; readonly Dictionary<BodyPart, float> hit = new Dictionary<BodyPart, float>();
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Spike Pit", pos, rot);
            Machines.Part(go.transform, "Frame", mb =>
            {
                mb.Color = Machines.Dark;
                mb.Box(new Vector3(0, 0.05f, 1.25f), new Vector3(2.6f, 0.1f, 0.1f)); mb.Box(new Vector3(0, 0.05f, -1.25f), new Vector3(2.6f, 0.1f, 0.1f));
                mb.Box(new Vector3(1.25f, 0.05f, 0), new Vector3(0.1f, 0.1f, 2.6f)); mb.Box(new Vector3(-1.25f, 0.05f, 0), new Vector3(0.1f, 0.1f, 2.6f));
                mb.SetColor(new Color(0.12f, 0.08f, 0.07f)); mb.Box(new Vector3(0, 0.01f, 0), new Vector3(2.4f, 0.02f, 2.4f));
                mb.SetColor(Traps2.Spike, 0.8f); Traps2.Spikes(mb, new Vector3(0, 0.02f, 0), Vector3.up, Vector3.right, Vector3.forward, 8, 8, 0.3f, 0.55f);
            }, null, false);
            Machines.Part(go.transform, "Floor", mb => { mb.Color = new Color(0.1f, 0.08f, 0.07f); mb.Box(new Vector3(0, -0.02f, 0), new Vector3(2.5f, 0.04f, 2.5f)); });
            var s = go.AddComponent<SpikePit>();
            s.zone = Machines.Zone(go.transform, new Vector3(0, 0.3f, 0), new Vector3(2.4f, 0.6f, 2.4f));
            return go;
        }
        void FixedUpdate()
        {
            if (!Machines.Authority) return;
            foreach (var rb in zone.Live())
            {
                var p = TriggerZone.PartOf(rb);
                float v = -rb.linearVelocityCompat().y;
                if (p == null || p.Doll == null) continue;
                if (v > 1.5f && Traps2.Cool(hit, p, 0.6f))
                {
                    Machines.Hurt(p, 15f + v * 12f, DamageType.Stab, Vector3.down, this, "Spike Pit");
                    BloodFX.Spurt(rb.position, Vector3.up, 1.5f);
                    AudioManager.Play("stab", rb.position, 0.9f);
                }
                RB.SetVel(rb, RB.Vel(rb) * 0.85f); // impaled bodies stick
            }
        }
    }

    public sealed class SpikeWall : MonoBehaviour
    {
        Transform wall; TriggerZone zone; readonly Dictionary<BodyPart, float> hit = new Dictionary<BodyPart, float>();
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Closing Spike Wall", pos, rot);
            Machines.Part(go.transform, "Rails", mb => { mb.Color = Machines.Dark; mb.Box(new Vector3(-1.6f, 0.05f, 1.2f), new Vector3(0.12f, 0.1f, 3f)); mb.Box(new Vector3(1.6f, 0.05f, 1.2f), new Vector3(0.12f, 0.1f, 3f)); }, null, false);
            var s = go.AddComponent<SpikeWall>();
            s.wall = Machines.Part(go.transform, "Wall", mb =>
            {
                mb.Color = Traps2.Rusty; mb.Box(new Vector3(0, 1.3f, 0), new Vector3(3.2f, 2.6f, 0.2f));
                mb.SetColor(Traps2.Spike, 0.8f); Traps2.Spikes(mb, new Vector3(0, 1.3f, 0.1f), Vector3.forward, Vector3.right, Vector3.up, 9, 7, 0.34f, 0.45f);
            }).transform;
            s.zone = Machines.Zone(s.wall, new Vector3(0, 1.3f, 0.35f), new Vector3(3.1f, 2.5f, 0.5f));
            AudioManager.CreateLoop("grind", go.transform, 0.35f, AudioBus.Sfx, 20f);
            return go;
        }
        void FixedUpdate()
        {
            float z = (1f - Mathf.Cos(Time.time * 0.6f)) * 1.3f;
            wall.localPosition = new Vector3(0, 0, z);
            if (!Machines.Authority) return;
            foreach (var rb in zone.Live())
            {
                var p = TriggerZone.PartOf(rb);
                if (p == null || p.Doll == null || !Traps2.Cool(hit, p, 0.5f)) continue;
                Machines.Hurt(p, 28f, DamageType.Stab, transform.forward, this, "Spike Wall");
                rb.AddForce(transform.forward * 8f, ForceMode.VelocityChange);
                BloodFX.Spurt(rb.position, -transform.forward, 1.2f);
            }
        }
    }

    // ====================================================================== blades

    /// <summary>Shared cutting behaviour: severs limbs that touch a blade zone, with a cooldown.</summary>
    public abstract class BladeTrap : MonoBehaviour
    {
        readonly Dictionary<BodyPart, float> hit = new Dictionary<BodyPart, float>();
        protected void Cut(Collider c, Vector3 at, Vector3 dir, float dmg, string name)
        {
            if (!Machines.Authority) return;
            var part = c.GetComponent<BodyPart>();
            if (part == null || part.Doll == null) { if (c.attachedRigidbody != null) { FX.Sparks(c.ClosestPoint(at), Vector3.up, 10, 6f); AudioManager.Play("metal_impact", at, 0.5f); } return; }
            if (!Traps2.Cool(hit, part, 0.35f)) return;
            Machines.Hurt(part, dmg, DamageType.Slash, dir, this, name);
            if (part.CanSever && part.Doll.Dismember != null && Random.value < 0.75f) part.Doll.Dismember.Sever(part, c.ClosestPoint(at), dir, true);
            BloodFX.Spurt(c.ClosestPoint(at), dir + Vector3.up * 0.4f, 2.2f);
            AudioManager.Play("slice", at, 1f);
        }
    }

    public sealed class Pendulum : BladeTrap
    {
        Transform arm;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Pendulum Blade", pos, rot);
            Machines.Part(go.transform, "Frame", mb =>
            {
                mb.Color = Machines.Dark;
                mb.Box(new Vector3(-1.4f, 2.2f, 0), new Vector3(0.18f, 4.4f, 0.18f)); mb.Box(new Vector3(1.4f, 2.2f, 0), new Vector3(0.18f, 4.4f, 0.18f));
                mb.Box(new Vector3(0, 4.45f, 0), new Vector3(3f, 0.2f, 0.25f));
            });
            var p = go.AddComponent<Pendulum>();
            var pivot = new GameObject("Pivot").transform; pivot.SetParent(go.transform, false); pivot.localPosition = new Vector3(0, 4.3f, 0);
            p.arm = pivot;
            Machines.Part(pivot, "Rod", mb => { mb.Color = Machines.Steel; mb.Box(new Vector3(0, -1.6f, 0), new Vector3(0.08f, 3.2f, 0.08f)); }, null, false);
            var blade = Machines.Part(pivot, "Blade", mb =>
            {
                mb.Color = Machines.Steel; mb.Gloss = 0.95f;
                var pts = new List<Vector2>();
                for (int i = 0; i <= 12; i++) { float a = Mathf.Lerp(-60f, 60f, i / 12f) * Mathf.Deg2Rad; pts.Add(new Vector2(Mathf.Sin(a) * 0.9f, -Mathf.Cos(a) * 0.45f)); }
                pts.Add(new Vector2(0.3f, 0.1f)); pts.Add(new Vector2(-0.3f, 0.1f));
                mb.Prism(pts.ToArray(), 0.03f, Vector3.zero);
            }, null, false, new Vector3(0, -3.3f, 0));
            blade.transform.localRotation = Quaternion.Euler(0, 90, 0);
            var z = Machines.Zone(blade.transform, new Vector3(0, -0.2f, 0), new Vector3(1.6f, 0.45f, 0.2f));
            z.Enter = c => p.Cut(c, blade.transform.position, p.transform.right * Mathf.Sign(Mathf.Cos(Time.time * 1.4f)), 85f, "Pendulum Blade");
            AudioManager.CreateLoop("fan_loop", go.transform, 0.25f, AudioBus.Sfx, 15f);
            return go;
        }
        void Update() { arm.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 1.4f) * 65f); }
    }

    public sealed class BladePillar : BladeTrap
    {
        Transform spinner;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Blade Pillar", pos, rot);
            Machines.Part(go.transform, "Pillar", mb => { mb.Color = Machines.Dark; mb.Cylinder(Vector3.zero, Vector3.up * 2.2f, 0.22f, 0.18f, 14); mb.SetColor(Machines.Yellow); mb.Cylinder(Vector3.zero, Vector3.up * 0.1f, 0.5f, 0.5f, 16); });
            var b = go.AddComponent<BladePillar>();
            b.spinner = new GameObject("Spinner").transform; b.spinner.SetParent(go.transform, false); b.spinner.localPosition = new Vector3(0, 1.1f, 0);
            for (int i = 0; i < 4; i++)
            {
                var arm = new GameObject("Arm").transform; arm.SetParent(b.spinner, false); arm.localRotation = Quaternion.Euler(0, i * 90f, 0);
                var bl = Machines.Part(arm, "Blade", mb => { mb.Color = Machines.Steel; mb.Gloss = 0.95f; mb.Box(new Vector3(0, 0, 0.8f), new Vector3(0.02f, 0.25f, 1.2f)); mb.SetColor(Machines.Dark); mb.Box(new Vector3(0, 0, 0.25f), new Vector3(0.06f, 0.1f, 0.3f)); }, null, false, new Vector3(0, (i % 2) * 0.6f - 0.3f, 0));
                var z = Machines.Zone(bl.transform, new Vector3(0, 0, 0.85f), new Vector3(0.25f, 0.3f, 1.2f));
                z.Enter = c => b.Cut(c, bl.transform.position, bl.transform.right, 70f, "Blade Pillar");
            }
            AudioManager.CreateLoop("fan_loop", go.transform, 0.5f, AudioBus.Sfx, 20f);
            return go;
        }
        void Update() => spinner.Rotate(0, 420f * Time.deltaTime, 0, Space.Self);
    }

    public sealed class RailSaw : BladeTrap
    {
        Transform saw;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Rail Saw", pos, rot);
            Machines.Part(go.transform, "Rail", mb => { mb.Color = Machines.Dark; mb.Box(new Vector3(0, 0.05f, 0), new Vector3(6f, 0.1f, 0.3f)); mb.SetColor(Machines.Yellow); for (int i = 0; i < 12; i++) mb.Box(new Vector3(-2.75f + i * 0.5f, 0.105f, 0), new Vector3(0.25f, 0.01f, 0.32f)); }, null, true);
            var r = go.AddComponent<RailSaw>();
            r.saw = Machines.Part(go.transform, "Saw", mb =>
            {
                mb.Color = Machines.Steel; mb.Gloss = 0.9f;
                mb.Cylinder(new Vector3(-0.01f, 0, 0), new Vector3(0.01f, 0, 0), 0.6f, 0.6f, 24);
                for (int k = 0; k < 18; k++) { var d = Quaternion.Euler(k * 20f, 0, 0) * Vector3.up; mb.Cylinder(d * 0.59f, d * 0.68f + Quaternion.Euler(90, 0, 0) * d * 0.05f, 0.02f, 0.002f, 3); }
            }, null, false, new Vector3(0, 0.45f, 0)).transform;
            var z = Machines.Zone(r.saw, Vector3.zero, new Vector3(0.2f, 1.3f, 1.3f));
            z.Enter = c => r.Cut(c, r.saw.position, r.transform.right, 75f, "Rail Saw");
            AudioManager.CreateLoop("grind", r.saw, 0.5f, AudioBus.Sfx, 25f);
            return go;
        }
        void Update()
        {
            saw.localPosition = new Vector3(Mathf.PingPong(Time.time * 2.4f, 5f) - 2.5f, 0.45f, 0);
            saw.localRotation = Quaternion.Euler(Time.time * -1500f, 0, 0);
        }
    }

    // ====================================================================== fire, acid, electricity, lasers

    public sealed class FlameJet : MonoBehaviour
    {
        TriggerZone zone; float next, until;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Flame Jet", pos, rot);
            Machines.Part(go.transform, "Nozzle", mb => { mb.Color = Machines.Dark; mb.Cylinder(Vector3.zero, Vector3.up * 0.25f, 0.25f, 0.18f, 12); mb.SetColor(Machines.Red); mb.Cylinder(Vector3.up * 0.25f, Vector3.up * 0.32f, 0.1f, 0.08f, 10); });
            var f = go.AddComponent<FlameJet>();
            f.zone = Machines.Zone(go.transform, new Vector3(0, 1.6f, 0), new Vector3(1f, 2.6f, 1f));
            return go;
        }
        void Update()
        {
            if (Time.time > next) { next = Time.time + 4f; until = Time.time + 1.6f; AudioManager.Play("flamer", transform.position + Vector3.up, 0.8f); }
            if (Time.time > until) return;
            var top = transform.position + transform.up * 0.35f;
            for (int i = 0; i < 3; i++) FX.Fire(top + Random.insideUnitSphere * 0.08f, transform.up * Random.Range(5f, 8f) + Random.insideUnitSphere, Random.Range(0.3f, 0.6f));
            if (!Machines.Authority) return;
            foreach (var rb in zone.Live())
            {
                var p = TriggerZone.PartOf(rb);
                if (p != null && p.Doll != null) Burning.Apply(p.Doll, 10f, this);
                rb.AddForce(transform.up * 6f, ForceMode.Acceleration);
            }
        }
    }

    public sealed class AcidPool : MonoBehaviour
    {
        TriggerZone zone; readonly Dictionary<BodyPart, float> hit = new Dictionary<BodyPart, float>(); float nextBubble;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Acid Pool", pos, rot);
            Machines.Part(go.transform, "Rim", mb =>
            {
                mb.Color = new Color(0.35f, 0.36f, 0.33f);
                mb.Box(new Vector3(0, 0.25f, 1.55f), new Vector3(3.2f, 0.5f, 0.12f)); mb.Box(new Vector3(0, 0.25f, -1.55f), new Vector3(3.2f, 0.5f, 0.12f));
                mb.Box(new Vector3(1.55f, 0.25f, 0), new Vector3(0.12f, 0.5f, 3.2f)); mb.Box(new Vector3(-1.55f, 0.25f, 0), new Vector3(0.12f, 0.5f, 3.2f));
            });
            Machines.Part(go.transform, "Acid", mb => { mb.Color = Color.white; mb.Box(new Vector3(0, 0.3f, 0), new Vector3(3f, 0.02f, 3f)); }, Mats.Glow(new Color(0.35f, 1f, 0.15f), 1.4f), false);
            var a = go.AddComponent<AcidPool>();
            a.zone = Machines.Zone(go.transform, new Vector3(0, 0.25f, 0), new Vector3(3f, 0.5f, 3f));
            var l = new GameObject("Glow").AddComponent<Light>(); l.transform.SetParent(go.transform, false); l.transform.localPosition = Vector3.up * 0.8f;
            l.type = LightType.Point; l.color = new Color(0.4f, 1f, 0.2f); l.range = 5f; l.intensity = 1.2f;
            return go;
        }
        void Update()
        {
            if (Time.time > nextBubble) { nextBubble = Time.time + 0.15f; FX.Dust(transform.TransformPoint(new Vector3(Random.Range(-1.4f, 1.4f), 0.32f, Random.Range(-1.4f, 1.4f))), 0.02f, new Color(0.5f, 1f, 0.3f, 0.6f), 2); }
            if (!Machines.Authority) return;
            foreach (var rb in zone.Live())
            {
                RB.SetVel(rb, RB.Vel(rb) * 0.9f); // thick liquid
                var p = TriggerZone.PartOf(rb);
                if (p == null || p.Doll == null || !Traps2.Cool(hit, p, 0.5f)) continue;
                Machines.Hurt(p, 9f, DamageType.Explosion, Vector3.up, this, "Acid");
                GoreKit.Burn(p, 0.25f);
                if (p.Doll.Vitals.Conscious && Random.value < 0.3f) p.Doll.Voice.Say("v_scream");
            }
        }
    }

    public sealed class ElectricFloor : MonoBehaviour
    {
        TriggerZone zone; float next; Renderer plate;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Electric Floor", pos, rot);
            Machines.Part(go.transform, "Base", mb => { mb.Color = Machines.Dark; mb.Box(new Vector3(0, 0.04f, 0), new Vector3(2.6f, 0.08f, 2.6f)); mb.SetColor(Machines.Yellow); mb.Box(new Vector3(0, 0.085f, 1.25f), new Vector3(2.6f, 0.01f, 0.1f)); mb.Box(new Vector3(0, 0.085f, -1.25f), new Vector3(2.6f, 0.01f, 0.1f)); });
            var e = go.AddComponent<ElectricFloor>();
            e.plate = Machines.Part(go.transform, "Plate", mb => { mb.Color = Color.white; mb.Box(new Vector3(0, 0.09f, 0), new Vector3(2.3f, 0.01f, 2.3f)); }, Mats.Glow(new Color(0.4f, 0.65f, 1f), 0.6f), false).GetComponent<Renderer>();
            e.zone = Machines.Zone(go.transform, new Vector3(0, 0.6f, 0), new Vector3(2.4f, 1.1f, 2.4f));
            return go;
        }
        void Update()
        {
            if (Time.time < next) return;
            next = Time.time + 1.2f;
            bool any = false;
            foreach (var rb in zone.Live())
            {
                var p = TriggerZone.PartOf(rb);
                if (p == null || p.Doll == null) continue;
                any = true;
                FX.Sparks(rb.position, Vector3.up, 8, 4f);
                if (!Machines.Authority) continue;
                Machines.Hurt(p, 12f, DamageType.Explosion, Vector3.up, this, "Electric Floor");
                if (p.Doll.Ragdoll != null) p.Doll.Ragdoll.Fall(1.5f);
                rb.AddForce(Random.insideUnitSphere * 4f, ForceMode.VelocityChange);
            }
            if (any) AudioManager.Play("zap", transform.position, 0.9f);
        }
    }

    public sealed class LaserGrid : BladeTrap
    {
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Laser Grid", pos, rot);
            Machines.Part(go.transform, "Posts", mb => { mb.Color = Machines.Dark; mb.Box(new Vector3(-1.5f, 1.2f, 0), new Vector3(0.15f, 2.4f, 0.15f)); mb.Box(new Vector3(1.5f, 1.2f, 0), new Vector3(0.15f, 2.4f, 0.15f)); });
            var g = go.AddComponent<LaserGrid>();
            for (int i = 0; i < 6; i++)
            {
                float y = 0.25f + i * 0.38f;
                var beam = Machines.Part(go.transform, "Laser", mb => { mb.Color = Color.white; mb.Box(Vector3.zero, new Vector3(2.9f, 0.012f, 0.012f)); }, Mats.Glow(new Color(1f, 0.08f, 0.05f), 5f), false, new Vector3(0, y, 0));
                var z = Machines.Zone(beam.transform, Vector3.zero, new Vector3(2.9f, 0.05f, 0.1f));
                z.Enter = c => g.Cut(c, beam.transform.position, g.transform.forward, 60f, "Laser Grid");
            }
            AudioManager.CreateLoop("intro_buzz", go.transform, 0.15f, AudioBus.Sfx, 10f);
            return go;
        }
    }

    // ====================================================================== crushing

    public sealed class CrusherCeiling : MonoBehaviour
    {
        Transform slab; TriggerZone zone; readonly Dictionary<BodyPart, float> hit = new Dictionary<BodyPart, float>(); float lastY;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Crusher Ceiling", pos, rot);
            Machines.Part(go.transform, "Frame", mb =>
            {
                mb.Color = Machines.Dark;
                foreach (var x in new[] { -1.4f, 1.4f }) foreach (var z in new[] { -1.4f, 1.4f }) mb.Box(new Vector3(x, 2f, z), new Vector3(0.18f, 4f, 0.18f));
                mb.Box(new Vector3(0, 4.05f, 0), new Vector3(3f, 0.2f, 3f));
                mb.SetColor(Machines.Yellow); mb.Box(new Vector3(0, 0.02f, 0), new Vector3(2.8f, 0.04f, 2.8f));
            });
            var c = go.AddComponent<CrusherCeiling>();
            c.slab = Machines.Part(go.transform, "Slab", mb => { mb.Color = Traps2.Rusty; mb.Box(Vector3.zero, new Vector3(2.6f, 0.4f, 2.6f)); }, null, true, new Vector3(0, 3.7f, 0)).transform;
            c.zone = Machines.Zone(c.slab, new Vector3(0, -0.4f, 0), new Vector3(2.5f, 0.5f, 2.5f));
            return go;
        }
        void FixedUpdate()
        {
            // 6 s cycle: slow descent, a hold at the bottom, then back up.
            float t = Mathf.Repeat(Time.time, 8f);
            float y = t < 4f ? Mathf.Lerp(3.7f, 0.25f, t / 4f) : t < 5f ? 0.25f : Mathf.Lerp(0.25f, 3.7f, (t - 5f) / 3f);
            slab.localPosition = new Vector3(0, y, 0);
            bool down = y < lastY; lastY = y;
            if (!Machines.Authority || !down) return;
            foreach (var rb in zone.Live())
            {
                var p = TriggerZone.PartOf(rb);
                if (p == null || p.Doll == null || !Traps2.Cool(hit, p, 0.4f)) continue;
                float squeeze = Mathf.Clamp01((1.4f - y) / 1.2f);
                if (squeeze <= 0f) continue;
                Machines.Hurt(p, 20f + 80f * squeeze, DamageType.Crush, Vector3.down, this, "Crusher");
                if (squeeze > 0.85f && p.IsHead && p.Doll.Dismember != null) p.Doll.Dismember.Gib(p, Vector3.down);
                AudioManager.Play("bone_crack", rb.position, 0.6f + squeeze * 0.4f);
            }
        }
    }

    public sealed class IronMaiden : MonoBehaviour, IInteractable
    {
        Transform doorL, doorR; TriggerZone inside; float closeAt = -1f; bool shut;
        public string Prompt => shut ? "Open the doors" : "Close the doors";
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Iron Maiden", pos, rot);
            Machines.Part(go.transform, "Body", mb =>
            {
                mb.Color = new Color(0.3f, 0.3f, 0.32f);
                mb.Box(new Vector3(0, 1.1f, -0.4f), new Vector3(0.9f, 2.2f, 0.08f));
                mb.Box(new Vector3(-0.45f, 1.1f, -0.15f), new Vector3(0.08f, 2.2f, 0.5f)); mb.Box(new Vector3(0.45f, 1.1f, -0.15f), new Vector3(0.08f, 2.2f, 0.5f));
                mb.Box(new Vector3(0, 2.25f, -0.15f), new Vector3(0.98f, 0.1f, 0.58f));
                mb.SetColor(Traps2.Spike, 0.8f); Traps2.Spikes(mb, new Vector3(0, 1.1f, -0.36f), Vector3.forward, Vector3.right, Vector3.up, 3, 7, 0.22f, 0.2f);
            });
            var m = go.AddComponent<IronMaiden>();
            foreach (var side in new[] { -1f, 1f })
            {
                var hinge = new GameObject("Hinge").transform; hinge.SetParent(go.transform, false); hinge.localPosition = new Vector3(side * 0.45f, 0, 0.1f);
                Machines.Part(hinge, "Door", mb =>
                {
                    mb.Color = new Color(0.3f, 0.3f, 0.32f); mb.Box(new Vector3(-side * 0.225f, 1.1f, 0), new Vector3(0.45f, 2.2f, 0.06f));
                    mb.SetColor(Traps2.Spike, 0.8f); Traps2.Spikes(mb, new Vector3(-side * 0.225f, 1.1f, -0.03f), Vector3.back, Vector3.right, Vector3.up, 2, 7, 0.2f, 0.22f);
                }, null, false);
                if (side < 0) m.doorL = hinge; else m.doorR = hinge;
            }
            m.inside = Machines.Zone(go.transform, new Vector3(0, 1.1f, -0.1f), new Vector3(0.8f, 2.1f, 0.5f));
            return go;
        }
        public void Interact(GameObject user) { shut = !shut; closeAt = Time.time; AudioManager.Play("creak", transform.position + Vector3.up, 0.9f); }
        void Update()
        {
            float target = shut ? 0f : 110f;
            doorL.localRotation = Quaternion.Slerp(doorL.localRotation, Quaternion.Euler(0, -target, 0), Time.deltaTime * (shut ? 9f : 2f));
            doorR.localRotation = Quaternion.Slerp(doorR.localRotation, Quaternion.Euler(0, target, 0), Time.deltaTime * (shut ? 9f : 2f));
            if (!shut || closeAt < 0f || Time.time - closeAt < 0.25f) return;
            closeAt = -1f;
            AudioManager.Play("sledge_world", transform.position + Vector3.up, 0.9f);
            if (!Machines.Authority) return;
            foreach (var rb in inside.Live())
            {
                var p = TriggerZone.PartOf(rb);
                if (p == null || p.Doll == null) continue;
                Machines.Hurt(p, 55f, DamageType.Stab, -transform.forward, this, "Iron Maiden");
                BloodFX.Spurt(rb.position, transform.forward, 2f);
                rb.isKinematic = false;
            }
        }
    }

    public sealed class LandMine : MonoBehaviour
    {
        bool armed = true;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Land Mine", pos, rot);
            Machines.Part(go.transform, "Mine", mb => { mb.Color = new Color(0.25f, 0.3f, 0.2f); mb.Cylinder(Vector3.zero, Vector3.up * 0.07f, 0.16f, 0.14f, 14); mb.SetColor(Machines.Dark); mb.Cylinder(Vector3.up * 0.07f, Vector3.up * 0.1f, 0.05f, 0.05f, 10); }, null, false);
            var m = go.AddComponent<LandMine>();
            var z = Machines.Zone(go.transform, new Vector3(0, 0.15f, 0), new Vector3(0.45f, 0.3f, 0.45f));
            z.Enter = c => m.Step(c);
            return go;
        }
        void Step(Collider c)
        {
            if (!armed || !Machines.Authority) return;
            bool body = c.GetComponent<BodyPart>() != null || c.GetComponentInParent<PlayerController>() != null || c.GetComponentInParent<RemotePlayer>() != null;
            if (!body) return;
            armed = false;
            AudioManager.Play("grenade_pin", transform.position, 1f);
            Invoke(nameof(Boom), 0.25f);
        }
        void Boom() { Explosion.Create(transform.position + Vector3.up * 0.1f, 4f, 260f, this, 1.6f); Destroy(gameObject); }
    }

    // ====================================================================== props

    public sealed class PunchingBag : MonoBehaviour
    {
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Punching Bag", pos, rot);
            Machines.Part(go.transform, "Stand", mb =>
            {
                mb.Color = Machines.Dark;
                mb.Box(new Vector3(0, 1.4f, -0.6f), new Vector3(0.1f, 2.8f, 0.1f)); mb.Box(new Vector3(0, 2.75f, -0.25f), new Vector3(0.1f, 0.1f, 0.8f));
                mb.Box(new Vector3(0, 0.05f, -0.6f), new Vector3(0.8f, 0.1f, 0.8f));
            });
            var bag = new GameObject("Bag");
            bag.layer = Layers.Props;
            bag.transform.SetParent(go.transform, false);
            bag.transform.localPosition = new Vector3(0, 1.55f, 0.1f);
            var mb2 = new MeshBuilder { Color = new Color(0.55f, 0.1f, 0.08f), Gloss = 0.35f };
            mb2.Cylinder(new Vector3(0, -0.55f, 0), new Vector3(0, 0.55f, 0), 0.2f, 0.2f, 16);
            mb2.SetColor(Machines.Dark); mb2.Cylinder(new Vector3(0, 0.55f, 0), new Vector3(0, 1.1f, 0), 0.01f, 0.01f, 4);
            bag.AddComponent<MeshFilter>().sharedMesh = mb2.ToMesh("Bag");
            bag.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            var cap = bag.AddComponent<CapsuleCollider>(); cap.radius = 0.2f; cap.height = 1.1f;
            var rb = bag.AddComponent<Rigidbody>(); rb.mass = 35f; RB.SetDamping(rb, 0.3f, 0.6f);
            var j = bag.AddComponent<ConfigurableJoint>();
            j.connectedBody = go.GetComponent<Rigidbody>();
            j.anchor = new Vector3(0, 1.1f, 0);
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
            bag.AddComponent<Surface>().Type = SurfaceType.Carpet;
            bag.AddComponent<ImpactSounds>();
            go.AddComponent<PunchingBag>();
            return go;
        }
    }

    public sealed class HospitalBed : MonoBehaviour
    {
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Hospital Bed", pos, rot, false, 70f);
            Machines.Part(go.transform, "Bed", mb =>
            {
                mb.Color = Machines.Steel;
                foreach (var x in new[] { -0.45f, 0.45f }) foreach (var z in new[] { -0.95f, 0.95f }) mb.Box(new Vector3(x, 0.3f, z), new Vector3(0.05f, 0.6f, 0.05f));
                mb.Box(new Vector3(0, 0.55f, 0), new Vector3(0.95f, 0.06f, 2f));
                mb.SetColor(new Color(0.9f, 0.92f, 0.95f), 0.1f); mb.BevelBox(new Vector3(0, 0.66f, 0), new Vector3(0.9f, 0.16f, 1.95f), 0.04f);
                mb.SetColor(new Color(0.95f, 0.96f, 0.97f)); mb.BevelBox(new Vector3(0, 0.77f, 0.75f), new Vector3(0.6f, 0.1f, 0.35f), 0.04f);
                mb.SetColor(Machines.Steel); mb.Box(new Vector3(0, 0.95f, 1.0f), new Vector3(0.95f, 0.6f, 0.05f)); mb.Box(new Vector3(0, 0.8f, -1.0f), new Vector3(0.95f, 0.35f, 0.05f));
                mb.Box(new Vector3(0.5f, 0.9f, 0), new Vector3(0.03f, 0.25f, 1.2f)); mb.Box(new Vector3(-0.5f, 0.9f, 0), new Vector3(0.03f, 0.25f, 1.2f));
                mb.SetColor(new Color(0.2f, 0.18f, 0.15f)); foreach (var z in new[] { -0.6f, 0f, 0.5f }) mb.Box(new Vector3(0, 0.75f, z), new Vector3(0.96f, 0.02f, 0.08f)); // restraint straps
            });
            go.AddComponent<HospitalBed>();
            go.AddComponent<ImpactSounds>();
            return go;
        }
    }

    public sealed class Wheelchair : MonoBehaviour
    {
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Wheelchair", pos + Vector3.up * 0.1f, rot, false, 18f);
            var p = Machines.Part(go.transform, "Chair", mb =>
            {
                mb.Color = Machines.Steel;
                mb.Torus(new Vector3(-0.3f, 0.3f, 0f), 0.3f, 0.02f, 18, 4, Vector3.right); mb.Torus(new Vector3(0.3f, 0.3f, 0f), 0.3f, 0.02f, 18, 4, Vector3.right);
                mb.SetColor(Machines.Dark); mb.Box(new Vector3(0, 0.5f, 0), new Vector3(0.5f, 0.05f, 0.45f)); mb.Box(new Vector3(0, 0.8f, -0.22f), new Vector3(0.5f, 0.55f, 0.04f));
                mb.SetColor(Machines.Steel); mb.Cylinder(new Vector3(-0.2f, 0.1f, 0.35f), new Vector3(-0.2f, 0.45f, 0.25f), 0.015f, 0.015f, 6); mb.Cylinder(new Vector3(0.2f, 0.1f, 0.35f), new Vector3(0.2f, 0.45f, 0.25f), 0.015f, 0.015f, 6);
            });
            UnityEngine.Object.Destroy(p.GetComponent<BoxCollider>());
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.45f, 0); col.size = new Vector3(0.65f, 0.9f, 0.7f);
            PhysMat.Apply(col, "Wheels", 0.1f, 0.02f, false); // rolls
            go.AddComponent<Wheelchair>(); go.AddComponent<ImpactSounds>();
            return go;
        }
    }

    public sealed class OfficeChair : MonoBehaviour
    {
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Office Chair", pos + Vector3.up * 0.05f, rot, false, 12f);
            var p = Machines.Part(go.transform, "Chair", mb =>
            {
                mb.Color = Machines.Dark;
                for (int i = 0; i < 5; i++) { var d = Quaternion.Euler(0, i * 72f, 0) * Vector3.forward; mb.Box(d * 0.18f + Vector3.up * 0.07f, new Vector3(0.05f, 0.04f, 0.36f)); mb.Sphere(d * 0.33f + Vector3.up * 0.04f, Vector3.one * 0.035f, 5, 6); }
                mb.SetColor(Machines.Steel); mb.Cylinder(Vector3.up * 0.07f, Vector3.up * 0.45f, 0.025f, 0.025f, 8);
                mb.SetColor(new Color(0.12f, 0.12f, 0.16f)); mb.BevelBox(new Vector3(0, 0.5f, 0), new Vector3(0.48f, 0.08f, 0.46f), 0.03f); mb.BevelBox(new Vector3(0, 0.85f, -0.22f), new Vector3(0.44f, 0.55f, 0.06f), 0.03f);
            });
            UnityEngine.Object.Destroy(p.GetComponent<BoxCollider>());
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.55f, 0); col.size = new Vector3(0.6f, 1.1f, 0.6f);
            PhysMat.Apply(col, "Casters", 0.05f, 0.02f, false);
            go.AddComponent<OfficeChair>(); go.AddComponent<ImpactSounds>();
            return go;
        }
    }

    public sealed class Hydrant : MonoBehaviour, IDamageable
    {
        float health = 60f, sprayUntil; TriggerZone jet;
        public bool IsAlive => true;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Fire Hydrant", pos, rot);
            Machines.Part(go.transform, "Hydrant", mb =>
            {
                mb.Color = Machines.Red; mb.Gloss = 0.6f;
                mb.Cylinder(Vector3.zero, Vector3.up * 0.7f, 0.14f, 0.13f, 12); mb.Sphere(Vector3.up * 0.72f, new Vector3(0.14f, 0.1f, 0.14f), 8, 10);
                mb.Cylinder(new Vector3(0, 0.45f, 0), new Vector3(0, 0.45f, 0.24f), 0.06f, 0.06f, 8);
                mb.Cylinder(new Vector3(0, 0.45f, 0), new Vector3(0.2f, 0.45f, 0), 0.045f, 0.045f, 8); mb.Cylinder(new Vector3(0, 0.45f, 0), new Vector3(-0.2f, 0.45f, 0), 0.045f, 0.045f, 8);
            });
            var h = go.AddComponent<Hydrant>();
            h.jet = Machines.Zone(go.transform, new Vector3(0, 0.5f, 2.5f), new Vector3(1f, 1.2f, 4.5f));
            return go;
        }
        public void TakeDamage(DamageInfo info)
        {
            if (NetGuard.Intercept(this, info)) return;
            health -= info.Amount;
            if (health <= 0f) { sprayUntil = Time.time + 25f; health = 60f; AudioManager.Play("metal_impact", transform.position, 1f, 0.7f); }
        }
        void Update()
        {
            if (Time.time > sprayUntil) return;
            var o = transform.TransformPoint(new Vector3(0, 0.45f, 0.26f));
            for (int i = 0; i < 4; i++) FX.Dust(o, 0.05f, new Color(0.75f, 0.85f, 1f, 0.6f), 1);
            if (!Machines.Authority) return;
            foreach (var rb in jet.Live()) rb.AddForce(transform.forward * 30f + Vector3.up * 6f, ForceMode.Acceleration);
        }
    }

    public sealed class JetEngine : MonoBehaviour
    {
        TriggerZone intake, mouth; readonly Dictionary<BodyPart, float> hit = new Dictionary<BodyPart, float>(); Transform fan;
        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Jet Engine", pos, rot);
            Machines.Part(go.transform, "Nacelle", mb =>
            {
                mb.Color = new Color(0.8f, 0.82f, 0.85f); mb.Gloss = 0.8f;
                mb.Cylinder(new Vector3(0, 1.3f, -1.2f), new Vector3(0, 1.3f, 1.2f), 1.0f, 1.1f, 24);
                mb.SetColor(Machines.Dark); mb.Box(new Vector3(0, 0.4f, 0), new Vector3(0.4f, 0.8f, 1.2f));
            });
            var j = go.AddComponent<JetEngine>();
            j.fan = Machines.Part(go.transform, "Fan", mb =>
            {
                mb.Color = Machines.Steel; mb.Gloss = 0.9f;
                mb.Sphere(Vector3.zero, new Vector3(0.25f, 0.25f, 0.3f), 8, 10);
                for (int i = 0; i < 18; i++) { var d = Quaternion.Euler(0, 0, i * 20f) * Vector3.up; mb.Box(d * 0.55f, new Vector3(0.12f, 0.9f, 0.02f)); }
            }, null, false, new Vector3(0, 1.3f, 1.15f)).transform;
            j.intake = Machines.Zone(go.transform, new Vector3(0, 1.3f, 4f), new Vector3(3f, 3f, 5.5f));
            j.mouth = Machines.Zone(go.transform, new Vector3(0, 1.3f, 1.3f), new Vector3(1.8f, 1.8f, 0.6f));
            AudioManager.CreateLoop("fan_loop", go.transform, 1f, AudioBus.Sfx, 60f);
            return go;
        }
        void Update() => fan.Rotate(0, 0, 2200f * Time.deltaTime, Space.Self);
        void FixedUpdate()
        {
            if (!Machines.Authority) return;
            var pull = transform.TransformPoint(new Vector3(0, 1.3f, 1.2f));
            foreach (var rb in intake.Live()) if (!rb.isKinematic) rb.AddForce((pull - rb.worldCenterOfMass).normalized * 55f, ForceMode.Acceleration);
            foreach (var rb in mouth.Live())
            {
                var p = TriggerZone.PartOf(rb);
                if (p != null && p.Doll != null && Traps2.Cool(hit, p, 0.2f))
                {
                    Machines.Hurt(p, 120f, DamageType.Slash, -transform.forward, this, "Jet Engine");
                    if (p.Doll.Dismember != null) p.Doll.Dismember.Gib(p, Vector3.down);
                    BloodFX.Burst(rb.position, -transform.forward, 1.5f, Settings.BloodColor);
                    AudioManager.Play("grind", rb.position, 1f);
                }
                else if (p == null && rb.GetComponent<Spawned>() != null) { FX.Sparks(rb.position, Vector3.up, 20, 8f); Destroy(rb.gameObject); }
            }
        }
    }

    internal static class RbCompat
    {
        public static Vector3 linearVelocityCompat(this Rigidbody rb) => RB.Vel(rb);
    }
}
