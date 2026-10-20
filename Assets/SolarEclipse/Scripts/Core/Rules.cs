using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>In-game cheats/modifiers editable from the Esc game settings and the dev panel. Reset per session.</summary>
    public static class Rules
    {
        public static bool NoFallDamage, Invincible, AiIgnorePlayer, InfiniteAmmo, NoReload, OneHitKill, NoclipAllowed = true;
        public static bool ShowAiThoughts, AlwaysDismember, SuperKick, ExplosiveBullets, BigHeads, NpcInfighting, SlowMoOnKill, ShowColliders, Xray;
        public static bool FreezeAi, NoBleeding, NoPain, InstantDeathOnHeadshot = true, BulletTracers = true, PlayerRagdollFall;
        public static float DamageMultiplier = 1f, BleedMultiplier = 1f, NpcHealthMultiplier = 1f, BulletForceMultiplier = 1f;
        public static float PlayerSpeedMultiplier = 1f, JumpMultiplier = 1f, NpcSpeedMultiplier = 1f, AiAggression = 0f, GravityScale = 1f;
        static float timeScale = 1f;

        public static float TimeScale
        {
            get => timeScale;
            set
            {
                timeScale = Mathf.Clamp(value, 0.05f, 3f);
                Time.timeScale = timeScale;
                Time.fixedDeltaTime = (1f / 60f) * Mathf.Min(1f, timeScale);
            }
        }

        public static void ApplyGravity() => Physics.gravity = new Vector3(0f, -9.81f * GravityScale, 0f);

        public static void ResetAll()
        {
            NoFallDamage = Invincible = AiIgnorePlayer = InfiniteAmmo = NoReload = OneHitKill = false;
            NoclipAllowed = true;
            ShowAiThoughts = AlwaysDismember = SuperKick = ExplosiveBullets = BigHeads = NpcInfighting = SlowMoOnKill = ShowColliders = Xray = false;
            FreezeAi = NoBleeding = NoPain = PlayerRagdollFall = false;
            InstantDeathOnHeadshot = BulletTracers = true;
            DamageMultiplier = BleedMultiplier = NpcHealthMultiplier = BulletForceMultiplier = 1f;
            PlayerSpeedMultiplier = JumpMultiplier = NpcSpeedMultiplier = GravityScale = 1f;
            AiAggression = 0f;
            TimeScale = 1f;
            ApplyGravity();
        }
    }
}
