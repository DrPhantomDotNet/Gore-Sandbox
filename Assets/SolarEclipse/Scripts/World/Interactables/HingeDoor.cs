using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Physics door on a hinge. Push it, shoot it, or press E to swing it.</summary>
    public sealed class HingeDoor : MonoBehaviour, IInteractable
    {
        Rigidbody rb;
        float lastCreak;
        public string Prompt => "Open / close";

        public static void Create(Transform parent, Vector3 hingePos, float yaw, float width = 1.1f, float height = 2.2f)
        {
            var go = new GameObject("Door");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(hingePos, Quaternion.Euler(0, yaw, 0));
            var mb = new MeshBuilder { Color = new Color(0.55f, 0.38f, 0.22f), Gloss = 0.3f };
            mb.BevelBox(new Vector3(width / 2, height / 2, 0), new Vector3(width, height, 0.06f), 0.015f);
            mb.SetColor(new Color(0.8f, 0.75f, 0.4f), 0.85f);
            mb.Sphere(new Vector3(width - 0.12f, 1f, 0.06f), Vector3.one * 0.035f, 6, 8);
            mb.Sphere(new Vector3(width - 0.12f, 1f, -0.06f), Vector3.one * 0.035f, 6, 8);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Door");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(width / 2, height / 2, 0); bc.size = new Vector3(width, height, 0.06f);
            go.AddComponent<Surface>().Type = SurfaceType.Wood;
            go.layer = Layers.Props;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 25f;
            RB.SetDamping(rb, 0.5f, 2f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var hj = go.AddComponent<HingeJoint>();
            hj.anchor = Vector3.zero;
            hj.axis = Vector3.up;
            hj.useLimits = true;
            hj.limits = new JointLimits { min = -105f, max = 105f };
            hj.useSpring = true;
            hj.spring = new JointSpring { spring = 2f, damper = 3f, targetPosition = 0 };
            var d = go.AddComponent<HingeDoor>();
            d.rb = rb;
        }

        public void Interact(GameObject user)
        {
            var dir = (transform.position - user.transform.position);
            dir.y = 0;
            rb.AddForceAtPosition(dir.normalized * 220f, transform.TransformPoint(new Vector3(0.9f, 1f, 0)), ForceMode.Impulse);
        }

        void FixedUpdate()
        {
            if (rb == null) return;
            if (rb.angularVelocity.magnitude > 1.2f && Time.time - lastCreak > 1.2f)
            {
                lastCreak = Time.time;
                AudioManager.Play("creak", transform.position, 0.5f, Random.Range(0.9f, 1.1f));
            }
        }
    }
}
