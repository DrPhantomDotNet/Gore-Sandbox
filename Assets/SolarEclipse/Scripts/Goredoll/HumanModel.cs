using System;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Procedural human body meshes, one per body part, in part-local space (origin at the joint).
    /// Smooth lofted limbs and torso with clothing seams, hands with fingers, shoes and a sculpted head.
    /// </summary>
    public static class HumanModel
    {
        const int Seg = 16;

        public static Mesh Build(PartType type, Appearance a)
        {
            var mb = new MeshBuilder { Gloss = 0.3f };
            switch (type)
            {
                case PartType.Pelvis: Pelvis(mb, a); break;
                case PartType.Abdomen: Abdomen(mb, a); break;
                case PartType.Chest: Chest(mb, a); break;
                case PartType.Neck: Neck(mb, a); break;
                case PartType.Head: Head(mb, a); break;
                case PartType.UpperArmL: case PartType.UpperArmR: UpperArm(mb, a); break;
                case PartType.ForearmL: case PartType.ForearmR: Forearm(mb, a); break;
                case PartType.HandL: case PartType.HandR: Hand(mb, a, Anatomy.Parts[type].Side); break;
                case PartType.ThighL: case PartType.ThighR: Thigh(mb, a); break;
                case PartType.ShinL: case PartType.ShinR: Shin(mb, a); break;
                case PartType.FootL: case PartType.FootR: Foot(mb, a); break;
            }
            return mb.ToMesh(type.ToString());
        }

        static Color Gloss(Color c, float g) { c.a = g; return c; }

        /// <summary>Loft rings along an axis from samples (t, radiusX, radiusZ, colour, gloss, zOffset).</summary>
        static void Rings(MeshBuilder mb, Vector3 start, Vector3 axis, float length, (float t, float rx, float rz, Color col, float gloss, float z)[] rings, bool capStart, bool capEnd)
        {
            mb.Loft(rings.Length, Seg, i => (start + axis * (rings[i].t * length) + Vector3.forward * rings[i].z, rings[i].rx, rings[i].rz), axis, capStart, capEnd, null,
                i => { mb.Gloss = rings[i].gloss; return rings[i].col; });
        }

        // ------------------------------------------------------------------ torso

        static void Pelvis(MeshBuilder mb, Appearance a)
        {
            var p = a.Pants; var belt = new Color(0.12f, 0.09f, 0.07f);
            float b = a.Build;
            Rings(mb, new Vector3(0, -0.1f, 0), Vector3.up, 0.21f, new[]
            {
                (0f, 0.12f * b, 0.09f, p, 0.15f, 0.0f),
                (0.25f, 0.155f * b, 0.105f, p, 0.15f, 0.0f),
                (0.6f, 0.165f * b, 0.11f, p, 0.15f, 0.005f),
                (0.82f, 0.158f * b, 0.105f, p, 0.15f, 0.005f),
                (0.82f, 0.158f * b, 0.106f, belt, 0.5f, 0.005f),
                (1f, 0.152f * b, 0.102f, belt, 0.5f, 0.005f),
            }, true, true);
            mb.SetColor(new Color(0.75f, 0.7f, 0.5f), 0.8f);
            mb.BevelBox(new Vector3(0, 0.07f, 0.104f), new Vector3(0.05f, 0.03f, 0.01f), 0.004f);
        }

        static void Abdomen(MeshBuilder mb, Appearance a)
        {
            var s = a.Shirt; float b = a.Build;
            Rings(mb, Vector3.zero, Vector3.up, 0.17f, new[]
            {
                (0f, 0.15f * b, 0.1f, s, 0.15f, 0.005f),
                (0.5f, 0.145f * b, 0.105f, s, 0.15f, 0.012f),
                (1f, 0.152f * b, 0.108f, s, 0.15f, 0.012f),
            }, true, true);
        }

        static void Chest(MeshBuilder mb, Appearance a)
        {
            var s = a.Shirt; float b = a.Build;
            Rings(mb, Vector3.zero, Vector3.up, 0.29f, new[]
            {
                (0f, 0.152f * b, 0.108f, s, 0.15f, 0.012f),
                (0.35f, 0.16f * b, 0.118f, s, 0.15f, 0.02f),
                (0.65f, 0.175f * b, 0.12f, s, 0.15f, 0.018f),
                (0.85f, 0.185f * b, 0.105f, s, 0.15f, 0.005f),
                (0.96f, 0.14f * b, 0.085f, s, 0.15f, -0.005f),
                (1f, 0.07f, 0.06f, s, 0.15f, -0.005f),
            }, true, true);
            // Collar
            mb.SetColor(s * 0.8f, 0.2f);
            mb.Torus(new Vector3(0, 0.285f, 0.0f), 0.058f, 0.012f, 16, 6);
        }

        static void Neck(MeshBuilder mb, Appearance a)
        {
            Rings(mb, new Vector3(0, -0.02f, 0), Vector3.up, 0.14f, new[]
            {
                (0f, 0.058f, 0.052f, a.Skin, 0.3f, 0f),
                (0.5f, 0.05f, 0.048f, a.Skin, 0.3f, 0.002f),
                (1f, 0.052f, 0.05f, a.Skin, 0.3f, 0.0f),
            }, false, false);
            // Adam's apple
            mb.SetColor(a.Skin, 0.3f);
            mb.Sphere(new Vector3(0, 0.055f, 0.042f), new Vector3(0.012f, 0.014f, 0.01f), 4, 8);
        }

        // ------------------------------------------------------------------ limbs

        static void UpperArm(MeshBuilder mb, Appearance a)
        {
            var sleeve = a.Shirt;
            var skin = a.Skin;
            float cut = a.LongSleeves ? 1.1f : 0.55f;
            Rings(mb, new Vector3(0, 0.03f, 0), Vector3.down, 0.31f, new[]
            {
                (0f, 0.05f, 0.05f, sleeve, 0.15f, 0f),
                (0.25f, 0.054f, 0.052f, sleeve, 0.15f, 0f),
                (Mathf.Min(cut, 1f), 0.05f, 0.048f, a.LongSleeves ? sleeve : sleeve, 0.15f, 0f),
                (Mathf.Min(cut, 1f), 0.046f, 0.044f, a.LongSleeves ? sleeve : skin, a.LongSleeves ? 0.15f : 0.3f, 0f),
                (0.8f, 0.042f, 0.042f, a.LongSleeves ? sleeve : skin, a.LongSleeves ? 0.15f : 0.3f, 0f),
                (1f, 0.038f, 0.038f, a.LongSleeves ? sleeve : skin, a.LongSleeves ? 0.15f : 0.3f, 0f),
            }, true, true);
        }

        static void Forearm(MeshBuilder mb, Appearance a)
        {
            var c = a.LongSleeves ? a.Shirt : a.Skin;
            float g = a.LongSleeves ? 0.15f : 0.3f;
            Rings(mb, new Vector3(0, 0.02f, 0), Vector3.down, 0.27f, new[]
            {
                (0f, 0.038f, 0.038f, c, g, 0f),
                (0.3f, 0.042f, 0.04f, c, g, 0.002f),
                (0.75f, 0.032f, 0.027f, c, g, 0f),
                (a.LongSleeves ? 0.9f : 1f, 0.028f, 0.024f, c, g, 0f),
                (a.LongSleeves ? 0.9f : 1f, 0.026f, 0.022f, a.Skin, 0.3f, 0f),
                (1f, 0.025f, 0.021f, a.Skin, 0.3f, 0f),
            }, true, true);
        }

        /// <summary>Relaxed hand hanging down: palm facing the body, fingers slightly curled.</summary>
        static void Hand(MeshBuilder mb, Appearance a, float side)
        {
            mb.SetColor(a.Skin, 0.3f);
            // Palm: thin in X (faces inward), wide in Z.
            mb.BevelBox(new Vector3(0, -0.045f, 0.005f), new Vector3(0.03f, 0.085f, 0.082f), 0.012f);
            // Four fingers along the bottom edge, two segments each with a gentle curl toward the palm.
            for (int f = 0; f < 4; f++)
            {
                float z = -0.028f + f * 0.019f + 0.005f;
                float len = f == 1 ? 0.085f : f == 2 ? 0.08f : f == 0 ? 0.07f : 0.062f;
                var basePt = new Vector3(0, -0.088f, z);
                var mid = basePt + new Vector3(-side * 0.008f, -len * 0.55f, 0);
                var tip = mid + new Vector3(-side * 0.016f, -len * 0.42f, 0);
                mb.Capsule(basePt, mid, 0.0085f, 0.0078f, 8, 2);
                mb.Capsule(mid, tip, 0.0078f, 0.0068f, 8, 2);
            }
            // Thumb from the front edge, angled forward/inward.
            var tb = new Vector3(-side * 0.008f, -0.03f, 0.042f);
            var tm = tb + new Vector3(-side * 0.012f, -0.03f, 0.02f);
            var tt = tm + new Vector3(-side * 0.012f, -0.026f, 0.006f);
            mb.Capsule(tb, tm, 0.0105f, 0.0095f, 8, 2);
            mb.Capsule(tm, tt, 0.0095f, 0.008f, 8, 2);
        }

        static void Thigh(MeshBuilder mb, Appearance a)
        {
            var p = a.Pants;
            Rings(mb, new Vector3(0, 0.04f, 0), Vector3.down, 0.46f, new[]
            {
                (0f, 0.082f, 0.085f, p, 0.12f, 0f),
                (0.2f, 0.08f, 0.083f, p, 0.12f, 0.005f),
                (0.6f, 0.066f, 0.068f, p, 0.12f, 0.005f),
                (0.92f, 0.055f, 0.058f, p, 0.12f, 0.004f),
                (1f, 0.052f, 0.055f, p, 0.12f, 0.004f),
            }, true, true);
        }

        static void Shin(MeshBuilder mb, Appearance a)
        {
            var p = a.Pants;
            Rings(mb, new Vector3(0, 0.03f, 0), Vector3.down, 0.43f, new[]
            {
                (0f, 0.055f, 0.058f, p, 0.12f, 0f),
                (0.25f, 0.058f, 0.06f, p, 0.12f, -0.008f),
                (0.7f, 0.05f, 0.052f, p, 0.12f, 0f),
                (0.92f, 0.054f, 0.056f, p, 0.12f, 0f),
                (1f, 0.054f, 0.056f, p, 0.12f, 0f),
            }, true, true);
        }

        static void Foot(MeshBuilder mb, Appearance a)
        {
            mb.SetColor(a.Shoes, 0.35f);
            mb.BevelBox(new Vector3(0, -0.035f, 0.045f), new Vector3(0.095f, 0.07f, 0.24f), 0.025f);
            mb.Sphere(new Vector3(0, -0.04f, 0.15f), new Vector3(0.048f, 0.032f, 0.05f), 6, 10);
            mb.SetColor(a.Shoes * 0.4f + new Color(0.1f, 0.1f, 0.1f), 0.2f);
            mb.Box(new Vector3(0, -0.07f, 0.048f), new Vector3(0.1f, 0.012f, 0.255f));
            mb.SetColor(Color.white, 0.3f);
            for (int i = 0; i < 3; i++) mb.Box(new Vector3(0, -0.0f, 0.06f + i * 0.025f), new Vector3(0.05f, 0.004f, 0.006f));
        }

        // ------------------------------------------------------------------ head

        public const float HeadCenterY = 0.115f;

        /// <summary>Skull + face sculpt, nose, ears, hair and beard. Eyes, lids, brows and mouth are separate (FaceRig).</summary>
        static void Head(MeshBuilder mb, Appearance a)
        {
            var c = new Vector3(0, HeadCenterY, 0.005f);
            var radii = new Vector3(0.077f, 0.112f, 0.095f);
            mb.SetColor(a.Skin, 0.32f);
            mb.Sphere(c, radii, 18, 24, p => SculptFace(p, radii));

            // Nose: a lofted wedge from the bridge down to the tip, plus nostril wings.
            float noseZ = 0.083f;
            mb.SetColor(a.Skin * 0.98f, 0.35f);
            mb.Loft(6, 10, i =>
            {
                float t = i / 5f;
                var pos = c + new Vector3(0, 0.03f - t * 0.055f, noseZ - 0.008f + t * 0.026f);
                float rx = Mathf.Lerp(0.007f, 0.013f, t * t);
                float rz = Mathf.Lerp(0.009f, 0.014f, t);
                return (pos, rx, rz);
            }, new Vector3(0, -0.055f, 0.026f).normalized, true, true);
            mb.Sphere(c + new Vector3(0, -0.026f, noseZ + 0.016f), new Vector3(0.012f, 0.01f, 0.01f), 5, 8);
            mb.Sphere(c + new Vector3(0.012f, -0.028f, noseZ + 0.008f), new Vector3(0.008f, 0.007f, 0.008f), 4, 6);
            mb.Sphere(c + new Vector3(-0.012f, -0.028f, noseZ + 0.008f), new Vector3(0.008f, 0.007f, 0.008f), 4, 6);
            mb.SetColor(new Color(0.15f, 0.08f, 0.06f), 0.1f);
            mb.Sphere(c + new Vector3(0.007f, -0.033f, noseZ + 0.012f), new Vector3(0.0035f, 0.0025f, 0.004f), 3, 6);
            mb.Sphere(c + new Vector3(-0.007f, -0.033f, noseZ + 0.012f), new Vector3(0.0035f, 0.0025f, 0.004f), 3, 6);

            // Ears
            mb.SetColor(a.Skin * 0.96f, 0.3f);
            foreach (var s in new[] { -1f, 1f })
            {
                mb.Sphere(c + new Vector3(s * 0.076f, 0.0f, -0.008f), new Vector3(0.012f, 0.03f, 0.02f), 6, 10);
                mb.Torus(c + new Vector3(s * 0.08f, 0.002f, -0.008f), 0.016f, 0.004f, 12, 4, new Vector3(1, 0, 0));
            }

            // Hair
            if (a.HairStyle != 2)
            {
                mb.SetColor(a.Hair, a.HairStyle == 1 ? 0.15f : 0.35f);
                float thick = a.HairStyle == 1 ? 1.03f : a.HairStyle == 3 ? 1.1f : 1.07f;
                var hr = radii * thick;
                mb.Sphere(c + new Vector3(0, 0.004f, -0.004f), hr, 14, 20, p => HairShape(p, hr, a.HairStyle));
            }
            if (a.Beard)
            {
                mb.SetColor(a.Hair, 0.2f);
                mb.Sphere(c + new Vector3(0, -0.06f, 0.04f), new Vector3(0.062f, 0.05f, 0.055f), 8, 12, p => p.z < -0.01f ? p * 0.6f : p);
            }
        }

        static float Gauss(float x, float y, float sx, float sy) => Mathf.Exp(-(x * x) / (2 * sx * sx) - (y * y) / (2 * sy * sy));

        static Vector3 SculptFace(Vector3 p, Vector3 r)
        {
            float nx = p.x / r.x, ny = p.y / r.y, nz = p.z / r.z;
            // Jaw: narrows toward the chin, back of the lower skull tucks into the neck.
            if (ny < -0.15f)
            {
                float k = MathX.SmoothStep01((-ny - 0.15f) / 0.85f);
                p.x *= Mathf.Lerp(1f, 0.62f, k);
                if (nz < 0) p.z *= Mathf.Lerp(1f, 0.55f, k);
                else p.z *= Mathf.Lerp(1f, 0.92f, k);
            }
            // Chin forward.
            p.z += 0.009f * Gauss(nx, ny + 0.88f, 0.35f, 0.15f) * Mathf.Clamp01(nz * 2f);
            // Back of the skull is fuller.
            if (nz < -0.2f && ny > -0.1f) p.z *= 1.05f;
            // Flatter face plane.
            if (nz > 0.5f) p.z *= 0.97f;
            // Brow ridge.
            p.z += 0.006f * Gauss(nx, ny - 0.22f, 0.55f, 0.07f) * Mathf.Clamp01(nz * 2f - 0.8f);
            // Eye sockets.
            p.z -= 0.009f * (Gauss(nx - 0.4f, ny - 0.1f, 0.18f, 0.08f) + Gauss(nx + 0.4f, ny - 0.1f, 0.18f, 0.08f)) * Mathf.Clamp01(nz * 2f - 0.6f);
            // Cheekbones.
            p.x += 0.005f * Mathf.Sign(nx) * Gauss(Mathf.Abs(nx) - 0.65f, ny + 0.05f, 0.2f, 0.12f) * Mathf.Clamp01(nz * 2f);
            // Mouth area slightly forward (muzzle), philtrum.
            p.z += 0.004f * Gauss(nx, ny + 0.42f, 0.35f, 0.15f) * Mathf.Clamp01(nz * 2f - 0.8f);
            // Temples slightly in.
            p.x -= 0.004f * Mathf.Sign(nx) * Gauss(Mathf.Abs(nx) - 0.85f, ny - 0.25f, 0.15f, 0.2f) * Mathf.Clamp01(nz + 0.5f);
            return p;
        }

        static Vector3 HairShape(Vector3 p, Vector3 r, int style)
        {
            float ny = p.y / r.y, nz = p.z / r.z;
            // Hairline: front stops at the forehead, sides above the ears, back at the nape (long style lower).
            float hairline = nz > 0.4f ? 0.38f : nz > -0.2f ? (style == 3 ? -0.1f : 0.12f) : (style == 3 ? -0.55f : -0.25f);
            if (ny < hairline) return p * 0.86f; // tuck inside the head
            if (style == 0 && ny > 0.6f) p.y += 0.004f;
            return p;
        }

        // ------------------------------------------------------------------ face features (FaceRig)

        public static Mesh Eye(Color iris)
        {
            var mb = new MeshBuilder();
            // Rings at polar angles from +Z; duplicated rings give crisp pupil/iris/sclera borders.
            (float ang, Color col, float gloss)[] rings =
            {
                (0f, Color.black, 0.95f), (14f, Color.black, 0.95f), (14f, iris, 0.95f), (26f, iris * 0.8f, 0.95f), (40f, iris * 0.6f, 0.95f),
                (40f, new Color(0.95f, 0.93f, 0.9f), 0.9f), (70f, new Color(0.95f, 0.93f, 0.9f), 0.9f), (110f, new Color(0.9f, 0.85f, 0.85f), 0.8f),
                (150f, new Color(0.85f, 0.75f, 0.75f), 0.6f), (179f, new Color(0.8f, 0.7f, 0.7f), 0.5f),
            };
            float r = 0.0118f;
            mb.Loft(rings.Length, 16, i =>
            {
                float a = rings[i].ang * Mathf.Deg2Rad;
                return (new Vector3(0, 0, Mathf.Cos(a) * r), Mathf.Max(Mathf.Sin(a) * r, 0.0003f), Mathf.Max(Mathf.Sin(a) * r, 0.0003f));
            }, Vector3.back, true, true, null, i => { mb.Gloss = rings[i].gloss; return rings[i].col; });
            return mb.ToMesh("Eye");
        }

        /// <summary>Upper eyelid: a shell over the top of the eye, pivot at the eye centre.</summary>
        public static Mesh Eyelid(Color skin, bool upper)
        {
            var mb = new MeshBuilder { Color = skin, Gloss = 0.35f };
            float r = 0.0132f;
            mb.Sphere(Vector3.zero, Vector3.one * r, 8, 14, p =>
            {
                // keep only the front upper (or lower) cap; collapse the rest into the eye
                bool keep = upper ? p.y > -0.002f && p.z > -0.004f : p.y < -0.004f && p.z > -0.004f;
                return keep ? p : p * 0.6f;
            });
            return mb.ToMesh(upper ? "LidUpper" : "LidLower");
        }

        public static Mesh Brow(Color hair)
        {
            var mb = new MeshBuilder { Color = hair, Gloss = 0.1f };
            mb.Capsule(new Vector3(-0.013f, 0, 0), new Vector3(0.013f, 0.002f, -0.002f), 0.0042f, 0.0028f, 6, 2);
            return mb.ToMesh("Brow");
        }

        public static Mesh Lip(Color lip, bool upper)
        {
            var mb = new MeshBuilder { Color = lip, Gloss = 0.55f };
            mb.Capsule(new Vector3(-0.019f, 0, 0), new Vector3(0.019f, 0, 0), upper ? 0.0055f : 0.0065f, upper ? 0.0055f : 0.0065f, 10, 3);
            return mb.ToMesh(upper ? "LipUpper" : "LipLower");
        }

        /// <summary>Dark mouth cavity with upper teeth and tongue; scaled vertically to open the mouth.</summary>
        public static Mesh MouthCavity()
        {
            var mb = new MeshBuilder { Color = new Color(0.18f, 0.03f, 0.04f), Gloss = 0.6f };
            mb.Sphere(new Vector3(0, -0.006f, -0.004f), new Vector3(0.019f, 0.009f, 0.008f), 6, 12);
            mb.SetColor(new Color(0.95f, 0.93f, 0.85f), 0.8f);
            mb.Box(new Vector3(0, 0.0005f, 0.002f), new Vector3(0.026f, 0.0045f, 0.004f));
            mb.SetColor(new Color(0.7f, 0.3f, 0.32f), 0.6f);
            mb.Sphere(new Vector3(0, -0.011f, -0.002f), new Vector3(0.012f, 0.003f, 0.007f), 4, 8);
            return mb.ToMesh("Mouth");
        }
    }
}
