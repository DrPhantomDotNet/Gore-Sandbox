using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Real recorded voices (Gore Sandbox 2). Drop audio files into Assets/Resources/Voices/&lt;sound name&gt;/, for example
    /// Voices/v_scream/scream1.ogg or Voices/v_choke/choke_02.wav, and every Goredoll uses them instead of the
    /// synthesised voice for that sound. Sounds with no recordings keep the procedural voice.
    /// Sound names: v_scream v_no v_pain v_argh v_ow v_moan v_whimper v_hey v_huh v_whoa v_choke v_cough v_cough_wet
    /// v_gasp v_hum v_whistle v_death v_sneeze.
    /// </summary>
    public static class RealVoices
    {
        static readonly Dictionary<string, AudioClip[]> cache = new Dictionary<string, AudioClip[]>();
        static readonly Dictionary<string, int> last = new Dictionary<string, int>();

        public static AudioClip Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (!cache.TryGetValue(name, out var clips))
            {
                clips = Resources.LoadAll<AudioClip>("Voices/" + name);
                cache[name] = clips;
            }
            if (clips == null || clips.Length == 0) return null;
            int i = Random.Range(0, clips.Length);
            if (clips.Length > 1 && last.TryGetValue(name, out var l) && l == i) i = (i + 1) % clips.Length;
            last[name] = i;
            return clips[i];
        }
    }
}
