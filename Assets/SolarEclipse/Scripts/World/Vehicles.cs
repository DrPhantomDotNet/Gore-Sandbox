using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Spawnable vehicles (Content Update): every Open World car plus the go-kart, bulldozer, tank and shopping cart
    /// (OpenWorld/OWCar.cs), and a helicopter whose rotor blades cut through anything. Press E to get in.
    /// </summary>
    public static class Vehicles
    {
        public static void Register(System.Action<string, string, string, string, System.Func<Vector3, Quaternion, GameObject>> add)
        {
            foreach (var it in OWItems.In(OWCat.Car))
            {
                var id = it.Id;
                add("veh_" + id.Substring(2), it.Name, it.Desc + " Press E to drive.", "car", (p, r) => OWCar.Create(OWCar.DefFor(id, "", "", id == "c_super", id == "c_tank", false), p + Vector3.up * 0.6f, r));
            }
            add("veh_heli", "Helicopter", "Press E to fly. Space up, Ctrl/C down, W/S forward/back, A/D turn. The rotor blades cut through Goredolls (host / singleplayer).", "wind", (p, r) => Heli.Create(p + Vector3.up * 0.2f, r));
        }
    }

    public sealed class Heli : MonoBehaviour, IInteractable, IDamageable
    {
        public static Heli Local;
        Rigidbody rb; Transform rotor, tailRotor, seat;
        float spin, health = 900f, orbit, nextCut;
        Vector2 input; float lift; bool driven;
        AudioSource engine;

        public string Prompt => driven ? "" : "Fly the helicopter";
        public bool IsAlive => health > 0f;

        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Helicopter", pos, rot, false, 1400f);
            var rbody = go.GetComponent<Rigidbody>();
            rbody.drag = 0.5f; rbody.angularDrag = 3f; rbody.interpolation = RigidbodyInterpolation.Interpolate;
            Machines.Part(go.transform, "Body", mb =>
            {
                mb.Color = new Color(0.12f, 0.15f, 0.2f); mb.Gloss = 0.6f;
                mb.Capsule(new Vector3(0, 1.4f, -0.6f), new Vector3(0, 1.4f, 1.2f), 0.95f, 0.85f, 14, 4);
                mb.SetColor(new Color(0.1f, 0.15f, 0.2f), 1f); mb.Sphere(new Vector3(0, 1.55f, 1.55f), new Vector3(0.8f, 0.6f, 0.6f), 10, 12);
                mb.SetColor(new Color(0.12f, 0.15f, 0.2f), 0.6f); mb.Cylinder(new Vector3(0, 1.6f, -1.2f), new Vector3(0, 1.85f, -5.5f), 0.35f, 0.12f, 10);
                mb.Box(new Vector3(0, 2.3f, -5.4f), new Vector3(0.08f, 1f, 0.6f));
                mb.SetColor(new Color(0.3f, 0.3f, 0.32f), 0.7f);
                mb.Box(new Vector3(0.8f, 0.15f, 0.3f), new Vector3(0.08f, 0.08f, 2.8f)); mb.Box(new Vector3(-0.8f, 0.15f, 0.3f), new Vector3(0.08f, 0.08f, 2.8f));
                for (int i = 0; i < 2; i++) { float z = i == 0 ? -0.4f : 1f; mb.Box(new Vector3(0.6f, 0.45f, z), new Vector3(0.06f, 0.6f, 0.06f)); mb.Box(new Vector3(-0.6f, 0.45f, z), new Vector3(0.06f, 0.6f, 0.06f)); }
                mb.Cylinder(new Vector3(0, 2.3f, 0.3f), new Vector3(0, 2.65f, 0.3f), 0.12f, 0.12f, 8);
            }, null, false);
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 1.3f, 0.3f); col.size = new Vector3(1.8f, 1.8f, 3.4f);
            var tail = go.AddComponent<BoxCollider>(); tail.center = new Vector3(0, 1.75f, -3.4f); tail.size = new Vector3(0.5f, 0.5f, 4.2f);
            var skid = go.AddComponent<BoxCollider>(); skid.center = new Vector3(0, 0.15f, 0.3f); skid.size = new Vector3(1.7f, 0.15f, 2.8f);
            var h = go.AddComponent<Heli>();
            h.rb = rbody;
            h.rotor = Machines.Part(go.transform, "Rotor", mb => { mb.Color = new Color(0.08f, 0.08f, 0.09f); for (int i = 0; i < 4; i++) { mb.Push(Vector3.zero, Quaternion.Euler(0, i * 90f, 0)); mb.Box(new Vector3(0, 0, 2.4f), new Vector3(0.28f, 0.03f, 4.6f)); mb.Pop(); } }, null, false, new Vector3(0, 2.7f, 0.3f)).transform;
            h.tailRotor = Machines.Part(go.transform, "Tail Rotor", mb => { mb.Color = new Color(0.08f, 0.08f, 0.09f); mb.Box(Vector3.zero, new Vector3(0.03f, 1.1f, 0.15f)); mb.Box(Vector3.zero, new Vector3(0.03f, 0.15f, 1.1f)); }, null, false, new Vector3(0.12f, 2.1f, -5.4f)).transform;
            h.seat = new GameObject("Seat").transform; h.seat.SetParent(go.transform, false); h.seat.localPosition = new Vector3(-0.35f, 1.1f, 0.9f);
            h.engine = go.AddComponent<AudioSource>();
            h.engine.clip = SoundBank.Get("fan_loop"); h.engine.loop = true; h.engine.spatialBlend = 1f; h.engine.maxDistance = 120f; h.engine.volume = 0f;
            if (h.engine.clip != null) h.engine.Play();
            return go;
        }

        public void Interact(GameObject user)
        {
            if (driven) return;
            if (NetState.IsClient) { GameEvents.ShowToast("Only the host can fly helicopters for now."); return; }
            driven = true; Local = this;
            GameEvents.ShowToast(Settings.TouchControlsActive ? "Stick: fly · JUMP: up · CROUCH: down · INTERACT: get out" : "Space up · Ctrl/C down · W/S forward · A/D turn · E get out");
        }

        public void TakeDamage(DamageInfo info)
        {
            if (NetState.IsClient || health <= 0f) return;
            health -= info.Amount * (info.Type == DamageType.Explosion ? 3f : 1f);
            if (health <= 0f) { if (Local == this) Leave(); Explosion.Create(transform.position + Vector3.up * 1.4f, 8f, 300f, this, 2f); Destroy(gameObject, 0.05f); }
        }

        void Leave()
        {
            var p = PlayerController.Instance;
            driven = false; Local = null;
            if (p != null) p.Teleport(transform.position + transform.right * -2.5f + Vector3.up * 0.5f);
        }

        void OnDestroy() { if (Local == this) Local = null; }

        public void DriveTick(PlayerController p, InputFrame f, float dt)
        {
            if (this == null) { Local = null; return; }
            if (f.Use) { Leave(); return; }
            input = f.Move;
            lift = (f.JumpHeld ? 1f : 0f) - (f.Crouch ? 1f : 0f);
            if (VRManager.Active) { input = VRManager.Left.Stick; lift = VRManager.Right.Trigger - VRManager.Left.Trigger; }
            if (p.CC.enabled) p.CC.enabled = false;
            p.transform.SetPositionAndRotation(seat.position - Vector3.up * 0.9f, Quaternion.Euler(0, transform.eulerAngles.y, 0));
            if (VRManager.Active) return;
            orbit = Mathf.Clamp(Mathf.Lerp(orbit, 0f, dt * 1.2f) + f.Look.x * 0.4f, -150f, 150f);
            var target = transform.position + Vector3.up * 1.6f;
            var cp = target - Quaternion.Euler(14f, transform.eulerAngles.y + orbit, 0) * Vector3.forward * 11f + Vector3.up * 2f;
            p.CamPivot.position = Vector3.Lerp(p.CamPivot.position, cp, 1f - Mathf.Exp(-8f * dt));
            p.CamPivot.rotation = Quaternion.LookRotation(target - p.CamPivot.position);
            p.Cam.transform.localPosition = Vector3.zero; p.Cam.transform.localRotation = Quaternion.identity;
        }

        void FixedUpdate()
        {
            float target = driven ? 1f : 0f;
            spin = Mathf.MoveTowards(spin, target, Time.fixedDeltaTime * 0.35f);
            if (NetState.IsClient || rb == null) return;
            if (spin > 0.6f)
            {
                float hover = rb.mass * -Physics.gravity.y;
                float up = hover * (spin - 0.6f) / 0.4f * (1f + lift * 0.5f);
                if (!driven) up *= 0.97f;
                rb.AddForce(Vector3.up * up);
                var flatF = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                rb.AddForce(flatF * input.y * rb.mass * 9f);
                rb.AddTorque(Vector3.up * input.x * rb.mass * 2.2f);
                // Tilt with the stick, and keep it upright.
                var wantUp = Quaternion.AngleAxis(input.y * 14f, transform.right) * Quaternion.AngleAxis(-input.x * 8f, transform.forward) * Vector3.up;
                var axis = Vector3.Cross(transform.up, wantUp);
                rb.AddTorque(axis * rb.mass * 6f - rb.angularVelocity * rb.mass * 0.5f);
            }
        }

        void Update()
        {
            if (rotor != null) rotor.Rotate(0, spin * 1500f * Time.deltaTime, 0, Space.Self);
            if (tailRotor != null) tailRotor.Rotate(spin * 2200f * Time.deltaTime, 0, 0, Space.Self);
            if (engine != null) { engine.volume = spin * 0.8f * Settings.Data.sfxVolume * Settings.Data.masterVolume; engine.pitch = 0.5f + spin * 0.4f; }
            if (spin > 0.5f && Time.time > nextCut && !NetState.IsClient) { nextCut = Time.time + 0.08f; Cut(); }
        }

        void Cut()
        {
            var c = rotor.position;
            foreach (var col in Physics.OverlapCapsule(c + Vector3.down * 0.15f, c + Vector3.up * 0.15f, 4.6f, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                if (col.transform.IsChildOf(transform)) continue;
                var p = col.ClosestPoint(c);
                if (Mathf.Abs(p.y - c.y) > 0.35f) continue;
                var part = col.GetComponent<BodyPart>();
                var dir = (p - c).normalized;
                if (part != null && part.Doll != null)
                {
                    Machines.Hurt(part, 90f, DamageType.Slash, dir, this, "Helicopter rotor");
                    if (part.CanSever && part.Doll.Dismember != null) part.Doll.Dismember.Sever(part, p, Vector3.up, true);
                    else if (part.Gibbable && part.Doll.Dismember != null && Random.value < 0.4f) part.Doll.Dismember.Gib(part, dir);
                    BloodFX.Spurt(p, dir + Vector3.down * 0.3f, 3f);
                    AudioManager.Play("slice", p, 1f);
                }
                else if (col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic)
                {
                    col.attachedRigidbody.AddForce(dir * 12f + Vector3.down * 4f, ForceMode.VelocityChange);
                    FX.Sparks(p, -dir, 10, 7f);
                    AudioManager.Play("metal_impact", p, 0.7f);
                }
            }
        }
    }
}
