using System;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// One-line hooks from the shared damage code (BodyPart.TakeDamage, Goredoll.Launch, Dismemberment) into the story
    /// characters. Everything is a no-op for ordinary Goredolls (one GetComponent per call).
    /// </summary>
    public static class StoryHooks
    {
        /// <summary>BodyPart.TakeDamage: false = ignore this hit entirely. May rewrite the hit (Z's boss pool).</summary>
        public static bool Process(BodyPart part, ref DamageInfo info)
        {
            if (part == null || part.Doll == null) return true;
            var z = part.Doll.GetComponent<ZBoss>();
            return z == null || z.OnHit(part, ref info);
        }

        /// <summary>Goredoll.Launch: Z can't be thrown around before the fight starts.</summary>
        public static bool NoLaunch(Goredoll d)
        {
            if (d == null) return false;
            var z = d.GetComponent<ZBoss>();
            return z != null && z.Stage == 0;
        }

        /// <summary>Dismemberment: Z keeps his limbs and head until his health pool is empty.</summary>
        public static bool NoDismember(BodyPart part)
        {
            if (part == null || part.Doll == null) return false;
            var z = part.Doll.GetComponent<ZBoss>();
            return z != null && !z.Defeated;
        }
    }

    /// <summary>Something you can talk to with the Use / INTERACT button (PlayerController.Use finds it on a Goredoll
    /// root or on any collider's parent).</summary>
    public sealed class StoryTalk : MonoBehaviour, IInteractable
    {
        public string Label = "Talk";
        public Action OnTalk;
        public Func<bool> CanTalk;

        public string Prompt => Label;

        public void Interact(GameObject user)
        {
            if (CanTalk != null && !CanTalk()) { AudioManager.Ui("ui_error", 0.4f); return; }
            OnTalk?.Invoke();
        }
    }

    // ====================================================================== Big Marcus

    /// <summary>
    /// Big Marcus: a static (no physics) Goredoll model, so nothing can hurt, push, grab or ragdoll him. Bullets pass
    /// through (his blocking capsule is on the Trigger layer, which only the player, ragdolls and props collide with,
    /// and which the Use raycast includes). Idle animation: breathing, weight shift, turning to look at the player.
    /// </summary>
    public sealed class BigMarcus : MonoBehaviour
    {
        public const string Name = "Big Marcus";
        Transform model, head, chest, neck;
        Quaternion headRest;
        float yaw, baseYaw, t0;
        FaceRig face;
        public StoryTalk Talk { get; private set; }

        public Vector3 HeadPosition => head != null ? head.position + Vector3.up * 0.15f : transform.position + Vector3.up * 2.1f;

        public static BigMarcus Spawn(Transform parent, Vector3 feet, float yawDeg)
        {
            var look = new Appearance
            {
                Name = Name,
                Skin = new Color(0.36f, 0.24f, 0.17f), Shirt = new Color(0.93f, 0.93f, 0.9f), Pants = new Color(0.13f, 0.14f, 0.18f),
                Shoes = new Color(0.9f, 0.9f, 0.9f), Hair = new Color(0.05f, 0.05f, 0.05f), Eyes = new Color(0.18f, 0.11f, 0.06f),
                HairStyle = 1, LongSleeves = false, Beard = true, Height = 1.14f, Build = 1.4f,
            };
            look.Lips = Color.Lerp(look.Skin, new Color(0.55f, 0.3f, 0.3f), 0.35f);

            var root = new GameObject("Big Marcus");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(feet, Quaternion.Euler(0, yawDeg, 0));
            var bm = root.AddComponent<BigMarcus>();
            bm.yaw = bm.baseYaw = yawDeg;
            bm.t0 = UnityEngine.Random.value * 10f;

            GameObject m;
            try { m = CustomDolls.BuildMannequin(look, Armor.None, false, null, 0); }
            catch (Exception e) { Debug.LogWarning("Big Marcus model failed: " + e.Message); m = new GameObject("Mannequin"); }
            m.transform.SetParent(root.transform, false);
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.identity;
            m.transform.localScale = Vector3.one * look.Height;
            bm.model = m.transform;
            bm.head = m.transform.Find("Head");
            bm.chest = m.transform.Find("Chest");
            bm.neck = m.transform.Find("Neck");
            if (bm.head != null) { bm.headRest = bm.head.localRotation; bm.face = bm.head.GetComponentInChildren<FaceRig>(); }

            // Outfit details: gold chain, backwards cap.
            if (bm.chest != null) AddPiece(bm.chest, "Chain", ChainMesh());
            if (bm.head != null) AddPiece(bm.head, "Cap", CapMesh());

            // Blocks the player, is found by the Use raycast, ignored by bullets.
            var col = new GameObject("Marcus Blocker");
            col.layer = Layers.Trigger;
            col.transform.SetParent(root.transform, false);
            var cap = col.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0, 1.08f * look.Height, 0);
            cap.height = 2.1f * look.Height;
            cap.radius = 0.42f;
            bm.Talk = root.AddComponent<StoryTalk>();
            bm.Talk.Label = "Talk to " + Name;
            return bm;
        }

        static void AddPiece(Transform parent, string name, Mesh mesh)
        {
            var go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
        }

        static Mesh chainMesh, capMesh;

        static Mesh ChainMesh()
        {
            if (chainMesh != null) return chainMesh;
            // Chest-local: the neck sits ~0.28 m above the chest pivot. A gold loop draped down the front.
            var mb = new MeshBuilder { Color = new Color(0.95f, 0.75f, 0.2f), Gloss = 0.85f };
            const int links = 22;
            for (int i = 0; i < links; i++)
            {
                float a = i / (float)links * Mathf.PI * 2f;
                float x = Mathf.Sin(a) * 0.105f;
                float z = Mathf.Cos(a) * 0.085f;
                float y = 0.27f - (z > 0f ? z * 1.6f : 0f); // dips down the chest at the front
                mb.Sphere(new Vector3(x, y, z + 0.015f), new Vector3(0.012f, 0.012f, 0.012f), 4, 6);
            }
            // Pendant
            mb.Box(new Vector3(0, 0.12f, 0.115f), new Vector3(0.045f, 0.05f, 0.012f));
            chainMesh = mb.ToMesh("MarcusChain");
            return chainMesh;
        }

        static Mesh CapMesh()
        {
            if (capMesh != null) return capMesh;
            float y = HumanModel.HeadCenterY;
            var mb = new MeshBuilder { Color = new Color(0.12f, 0.12f, 0.14f), Gloss = 0.1f };
            // Crown: a squashed half-ish dome sitting on top of the head.
            mb.Sphere(new Vector3(0, y + 0.045f, -0.002f), new Vector3(0.112f, 0.085f, 0.122f), 8, 14);
            // Backwards brim.
            mb.Box(new Vector3(0, y + 0.03f, -0.15f), new Vector3(0.15f, 0.012f, 0.1f));
            mb.SetColor(new Color(0.8f, 0.1f, 0.1f), 0.2f);
            mb.Box(new Vector3(0, y + 0.07f, 0.112f), new Vector3(0.05f, 0.04f, 0.01f)); // logo patch
            capMesh = mb.ToMesh("MarcusCap");
            return capMesh;
        }

        void Update()
        {
            float t = Time.time + t0;
            var p = PlayerController.Instance;
            bool near = false;
            if (p != null)
            {
                var to = MathX.Flat(p.Position - transform.position);
                float d = to.magnitude;
                near = d < 10f;
                float targetYaw = near && d > 0.2f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg : baseYaw;
                yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, 70f * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0, yaw, 0);
                if (face != null) { if (near) face.LookAt(p.EyePosition); else face.ClearLook(); }
            }
            // Breathing + weight shift.
            if (chest != null) chest.localScale = new Vector3(1f + Mathf.Sin(t * 1.6f) * 0.012f, 1f + Mathf.Sin(t * 1.6f) * 0.01f, 1f + Mathf.Sin(t * 1.6f) * 0.02f);
            if (model != null) model.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 0.35f) * 1.4f);
            if (head != null)
            {
                float pitch = 0f;
                if (near && p != null)
                {
                    var d = p.EyePosition - head.position;
                    pitch = Mathf.Clamp(-Mathf.Atan2(d.y, MathX.Flat(d).magnitude) * Mathf.Rad2Deg, -20f, 20f);
                }
                head.localRotation = headRest * Quaternion.Euler(pitch + Mathf.Sin(t * 0.8f) * 1.5f, Mathf.Sin(t * 0.27f) * (near ? 3f : 12f), 0);
            }
        }
    }

    // ====================================================================== barrier

    /// <summary>A glowing, see-through wall across the alley. Blocks the player (Trigger layer: player/ragdolls/props
    /// collide with it, bullets don't) until Big Marcus gives the go-ahead; then it fades away with a sound.</summary>
    public sealed class StoryBarrier : MonoBehaviour
    {
        Mesh mesh;
        Color[] baseColors, colors;
        BoxCollider col;
        Light glow;
        float fade = -1f;
        static Texture2D tex;
        static Material mat;

        public static StoryBarrier Create(Transform parent, Vector3 center, float yawDeg, float width, float height)
        {
            var go = new GameObject("Story Barrier");
            go.layer = Layers.Trigger;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0, yawDeg, 0));
            var b = go.AddComponent<StoryBarrier>();
            var mb = new MeshBuilder { Color = new Color(0.25f, 0.75f, 1f, 0.75f) };
            mb.Face(new Vector3(0, height * 0.5f, 0), Vector3.back, Vector3.up, width * 0.5f, height * 0.5f);
            mb.Face(new Vector3(0, height * 0.5f, 0), Vector3.forward, Vector3.up, width * 0.5f, height * 0.5f);
            b.mesh = mb.ToMesh("StoryBarrier");
            b.baseColors = b.mesh.colors;
            if (b.baseColors == null || b.baseColors.Length != b.mesh.vertexCount)
            {
                b.baseColors = new Color[b.mesh.vertexCount];
                for (int i = 0; i < b.baseColors.Length; i++) b.baseColors[i] = new Color(0.25f, 0.75f, 1f, 0.75f);
            }
            b.colors = new Color[b.baseColors.Length];
            go.AddComponent<MeshFilter>().sharedMesh = b.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b.col = go.AddComponent<BoxCollider>();
            b.col.center = new Vector3(0, height * 0.5f, 0);
            b.col.size = new Vector3(width, height, 0.3f);
            var lg = new GameObject("Barrier Glow");
            lg.transform.SetParent(go.transform, false);
            lg.transform.localPosition = new Vector3(0, 1.4f, -0.6f);
            b.glow = lg.AddComponent<Light>();
            b.glow.type = LightType.Point;
            b.glow.color = new Color(0.3f, 0.75f, 1f);
            b.glow.range = 7f;
            b.glow.intensity = 1.2f;
            b.glow.shadows = LightShadows.None;
            return b;
        }

        static Material Material
        {
            get
            {
                if (mat != null) return mat;
                if (tex == null)
                {
                    const int W = 64, H = 128;
                    tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "BarrierTex", wrapMode = TextureWrapMode.Clamp };
                    var px = new Color[W * H];
                    for (int y = 0; y < H; y++)
                        for (int x = 0; x < W; x++)
                        {
                            float u = x / (float)(W - 1), v = y / (float)(H - 1);
                            float edge = Mathf.Max(Mathf.Pow(1f - Mathf.Min(u, 1f - u) * 2f, 6f), Mathf.Pow(1f - v, 5f) * 0.8f);
                            float lines = (y % 8 < 2) ? 0.55f : 0.18f;
                            float hex = ((x / 8 + y / 8) % 2 == 0) ? 0.06f : 0f;
                            float a = Mathf.Clamp01(lines * (0.55f + 0.45f * v) + edge + hex) * Mathf.Clamp01(v * 6f + 0.2f) * (1f - Mathf.Pow(v, 6f) * 0.7f);
                            px[y * W + x] = new Color(1f, 1f, 1f, a);
                        }
                    tex.SetPixels(px);
                    tex.Apply(true);
                }
                mat = Mats.AdditiveWith(tex);
                return mat;
            }
        }

        public bool Open => fade >= 0f;

        /// <summary>Fades the wall away (the collider goes immediately).</summary>
        public void Dissolve()
        {
            if (fade >= 0f) return;
            fade = 0f;
            if (col != null) col.enabled = false;
            AudioManager.Play("story_barrier", transform.position + Vector3.up * 1.5f, 1f, 1f, 0.6f, 3f, 60f);
        }

        void Update()
        {
            float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 3.1f) + 0.06f * Mathf.Sin(Time.time * 17f);
            float k = 1f;
            if (fade >= 0f)
            {
                fade += Time.deltaTime / 1.3f;
                k = 1f - Mathf.Clamp01(fade);
                pulse *= 1f + (1f - k) * 1.5f * Mathf.Sin(fade * 40f) * 0.3f;
                if (fade >= 1f) { Destroy(gameObject); return; }
            }
            for (int i = 0; i < colors.Length; i++)
            {
                var c = baseColors[i];
                colors[i] = new Color(c.r, c.g, c.b, c.a * pulse * k);
            }
            mesh.colors = colors;
            if (glow != null) glow.intensity = 1.2f * pulse * k;
        }
    }
}
