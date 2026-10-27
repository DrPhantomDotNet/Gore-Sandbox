using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// One rope: a ConfigurableJoint whose linear limit is the rope length (pulls, never pushes) plus a sagging
    /// LineRenderer. End A is always a rigidbody (the joint lives on it); end B is another rigidbody or a fixed
    /// world point (B body null). Host/singleplayer only: the joint is plain physics, so clients simply see the
    /// result in the synced positions.
    /// </summary>
    public sealed class Rope : MonoBehaviour
    {
        public static readonly List<Rope> All = new List<Rope>();
        public const int MaxRopes = 40;
        const int Points = 18;

        ConfigurableJoint joint;
        Rigidbody a, b;
        Vector3 localA, localB;   // localB is a world point when b is null
        bool worldAnchored;
        float length;
        LineRenderer line;

        public Vector3 EndA => a != null ? a.transform.TransformPoint(localA) : Vector3.zero;
        public Vector3 EndB => worldAnchored ? localB : (b != null ? b.transform.TransformPoint(localB) : EndA);
        public bool Touches(Rigidbody rb) => rb != null && (rb == a || rb == b);

        /// <summary>Ties two points together. <paramref name="bodyA"/> must not be null; <paramref name="bodyB"/> null = world anchor (pointB in world space).</summary>
        public static Rope Create(Rigidbody bodyA, Vector3 worldA, Rigidbody bodyB, Vector3 worldB, float ropeLength)
        {
            if (bodyA == null) return null;
            while (All.Count >= MaxRopes) { var old = All[0]; All.RemoveAt(0); if (old != null) Destroy(old.gameObject); }

            var go = new GameObject("Rope");
            go.layer = Layers.Effects;
            if (WorldRoot.Current != null) go.transform.SetParent(WorldRoot.Current.DynamicRoot, false);
            var r = go.AddComponent<Rope>();
            r.a = bodyA; r.b = bodyB;
            r.worldAnchored = bodyB == null;
            r.localA = bodyA.transform.InverseTransformPoint(worldA);
            r.localB = bodyB != null ? bodyB.transform.InverseTransformPoint(worldB) : worldB;
            r.length = Mathf.Max(0.15f, ropeLength);

            var j = bodyA.gameObject.AddComponent<ConfigurableJoint>();
            j.autoConfigureConnectedAnchor = false;
            j.anchor = r.localA;
            j.connectedBody = bodyB;
            j.connectedAnchor = r.localB; // body-local, or world space when connectedBody is null
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Limited;
            j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Free;
            j.linearLimit = new SoftJointLimit { limit = r.length, bounciness = 0f, contactDistance = 0.02f };
            j.linearLimitSpring = new SoftJointLimitSpring { spring = 0f, damper = 0f };
            j.enableCollision = true;
            j.enablePreprocessing = false;
            j.breakForce = Mathf.Infinity;
            j.breakTorque = Mathf.Infinity;
            r.joint = j;
            bodyA.WakeUp();
            if (bodyB != null) bodyB.WakeUp();

            r.line = go.AddComponent<LineRenderer>();
            r.line.positionCount = Points;
            r.line.useWorldSpace = true;
            r.line.sharedMaterial = Mats.ParticleWith(TextureFactory.White);
            r.line.widthMultiplier = 0.022f;
            r.line.numCapVertices = 2;
            r.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.line.receiveShadows = false;
            var col = new Color(0.5f, 0.37f, 0.22f);
            r.line.startColor = col; r.line.endColor = col * 0.9f;
            All.Add(r);
            r.UpdateLine();
            return r;
        }

        public void Cut()
        {
            if (this == null) return;
            Destroy(gameObject);
        }

        void LateUpdate()
        {
            if (joint == null || a == null || (!worldAnchored && b == null)) { Destroy(gameObject); return; }
            UpdateLine();
        }

        void UpdateLine()
        {
            var p0 = EndA; var p1 = EndB;
            float d = Vector3.Distance(p0, p1);
            // Parabola sag approximating the slack: deeper the more rope there is than distance.
            float slack = Mathf.Max(0f, length - d);
            float sag = Mathf.Min(0.5f * Mathf.Sqrt(slack * (length + d)), length * 0.5f);
            for (int i = 0; i < Points; i++)
            {
                float t = i / (float)(Points - 1);
                var p = Vector3.Lerp(p0, p1, t) + Vector3.down * sag * 4f * t * (1f - t);
                line.SetPosition(i, p);
            }
        }

        /// <summary>Distance from a ray to the rope's drawn polyline (for picking with RMB).</summary>
        public float RayDistance(Vector3 origin, Vector3 dir, float maxDist)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Points; i++)
            {
                var p = line.GetPosition(i);
                var rel = p - origin;
                float along = Vector3.Dot(rel, dir);
                if (along < 0f || along > maxDist) continue;
                best = Mathf.Min(best, (rel - dir * along).magnitude);
            }
            return best;
        }

        void OnDestroy()
        {
            All.Remove(this);
            if (joint != null) Destroy(joint);
        }

        public static void ClearAll()
        {
            foreach (var r in All.ToArray()) if (r != null) Destroy(r.gameObject);
            All.Clear();
        }
    }

    /// <summary>
    /// Rope Tool. LMB on point A, then LMB on point B ties them with a rope (props, Goredoll body parts, or the
    /// static world as an anchor, so you can hang Goredolls from the ceiling). RMB cuts the rope you aim at (or
    /// cancels a half-made rope). R cycles the length mode (tight / slack / long). Host and singleplayer only.
    /// </summary>
    public sealed class RopeTool : Weapon
    {
        static readonly string[] ModeNames = { "TIGHT", "SLACK", "LONG" };
        static readonly float[] ModeLength = { 1f, 1.25f, 1.6f };
        int mode;
        bool hasFirst;
        Rigidbody firstBody;
        Vector3 firstLocal;   // body-local, or world point when firstBody is null
        LineRenderer preview;
        float pulse;

        public override string AmmoText => Rope.All.Count + " / " + Rope.MaxRopes + " ropes";
        public override string ModeText => (hasFirst ? "PICK END B" : "PICK END A") + " | " + ModeNames[mode];

        protected override void OnAttach()
        {
            var go = new GameObject("RopePreview");
            go.layer = Layers.Effects;
            go.transform.SetParent(Player.transform, false);
            preview = go.AddComponent<LineRenderer>();
            preview.positionCount = 2;
            preview.sharedMaterial = Mats.AdditiveWith(TextureFactory.Spark);
            preview.widthMultiplier = 0.015f;
            preview.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            preview.receiveShadows = false;
            preview.enabled = false;
        }

        protected override void OnEquip()
        {
            Vm.Right.SetPose(HandPose.PistolGrip);
            Vm.Left.SetPose(HandPose.Support);
        }

        protected override void OnHolster()
        {
            hasFirst = false;
            if (preview != null) preview.enabled = false;
        }

        public override void Destroy()
        {
            base.Destroy();
            if (preview != null) Object.Destroy(preview.gameObject);
        }

        Vector3 FirstWorld => firstBody != null ? firstBody.transform.TransformPoint(firstLocal) : firstLocal;

        public override void Tick(InputFrame f, float dt)
        {
            Vm.ResetHands();
            pulse += dt;
            if (hasFirst && firstBody == null && firstWasBody) { hasFirst = false; firstWasBody = false; }

            RaycastHit hit;
            bool aimed = Physics.Raycast(Eye, Aim, out hit, 60f, Layers.GrabMask | Layers.WorldMask, QueryTriggerInteraction.Ignore);

            if (f.Reload)
            {
                mode = (mode + 1) % ModeNames.Length;
                AudioManager.Play("sg_tick", Eye, 0.4f, 1f + mode * 0.15f, 0f);
                GameEvents.ShowToast("Rope length: " + ModeNames[mode].ToLowerInvariant());
            }
            if (f.Fire) Click(aimed, hit);
            if (f.AimPressed)
            {
                if (hasFirst) { hasFirst = false; AudioManager.Play("sg_release", Eye, 0.4f, 1f, 0f); GameEvents.ShowToast("Rope cancelled"); }
                else CutAimed(aimed, hit);
            }

            // Preview line from end A to where we aim.
            preview.enabled = hasFirst;
            if (hasFirst)
            {
                var a = FirstWorld;
                var bpt = aimed ? hit.point : Eye + Aim * 6f;
                preview.SetPosition(0, a);
                preview.SetPosition(1, bpt);
                var c = new Color(1f, 0.7f, 0.25f, 0.6f + Mathf.Sin(pulse * 10f) * 0.2f);
                preview.startColor = c; preview.endColor = c;
            }

            var pos = Def.HipPos + new Vector3(0, Mathf.Sin(pulse * 2f) * 0.002f, 0);
            var rot = Quaternion.identity;
            TickInspect(f, dt, ref pos, ref rot);
            Vm.TargetPos = pos;
            Vm.TargetRot = rot;
            if (Model.Glow != null) Model.Glow.localScale = Vector3.one * (1f + (hasFirst ? 0.1f + Mathf.Sin(pulse * 14f) * 0.05f : 0f));
        }

        bool firstWasBody;

        static bool HostOnly()
        {
            if (!NetState.IsClient) return true;
            GameEvents.ShowToast("Rope Tool: only the host can tie ropes in multiplayer.");
            AudioManager.Ui("ui_error", 0.5f);
            return false;
        }

        void Click(bool aimed, RaycastHit hit)
        {
            if (!aimed) { AudioManager.Play("sg_tick", Eye, 0.3f, 0.7f, 0f); return; }
            if (!HostOnly()) return;
            if (hit.collider.gameObject.layer == Layers.Boundaries && hit.rigidbody == null) { /* invisible walls make poor anchors, but allow it */ }
            var rb = hit.rigidbody;
            if (!hasFirst)
            {
                hasFirst = true;
                firstBody = rb;
                firstWasBody = rb != null;
                firstLocal = rb != null ? rb.transform.InverseTransformPoint(hit.point) : hit.point;
                AudioManager.Play("rope_pick", hit.point, 0.6f);
                FX.Dust(hit.point, 0.05f, new Color(1f, 0.75f, 0.3f, 0.7f), 6);
                Vm.AddRecoil(new Vector3(0, 0.01f, -0.04f), new Vector3(-2f, 0, 0));
                return;
            }

            if (rb != null && rb == firstBody) { GameEvents.ShowToast("Pick a different object for the other end."); AudioManager.Ui("ui_error", 0.4f); return; }
            if (rb == null && firstBody == null) { GameEvents.ShowToast("At least one end must be a physics object (prop or Goredoll)."); AudioManager.Ui("ui_error", 0.4f); return; }

            var pa = FirstWorld;
            var pb = hit.point;
            float len = Vector3.Distance(pa, pb) * ModeLength[mode];
            if (len > 50f) { GameEvents.ShowToast("Too far apart for a rope (max 50 m)."); AudioManager.Ui("ui_error", 0.4f); return; }
            Rope rope = firstBody != null ? Rope.Create(firstBody, pa, rb, pb, len) : Rope.Create(rb, pb, null, pa, len);
            hasFirst = false;
            if (rope == null) return;

            // Goredolls on a rope go limp for a moment so they actually hang instead of balancing.
            foreach (var body in new[] { firstBody, rb })
            {
                if (body == null) continue;
                var doll = body.GetComponentInParent<Goredoll>();
                if (doll != null && doll.Ragdoll != null) doll.Ragdoll.Fall(3f);
            }
            AudioManager.Play("rope_attach", pb, 0.7f);
            FX.Dust(pb, 0.05f, new Color(1f, 0.75f, 0.3f, 0.7f), 8);
            Vm.AddRecoil(new Vector3(0, 0.02f, -0.08f), new Vector3(-4f, 0, 0));
            Stats.Add("ropes");
        }

        void CutAimed(bool aimed, RaycastHit hit)
        {
            if (Rope.All.Count == 0) { AudioManager.Play("sg_tick", Eye, 0.3f, 0.7f, 0f); return; }
            if (!HostOnly()) return;
            Rope best = null;
            float bestD = 0.35f;
            float maxDist = aimed ? hit.distance + 0.5f : 60f;
            foreach (var r in Rope.All)
            {
                if (r == null) continue;
                float d = r.RayDistance(Eye, Aim, maxDist);
                if (d < bestD) { bestD = d; best = r; }
            }
            // Aiming at something tied up cuts its (most recent) rope.
            if (best == null && aimed && hit.rigidbody != null)
                for (int i = Rope.All.Count - 1; i >= 0; i--)
                    if (Rope.All[i] != null && Rope.All[i].Touches(hit.rigidbody)) { best = Rope.All[i]; break; }
            if (best == null) { AudioManager.Play("sg_tick", Eye, 0.3f, 0.7f, 0f); return; }
            var p = (best.EndA + best.EndB) * 0.5f;
            best.Cut();
            AudioManager.Play("rope_cut", p, 0.7f);
            FX.Dust(p, 0.1f, new Color(0.6f, 0.45f, 0.3f, 0.7f), 10);
        }
    }
}
