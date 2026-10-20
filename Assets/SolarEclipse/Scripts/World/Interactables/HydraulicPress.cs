using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Slams a heavy plate down every few seconds. Anything underneath has a bad day.</summary>
    public sealed class HydraulicPress : MonoBehaviour
    {
        Rigidbody plate;
        float top, bottom, cycle;
        bool slammed;

        public static HydraulicPress Create(Transform parent, Vector3 basePos, float height = 4.5f)
        {
            var go = new GameObject("Hydraulic Press");
            go.transform.SetParent(parent, false);
            go.transform.position = basePos;
            var press = go.AddComponent<HydraulicPress>();
            var frame = new MeshBuilder { Color = new Color(0.85f, 0.65f, 0.1f), Gloss = 0.4f };
            frame.Box(new Vector3(-1.4f, height / 2, 0), new Vector3(0.3f, height, 0.5f));
            frame.Box(new Vector3(1.4f, height / 2, 0), new Vector3(0.3f, height, 0.5f));
            frame.Box(new Vector3(0, height + 0.2f, 0), new Vector3(3.1f, 0.4f, 0.8f));
            frame.SetColor(new Color(0.3f, 0.3f, 0.32f));
            frame.Box(new Vector3(0, 0.1f, 0), new Vector3(2.5f, 0.2f, 2f));
            go.AddComponent<MeshFilter>().sharedMesh = frame.ToMesh("PressFrame");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            foreach (var c in new[] { new Vector3(-1.4f, height / 2, 0), new Vector3(1.4f, height / 2, 0) })
            {
                var bc = go.AddComponent<BoxCollider>(); bc.center = c; bc.size = new Vector3(0.3f, height, 0.5f);
            }
            var floor = go.AddComponent<BoxCollider>(); floor.center = new Vector3(0, 0.1f, 0); floor.size = new Vector3(2.5f, 0.2f, 2f);
            go.AddComponent<Surface>().Type = SurfaceType.Metal;

            var plateGo = new GameObject("Plate");
            plateGo.transform.SetParent(go.transform, false);
            var pm = new MeshBuilder { Color = new Color(0.4f, 0.4f, 0.42f), Gloss = 0.6f };
            pm.BevelBox(Vector3.zero, new Vector3(2.4f, 0.5f, 1.9f), 0.04f);
            pm.SetColor(new Color(0.7f, 0.7f, 0.72f));
            pm.Cylinder(Vector3.up * 0.25f, Vector3.up * (height), 0.18f, 0.18f, 12);
            plateGo.AddComponent<MeshFilter>().sharedMesh = pm.ToMesh("PressPlate");
            plateGo.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            plateGo.AddComponent<BoxCollider>().size = new Vector3(2.4f, 0.5f, 1.9f);
            plateGo.AddComponent<Surface>().Type = SurfaceType.Metal;
            press.plate = plateGo.AddComponent<Rigidbody>();
            press.plate.isKinematic = true;
            press.plate.interpolation = RigidbodyInterpolation.Interpolate;
            press.top = basePos.y + height - 0.6f;
            press.bottom = basePos.y + 0.45f;
            plateGo.transform.position = new Vector3(basePos.x, press.top, basePos.z);
            return press;
        }

        void FixedUpdate()
        {
            if (plate == null) return;
            cycle += Time.fixedDeltaTime;
            float period = 4.5f;
            float t = cycle % period;
            float y;
            if (t < 2.2f) { y = top; slammed = false; }
            else if (t < 2.45f)
            {
                y = Mathf.Lerp(top, bottom, (t - 2.2f) / 0.25f);
            }
            else if (t < 3f)
            {
                y = bottom;
                if (!slammed) { slammed = true; Slam(); }
            }
            else y = Mathf.Lerp(bottom, top, (t - 3f) / 1.5f);
            var p = plate.position;
            plate.MovePosition(new Vector3(p.x, y, p.z));
        }

        void Slam()
        {
            AudioManager.Play("press_slam", plate.position, 1f);
            CameraFX.ShakeAt(plate.position, 0.4f, 12f);
            FX.Dust(plate.position + Vector3.down * 0.3f, 1.2f, new Color(0.5f, 0.48f, 0.45f, 0.5f), 12);
            var hits = Physics.OverlapBox(plate.position + Vector3.down * 0.4f, new Vector3(1.2f, 0.35f, 0.95f), plate.rotation, Layers.Mask(Layers.Ragdoll, Layers.Props, Layers.Player), QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                var d = h.GetComponentInParent<IDamageable>();
                d?.TakeDamage(new DamageInfo { Amount = 400f, Force = Vector3.down * 50f, Point = h.ClosestPoint(plate.position), Normal = Vector3.up, Direction = Vector3.down, Type = DamageType.Crush, Source = this, Weapon = "Hydraulic Press" });
            }
        }
    }
}
