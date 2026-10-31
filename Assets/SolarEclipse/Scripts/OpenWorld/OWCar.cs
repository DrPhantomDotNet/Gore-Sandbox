using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// A drivable car (Open World). Real wheel physics, 12 models (OWItems cars), paint / neon / nitro / armour,
    /// health (shoot it enough and it blows up), sirens on the police car, horns, an engine that revs with speed.
    /// Cars are networked props (def id "car|model|paint|neon"): the host simulates them; a client who drives one
    /// sends its stick input to the host (OWNet) and sees the synced result. Run Goredolls over: the body parts take
    /// impact damage from the collision like any other hit.
    /// </summary>
    public sealed class OWCar : MonoBehaviour, IInteractable, IDamageable
    {
        public static readonly List<OWCar> All = new List<OWCar>();
        /// <summary>The car the local player is driving (null on foot).</summary>
        public static OWCar LocalDriving;

        public OWItem Model;
        public string DefId = "";
        public byte DriverId = 255;          // NetPlayer id (SP / host's own id when the host drives), 255 = nobody
        public byte OwnerId = 255;           // who called it (one called car per player)
        public float Health, MaxHealth;
        public bool Wrecked;
        public float Throttle, Steer;
        public bool Brake, Boost;
        public bool Nitro, Armor, AirHorn;

        Rigidbody rb;
        readonly WheelCollider[] wheels = new WheelCollider[4];
        readonly Transform[] wheelVis = new Transform[4];
        Transform seat, sirenL, sirenR;
        AudioSource engine;
        Light neon;
        float flipTimer, sirenT, wreckAt;
        Vector3 lastPos;
        bool proxy;

        public Vector3 Seat => seat != null ? seat.position : transform.position + Vector3.up;
        public float Speed => rb != null && !rb.isKinematic ? rb.velocity.magnitude : (transform.position - lastPos).magnitude / Mathf.Max(Time.deltaTime, 0.001f);
        public bool IsPolice => Model != null && Model.Variant == 6;
        public string Prompt => Wrecked ? "" : DriverId != 255 ? "" : "Drive the " + (Model != null ? Model.Name : "car");
        public bool IsAlive => !Wrecked;

        // body: length, width, height, cabin length, cabin height, cabin offset, wheel radius, ride height
        static readonly float[,] Dim =
        {
            { 3.6f, 1.7f, 0.7f, 2.0f, 0.55f, -0.2f, 0.33f, 0.25f }, // hatch
            { 4.4f, 1.8f, 0.65f, 2.3f, 0.5f, -0.2f, 0.34f, 0.25f }, // sedan
            { 4.7f, 1.9f, 0.6f, 2.0f, 0.45f, -0.4f, 0.36f, 0.22f }, // muscle
            { 4.4f, 1.95f, 0.5f, 1.8f, 0.38f, -0.3f, 0.35f, 0.18f }, // sports
            { 4.7f, 2.0f, 0.9f, 2.6f, 0.65f, -0.3f, 0.42f, 0.35f }, // suv
            { 5.2f, 2.0f, 0.8f, 1.8f, 0.65f, 0.6f, 0.42f, 0.35f },  // pickup
            { 4.6f, 1.85f, 0.65f, 2.3f, 0.5f, -0.2f, 0.35f, 0.25f }, // police
            { 7.0f, 1.9f, 0.65f, 5.0f, 0.5f, -0.3f, 0.35f, 0.25f }, // limo
            { 4.5f, 2.0f, 0.45f, 1.6f, 0.35f, -0.1f, 0.36f, 0.15f }, // super
            { 5.0f, 2.0f, 1.5f, 1.2f, 0.4f, 1.6f, 0.38f, 0.3f },    // van
            { 4.8f, 2.4f, 1.0f, 2.2f, 0.7f, -0.3f, 0.9f, 0.6f },    // monster
            { 2.1f, 0.5f, 0.55f, 0f, 0f, 0f, 0.32f, 0.25f },        // bike
        };

        // ------------------------------------------------------------------ building

        public static string DefFor(string model, string paint, string neon, bool nitro, bool armor, bool horn)
            => "car|" + model + "|" + (paint ?? "") + "|" + (neon ?? "") + "|" + (nitro ? "n" : "") + (armor ? "a" : "") + (horn ? "h" : "");

        /// <summary>PropFactory.Spawn hook for "car|..." ids (host and clients build the same car).</summary>
        public static GameObject Create(string defId, Vector3 pos, Quaternion rot)
        {
            var p = defId.Split('|');
            var model = OWItems.Get(p.Length > 1 ? p[1] : "") ?? OWItems.Get("c_sedan");
            var paint = p.Length > 2 ? OWItems.Get(p[2]) : null;
            var neonIt = p.Length > 3 ? OWItems.Get(p[3]) : null;
            string flags = p.Length > 4 ? p[4] : "";
            int body = Mathf.Clamp(model.Variant, 0, Dim.GetLength(0) - 1);
            float L = Dim[body, 0], Wd = Dim[body, 1], Hb = Dim[body, 2], CL = Dim[body, 3], CH = Dim[body, 4], CO = Dim[body, 5], WR = Dim[body, 6], RH = Dim[body, 7];
            var col = paint != null ? paint.Color : model.Color;

            var go = new GameObject("Car " + model.Name);
            go.layer = Layers.Props;
            go.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var car = go.AddComponent<OWCar>();
            car.Model = model; car.DefId = defId;
            car.Nitro = flags.Contains("n"); car.Armor = flags.Contains("a"); car.AirHorn = flags.Contains("h");
            car.MaxHealth = car.Health = 900f * model.Health;

            float bodyY = RH + WR + Hb / 2f;
            var mb = new MeshBuilder { Color = col, Gloss = paint != null && (paint.Id.Contains("chrome") || paint.Id.Contains("gold")) ? 0.95f : 0.6f };
            if (body == 11)
            {
                mb.BevelBox(new Vector3(0, bodyY, 0), new Vector3(0.35f, 0.4f, 1.4f), 0.08f);
                mb.SetColor(new Color(0.1f, 0.1f, 0.1f), 0.3f);
                mb.Box(new Vector3(0, bodyY + 0.25f, -0.25f), new Vector3(0.3f, 0.1f, 0.6f));      // seat
                mb.Cylinder(new Vector3(0, bodyY + 0.2f, 0.55f), new Vector3(0, bodyY + 0.55f, 0.7f), 0.03f, 0.03f, 6); // bars
                mb.Box(new Vector3(0, bodyY + 0.55f, 0.7f), new Vector3(0.7f, 0.04f, 0.04f));
            }
            else
            {
                mb.BevelBox(new Vector3(0, bodyY, 0), new Vector3(Wd, Hb, L), 0.1f);
                if (body == 5) { mb.SetColor(col * 0.7f, 0.3f); mb.Box(new Vector3(0, bodyY + Hb / 2f + 0.25f, -L * 0.25f), new Vector3(Wd, 0.5f, L * 0.45f)); } // pickup bed walls
                mb.SetColor(new Color(0.08f, 0.1f, 0.13f), 0.95f);
                mb.BevelBox(new Vector3(0, bodyY + Hb / 2f + CH / 2f, CO), new Vector3(Wd * 0.9f, CH, CL), 0.08f); // glass cabin
                mb.SetColor(col, 0.6f);
                mb.Box(new Vector3(0, bodyY + Hb / 2f + CH + 0.02f, CO), new Vector3(Wd * 0.88f, 0.05f, CL * 0.85f)); // roof
                mb.SetColor(new Color(0.06f, 0.06f, 0.06f), 0.3f);
                mb.Box(new Vector3(0, RH + WR * 0.9f, L / 2f + 0.03f), new Vector3(Wd * 0.95f, 0.18f, 0.08f)); // bumpers
                mb.Box(new Vector3(0, RH + WR * 0.9f, -L / 2f - 0.03f), new Vector3(Wd * 0.95f, 0.18f, 0.08f));
                if (body == 8) { mb.SetColor(col * 0.6f, 0.6f); mb.Box(new Vector3(0, bodyY + Hb / 2f + 0.25f, -L / 2f + 0.2f), new Vector3(Wd, 0.06f, 0.35f)); } // spoiler
            }
            mb.SetColor(new Color(1f, 0.95f, 0.8f), 0.9f);
            mb.Box(new Vector3(-Wd * 0.32f, bodyY, L / 2f + 0.01f), new Vector3(0.3f, 0.14f, 0.04f));
            mb.Box(new Vector3(Wd * 0.32f, bodyY, L / 2f + 0.01f), new Vector3(0.3f, 0.14f, 0.04f));
            mb.SetColor(new Color(0.9f, 0.05f, 0.05f), 0.9f);
            mb.Box(new Vector3(-Wd * 0.32f, bodyY, -L / 2f - 0.01f), new Vector3(0.3f, 0.12f, 0.04f));
            mb.Box(new Vector3(Wd * 0.32f, bodyY, -L / 2f - 0.01f), new Vector3(0.3f, 0.12f, 0.04f));
            var bodyGo = new GameObject("Body");
            bodyGo.layer = Layers.Props;
            bodyGo.transform.SetParent(go.transform, false);
            bodyGo.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Car_" + model.Id);
            bodyGo.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;

            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, bodyY + (body == 11 ? 0.1f : CH * 0.3f), 0);
            bc.size = new Vector3(Mathf.Max(Wd, 0.5f), Hb + (body == 11 ? 0.4f : CH * 0.8f), L);

            if (body == 6)
            {
                car.sirenL = Light(go.transform, "Siren L", new Vector3(-0.3f, bodyY + Hb / 2f + CH + 0.12f, CO), new Color(1f, 0.1f, 0.1f));
                car.sirenR = Light(go.transform, "Siren R", new Vector3(0.3f, bodyY + Hb / 2f + CH + 0.12f, CO), new Color(0.1f, 0.3f, 1f));
            }

            // Wheels
            float wx = Mathf.Max(Wd / 2f - WR * 0.3f, 0.12f), wz = L / 2f - WR - 0.25f;
            var wm = new MeshBuilder { Color = new Color(0.07f, 0.07f, 0.07f), Gloss = 0.2f };
            float tw = body == 10 ? 0.6f : body == 11 ? 0.12f : 0.25f;
            wm.Cylinder(Vector3.left * tw / 2f, Vector3.right * tw / 2f, WR, WR, 16);
            wm.SetColor(new Color(0.7f, 0.7f, 0.72f), 0.8f);
            wm.Cylinder(Vector3.left * (tw / 2f + 0.01f), Vector3.right * (tw / 2f + 0.01f), WR * 0.55f, WR * 0.55f, 12);
            var wheelMesh = wm.ToMesh("Wheel_" + body);
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2, left = i % 2 == 0;
                var local = new Vector3(body == 11 ? (left ? -0.06f : 0.06f) : left ? -wx : wx, RH + WR, front ? wz : -wz);
                var wgo = new GameObject("Wheel" + i);
                wgo.layer = Layers.Props;
                wgo.transform.SetParent(go.transform, false);
                wgo.transform.localPosition = local + Vector3.up * 0.2f;
                var wc = wgo.AddComponent<WheelCollider>();
                wc.radius = WR; wc.suspensionDistance = body == 10 ? 0.5f : 0.25f; wc.mass = 25f;
                wc.center = Vector3.zero;
                var spring = wc.suspensionSpring; spring.spring = 42000f * model.Mass; spring.damper = 4500f * model.Mass; spring.targetPosition = 0.45f; wc.suspensionSpring = spring;
                var ff = wc.forwardFriction; ff.stiffness = 1.6f * model.Grip; wc.forwardFriction = ff;
                var sf = wc.sidewaysFriction; sf.stiffness = 1.8f * model.Grip; wc.sidewaysFriction = sf;
                car.wheels[i] = wc;
                if (body == 11 && !left) continue; // a bike shows one front and one back wheel
                var vis = new GameObject("WheelMesh" + i);
                vis.layer = Layers.Props;
                vis.transform.SetParent(go.transform, false);
                vis.AddComponent<MeshFilter>().sharedMesh = wheelMesh;
                vis.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                car.wheelVis[i] = vis.transform;
            }
            car.seat = new GameObject("Seat").transform;
            car.seat.SetParent(go.transform, false);
            car.seat.localPosition = new Vector3(body == 11 ? 0f : -Wd * 0.22f, bodyY + 0.1f, body == 11 ? -0.2f : CO + 0.15f);

            if (neonIt != null)
            {
                car.neon = new GameObject("Neon").AddComponent<Light>();
                car.neon.transform.SetParent(go.transform, false);
                car.neon.transform.localPosition = new Vector3(0, RH * 0.5f, 0);
                car.neon.type = LightType.Point; car.neon.color = neonIt.Color; car.neon.range = Mathf.Max(L, 4f); car.neon.intensity = 2.5f; car.neon.shadows = LightShadows.None;
            }

            var rbd = go.AddComponent<Rigidbody>();
            rbd.mass = 1300f * model.Mass;
            rbd.interpolation = RigidbodyInterpolation.Interpolate;
            rbd.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rbd.centerOfMass = new Vector3(0, RH + WR * 0.6f, 0.05f);
            rbd.angularDrag = 0.6f;
            car.rb = rbd;
            go.AddComponent<Surface>().Type = SurfaceType.Metal;
            go.AddComponent<ImpactSounds>();

            car.engine = go.AddComponent<AudioSource>();
            car.engine.clip = SoundBank.Get("car_engine");
            car.engine.loop = true; car.engine.spatialBlend = 1f; car.engine.minDistance = 4f; car.engine.maxDistance = 60f; car.engine.volume = 0f;
            if (car.engine.clip != null) car.engine.Play();
            return go;
        }

        static Transform Light(Transform parent, string name, Vector3 pos, Color c)
        {
            var g = new GameObject(name);
            g.layer = Layers.Props;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            var mb = new MeshBuilder { Color = c };
            mb.Box(Vector3.zero, new Vector3(0.35f, 0.12f, 0.2f));
            g.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Siren");
            g.AddComponent<MeshRenderer>().sharedMaterial = Mats.Glow(c, 3f);
            return g.transform;
        }

        void OnEnable() { All.Add(this); lastPos = transform.position; }
        void OnDisable() { All.Remove(this); if (LocalDriving == this) LocalDriving = null; }

        // ------------------------------------------------------------------ physics (authority only)

        void FixedUpdate()
        {
            if (rb == null) return;
            proxy = rb.isKinematic;
            if (proxy || Wrecked) { if (Wrecked) foreach (var w in wheels) if (w != null) { w.motorTorque = 0; w.brakeTorque = 3000f; } return; }
            bool driven = DriverId != 255;
            float fwdSpeed = Vector3.Dot(rb.velocity, transform.forward);
            float cap = 30f * Model.Speed;
            float torque = 2200f * Model.Accel * Model.Mass;
            float t = driven ? Throttle : 0f;
            bool reversing = t < -0.1f && fwdSpeed < 1f;
            bool braking = (driven && Brake) || (t < -0.1f && fwdSpeed > 1f) || (t > 0.1f && fwdSpeed < -1f) || (!driven && Mathf.Abs(fwdSpeed) > 0.1f);
            float steerMax = Mathf.Lerp(34f, 9f, Mathf.Clamp01(Mathf.Abs(fwdSpeed) / cap));
            for (int i = 0; i < 4; i++)
            {
                var w = wheels[i];
                if (w == null) continue;
                bool front = i < 2;
                if (front) w.steerAngle = (driven ? Steer : 0f) * steerMax;
                float motor = (Mathf.Abs(fwdSpeed) < cap || Mathf.Sign(t) != Mathf.Sign(fwdSpeed)) ? t * torque * (reversing ? 0.5f : 1f) : 0f;
                w.motorTorque = front && Model.Variant != 10 ? motor * 0.3f : motor; // mostly rear-wheel drive; monster truck AWD
                w.brakeTorque = braking ? (driven && Brake ? 5000f : 2500f) : 0f;
            }
            if (driven && Boost && Nitro) rb.AddForce(transform.forward * 9f, ForceMode.Acceleration);
            rb.AddForce(-transform.up * Mathf.Abs(fwdSpeed) * 25f * Model.Mass); // downforce
            // Flipped and slow for a while: put it back on its wheels.
            if (transform.up.y < 0.25f && rb.velocity.magnitude < 2f) { flipTimer += Time.fixedDeltaTime; if (flipTimer > 2.5f) { flipTimer = 0f; transform.SetPositionAndRotation(transform.position + Vector3.up * 1.5f, Quaternion.Euler(0, transform.eulerAngles.y, 0)); rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; } }
            else flipTimer = 0f;
        }

        void Update()
        {
            float spd = Speed;
            lastPos = transform.position;
            for (int i = 0; i < 4; i++)
            {
                if (wheelVis[i] == null || wheels[i] == null) continue;
                if (!proxy) { wheels[i].GetWorldPose(out var p, out var q); wheelVis[i].SetPositionAndRotation(p, q); }
                else
                {
                    var wc = wheels[i];
                    wheelVis[i].position = wc.transform.position - transform.up * 0.2f;
                    wheelVis[i].rotation = transform.rotation * Quaternion.Euler(Time.time * spd * 60f, i < 2 ? Steer * 25f : 0f, 0);
                }
            }
            if (engine != null)
            {
                bool driven = DriverId != 255 && !Wrecked;
                engine.volume = Mathf.MoveTowards(engine.volume, driven ? 0.45f * Settings.Data.sfxVolume * Settings.Data.masterVolume : 0f, Time.deltaTime);
                engine.pitch = 0.7f + Mathf.Clamp01(spd / (30f * Model.Speed)) * 1.3f + (Boost && Nitro ? 0.3f : 0f);
            }
            if (sirenL != null)
            {
                sirenT += Time.deltaTime;
                bool on = DriverId != 255;
                bool a = on && Mathf.Repeat(sirenT, 0.5f) < 0.25f;
                sirenL.gameObject.SetActive(!on || a); sirenR.gameObject.SetActive(!on || !a);
            }
            if (Wrecked && wreckAt > 0f && Time.time > wreckAt && !NetState.IsClient) Destroy(gameObject);
        }

        // ------------------------------------------------------------------ use / damage

        public void Interact(GameObject user) { if (!Wrecked && DriverId == 255) OWDirector.RequestDrive(this); }

        public void TakeDamage(DamageInfo info)
        {
            if (Wrecked || NetState.IsClient) return;
            float dmg = info.Amount * (Armor ? 0.5f : 1f) * (info.Type == DamageType.Explosion ? 3f : 1f);
            Health -= dmg;
            if (info.Force.sqrMagnitude > 0 && rb != null && !rb.isKinematic) rb.AddForceAtPosition(info.Force * 0.3f, info.Point, ForceMode.Impulse);
            if (Health <= 0f) Explode();
        }

        public void Explode()
        {
            if (Wrecked) return;
            Wrecked = true;
            Health = 0f;
            Explosion.Create(transform.position + Vector3.up, 6f, 140f, this, 1.4f);
            foreach (var r in GetComponentsInChildren<MeshRenderer>()) foreach (var m in r.materials) m.color = new Color(0.08f, 0.07f, 0.06f);
            if (neon != null) neon.enabled = false;
            if (rb != null && !rb.isKinematic) rb.AddForce(Vector3.up * 6f, ForceMode.VelocityChange);
            OWDirector.OnCarWrecked(this);
            wreckAt = Time.time + 25f;
        }

        // ------------------------------------------------------------------ driving (local player)

        /// <summary>Called by PlayerController every frame while you drive: input, seat, chase camera, exit.</summary>
        public void DriveTick(PlayerController p, InputFrame f, float dt)
        {
            if (Wrecked || this == null) { OWDirector.LeaveCar(); return; }
            float throttle = f.Move.y, steer = f.Move.x;
            if (VRManager.Active) { throttle = VRManager.Right.Trigger - VRManager.Left.Trigger; steer = VRManager.Left.Stick.x; }
            bool brake = f.JumpHeld, boost = f.Sprint;
            OWDirector.SendCarInput(this, throttle, steer, brake, boost);
            if (f.Use) { OWDirector.LeaveCar(); return; }
            if ((Input.GetKeyDown(KeyCode.H) || f.Kick) && !Settings.TouchControlsActive || (Settings.TouchControlsActive && f.Kick)) Honk();
            // Sit in the seat.
            if (p.CC.enabled) p.CC.enabled = false;
            p.transform.SetPositionAndRotation(Seat - Vector3.up * 0.9f, Quaternion.Euler(0, transform.eulerAngles.y, 0));
            if (VRManager.Active) return;
            // Chase camera (mouse / right side of the screen orbits a little).
            orbit = Mathf.Lerp(orbit, 0f, dt * 1.5f) + f.Look.x * 0.4f;
            orbit = Mathf.Clamp(orbit, -120f, 120f);
            float len = Dim[Mathf.Clamp(Model.Variant, 0, 11), 0];
            var yawRot = Quaternion.Euler(12f, transform.eulerAngles.y + orbit, 0f);
            var target = transform.position + Vector3.up * 1.3f;
            var camPos = target - yawRot * Vector3.forward * (len + 4.5f) + Vector3.up * 1.2f;
            if (Physics.Linecast(target, camPos, out var hit, Layers.WorldMask, QueryTriggerInteraction.Ignore)) camPos = hit.point + (target - camPos).normalized * 0.3f;
            p.CamPivot.position = Vector3.Lerp(p.CamPivot.position, camPos, 1f - Mathf.Exp(-12f * dt));
            p.CamPivot.rotation = Quaternion.LookRotation(target - p.CamPivot.position);
            p.Cam.transform.localPosition = Vector3.zero; p.Cam.transform.localRotation = Quaternion.identity;
            p.Cam.fieldOfView = Mathf.Lerp(Settings.Data.fov, Settings.Data.fov + 14f, Mathf.Clamp01(Speed / 40f));
        }
        float orbit;

        public void Honk()
        {
            if (Time.time < honkReady) return;
            honkReady = Time.time + 0.6f;
            AudioManager.Play(AirHorn ? "air_horn" : IsPolice ? "siren" : "car_horn", transform.position, 1f, 1f, 1f, 5f, 120f);
            GameEvents.EmitNoise(transform.position, 25f, 0.2f, this);
        }
        float honkReady;

        /// <summary>Host: apply a driver's input.</summary>
        public void SetInput(float throttle, float steer, bool brake, bool boost)
        {
            Throttle = Mathf.Clamp(throttle, -1f, 1f); Steer = Mathf.Clamp(steer, -1f, 1f); Brake = brake; Boost = boost;
        }
    }
}
