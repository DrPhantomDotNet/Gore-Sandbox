using System.Collections;
using System.IO;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Automatic screenshots for development (.github/workflows/screenshots.yml). Only runs when the game is started
    /// with "-gs-screenshots &lt;folder&gt;": plays the intro, opens the menu, loads maps, spawns and hurts Goredolls,
    /// switches to Boxing, saves a PNG at each step and quits. Normal players never trigger it.
    /// </summary>
    public sealed class ScreenshotTour : MonoBehaviour
    {
        string dir;
        int n;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-gs-screenshots")
                {
                    var go = new GameObject("ScreenshotTour");
                    DontDestroyOnLoad(go);
                    var t = go.AddComponent<ScreenshotTour>();
                    t.dir = args[i + 1];
                    Directory.CreateDirectory(t.dir);
                    t.StartCoroutine(t.Run());
                    return;
                }
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            var path = Path.Combine(dir, (++n).ToString("00") + "_" + name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log("[ScreenshotTour] saved " + path);
        }

        static IEnumerator Wait(float s) { float end = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < end) yield return null; }

        IEnumerator Run()
        {
            Debug.Log("[ScreenshotTour] start");
            float limit = Time.realtimeSinceStartup + 120f;
            while ((GameManager.I == null || !SoundBank.Ready) && Time.realtimeSinceStartup < limit) yield return null;
            Settings.Data.setupDone = true;
            Settings.Data.goreLevel = Mathf.Max(Settings.Data.goreLevel, 5);
            yield return Wait(1f);
            var setup = UIRoot.Find<SetupScreen>(); if (setup != null) UIRoot.Remove(setup);

            // 1. The intro.
            UIRoot.Replace(new Intro2Screen());
            float[] marks = { 1.2f, 4.6f, 7.2f, 9.6f, 11.45f, 14.3f, 15.6f };
            string[] names = { "intro_studio", "intro_morgue", "intro_gurney", "intro_hand", "intro_jolt", "intro_title", "intro_title2" };
            for (int i = 0; i < marks.Length; i++)
            {
                limit = Time.realtimeSinceStartup + 30f;
                while (Intro2Screen.Time01 < marks[i] && Time.realtimeSinceStartup < limit) yield return null;
                yield return Shot(names[i]);
            }

            // 2. The menu (operating room, then the classic stage).
            limit = Time.realtimeSinceStartup + 30f;
            while (!(UIRoot.Top is MainMenuScreen) && Time.realtimeSinceStartup < limit) yield return null;
            yield return Wait(3f);
            yield return Shot("menu_operating_room");

            // 3. A map, Goredolls, gore.
            foreach (var mapId in new[] { "hospital", "flatgrass" })
            {
                GameManager.I.StartMap(MapCatalog.Get(mapId), new MapOptions { Time = MapCatalog.Get(mapId).DefaultTime, StartingNpcs = 0 });
                limit = Time.realtimeSinceStartup + 60f;
                while ((GameManager.I.State != GameState.Playing || PlayerController.Instance == null) && Time.realtimeSinceStartup < limit) yield return null;
                yield return Wait(3f);
                var p = PlayerController.Instance;
                if (p == null) continue;
                var fwd = MathX.Flat(p.AimDirection).normalized;
                var right = Vector3.Cross(Vector3.up, fwd);
                var dolls = new System.Collections.Generic.List<Goredoll>();
                for (int i = 0; i < 4; i++)
                {
                    var at = p.transform.position + fwd * (3.2f + (i % 2) * 1.2f) + right * (i - 1.5f) * 1.1f;
                    var d = GoredollFactory.Spawn(at, Quaternion.LookRotation(-fwd).eulerAngles.y, "Normal", -1, null);
                    if (d != null) dolls.Add(d);
                }
                yield return Wait(2.5f);
                yield return Shot(mapId + "_goredolls");
                // Shoot them: head, neck (arterial), chest; blow one up.
                for (int i = 0; i < dolls.Count; i++)
                {
                    var d = dolls[i];
                    if (d == null) continue;
                    foreach (var pt in i == 0 ? new[] { PartType.Neck, PartType.Chest } : i == 1 ? new[] { PartType.ThighL, PartType.Abdomen, PartType.Abdomen } : new[] { PartType.Chest, PartType.UpperArmR })
                        if (d.Parts.TryGetValue(pt, out var bp) && bp != null && bp.Body != null)
                        {
                            var dir = (bp.Body.position - p.EyePosition).normalized;
                            bp.TakeDamage(new DamageInfo { Amount = 45f, Force = dir * 30f, Point = bp.Body.position - dir * 0.08f, Normal = -dir, Direction = dir, Type = DamageType.Bullet, Caliber = 9f, Source = p, Weapon = "Screenshot" });
                        }
                }
                if (dolls.Count > 3 && dolls[3] != null) Explosion.Create(dolls[3].Position + Vector3.down * 0.5f, 3f, 300f, null);
                yield return Wait(1.2f);
                yield return Shot(mapId + "_gore_hit");
                yield return Wait(12f);
                yield return Shot(mapId + "_gore_pools");
                // Boxing in third person.
                p.Inventory.Give(FightingStyles.BoxingId);
                yield return Wait(2.5f);
                yield return Shot(mapId + "_boxing");
            }
            Debug.Log("[ScreenshotTour] done");
            yield return Wait(1f);
            Application.Quit();
        }
    }
}
