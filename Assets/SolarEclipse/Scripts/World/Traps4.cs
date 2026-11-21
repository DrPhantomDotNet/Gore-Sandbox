using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 traps and props, part 3: shark tank and piranha pool.
    /// Registered through Traps3.Register.
    /// </summary>
    public static class Traps4
    {
        public static void Register(Action<string, string, string, string, Func<Vector3, Quaternion, GameObject>> add)
        {
            add("sharktank", "Shark Tank", "A glass tank with a hungry shark inside. Throw something in.", "drop", FishTank.Shark);
            add("piranhapool", "Piranha Pool", "A pool full of piranhas. They strip a body in seconds.", "drop", FishTank.Piranha);
        }

        /// <summary>Walls of a box-shaped tank as separate colliders (Machines.Part gives each part one box collider).</summary>
        internal static void TankWalls(Transform t, float w, float d, float h, Material glass, Color frame)
        {
            Machines.Part(t, "Floor", mb => { mb.Color = frame; mb.Box(new Vector3(0, 0.05f, 0), new Vector3(w, 0.1f, d)); });
            Machines.Part(t, "Wall N", mb => { mb.Color = new Color(0.8f, 0.9f, 1f); mb.Box(new Vector3(0, h / 2f, d / 2f), new Vector3(w, h, 0.06f)); }, glass);
            Machines.Part(t, "Wall S", mb => { mb.Color = new Color(0.8f, 0.9f, 1f); mb.Box(new Vector3(0, h / 2f, -d / 2f), new Vector3(w, h, 0.06f)); }, glass);
            Machines.Part(t, "Wall E", mb => { mb.Color = new Color(0.8f, 0.9f, 1f); mb.Box(new Vector3(w / 2f, h / 2f, 0), new Vector3(0.06f, h, d)); }, glass);
            Machines.Part(t, "Wall W", mb => { mb.Color = new Color(0.8f, 0.9f, 1f); mb.Box(new Vector3(-w / 2f, h / 2f, 0), new Vector3(0.06f, h, d)); }, glass);
            Machines.Part(t, "Frame", mb =>
            {
                mb.Color = frame;
                for (int sx = -1; sx <= 1; sx += 2) for (int sz = -1; sz <= 1; sz += 2) mb.Box(new Vector3(sx * w / 2f, h / 2f, sz * d / 2f), new Vector3(0.1f, h + 0.1f, 0.1f));
                mb.Box(new Vector3(0, h, d / 2f), new Vector3(w, 0.08f, 0.1f)); mb.Box(new Vector3(0, h, -d / 2f), new Vector3(w, 0.08f, 0.1f));
                mb.Box(new Vector3(w / 2f, h, 0), new Vector3(0.1f, 0.08f, d)); mb.Box(new Vector3(-w / 2f, h, 0), new Vector3(0.1f, 0.08f, d));
            }, null, false);
        }
    }

    public sealed class FishTank : MonoBehaviour
    {
        TriggerZone zone; bool shark; Transform fish; readonly List<Transform> school = new List<Transform>();
        readonly Dictionary<BodyPart, float> hit = new Dictionary<BodyPart, float>();
        float angle, lunge, nextBite; Vector3 lungeTo; float waterTop, half;

        public static GameObject Shark(Vector3 pos, Quaternion rot) => Build(pos, rot, true);
        public static GameObject Piranha(Vector3 pos, Quaternion rot) => Build(pos, rot, false);

        static GameObject Build(Vector3 pos, Quaternion rot, bool shark)
        {
            float w = shark ? 5f : 3.4f, d = shark ? 5f : 3.4f, h = shark ? 2.2f : 1.2f;
            var go = Machines.Root(shark ? "Shark Tank" : "Piranha Pool", pos, rot);
            Traps4.TankWalls(go.transform, w, d, h, Mats.Glass, shark ? Machines.Steel : new Color(0.35f, 0.3f, 0.25f));
            Machines.Part(go.transform, "Water", mb => { mb.Color = shark ? new Color(0.25f, 0.55f, 0.8f, 0.5f) : new Color(0.3f, 0.4f, 0.25f, 0.6f); mb.Box(new Vector3(0, h * 0.45f, 0), new Vector3(w - 0.1f, h * 0.88f, d - 0.1f)); }, Mats.Glass, false);
            var t = go.AddComponent<FishTank>();
            t.shark = shark; t.waterTop = h * 0.9f; t.half = w / 2f - 0.4f;
            t.zone = Machines.Zone(go.transform, new Vector3(0, h * 0.45f, 0), new Vector3(w - 0.2f, h * 0.9f, d - 0.2f));
            if (shark)
            {
                t.fish = Machines.Part(go.transform, "Shark", mb =>
                {
                    mb.Color = new Color(0.45f, 0.5f, 0.55f);
                    mb.Sphere(Vector3.zero, new Vector3(0.32f, 0.32f, 1.1f));
                    mb.Box(new Vector3(0, 0.38f, -0.1f), new Vector3(0.05f, 0.4f, 0.35f));
                    mb.Box(new Vector3(0, 0.1f, -1.1f), new Vector3(0.05f, 0.55f, 0.3f));
                    mb.SetColor(new Color(0.9f, 0.9f, 0.88f)); mb.Box(new Vector3(0, -0.12f, 0.85f), new Vector3(0.24f, 0.04f, 0.2f));
                }, Mats.Lit, false).transform;
            }
            else
            {
                for (int i = 0; i < 14; i++)
                    t.school.Add(Machines.Part(go.transform, "Piranha", mb => { mb.Color = new Color(0.55f, 0.25f, 0.15f); mb.Sphere(Vector3.zero, new Vector3(0.05f, 0.08f, 0.12f)); mb.SetColor(new Color(0.6f, 0.6f, 0.6f)); mb.Box(new Vector3(0, 0, -0.14f), new Vector3(0.02f, 0.1f, 0.06f)); }, Mats.Lit, false).transform);
            }
            return go;
        }

        void Update()
        {
            // Swimming.
            angle += Time.deltaTime * (shark ? 0.6f : 1.6f);
            if (shark && fish != null)
            {
                Vector3 circle = new Vector3(Mathf.Cos(angle) * half * 0.7f, waterTop * 0.5f, Mathf.Sin(angle) * half * 0.7f);
                Vector3 target = lunge > 0f ? transform.InverseTransformPoint(lungeTo) : circle;
                lunge -= Time.deltaTime;
                var prev = fish.localPosition;
                fish.localPosition = Vector3.MoveTowards(prev, target, Time.deltaTime * (lunge > 0f ? 6f : 1.8f));
                var dir = fish.localPosition - prev;
                if (dir.sqrMagnitude > 1e-6f) fish.localRotation = Quaternion.Slerp(fish.localRotation, Quaternion.LookRotation(dir) * Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 6f) * 8f), Time.deltaTime * 8f);
            }
            for (int i = 0; i < school.Count; i++)
            {
                float a = angle * (1f + i * 0.07f) + i;
                school[i].localPosition = new Vector3(Mathf.Cos(a) * half * (0.3f + (i % 4) * 0.18f), waterTop * (0.25f + (i % 3) * 0.2f), Mathf.Sin(a * 1.3f) * half * 0.8f);
                school[i].localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
            }
            if (!Machines.Authority) return;
            foreach (var rb in zone.Live())
            {
                var p = TriggerZone.PartOf(rb);
                RB.SetVel(rb, RB.Vel(rb) * 0.95f); // water drag
                rb.AddForce(Vector3.up * 6f, ForceMode.Acceleration); // floats a bit
                if (p == null || p.Doll == null) continue;
                if (shark)
                {
                    if (Time.time < nextBite || !Traps2.Cool(hit, p, 1.5f)) continue;
                    nextBite = Time.time + 1.3f; lunge = 0.6f; lungeTo = p.transform.position;
                    Machines.Hurt(p, 45f, DamageType.Slash, Random.onUnitSphere, this, "Shark");
                    BloodFX.Burst(p.transform.position, Vector3.up, 3f, new Color(0.45f, 0.02f, 0.02f));
                    AudioManager.Play("blade_chop", p.transform.position, 1f, 0.7f);
                    if (p.CanSever && p.Doll.Dismember != null && Random.value < 0.4f) p.Doll.Dismember.Sever(p, p.transform.position, Random.onUnitSphere, false);
                    if (rb != null) rb.AddForce(Random.onUnitSphere * 6f, ForceMode.VelocityChange);
                }
                else
                {
                    if (!Traps2.Cool(hit, p, 0.3f)) continue;
                    Machines.Hurt(p, 5f, DamageType.Slash, Random.onUnitSphere, this, "Piranhas");
                    if (Random.value < 0.3f) BloodFX.Mist(p.transform.position, Vector3.up, 1.2f);
                    if (Random.value < 0.05f) GoreKit.Burn(p, 0.25f); // flesh stripped down
                    rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
                }
            }
        }
    }
}
