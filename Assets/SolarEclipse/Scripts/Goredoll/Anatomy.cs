using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    public enum PartType
    {
        Pelvis, Abdomen, Chest, Neck, Head,
        UpperArmL, ForearmL, HandL, UpperArmR, ForearmR, HandR,
        ThighL, ShinL, FootL, ThighR, ShinR, FootR,
    }

    public enum ColliderShape { Box, Capsule, Sphere }

    public enum OrganKind
    {
        Brain, Eye, Jaw, Trachea, Carotid, CervicalSpine, Heart, Aorta, Lung, ThoracicSpine, Liver, Stomach, Spleen,
        Kidney, Intestines, LumbarSpine, Bladder, IliacArtery, FemoralArtery, BrachialArtery, RadialArtery, Bone,
    }

    /// <summary>Static data for one body part. Rest pose: standing, facing +Z, arms hanging, feet at y=0. Right side = +X.</summary>
    public sealed class PartSpec
    {
        public PartType Type;
        public string Name, Medical;
        public PartType? Parent;
        public Vector3 Pivot;              // world rest position of the joint (part origin)
        public ColliderShape Shape;
        public Vector3 ColliderCenter, ColliderSize; // capsule: size.x = radius, size.y = height
        public float Mass, Health;
        public Vector2 LimitX;             // (min, max) degrees, in "natural" sign (see ActiveRagdoll)
        public float LimitY, LimitZ;
        public float Muscle;               // slerp drive spring
        public float DamageMultiplier = 1f;
        public bool Vital, CanSever, IsLeg, IsArm;
        public float Length;               // limb length along -Y (or +Y for spine)
        public bool Left => Name.EndsWith("L");
        public bool Right => Name.EndsWith("R");
        public float Side => Name.EndsWith("L") ? -1f : 1f;
    }

    /// <summary>An organ or vessel volume in a part's local space.</summary>
    public sealed class OrganSpec
    {
        public OrganKind Kind;
        public PartType Part;
        public string Name;
        public Vector3 Center;
        public float Radius;
        public float BleedMl;      // ml/s added when hit
        public float Pain;         // pain added
        public float Lethality;    // 0..1, 1 = instant death
        public bool Arterial;
    }

    public static class Anatomy
    {
        public static readonly Dictionary<PartType, PartSpec> Parts = new Dictionary<PartType, PartSpec>();
        public static readonly List<OrganSpec> Organs = new List<OrganSpec>();
        public const float TotalBlood = 5000f;

        static Anatomy()
        {
            const float sx = 0.215f, lx = 0.095f;
            Add(PartType.Pelvis, "Pelvis", "pelvis", null, new Vector3(0, 0.95f, 0), ColliderShape.Box, new Vector3(0, 0.01f, 0), new Vector3(0.32f, 0.2f, 0.2f), 11f, 120f, new Vector2(0, 0), 0, 0, 0, 1f, true, false);
            Add(PartType.Abdomen, "Abdomen", "abdomen", PartType.Pelvis, new Vector3(0, 1.06f, 0), ColliderShape.Box, new Vector3(0, 0.085f, 0.005f), new Vector3(0.28f, 0.17f, 0.19f), 8f, 110f, new Vector2(-30, 40), 20, 20, 2600f, 1f, true, false, 0.17f);
            Add(PartType.Chest, "Chest", "thorax", PartType.Abdomen, new Vector3(0, 1.23f, 0), ColliderShape.Box, new Vector3(0, 0.14f, 0.01f), new Vector3(0.35f, 0.28f, 0.22f), 16f, 130f, new Vector2(-25, 35), 25, 20, 2600f, 1f, true, false, 0.28f);
            Add(PartType.Neck, "Neck", "neck", PartType.Chest, new Vector3(0, 1.51f, 0), ColliderShape.Capsule, new Vector3(0, 0.045f, 0), new Vector3(0.052f, 0.12f, 0), 1.2f, 45f, new Vector2(-30, 40), 40, 25, 700f, 1.6f, true, true, 0.09f);
            Add(PartType.Head, "Head", "head", PartType.Neck, new Vector3(0, 1.60f, 0), ColliderShape.Capsule, new Vector3(0, 0.115f, 0.01f), new Vector3(0.095f, 0.25f, 0), 5f, 60f, new Vector2(-30, 35), 50, 25, 450f, 2.5f, true, true, 0.23f);
            foreach (var side in new[] { -1f, 1f })
            {
                string s = side < 0 ? "L" : "R";
                var ua = side < 0 ? PartType.UpperArmL : PartType.UpperArmR;
                var fa = side < 0 ? PartType.ForearmL : PartType.ForearmR;
                var ha = side < 0 ? PartType.HandL : PartType.HandR;
                var th = side < 0 ? PartType.ThighL : PartType.ThighR;
                var sh = side < 0 ? PartType.ShinL : PartType.ShinR;
                var ft = side < 0 ? PartType.FootL : PartType.FootR;
                string sideName = side < 0 ? "left" : "right";
                Add(ua, "UpperArm" + s, sideName + " upper arm", PartType.Chest, new Vector3(sx * side, 1.46f, 0), ColliderShape.Capsule, new Vector3(0, -0.14f, 0), new Vector3(0.048f, 0.33f, 0), 2.3f, 60f, new Vector2(-160, 60), 70, 90, 650f, 0.75f, false, true, 0.28f, arm: true);
                Add(fa, "Forearm" + s, sideName + " forearm", ua, new Vector3(sx * side, 1.18f, 0), ColliderShape.Capsule, new Vector3(0, -0.125f, 0), new Vector3(0.04f, 0.29f, 0), 1.5f, 50f, new Vector2(-150, 0), 60, 5, 420f, 0.7f, false, true, 0.25f, arm: true);
                Add(ha, "Hand" + s, sideName + " hand", fa, new Vector3(sx * side, 0.93f, 0), ColliderShape.Box, new Vector3(0, -0.08f, 0.005f), new Vector3(0.045f, 0.17f, 0.095f), 0.5f, 30f, new Vector2(-60, 60), 20, 35, 90f, 0.6f, false, true, 0.18f, arm: true);
                Add(th, "Thigh" + s, sideName + " thigh", PartType.Pelvis, new Vector3(lx * side, 0.90f, 0), ColliderShape.Capsule, new Vector3(0, -0.21f, 0), new Vector3(0.075f, 0.46f, 0), 9.5f, 90f, new Vector2(-110, 35), 35, 45, 2400f, 0.85f, false, true, 0.42f, leg: true);
                Add(sh, "Shin" + s, sideName + " lower leg", th, new Vector3(lx * side, 0.48f, 0), ColliderShape.Capsule, new Vector3(0, -0.2f, 0), new Vector3(0.055f, 0.44f, 0), 4f, 70f, new Vector2(0, 145), 5, 5, 1900f, 0.75f, false, true, 0.40f, leg: true);
                Add(ft, "Foot" + s, sideName + " foot", sh, new Vector3(lx * side, 0.08f, 0), ColliderShape.Box, new Vector3(0, -0.035f, 0.05f), new Vector3(0.1f, 0.075f, 0.26f), 1.1f, 40f, new Vector2(-35, 40), 15, 20, 650f, 0.55f, false, true, 0.08f, leg: true);

                Organ(OrganKind.FemoralArtery, th, "femoral artery", new Vector3(-0.03f * side, -0.1f, 0.03f), 0.022f, 45f, 25f, 0, true);
                Organ(OrganKind.Bone, th, "femur", new Vector3(0, -0.21f, 0), 0.025f, 4f, 35f, 0);
                Organ(OrganKind.Bone, sh, "tibia", new Vector3(0, -0.2f, 0.015f), 0.022f, 2f, 30f, 0);
                Organ(OrganKind.BrachialArtery, ua, "brachial artery", new Vector3(-0.022f * side, -0.15f, 0.01f), 0.014f, 25f, 20f, 0, true);
                Organ(OrganKind.Bone, ua, "humerus", new Vector3(0, -0.14f, 0), 0.02f, 2f, 25f, 0);
                Organ(OrganKind.RadialArtery, fa, "radial artery", new Vector3(0, -0.21f, 0.022f), 0.012f, 14f, 15f, 0, true);
                Organ(OrganKind.Bone, fa, "radius and ulna", new Vector3(0, -0.12f, 0), 0.018f, 2f, 22f, 0);
                Organ(OrganKind.Kidney, PartType.Abdomen, sideName + " kidney", new Vector3(0.065f * side, 0.07f, -0.065f), 0.032f, 12f, 30f, 0);
                Organ(OrganKind.Lung, PartType.Chest, sideName + " lung", new Vector3(0.08f * side, 0.15f, 0.0f), 0.072f, 6f, 25f, 0);
                Organ(OrganKind.Carotid, PartType.Neck, sideName + " carotid artery", new Vector3(0.026f * side, 0.045f, 0.022f), 0.017f, 70f, 20f, 0, true);
                Organ(OrganKind.Eye, PartType.Head, sideName + " eye", new Vector3(0.031f * side, 0.127f, 0.075f), 0.014f, 1f, 40f, 0);
                Organ(OrganKind.IliacArtery, PartType.Pelvis, sideName + " iliac artery", new Vector3(0.07f * side, 0.0f, 0.03f), 0.018f, 35f, 20f, 0, true);
            }
            Organ(OrganKind.Brain, PartType.Head, "brain", new Vector3(0, 0.15f, -0.005f), 0.072f, 15f, 0f, 1f);
            Organ(OrganKind.Jaw, PartType.Head, "mandible", new Vector3(0, 0.04f, 0.06f), 0.045f, 6f, 45f, 0);
            Organ(OrganKind.Trachea, PartType.Neck, "trachea", new Vector3(0, 0.045f, 0.03f), 0.016f, 4f, 30f, 0);
            Organ(OrganKind.CervicalSpine, PartType.Neck, "cervical spine", new Vector3(0, 0.045f, -0.025f), 0.02f, 3f, 20f, 0);
            Organ(OrganKind.Heart, PartType.Chest, "heart", new Vector3(-0.03f, 0.12f, 0.035f), 0.055f, 90f, 30f, 0.9f, true);
            Organ(OrganKind.Aorta, PartType.Chest, "aorta", new Vector3(-0.01f, 0.2f, 0.0f), 0.02f, 120f, 25f, 0.8f, true);
            Organ(OrganKind.ThoracicSpine, PartType.Chest, "thoracic spine", new Vector3(0, 0.12f, -0.09f), 0.032f, 4f, 20f, 0);
            Organ(OrganKind.ThoracicSpine, PartType.Chest, "thoracic spine", new Vector3(0, 0.22f, -0.09f), 0.03f, 4f, 20f, 0);
            Organ(OrganKind.Liver, PartType.Abdomen, "liver", new Vector3(0.07f, 0.12f, 0.02f), 0.06f, 30f, 35f, 0);
            Organ(OrganKind.Stomach, PartType.Abdomen, "stomach", new Vector3(-0.05f, 0.11f, 0.045f), 0.05f, 10f, 40f, 0);
            Organ(OrganKind.Spleen, PartType.Abdomen, "spleen", new Vector3(-0.09f, 0.12f, -0.02f), 0.035f, 25f, 25f, 0);
            Organ(OrganKind.Intestines, PartType.Abdomen, "small intestine", new Vector3(0, 0.03f, 0.04f), 0.075f, 8f, 45f, 0);
            Organ(OrganKind.LumbarSpine, PartType.Abdomen, "lumbar spine", new Vector3(0, 0.08f, -0.085f), 0.032f, 4f, 20f, 0);
            Organ(OrganKind.Aorta, PartType.Abdomen, "abdominal aorta", new Vector3(-0.012f, 0.08f, -0.05f), 0.016f, 90f, 20f, 0.5f, true);
            Organ(OrganKind.Bladder, PartType.Pelvis, "bladder", new Vector3(0, 0.0f, 0.05f), 0.04f, 6f, 30f, 0);
            Organ(OrganKind.Intestines, PartType.Pelvis, "large intestine", new Vector3(0, 0.05f, 0.02f), 0.06f, 6f, 35f, 0);
        }

        static void Add(PartType t, string name, string medical, PartType? parent, Vector3 pivot, ColliderShape shape, Vector3 cc, Vector3 cs,
            float mass, float health, Vector2 limX, float limY, float limZ, float muscle, float dmgMul, bool vital, bool canSever, float length = 0.1f, bool arm = false, bool leg = false)
        {
            Parts[t] = new PartSpec
            {
                Type = t, Name = name, Medical = medical, Parent = parent, Pivot = pivot, Shape = shape, ColliderCenter = cc, ColliderSize = cs,
                Mass = mass, Health = health, LimitX = limX, LimitY = limY, LimitZ = limZ, Muscle = muscle, DamageMultiplier = dmgMul,
                Vital = vital, CanSever = canSever, Length = length, IsArm = arm, IsLeg = leg,
            };
        }

        static void Organ(OrganKind kind, PartType part, string name, Vector3 c, float r, float bleed, float pain, float lethal, bool arterial = false)
            => Organs.Add(new OrganSpec { Kind = kind, Part = part, Name = name, Center = c, Radius = r, BleedMl = bleed, Pain = pain, Lethality = lethal, Arterial = arterial });

        public static IEnumerable<OrganSpec> OrgansOf(PartType part)
        {
            foreach (var o in Organs) if (o.Part == part) yield return o;
        }

        /// <summary>Organs crossed by a wound track from <paramref name="localEntry"/> along <paramref name="localDir"/> for <paramref name="depth"/> metres.</summary>
        public static List<OrganSpec> Track(PartType part, Vector3 localEntry, Vector3 localDir, float depth, float widen = 0f)
        {
            var hits = new List<OrganSpec>();
            foreach (var o in Organs)
            {
                if (o.Part != part) continue;
                var toC = o.Center - localEntry;
                float t = Mathf.Clamp(Vector3.Dot(toC, localDir), 0f, depth);
                float d = Vector3.Distance(localEntry + localDir * t, o.Center);
                if (d <= o.Radius + widen) hits.Add(o);
            }
            hits.Sort((a, b) => Vector3.Dot(a.Center - localEntry, localDir).CompareTo(Vector3.Dot(b.Center - localEntry, localDir)));
            return hits;
        }

        public static bool IsLeftSide(PartType t) => Parts[t].Left;

        /// <summary>Human-readable anatomical location from a local hit point.</summary>
        public static string Describe(PartType part, Vector3 local)
        {
            var spec = Parts[part];
            string face = local.z > 0.025f ? "anterior" : local.z < -0.025f ? "posterior" : "lateral";
            string side = "";
            if (!spec.Left && !spec.Right) side = local.x > 0.03f ? "right " : local.x < -0.03f ? "left " : "midline ";
            string level = "";
            if (spec.Length > 0.15f)
            {
                float t = Mathf.Abs(local.y) / spec.Length;
                level = t < 0.33f ? (spec.Type == PartType.Chest || spec.Type == PartType.Abdomen ? "lower " : "proximal ") : t < 0.66f ? "mid " : (spec.Type == PartType.Chest || spec.Type == PartType.Abdomen ? "upper " : "distal ");
            }
            return face + " " + side + level + spec.Medical;
        }
    }
}
