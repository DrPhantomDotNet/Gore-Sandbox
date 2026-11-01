using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// The Fighting Styles weapon category (Fight Update). Boxing puts the camera in third person and shows your own
    /// body, animated procedurally (<see cref="FightAnim"/>): a punch combo, a body hook, an overhand, and kicks
    /// (roundhouse, low kick, spinning back kick, flying kick, axe kick, push kick). Close to a Goredoll you can put it
    /// in a headlock and see its consciousness, alert state, pain and oxygen above its head. Head and neck shots do
    /// reduced damage so it takes a lot of punches to kill. Moves are sent to other players so they see the animation.
    /// </summary>
    public enum FightMove : byte { None, Jab, Cross, Hook, Uppercut, Haymaker, BodyHook, Roundhouse, LowKick, SpinKick, FlyingKick, AxeKick, FrontKick, Knee, LockPunch, Throw }

    public sealed class MoveSpec
    {
        public string Name;
        /// <summary>Duration in seconds; Wind / Hit / Hold are fractions of it (end of wind-up, contact, end of hold).</summary>
        public float Dur, Wind, Hit, Hold, Reach, Dmg, Force, Knock, Shake;
        public int Zone; // 0 head, 1 body, 2 legs
        public bool Kick;
        public string Whoosh;
    }

    public static class FightingStyles
    {
        public const string BoxingId = "boxing";
        /// <summary>Head and neck shots from fighting styles do this much of their damage: it takes a lot of punches to kill.</summary>
        public const float HeadMul = 0.2f;
        static readonly MoveSpec[] moves = new MoveSpec[16];

        static FightingStyles()
        {
            M(FightMove.Jab, "Jab", 0.30f, 0.15f, 0.38f, 0.55f, 1.55f, 9f, 45f, 0f, 0, false, 0.08f);
            M(FightMove.Cross, "Cross", 0.38f, 0.20f, 0.42f, 0.58f, 1.6f, 13f, 70f, 0f, 0, false, 0.12f);
            M(FightMove.Hook, "Left Hook", 0.46f, 0.25f, 0.50f, 0.62f, 1.45f, 16f, 90f, 0f, 0, false, 0.15f);
            M(FightMove.Uppercut, "Uppercut", 0.55f, 0.30f, 0.50f, 0.65f, 1.4f, 20f, 110f, 3.5f, 0, false, 0.25f, "whoosh_big");
            M(FightMove.Haymaker, "Overhand", 0.70f, 0.42f, 0.60f, 0.72f, 1.7f, 26f, 160f, 5f, 0, false, 0.3f, "whoosh_big");
            M(FightMove.BodyHook, "Body Shot", 0.45f, 0.25f, 0.50f, 0.62f, 1.45f, 15f, 90f, 0f, 1, false, 0.12f);
            M(FightMove.Roundhouse, "Roundhouse Kick", 0.68f, 0.32f, 0.55f, 0.70f, 2.0f, 24f, 220f, 6f, 0, true, 0.28f, "whoosh_big");
            M(FightMove.LowKick, "Low Kick", 0.50f, 0.28f, 0.50f, 0.62f, 1.8f, 14f, 120f, 2.5f, 2, true, 0.15f);
            M(FightMove.SpinKick, "Spinning Back Kick", 0.80f, 0.25f, 0.45f, 0.62f, 2.1f, 30f, 380f, 10f, 1, true, 0.4f, "whoosh_big");
            M(FightMove.FlyingKick, "Flying Kick", 0.80f, 0.20f, 0.35f, 0.75f, 1.7f, 34f, 450f, 11f, 1, true, 0.45f, "whoosh_big");
            M(FightMove.AxeKick, "Axe Kick", 0.80f, 0.45f, 0.60f, 0.72f, 1.8f, 26f, 160f, 2f, 0, true, 0.3f, "whoosh_big");
            M(FightMove.FrontKick, "Push Kick", 0.60f, 0.30f, 0.48f, 0.62f, 2.0f, 18f, 320f, 8f, 1, true, 0.22f, "whoosh_big");
            M(FightMove.Knee, "Knee", 0.45f, 0.25f, 0.48f, 0.60f, 1.4f, 16f, 60f, 0f, 0, true, 0.2f);
            M(FightMove.LockPunch, "Punch", 0.32f, 0.15f, 0.40f, 0.55f, 1.4f, 12f, 40f, 0f, 0, false, 0.1f);
            M(FightMove.Throw, "Throw", 0.60f, 0.30f, 0.50f, 0.65f, 2.5f, 0f, 0f, 0f, 1, false, 0.2f, "whoosh_big");
        }

        static void M(FightMove id, string name, float dur, float wind, float hit, float hold, float reach, float dmg, float force, float knock, int zone, bool kick, float shake, string whoosh = "whoosh_small")
            => moves[(int)id] = new MoveSpec { Name = name, Dur = dur, Wind = wind, Hit = hit, Hold = hold, Reach = reach, Dmg = dmg, Force = force, Knock = knock, Zone = zone, Kick = kick, Shake = shake, Whoosh = whoosh };

        public static MoveSpec Spec(FightMove m) => (int)m > 0 && (int)m < moves.Length ? moves[(int)m] : null;

        public static void AppendDefs(List<WeaponDef> all)
        {
            if (all.Exists(d => d.Id == BoxingId)) return;
            all.Add(new WeaponDef
            {
                Id = BoxingId, Name = "Boxing", Kind = WeaponKind.FightingStyle, Glyph = "fist", Damage = 10, Force = 40,
                Description = "Fighting style, third person. LMB punch combo, F kick, R spinning kick, Q axe kick, hold RMB guard, E headlock, MMB grab.",
            });
        }

        public static Weapon Create(WeaponDef def) => def != null && def.Kind == WeaponKind.FightingStyle ? new Boxing { Def = def } : null;

        public static bool IsStyle(int catalogIndex) => catalogIndex > 0 && catalogIndex < WeaponCatalog.All.Count && WeaponCatalog.All[catalogIndex].Kind == WeaponKind.FightingStyle;

        // ------------------------------------------------------------------ alert state shown in the headlock counter

        public static readonly string[] AlertNames = { "DEAD", "UNCONSCIOUS", "DAZED", "PANICKING", "FIGHTING BACK", "TERRIFIED", "ANGRY", "ALERT" };

        public static int AlertCode(Goredoll d)
        {
            var v = d.Vitals;
            if (v.State == LifeState.Dead) return 0;
            if (v.State == LifeState.Unconscious) return 1;
            if (v.Consciousness < 0.6f) return 2;
            var b = d.Brain;
            if (b != null)
            {
                if (b.Action == AiAction.Panic || b.Action == AiAction.Flee) return 3;
                if (b.Action == AiAction.Fight) return 4;
                if (b.Fear > 0.5f) return 5;
                if (b.Anger > 0.5f) return 6;
            }
            return 7;
        }
    }

    // ====================================================================== animation

    public struct FightPose
    {
        public FightMove Move;
        public float T, Guard, Speed, Phase, Crouch, Pitch, Time;
        public bool Headlock, Holding, Airborne;
    }

    /// <summary>
    /// Procedural fighter animation on the shared humanoid rig (parts from <see cref="Anatomy"/>). Every move blends
    /// stance → wind-up → strike → back to stance. Angles: negative X swings a limb forward, +Z swings a right limb
    /// outward (-Z for left), +Y turns toward the right.
    /// </summary>
    public static class FightAnim
    {
        const int N = 17;
        static readonly Vector3[] B = new Vector3[N], W = new Vector3[N], S = new Vector3[N];
        static int I(PartType p) => (int)p;
        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        static void Env(float t, MoveSpec m, out float w, out float s)
        {
            if (t < m.Wind) { w = Ease(t / m.Wind); s = 0f; }
            else if (t < m.Hit) { float k = (t - m.Wind) / Mathf.Max(0.001f, m.Hit - m.Wind); w = 1f - k; s = 1f - (1f - k) * (1f - k) * (1f - k); }
            else if (t < m.Hold) { w = 0f; s = 1f; }
            else { w = 0f; s = 1f - Ease((t - m.Hold) / Mathf.Max(0.001f, 1f - m.Hold)); }
        }

        static void FixFeet(Vector3[] a)
        {
            a[I(PartType.FootL)].x = -(a[I(PartType.ThighL)].x + a[I(PartType.ShinL)].x) * 0.8f;
            a[I(PartType.FootR)].x = -(a[I(PartType.ThighR)].x + a[I(PartType.ShinR)].x) * 0.8f;
        }

        public static void Apply(Dictionary<PartType, Transform> parts, Transform model, float height, FightPose p)
        {
            for (int i = 0; i < N; i++) B[i] = Vector3.zero;
            float bob = Mathf.Sin(p.Time * 7.5f), pitch = Mathf.Clamp(p.Pitch, -60f, 60f);
            // ---- stance: orthodox, chin down, gloves up, knees bent
            B[I(PartType.Chest)] = new Vector3(6f + pitch * 0.3f, -8f, bob * 1.5f);
            B[I(PartType.Abdomen)] = new Vector3(4f + 10f * p.Crouch, -6f, 0f);
            B[I(PartType.Neck)] = new Vector3(pitch * 0.2f, 6f, 0f);
            B[I(PartType.Head)] = new Vector3(-4f + pitch * 0.3f, 4f, 0f);
            B[I(PartType.UpperArmR)] = new Vector3(-38f, 0f, -18f); B[I(PartType.ForearmR)] = new Vector3(-128f, 0f, 0f);
            B[I(PartType.UpperArmL)] = new Vector3(-50f, 0f, 18f); B[I(PartType.ForearmL)] = new Vector3(-120f, 0f, 0f);
            float ct = -62f * p.Crouch, cs = 104f * p.Crouch;
            B[I(PartType.ThighL)] = new Vector3(-24f + ct, 0f, -5f); B[I(PartType.ShinL)] = new Vector3(30f + cs, 0f, 0f);
            B[I(PartType.ThighR)] = new Vector3(10f + ct, 0f, 5f); B[I(PartType.ShinR)] = new Vector3(24f + cs, 0f, 0f);
            // ---- footwork
            float swing = Mathf.Clamp01(p.Speed / 4f), s = Mathf.Sin(p.Phase);
            if (!p.Airborne)
            {
                B[I(PartType.ThighL)].x += -26f * s * swing; B[I(PartType.ThighR)].x += 26f * s * swing;
                B[I(PartType.ShinL)].x += Mathf.Max(0f, -s) * 40f * swing; B[I(PartType.ShinR)].x += Mathf.Max(0f, s) * 40f * swing;
            }
            else { B[I(PartType.ThighL)].x -= 30f; B[I(PartType.ShinL)].x += 50f; B[I(PartType.ThighR)].x -= 10f; B[I(PartType.ShinR)].x += 40f; }
            // ---- guard (RMB): gloves cover the face
            if (p.Guard > 0.001f)
            {
                float g = p.Guard;
                B[I(PartType.UpperArmR)] = Vector3.Lerp(B[I(PartType.UpperArmR)], new Vector3(-72f, 0f, -26f), g);
                B[I(PartType.UpperArmL)] = Vector3.Lerp(B[I(PartType.UpperArmL)], new Vector3(-72f, 0f, 26f), g);
                B[I(PartType.ForearmR)] = Vector3.Lerp(B[I(PartType.ForearmR)], new Vector3(-145f, 0f, 0f), g);
                B[I(PartType.ForearmL)] = Vector3.Lerp(B[I(PartType.ForearmL)], new Vector3(-145f, 0f, 0f), g);
                B[I(PartType.Head)].x += 12f * g; B[I(PartType.Chest)].x += 6f * g;
            }
            // ---- headlock / carrying
            if (p.Headlock)
            {
                B[I(PartType.UpperArmR)] = new Vector3(-58f, -25f, -30f); B[I(PartType.ForearmR)] = new Vector3(-75f, -40f, 0f);
                B[I(PartType.UpperArmL)] = new Vector3(-62f, 25f, 35f); B[I(PartType.ForearmL)] = new Vector3(-95f, 0f, 0f);
                B[I(PartType.Chest)].x += 18f; B[I(PartType.Abdomen)].x += 8f; B[I(PartType.Head)].x += 8f;
                B[I(PartType.ThighL)].z -= 8f; B[I(PartType.ThighR)].z += 8f;
            }
            else if (p.Holding)
            {
                B[I(PartType.UpperArmR)] = new Vector3(-80f, 0f, -12f); B[I(PartType.ForearmR)] = new Vector3(-35f, 0f, 0f);
                B[I(PartType.UpperArmL)] = new Vector3(-80f, 0f, 12f); B[I(PartType.ForearmL)] = new Vector3(-35f, 0f, 0f);
                B[I(PartType.Chest)].x -= 6f;
            }
            FixFeet(B);
            System.Array.Copy(B, W, N); System.Array.Copy(B, S, N);

            float w = 0f, st = 0f, yawW = 0f, yawS = 0f, yW = 0f, yS = 0f, spin = 0f;
            var m = FightingStyles.Spec(p.Move);
            if (m != null)
            {
                Env(p.T, m, out w, out st);
                switch (p.Move)
                {
                    case FightMove.Jab:
                        W[I(PartType.UpperArmL)] = new Vector3(-42f, 0f, 14f);
                        S[I(PartType.UpperArmL)] = new Vector3(-92f, -4f, 4f); S[I(PartType.ForearmL)] = new Vector3(-6f, 0f, 0f);
                        S[I(PartType.Chest)].y += 22f; S[I(PartType.Abdomen)].y += 8f; S[I(PartType.ThighL)].x -= 8f; yawS = 4f;
                        break;
                    case FightMove.Cross:
                        W[I(PartType.Chest)].y += 10f;
                        S[I(PartType.UpperArmR)] = new Vector3(-93f, 6f, -8f); S[I(PartType.ForearmR)] = new Vector3(-5f, 0f, 0f);
                        S[I(PartType.UpperArmL)] = new Vector3(-60f, 0f, 24f);
                        S[I(PartType.Chest)].y -= 30f; S[I(PartType.Abdomen)].y -= 14f; S[I(PartType.ThighR)].x += 12f; yawS = -8f;
                        break;
                    case FightMove.Hook:
                        W[I(PartType.UpperArmL)] = new Vector3(-20f, 10f, -55f); W[I(PartType.ForearmL)] = new Vector3(-100f, 0f, 0f); W[I(PartType.Chest)].y -= 14f; yW = -0.02f;
                        S[I(PartType.UpperArmL)] = new Vector3(-5f, 72f, -82f); S[I(PartType.ForearmL)] = new Vector3(-92f, 0f, 0f);
                        S[I(PartType.Chest)].y += 34f; S[I(PartType.Abdomen)].y += 14f; yawS = 10f;
                        break;
                    case FightMove.Uppercut:
                        W[I(PartType.UpperArmR)] = new Vector3(14f, 0f, -6f); W[I(PartType.ForearmR)] = new Vector3(-105f, 0f, 0f);
                        W[I(PartType.Chest)] += new Vector3(12f, 14f, -6f); yW = -0.08f;
                        S[I(PartType.UpperArmR)] = new Vector3(-138f, -12f, -14f); S[I(PartType.ForearmR)] = new Vector3(-72f, 0f, 0f);
                        S[I(PartType.Chest)] += new Vector3(-14f, -22f, 0f); S[I(PartType.Head)].x -= 12f;
                        S[I(PartType.ShinL)].x -= 20f; S[I(PartType.ShinR)].x -= 20f; yS = 0.06f;
                        break;
                    case FightMove.Haymaker:
                        W[I(PartType.UpperArmR)] = new Vector3(28f, 0f, 62f); W[I(PartType.ForearmR)] = new Vector3(-95f, 0f, 0f);
                        W[I(PartType.Chest)] += new Vector3(-6f, 38f, 0f); yawW = 12f;
                        S[I(PartType.UpperArmR)] = new Vector3(-110f, -28f, -8f); S[I(PartType.ForearmR)] = new Vector3(-12f, 0f, 0f);
                        S[I(PartType.Chest)] += new Vector3(16f, -42f, 0f); S[I(PartType.Abdomen)].y -= 16f; S[I(PartType.ThighR)].x += 22f;
                        yawS = -18f; yS = -0.03f;
                        break;
                    case FightMove.BodyHook:
                        W[I(PartType.UpperArmR)] = new Vector3(-10f, -10f, 40f); W[I(PartType.ForearmR)] = new Vector3(-100f, 0f, 0f); W[I(PartType.Chest)].y += 14f;
                        S[I(PartType.UpperArmR)] = new Vector3(-25f, -70f, 55f); S[I(PartType.ForearmR)] = new Vector3(-95f, 0f, 0f);
                        S[I(PartType.Chest)] += new Vector3(14f, -30f, 0f); yS = -0.08f;
                        break;
                    case FightMove.Roundhouse:
                        W[I(PartType.ThighR)] = new Vector3(-95f, 0f, 25f); W[I(PartType.ShinR)] = new Vector3(115f, 0f, 0f); W[I(PartType.FootR)] = new Vector3(30f, 0f, 0f);
                        W[I(PartType.Chest)] += new Vector3(-5f, 0f, 12f); yawW = -10f;
                        S[I(PartType.ThighR)] = new Vector3(-15f, -62f, 115f); S[I(PartType.ShinR)] = new Vector3(6f, 0f, 0f); S[I(PartType.FootR)] = new Vector3(35f, 0f, 0f);
                        S[I(PartType.Chest)] += new Vector3(-8f, -10f, 38f); S[I(PartType.Abdomen)].z += 18f;
                        S[I(PartType.UpperArmR)] = new Vector3(25f, 0f, 45f); S[I(PartType.UpperArmL)] = new Vector3(-60f, 0f, 30f);
                        S[I(PartType.ThighL)].x = -6f; S[I(PartType.ShinL)].x = 12f; yawS = -45f; yS = 0.02f;
                        break;
                    case FightMove.LowKick:
                        W[I(PartType.ThighR)] = new Vector3(-35f, 0f, 35f); W[I(PartType.ShinR)] = new Vector3(70f, 0f, 0f); W[I(PartType.Chest)].y += 10f;
                        S[I(PartType.ThighR)] = new Vector3(-20f, -55f, 45f); S[I(PartType.ShinR)] = new Vector3(8f, 0f, 0f); S[I(PartType.FootR)] = new Vector3(15f, 0f, 0f);
                        S[I(PartType.Chest)] += new Vector3(0f, -22f, 16f); yawS = -30f; yS = -0.04f;
                        break;
                    case FightMove.SpinKick:
                        // A full turn: the leg shoots out backwards when you're facing away, so it lands in front.
                        spin = 360f * Ease((p.T - 0.05f) / 0.8f);
                        W[I(PartType.ThighR)] = new Vector3(-70f, 0f, 0f); W[I(PartType.ShinR)] = new Vector3(110f, 0f, 0f); W[I(PartType.Chest)].x += 10f;
                        S[I(PartType.ThighR)] = new Vector3(75f, 0f, 15f); S[I(PartType.ShinR)] = new Vector3(4f, 0f, 0f); S[I(PartType.FootR)] = new Vector3(-30f, 0f, 0f);
                        S[I(PartType.Chest)].x = B[I(PartType.Chest)].x + 32f; S[I(PartType.Abdomen)].x += 10f; S[I(PartType.Head)].x -= 20f;
                        S[I(PartType.UpperArmL)] = new Vector3(-30f, 0f, -45f); S[I(PartType.UpperArmR)] = new Vector3(-30f, 0f, 45f);
                        S[I(PartType.ThighL)].x = -10f; S[I(PartType.ShinL)].x = 14f; yS = 0.04f;
                        break;
                    case FightMove.FlyingKick:
                        W[I(PartType.ThighR)] = new Vector3(-100f, 0f, 0f); W[I(PartType.ShinR)] = new Vector3(120f, 0f, 0f);
                        W[I(PartType.ThighL)] = new Vector3(-80f, 0f, 0f); W[I(PartType.ShinL)] = new Vector3(110f, 0f, 0f);
                        S[I(PartType.ThighR)] = new Vector3(-82f, 0f, 4f); S[I(PartType.ShinR)] = new Vector3(0f, 0f, 0f); S[I(PartType.FootR)] = new Vector3(35f, 0f, 0f);
                        S[I(PartType.ThighL)] = new Vector3(-115f, 0f, -6f); S[I(PartType.ShinL)] = new Vector3(135f, 0f, 0f);
                        S[I(PartType.Chest)].x = B[I(PartType.Chest)].x - 22f; S[I(PartType.Head)].x += 10f;
                        S[I(PartType.UpperArmR)] = new Vector3(35f, 0f, 40f); S[I(PartType.UpperArmL)] = new Vector3(35f, 0f, -40f); yS = 0.1f;
                        break;
                    case FightMove.AxeKick:
                        W[I(PartType.ThighR)] = new Vector3(-165f, 0f, 12f); W[I(PartType.ShinR)] = new Vector3(4f, 0f, 0f); W[I(PartType.FootR)] = new Vector3(20f, 0f, 0f);
                        W[I(PartType.Chest)].x = B[I(PartType.Chest)].x - 14f; W[I(PartType.ThighL)].x = -4f; W[I(PartType.ShinL)].x = 8f; yW = 0.04f;
                        S[I(PartType.ThighR)] = new Vector3(-35f, 0f, 8f); S[I(PartType.ShinR)] = new Vector3(10f, 0f, 0f);
                        S[I(PartType.Chest)].x = B[I(PartType.Chest)].x + 22f; yS = -0.03f;
                        break;
                    case FightMove.FrontKick:
                        W[I(PartType.ThighR)] = new Vector3(-100f, 0f, 0f); W[I(PartType.ShinR)] = new Vector3(115f, 0f, 0f); W[I(PartType.Chest)].x -= 6f;
                        S[I(PartType.ThighR)] = new Vector3(-88f, 0f, 0f); S[I(PartType.ShinR)] = new Vector3(2f, 0f, 0f); S[I(PartType.FootR)] = new Vector3(-10f, 0f, 0f);
                        S[I(PartType.Chest)].x = B[I(PartType.Chest)].x - 20f; yawS = -10f;
                        break;
                    case FightMove.Knee:
                        W[I(PartType.ThighR)] = new Vector3(-30f, 0f, 0f); W[I(PartType.ShinR)] = new Vector3(70f, 0f, 0f);
                        S[I(PartType.ThighR)] = new Vector3(-105f, 0f, 0f); S[I(PartType.ShinR)] = new Vector3(125f, 0f, 0f); S[I(PartType.FootR)] = new Vector3(30f, 0f, 0f);
                        S[I(PartType.Chest)].x += 8f;
                        break;
                    case FightMove.LockPunch:
                        W[I(PartType.UpperArmL)] = new Vector3(-20f, 10f, 40f); W[I(PartType.ForearmL)] = new Vector3(-120f, 0f, 0f);
                        S[I(PartType.UpperArmL)] = new Vector3(-55f, 40f, -5f); S[I(PartType.ForearmL)] = new Vector3(-25f, 0f, 0f); S[I(PartType.Chest)].y += 10f;
                        break;
                    case FightMove.Throw:
                        W[I(PartType.Chest)] += new Vector3(10f, 30f, 0f); yawW = 15f;
                        S[I(PartType.Chest)] += new Vector3(-10f, -45f, 0f);
                        S[I(PartType.UpperArmR)] = new Vector3(-115f, -40f, -10f); S[I(PartType.UpperArmL)] = new Vector3(-115f, 30f, 10f);
                        S[I(PartType.ForearmR)] = new Vector3(-10f, 0f, 0f); S[I(PartType.ForearmL)] = new Vector3(-10f, 0f, 0f); yawS = -40f;
                        break;
                }
            }

            foreach (var kv in parts)
            {
                int i = (int)kv.Key;
                if (i < 0 || i >= N || kv.Value == null) continue;
                kv.Value.localRotation = Quaternion.Euler(Vector3.Lerp(Vector3.Lerp(B[i], W[i], w), S[i], st));
            }
            float y = (-0.42f * p.Crouch - 0.035f) * height + bob * 0.012f * (1f - p.Crouch) + Mathf.Lerp(yW * w, yS, st);
            model.localPosition = new Vector3(0f, y, 0f);
            model.localRotation = Quaternion.Euler(0f, Mathf.Lerp(yawW * w, yawS, st) + spin, 0f);
        }

        /// <summary>Red boxing gloves on both hands.</summary>
        public static GameObject[] Gloves(Dictionary<PartType, Transform> parts, int layer)
        {
            var list = new GameObject[2];
            for (int k = 0; k < 2; k++)
            {
                if (!parts.TryGetValue(k == 0 ? PartType.HandL : PartType.HandR, out var hand) || hand == null) continue;
                var go = new GameObject("Glove");
                go.layer = layer;
                go.transform.SetParent(hand, false);
                go.transform.localPosition = new Vector3(0f, -0.075f, 0.012f);
                var mb = new MeshBuilder { Color = new Color(0.78f, 0.06f, 0.05f), Gloss = 0.55f };
                mb.Sphere(Vector3.zero, new Vector3(0.062f, 0.075f, 0.07f), 8, 12);
                mb.SetColor(new Color(0.95f, 0.95f, 0.95f));
                mb.Cylinder(new Vector3(0f, 0.04f, 0f), new Vector3(0f, 0.1f, 0f), 0.05f, 0.048f, 10);
                go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Glove");
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                list[k] = go;
            }
            return list;
        }
    }

    // ====================================================================== your own body in third person

    public sealed class FighterRig : MonoBehaviour
    {
        public readonly Dictionary<PartType, Transform> Parts = new Dictionary<PartType, Transform>();
        Transform model;
        float height = 1f;
        FaceRig face;

        public static FighterRig Create(PlayerController p)
        {
            var go = new GameObject("FighterBody");
            go.layer = Layers.Effects;
            go.transform.SetParent(p.transform, false);
            var r = go.AddComponent<FighterRig>();
            r.Build();
            return r;
        }

        void Build()
        {
            var prof = PlayerProfile.Local;
            var custom = prof != null ? prof.BodyLook : null;
            Appearance look;
            if (custom != null) look = custom.ToAppearance();
            else
            {
                var st = Random.state;
                Random.InitState((PlayerProfile.LocalName ?? "p").GetHashCode() ^ 0x5EED);
                look = Appearance.Random(-1);
                Random.state = st;
            }
            height = look.Height;
            if (prof != null) OWOutfit.Apply(look, prof.Outfit);
            model = new GameObject("Model").transform;
            model.SetParent(transform, false);
            model.localScale = Vector3.one * look.Height;
            foreach (PartType t in System.Enum.GetValues(typeof(PartType)))
            {
                var spec = Anatomy.Parts[t];
                var go = new GameObject(spec.Name);
                var parent = spec.Parent.HasValue ? Parts[spec.Parent.Value] : model;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = spec.Parent.HasValue ? spec.Pivot - Anatomy.Parts[spec.Parent.Value].Pivot : spec.Pivot;
                go.AddComponent<MeshFilter>().sharedMesh = HumanModel.Build(t, look);
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                Parts[t] = go.transform;
            }
            face = FaceRig.Build(Parts[PartType.Head], look);
            if (custom != null && custom.Glasses) CustomDolls.AddGlasses(Parts[PartType.Head], Layers.Effects);
            if (prof != null && !OWOutfit.HasHat(prof.Outfit)) Hats.Attach(Parts[PartType.Head], prof.Hat, prof.HatColor, Layers.Effects);
            if (prof != null) OWOutfit.Attach(Parts[PartType.Head], Parts[PartType.Chest], Parts.TryGetValue(PartType.ForearmL, out var fa) ? fa : null, prof.Outfit, Layers.Effects);
            FightAnim.Gloves(Parts, Layers.Effects);
            IconFactory.SetLayer(model.gameObject, Layers.Effects);
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        public void Pose(FightPose p, bool visible)
        {
            if (model.gameObject.activeSelf != visible) model.gameObject.SetActive(visible);
            if (!visible) return;
            FightAnim.Apply(Parts, model, height, p);
            if (face != null) face.Expression = p.Move != FightMove.None || p.Headlock ? Expression.Angry : Expression.Neutral;
        }
    }

    // ====================================================================== grabbing Goredolls with your hands

    /// <summary>Grab a Goredoll (or a light prop) with your bare hands, carry it and throw it. Fists and Boxing (MMB).</summary>
    public sealed class HandGrab
    {
        public Rigidbody Body;
        public Goredoll Doll;
        bool remote;
        float fallT;
        public bool Holding => Body != null;

        public bool TryGrab(PlayerController p, float range)
        {
            Rigidbody rb = null; Goredoll doll = null;
            var origin = p.EyePosition;
            if (Physics.SphereCast(origin, 0.22f, p.AimDirection, out var hit, range, Layers.GrabMask, QueryTriggerInteraction.Ignore) && hit.rigidbody != null)
            {
                rb = hit.rigidbody;
                doll = rb.GetComponentInParent<Goredoll>();
            }
            if (rb == null) { doll = Boxing.NearestDoll(p, range, 60f); }
            if (doll != null)
            {
                rb = null;
                foreach (var t in new[] { PartType.Chest, PartType.Abdomen, PartType.Neck, PartType.Pelvis })
                    if (doll.Parts.TryGetValue(t, out var bp) && bp != null && !bp.Severed && !bp.Gibbed && bp.Body != null) { rb = bp.Body; break; }
                if (rb == null) return false;
            }
            if (rb == null || (doll == null && (rb.mass > 150f || rb.isKinematic))) return false;
            if (Vector3.Distance(rb.position, p.transform.position + Vector3.up) > range + 0.8f) return false;
            Body = rb; Doll = doll; fallT = 0f;
            remote = NetState.IsClient && NetManager.Handle(rb, out _, out _);
            if (remote)
            {
                var part = rb.GetComponent<BodyPart>();
                if (doll != null && part != null) NetManager.I.RequestDollCommand(doll, 26, part.Type); // fight grab: no Creative needed
                else NetManager.I.RequestGrab(rb, Vector3.zero);
                NetManager.I.ClientGrabActive = true;
            }
            else if (doll != null && doll.Ragdoll != null) doll.Ragdoll.Fall(3f);
            if (doll != null && doll.Vitals.Conscious) doll.Voice.Say("v_whoa", 1f, true);
            AudioManager.Play("cloth", rb.position, 0.7f);
            Stats.Add("grabs");
            return true;
        }

        public void Tick(Vector3 target, float dt)
        {
            if (Body == null) return;
            if (!Body || (Doll != null && !Doll)) { Body = null; Doll = null; if (remote && NetManager.I != null) NetManager.I.ClientGrabActive = false; remote = false; return; }
            if ((Body.position - target).sqrMagnitude > 3.5f * 3.5f) { Release(Vector3.zero); return; }
            if (remote)
            {
                var nm = NetManager.I;
                nm.ClientGrabTarget = target; nm.ClientGrabRot = Body.rotation; nm.ClientGrabRotating = false; nm.ClientGrabActive = true;
                return;
            }
            if (Body.isKinematic) return;
            var desired = Vector3.ClampMagnitude((target - Body.position) * 14f, 30f);
            RB.SetVel(Body, Vector3.Lerp(RB.Vel(Body), desired, 1f - Mathf.Exp(-18f * dt)));
            Body.WakeUp();
            fallT -= dt;
            if (Doll != null && Doll.Ragdoll != null && fallT <= 0f) { Doll.Ragdoll.Fall(2f); fallT = 1.5f; }
        }

        public void Release(Vector3 throwVelocity)
        {
            if (Body == null) return;
            if (remote) { if (NetManager.I != null) NetManager.I.RequestGrabEnd(throwVelocity); }
            else if (Body && !Body.isKinematic) RB.SetVel(Body, throwVelocity != Vector3.zero ? throwVelocity : Vector3.ClampMagnitude(RB.Vel(Body), 8f));
            if (Doll != null && Doll && throwVelocity.sqrMagnitude > 1f) Doll.Launch(throwVelocity);
            if (Body) AudioManager.Play(throwVelocity.sqrMagnitude > 1f ? "whoosh_big" : "cloth", Body.position, 0.6f);
            if (throwVelocity.sqrMagnitude > 1f) Stats.Add("throws");
            Body = null; Doll = null; remote = false;
        }
    }

    // ====================================================================== Boxing

    public sealed class Boxing : Weapon
    {
        /// <summary>Damage multiplier for hits on the local player (0.35 while guarding). Player/PlayerHealth.cs.</summary>
        public static float GuardMul = 1f;
        public static Boxing Current;

        FighterRig rig;
        FightMove move;
        float moveT, guard, phase, animTime, comboUntil, flashT, hitComboUntil, chokeSend, lockFall, lockStruggle, nextVoice, choke;
        int combo, hitCombo;
        bool hitDone, queued;
        string flashText = "";
        Goredoll locked, candidate;
        BodyPart lockPart;
        readonly HandGrab grab = new HandGrab();
        int sentState = -1;

        public override bool ThirdPerson => true;
        public override bool ConsumesKick => true;
        public override bool IsAiming => guard > 0.5f; // other players see the guard; no zoom
        public override float AimFov => 1f;
        public override bool ConsumesUse => locked != null || candidate != null;
        public override float MoveSpeedMul => locked != null ? 0.55f : grab.Holding ? 0.75f : move == FightMove.FlyingKick ? 1f : 0.95f;
        public override string AmmoText => "";
        public override string ModeText => locked != null ? "HEADLOCK" : grab.Holding ? "CARRYING" : guard > 0.5f ? "GUARD" : "";
        public override bool Busy => move != FightMove.None;
        public bool Locking => locked != null;
        public bool Holding => grab.Holding;
        public bool HasTarget => candidate != null;

        protected override void OnEquip()
        {
            Current = this;
            Vm.ResetHands();
            if (rig == null) rig = FighterRig.Create(Player);
            move = FightMove.None; combo = 0; guard = 0f;
            AudioManager.Play("cloth", Player.transform.position, 0.6f);
        }

        protected override void OnHolster()
        {
            ReleaseLock();
            grab.Release(Vector3.zero);
            GuardMul = 1f;
            if (Current == this) Current = null;
            if (rig != null) { Object.Destroy(rig.gameObject); rig = null; }
            SendState(0, true);
        }

        public override void Destroy()
        {
            base.Destroy();
            GuardMul = 1f;
            if (rig != null) Object.Destroy(rig.gameObject);
        }

        Vector3 Flat => MathX.Flat(Aim).sqrMagnitude > 0.001f ? MathX.Flat(Aim).normalized : Player.transform.forward;
        Vector3 Right => Vector3.Cross(Vector3.up, Flat);

        public override void Tick(InputFrame f, float dt)
        {
            var p = Player;
            animTime += dt;
            phase += p.Speed * dt * 4.5f;
            flashT -= dt;
            if (locked != null && (!locked || lockPart == null || !lockPart)) { locked = null; lockPart = null; }

            bool wantGuard = f.Aim && locked == null && !grab.Holding && move == FightMove.None;
            guard = MathX.Damp(guard, wantGuard ? 1f : 0f, 14f, dt);
            GuardMul = guard > 0.5f ? 0.35f : 1f;
            candidate = locked == null && !grab.Holding && move == FightMove.None ? NearestDoll(p, 1.75f, 70f) : null;

            if (grab.Holding)
            {
                grab.Tick(p.transform.position + Flat * 0.95f + Vector3.up * 1.3f, dt);
                if (f.Fire && move == FightMove.None) Start(FightMove.Throw);
                else if (f.AimPressed || f.Middle) grab.Release(Vector3.zero);
            }
            else if (locked != null) TickLock(f, dt);
            else
            {
                if (f.Middle && move == FightMove.None) grab.TryGrab(p, 2.3f);
                else if (f.Use && candidate != null && move == FightMove.None) StartLock(candidate);
                else ReadAttacks(f, p);
            }

            // advance the current move
            var spec = FightingStyles.Spec(move);
            if (spec != null)
            {
                moveT += dt / spec.Dur;
                if (!hitDone && moveT >= spec.Hit) { hitDone = Strike(spec); if (move != FightMove.FlyingKick) hitDone = true; }
                if (move == FightMove.FlyingKick && !hitDone && moveT > spec.Hold) hitDone = true;
                if (moveT >= 1f) { move = FightMove.None; moveT = 0f; }
            }
            if (move == FightMove.None && queued) { queued = false; ReadPunch(p); }

            if (rig != null)
            {
                rig.Pose(new FightPose
                {
                    Move = move, T = moveT, Guard = guard, Speed = p.Speed, Phase = phase, Crouch = p.Crouching ? 1f : 0f,
                    Pitch = CamT.eulerAngles.x > 180f ? CamT.eulerAngles.x - 360f : CamT.eulerAngles.x, Time = animTime,
                    Headlock = locked != null, Holding = grab.Holding, Airborne = !p.Grounded && !p.Noclip,
                }, p.ThirdPersonBlend > 0.35f);
            }
            SendState((locked != null ? 1 : 0) | (grab.Holding ? 2 : 0), false);
            Vm.TargetPos = new Vector3(0f, -0.6f, 0.2f); // keep the first-person hands out of view
        }

        void ReadAttacks(InputFrame f, PlayerController p)
        {
            if (f.Fire)
            {
                if (move == FightMove.None) ReadPunch(p);
                else if (moveT > 0.35f && !FightingStyles.Spec(move).Kick) queued = true;
                return;
            }
            if (move != FightMove.None) return;
            if (f.Kick)
            {
                if (!p.Grounded && !p.Noclip) Start(FightMove.FlyingKick);
                else if (p.Crouching) Start(FightMove.LowKick);
                else if (f.Sprint && p.Speed > 3f) Start(FightMove.FrontKick);
                else Start(FightMove.Roundhouse);
            }
            else if (f.Reload) Start(FightMove.SpinKick);
            else if (f.Inspect) Start(FightMove.AxeKick);
        }

        void ReadPunch(PlayerController p)
        {
            if (p.Crouching) { Start(FightMove.BodyHook); return; }
            if (PlayerInput.Frame.Sprint && p.Speed > 3f) { Start(FightMove.Haymaker); return; }
            if (Time.time > comboUntil) combo = 0;
            var m = combo == 0 ? FightMove.Jab : combo == 1 ? FightMove.Cross : combo == 2 ? FightMove.Hook : FightMove.Uppercut;
            combo = (combo + 1) % 4;
            Start(m);
        }

        void Start(FightMove m)
        {
            var spec = FightingStyles.Spec(m);
            if (spec == null) return;
            move = m; moveT = 0f; hitDone = false;
            comboUntil = Time.time + spec.Dur + 0.7f;
            Swing(spec.Whoosh, spec.Kick ? 0.8f : 0.6f);
            if (m == FightMove.FlyingKick) Player.Launch(Flat * 8f + Vector3.up * 3.5f);
            if (m == FightMove.FrontKick || m == FightMove.Haymaker) Player.Launch(Flat * 2.5f);
            FightNet.SendMove(m);
            if (spec.Kick) Stats.Add(Stats.Kicks); else Stats.Add("punches");
        }

        // ------------------------------------------------------------------ hitting

        public static Goredoll NearestDoll(PlayerController p, float range, float maxAngle)
        {
            var fwd = MathX.Flat(p.AimDirection).sqrMagnitude > 0.001f ? MathX.Flat(p.AimDirection).normalized : p.transform.forward;
            Goredoll best = null; float bestScore = float.MaxValue;
            foreach (var d in Goredoll.All)
            {
                if (d == null || !d.gameObject.activeInHierarchy || !d.Parts.ContainsKey(PartType.Pelvis)) continue;
                var to = MathX.Flat(d.Position - p.transform.position);
                float dist = to.magnitude;
                if (dist > range + 0.35f || Mathf.Abs(d.Position.y - p.transform.position.y) > 2.2f) continue;
                float ang = dist < 0.3f ? 0f : Vector3.Angle(fwd, to);
                if (ang > maxAngle) continue;
                float score = dist + ang / 60f;
                if (score < bestScore) { bestScore = score; best = d; }
            }
            return best;
        }

        static readonly PartType[] HeadZone = { PartType.Head, PartType.Neck };
        static readonly PartType[] BodyZone = { PartType.Chest, PartType.Abdomen, PartType.Pelvis };
        static readonly PartType[] LegZone = { PartType.ShinL, PartType.ShinR, PartType.ThighL, PartType.ThighR };

        static BodyPart PickPart(Goredoll d, int zone, Vector3 strike, float reach)
        {
            BodyPart best = null; float bestD = float.MaxValue;
            float Score(BodyPart bp) { var c = bp.Body.worldCenterOfMass; var fd = MathX.Flat(c - strike).magnitude; return fd + Mathf.Abs(c.y - strike.y) * 0.35f; }
            foreach (var t in zone == 0 ? HeadZone : zone == 1 ? BodyZone : LegZone)
                if (d.Parts.TryGetValue(t, out var bp) && bp != null && !bp.Severed && !bp.Gibbed && bp.Body != null) { float s = Score(bp); if (s < bestD) { bestD = s; best = bp; } }
            if (best == null || bestD > 1.1f)
                foreach (var bp in d.Parts.Values)
                    if (bp != null && !bp.Severed && !bp.Gibbed && bp.Body != null) { float s = Score(bp) + 0.3f; if (s < bestD) { bestD = s; best = bp; } }
            return bestD <= reach ? best : null;
        }

        Vector3 HitDir(FightMove m)
        {
            var f = Flat; var r = Right;
            switch (m)
            {
                case FightMove.Uppercut: return (f * 0.5f + Vector3.up).normalized;
                case FightMove.Hook: return (f * 0.6f + r * 0.8f).normalized;
                case FightMove.BodyHook: case FightMove.Roundhouse: case FightMove.LowKick: return (f * 0.6f - r * 0.8f).normalized;
                case FightMove.AxeKick: return (Vector3.down + f * 0.3f).normalized;
                case FightMove.Knee: return (Vector3.up * 0.8f + f * 0.4f).normalized;
                case FightMove.LockPunch: return (Vector3.down * 0.4f + f).normalized;
                default: return (f + Vector3.up * 0.15f).normalized;
            }
        }

        /// <summary>Lands the move. Returns true if it hit something.</summary>
        bool Strike(MoveSpec spec)
        {
            var p = Player;
            if (move == FightMove.Throw)
            {
                var v = Flat * 13f + Vector3.up * 4f;
                if (grab.Holding) grab.Release(v);
                else if (locked != null) { var d = locked; ReleaseLock(); d.Launch(Flat * 9f + Vector3.up * 4.5f); }
                CameraFX.Shake(0.2f);
                return true;
            }
            BodyPart part = null; Goredoll doll = null;
            if (locked != null && (move == FightMove.LockPunch || move == FightMove.Knee))
            {
                doll = locked;
                if (!locked.Parts.TryGetValue(PartType.Head, out part) || part == null || part.Severed || part.Body == null) part = lockPart;
            }
            else
            {
                float h = spec.Zone == 0 ? 1.6f : spec.Zone == 1 ? 1.1f : 0.45f;
                var strike = p.transform.position + Vector3.up * h + Flat * (spec.Reach * 0.7f);
                doll = NearestDoll(p, spec.Reach, 75f);
                if (doll != null) part = PickPart(doll, spec.Zone, strike, spec.Reach);
            }
            var dir = HitDir(move);
            bool super = Rules.SuperKick;
            if (part != null && part.Body != null)
            {
                bool head = part.Type == PartType.Head || part.Type == PartType.Neck;
                var point = part.Body.worldCenterOfMass - Flat * 0.08f;
                var info = new DamageInfo
                {
                    Amount = spec.Dmg * (head ? FightingStyles.HeadMul : 1f) * (super ? 3f : 1f), Force = dir * spec.Force * (super ? 4f : 1f),
                    Point = point, Normal = -dir, Direction = dir, Type = DamageType.Blunt, Source = p, Weapon = "Boxing: " + spec.Name,
                };
                part.TakeDamage(info);
                if (spec.Knock > 0f) doll.Launch(dir * spec.Knock * (super ? 3f : 1f) + Vector3.up * (move == FightMove.LowKick ? 2.5f : 1f));
                else if (!doll.NetProxy && doll.Ragdoll != null) doll.Ragdoll.OnImpact(3f + spec.Dmg * 0.2f);
                if (head && Settings.GoreEnabled && (spec.Dmg >= 16f || Random.value < 0.3f)) BloodFX.Mist(point, dir, 0.25f * Settings.BloodScale);
                if (part.Type == PartType.Head && spec.Dmg >= 20f && Settings.Organs && Random.value < 0.35f) GoreKit.KnockTeeth(part, dir, Random.Range(1, 3));
                AudioManager.Play(spec.Kick ? "kick_hit" : "punch_hit", point, 1f, Random.Range(0.9f, 1.1f));
                if (head && spec.Dmg >= 20f) AudioManager.Play("bone_crack", point, 0.45f, Random.Range(0.9f, 1.1f));
                Flash(spec.Name);
                CameraFX.Shake(spec.Shake);
                CameraFX.Punch(-spec.Shake * 2f, 0f);
                GameEvents.EmitNoise(point, 10f, 0.4f, p);
                return true;
            }
            // Other players (PvP), props and anything else that can be hit.
            var origin = p.transform.position + Vector3.up * (spec.Zone == 0 ? 1.5f : spec.Zone == 1 ? 1.1f : 0.5f);
            if (Physics.SphereCast(origin, 0.3f, Flat, out var hit, spec.Reach, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                var info = new DamageInfo { Amount = spec.Dmg * (super ? 3f : 1f), Force = dir * spec.Force, Point = hit.point, Normal = hit.normal, Direction = dir, Type = DamageType.Blunt, Source = p, Weapon = "Boxing: " + spec.Name };
                var dmg = hit.collider.GetComponentInParent<IDamageable>();
                if (dmg != null) dmg.TakeDamage(info);
                if (hit.rigidbody != null) NetPhysics.Impulse(hit.rigidbody, dir * spec.Force * (super ? 4f : 1f), hit.point);
                AudioManager.Play(dmg is RemotePlayer ? (spec.Kick ? "kick_hit" : "punch_hit") : Surface.Of(hit.collider) == SurfaceType.Metal ? "metal_impact" : "body_thud_soft", hit.point, 0.8f, Random.Range(0.9f, 1.1f));
                CameraFX.Shake(spec.Shake * 0.6f);
                if (dmg is RemotePlayer) Flash(spec.Name);
                return true;
            }
            return false;
        }

        void Flash(string name)
        {
            hitCombo = Time.time < hitComboUntil ? hitCombo + 1 : 1;
            hitComboUntil = Time.time + 1.6f;
            flashText = name.ToUpperInvariant() + (hitCombo > 1 ? "   x" + hitCombo : "");
            flashT = 1.1f;
        }

        // ------------------------------------------------------------------ headlock

        Vector3 LockPoint()
        {
            var t = Player.transform;
            return t.position + Flat * 0.45f + Right * 0.16f + Vector3.up * 1.12f;
        }

        void StartLock(Goredoll d)
        {
            BodyPart bp = null;
            foreach (var t in new[] { PartType.Neck, PartType.Head, PartType.Chest })
                if (d.Parts.TryGetValue(t, out var c) && c != null && !c.Severed && !c.Gibbed && c.Body != null) { bp = c; break; }
            if (bp == null) return;
            locked = d; lockPart = bp; lockFall = 0f; chokeSend = 0f;
            if (d.NetProxy && NetState.IsClient)
            {
                NetManager.I.RequestDollCommand(d, 26, bp.Type);
                NetManager.I.ClientGrabActive = true;
            }
            else if (d.Ragdoll != null) d.Ragdoll.Fall(2.5f);
            if (d.Vitals.Conscious) d.Voice.Say(Random.value < 0.5f ? "v_no" : "v_scream", 1f, true);
            AudioManager.Play("cloth", bp.Body.position, 0.8f);
            Stats.Add("headlocks");
        }

        void ReleaseLock()
        {
            if (locked == null) return;
            if (NetState.IsClient && NetManager.I != null) NetManager.I.RequestGrabEnd(Vector3.zero);
            else if (locked && locked.Vitals != null) locked.Vitals.Choke = 0f;
            locked = null; lockPart = null;
        }

        void TickLock(InputFrame f, float dt)
        {
            var body = lockPart != null ? lockPart.Body : null;
            if (body == null || lockPart.Severed || lockPart.Gibbed) { ReleaseLock(); return; }
            var target = LockPoint();
            if (Vector3.Distance(body.position, target) > 2.6f) { ReleaseLock(); return; }
            choke = f.Aim ? 1f : 0.35f;
            if (NetState.IsClient && locked.NetProxy)
            {
                var nm = NetManager.I;
                nm.ClientGrabTarget = target; nm.ClientGrabRot = body.rotation; nm.ClientGrabRotating = false; nm.ClientGrabActive = true;
                chokeSend -= dt;
                if (chokeSend <= 0f) { chokeSend = 0.35f; nm.RequestDollCommand(locked, 23, (PartType)(choke > 0.8f ? 2 : 1)); }
            }
            else
            {
                var desired = Vector3.ClampMagnitude((target - body.position) * 16f, 22f);
                RB.SetVel(body, Vector3.Lerp(RB.Vel(body), desired, 1f - Mathf.Exp(-20f * dt)));
                body.WakeUp();
                if (locked.Parts.TryGetValue(PartType.Chest, out var chest) && chest != null && chest != lockPart && chest.Body != null && !chest.Severed)
                {
                    var ct = target + Flat * 0.2f + Vector3.down * 0.3f;
                    RB.SetVel(chest.Body, Vector3.Lerp(RB.Vel(chest.Body), Vector3.ClampMagnitude((ct - chest.Body.position) * 8f, 12f), 1f - Mathf.Exp(-8f * dt)));
                }
                lockFall -= dt;
                if (lockFall <= 0f && locked.Ragdoll != null) { locked.Ragdoll.Fall(2f); lockFall = 1.5f; }
                var v = locked.Vitals;
                v.Choke = choke; v.ChokeUntil = Time.time + 0.4f;
                // A conscious Goredoll kicks and claws to get free.
                float str = v.Conscious ? v.Consciousness * (1f - choke * 0.6f) : 0f;
                lockStruggle -= dt;
                if (str > 0.2f && lockStruggle <= 0f)
                {
                    lockStruggle = Random.Range(0.15f, 0.4f);
                    var limb = Rng.Pick(new[] { PartType.HandL, PartType.HandR, PartType.FootL, PartType.FootR, PartType.ForearmL, PartType.ForearmR });
                    if (locked.Parts.TryGetValue(limb, out var lp) && lp != null && lp.Body != null && !lp.Severed) lp.Body.AddForce(Random.onUnitSphere * lp.Body.mass * 3f * str, ForceMode.Impulse);
                }
                if (v.Conscious && choke > 0.8f && Time.time > nextVoice) { nextVoice = Time.time + Random.Range(1.2f, 2.2f); locked.Voice.Say("v_choke", 0.9f, false); }
            }
            if (move == FightMove.None)
            {
                if (f.Fire) Start(FightMove.LockPunch);
                else if (f.Kick) Start(FightMove.Knee);
                else if (f.Reload) Start(FightMove.Throw);
                else if (f.Use || f.Middle) ReleaseLock();
            }
        }

        // ------------------------------------------------------------------ network

        void SendState(int st, bool force)
        {
            if (!force && st == sentState) return;
            sentState = st;
            FightNet.SendState(st);
        }

        // ------------------------------------------------------------------ HUD

        public override void DrawOverlay(float W, float H)
        {
            var p = Player;
            if (p == null || p.Cam == null) return;
            bool touch = Settings.TouchControlsActive;
            if (locked != null && locked) DrawVitals(p, locked);
            else if (candidate != null && candidate && !grab.Holding)
            {
                var top = candidate.Position + Vector3.up * 1.15f;
                var sp = p.Cam.WorldToScreenPoint(top);
                if (sp.z > 0.1f)
                {
                    var g = new Vector2(sp.x / UI.Scale, (Screen.height - sp.y) / UI.Scale);
                    string txt = touch ? "HEADLOCK  /  GRAB" : "E  HEADLOCK      MMB  GRAB";
                    float tw = UI.TextWidth(txt, 18) + 30;
                    UI.Rounded(new Rect(g.x - tw / 2, g.y - 20, tw, 38), new Color(0f, 0f, 0f, 0.6f), true);
                    UI.Label(new Rect(g.x - tw / 2, g.y - 20, tw, 38), txt, 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                }
            }
            if (flashT > 0f)
            {
                float a = Mathf.Clamp01(flashT / 0.4f);
                float sc = 1f + Mathf.Clamp01(flashT - 0.8f) * 0.6f;
                UI.Label(new Rect(0, H * 0.2f, W, 70), flashText, Mathf.RoundToInt(44 * sc), new Color(1f, 0.85f, 0.2f, a), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
            }
            if (!touch)
            {
                string help = locked != null ? "LMB punch   F knee   hold RMB choke   R throw   E release"
                    : grab.Holding ? "LMB throw   RMB / MMB drop"
                    : "LMB combo   crouch+LMB body shot   sprint+LMB overhand   F kick (crouch: low kick, in air: flying kick, sprint: push kick)   R spinning kick   Q axe kick   RMB guard";
                UI.Label(new Rect(20, H - 46, W - 40, 30), help, 15, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter);
            }
        }

        static void DrawVitals(PlayerController p, Goredoll d)
        {
            Vector3 top = d.Position + Vector3.up * 0.9f;
            if (d.Parts.TryGetValue(PartType.Head, out var hp) && hp != null && hp.Body != null) top = hp.Body.position + Vector3.up * 0.55f;
            var sp = p.Cam.WorldToScreenPoint(top);
            Vector2 g = sp.z > 0.1f ? new Vector2(sp.x / UI.Scale, (Screen.height - sp.y) / UI.Scale) : new Vector2(UI.VW / 2f, 200f);
            float cons, pain, spo2; int alert;
            if (NetState.IsClient && FightNet.VitalsFresh) { cons = FightNet.RemoteVitals.x / 100f; pain = FightNet.RemoteVitals.y; spo2 = FightNet.RemoteVitals.z; alert = FightNet.RemoteAlert; }
            else { var v = d.Vitals; cons = v.Consciousness; pain = v.Pain; spo2 = v.SpO2; alert = FightingStyles.AlertCode(d); }
            var r = new Rect(g.x - 170, g.y - 200, 340, 190);
            r.x = Mathf.Clamp(r.x, 10, UI.VW - r.width - 10); r.y = Mathf.Clamp(r.y, 10, UI.VH - r.height - 10);
            UI.Rounded(r, new Color(0.04f, 0.04f, 0.06f, 0.82f));
            UI.Label(new Rect(r.x + 14, r.y + 6, r.width - 28, 30), d.DisplayName.ToUpperInvariant(), 19, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            alert = Mathf.Clamp(alert, 0, FightingStyles.AlertNames.Length - 1);
            Color ac = alert <= 1 ? new Color(0.6f, 0.6f, 0.65f) : alert == 2 ? new Color(1f, 0.75f, 0.2f) : alert <= 5 ? new Color(1f, 0.35f, 0.3f) : new Color(0.4f, 0.9f, 0.45f);
            UI.Label(new Rect(r.x + 14, r.y + 6, r.width - 28, 30), FightingStyles.AlertNames[alert], 17, ac, TextAnchor.MiddleRight, FontStyle.Bold);
            float y = r.y + 42;
            Bar(new Rect(r.x + 14, y, r.width - 28, 30), "CONSCIOUSNESS", Mathf.Clamp01(cons), Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.3f, 0.85f, 0.4f), cons), Mathf.RoundToInt(cons * 100f) + "%"); y += 36;
            Bar(new Rect(r.x + 14, y, r.width - 28, 30), "PAIN", Mathf.Clamp01(pain / 100f), new Color(1f, 0.5f, 0.15f), Mathf.RoundToInt(pain) + "%"); y += 36;
            float ox = Mathf.Clamp01((spo2 - 30f) / 69f);
            Bar(new Rect(r.x + 14, y, r.width - 28, 30), "OXYGEN", ox, Color.Lerp(new Color(0.55f, 0.2f, 0.75f), new Color(0.3f, 0.7f, 1f), ox), Mathf.RoundToInt(spo2) + "%"); y += 36;
            UI.Label(new Rect(r.x + 14, y - 2, r.width - 28, 22), Settings.TouchControlsActive ? "AIM = choke harder" : "hold RMB to choke harder", 14, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleCenter);
        }

        static void Bar(Rect r, string label, float k, Color c, string value)
        {
            UI.Fill(r, new Color(1f, 1f, 1f, 0.08f));
            UI.Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(k), r.height), c.WithAlpha(0.75f));
            UI.Label(new Rect(r.x + 8, r.y, r.width - 16, r.height), label, 15, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Label(new Rect(r.x + 8, r.y, r.width - 16, r.height), value, 15, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
        }
    }

    // ====================================================================== multiplayer

    /// <summary>
    /// Fight moves and headlock/carry state go to every player through the Open World event channel so their copy of
    /// your body plays the same animation (Net/RemotePlayer.cs). The host sends headlock vitals to the player holding it.
    /// </summary>
    public static class FightNet
    {
        public static Vector3 RemoteVitals;
        public static int RemoteAlert = 7;
        static float vitalsTime = -10f;
        public static bool VitalsFresh => Time.time - vitalsTime < 1.2f;

        public static void SendMove(FightMove m)
        {
            if (!NetState.Active || NetManager.I == null) return;
            NetManager.I.OWRequest(OWReq.Fight, "m", (long)m);
        }

        public static void SendState(int st)
        {
            if (!NetState.Active || NetManager.I == null) return;
            NetManager.I.OWRequest(OWReq.Fight, "st", st);
        }

        public static void HostRequest(byte pid, string s, long n, Vector3 v)
        {
            if (NetManager.I == null) return;
            if (s == "m" || s == "st") NetManager.I.OWEvent(255, OWEv.Fight, pid, s, n, v);
        }

        public static void OnEvent(byte from, string s, long n, Vector3 v)
        {
            if (s == "vit") { RemoteVitals = v; RemoteAlert = (int)n; vitalsTime = Time.time; return; }
            var nm = NetManager.I;
            if (nm == null || from == OWDirector.MyId || !nm.Players.TryGetValue(from, out var np) || np.Avatar == null) return;
            if (s == "m") { np.Avatar.FightMove = (byte)n; np.Avatar.FightStart = Time.time; }
            else if (s == "st") np.Avatar.FightFlags = (int)n;
        }

        // ---- host: client headlocks (doll command 23) and fight grabs (26)

        sealed class Lock { public Goredoll Doll; public float Level, Until, NextSend, NextFall; }
        static readonly Dictionary<byte, Lock> locks = new Dictionary<byte, Lock>();

        public static void HostChoke(byte pid, Goredoll d, int level)
        {
            if (d == null) return;
            if (!locks.TryGetValue(pid, out var l)) locks[pid] = l = new Lock();
            l.Doll = d; l.Level = level >= 2 ? 1f : 0.35f; l.Until = Time.time + 1f;
            d.Vitals.Choke = l.Level; d.Vitals.ChokeUntil = l.Until;
            FightHost.Ensure();
        }

        static float nextGrabFall;

        public static void HostTick()
        {
            if (!NetState.IsHost) return;
            var nm = NetManager.I;
            if (nm == null) return;
            // Goredolls carried by clients stay limp (they'd stand back up otherwise).
            if (Time.time >= nextGrabFall)
            {
                nextGrabFall = Time.time + 1.5f;
                foreach (var np in nm.Players.Values)
                {
                    if (np.GrabBody == null || !np.GrabIsDoll) continue;
                    var gd = np.GrabBody.GetComponentInParent<Goredoll>();
                    if (gd != null && gd.Ragdoll != null) gd.Ragdoll.Fall(2f);
                }
            }
            if (locks.Count == 0) return;
            List<byte> dead = null;
            foreach (var kv in locks)
            {
                var l = kv.Value;
                if (l.Doll == null || Time.time > l.Until) { (dead ??= new List<byte>()).Add(kv.Key); continue; }
                if (Time.time >= l.NextFall && l.Doll.Ragdoll != null) { l.NextFall = Time.time + 1.5f; l.Doll.Ragdoll.Fall(2f); }
                if (Time.time < l.NextSend) continue;
                l.NextSend = Time.time + 0.3f;
                var v = l.Doll.Vitals;
                nm.OWEvent(kv.Key, OWEv.Fight, 255, "vit", FightingStyles.AlertCode(l.Doll), new Vector3(v.Consciousness * 100f, v.Pain, v.SpO2));
            }
            if (dead != null) foreach (var k in dead) locks.Remove(k);
        }
    }

    /// <summary>Host-side ticker for <see cref="FightNet"/> (made on the first client headlock).</summary>
    public sealed class FightHost : MonoBehaviour
    {
        static FightHost I;
        public static void Ensure()
        {
            if (I != null) return;
            var go = new GameObject("FightHost");
            DontDestroyOnLoad(go);
            I = go.AddComponent<FightHost>();
        }
        void Update() => FightNet.HostTick();
    }
}
