using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// The VR player (added to the PlayerController while VR is on). Head tracking, room-scale walking (the body
    /// follows your real position), stick movement + snap/smooth turn, tracked hands, the held weapon in your right
    /// hand (two-handed with the left on the foregrip), hip magazine pouches (infinite mags: reach down to a hip,
    /// grip, push the mag into the gun, pull the slide), grabbing Goredolls and props with either hand, punching,
    /// and physical melee: swing a blade to slice (cuts limbs along the blade's path) or thrust to stab (the blade
    /// sticks in until you pull it out).
    /// Controls: L stick move · R stick turn (up/down: next/previous weapon) · R trigger fire · grips grab ·
    /// A jump · B eject mag / reload · X crouch · Y menu · L trigger interact · R stick click kick · L stick click sprint.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class VRRig : MonoBehaviour
    {
        public static VRRig I { get; private set; }

        PlayerController p;
        public Transform Origin => p.CamPivot;
        public Hand L, R;
        public Transform AimT { get; private set; }
        public Vector3 RoomOffset { get; private set; }
        public float MoveYaw { get; private set; }
        float pendingTurn, snapReady, nextSwitch;
        bool sprintToggle, crouchToggle;
        Transform belt, pouchL, pouchR;

        // Weapon state
        Weapon lastWeapon;
        Vector3 gripLocal;          // the weapon's right-hand grip point in socket space
        float bladeTipZ, bladeBaseZ;
        bool twoHand, racking, rackPulled;
        Vector3 rackStart;
        GameObject heldMag;
        readonly Vector3[] prevBlade = new Vector3[5];
        bool prevBladeValid;
        readonly Dictionary<Collider, float> hitCooldown = new Dictionary<Collider, float>();
        // Stab: the blade stays in until pulled out
        ConfigurableJoint stabJoint;
        BodyPart stabPart;
        Vector3 stabAxis, stabPoint;
        float stabSince;

        public sealed class Hand
        {
            public bool IsRight;
            public Transform T;                 // tracked pose (child of the origin)
            public Rigidbody Body;              // kinematic, follows T in FixedUpdate (joints attach to it)
            public Transform Visual;
            public Transform[] Fingers = new Transform[4];
            public Transform Thumb;
            public Vector3 Velocity;            // world, from tracking
            public ConfigurableJoint Grab;
            public Rigidbody Grabbed;
            public Goredoll GrabbedDoll;
            public float PunchCooldown;
            public VRHandState State => IsRight ? VRManager.Right : VRManager.Left;
        }

        // ================================================================== setup

        void Start()
        {
            p = GetComponent<PlayerController>();
            if (p == null || !VRManager.Active) { Destroy(this); return; }
            I = this;
            Origin.localPosition = Vector3.zero;
            Origin.localRotation = Quaternion.identity;
            p.Cam.nearClipPlane = 0.02f;
            p.Cam.cullingMask |= 1 << Layers.Viewmodel; // weapons are in the world in VR
            p.ViewCam.enabled = false;
            L = MakeHand(false);
            R = MakeHand(true);
            p.Viewmodel.transform.SetParent(R.T, false);
            p.Viewmodel.transform.localPosition = Vector3.zero;
            p.Viewmodel.transform.localRotation = Quaternion.identity;
            AimT = new GameObject("VR Aim").transform;
            AimT.SetParent(p.Viewmodel.transform, false);
            MakeBelt();
            MoveYaw = transform.eulerAngles.y;
            Application.onBeforeRender += BeforeRender;
            GameEvents.ShowToast("VR: L stick move · R stick turn · grips grab · B eject mag · reach to your hips for mags · Y menu");
        }

        void OnDestroy()
        {
            Application.onBeforeRender -= BeforeRender;
            if (I == this) I = null;
            ReleaseStab();
            foreach (var h in new[] { L, R })
            {
                if (h == null) continue;
                Release(h, false);
                if (h.T != null) Destroy(h.T.gameObject);
                if (h.Body != null) Destroy(h.Body.gameObject);
            }
            if (heldMag != null) Destroy(heldMag);
            if (belt != null) Destroy(belt.gameObject);
            if (p != null)
            {
                // Back to flat: viewmodel on the camera again.
                if (p.Viewmodel != null && p.ViewCam != null) { p.Viewmodel.transform.SetParent(p.ViewCam.transform, false); p.Viewmodel.transform.localPosition = Vector3.zero; p.Viewmodel.transform.localRotation = Quaternion.identity; }
                if (p.Cam != null) { p.Cam.cullingMask &= ~(1 << Layers.Viewmodel); p.Cam.transform.localPosition = Vector3.zero; p.Cam.transform.localRotation = Quaternion.identity; }
            }
            if (AimT != null) Destroy(AimT.gameObject);
        }

        Hand MakeHand(bool right)
        {
            var h = new Hand { IsRight = right };
            h.T = new GameObject(right ? "VR Hand R" : "VR Hand L").transform;
            h.T.SetParent(Origin, false);
            var vis = new GameObject("Glove");
            vis.layer = Layers.Viewmodel;
            vis.transform.SetParent(h.T, false);
            h.Visual = vis.transform;
            var glove = new Color(0.12f, 0.12f, 0.13f);
            var skin = new Color(0.9f, 0.72f, 0.58f);
            var mb = new MeshBuilder { Color = glove, Gloss = 0.2f };
            mb.BevelBox(new Vector3(right ? 0.005f : -0.005f, 0.0f, -0.02f), new Vector3(0.035f, 0.085f, 0.09f), 0.012f);
            mb.SetColor(skin, 0.3f);
            mb.Capsule(new Vector3(0, 0, -0.09f), new Vector3(0, 0, -0.05f), 0.028f, 0.03f, 8, 2); // wrist
            vis.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("VRPalm");
            vis.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            // Fingers (curl with the grip) and thumb (curls with the trigger).
            var fm = new MeshBuilder { Color = glove, Gloss = 0.2f };
            fm.Capsule(Vector3.zero, new Vector3(0, 0, 0.075f), 0.009f, 0.008f, 6, 2);
            var fingerMesh = fm.ToMesh("VRFinger");
            for (int i = 0; i < 4; i++)
            {
                var f = new GameObject("Finger" + i).transform;
                f.gameObject.layer = Layers.Viewmodel;
                f.SetParent(vis.transform, false);
                f.localPosition = new Vector3(right ? 0.012f : -0.012f, 0.03f - i * 0.02f, 0.02f);
                f.gameObject.AddComponent<MeshFilter>().sharedMesh = fingerMesh;
                f.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                h.Fingers[i] = f;
            }
            h.Thumb = new GameObject("Thumb").transform;
            h.Thumb.gameObject.layer = Layers.Viewmodel;
            h.Thumb.SetParent(vis.transform, false);
            h.Thumb.localPosition = new Vector3(right ? -0.012f : 0.012f, 0.04f, -0.02f);
            h.Thumb.gameObject.AddComponent<MeshFilter>().sharedMesh = fingerMesh;
            h.Thumb.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;

            var bodyGo = new GameObject(right ? "VR Hand Body R" : "VR Hand Body L");
            bodyGo.layer = Layers.PlayerNoclip; // touches nothing; joints only
            bodyGo.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, false);
            h.Body = bodyGo.AddComponent<Rigidbody>();
            h.Body.isKinematic = true;
            h.Body.interpolation = RigidbodyInterpolation.Interpolate;
            return h;
        }

        void MakeBelt()
        {
            belt = new GameObject("VR Belt").transform;
            belt.SetParent(transform, false);
            var mb = new MeshBuilder { Color = new Color(0.2f, 0.17f, 0.12f), Gloss = 0.15f };
            mb.BevelBox(Vector3.zero, new Vector3(0.06f, 0.12f, 0.1f), 0.012f);
            mb.SetColor(new Color(0.3f, 0.3f, 0.32f), 0.4f);
            mb.Box(new Vector3(0, 0.07f, 0), new Vector3(0.03f, 0.03f, 0.05f)); // a mag sticking out
            var mesh = mb.ToMesh("VRPouch");
            Transform Pouch(string n)
            {
                var t = new GameObject(n).transform;
                t.gameObject.layer = Layers.Viewmodel;
                t.SetParent(belt, false);
                t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                return t;
            }
            pouchL = Pouch("Mag Pouch L");
            pouchR = Pouch("Mag Pouch R");
        }

        public void ResetRoomOffset() => RoomOffset = Vector3.zero;

        // ================================================================== per frame

        /// <summary>Turning since the last call (PlayerController adds it to its yaw).</summary>
        public float ConsumeTurn() { float t = pendingTurn; pendingTurn = 0f; return t; }

        public bool Sprint => sprintToggle;
        public bool CrouchToggle => crouchToggle;

        void Update()
        {
            if (!VRManager.Active || p == null) { Destroy(this); return; }
            var d = Settings.Data;
            var Ls = VRManager.Left; var Rs = VRManager.Right;
            bool menus = UIRoot.AnyBlocking;

            // Turning (right stick X): snap or smooth.
            float sx = Rs.Stick.x;
            if (!menus)
            {
                if (d.vrSnapTurn)
                {
                    if (Mathf.Abs(sx) > 0.7f && snapReady <= 0f) { pendingTurn += Mathf.Sign(sx) * d.vrSnapAngle; snapReady = 1f; }
                    if (Mathf.Abs(sx) < 0.3f) snapReady = 0f;
                }
                else if (Mathf.Abs(sx) > 0.15f) pendingTurn += sx * d.vrTurnSpeed * Time.deltaTime;
                // Weapon switching (right stick up/down).
                float sy = Rs.Stick.y;
                if (Mathf.Abs(sy) > 0.75f && Time.time > nextSwitch && !(p.Inventory.Current is SpawnGun)) { nextSwitch = Time.time + 0.35f; Cycle(sy > 0 ? 1 : -1); }
                if (Ls.StickClickDown) sprintToggle = !sprintToggle;
                if (Ls.Stick.magnitude < 0.2f) sprintToggle = false;
                if (Ls.PrimaryDown) crouchToggle = !crouchToggle;
            }

            UpdateTracking();
            UpdateBelt();
            if (!menus && !p.IsDead)
            {
                HandleWeapon();
                foreach (var h in new[] { L, R }) HandleGrab(h);
                Melee();
                Punch(L); Punch(R);
                UpdateStab();
            }
            UpdateFingers(L); UpdateFingers(R);
        }

        void Cycle(int dir)
        {
            var inv = p.Inventory;
            int cur = -1;
            for (int i = 0; i < Inventory.SlotCount; i++) if (inv.Slots[i] == inv.Current && inv.Current != null) cur = i;
            for (int k = 1; k <= Inventory.SlotCount; k++)
            {
                int i = ((cur + dir * k) % Inventory.SlotCount + Inventory.SlotCount) % Inventory.SlotCount;
                if (inv.Slots[i] != null) { inv.Select(i); VRManager.Haptic(true, 0.2f, 0.03f); return; }
            }
        }

        /// <summary>Head, hands, room-scale and body height.</summary>
        void UpdateTracking()
        {
            var head = VRManager.Head;
            var headLocal = head.Pos + Vector3.up * Settings.Data.vrHeightOffset;
            // Room-scale: move the body to stand under the head (walls stop it), shifting the tracking space back.
            var flat = new Vector3(headLocal.x + RoomOffset.x, 0f, headLocal.z + RoomOffset.z);
            if (flat.sqrMagnitude > 0.0025f && p.CC.enabled && !p.Noclip)
            {
                var before = transform.position;
                p.CC.Move(transform.rotation * flat);
                var moved = Quaternion.Inverse(transform.rotation) * (transform.position - before);
                RoomOffset -= new Vector3(moved.x, 0f, moved.z);
            }
            else if (p.Noclip) { transform.position += transform.rotation * flat; RoomOffset -= flat; }
            Origin.localPosition = RoomOffset;
            Origin.localRotation = Quaternion.identity;
            // Body height follows the head (real crouching works).
            float h = Mathf.Clamp(headLocal.y + 0.12f, 0.9f, 2.05f);
            if (crouchToggle) h = Mathf.Min(h, 1.15f);
            if (!p.Noclip && Mathf.Abs(p.CC.height - h) > 0.02f) { p.CC.height = h; p.CC.center = Vector3.up * h / 2f; }
            // Move direction: where you look (or where the left hand points).
            var dirRot = Settings.Data.vrHandDirection && L.State.Valid ? L.State.Rot : head.Rot;
            MoveYaw = transform.eulerAngles.y + (dirRot * Vector3.forward).Yaw();
            SetCamera(headLocal, head.Rot);
            SetHand(L); SetHand(R);
        }

        void SetCamera(Vector3 localPos, Quaternion localRot)
        {
            if (p.Cam == null) return;
            if (crouchToggle) localPos.y = Mathf.Min(localPos.y, 1.05f);
            p.Cam.transform.localPosition = localPos;
            p.Cam.transform.localRotation = localRot;
        }

        void BeforeRender()
        {
            if (p == null || !VRManager.Active) return;
            if (VRManager.HeadPose(out var pos, out var rot)) SetCamera(pos + Vector3.up * Settings.Data.vrHeightOffset, rot);
        }

        void SetHand(Hand h)
        {
            var s = h.State;
            var prev = h.T.position;
            h.T.gameObject.SetActive(s.Valid);
            if (!s.Valid) return;
            h.T.localPosition = s.Pos + Vector3.up * Settings.Data.vrHeightOffset;
            h.T.localRotation = s.Rot;
            h.Velocity = Origin.TransformDirection(s.Vel) + p.CC.velocity;
        }

        void FixedUpdate()
        {
            foreach (var h in new[] { L, R })
            {
                if (h == null || h.Body == null || h.T == null) continue;
                h.Body.MovePosition(h.T.position);
                h.Body.MoveRotation(h.T.rotation);
            }
        }

        void UpdateFingers(Hand h)
        {
            if (h == null || h.Visual == null) return;
            bool holding = h.IsRight && HoldsWeapon;
            float g = holding ? 1f : h.State.Grip, t = holding ? 0.4f : h.State.Trigger;
            for (int i = 0; i < 4; i++) if (h.Fingers[i] != null) h.Fingers[i].localRotation = Quaternion.Euler(Mathf.Lerp(5f, 95f, i == 0 ? t : g), 0, 0);
            if (h.Thumb != null) h.Thumb.localRotation = Quaternion.Euler(Mathf.Lerp(-10f, 50f, g), h.IsRight ? 30f : -30f, 0);
            // The right glove hides inside the weapon's grip; empty hands always show.
            h.Visual.gameObject.SetActive(!(h.IsRight && HoldsWeapon && !(p.Inventory.Current is Fists)));
        }

        bool HoldsWeapon => p.Inventory != null && p.Inventory.Current != null && !(p.Inventory.Current is Fists);

        // ================================================================== belt (hip mag pouches)

        void UpdateBelt()
        {
            var cam = p.Cam.transform;
            var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) fwd = transform.forward;
            fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd);
            float headH = Mathf.Max(0.6f, cam.position.y - transform.position.y);
            var waist = new Vector3(cam.position.x, transform.position.y + headH * 0.56f, cam.position.z) - fwd * 0.03f;
            belt.position = waist;
            belt.rotation = Quaternion.LookRotation(fwd);
            pouchL.localPosition = new Vector3(-0.21f, 0f, 0.02f);
            pouchR.localPosition = new Vector3(0.21f, 0f, 0.02f);
            bool show = p.Inventory.Current is Firearm fa && fa.VRManualMag;
            pouchL.gameObject.SetActive(show); pouchR.gameObject.SetActive(show);
        }

        // ================================================================== weapons

        void HandleWeapon()
        {
            var w = p.Inventory.Current;
            if (w != lastWeapon) { OnWeaponChanged(w); lastWeapon = w; }
            if (w == null || w.Model == null) return;
            if (w is Fists) { w.Model.gameObject.SetActive(false); return; }
            var fa = w as Firearm;

            // B: eject the magazine (manual) / reload (other guns: handled as Reload in PlayerInput).
            if (fa != null && fa.VRManualMag && VRManager.Right.SecondaryDown) { fa.VREjectMag(); VRManager.Haptic(true, 0.35f, 0.05f); }

            var Ls = VRManager.Left;
            if (fa != null && fa.VRManualMag && !twoHand && !racking && L.Grabbed == null)
            {
                // Reach to a hip pouch and grip: a fresh magazine (infinite).
                if (heldMag == null && Ls.GripDown && (Near(L.T.position, pouchL.position, 0.2f) || Near(L.T.position, pouchR.position, 0.2f)))
                    TakeMag(fa);
                if (heldMag != null)
                {
                    var well = fa.Model.MagWell != null ? fa.Model.MagWell.position : fa.Model.Mag != null ? fa.Model.Mag.parent.TransformPoint(fa.Model.MagRest) : fa.Model.transform.position;
                    if (!fa.VRMagIn && Near(heldMag.transform.position, well, 0.11f))
                    {
                        fa.VRInsertMag();
                        Destroy(heldMag); heldMag = null;
                        VRManager.Haptic(false, 0.6f, 0.06f); VRManager.Haptic(true, 0.5f, 0.06f);
                    }
                    else if (Ls.GripUp) DropHeldMag();
                }
            }
            else if (heldMag != null && Ls.GripUp) DropHeldMag();

            // Rack: grab the slide / charging handle and pull it back.
            if (fa != null && heldMag == null && L.Grabbed == null)
            {
                var handle = fa.Model.Slide != null ? fa.Model.Slide : fa.Model.Charge;
                if (!racking && !twoHand && Ls.GripDown && handle != null && Near(L.T.position, handle.position, 0.11f))
                {
                    racking = true; rackPulled = false; rackStart = L.T.position;
                    VRManager.Haptic(false, 0.25f, 0.03f);
                }
                if (racking)
                {
                    var axis = fa.Model.transform.forward;
                    float pull = Mathf.Clamp(Vector3.Dot(rackStart - L.T.position, axis), 0f, 0.07f);
                    if (handle != null)
                    {
                        var rest = handle == fa.Model.Slide ? fa.Model.SlideRest : fa.Model.ChargeRest;
                        handle.localPosition = rest - handle.parent.InverseTransformDirection(axis) * Mathf.Min(pull, 0.05f) / Mathf.Max(0.01f, handle.parent.lossyScale.x);
                    }
                    if (!rackPulled && pull > 0.035f) { rackPulled = true; AudioManager.Play("slide_back", handle != null ? handle.position : L.T.position, 0.7f); VRManager.Haptic(false, 0.5f, 0.04f); }
                    if (Ls.GripUp || (rackPulled && pull < 0.012f))
                    {
                        if (rackPulled) { fa.VRRack(); VRManager.Haptic(false, 0.6f, 0.05f); VRManager.Haptic(true, 0.4f, 0.05f); }
                        if (handle != null) handle.localPosition = handle == fa.Model.Slide ? fa.Model.SlideRest : fa.Model.ChargeRest;
                        racking = false;
                    }
                }
            }

            // Two hands: left grip on the foregrip steadies and aims the gun.
            if (!twoHand && !racking && heldMag == null && L.Grabbed == null && Ls.GripDown && w.Model.SupportL != null && Near(L.T.position, w.Model.SupportL.position, 0.16f))
            { twoHand = true; VRManager.Haptic(false, 0.2f, 0.03f); }
            if (twoHand && !Ls.GripHeld) twoHand = false;
        }

        void OnWeaponChanged(Weapon w)
        {
            twoHand = racking = false;
            DropHeldMag();
            ReleaseStab();
            prevBladeValid = false;
            gripLocal = Vector3.zero;
            if (w == null || w.Model == null) return;
            var m = w.Model;
            var mt = m.transform;
            gripLocal = m.GripR != null ? mt.localPosition + mt.localRotation * Vector3.Scale(mt.localScale, m.GripR.localPosition) : Vector3.zero;
            // Blade extent along the weapon's forward axis (socket space).
            float zMin = float.MaxValue, zMax = float.MinValue;
            foreach (var mf in m.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var sp = p.Viewmodel.Socket.InverseTransformPoint(mf.transform.TransformPoint(c));
                    zMin = Mathf.Min(zMin, sp.z); zMax = Mathf.Max(zMax, sp.z);
                }
            }
            bladeTipZ = zMax > zMin ? zMax : 0.3f;
            bladeBaseZ = Mathf.Max(gripLocal.z + 0.07f, zMin);
        }

        void TakeMag(Firearm fa)
        {
            if (fa.Model.MagMesh == null) return;
            heldMag = new GameObject("Held Magazine");
            heldMag.layer = Layers.Viewmodel;
            heldMag.transform.SetParent(L.T, false);
            heldMag.transform.localPosition = new Vector3(0f, -0.02f, 0.04f);
            heldMag.transform.localRotation = Quaternion.identity;
            heldMag.transform.localScale = fa.Model.Mag != null ? fa.Model.Mag.lossyScale : Vector3.one;
            heldMag.AddComponent<MeshFilter>().sharedMesh = fa.Model.MagMesh;
            var src = fa.Model.Mag != null ? fa.Model.Mag.GetComponent<Renderer>() : null;
            heldMag.AddComponent<MeshRenderer>().sharedMaterial = src != null ? src.sharedMaterial : Mats.LitMetal;
            AudioManager.Play("cloth", L.T.position, 0.5f);
            VRManager.Haptic(false, 0.4f, 0.04f);
        }

        void DropHeldMag()
        {
            if (heldMag == null) return;
            var go = heldMag;
            heldMag = null;
            go.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, true);
            go.layer = Layers.Debris;
            var mf = go.GetComponent<MeshFilter>();
            var bc = go.AddComponent<BoxCollider>();
            if (mf != null && mf.sharedMesh != null) { bc.center = mf.sharedMesh.bounds.center; bc.size = mf.sharedMesh.bounds.size; }
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.25f;
            RB.SetVel(rb, L.Velocity);
            go.AddComponent<Surface>().Type = SurfaceType.Metal;
            go.AddComponent<ImpactSounds>();
            Debris.Register(go, 40f, Debris.GroupMag);
        }

        static bool Near(Vector3 a, Vector3 b, float r) => (a - b).sqrMagnitude < r * r;

        void LateUpdate()
        {
            if (p == null || p.Viewmodel == null) return;
            var sock = p.Viewmodel.Socket;
            var w = p.Inventory.Current;
            // The weapon sits in the right hand by its grip; recoil kicks it up a little.
            var recoil = p.Viewmodel.RecoilRot;
            var rot = Quaternion.Euler(Settings.Data.vrGunAngle, 0, 0) * Quaternion.Euler(recoil * 0.5f);
            sock.localRotation = rot;
            sock.localPosition = -(rot * gripLocal) + p.Viewmodel.RecoilPos * 0.4f;
            if (twoHand && w != null && w.Model != null && w.Model.SupportL != null && L.T.gameObject.activeInHierarchy)
            {
                var grip = R.T.position;
                var support = w.Model.SupportL.position;
                var want = L.T.position;
                var fix = Quaternion.FromToRotation(support - grip, want - grip);
                sock.rotation = fix * sock.rotation;
                sock.position = grip - sock.rotation * gripLocal;
            }
            var muzzle = w != null && w.Model != null && w.Model.Muzzle != null ? w.Model.Muzzle.position : sock.TransformPoint(new Vector3(0, gripLocal.y, bladeTipZ));
            AimT.SetPositionAndRotation(muzzle, sock.rotation);
        }

        // ================================================================== grabbing

        const int GrabMask = (1 << Layers.Ragdoll) | (1 << Layers.Props) | (1 << Layers.Debris);
        static readonly Collider[] overlap = new Collider[16];

        void HandleGrab(Hand h)
        {
            var s = h.State;
            if (!s.Valid) return;
            bool free = !h.IsRight || !HoldsWeapon;
            if (!h.IsRight && (heldMag != null || racking || twoHand)) free = false;
            if (h.Grab == null && free && s.GripDown) TryGrab(h);
            if ((h.Grab != null || h.Grabbed != null) && !s.GripHeld) Release(h, true);
            if (h.Grabbed == null && h.Grab != null) Release(h, false);
        }

        void TryGrab(Hand h)
        {
            if (NetState.IsClient) { GameEvents.ShowToast("Grabbing with your hands works in singleplayer or as the host."); return; }
            var pos = h.T.position;
            int n = Physics.OverlapSphereNonAlloc(pos, 0.11f, overlap, GrabMask, QueryTriggerInteraction.Ignore);
            Rigidbody best = null; Vector3 bestPoint = pos; float bd = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = overlap[i];
                var rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic || rb == L.Body || rb == R.Body) continue;
                var cp = c.ClosestPoint(pos);
                float d = (cp - pos).sqrMagnitude;
                if (d < bd) { bd = d; best = rb; bestPoint = cp; }
            }
            if (best == null) return;
            var j = best.gameObject.AddComponent<ConfigurableJoint>();
            j.connectedBody = h.Body;
            j.autoConfigureConnectedAnchor = false;
            j.anchor = best.transform.InverseTransformPoint(bestPoint);
            j.connectedAnchor = h.Body.transform.InverseTransformPoint(bestPoint);
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
            j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Locked;
            j.projectionMode = JointProjectionMode.PositionAndRotation;
            j.breakForce = 40000f;
            h.Grab = j; h.Grabbed = best;
            var part = best.GetComponent<BodyPart>();
            h.GrabbedDoll = part != null ? part.Doll : null;
            if (h.GrabbedDoll != null && h.GrabbedDoll.Ragdoll != null) h.GrabbedDoll.Ragdoll.Fall(1.2f);
            AudioManager.Play(part != null ? "cloth" : "grab", bestPoint, 0.6f);
            VRManager.Haptic(h.IsRight, 0.45f, 0.05f);
        }

        void Release(Hand h, bool throwIt)
        {
            if (h.Grab != null) Destroy(h.Grab);
            if (throwIt && h.Grabbed != null)
            {
                var v = h.Velocity * 1.25f;
                if (h.GrabbedDoll != null)
                {
                    foreach (var part in h.GrabbedDoll.Parts.Values)
                        if (part != null && part.Body != null && !part.Severed) RB.SetVel(part.Body, Vector3.Lerp(RB.Vel(part.Body), v, 0.6f));
                    if (v.magnitude > 4f && h.GrabbedDoll.Ragdoll != null) h.GrabbedDoll.Ragdoll.Fall(2.5f);
                }
                else RB.SetVel(h.Grabbed, v);
            }
            h.Grab = null; h.Grabbed = null; h.GrabbedDoll = null;
        }

        // ================================================================== punching

        void Punch(Hand h)
        {
            h.PunchCooldown -= Time.deltaTime;
            var s = h.State;
            if (!s.Valid || h.PunchCooldown > 0f || h.Grab != null || !s.GripHeld) return;
            if (h.IsRight && HoldsWeapon) return;
            float speed = h.Velocity.magnitude;
            if (speed < 2.4f) return;
            if (!Physics.SphereCast(h.T.position - h.Velocity.normalized * 0.08f, 0.05f, h.Velocity.normalized, out var hit, 0.16f, Layers.ShootMask, QueryTriggerInteraction.Ignore)) return;
            h.PunchCooldown = 0.25f;
            var dir = h.Velocity.normalized;
            float dmg = Mathf.Clamp(6f + speed * 4f, 6f, 45f);
            using (NetState.Local())
            {
                var target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null) target.TakeDamage(new DamageInfo { Amount = dmg, Force = dir * speed * 9f, Point = hit.point, Normal = hit.normal, Direction = dir, Type = DamageType.Blunt, Source = p, Weapon = "Fist" });
                else if (hit.rigidbody != null) NetPhysics.Impulse(hit.rigidbody, dir * speed * 3f, hit.point);
            }
            var part = hit.collider.GetComponent<BodyPart>();
            if (part != null && part.Doll != null && speed > 5f) part.Doll.Ragdoll.OnImpact(speed);
            AudioManager.Play(part != null ? "punch_hit" : "kick_hit", hit.point, Mathf.Clamp01(speed / 6f));
            VRManager.Haptic(h.IsRight, Mathf.Clamp01(speed / 6f), 0.06f);
        }

        // ================================================================== melee: slicing and stabbing

        bool IsBlade(Weapon w)
        {
            if (w is Knife || w is Katana || w is DualKatanas) return true;
            if (w is MeleeWeapon mw) return mw.Spec != null && mw.Spec.Blade;
            if (w is Mods.ModMelee) { var d = Mods.ModWeapons.Design(w.Def); return d != null && d.meleeType == 0; }
            return w.Def != null && w.Def.Kind == WeaponKind.Knife;
        }

        bool IsMelee(Weapon w) => w != null && !(w is Fists) && (w is Knife || w is Katana || w is DualKatanas || w is MeleeWeapon || w is Mods.ModMelee || w.Def.Kind == WeaponKind.Knife || w.Def.Kind == WeaponKind.Melee);

        void Melee()
        {
            var w = p.Inventory.Current;
            if (!IsMelee(w) || w.Model == null) { prevBladeValid = false; return; }
            if (stabJoint != null) { prevBladeValid = false; return; }
            var sock = p.Viewmodel.Socket;
            bool blade = IsBlade(w);
            float dt = Mathf.Max(Time.deltaTime, 0.001f);
            var tipW = sock.TransformPoint(new Vector3(0, gripLocal.y, bladeTipZ));
            var baseW = sock.TransformPoint(new Vector3(0, gripLocal.y, bladeBaseZ));
            var axis = (tipW - baseW).normalized;
            for (int i = 0; i < 5; i++)
            {
                var pt = Vector3.Lerp(baseW, tipW, i / 4f);
                if (prevBladeValid && (pt - prevBlade[i]).sqrMagnitude > 0.00002f)
                {
                    var from = prevBlade[i];
                    var move = pt - from;
                    if (Physics.Linecast(from, pt, out var hit, Layers.ShootMask, QueryTriggerInteraction.Ignore))
                        OnBladeHit(w, blade, hit, move / dt, axis, i == 4);
                }
                prevBlade[i] = pt;
            }
            prevBladeValid = true;
            if (hitCooldown.Count > 64) hitCooldown.Clear();
        }

        void OnBladeHit(Weapon w, bool blade, RaycastHit hit, Vector3 vel, Vector3 axis, bool tip)
        {
            float speed = vel.magnitude;
            if (speed < 1.3f) return;
            if (hitCooldown.TryGetValue(hit.collider, out var until) && Time.time < until) return;
            hitCooldown[hit.collider] = Time.time + 0.22f;
            var dir = vel / speed;
            bool stab = blade && tip && Vector3.Dot(dir, axis) > 0.65f;
            float baseDmg = w.Def != null && w.Def.Damage > 0 ? w.Def.Damage : 35f;
            float dmg = baseDmg * Mathf.Clamp(speed / 3f, 0.35f, 2.4f) * (stab ? 1.35f : 1f);
            var part = hit.collider.GetComponent<BodyPart>();
            using (NetState.Local())
            {
                var target = hit.collider.GetComponentInParent<IDamageable>();
                var type = !blade ? DamageType.Blunt : stab ? DamageType.Stab : DamageType.Slash;
                if (target != null)
                    target.TakeDamage(new DamageInfo { Amount = dmg, Force = dir * speed * (blade ? 3f : 9f), Point = hit.point, Normal = hit.normal, Direction = stab ? axis : dir, Type = type, Source = p, Weapon = w.Def != null ? w.Def.Name : "Blade" });
                else if (hit.rigidbody != null) NetPhysics.Impulse(hit.rigidbody, dir * speed * 2f, hit.point);

                // Procedural cuts: the blade's path through a limb is the cut plane.
                if (blade && !stab && part != null && part.Doll != null && !NetState.IsClient && Settings.Dismemberment && !part.Severed)
                {
                    float need = Settings.SuperUltra ? 2.2f : Settings.Unreal ? 3f : 4.2f;
                    var planeNormal = Vector3.Cross(dir, axis);
                    if (speed > need && planeNormal.sqrMagnitude > 0.01f)
                    {
                        if (part.IsLimb) part.Doll.Dismember.Slice(part, hit.point, planeNormal.normalized);
                        else if (part.Type == PartType.Neck && speed > need + 1f) part.Doll.Dismember.Sever(part, hit.point, hit.normal, true);
                    }
                }
            }
            if (part != null && Settings.GoreEnabled)
            {
                BloodFX.Spurt(hit.point, (stab ? -axis : Vector3.Cross(dir, axis).normalized + Random.insideUnitSphere * 0.3f).normalized, Mathf.Clamp01(speed / 5f) * Settings.BloodScale);
                GoreKit.WallSpray(hit.point, dir, dmg);
            }
            AudioManager.Play(!blade ? "punch_hit" : stab ? "stab" : "slice", hit.point, Mathf.Clamp01(0.4f + speed / 6f), Random.Range(0.9f, 1.1f));
            VRManager.Haptic(true, Mathf.Clamp01(0.3f + speed / 6f), stab ? 0.12f : 0.07f);
            if (stab && part != null && part.Body != null && !NetState.IsClient) BeginStab(part, hit.point, axis);
        }

        // A stab: the body hangs on the blade until you pull it back out.
        void BeginStab(BodyPart part, Vector3 point, Vector3 axis)
        {
            ReleaseStab();
            var j = part.Body.gameObject.AddComponent<ConfigurableJoint>();
            j.connectedBody = R.Body;
            j.autoConfigureConnectedAnchor = false;
            j.anchor = part.Body.transform.InverseTransformPoint(point);
            j.connectedAnchor = R.Body.transform.InverseTransformPoint(point);
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Limited;
            j.linearLimit = new SoftJointLimit { limit = 0.03f };
            j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Free;
            j.breakForce = 25000f;
            stabJoint = j; stabPart = part; stabAxis = axis; stabPoint = point; stabSince = Time.time;
            if (part.Doll != null && part.Doll.Ragdoll != null) part.Doll.Ragdoll.Fall(1.5f);
        }

        void UpdateStab()
        {
            if (stabJoint == null) { if (stabPart != null) stabPart = null; return; }
            if (stabPart == null || stabPart.Severed || stabPart.Body == null) { ReleaseStab(); return; }
            VRManager.Haptic(true, 0.15f + Mathf.PerlinNoise(Time.time * 20f, 0) * 0.2f, 0.03f); // grinding feel
            if (Settings.GoreEnabled && Random.value < 0.15f) BloodFX.Drip(stabPoint, Vector3.zero);
            // Pulling back along the blade (or holding it in for too long) pulls it out.
            var back = Vector3.Dot(R.Velocity, -stabAxis);
            if (back > 1.6f || Time.time - stabSince > 2.5f || !VRManager.Right.Valid)
            {
                var point = stabJoint.transform.TransformPoint(stabJoint.anchor);
                using (NetState.Local())
                    stabPart.TakeDamage(new DamageInfo { Amount = 12f, Point = point, Normal = -stabAxis, Direction = -stabAxis, Type = DamageType.Slash, Source = p, Weapon = "Blade" });
                if (Settings.GoreEnabled) { BloodFX.Spurt(point, -stabAxis, 1.2f * Settings.BloodScale); BloodFX.Burst(point, -stabAxis, 0.6f * Settings.BloodScale, Settings.BloodColor); }
                AudioManager.Play("slice", point, 0.7f, 0.8f);
                VRManager.Haptic(true, 0.7f, 0.08f);
                ReleaseStab();
            }
        }

        void ReleaseStab()
        {
            if (stabJoint != null) Destroy(stabJoint);
            stabJoint = null; stabPart = null;
            prevBladeValid = false;
        }
    }

    static class VRMath
    {
        /// <summary>Yaw (degrees) of a direction on the ground plane.</summary>
        public static float Yaw(this Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
    }
}
