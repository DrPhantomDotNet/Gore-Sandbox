using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Global camera feedback requests (shake, punch, flash). The player camera consumes them.</summary>
    public static class CameraFX
    {
        public static float Trauma;
        public static Vector2 Kick;        // degrees (pitch, yaw) to add once
        public static float FlashAlpha;
        public static Color FlashColor = Color.white;
        public static Vector3 ListenerPosition;

        public static void Shake(float amount) => Trauma = Mathf.Clamp01(Trauma + amount * Settings.Data.screenShake);

        public static void ShakeAt(Vector3 pos, float strength, float radius)
        {
            float d = Vector3.Distance(pos, ListenerPosition);
            if (d < radius) Shake(strength * (1f - d / radius));
        }

        public static void Punch(float pitch, float yaw) { Kick.x += pitch; Kick.y += yaw; }

        public static void Flash(Color c, float alpha) { FlashColor = c; FlashAlpha = Mathf.Max(FlashAlpha, alpha); }
    }

    /// <summary>Something the player can use with E.</summary>
    public interface IInteractable
    {
        string Prompt { get; }
        void Interact(GameObject user);
    }
}
