using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Developer gun with infinite ammo. B cycles modes: Smite (lightning), Gib, Launch, Revive, Delete,
    /// Ignite (explosive round) and Pacify (freezes the target's brain).
    /// </summary>
    public sealed class AdminGun : Weapon
    {
        public enum Mode { Smite, Gib, Launch, Revive, Delete, Explode, Pacify }
        public Mode Current = Mode.Smite;
        float nextFire, kick, charge;
        static readonly Color[] ModeColors =
        {
            new Color(0.4f, 0.9f, 1f), new Color(1f, 0.25f, 0.2f), new Color(1f, 0.85f, 0.2f), new Color(0.3f, 1f, 0.45f),
            new Color(0.75f, 0.4f, 1f), new Color(1f, 0.5f, 0.1f), new Color(1f, 0.6f, 0.9f),
        };

        public override string AmmoText => "INF";
        public override string ModeText => Current.ToString().ToUpper();
        public override bool IsAiming => PlayerInput.Frame.Aim && !Player.Sprinting;
        public Color ModeColor => ModeColors[(int)Current];

        protected override void OnEquip()
        {
            Vm.Right.SetPose(HandPose.PistolGrip);
            Vm.Left.SetPose(HandPose.Support);
            AudioManager.Play("admin_mode", Eye, 0.4f, 1f, 0f);
            Tint();
        }

        void Tint()
        {
            foreach (var r in Model.GlowRenderers)
                if (r != null) r.material.SetColor("_Color", ModeColor * 2.5f);
        }

        public override void Tick(InputFrame f, float dt)
        {
            Vm.ResetHands();
            aim = MathX.Damp(aim, IsAiming ? 1f : 0f, 14f, dt);
            Vm.AimWeight = aim;
            if (f.Mode)
            {
                Current = (Mode)(((int)Current + 1) % ModeColors.Length);
                AudioManager.Play("admin_mode", Eye, 0.5f, 1f + (int)Current * 0.05f, 0f);
                GameEvents.ShowToast("Admin Gun: " + Current);
                Tint();
            }
            if (f.Fire && Time.time >= nextFire) Fire();
            kick = Mathf.MoveTowards(kick, 0f, dt * 6f);
            charge += dt;
            if (Model.Glow != null) Model.Glow.localScale = Vector3.one * (1f + Mathf.Sin(charge * 8f) * 0.03f + kick * 0.15f);

            var pos = Vector3.Lerp(Def.HipPos, AimPos(), Ease(aim));
            var rot = Quaternion.Slerp(Quaternion.identity, AimRot(), Ease(aim));
            TickInspect(f, dt, ref pos, ref rot);
            Vm.TargetPos = pos;
            Vm.TargetRot = rot;
        }

        void Fire()
        {
            nextFire = Time.time + 60f / Def.Rpm;
            kick = 1f;
            AudioManager.Play(Def.FireSound, Eye, 0.9f, 1f + (int)Current * 0.04f, 0f);
            Vm.AddRecoil(new Vector3(0, 0.1f, -0.6f) * 0.3f, new Vector3(-18f, 0, Random.Range(-6f, 6f)));
            CameraFX.Punch(0.6f, 0f);
            FX.Flash(Model.Muzzle.position, ModeColor, 6f, 3f, 0.08f);
            if (NetState.IsClient)
            {
                // The host performs it (Creative mode required); show our own tracer right away.
                if (Physics.Raycast(Eye, Aim, out var h, 600f, Layers.ShootMask, QueryTriggerInteraction.Ignore)) Tracers.Spawn(Model.Muzzle.position, h.point, ModeColor);
                NetManager.I.RequestAdmin(Current, Eye, Aim);
                return;
            }
            Perform(Current, Eye, Aim, Model.Muzzle.position, Player);
        }

        static readonly Color[] PerformColors = ModeColors;

        /// <summary>Applies one Admin Gun shot. Runs on the host for every player (source = who fired).</summary>
        public static void Perform(Mode mode, Vector3 eye, Vector3 aim, Vector3 muzzle, object source)
        {
            var color = PerformColors[(int)mode];
            if (NetState.IsHost)
            {
                byte shooter = NetManager.I.CurrentRequester != null ? NetManager.I.CurrentRequester.Id : NetManager.I.LocalId;
                NetManager.I.BroadcastFire(shooter, WeaponCatalog.All.FindIndex(w => w.Kind == WeaponKind.Admin), eye, new System.Collections.Generic.List<Vector3> { aim });
            }
            if (!Physics.Raycast(eye, aim, out var hit, 600f, Layers.ShootMask | (1 << Layers.Debris), QueryTriggerInteraction.Ignore))
            {
                Tracers.Spawn(muzzle, eye + aim * 200f, color);
                return;
            }
            Tracers.Spawn(muzzle, hit.point, color);
            var part = hit.collider.GetComponent<BodyPart>();
            var doll = part != null ? part.Doll : hit.collider.GetComponentInParent<Goredoll>();
            DamageInfo Dmg(float amount) => new DamageInfo { Amount = amount, Force = aim * 60f, Point = hit.point, Normal = hit.normal, Direction = aim, Type = DamageType.Admin, Source = source, Weapon = "Admin Gun", Penetration = 3f };
            switch (mode)
            {
                case Mode.Smite:
                    Lightning(hit.point, source);
                    if (doll != null)
                    {
                        doll.Kill("Struck by lightning (Admin Gun)");
                        doll.Launch(Vector3.up * 6f + aim * 4f);
                        foreach (var p in doll.Parts.Values) if (p != null && p.Renderer != null) p.Renderer.material.color = new Color(0.25f, 0.22f, 0.2f);
                    }
                    else PushRigidbody(hit, eye, 30f);
                    break;
                case Mode.Gib:
                    if (part != null && part.Gibbable) part.Doll.Dismember.Gib(part, aim);
                    else if (part != null) part.TakeDamage(Dmg(999f));
                    else Explosion.Create(hit.point, 1.5f, 40f, source, 0.5f);
                    break;
                case Mode.Launch:
                    if (doll != null) doll.Launch(aim * 22f + Vector3.up * 9f);
                    else PushRigidbody(hit, eye, 400f);
                    AudioManager.Play("boing", hit.point, 0.7f);
                    break;
                case Mode.Revive:
                    if (doll != null)
                    {
                        bool ok = doll.Revive();
                        GameEvents.ShowToast(ok ? doll.DisplayName + " was revived." : "Can't revive — no head to put back on.");
                        if (ok) EasterEggs.OnRevived(doll);
                        FX.Flash(hit.point, color, 5f, 4f, 0.4f);
                        AudioManager.Play(ok ? "ui_success" : "ui_error", hit.point, 0.8f);
                    }
                    break;
                case Mode.Delete:
                    if (doll != null) { FX.Dust(doll.Position, 0.6f, color, 30); doll.Despawn(); AudioManager.Play("sg_delete", hit.point, 0.8f); }
                    else
                    {
                        var go = SpawnGun.DeletableRoot(hit.collider);
                        if (go != null) { FX.Dust(hit.point, 0.4f, color, 20); SpawnGun.DeleteObject(go); AudioManager.Play("sg_delete", hit.point, 0.8f); }
                    }
                    break;
                case Mode.Explode:
                    Explosion.Create(hit.point + hit.normal * 0.2f, 4f, 160f, source, 1.2f);
                    break;
                case Mode.Pacify:
                    if (doll != null && doll.Brain != null)
                    {
                        doll.Brain.ForceDance = true;
                        GameEvents.ShowToast(doll.DisplayName + " feels strangely calm.");
                        if (source is PlayerController) EasterEggs.Found("Pacifist", "Make dance, not war.");
                    }
                    break;
            }
        }

        static void PushRigidbody(RaycastHit hit, Vector3 eye, float impulse)
        {
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                hit.rigidbody.AddForceAtPosition((hit.point - eye).normalized * impulse + Vector3.up * impulse * 0.3f, hit.point, ForceMode.Impulse);
        }

        static void Lightning(Vector3 at, object source)
        {
            // Jagged bolt from the sky.
            var top = at + Vector3.up * 40f + Random.insideUnitSphere * 4f;
            var prev = top;
            for (int i = 1; i <= 10; i++)
            {
                var p = Vector3.Lerp(top, at, i / 10f) + (i < 10 ? Random.insideUnitSphere * 1.2f : Vector3.zero);
                Tracers.Spawn(prev, p, new Color(0.7f, 0.9f, 1f));
                prev = p;
            }
            FX.Flash(at + Vector3.up, new Color(0.7f, 0.85f, 1f), 30f, 8f, 0.25f);
            FX.Sparks(at, Vector3.up, 30, 9f);
            Decals.Scorch(at, Vector3.up, 1.2f, null);
            CameraFX.ShakeAt(at, 0.5f, 25f);
            CameraFX.Flash(new Color(0.8f, 0.9f, 1f), 0.35f);
            AudioManager.Play("explosion", at, 1f, 1.3f, 1f, 5f, 300f);
            GameEvents.EmitNoise(at, 80f, 1f, source);
        }
    }
}
