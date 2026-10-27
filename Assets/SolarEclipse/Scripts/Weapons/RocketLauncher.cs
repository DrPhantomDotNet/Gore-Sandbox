using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Shoulder-fired rocket launcher. LMB fires a rocket (visible projectile with smoke trail and light that explodes on
    /// impact), RMB aims through the side optic, R (or LMB when empty) loads a new rocket by hand. Big backblast + kick.
    /// </summary>
    public sealed class RocketLauncher : Weapon, IRefillable
    {
        bool loaded = true;
        float reloadT = -1f, kick, sinceFire = 10f;
        bool clicked;

        public override bool IsAiming => aim > 0.5f;
        public override bool Busy => reloadT >= 0f;
        public override string AmmoText => loaded ? "1" : "0";
        public override string ModeText => reloadT >= 0f ? "LOADING" : loaded ? "ROCKET READY" : "R: RELOAD";

        public void Refill()
        {
            loaded = true;
            reloadT = -1f;
            ShowRocket(true, Model != null ? Model.MagRest : Vector3.zero);
        }

        void ShowRocket(bool on, Vector3 at)
        {
            if (Model == null || Model.Mag == null) return;
            Model.Mag.gameObject.SetActive(on);
            Model.Mag.localPosition = at;
        }

        protected override void OnEquip()
        {
            Vm.Right.SetPose(HandPose.PistolGrip);
            Vm.Left.SetPose(HandPose.PistolGrip);
            ShowRocket(loaded, Model.MagRest);
        }

        protected override void OnHolster()
        {
            if (reloadT >= 0f) { reloadT = -1f; ShowRocket(false, Model.MagRest); }
            Vm.Right.SetPose(HandPose.Relaxed);
            Vm.Left.SetPose(HandPose.Relaxed);
        }

        public override void Tick(InputFrame f, float dt)
        {
            sinceFire += dt;
            aim = MathX.Damp(aim, f.Aim && reloadT < 0f && !Player.Sprinting ? 1f : 0f, 10f, dt);
            kick = MathX.Damp(kick, 0f, 6f, dt);

            if (reloadT < 0f)
            {
                if (f.Fire && loaded && sinceFire > 0.3f) Fire();
                else if (!loaded && (f.Reload || (f.Fire && sinceFire > 0.5f) || (Rules.NoReload && sinceFire > 0.3f))) StartReload();
            }

            var pos = Vector3.Lerp(Def.HipPos, AimPos(), Ease(aim));
            var rot = Quaternion.identity;
            pos += new Vector3(0, 0.01f, -0.09f) * kick;
            rot *= Quaternion.Euler(-14f * kick, 0, 0);
            Vm.AimWeight = aim;

            Vm.LeftOnAnchor = true;
            Vm.LeftInSocket = false;
            if (reloadT >= 0f)
            {
                reloadT += dt / (Rules.NoReload ? 0.4f : Def.ReloadTime);
                float t = reloadT;
                // Tilt the tube up and to the left while the left hand fetches a rocket and pushes it into the muzzle.
                float tilt = Ease(Ramp(t, 0f, 0.2f)) * (1f - Ease(Ramp(t, 0.82f, 1f)));
                pos += new Vector3(-0.02f, -0.06f, 0.06f) * tilt;
                rot *= Quaternion.Euler(-12f * tilt, -18f * tilt, 18f * tilt);
                var front = Model.MagRest + new Vector3(0.0f, -0.03f, 0.34f);
                if (t < 0.18f) Vm.LeftOnAnchor = true;
                else if (t < 0.4f)
                {
                    // Hand drops out of view.
                    float k = Ease(Ramp(t, 0.18f, 0.4f));
                    Vm.LeftOnAnchor = false;
                    Vm.LeftPos = Vector3.Lerp(new Vector3(-0.08f, -0.2f, 0.2f), new Vector3(-0.15f, -0.55f, 0.25f), k);
                    Vm.LeftRot = Quaternion.Euler(-10f, 10f, 80f);
                }
                else if (t < 0.78f)
                {
                    // Rocket comes up in front of the muzzle and slides in.
                    float up = Ease(Ramp(t, 0.4f, 0.55f)), slide = Ease(Ramp(t, 0.55f, 0.72f));
                    var rp = Vector3.Lerp(front + new Vector3(-0.05f, -0.3f, -0.05f), front, up);
                    rp = Vector3.Lerp(rp, Model.MagRest, slide);
                    ShowRocket(true, rp);
                    Vm.LeftOnAnchor = false;
                    Vm.LeftInSocket = true;
                    Vm.LeftPos = rp + new Vector3(-0.045f, -0.01f, 0.02f);
                    Vm.LeftRot = Quaternion.Euler(0f, 0f, 90f);
                    if (!clicked && t > 0.7f)
                    {
                        clicked = true;
                        AudioManager.Play("rocket_reload", Eye, 0.7f, 1f, 0f);
                    }
                }
                else
                {
                    if (!loaded) { loaded = true; ShowRocket(true, Model.MagRest); }
                }
                if (reloadT >= 1f) { reloadT = -1f; loaded = true; ShowRocket(true, Model.MagRest); }
            }
            else if (aim < 0.1f) TickInspect(f, dt, ref pos, ref rot, 2f);

            Vm.TargetPos = pos;
            Vm.TargetRot = rot;
        }

        void StartReload()
        {
            reloadT = 0f;
            clicked = false;
            inspectT = -1f;
            ShowRocket(false, Model.MagRest);
            AudioManager.Play("cloth", Eye, 0.5f, 1f, 0f);
        }

        void Fire()
        {
            loaded = false;
            sinceFire = 0f;
            inspectT = -1f;
            ShowRocket(false, Model.MagRest);

            // Spawn just in front of the tube; aim at whatever is under the crosshair.
            float hip = 1f - aim;
            var origin = Eye + Aim * 0.7f + CamT.right * 0.14f * hip - CamT.up * 0.06f * hip;
            if (Physics.Linecast(Eye, origin, out var block, Layers.WorldMask, QueryTriggerInteraction.Ignore)) origin = Eye + Aim * Mathf.Max(0.05f, block.distance - 0.1f);
            var target = Physics.Raycast(Eye, Aim, out var hit, 400f, Layers.ShootMask, QueryTriggerInteraction.Ignore) ? hit.point : Eye + Aim * 400f;
            var dir = (target - origin).sqrMagnitude > 0.5f ? (target - origin).normalized : Aim;
            // A little inaccuracy from the hip.
            dir = Quaternion.Euler(Random.Range(-1f, 1f) * hip, Random.Range(-1f, 1f) * hip, 0) * dir;
            Explosives.Launch(ThrowableKind.Rocket, origin, dir * Explosives.RocketSpeed, 0f, Player);

            AudioManager.Play("rocket_fire", Eye, 1f, Random.Range(0.96f, 1.04f), 0f);
            GameEvents.EmitNoise(Eye, Def.NoiseRadius, 1f, Player);
            // Backblast behind the shooter + muzzle flash.
            var back = Eye - Aim * 1f + CamT.right * 0.15f;
            for (int i = 0; i < 14; i++)
                FX.Smoke(back - Aim * Random.Range(0f, 1.2f), -Aim * Random.Range(2f, 6f) + Random.insideUnitSphere * 1.2f, Random.Range(0.35f, 0.8f), new Color(0.7f, 0.7f, 0.68f, 0.6f), Random.Range(1.5f, 3f));
            for (int i = 0; i < 6; i++) FX.Fire(back, -Aim * Random.Range(3f, 8f) + Random.insideUnitSphere, Random.Range(0.25f, 0.5f));
            FX.MuzzleSmoke(origin, dir, 3f);
            FX.Flash(origin, new Color(1f, 0.65f, 0.3f), 8f, 5f, 0.12f);

            kick = 1f;
            Vm.AddRecoil(new Vector3(0f, 0.3f, -2.6f) * 0.4f, new Vector3(-30f, Random.Range(-6f, 6f), Random.Range(-8f, 8f)));
            CameraFX.Punch(4f * Def.Recoil * 0.35f, Random.Range(-0.8f, 0.8f));
            CameraFX.Shake(0.35f);
        }
    }
}
