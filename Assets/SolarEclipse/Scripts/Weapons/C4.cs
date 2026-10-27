using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// C4 with a hand-held detonator. LMB throws a sticky charge (sticks to walls, props and Goredoll limbs; up to
    /// <see cref="Explosives.MaxC4"/> live charges), RMB presses the detonator: beep-beep, then every charge goes off.
    /// </summary>
    public sealed class C4Weapon : Weapon
    {
        float throwT = -1f, pressT = -1f, idle;
        bool thrown;

        public override bool Busy => throwT >= 0f;
        public override string AmmoText => Explosives.ChargeCount(Explosives.LocalOwner) + "/" + Explosives.MaxC4;
        public override string ModeText => Explosives.ChargeCount(Explosives.LocalOwner) > 0 ? "RMB: DETONATE" : "LMB: THROW CHARGE";

        protected override void OnEquip()
        {
            Vm.Right.SetPose(HandPose.Hold);
            Vm.Left.SetPose(HandPose.Hold);
            throwT = pressT = -1f;
            ShowBrick(true);
        }

        protected override void OnHolster()
        {
            Vm.Right.SetPose(HandPose.Relaxed);
            Vm.Left.SetPose(HandPose.Relaxed);
        }

        void ShowBrick(bool on)
        {
            if (Model == null || Model.Mag == null) return;
            Model.Mag.gameObject.SetActive(on);
            Model.Mag.localPosition = Model.MagRest;
        }

        public override void Tick(InputFrame f, float dt)
        {
            idle += dt;
            var pos = Def.HipPos + new Vector3(0, Mathf.Sin(idle * 1.6f) * 0.003f, 0);
            var rot = Quaternion.Euler(-8f, -6f, 0f);
            int live = Explosives.ChargeCount(Explosives.LocalOwner);

            if (throwT < 0f && f.Fire)
            {
                if (live >= Explosives.MaxC4) { AudioManager.Ui("ui_error", 0.4f); GameEvents.ShowToast("Max " + Explosives.MaxC4 + " charges - detonate first (RMB)"); }
                else { throwT = 0f; thrown = false; inspectT = -1f; Swing("whoosh_small", 0.5f); }
            }
            if (pressT < 0f && f.AimPressed)
            {
                pressT = 0f;
                AudioManager.Play("c4_arm", Eye, 0.7f, 1f, 0f);
                if (live > 0) Explosives.Detonate();
            }

            if (throwT >= 0f)
            {
                throwT += dt / 0.55f;
                // Lob the charge with the right hand (the detonator stays in the left).
                float wind = Ease(Ramp(throwT, 0f, 0.3f)), go = Ease(Ramp(throwT, 0.3f, 0.5f)), back = Ease(Ramp(throwT, 0.6f, 1f));
                var brickPos = Model.MagRest + new Vector3(0.05f, 0.08f, -0.12f) * wind * (1f - go) + new Vector3(-0.02f, 0.02f, 0.3f) * go * (1f - back);
                if (Model.Mag != null && !thrown) Model.Mag.localPosition = brickPos;
                Vm.RightOnAnchor = false;
                Vm.RightInSocket = true;
                Vm.RightPos = Model.GripR.localPosition + (brickPos - Model.MagRest);
                Vm.RightRot = Model.GripR.localRotation * Quaternion.Euler(-30f * wind + 50f * go * (1f - back), 0, 0);
                if (!thrown && throwT >= 0.45f)
                {
                    thrown = true;
                    var origin = Eye + Aim * 0.5f + CamT.right * 0.12f - CamT.up * 0.05f;
                    if (Physics.Linecast(Eye, origin, out var block, Layers.WorldMask, QueryTriggerInteraction.Ignore)) origin = block.point + block.normal * 0.08f;
                    Explosives.Launch(ThrowableKind.C4, origin, Aim * 10f + Vector3.up * 1.2f, 0f, Player);
                    ShowBrick(false);
                }
                if (throwT >= 0.8f && thrown && Model.Mag != null && !Model.Mag.gameObject.activeSelf) ShowBrick(true);
                if (throwT >= 1f) { throwT = -1f; Vm.RightOnAnchor = true; Vm.RightInSocket = false; ShowBrick(true); }
            }
            else
            {
                Vm.RightOnAnchor = true;
                Vm.RightInSocket = false;
                TickInspect(f, dt, ref pos, ref rot, 1.6f);
            }

            // Detonator thumb press.
            if (pressT >= 0f)
            {
                pressT += dt / 0.35f;
                float p = Bell(pressT, 0f, 1f);
                if (Model.Charge != null) Model.Charge.localPosition = Model.ChargeRest + Vector3.down * 0.006f * p;
                pos += new Vector3(0f, -0.01f, 0.01f) * p;
                if (pressT >= 1f) pressT = -1f;
            }
            // Detonator LED: blinks while charges are armed.
            if (Model.Glow != null) Model.Glow.gameObject.SetActive(live > 0 ? Mathf.Repeat(idle, 0.6f) < 0.3f : false);

            Vm.TargetPos = pos;
            Vm.TargetRot = rot;
        }
    }
}
