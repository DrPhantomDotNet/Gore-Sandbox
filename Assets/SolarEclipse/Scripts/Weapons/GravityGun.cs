using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gravity Gun. RMB pulls the object (or Goredoll body part) you aim at and holds it floating in front of you;
    /// RMB again drops it. LMB punts: launches the held object hard, or blasts whatever you aim at within punt range.
    /// Multiplayer clients hold through the host's remote-grab requests (Creative, like the Spawn Gun) and punt
    /// through impulse / launch requests.
    /// </summary>
    public sealed class GravityGun : Weapon
    {
        const float PullRange = 30f, PuntRange = 14f, MaxPullMass = 400f;

        Rigidbody held;
        Goredoll heldDoll;
        Vector3 localGrab;
        float holdDist, pulse, puntFlash, nextPunt, nextFall;
        bool remoteHeld;
        LineRenderer beam;
        AudioSource hum;

        public bool Holding => held != null;
        public override bool ConsumesScroll => held != null;
        public override string AmmoText => held != null ? (heldDoll != null ? heldDoll.DisplayName : held.name) : "Gravity Gun";
        public override string ModeText => held != null ? "HOLDING | LMB PUNT" : "RMB PULL | LMB PUNT";

        protected override void OnAttach()
        {
            var go = new GameObject("GravityGunBeam");
            go.layer = Layers.Effects;
            go.transform.SetParent(Player.transform, false);
            beam = go.AddComponent<LineRenderer>();
            beam.positionCount = 12;
            beam.sharedMaterial = Mats.AdditiveWith(TextureFactory.Spark);
            beam.widthMultiplier = 0.05f;
            beam.textureMode = LineTextureMode.Stretch;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.receiveShadows = false;
            beam.enabled = false;
        }

        protected override void OnEquip()
        {
            Vm.Right.SetPose(HandPose.PistolGrip);
            Vm.Left.SetPose(HandPose.Support);
        }

        protected override void OnHolster()
        {
            Drop(Vector3.zero, false);
            if (beam != null) beam.enabled = false;
            if (hum != null) hum.Stop();
        }

        public override void Destroy()
        {
            base.Destroy();
            if (beam != null) Object.Destroy(beam.gameObject);
        }

        public override void Tick(InputFrame f, float dt)
        {
            Vm.ResetHands();
            pulse += dt;
            puntFlash = Mathf.MoveTowards(puntFlash, 0f, dt * 5f);
            if (held == null || !held) { if (held != null || remoteHeld) Drop(Vector3.zero, false); held = null; heldDoll = null; }

            if (f.AimPressed)
            {
                if (held != null) Drop(Vector3.zero, true);
                else TryPull();
            }
            if (f.Fire && Time.time >= nextPunt) Punt();
            if (held != null) HoldTick(f, dt);

            var pos = Def.HipPos + new Vector3(0, Mathf.Sin(pulse * 2f) * 0.002f, 0) + (held != null ? new Vector3(0, 0.008f, 0.02f) : Vector3.zero);
            var rot = Quaternion.identity;
            if (held == null) TickInspect(f, dt, ref pos, ref rot);
            Vm.TargetPos = pos;
            Vm.TargetRot = rot;
            if (Model.Glow != null)
                Model.Glow.localScale = Vector3.one * (1f + (held != null ? 0.18f + Mathf.Sin(pulse * 35f) * 0.05f : Mathf.Sin(pulse * 3f) * 0.03f) + puntFlash * 0.5f);
            if (hum != null) hum.pitch = held != null ? 1f + Mathf.Sin(pulse * 6f) * 0.03f : 1f;
            UpdateBeam();
        }

        // ------------------------------------------------------------------ pull / hold

        void TryPull()
        {
            if (!Physics.Raycast(Eye, Aim, out var hit, PullRange, Layers.GrabMask, QueryTriggerInteraction.Ignore) || hit.rigidbody == null || hit.rigidbody.isKinematic)
            {
                AudioManager.Play("gg_dry", Eye, 0.5f, 1f, 0f);
                return;
            }
            var rb = hit.rigidbody;
            if (rb.mass > MaxPullMass) { AudioManager.Play("gg_dry", Eye, 0.5f, 0.8f, 0f); GameEvents.ShowToast("Too heavy for the Gravity Gun."); return; }
            held = rb;
            heldDoll = rb.GetComponentInParent<Goredoll>();
            localGrab = rb.transform.InverseTransformPoint(rb.worldCenterOfMass);
            float size = 0.5f;
            var col = hit.collider;
            if (col != null) size = Mathf.Clamp(col.bounds.extents.magnitude, 0.2f, 2.5f);
            holdDist = 1.4f + size;
            remoteHeld = NetState.IsClient && NetManager.Handle(rb, out _, out _);
            if (remoteHeld)
            {
                if (!NetState.LocalCreative) { held = null; heldDoll = null; remoteHeld = false; GameEvents.ShowToast("The Gravity Gun needs Creative mode in multiplayer - ask the host."); AudioManager.Ui("ui_error", 0.5f); return; }
                NetManager.I.RequestGrab(rb, localGrab);
            }
            else if (heldDoll != null && heldDoll.Ragdoll != null) heldDoll.Ragdoll.Fall(3f);
            if (heldDoll != null && heldDoll.Vitals.Conscious) heldDoll.Voice.Say("v_whoa", 1f, true);
            if (hum == null) hum = AudioManager.CreateLoop("gg_hum", Player.transform, 0.35f, AudioBus.Sfx, 20f, false);
            if (hum != null) hum.Play();
            AudioManager.Play("gg_grab", hit.point, 0.7f);
            FX.Flash(hit.point, new Color(1f, 0.6f, 0.2f), 2.5f, 2f, 0.15f);
            nextFall = Time.time + 1f;
            Stats.Add("grabs");
        }

        void HoldTick(InputFrame f, float dt)
        {
            if (Mathf.Abs(f.Scroll) > 0.01f) holdDist = Mathf.Clamp(holdDist + f.Scroll * 0.3f, 1.2f, 6f);
            var target = Eye + Aim * holdDist;
            if (remoteHeld)
            {
                var nm = NetManager.I;
                nm.ClientGrabTarget = target; nm.ClientGrabRot = held.rotation; nm.ClientGrabRotating = false;
                return;
            }
            if (held.isKinematic) { Drop(Vector3.zero, true); return; }
            var grabWorld = held.transform.TransformPoint(localGrab);
            var err = target - grabWorld;
            // Pulls fast from afar, then settles: critically-damped-ish velocity steering.
            var desired = Vector3.ClampMagnitude(err * 11f, 30f);
            var v = Vector3.Lerp(RB.Vel(held), desired, 1f - Mathf.Exp(-14f * dt));
            RB.SetVel(held, v);
            held.angularVelocity *= Mathf.Exp(-4f * dt);
            held.WakeUp();
            if (heldDoll != null && Time.time >= nextFall && heldDoll.Ragdoll != null) { heldDoll.Ragdoll.Fall(2f); nextFall = Time.time + 1f; }
            // Lost it behind a wall or yanked away: let go.
            if (err.magnitude > PullRange + 5f) Drop(Vector3.zero, true);
        }

        void Drop(Vector3 velocity, bool sound)
        {
            if (held == null && !remoteHeld) return;
            if (remoteHeld) { NetManager.I.RequestGrabEnd(velocity); remoteHeld = false; }
            else if (held != null && !held.isKinematic)
            {
                if (velocity != Vector3.zero) RB.SetVel(held, velocity);
                else RB.SetVel(held, Vector3.ClampMagnitude(RB.Vel(held), 8f));
            }
            if (sound && held != null) AudioManager.Play("gg_drop", held.position, 0.5f);
            held = null;
            heldDoll = null;
            if (hum != null) hum.Stop();
        }

        // ------------------------------------------------------------------ punt

        void Punt()
        {
            nextPunt = Time.time + 0.45f;
            Vector3 point;
            if (held != null)
            {
                var rb = held;
                var doll = heldDoll;
                point = rb.position;
                float speed = Mathf.Lerp(38f, 16f, Mathf.Clamp01(rb.mass / 150f));
                bool remote = remoteHeld;
                Drop(Aim * speed, false);
                if (!remote && doll != null) doll.Launch(Aim * 24f + Vector3.up * 2f);
            }
            else if (Physics.Raycast(Eye, Aim, out var hit, PuntRange, Layers.GrabMask, QueryTriggerInteraction.Ignore) && hit.rigidbody != null)
            {
                point = hit.point;
                var rb = hit.rigidbody;
                var doll = rb.GetComponentInParent<Goredoll>();
                float falloff = Mathf.Lerp(1f, 0.5f, hit.distance / PuntRange);
                if (doll != null)
                {
                    doll.Launch((Aim * 20f + Vector3.up * 3f) * falloff);
                    NetPhysics.Impulse(rb, Aim * 300f * falloff, hit.point);
                }
                else NetPhysics.Impulse(rb, Aim * Mathf.Clamp(rb.mass * 22f, 5f, 4000f) * falloff, hit.point);
            }
            else
            {
                AudioManager.Play("gg_dry", Eye, 0.5f, 1.2f, 0f);
                Vm.AddRecoil(new Vector3(0, 0.01f, -0.04f), new Vector3(-3f, 0, 0));
                return;
            }
            puntFlash = 1f;
            AudioManager.Play("gg_punt", Eye, 0.8f, Random.Range(0.95f, 1.05f), 0f);
            FX.Flash(point, new Color(1f, 0.65f, 0.25f), 4f, 3f, 0.2f);
            FX.Sparks(point, -Aim, 12, 6f);
            Tracers.Spawn(Model.Muzzle.position, point, new Color(1f, 0.7f, 0.3f));
            Vm.AddRecoil(new Vector3(0, 0.03f, -0.12f), new Vector3(-10f, 0, 0));
            CameraFX.Shake(0.12f);
            Stats.Add("throws");
        }

        // ------------------------------------------------------------------ visuals

        void UpdateBeam()
        {
            beam.enabled = held != null || puntFlash > 0.05f;
            if (!beam.enabled) return;
            var a = Model.Muzzle.position;
            var b = held != null ? held.transform.TransformPoint(localGrab) : Eye + Aim * 6f;
            var mid = Eye + Aim * Vector3.Distance(Eye, b) * 0.5f;
            for (int i = 0; i < 12; i++)
            {
                float t = i / 11f;
                var p = Vector3.Lerp(Vector3.Lerp(a, mid, t), Vector3.Lerp(mid, b, t), t);
                p += Random.insideUnitSphere * 0.012f * Mathf.Sin(t * Mathf.PI);
                beam.SetPosition(i, p);
            }
            var col = new Color(1f, 0.62f, 0.2f);
            float alpha = held != null ? 0.75f : puntFlash;
            beam.startColor = col.WithAlpha(alpha);
            beam.endColor = col.WithAlpha(alpha * 0.5f);
            beam.widthMultiplier = (held != null ? 0.04f : 0.08f * puntFlash) + Mathf.Sin(pulse * 50f) * 0.006f;
        }
    }
}
