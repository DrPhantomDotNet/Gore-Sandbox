using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Hitscan bullets with penetration: through-and-through wounds (entry + exit), exit blood splattering the
    /// wall behind, over-penetration into the next body, thin wood/glass penetration, ricochets, impacts and tracers.
    /// </summary>
    public static class Ballistics
    {
        static readonly Dictionary<BodyPart, float> shotDamage = new Dictionary<BodyPart, float>();

        public static void BeginShot() => shotDamage.Clear();

        /// <summary>After all pellets of a shotgun blast: big totals pop heads and tear off limbs.</summary>
        public static void EndShot(Vector3 dir)
        {
            foreach (var kv in shotDamage)
            {
                var part = kv.Key;
                if (part == null || part.Gibbed || part.Doll == null || !Settings.Dismemberment) continue;
                if (part.IsHead && kv.Value > 100f) part.Doll.Dismember.Gib(part, dir);
                else if (part.CanSever && kv.Value > part.MaxHealth * 1.2f) part.Doll.Dismember.Sever(part, part.transform.position, -dir, false);
            }
            shotDamage.Clear();
        }

        /// <param name="applyDamage">False on multiplayer clients: draw impacts and tracers only; the host does the real shot.</param>
        /// <param name="mask">Layer mask (0 = <see cref="Layers.ShootMask"/>).</param>
        public static Vector3 Fire(Vector3 origin, Vector3 dir, WeaponDef def, object source, Vector3 tracerFrom, DamageType type = DamageType.Bullet, bool tracer = true, bool applyDamage = true, int mask = 0)
        {
            float pen = def.Penetration, dmg = def.Damage, remaining = def.Range;
            if (mask == 0) mask = Layers.ShootMask;
            var pos = origin;
            var end = origin + dir * remaining;
            for (int i = 0; i < 8 && remaining > 0.01f; i++)
            {
                if (!Physics.Raycast(pos, dir, out var hit, remaining, mask, QueryTriggerInteraction.Ignore)) { end = pos + dir * remaining; break; }
                end = hit.point;
                remaining -= hit.distance;
                var part = hit.collider.GetComponent<BodyPart>();
                if (!applyDamage && (part != null || hit.collider.GetComponentInParent<RemotePlayer>() != null)) break; // the host's damage event draws the blood
                var other = hit.collider.GetComponentInParent<RemotePlayer>();
                if (other != null || hit.collider.gameObject.layer == Layers.LocalHitbox)
                {
                    hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo { Amount = dmg, Force = dir * def.Force, Point = hit.point, Normal = hit.normal, Direction = dir, Type = type, Source = source, Weapon = def.Name, Caliber = def.Caliber });
                    break;
                }
                if (part != null)
                {
                    float d = dmg * (def.Pellets > 1 && Vector3.Distance(origin, hit.point) < 5f ? 1.5f : 1f);
                    part.TakeDamage(new DamageInfo { Amount = d, Force = dir * def.Force, Point = hit.point, Normal = hit.normal, Direction = dir, Type = type, Source = source, Weapon = def.Name, Caliber = def.Caliber, Penetration = pen });
                    shotDamage.TryGetValue(part, out var acc);
                    shotDamage[part] = acc + d;
                    // Find the exit point on the far side of this body part.
                    var back = new Ray(hit.point + dir * 0.8f, -dir);
                    if (part.Col != null && part.Col.enabled && pen > 0.15f && part.Col.Raycast(back, out var exit, 0.8f))
                    {
                        float thickness = Vector3.Distance(hit.point, exit.point);
                        part.TakeDamage(new DamageInfo { Amount = d * 0.12f, Point = exit.point, Normal = exit.normal, Direction = dir, Type = type, Source = source, Weapon = def.Name, Caliber = def.Caliber, IsExit = true });
                        pen -= thickness * 2f + 0.15f;
                        dmg *= 0.7f;
                        pos = exit.point + dir * 0.005f;
                        if (pen <= 0f) break;
                        continue;
                    }
                    break;
                }

                var surface = Surface.Of(hit.collider);
                if (hit.collider.gameObject.layer == Layers.Boundaries) { end = hit.point; break; }
                FX.Impact(hit.point, hit.normal, surface);
                if (hit.rigidbody == null || hit.collider.gameObject.layer == Layers.Props) Decals.BulletHole(hit.point, hit.normal, surface, hit.collider, def.Caliber > 8f ? 0.11f : 0.08f);
                string clip = surface == SurfaceType.Metal ? "impact_metal" : surface == SurfaceType.Wood ? "impact_wood" : surface == SurfaceType.Dirt || surface == SurfaceType.Grass ? "impact_dirt" : surface == SurfaceType.Glass ? "glass_break" : "impact_concrete";
                AudioManager.Play(clip, hit.point, 0.7f, Random.Range(0.9f, 1.1f), 1f, 2f, 60f);
                if (applyDamage)
                {
                    hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo { Amount = dmg, Force = dir * def.Force * 0.5f, Point = hit.point, Normal = hit.normal, Direction = dir, Type = type, Source = source, Weapon = def.Name, Caliber = def.Caliber });
                    if (hit.rigidbody != null && !hit.rigidbody.isKinematic) hit.rigidbody.AddForceAtPosition(dir * def.Force * 0.5f, hit.point, ForceMode.Impulse);
                    GameEvents.EmitNoise(hit.point, 8f, 0.6f, source);
                }

                float angle = Vector3.Angle(-dir, hit.normal);
                if (surface == SurfaceType.Metal && angle > 70f && Random.value < 0.5f)
                {
                    AudioManager.Play("ricochet", hit.point, 0.6f);
                    FX.Sparks(hit.point, Vector3.Reflect(dir, hit.normal), 8, 10f);
                    break;
                }
                if ((surface == SurfaceType.Wood || surface == SurfaceType.Glass) && pen > 0.3f)
                {
                    var through = new Ray(hit.point + dir * 0.6f, -dir);
                    if (hit.collider.Raycast(through, out var exitHit, 0.6f))
                    {
                        pen -= 0.35f;
                        dmg *= 0.75f;
                        pos = exitHit.point + dir * 0.005f;
                        continue;
                    }
                }
                break;
            }
            if (applyDamage && Rules.ExplosiveBullets) Explosion.Create(end, 2.5f, 60f, source, 0.6f);
            if (tracer && Rules.BulletTracers) Tracers.Spawn(tracerFrom, end, type == DamageType.Admin ? new Color(0.4f, 1f, 1f) : new Color(1f, 0.85f, 0.5f));
            if (applyDamage) NearMisses(origin, end, source);
            return end;
        }

        static void NearMisses(Vector3 a, Vector3 b, object source)
        {
            foreach (var g in Goredoll.All)
            {
                if (!g.Vitals.Conscious) continue;
                var head = g.Position + Vector3.up * 0.7f;
                var ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(head - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
                var closest = a + ab * t;
                if (Vector3.Distance(closest, head) < 1.6f)
                {
                    GameEvents.EmitNoise(closest, 4f, 1f, source);
                    if (Random.value < 0.3f) AudioManager.Play("whiz", closest, 0.6f, Random.Range(0.9f, 1.1f), 1f, 1f, 10f);
                }
            }
        }
    }

    /// <summary>Fading additive bullet tracers.</summary>
    public static class Tracers
    {
        static readonly List<(LineRenderer lr, float born)> active = new List<(LineRenderer, float)>();
        static readonly Queue<LineRenderer> pool = new Queue<LineRenderer>();
        static Transform root;

        public static void Spawn(Vector3 from, Vector3 to, Color color)
        {
            if (root == null)
            {
                root = new GameObject("Tracers").transform;
                Object.DontDestroyOnLoad(root.gameObject);
                root.gameObject.AddComponent<TracerUpdater>();
            }
            var lr = pool.Count > 0 ? pool.Dequeue() : null;
            if (lr == null)
            {
                var go = new GameObject("Tracer");
                go.layer = Layers.Effects;
                go.transform.SetParent(root, false);
                lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = Mats.AdditiveWith(TextureFactory.Spark);
                lr.positionCount = 2;
                lr.numCapVertices = 0;
                lr.textureMode = LineTextureMode.Stretch;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
            }
            lr.gameObject.SetActive(true);
            // Start a little in front of the muzzle so it reads as a streak, not a laser.
            var dir = (to - from).normalized;
            float len = Vector3.Distance(from, to);
            var start = from + dir * Mathf.Min(1.5f, len * 0.1f);
            lr.SetPosition(0, start);
            lr.SetPosition(1, to);
            lr.startColor = color.WithAlpha(0f);
            lr.endColor = color;
            lr.widthMultiplier = 0.025f;
            active.Add((lr, Time.time));
        }

        internal static void Tick()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var (lr, born) = active[i];
                float t = (Time.time - born) / 0.09f;
                if (lr == null) { active.RemoveAt(i); continue; }
                if (t >= 1f) { lr.gameObject.SetActive(false); pool.Enqueue(lr); active.RemoveAt(i); continue; }
                var c = lr.endColor; c.a = 1f - t; lr.endColor = c;
                lr.widthMultiplier = 0.025f * (1f - t * 0.5f);
            }
        }

        sealed class TracerUpdater : MonoBehaviour { void LateUpdate() => Tick(); }
    }

    /// <summary>Ejected brass and shotgun shells that bounce and tinkle.</summary>
    public static class Casings
    {
        static Mesh pistolMesh, rifleMesh, shellMesh;

        public static void Eject(Vector3 pos, Vector3 right, Vector3 up, Vector3 inheritVel, int type)
        {
            if (Settings.Data.maxCasings <= 0) return;
            var go = new GameObject("Casing");
            go.layer = Layers.Debris;
            go.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(right) * Quaternion.Euler(Random.Range(-20, 20), 90, 0));
            go.AddComponent<MeshFilter>().sharedMesh = MeshFor(type);
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            var cc = go.AddComponent<CapsuleCollider>();
            cc.direction = 2;
            cc.radius = type == 2 ? 0.011f : 0.0055f;
            cc.height = type == 2 ? 0.07f : type == 1 ? 0.045f : 0.02f;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = type == 2 ? 0.04f : 0.012f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            RB.SetVel(rb, inheritVel + right * Random.Range(2.2f, 3.4f) + up * Random.Range(1.2f, 2.2f));
            rb.angularVelocity = Random.insideUnitSphere * 25f;
            go.AddComponent<CasingSound>().Shell = type == 2;
            Debris.Register(go, 25f, Debris.GroupCasing);
        }

        static Mesh MeshFor(int type)
        {
            if (type == 2)
            {
                if (shellMesh != null) return shellMesh;
                var mb = new MeshBuilder { Color = new Color(0.75f, 0.12f, 0.1f), Gloss = 0.4f };
                mb.Cylinder(new Vector3(0, 0, -0.025f), new Vector3(0, 0, 0.035f), 0.0105f, 0.0105f, 10);
                mb.SetColor(new Color(0.8f, 0.62f, 0.25f), 0.85f);
                mb.Cylinder(new Vector3(0, 0, -0.035f), new Vector3(0, 0, -0.025f), 0.011f, 0.011f, 10);
                return shellMesh = mb.ToMesh("Shell");
            }
            if (type == 1 && rifleMesh != null) return rifleMesh;
            if (type == 0 && pistolMesh != null) return pistolMesh;
            var m = new MeshBuilder { Color = new Color(0.82f, 0.64f, 0.28f), Gloss = 0.9f };
            float len = type == 1 ? 0.045f : 0.019f;
            float r = type == 1 ? 0.0048f : 0.0049f;
            m.Cylinder(new Vector3(0, 0, -len / 2), new Vector3(0, 0, len / 2 - (type == 1 ? 0.01f : 0)), r, r, 8);
            if (type == 1) m.Cylinder(new Vector3(0, 0, len / 2 - 0.01f), new Vector3(0, 0, len / 2), r, r * 0.65f, 8);
            var mesh = m.ToMesh("Casing");
            if (type == 1) rifleMesh = mesh; else pistolMesh = mesh;
            return mesh;
        }

        sealed class CasingSound : MonoBehaviour
        {
            public bool Shell;
            int bounces;
            void OnCollisionEnter(Collision c)
            {
                if (bounces++ > 2 || c.relativeVelocity.magnitude < 0.6f) return;
                if (bounces == 1) AudioManager.Play(Shell ? "shell_casing" : "casing", transform.position, 0.45f, Random.Range(0.92f, 1.1f), 1f, 1f, 15f);
            }
        }
    }
}
