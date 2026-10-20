using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Spinning industrial blades. Limbs that touch them get cut off.</summary>
    public sealed class BladeFan : MonoBehaviour
    {
        public float Rpm = 220f;
        Transform rotor;
        Rigidbody rotorBody;
        AudioSource loop;

        public static BladeFan Create(Transform parent, Vector3 pos, Quaternion rot, float radius = 1.4f)
        {
            var go = new GameObject("Blade Fan");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var fan = go.AddComponent<BladeFan>();
            var housing = new MeshBuilder { Color = new Color(0.3f, 0.3f, 0.32f), Gloss = 0.5f };
            housing.Torus(Vector3.zero, radius + 0.1f, 0.08f, 32, 8, Vector3.forward);
            housing.Cylinder(new Vector3(0, -radius - 0.15f, 0), new Vector3(0, -radius - 1.6f, 0), 0.12f, 0.15f, 8);
            go.AddComponent<MeshFilter>().sharedMesh = housing.ToMesh("FanHousing");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;

            var rotorGo = new GameObject("Rotor");
            rotorGo.transform.SetParent(go.transform, false);
            var blades = new MeshBuilder { Color = new Color(0.75f, 0.75f, 0.78f), Gloss = 0.8f };
            for (int i = 0; i < 4; i++)
            {
                blades.Push(Vector3.zero, Quaternion.Euler(0, 0, i * 90f));
                blades.Push(new Vector3(0, radius * 0.5f, 0), Quaternion.Euler(0, 25f, 0));
                blades.Box(Vector3.zero, new Vector3(0.28f, radius, 0.03f));
                blades.Pop();
                blades.Pop();
            }
            blades.SetColor(new Color(0.2f, 0.2f, 0.2f));
            blades.Cylinder(Vector3.back * 0.1f, Vector3.forward * 0.1f, 0.18f, 0.18f, 12);
            rotorGo.AddComponent<MeshFilter>().sharedMesh = blades.ToMesh("FanRotor");
            rotorGo.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            for (int i = 0; i < 4; i++)
            {
                var bc = new GameObject("BladeCollider");
                bc.transform.SetParent(rotorGo.transform, false);
                bc.transform.localRotation = Quaternion.Euler(0, 0, i * 90f);
                var box = bc.AddComponent<BoxCollider>();
                box.center = new Vector3(0, radius * 0.5f, 0);
                box.size = new Vector3(0.3f, radius, 0.12f);
                bc.AddComponent<Surface>().Type = SurfaceType.Metal;
                bc.AddComponent<BladeEdge>().Fan = fan;
            }
            fan.rotorBody = rotorGo.AddComponent<Rigidbody>();
            fan.rotorBody.isKinematic = true;
            fan.rotor = rotorGo.transform;
            fan.loop = AudioManager.CreateLoop("fan_loop", go.transform, 0.8f, AudioBus.Sfx, 25f);
            return fan;
        }

        void FixedUpdate()
        {
            if (rotorBody == null) return;
            rotorBody.MoveRotation(rotorBody.rotation * Quaternion.Euler(0, 0, Rpm * 6f * Time.fixedDeltaTime));
        }

        internal void OnBladeHit(Collision c)
        {
            var part = c.collider.GetComponent<BodyPart>();
            var p = c.GetContact(0).point;
            if (part != null)
            {
                part.TakeDamage(new DamageInfo { Amount = 60f, Force = Random.onUnitSphere * 30f, Point = p, Normal = c.GetContact(0).normal, Direction = -c.GetContact(0).normal, Type = DamageType.Slash, Source = this, Weapon = "Blade Fan" });
                if (Settings.Dismemberment && part.CanSever) part.Doll.Dismember.Sever(part, p, c.GetContact(0).normal, true);
                AudioManager.Play("slice", p, 1f);
            }
            else if (c.rigidbody != null)
            {
                FX.Sparks(p, c.GetContact(0).normal, 10);
                AudioManager.Play("metal_impact", p, 0.6f);
            }
        }
    }

    public sealed class BladeEdge : MonoBehaviour
    {
        public BladeFan Fan;
        void OnCollisionEnter(Collision c) { if (Fan != null) Fan.OnBladeHit(c); }
    }
}
