using System;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>A sound the AI can hear. Loudness is the radius in metres at which it is noticed.</summary>
    public struct NoiseEvent
    {
        public Vector3 Position;
        public float Radius;
        public float Threat; // 0 = curious sound, 1 = gunfire/explosion
        public object Source;
    }

    /// <summary>Decoupled notifications between systems (stats, achievements, AI, HUD).</summary>
    public static class GameEvents
    {
        public static event Action<NoiseEvent> Noise;
        public static event Action<string> Toast;
        public static event Action<string, Color> KillFeed;
        public static event Action<Vector3, float> HitMarker; // world point, damage
        public static event Action<string> StatIncrement;

        public static void EmitNoise(Vector3 pos, float radius, float threat, object source = null)
            => Noise?.Invoke(new NoiseEvent { Position = pos, Radius = radius, Threat = threat, Source = source });

        public static void ShowToast(string message) => Toast?.Invoke(message);
        public static void Kill(string text, Color color) => KillFeed?.Invoke(text, color);
        public static void Hit(Vector3 point, float damage) => HitMarker?.Invoke(point, damage);
        public static void Stat(string stat) => StatIncrement?.Invoke(stat);
    }
}
