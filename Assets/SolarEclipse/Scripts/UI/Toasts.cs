using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Notification toasts, achievement pop-ups and the kill feed.</summary>
    public sealed class Toasts : MonoBehaviour
    {
        struct Item { public string Title, Text; public Color Color; public float Time; public bool Achievement; }
        struct Feed { public string Text; public Color Color; public float Time; }

        static readonly List<Item> items = new List<Item>();
        static readonly List<Feed> feed = new List<Feed>();

        void OnEnable()
        {
            GameEvents.Toast += OnToast;
            GameEvents.KillFeed += OnKill;
            Stats.Unlocked += OnAchievement;
        }

        void OnDisable()
        {
            GameEvents.Toast -= OnToast;
            GameEvents.KillFeed -= OnKill;
            Stats.Unlocked -= OnAchievement;
        }

        static void OnToast(string text) => Show(null, text, UI.Accent2);

        static void OnKill(string text, Color color)
        {
            if (!Settings.Data.killFeed) return;
            feed.Add(new Feed { Text = text, Color = color, Time = Time.unscaledTime });
            if (feed.Count > 6) feed.RemoveAt(0);
        }

        static void OnAchievement(Achievement a)
        {
            items.Add(new Item { Title = "ACHIEVEMENT UNLOCKED", Text = a.Name + " — " + a.Description, Color = UI.Warn, Time = Time.unscaledTime, Achievement = true });
            AudioManager.Ui("ui_achievement");
        }

        public static void Show(string title, string text, Color color)
        {
            items.Add(new Item { Title = title, Text = text, Color = color, Time = Time.unscaledTime });
            if (items.Count > 5) items.RemoveAt(0);
            AudioManager.Ui("ui_notify", 0.6f);
        }

        public static void Draw()
        {
            float now = Time.unscaledTime;
            float y = 90;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                float age = now - it.Time;
                float life = it.Achievement ? 5f : 3.2f;
                if (age > life) { items.RemoveAt(i); continue; }
                float a = Mathf.Clamp01(age * 5f) * Mathf.Clamp01((life - age) * 3f);
                float w = 470, h = it.Title != null ? 84 : 56;
                float x = UI.VW - w - 24 + (1f - Mathf.Clamp01(age * 5f)) * 60f;
                var r = new Rect(x, y, w, h);
                UI.Shadow(r, 16, 0.5f * a);
                UI.Rounded(r, new Color(0.05f, 0.05f, 0.07f, 0.93f * a));
                UI.Fill(new Rect(r.x, r.y + 10, 5, r.height - 20), it.Color.WithAlpha(a));
                if (it.Achievement) UI.Tex(new Rect(r.x + 16, r.y + 14, 56, 56), IconFactory.Get("trophy"), UI.Warn.WithAlpha(a));
                float tx = r.x + (it.Achievement ? 84 : 22);
                if (it.Title != null)
                {
                    UI.Label(new Rect(tx, r.y + 8, w - (tx - r.x) - 10, 30), it.Title, 20, it.Color.WithAlpha(a), TextAnchor.MiddleLeft, FontStyle.Bold);
                    UI.Label(new Rect(tx, r.y + 38, w - (tx - r.x) - 14, 40), it.Text, 19, UI.TextColor.WithAlpha(a), TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                }
                else UI.Label(new Rect(tx, r.y, w - 30, h), it.Text, 21, UI.TextColor.WithAlpha(a), TextAnchor.MiddleLeft, FontStyle.Normal, true, true);
                y += h + 10;
            }

            float fy = 140;
            for (int i = feed.Count - 1; i >= 0; i--)
            {
                var f = feed[i];
                float age = now - f.Time;
                if (age > 6f) { feed.RemoveAt(i); continue; }
                float a = Mathf.Clamp01((6f - age) * 2f);
                float w = UI.TextWidth(f.Text, 20) + 30;
                var r = new Rect(24, fy, w, 34);
                UI.Rounded(r, new Color(0, 0, 0, 0.55f * a), true);
                UI.Label(new Rect(r.x + 14, r.y, w, 34), f.Text, 20, f.Color.WithAlpha(a));
                fy += 40;
            }
        }
    }
}
