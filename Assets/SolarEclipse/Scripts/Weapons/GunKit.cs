using UnityEngine;

namespace SolarEclipse
{
    public enum GunFrame { Pistol, Long }
    public enum GunStock { None, Wire, Fixed, Skeleton, Wood, Bullpup, Heavy }
    public enum GunMag { Grip, Stick, Curved, Drum, Box, Top, Tube, Shell }
    public enum GunGuard { None, Round, Wood, Rail, Shroud, Slim }

    /// <summary>
    /// Everything GunKit needs to assemble a gun. Model space: +Z forward, origin at the top of the pistol grip,
    /// metres. Bore = barrel axis height. Defaults describe an AR-style rifle.
    /// </summary>
    public sealed class GunSpec
    {
        public GunFrame Frame = GunFrame.Long;
        public float Bore = 0.055f;
        // Receiver (Long) / frame (Pistol)
        public float RecBack = -0.08f, RecFront = 0.17f, RecH = 0.06f, RecW = 0.028f;
        public float Muzzle = 0.6f, BarrelR = 0.009f;
        public GunGuard Guard = GunGuard.Round;
        public float GuardStart = 0.16f, GuardEnd = 0.42f, GuardR = 0.024f;
        public GunStock Stock = GunStock.Fixed;
        public float StockLen = 0.26f, StockDrop = 0.02f;
        public float GripZ = -0.02f, GripAngle = 18f;
        public GunMag Mag = GunMag.Stick;
        public float MagZ = 0.08f, MagLen = 0.15f, MagW = 0.022f, MagD = 0.06f, MagAngle = 10f;
        public bool Scope, Rail, Bipod, Brake, FlashHider, GasTube, DoubleBarrel, Foregrip, CarryHandle, Hammer, Lever, TopCharge, RibbedSlide;
        /// <summary>Hinged barrels that tip down while reloading (break action). The piece is called "Hinge".</summary>
        public bool BreakAction;
        public Color Body = new Color(0.11f, 0.11f, 0.12f), Furniture = new Color(0.11f, 0.11f, 0.12f), Metal = new Color(0.12f, 0.12f, 0.13f);
        public float BodyGloss = 0.3f;
        public bool MetalBody;
        // Pistol slide
        public float SlideLen = 0.186f, SlideH = 0.03f, SlideZ = 0.077f;
        /// <summary>Optional left-hand override (model local). Zero = computed.</summary>
        public Vector3 SupportPos;
        public Vector3 SupportRot = new Vector3(-15f, 0f, 165f);

        // ---- derived, used by the builder and the attachment mounts
        public float RecTop => Bore + (Frame == GunFrame.Pistol ? SlideH * 0.5f + 0.002f : 0.025f);
        public float RecBottom => RecTop - RecH;
        public Vector3 RailPos => Frame == GunFrame.Pistol ? new Vector3(0, RecTop + 0.0005f, SlideZ - SlideLen * 0.25f) : new Vector3(0, RecTop + (Rail ? 0.006f : 0f), Mathf.Lerp(RecBack, RecFront, 0.45f));
        public Vector3 UnderPos => Guard == GunGuard.None || Foregrip ? Vector3.zero : new Vector3(0, Bore - GuardR - 0.002f, Mathf.Lerp(GuardStart, GuardEnd, 0.6f));
        public Vector3 LaserPos => Frame == GunFrame.Pistol ? new Vector3(0, Bore - 0.05f, SlideZ + SlideLen * 0.3f)
            : new Vector3(GuardR + 0.009f, Bore, Guard == GunGuard.None ? RecFront - 0.03f : Mathf.Lerp(GuardStart, GuardEnd, 0.8f));
    }

    /// <summary>
    /// Parametric gun model builder: assembles receiver/slide, barrel, handguard, stock, grip, magazine, sights or scope,
    /// bipod and muzzle devices from a <see cref="GunSpec"/>, and sets every <see cref="WeaponModel"/> anchor
    /// (GripR, SupportL, Muzzle, Eject, MagWell, Sight, SightRear/Front, Slide/Charge, Mag + MagMesh).
    /// </summary>
    public static class GunKit
    {
        static readonly Color Steel = new Color(0.22f, 0.22f, 0.24f), Black = new Color(0.07f, 0.07f, 0.08f), Brass = new Color(0.8f, 0.62f, 0.25f);
        static readonly Color Tritium = new Color(0.45f, 1f, 0.35f);
        public static readonly Color Polymer = new Color(0.11f, 0.11f, 0.12f), DarkSteel = new Color(0.12f, 0.12f, 0.13f), Wood = new Color(0.45f, 0.28f, 0.15f),
            DarkWood = new Color(0.3f, 0.17f, 0.09f), Tan = new Color(0.62f, 0.52f, 0.36f), Olive = new Color(0.3f, 0.32f, 0.22f), Silver = new Color(0.62f, 0.63f, 0.66f),
            Gunmetal = new Color(0.2f, 0.21f, 0.23f);

        static Quaternion RightGrip(float pitch) => Quaternion.Euler(pitch, 0, -90f);

        public static WeaponModel Build(string name, GunSpec s, int layer)
        {
            var m = WeaponModels.Root(name, layer);
            if (s.Frame == GunFrame.Pistol) BuildPistol(m, s);
            else BuildLong(m, s);
            return m;
        }

        // ================================================================== pistols

        static void BuildPistol(WeaponModel m, GunSpec s)
        {
            float B = s.Bore, w = s.RecW;
            var frame = new MeshBuilder { Color = s.Body, Gloss = s.BodyGloss };
            frame.BevelBox(new Vector3(0, B - 0.025f, s.SlideZ - 0.007f), new Vector3(w, 0.024f, s.SlideLen * 0.89f), 0.005f);
            frame.SetColor(s.Furniture, 0.25f);
            frame.Push(new Vector3(0, B - 0.075f, s.GripZ), Quaternion.Euler(-s.GripAngle, 0, 0));
            frame.BevelBox(Vector3.zero, new Vector3(w + 0.001f, 0.105f, 0.05f), 0.008f);
            for (int i = 0; i < 4; i++) frame.Box(new Vector3(0, -0.03f + i * 0.018f, 0.0255f), new Vector3(w - 0.004f, 0.005f, 0.003f));
            frame.SetColor(s.Body, s.BodyGloss);
            frame.Box(new Vector3(0, -0.05f, 0.0f), new Vector3(w + 0.003f, 0.008f, 0.052f));
            frame.Pop();
            frame.Box(new Vector3(0, B - 0.051f, 0.05f), new Vector3(0.012f, 0.005f, 0.05f));
            frame.Box(new Vector3(0, B - 0.037f, 0.074f), new Vector3(0.012f, 0.03f, 0.006f));
            frame.SetColor(DarkSteel, 0.5f);
            frame.Box(new Vector3(0, B - 0.037f, 0.045f), new Vector3(0.006f, 0.022f, 0.006f));
            if (s.Hammer)
                frame.Box(new Vector3(0, B + 0.006f, s.SlideZ - s.SlideLen * 0.5f - 0.004f), new Vector3(0.008f, 0.016f, 0.01f));
            m.Piece("Frame", frame, s.MetalBody ? Mats.LitMetal : Mats.Lit, Vector3.zero);

            // Slide (moves back on every shot), with its own sights.
            float hl = s.SlideLen * 0.5f, top = s.SlideH * 0.5f;
            var slide = new MeshBuilder { Color = s.Metal, Gloss = 0.55f };
            slide.BevelBox(Vector3.zero, new Vector3(w - 0.0025f, s.SlideH, s.SlideLen), 0.004f);
            if (s.RibbedSlide) slide.Box(new Vector3(0, top + 0.002f, 0.01f), new Vector3(0.008f, 0.004f, s.SlideLen * 0.8f));
            slide.SetColor(s.Metal * 0.7f, 0.4f);
            for (int i = 0; i < 6; i++) slide.Box(new Vector3(0, 0, -hl + 0.016f + i * 0.006f), new Vector3(w - 0.0015f, s.SlideH * 0.72f, 0.002f));
            slide.Box(new Vector3((w - 0.0025f) * 0.5f, 0.004f, 0.01f), new Vector3(0.002f, 0.012f, 0.05f));
            float sightTop = top + 0.0055f;
            slide.Box(new Vector3(0, top + 0.0025f, hl - 0.007f), new Vector3(0.004f, 0.006f, 0.006f));
            slide.Box(new Vector3(-0.007f, top + 0.0025f, -hl + 0.01f), new Vector3(0.006f, 0.006f, 0.006f));
            slide.Box(new Vector3(0.007f, top + 0.0025f, -hl + 0.01f), new Vector3(0.006f, 0.006f, 0.006f));
            slide.SetColor(Color.black, 0.3f);
            slide.Cylinder(new Vector3(0, -0.002f, hl), new Vector3(0, -0.002f, hl + 0.0008f), s.BarrelR * 0.65f, s.BarrelR * 0.65f, 10);
            m.Slide = m.Piece("Slide", slide, Mats.LitMetal, new Vector3(0, B + 0.002f, s.SlideZ));
            m.SlideRest = m.Slide.localPosition;
            WeaponModels.SightDot(m, new Vector3(0, top + 0.004f, hl - 0.0102f), Tritium, 0.00095f, 3f, m.Slide);
            m.SightRear = m.Anchor("SightRear", new Vector3(0, B + 0.002f + sightTop, s.SlideZ - hl + 0.01f), Quaternion.identity);
            m.SightFront = m.Anchor("SightFront", new Vector3(0, B + 0.002f + sightTop, s.SlideZ + hl - 0.007f), Quaternion.identity);

            // Magazine in the grip (longer ones stick out of the bottom).
            var mag = new MeshBuilder { Color = DarkSteel, Gloss = 0.35f };
            float ml = Mathf.Max(0.09f, s.MagLen);
            mag.Box(new Vector3(0, 0.015f - ml * 0.5f, 0), new Vector3(w - 0.005f, ml, 0.034f));
            mag.SetColor(s.Furniture, 0.2f);
            mag.Box(new Vector3(0, 0.015f - ml - 0.003f, 0.003f), new Vector3(w + 0.001f, 0.008f, 0.042f));
            mag.SetColor(Brass, 0.7f);
            mag.Cylinder(new Vector3(0, 0.016f, -0.012f), new Vector3(0, 0.016f, 0.008f), 0.0045f, 0.0045f, 8);
            m.MagMesh = mag.ToMesh(m.name + "Mag");
            m.Mag = m.Piece("Mag", mag, Mats.LitMetal, new Vector3(0, B - 0.065f, s.GripZ + 0.003f));
            m.Mag.localRotation = Quaternion.Euler(-s.GripAngle, 0, 0);
            m.MagRest = m.Mag.localPosition;

            m.GripR = m.Anchor("GripR", new Vector3(0.024f, B - 0.1f, s.GripZ - 0.057f), RightGrip(s.GripAngle - 4f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.03f, B - 0.125f, s.GripZ - 0.015f), Quaternion.Euler(30f, 20f, 65f));
            m.Muzzle = m.Anchor("Muzzle", new Vector3(0, B, s.SlideZ + hl + 0.005f), Quaternion.identity);
            m.Eject = m.Anchor("Eject", new Vector3(0.016f, B + 0.01f, s.SlideZ + 0.008f), Quaternion.identity);
            m.MagWell = m.Anchor("MagWell", new Vector3(0, B - 0.13f - (ml - 0.09f), s.GripZ - 0.015f), Quaternion.identity);
            m.Sight = m.Anchor("Sight", new Vector3(0, B + 0.002f + sightTop, 0), Quaternion.identity);
            m.Charge = m.Anchor("ChargePoint", new Vector3(-0.005f, B + 0.005f, s.SlideZ - hl * 0.8f), Quaternion.Euler(0, 0, 90));
        }

        // ================================================================== long guns (SMGs, rifles, shotguns, LMGs)

        static void BuildLong(WeaponModel m, GunSpec s)
        {
            float B = s.Bore, top = s.RecTop, bot = s.RecBottom, w = s.RecW;
            float recLen = s.RecFront - s.RecBack, recZ = (s.RecFront + s.RecBack) * 0.5f;
            var body = new MeshBuilder { Color = s.Body, Gloss = s.BodyGloss };

            // ---- receiver
            body.BevelBox(new Vector3(0, (top + bot) * 0.5f, recZ), new Vector3(w, s.RecH, recLen), Mathf.Min(0.006f, s.RecH * 0.15f));
            body.SetColor(s.Body * 0.75f, s.BodyGloss);
            body.Box(new Vector3(w * 0.5f + 0.0005f, B + 0.004f, s.RecFront - recLen * 0.45f), new Vector3(0.002f, 0.014f, Mathf.Min(0.06f, recLen * 0.3f))); // ejection port
            if (s.Rail || s.Scope)
            {
                body.SetColor(DarkSteel, 0.4f);
                float r0 = s.RecBack + 0.01f, r1 = s.RecFront - 0.005f;
                body.Box(new Vector3(0, top + 0.003f, (r0 + r1) * 0.5f), new Vector3(0.021f, 0.006f, r1 - r0));
                int n = Mathf.Max(3, (int)((r1 - r0) / 0.012f));
                for (int i = 0; i < n; i++) body.Box(new Vector3(0, top + 0.007f, r0 + 0.006f + i * 0.012f), new Vector3(0.021f, 0.003f, 0.006f));
            }
            if (s.CarryHandle)
            {
                body.SetColor(s.Body, s.BodyGloss);
                float h0 = s.RecBack + 0.03f, h1 = s.RecFront - 0.01f;
                body.Box(new Vector3(0, top + 0.034f, (h0 + h1) * 0.5f), new Vector3(0.016f, 0.01f, h1 - h0));
                body.Box(new Vector3(0, top + 0.016f, h0 + 0.01f), new Vector3(0.014f, 0.03f, 0.02f));
                body.Box(new Vector3(0, top + 0.016f, h1 - 0.01f), new Vector3(0.014f, 0.03f, 0.02f));
            }

            // ---- barrel + muzzle device
            body.SetColor(s.Metal, 0.5f);
            Transform hinge = null;
            var barrel = s.BreakAction ? new MeshBuilder { Color = s.Metal, Gloss = 0.55f } : body;
            float bz0 = s.BreakAction ? 0f : s.RecFront - 0.01f, bz1 = s.Muzzle - (s.BreakAction ? s.RecFront : 0f);
            float by = s.BreakAction ? 0f : B;
            if (s.DoubleBarrel)
            {
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    barrel.Cylinder(new Vector3(sx * s.BarrelR, by, bz0), new Vector3(sx * s.BarrelR, by, bz1), s.BarrelR, s.BarrelR * 0.95f, 12);
                    barrel.SetColor(Color.black, 0.2f);
                    barrel.Cylinder(new Vector3(sx * s.BarrelR, by, bz1), new Vector3(sx * s.BarrelR, by, bz1 + 0.0008f), s.BarrelR * 0.75f, s.BarrelR * 0.75f, 10);
                    barrel.SetColor(s.Metal, 0.55f);
                }
                barrel.Box(new Vector3(0, by + s.BarrelR, (bz0 + bz1) * 0.5f), new Vector3(0.006f, 0.004f, bz1 - bz0)); // rib
            }
            else
            {
                barrel.Cylinder(new Vector3(0, by, bz0), new Vector3(0, by, bz1), s.BarrelR, s.BarrelR * 0.92f, 12);
                if (s.Brake)
                {
                    barrel.BevelBox(new Vector3(0, by, bz1 - 0.045f), new Vector3(s.BarrelR * 4.2f, s.BarrelR * 2.6f, 0.09f), 0.004f);
                    barrel.SetColor(Black, 0.2f);
                    for (int i = 0; i < 3; i++) barrel.Box(new Vector3(0, by, bz1 - 0.075f + i * 0.026f), new Vector3(s.BarrelR * 4.4f, s.BarrelR * 1.6f, 0.01f));
                    barrel.SetColor(s.Metal, 0.5f);
                }
                else if (s.FlashHider)
                {
                    barrel.Cylinder(new Vector3(0, by, bz1 - 0.05f), new Vector3(0, by, bz1), s.BarrelR * 1.35f, s.BarrelR * 1.35f, 10);
                    barrel.SetColor(Black, 0.2f);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * 90f + 45f;
                        var off = Quaternion.Euler(0, 0, a) * Vector3.up * s.BarrelR * 1.3f;
                        barrel.Box(new Vector3(off.x, by + off.y, bz1 - 0.018f), new Vector3(0.003f, 0.003f, 0.03f));
                    }
                    barrel.SetColor(s.Metal, 0.5f);
                }
                barrel.SetColor(Color.black, 0.2f);
                barrel.Cylinder(new Vector3(0, by, bz1), new Vector3(0, by, bz1 + 0.0008f), s.BarrelR * 0.6f, s.BarrelR * 0.6f, 10);
                barrel.SetColor(s.Metal, 0.5f);
            }
            if (s.GasTube) body.Cylinder(new Vector3(0, B + s.BarrelR + 0.008f, s.RecFront - 0.02f), new Vector3(0, B + s.BarrelR + 0.008f, s.GuardEnd + 0.04f), 0.007f, 0.007f, 8);

            // ---- handguard / forend
            float g0 = s.GuardStart, g1 = s.GuardEnd, gr = s.GuardR, gm = (g0 + g1) * 0.5f;
            if (s.BreakAction) { g0 -= s.RecFront; g1 -= s.RecFront; gm -= s.RecFront; }
            var gb = s.BreakAction ? barrel : body;
            float gy = s.BreakAction ? 0f : B;
            switch (s.Guard)
            {
                case GunGuard.Round:
                    gb.SetColor(s.Furniture, 0.2f);
                    gb.Loft(2, 10, i => (new Vector3(0, gy, i == 0 ? g0 : g1), gr, gr), Vector3.forward);
                    for (float z = g0 + 0.02f; z < g1 - 0.01f; z += 0.032f) gb.Box(new Vector3(0, gy, z), new Vector3(gr * 2.08f, 0.008f, 0.012f));
                    break;
                case GunGuard.Slim:
                    gb.SetColor(s.Furniture, 0.25f);
                    gb.BevelBox(new Vector3(0, gy - gr * 0.25f, gm), new Vector3(gr * 1.6f, gr * 1.5f, g1 - g0), gr * 0.4f);
                    break;
                case GunGuard.Wood:
                    gb.SetColor(s.Furniture, 0.35f);
                    gb.BevelBox(new Vector3(0, gy - gr * 0.45f, gm), new Vector3(gr * 1.7f, gr * 1.5f, g1 - g0), gr * 0.45f);
                    break;
                case GunGuard.Rail:
                    gb.SetColor(s.Furniture, 0.3f);
                    gb.BevelBox(new Vector3(0, gy, gm), new Vector3(gr * 2f, gr * 2f, g1 - g0), 0.004f);
                    gb.SetColor(DarkSteel, 0.4f);
                    for (float z = g0 + 0.01f; z < g1 - 0.006f; z += 0.012f)
                    {
                        gb.Box(new Vector3(0, gy + gr + 0.002f, z), new Vector3(0.02f, 0.004f, 0.006f));
                        gb.Box(new Vector3(0, gy - gr - 0.002f, z), new Vector3(0.02f, 0.004f, 0.006f));
                        gb.Box(new Vector3(gr + 0.002f, gy, z), new Vector3(0.004f, 0.02f, 0.006f));
                        gb.Box(new Vector3(-gr - 0.002f, gy, z), new Vector3(0.004f, 0.02f, 0.006f));
                    }
                    break;
                case GunGuard.Shroud:
                    gb.SetColor(s.Furniture, 0.35f);
                    gb.Cylinder(new Vector3(0, gy, g0), new Vector3(0, gy, g1), gr, gr, 12);
                    gb.SetColor(Black, 0.1f);
                    for (float z = g0 + 0.015f; z < g1 - 0.01f; z += 0.022f)
                        for (int k = 0; k < 4; k++)
                        {
                            var off = Quaternion.Euler(0, 0, k * 90f + 45f) * Vector3.up * gr;
                            gb.Box(new Vector3(off.x, gy + off.y, z), new Vector3(0.006f, 0.006f, 0.008f));
                        }
                    break;
            }
            if (s.Mag == GunMag.Tube && !s.BreakAction)
            {
                body.SetColor(s.Metal, 0.5f);
                body.Cylinder(new Vector3(0, B - s.BarrelR - 0.011f, s.RecFront - 0.01f), new Vector3(0, B - s.BarrelR - 0.011f, s.Muzzle - 0.04f), 0.01f, 0.01f, 10);
            }
            if (s.Foregrip)
            {
                body.SetColor(s.Furniture, 0.3f);
                body.Push(new Vector3(0, B - gr - 0.045f, Mathf.Lerp(g0, g1, 0.55f)), Quaternion.Euler(8, 0, 0));
                body.BevelBox(Vector3.zero, new Vector3(0.026f, 0.08f, 0.032f), 0.009f);
                body.Pop();
            }
            if (s.Bipod)
            {
                body.SetColor(Black, 0.35f);
                float bzp = s.BreakAction ? s.GuardEnd : Mathf.Max(s.GuardEnd, s.RecFront) + 0.04f;
                body.Box(new Vector3(0, B - s.BarrelR - 0.008f, bzp), new Vector3(0.03f, 0.012f, 0.025f));
                for (int sx = -1; sx <= 1; sx += 2)
                    body.Cylinder(new Vector3(sx * 0.011f, B - s.BarrelR - 0.012f, bzp), new Vector3(sx * 0.013f, B - s.BarrelR - 0.018f, bzp + 0.2f), 0.0045f, 0.004f, 6);
            }

            // ---- pistol grip + trigger guard
            body.SetColor(s.Furniture, 0.22f);
            body.Push(new Vector3(0, bot - 0.042f, s.GripZ), Quaternion.Euler(-s.GripAngle, 0, 0));
            body.BevelBox(Vector3.zero, new Vector3(0.028f, 0.095f, 0.042f), 0.008f);
            body.SetColor(s.Furniture * 0.8f, 0.2f);
            for (int i = 0; i < 3; i++) body.Box(new Vector3(0, -0.025f + i * 0.02f, 0.021f), new Vector3(0.024f, 0.006f, 0.004f));
            body.Pop();
            body.SetColor(s.Body, s.BodyGloss);
            if (s.Lever)
            {
                // Lever loop is its own moving piece (below); just the trigger here.
            }
            else
            {
                body.Box(new Vector3(0, bot - 0.028f, s.GripZ + 0.05f), new Vector3(0.012f, 0.005f, 0.06f));
                body.Box(new Vector3(0, bot - 0.014f, s.GripZ + 0.079f), new Vector3(0.012f, 0.03f, 0.005f));
            }
            body.SetColor(DarkSteel, 0.5f);
            body.Box(new Vector3(0, bot - 0.012f, s.GripZ + 0.04f), new Vector3(0.006f, 0.022f, 0.006f));

            // ---- stock
            float sb = s.RecBack, sl = s.StockLen, sy = Mathf.Min(B, (top + bot) * 0.5f);
            switch (s.Stock)
            {
                case GunStock.Wire:
                    body.SetColor(Steel, 0.6f);
                    for (int sx = -1; sx <= 1; sx += 2)
                        body.Cylinder(new Vector3(sx * 0.014f, sy, sb), new Vector3(sx * 0.014f, sy - s.StockDrop, sb - sl), 0.004f, 0.004f, 6);
                    body.Box(new Vector3(0, sy - s.StockDrop - 0.012f, sb - sl - 0.004f), new Vector3(0.036f, 0.05f, 0.008f));
                    break;
                case GunStock.Fixed:
                    body.SetColor(s.Furniture, 0.2f);
                    body.Cylinder(new Vector3(0, B, sb), new Vector3(0, B, sb - sl * 0.55f), 0.016f, 0.016f, 10);
                    body.BevelBox(new Vector3(0, B - 0.018f - s.StockDrop * 0.5f, sb - sl * 0.7f), new Vector3(0.04f, 0.08f, sl * 0.62f), 0.01f);
                    body.SetColor(Black, 0.1f);
                    body.Box(new Vector3(0, B - 0.018f - s.StockDrop * 0.5f, sb - sl - 0.004f), new Vector3(0.042f, 0.084f, 0.01f));
                    break;
                case GunStock.Skeleton:
                    body.SetColor(s.Furniture, 0.2f);
                    body.Box(new Vector3(0, top - 0.008f, sb - sl * 0.5f), new Vector3(0.02f, 0.014f, sl));
                    body.Box(new Vector3(0, bot + 0.012f - s.StockDrop, sb - sl * 0.55f), new Vector3(0.02f, 0.014f, sl * 0.9f));
                    body.BevelBox(new Vector3(0, (top + bot) * 0.5f - s.StockDrop * 0.5f, sb - sl), new Vector3(0.036f, s.RecH + 0.02f + s.StockDrop, 0.03f), 0.006f);
                    break;
                case GunStock.Wood:
                {
                    body.SetColor(s.Furniture, 0.35f);
                    float d = s.StockDrop;
                    body.Loft(6, 12, i =>
                    {
                        float t = i / 5f;
                        return (new Vector3(0, (top + bot) * 0.5f - 0.004f - t * (d + 0.02f), sb + 0.01f - t * sl), 0.018f + t * 0.004f, 0.022f + t * 0.032f);
                    }, Vector3.back, true, true);
                    break;
                }
                case GunStock.Heavy:
                    body.SetColor(s.Furniture, 0.25f);
                    body.BevelBox(new Vector3(0, (top + bot) * 0.5f - s.StockDrop * 0.5f, sb - sl * 0.5f), new Vector3(0.036f, s.RecH * 0.9f + s.StockDrop, sl), 0.008f);
                    body.BevelBox(new Vector3(0, bot - 0.004f - s.StockDrop, sb - sl * 0.7f), new Vector3(0.03f, 0.03f, sl * 0.35f), 0.008f);
                    body.SetColor(Black, 0.1f);
                    body.Box(new Vector3(0, (top + bot) * 0.5f - s.StockDrop * 0.5f, sb - sl - 0.006f), new Vector3(0.04f, s.RecH + s.StockDrop + 0.01f, 0.014f));
                    break;
                case GunStock.Bullpup:
                    body.SetColor(Black, 0.1f);
                    body.Box(new Vector3(0, (top + bot) * 0.5f, sb - 0.006f), new Vector3(w + 0.004f, s.RecH + 0.004f, 0.012f));
                    break;
            }

            // ---- sights or scope
            if (s.Scope)
            {
                float sy2 = top + (s.Rail ? 0.009f : 0f) + 0.032f, s0 = s.RecBack + 0.02f, s1 = s.RecFront + 0.03f;
                var sc = new MeshBuilder { Color = Black, Gloss = 0.5f };
                sc.Cylinder(new Vector3(0, sy2, s0 + 0.05f), new Vector3(0, sy2, s1 - 0.06f), 0.0135f, 0.0135f, 14);
                sc.Cylinder(new Vector3(0, sy2, s0), new Vector3(0, sy2, s0 + 0.05f), 0.019f, 0.0135f, 14);
                sc.Cylinder(new Vector3(0, sy2, s1 - 0.06f), new Vector3(0, sy2, s1), 0.0135f, 0.024f, 14);
                sc.Cylinder(new Vector3(0, sy2 + 0.013f, (s0 + s1) * 0.5f - 0.01f), new Vector3(0, sy2 + 0.025f, (s0 + s1) * 0.5f - 0.01f), 0.007f, 0.007f, 8);
                sc.Cylinder(new Vector3(0.013f, sy2, (s0 + s1) * 0.5f - 0.01f), new Vector3(0.025f, sy2, (s0 + s1) * 0.5f - 0.01f), 0.007f, 0.007f, 8);
                for (int k = 0; k < 2; k++)
                {
                    float z = Mathf.Lerp(s0 + 0.06f, s1 - 0.07f, k);
                    sc.Box(new Vector3(0, (sy2 + top) * 0.5f, z), new Vector3(0.02f, sy2 - top, 0.016f));
                }
                sc.SetColor(new Color(0.25f, 0.4f, 0.55f), 1f);
                sc.Cylinder(new Vector3(0, sy2, s1 - 0.001f), new Vector3(0, sy2, s1), 0.021f, 0.021f, 14);
                m.Piece("Scope", sc, Mats.LitMetal, Vector3.zero);
                m.SightRear = m.Anchor("SightRear", new Vector3(0, sy2, s0), Quaternion.identity);
                m.SightFront = m.Anchor("SightFront", new Vector3(0, sy2, s1), Quaternion.identity);
                m.Sight = m.SightRear;
            }
            else
            {
                float baseRear = top + (s.Rail ? 0.009f : 0f) + (s.CarryHandle ? 0.039f : 0f);
                float sightY = baseRear + 0.012f;
                float rearZ = s.CarryHandle ? s.RecBack + 0.04f : (s.Stock == GunStock.Bullpup ? s.RecBack + recLen * 0.45f : s.RecBack + 0.03f);
                float frontZ = s.DoubleBarrel || s.BreakAction ? s.Muzzle - 0.012f : Mathf.Max(s.GuardEnd, s.RecFront) + 0.02f;
                if (frontZ > s.Muzzle - 0.01f) frontZ = s.Muzzle - 0.01f;
                float frontBase = s.DoubleBarrel || s.BreakAction ? B + s.BarrelR + 0.002f : B + s.BarrelR;
                if (s.Guard != GunGuard.None && frontZ <= s.GuardEnd + 0.001f && frontZ >= s.GuardStart) frontBase = B + s.GuardR;
                if (frontZ < s.RecFront) frontBase = top;
                if (s.DoubleBarrel || s.BreakAction)
                {
                    // Shotgun bead + notch on the tang.
                    WeaponModels.SightDot(m, new Vector3(0, sightY - 0.0012f, frontZ), new Color(1f, 0.85f, 0.4f), 0.0022f, 1.6f);
                    var notch = new MeshBuilder { Color = s.Metal, Gloss = 0.4f };
                    notch.Box(new Vector3(0, (sightY + top) * 0.5f - 0.001f, rearZ), new Vector3(0.012f, sightY - top, 0.006f));
                    m.Piece("IronSights", notch, Mats.LitMetal, Vector3.zero);
                    var bead = new MeshBuilder { Color = s.Metal, Gloss = 0.4f };
                    bead.Box(new Vector3(0, (sightY + frontBase) * 0.5f - 0.002f, frontZ), new Vector3(0.004f, sightY - frontBase - 0.002f, 0.004f));
                    m.Piece("FrontPost", bead, Mats.LitMetal, Vector3.zero);
                    m.SightRear = m.Anchor("SightRear", new Vector3(0, sightY, rearZ), Quaternion.identity);
                    m.SightFront = m.Anchor("SightFront", new Vector3(0, sightY, frontZ), Quaternion.identity);
                }
                else WeaponModels.IronSights(m, new Vector3(0, sightY, rearZ), baseRear, new Vector3(0, sightY, frontZ), frontBase, DarkSteel, Tritium, s.Frame == GunFrame.Long && s.Stock != GunStock.None && s.Stock != GunStock.Wire, true);
                m.Sight = m.SightRear;
            }

            m.Piece("Body", body, s.MetalBody ? Mats.LitMetal : Mats.Lit, Vector3.zero);
            if (s.BreakAction)
            {
                hinge = m.Piece("Hinge", barrel, Mats.LitMetal, new Vector3(0, B, s.RecFront));
            }

            // ---- charging handle / bolt / lever (moving part)
            if (s.Lever)
            {
                var lv = new MeshBuilder { Color = s.Metal, Gloss = 0.6f };
                lv.Box(new Vector3(0, -0.006f, -0.03f), new Vector3(0.008f, 0.006f, 0.07f));
                lv.Box(new Vector3(0, -0.03f, -0.066f), new Vector3(0.008f, 0.05f, 0.006f));
                lv.Box(new Vector3(0, -0.055f, -0.035f), new Vector3(0.008f, 0.006f, 0.066f));
                lv.Box(new Vector3(0, -0.03f, -0.003f), new Vector3(0.008f, 0.05f, 0.006f));
                m.Charge = m.Piece("Lever", lv, Mats.LitMetal, new Vector3(0, bot, s.GripZ + 0.08f));
                m.ChargeRest = m.Charge.localPosition;
            }
            else if (!s.BreakAction)
            {
                var ch = new MeshBuilder { Color = DarkSteel, Gloss = 0.45f };
                if (s.TopCharge) ch.Box(Vector3.zero, new Vector3(0.03f, 0.01f, 0.015f));
                else { ch.Box(Vector3.zero, new Vector3(0.014f, 0.008f, 0.012f)); ch.Sphere(new Vector3(0.008f, 0, 0), Vector3.one * 0.005f, 4, 6); }
                var cp = s.TopCharge ? new Vector3(0, top + 0.002f, s.RecBack + 0.015f) : new Vector3(w * 0.5f + 0.006f, B + 0.006f, Mathf.Lerp(s.RecBack, s.RecFront, 0.62f));
                m.Charge = m.Piece("ChargingHandle", ch, Mats.LitMetal, cp);
                m.ChargeRest = m.Charge.localPosition;
            }

            // ---- anchors
            float gripCy = bot - 0.042f;
            m.GripR = m.Anchor("GripR", new Vector3(0.024f, gripCy - 0.02f, s.GripZ - 0.06f), RightGrip(s.GripAngle));
            Vector3 sup;
            Quaternion supRot = Quaternion.Euler(s.SupportRot);
            if (s.SupportPos != Vector3.zero) sup = s.SupportPos;
            else if (s.Foregrip)
            {
                var gc = new Vector3(0, B - s.GuardR - 0.045f, Mathf.Lerp(s.GuardStart, s.GuardEnd, 0.55f));
                sup = gc + new Vector3(-0.024f, -0.02f, -0.055f);
                supRot = Quaternion.Euler(-2f, 0f, 90f);
            }
            else if (s.Guard != GunGuard.None) sup = new Vector3(-0.004f, B - s.GuardR - 0.018f, Mathf.Lerp(s.GuardStart, s.GuardEnd, 0.42f));
            else sup = new Vector3(-0.005f, bot - 0.04f, s.MagZ);
            m.SupportL = m.Anchor("SupportL", sup, supRot);
            m.Muzzle = m.Anchor("Muzzle", new Vector3(0, B, s.Muzzle + 0.005f), Quaternion.identity);
            if (hinge != null) m.Muzzle.SetParent(hinge, true);
            m.Eject = m.Anchor("Eject", new Vector3(w * 0.5f + 0.004f, B + 0.004f, s.RecFront - recLen * 0.45f), Quaternion.identity);
            if (s.Mag == GunMag.Tube) m.MagWell = m.Anchor("LoadPort", new Vector3(0.01f, bot + 0.01f, s.RecFront - 0.05f), Quaternion.identity);
            else if (s.Mag == GunMag.Shell) m.MagWell = m.Anchor("LoadPort", new Vector3(0.01f, B + 0.01f, s.RecFront + 0.02f), Quaternion.identity);
            BuildMag(m, s);
            // Break action: the bead travels with the barrels.
            if (hinge != null) foreach (var n in new[] { "FrontPost", "SightDot" }) { var t = m.transform.Find(n); if (t != null) t.SetParent(hinge, true); }
        }

        static void BuildMag(WeaponModel m, GunSpec s)
        {
            float bot = s.RecBottom;
            if (s.Mag == GunMag.Tube || s.Mag == GunMag.Shell)
            {
                // Loaded one shell at a time: a hidden shell that rides in the hand.
                var shell = new MeshBuilder { Color = new Color(0.75f, 0.12f, 0.1f), Gloss = 0.4f };
                shell.Cylinder(new Vector3(0, 0, -0.03f), new Vector3(0, 0, 0.02f), 0.0105f, 0.0105f, 10);
                shell.SetColor(Brass, 0.8f);
                shell.Cylinder(new Vector3(0, 0, -0.04f), new Vector3(0, 0, -0.03f), 0.011f, 0.011f, 10);
                m.MagMesh = shell.ToMesh("Shell");
                m.Mag = m.Piece("Shell", shell, Mats.Lit, new Vector3(0.03f, s.Bore, s.RecFront - 0.05f));
                m.MagRest = m.Mag.localPosition;
                m.Mag.gameObject.SetActive(false);
                if (s.Mag == GunMag.Tube && s.Lever) m.Eject.localPosition = new Vector3(s.RecW * 0.5f + 0.004f, s.Bore - 0.01f, s.RecFront - 0.05f);
                if (s.Mag == GunMag.Shell) m.Eject.localPosition = new Vector3(0.012f, s.Bore + 0.01f, s.RecFront + 0.02f);
                return;
            }
            var mag = new MeshBuilder { Color = s.Mag == GunMag.Top ? new Color(0.35f, 0.32f, 0.22f) : DarkSteel, Gloss = 0.35f };
            Vector3 pos = new Vector3(0, bot + 0.012f, s.MagZ);
            Quaternion rot = Quaternion.identity;
            float L = s.MagLen, W = s.MagW, D = s.MagD;
            Vector3 well;
            switch (s.Mag)
            {
                case GunMag.Curved:
                {
                    // Banana: segments bending forward as they go down.
                    const int n = 6;
                    float seg = L / n;
                    var p = Vector3.zero;
                    float ang = s.MagAngle;
                    for (int i = 0; i < n; i++)
                    {
                        var dir = Quaternion.Euler(ang, 0, 0) * Vector3.down;
                        mag.Push(p + dir * seg * 0.5f, Quaternion.Euler(ang, 0, 0));
                        mag.Box(Vector3.zero, new Vector3(W, seg + 0.002f, D));
                        mag.Box(new Vector3(0, 0, -D * 0.5f), new Vector3(W + 0.002f, seg * 0.6f, 0.004f));
                        mag.Pop();
                        p += dir * seg;
                        ang -= 7f;
                    }
                    mag.Push(p, Quaternion.Euler(ang + 7f, 0, 0));
                    mag.Box(new Vector3(0, -0.004f, 0), new Vector3(W + 0.004f, 0.008f, D + 0.006f));
                    mag.Pop();
                    well = pos + p;
                    break;
                }
                case GunMag.Drum:
                {
                    mag.Box(new Vector3(0, -0.02f, 0), new Vector3(W, 0.045f, D));
                    float r = Mathf.Max(0.045f, L * 0.5f);
                    mag.SetColor(DarkSteel * 1.2f, 0.45f);
                    mag.Cylinder(new Vector3(-0.022f, -0.04f - r, 0), new Vector3(0.022f, -0.04f - r, 0), r, r, 20);
                    mag.SetColor(Steel, 0.6f);
                    mag.Cylinder(new Vector3(0.022f, -0.04f - r, 0), new Vector3(0.026f, -0.04f - r, 0), r * 0.4f, r * 0.4f, 12);
                    well = pos + new Vector3(0, -0.04f - 2f * r, 0);
                    break;
                }
                case GunMag.Box:
                {
                    // LMG ammo box hanging off the left side, belt feeding in.
                    mag.SetColor(Olive, 0.3f);
                    mag.BevelBox(new Vector3(-0.03f, -L * 0.5f - 0.01f, 0), new Vector3(0.07f, L, D), 0.006f);
                    mag.SetColor(Brass, 0.75f);
                    for (int i = 0; i < 5; i++) mag.Cylinder(new Vector3(-0.004f, 0.004f - i * 0.004f, -0.02f + i * 0.0f), new Vector3(-0.004f, 0.004f - i * 0.004f, 0.02f), 0.004f, 0.004f, 6);
                    well = pos + new Vector3(-0.03f, -L - 0.02f, 0);
                    break;
                }
                case GunMag.Top:
                {
                    // P90-style: translucent box lying on top of the receiver.
                    pos = new Vector3(0, s.RecTop + 0.002f, s.MagZ);
                    mag.BevelBox(new Vector3(0, 0.012f, 0), new Vector3(W, 0.024f, L), 0.005f);
                    mag.SetColor(Brass, 0.7f);
                    mag.Box(new Vector3(0, 0.012f, 0), new Vector3(W * 0.6f, 0.012f, L * 0.92f));
                    well = pos + new Vector3(0, 0.06f, 0);
                    break;
                }
                default: // Stick
                {
                    rot = Quaternion.Euler(s.MagAngle, 0, 0);
                    mag.Push(Vector3.zero, rot);
                    mag.Box(new Vector3(0, -L * 0.5f, 0), new Vector3(W, L, D));
                    mag.Box(new Vector3(0, -L - 0.004f, 0.003f), new Vector3(W + 0.004f, 0.008f, D + 0.006f));
                    mag.Pop();
                    rot = Quaternion.identity;
                    well = pos + Quaternion.Euler(s.MagAngle, 0, 0) * new Vector3(0, -L - 0.02f, 0);
                    break;
                }
            }
            mag.SetColor(Brass, 0.7f);
            if (s.Mag != GunMag.Top && s.Mag != GunMag.Box) mag.Cylinder(new Vector3(0, 0.005f, -D * 0.3f), new Vector3(0, 0.005f, D * 0.3f), Mathf.Min(W * 0.22f, 0.0055f), Mathf.Min(W * 0.22f, 0.0055f), 8);
            m.MagMesh = mag.ToMesh(m.name + "Mag");
            m.Mag = m.Piece("Mag", mag, Mats.LitMetal, pos);
            m.Mag.localRotation = rot;
            m.MagRest = m.Mag.localPosition;
            m.MagWell = m.Anchor("MagWell", well, Quaternion.identity);
        }
    }
}
