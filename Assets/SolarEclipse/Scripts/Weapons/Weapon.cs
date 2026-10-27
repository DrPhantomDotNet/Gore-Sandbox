using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Base for everything held in the hands. Weapons are plain objects owned by the Inventory; each one owns a
    /// viewmodel mesh under the Viewmodel socket and drives the viewmodel pose every frame while equipped.
    /// </summary>
    public abstract class Weapon
    {
        public WeaponDef Def;
        public WeaponModel Model;
        protected PlayerController Player;
        protected Viewmodel Vm;
        protected float aim;           // 0..1 smoothed ADS
        protected float inspectT = -1f;
        public bool Equipped { get; private set; }

        public virtual bool IsAiming => false;
        public float AimWeight => aim;
        public virtual float AimFov => Def.AimFov;
        public virtual bool ConsumesKick => false;
        public virtual bool ConsumesScroll => false;
        public virtual bool Busy => false;
        /// <summary>Text shown on the HUD (ammo etc.).</summary>
        public virtual string AmmoText => "";
        public virtual string ModeText => "";
        /// <summary>Multiplies the player's walk speed while this weapon is held (e.g. a spinning minigun).</summary>
        public virtual float MoveSpeedMul => 1f;
        /// <summary>Full-screen HUD overlay drawn under the HUD (e.g. a sniper scope). Virtual canvas units.</summary>
        public virtual void DrawOverlay(float W, float H) { }

        public static Weapon Create(WeaponDef def)
        {
            var extra = Guns2.Create(def); // Weapons/Guns2.cs: shotgun, sniper, revolver, minigun, harpoon gun
            if (extra != null) return extra;
            var boom = Explosives.CreateWeapon(def); // Weapons/Explosives.cs: rocket launcher, frag, C4, katana, sledgehammer
            if (boom != null) return boom;
            switch (def.Kind)
            {
                case WeaponKind.Fists: return new Fists { Def = def };
                case WeaponKind.Knife: return new Knife { Def = def };
                case WeaponKind.Admin: return new AdminGun { Def = def };
                case WeaponKind.SpawnGun: return new SpawnGun { Def = def };
                case WeaponKind.RopeTool: return new RopeTool { Def = def };
                case WeaponKind.GravityGun: return new GravityGun { Def = def };
                default: return new Firearm { Def = def };
            }
        }

        public void Attach(PlayerController p)
        {
            Player = p;
            Vm = p.Viewmodel;
            Model = WeaponModels.Build(Def.Id, Layers.Viewmodel);
            Model.transform.SetParent(Vm.Socket, false);
            IconFactory.SetLayer(Model.gameObject, Layers.Viewmodel);
            foreach (var r in Model.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Model.gameObject.SetActive(false);
            OnAttach();
        }

        protected virtual void OnAttach() { }

        public void Equip()
        {
            Equipped = true;
            Model.gameObject.SetActive(true);
            Model.transform.SetAsFirstSibling();
            Vm.PlayEquip();
            Vm.ResetHands();
            Vm.TargetPos = Def.HipPos;
            Vm.TargetRot = Quaternion.identity;
            aim = 0f;
            inspectT = -1f;
            OnEquip();
        }

        public void Holster()
        {
            Equipped = false;
            OnHolster();
            Model.gameObject.SetActive(false);
            Player.LockLook = false;
        }

        protected virtual void OnEquip() { }
        protected virtual void OnHolster() { }

        /// <summary>Per-frame update while equipped.</summary>
        public abstract void Tick(InputFrame f, float dt);

        public virtual void Destroy() { if (Model != null) Object.Destroy(Model.gameObject); }

        // ---- shared helpers

        protected Vector3 Eye => Player.EyePosition;
        protected Vector3 Aim => Player.AimDirection;
        protected Transform CamT => Player.Cam.transform;

        /// <summary>
        /// Camera-local socket position for aiming down the iron sights: with <see cref="AimRot"/> applied, the rear sight
        /// lands on the camera's forward axis Def.AimDistance in front of the eye and the front post further along it.
        /// Falls back to centring the single Sight anchor for models without iron sights.
        /// </summary>
        protected Vector3 AimPos()
        {
            if (Model == null) return Def.HipPos;
            if (Model.SightRear != null && Model.SightFront != null)
                return new Vector3(0f, 0f, Def.AimDistance) - AimRot() * SocketLocal(Model.SightRear);
            if (Model.Sight == null) return Def.HipPos;
            var s = Model.Sight.localPosition;
            return new Vector3(-s.x, -s.y, Def.AimDistance - s.z);
        }

        /// <summary>Socket-local rotation that turns the SightRear→SightFront line onto the camera's forward axis (no roll).</summary>
        protected Quaternion AimRot()
        {
            if (Model == null || Model.SightRear == null || Model.SightFront == null) return Quaternion.identity;
            var d = SocketLocal(Model.SightFront) - SocketLocal(Model.SightRear);
            if (d.sqrMagnitude < 1e-8f) return Quaternion.identity;
            // LookRotation maps +Z to the sight line (keeping the model's up as up); its inverse maps the sight line to +Z.
            var up = Model.transform.localRotation * Vector3.up;
            return Quaternion.Inverse(Quaternion.LookRotation(d, up));
        }

        /// <summary>A model anchor's position in the viewmodel socket's local space (independent of the socket's current pose).</summary>
        Vector3 SocketLocal(Transform anchor)
        {
            var mt = Model.transform;
            var p = Vector3.Scale(mt.localScale, mt.InverseTransformPoint(anchor.position));
            return mt.localPosition + mt.localRotation * p;
        }

        protected static float Ease(float t) => t * t * (3f - 2f * t);
        protected static float Bell(float t, float a, float b) => t <= a || t >= b ? 0f : Mathf.Sin((t - a) / (b - a) * Mathf.PI);
        protected static float Ramp(float t, float a, float b) => Mathf.Clamp01((t - a) / Mathf.Max(0.0001f, b - a));

        /// <summary>A generic weapon "inspect" flourish (Q): tilt toward the camera and back.</summary>
        protected bool TickInspect(InputFrame f, float dt, ref Vector3 pos, ref Quaternion rot, float duration = 2.4f)
        {
            if (f.Inspect && inspectT < 0f && !Busy)
            {
                inspectT = 0f;
                AudioManager.Play("cloth", Eye, 0.4f, 1f, 0f);
            }
            if (inspectT < 0f) return false;
            inspectT += dt / duration;
            if (inspectT >= 1f || f.Fire || f.Aim) { inspectT = -1f; return false; }
            float a = Ease(Ramp(inspectT, 0f, 0.2f)) * (1f - Ease(Ramp(inspectT, 0.8f, 1f)));
            float mid = Ease(Ramp(inspectT, 0.4f, 0.6f));
            pos += new Vector3(-0.09f, 0.04f, 0.02f) * a;
            rot *= Quaternion.Euler(Mathf.Lerp(-10f, 15f, mid) * a, -50f * a, Mathf.Lerp(40f, -70f, mid) * a);
            return true;
        }

        protected void Swing(string clip = "whoosh_small", float vol = 0.6f) =>
            AudioManager.Play(clip, Eye + Aim * 0.5f, vol, Random.Range(0.9f, 1.12f), 0f);

        /// <summary>Melee sweep: returns the first thing in front within reach.</summary>
        protected bool MeleeTrace(Vector3 dir, float reach, float radius, out RaycastHit hit)
        {
            var mask = Layers.ShootMask;
            if (Physics.Raycast(Eye, dir, out hit, reach, mask, QueryTriggerInteraction.Ignore)) return true;
            return Physics.SphereCast(Eye, radius, dir, out hit, reach - radius, mask, QueryTriggerInteraction.Ignore);
        }
    }
}
