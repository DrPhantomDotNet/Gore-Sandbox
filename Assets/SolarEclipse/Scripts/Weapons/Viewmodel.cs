using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// First-person arms + weapon on the viewmodel layer. The active weapon supplies a target pose each frame;
    /// this adds sway, bob, sprint pose, recoil springs, landing dip and the equip raise, then places the hands
    /// on the weapon's grip anchors (or on per-animation targets) and stretches the forearms toward the elbows.
    /// </summary>
    public sealed class Viewmodel : MonoBehaviour
    {
        public Transform Socket;               // weapon model parent (camera local)
        public HandRig Right, Left;
        public Transform Leg;
        PlayerController player;

        // Pose requested by the weapon this frame (camera-local).
        public Vector3 TargetPos = new Vector3(0.13f, -0.14f, 0.3f);
        public Quaternion TargetRot = Quaternion.identity;
        public float AimWeight;                // 0..1
        public bool RightOnAnchor = true, LeftOnAnchor = true;
        public Vector3 RightPos, LeftPos;      // camera-local when not on anchor
        public Quaternion RightRot = Quaternion.identity, LeftRot = Quaternion.identity;
        public bool HideLeft, HideRight;
        public bool RightInSocket, LeftInSocket;   // RightPos/LeftPos are in weapon (socket) space instead of camera space

        Vector3 swayPos, swayVel, recoilPos, recoilPosVel;
        Vector3 swayRot, swayRotVel, recoilRot, recoilRotVel;
        float bob, sprint, equip = 1f, landDip, kickT = 1f;
        bool dropKick;
        Vector3 smoothPos;
        Quaternion smoothRot = Quaternion.identity;

        static readonly Vector3 ElbowR = new Vector3(0.26f, -0.5f, 0.02f), ElbowL = new Vector3(-0.26f, -0.5f, 0.06f);

        public static Viewmodel Create(Transform cam, PlayerController p)
        {
            var go = new GameObject("Viewmodel");
            go.layer = Layers.Viewmodel;
            go.transform.SetParent(cam, false);
            var vm = go.AddComponent<Viewmodel>();
            vm.player = p;
            vm.Socket = new GameObject("Socket").transform;
            vm.Socket.gameObject.layer = Layers.Viewmodel;
            vm.Socket.SetParent(go.transform, false);
            var skin = new Color(0.9f, 0.72f, 0.58f);
            var sleeve = new Color(0.16f, 0.17f, 0.2f);
            vm.Right = HandRig.Build(go.transform, "HandR", false, skin, sleeve, Layers.Viewmodel);
            vm.Left = HandRig.Build(go.transform, "HandL", true, skin, sleeve, Layers.Viewmodel);
            // A leg for kicks.
            var leg = new GameObject("KickLeg");
            leg.layer = Layers.Viewmodel;
            leg.transform.SetParent(go.transform, false);
            var lm = new MeshBuilder { Color = new Color(0.2f, 0.28f, 0.45f), Gloss = 0.15f };
            lm.Capsule(new Vector3(0, 0, -0.6f), new Vector3(0, 0, 0f), 0.075f, 0.06f, 12, 3);
            lm.SetColor(new Color(0.12f, 0.1f, 0.09f), 0.35f);
            lm.BevelBox(new Vector3(0, -0.02f, 0.08f), new Vector3(0.1f, 0.09f, 0.26f), 0.02f);
            lm.SetColor(new Color(0.85f, 0.85f, 0.85f), 0.2f);
            lm.Box(new Vector3(0, -0.07f, 0.08f), new Vector3(0.105f, 0.015f, 0.27f));
            leg.AddComponent<MeshFilter>().sharedMesh = lm.ToMesh("KickLeg");
            leg.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            vm.Leg = leg.transform;
            leg.SetActive(false);
            IconFactory.SetLayer(go, Layers.Viewmodel);
            return vm;
        }

        public void AddRecoil(Vector3 posKick, Vector3 rotKick)
        {
            recoilPosVel += posKick;
            recoilRotVel += rotKick;
        }

        public void PlayEquip() => equip = 0f;

        /// <summary>Resets every per-weapon override back to "hands on the weapon's anchors".</summary>
        public void ResetHands()
        {
            RightOnAnchor = LeftOnAnchor = true;
            RightInSocket = LeftInSocket = false;
            HideLeft = HideRight = false;
            AimWeight = 0f;
        }
        public void PlayKick(bool drop) { kickT = 0f; dropKick = drop; }
        public void Land(float amount) => landDip = Mathf.Max(landDip, amount);

        static void Spring(ref Vector3 x, ref Vector3 v, Vector3 target, float k, float d, float dt)
        {
            var a = (target - x) * k - v * d;
            v += a * dt;
            x += v * dt;
        }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (player == null) return;
            var look = player.LookDelta;
            float aimDamp = Mathf.Lerp(1f, 0.08f, AimWeight);   // sway/bob/breath almost gone while aiming down the sights

            // Sway lags behind the mouse.
            Spring(ref swayPos, ref swayVel, new Vector3(-look.x * 0.0035f, -look.y * 0.0035f, 0) * aimDamp, 120f, 14f, dt);
            Spring(ref swayRot, ref swayRotVel, new Vector3(look.y * 0.7f, -look.x * 0.7f, -look.x * 1.2f) * aimDamp, 120f, 14f, dt);
            swayPos = Vector3.ClampMagnitude(swayPos, 0.04f);
            // Recoil springs back to rest.
            Spring(ref recoilPos, ref recoilPosVel, Vector3.zero, 160f, 18f, dt);
            Spring(ref recoilRot, ref recoilRotVel, Vector3.zero, 140f, 16f, dt);

            float speed = player.Speed;
            bool grounded = player.Grounded;
            bob += dt * (6f + speed * 1.1f);
            float bobAmt = grounded ? Mathf.Clamp01(speed / 7f) * aimDamp : 0f;
            var bobPos = new Vector3(Mathf.Sin(bob) * 0.012f, -Mathf.Abs(Mathf.Cos(bob)) * 0.012f, 0) * bobAmt;
            var bobRot = new Vector3(Mathf.Abs(Mathf.Cos(bob)) * 1.5f, Mathf.Sin(bob) * 1.2f, Mathf.Sin(bob) * 1.5f) * bobAmt;
            float breathe = Mathf.Sin(Time.time * 1.6f) * 0.002f * aimDamp;

            sprint = MathX.Damp(sprint, player.Sprinting ? 1f : 0f, 8f, dt);
            equip = Mathf.MoveTowards(equip, 1f, dt / 0.35f);
            landDip = Mathf.MoveTowards(landDip, 0f, dt * 2f);
            float eq = 1f - MathX.SmoothStep01(equip);

            var pos = TargetPos + swayPos + bobPos + recoilPos + new Vector3(0, breathe - landDip * 0.04f - eq * 0.25f, 0)
                      + new Vector3(-0.04f, -0.05f, -0.03f) * sprint;
            var rot = TargetRot * Quaternion.Euler(swayRot + bobRot + recoilRot + new Vector3(eq * 40f, 0, 0) + new Vector3(10f, -35f, 18f) * sprint);
            smoothPos = Vector3.Lerp(smoothPos, pos, 1f - Mathf.Exp(-30f * dt));
            smoothRot = Quaternion.Slerp(smoothRot, rot, 1f - Mathf.Exp(-30f * dt));
            Socket.localPosition = smoothPos;
            Socket.localRotation = smoothRot;

            var model = Socket.childCount > 0 ? Socket.GetChild(0).GetComponent<WeaponModel>() : null;
            PlaceHand(Right, RightOnAnchor && model != null ? model.GripR : null, RightPos, RightRot, HideRight, ElbowR, RightInSocket);
            PlaceHand(Left, LeftOnAnchor && model != null ? model.SupportL : null, LeftPos, LeftRot, HideLeft, ElbowL, LeftInSocket);

            // Kick leg
            kickT = Mathf.MoveTowards(kickT, 1f, dt / 0.45f);
            Leg.gameObject.SetActive(kickT < 1f);
            if (kickT < 1f)
            {
                float k = Mathf.Sin(kickT * Mathf.PI);
                // Both kicks end inside the view (the old ground kick stopped ~40 deg below the screen, so only
                // jump kicks were visible). The leg also follows the camera pitch a little less when looking down.
                Leg.localPosition = Vector3.Lerp(new Vector3(0.14f, -0.85f, 0.15f), dropKick ? new Vector3(0.05f, -0.3f, 0.78f) : new Vector3(0.12f, -0.3f, 0.72f), k);
                Leg.localRotation = Quaternion.Euler(Mathf.Lerp(55f, dropKick ? -5f : 2f, k), -8f, 0);
            }
        }

        void PlaceHand(HandRig hand, Transform anchor, Vector3 localPos, Quaternion localRot, bool hide, Vector3 elbow, bool inSocket)
        {
            hand.gameObject.SetActive(!hide);
            if (hand.Forearm != null) hand.Forearm.gameObject.SetActive(!hide);
            if (hide) return;
            if (anchor != null) hand.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            else if (inSocket)
            {
                hand.transform.localPosition = Socket.localPosition + Socket.localRotation * localPos;
                hand.transform.localRotation = Socket.localRotation * localRot;
            }
            else { hand.transform.localPosition = localPos; hand.transform.localRotation = localRot; }
            var elbowWorld = transform.TransformPoint(elbow + new Vector3(0, -0.04f * sprint, 0));
            hand.AimForearm(elbowWorld);
        }
    }
}
