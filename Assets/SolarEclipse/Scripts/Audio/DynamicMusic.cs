using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 dynamic music (Settings > Audio): combat music fades in when a fight breaks out near you
    /// (gunfire, explosions, deaths close by) and fades out after things have been quiet for a while.
    /// Stays out of the way of story mode, which runs its own music.
    /// </summary>
    public sealed class DynamicMusic : MonoBehaviour
    {
        static DynamicMusic inst;
        float intensity, quietFor;
        bool playing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (inst != null) return;
            var go = new GameObject("Dynamic Music"); DontDestroyOnLoad(go);
            inst = go.AddComponent<DynamicMusic>();
            GameEvents.Noise += inst.OnNoise;
            Goredoll.Died += inst.OnDied;
        }

        void OnNoise(NoiseEvent e)
        {
            var p = PlayerController.Instance;
            if (p == null || e.Threat < 0.85f) return;
            float d = Vector3.Distance(p.transform.position, e.Position);
            if (d < 45f) intensity = Mathf.Min(1.5f, intensity + Mathf.Lerp(0.25f, 0.05f, d / 45f));
        }

        void OnDied(Goredoll d, object attacker)
        {
            var p = PlayerController.Instance;
            if (p != null && d != null && (d.Position - p.transform.position).sqrMagnitude < 900f) intensity = Mathf.Min(1.5f, intensity + 0.3f);
        }

        void Update()
        {
            var p = PlayerController.Instance;
            bool allowed = Settings.Data.dynamicMusic && p != null && !StoryRunner.Active;
            intensity = Mathf.Max(0f, intensity - Time.unscaledDeltaTime / 25f);
            if (!allowed) { if (playing && p != null) AudioManager.StopMusic(); playing = false; intensity = 0f; return; } // back at the menu: the menu picks its own music
            if (!playing && intensity > 0.7f) { playing = true; quietFor = 0f; AudioManager.PlayMusic("music_combat", 0.5f); }
            if (playing)
            {
                quietFor = intensity < 0.1f ? quietFor + Time.unscaledDeltaTime : 0f;
                if (quietFor > 6f) { playing = false; AudioManager.StopMusic(); }
            }
        }
    }
}
