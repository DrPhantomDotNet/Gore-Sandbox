using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2: when the game loses focus (alt-tab, a phone call, the home button) it opens the pause menu in
    /// singleplayer and mutes the audio until you come back (Settings > Pause when unfocused).
    /// </summary>
    public sealed class FocusPause : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { var go = new GameObject("FocusPause"); DontDestroyOnLoad(go); go.AddComponent<FocusPause>(); }

        void OnApplicationFocus(bool focus) => Handle(!focus);
        void OnApplicationPause(bool paused) => Handle(paused);

        static void Handle(bool away)
        {
            if (Settings.Data == null || !Settings.Data.pauseOnFocusLoss) { AudioListener.pause = false; return; }
            AudioListener.pause = away;
            if (!away) return;
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing || NetState.Active || UIRoot.AnyPausesTime || UIRoot.Count > 0) return;
            UIRoot.Push(new PauseMenuScreen());
        }
    }
}
