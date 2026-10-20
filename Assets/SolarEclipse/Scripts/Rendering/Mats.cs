using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Shared materials built from the shaders in Resources/Shaders, with safe fallbacks.</summary>
    public static class Mats
    {
        static Material lit, litMetal, particle, additive, decalBlood, decalHoles, decalWounds, sky, xray, flesh, glass, lineMat;
        static readonly Dictionary<Texture, Material> litTextured = new Dictionary<Texture, Material>();
        static readonly Dictionary<Color, Material> glow = new Dictionary<Color, Material>();
        static readonly Dictionary<Texture, Material> particleTex = new Dictionary<Texture, Material>();
        static readonly Dictionary<Texture, Material> additiveTex = new Dictionary<Texture, Material>();

        public static Shader Find(string name, string fallback, string fallback2 = "Sprites/Default")
        {
            var s = Shader.Find(name);
            if (s == null || !s.isSupported) s = Shader.Find(fallback);
            if (s == null) s = Shader.Find(fallback2);
            if (s == null) Debug.LogError("Missing shader " + name);
            return s;
        }

        public static Material Lit => lit ??= Make(Find("SolarEclipse/Lit", "Standard"), "SEG_Lit");

        public static Material LitMetal
        {
            get
            {
                if (litMetal == null) { litMetal = Make(Find("SolarEclipse/Lit", "Standard"), "SEG_LitMetal"); litMetal.SetFloat("_Metallic", 0.75f); }
                return litMetal;
            }
        }

        public static Material Flesh
        {
            get
            {
                if (flesh == null) { flesh = Make(Find("SolarEclipse/Lit", "Standard"), "SEG_FleshCap"); flesh.mainTexture = TextureFactory.FleshCap; }
                return flesh;
            }
        }

        public static Material LitTextured(Texture tex)
        {
            if (!litTextured.TryGetValue(tex, out var m))
            {
                m = Make(Find("SolarEclipse/Lit", "Standard"), "SEG_Lit_" + tex.name);
                m.mainTexture = tex;
                litTextured[tex] = m;
            }
            return m;
        }

        /// <summary>Emissive lit material (lamps, screens, sci-fi glows).</summary>
        public static Material Glow(Color color, float intensity = 2f)
        {
            var key = color * intensity;
            if (!glow.TryGetValue(key, out var m))
            {
                m = Make(Find("SolarEclipse/Lit", "Standard"), "SEG_Glow");
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * intensity);
                m.SetColor("_Color", color);
                glow[key] = m;
            }
            return m;
        }

        public static Material Particle => particle ??= ParticleWith(TextureFactory.SoftDot);
        public static Material Additive => additive ??= AdditiveWith(TextureFactory.SoftDot);

        public static Material ParticleWith(Texture tex)
        {
            if (!particleTex.TryGetValue(tex, out var m))
            {
                m = Make(Find("SolarEclipse/Particle", "Particles/Standard Unlit"), "SEG_Particle_" + tex.name);
                m.mainTexture = tex;
                particleTex[tex] = m;
            }
            return m;
        }

        public static Material AdditiveWith(Texture tex)
        {
            if (!additiveTex.TryGetValue(tex, out var m))
            {
                m = Make(Find("SolarEclipse/Additive", "Particles/Standard Unlit"), "SEG_Add_" + tex.name);
                m.mainTexture = tex;
                additiveTex[tex] = m;
            }
            return m;
        }

        public static Material DecalBlood => decalBlood ??= DecalWith(TextureFactory.BloodAtlas, 0.75f);
        public static Material DecalHoles => decalHoles ??= DecalWith(TextureFactory.HoleAtlas, 0.2f);
        public static Material DecalWounds => decalWounds ??= DecalWith(TextureFactory.WoundAtlas, 0.65f);

        static Material DecalWith(Texture tex, float gloss)
        {
            var m = Make(Find("SolarEclipse/Decal", "Sprites/Default"), "SEG_Decal_" + tex.name);
            m.mainTexture = tex;
            m.SetFloat("_Glossiness", gloss);
            m.renderQueue = 2500;
            return m;
        }

        public static Material Sky => sky ??= Make(Find("SolarEclipse/Sky", "Skybox/Procedural"), "SEG_Sky");
        public static Material Xray => xray ??= Make(Find("SolarEclipse/Xray", "Sprites/Default"), "SEG_Xray");

        public static Material Glass
        {
            get
            {
                if (glass == null)
                {
                    glass = Make(Find("SolarEclipse/Particle", "Sprites/Default"), "SEG_Glass");
                    glass.color = new Color(0.7f, 0.85f, 0.95f, 0.28f);
                }
                return glass;
            }
        }

        /// <summary>Unlit colored lines (GL drawing, debug).</summary>
        public static Material Lines
        {
            get
            {
                if (lineMat == null)
                {
                    lineMat = new Material(Find("Hidden/Internal-Colored", "Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
                    lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    lineMat.SetInt("_ZWrite", 0);
                }
                return lineMat;
            }
        }

        static Material Make(Shader s, string name) => new Material(s) { name = name, hideFlags = HideFlags.DontSave, enableInstancing = true };
    }
}
