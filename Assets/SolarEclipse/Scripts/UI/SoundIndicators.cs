using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Accessibility: visual sound indicators (Settings > Interface). Loud sounds near you (gunfire, explosions,
    /// screams, footsteps of attackers) show a labelled marker around the crosshair pointing where they came from.
    /// </summary>
    public static class SoundIndicators
    {
        struct Mark { public Vector3 Pos; public float Born; public string Label; public Color Col; }
        static readonly List<Mark> marks = new List<Mark>();
        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            GameEvents.Noise += OnNoise;
            UIRoot.HudLayer += Draw;
        }

        static void OnNoise(NoiseEvent e)
        {
            if (!Settings.Data.soundIndicators) return;
            var p = PlayerController.Instance;
            if (p == null || p.IsDead) return;
            if (e.Source is PlayerController || (e.Source is Weapon && PlayerTargets.IsPlayerSource(e.Source))) return; // your own noise
            float d = Vector3.Distance(p.transform.position, e.Position);
            if (d > Mathf.Max(e.Radius, 8f) || d < 1.5f) return;
            string label = e.Threat >= 0.9f ? "GUNFIRE" : e.Threat >= 0.5f ? "SCREAM" : "SOUND";
            if (e.Radius > 60f && e.Threat >= 0.9f) label = "EXPLOSION";
            var col = e.Threat >= 0.9f ? new Color(1f, 0.35f, 0.25f) : e.Threat >= 0.5f ? new Color(1f, 0.8f, 0.3f) : new Color(0.85f, 0.85f, 0.9f);
            for (int i = 0; i < marks.Count; i++) // merge repeats from the same place
                if ((marks[i].Pos - e.Position).sqrMagnitude < 9f && marks[i].Label == label) { var m = marks[i]; m.Born = Time.time; marks[i] = m; return; }
            if (marks.Count >= 8) marks.RemoveAt(0);
            marks.Add(new Mark { Pos = e.Position, Born = Time.time, Label = label, Col = col });
        }

        static void Draw()
        {
            if (marks.Count == 0) return;
            var p = PlayerController.Instance;
            if (p == null || p.Cam == null || !Settings.Data.soundIndicators) { marks.Clear(); return; }
            float W = UI.VW, H = UI.VH; var c = new Vector2(W / 2f, H / 2f);
            var cam = p.Cam.transform;
            for (int i = marks.Count - 1; i >= 0; i--)
            {
                var m = marks[i];
                float age = Time.time - m.Born;
                if (age > 2.5f) { marks.RemoveAt(i); continue; }
                var to = m.Pos - cam.position; to.y = 0f;
                var fwd = cam.forward; fwd.y = 0f;
                float ang = Vector3.SignedAngle(fwd, to, Vector3.up) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang));
                float a = Mathf.Clamp01((2.5f - age) / 0.6f);
                var at = c + dir * 190f;
                UI.Rounded(new Rect(at.x - 70, at.y - 18, 140, 36), new Color(0, 0, 0, 0.55f * a), true);
                UI.Label(new Rect(at.x - 70, at.y - 18, 140, 36), m.Label, 18, m.Col.WithAlpha(a), TextAnchor.MiddleCenter, FontStyle.Bold);
                var tip = c + dir * 160f;
                UI.Rounded(new Rect(tip.x - 6, tip.y - 6, 12, 12), m.Col.WithAlpha(a), true);
            }
        }
    }
}
