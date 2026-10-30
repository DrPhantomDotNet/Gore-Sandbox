using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// One data-driven melee weapon for the arsenal pack (bats, crowbar, pipe, pan, machete, cleaver, combat knife,
    /// fire axe, chainsaw). Everything comes from a <see cref="MeleeSpec"/>:
    /// LMB = alternating diagonal swings that sweep rays through the arc (click during the follow-through to combo),
    /// or hold-to-wind-up for <see cref="MeleeSpec.ChargeSwing"/>; RMB = heavy overhead swing, or a stab for knives;
    /// <see cref="MeleeSpec.Continuous"/> = hold LMB to run a chainsaw. Blades slice limbs where they cross them
    /// (Dismemberment.Slice, like the katana); blunt weapons break bones, crush skulls and launch bodies (like the sledgehammer).
    /// Multiplayer: hits go through BodyPart.TakeDamage / NetPhysics like the katana and sledgehammer.
    /// </summary>
    public sealed class MeleeWeapon : Weapon
    {
        public MeleeSpec Spec;

        enum Act { None, Swing, Heavy, Stab, Wind }
        Act act;
        float t, dur, power = 1f, charge, idle, comboT;
        int side = 1, combo, hitCount;
        bool queued, blocked, impactDone;
        Vector3 lastSample;
        readonly HashSet<Rigidbody> hitBodies = new HashSet<Rigidbody>();
        readonly HashSet<Collider> hitCols = new HashSet<Collider>();

        // chainsaw
        AudioSource idleLoop, revLoop;
        float rev, sawTick, sawOnPart, cutSoundT, chainPhase;
        BodyPart sawPart;
        Transform chain;
        Vector3 chainRest;

        bool TwoHanded => Spec.TwoHanded;
        public override bool Busy => act != Act.None;
        public override string ModeText => Spec.Continuous ? (rev > 0.5f ? "CUTTING" : "IDLE") : act == Act.Wind ? "POWER " + Mathf.RoundToInt(Charge01 * 100f) + "%" : combo > 1 ? "COMBO x" + combo : "";
        float Charge01 => Mathf.Clamp01(charge / 0.9f);
        public override float MoveSpeedMul => Spec.Continuous ? 0.9f : Spec.ChargeSwing ? 0.95f : 1f;

        Vector3 IdlePos => Spec.Continuous ? new Vector3(0.1f, -0.27f, 0.22f) : TwoHanded ? new Vector3(0.12f, -0.22f, 0.3f) : new Vector3(0.15f, -0.19f, 0.32f);
        Quaternion IdleRot => Spec.Continuous ? Quaternion.Euler(4f, -6f, 0f) : TwoHanded ? Quaternion.Euler(-40f, -8f, -8f) : Quaternion.Euler(Spec.Blade ? -12f : -30f, -12f, -12f);

        protected override void OnAttach()
        {
            chain = Model.transform.Find("Chain");
            if (chain != null) chainRest = chain.localPosition;
        }

        protected override void OnEquip()
        {
            var pose = Spec.Blade || Spec.Continuous ? HandPose.Knife : HandPose.Fist;
            Vm.Right.SetPose(pose);
            Vm.Left.SetPose(TwoHanded ? pose : HandPose.Fist);
            Vm.LeftOnAnchor = TwoHanded;
            act = Act.None;
            combo = 0;
            rev = 0f;
            AudioManager.Play(Spec.Blade ? "katana_draw" : "cloth", Eye, 0.45f, Random.Range(0.95f, 1.05f), 0f);
            if (Spec.Continuous)
            {
                AudioManager.Play("chainsaw_start", Eye, 0.8f, 1f, 0f);
                if (idleLoop == null) idleLoop = AudioManager.CreateLoop("chainsaw_idle", Model.transform, 0.35f, AudioBus.Sfx, 40f, false);
                if (revLoop == null) revLoop = AudioManager.CreateLoop("chainsaw_rev", Model.transform, 0f, AudioBus.Sfx, 50f, false);
                if (idleLoop != null) { idleLoop.spatialBlend = 0f; idleLoop.PlayDelayed(0.5f); }
                if (revLoop != null) revLoop.spatialBlend = 0f;
            }
        }

        protected override void OnHolster()
        {
            act = Act.None;
            if (idleLoop != null) idleLoop.Stop();
            if (revLoop != null) revLoop.Stop();
            if (Spec.Continuous && rev > 0.05f) AudioManager.Play("chainsaw_stop", Eye, 0.6f, 1f, 0f);
            rev = 0f;
            Vm.Right.SetPose(HandPose.Relaxed);
            Vm.Left.SetPose(HandPose.Relaxed);
        }

        public override void Tick(InputFrame f, float dt)
        {
            idle += dt;
            var rest = IdlePos + new Vector3(0, Mathf.Sin(idle * 1.6f) * 0.003f, 0);
            var restRot = IdleRot;
            var pos = rest;
            var rot = restRot;

            if (Spec.Continuous) TickSaw(f, dt, ref pos, ref rot);
            else
            {
                if (act == Act.None)
                {
                    if (comboT > 0f) { comboT -= dt; if (comboT <= 0f) combo = 0; }
                    if (Spec.ChargeSwing && f.Fire) { act = Act.Wind; charge = 0f; inspectT = -1f; AudioManager.Play("cloth", Eye, 0.4f, 0.9f, 0f); }
                    else if (!Spec.ChargeSwing && (f.Fire || queued)) Begin(Act.Swing, 1f);
                    else if (f.AimPressed) Begin(Spec.RmbStab ? Act.Stab : Spec.ChargeSwing ? Act.Swing : Act.Heavy, Spec.ChargeSwing ? 0.55f : 1f);
                }
                else if (f.Fire && act == Act.Swing && t > 0.45f && !Spec.ChargeSwing) queued = true;

                switch (act)
                {
                    case Act.Wind: TickWind(f, dt, ref pos, ref rot, rest, restRot); break;
                    case Act.Swing: TickSwing(dt, ref pos, ref rot, rest, restRot); break;
                    case Act.Heavy: TickHeavy(dt, ref pos, ref rot, rest, restRot); break;
                    case Act.Stab: TickStab(dt, ref pos, ref rot, rest, restRot); break;
                    default: TickInspect(f, dt, ref pos, ref rot, 2f); break;
                }
            }

            Vm.TargetPos = pos;
            Vm.TargetRot = rot;
            if (!TwoHanded)
            {
                // Free hand in a loose guard on the left.
                Vm.LeftOnAnchor = false;
                Vm.LeftPos = new Vector3(-0.17f, -0.2f, 0.33f) + new Vector3(0, Mathf.Sin(idle * 1.7f + 1f) * 0.003f, 0);
                Vm.LeftRot = Quaternion.Euler(-12f, 10f, 72f);
            }
        }

        // ------------------------------------------------------------------ swings

        void Begin(Act a, float pw)
        {
            bool chained = queued || comboT > 0f;
            queued = false;
            act = a;
            t = 0f;
            power = pw;
            blocked = impactDone = false;
            hitBodies.Clear();
            hitCols.Clear();
            hitCount = 0;
            inspectT = -1f;
            combo = a == Act.Swing && chained ? combo + 1 : 1;
            if (a == Act.Swing)
            {
                side = -side;
                dur = Spec.SwingTime * Mathf.Lerp(1f, 0.78f, Mathf.Clamp01((combo - 1) / 3f));
            }
            else if (a == Act.Heavy) dur = Spec.ChargeSwing ? Mathf.Lerp(0.6f, 0.45f, power) * Spec.SwingTime / 0.5f : Spec.SwingTime * 1.7f;
            else dur = Mathf.Max(0.42f, Spec.SwingTime * 1.2f);
            lastSample = SweepDir(0f);
            bool heavy = a == Act.Heavy || !Spec.Blade && TwoHanded;
            AudioManager.Play(Spec.SwingSound, Eye + Aim * 0.5f, heavy ? 0.75f : 0.6f, Random.Range(0.9f, 1.1f) * (heavy ? 0.85f : 1f), 0f);
            if (a == Act.Heavy && power > 0.6f) CameraFX.Punch(-0.8f * power, 0.3f);
        }

        void End()
        {
            act = Act.None;
            comboT = 0.45f;
        }

        void TickWind(InputFrame f, float dt, ref Vector3 pos, ref Quaternion rot, Vector3 rest, Quaternion restRot)
        {
            charge += dt;
            float k = Ease(Mathf.Clamp01(charge / 0.55f));
            var wind = TwoHanded ? new Vector3(0.12f, 0.1f, 0.06f) : new Vector3(0.18f, 0.06f, 0.18f);
            var windRot = Quaternion.Euler(-110f, -10f, -10f);
            pos = Vector3.Lerp(rest, wind, k) + Random.insideUnitSphere * 0.002f * Charge01;
            rot = Quaternion.Slerp(restRot, windRot, k);
            if (!f.FireHeld || charge > 2.5f) Begin(Act.Heavy, 0.3f + 0.7f * Charge01);
        }

        void TickSwing(float dt, ref Vector3 pos, ref Quaternion rot, Vector3 rest, Quaternion restRot)
        {
            t += dt / dur;
            float s = side;
            Vector3 wind, through;
            Quaternion windRot, throughRot;
            if (TwoHanded)
            {
                wind = new Vector3(0.22f * s, 0.02f, 0.2f); through = new Vector3(-0.2f * s, -0.26f, 0.4f);
                windRot = Quaternion.Euler(-55f, 40f * s, -60f * s); throughRot = Quaternion.Euler(30f, -60f * s, -50f * s);
            }
            else
            {
                wind = new Vector3(0.24f * s, -0.02f, 0.26f); through = new Vector3(-0.16f * s, -0.24f, 0.46f);
                windRot = Quaternion.Euler(-35f, 35f * s, -70f * s); throughRot = Quaternion.Euler(35f, -55f * s, -40f * s);
            }
            if (t < 0.22f) { float k = Ease(t / 0.22f); pos = Vector3.Lerp(rest, wind, k); rot = Quaternion.Slerp(restRot, windRot, k); }
            else if (t < 0.52f) { float k = Ease((t - 0.22f) / 0.3f); pos = Vector3.Lerp(wind, through, k); rot = Quaternion.Slerp(windRot, throughRot, k); if (!blocked) Sweep(SweepDir(k)); }
            else { float k = Ease((t - 0.52f) / 0.48f); pos = Vector3.Lerp(through, rest, k); rot = Quaternion.Slerp(throughRot, restRot, k); }
            if (t >= 1f || queued && t >= 0.62f) End();
        }

        void TickHeavy(float dt, ref Vector3 pos, ref Quaternion rot, Vector3 rest, Quaternion restRot)
        {
            t += dt / dur;
            var up = TwoHanded ? new Vector3(0.04f, 0.12f, 0.12f) : new Vector3(0.1f, 0.08f, 0.2f);
            var down = new Vector3(0.0f, -0.32f, 0.45f);
            var upRot = Quaternion.Euler(-95f, -4f, 0f);
            var downRot = Quaternion.Euler(45f, -4f, 0f);
            float a0 = Spec.ChargeSwing ? 0.12f : 0.35f;
            if (t < a0) { float k = Ease(t / a0); pos = Vector3.Lerp(rest, up, k); rot = Quaternion.Slerp(restRot, upRot, k); }
            else if (t < a0 + 0.25f) { float k = (t - a0) / 0.25f; k *= k; pos = Vector3.Lerp(up, down, k); rot = Quaternion.Slerp(upRot, downRot, k); if (!blocked) Sweep(SweepDir(Mathf.Clamp01(k))); }
            else { float k = Ease(Ramp(t, a0 + 0.3f, 1f)); pos = Vector3.Lerp(down, rest, k); rot = Quaternion.Slerp(downRot, restRot, k); }
            if (t >= 1f) End();
        }

        void TickStab(float dt, ref Vector3 pos, ref Quaternion rot, Vector3 rest, Quaternion restRot)
        {
            t += dt / dur;
            var back = rest + new Vector3(0.02f, 0.02f, -0.08f);
            var thrust = new Vector3(0.05f, -0.1f, 0.62f);
            var thrustRot = Quaternion.Euler(-4f, -6f, -85f);
            if (t < 0.25f) { float k = Ease(t / 0.25f); pos = Vector3.Lerp(rest, back, k); }
            else if (t < 0.42f) { float k = Ease((t - 0.25f) / 0.17f); pos = Vector3.Lerp(back, thrust, k); rot = Quaternion.Slerp(restRot, thrustRot, k); }
            else { float k = Ease((t - 0.42f) / 0.58f); pos = Vector3.Lerp(thrust, rest, k); rot = Quaternion.Slerp(thrustRot, restRot, k); }
            if (!impactDone && t >= 0.4f) { impactDone = true; Stab(); }
            if (t >= 1f) End();
        }

        Vector3 SweepDir(float k)
        {
            if (act == Act.Heavy) return CamT.rotation * Quaternion.Euler(Mathf.Lerp(-45f, 35f, k), 0f, 0) * Vector3.forward;
            float s = side, w = TwoHanded ? 40f : 32f;
            return CamT.rotation * Quaternion.Euler(Mathf.Lerp(-20f, 24f, k), Mathf.Lerp(w * s, -w * s, k), 0) * Vector3.forward;
        }

        void Sweep(Vector3 now)
        {
            int max = Spec.Blade ? Mathf.Max(1, Spec.Pierce + (act == Act.Heavy ? 1 : 0)) : 1;
            for (int i = 1; i <= 5 && !blocked && hitCount < max; i++)
            {
                var dir = Vector3.Slerp(lastSample, now, i / 5f);
                var hits = Physics.RaycastAll(Eye, dir, Spec.Reach, Layers.ShootMask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var hit in hits)
                {
                    if (hitCols.Contains(hit.collider)) continue;
                    hitCols.Add(hit.collider);
                    var sweep = (now - lastSample).sqrMagnitude > 1e-6f ? (now - lastSample).normalized : CamT.right * -side;
                    var part = hit.collider.GetComponent<BodyPart>();
                    if (part != null)
                    {
                        if (part.Body != null && !hitBodies.Add(part.Body)) continue;
                        FleshHit(part, hit, dir, sweep);
                        hitCount++;
                        if (!Spec.Blade) blocked = true; // blunt: the first body stops the swing
                        if (blocked || hitCount >= max) break;
                        continue; // blades pass through flesh
                    }
                    WorldHit(hit, sweep);
                    break;
                }
            }
            lastSample = now;
        }

        float DamageMul => act == Act.Heavy ? (Spec.ChargeSwing ? 0.6f + 1.4f * power : 1.6f) : 1f + Mathf.Min(combo - 1, 3) * 0.08f;

        void FleshHit(BodyPart part, RaycastHit hit, Vector3 dir, Vector3 sweep)
        {
            bool heavy = act == Act.Heavy;
            float pw = heavy ? power : 0.45f;
            float dmg = Def.Damage * DamageMul;
            if (Spec.Blade)
            {
                part.TakeDamage(new DamageInfo { Amount = dmg, Force = sweep * Def.Force * (0.6f + pw), Point = hit.point, Normal = hit.normal, Direction = sweep, Type = DamageType.Slash, Source = Player, Weapon = Def.Name, Penetration = 0.5f });
                AudioManager.Play(Spec.FleshSound, hit.point, 1f, Random.Range(0.9f, 1.06f));
                bool canCut = part.CanSever && Settings.Dismemberment && part.Doll != null;
                float chance = Spec.CutChance * (heavy ? 1.6f : 1f) * (part.IsHead ? 0.5f : 1f);
                if (canCut && (Rules.AlwaysDismember || part.Health <= part.MaxHealth * 0.5f || Random.value < chance))
                {
                    part.Doll.Dismember.Slice(part, hit.point, Vector3.Cross(sweep, dir).normalized);
                    AudioManager.Play("dismember", hit.point, 0.9f);
                    CameraFX.Shake(0.15f);
                }
                CameraFX.Punch(0.3f + 0.3f * pw, -side * 0.5f);
            }
            else
            {
                var push = (dir + sweep * 0.5f).normalized;
                part.TakeDamage(new DamageInfo
                {
                    Amount = dmg, Force = push * Def.Force * pw, Point = hit.point, Normal = hit.normal, Direction = push,
                    Type = heavy && pw > 0.75f ? DamageType.Crush : DamageType.Blunt, Source = Player, Weapon = Def.Name,
                });
                AudioManager.Play(Spec.FleshSound, hit.point, 0.8f + 0.2f * pw, Random.Range(0.9f, 1.06f));
                if (heavy || TwoHanded) AudioManager.Play("bone_crack", hit.point, 0.4f + 0.5f * pw, Random.Range(0.85f, 1.1f));
                var doll = part.Doll;
                if (doll != null)
                {
                    if (part.IsHead && heavy && pw > 0.7f && Settings.Dismemberment && part.Gibbable && Random.value < Spec.CrushChance)
                    {
                        doll.Dismember.Gib(part, dir);
                        AudioManager.Play("headshot", hit.point, 0.9f);
                    }
                    if (Spec.Launch > 0f) doll.Launch(push * Spec.Launch * pw + Vector3.up * (1f + 3f * pw));
                }
                CameraFX.Punch(0.5f + 0.8f * pw, 0f);
                CameraFX.Shake(0.1f + 0.25f * pw);
            }
            if (Spec.Bleeds)
                part.TakeDamage(new DamageInfo { Amount = dmg * 0.25f, Point = hit.point, Normal = hit.normal, Direction = dir, Type = DamageType.Stab, Source = Player, Weapon = Def.Name, Penetration = 0.3f });
            GameEvents.EmitNoise(hit.point, 6f, 0.4f, Player);
        }

        void WorldHit(RaycastHit hit, Vector3 sweep)
        {
            blocked = true;
            bool heavy = act == Act.Heavy;
            float pw = heavy ? power : 0.45f;
            var dir = (Aim + sweep * 0.5f).normalized;
            hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo { Amount = Def.Damage * DamageMul * (Spec.Blade ? 0.6f : 1.2f), Force = dir * 6f, Point = hit.point, Normal = hit.normal, Direction = dir, Type = Spec.Blade ? DamageType.Slash : DamageType.Blunt, Source = Player, Weapon = Def.Name });
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                NetPhysics.Impulse(hit.rigidbody, (dir + Vector3.up * 0.2f).normalized * Mathf.Min(hit.rigidbody.mass * (Spec.Blade ? 2f : 3f + 6f * pw), 400f), hit.point);
            var surf = Surface.Of(hit.collider);
            bool hard = surf == SurfaceType.Metal || surf == SurfaceType.Concrete;
            AudioManager.Play(Spec.Blade && hard ? "katana_clang" : Spec.WorldSound, hit.point, 0.7f + 0.3f * pw, Random.Range(0.9f, 1.1f));
            if (hard) FX.Sparks(hit.point, hit.normal, 6 + (int)(8 * pw), 4f);
            if (!Spec.Blade) FX.Dust(hit.point, 0.12f, new Color(0.55f, 0.53f, 0.5f, 0.6f), 4 + (int)(8 * pw));
            Decals.BulletHole(hit.point, hit.normal, surf, hit.collider, Spec.Blade ? 0.07f : 0.08f + 0.06f * pw);
            CameraFX.Shake(0.12f + 0.2f * pw);
            Vm.AddRecoil(new Vector3(0f, 0.1f, -0.4f) * (0.5f + pw), new Vector3(-8f * (0.5f + pw), 0f, Random.Range(-4f, 4f)));
            if (act == Act.Swing) t = Mathf.Max(t, 0.52f); else if (act == Act.Heavy) t = Mathf.Max(t, Spec.ChargeSwing ? 0.4f : 0.62f); // bounces off
            GameEvents.EmitNoise(hit.point, 8f + 6f * pw, 0.5f, Player);
        }

        void Stab()
        {
            if (!MeleeTrace(Aim, Spec.Reach * 0.9f, 0.06f, out var hit)) return;
            var part = hit.collider.GetComponent<BodyPart>();
            float dmg = Def.Damage * 1.7f;
            if (part != null)
            {
                part.TakeDamage(new DamageInfo { Amount = dmg, Force = Aim * Def.Force, Point = hit.point, Normal = hit.normal, Direction = Aim, Type = DamageType.Stab, Source = Player, Weapon = Def.Name, Penetration = 0.9f });
                AudioManager.Play("stab", hit.point, 0.95f, Random.Range(0.92f, 1.08f));
                CameraFX.Punch(0.4f, 0f);
            }
            else
            {
                hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo { Amount = dmg * 0.5f, Point = hit.point, Normal = hit.normal, Direction = Aim, Type = DamageType.Stab, Source = Player, Weapon = Def.Name });
                AudioManager.Play("knife_hit_hard", hit.point, 0.8f);
                FX.Sparks(hit.point, hit.normal, 5, 3f);
                Decals.BulletHole(hit.point, hit.normal, Surface.Of(hit.collider), hit.collider, 0.05f);
            }
            CameraFX.Shake(0.12f);
        }

        // ------------------------------------------------------------------ chainsaw

        void TickSaw(InputFrame f, float dt, ref Vector3 pos, ref Quaternion rot)
        {
            bool want = f.FireHeld && !Player.Sprinting;
            if (want && rev < 0.05f) AudioManager.Play("chainsaw_rev_up", Eye, 0.6f, 1f, 0f);
            rev = Mathf.MoveTowards(rev, want ? 1f : 0f, dt / (want ? 0.25f : 0.5f));
            bool cutting = false;

            if (rev > 0.6f)
            {
                sawTick -= dt;
                if (sawTick <= 0f)
                {
                    sawTick = 0.07f;
                    cutting = SawCut(0.07f);
                }
            }
            else { sawPart = null; sawOnPart = 0f; }

            // Thrust forward while revving, buck and rattle.
            pos += new Vector3(-0.02f, 0.05f, 0.12f) * Ease(rev) + Random.insideUnitSphere * (0.0012f + 0.003f * rev + (cutting ? 0.004f : 0f));
            rot *= Quaternion.Euler(-6f * rev, 0f, 0f);
            if (rev < 0.05f) TickInspect(f, dt, ref pos, ref rot, 2f);

            chainPhase = (chainPhase + dt * (0.15f + 1.6f * rev)) % 0.012f;
            if (chain != null) chain.localPosition = chainRest + Vector3.forward * chainPhase;

            if (idleLoop != null) { AudioManager.ApplyBus(idleLoop, AudioBus.Sfx, 0.4f * (1f - rev) + 0.05f); idleLoop.pitch = 1f + 0.1f * rev; }
            if (revLoop != null)
            {
                float v = 0.6f * rev;
                AudioManager.ApplyBus(revLoop, AudioBus.Sfx, v);
                revLoop.pitch = Mathf.MoveTowards(revLoop.pitch, cutting ? 0.82f : 0.9f + 0.25f * rev, dt * 2f);
                if (v > 0.01f && !revLoop.isPlaying) revLoop.Play();
                else if (v <= 0.01f && revLoop.isPlaying) revLoop.Stop();
            }
        }

        /// <summary>One chainsaw damage tick. Returns true while biting into something.</summary>
        bool SawCut(float tick)
        {
            if (!MeleeTrace(Aim, Spec.Reach, 0.08f, out var hit)) { sawPart = null; sawOnPart = 0f; return false; }
            var part = hit.collider.GetComponent<BodyPart>();
            cutSoundT -= tick;
            if (part != null)
            {
                if (part != sawPart) { sawPart = part; sawOnPart = 0f; }
                sawOnPart += tick;
                part.TakeDamage(new DamageInfo { Amount = Def.Damage * tick, Force = Aim * 3f, Point = hit.point, Normal = hit.normal, Direction = Aim, Type = DamageType.Slash, Source = Player, Weapon = Def.Name, Penetration = 0.6f });
                BloodFX.Mist(hit.point, (-Aim + Random.insideUnitSphere * 0.6f).normalized, 0.8f);
                if (Random.value < 0.35f) BloodFX.Spurt(hit.point, (CamT.up + Random.insideUnitSphere * 0.5f).normalized, 0.8f);
                if (cutSoundT <= 0f) { cutSoundT = 0.28f; AudioManager.Play("chainsaw_cut", hit.point, 0.8f, Random.Range(0.92f, 1.06f)); }
                // Through the limb after a moment of grinding: cut across the bone axis.
                if (sawOnPart >= 0.45f && part.CanSever && Settings.Dismemberment && part.Doll != null)
                {
                    var axis = part.Joint != null && part.Joint.connectedBody != null && part.Body != null ? part.Body.worldCenterOfMass - part.Joint.connectedBody.worldCenterOfMass : CamT.up;
                    part.Doll.Dismember.Slice(part, hit.point, axis.sqrMagnitude > 1e-6f ? axis.normalized : CamT.up);
                    AudioManager.Play("dismember", hit.point, 0.9f);
                    CameraFX.Shake(0.2f);
                    sawPart = null; sawOnPart = 0f;
                }
                CameraFX.Shake(0.05f);
                GameEvents.EmitNoise(hit.point, 10f, 0.6f, Player);
                return true;
            }
            sawPart = null; sawOnPart = 0f;
            hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo { Amount = Def.Damage * tick * 0.5f, Point = hit.point, Normal = hit.normal, Direction = Aim, Type = DamageType.Slash, Source = Player, Weapon = Def.Name });
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic) NetPhysics.Impulse(hit.rigidbody, (Aim + CamT.right * 0.3f) * Mathf.Min(hit.rigidbody.mass * 0.6f, 60f), hit.point);
            FX.Sparks(hit.point, hit.normal, 3, 4f);
            if (cutSoundT <= 0f) { cutSoundT = 0.25f; AudioManager.Play("katana_clang", hit.point, 0.35f, Random.Range(1.3f, 1.6f)); Decals.BulletHole(hit.point, hit.normal, Surface.Of(hit.collider), hit.collider, 0.05f); }
            CameraFX.Shake(0.04f);
            return true;
        }

        public override void Destroy()
        {
            if (idleLoop != null) Object.Destroy(idleLoop.gameObject);
            if (revLoop != null) Object.Destroy(revLoop.gameObject);
            base.Destroy();
        }
    }
}
