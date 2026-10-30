using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// "SolarEclipse Games" intro: a sun rises, the moon slides across into totality, a diamond-ring flash
    /// detonates with a boom and screen shake, the corona blooms and the studio name assembles letter by letter.
    /// Waits for the sound synthesis to finish first so every hit lands in sync.
    /// </summary>
    public sealed class SplashScreen : UIScreen
    {
        struct Ember { public Vector2 Pos, Vel; public float Life, Size; }

        readonly List<Ember> embers = new List<Ember>();
        readonly Vector2[] stars = new Vector2[160];
        readonly float[] starPhase = new float[160];
        float start = -1f;
        bool whoosh, impact, shimmer, done;
        const float Length = 7.4f;
        const string Studio = "SOLARECLIPSE";

        public override void OnOpen()
        {
            for (int i = 0; i < stars.Length; i++) { stars[i] = new Vector2(Random.value, Random.value); starPhase[i] = Random.value * 10f; }
        }

        float T => start < 0 ? 0 : Time.unscaledTime - start;

        public override void Tick()
        {
            if (start < 0)
            {
                if (SoundBank.Ready && Age > 0.5f)
                {
                    start = Time.unscaledTime;
                    AudioManager.Play2D("logo_riser", 0.9f, 1f, AudioBus.Music);
                }
                return;
            }
            float t = T;
            if (!whoosh && t > 1.1f) { whoosh = true; AudioManager.Play2D("logo_whoosh", 0.8f, 1f, AudioBus.Music); }
            if (!impact && t > 2.6f) { impact = true; AudioManager.Play2D("logo_impact", 1f, 1f, AudioBus.Music); }
            if (!shimmer && t > 2.75f) { shimmer = true; AudioManager.Play2D("logo_shimmer", 0.7f, 1f, AudioBus.Music); }

            bool skip = t > 1f && (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
            if (!done && (t > Length || skip))
            {
                done = true;
                // The cinematic intro (UI/Screens/IntroScreen.cs) calls OnSplashFinished itself when it ends or is skipped.
                if (IntroScreen.ShouldPlay) UIRoot.Replace(new IntroScreen());
                else GameManager.I.OnSplashFinished();
            }

            // Embers drift out from the sun after the impact.
            if (impact && t < 5.6f && embers.Count < 140)
                for (int i = 0; i < 3; i++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    embers.Add(new Ember { Pos = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 140f, Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(40f, 180f), Life = Random.Range(1.2f, 2.8f), Size = Random.Range(3f, 9f) });
                }
            float dt = Time.unscaledDeltaTime;
            for (int i = embers.Count - 1; i >= 0; i--)
            {
                var e = embers[i];
                e.Pos += e.Vel * dt; e.Vel *= 1f - dt * 0.6f; e.Life -= dt;
                if (e.Life <= 0) embers.RemoveAt(i); else embers[i] = e;
            }
        }

        public override bool OnBack() => true;

        public override void Draw()
        {
            float W = UI.VW, H = UI.VH;
            UI.Fill(new Rect(0, 0, W, H), Color.black);
            if (start < 0) { DrawLoading(W, H); return; }
            float t = T;

            // Screen shake after the impact.
            float shake = t > 2.6f ? Mathf.Exp(-(t - 2.6f) * 3.5f) * 14f : 0f;
            var offset = new Vector2(Mathf.Sin(t * 91f) * shake, Mathf.Cos(t * 73f) * shake) * UI.Scale;
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one);

            var c = new Vector2(W / 2f, H * 0.42f);
            float sunR = 125f;
            float fadeIn = MathX.SmoothStep01(t / 1.4f);
            float moonT = MathX.SmoothStep01((t - 0.9f) / 1.75f);
            float totality = MathX.SmoothStep01((t - 2.45f) / 0.25f);

            // Deep space background
            UI.Gradient(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.04f), new Color(0.05f, 0.02f, 0.06f));
            for (int i = 0; i < stars.Length; i++)
            {
                float tw = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(t * 1.5f + starPhase[i]));
                float sz = 2f + (i % 3);
                UI.Tex(new Rect(stars[i].x * W, stars[i].y * H, sz, sz), TextureFactory.SoftDot, new Color(0.8f, 0.85f, 1f, tw * fadeIn * (0.4f + totality * 0.6f)));
            }

            // Sun glow and disc (dims as the moon covers it)
            float sunVis = Mathf.Lerp(1f, 0.08f, totality) * fadeIn;
            UI.Tex(Square(c, sunR * 7.5f), TextureFactory.Glow, new Color(1f, 0.55f, 0.2f, 0.55f * sunVis));
            UI.Tex(Square(c, sunR * 3.2f), TextureFactory.Glow, new Color(1f, 0.85f, 0.55f, 0.8f * sunVis));
            UI.Tex(Square(c, sunR * 2f), TextureFactory.Circle, new Color(1f, 0.95f, 0.8f, fadeIn));

            // Corona blooms at totality, slowly rotating
            float corona = totality * (1f + 0.25f * Mathf.Sin(t * 2f));
            if (corona > 0.01f)
            {
                var pivot = new Vector2(c.x * UI.Scale, c.y * UI.Scale) + offset;
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(t * 6f, pivot);
                UI.Tex(Square(c, sunR * 5.6f), TextureFactory.Corona, new Color(1f, 0.92f, 0.85f, 0.9f * corona));
                GUI.matrix = m;
                GUIUtility.RotateAroundPivot(-t * 4f, pivot);
                UI.Tex(Square(c, sunR * 4.2f), TextureFactory.Corona, new Color(1f, 0.6f, 0.35f, 0.6f * corona));
                GUI.matrix = m;
            }

            // Moon slides across
            var moonPos = new Vector2(Mathf.Lerp(-W * 0.35f, c.x + 2f, moonT), Mathf.Lerp(c.y + 90f, c.y, moonT));
            UI.Tex(Square(moonPos, sunR * 2.04f), TextureFactory.Circle, new Color(0.015f, 0.012f, 0.02f, 1f));
            UI.Tex(Square(moonPos, sunR * 2.12f), TextureFactory.Ring, new Color(1f, 0.6f, 0.3f, 0.25f * totality));

            // Diamond ring flash
            float flash = t > 2.45f ? Mathf.Exp(-(t - 2.6f) * 5f) * MathX.SmoothStep01((t - 2.45f) / 0.15f) : 0f;
            if (flash > 0.01f)
            {
                var ring = c + new Vector2(-sunR * 0.82f, -sunR * 0.55f);
                UI.Tex(Square(ring, 520f * flash + 80f), TextureFactory.Star4, new Color(1f, 0.97f, 0.9f, flash));
                UI.Tex(Square(ring, 300f), TextureFactory.Glow, new Color(1f, 0.9f, 0.7f, flash));
                UI.Fill(new Rect(-50, -50, W + 100, H + 100), new Color(1f, 0.95f, 0.88f, flash * 0.55f));
            }

            // Anamorphic streak
            float streak = (t > 2.6f ? Mathf.Exp(-(t - 2.6f) * 1.4f) : 0f) + 0.15f * totality;
            UI.Tex(new Rect(c.x - W * 0.7f, c.y - 22, W * 1.4f, 44), TextureFactory.Streak, new Color(1f, 0.55f, 0.25f, streak));

            foreach (var e in embers)
                UI.Tex(Square(c + e.Pos, e.Size), TextureFactory.SoftDot, new Color(1f, 0.6f, 0.25f, Mathf.Clamp01(e.Life)));

            DrawTitle(c, sunR, t, W);

            GUI.matrix = Matrix4x4.identity;
            float fadeOut = MathX.SmoothStep01((t - (Length - 1.1f)) / 1.0f);
            UI.Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, fadeOut));
            if (t > 1.2f && t < Length - 1.2f)
                UI.Label(new Rect(0, H - 60, W, 40), "click or press any key to skip", 18, new Color(1, 1, 1, 0.3f), TextAnchor.MiddleCenter, FontStyle.Italic, false);
        }

        void DrawTitle(Vector2 c, float sunR, float t, float W)
        {
            int size = 92;
            float spacing = 10f;
            float total = 0;
            var widths = new float[Studio.Length];
            for (int i = 0; i < Studio.Length; i++) { widths[i] = UI.TextWidth(Studio[i].ToString(), size, FontStyle.Normal, UI.HeavyFont); total += widths[i] + spacing; }
            total -= spacing;
            float x = (W - total) / 2f;
            float y = c.y + sunR * 1.9f;
            for (int i = 0; i < Studio.Length; i++)
            {
                float lt = MathX.SmoothStep01((t - 3.0f - i * 0.065f) / 0.35f);
                if (lt <= 0) { x += widths[i] + spacing; continue; }
                float rise = (1f - lt) * 40f;
                var col = Color.Lerp(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.95f, 0.85f), i / (float)(Studio.Length - 1));
                var r = new Rect(x - 20, y + rise, widths[i] + 40, 120);
                for (int g = 0; g < 4; g++)
                {
                    var o = new Vector2(Mathf.Cos(g * 1.57f), Mathf.Sin(g * 1.57f)) * 4f;
                    UI.Label(new Rect(r.x + o.x, r.y + o.y, r.width, r.height), Studio[i].ToString(), size, new Color(1f, 0.45f, 0.1f, 0.18f * lt), TextAnchor.MiddleCenter, FontStyle.Normal, false, false, UI.HeavyFont);
                }
                UI.Label(r, Studio[i].ToString(), size, col.WithAlpha(lt), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
                x += widths[i] + spacing;
            }
            float gt = MathX.SmoothStep01((t - 3.9f) / 0.6f);
            if (gt > 0)
            {
                UI.Fill(new Rect(W / 2 - 260 * gt, y + 128, 520 * gt, 2), new Color(1f, 0.55f, 0.2f, 0.7f * gt));
                UI.Label(new Rect(0, y + 136, W, 60), "G  A  M  E  S", 38, new Color(1f, 0.75f, 0.5f, gt), TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            float pt = MathX.SmoothStep01((t - 4.8f) / 0.6f);
            if (pt > 0) UI.Label(new Rect(0, y + 200, W, 40), "presents", 22, new Color(1f, 1f, 1f, 0.55f * pt), TextAnchor.MiddleCenter, FontStyle.Italic, false);
        }

        void DrawLoading(float W, float H)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            var c = new Vector2(W / 2f, H / 2f);
            UI.Tex(Square(c, 220 + pulse * 30), TextureFactory.Glow, new Color(1f, 0.5f, 0.2f, 0.25f + pulse * 0.1f));
            UI.Tex(Square(c, 90), TextureFactory.Ring, new Color(1f, 0.7f, 0.4f, 0.6f));
            float p = SoundBank.Progress;
            UI.Rounded(new Rect(W / 2 - 160, H / 2 + 90, 320, 6), new Color(1, 1, 1, 0.08f), true);
            UI.Rounded(new Rect(W / 2 - 160, H / 2 + 90, 320 * p, 6), new Color(1f, 0.6f, 0.25f, 0.9f), true);
            UI.Label(new Rect(0, H / 2 + 104, W, 30), "synthesising audio  " + Mathf.RoundToInt(p * 100) + "%", 18, new Color(1, 1, 1, 0.35f), TextAnchor.MiddleCenter, FontStyle.Normal, false);
        }

        static Rect Square(Vector2 center, float size) => new Rect(center.x - size / 2f, center.y - size / 2f, size, size);
    }
}
