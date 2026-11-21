using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Player hit points, damage feedback, slow regeneration, death and respawn.</summary>
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        public float Max = 100f;
        public float Current = 100f;
        public bool Dead;
        public float DamageFlash;
        public Vector3 LastDamageDir;
        public float LastDamageTime = -99f;
        public float RespawnAt;
        public string DeathCause = "";
        public float Bleed;           // GS2: HP lost per second until bandaged (H / tap the label)
        float nextBleedDrip;
        static int bandagedThisSession;

        public bool IsAlive => !Dead;

        public void TakeDamage(DamageInfo info)
        {
            if (NetState.Active && !NetManager.I.Server.PvP && info.Source is RemotePlayer) return; // PvP off on this server
            if (Dead || info.Amount <= 0) return;
            if (Rules.Invincible) { DamageFlash = Mathf.Max(DamageFlash, 0.15f); return; }
            if (info.Type == DamageType.Fall && Rules.NoFallDamage) return;
            if (info.Type == DamageType.Blunt || info.Type == DamageType.Slash || info.Type == DamageType.Stab) info.Amount *= Boxing.GuardMul; // boxing guard (RMB)
            if (StoryRunner.Active || StoryMode.Active) info.Amount *= Settings.Data.storyDifficulty == 0 ? 0.6f : Settings.Data.storyDifficulty == 2 ? 1.5f : 1f; // GS2 story difficulty
            Current -= info.Amount;
            DamageFlash = Mathf.Clamp01(DamageFlash + 0.25f + info.Amount / 60f);
            LastDamageDir = info.Direction;
            LastDamageTime = Time.time;
            CameraFX.Shake(Mathf.Clamp(info.Amount / 40f, 0.1f, 0.7f));
            if (VRManager.Active) { float hk = Mathf.Clamp(info.Amount / 40f, 0.3f, 1f); VRManager.Haptic(true, hk, 0.12f); VRManager.Haptic(false, hk, 0.12f); } // GS2 VR: getting hurt
#if (UNITY_ANDROID || UNITY_IOS) && !GS_REFCHECK // GS_REFCHECK: the offline compile check has no phone-only APIs
            if (Settings.Data.vibration && info.Amount >= 8f) Handheld.Vibrate(); // Gore Sandbox 2
#endif
            CameraFX.Punch(-info.Amount * 0.15f, Random.Range(-1f, 1f) * info.Amount * 0.1f);
            AudioManager.Play2D("player_hurt", 0.8f, Random.Range(0.95f, 1.05f), AudioBus.Sfx);
            if (Settings.GoreEnabled && !Rules.NoBleeding && info.Amount > 10f && (info.IsBallistic || info.Type == DamageType.Slash || info.Type == DamageType.Stab || info.Type == DamageType.Buckshot))
                Bleed = Mathf.Min(3f, Bleed + info.Amount * 0.02f);
            if (Current <= 0) Die(info.Type == DamageType.Fall ? "Fell to your death" : info.Type == DamageType.Explosion ? "Blown up" : info.Weapon ?? "Killed");
        }

        public void Die(string cause)
        {
            if (Dead) return;
            Dead = true;
            Current = 0;
            DeathCause = cause;
            RespawnAt = Time.time + 3.5f;
            Stats.Add(Stats.Deaths);
            AudioManager.Play2D("v_death", 0.8f, 1.1f, AudioBus.Sfx);
        }

        /// <summary>GS2: wrap your wounds. Stops the bleeding.</summary>
        public void Bandage()
        {
            if (Dead || Bleed <= 0f) return;
            Bleed = 0f;
            Stats.Add("bandages");
            if (++bandagedThisSession >= 5) EasterEggs.Found("Walking Wounded", "Bandaged yourself 5 times in one session.");
            AudioManager.Play2D("whoosh_small", 0.6f, 0.6f, AudioBus.Sfx);
            GameEvents.ShowToast("Bandaged. The bleeding stopped.");
        }

        public void Revive()
        {
            Bleed = 0f;
            Dead = false;
            Current = Max;
            DamageFlash = 0;
        }

        void Update()
        {
            DamageFlash = Mathf.MoveTowards(DamageFlash, 0, Time.deltaTime * 1.2f);
            if (Rules.Invincible) Bleed = 0f;
            if (!Dead && Bleed > 0f)
            {
                Current -= Bleed * Time.deltaTime;
                Bleed = Mathf.MoveTowards(Bleed, 0f, Time.deltaTime * 0.015f); // small wounds slowly clot
                if (Time.time >= nextBleedDrip && Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out var gh, 2.5f, Layers.DecalMask, QueryTriggerInteraction.Ignore))
                { nextBleedDrip = Time.time + Mathf.Lerp(1.6f, 0.3f, Bleed / 3f); Decals.Blood(gh.point, gh.normal, Random.Range(0.06f, 0.14f), 0, gh.collider); }
                if (!Application.isMobilePlatform && Input.GetKeyDown(KeyCode.H) && Settings.Data.pushToTalkKey != (int)KeyCode.H) Bandage();
                if (Current <= 0f) { Die("Bled out"); return; }
            }
            else if (!Dead && Current < Max && Time.time - LastDamageTime > 6f) Current = Mathf.Min(Max, Current + Time.deltaTime * 5f);
        }
    }
}
