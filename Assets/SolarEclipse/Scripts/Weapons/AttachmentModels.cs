using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Procedural attachment meshes (model-local, +Z forward, metres). Used by <see cref="Attachments"/>.</summary>
    public static class AttachmentModels
    {
        static readonly Color Black = new Color(0.07f, 0.07f, 0.08f), Dark = new Color(0.12f, 0.12f, 0.13f), Tan = new Color(0.5f, 0.43f, 0.3f);
        static readonly Color DotRed = new Color(1f, 0.12f, 0.08f);

        /// <summary>
        /// Builds an optic standing on <paramref name="basePos"/> (in <paramref name="parent"/>'s space) and returns the
        /// sight line (rear, front) in model-root space computed from <paramref name="rootBase"/> (same point, root space at rest).
        /// </summary>
        public static void Optic(WeaponModel m, int kind, Transform parent, Vector3 basePos, Vector3 rootBase, out Vector3 rear, out Vector3 front)
        {
            var b = new MeshBuilder { Color = Black, Gloss = 0.35f };
            float axis; float rz, fz;
            if (kind == 1)
            {
                // Red dot: low mount, small open hood. Dot on the front glass.
                axis = 0.028f;
                b.BevelBox(new Vector3(0, 0.005f, 0), new Vector3(0.022f, 0.01f, 0.042f), 0.002f);
                b.Box(new Vector3(0, (0.01f + axis - 0.0125f) * 0.5f, 0), new Vector3(0.026f, axis - 0.0125f - 0.01f + 0.002f, 0.03f));
                b.Box(new Vector3(0, axis - 0.0135f, 0), new Vector3(0.03f, 0.004f, 0.026f));
                b.Box(new Vector3(0, axis + 0.0135f, 0), new Vector3(0.03f, 0.004f, 0.026f));
                b.Box(new Vector3(0.0135f, axis, 0), new Vector3(0.004f, 0.031f, 0.026f));
                b.Box(new Vector3(-0.0135f, axis, 0), new Vector3(0.004f, 0.031f, 0.026f));
                b.Cylinder(new Vector3(0.0155f, axis, -0.004f), new Vector3(0.021f, axis, -0.004f), 0.005f, 0.005f, 8); // brightness knob
                rz = -0.013f; fz = 0.0115f;
                m.Piece("RedDot", b, Mats.LitMetal, basePos, parent);
                WeaponModels.SightDot(m, basePos + new Vector3(0, axis, fz), DotRed, 0.0012f, 4f, parent);
            }
            else if (kind == 2)
            {
                // Holographic: long box body with a square window at the front, ring + dot reticle.
                axis = 0.032f;
                b.BevelBox(new Vector3(0, 0.008f, -0.005f), new Vector3(0.032f, 0.016f, 0.075f), 0.003f);
                b.Box(new Vector3(0, axis - 0.016f, 0.018f), new Vector3(0.038f, 0.004f, 0.032f));
                b.Box(new Vector3(0, axis + 0.0165f, 0.018f), new Vector3(0.038f, 0.004f, 0.032f));
                b.Box(new Vector3(0.0175f, axis, 0.018f), new Vector3(0.004f, 0.037f, 0.032f));
                b.Box(new Vector3(-0.0175f, axis, 0.018f), new Vector3(0.004f, 0.037f, 0.032f));
                b.SetColor(Tan, 0.2f);
                b.Box(new Vector3(0.0165f, 0.008f, -0.025f), new Vector3(0.002f, 0.01f, 0.02f));
                rz = 0.002f; fz = 0.03f;
                m.Piece("HoloSight", b, Mats.LitMetal, basePos, parent);
                var ring = new MeshBuilder { Color = Color.white };
                ring.Torus(new Vector3(0, axis, fz), 0.0045f, 0.0003f, 24, 3, Vector3.forward);
                m.Piece("HoloRing", ring, Mats.Glow(DotRed, 3f), basePos, parent);
                WeaponModels.SightDot(m, basePos + new Vector3(0, axis, fz), DotRed, 0.0008f, 4f, parent);
            }
            else
            {
                // 4x scope: two rings, tube, eyepiece and objective bells.
                axis = 0.036f;
                for (int i = -1; i <= 1; i += 2)
                {
                    b.Box(new Vector3(0, 0.006f, i * 0.04f), new Vector3(0.02f, 0.012f, 0.014f));
                    b.Box(new Vector3(0, (0.012f + axis) * 0.5f, i * 0.04f), new Vector3(0.008f, axis - 0.012f, 0.012f));
                    b.Torus(new Vector3(0, axis, i * 0.04f), 0.0135f, 0.0025f, 14, 4, Vector3.forward);
                }
                b.Cylinder(new Vector3(0, axis, -0.07f), new Vector3(0, axis, 0.07f), 0.0115f, 0.0115f, 16);
                b.Cylinder(new Vector3(0, axis, -0.105f), new Vector3(0, axis, -0.07f), 0.0165f, 0.0115f, 16);
                b.Cylinder(new Vector3(0, axis, 0.07f), new Vector3(0, axis, 0.11f), 0.0115f, 0.019f, 16);
                b.Cylinder(new Vector3(0, axis + 0.011f, 0.0f), new Vector3(0, axis + 0.02f, 0.0f), 0.008f, 0.008f, 10);
                b.Cylinder(new Vector3(0.011f, axis, 0.0f), new Vector3(0.02f, axis, 0.0f), 0.008f, 0.008f, 10);
                b.SetColor(new Color(0.15f, 0.3f, 0.45f), 0.95f);
                b.Cylinder(new Vector3(0, axis, 0.109f), new Vector3(0, axis, 0.111f), 0.0175f, 0.0175f, 16);
                rz = -0.105f; fz = 0.11f;
                m.Piece("Scope4x", b, Mats.LitMetal, basePos, parent);
            }
            rear = rootBase + new Vector3(0, axis, rz);
            front = rootBase + new Vector3(0, axis, fz);
        }

        /// <summary>Swaps the magazine mesh for an extended stick or a drum (built from the stock mag's bounds).</summary>
        public static void Magazine(WeaponModel m, bool drum)
        {
            if (m.Mag == null) return;
            var mf = m.Mag.GetComponent<MeshFilter>();
            var src = m.MagMesh != null ? m.MagMesh : mf != null ? mf.sharedMesh : null;
            if (src == null || mf == null) return;
            var bb = src.bounds;
            float top = bb.max.y, len = bb.size.y, w = bb.size.x, d = bb.size.z;
            var mb = new MeshBuilder { Color = Dark, Gloss = 0.35f };
            if (!drum)
            {
                float L = len * 1.45f;
                mb.Box(new Vector3(bb.center.x, top - L * 0.5f, bb.center.z), new Vector3(w * 0.92f, L, d * 0.92f));
                mb.Box(new Vector3(bb.center.x, top - L + 0.004f, bb.center.z + 0.002f), new Vector3(w * 1.08f, 0.008f, d * 1.05f));
                for (int i = 0; i < 3; i++) mb.Box(new Vector3(bb.center.x, top - L * (0.3f + i * 0.2f), bb.center.z), new Vector3(w * 0.95f, 0.003f, d * 0.95f));
            }
            else
            {
                float stub = Mathf.Min(0.05f, len * 0.4f);
                float R = Mathf.Clamp(d * 1.35f, 0.045f, 0.068f);
                mb.Box(new Vector3(bb.center.x, top - stub * 0.5f, bb.center.z), new Vector3(w * 0.92f, stub, d * 0.92f));
                var c = new Vector3(bb.center.x, top - stub - R * 0.75f, bb.center.z);
                float half = Mathf.Max(0.026f, w * 1.1f);
                mb.Cylinder(c + Vector3.left * half, c + Vector3.right * half, R, R, 20);
                mb.SetColor(Black, 0.5f);
                mb.Cylinder(c + Vector3.right * half, c + Vector3.right * (half + 0.006f), R * 0.5f, R * 0.45f, 14); // winding key
                mb.Torus(c + Vector3.left * half * 0.4f, R * 1.002f, 0.002f, 20, 3, Vector3.right);
                mb.Torus(c + Vector3.right * half * 0.4f, R * 1.002f, 0.002f, 20, 3, Vector3.right);
            }
            var mesh = mb.ToMesh(drum ? "DrumMag" : "ExtMag");
            mf.sharedMesh = mesh;
            m.MagMesh = mesh;
        }

        /// <summary>Adds a suppressor or compensator in front of the muzzle; returns how far the muzzle moved forward.</summary>
        public static float MuzzleDevice(WeaponModel m, bool suppressor, bool big)
        {
            var mu = m.Muzzle;
            var b = new MeshBuilder { Color = Black, Gloss = 0.3f };
            float len;
            if (suppressor)
            {
                len = big ? 0.17f : 0.13f;
                float r = big ? 0.019f : 0.016f;
                b.Cylinder(new Vector3(0, 0, -0.012f), new Vector3(0, 0, 0f), r * 0.7f, r * 0.7f, 12);
                b.Cylinder(new Vector3(0, 0, 0f), new Vector3(0, 0, len), r, r, 18);
                b.SetColor(Dark, 0.5f);
                b.Torus(new Vector3(0, 0, 0.006f), r, 0.0015f, 18, 3, Vector3.forward);
                b.Torus(new Vector3(0, 0, len - 0.006f), r, 0.0015f, 18, 3, Vector3.forward);
                b.SetColor(Color.black, 0.1f);
                b.Cylinder(new Vector3(0, 0, len), new Vector3(0, 0, len + 0.0005f), r * 0.3f, r * 0.3f, 10);
            }
            else
            {
                len = big ? 0.055f : 0.04f;
                float r = big ? 0.013f : 0.011f;
                b.Cylinder(new Vector3(0, 0, 0f), new Vector3(0, 0, len), r, r, 14);
                b.SetColor(Color.black, 0.1f);
                for (int i = 0; i < 3; i++) b.Box(new Vector3(0, r * 0.85f, 0.01f + i * (len - 0.016f) / 2f), new Vector3(r * 1.1f, 0.003f, 0.006f)); // top ports
                for (int sx = -1; sx <= 1; sx += 2) b.Box(new Vector3(sx * r * 0.85f, 0, len * 0.55f), new Vector3(0.003f, r * 0.9f, len * 0.4f)); // side ports
                b.Cylinder(new Vector3(0, 0, len), new Vector3(0, 0, len + 0.0005f), r * 0.45f, r * 0.45f, 10);
            }
            var piece = m.Piece(suppressor ? "Suppressor" : "Compensator", b, Mats.LitMetal, mu.localPosition, mu.parent);
            piece.localRotation = mu.localRotation;
            return len;
        }

        /// <summary>Vertical or angled foregrip clamped under the handguard at <paramref name="under"/>.</summary>
        public static void Foregrip(WeaponModel m, Vector3 under, bool vertical)
        {
            var b = new MeshBuilder { Color = Black, Gloss = 0.2f };
            b.Box(new Vector3(0, -0.004f, 0), new Vector3(0.02f, 0.008f, vertical ? 0.035f : 0.075f)); // rail clamp
            if (vertical)
            {
                b.Cylinder(new Vector3(0, -0.006f, 0), new Vector3(0, -0.085f, -0.004f), 0.0135f, 0.0145f, 12);
                b.Sphere(new Vector3(0, -0.086f, -0.004f), new Vector3(0.015f, 0.006f, 0.015f), 4, 10);
                for (int i = 0; i < 4; i++) b.Torus(new Vector3(0, -0.025f - i * 0.016f, -0.002f), 0.0142f, 0.0012f, 12, 3, Vector3.up);
            }
            else
            {
                b.Push(new Vector3(0, -0.018f, -0.005f), Quaternion.Euler(-28f, 0, 0));
                b.BevelBox(Vector3.zero, new Vector3(0.022f, 0.022f, 0.07f), 0.006f);
                b.Pop();
                b.BevelBox(new Vector3(0, -0.032f, -0.035f), new Vector3(0.022f, 0.02f, 0.012f), 0.004f);
            }
            m.Piece(vertical ? "VerticalGrip" : "AngledGrip", b, Mats.Lit, under);
        }

        /// <summary>Laser module with a red emitter and a short visible beam; <paramref name="live"/> adds the world dot.</summary>
        public static void LaserModule(WeaponModel m, Vector3 pos, bool live)
        {
            var b = new MeshBuilder { Color = Tan, Gloss = 0.25f };
            b.BevelBox(Vector3.zero, new Vector3(0.016f, 0.016f, 0.045f), 0.003f);
            b.SetColor(Black, 0.3f);
            b.Box(new Vector3(pos.x >= 0 ? -0.009f : 0.009f, 0, 0), new Vector3(0.004f, 0.01f, 0.03f)); // clamp
            b.Box(new Vector3(0, 0.0085f, -0.01f), new Vector3(0.006f, 0.003f, 0.006f));               // switch
            var module = m.Piece("LaserModule", b, Mats.LitMetal, pos);
            var e = new MeshBuilder { Color = Color.white };
            e.Cylinder(new Vector3(0, -0.002f, 0.0225f), new Vector3(0, -0.002f, 0.024f), 0.0028f, 0.0028f, 10);
            m.Piece("LaserEmitter", e, Mats.Glow(DotRed, 4f), Vector3.zero, module);
            var beam = new MeshBuilder { Color = Color.white };
            beam.Box(new Vector3(0, -0.002f, 0.024f + 0.2f), new Vector3(0.0007f, 0.0007f, 0.4f));
            m.Piece("LaserBeam", beam, Mats.Glow(DotRed, 2f), Vector3.zero, module);
            if (live) module.gameObject.AddComponent<LaserDot>();
        }
    }

    /// <summary>World-space laser dot on whatever the local player is aiming at (shown while the gun is out).</summary>
    public sealed class LaserDot : MonoBehaviour
    {
        GameObject dot;

        void LateUpdate()
        {
            var p = PlayerController.Instance;
            if (p == null) { Hide(); return; }
            if (dot == null)
            {
                dot = new GameObject("LaserDot");
                var mb = new MeshBuilder { Color = Color.white };
                mb.Sphere(Vector3.zero, Vector3.one * 0.5f, 6, 8);
                dot.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("LaserDot");
                var mr = dot.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Mats.Glow(new Color(1f, 0.1f, 0.06f), 6f);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            var eye = p.EyePosition;
            var dir = p.AimDirection;
            if (Physics.Raycast(eye + dir * 0.3f, dir, out var hit, 80f, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                if (!dot.activeSelf) dot.SetActive(true);
                dot.transform.position = hit.point + hit.normal * 0.004f;
                dot.transform.localScale = Vector3.one * (0.012f + hit.distance * 0.0012f);
            }
            else Hide();
        }

        void Hide() { if (dot != null && dot.activeSelf) dot.SetActive(false); }
        void OnDisable() => Hide();
        void OnDestroy() { if (dot != null) Destroy(dot); }
    }
}
