using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Farewell message, then the game closes.</summary>
    public sealed class QuitScreen : UIScreen
    {
        static readonly string[] Messages =
        {
            "Thanks for playing! The Goredolls will miss you... probably not.",
            "Goodbye! Don't forget to wash your hands.",
            "Leaving so soon? The ragdolls were just getting comfortable.",
            "See you next time. Somebody has to mop all this up.",
            "Farewell! Remember: it's just ketchup. Mostly.",
            "The Goredolls are finally safe. For now.",
            "Thanks for playing " + GameInfo.Title + "! — " + GameInfo.Studio,
        };
        readonly string message = Messages[Random.Range(0, Messages.Length)];
        bool quit;

        public override bool OnBack() => true;
        public override void OnOpen() { AudioManager.StopMusic(); AudioManager.Ui("logo_shimmer", 0.6f); }

        public override void Tick()
        {
            if (!quit && Age > 2.8f)
            {
                quit = true;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        public override void Draw()
        {
            float W = UI.VW, H = UI.VH;
            UI.Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, Mathf.Clamp01(Age * 3f)));
            float a = Mathf.Clamp01((Age - 0.2f) * 3f) * Mathf.Clamp01((2.7f - Age) * 2.5f);
            UI.Tex(new Rect(W / 2 - 60, H / 2 - 170, 120, 120), IconFactory.Get("eclipse"), UI.Accent2.WithAlpha(a));
            UI.Label(new Rect(80, H / 2 - 30, W - 160, 80), message, 36, UI.TextColor.WithAlpha(a), TextAnchor.MiddleCenter, FontStyle.Bold, true, true);
            UI.Label(new Rect(0, H / 2 + 60, W, 40), "See you soon.", 22, UI.Muted.WithAlpha(a), TextAnchor.MiddleCenter, FontStyle.Italic);
        }
    }
}
