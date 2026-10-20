using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Starts the game from any scene: no prefabs or scene setup required. The first scene that loads gets a
    /// persistent "GameRoot" with the <see cref="GameManager"/> that creates everything else in code.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (GameManager.I != null) return;
            var root = new GameObject("GameRoot");
            root.AddComponent<GameManager>();
        }
    }
}
