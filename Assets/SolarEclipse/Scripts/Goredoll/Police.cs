using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2: frightened Goredolls call the police while they run. About half a minute later two armed officers
    /// arrive on foot and shoot back (Pause > World > "Police respond to calls"). Sandbox maps only; the Open World has
    /// its own police, and story missions and game modes are left alone.
    /// </summary>
    public static class Police
    {
        static float nextAllowed, arriveAt = -1f;

        public static void Call(Goredoll caller)
        {
            if (caller == null || !Rules.PoliceResponse || !Machines.Authority) return;
            if (OWDirector.Active || StoryRunner.Active || StoryMode.Active || ModeBase.Current != null) return;
            if (Time.time < nextAllowed || arriveAt > 0f) return;
            nextAllowed = Time.time + 150f;
            arriveAt = Time.time + Random.Range(25f, 40f);
            caller.Voice.ShowSubtitle("(on the phone) Police! Someone's killing people! Please hurry!", 4f);
            if (caller.Ragdoll != null) caller.Ragdoll.Pose = PoseMode.LookAround;
            GameEvents.ShowToast(caller.DisplayName + " called the police.");
            Ticker.Ensure();
        }

        static void Arrive()
        {
            arriveAt = -1f;
            var p = PlayerController.Instance;
            if (p == null || NavGrid.Current == null) return;
            for (int i = 0; i < 2; i++)
            {
                if (!NavGrid.Current.RandomPoint(p.transform.position, 28f, out var at)) continue;
                if ((at - p.transform.position).magnitude < 12f) at = p.transform.position + (at - p.transform.position).normalized * 18f;
                GoredollFactory.Spawn(at, Random.Range(0f, 360f), NpcTypes.Preset("cop"));
            }
            AudioManager.Play2D("siren", 0.5f, 1f, AudioBus.Sfx);
            GameEvents.ShowToast("The police have arrived.");
        }

        sealed class Ticker : MonoBehaviour
        {
            static Ticker I;
            public static void Ensure() { if (I == null) { var go = new GameObject("PoliceTicker"); I = go.AddComponent<Ticker>(); } }
            void Update() { if (arriveAt > 0f && Time.time >= arriveAt) Arrive(); }
        }
    }
}
