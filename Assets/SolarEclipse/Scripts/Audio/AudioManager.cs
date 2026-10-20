using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    public enum AudioBus { Sfx, Voice, Ui, Music, Ambience }

    /// <summary>Pooled one-shots, loops, music and ambience with per-bus volumes from Settings.</summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        const int PoolSize = 48;
        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly Dictionary<AudioSource, AudioBus> busOf = new Dictionary<AudioSource, AudioBus>();
        readonly Dictionary<AudioSource, float> baseVolume = new Dictionary<AudioSource, float>();
        readonly Dictionary<AudioSource, Transform> follow = new Dictionary<AudioSource, Transform>();
        AudioSource musicA, musicB, ambience;
        float musicTarget = 1f;
        bool musicOnA = true;
        int next;

        public static void Create(Transform parent)
        {
            if (I != null) return;
            var go = new GameObject("AudioManager");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<AudioManager>();
        }

        void Awake()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("OneShot" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.dopplerLevel = 0f;
                pool.Add(src);
            }
            musicA = MakeLoopSource("MusicA", AudioBus.Music);
            musicB = MakeLoopSource("MusicB", AudioBus.Music);
            ambience = MakeLoopSource("Ambience", AudioBus.Ambience);
            Settings.Changed += RefreshVolumes;
        }

        void OnDestroy() { Settings.Changed -= RefreshVolumes; }

        AudioSource MakeLoopSource(string name, AudioBus bus)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f;
            busOf[s] = bus; baseVolume[s] = 0f;
            return s;
        }

        public static float BusVolume(AudioBus bus)
        {
            var d = Settings.Data;
            switch (bus)
            {
                case AudioBus.Voice: return d.voiceVolume;
                case AudioBus.Ui: return d.uiVolume;
                case AudioBus.Music: return d.musicVolume;
                case AudioBus.Ambience: return d.ambienceVolume;
                default: return d.sfxVolume;
            }
        }

        void RefreshVolumes()
        {
            foreach (var kv in baseVolume)
                if (kv.Key != null && busOf.TryGetValue(kv.Key, out var bus) && kv.Key != musicA && kv.Key != musicB)
                    kv.Key.volume = kv.Value * BusVolume(bus);
        }

        void Update()
        {
            foreach (var kv in follow)
                if (kv.Key != null && kv.Value != null && kv.Key.isPlaying) kv.Key.transform.position = kv.Value.position;
            // Music crossfade
            float dt = Time.unscaledDeltaTime;
            var on = musicOnA ? musicA : musicB;
            var off = musicOnA ? musicB : musicA;
            float mv = Settings.Data.musicVolume;
            on.volume = Mathf.MoveTowards(on.volume, musicTarget * mv, dt * 0.5f);
            off.volume = Mathf.MoveTowards(off.volume, 0f, dt * 0.5f);
            if (off.volume <= 0f && off.isPlaying) off.Stop();
        }

        AudioSource NextSource()
        {
            for (int k = 0; k < pool.Count; k++)
            {
                var s = pool[(next + k) % pool.Count];
                if (!s.isPlaying) { next = (next + k + 1) % pool.Count; return s; }
            }
            var steal = pool[next];
            next = (next + 1) % pool.Count;
            return steal;
        }

        /// <summary>Plays a 3D one-shot at a position (spatial 1) or a 2D sound (spatial 0).</summary>
        public static AudioSource Play(string name, Vector3 position, float volume = 1f, float pitch = 1f, float spatial = 1f,
            float minDistance = 1.5f, float maxDistance = 80f, AudioBus bus = AudioBus.Sfx, Transform followTarget = null)
        {
            if (I == null) return null;
            var clip = SoundBank.Get(name);
            if (clip == null) return null;
            var s = I.NextSource();
            s.Stop();
            s.clip = clip;
            s.transform.position = position;
            s.spatialBlend = spatial;
            s.minDistance = minDistance;
            s.maxDistance = maxDistance;
            s.pitch = pitch * Time.timeScale < 0.99f ? pitch * Mathf.Lerp(0.6f, 1f, Time.timeScale) : pitch;
            s.loop = false;
            I.busOf[s] = bus;
            I.baseVolume[s] = volume;
            s.volume = volume * BusVolume(bus);
            if (followTarget != null) I.follow[s] = followTarget; else I.follow.Remove(s);
            s.Play();
            return s;
        }

        public static AudioSource PlayVaried(string name, Vector3 position, float volume = 1f, float pitchVariance = 0.08f, float maxDistance = 80f)
            => Play(name, position, volume, 1f + Random.Range(-pitchVariance, pitchVariance), 1f, 1.5f, maxDistance);

        public static AudioSource Play2D(string name, float volume = 1f, float pitch = 1f, AudioBus bus = AudioBus.Ui)
            => Play(name, Vector3.zero, volume, pitch, 0f, 1f, 500f, bus);

        public static void Ui(string name, float volume = 1f) => Play2D(name, volume, 1f, AudioBus.Ui);

        /// <summary>Creates a persistent looping source attached to <paramref name="parent"/>.</summary>
        public static AudioSource CreateLoop(string name, Transform parent, float volume, AudioBus bus = AudioBus.Sfx, float maxDistance = 40f, bool playNow = true)
        {
            var go = new GameObject("Loop_" + name);
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = SoundBank.Get(name);
            s.loop = true;
            s.spatialBlend = 1f;
            s.minDistance = 2f;
            s.maxDistance = maxDistance;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.dopplerLevel = 0f;
            s.playOnAwake = false;
            ApplyBus(s, bus, volume);
            if (playNow && s.clip != null) s.Play();
            return s;
        }

        /// <summary>Registers a source so its volume follows the bus setting.</summary>
        public static void ApplyBus(AudioSource s, AudioBus bus, float volume)
        {
            if (I != null) { I.busOf[s] = bus; I.baseVolume[s] = volume; }
            s.volume = volume * BusVolume(bus);
        }

        public static void PlayMusic(string name, float volume = 1f)
        {
            if (I == null) return;
            var clip = SoundBank.Get(name);
            var current = I.musicOnA ? I.musicA : I.musicB;
            if (current.clip == clip && current.isPlaying) { I.musicTarget = volume; return; }
            I.musicOnA = !I.musicOnA;
            var target = I.musicOnA ? I.musicA : I.musicB;
            target.clip = clip;
            target.volume = 0f;
            if (clip != null) target.Play();
            I.musicTarget = volume;
        }

        public static void StopMusic() { if (I != null) I.musicTarget = 0f; }

        public static void PlayAmbience(string name, float volume = 1f)
        {
            if (I == null) return;
            var clip = name == null ? null : SoundBank.Get(name);
            I.ambience.Stop();
            I.ambience.clip = clip;
            ApplyBus(I.ambience, AudioBus.Ambience, volume);
            if (clip != null) I.ambience.Play();
        }

        public static int ActiveVoices
        {
            get
            {
                if (I == null) return 0;
                int n = 0;
                foreach (var s in I.pool) if (s.isPlaying) n++;
                return n;
            }
        }
    }
}
