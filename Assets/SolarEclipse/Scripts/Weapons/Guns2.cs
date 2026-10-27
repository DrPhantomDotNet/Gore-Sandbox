using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gun pack 2: pump shotgun, bolt-action sniper (scope overlay), .357 revolver, minigun and harpoon gun.
    /// All of them are <see cref="Firearm"/>s (fire / reload / recoil / multiplayer come from there) with extra mechanics.
    /// Models: WeaponModels2.cs. Sounds: Audio/RecipesGuns2.cs. Projectile: Harpoon.cs.
    /// </summary>
    public static class Guns2
    {
        public static Weapon Create(WeaponDef def)
        {
            if (def == null) return null;
            switch (def.Id)
            {
                case "shotgun": return new PumpShotgun { Def = def };
                case "sniper": return new SniperRifle { Def = def };
                case "revolver": return new Revolver { Def = def };
                case "minigun": return new Minigun { Def = def };
                case "harpoon": return new HarpoonGun { Def = def };
                default: return null;
            }
        }

        public static bool IsHarpoon(WeaponDef def) => def != null && def.Id == "harpoon";
    }

    // ====================================================================== Pump shotgun

    public sealed class PumpShotgun : Firearm
    {
        float pumpT = -1f;
        bool pumpSound;
        Vector3 supportRest;

        protected override bool CanShoot => pumpT < 0f;
        public override string ModeText => "PUMP 12GA";

        protected override void OnAttach()
        {
            base.OnAttach();
            supportRest = Model.SupportL.localPosition;
        }

        protected override void OnHolster()
        {
            base.OnHolster();
            pumpT = -1f;
            ResetPump();
        }

        void ResetPump()
        {
            if (Model.Charge != null) Model.Charge.localPosition = Model.ChargeRest;
            Model.SupportL.localPosition = supportRest;
        }

        protected override void OnShot()
        {
            pumpT = 0f;
            pumpSound = false;
            // Point-blank blast: the whole body gets thrown (pellet damage itself comes from the pellets).
            if (!NetState.Authority) return;
            if (Physics.SphereCast(Eye, 0.12f, Aim, out var hit, 4f, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                var part = hit.collider.GetComponent<BodyPart>();
                if (part != null && part.Doll != null)
                {
                    float k = 1f - hit.distance / 4f;
                    part.Doll.Launch(Aim * Mathf.Lerp(2f, 9f, k) + Vector3.up * 1.5f * k);
                    BloodFX.Burst(hit.point, Aim, 1.5f + 2f * k, new Color(0.45f, 0.02f, 0.02f));
                    BloodFX.Mist(hit.point, Aim, 1f + k);
                }
            }
        }

        public override void Tick(InputFrame f, float dt)
        {
            base.Tick(f, dt);
            if (pumpT < 0f) { if (!Reloading) Model.SupportL.localPosition = supportRest; return; }
            pumpT += dt / 0.62f;
            if (!pumpSound && pumpT > 0.12f) { pumpSound = true; AudioManager.Play(Def.Charge, Eye, 0.7f, Random.Range(0.97f, 1.03f), 0f); }
            float back = Ease(Ramp(pumpT, 0.14f, 0.4f)) * (1f - Ease(Ramp(pumpT, 0.52f, 0.78f)));
            var offset = Vector3.back * 0.085f * back;
            if (Model.Charge != null) Model.Charge.localPosition = Model.ChargeRest + offset;
            Model.SupportL.localPosition = supportRest + offset;
            Vm.TargetRot *= Quaternion.Euler(-3f * back, 0f, 6f * back);
            if (pumpT >= 1f) { pumpT = -1f; ResetPump(); }
        }
    }

    // ====================================================================== Bolt-action sniper

    public sealed class SniperRifle : Firearm
    {
        float boltT = -1f, scope;
        bool boltSound, hidden;
        Vector2 sway, swayApplied;
        Renderer[] renderers;
        static Texture2D scopeMask;

        protected override bool CanShoot => boltT < 0f;
        public override string ModeText => "BOLT 5x";
        bool Scoped => scope > 0.5f;

        protected override void OnAttach()
        {
            base.OnAttach();
            renderers = Model.GetComponentsInChildren<Renderer>(true);
        }

        protected override void OnHolster()
        {
            base.OnHolster();
            boltT = -1f;
            scope = 0f;
            SetHidden(false);
            UnwindSway();
            if (Model.Charge != null) { Model.Charge.localPosition = Model.ChargeRest; Model.Charge.localRotation = Quaternion.identity; }
        }

        protected override void OnShot()
        {
            boltT = 0f;
            boltSound = false;
        }

        void SetHidden(bool h)
        {
            if (hidden == h || renderers == null) return;
            hidden = h;
            foreach (var r in renderers) if (r != null) r.enabled = !h;
        }

        void UnwindSway()
        {
            CameraFX.Kick -= swayApplied / 0.35f;
            swayApplied = Vector2.zero;
        }

        public override void Tick(InputFrame f, float dt)
        {
            base.Tick(f, dt);

            // ----- Bolt cycle: lift, pull back, push forward, lock down (right hand leaves the grip to work it).
            if (boltT >= 0f)
            {
                boltT += dt / 0.95f;
                if (!boltSound && boltT > 0.1f) { boltSound = true; AudioManager.Play(Def.Charge, Eye, 0.65f, 1f, 0f); }
                float lift = Ease(Ramp(boltT, 0.18f, 0.32f)) * (1f - Ease(Ramp(boltT, 0.72f, 0.86f)));
                float pull = Ease(Ramp(boltT, 0.32f, 0.48f)) * (1f - Ease(Ramp(boltT, 0.56f, 0.72f)));
                if (Model.Charge != null)
                {
                    Model.Charge.localRotation = Quaternion.Euler(0f, 0f, 65f * lift);
                    Model.Charge.localPosition = Model.ChargeRest + Vector3.back * 0.09f * pull;
                }
                float hand = Ease(Ramp(boltT, 0f, 0.18f)) * (1f - Ease(Ramp(boltT, 0.86f, 1f)));
                if (Model.Charge != null && hand > 0.001f)
                {
                    var knob = Model.Charge.localPosition + Model.Charge.localRotation * new Vector3(0.041f, -0.013f, 0f);
                    Vm.RightOnAnchor = false;
                    Vm.RightInSocket = true;
                    Vm.RightPos = Vector3.Lerp(Model.GripR.localPosition, knob + new Vector3(0.012f, -0.012f, -0.01f), hand);
                    Vm.RightRot = Quaternion.Slerp(Model.GripR.localRotation, Model.GripR.localRotation * Quaternion.Euler(-25f, 0f, 35f), hand);
                }
                Vm.TargetRot *= Quaternion.Euler(-2f * hand, 0f, -10f * hand);
                if (boltT >= 1f)
                {
                    boltT = -1f;
                    if (Model.Charge != null) { Model.Charge.localPosition = Model.ChargeRest; Model.Charge.localRotation = Quaternion.identity; }
                }
            }
            else if (Model.Charge != null && !Reloading) Model.Charge.localRotation = Quaternion.identity;

            // ----- Scope: hide the gun and hands once the eye is behind the glass.
            scope = Mathf.Clamp01((aim - 0.78f) / 0.17f);
            SetHidden(Scoped);
            if (Scoped) { Vm.HideLeft = true; Vm.HideRight = true; }

            // ----- Scope sway (hold Sprint to steady). Applied as camera deltas so the real aim moves.
            var target = Vector2.zero;
            if (Scoped)
            {
                float t = Time.time * 0.45f;
                float amp = (Player.Crouching ? 0.25f : 0.6f) * (f.Sprint ? 0.15f : 1f) * (Player.Speed > 0.5f ? 2f : 1f);
                target = new Vector2(Mathf.PerlinNoise(t, 3.1f) - 0.5f, Mathf.PerlinNoise(7.7f, t) - 0.5f) * 2f * amp;
            }
            sway = Vector2.Lerp(sway, target, 1f - Mathf.Exp(-6f * dt));
            var delta = sway - swayApplied;
            CameraFX.Kick += delta / 0.35f;
            swayApplied = sway;
        }

        // ------------------------------------------------------------------ scope overlay

        static Texture2D ScopeMask()
        {
            if (scopeMask != null) return scopeMask;
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "ScopeMask" };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // Clear glass, a dark inner vignette towards the rim, then solid black outside.
                    float a = r >= 0.97f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.97f, r)) * 0.85f;
                    px[y * N + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            scopeMask = tex;
            return tex;
        }

        public override void DrawOverlay(float W, float H)
        {
            if (!Equipped || scope <= 0.01f) return;
            float a = Mathf.Clamp01(scope * 1.4f);
            var black = new Color(0f, 0f, 0f, a);
            // Slight lag of the scope picture against the view while swaying / moving.
            float ox = -sway.x * 6f, oy = sway.y * 6f;
            float D = H * 0.98f, cx = W / 2f + ox, cy = H / 2f + oy;
            var circle = new Rect(cx - D / 2f, cy - D / 2f, D, D);
            UI.Tex(circle, ScopeMask(), new Color(1f, 1f, 1f, a));
            UI.Fill(new Rect(-20f, -20f, circle.x + 21f, H + 40f), black);
            UI.Fill(new Rect(circle.xMax - 1f, -20f, W - circle.xMax + 21f, H + 40f), black);
            UI.Fill(new Rect(circle.x, -20f, D, circle.y + 21f), black);
            UI.Fill(new Rect(circle.x, circle.yMax - 1f, D, H - circle.yMax + 21f), black);

            // Reticle: thick outer posts, thin centre cross, mil dots and a small red centre dot.
            float R = D / 2f;
            var ink = new Color(0f, 0f, 0f, 0.92f * a);
            UI.Fill(new Rect(cx - R, cy - 3f, R * 0.62f, 6f), ink);
            UI.Fill(new Rect(cx + R * 0.38f, cy - 3f, R * 0.62f, 6f), ink);
            UI.Fill(new Rect(cx - 3f, cy + R * 0.38f, 6f, R * 0.62f), ink);
            UI.Fill(new Rect(cx - 3f, cy - R, 6f, R * 0.62f), ink);
            UI.Fill(new Rect(cx - R * 0.38f, cy - 0.75f, R * 0.76f, 1.5f), ink);
            UI.Fill(new Rect(cx - 0.75f, cy - R * 0.38f, 1.5f, R * 0.76f), ink);
            for (int i = 1; i <= 4; i++)
            {
                float s = R * 0.085f * i;
                UI.Fill(new Rect(cx + s - 2f, cy - 2f, 4f, 4f), ink);
                UI.Fill(new Rect(cx - s - 2f, cy - 2f, 4f, 4f), ink);
                UI.Fill(new Rect(cx - 2f, cy + s - 2f, 4f, 4f), ink);
                UI.Fill(new Rect(cx - 2f, cy - s - 2f, 4f, 4f), ink);
            }
            UI.Fill(new Rect(cx - 1.5f, cy - 1.5f, 3f, 3f), new Color(1f, 0.1f, 0.05f, a));
            if (a > 0.9f) UI.Label(new Rect(W / 2f - 200f, H - 120f, 400f, 30f), "Hold SPRINT to steady your aim", 18, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter);
        }
    }

    // ====================================================================== .357 revolver

    public sealed class Revolver : Firearm
    {
        Transform cylinder, hammer;
        Vector3 cylRest;
        float cylAngle, cylTarget, hammerT = 1f;
        bool cockSound = true, casingsOut, closeSound;

        public override bool OpenBolt => true;            // 6 in the cylinder, no +1
        protected override bool EjectOnFire => false;     // empties come out when the cylinder is opened
        public override string ModeText => "DA .357";

        protected override void OnAttach()
        {
            base.OnAttach();
            cylinder = Model.transform.Find("Cylinder");
            hammer = Model.transform.Find("Hammer");
            if (cylinder != null) cylRest = cylinder.localPosition;
        }

        protected override void OnShot()
        {
            cylTarget += 60f;
            hammerT = 0f;
            cockSound = false;
        }

        public override void Tick(InputFrame f, float dt)
        {
            bool wasReloading = Reloading;
            base.Tick(f, dt);

            // Hammer falls on the shot, then is drawn back (double action) ready for the next one.
            hammerT = Mathf.MoveTowards(hammerT, 1f, dt / 0.36f);
            if (!cockSound && hammerT > 0.35f) { cockSound = true; AudioManager.Play(Def.Charge, Eye, 0.4f, Random.Range(0.97f, 1.05f), 0f); }
            if (hammer != null) hammer.localRotation = Quaternion.Euler(-38f * Ease(Ramp(hammerT, 0.35f, 0.9f)), 0f, 0f);

            // Cylinder: index one chamber per shot; swings out to the left while reloading.
            cylAngle = Mathf.MoveTowards(cylAngle, cylTarget, dt * 900f);
            float p = ReloadProgress;
            float swing = 0f;
            if (Reloading)
            {
                swing = Ease(Ramp(p, 0.06f, 0.18f)) * (1f - Ease(Ramp(p, 0.72f, 0.84f)));
                if (!casingsOut && p > 0.22f)
                {
                    casingsOut = true;
                    if (Model.Eject != null)
                        for (int i = 0; i < 6; i++)
                            Casings.Eject(Model.Eject.position + Random.insideUnitSphere * 0.01f, -CamT.up + Random.insideUnitSphere * 0.3f, -CamT.forward, Player.CC.velocity, 0);
                }
                if (!closeSound && p > 0.78f) { closeSound = true; AudioManager.Play("revolver_close", Eye, 0.6f, 1f, 0f); cylTarget += 30f; }
                Vm.TargetRot *= Quaternion.Euler(0f, 0f, 12f * swing);
            }
            else if (wasReloading || casingsOut) { casingsOut = false; closeSound = false; }
            if (cylinder != null)
            {
                cylinder.localPosition = cylRest + new Vector3(-0.034f, -0.014f, 0f) * swing;
                cylinder.localRotation = Quaternion.Euler(0f, 0f, cylAngle + 70f * swing);
            }
        }
    }

    // ====================================================================== Minigun

    public sealed class Minigun : Firearm
    {
        float spin, barrelAngle;
        bool wanted;
        int ejectCount;
        Transform barrels;
        AudioSource loop;

        protected override bool CanShoot => spin >= 0.97f;
        protected override bool EjectOnFire => (++ejectCount % 3) == 0;
        public override bool IsAiming => false;
        public override float MoveSpeedMul => 0.85f - 0.4f * spin;
        public override string ModeText => spin >= 0.97f ? "ROTARY" : "SPIN " + Mathf.RoundToInt(spin * 100f) + "%";

        protected override void OnAttach()
        {
            base.OnAttach();
            barrels = Model.transform.Find("Barrels");
            loop = AudioManager.CreateLoop("minigun_spin", Model.transform, 0f, AudioBus.Sfx, 40f, false);
            if (loop != null) loop.spatialBlend = 0f;
        }

        protected override void OnHolster()
        {
            base.OnHolster();
            spin = 0f;
            wanted = false;
            if (loop != null) loop.Stop();
        }

        public override void Tick(InputFrame f, float dt)
        {
            bool want = f.FireHeld && !Reloading && !Player.Sprinting;
            if (want && !wanted) AudioManager.Play("minigun_windup", Eye, 0.7f, 1f, 0f);
            if (!want && wanted && spin > 0.3f) AudioManager.Play("minigun_winddown", Eye, 0.6f * spin, 1f, 0f);
            wanted = want;
            spin = Mathf.MoveTowards(spin, want ? 1f : 0f, dt / (want ? 0.6f : 1.4f));

            f.Mode = false; // always full auto
            base.Tick(f, dt);

            barrelAngle = (barrelAngle + spin * 2000f * dt) % 360f;
            if (barrels != null) barrels.localRotation = Quaternion.Euler(0f, 0f, barrelAngle);

            if (loop != null)
            {
                float vol = Mathf.Clamp01((spin - 0.5f) * 2f) * 0.45f;
                AudioManager.ApplyBus(loop, AudioBus.Sfx, vol);
                loop.pitch = 0.75f + 0.3f * spin;
                if (vol > 0.001f && !loop.isPlaying) loop.Play();
                else if (vol <= 0.001f && loop.isPlaying) loop.Stop();
            }
            // Heavy: rides low and shakes while spinning.
            if (spin > 0.05f) Vm.TargetPos += new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * 0.0015f * spin;
        }
    }

    // ====================================================================== Harpoon gun

    public sealed class HarpoonGun : Firearm
    {
        Transform loaded;
        float lastShot = -10f;

        protected override bool EjectOnFire => false;
        public override string ModeText => "HARPOON";

        protected override void OnAttach()
        {
            base.OnAttach();
            loaded = Model.transform.Find("LoadedHarpoon");
        }

        protected override void OnShot() => lastShot = Time.time;

        protected override void FireRound(Vector3 origin, Vector3 dir, Vector3 muzzle, int index, bool authority)
        {
            // The projectile flies from just below the eye (so what you aim at is what you hit).
            Harpoon.Launch(origin + dir * 0.35f - CamT.up * 0.06f, dir, Def, Player, authority);
        }

        public override void Tick(InputFrame f, float dt)
        {
            base.Tick(f, dt);
            if (loaded != null)
            {
                bool show = (Ammo > 0 || Rules.InfiniteAmmo) && Time.time - lastShot > (Rules.InfiniteAmmo ? 0.6f : 0f);
                if (loaded.gameObject.activeSelf != show) loaded.gameObject.SetActive(show);
            }
        }
    }
}
