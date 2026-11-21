using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 traps and props, part 3: shark tank, piranha pool, balloon, thruster, gravity zone, oil slick and ice patch.
    /// Registered through Traps3.Register.
    /// </summary>
    public static class Traps4
    {
        public static void Register(Action<string, string, string, string, Func<Vector3, Quaternion, GameObject>> add)
        {
            add("sharktank", "Shark Tank", "A glass tank with a hungry shark inside. Throw something in.", "drop", FishTank.Shark);
            add("piranhapool", "Piranha Pool", "A pool full of piranhas. They strip a body in seconds.", "drop", FishTank.Piranha);
            add("balloon", "Balloon", "A helium balloon. It ties itself to the first thing it touches and lifts it. Use several.", "globe", Balloon.Create);
            add("thruster", "Thruster", "A rocket thruster. Sticks to what it touches. Press E to fire it.", "explosion", Thruster.Create);
            add("gravityzone", "Gravity Zone", "A field that flips gravity: everything inside floats up. Press E to switch it on/off.", "arrow_down", GravityZone.Create);
            add("oilslick", "Oil Slick", "A slippery puddle of oil. Goredolls that walk into it go down.", "drop", Slick.Oil);
            add("icepatch", "Ice Patch", "A sheet of ice. Goredolls slip and slide on it.", "snow", Slick.Ice);
            add("optable", "Operating Table", "A steel surgery table with a lamp and a tray of instruments. Lay a Goredoll on it and press E to strap them down.", "scalpel", OperatingTable.Create);
            add("ecgmonitor", "ECG Monitor", "A bedside monitor: shows the nearest Goredoll's heart rate, blood pressure and oxygen, and beeps with every heartbeat.", "heart", EcgMonitor.Create);
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
                    EasterEggs.Found("Fresh Meat", "Fed someone to the shark.");
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

    /// <summary>Helium balloon: floats up, ties itself (spring "string") to the first rigidbody it touches.</summary>
    public sealed class Balloon : MonoBehaviour
    {
        Rigidbody rb; bool tied; float born;
        static readonly Color[] Colors = { new Color(0.9f, 0.15f, 0.15f), new Color(0.2f, 0.5f, 0.95f), new Color(0.95f, 0.8f, 0.15f), new Color(0.3f, 0.85f, 0.35f), new Color(0.85f, 0.3f, 0.85f) };
        LineRenderer line; Rigidbody other; Vector3 otherAnchor;

        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Balloon", pos + Vector3.up * 1.2f, Quaternion.identity, false, 0.05f);
            var c = Colors[Random.Range(0, Colors.Length)];
            Machines.Part(go.transform, "Balloon", mb => { mb.Color = c; mb.Sphere(Vector3.zero, new Vector3(0.28f, 0.34f, 0.28f)); mb.Box(new Vector3(0, -0.36f, 0), new Vector3(0.05f, 0.05f, 0.05f)); }, Mats.Lit, true);
            var b = go.AddComponent<Balloon>();
            b.rb = go.GetComponent<Rigidbody>(); b.rb.useGravity = false; RB.SetDamping(b.rb, 1.5f, 2f); b.born = Time.time;
            b.line = go.AddComponent<LineRenderer>(); b.line.widthMultiplier = 0.008f; b.line.sharedMaterial = Mats.Lit; b.line.positionCount = 2; b.line.enabled = false;
            return go;
        }

        void FixedUpdate() { if (rb != null) rb.AddForce(Vector3.up * 9.81f * 0.06f + Vector3.up * 2.2f, ForceMode.Force); } // lifts about 0.25 kg each

        void LateUpdate()
        {
            if (!tied) return;
            if (other == null) { tied = false; line.enabled = false; foreach (var j in GetComponents<SpringJoint>()) Destroy(j); return; }
            line.SetPosition(0, transform.position + Vector3.down * 0.36f);
            line.SetPosition(1, other.transform.TransformPoint(otherAnchor));
        }

        void OnCollisionEnter(Collision c)
        {
            if (tied || !Machines.Authority || Time.time - born < 0.3f) return;
            var o = c.rigidbody;
            if (o == null || o == rb || o.GetComponent<Balloon>() != null) return;
            tied = true; other = o;
            otherAnchor = o.transform.InverseTransformPoint(c.GetContact(0).point);
            var j = gameObject.AddComponent<SpringJoint>();
            j.connectedBody = o; j.autoConfigureConnectedAnchor = false; j.anchor = Vector3.down * 0.36f; j.connectedAnchor = otherAnchor;
            j.spring = 40f; j.damper = 2f; j.maxDistance = 1.4f; j.minDistance = 0f;
            transform.position += Vector3.up * 0.8f;
            line.enabled = true;
        }
    }

    /// <summary>Thruster: welds itself to the first rigidbody it touches; E toggles a constant push along its nozzle.</summary>
    public sealed class Thruster : MonoBehaviour, IInteractable
    {
        Rigidbody rb; bool on, stuck; AudioSource loop; float born;
        public string Prompt => on ? "Stop the thruster" : "Fire the thruster";

        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Thruster", pos + Vector3.up * 0.3f, rot, false, 4f);
            Machines.Part(go.transform, "Body", mb => { mb.Color = Machines.Steel; mb.Cylinder(new Vector3(0, 0, -0.25f), new Vector3(0, 0, 0.15f), 0.12f, 0.12f, 14); mb.SetColor(Machines.Dark); mb.Cylinder(new Vector3(0, 0, -0.25f), new Vector3(0, 0, -0.38f), 0.09f, 0.14f, 14); mb.SetColor(Machines.Yellow); mb.Box(new Vector3(0, 0.13f, 0), new Vector3(0.08f, 0.02f, 0.3f)); });
            var t = go.AddComponent<Thruster>(); t.rb = go.GetComponent<Rigidbody>(); t.born = Time.time;
            return go;
        }

        public void Interact(GameObject user)
        {
            on = !on;
            AudioManager.Ui(on ? "ui_success" : "ui_tab");
            if (on && loop == null) loop = AudioManager.CreateLoop("flamer", transform, 0.5f, AudioBus.Sfx, 25f);
            if (loop != null) { if (on) loop.Play(); else loop.Stop(); }
        }

        void OnCollisionEnter(Collision c)
        {
            if (stuck || !Machines.Authority || Time.time - born < 0.2f || c.rigidbody == null) return;
            stuck = true;
            var j = gameObject.AddComponent<FixedJoint>(); j.connectedBody = c.rigidbody; j.breakForce = 6000f;
        }

        void FixedUpdate()
        {
            if (!on) return;
            if (Machines.Authority && rb != null) rb.AddForce(transform.forward * 900f, ForceMode.Force);
        }

        void Update()
        {
            if (!on) return;
            var noz = transform.TransformPoint(new Vector3(0, 0, -0.42f));
            FX.Fire(noz, -transform.forward * 6f + Random.insideUnitSphere * 0.5f, 0.18f);
        }
    }

    /// <summary>Gravity zone: rigidbodies inside fall upward (gentle) and drift. E toggles it.</summary>
    public sealed class GravityZone : MonoBehaviour, IInteractable
    {
        TriggerZone zone; bool on = true; Renderer field;
        public string Prompt => on ? "Switch the gravity field off" : "Switch the gravity field on";

        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Gravity Zone", pos, rot);
            Machines.Part(go.transform, "Base", mb => { mb.Color = Machines.Dark; mb.Cylinder(new Vector3(0, 0, 0), new Vector3(0, 0.15f, 0), 2.2f, 2.2f, 28); mb.SetColor(new Color(0.4f, 0.3f, 0.9f)); mb.Cylinder(new Vector3(0, 0.15f, 0), new Vector3(0, 0.17f, 0), 1.9f, 1.9f, 28, false); });
            var f = Machines.Part(go.transform, "Field", mb => { mb.Color = new Color(0.5f, 0.4f, 1f, 0.25f); mb.Cylinder(new Vector3(0, 0.2f, 0), new Vector3(0, 6f, 0), 2.1f, 2.1f, 28, false); }, Mats.Glass, false);
            var g = go.AddComponent<GravityZone>();
            g.field = f.GetComponent<Renderer>();
            g.zone = Machines.Zone(go.transform, new Vector3(0, 3.1f, 0), new Vector3(4.2f, 6f, 4.2f));
            return go;
        }

        public void Interact(GameObject user) { on = !on; if (field != null) field.enabled = on; AudioManager.Ui(on ? "ui_success" : "ui_tab"); }

        void FixedUpdate()
        {
            if (!on || !Machines.Authority) return;
            foreach (var rb in zone.Live())
            {
                if (rb.isKinematic) continue;
                rb.AddForce(-Physics.gravity * 1.25f, ForceMode.Acceleration);
                RB.SetVel(rb, RB.Vel(rb) * 0.985f);
                var p = TriggerZone.PartOf(rb);
                if (p != null && p.Doll != null && p.Type == PartType.Pelvis) p.Doll.Ragdoll.Fall(2f);
            }
        }
    }

    /// <summary>Oil slick / ice patch: a frictionless flat patch; Goredolls walking on it lose their footing.</summary>
    public sealed class Slick : MonoBehaviour
    {
        TriggerZone zone; bool ice; readonly Dictionary<BodyPart, float> hit = new Dictionary<BodyPart, float>();

        public static GameObject Oil(Vector3 pos, Quaternion rot) => Build(pos, rot, false);
        public static GameObject Ice(Vector3 pos, Quaternion rot) => Build(pos, rot, true);

        static GameObject Build(Vector3 pos, Quaternion rot, bool ice)
        {
            var go = Machines.Root(ice ? "Ice Patch" : "Oil Slick", pos, rot);
            var top = Machines.Part(go.transform, "Patch", mb =>
            {
                mb.Color = ice ? new Color(0.8f, 0.92f, 1f) : new Color(0.04f, 0.04f, 0.05f);
                mb.Box(new Vector3(0, 0.01f, 0), new Vector3(ice ? 4f : 3f, 0.02f, ice ? 4f : 2.4f));
            }, ice ? Mats.Glass : Mats.LitMetal, true);
            PhysMat.Frictionless(top.GetComponent<Collider>()); // Core/RB.cs: works on Unity 6 and older
            var s = go.AddComponent<Slick>(); s.ice = ice;
            s.zone = Machines.Zone(go.transform, new Vector3(0, 0.4f, 0), new Vector3(ice ? 4f : 3f, 0.8f, ice ? 4f : 2.4f));
            return go;
        }

        void FixedUpdate()
        {
            if (!Machines.Authority) return;
            foreach (var rb in zone.Live())
            {
                var p = TriggerZone.PartOf(rb);
                if (p == null || p.Doll == null || p.Doll.IsDead) continue;
                if (p.Type != PartType.FootL && p.Type != PartType.FootR) continue;
                if (RB.Vel(rb).sqrMagnitude < 0.3f || !Traps2.Cool(hit, p, 1f)) continue;
                if (Random.value < (ice ? 0.25f : 0.45f))
                {
                    p.Doll.Ragdoll.Fall(ice ? 1.5f : 2.5f);
                    var pel = p.Doll.Parts.TryGetValue(PartType.Pelvis, out var pv) ? pv : null;
                    if (pel != null && pel.Body != null) pel.Body.AddForce(RB.Vel(rb).normalized * 3f + Vector3.up, ForceMode.VelocityChange);
                    if (p.Doll.Voice != null) p.Doll.Voice.Say("v_whoa", 0.9f, false);
                }
            }
        }
    }

    /// <summary>Bedside monitor: reads the nearest Goredoll within 3 m; live numbers shown over it when you're close; beeps per heartbeat.</summary>
    public sealed class EcgMonitor : MonoBehaviour
    {
        Goredoll patient; float nextScan, nextBeat; Transform screen;
        static bool hooked;
        static readonly System.Collections.Generic.List<EcgMonitor> all = new System.Collections.Generic.List<EcgMonitor>();

        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("ECG Monitor", pos, rot);
            Machines.Part(go.transform, "Stand", mb => { mb.Color = Machines.Steel; mb.Cylinder(new Vector3(0, 0, 0), new Vector3(0, 0.05f, 0), 0.3f, 0.3f, 16); mb.Cylinder(new Vector3(0, 0.05f, 0), new Vector3(0, 1.2f, 0), 0.025f, 0.025f, 8); mb.SetColor(new Color(0.85f, 0.86f, 0.88f)); mb.BevelBox(new Vector3(0, 1.38f, 0), new Vector3(0.42f, 0.32f, 0.14f), 0.02f); });
            var scr = Machines.Part(go.transform, "Screen", mb => { mb.Color = Color.white; mb.Box(new Vector3(0, 1.38f, 0.072f), new Vector3(0.36f, 0.25f, 0.01f)); }, Mats.Glow(new Color(0.1f, 0.35f, 0.18f), 1f), false);
            var m = go.AddComponent<EcgMonitor>(); m.screen = scr.transform;
            return go;
        }

        void OnEnable() { all.Add(this); if (!hooked) { hooked = true; UIRoot.HudLayer += DrawAll; } }
        void OnDisable() { all.Remove(this); }

        void Update()
        {
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 1f; patient = null; float best = 9f;
                foreach (var d in Goredoll.All)
                {
                    if (d == null) continue;
                    float dd = (d.Position - transform.position).sqrMagnitude;
                    if (dd < best) { best = dd; patient = d; }
                }
            }
            if (patient == null) return;
            var v = patient.Vitals;
            if (patient.IsDead) { if (Time.time >= nextBeat) { nextBeat = Time.time + 2.5f; AudioManager.Play("c4_beep", transform.position + Vector3.up * 1.4f, 0.35f, 0.6f, 1f, 1f, 12f); } return; }
            if (Time.time >= nextBeat) { nextBeat = Time.time + 60f / Mathf.Max(20f, v.HeartRate); AudioManager.Play("c4_beep", transform.position + Vector3.up * 1.4f, 0.3f, v.SpO2 < 88f ? 0.8f : 1.1f, 1f, 1f, 12f); }
        }

        static void DrawAll()
        {
            var p = PlayerController.Instance;
            if (p == null || p.Cam == null) return;
            foreach (var m in all)
            {
                if (m == null || m.screen == null) continue;
                var top = m.transform.position + Vector3.up * 1.7f;
                float dist = Vector3.Distance(p.Cam.transform.position, top);
                if (dist > 7f) continue;
                var sp = p.Cam.WorldToScreenPoint(top);
                if (sp.z < 0.1f) continue;
                var g = new Vector2(sp.x / UI.Scale, (Screen.height - sp.y) / UI.Scale);
                var r = new Rect(g.x - 120, g.y - 96, 240, 92);
                UI.Rounded(r, new Color(0.01f, 0.05f, 0.03f, 0.85f), true);
                if (m.patient == null) { UI.Label(r, "NO PATIENT", 18, new Color(0.3f, 1f, 0.45f), TextAnchor.MiddleCenter, FontStyle.Bold); continue; }
                var v = m.patient.Vitals; bool dead = m.patient.IsDead;
                UI.Label(new Rect(r.x + 10, r.y + 4, 220, 26), dead ? "HR 0  ASYSTOLE" : "HR " + Mathf.RoundToInt(v.HeartRate) + " bpm", 19, new Color(0.3f, 1f, 0.45f), TextAnchor.MiddleLeft, FontStyle.Bold);
                UI.Label(new Rect(r.x + 10, r.y + 32, 220, 26), "BP " + (dead ? "0/0" : Mathf.RoundToInt(v.Systolic) + "/" + Mathf.RoundToInt(v.Diastolic)), 18, new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleLeft, FontStyle.Bold);
                UI.Label(new Rect(r.x + 10, r.y + 60, 220, 26), "SpO2 " + (dead ? "--" : Mathf.RoundToInt(v.SpO2) + "%"), 18, new Color(0.4f, 0.8f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            }
        }
    }

    /// <summary>GS2 operating table: E straps whoever lies on it down by the chest, wrists and ankles (E again releases).</summary>
    public sealed class OperatingTable : MonoBehaviour, IInteractable
    {
        TriggerZone zone; bool strapped; readonly List<FixedJoint> straps = new List<FixedJoint>();
        public string Prompt => strapped ? "Release the straps" : "Strap the patient down";

        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Operating Table", pos, rot);
            Machines.Part(go.transform, "Top", mb => { mb.Color = new Color(0.75f, 0.77f, 0.8f); mb.BevelBox(new Vector3(0, 0.9f, 0), new Vector3(0.7f, 0.08f, 2.0f), 0.02f); });
            Machines.Part(go.transform, "Post", mb => { mb.Color = Machines.Steel; mb.Box(new Vector3(0, 0.45f, 0), new Vector3(0.25f, 0.86f, 0.25f)); mb.Box(new Vector3(0, 0.03f, 0), new Vector3(0.6f, 0.06f, 0.9f)); });
            Machines.Part(go.transform, "Tray", mb =>
            {
                mb.Color = Machines.Steel; mb.Box(new Vector3(0.75f, 1.0f, 0.3f), new Vector3(0.45f, 0.03f, 0.3f)); mb.Box(new Vector3(0.75f, 0.5f, 0.3f), new Vector3(0.03f, 1f, 0.03f));
                mb.SetColor(new Color(0.9f, 0.9f, 0.93f)); for (int k = 0; k < 5; k++) mb.Box(new Vector3(0.6f + k * 0.07f, 1.02f, 0.3f), new Vector3(0.01f, 0.008f, 0.22f));
            }, null, false);
            Machines.Part(go.transform, "Lamp", mb => { mb.Color = new Color(0.85f, 0.86f, 0.88f); mb.Box(new Vector3(-0.5f, 1.3f, 0), new Vector3(0.04f, 2.6f, 0.04f)); mb.Cylinder(new Vector3(-0.2f, 2.3f, 0), new Vector3(-0.2f, 2.42f, 0), 0.3f, 0.3f, 18); }, null, false);
            var l = new GameObject("Surgical Light").AddComponent<Light>(); l.transform.SetParent(go.transform, false); l.transform.localPosition = new Vector3(-0.2f, 2.25f, 0); l.transform.localRotation = Quaternion.Euler(90, 0, 0);
            l.type = LightType.Spot; l.spotAngle = 60f; l.range = 4f; l.intensity = 3f; l.color = new Color(1f, 0.98f, 0.92f); l.shadows = LightShadows.Soft;
            var t = go.AddComponent<OperatingTable>();
            t.zone = Machines.Zone(go.transform, new Vector3(0, 1.15f, 0), new Vector3(0.8f, 0.5f, 2.1f));
            return go;
        }

        public void Interact(GameObject user)
        {
            strapped = !strapped;
            AudioManager.Play("creak", transform.position + Vector3.up, 0.6f, 1.3f);
            foreach (var j in straps) if (j != null) Destroy(j);
            straps.Clear();
            if (!strapped || !Machines.Authority) return;
            var root = GetComponent<Rigidbody>();
            foreach (var rb in zone.Live())
            {
                var p = TriggerZone.PartOf(rb);
                if (p == null || p.Doll == null) continue;
                if (p.Type != PartType.Chest && p.Type != PartType.Pelvis && p.Type != PartType.HandL && p.Type != PartType.HandR && p.Type != PartType.FootL && p.Type != PartType.FootR) continue;
                var j = rb.gameObject.AddComponent<FixedJoint>(); j.connectedBody = root; j.breakForce = 8000f; straps.Add(j);
                if (p.Type == PartType.Chest && p.Doll.Vitals.Conscious) p.Doll.Voice.Say("v_no");
            }
            GameEvents.ShowToast(straps.Count > 0 ? "Strapped down." : "Nobody on the table.");
        }
    }
}
