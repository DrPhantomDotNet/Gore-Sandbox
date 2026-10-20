using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Finger curl presets (degrees per finger: proximal, middle, distal) plus thumb.</summary>
    public struct HandPose
    {
        public Vector3 Index, Middle, Ring, Pinky, Thumb;
        public float ThumbSwing;

        public static HandPose Lerp(HandPose a, HandPose b, float t) => new HandPose
        {
            Index = Vector3.Lerp(a.Index, b.Index, t), Middle = Vector3.Lerp(a.Middle, b.Middle, t), Ring = Vector3.Lerp(a.Ring, b.Ring, t),
            Pinky = Vector3.Lerp(a.Pinky, b.Pinky, t), Thumb = Vector3.Lerp(a.Thumb, b.Thumb, t), ThumbSwing = Mathf.Lerp(a.ThumbSwing, b.ThumbSwing, t),
        };

        static Vector3 V(float a, float b, float c) => new Vector3(a, b, c);
        public static readonly HandPose Relaxed = new HandPose { Index = V(12, 18, 10), Middle = V(16, 22, 12), Ring = V(20, 24, 12), Pinky = V(24, 26, 14), Thumb = V(10, 10, 8), ThumbSwing = 25 };
        public static readonly HandPose Fist = new HandPose { Index = V(88, 100, 65), Middle = V(90, 102, 65), Ring = V(92, 100, 62), Pinky = V(92, 98, 60), Thumb = V(35, 45, 25), ThumbSwing = 70 };
        public static readonly HandPose PistolGrip = new HandPose { Index = V(45, 55, 25), Middle = V(80, 92, 55), Ring = V(84, 92, 55), Pinky = V(86, 90, 55), Thumb = V(20, 15, 10), ThumbSwing = 55 };
        public static readonly HandPose Support = new HandPose { Index = V(55, 45, 25), Middle = V(60, 50, 28), Ring = V(62, 52, 30), Pinky = V(64, 52, 30), Thumb = V(25, 20, 10), ThumbSwing = 45 };
        public static readonly HandPose Knife = new HandPose { Index = V(80, 95, 60), Middle = V(85, 98, 60), Ring = V(88, 98, 58), Pinky = V(90, 96, 56), Thumb = V(30, 35, 20), ThumbSwing = 65 };
        public static readonly HandPose Hold = new HandPose { Index = V(55, 60, 35), Middle = V(58, 62, 35), Ring = V(60, 62, 35), Pinky = V(62, 62, 35), Thumb = V(35, 30, 15), ThumbSwing = 50 };
        public static readonly HandPose Open = new HandPose { Index = V(4, 6, 4), Middle = V(4, 6, 4), Ring = V(5, 6, 4), Pinky = V(6, 6, 4), Thumb = V(5, 5, 5), ThumbSwing = 10 };
        public static readonly HandPose Point = new HandPose { Index = V(2, 4, 2), Middle = V(85, 98, 60), Ring = V(88, 98, 58), Pinky = V(90, 96, 56), Thumb = V(30, 35, 20), ThumbSwing = 60 };
    }

    /// <summary>
    /// First-person hand with five jointed fingers (three segments each), a wrist and a sleeved forearm.
    /// Built as a right hand; the left hand is the same rig mirrored with negative X scale.
    /// Local frame: fingers along +Z, palm facing -Y, thumb on -X.
    /// </summary>
    public sealed class HandRig : MonoBehaviour
    {
        readonly Transform[][] fingers = new Transform[4][];
        readonly Transform[] thumb = new Transform[3];
        Transform thumbBase;
        HandPose current = HandPose.Relaxed, target = HandPose.Relaxed;
        public Transform Forearm;
        public Transform Wrist => transform;

        static readonly float[][] Lengths =
        {
            new[] { 0.040f, 0.025f, 0.021f }, // index
            new[] { 0.045f, 0.028f, 0.023f }, // middle
            new[] { 0.042f, 0.026f, 0.022f }, // ring
            new[] { 0.033f, 0.021f, 0.019f }, // pinky
        };
        static readonly float[] FingerX = { -0.030f, -0.010f, 0.010f, 0.029f };

        public static HandRig Build(Transform parent, string name, bool left, Color skin, Color sleeve, int layer)
        {
            var root = new GameObject(name);
            root.layer = layer;
            root.transform.SetParent(parent, false);
            if (left) root.transform.localScale = new Vector3(-1, 1, 1);
            var rig = root.AddComponent<HandRig>();

            // Palm and knuckles
            var palm = new MeshBuilder { Color = skin, Gloss = 0.32f };
            palm.BevelBox(new Vector3(0, 0, 0.045f), new Vector3(0.082f, 0.026f, 0.092f), 0.011f);
            palm.Sphere(new Vector3(-0.032f, 0.004f, 0.012f), new Vector3(0.022f, 0.016f, 0.03f), 6, 8);
            for (int i = 0; i < 4; i++) palm.Sphere(new Vector3(FingerX[i], 0.004f, 0.09f), Vector3.one * 0.0105f, 5, 8);
            // Wrist
            palm.Cylinder(new Vector3(0, 0, -0.025f), new Vector3(0, 0, 0.005f), 0.03f, 0.034f, 12);
            Part(root.transform, "Palm", palm.ToMesh("Palm"), Vector3.zero, layer);

            for (int f = 0; f < 4; f++)
            {
                rig.fingers[f] = new Transform[3];
                Transform p = root.transform;
                var pos = new Vector3(FingerX[f], 0.002f, 0.09f);
                for (int s = 0; s < 3; s++)
                {
                    var seg = new GameObject("F" + f + "_" + s).transform;
                    seg.gameObject.layer = layer;
                    seg.SetParent(p, false);
                    seg.localPosition = pos;
                    float len = Lengths[f][s];
                    float r = (0.0088f - s * 0.0009f) * (f == 3 ? 0.88f : 1f);
                    var mb = new MeshBuilder { Color = skin, Gloss = 0.32f };
                    mb.Capsule(Vector3.zero, new Vector3(0, 0, len), r, r * 0.92f, 8, 2);
                    if (s == 2)
                    {
                        mb.SetColor(Color.Lerp(skin, new Color(1f, 0.85f, 0.8f), 0.5f), 0.7f);
                        mb.Sphere(new Vector3(0, r * 0.65f, len * 0.6f), new Vector3(r * 0.75f, r * 0.25f, len * 0.4f), 4, 6);
                    }
                    var m = mb.ToMesh("Finger");
                    seg.gameObject.AddComponent<MeshFilter>().sharedMesh = m;
                    seg.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                    rig.fingers[f][s] = seg;
                    p = seg;
                    pos = new Vector3(0, 0, len);
                }
            }
            rig.thumbBase = new GameObject("ThumbBase").transform;
            rig.thumbBase.gameObject.layer = layer;
            rig.thumbBase.SetParent(root.transform, false);
            rig.thumbBase.localPosition = new Vector3(-0.036f, -0.006f, 0.02f);
            Transform tp = rig.thumbBase;
            float[] tl = { 0.032f, 0.028f, 0.024f };
            for (int s = 0; s < 3; s++)
            {
                var seg = new GameObject("T" + s).transform;
                seg.gameObject.layer = layer;
                seg.SetParent(tp, false);
                seg.localPosition = s == 0 ? Vector3.zero : new Vector3(0, 0, tl[s - 1]);
                var mb = new MeshBuilder { Color = skin, Gloss = 0.32f };
                float r = 0.0105f - s * 0.001f;
                mb.Capsule(Vector3.zero, new Vector3(0, 0, tl[s]), r, r * 0.92f, 8, 2);
                seg.gameObject.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Thumb");
                seg.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                rig.thumb[s] = seg;
                tp = seg;
            }

            // Forearm with a jacket sleeve; stretched from the wrist toward an elbow point every frame.
            var fa = new GameObject("Forearm");
            fa.layer = layer;
            fa.transform.SetParent(parent, false);
            var fm = new MeshBuilder();
            fm.Loft(6, 14, i =>
            {
                float t = i / 5f;
                float rr = Mathf.Lerp(0.031f, 0.046f, t);
                return (new Vector3(0, 0, -t), rr, rr * 0.88f);
            }, Vector3.back, false, true, null, i => { fm.Gloss = i == 0 ? 0.3f : 0.12f; return i == 0 ? skin : sleeve; });
            fm.SetColor(sleeve * 0.85f, 0.12f);
            fm.Torus(new Vector3(0, 0, -0.18f), 0.045f, 0.008f, 14, 4, Vector3.forward);
            fa.AddComponent<MeshFilter>().sharedMesh = fm.ToMesh("Forearm");
            fa.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            rig.Forearm = fa.transform;
            rig.SetPose(HandPose.Relaxed, true);
            return rig;
        }

        static void Part(Transform parent, string name, Mesh mesh, Vector3 pos, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void SetPose(HandPose pose, bool instant = false)
        {
            target = pose;
            if (instant) { current = pose; Apply(); }
        }

        /// <summary>Stretches the forearm from the wrist to an elbow point given in world space.</summary>
        public void AimForearm(Vector3 elbowWorld)
        {
            if (Forearm == null) return;
            var wrist = transform.TransformPoint(new Vector3(0, 0, -0.02f));
            var dir = elbowWorld - wrist;
            float len = dir.magnitude;
            Forearm.position = wrist;
            Forearm.rotation = Quaternion.LookRotation(-dir.normalized, transform.up);
            Forearm.localScale = new Vector3(1, 1, Mathf.Max(0.05f, len));
        }

        void LateUpdate()
        {
            current = HandPose.Lerp(current, target, 1f - Mathf.Exp(-18f * Time.deltaTime));
            Apply();
        }

        void Apply()
        {
            Vector3[] curls = { current.Index, current.Middle, current.Ring, current.Pinky };
            for (int f = 0; f < 4; f++)
            {
                if (fingers[f] == null) continue;
                float spread = (f - 1.5f) * 3f;
                fingers[f][0].localRotation = Quaternion.Euler(curls[f].x, spread, 0);
                fingers[f][1].localRotation = Quaternion.Euler(curls[f].y, 0, 0);
                fingers[f][2].localRotation = Quaternion.Euler(curls[f].z, 0, 0);
            }
            if (thumbBase != null)
            {
                thumbBase.localRotation = Quaternion.Euler(10f, -45f + current.ThumbSwing * 0.6f, -30f - current.ThumbSwing * 0.5f);
                thumb[0].localRotation = Quaternion.Euler(current.Thumb.x * 0.5f, 0, 0);
                thumb[1].localRotation = Quaternion.Euler(current.Thumb.y, 0, 0);
                thumb[2].localRotation = Quaternion.Euler(current.Thumb.z, 0, 0);
            }
        }
    }
}
