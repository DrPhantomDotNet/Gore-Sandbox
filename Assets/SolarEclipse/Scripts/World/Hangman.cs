using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// The Hangman prop's rope (World/PropFactory.cs builds the wooden frame). The rope is a chain of light physics
    /// segments hanging from the beam, ending in a noose. A Goredoll gets hung when its neck is brought to the noose
    /// (Spawn Gun) or when you press INTERACT on the frame / rope (it takes the nearest Goredoll). The body hangs by the
    /// neck on a joint, goes limp and swings with the rope; push it, shoot it, spin it. INTERACT again, or shooting /
    /// cutting the rope, drops it. Singleplayer and host only (clients just see it).
    /// </summary>
    public sealed class HangmanRig : MonoBehaviour, IInteractable
    {
        public const float AnchorZ = 0.45f;
        const int Segments = 9;
        const float SegLen = 0.11f, AutoRange = 0.32f;
        static readonly Vector3 Anchor = new Vector3(0, 3.21f, AnchorZ);
        static readonly HashSet<Goredoll> allHung = new HashSet<Goredoll>();
        static Mesh segMesh, nooseMesh;

        readonly List<Rigidbody> rope = new List<Rigidbody>();
        readonly Dictionary<Goredoll, float> cooldown = new Dictionary<Goredoll, float>();
        Rigidbody frame;
        Goredoll hung;
        BodyPart hungPart;
        ConfigurableJoint noose;
        float nextAuto;

        public static bool IsHung(Goredoll d) => d != null && allHung.Contains(d);

        void Start()
        {
            frame = GetComponent<Rigidbody>();
            if (frame == null) return;
            BuildRope();
        }

        Rigidbody End => rope.Count > 0 ? rope[rope.Count - 1] : null;
        Vector3 NoosePoint => End != null ? End.transform.TransformPoint(new Vector3(0, -SegLen * 0.5f - 0.05f, 0)) : transform.TransformPoint(Anchor);

        // ------------------------------------------------------------------ rope

        static Mesh SegMesh()
        {
            if (segMesh != null) return segMesh;
            var mb = new MeshBuilder { Color = new Color(0.62f, 0.5f, 0.32f), Gloss = 0.05f };
            mb.Cylinder(new Vector3(0, -SegLen * 0.55f, 0), new Vector3(0, SegLen * 0.55f, 0), 0.016f, 0.016f, 7);
            mb.SetColor(new Color(0.5f, 0.4f, 0.25f), 0.05f); // twist bands
            mb.Cylinder(new Vector3(0, -0.01f, 0), new Vector3(0, 0.01f, 0), 0.0175f, 0.0175f, 7, false);
            return segMesh = mb.ToMesh("HangmanRope");
        }

        static Mesh NooseMesh()
        {
            if (nooseMesh != null) return nooseMesh;
            var mb = new MeshBuilder { Color = new Color(0.62f, 0.5f, 0.32f), Gloss = 0.05f };
            const int n = 12;
            float r = 0.075f;
            Vector3 P(int i) { float a = i / (float)n * Mathf.PI * 2f; return new Vector3(Mathf.Sin(a) * r, -0.05f - r + Mathf.Cos(a) * r, 0); }
            for (int i = 0; i < n; i++) mb.Cylinder(P(i), P(i + 1), 0.015f, 0.015f, 6);
            mb.SetColor(new Color(0.5f, 0.4f, 0.25f), 0.05f);
            mb.Cylinder(new Vector3(0, -0.07f, 0), new Vector3(0, 0.02f, 0), 0.026f, 0.026f, 8); // the knot coils
            return nooseMesh = mb.ToMesh("HangmanNoose");
        }

        void BuildRope()
        {
            var parent = WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null;
            var frameCols = GetComponentsInChildren<Collider>();
            Rigidbody prev = frame;
            for (int i = 0; i < Segments; i++)
            {
                var go = new GameObject(i == Segments - 1 ? "Noose" : "Rope");
                go.layer = Layers.Props;
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(transform.TransformPoint(Anchor - new Vector3(0, SegLen * (i + 0.5f), 0)), transform.rotation);
                go.AddComponent<MeshFilter>().sharedMesh = i == Segments - 1 ? NooseMesh() : SegMesh();
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                var cc = go.AddComponent<CapsuleCollider>();
                cc.radius = 0.02f; cc.height = SegLen; cc.direction = 1;
                foreach (var fc in frameCols) if (fc != null) Physics.IgnoreCollision(cc, fc);
                foreach (var other in rope) Physics.IgnoreCollision(cc, other.GetComponent<Collider>());
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 1.2f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.solverIterations = 16;
                rb.solverVelocityIterations = 8;
                RB.SetDamping(rb, 0.05f, 1.5f);
                var j = go.AddComponent<ConfigurableJoint>();
                j.connectedBody = prev;
                j.autoConfigureConnectedAnchor = false;
                j.anchor = new Vector3(0, SegLen * 0.5f, 0);
                j.connectedAnchor = i == 0 ? Anchor : new Vector3(0, -SegLen * 0.5f, 0);
                j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
                j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Free;
                j.projectionMode = JointProjectionMode.PositionAndRotation;
                j.projectionDistance = 0.02f;
                go.AddComponent<Surface>().Type = SurfaceType.Wood;
                go.AddComponent<HangmanRope>().Rig = this;
                rope.Add(rb);
                prev = rb;
            }
        }

        // ------------------------------------------------------------------ hanging

        public string Prompt => hung != null ? "Drop " + hung.DisplayName : "Hang the nearest Goredoll";

        public void Interact(GameObject user)
        {
            if (NetState.IsClient) { GameEvents.ShowToast("Only the host can use the Hangman."); return; }
            if (hung != null) { Release(); return; }
            var d = Nearest(7f);
            if (d == null) { GameEvents.ShowToast("Bring a Goredoll closer (within 7 m)."); AudioManager.Ui("ui_error", 0.5f); return; }
            Hang(d, true);
        }

        static BodyPart NeckOf(Goredoll d)
        {
            if (d == null) return null;
            if (d.Parts.TryGetValue(PartType.Neck, out var n) && n != null && !n.Severed && !n.Gibbed && n.Body != null) return n;
            return null;
        }

        Goredoll Nearest(float range)
        {
            Goredoll best = null;
            float bd = range * range;
            foreach (var d in Goredoll.All)
            {
                if (d == null || allHung.Contains(d) || d.NetProxy || NeckOf(d) == null) continue;
                if (d.GetComponent<ZBoss>() != null || d.GetComponent<StoryBoss>() != null) continue;
                float dd = (d.Position - transform.position).sqrMagnitude;
                if (dd < bd) { bd = dd; best = d; }
            }
            return best;
        }

        void Hang(Goredoll d, bool teleport)
        {
            var neck = NeckOf(d);
            if (neck == null || End == null || noose != null) return;
            if (teleport)
            {
                // Lift the whole body so the neck sits in the noose.
                var off = NoosePoint - neck.Body.worldCenterOfMass;
                foreach (var p in d.Parts.Values)
                {
                    if (p == null || p.Body == null || p.Severed) continue;
                    p.Body.position += off;
                    p.transform.position += off;
                    RB.SetVel(p.Body, Vector3.zero);
                    p.Body.angularVelocity = Vector3.zero;
                }
            }
            noose = neck.gameObject.AddComponent<ConfigurableJoint>();
            noose.connectedBody = End;
            noose.autoConfigureConnectedAnchor = false;
            noose.anchor = neck.transform.InverseTransformPoint(neck.Body.worldCenterOfMass);
            noose.connectedAnchor = End.transform.InverseTransformPoint(NoosePoint);
            noose.xMotion = noose.yMotion = noose.zMotion = ConfigurableJointMotion.Locked;
            noose.angularXMotion = noose.angularYMotion = noose.angularZMotion = ConfigurableJointMotion.Free;
            noose.projectionMode = JointProjectionMode.PositionAndRotation;
            noose.projectionDistance = 0.03f;
            noose.breakForce = 30000f;
            foreach (var rb in rope)
                foreach (var p in d.Parts.Values)
                    if (p != null && p.Col != null) Physics.IgnoreCollision(rb.GetComponent<Collider>(), p.Col);
            if (d.Ragdoll != null) d.Ragdoll.Fall(1e6f); // stays down: hangs limp instead of trying to stand
            if (d.Brain != null) d.Brain.enabled = false;
            hung = d; hungPart = neck;
            allHung.Add(d);
            AudioManager.Play("body_thud_soft", neck.Body.position, 0.8f, 0.8f);
            CameraFX.ShakeAt(neck.Body.position, 0.1f, 6f);
        }

        void Release()
        {
            if (noose != null) Destroy(noose);
            noose = null;
            if (hung != null)
            {
                allHung.Remove(hung);
                cooldown[hung] = Time.time + 3f;
                if (hung.Ragdoll != null && !hung.IsDead) hung.Ragdoll.Fall(1.5f);
                if (hung.Brain != null && !hung.IsDead && !hung.NetProxy) hung.Brain.enabled = true;
                if (hungPart != null && hungPart.Body != null) AudioManager.Play("body_thud", hungPart.Body.position, 0.6f);
            }
            hung = null; hungPart = null;
        }

        /// <summary>The rope was shot or cut.</summary>
        public void Cut()
        {
            if (hung == null) return;
            if (End != null) AudioManager.Play("slice", End.position, 0.7f, 1.2f);
            Release();
        }

        void Update()
        {
            if (hung != null || noose != null)
            {
                bool gone = hung == null || noose == null || hungPart == null || hungPart.Severed || hungPart.Gibbed;
                if (gone) Release();
                return;
            }
            if (NetState.IsClient || End == null || Time.time < nextAuto) return;
            nextAuto = Time.time + 0.12f;
            // A Goredoll's neck brought to the noose (Spawn Gun) gets caught.
            var np = NoosePoint;
            foreach (var d in Goredoll.All)
            {
                if (d == null || d.NetProxy || allHung.Contains(d)) continue;
                if (cooldown.TryGetValue(d, out var until) && Time.time < until) continue;
                var neck = NeckOf(d);
                if (neck == null || (neck.Body.worldCenterOfMass - np).sqrMagnitude > AutoRange * AutoRange) continue;
                if (d.GetComponent<ZBoss>() != null || d.GetComponent<StoryBoss>() != null) continue;
                Hang(d, false);
                break;
            }
            if (cooldown.Count > 32) cooldown.Clear();
        }

        void OnDestroy()
        {
            Release();
            foreach (var rb in rope) if (rb != null) Destroy(rb.gameObject);
            rope.Clear();
        }
    }

    /// <summary>A rope segment: INTERACT works on it too, and shooting or cutting it drops whoever is hanging.</summary>
    public sealed class HangmanRope : MonoBehaviour, IDamageable, IInteractable
    {
        public HangmanRig Rig;
        public bool IsAlive => Rig != null;
        public string Prompt => Rig != null ? Rig.Prompt : "";
        public void Interact(GameObject user) { if (Rig != null) Rig.Interact(user); }

        public void TakeDamage(DamageInfo info)
        {
            if (Rig == null || NetState.IsClient) return;
            if (info.IsBallistic || info.IsSharp || info.Type == DamageType.Explosion) Rig.Cut();
        }
    }
}
