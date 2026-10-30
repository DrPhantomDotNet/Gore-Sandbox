using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SolarEclipse.Mods
{
    /// <summary>Glue between the Advanced Modding editors and the Modding Hub's storage (developer B's ModStore).</summary>
    public static class AdvancedModding
    {
        /// <summary>Set by the Modding Hub: saves (and installs) a mod made in an Advanced editor. When nothing is set,
        /// the editor installs the mod for this session only.</summary>
        public static Action<ModPackage> SaveHook;

        public static void Save(ModPackage p)
        {
            if (p == null) return;
            if (string.IsNullOrEmpty(p.id)) p.id = NewId();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (p.created == 0) p.created = now;
            p.updated = now;
            if (SaveHook != null) { SaveHook(p); return; }
            ModHub.Handler(p.Kind)?.Install?.Invoke(p);
        }

        public static string NewId()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var sb = new StringBuilder();
            var r = new System.Random(Guid.NewGuid().GetHashCode());
            for (int i = 0; i < 12; i++) sb.Append(chars[r.Next(chars.Length)]);
            return sb.ToString();
        }

        /// <summary>Mod ids as they travel inside a preset string: letters, digits, '_' and '-' only.</summary>
        public static string SafeId(string id)
        {
            var sb = new StringBuilder();
            if (id != null) foreach (char c in id) { if (sb.Length >= 24) break; if (c < 128 && (char.IsLetterOrDigit(c) || c == '_' || c == '-')) sb.Append(c); }
            return sb.ToString();
        }
    }

    /// <summary>The last lines a script printed or failed with (shown in the script editor).</summary>
    public static class ModScriptLog
    {
        public static readonly List<string> Lines = new List<string>();
        public static void Add(string mod, string text)
        {
            text = ModChat.Clean(text);
            Lines.Add("[" + mod + "] " + text);
            while (Lines.Count > 40) Lines.RemoveAt(0);
        }
    }

    public static class ModChat
    {
        /// <summary>Printable ASCII only, max 120 chars, through the chat filter.</summary>
        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            foreach (char c in s) { if (sb.Length >= 120) break; if (c >= 32 && c < 127) sb.Append(c); }
            return ChatFilter.Apply(sb.ToString());
        }
    }

    /// <summary>The whitelisted sounds and effects scripts may use.</summary>
    public static class ModSounds
    {
        static string Clip(string n)
        {
            switch ((n ?? "").ToLowerInvariant())
            {
                case "scream": return "v_scream";
                case "yell": return "v_whoa";
                case "laugh": return "v_huh";
                case "hey": return "v_hey";
                case "no": return "v_no";
                case "argh": return "v_argh";
                case "growl": return "z_growl";
                case "shriek": return "z_shriek";
                case "beep": return "ui_notify";
                case "zap": return "gun_admin";
                case "boom": return "explosion_far";
                case "punch": return "punch_hit";
                case "slash": return "knife_hit_hard";
                case "gunshot": return "gun_glock";
                case "click": return "ui_click";
                case "whoosh": return "whoosh_small";
            }
            return null;
        }

        public static void Play(string name, Vector3 pos, Goredoll voice = null)
        {
            var clip = Clip(name);
            if (clip == null) return;
            if (voice != null && voice.Voice != null && (clip.StartsWith("v_") || clip.StartsWith("z_"))) { voice.Voice.Say(clip, 1f); return; }
            AudioManager.Play(clip, pos, 0.9f, UnityEngine.Random.Range(0.95f, 1.05f), 1f, 2f, 60f);
        }

        public static void Effect(string name, Vector3 pos, Vector3 normal)
        {
            switch ((name ?? "").ToLowerInvariant())
            {
                case "blood": if (Settings.GoreEnabled) BloodFX.Burst(pos, normal, 0.6f, Settings.BloodColor); break;
                case "sparks": FX.Sparks(pos, normal, 16, 7f); break;
                case "smoke": FX.Smoke(pos, Vector3.up * 0.6f, 0.5f, new Color(0.5f, 0.5f, 0.5f, 0.7f), 2f); break;
                case "fire": for (int i = 0; i < 4; i++) FX.Fire(pos + UnityEngine.Random.insideUnitSphere * 0.15f, Vector3.up * 0.8f, 0.35f); break;
                case "dust": FX.Dust(pos, 0.6f, new Color(0.6f, 0.55f, 0.5f), 10); break;
            }
        }
    }

    /// <summary>Floating speech bubbles above modded NPCs ("say").</summary>
    public sealed class SpeechBubbles : MonoBehaviour
    {
        sealed class Bubble { public Transform Anchor; public Vector3 Offset; public string Text; public float Until; }
        static SpeechBubbles inst;
        readonly List<Bubble> bubbles = new List<Bubble>();
        GUIStyle style;

        public static void Say(Transform anchor, Vector3 worldPos, string text)
        {
            if (string.IsNullOrEmpty(text) || anchor == null) return;
            if (inst == null)
            {
                var go = new GameObject("SpeechBubbles");
                DontDestroyOnLoad(go);
                inst = go.AddComponent<SpeechBubbles>();
            }
            inst.bubbles.RemoveAll(b => b.Anchor == anchor);
            inst.bubbles.Add(new Bubble { Anchor = anchor, Offset = worldPos - anchor.position + Vector3.up * 0.45f, Text = text, Until = Time.time + 2.5f + text.Length * 0.05f });
            if (inst.bubbles.Count > 16) inst.bubbles.RemoveAt(0);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            var cam = Camera.main;
            if (cam == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.MiddleCenter };
            style.fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height / 40f));
            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                var b = bubbles[i];
                if (b.Anchor == null || Time.time > b.Until) { bubbles.RemoveAt(i); continue; }
                var wp = b.Anchor.position + b.Offset;
                var sp = cam.WorldToScreenPoint(wp);
                if (sp.z <= 0.1f || sp.z > 40f) continue;
                float w = Mathf.Min(Screen.width * 0.4f, style.CalcSize(new GUIContent(b.Text)).x + 20f);
                float h = style.CalcHeight(new GUIContent(b.Text), w) + 8f;
                var r = new Rect(sp.x - w / 2f, Screen.height - sp.y - h, w, h);
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01(b.Until - Time.time));
                GUI.Box(r, b.Text, style);
                GUI.color = Color.white;
            }
        }
    }
}
