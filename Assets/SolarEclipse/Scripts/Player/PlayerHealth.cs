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

        public bool IsAlive => !Dead;

        public void TakeDamage(DamageInfo info)
        {
            if (NetState.Active && !NetManager.I.Server.PvP && info.Source is RemotePlayer) return; // PvP off on this server
            if (Dead || info.Amount <= 0) return;
            if (Rules.Invincible) { DamageFlash = Mathf.Max(DamageFlash, 0.15f); return; }
            if (info.Type == DamageType.Fall && Rules.NoFallDamage) return;
            Current -= info.Amount;
            DamageFlash = Mathf.Clamp01(DamageFlash + 0.25f + info.Amount / 60f);
            LastDamageDir = info.Direction;
            LastDamageTime = Time.time;
            CameraFX.Shake(Mathf.Clamp(info.Amount / 40f, 0.1f, 0.7f));
            CameraFX.Punch(-info.Amount * 0.15f, Random.Range(-1f, 1f) * info.Amount * 0.1f);
            AudioManager.Play2D("player_hurt", 0.8f, Random.Range(0.95f, 1.05f), AudioBus.Sfx);
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

        public void Revive()
        {
            Dead = false;
            Current = Max;
            DamageFlash = 0;
        }

        void Update()
        {
            DamageFlash = Mathf.MoveTowards(DamageFlash, 0, Time.deltaTime * 1.2f);
            if (!Dead && Current < Max && Time.time - LastDamageTime > 6f) Current = Mathf.Min(Max, Current + Time.deltaTime * 5f);
        }
    }
}
