using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>One spawnable thing for the spawn menu (prop, NPC, item, entity).</summary>
    public sealed class SpawnableDef
    {
        public string Id, Name, Category, Description, Glyph = "box";
        public Func<Vector3, Quaternion, GameObject> Spawn;
        public bool Secret;
        public Func<bool> Unlocked = () => true;
    }

    /// <summary>Marks a spawned root so the spawn gun can delete it and undo can find it.</summary>
    public sealed class Spawned : MonoBehaviour
    {
        public string DefId;
        public string DisplayName;
    }

    /// <summary>Builds physics props from code. All props are dynamic rigidbodies on the Props layer.</summary>
    public static class PropFactory
    {
        public static readonly List<SpawnableDef> Props = new List<SpawnableDef>();

        static PropFactory()
        {
            Add("crate", "Wooden Crate", "Breakable shipping crate.", "box", (p, r) => Crate(p, r, 0.8f));
            Add("small_crate", "Small Crate", "A smaller, breakable crate.", "box", (p, r) => Crate(p, r, 0.5f));
            Add("barrel", "Oil Drum", "Heavy steel drum.", "barrel", (p, r) => Drum(p, r, new Color(0.2f, 0.4f, 0.65f), false));
            Add("explosive_barrel", "Explosive Barrel", "Shoot it. You know you want to.", "explosion", (p, r) => Drum(p, r, new Color(0.75f, 0.1f, 0.08f), true));
            Add("propane", "Propane Tank", "Vents and rockets around when punctured.", "fire", Propane);
            Add("plank", "Plank", "Long wooden board.", "list", (p, r) => SimpleBody("Plank", p, r, new Vector3(0.25f, 0.05f, 2.2f), new Color(0.62f, 0.45f, 0.28f), 6f, SurfaceType.Wood, Layers.Props, TextureFactory.Wood).gameObject);
            Add("concrete", "Concrete Block", "Very heavy.", "box", (p, r) => SimpleBody("Concrete Block", p, r, new Vector3(0.9f, 0.45f, 0.45f), new Color(0.65f, 0.64f, 0.6f), 90f, SurfaceType.Concrete, Layers.Props, TextureFactory.Concrete).gameObject);
            Add("cinder", "Cinder Block", "Grey block with holes.", "grid", Cinder);
            Add("tire", "Tyre", "Bouncy rubber tyre.", "refresh", Tire);
            Add("chair", "Chair", "Wooden chair.", "home", Chair);
            Add("table", "Table", "Wooden table.", "grid", Table);
            Add("cone", "Traffic Cone", "Orange and white.", "warning", Cone);
            Add("mattress", "Mattress", "Soft landing.", "list", (p, r) => SimpleBody("Mattress", p, r, new Vector3(1f, 0.22f, 2f), new Color(0.85f, 0.85f, 0.9f), 15f, SurfaceType.Carpet, Layers.Props).gameObject);
            Add("watermelon", "Watermelon", "Bursts like a head would. Good for testing.", "drop", Watermelon);
            Add("ball", "Beach Ball", "Light and bouncy.", "globe", Ball);
            Add("bowling", "Bowling Pin", "Set them up, knock them down.", "flag", Pin);
            Add("can", "Soda Can", "Fizzy.", "bullet", Can);
            Add("bottle", "Glass Bottle", "Shatters.", "pill", Bottle);
            Add("pallet", "Pallet", "Wooden shipping pallet.", "grid", Pallet);
            Add("dumbbell", "Dumbbell", "20 kg of iron.", "link", Dumbbell);
            Add("hangman", "Hangman", "Wooden gallows with a rope. Bring a Goredoll's head to the noose with the Spawn Gun, or press INTERACT on it to hang the nearest one. Shoot or cut the rope to drop them.", "link", Hangman); // World/Hangman.cs
        }

        static void Add(string id, string name, string desc, string glyph, Func<Vector3, Quaternion, GameObject> spawn)
            => Props.Add(new SpawnableDef { Id = id, Name = name, Description = desc, Glyph = glyph, Category = "Props", Spawn = spawn });

        public static Rigidbody Spawn(string id, Vector3 pos, Quaternion rot)
        {
            if (id != null && id.StartsWith("car|")) // Open World cars (OpenWorld/OWCar.cs)
            {
                var car = OWCar.Create(id, pos, rot);
                var sp = car.AddComponent<Spawned>(); sp.DefId = id; sp.DisplayName = car.name;
                if (NetState.IsHost) NetManager.I.OnPropSpawned(car, id);
                return car.GetComponent<Rigidbody>();
            }
            foreach (var p in Props)
                if (p.Id == id)
                {
                    var go = p.Spawn(pos, rot);
                    var s = go.GetComponent<Spawned>() ?? go.AddComponent<Spawned>();
                    s.DefId = id; s.DisplayName = p.Name;
                    if (NetState.IsHost) NetManager.I.OnPropSpawned(go, id);
                    return go.GetComponent<Rigidbody>();
                }
            return null;
        }

        static Transform Parent => WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null;

        static GameObject Root(string name, Vector3 pos, Quaternion rot, int layer = Layers.Props)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(Parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            return go;
        }

        static Rigidbody Body(GameObject go, float mass, SurfaceType surface)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            RB.SetDamping(rb, 0.05f, 0.08f);
            go.AddComponent<Surface>().Type = surface;
            go.AddComponent<ImpactSounds>();
            return rb;
        }

        static void Render(GameObject go, MeshBuilder mb, Material mat)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(go.name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
        }

        /// <summary>A plain box body (fragments, planks, blocks).</summary>
        public static Rigidbody SimpleBody(string name, Vector3 pos, Quaternion rot, Vector3 size, Color color, float mass, SurfaceType surface, int layer, Texture2D tex = null)
        {
            var go = Root(name, pos, rot, layer);
            var mb = new MeshBuilder { Color = color, Gloss = surface == SurfaceType.Metal ? 0.6f : 0.15f };
            if (Mathf.Min(size.x, Mathf.Min(size.y, size.z)) > 0.08f) mb.BevelBox(Vector3.zero, size, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.08f, 1f);
            else mb.Box(Vector3.zero, size, 1f);
            Render(go, mb, tex != null ? Mats.LitTextured(tex) : Mats.Lit);
            go.AddComponent<BoxCollider>().size = size;
            return Body(go, mass, surface);
        }

        static GameObject Crate(Vector3 p, Quaternion r, float s)
        {
            var go = Root("Crate", p, r);
            var mb = new MeshBuilder { Color = new Color(0.78f, 0.58f, 0.36f), Gloss = 0.12f };
            mb.Box(Vector3.zero, Vector3.one * s * 0.94f, 1.5f);
            mb.SetColor(new Color(0.55f, 0.38f, 0.22f));
            float e = s * 0.5f, t = s * 0.08f;
            foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
                for (int a = -1; a <= 1; a += 2)
                    for (int b = -1; b <= 1; b += 2)
                    {
                        var u = axis == Vector3.right ? Vector3.up : Vector3.right;
                        var v = Vector3.Cross(axis, u);
                        var size = new Vector3(Mathf.Abs(axis.x) * s + t, Mathf.Abs(axis.y) * s + t, Mathf.Abs(axis.z) * s + t);
                        size = new Vector3(axis.x != 0 ? s : t, axis.y != 0 ? s : t, axis.z != 0 ? s : t);
                        mb.Box(u * e * a + v * e * b, size, 1.5f);
                    }
            Render(go, mb, Mats.LitTextured(TextureFactory.Wood));
            go.AddComponent<BoxCollider>().size = Vector3.one * s;
            Body(go, 18f * s / 0.8f, SurfaceType.Wood);
            var br = go.AddComponent<BreakableProp>();
            br.Health = 35f * s / 0.8f; br.Material = SurfaceType.Wood; br.Fragments = s > 0.6f ? 8 : 5;
            return go;
        }

        static GameObject Drum(Vector3 p, Quaternion r, Color c, bool explosive)
        {
            var go = Root(explosive ? "Explosive Barrel" : "Oil Drum", p, r);
            var mb = new MeshBuilder { Color = c, Gloss = 0.45f };
            mb.Cylinder(new Vector3(0, -0.45f, 0), new Vector3(0, 0.45f, 0), 0.3f, 0.3f, 18);
            mb.SetColor(c * 0.7f);
            mb.Torus(new Vector3(0, 0.15f, 0), 0.305f, 0.015f, 18, 4);
            mb.Torus(new Vector3(0, -0.15f, 0), 0.305f, 0.015f, 18, 4);
            if (explosive)
            {
                mb.SetColor(new Color(1f, 0.85f, 0.1f), 0.3f);
                mb.Box(new Vector3(0, 0.02f, 0.301f), new Vector3(0.22f, 0.22f, 0.005f));
            }
            Render(go, mb, Mats.LitMetal);
            var cc = go.AddComponent<CapsuleCollider>();
            cc.radius = 0.3f; cc.height = 0.9f;
            Body(go, explosive ? 30f : 45f, SurfaceType.Metal);
            if (explosive) go.AddComponent<ExplosiveBarrel>();
            return go;
        }

        static GameObject Propane(Vector3 p, Quaternion r)
        {
            var go = Root("Propane Tank", p, r);
            var mb = new MeshBuilder { Color = new Color(0.9f, 0.9f, 0.88f), Gloss = 0.5f };
            mb.Capsule(new Vector3(0, -0.2f, 0), new Vector3(0, 0.2f, 0), 0.16f, 0.16f, 16, 4);
            mb.SetColor(new Color(0.3f, 0.3f, 0.3f));
            mb.Cylinder(new Vector3(0, 0.34f, 0), new Vector3(0, 0.44f, 0), 0.05f, 0.05f, 8);
            Render(go, mb, Mats.LitMetal);
            var cc = go.AddComponent<CapsuleCollider>();
            cc.radius = 0.16f; cc.height = 0.75f;
            Body(go, 12f, SurfaceType.Metal);
            go.AddComponent<PropaneTank>();
            return go;
        }

        static GameObject Cinder(Vector3 p, Quaternion r)
        {
            var go = Root("Cinder Block", p, r);
            var mb = new MeshBuilder { Color = new Color(0.55f, 0.55f, 0.53f), Gloss = 0.05f };
            mb.Box(new Vector3(0, 0, 0.085f), new Vector3(0.4f, 0.2f, 0.03f), 2f);
            mb.Box(new Vector3(0, 0, -0.085f), new Vector3(0.4f, 0.2f, 0.03f), 2f);
            mb.Box(new Vector3(0.185f, 0, 0), new Vector3(0.03f, 0.2f, 0.2f), 2f);
            mb.Box(new Vector3(-0.185f, 0, 0), new Vector3(0.03f, 0.2f, 0.2f), 2f);
            mb.Box(Vector3.zero, new Vector3(0.03f, 0.2f, 0.2f), 2f);
            Render(go, mb, Mats.LitTextured(TextureFactory.Concrete));
            go.AddComponent<BoxCollider>().size = new Vector3(0.4f, 0.2f, 0.2f);
            Body(go, 14f, SurfaceType.Concrete);
            return go;
        }

        static GameObject Tire(Vector3 p, Quaternion r)
        {
            var go = Root("Tyre", p, r);
            var mb = new MeshBuilder { Color = new Color(0.08f, 0.08f, 0.08f), Gloss = 0.25f };
            mb.Torus(Vector3.zero, 0.3f, 0.13f, 20, 10);
            Render(go, mb, Mats.Lit);
            var cc = go.AddComponent<CapsuleCollider>();
            cc.radius = 0.43f; cc.height = 0.26f; cc.direction = 1;
            Body(go, 9f, SurfaceType.Carpet);
            PhysMat.Apply(cc, "Rubber", 0.6f, 0.9f);
            return go;
        }

        /// <summary>Gallows frame (base, post, beam, brace); the rope and noose are physics bodies made by HangmanRig.</summary>
        static GameObject Hangman(Vector3 p, Quaternion r)
        {
            var go = Root("Hangman", p, r);
            var mb = new MeshBuilder { Color = new Color(0.42f, 0.29f, 0.18f), Gloss = 0.15f };
            mb.Box(new Vector3(0, 0.1f, 0), new Vector3(2.2f, 0.2f, 2.2f));
            mb.Box(new Vector3(0, 1.75f, -0.8f), new Vector3(0.2f, 3.3f, 0.2f));
            mb.Box(new Vector3(0, 3.3f, -0.15f), new Vector3(0.18f, 0.18f, 1.5f));
            mb.Push(new Vector3(0, 2.95f, -0.52f), Quaternion.Euler(45f, 0, 0));
            mb.Box(Vector3.zero, new Vector3(0.12f, 0.12f, 0.75f));
            mb.Pop();
            mb.SetColor(new Color(0.55f, 0.45f, 0.3f), 0.1f);
            mb.Cylinder(new Vector3(0, 3.22f, HangmanRig.AnchorZ), new Vector3(0, 3.38f, HangmanRig.AnchorZ), 0.035f, 0.035f, 8); // knot on the beam
            Render(go, mb, Mats.LitTextured(TextureFactory.Wood));
            var b = go.AddComponent<BoxCollider>(); b.center = new Vector3(0, 0.1f, 0); b.size = new Vector3(2.2f, 0.2f, 2.2f);
            var post = go.AddComponent<BoxCollider>(); post.center = new Vector3(0, 1.75f, -0.8f); post.size = new Vector3(0.2f, 3.3f, 0.2f);
            var beam = go.AddComponent<BoxCollider>(); beam.center = new Vector3(0, 3.3f, -0.15f); beam.size = new Vector3(0.18f, 0.18f, 1.5f);
            var rb = Body(go, 260f, SurfaceType.Wood);
            rb.centerOfMass = new Vector3(0, 0.25f, -0.2f);
            go.AddComponent<HangmanRig>();
            return go;
        }

        static GameObject Chair(Vector3 p, Quaternion r)
        {
            var go = Root("Chair", p, r);
            var mb = new MeshBuilder { Color = new Color(0.6f, 0.42f, 0.26f), Gloss = 0.2f };
            mb.Box(new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.05f, 0.45f));
            mb.Box(new Vector3(0, 0.75f, -0.2f), new Vector3(0.45f, 0.55f, 0.05f));
            for (int i = 0; i < 4; i++) mb.Box(new Vector3(i % 2 == 0 ? -0.19f : 0.19f, 0.22f, i < 2 ? -0.19f : 0.19f), new Vector3(0.05f, 0.45f, 0.05f));
            Render(go, mb, Mats.LitTextured(TextureFactory.Wood));
            var seat = go.AddComponent<BoxCollider>(); seat.center = new Vector3(0, 0.3f, 0); seat.size = new Vector3(0.45f, 0.6f, 0.45f);
            var back = go.AddComponent<BoxCollider>(); back.center = new Vector3(0, 0.75f, -0.2f); back.size = new Vector3(0.45f, 0.55f, 0.05f);
            Body(go, 6f, SurfaceType.Wood);
            var br = go.AddComponent<BreakableProp>(); br.Health = 30f; br.Fragments = 5;
            return go;
        }

        static GameObject Table(Vector3 p, Quaternion r)
        {
            var go = Root("Table", p, r);
            var mb = new MeshBuilder { Color = new Color(0.62f, 0.44f, 0.28f), Gloss = 0.25f };
            mb.Box(new Vector3(0, 0.75f, 0), new Vector3(1.6f, 0.06f, 0.9f));
            for (int i = 0; i < 4; i++) mb.Box(new Vector3(i % 2 == 0 ? -0.72f : 0.72f, 0.36f, i < 2 ? -0.38f : 0.38f), new Vector3(0.07f, 0.72f, 0.07f));
            Render(go, mb, Mats.LitTextured(TextureFactory.Wood));
            var top = go.AddComponent<BoxCollider>(); top.center = new Vector3(0, 0.75f, 0); top.size = new Vector3(1.6f, 0.06f, 0.9f);
            var legs = go.AddComponent<BoxCollider>(); legs.center = new Vector3(0, 0.36f, 0); legs.size = new Vector3(1.5f, 0.72f, 0.8f);
            Body(go, 20f, SurfaceType.Wood);
            return go;
        }

        static GameObject Cone(Vector3 p, Quaternion r)
        {
            var go = Root("Traffic Cone", p, r);
            var mb = new MeshBuilder { Color = new Color(1f, 0.45f, 0.05f), Gloss = 0.35f };
            mb.Box(new Vector3(0, 0.02f, 0), new Vector3(0.38f, 0.04f, 0.38f));
            mb.Cylinder(new Vector3(0, 0.04f, 0), new Vector3(0, 0.25f, 0), 0.15f, 0.1f, 14, false);
            mb.SetColor(Color.white);
            mb.Cylinder(new Vector3(0, 0.25f, 0), new Vector3(0, 0.38f, 0), 0.1f, 0.068f, 14, false);
            mb.SetColor(new Color(1f, 0.45f, 0.05f));
            mb.Cylinder(new Vector3(0, 0.38f, 0), new Vector3(0, 0.6f, 0), 0.068f, 0.02f, 14);
            Render(go, mb, Mats.Lit);
            var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.3f, 0); bc.size = new Vector3(0.3f, 0.6f, 0.3f);
            Body(go, 2.5f, SurfaceType.Carpet);
            return go;
        }

        static GameObject Watermelon(Vector3 p, Quaternion r)
        {
            var go = Root("Watermelon", p, r);
            var mb = new MeshBuilder { Color = new Color(0.15f, 0.45f, 0.15f), Gloss = 0.6f };
            mb.Sphere(Vector3.zero, new Vector3(0.17f, 0.15f, 0.22f), 10, 16, q => q * (1f + 0.02f * Mathf.Sin(Mathf.Atan2(q.x, q.y) * 14f)));
            Render(go, mb, Mats.Lit);
            go.AddComponent<SphereCollider>().radius = 0.17f;
            Body(go, 6f, SurfaceType.Flesh);
            go.AddComponent<Watermelon>();
            return go;
        }

        static GameObject Ball(Vector3 p, Quaternion r)
        {
            var go = Root("Beach Ball", p, r);
            Color[] cols = { Color.red, Color.white, Color.blue, Color.yellow, Color.white, new Color(0.1f, 0.8f, 0.2f) };
            var mb = new MeshBuilder { Color = Color.white, Gloss = 0.6f };
            mb.Sphere(Vector3.zero, Vector3.one * 0.3f, 10, 18);
            for (int i = 0; i < mb.C.Count; i++)
            {
                var v = mb.V[i];
                int seg = (int)((Mathf.Atan2(v.z, v.x) + Mathf.PI) / (Mathf.PI * 2f) * 6f) % 6;
                var c = QualitySettings.activeColorSpace == ColorSpace.Linear ? cols[seg].linear : cols[seg];
                c.a = 0.6f;
                mb.C[i] = c;
            }
            Render(go, mb, Mats.Lit);
            var sc = go.AddComponent<SphereCollider>();
            sc.radius = 0.3f;
            PhysMat.Apply(sc, "Bouncy", 0.85f, 0.4f);
            var rb = Body(go, 0.4f, SurfaceType.Carpet);
            RB.SetDamping(rb, 0.4f, 0.4f);
            return go;
        }

        static GameObject Pin(Vector3 p, Quaternion r)
        {
            var go = Root("Bowling Pin", p, r);
            var mb = new MeshBuilder { Color = Color.white, Gloss = 0.8f };
            float[] prof = { 0.035f, 0.055f, 0.06f, 0.055f, 0.035f, 0.025f, 0.03f, 0.032f, 0.025f, 0.005f };
            mb.Loft(prof.Length, 14, i => (Vector3.up * (i * 0.042f), prof[i], prof[i]), Vector3.up, true, true);
            mb.SetColor(Color.red);
            mb.Cylinder(Vector3.up * 0.235f, Vector3.up * 0.255f, 0.027f, 0.027f, 14);
            Render(go, mb, Mats.Lit);
            var cc = go.AddComponent<CapsuleCollider>(); cc.radius = 0.055f; cc.height = 0.38f; cc.center = Vector3.up * 0.19f;
            Body(go, 1.5f, SurfaceType.Wood);
            return go;
        }

        static GameObject Can(Vector3 p, Quaternion r)
        {
            var go = Root("Soda Can", p, r);
            var mb = new MeshBuilder { Color = Rng.Pick(new[] { new Color(0.85f, 0.1f, 0.1f), new Color(0.1f, 0.4f, 0.85f), new Color(0.15f, 0.7f, 0.25f) }), Gloss = 0.7f };
            mb.Cylinder(new Vector3(0, -0.06f, 0), new Vector3(0, 0.06f, 0), 0.033f, 0.033f, 12);
            Render(go, mb, Mats.LitMetal);
            var cc = go.AddComponent<CapsuleCollider>(); cc.radius = 0.033f; cc.height = 0.12f;
            Body(go, 0.35f, SurfaceType.Metal);
            return go;
        }

        static GameObject Bottle(Vector3 p, Quaternion r)
        {
            var go = Root("Bottle", p, r);
            var mb = new MeshBuilder { Color = new Color(0.25f, 0.55f, 0.25f), Gloss = 0.9f };
            float[] prof = { 0.035f, 0.036f, 0.036f, 0.035f, 0.03f, 0.016f, 0.014f, 0.015f };
            mb.Loft(prof.Length, 12, i => (Vector3.up * (i * 0.04f - 0.14f), prof[i], prof[i]), Vector3.up, true, true);
            Render(go, mb, Mats.Lit);
            var cc = go.AddComponent<CapsuleCollider>(); cc.radius = 0.036f; cc.height = 0.28f;
            Body(go, 0.5f, SurfaceType.Glass);
            var br = go.AddComponent<BreakableProp>(); br.Health = 5f; br.Material = SurfaceType.Glass;
            return go;
        }

        static GameObject Pallet(Vector3 p, Quaternion r)
        {
            var go = Root("Pallet", p, r);
            var mb = new MeshBuilder { Color = new Color(0.7f, 0.55f, 0.35f), Gloss = 0.1f };
            for (int i = 0; i < 5; i++) mb.Box(new Vector3(-0.48f + i * 0.24f, 0.13f, 0), new Vector3(0.14f, 0.025f, 1.2f), 1.5f);
            for (int i = 0; i < 3; i++) mb.Box(new Vector3(0, 0.06f, -0.55f + i * 0.55f), new Vector3(1.1f, 0.11f, 0.1f), 1.5f);
            Render(go, mb, Mats.LitTextured(TextureFactory.Wood));
            var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.07f, 0); bc.size = new Vector3(1.1f, 0.15f, 1.2f);
            Body(go, 15f, SurfaceType.Wood);
            var br = go.AddComponent<BreakableProp>(); br.Health = 60f; br.Fragments = 7;
            return go;
        }

        static GameObject Dumbbell(Vector3 p, Quaternion r)
        {
            var go = Root("Dumbbell", p, r);
            var mb = new MeshBuilder { Color = new Color(0.15f, 0.15f, 0.16f), Gloss = 0.6f };
            mb.Cylinder(new Vector3(-0.22f, 0, 0), new Vector3(-0.12f, 0, 0), 0.1f, 0.1f, 14);
            mb.Cylinder(new Vector3(0.12f, 0, 0), new Vector3(0.22f, 0, 0), 0.1f, 0.1f, 14);
            mb.SetColor(new Color(0.7f, 0.7f, 0.72f), 0.85f);
            mb.Cylinder(new Vector3(-0.12f, 0, 0), new Vector3(0.12f, 0, 0), 0.02f, 0.02f, 10);
            Render(go, mb, Mats.LitMetal);
            var cc = go.AddComponent<CapsuleCollider>(); cc.radius = 0.1f; cc.height = 0.44f; cc.direction = 0;
            Body(go, 20f, SurfaceType.Metal);
            return go;
        }
    }
}
