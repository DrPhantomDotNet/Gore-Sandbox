using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Nextbots: flat, always-facing-you images that hunt the player over the nav grid with a looping chase
    /// sound. Touching one is lethal (unless Invincible). They also bowl over Goredolls in the way.
    /// The faces are painted in code (original designs).
    /// </summary>
    public sealed class Nextbot : MonoBehaviour
    {
        public static readonly List<Nextbot> All = new List<Nextbot>();
        public string Id, DisplayName;
        public float Speed = 9f;
        readonly List<Vector3> path = new List<Vector3>();
        int pathIndex;
        float repath, stuckTimer, killCooldown, bob;
        AudioSource loop;
        Transform quad;
        Vector3 lastPos;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Init(Nextbots.Def def)
        {
            Id = def.Id; DisplayName = def.Name; Speed = def.Speed;
            quad = transform.GetChild(0);
            loop = AudioManager.CreateLoop("nextbot_chase", transform, 0.9f, AudioBus.Sfx, 45f);
            if (loop != null) loop.pitch = def.Pitch;
            lastPos = transform.position;
        }

        /// <summary>Client copy: only faces the camera and bobs; the host moves it and decides who it kills.</summary>
        public bool Proxy;

        void Update()
        {
            float dt = Time.deltaTime;
            var cam = Camera.main != null ? Camera.main.transform : null;
            bob += dt * 6f;
            if (cam != null)
            {
                var flat = MathX.Flat(cam.position - transform.position);
                if (flat.sqrMagnitude > 0.01f) quad.rotation = Quaternion.LookRotation(-flat.normalized, Vector3.up);
            }
            quad.localPosition = new Vector3(0, 1.2f + Mathf.Sin(bob) * 0.04f, 0);
            if (Proxy) return;

            var target = PlayerTargets.Nearest(transform.position);
            if (Rules.FreezeAi) { if (loop != null) loop.volume = 0.2f; return; }
            if (loop != null) loop.volume = 0.9f;

            var goal = Rules.AiIgnorePlayer || target == null ? RoamTarget() : target.Position;
            repath -= dt;
            if (repath <= 0f)
            {
                repath = 0.5f;
                path.Clear();
                pathIndex = 0;
                if (NavGrid.Current == null || !NavGrid.Current.Ready || !NavGrid.Current.FindPath(transform.position, goal, path)) { path.Clear(); path.Add(goal); }
            }
            if (pathIndex < path.Count)
            {
                var wp = path[pathIndex];
                var to = MathX.Flat(wp - transform.position);
                if (to.magnitude < 0.6f) pathIndex++;
                else
                {
                    var step = to.normalized * Speed * Rules.NpcSpeedMultiplier * dt;
                    var next = transform.position + step;
                    if (Physics.Raycast(next + Vector3.up * 1.5f, Vector3.down, out var g, 4f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) next.y = Mathf.Lerp(next.y, g.point.y, 0.5f);
                    transform.position = next;
                }
            }

            if ((transform.position - lastPos).sqrMagnitude < 0.0004f) stuckTimer += dt; else stuckTimer = 0f;
            lastPos = transform.position;
            if (stuckTimer > 2f) { transform.position = Vector3.MoveTowards(transform.position, goal, 3f); stuckTimer = 0f; }

            // Touching any player is lethal.
            killCooldown -= dt;
            if (target != null && !Rules.AiIgnorePlayer && killCooldown <= 0f && MathX.Flat(target.Position - transform.position).magnitude < 0.9f
                && Mathf.Abs(target.Position.y - transform.position.y) < 2f)
            {
                killCooldown = 2f;
                bool local = target is PlayerController;
                if (local)
                {
                    AudioManager.Play2D("jumpscare", 1f, 1f, AudioBus.Sfx);
                    CameraFX.Flash(new Color(0.1f, 0f, 0f), 0.85f);
                    CameraFX.Shake(1f);
                    Stats.Add("nextbot_deaths");
                }
                target.Damageable?.TakeDamage(new DamageInfo { Amount = 1000f, Type = DamageType.Crush, Source = this, Weapon = DisplayName, Point = target.Position, Direction = (target.Position - transform.position).normalized });
            }

            foreach (var d in Goredoll.All)
            {
                if (d == null || d.IsDead) continue;
                var off = d.Position - transform.position;
                if (MathX.Flat(off).magnitude < 0.8f && Mathf.Abs(off.y) < 2f)
                {
                    d.Launch(MathX.Flat(off).normalized * 14f + Vector3.up * 6f);
                    d.Parts[PartType.Chest].TakeDamage(new DamageInfo { Amount = 40f, Type = DamageType.Blunt, Point = d.Position, Direction = off.normalized, Source = this, Weapon = DisplayName });
                }
            }
        }

        Vector3 roam;
        float roamTimer;
        Vector3 RoamTarget()
        {
            roamTimer -= Time.deltaTime;
            if (roamTimer <= 0f || (roam - transform.position).sqrMagnitude < 1f)
            {
                roamTimer = 6f;
                if (NavGrid.Current == null || !NavGrid.Current.RandomPoint(transform.position, 25f, out roam)) roam = transform.position + Random.insideUnitSphere * 10f;
            }
            return roam;
        }
    }

    public static class Nextbots
    {
        public sealed class Def
        {
            public string Id, Name, Description;
            public float Speed = 9f, Pitch = 1f;
            public System.Action<PixelCanvas> Paint;
        }

        public static readonly List<Def> Defs = new List<Def>
        {
            new Def { Id = "grinner", Name = "The Grinner", Description = "Too many teeth. Fast.", Speed = 10f, Pitch = 1f, Paint = Grinner },
            new Def { Id = "stare", Name = "The Stare", Description = "It never blinks. Slow, but it never stops.", Speed = 6.5f, Pitch = 0.75f, Paint = Stare },
            new Def { Id = "eclipse", Name = "Eclipse", Description = "A black sun with a face. Very fast.", Speed = 12.5f, Pitch = 1.25f, Paint = Eclipse },
            new Def { Id = "melon", Name = "Melon Head", Description = "Revenge for every watermelon you shot.", Speed = 8.5f, Pitch = 1.1f, Paint = Melon },
            new Def { Id = "static", Name = "Static", Description = "A face made of TV noise.", Speed = 9.5f, Pitch = 0.9f, Paint = Static },
        };

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        public static Def Get(string id) => Defs.Find(d => d.Id == id) ?? Defs[0];

        public static Texture2D Face(string id)
        {
            var def = Get(id);
            var key = "nextbot_" + def.Id;
            if (IconFactory.Has(key)) return IconFactory.Get(key);
            var c = new PixelCanvas(256, 256, new Color(0, 0, 0, 0));
            def.Paint(c);
            var tex = c.ToTexture(key);
            IconFactory.Register(key, tex);
            return tex;
        }

        public static Nextbot Spawn(string id, Vector3 pos)
        {
            var def = Get(id);
            var go = new GameObject("Nextbot " + def.Name);
            go.layer = Layers.Nextbot;
            go.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, false);
            go.transform.position = pos;
            var q = new GameObject("Face");
            q.layer = Layers.Nextbot;
            q.transform.SetParent(go.transform, false);
            var mb = new MeshBuilder();
            mb.Face(Vector3.zero, Vector3.back, Vector3.up, 1.1f, 1.1f);
            mb.Face(Vector3.zero, Vector3.forward, Vector3.up, 1.1f, 1.1f);
            q.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("NextbotQuad");
            if (!mats.TryGetValue(def.Id, out var mat) || mat == null)
            {
                mat = Mats.ParticleWith(Face(def.Id));
                mats[def.Id] = mat;
            }
            q.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var col = go.AddComponent<CapsuleCollider>();
            col.center = Vector3.up * 1.2f; col.height = 2.2f; col.radius = 0.6f;
            col.isTrigger = true;
            var nb = go.AddComponent<Nextbot>();
            nb.Init(def);
            Stats.Add(Stats.Nextbots);
            if (NetState.IsHost) NetManager.I.OnNextbotSpawned(nb, def.Id);
            return nb;
        }

        // ---------------------------------------------------------------- face painters (256x256, y up)

        static void Grinner(PixelCanvas c)
        {
            c.Circle(128, 128, 120, new Color(0.95f, 0.85f, 0.2f));
            c.Circle(88, 160, 26, Color.white); c.Circle(168, 160, 26, Color.white);
            c.Circle(92, 156, 10, Color.black); c.Circle(164, 156, 10, Color.black);
            c.Circle(128, 80, 70, new Color(0.25f, 0.02f, 0.02f));
            c.Rect(50, 82, 156, 60, new Color(0.95f, 0.85f, 0.2f));
            for (int i = 0; i < 12; i++) c.Rect(66 + i * 10.5f, 60, 8, 22, Color.white);
            for (int i = 0; i < 10; i++) c.Rect(76 + i * 10.5f, 34, 8, 18, Color.white);
        }

        static void Stare(PixelCanvas c)
        {
            c.Circle(128, 128, 118, new Color(0.88f, 0.86f, 0.84f));
            c.Circle(84, 150, 34, Color.black); c.Circle(172, 150, 34, Color.black);
            c.Circle(84, 150, 5, Color.white); c.Circle(172, 150, 5, Color.white);
            c.Line(96, 70, 160, 70, 4, new Color(0.2f, 0.2f, 0.2f));
            c.Ring(128, 128, 118, 6, new Color(0.3f, 0.3f, 0.3f));
        }

        static void Eclipse(PixelCanvas c)
        {
            for (int r = 128; r > 96; r -= 2) c.Circle(128, 128, r, new Color(1f, 0.55f, 0.15f, 0.08f), 1f);
            c.Circle(128, 128, 98, new Color(0.02f, 0.02f, 0.03f));
            c.Ring(128, 128, 98, 4, new Color(1f, 0.85f, 0.5f));
            c.Circle(96, 146, 14, new Color(1f, 0.3f, 0.1f)); c.Circle(160, 146, 14, new Color(1f, 0.3f, 0.1f));
            c.Line(86, 92, 128, 76, 6, new Color(1f, 0.3f, 0.1f)); c.Line(128, 76, 170, 92, 6, new Color(1f, 0.3f, 0.1f));
        }

        static void Melon(PixelCanvas c)
        {
            c.Circle(128, 128, 120, new Color(0.15f, 0.45f, 0.15f));
            for (int i = 0; i < 7; i++) c.Line(40 + i * 30, 20, 40 + i * 30 + 10, 236, 8, new Color(0.08f, 0.3f, 0.08f));
            c.Circle(128, 128, 100, new Color(0.9f, 0.2f, 0.25f));
            for (int i = 0; i < 14; i++) { float a = i * 0.45f; c.Circle(128 + Mathf.Cos(a) * 60, 128 + Mathf.Sin(a) * 60, 5, Color.black); }
            c.Circle(100, 150, 16, Color.white); c.Circle(156, 150, 16, Color.white);
            c.Circle(100, 150, 7, Color.black); c.Circle(156, 150, 7, Color.black);
            c.Ring(128, 92, 30, 6, new Color(0.3f, 0f, 0f));
        }

        static void Static(PixelCanvas c)
        {
            var rng = new System.Random(7);
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float dx = x - 128, dy = y - 128;
                    if (dx * dx + dy * dy > 120 * 120) continue;
                    float v = (float)rng.NextDouble();
                    c.Set(x, y, new Color(v, v, v));
                }
            c.Rect(70, 135, 40, 30, Color.black); c.Rect(146, 135, 40, 30, Color.black);
            c.Rect(80, 60, 96, 24, Color.black);
        }
    }
}
