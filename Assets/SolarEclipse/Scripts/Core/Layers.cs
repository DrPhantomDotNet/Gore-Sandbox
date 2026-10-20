using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Physics/render layers (names are set in ProjectSettings/TagManager.asset).</summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int Player = 8;
        public const int PlayerNoclip = 9;
        public const int Ragdoll = 10;
        public const int Props = 11;
        public const int Debris = 12;
        public const int Boundaries = 13;
        public const int Viewmodel = 14;
        public const int Effects = 15;
        public const int Nextbot = 16;
        public const int Trigger = 17;
        public const int RemotePlayer = 18;   // other players' bodies (multiplayer)
        public const int LocalHitbox = 19;    // host's own body, only hit by other players' shots

        public static int Mask(params int[] layers)
        {
            int m = 0;
            foreach (var l in layers) m |= 1 << l;
            return m;
        }

        /// <summary>What bullets and melee can hit.</summary>
        public static readonly int ShootMask = Mask(Default, Ragdoll, Props, Boundaries, Nextbot, RemotePlayer);
        /// <summary>Shots fired by remote players, simulated on the host: can also hit the host's own body.</summary>
        public static readonly int RemoteShootMask = Mask(Default, Ragdoll, Props, Boundaries, Nextbot, RemotePlayer, LocalHitbox);
        /// <summary>Static and solid things the player/NPCs stand on or are blocked by.</summary>
        public static readonly int WorldMask = Mask(Default, Props, Boundaries);
        /// <summary>Surfaces blood and decals stick to.</summary>
        public static readonly int DecalMask = Mask(Default, Props);
        /// <summary>Line of sight blockers for AI.</summary>
        public static readonly int SightMask = Mask(Default, Props);
        /// <summary>Things the spawn gun can grab.</summary>
        public static readonly int GrabMask = Mask(Default, Ragdoll, Props, Debris, Nextbot);

        public static void ConfigureCollisionMatrix()
        {
            for (int i = 0; i < 32; i++)
            {
                // Noclip player only collides with the boundaries.
                Physics.IgnoreLayerCollision(PlayerNoclip, i, i != Boundaries);
                Physics.IgnoreLayerCollision(Effects, i, true);
                Physics.IgnoreLayerCollision(Viewmodel, i, true);
                Physics.IgnoreLayerCollision(Trigger, i, i != Player && i != Ragdoll && i != Props);
                Physics.IgnoreLayerCollision(LocalHitbox, i, true); // raycast-only
            }
            Physics.IgnoreLayerCollision(Debris, Player, true);
            Physics.IgnoreLayerCollision(Debris, PlayerNoclip, true);
            Physics.IgnoreLayerCollision(Nextbot, Ragdoll, true);
            Physics.IgnoreLayerCollision(Nextbot, Props, true);
            Physics.IgnoreLayerCollision(Nextbot, Debris, true);
        }
    }
}
