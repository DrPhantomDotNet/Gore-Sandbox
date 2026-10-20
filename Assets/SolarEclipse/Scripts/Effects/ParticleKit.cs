using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Helpers to build always-alive, world-space particle systems that are fed with Emit() calls.
    /// One system per effect type serves every instance in the scene (cheap and pool-free).
    /// </summary>
    public static class ParticleKit
    {
        public static ParticleSystem Create(string name, Transform parent, Material mat, int maxParticles, float gravity,
            ParticleSystemRenderMode mode = ParticleSystemRenderMode.Billboard, float lengthScale = 1f, float velocityScale = 0f)
        {
            var go = new GameObject("PS_" + name);
            go.transform.SetParent(parent, false);
            go.layer = Layers.Effects;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.gravityModifier = gravity;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.startSize = 0.1f;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = mode;
            if (mode == ParticleSystemRenderMode.Stretch) { r.lengthScale = lengthScale; r.velocityScale = velocityScale; }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = 10;
            ps.Play();
            return ps;
        }

        public static void FadeOut(ParticleSystem ps, float startAlpha = 1f)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(startAlpha, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        public static void SizeOverLife(ParticleSystem ps, float start, float end)
        {
            var s = ps.sizeOverLifetime;
            s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, start, 1, end));
        }

        public static void Collide(ParticleSystem ps, float bounce, float lifetimeLoss, int mask, bool sendMessages, float radiusScale = 0.5f)
        {
            var c = ps.collision;
            c.enabled = true;
            c.type = ParticleSystemCollisionType.World;
            c.mode = ParticleSystemCollisionMode.Collision3D;
            c.bounce = bounce;
            c.lifetimeLoss = lifetimeLoss;
            c.dampen = 0.4f;
            c.collidesWith = mask;
            c.sendCollisionMessages = sendMessages;
            c.radiusScale = radiusScale;
            c.quality = ParticleSystemCollisionQuality.Medium;
            c.maxCollisionShapes = 64;
        }

        public static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life, Color color, float rotation = 0f)
        {
            if (ps == null) return;
            var p = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startSize = size,
                startLifetime = life,
                startColor = color,
                rotation = rotation,
                applyShapeToPosition = false,
            };
            ps.Emit(p, 1);
        }
    }
}
