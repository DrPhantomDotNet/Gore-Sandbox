using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Frag grenade. Press LMB (overhand) or RMB (short underhand toss) to pull the pin: the 3.5 s fuse starts then,
    /// so holding the button "cooks" it (fuse shown on the HUD). Release to throw. Holding it too long is a bad idea.
    /// Carries a stack of 5 that refills like ammo.
    /// </summary>
    public sealed class FragGrenade : Weapon, IRefillable
    {
        enum State { Idle, Cooking, Throwing, Drawing }
        State state;
        float t, cook;
        bool underhand, released;
        int count = -1;

        int Max => Mathf.Max(1, Def.Mag);
        public override bool Busy => state != State.Idle;
        public override string AmmoText => Rules.InfiniteAmmo ? "INF" : count.ToString();
        public override string ModeText => state == State.Cooking ? "FUSE " + Mathf.Max(0f, Explosives.FragFuse - cook).ToString("0.0") + "s" : count > 0 || Rules.InfiniteAmmo ? (underhand ? "UNDERHAND" : "LMB THROW / RMB TOSS") : "EMPTY";

        public void Refill()
        {
            count = Max;
            if (Equipped && state == State.Idle) ShowGrenade(true);
        }

        protected override void OnAttach() { if (count < 0) count = Max; }

        void ShowGrenade(bool on)
        {
            if (Model == null) return;
            if (Model.Mag != null) { Model.Mag.gameObject.SetActive(on); Model.Mag.localPosition = Model.MagRest; }
            if (Model.Charge != null) { Model.Charge.gameObject.SetActive(on); Model.Charge.localPosition = Model.ChargeRest; }
        }

        bool HasAny => count > 0 || Rules.InfiniteAmmo;

        protected override void OnEquip()
        {
            Vm.Right.SetPose(HandPose.Hold);
            Vm.Left.SetPose(HandPose.Relaxed);
            state = State.Idle;
            ShowGrenade(HasAny);
        }

        protected override void OnHolster()
        {
            // Holstering a cooked grenade drops it at your feet.
            if (state == State.Cooking) Release(true);
            state = State.Idle;
            Vm.Right.SetPose(HandPose.Relaxed);
        }

        public override void Tick(InputFrame f, float dt)
        {
            var idle = Def.HipPos + new Vector3(0, Mathf.Sin(Time.time * 1.6f) * 0.003f, 0);
            var pos = idle;
            var rot = Quaternion.Euler(-10f, -12f, 0f);
            Vm.LeftOnAnchor = false;
            Vm.LeftInSocket = false;
            var leftPos = new Vector3(-0.17f, -0.22f, 0.32f);
            var leftRot = Quaternion.Euler(-12f, 10f, 72f);

            switch (state)
            {
                case State.Idle:
                    if (HasAny && (f.Fire || f.AimPressed))
                    {
                        underhand = !f.Fire;
                        state = State.Cooking;
                        cook = 0f;
                        t = 0f;
                        inspectT = -1f;
                        AudioManager.Play("grenade_pin", Eye, 0.7f, Random.Range(0.95f, 1.05f), 0f);
                    }
                    else if (HasAny) TickInspect(f, dt, ref pos, ref rot, 1.6f);
                    break;

                case State.Cooking:
                {
                    cook += dt;
                    t += dt;
                    // Pin pull: the left hand grabs the ring and yanks it away.
                    float grab = Ease(Ramp(t, 0f, 0.15f)), pull = Ease(Ramp(t, 0.15f, 0.3f)), back = Ease(Ramp(t, 0.3f, 0.5f));
                    var pinAt = Model.ChargeRest + new Vector3(-0.1f, 0.02f, 0f) * pull;
                    if (Model.Charge != null)
                    {
                        Model.Charge.localPosition = pinAt;
                        if (back > 0.5f) Model.Charge.gameObject.SetActive(false);
                    }
                    if (back < 1f)
                    {
                        Vm.LeftInSocket = true;
                        leftPos = pinAt + new Vector3(-0.04f, -0.02f, -0.07f);
                        leftRot = Quaternion.Euler(-20f, 30f, 60f);
                        Vm.LeftPos = leftPos; Vm.LeftRot = leftRot;
                        pos = Vector3.Lerp(idle, idle + new Vector3(-0.06f, 0.03f, 0.02f), grab * (1f - back));
                    }
                    // Then wind up (arm back over the shoulder, or low for the underhand toss) while the free hand points.
                    float wind = Ease(Ramp(t, 0.35f, 0.65f));
                    var windPos = underhand ? new Vector3(0.18f, -0.36f, 0.18f) : new Vector3(0.22f, 0.0f, 0.08f);
                    var windRot = underhand ? Quaternion.Euler(35f, -20f, 0f) : Quaternion.Euler(-45f, -25f, -20f);
                    pos = Vector3.Lerp(pos, windPos, wind);
                    rot = Quaternion.Slerp(rot, windRot, wind);
                    if (back >= 1f)
                    {
                        Vm.LeftInSocket = false;
                        leftPos = Vector3.Lerp(leftPos, new Vector3(-0.2f, -0.08f, 0.45f), wind);
                        leftRot = Quaternion.Slerp(leftRot, Quaternion.Euler(-30f, 5f, 80f), wind);
                    }
                    // Fuse runs while cooking: shake a little near the end.
                    float left = Explosives.FragFuse - cook;
                    if (left < 1f) pos += Random.insideUnitSphere * 0.003f;
                    bool held = underhand ? f.Aim : f.FireHeld;
                    if (left <= 0.05f) Release(true);
                    else if (!held && t > 0.32f) { state = State.Throwing; t = 0f; released = false; Swing("whoosh_small", 0.6f); }
                    break;
                }

                case State.Throwing:
                {
                    t += dt / 0.42f;
                    if (!released) cook += dt;
                    var windPos = underhand ? new Vector3(0.18f, -0.36f, 0.18f) : new Vector3(0.22f, 0.0f, 0.08f);
                    var windRot = underhand ? Quaternion.Euler(35f, -20f, 0f) : Quaternion.Euler(-45f, -25f, -20f);
                    var outPos = underhand ? new Vector3(0.05f, -0.12f, 0.5f) : new Vector3(0.04f, -0.12f, 0.55f);
                    var outRot = underhand ? Quaternion.Euler(-35f, -5f, 0f) : Quaternion.Euler(45f, -5f, 10f);
                    float k = Ease(Ramp(t, 0f, 0.45f)), ret = Ease(Ramp(t, 0.5f, 1f));
                    pos = Vector3.Lerp(Vector3.Lerp(windPos, outPos, k), idle + Vector3.down * 0.3f, ret);
                    rot = Quaternion.Slerp(Quaternion.Slerp(windRot, outRot, k), rot, ret);
                    leftPos = new Vector3(-0.2f, -0.08f, 0.45f);
                    leftRot = Quaternion.Euler(-30f, 5f, 80f);
                    if (!released && t >= 0.38f) Release(false);
                    if (t >= 1f) { state = State.Drawing; t = 0f; ShowGrenade(HasAny); }
                    break;
                }

                case State.Drawing:
                    t += dt / 0.35f;
                    pos = Vector3.Lerp(idle + Vector3.down * 0.3f, idle, Ease(Mathf.Clamp01(t)));
                    if (t >= 1f) state = State.Idle;
                    break;
            }

            if (!Vm.LeftInSocket) { Vm.LeftPos = leftPos; Vm.LeftRot = leftRot; }
            Vm.TargetPos = pos;
            Vm.TargetRot = rot;
        }

        /// <summary>Lets go of the grenade: thrown, or dropped at your feet (cooked off / holstered).</summary>
        void Release(bool drop)
        {
            released = true;
            if (!Rules.InfiniteAmmo) count = Mathf.Max(0, count - 1);
            float fuse = Explosives.FragFuse - cook;
            Vector3 origin, vel;
            if (drop)
            {
                origin = Eye + Aim * 0.3f + Vector3.down * 0.4f;
                vel = Vector3.down * 0.5f;
            }
            else
            {
                origin = Eye + Aim * 0.45f + CamT.right * 0.1f + CamT.up * (underhand ? -0.2f : 0.05f);
                vel = underhand ? Aim * 7f + Vector3.up * 2.2f : Aim * 17f + Vector3.up * 2.5f;
            }
            if (Physics.Linecast(Eye, origin, out var block, Layers.WorldMask, QueryTriggerInteraction.Ignore)) origin = block.point + block.normal * 0.06f;
            Explosives.Launch(ThrowableKind.Grenade, origin, vel, fuse, Player);
            if (Model.Mag != null) Model.Mag.gameObject.SetActive(false);
            if (Model.Charge != null) Model.Charge.gameObject.SetActive(false);
            if (drop) { state = State.Drawing; t = 0f; ShowGrenade(HasAny); }
            cook = 0f;
        }
    }
}
