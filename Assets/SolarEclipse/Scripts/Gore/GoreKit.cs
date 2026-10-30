using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// The Gore Update (v0.9.0): extra gore on top of wounds / dismemberment / gibs. Teeth knocked out, bones breaking
    /// through skin, burnt skin, blood sprayed on the wall behind a hit, blood on the screen, spreading blood pools,
    /// bloody footprints, drag smears. Everything follows the gore level (Settings.GoreEnabled / Organs / Unreal /
    /// SuperUltra) and the phone limits (decal and gib caps).
    /// </summary>
    public static class GoreKit
    {
        public const string StatTeeth = "teeth_lost", StatHeads = "heads_popped", StatPools = "blood_pools", StatBones = "compound_fractures", StatCharred = "charred";

        /// <summary>How many more chunks big gore makes (High 1x, Unreal 1.6x, Super Ultra 2.5x).</summary>
        public static float ChunkMul => Settings.SuperUltra ? 2.5f : Settings.Unreal ? 1.6f : 1f;

        static bool registered;

        public static void EnsureAchievements()
        {
            if (registered) return;
            registered = true;
            void Add(string id, string name, string desc, string stat, float target)
            {
                if (!Stats.Achievements.Exists(a => a.Id == id)) Stats.Achievements.Add(new Achievement(id, name, desc, stat, target));
            }
            Add("gore_teeth", "Dentist", "Knock out 50 teeth.", StatTeeth, 50);
            Add("gore_heads", "Pop Goes the Head", "Pop 25 heads.", StatHeads, 25);
            Add("gore_pools", "Blood Bath", "Leave 50 pools of blood.", StatPools, 50);
            Add("gore_bones", "Compound Interest", "Break 20 bones so badly they come through the skin.", StatBones, 20);
            Add("gore_charred", "Well Done", "Burn 30 body parts with explosions.", StatCharred, 30);
            Add("gore_super", "SUPER ULTRA", "Play a map on the Super Ultra gore level.", "gore_super", 1);
        }

        // ------------------------------------------------------------------ teeth

        /// <summary>Teeth fly out of the mouth (punches, bats, kicks and bullets to the face).</summary>
        public static void KnockTeeth(BodyPart head, Vector3 dir, int count)
        {
            if (head == null || !Settings.Organs || count <= 0 || head.Gibbed) return;
            var t = head.transform;
            var mouth = t.TransformPoint(new Vector3(0, HumanModel.HeadCenterY - 0.07f, 0.11f));
            var vel = head.Body != null ? RB.Vel(head.Body) : Vector3.zero;
            for (int i = 0; i < count; i++)
                OrganModels.Chunk("Tooth", OrganModels.Tooth(Random.value < 0.3f ? 0 : 1), mouth + Random.insideUnitSphere * 0.015f, 0.005f, 0.006f,
                    vel + (dir.normalized * 0.6f + t.forward * 0.6f + Random.insideUnitSphere * 0.5f) * Random.Range(1.5f, 4f), 45f);
            BloodFX.Burst(mouth, t.forward, 0.6f * Settings.BloodScale, Settings.BloodColor);
            Stats.Add(StatTeeth, count);
        }

        // ------------------------------------------------------------------ wall spray

        /// <summary>Blood thrown onto whatever is behind the hit (exit wounds, big hits): a splat plus droplets.</summary>
        public static void WallSpray(Vector3 point, Vector3 dir, float damage)
        {
            if (!Settings.GoreEnabled || dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();
            float reach = Mathf.Clamp(1.5f + damage / 25f, 1.5f, 6f) * (Settings.SuperUltra ? 1.5f : 1f);
            float scale = Mathf.Clamp(damage / 40f, 0.4f, 1.6f) * Mathf.Sqrt(Mathf.Max(0.1f, Settings.BloodScale));
            if (Physics.Raycast(point + dir * 0.15f, dir, out var hit, reach, Layers.DecalMask, QueryTriggerInteraction.Ignore))
            {
                bool wall = Mathf.Abs(hit.normal.y) < 0.6f;
                Decals.Blood(hit.point, hit.normal, Random.Range(0.35f, 0.7f) * scale, wall ? 1 : 0, hit.collider);
                int drops = Settings.SuperUltra ? 5 : Settings.Unreal ? 3 : 1;
                for (int i = 0; i < drops; i++)
                {
                    var d = (dir + Random.insideUnitSphere * 0.35f).normalized;
                    if (Physics.Raycast(point + dir * 0.15f, d, out var h2, reach, Layers.DecalMask, QueryTriggerInteraction.Ignore))
                        Decals.Blood(h2.point, h2.normal, Random.Range(0.1f, 0.3f) * scale, 3, h2.collider);
                }
            }
        }

        // ------------------------------------------------------------------ bone through the skin

        static Mesh protrudingBone;

        /// <summary>A broken bone sticking out of the limb at the wound (compound fracture).</summary>
        public static void CompoundFracture(BodyPart part, Vector3 worldPoint, Vector3 worldNormal)
        {
            if (part == null || !Settings.Organs || !part.IsLimb || part.Gibbed || part.Severed) return;
            if (part.transform.Find("Broken Bone") != null) return; // one per limb
            if (protrudingBone == null)
            {
                var mb = new MeshBuilder();
                mb.SetColor(new Color(0.94f, 0.9f, 0.8f), 0.45f);
                mb.Cylinder(new Vector3(0, -0.02f, 0), new Vector3(0, 0.05f, 0), 0.011f, 0.008f, 7);
                mb.Cylinder(new Vector3(0, 0.05f, 0), new Vector3(0.004f, 0.075f, 0.002f), 0.008f, 0.0015f, 5); // jagged tip
                mb.Cylinder(new Vector3(0, 0.048f, 0), new Vector3(-0.005f, 0.066f, -0.003f), 0.006f, 0.001f, 4);
                mb.SetColor(new Color(0.55f, 0.06f, 0.08f), 0.8f);
                mb.Sphere(Vector3.zero, new Vector3(0.022f, 0.008f, 0.022f), 5, 8); // torn flesh ring
                protrudingBone = mb.ToMesh("BrokenBone");
            }
            var go = new GameObject("Broken Bone");
            go.layer = part.gameObject.layer;
            go.transform.SetParent(part.transform, false);
            var n = Vector3.Slerp(worldNormal, Random.onUnitSphere, 0.25f).normalized;
            go.transform.SetPositionAndRotation(worldPoint - worldNormal * 0.01f, Quaternion.FromToRotation(Vector3.up, n));
            go.transform.localScale = Vector3.one / Mathf.Max(0.3f, part.transform.lossyScale.x);
            go.AddComponent<MeshFilter>().sharedMesh = protrudingBone;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            BloodFX.Burst(worldPoint, worldNormal, 0.8f * Settings.BloodScale, Settings.BloodColor);
            AudioManager.Play("bone_crack", worldPoint, 1f, 0.8f);
            Stats.Add(StatBones);
        }

        // ------------------------------------------------------------------ burnt skin

        static readonly HashSet<Mesh> ownMeshes = new HashSet<Mesh>();

        /// <summary>New map: free the burnt mesh copies of the old one's bodies.</summary>
        public static void ResetWorld()
        {
            foreach (var m in ownMeshes) if (m != null) Object.Destroy(m);
            ownMeshes.Clear();
            Pools.Clear();
            Splats.Clear();
        }
        static readonly Color Char = new Color(0.07f, 0.05f, 0.04f);

        /// <summary>Darkens a body part toward charred black (explosions). amount 0..1 per call, adds up.</summary>
        public static void Burn(BodyPart part, float amount)
        {
            if (part == null || !Settings.GoreEnabled || amount <= 0f || part.Gibbed) return;
            var mf = part.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var mesh = mf.sharedMesh;
            if (!ownMeshes.Contains(mesh))
            {
                if (!mesh.isReadable) return;
                mesh = Object.Instantiate(mesh);
                mesh.name = mf.sharedMesh.name + " (burnt)";
                mf.sharedMesh = mesh;
                ownMeshes.Add(mesh);
                Stats.Add(StatCharred);
            }
            var cols = mesh.colors;
            if (cols == null || cols.Length == 0) return;
            float k = Mathf.Clamp01(amount) * (Settings.SuperUltra ? 1f : 0.7f);
            for (int i = 0; i < cols.Length; i++)
            {
                // Uneven: some spots burn faster (noise from the vertex index).
                float local = k * (0.6f + 0.8f * Mathf.PerlinNoise(i * 0.137f, 0.5f));
                var c = Color.Lerp(cols[i], Char, Mathf.Clamp01(local));
                c.a = cols[i].a;
                cols[i] = c;
            }
            mesh.colors = cols;
        }

        // ------------------------------------------------------------------ blood on the screen

        public struct Splat { public Vector2 Pos; public float Size, Born, Life, Rot; public int Tex; }
        public static readonly List<Splat> Splats = new List<Splat>();

        /// <summary>Blood hits the camera when gore happens close to it.</summary>
        public static void ScreenSplat(Vector3 worldPos, float amount)
        {
            if (!Settings.GoreEnabled || !Settings.Data.bloodOnScreen) return;
            var cam = Camera.main;
            if (cam == null) return;
            float d = Vector3.Distance(cam.transform.position, worldPos);
            float range = Settings.SuperUltra ? 4.5f : 3f;
            if (d > range) return;
            var vp = cam.WorldToViewportPoint(worldPos);
            if (vp.z <= 0f) return;
            int n = Mathf.Clamp(Mathf.RoundToInt(amount * (1f - d / range) * (Settings.SuperUltra ? 6f : 3f)), 1, 8);
            for (int i = 0; i < n; i++)
            {
                if (Splats.Count > 24) Splats.RemoveAt(0);
                Splats.Add(new Splat
                {
                    Pos = new Vector2(Mathf.Clamp01(vp.x + Random.Range(-0.35f, 0.35f)), Mathf.Clamp01(1f - vp.y + Random.Range(-0.3f, 0.3f))),
                    Size = Random.Range(160f, 420f) * (1f - d / range * 0.5f), Born = Time.unscaledTime, Life = Random.Range(2.5f, 5f),
                    Rot = Random.Range(0f, 360f), Tex = Random.Range(0, 4),
                });
            }
        }

        // ------------------------------------------------------------------ pools

        public struct Pool { public Vector3 Pos; public float Radius; public float Born; }
        public static readonly List<Pool> Pools = new List<Pool>();

        /// <summary>A pool of blood that spreads on the ground under a point.</summary>
        public static void PoolAt(Vector3 above, float size)
        {
            if (!Settings.GoreEnabled) return;
            if (!Physics.Raycast(above + Vector3.up * 0.3f, Vector3.down, out var hit, 2.5f, Layers.DecalMask, QueryTriggerInteraction.Ignore)) return;
            if (hit.normal.y < 0.6f) return;
            float s = size * Mathf.Sqrt(Mathf.Max(0.2f, Settings.BloodScale));
            Decals.Blood(hit.point, hit.normal, s, 2, hit.collider, Settings.SuperUltra ? 14f : 22f);
            Pools.Add(new Pool { Pos = hit.point, Radius = s * 0.45f, Born = Time.time });
            if (Pools.Count > 60) Pools.RemoveAt(0);
            Stats.Add(StatPools);
        }

        public static bool InPool(Vector3 feet)
        {
            foreach (var p in Pools)
            {
                float grown = p.Radius * Mathf.Clamp01((Time.time - p.Born) / 6f + 0.3f);
                var d = feet - p.Pos;
                if (Mathf.Abs(d.y) < 0.4f && d.x * d.x + d.z * d.z < grown * grown) return true;
            }
            return false;
        }
    }

    /// <summary>Runs the per-frame gore: pools under the dead, footprints, drag smears and the blood on the screen.</summary>
    public sealed class GoreDirector : MonoBehaviour
    {
        static GoreDirector I;
        static Texture2D[] splatTex;
        readonly HashSet<Goredoll> pooled = new HashSet<Goredoll>();
        readonly Dictionary<Goredoll, Vector3> lastSmear = new Dictionary<Goredoll, Vector3>();
        readonly Dictionary<Goredoll, (int steps, Vector3 last, bool left)> dollSteps = new Dictionary<Goredoll, (int, Vector3, bool)>();
        int playerSteps;
        Vector3 playerLast;
        bool playerLeft;
        float nextScan;
        Object world;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (I != null) return;
            var go = new GameObject("GoreDirector");
            DontDestroyOnLoad(go);
            I = go.AddComponent<GoreDirector>();
            GoreKit.EnsureAchievements();
            UIRoot.OverlayLayer += DrawSplats;
        }

        void Update()
        {
            // New map: forget the old one's pools.
            if (world != (Object)WorldRoot.Current)
            {
                world = WorldRoot.Current;
                GoreKit.ResetWorld(); pooled.Clear(); lastSmear.Clear(); dollSteps.Clear(); playerSteps = 0;
                if (world != null && Settings.SuperUltra) Stats.UnlockById("gore_super");
            }
            if (WorldRoot.Current == null || !Settings.GoreEnabled) return;
            Footsteps();
            if (Time.time < nextScan) return;
            nextScan = Time.time + 0.12f;
            Scan();
        }

        void Scan()
        {
            foreach (var d in Goredoll.All)
            {
                if (d == null || !d.Parts.TryGetValue(PartType.Pelvis, out var pelvis) || pelvis == null || pelvis.Body == null) continue;
                var pos = pelvis.Body.position;
                if (d.IsDead)
                {
                    // A pool spreads under the body once it lies still.
                    if (!pooled.Contains(d) && RB.Vel(pelvis.Body).sqrMagnitude < 0.25f)
                    {
                        pooled.Add(d);
                        float size = Settings.SuperUltra ? 2.6f : Settings.Unreal ? 1.9f : 1.3f;
                        GoreKit.PoolAt(pos, size);
                        if (d.Parts.TryGetValue(PartType.Chest, out var chest) && chest != null && Settings.Unreal) GoreKit.PoolAt(chest.transform.position, size * 0.7f);
                    }
                    // Dragged bodies leave smears.
                    var v = RB.Vel(pelvis.Body);
                    if (v.sqrMagnitude > 0.6f && Physics.Raycast(pos, Vector3.down, out var hit, 0.45f, Layers.DecalMask, QueryTriggerInteraction.Ignore))
                    {
                        if (!lastSmear.TryGetValue(d, out var last) || (last - hit.point).sqrMagnitude > 0.09f)
                        {
                            lastSmear[d] = hit.point;
                            Decals.Blood(hit.point, hit.normal, Random.Range(0.25f, 0.45f) * Mathf.Sqrt(Settings.BloodScale), 0, hit.collider);
                        }
                    }
                }
                else if (d.Ragdoll != null && d.Ragdoll.IsStanding)
                {
                    // Living Goredolls walking through blood leave red footprints too.
                    if (!dollSteps.TryGetValue(d, out var st)) st = (0, pos, false);
                    var feet = pos + Vector3.down * 0.9f * (d.Look != null ? d.Look.Height : 1f);
                    if (GoreKit.InPool(feet)) st.steps = 10;
                    if (st.steps > 0 && (MathX.Flat(pos - st.last)).magnitude > 0.7f)
                    {
                        Footprint(pos, MathX.Flat(pos - st.last).normalized, st.left, st.steps);
                        st.steps--; st.left = !st.left; st.last = pos;
                    }
                    else if (st.steps == 0) st.last = pos;
                    dollSteps[d] = st;
                }
            }
            if (dollSteps.Count > 64) dollSteps.Clear();
            if (lastSmear.Count > 64) lastSmear.Clear();
        }

        void Footsteps()
        {
            var p = PlayerController.Instance;
            if (p == null || p.IsDead || p.Noclip) return;
            var pos = p.Position;
            if (GoreKit.InPool(pos)) playerSteps = Settings.SuperUltra ? 18 : 12;
            var flat = MathX.Flat(pos - playerLast);
            if (playerSteps > 0 && flat.magnitude > 0.75f)
            {
                Footprint(pos + Vector3.up * 0.3f, flat.normalized, playerLeft, playerSteps);
                playerSteps--; playerLeft = !playerLeft; playerLast = pos;
            }
            else if (playerSteps == 0) playerLast = pos;
        }

        static void Footprint(Vector3 above, Vector3 forward, bool left, int stepsLeft)
        {
            var right = Vector3.Cross(Vector3.up, forward);
            var at = above + right * (left ? -0.12f : 0.12f);
            if (!Physics.Raycast(at, Vector3.down, out var hit, 2.2f, Layers.DecalMask, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.6f) return;
            float fade = Mathf.Clamp01(stepsLeft / 8f); // prints get fainter (smaller) as the blood wears off
            Decals.Blood(hit.point, hit.normal, Mathf.Lerp(0.09f, 0.2f, fade), 3, hit.collider);
        }

        // ------------------------------------------------------------------ screen blood

        static void EnsureTextures()
        {
            if (splatTex != null) return;
            splatTex = new Texture2D[4];
            for (int k = 0; k < 4; k++)
            {
                const int S = 128;
                var c = new PixelCanvas(S, S, new Color(1, 1, 1, 0));
                var r = new System.Random(900 + k);
                float cx = S / 2f, cy = S / 2f;
                c.Circle(cx, cy, 18 + r.Next(10), Color.white, 6f, true);
                int blobs = 8 + r.Next(8);
                for (int i = 0; i < blobs; i++)
                {
                    float a = (float)(r.NextDouble() * Mathf.PI * 2), dist = 14 + (float)r.NextDouble() * 36;
                    c.Circle(cx + Mathf.Cos(a) * dist, cy + Mathf.Sin(a) * dist, 3 + (float)r.NextDouble() * 9, Color.white, 2f, true);
                }
                for (int i = 0; i < 2 + r.Next(3); i++) // drips run down the screen (texture y up = screen up)
                {
                    float x = cx + (float)(r.NextDouble() - 0.5) * 36;
                    c.Line(x, cy, x + (float)(r.NextDouble() - 0.5) * 4, cy - 30 - (float)r.NextDouble() * 30, 3 + (float)r.NextDouble() * 3, Color.white, 1.5f);
                }
                splatTex[k] = c.ToTexture("ScreenBlood" + k, true, TextureWrapMode.Clamp);
            }
        }

        static void DrawSplats()
        {
            var list = GoreKit.Splats;
            if (list.Count == 0 || Event.current.type != EventType.Repaint) return;
            EnsureTextures();
            float t = Time.unscaledTime;
            var col = Settings.BloodColor;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var s = list[i];
                float age = t - s.Born;
                if (age > s.Life) { list.RemoveAt(i); continue; }
                float a = Mathf.Clamp01(1f - (age - s.Life * 0.6f) / (s.Life * 0.4f)) * 0.85f;
                float slide = age * 14f; // runs down slowly
                var r = new Rect(s.Pos.x * UI.VW - s.Size / 2f, s.Pos.y * UI.VH - s.Size / 2f + slide, s.Size, s.Size);
                UI.Tex(r, splatTex[s.Tex], new Color(col.r * 0.75f, col.g * 0.7f, col.b * 0.7f, a));
            }
        }
    }
}
