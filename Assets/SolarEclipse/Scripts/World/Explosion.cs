using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Radial blast: physics impulse, damage with falloff and cover, fire/smoke/sparks, sound, shake, scorch.</summary>
    public static class Explosion
    {
        public static void Create(Vector3 pos, float radius, float damage, object source, float push = 1f)
        {
            if (NetState.IsClient)
            {
                // Clients only ever show explosions the host tells them about; their own requests go to the host.
                if (!NetManager.ApplyingExplosionFx)
                {
                    if (NetState.LocalAction && !NetState.ApplyingRemote) NetManager.I.RequestExplosion(pos, radius, damage, push);
                    return;
                }
                damage = 0f; push = 0f;
            }
            else if (NetState.IsHost) NetManager.I.BroadcastExplosion(pos, radius);
            FX.Explosion(pos, radius * 0.4f);
            AudioManager.Play("explosion", pos, 1f, Random.Range(0.92f, 1.05f), 1f, 6f, 250f);
            AudioManager.Play("explosion_far", pos, 0.6f, 1f, 1f, 40f, 600f);
            GameEvents.EmitNoise(pos, 140f, 1f, source);
            Stats.Add(Stats.Explosions);
            CameraFX.ShakeAt(pos, 1.1f, radius * 5f);
            if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out var ground, 3f, Layers.DecalMask, QueryTriggerInteraction.Ignore))
                Decals.Scorch(ground.point, ground.normal, radius * 0.9f, ground.collider);

            if (damage <= 0f && push <= 0f) return; // visual only
            var cols = Physics.OverlapSphere(pos, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            var bodies = new HashSet<Rigidbody>();
            var damaged = new HashSet<IDamageable>();
            foreach (var c in cols)
            {
                var closest = c.ClosestPoint(pos);
                float dist = Vector3.Distance(pos, closest);
                float falloff = Mathf.Clamp01(1f - dist / radius);
                var dir = (closest - pos).sqrMagnitude > 0.0001f ? (closest - pos).normalized : Random.onUnitSphere;
                // Cover: walls soak most of the blast.
                if (Physics.Linecast(pos, closest - dir * 0.05f, out var block, Layers.Mask(Layers.Default), QueryTriggerInteraction.Ignore) && block.collider != c)
                    falloff *= 0.25f;

                var rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic && bodies.Add(rb))
                    rb.AddForce((dir + Vector3.up * 0.6f).normalized * (radius * 3.2f * falloff * push), ForceMode.VelocityChange);

                var dmg = c.GetComponentInParent<IDamageable>();
                if (dmg != null && damaged.Add(dmg))
                {
                    dmg.TakeDamage(new DamageInfo
                    {
                        Amount = damage * falloff * falloff, Force = dir * damage * falloff * 0.3f, Point = closest, Normal = -dir, Direction = dir,
                        Type = DamageType.Explosion, Source = source, Weapon = "Explosion",
                    });
                }
            }
        }
    }
}
