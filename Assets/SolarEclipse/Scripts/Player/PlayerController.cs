using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// First-person player: walk / sprint / crouch / jump on a CharacterController, mouse look, noclip (V) that only
    /// the Boundaries can stop, fall damage, footsteps per surface, kicks and dropkicks (F), use / inspect wounds (E),
    /// pushing physics objects, camera bob / shake / recoil / FOV. Owns the inventory and viewmodel.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour, IPlayerTarget
    {
        public static PlayerController Instance { get; private set; }

        public CharacterController CC;
        public PlayerHealth Health;
        public Inventory Inventory;
        public Camera Cam, ViewCam;
        public Transform CamPivot;
        public Viewmodel Viewmodel;
        public string LookHint { get; private set; }
        public bool Noclip { get; private set; }
        public bool Grounded { get; private set; }
        public float Speed { get; private set; }
        public bool LockLook;

        const float StandHeight = 1.8f, CrouchHeight = 1.15f, EyeStand = 1.64f, EyeCrouch = 1.02f;
        float yaw, pitch, eye = EyeStand, bob, stepAccum, maxFall, recoverPitch, fovCurrent, deathTilt, kickCooldown;
        Vector3 velocity, spawnPos, lastPos;
        Vector2 recoil;
        float spawnYaw;
        bool crouching;

        public Vector3 EyePosition => Cam != null ? Cam.transform.position : transform.position + Vector3.up * EyeStand;
        public Vector3 AimDirection => Cam != null ? Cam.transform.forward : transform.forward;
        public bool HoldingGun => Inventory != null && Inventory.Current is Firearm;
        public bool Crouching => crouching;

        public static PlayerController Create(Vector3 position, float yawDeg, Transform parent)
        {
            var go = new GameObject("Player");
            go.layer = Layers.Player;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yawDeg, 0));
            var p = go.AddComponent<PlayerController>();
            p.spawnPos = position; p.spawnYaw = yawDeg; p.yaw = yawDeg;
            p.CC = go.AddComponent<CharacterController>();
            p.CC.height = StandHeight; p.CC.radius = 0.32f; p.CC.center = Vector3.up * StandHeight / 2f;
            p.CC.stepOffset = 0.42f; p.CC.slopeLimit = 48f; p.CC.skinWidth = 0.04f; p.CC.minMoveDistance = 0f;
            p.Health = go.AddComponent<PlayerHealth>();

            p.CamPivot = new GameObject("CameraPivot").transform;
            p.CamPivot.SetParent(go.transform, false);
            p.CamPivot.localPosition = Vector3.up * EyeStand;
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(p.CamPivot, false);
            p.Cam = camGo.AddComponent<Camera>();
            p.Cam.nearClipPlane = 0.03f;
            p.Cam.farClipPlane = Settings.Data.renderDistance;
            p.Cam.cullingMask = ~(1 << Layers.Viewmodel) & ~(1 << 31);
            p.Cam.depth = 0;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraOverlays>();
            var vmGo = new GameObject("Viewmodel Camera");
            vmGo.transform.SetParent(camGo.transform, false);
            p.ViewCam = vmGo.AddComponent<Camera>();
            p.ViewCam.clearFlags = CameraClearFlags.Depth;
            p.ViewCam.cullingMask = 1 << Layers.Viewmodel;
            p.ViewCam.depth = 1;
            p.ViewCam.nearClipPlane = 0.01f;
            p.ViewCam.farClipPlane = 4f;
            p.Viewmodel = Viewmodel.Create(vmGo.transform, p);
            p.Inventory = go.AddComponent<Inventory>();
            p.Inventory.Init(p);
            p.fovCurrent = Settings.Data.fov;
            Instance = p;
            return p;
        }

        void OnEnable() { if (!PlayerTargets.All.Contains(this)) PlayerTargets.All.Add(this); }
        void OnDisable() { PlayerTargets.All.Remove(this); }
        void OnDestroy() { if (Instance == this) Instance = null; PlayerTargets.All.Remove(this); }

        // IPlayerTarget (lets Goredolls and nextbots treat every player the same way)
        public Vector3 Position => transform.position;
        public bool IsDead => Health == null || Health.Dead;
        public Transform Root => transform;
        public IDamageable Damageable => Health;
        public string PlayerName => NetManager.I != null && NetState.Active ? NetManager.I.LocalName : "You";

        public void Teleport(Vector3 pos, float? yawDeg = null)
        {
            CC.enabled = false;
            transform.position = pos;
            CC.enabled = true;
            velocity = Vector3.zero;
            maxFall = 0;
            if (yawDeg.HasValue) yaw = yawDeg.Value;
        }

        public void Launch(Vector3 v) { velocity = v; Grounded = false; maxFall = 0; }

        public void SetNoclip(bool on)
        {
            if (on && !Rules.NoclipAllowed) { GameEvents.ShowToast("Noclip is disabled in the spawn menu options."); return; }
            if (on && !NetState.LocalCreative) { GameEvents.ShowToast("Noclip needs Creative mode — ask the host (Esc > Players)."); AudioManager.Ui("ui_error"); return; }
            Noclip = on;
            gameObject.layer = on ? Layers.PlayerNoclip : Layers.Player;
            velocity = Vector3.zero;
            maxFall = 0;
            AudioManager.Play2D(on ? "noclip_on" : "noclip_off", 0.6f, 1f, AudioBus.Sfx);
            Stats.Add(Stats.Noclips);
            GameEvents.ShowToast(on ? "Noclip ON (V) — only the Boundaries can stop you" : "Noclip OFF");
        }

        CapsuleCollider localHitbox;

        /// <summary>Multiplayer: a raycast-only body so other players' shots (simulated on the host) can hit us.</summary>
        public void AddLocalHitbox()
        {
            if (localHitbox != null) return;
            var go = new GameObject("LocalHitbox");
            go.layer = Layers.LocalHitbox;
            go.transform.SetParent(transform, false);
            localHitbox = go.AddComponent<CapsuleCollider>();
            localHitbox.radius = 0.3f;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            Physics.IgnoreCollision(localHitbox, CC, true);
        }

        void Update()
        {
            // Everything the local player does this frame is "theirs" (clients turn it into requests to the host).
            using (NetState.Local()) Tick();
            if (localHitbox != null)
            {
                localHitbox.height = CC.height;
                localHitbox.center = CC.center;
                localHitbox.enabled = !Health.Dead && !Noclip;
            }
        }

        void Tick()
        {
            PlayerInput.Update();
            var f = PlayerInput.Frame;
            float dt = Time.deltaTime * BulletTime.PlayerDtScale; // bullet time: the player stays full speed
            CameraFX.ListenerPosition = EyePosition;

            if (Health.Dead)
            {
                deathTilt = Mathf.MoveTowards(deathTilt, 1f, dt * 1.5f);
                if (Time.time > Health.RespawnAt) Respawn();
                UpdateCamera(dt, f);
                return;
            }
            deathTilt = Mathf.MoveTowards(deathTilt, 0f, dt * 4f);

            if (!LockLook)
            {
                float sens = Inventory.Current != null && Inventory.Current.IsAiming ? Settings.Data.adsSensitivity : 1f;
                yaw += f.Look.x * sens;
                pitch = Mathf.Clamp(pitch - f.Look.y * sens, -88f, 88f);
            }
            if (f.Noclip) SetNoclip(!Noclip);
            if (Noclip) MoveNoclip(f, dt); else Move(f, dt);

            if (f.Use) Use();
            if (f.Kick && !Inventory.ConsumesKick) Kick();
            kickCooldown -= dt;
            Inventory.Tick(f, dt);
            UpdateLookHint();
            UpdateCamera(dt, f);

            var flat = MathX.Flat(transform.position - lastPos).magnitude;
            if (flat < 5f) Stats.Add(Stats.Distance, flat);
            lastPos = transform.position;
            if (WorldRoot.Current != null && transform.position.y < WorldRoot.Current.KillY) Respawn();
        }

        void Move(InputFrame f, float dt)
        {
            // Crouch with head-room check.
            bool wantCrouch = f.Crouch;
            if (!wantCrouch && crouching && Physics.CheckCapsule(transform.position + Vector3.up * 0.5f, transform.position + Vector3.up * (StandHeight - 0.3f), 0.3f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                wantCrouch = true;
            if (wantCrouch != crouching)
            {
                crouching = wantCrouch;
                CC.height = crouching ? CrouchHeight : StandHeight;
                CC.center = Vector3.up * CC.height / 2f;
            }

            var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            var wish = (fwd * f.Move.y + right * f.Move.x);
            bool aiming = Inventory.Current != null && Inventory.Current.IsAiming;
            bool sprint = f.Sprint && f.Move.y > 0.1f && !crouching && !aiming;
            float speed = (crouching ? 2.2f : sprint ? 7.2f : aiming ? 3f : 4.4f) * Rules.PlayerSpeedMultiplier
                          * (Inventory.Current != null ? Inventory.Current.MoveSpeedMul : 1f);
            var target = wish * speed;
            float accel = Grounded ? 55f : 9f;
            var horiz = new Vector3(velocity.x, 0, velocity.z);
            if (Grounded || wish.sqrMagnitude > 0.01f) horiz = Vector3.MoveTowards(horiz, target, accel * dt);
            velocity.x = horiz.x; velocity.z = horiz.z;

            if (Grounded)
            {
                if (velocity.y < -2f) velocity.y = -2f;
                if (f.Jump)
                {
                    velocity.y = 5.4f * Rules.JumpMultiplier;
                    Grounded = false;
                    AudioManager.Play("cloth", transform.position, 0.4f);
                }
            }
            velocity.y += Physics.gravity.y * dt;
            if (!Grounded) maxFall = Mathf.Max(maxFall, -velocity.y);

            var flags = CC.Move(velocity * dt);
            bool wasGrounded = Grounded;
            Grounded = (flags & CollisionFlags.Below) != 0;
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0) velocity.y = 0;
            Speed = MathX.Flat(CC.velocity).magnitude;
            Sprinting = sprint && Speed > 5f;

            if (Grounded && !wasGrounded) Land();
            if (Grounded)
            {
                stepAccum += Speed * dt;
                float stride = sprint ? 2.4f : crouching ? 1.4f : 2.0f;
                if (stepAccum > stride && Speed > 0.5f) { stepAccum = 0; Footstep(sprint); }
            }
        }

        public bool Sprinting { get; private set; }

        void MoveNoclip(InputFrame f, float dt)
        {
            var camRot = Quaternion.Euler(pitch, yaw, 0);
            var wish = camRot * new Vector3(f.Move.x, 0, f.Move.y);
            if (f.JumpHeld) wish += Vector3.up;
            if (f.Crouch) wish += Vector3.down;
            float speed = (f.Sprint ? 26f : 9f) * Rules.PlayerSpeedMultiplier;
            velocity = Vector3.Lerp(velocity, wish * speed, 1f - Mathf.Exp(-10f * dt));
            CC.Move(velocity * dt);
            Grounded = false;
            maxFall = 0;
            Speed = velocity.magnitude;
        }

        void Land()
        {
            float v = maxFall;
            maxFall = 0;
            if (v > 3f)
            {
                AudioManager.Play("land", transform.position, Mathf.Clamp01(v / 12f));
                CameraFX.Shake(Mathf.Clamp01((v - 3f) / 15f) * 0.6f);
                CameraFX.Punch(Mathf.Clamp(v * 0.4f, 0f, 6f), 0);
            }
            if (v > 12f)
            {
                Health.TakeDamage(new DamageInfo { Amount = (v - 12f) * 9f, Type = DamageType.Fall, Point = transform.position, Direction = Vector3.down, Weapon = "Fall" });
                AudioManager.Play("bone_crack", transform.position, Mathf.Clamp01((v - 12f) / 8f));
            }
        }

        void Footstep(bool sprint)
        {
            string clip = "step_concrete";
            if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out var hit, 0.6f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                switch (Surface.Of(hit.collider))
                {
                    case SurfaceType.Grass: case SurfaceType.Dirt: clip = "step_grass"; break;
                    case SurfaceType.Metal: clip = "step_metal"; break;
                    case SurfaceType.Wood: clip = "step_wood"; break;
                    case SurfaceType.Carpet: clip = "step_carpet"; break;
                }
            }
            AudioManager.Play(clip, transform.position, crouching ? 0.25f : sprint ? 0.8f : 0.5f, Random.Range(0.9f, 1.1f), 0.6f);
            GameEvents.EmitNoise(transform.position, crouching ? 2f : sprint ? 12f : 6f, 0f, this);
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var rb = hit.collider.attachedRigidbody;
            if (rb == null || hit.moveDirection.y < -0.4f) return;
            var push = new Vector3(hit.moveDirection.x, 0.05f, hit.moveDirection.z) * Mathf.Max(1.5f, Speed) * 6f;
            if (NetState.IsClient && NetManager.Handle(rb, out _, out _))
            {
                // Host-owned object: send a (rate limited) push.
                if (Time.time > nextNetPush) { nextNetPush = Time.time + 0.1f; using (NetState.Local()) NetManager.I.RequestImpulse(rb, push * 0.1f, hit.point); }
                return;
            }
            if (rb.isKinematic) return;
            rb.AddForceAtPosition(push, hit.point, ForceMode.Force);
            var part = hit.collider.GetComponent<BodyPart>();
            if (part != null && Speed > 5.5f && part.Doll != null) part.Doll.Ragdoll.OnImpact(Speed * 0.9f);
        }

        // ------------------------------------------------------------------ actions

        void Use()
        {
            if (!Physics.Raycast(EyePosition, AimDirection, out var hit, 3.2f, Layers.ShootMask | (1 << Layers.Trigger), QueryTriggerInteraction.Collide)) return;
            var part = hit.collider.GetComponent<BodyPart>();
            // Characters you can talk to (story NPCs) put an IInteractable on the Goredoll: talking wins over inspecting.
            var talk = part != null && part.Doll != null ? part.Doll.GetComponentInParent<IInteractable>() : null;
            if (talk != null) { talk.Interact(gameObject); return; }
            if (part != null && part.Doll != null && StoryMode.Active) return; // no wound inspector mid-mission (it would pause the fight)
            if (part != null && part.Doll != null)
            {
                UIRoot.Push(new WoundInspectorScreen(part.Doll, part));
                Stats.Add(Stats.Inspections);
                return;
            }
            var use = hit.collider.GetComponentInParent<IInteractable>();
            if (use == null) return;
            var ne = use is Component uc ? uc.GetComponentInParent<NetEntity>() : null;
            if (NetState.IsClient && ne != null) { NetManager.I.RequestInteract(ne); AudioManager.Ui("ui_click", 0.4f); return; }
            use.Interact(gameObject);
        }

        float nextNetPush;

        /// <summary>What the INTERACT/USE action would do right now ("Talk to Big Marcus", "Open door"...), or null.</summary>
        public string InteractTarget { get; private set; }
        /// <summary>True when something usable/talkable is under the crosshair (the mobile INTERACT button lights up).</summary>
        public bool CanInteract => InteractTarget != null;

        void UpdateLookHint()
        {
            LookHint = null;
            InteractTarget = null;
            if (!Physics.Raycast(EyePosition, AimDirection, out var hit, 3.2f, Layers.ShootMask | (1 << Layers.Trigger), QueryTriggerInteraction.Collide)) return;
            string key = Settings.TouchControlsActive ? "Tap INTERACT: " : "[E] ";
            var part = hit.collider.GetComponent<BodyPart>();
            var talk = part != null && part.Doll != null ? part.Doll.GetComponentInParent<IInteractable>() : null;
            if (talk != null) { InteractTarget = talk.Prompt; LookHint = key + talk.Prompt; return; }
            if (part != null && part.Doll != null && StoryMode.Active) return;
            if (part != null && part.Doll != null) { InteractTarget = "Inspect"; LookHint = key + "Inspect " + part.Doll.DisplayName + "'s wounds"; return; }
            var use = hit.collider.GetComponentInParent<IInteractable>();
            if (use != null) { InteractTarget = use.Prompt; LookHint = key + use.Prompt; }
        }

        public void Kick()
        {
            if (kickCooldown > 0) return;
            kickCooldown = 0.55f;
            bool drop = !Grounded && !Noclip;
            Viewmodel.PlayKick(drop);
            AudioManager.Play("whoosh_big", EyePosition, 0.7f);
            Stats.Add(Stats.Kicks);
            float mult = Rules.SuperKick ? 6f : 1f;
            var origin = EyePosition + Vector3.down * (drop ? 0.6f : 0.9f);
            if (Physics.SphereCast(origin, 0.25f, AimDirection, out var hit, 1.9f, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                var dir = (AimDirection + Vector3.up * 0.15f).normalized;
                float force = (drop ? 260f : 80f) * mult;
                var dmg = hit.collider.GetComponentInParent<IDamageable>();
                if (dmg != null)
                    dmg.TakeDamage(new DamageInfo { Amount = (drop ? 35f : 16f) * mult, Force = dir * force, Point = hit.point, Normal = hit.normal, Direction = dir, Type = DamageType.Blunt, Source = this, Weapon = drop ? "Dropkick" : "Kick" });
                else if (hit.rigidbody != null) NetPhysics.Impulse(hit.rigidbody, dir * force, hit.point);
                var part = hit.collider.GetComponent<BodyPart>();
                if (part != null && part.Doll != null) { if (drop || Rules.SuperKick) part.Doll.Launch(dir * (drop ? 6f : 4f) * (Rules.SuperKick ? 3f : 1f)); else part.Doll.Ragdoll.OnImpact(5f); }
                AudioManager.Play(part != null ? "punch_hit" : "kick_hit", hit.point, 1f);
                CameraFX.Shake(drop ? 0.35f : 0.15f);
                GameEvents.EmitNoise(hit.point, 10f, 0.4f, this);
                if (drop) velocity = -AimDirection * 3f + Vector3.up * 2f;
            }
        }

        public void Respawn()
        {
            if (Noclip) SetNoclip(false);
            Teleport(WorldRoot.Current != null ? WorldRoot.Current.PlayerSpawn : spawnPos, WorldRoot.Current != null ? WorldRoot.Current.PlayerYaw : spawnYaw);
            pitch = 0;
            Health.Revive();
        }

        // ------------------------------------------------------------------ camera

        void UpdateCamera(float dt, InputFrame f)
        {
            var d = Settings.Data;
            float targetEye = Health.Dead ? 0.35f : crouching ? EyeCrouch : EyeStand;
            eye = MathX.Damp(eye, targetEye, 12f, dt);
            float bobAmount = Grounded && !Noclip && d.headBob ? Mathf.Clamp01(Speed / 7f) : 0f;
            bob += dt * (5.5f + Speed * 0.9f);
            var bobOffset = new Vector3(Mathf.Sin(bob) * 0.025f, Mathf.Abs(Mathf.Cos(bob)) * 0.035f, 0) * bobAmount;
            CamPivot.localPosition = new Vector3(0, eye, 0) + bobOffset;

            // Recoil: kick now, recover most of it.
            recoil += CameraFX.Kick;
            pitch = Mathf.Clamp(pitch - CameraFX.Kick.x * 0.35f, -88f, 88f);
            yaw += CameraFX.Kick.y * 0.35f;
            CameraFX.Kick = Vector2.zero;
            recoil = Vector2.Lerp(recoil, Vector2.zero, 1f - Mathf.Exp(-9f * dt));

            CameraFX.Trauma = Mathf.MoveTowards(CameraFX.Trauma, 0f, dt * 1.4f);
            float shake = CameraFX.Trauma * CameraFX.Trauma;
            float t = Time.time * 25f;
            var shakeRot = new Vector3((Mathf.PerlinNoise(t, 1) - 0.5f), (Mathf.PerlinNoise(2, t) - 0.5f), (Mathf.PerlinNoise(t, 3) - 0.5f)) * 9f * shake;

            transform.rotation = Quaternion.Euler(0, yaw, 0);
            float roll = deathTilt * 70f + (Health.DamageFlash * 3f * Mathf.Sin(Time.time * 30f));
            CamPivot.localRotation = Quaternion.Euler(pitch - recoil.x * 0.65f + shakeRot.x, recoil.y * 0.65f + shakeRot.y, roll + shakeRot.z);

            float aimMul = Inventory.Current != null ? Mathf.Lerp(1f, Inventory.Current.AimFov, Inventory.Current.AimWeight) : 1f;
            float targetFov = (d.fov + (Sprinting ? 6f : 0f)) * aimMul;
            fovCurrent = MathX.Damp(fovCurrent, targetFov, 12f, dt);
            Cam.fieldOfView = fovCurrent;
            Cam.farClipPlane = d.renderDistance;
            ViewCam.fieldOfView = d.viewmodelFov * Mathf.Lerp(1f, 0.85f, Inventory.Current != null ? Inventory.Current.AimWeight : 0f);
            ViewCam.enabled = d.viewmodel && !Health.Dead;
        }

        public Vector2 LookDelta => PlayerInput.Frame.Look;
    }
}
