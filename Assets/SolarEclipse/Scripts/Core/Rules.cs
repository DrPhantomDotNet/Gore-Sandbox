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
        public static bool LowGravity, Noir, Retro, Mirror;
        public static bool ForceEclipse;
        public static bool GunJams;      // realism: guns sometimes jam (Weapons/Firearm.cs) // next daytime outdoor map starts a Solar Eclipse after 8 s (World/Weather2.cs)
        public static bool PoliceResponse = true;      // frightened Goredolls call the police (Goredoll/Police.cs) // Gore Sandbox 2 fun modes (Player/ScreenFx.cs)
        public static float DamageMultiplier = 1f, BleedMultiplier = 1f, NpcHealthMultiplier = 1f, BulletForceMultiplier = 1f;
        public static float PlayerSpeedMultiplier = 1f, JumpMultiplier = 1f, NpcSpeedMultiplier = 1f, AiAggression = 0f, GravityScale = 1f;
        static float timeScale = 1f;
        /// <summary>GS2: base physics step from Settings > Graphics > Physics rate (30-120 Hz).</summary>
        public static float PhysicsStep => 1f / Mathf.Clamp(Settings.Data.physicsHz, 30, 120);

        public static float TimeScale
        {
            get => timeScale;
            set
            {
                timeScale = Mathf.Clamp(value, 0.05f, 3f);
                Time.timeScale = timeScale;
                Time.fixedDeltaTime = PhysicsStep * Mathf.Min(1f, timeScale);
            }
        }

        public static void ApplyGravity() => Physics.gravity = new Vector3(0f, -9.81f * GravityScale, 0f);

        public static void ResetAll()
        {
            NoFallDamage = Invincible = AiIgnorePlayer = InfiniteAmmo = NoReload = OneHitKill = false;
            NoclipAllowed = true;
            ShowAiThoughts = AlwaysDismember = SuperKick = ExplosiveBullets = BigHeads = NpcInfighting = SlowMoOnKill = ShowColliders = Xray = false;
            LowGravity = Noir = Retro = Mirror = false;
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
