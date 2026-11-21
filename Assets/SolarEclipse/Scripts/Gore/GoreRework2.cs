using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Slash wounds gape: the decal widens across the cut over about 20 seconds as the edges pull apart.</summary>
    public sealed class WoundGape : MonoBehaviour
    {
        Vector3 baseScale; float born, extra;
        public static void Begin(GameObject decal, float amount)
        {
            if (decal == null) return;
            var g = decal.GetComponent<WoundGape>() ?? decal.AddComponent<WoundGape>();
            g.baseScale = decal.transform.localScale; g.born = Time.time; g.extra = amount; g.enabled = true;
        }
        void Update()
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - born) / 20f));
            // The quad's X runs along the cut (rotation follows the slash direction), so Y is the opening.
            transform.localScale = new Vector3(baseScale.x * (1f + extra * 0.25f * k), baseScale.y * (1f + extra * k), baseScale.z);
            if (k >= 1f) enabled = false;
        }
    }

    /// <summary>Capillary bleeding: small cuts and grazes bead with blood and let a few drops fall for half a minute.</summary>
    public sealed class WoundBead : MonoBehaviour
    {
        float born, next;
        public static void Begin(GameObject decal)
        {
            if (decal == null) return;
            var b = decal.GetComponent<WoundBead>() ?? decal.AddComponent<WoundBead>();
            b.born = Time.time; b.next = Time.time + Random.Range(1.5f, 3f); b.enabled = true;
        }
        void Update()
        {
            if (Time.time - born > 30f) { enabled = false; return; }
            if (Time.time < next) return;
            next = Time.time + Random.Range(1.8f, 4.5f) * (1f + (Time.time - born) / 15f); // slows as it clots
            BloodFX.Drip(transform.position, Vector3.zero);
        }
    }

    /// <summary>Black eyes: blunt hits to the face leave a dark bruise around the nearest eye.</summary>
    public static class BlackEye
    {
        public static void Maybe(BodyPart head, Vector3 hitPos)
        {
            if (head == null || !Settings.GoreEnabled) return;
            var t = head.transform;
            var local = t.InverseTransformPoint(hitPos);
            if (local.z < 0.02f) return; // back of the head
            float side = local.x >= 0f ? 1f : -1f;
            var eye = t.TransformPoint(new Vector3(0.031f * side, 0.12f, 0.085f));
            var col = head.GetComponent<Collider>();
            Bruise.Begin(Decals.Wound(eye, t.forward, 7, 0.06f, col, Random.Range(0f, 360f)));
        }
    }
}
