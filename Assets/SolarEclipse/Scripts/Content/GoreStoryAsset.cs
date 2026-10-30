using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// A story mission made in the Unity editor: Assets > Create > Gore Sandbox > Story. Put it under a
    /// Resources/GoreContent folder and it shows up in PLAY STORY > MORE STORIES. Steps are edited in the Inspector
    /// (dialogue, choices, phone texts, cutscene camera shots, objectives, fights, endings).
    /// </summary>
    [CreateAssetMenu(fileName = "New Story", menuName = "Gore Sandbox/Story", order = 1)]
    public sealed class GoreStoryAsset : ScriptableObject
    {
        [Tooltip("Untick to hide it from the game without deleting it.")] public bool includeInGame = true;
        [Tooltip("Order in the MORE STORIES list (lower first).")] public int order;
        public StoryPack story = new StoryPack();

        /// <summary>A cleaned copy ready to play (built-in rules: Unlock steps allowed).</summary>
        public StoryPack ToPack(out string error)
        {
            var p = story != null ? story.Clone() : new StoryPack();
            error = StoryRules.Sanitize(p, false);
            p.builtIn = true;
            return p;
        }
    }
}
