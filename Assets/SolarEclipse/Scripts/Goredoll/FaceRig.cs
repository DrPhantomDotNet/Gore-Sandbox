using UnityEngine;

namespace SolarEclipse
{
    public enum Expression { Neutral, Happy, Curious, Afraid, Pain, Angry, Choking, Unconscious, Dead }

    /// <summary>
    /// Eyes that track a target and blink, eyelids, eyebrows and a mouth that opens for screams and breaths.
    /// Expressions are driven by the Goredoll's state. Lives on the head bone (no skinning needed).
    /// </summary>
    public sealed class FaceRig : MonoBehaviour
    {
        Transform eyeL, eyeR, lidL, lidR, browL, browR, lipLower, mouth;
        Vector3 browLRest, browRRest, lipRest;
        bool eyeLGone, eyeRGone;
        float blinkTimer, blink, mouthOpen, mouthTarget, talkUntil;
        Vector3 lookTarget;
        bool hasLook;
        public Expression Expression = Expression.Neutral;
        float pupilsDilated;

        static readonly Vector3 HeadC = new Vector3(0, HumanModel.HeadCenterY, 0.005f);

        public static FaceRig Build(Transform head, Appearance a)
        {
            var rig = head.gameObject.AddComponent<FaceRig>();
            var eyeMesh = HumanModel.Eye(a.Eyes);
            var lidMesh = HumanModel.Eyelid(a.Skin, true);
            var lowerLid = HumanModel.Eyelid(a.Skin * 0.97f, false);
            var brow = HumanModel.Brow(a.Hair);
            float eyeY = HeadC.y + 0.012f, eyeZ = HeadC.z + 0.072f, eyeX = 0.031f;
            rig.eyeL = Part(head, "EyeL", eyeMesh, new Vector3(-eyeX, eyeY, eyeZ));
            rig.eyeR = Part(head, "EyeR", eyeMesh, new Vector3(eyeX, eyeY, eyeZ));
            rig.lidL = Part(head, "LidL", lidMesh, new Vector3(-eyeX, eyeY, eyeZ));
            rig.lidR = Part(head, "LidR", lidMesh, new Vector3(eyeX, eyeY, eyeZ));
            Part(head, "LowLidL", lowerLid, new Vector3(-eyeX, eyeY, eyeZ));
            Part(head, "LowLidR", lowerLid, new Vector3(eyeX, eyeY, eyeZ));
            rig.browL = Part(head, "BrowL", brow, new Vector3(-eyeX, eyeY + 0.024f, eyeZ + 0.011f));
            rig.browR = Part(head, "BrowR", brow, new Vector3(eyeX, eyeY + 0.024f, eyeZ + 0.011f));
            rig.browL.localScale = new Vector3(-1, 1, 1);
            rig.browLRest = rig.browL.localPosition; rig.browRRest = rig.browR.localPosition;
            float mouthY = HeadC.y - 0.048f, mouthZ = HeadC.z + 0.084f;
            Part(head, "LipUpper", HumanModel.Lip(a.Lips, true), new Vector3(0, mouthY + 0.006f, mouthZ));
            rig.mouth = Part(head, "Mouth", HumanModel.MouthCavity(), new Vector3(0, mouthY, mouthZ - 0.006f));
            rig.lipLower = Part(head, "LipLower", HumanModel.Lip(a.Lips * 0.95f, false), new Vector3(0, mouthY - 0.006f, mouthZ - 0.001f));
            rig.lipRest = rig.lipLower.localPosition;
            rig.blinkTimer = Random.Range(1f, 4f);
            return rig;
        }

        static Transform Part(Transform head, string name, Mesh mesh, Vector3 pos)
        {
            var go = new GameObject(name);
            go.layer = head.gameObject.layer;
            go.transform.SetParent(head, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        public void LookAt(Vector3 worldPoint) { lookTarget = worldPoint; hasLook = true; }
        public void ClearLook() => hasLook = false;
        public void Talk(float seconds) { talkUntil = Time.time + seconds; }
        public void OpenMouth(float amount) { mouthTarget = Mathf.Max(mouthTarget, amount); }

        public void DestroyEye(bool left)
        {
            var e = left ? eyeL : eyeR;
            if (e == null) return;
            if (left) eyeLGone = true; else eyeRGone = true;
            var mr = e.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = Mats.Flesh;
            e.localScale = new Vector3(1f, 0.6f, 0.5f);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            bool closedEyes = Expression == Expression.Unconscious;
            bool dead = Expression == Expression.Dead;

            // Blinking
            blinkTimer -= dt;
            if (blinkTimer <= 0f && !dead && !closedEyes)
            {
                blink = 1f;
                blinkTimer = Random.Range(2f, 5.5f) * (Expression == Expression.Afraid || Expression == Expression.Pain ? 0.5f : 1f);
            }
            blink = Mathf.MoveTowards(blink, 0f, dt * 9f);
            float lidClose = closedEyes ? 1f : dead ? 0.55f : Expression == Expression.Pain ? 0.45f : Expression == Expression.Afraid ? -0.25f : Expression == Expression.Angry ? 0.3f : 0.05f;
            lidClose = Mathf.Max(lidClose, blink > 0.5f ? 1f : blink * 2f);
            // Lid rotates around the eye centre: 0 = open (rotated up), 1 = closed.
            var lidRot = Quaternion.Euler(Mathf.Lerp(-55f, 40f, Mathf.Clamp01(lidClose)), 0, 0);
            if (lidL != null) lidL.localRotation = lidRot;
            if (lidR != null) lidR.localRotation = lidRot;

            // Eyes track the look target (clamped), roll up when unconscious, stare when dead.
            Quaternion eyeRot = Quaternion.identity;
            if (closedEyes) eyeRot = Quaternion.Euler(-35f, 0, 0);
            else if (dead) eyeRot = Quaternion.Euler(Random.Range(-1f, 1f) * 0f + 8f, 6f, 0);
            else if (hasLook)
            {
                var local = transform.InverseTransformPoint(lookTarget) - new Vector3(0, HeadC.y + 0.012f, HeadC.z + 0.072f);
                if (local.z > 0.05f)
                {
                    var r = Quaternion.LookRotation(local.normalized, Vector3.up).eulerAngles;
                    eyeRot = Quaternion.Euler(Mathf.Clamp(MathX.WrapAngle(r.x), -25f, 25f), Mathf.Clamp(MathX.WrapAngle(r.y), -35f, 35f), 0);
                }
            }
            else eyeRot = Quaternion.Euler(Mathf.Sin(Time.time * 0.7f) * 4f, Mathf.Sin(Time.time * 0.43f) * 10f, 0);
            if (eyeL != null && !eyeLGone) eyeL.localRotation = Quaternion.Slerp(eyeL.localRotation, eyeRot, dt * 18f);
            if (eyeR != null && !eyeRGone) eyeR.localRotation = Quaternion.Slerp(eyeR.localRotation, eyeRot, dt * 18f);

            // Brows: inner corners up when afraid / in pain, down when angry.
            float browY = 0, browTilt = 0;
            switch (Expression)
            {
                case Expression.Afraid: browY = 0.005f; browTilt = -18f; break;
                case Expression.Pain: browY = -0.002f; browTilt = -22f; break;
                case Expression.Angry: browY = -0.004f; browTilt = 20f; break;
                case Expression.Curious: browY = 0.004f; browTilt = -6f; break;
                case Expression.Choking: browY = 0.003f; browTilt = -25f; break;
            }
            if (browL != null)
            {
                browL.localPosition = MathX.Damp(browL.localPosition, browLRest + Vector3.up * browY, 10f, dt);
                browR.localPosition = MathX.Damp(browR.localPosition, browRRest + Vector3.up * browY, 10f, dt);
                browL.localRotation = MathX.Damp(browL.localRotation, Quaternion.Euler(0, 0, browTilt), 10f, dt);
                browR.localRotation = MathX.Damp(browR.localRotation, Quaternion.Euler(0, 0, -browTilt), 10f, dt);
            }

            // Mouth
            float baseOpen = Expression == Expression.Afraid ? 0.35f : Expression == Expression.Pain ? 0.4f : Expression == Expression.Choking ? 0.6f + 0.3f * Mathf.Sin(Time.time * 9f) : dead ? 0.3f : Expression == Expression.Unconscious ? 0.15f : 0f;
            if (Time.time < talkUntil) baseOpen = Mathf.Max(baseOpen, 0.3f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 17f)));
            mouthTarget = Mathf.MoveTowards(mouthTarget, 0f, dt * 2f);
            mouthOpen = MathX.Damp(mouthOpen, Mathf.Max(baseOpen, mouthTarget), 14f, dt);
            if (lipLower != null) lipLower.localPosition = lipRest + new Vector3(0, -0.016f * mouthOpen, -0.002f * mouthOpen);
            if (mouth != null) mouth.localScale = new Vector3(1f + 0.15f * mouthOpen, 0.25f + 1.6f * mouthOpen, 1f);
        }
    }
}
