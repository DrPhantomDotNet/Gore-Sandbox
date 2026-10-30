using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// The live 3D scene behind the main menu: a blood-moon stage with an idle Goredoll that watches the
    /// camera. Click it (poke) and it reacts; poke it enough and it falls over.
    /// </summary>
    public sealed class MenuBackdrop : MonoBehaviour
    {
        static MenuBackdrop current;
        Camera cam;
        Goredoll doll;
        float orbit, nextIdle, pokeCooldown;
        int pokes;
        static readonly string[] PokeLines = { "v_hey", "v_ow", "v_huh", "v_no", "v_whoa" };

        /// <summary>The live menu camera (the intro cinematic flies into its pose), or null.</summary>
        public static Camera Cam => current != null ? current.cam : null;

        public static void Create()
        {
            if (current != null) return;
            var go = new GameObject("MenuBackdrop");
            current = go.AddComponent<MenuBackdrop>();
            current.Build();
        }

        public static void DestroyBackdrop()
        {
            if (current == null) return;
            if (current.doll != null) current.doll.Despawn();
            Destroy(current.gameObject);
            current = null;
        }

        void Build()
        {
            var root = transform;
            MapLighting.Apply(TimeOfDay.BloodMoon, false, root);
            var b = new MapBuilder(root, null, new MapOptions());
            var floor = MapBuilder.Tex(TextureFactory.Concrete);
            b.Block("Stage", new Vector3(-12, -0.5f, -12), new Vector3(12, 0, 12), floor, SurfaceType.Concrete, new Color(0.45f, 0.38f, 0.38f));
            b.Block("BackWall", new Vector3(-12, 0, 6), new Vector3(12, 8, 6.6f), MapBuilder.Tex(TextureFactory.Brick), SurfaceType.Concrete, new Color(0.5f, 0.3f, 0.3f));
            b.Box("Crate", new Vector3(-2.6f, 0.4f, 2.5f), Quaternion.Euler(0, 20, 0), Vector3.one * 0.8f, MapBuilder.Tex(TextureFactory.Wood), SurfaceType.Wood);
            b.Box("Crate2", new Vector3(-2.4f, 1.1f, 2.6f), Quaternion.Euler(0, -10, 0), Vector3.one * 0.6f, MapBuilder.Tex(TextureFactory.Wood), SurfaceType.Wood);
            b.Cylinder("Barrel", new Vector3(3.2f, 0, 3f), 0.32f, 0.95f, Mats.LitMetal, SurfaceType.Metal, new Color(0.7f, 0.1f, 0.08f));
            b.StreetLamp(new Vector3(4.5f, 0, 4.5f), -135f, new Color(1f, 0.55f, 0.4f), 4.5f, 14f, 3f);
            b.Finish();
            Physics.SyncTransforms();
            // Old blood on the floor and wall.
            for (int i = 0; i < 18; i++)
            {
                var p = new Vector3(Random.Range(-5f, 5f), 0.01f, Random.Range(-3f, 5.5f));
                Decals.Blood(p, Vector3.up, Random.Range(0.4f, 1.6f), Random.value < 0.3f ? 2 : 0, null);
            }
            for (int i = 0; i < 6; i++) Decals.Blood(new Vector3(Random.Range(-4f, 4f), Random.Range(0.5f, 3f), 5.99f), Vector3.back, Random.Range(0.5f, 1.4f), 1, null);

            var camGo = new GameObject("MenuCamera");
            camGo.transform.SetParent(root, false);
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.05f;
            cam.cullingMask = ~((1 << Layers.Viewmodel) | (1 << 31));
            camGo.AddComponent<AudioListener>();

            doll = GoredollFactory.Spawn(new Vector3(0.6f, 0f, 1.2f), 200f, "Passive", 0, root);
            if (doll.Brain != null) doll.Brain.enabled = false;
            doll.DisplayName = "Menu Guy";
            UpdateCamera(0f);
        }

        public static void TryPoke(Vector2 guiPosition)
        {
            if (current == null || current.cam == null || current.doll == null || current.pokeCooldown > 0f) return;
            var ray = current.cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 50f, 1 << Layers.Ragdoll)) return;
            var part = hit.collider.GetComponent<BodyPart>();
            if (part == null || part.Doll != current.doll) return;
            current.Poke(part, hit, ray.direction);
        }

        void Poke(BodyPart part, RaycastHit hit, Vector3 dir)
        {
            pokeCooldown = 0.25f;
            pokes++;
            part.Body.AddForceAtPosition(dir * 45f, hit.point, ForceMode.Impulse);
            AudioManager.Play("punch_hit", hit.point, 0.4f, 1.3f);
            if (doll.Vitals.Conscious) doll.Voice.Say(PokeLines[Random.Range(0, PokeLines.Length)], 1f, true);
            if (doll.Face != null) doll.Face.Expression = pokes > 4 ? Expression.Angry : Expression.Afraid;
            if (pokes % 6 == 0) doll.Ragdoll.Fall(2f);
            Stats.Add("menu_pokes");
            if (pokes == 25) EasterEggs.Found("Menu Bully", "Poked the menu Goredoll 25 times. He will remember that.");
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            pokeCooldown -= dt;
            UpdateCamera(dt);
            if (doll == null) return;
            if (doll.IsDead && Time.time - doll.Vitals.TimeOfDeath > 4f) doll.Revive();
            doll.Ragdoll.LookTarget = cam.transform.position;
            if (doll.Face != null) doll.Face.LookAt(cam.transform.position);
            doll.Ragdoll.DesiredVelocity = Vector3.zero;
            doll.Ragdoll.DesiredYaw = Quaternion.LookRotation(MathX.Flat(cam.transform.position - doll.Position) + Vector3.forward * 0.001f).eulerAngles.y;

            // Small idle routine: wave, look around, dance, back to idle.
            nextIdle -= dt;
            if (nextIdle <= 0f && doll.Ragdoll.IsStanding)
            {
                nextIdle = Random.Range(5f, 10f);
                var r = Random.value;
                doll.Ragdoll.Pose = r < 0.25f ? PoseMode.Wave : r < 0.45f ? PoseMode.LookAround : r < 0.55f ? PoseMode.Dance : PoseMode.Idle;
                if (doll.Ragdoll.Pose == PoseMode.Wave) doll.Voice.Say("v_hey", 0.8f, false);
                if (doll.Ragdoll.Pose == PoseMode.Idle && Random.value < 0.4f) doll.Voice.Say(Random.value < 0.5f ? "v_hum" : "v_whistle", 0.6f, false);
                if (doll.Face != null) doll.Face.Expression = doll.Ragdoll.Pose == PoseMode.Dance ? Expression.Happy : Expression.Neutral;
            }
        }

        void UpdateCamera(float dt)
        {
            orbit += dt * 0.06f;
            // Doll on the right third of the screen, menu buttons on the left.
            var focus = new Vector3(0.6f, 1.05f, 1.2f);
            float yaw = Mathf.Sin(orbit) * 12f + 200f;
            var offset = Quaternion.Euler(6f + Mathf.Sin(orbit * 0.7f) * 2f, yaw, 0) * new Vector3(0, 0, 4.2f);
            var mouse = new Vector2(Input.mousePosition.x / Mathf.Max(1, Screen.width) - 0.5f, Input.mousePosition.y / Mathf.Max(1, Screen.height) - 0.5f);
            cam.transform.position = focus - offset + new Vector3(mouse.x * 0.25f, mouse.y * 0.15f, 0);
            cam.transform.rotation = Quaternion.LookRotation(focus - cam.transform.position) * Quaternion.Euler(0, -9f, 0);
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }
    }
}
