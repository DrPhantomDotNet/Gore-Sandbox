using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 player feel: sprint stamina (with panting when it runs out), a limp after a hard landing,
    /// a heartbeat when your health is low, and ringing ears with muffled hearing after a close explosion.
    /// Driven from PlayerController (Tick) and Explosion (Blast). HUD bar in UI/Hud.cs.
    /// </summary>
    public static class PlayerFeel
    {
        public static float Stamina = 1f;
        static bool exhausted;
        static float limpUntil, nextBeat, nextPant, ringUntil, ringStart;
        static AudioLowPassFilter muffle;
        static AudioSource ring;

        public static bool CanSprint => !exhausted;
        public static bool Limping => Time.time < limpUntil;
        public static float SpeedMul => Limping ? 0.62f : 1f;

        public static void Reset() { Stamina = 1f; exhausted = false; limpUntil = 0f; ringUntil = 0f; }

        public static void OnHardLanding(float fallSpeed)
        {
            if (fallSpeed > 10f) limpUntil = Time.time + Mathf.Clamp((fallSpeed - 10f) * 3f, 4f, 25f);
        }

        /// <summary>Called by explosions: strength 0..1 from how close you were.</summary>
        public static void Blast(float strength)
        {
            if (strength < 0.15f) return;
            ringStart = Time.time;
            ringUntil = Mathf.Max(ringUntil, Time.time + 1.5f + strength * 5f);
            var p = PlayerController.Instance;
            if (p == null || p.Cam == null) return;
            if (ring == null)
            {
                ring = p.Cam.gameObject.AddComponent<AudioSource>();
                ring.playOnAwake = false; ring.spatialBlend = 0f; ring.loop = false;
            }
            var c = SoundBank.Get("tinnitus");
            if (c != null) { ring.clip = c; ring.volume = 0.25f * strength * Settings.Data.sfxVolume; ring.Play(); }
        }

        public static void Tick(PlayerController p, bool sprinting, float dt)
        {
            // Stamina: about 8 seconds of sprinting, refills in 5; empty = no sprint until it is back to 30%.
            if (sprinting) Stamina = Mathf.Max(0f, Stamina - dt / 8f);
            else Stamina = Mathf.Min(1f, Stamina + dt / 5f);
            if (Stamina <= 0f && !exhausted) { exhausted = true; nextPant = 0f; }
            if (exhausted && Stamina > 0.3f) exhausted = false;
            if (Stamina < 0.35f && Time.time > nextPant && !p.IsDead)
            {
                nextPant = Time.time + Mathf.Lerp(0.5f, 0.9f, Stamina / 0.35f);
                AudioManager.Play2D(Random.value < 0.5f ? "breath_out_2" : "breath_in_2", 0.35f, Random.Range(0.95f, 1.05f), AudioBus.Sfx);
            }

            // Heartbeat when hurt (faster the closer you are to death).
            float hp = p.Health != null ? p.Health.Current / Mathf.Max(1f, p.Health.Max) : 1f;
            if (hp < 0.35f && !p.IsDead && Time.time > nextBeat)
            {
                nextBeat = Time.time + Mathf.Lerp(0.45f, 0.9f, hp / 0.35f);
                AudioManager.Play2D("heartbeat", Mathf.Lerp(0.7f, 0.35f, hp / 0.35f), 1f, AudioBus.Sfx);
            }

            // Muffled hearing after a blast.
            if (p.Cam != null)
            {
                if (muffle == null) { muffle = p.Cam.GetComponent<AudioLowPassFilter>() ?? p.Cam.gameObject.AddComponent<AudioLowPassFilter>(); muffle.cutoffFrequency = 22000f; }
                float left = ringUntil - Time.time, total = Mathf.Max(0.01f, ringUntil - ringStart);
                float k = left > 0f ? Mathf.Clamp01(left / total) : 0f;
                muffle.cutoffFrequency = Mathf.Lerp(22000f, 650f, k * k);
                muffle.enabled = k > 0.001f;
            }
        }
    }
}
