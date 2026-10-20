using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Everything a Goredoll says or breathes. Voice and breathing go silent the instant it is unconscious or dead
    /// (blood sounds come from elsewhere). Breathing speeds up with blood loss and gets heavier with every hit,
    /// and is always audible while paralysed. Neck wounds trigger choking.
    /// </summary>
    public sealed class GoredollVoice : MonoBehaviour
    {
        Goredoll doll;
        AudioSource voice, breath;
        float nextBreath, nextIdle, chokeUntil, nextChoke, nextCough, lastPain;
        bool inhale = true;
        float pitch;
        public string Subtitle { get; private set; }
        public float SubtitleUntil { get; private set; }

        static readonly Dictionary<string, string[]> Lines = new Dictionary<string, string[]>
        {
            { "v_scream", new[] { "AAAAAAH!", "AAARGH!", "AAAH! STOP!", "HELP! HELP ME!" } },
            { "v_no", new[] { "No! No no no!", "Please, no!", "Don't! Please!", "Noooo!" } },
            { "v_pain", new[] { "Ugh!", "Agh!", "Ngh!", "Oof!" } },
            { "v_argh", new[] { "Aaargh!", "Rrrgh!", "Gah!" } },
            { "v_ow", new[] { "Ow!", "Ouch!", "Ow ow ow!" } },
            { "v_moan", new[] { "Ohhh...", "Uhhh...", "It hurts...", "Mmmh..." } },
            { "v_whimper", new[] { "*whimpers*", "Please...", "Mom...", "I don't wanna die..." } },
            { "v_hey", new[] { "Hey!", "Hey, you!", "Hey there!" } },
            { "v_huh", new[] { "Huh?", "What was that?", "Hello?" } },
            { "v_whoa", new[] { "Whoa!", "Whoa whoa!" } },
            { "v_choke", new[] { "*choking*", "*gurgling*", "*gasping for air*" } },
            { "v_cough", new[] { "*coughs*" } },
            { "v_cough_wet", new[] { "*coughs up blood*" } },
            { "v_gasp", new[] { "*gasps*" } },
            { "v_hum", new[] { "~ Hmm hm hmm ~" } },
            { "v_whistle", new[] { "~ *whistles* ~" } },
            { "v_death", new[] { "Uhhhh..." } },
            { "v_sneeze", new[] { "Achoo!" } },
        };

        public void Init(Goredoll d, Transform head, Transform chest)
        {
            doll = d;
            pitch = Random.Range(0.9f, 1.12f);
            voice = MakeSource(head, "Voice");
            breath = MakeSource(chest, "Breath");
            nextIdle = Time.time + Random.Range(6f, 20f);
        }

        static AudioSource MakeSource(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<AudioSource>();
            s.spatialBlend = 1f;
            s.minDistance = 1.2f;
            s.maxDistance = 35f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.dopplerLevel = 0f;
            s.playOnAwake = false;
            AudioManager.ApplyBus(s, AudioBus.Voice, 1f);
            return s;
        }

        bool CanVocalise => doll != null && doll.Vitals.State == LifeState.Alive && !doll.Vitals.BrainDestroyed && !doll.Vitals.Decapitated;

        /// <summary>Plays a voice clip with a subtitle. Returns false if the Goredoll can't speak right now.</summary>
        /// <summary>Client copy: only speaks when the host says so (breathing stays local, driven by synced vitals).</summary>
        public bool Proxy;
        /// <summary>Zombie: no breathing, pain cries, chatter or coughing (zombie sounds come from ZombieBrain).</summary>
        public bool Undead;

        public bool Say(string clip, float volume = 1f, bool interrupt = true)
        {
            if (Proxy && !NetState.ApplyingRemote) return false;
            if (Undead && (clip == null || !clip.StartsWith("z_"))) return false; // zombies only make zombie sounds
            if (!CanVocalise || voice == null) return false;
            if (NetState.IsHost && doll != null) NetManager.I.BroadcastSay(doll, clip, volume, interrupt);
            if (!interrupt && voice.isPlaying) return false;
            var c = SoundBank.Get(clip);
            if (c == null) return false;
            voice.clip = c;
            voice.pitch = pitch * Random.Range(0.96f, 1.04f) * Mathf.Lerp(0.6f, 1f, Time.timeScale);
            AudioManager.ApplyBus(voice, AudioBus.Voice, volume);
            voice.Play();
            doll.Face?.Talk(c.length * 0.9f);
            if (Lines.TryGetValue(clip, out var lines)) ShowSubtitle(lines[Random.Range(0, lines.Length)], Mathf.Max(1.5f, c.length + 0.5f));
            GameEvents.EmitNoise(transform.position, clip == "v_scream" ? 30f : 12f, 0.3f, doll);
            return true;
        }

        public void ShowSubtitle(string text, float seconds)
        {
            Subtitle = text;
            SubtitleUntil = Time.time + seconds;
        }

        public void Silence()
        {
            if (voice != null) voice.Stop();
            if (breath != null) breath.Stop();
            Subtitle = null;
        }

        public void OnHurt(float damage, bool sharp)
        {
            if (Undead || !CanVocalise || Time.time - lastPain < 0.35f) return;
            lastPain = Time.time;
            if (chokeUntil > Time.time) return;
            string clip = damage > 45f ? (Random.value < 0.6f ? "v_scream" : "v_argh") : damage > 18f ? Rng.Pick(new[] { "v_argh", "v_ow", "v_pain" }) : "v_pain";
            Say(clip, Mathf.Clamp(0.6f + damage / 60f, 0.6f, 1f));
        }

        public void StartChoking()
        {
            if (Undead) return;
            chokeUntil = Time.time + Random.Range(6f, 12f);
            nextChoke = Time.time + 0.2f;
        }

        void Update()
        {
            if (doll == null) return;
            var v = doll.Vitals;
            if (!CanVocalise)
            {
                if (voice.isPlaying) voice.Stop();
                if (breath.isPlaying) breath.Stop();
                return;
            }

            if (Undead) return;
            // Choking after neck wounds.
            if (Time.time < chokeUntil && Time.time > nextChoke)
            {
                nextChoke = Time.time + Random.Range(1.6f, 2.6f);
                Say("v_choke", 1f);
                if (doll.Face != null) doll.Face.Expression = Expression.Choking;
            }

            // Wet coughing with lung damage.
            if (v.LungDamage > 0.5f && Time.time > nextCough && !voice.isPlaying)
            {
                nextCough = Time.time + Random.Range(4f, 9f);
                Say("v_cough_wet", 0.8f, false);
                if (Settings.GoreEnabled) BloodFX.Mouth(doll);
            }

            // Breathing
            if (Time.time >= nextBreath)
            {
                float period = 60f / Mathf.Max(6f, v.RespRate);
                float distress = Mathf.Clamp01((v.RespRate - 15f) / 22f) + v.HitsWhileParalyzed * 0.3f + v.Pain / 140f + v.BloodLossFraction * 2f;
                int level = Mathf.Clamp(Mathf.FloorToInt(distress * 3.2f), 0, 3);
                bool paralysed = v.Paralysis == Paralysis.Total;
                bool audible = paralysed || level >= 1 || doll.Brain != null && doll.Brain.Running;
                if (audible && !voice.isPlaying)
                {
                    bool wet = v.LungDamage > 0.5f || v.AirwayBlood > 0.4f;
                    string clip = wet && level >= 1 ? (inhale ? "breath_in_wet" : "breath_out_wet") : (inhale ? "breath_in_" : "breath_out_") + level;
                    breath.clip = SoundBank.Get(clip);
                    breath.pitch = pitch * Mathf.Lerp(1f, 1.15f, distress * 0.5f);
                    AudioManager.ApplyBus(breath, AudioBus.Voice, (0.35f + level * 0.22f) * (paralysed ? 1.2f : 1f));
                    if (breath.clip != null) breath.Play();
                    if (doll.Face != null) doll.Face.OpenMouth(inhale ? 0.15f + level * 0.12f : 0.08f + level * 0.08f);
                }
                nextBreath = Time.time + period * (inhale ? 0.45f : 0.55f);
                inhale = !inhale;
            }

            // Idle chatter when calm.
            if (Time.time > nextIdle && !voice.isPlaying)
            {
                nextIdle = Time.time + Random.Range(10f, 30f);
                if (v.Pain > 30f || v.BloodLossFraction > 0.1f) Say(Random.value < 0.5f ? "v_moan" : "v_whimper", 0.8f, false);
                else if (doll.Brain != null && doll.Brain.IsCalm) Say(Random.value < 0.08f ? "v_sneeze" : Random.value < 0.5f ? "v_hum" : "v_whistle", 0.6f, false);
            }
        }
    }
}
