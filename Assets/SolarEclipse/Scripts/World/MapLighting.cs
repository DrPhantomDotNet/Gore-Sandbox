using UnityEngine;
using UnityEngine.Rendering;

namespace SolarEclipse
{
    /// <summary>Sun/moon, ambient, fog and sky for each time of day.</summary>
    public static class MapLighting
    {
        public static Light Sun { get; private set; }
        static Material sky;

        public static void Apply(TimeOfDay time, bool indoor, Transform parent)
        {
            if (Sun == null)
            {
                var go = new GameObject("Sun");
                Sun = go.AddComponent<Light>();
                Sun.type = LightType.Directional;
            }
            Sun.transform.SetParent(parent, false);
            Sun.shadows = Settings.Data.shadowQuality == 2 ? LightShadows.Soft : Settings.Data.shadowQuality == 1 ? LightShadows.Hard : LightShadows.None;
            Sun.shadowStrength = 0.85f;
            Sun.shadowBias = 0.04f;
            Sun.shadowNormalBias = 0.3f;

            if (sky == null) sky = new Material(Mats.Sky) { name = "MapSky" };
            Color top, horizon, bottom, sunCol, ambSky, ambEq, ambGround, fog;
            float intensity, fogDensity, stars = 0f, sunSize = 0.012f, eclipse = 0f;
            Quaternion rot;
            switch (time)
            {
                case TimeOfDay.Sunset:
                    rot = Quaternion.Euler(9, -65, 0); sunCol = new Color(1f, 0.62f, 0.38f); intensity = 1.0f;
                    top = new Color(0.18f, 0.22f, 0.45f); horizon = new Color(1f, 0.56f, 0.32f); bottom = new Color(0.25f, 0.18f, 0.16f);
                    ambSky = new Color(0.5f, 0.45f, 0.55f); ambEq = new Color(0.55f, 0.4f, 0.32f); ambGround = new Color(0.2f, 0.16f, 0.14f);
                    fog = new Color(0.8f, 0.52f, 0.4f); fogDensity = 0.007f; sunSize = 0.02f;
                    break;
                case TimeOfDay.Night:
                    rot = Quaternion.Euler(38, 35, 0); sunCol = new Color(0.55f, 0.65f, 1f); intensity = 0.28f;
                    top = new Color(0.01f, 0.015f, 0.04f); horizon = new Color(0.05f, 0.07f, 0.13f); bottom = new Color(0.02f, 0.02f, 0.03f);
                    ambSky = new Color(0.08f, 0.1f, 0.17f); ambEq = new Color(0.06f, 0.07f, 0.1f); ambGround = new Color(0.03f, 0.03f, 0.04f);
                    fog = new Color(0.03f, 0.04f, 0.07f); fogDensity = 0.018f; stars = 1f; sunSize = 0.008f;
                    break;
                case TimeOfDay.BloodMoon:
                    rot = Quaternion.Euler(30, 160, 0); sunCol = new Color(1f, 0.28f, 0.2f); intensity = 0.45f;
                    top = new Color(0.08f, 0.0f, 0.02f); horizon = new Color(0.55f, 0.07f, 0.07f); bottom = new Color(0.12f, 0.02f, 0.02f);
                    ambSky = new Color(0.3f, 0.06f, 0.07f); ambEq = new Color(0.22f, 0.05f, 0.05f); ambGround = new Color(0.08f, 0.02f, 0.02f);
                    fog = new Color(0.35f, 0.04f, 0.05f); fogDensity = 0.022f; stars = 0.4f; sunSize = 0.03f;
                    break;
                default:
                    rot = Quaternion.Euler(52, -35, 0); sunCol = new Color(1f, 0.96f, 0.88f); intensity = 1.15f;
                    top = new Color(0.22f, 0.43f, 0.85f); horizon = new Color(0.72f, 0.8f, 0.9f); bottom = new Color(0.35f, 0.36f, 0.36f);
                    ambSky = new Color(0.56f, 0.63f, 0.76f); ambEq = new Color(0.45f, 0.47f, 0.5f); ambGround = new Color(0.24f, 0.22f, 0.2f);
                    fog = new Color(0.72f, 0.8f, 0.88f); fogDensity = 0.0045f;
                    break;
            }
            if (indoor) { intensity *= 0.35f; fogDensity *= 0.5f; }
            if (Stats.HasEasterEgg("Total Eclipse") && Random.value < 0.15f) { eclipse = 1f; intensity *= 0.3f; }
            float b = Settings.Data.brightness;
            Sun.transform.rotation = rot;
            Sun.color = sunCol;
            Sun.intensity = intensity;
            RenderSettings.sun = Sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambSky * b;
            RenderSettings.ambientEquatorColor = ambEq * b;
            RenderSettings.ambientGroundColor = ambGround * b;
            RenderSettings.fog = Settings.Data.fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = fogDensity;
            sky.SetColor("_Top", top);
            sky.SetColor("_Horizon", horizon);
            sky.SetColor("_Bottom", bottom);
            sky.SetColor("_SunColor", sunCol);
            sky.SetVector("_SunDir", -(rot * Vector3.forward));
            sky.SetFloat("_SunSize", sunSize);
            sky.SetFloat("_Stars", stars);
            sky.SetFloat("_Eclipse", eclipse);
            RenderSettings.skybox = sky;
            RenderSettings.reflectionIntensity = time == TimeOfDay.Day ? 0.6f : 0.25f;
            float particleLight = Mathf.Clamp(ambSky.grayscale * 1.6f + intensity * 0.55f, 0.25f, 1f) * b;
            Shader.SetGlobalFloat("_SEG_ParticleLight", particleLight);
            QualitySettings.pixelLightCount = time == TimeOfDay.Day && !indoor ? 4 : 8;
        }
    }
}
