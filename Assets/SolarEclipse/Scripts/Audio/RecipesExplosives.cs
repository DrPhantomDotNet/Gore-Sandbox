using System;
using static SolarEclipse.Audio.SynthKit;

namespace SolarEclipse.Audio
{
    /// <summary>Rocket launcher, frag grenade, C4, katana and sledgehammer sounds.</summary>
    public static class RecipesExplosives
    {
        static float[] Mix(int rate, float length, params (float time, float[] sound, float gain)[] parts)
        {
            var b = Buf(length, rate);
            foreach (var p in parts) Add(b, p.sound, (int)(p.time * rate), p.gain);
            Normalize(b, 0.85f);
            return b;
        }

        /// <summary>Rocket leaving the tube: a hard pop, a roaring hiss and a low backblast boom.</summary>
        public static float[] RocketFire(FastRng r, int rate)
        {
            var b = Buf(1.4f, rate);
            var hiss = Biquad.BandPass(rate, 1800, 0.6f);
            var roar = Biquad.LowPass(rate, 260);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float n = r.Bipolar();
                float pop = n * Env.Exp(t, 0.012f) * 1.4f;
                float h = hiss.Process(n) * Env.AR(t, 0.01f, 0.25f, 0.9f) * 1.2f;
                float boom = roar.Process(r.Bipolar()) * Env.Exp(t, 0.35f) * 4f + Sin(t * 48f) * Env.Exp(t, 0.18f) * 0.8f;
                b[i] = pop + h + boom;
            }
            Drive(b, 2.2f);
            b = WithReverb(b, rate, 0.75f, 0.4f, 0.8f);
            Normalize(b, 0.9f);
            return b;
        }

        /// <summary>Seamless rocket motor loop (filtered noise with a slight flutter).</summary>
        public static float[] RocketLoop(FastRng r, int rate)
        {
            var b = Buf(1f, rate);
            var bp = Biquad.BandPass(rate, 1100, 0.5f);
            var lp = Biquad.LowPass(rate, 300);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float flutter = 0.8f + 0.2f * Sin(t * 31f);
                b[i] = (bp.Process(r.Bipolar()) * 1.1f + lp.Process(r.Bipolar()) * 2f) * flutter;
            }
            // Cross-fade the ends so the loop has no click.
            int fade = rate / 20;
            for (int i = 0; i < fade; i++) { float k = i / (float)fade; b[i] = b[i] * k + b[b.Length - fade + i] * (1f - k); }
            Array.Resize(ref b, b.Length - fade);
            Normalize(b, 0.7f);
            return b;
        }

        public static float[] RocketReload(FastRng r, int rate) => Mix(rate, 0.8f,
            (0f, RecipesWeapons.Whoosh(r, rate, 0.3f, 400, 1200), 0.4f),
            (0.25f, RecipesWeapons.Thud(r, rate, 0.5f, 0.2f), 0.6f),
            (0.3f, RecipesWeapons.Click(r, rate, 0.6f, 0.9f), 0.9f),
            (0.45f, RecipesWeapons.Click(r, rate, 1.1f, 0.3f), 0.6f));

        /// <summary>Pin pull: a metallic ping of the ring plus a scrape.</summary>
        public static float[] GrenadePin(FastRng r, int rate)
        {
            var ping = Modal(r, rate, 0.4f, new[] { 3100f, 5200f, 7400f }, new[] { 0.12f, 0.08f, 0.05f }, 0.02f, 0.3f);
            return Mix(rate, 0.45f, (0f, RecipesWeapons.Click(r, rate, 0.9f, 0.2f), 0.6f), (0.03f, ping, 0.7f));
        }

        /// <summary>Spoon flying off: a quick springy clink.</summary>
        public static float[] GrenadeSpoon(FastRng r, int rate)
        {
            var b = Modal(r, rate, 0.3f, new[] { 2300f, 3900f, 6100f }, new[] { 0.06f, 0.04f, 0.03f }, 0.04f, 0.6f);
            Normalize(b, 0.6f);
            return b;
        }

        /// <summary>A grenade bouncing on a hard floor: dull metal tok.</summary>
        public static float[] GrenadeBounce(FastRng r, int rate)
        {
            var b = Modal(r, rate, 0.25f, new[] { 900f, 1700f, 2900f }, new[] { 0.05f, 0.035f, 0.02f }, 0.05f, 0.8f);
            Add(b, RecipesWeapons.Thud(r, rate, 0.3f, 0.12f), 0, 0.5f);
            Normalize(b, 0.7f);
            return b;
        }

        /// <summary>Sticky charge slapped onto a surface.</summary>
        public static float[] C4Place(FastRng r, int rate)
        {
            var b = Buf(0.3f, rate);
            var lp = Biquad.LowPass(rate, 900);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                b[i] = lp.Process(r.Bipolar()) * Env.AD(t, 0.002f, 0.04f) * 2f;
            }
            Add(b, RecipesWeapons.Click(r, rate, 0.7f, 0.6f), (int)(0.05f * rate), 0.5f);
            Normalize(b, 0.7f);
            return b;
        }

        /// <summary>Electronic beep (square-ish sine).</summary>
        public static float[] Beep(FastRng r, int rate, float hz, float seconds)
        {
            var b = Buf(seconds, rate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float s = Sin(t * hz);
                b[i] = Math.Sign(s) * 0.35f + s * 0.4f;
                b[i] *= Env.AR(t, 0.003f, seconds * 0.8f, seconds * 0.15f);
            }
            LowPass(b, rate, 5000);
            Normalize(b, 0.55f);
            return b;
        }

        public static float[] C4Arm(FastRng r, int rate) => Mix(rate, 0.5f, (0f, RecipesWeapons.Click(r, rate, 1.3f, 0.1f), 0.6f), (0.06f, Beep(r, rate, 1800, 0.08f), 0.8f), (0.2f, Beep(r, rate, 2400, 0.12f), 0.8f));

        /// <summary>Katana leaving the sheath / ringing steel.</summary>
        public static float[] KatanaDraw(FastRng r, int rate)
        {
            var b = RecipesWeapons.Whoosh(r, rate, 0.55f, 3000, 7500, 3f);
            var ring = Modal(r, rate, 0.9f, new[] { 2200f, 3600f, 5900f, 8100f }, new[] { 0.5f, 0.35f, 0.22f, 0.15f }, 0.01f, 0.08f);
            Add(b, ring, (int)(0.3f * rate), 0.6f);
            Normalize(b, 0.7f);
            return b;
        }

        /// <summary>Blade hitting stone / metal.</summary>
        public static float[] KatanaClang(FastRng r, int rate)
        {
            var b = Modal(r, rate, 0.8f, new[] { 1900f, 3300f, 5200f, 7700f }, new[] { 0.35f, 0.25f, 0.15f, 0.1f }, 0.02f, 1f);
            Normalize(b, 0.75f);
            return b;
        }

        /// <summary>Sledgehammer hitting a body: deep thump plus bone crunch.</summary>
        public static float[] SledgeFlesh(FastRng r, int rate)
        {
            var thud = RecipesWeapons.Thud(r, rate, 1f, 0.5f);
            var crunch = Buf(0.25f, rate);
            var bp = Biquad.BandPass(rate, 1500, 1.2f);
            for (int i = 0; i < crunch.Length; i++)
            {
                float t = i / (float)rate;
                float grain = r.Next() < 0.08f ? r.Bipolar() * 4f : r.Bipolar() * 0.3f;
                crunch[i] = bp.Process(grain) * Env.Exp(t, 0.06f);
            }
            var b = Mix(rate, 0.6f, (0f, thud, 1f), (0.005f, crunch, 0.7f));
            Drive(b, 1.6f);
            Normalize(b, 0.9f);
            return b;
        }

        /// <summary>Sledgehammer hitting the world: heavy concrete/metal impact.</summary>
        public static float[] SledgeWorld(FastRng r, int rate)
        {
            var thud = RecipesWeapons.Thud(r, rate, 1f, 0.6f);
            var ring = Modal(r, rate, 0.7f, new[] { 420f, 980f, 1750f, 2900f }, new[] { 0.2f, 0.14f, 0.09f, 0.05f }, 0.04f, 0.9f);
            var b = Mix(rate, 0.8f, (0f, thud, 1f), (0f, ring, 0.45f));
            b = WithReverb(b, rate, 0.6f, 0.25f, 0.6f);
            Normalize(b, 0.9f);
            return b;
        }
    }
}
