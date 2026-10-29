using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Data-driven firearm (Glock 19, UZI, AR-15, AR-17). Handles hip/ADS, semi/auto fire, spread + bloom,
    /// recoil, muzzle flash + light + smoke, casing ejection, slide/bolt cycling, slide lock on empty,
    /// tactical (+1 in chamber) vs empty magazine reloads with a dropped physical magazine, one-at-a-time
    /// shell reloads for the AR-17, charging, dry fire and an inspect animation.
    /// </summary>
    public class Firearm : Weapon
    {
        public int Ammo;
        public bool Reloading => reloadT >= 0f;
        bool auto;
        float nextFire, bloom, reloadT = -1f, reloadDur, slideKick, flashT;
        bool emptyReload, magDropped, magInserted, slideLocked, chargePlayed1, chargePlayed2;
        int shellsToLoad;
        float shellT;
        GameObject flash;
        Transform handMag;
        Light flashLight;

        /// <summary>Attachment modifiers (Weapons/Attachments.cs). Identity for guns without a saved loadout.</summary>
        public AttachStats Mods = AttachStats.None;
        /// <summary>Def used for the bullets themselves (damage/range modified by a suppressor); the catalog Def otherwise.</summary>
        public WeaponDef ShotDef;
        float scopeView;
        public int Capacity => Mathf.Max(1, Mathf.RoundToInt(Def.Mag * Mods.MagMul));
        float HipSpreadM => Def.HipSpread * Mods.HipSpreadMul;
        float AimSpreadM => Def.AimSpread * Mods.AimSpreadMul;
        public override float AimFov => Mods.AimFov > 0f ? Mods.AimFov : Def.AimFov;
        protected override float AimDistance => Mods.AimDistance > 0f ? Mods.AimDistance : Def.AimDistance;
        public override void DrawOverlay(float W, float H) { if (Equipped && Mods.Optic == 3) AttachmentScope.Draw(W, H, scopeView); }
        /// <summary>Current cone in degrees (crosshair gap).</summary>
        public float Spread => Mathf.Lerp(HipSpreadM, AimSpreadM, aim) + bloom + (Player != null ? Mathf.Clamp01(Player.Speed / 6f) * 1.2f * (1f - aim * 0.6f) : 0f);
        public virtual bool OpenBolt => Def.Id == "uzi";
        // ---- subclass hooks (Weapons/Guns2: pump shotgun, sniper, revolver, minigun, harpoon gun)
        /// <summary>False blocks firing (pump/bolt cycling, minigun spin-up).</summary>
        protected virtual bool CanShoot => true;
        /// <summary>Whether a casing flies out on every shot (revolvers keep theirs).</summary>
        protected virtual bool EjectOnFire => true;
        /// <summary>Reload timeline 0..1, or -1 when not reloading.</summary>
        protected float ReloadProgress => reloadT;
        /// <summary>Called after every shot.</summary>
        protected virtual void OnShot() { }
        /// <summary>Fires one bullet/pellet (default: hitscan). Override for projectiles.</summary>
        protected virtual void FireRound(Vector3 origin, Vector3 dir, Vector3 muzzle, int index, bool authority) =>
            Ballistics.Fire(origin, dir, ShotDef ?? Def, Player, muzzle, DamageType.Bullet, Def.Pellets == 1 || index % 3 == 0, authority);
        public override bool Busy => Reloading;
        public override bool IsAiming => PlayerInput.Frame.Aim && !Reloading && !Player.Sprinting && inspectT < 0f;
        public override string AmmoText => Rules.InfiniteAmmo ? "INF" : Ammo + " / " + Capacity;
        public override string ModeText => Def.Auto ? (auto ? "AUTO" : "SEMI") : Def.Pellets > 1 ? "SEMI · 12GA" : "SEMI";

        protected override void OnAttach()
        {
            Attachments.Apply(this); // saved sight / magazine / muzzle / grip / laser for this gun (before the ammo count)
            Ammo = Capacity + (OpenBolt || Def.ShellReload ? 0 : 1);
            auto = Def.Auto;
            BuildFlash();
            // A magazine copy that rides in the left hand during reloads.
            if (Model.MagMesh != null || Model.Mag != null)
            {
                var mesh = Model.MagMesh != null ? Model.MagMesh : Model.Mag.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh != null)
                {
                    var go = new GameObject("HandMag");
                    go.layer = Layers.Viewmodel;
                    go.transform.SetParent(Vm.Left.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = Model.Mag != null ? Model.Mag.GetComponent<Renderer>().sharedMaterial : Mats.Lit;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    handMag = go.transform;
                    handMag.localPosition = Def.ShellReload ? new Vector3(0f, -0.02f, 0.07f) : new Vector3(0f, -0.06f, 0.05f);
                    handMag.localRotation = Def.ShellReload ? Quaternion.Euler(0, 90, 0) : Quaternion.Euler(0, 0, 0);
                    go.SetActive(false);
                }
            }
        }

        public void Refill()
        {
            Ammo = Capacity + (OpenBolt || Def.ShellReload ? 0 : 1);
            slideLocked = false;
        }

        void BuildFlash()
        {
            flash = new GameObject("MuzzleFlash");
            flash.layer = Layers.Viewmodel;
            flash.transform.SetParent(Model.Muzzle, false);
            var mb = new MeshBuilder { Color = new Color(1f, 0.75f, 0.4f) };
            float size = Def.Pellets > 1 ? 0.13f : Def.Id == "ar15" ? 0.11f : 0.08f;
            float len = size * 2.2f;
            // Front star + two crossed side streaks.
            mb.Face(new Vector3(0, 0, 0.005f), Vector3.back, Vector3.up, size, size);
            mb.Face(new Vector3(0, 0, len * 0.5f), Vector3.right, Vector3.up, len * 0.5f, size * 0.55f);
            mb.Face(new Vector3(0, 0, len * 0.5f), Vector3.left, Vector3.up, len * 0.5f, size * 0.55f);
            mb.Face(new Vector3(0, 0, len * 0.5f), Vector3.up, Vector3.right, size * 0.55f, len * 0.5f);
            mb.Face(new Vector3(0, 0, len * 0.5f), Vector3.down, Vector3.right, size * 0.55f, len * 0.5f);
            flash.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("MuzzleFlash", false);
            var mr = flash.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.AdditiveWith(TextureFactory.Star4);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flash.SetActive(false);

            var lightGo = new GameObject("MuzzleLight");
            lightGo.transform.SetParent(Model.Muzzle, false);
            lightGo.transform.localPosition = new Vector3(0, 0, 0.1f);
            flashLight = lightGo.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = new Color(1f, 0.72f, 0.4f);
            flashLight.range = 7f;
            flashLight.intensity = 0f;
            flashLight.shadows = LightShadows.None;
            flashLight.enabled = false;
        }

        protected override void OnEquip()
        {
            Vm.Right.SetPose(HandPose.PistolGrip);
            Vm.Left.SetPose(HandPose.Support);
            reloadT = -1f;
            if (handMag != null) handMag.gameObject.SetActive(false);
            if (Model.Mag != null && !Def.ShellReload) { Model.Mag.gameObject.SetActive(true); Model.Mag.localPosition = Model.MagRest; }
            AudioManager.Play(Def.Charge ?? "equip_gun", Eye, 0.35f, 1.1f, 0f);
        }

        protected override void OnHolster()
        {
            if (Reloading && magDropped && !magInserted) { } // magazine already dropped; ammo stays as-is
            reloadT = -1f;
            flash.SetActive(false);
            flashLight.enabled = false;
            if (handMag != null) handMag.gameObject.SetActive(false);
            Vm.Right.SetPose(HandPose.Relaxed);
            Vm.Left.SetPose(HandPose.Relaxed);
        }

        public override void Tick(InputFrame f, float dt)
        {
            Vm.ResetHands();
            aim = MathX.Damp(aim, IsAiming ? 1f : 0f, 14f * Mods.AimSpeedMul, dt);
            Vm.AimWeight = aim;
            bloom = Mathf.MoveTowards(bloom, 0f, dt * 3f);

            if (f.Mode && Def.Auto) { auto = !auto; AudioManager.Play("fire_mode", Eye, 0.5f, 1f, 0f); GameEvents.ShowToast(Def.Name + ": " + (auto ? "full auto" : "semi auto")); }

            // ----- Firing
            bool wantsFire = auto ? f.FireHeld : f.Fire;
            if (Reloading && Def.ShellReload && f.Fire && Ammo > 0) { reloadT = -1f; if (handMag != null) handMag.gameObject.SetActive(false); }
            if (wantsFire && !Reloading && Time.time >= nextFire && !Player.Sprinting && CanShoot)
            {
                if (Ammo > 0 || Rules.InfiniteAmmo) Shoot();
                else if (f.Fire)
                {
                    AudioManager.Play("dry_fire", Eye, 0.6f, 1f, 0f);
                    nextFire = Time.time + 0.25f;
                    if (Settings.Data.autoReload) StartReload();
                }
            }
            if (f.Reload && !Reloading) StartReload();

            // ----- Pose
            var pos = Vector3.Lerp(Def.HipPos, AimPos(), Ease(aim));
            var rot = Quaternion.Slerp(Quaternion.Euler(0, 0, -1.5f), AimRot(), Ease(aim));
            if (Reloading) TickReload(dt, ref pos, ref rot);
            else TickInspect(f, dt, ref pos, ref rot);
            Vm.TargetPos = pos;
            Vm.TargetRot = rot;
            if (Mods.Optic == 3)
            {
                // 4x scope attachment: hide the gun and hands once the eye is behind the glass (like the sniper).
                scopeView = Mathf.Clamp01((aim - 0.78f) / 0.17f);
                AttachmentScope.SetHidden(Model, scopeView > 0.5f);
                if (scopeView > 0.5f) { Vm.HideLeft = true; Vm.HideRight = true; }
            }

            // ----- Moving parts
            slideKick = Mathf.MoveTowards(slideKick, 0f, dt / 0.06f);
            if (Model.Slide != null && !(Reloading && emptyReload))
            {
                float back = slideLocked ? 1f : slideKick;
                float travel = Def.Id == "glock" ? 0.032f : 0.03f;
                Model.Slide.localPosition = Model.SlideRest + Vector3.back * travel * back;
            }
            if (Model.Charge != null && Model.ChargeRest != Vector3.zero && !Reloading)
                Model.Charge.localPosition = Model.ChargeRest + Vector3.back * 0.035f * (Def.Id == "ar17" ? slideKick : 0f);

            flashT -= dt;
            bool showFlash = flashT > 0f;
            if (flash.activeSelf != showFlash) flash.SetActive(showFlash);
            flashLight.enabled = showFlash;
            if (showFlash) flashLight.intensity = 4f * Mathf.Clamp01(flashT / 0.05f);
        }

        // ------------------------------------------------------------------ Shooting

        void Shoot()
        {
            if (!Rules.InfiniteAmmo) Ammo--;
            nextFire = Time.time + 60f / Def.Rpm;
            Stats.Add(Stats.ShotsFired);
            inspectT = -1f;

            var muzzle = Model.Muzzle.position;
            if (Mods.Suppressed)
                AudioManager.Play("gun_suppressed", Eye, 0.85f, Random.Range(0.96f, 1.06f) * (Def.Caliber > 7f ? 0.82f : 1f), 0f, 1.5f, 40f, AudioBus.Sfx);
            else
            {
                AudioManager.Play(Def.FireSound, Eye, 1f, Random.Range(0.96f, 1.04f), 0f, 1.5f, 80f, AudioBus.Sfx);
                AudioManager.Play("gun_tail", Eye + Aim * 2f, 0.55f, Random.Range(0.9f, 1.05f), 0.6f, 5f, 200f);
            }
            GameEvents.EmitNoise(Eye, Def.NoiseRadius * (Mods.Suppressed ? 0.25f : 1f), 1f, Player);

            float spread = Mathf.Lerp(HipSpreadM, AimSpreadM, aim) + bloom + Mathf.Clamp01(Player.Speed / 6f) * 1.2f * (1f - aim * 0.6f);
            if (!Player.Grounded) spread += 2f;
            // Offline/host: real shot. Client: draw it now, send the exact same directions to the host.
            bool authority = NetState.Authority;
            var dirs = new System.Collections.Generic.List<Vector3>(Def.Pellets);
            Ballistics.BeginShot();
            for (int i = 0; i < Def.Pellets; i++)
            {
                float pelletSpread = Def.Pellets > 1 ? HipSpreadM * Mathf.Lerp(1f, 0.8f, aim) : spread;
                var r = Random.insideUnitCircle * pelletSpread;
                var dir = CamT.rotation * Quaternion.Euler(r.y, r.x, 0) * Vector3.forward;
                dirs.Add(dir);
                FireRound(Eye, dir, muzzle, i, authority);
            }
            if (authority) Ballistics.EndShot(Aim);
            if (NetState.IsClient) NetManager.I.RequestFire(Def, Eye, dirs);
            else if (NetState.IsHost) NetManager.I.BroadcastFire(NetManager.I.LocalId, WeaponCatalog.All.IndexOf(Def), Eye, dirs);
            bloom = Mathf.Min(bloom + Def.Recoil * Mods.RecoilMul * 0.25f, 4f);

            // Flash, light, smoke (a suppressor hides the flash)
            flashT = Mods.Suppressed ? 0f : 0.045f;
            flash.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            flash.transform.localScale = Vector3.one * Random.Range(0.8f, 1.25f);
            FX.MuzzleSmoke(muzzle, Aim, (Def.Pellets > 1 ? 1.8f : 1f) * (Mods.Suppressed ? 0.5f : 1f));
            if (!Mods.Suppressed) FX.Flash(Eye + Aim * 0.8f, new Color(1f, 0.75f, 0.45f), 9f, 2.2f, 0.05f);

            // Casing: the AR-17 ejects its shell, open-bolt UZI throws brass right.
            if (EjectOnFire) Casings.Eject(Model.Eject != null ? Model.Eject.position : muzzle, CamT.right, CamT.up, Player.CC.velocity, Def.CasingType);

            slideKick = 1f;
            if (Ammo == 0 && Def.Id == "glock" && !Rules.InfiniteAmmo) slideLocked = true;

            // Recoil: viewmodel kick + camera punch (less when aiming).
            float R = Def.Recoil * Mods.RecoilMul * (Player.Crouching ? 0.7f : 1f);
            float a = Mathf.Lerp(1f, 0.55f, aim);
            Vm.AddRecoil(new Vector3(Random.Range(-0.15f, 0.15f), 0.25f, -1.6f) * 0.4f * R * a, new Vector3(-28f * R * a, Random.Range(-6f, 6f) * R, Random.Range(-10f, 10f) * R));
            CameraFX.Punch(0.55f * R * a, Random.Range(-0.35f, 0.35f) * R);
            CameraFX.Shake(0.03f * R);
            OnShot();
        }

        // ------------------------------------------------------------------ Reloading

        void StartReload()
        {
            if (Reloading || Rules.InfiniteAmmo && Ammo > 0) return;
            int full = Capacity + (OpenBolt || Def.ShellReload ? 0 : 1);
            if (Ammo >= full) return;
            if (Def.ShellReload && Ammo >= Capacity) return;
            emptyReload = Ammo == 0;
            magDropped = magInserted = chargePlayed1 = chargePlayed2 = false;
            inspectT = -1f;
            reloadT = 0f;
            if (Def.ShellReload)
            {
                shellsToLoad = Capacity - Ammo;
                shellT = 0f;
                reloadDur = 0.35f + shellsToLoad * Def.ReloadTime + (emptyReload ? Def.EmptyExtra : 0f);
            }
            else reloadDur = (Rules.NoReload ? 0.05f : Def.ReloadTime * Mods.ReloadMul + (emptyReload ? Def.EmptyExtra : 0f));
            AudioManager.Play("cloth", Eye, 0.35f, 1f, 0f);
        }

        void TickReload(float dt, ref Vector3 pos, ref Quaternion rot)
        {
            reloadT += dt / reloadDur;
            if (Def.ShellReload) { TickShellReload(dt, ref pos, ref rot); return; }

            // Normalised timeline. With an empty reload the last part is the slide release / charge.
            float baseFrac = emptyReload ? Def.ReloadTime / (Def.ReloadTime + Def.EmptyExtra) : 1f;
            float t = reloadT / baseFrac; // 0..1 over the magazine swap part
            float tilt = Ease(Ramp(reloadT, 0f, 0.12f)) * (1f - Ease(Ramp(reloadT, 0.9f, 1f)));
            pos += new Vector3(-0.035f, 0.03f, -0.02f) * tilt;
            rot *= Quaternion.Euler(-8f * tilt, 18f * tilt, 32f * tilt);

            var magWell = Model.MagWell != null ? Model.MagWell.localPosition : Model.MagRest + Vector3.down * 0.06f;
            var magRestPos = Model.MagRest;
            var down = Model.Mag != null ? Model.Mag.localRotation * Vector3.down : Vector3.down;
            var handOnMag = Quaternion.Euler(80f, 0f, 0f);
            var pouch = Vm.Socket != null ? Quaternion.Inverse(Vm.Socket.localRotation) * (new Vector3(-0.12f, -0.5f, 0.2f) - Vm.Socket.localPosition) : new Vector3(-0.1f, -0.4f, 0f);

            Vm.LeftOnAnchor = false;
            Vm.LeftInSocket = true;
            Vector3 lp; Quaternion lr;
            if (t < 0.18f)
            {
                // Reach to the magazine well.
                float k = Ease(t / 0.18f);
                lp = Vector3.Lerp(Model.SupportL.localPosition, magWell + Vector3.down * 0.03f, k);
                lr = Quaternion.Slerp(Model.SupportL.localRotation, handOnMag, k);
            }
            else if (t < 0.45f)
            {
                // Mag drops free; hand dives to the pouch and comes back with a fresh one.
                if (!magDropped) DropMag();
                float k = (t - 0.18f) / 0.27f;
                float there = Mathf.Sin(k * Mathf.PI);
                lp = Vector3.Lerp(magWell + Vector3.down * 0.03f, pouch, there);
                lr = handOnMag;
                if (handMag != null) handMag.gameObject.SetActive(k > 0.5f);
            }
            else if (t < 0.62f)
            {
                // Insert: the new mag slides up into the well, hand follows its base.
                float k = Ease((t - 0.45f) / 0.17f);
                if (handMag != null) handMag.gameObject.SetActive(false);
                if (Model.Mag != null)
                {
                    Model.Mag.gameObject.SetActive(true);
                    Model.Mag.localPosition = magRestPos + down * 0.09f * (1f - k);
                }
                lp = magWell + Vector3.down * (0.03f + 0.09f * (1f - k));
                lr = handOnMag;
                if (k >= 1f && !magInserted) InsertMag();
            }
            else if (t < 0.78f || !emptyReload)
            {
                // Palm slap and back to the support position.
                if (!magInserted) InsertMag();
                float k = Ease(Mathf.Clamp01((t - 0.62f) / 0.25f));
                lp = Vector3.Lerp(magWell + Vector3.down * 0.02f, Model.SupportL.localPosition, k);
                lr = Quaternion.Slerp(handOnMag, Model.SupportL.localRotation, k);
            }
            else
            {
                if (!magInserted) InsertMag();
                // Charge: hand to the slide / charging handle, pull back, release.
                float k = Ramp(reloadT, baseFrac, 1f);
                var cp = Model.Charge != null ? (Model.ChargeRest != Vector3.zero ? Model.ChargeRest : Model.Charge.localPosition) : Model.SupportL.localPosition;
                float grab = Ease(Ramp(k, 0f, 0.3f));
                float pull = Bell(k, 0.3f, 0.75f);
                float back = Ease(Ramp(k, 0.75f, 1f));
                var start = Model.SupportL.localPosition;
                lp = Vector3.Lerp(Vector3.Lerp(start, cp + new Vector3(-0.02f, 0.01f, 0f), grab), start, back) + Vector3.back * 0.035f * pull;
                lr = Quaternion.Slerp(Quaternion.Slerp(Model.SupportL.localRotation, Quaternion.Euler(10f, 0f, 90f), grab), Model.SupportL.localRotation, back);
                Vm.Left.SetPose(pull > 0.1f ? HandPose.Hold : HandPose.Support);
                var moving = Model.ChargeRest != Vector3.zero ? Model.Charge : Model.Slide;
                var rest = Model.ChargeRest != Vector3.zero ? Model.ChargeRest : Model.SlideRest;
                if (moving != null)
                {
                    float travel = slideLocked && Def.Id == "glock" ? Mathf.Max(pull, 1f - Ease(Ramp(k, 0.3f, 0.5f))) : pull;
                    moving.localPosition = rest + Vector3.back * 0.035f * travel;
                }
                if (!chargePlayed1 && k > 0.35f) { chargePlayed1 = true; AudioManager.Play(Def.Id == "glock" ? "slide_back" : Def.Charge, Eye, 0.6f, 1f, 0f); }
                if (!chargePlayed2 && k > 0.62f) { chargePlayed2 = true; slideLocked = false; AudioManager.Play(Def.Id == "glock" ? "slide_fwd" : "bolt_release", Eye, 0.6f, 1f, 0f); }
            }
            Vm.LeftPos = lp;
            Vm.LeftRot = lr;

            if (reloadT >= 1f) FinishReload();
        }

        void TickShellReload(float dt, ref Vector3 pos, ref Quaternion rot)
        {
            // Rifle cants to show the loading port; each shell: hand to pouch → port → push.
            float tilt = Ease(Ramp(reloadT, 0f, 0.08f)) * (1f - Ease(Ramp(reloadT, 0.94f, 1f)));
            pos += new Vector3(-0.03f, 0.04f, 0.03f) * tilt;
            rot *= Quaternion.Euler(-6f * tilt, 12f * tilt, 38f * tilt);
            Vm.LeftOnAnchor = false;
            Vm.LeftInSocket = true;

            var port = Model.Eject != null ? Model.Eject.localPosition : Model.MagRest;
            var pouch = Quaternion.Inverse(Vm.Socket.localRotation) * (new Vector3(-0.08f, -0.45f, 0.25f) - Vm.Socket.localPosition);
            float elapsed = reloadT * reloadDur - 0.2f;
            int shell = Mathf.FloorToInt(elapsed / Def.ReloadTime);
            float k = elapsed < 0 ? 0f : (elapsed - shell * Def.ReloadTime) / Def.ReloadTime;
            Vector3 lp = Model.SupportL.localPosition;
            Quaternion lr = Model.SupportL.localRotation;
            if (elapsed >= 0 && shell < shellsToLoad)
            {
                var portPos = port + new Vector3(-0.005f, -0.035f, 0f);
                var portRot = Quaternion.Euler(-20f, 0f, 120f);
                if (k < 0.45f) { float s = Mathf.Sin(Ease(k / 0.45f) * Mathf.PI * 0.5f); lp = Vector3.Lerp(shell == 0 ? Model.SupportL.localPosition : portPos, pouch, s); lr = portRot; }
                else { float s = Ease((k - 0.45f) / 0.4f); lp = Vector3.Lerp(pouch, portPos, s) + Vector3.up * 0.02f * Bell(k, 0.85f, 1f); lr = portRot; }
                if (handMag != null) handMag.gameObject.SetActive(k > 0.3f && k < 0.86f);
                Vm.Left.SetPose(HandPose.Hold);
                if (k > 0.86f && shell + 1 > shellT)
                {
                    shellT = shell + 1;
                    Ammo = Mathf.Min(Capacity, Ammo + 1);
                    AudioManager.Play(Def.MagIn, Eye, 0.6f, Random.Range(0.95f, 1.05f), 0f);
                }
            }
            else if (shell >= shellsToLoad)
            {
                if (handMag != null) handMag.gameObject.SetActive(false);
                Vm.Left.SetPose(HandPose.Support);
                if (emptyReload)
                {
                    // Work the bolt handle once to chamber.
                    float c = Ramp(elapsed - shellsToLoad * Def.ReloadTime, 0f, Def.EmptyExtra);
                    float pull = Bell(c, 0.2f, 0.8f);
                    var handle = Model.ChargeRest;
                    lp = Vector3.Lerp(Model.SupportL.localPosition, handle + new Vector3(0.0f, -0.02f, 0f), Bell(c, 0f, 1f)) + Vector3.back * 0.04f * pull;
                    if (Model.Charge != null) Model.Charge.localPosition = Model.ChargeRest + Vector3.back * 0.04f * pull;
                    if (!chargePlayed1 && c > 0.3f) { chargePlayed1 = true; AudioManager.Play(Def.Charge, Eye, 0.6f, 1f, 0f); }
                }
            }
            Vm.LeftPos = lp;
            Vm.LeftRot = lr;
            if (reloadT >= 1f) FinishReload();
        }

        void DropMag()
        {
            magDropped = true;
            AudioManager.Play(Def.MagOut, Eye, 0.6f, 1f, 0f);
            if (Model.Mag == null) return;
            // Spawn a physical magazine where the viewmodel one is and let it fall to the floor.
            var src = Model.Mag;
            var mf = src.GetComponent<MeshFilter>();
            var go = new GameObject("Dropped Magazine");
            go.layer = Layers.Debris;
            go.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, true);
            // The viewmodel is drawn with its own FOV, so drop it slightly lower in the world to read right.
            go.transform.SetPositionAndRotation(src.position + Vector3.down * 0.1f, src.rotation);
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = src.GetComponent<Renderer>().sharedMaterial;
            var b = mf.sharedMesh.bounds;
            var bc = go.AddComponent<BoxCollider>();
            bc.center = b.center; bc.size = b.size;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.25f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            RB.SetVel(rb, Player.CC.velocity + Vector3.down * 1.2f - CamT.right * 0.3f);
            rb.angularVelocity = Random.insideUnitSphere * 4f;
            go.AddComponent<Surface>().Type = SurfaceType.Metal;
            go.AddComponent<ImpactSounds>();
            Debris.Register(go, 40f, Debris.GroupMag);
            src.gameObject.SetActive(false);
        }

        void InsertMag()
        {
            magInserted = true;
            AudioManager.Play(Def.MagIn, Eye, 0.65f, 1f, 0f);
            if (Model.Mag != null) { Model.Mag.gameObject.SetActive(true); Model.Mag.localPosition = Model.MagRest; }
            CameraFX.Punch(0.15f, 0f);
            // Tactical reload keeps the round in the chamber.
            bool chambered = Ammo > 0 && !OpenBolt;
            Ammo = Capacity + (chambered ? 1 : 0);
            if (emptyReload && OpenBolt) Ammo = Capacity;
        }

        void FinishReload()
        {
            if (!Def.ShellReload && !magInserted) InsertMag();
            if (emptyReload && !Def.ShellReload && !OpenBolt) Ammo = Capacity; // chambered from the new mag
            reloadT = -1f;
            slideLocked = false;
            if (handMag != null) handMag.gameObject.SetActive(false);
            if (Model.Slide != null) Model.Slide.localPosition = Model.SlideRest;
            if (Model.Charge != null && Model.ChargeRest != Vector3.zero) Model.Charge.localPosition = Model.ChargeRest;
            Vm.Left.SetPose(HandPose.Support);
            Stats.Add("reloads");
        }
    }
}
