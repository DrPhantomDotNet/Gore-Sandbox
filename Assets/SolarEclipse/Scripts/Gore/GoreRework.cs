using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 gore and realism layer (on top of Bleeding, BloodFX and Decals):
    ///   • BloodPools: where the pools are, so feet that step in them leave bloody footprints (player and Goredolls).
    ///   • Footprints: foot-shaped decals that fade as the soles dry (about 10 steps).
    ///   • SkinTone: skin goes pale with blood loss, blue with low oxygen, grey-white after death.
    ///   • FilmLook: optional cinematic grain and vignette over the game view.
    /// Pools darkening as they dry and blood soaking into clothes around wounds live in Decals.cs / Bleeding.cs.
    /// </summary>
    public static class BloodPools
    {
        struct Pool { public Vector3 P; public float R, Born; }
        static readonly List<Pool> pools = new List<Pool>();

        public static void Add(Vector3 p, float size)
        {
            if (pools.Count >= 80) pools.RemoveAt(0);
            pools.Add(new Pool { P = p, R = size * 0.45f, Born = Time.time });
        }

        public static void Clear() => pools.Clear();

        /// <summary>Is this point standing in fresh-ish blood (a pool less than 10 minutes old)?</summary>
        public static bool In(Vector3 p)
        {
            for (int i = pools.Count - 1; i >= 0; i--)
            {
                var q = pools[i];
                if (Time.time - q.Born > 600f) continue;
                var d = p - q.P;
                if (Mathf.Abs(d.y) < 0.45f && d.x * d.x + d.z * d.z < q.R * q.R) return true;
            }
            return false;
        }
    }

    public static class Footprints
    {
        static Texture2D tex;
        static Material mat;

        public static Material Material => mat != null ? mat : (mat = Mats.DecalFor(Texture, 0.6f));

        /// <summary>A right shoe sole (heel at the bottom, toes up); left feet use it mirrored.</summary>
        static Texture2D Texture
        {
            get
            {
                if (tex != null) return tex;
                var c = new PixelCanvas(64, 128, new Color(1, 1, 1, 0));
                var ink = new Color(1, 1, 1, 0.95f);
                c.Circle(32, 22, 15, ink, 3f);                     // heel
                c.Polygon(new[] { new Vector2(20, 26), new Vector2(44, 26), new Vector2(48, 70), new Vector2(16, 70) }, ink);
                c.Circle(33, 80, 20, ink, 3f);                     // ball of the foot
                c.Circle(36, 100, 16, ink, 3f);                    // toe box
                // Tread lines and a dry, broken edge so it reads as a shoe print, not a blob.
                var gap = new Color(1, 1, 1, 0f);
                for (int y = 10; y < 116; y += 9) c.Line(8, y, 56, y + 3, 1.6f, gap, 1f);
                for (int i = 0; i < 70; i++) c.Circle(Random.Range(10f, 54f), Random.Range(6f, 118f), Random.Range(0.8f, 2.4f), gap, 1f);
                tex = c.ToTexture("Footprint", true);
                return tex;
            }
        }

        /// <summary>Leaves a print where a foot lands. wet 0..1 (fades over the steps after a pool).</summary>
        public static void Step(Vector3 foot, float yaw, bool left, float wet)
        {
            if (!Settings.GoreEnabled || wet <= 0.02f) return;
            if (!Physics.Raycast(foot + Vector3.up * 0.3f, Vector3.down, out var hit, 0.8f, Layers.DecalMask, QueryTriggerInteraction.Ignore)) return;
            var c = Settings.BloodColor * 0.8f;
            c.a = Mathf.Clamp01(0.25f + wet * 0.75f);
            Decals.Footprint(hit.point, hit.normal, yaw, left, c, hit.collider);
        }
    }

    /// <summary>Your own bloody footprints (called from PlayerController.Footstep).</summary>
    public static class PlayerFootprints
    {
        static int wet;
        static bool left;

        public static void OnStep(Vector3 feet, float yaw)
        {
            if (BloodPools.In(feet)) wet = 10;
            if (wet <= 0) return;
            left = !left;
            var side = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * (left ? -0.11f : 0.11f);
            Footprints.Step(feet + side, yaw, left, wet / 10f);
            wet--;
        }
    }

    /// <summary>Goredolls that walk through blood leave footprints too (added in GoredollFactory).</summary>
    public sealed class DollFootprints : MonoBehaviour
    {
        Goredoll doll;
        Vector3 last;
        float dist;
        int wet;
        bool left;

        public static void Attach(Goredoll d) { if (d != null && d.GetComponent<DollFootprints>() == null) d.gameObject.AddComponent<DollFootprints>().doll = d; }

        void Update()
        {
            if (doll == null || doll.Ragdoll == null || !doll.Parts.TryGetValue(PartType.Pelvis, out var pelvis) || pelvis == null || pelvis.Body == null) return;
            var p = pelvis.Body.position;
            var step = MathX.Flat(p - last);
            last = p;
            if (!doll.Ragdoll.IsStanding || step.magnitude > 1f) return;
            dist += step.magnitude;
            if (dist < 0.72f) return;
            dist = 0f;
            var feet = new Vector3(p.x, p.y - 0.9f, p.z);
            if (BloodPools.In(feet)) wet = 8;
            if (wet <= 0) return;
            left = !left;
            var footPart = left ? PartType.FootL : PartType.FootR;
            Vector3 at = feet;
            if (doll.Parts.TryGetValue(footPart, out var fp) && fp != null && !fp.Severed && fp.Body != null) at = fp.Body.position;
            float yaw = step.sqrMagnitude > 0.0001f ? Mathf.Atan2(step.x, step.z) * Mathf.Rad2Deg : doll.transform.eulerAngles.y;
            Footprints.Step(at, yaw, left, wet / 8f);
            wet--;
        }
    }

    /// <summary>
    /// Skin colour follows the body: pale and grey with blood loss, blue lips and face with low oxygen, waxy after
    /// death. Tints the head and hands (mostly skin) through a property block, so no extra materials.
    /// </summary>
    public sealed class SkinTone : MonoBehaviour
    {
        Goredoll doll;
        readonly List<Renderer> skin = new List<Renderer>();
        MaterialPropertyBlock mpb;
        Color current = Color.white;
        float next;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public static void Attach(Goredoll d) { if (d != null && d.GetComponent<SkinTone>() == null) d.gameObject.AddComponent<SkinTone>().doll = d; }

        void Start()
        {
            mpb = new MaterialPropertyBlock();
            foreach (var t in new[] { PartType.Head, PartType.HandL, PartType.HandR, PartType.Neck })
                if (doll != null && doll.Parts.TryGetValue(t, out var bp) && bp != null)
                {
                    var r = bp.GetComponent<Renderer>();
                    if (r != null) skin.Add(r);
                }
        }

        void Update()
        {
            if (doll == null || Time.time < next) return;
            next = Time.time + 0.25f;
            var v = doll.Vitals;
            if (doll.Zombie != null || v.Undead) return;
            float loss = Mathf.Clamp01(v.BloodLossFraction / 0.4f);
            float hypoxia = Mathf.Clamp01((92f - v.SpO2) / 40f);
            float dead = v.State == LifeState.Dead ? Mathf.Clamp01((Time.time - v.TimeOfDeath) / 25f) : 0f;
            var target = Color.white;
            target = Color.Lerp(target, new Color(0.86f, 0.84f, 0.84f), loss);           // pallor
            target = Color.Lerp(target, new Color(0.78f, 0.82f, 0.98f), hypoxia * 0.85f); // cyanosis
            target = Color.Lerp(target, new Color(0.8f, 0.8f, 0.84f), dead);              // waxy, bloodless
            if (Mathf.Abs(target.r - current.r) + Mathf.Abs(target.g - current.g) + Mathf.Abs(target.b - current.b) < 0.004f) return;
            current = Color.Lerp(current, target, 0.35f);
            foreach (var r in skin)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(ColorId, current);
                r.SetPropertyBlock(mpb);
            }
        }
    }

    /// <summary>Optional cinematic look over the game view (Settings > Video > Film look): grain and a soft vignette.</summary>
    public static class FilmLook
    {
        public static void Draw(float W, float H)
        {
            if (!Settings.Data.filmLook) return;
            UI.Tex(new Rect(0, 0, W, H), TextureFactory.Vignette, new Color(0f, 0f, 0f, 0.55f));
            var g = TextureFactory.Grain;
            if (g == null) return;
            // Jitter the grain each frame so it reads as film noise, not a dirty screen.
            float ox = Random.Range(0f, 1f), oy = Random.Range(0f, 1f);
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, Screen.width, Screen.height), g, new Rect(ox, oy, Screen.width / 128f, Screen.height / 128f));
            GUI.color = Color.white;
        }
    }
}

namespace SolarEclipse
{
    /// <summary>
    /// A bruise ageing like a real one (sped up): red at first, purple within a minute, blue-black, then green-yellow
    /// at the edges as it heals. Lives on a skin wound decal (Decals.Wound); disabled when the decal is reused.
    /// </summary>
    public sealed class Bruise : MonoBehaviour
    {
        float born, next;
        Color baseCol;
        Mesh mesh;
        static readonly Color[] Stages = { new Color(1f, 0.75f, 0.75f), new Color(0.7f, 0.45f, 0.85f), new Color(0.45f, 0.42f, 0.7f), new Color(0.85f, 0.85f, 0.45f) };
        static readonly float[] Times = { 0f, 45f, 150f, 420f };

        public static void Begin(GameObject decal)
        {
            if (decal == null) return;
            var b = decal.GetComponent<Bruise>() ?? decal.AddComponent<Bruise>();
            var mf = decal.GetComponent<MeshFilter>();
            b.mesh = mf != null ? mf.sharedMesh : null;
            b.baseCol = b.mesh != null && b.mesh.colors.Length > 0 ? b.mesh.colors[0] : Color.white;
            b.born = Time.time; b.next = 0f;
            b.enabled = true;
        }

        void Update()
        {
            if (mesh == null || Time.time < next) return;
            next = Time.time + 1f;
            float age = Time.time - born;
            int i = 0;
            while (i < Times.Length - 2 && age > Times[i + 1]) i++;
            float k = Mathf.Clamp01((age - Times[i]) / (Times[i + 1] - Times[i]));
            var tint = Color.Lerp(Stages[i], Stages[i + 1], k);
            var c = new Color(baseCol.r * tint.r, baseCol.g * tint.g, baseCol.b * tint.b, baseCol.a);
            mesh.colors = new[] { c, c, c, c };
            if (age > Times[Times.Length - 1]) enabled = false;
        }
    }
}
