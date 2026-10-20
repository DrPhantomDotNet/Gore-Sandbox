using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using SolarEclipse.Audio;
using W = SolarEclipse.Audio.RecipesWeapons;
using G = SolarEclipse.Audio.RecipesGore;
using V = SolarEclipse.Audio.RecipesVoice;
using Wo = SolarEclipse.Audio.RecipesWorld;
using U = SolarEclipse.Audio.RecipesUi;

namespace SolarEclipse
{
    /// <summary>
    /// Every sound in the game, synthesised at startup on worker threads.
    /// Drop a clip named after an entry into Resources/AudioOverrides/ to replace it with a recording.
    /// </summary>
    public static class SoundBank
    {
        sealed class Entry
        {
            public string Name;
            public int Variants;
            public int Rate;
            public Func<FastRng, int, int, float[]> Gen;
            public float[][] Data;
            public AudioClip[] Clips;
            public int Last = -1;
        }

        static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        static readonly HashSet<string> warned = new HashSet<string>();
        public static float Progress { get; private set; }
        public static bool Ready { get; private set; }
        public static int ClipCount { get; private set; }
        public static int SoundCount => entries.Count;
        public static IEnumerable<string> Names => entries.Keys;

        static void Def(string name, int variants, Func<FastRng, int, int, float[]> gen, int rate = 44100)
            => entries[name] = new Entry { Name = name, Variants = variants, Gen = gen, Rate = rate };

        static void DefineAll()
        {
            if (entries.Count > 0) return;
            // Firearms
            Def("gun_glock", 3, (r, s, v) => W.Gunshot(r, s, W.Glock));
            Def("gun_uzi", 4, (r, s, v) => W.Gunshot(r, s, W.Uzi));
            Def("gun_ar15", 3, (r, s, v) => W.Gunshot(r, s, W.Ar15));
            Def("gun_ar17", 2, (r, s, v) => W.Gunshot(r, s, W.Ar17));
            Def("gun_admin", 2, (r, s, v) => W.AdminShot(r, s));
            Def("gun_tail", 2, (r, s, v) => W.DistantTail(r, s), 32000);
            Def("dry_fire", 2, (r, s, v) => W.DryFire(r, s));
            Def("slide_back", 2, (r, s, v) => W.SlideBack(r, s));
            Def("slide_fwd", 2, (r, s, v) => W.SlideForward(r, s));
            Def("mag_out_pistol", 2, (r, s, v) => W.MagOut(r, s, 1.1f));
            Def("mag_in_pistol", 2, (r, s, v) => W.MagIn(r, s, 1.1f));
            Def("mag_out_smg", 2, (r, s, v) => W.MagOut(r, s, 0.9f));
            Def("mag_in_smg", 2, (r, s, v) => W.MagIn(r, s, 0.9f));
            Def("mag_out_rifle", 2, (r, s, v) => W.MagOut(r, s, 0.75f));
            Def("mag_in_rifle", 2, (r, s, v) => W.MagIn(r, s, 0.75f));
            Def("charging_handle", 2, (r, s, v) => W.ChargingHandle(r, s));
            Def("bolt_release", 2, (r, s, v) => W.BoltRelease(r, s));
            Def("shell_in", 3, (r, s, v) => W.ShellIn(r, s));
            Def("fire_mode", 1, (r, s, v) => W.FireMode(r, s));
            Def("equip_gun", 2, (r, s, v) => W.EquipGun(r, s));
            Def("equip_knife", 2, (r, s, v) => W.KnifeDraw(r, s));
            Def("cloth", 3, (r, s, v) => W.Cloth(r, s, 0.4f));
            Def("casing", 5, (r, s, v) => W.Casing(r, s, false));
            Def("shell_casing", 3, (r, s, v) => W.Casing(r, s, true));
            Def("whoosh_small", 4, (r, s, v) => W.Whoosh(r, s, 0.28f, 500, 2400));
            Def("whoosh_big", 3, (r, s, v) => W.Whoosh(r, s, 0.4f, 250, 1300));
            Def("knife_hit_hard", 2, (r, s, v) => W.KnifeHitHard(r, s));
            Def("punch_hit", 4, (r, s, v) => W.PunchHit(r, s));
            Def("kick_hit", 3, (r, s, v) => W.Thud(r, s, 1f, 0.4f));
            Def("admin_mode", 1, (r, s, v) => W.Chime(r, s, 880f));
            // Spawn gun
            Def("sg_hum", 1, (r, s, v) => W.SpawnGunHum(r, s));
            Def("sg_spawn", 3, (r, s, v) => W.SpawnPop(r, s));
            Def("sg_delete", 2, (r, s, v) => W.Zap(r, s, 0.4f, 2200, 120));
            Def("sg_freeze", 1, (r, s, v) => W.Chime(r, s, 1760f));
            Def("sg_unfreeze", 1, (r, s, v) => W.Chime(r, s, 880f));
            Def("sg_grab", 2, (r, s, v) => W.Zap(r, s, 0.15f, 300, 900));
            Def("sg_release", 2, (r, s, v) => W.Zap(r, s, 0.15f, 900, 300));
            Def("sg_throw", 2, (r, s, v) => W.Zap(r, s, 0.35f, 200, 1800));
            Def("sg_tick", 1, (r, s, v) => U.Blip(r, s, 2600, 2600, 0.03f, 0.3f));
            Def("sg_scale", 1, (r, s, v) => U.Blip(r, s, 500, 1200, 0.2f, 0.4f));
            // Gore
            Def("flesh_hit", 6, (r, s, v) => G.FleshHit(r, s));
            Def("headshot", 3, (r, s, v) => G.Headshot(r, s));
            Def("bone_crack", 4, (r, s, v) => G.BoneCrack(r, s));
            Def("stab", 3, (r, s, v) => G.Stab(r, s));
            Def("slice", 4, (r, s, v) => G.Slice(r, s));
            Def("gib", 3, (r, s, v) => G.Gib(r, s));
            Def("dismember", 3, (r, s, v) => G.Dismember(r, s));
            Def("blood_spray", 2, (r, s, v) => G.Spray(r, s, 1.2f));
            Def("arterial", 3, (r, s, v) => G.ArterialPulse(r, s));
            Def("drip", 6, (r, s, v) => G.Drip(r, s), 32000);
            Def("splat", 4, (r, s, v) => G.Splat(r, s));
            Def("gurgle", 3, (r, s, v) => G.Gurgle(r, s, 1.2f));
            Def("heartbeat", 1, (r, s, v) => G.Heartbeat(r, s), 22050);
            // Voice (Goredoll)
            Def("v_pain", 6, (r, s, v) => V.PainGrunt(r, s, v), 32000);
            Def("v_scream", 5, (r, s, v) => V.Scream(r, s, v), 32000);
            Def("v_argh", 4, (r, s, v) => V.Argh(r, s, v), 32000);
            Def("v_moan", 4, (r, s, v) => V.Moan(r, s, v), 32000);
            Def("v_whimper", 4, (r, s, v) => V.Whimper(r, s, v), 32000);
            Def("v_no", 3, (r, s, v) => V.NoNoNo(r, s, v), 32000);
            Def("v_hey", 2, (r, s, v) => V.Hey(r, s, v), 32000);
            Def("v_huh", 2, (r, s, v) => V.Huh(r, s, v), 32000);
            Def("v_whoa", 2, (r, s, v) => V.Whoa(r, s, v), 32000);
            Def("v_ow", 4, (r, s, v) => V.Ow(r, s, v), 32000);
            Def("v_cough", 3, (r, s, v) => V.Cough(r, s, v, false), 32000);
            Def("v_cough_wet", 3, (r, s, v) => V.Cough(r, s, v, true), 32000);
            Def("v_choke", 4, (r, s, v) => V.Choke(r, s, v), 32000);
            Def("v_gasp", 3, (r, s, v) => V.Gasp(r, s, v), 32000);
            Def("v_hum", 2, (r, s, v) => V.Hum(r, s, v), 32000);
            Def("v_whistle", 2, (r, s, v) => V.Whistle(r, s, v), 32000);
            Def("v_death", 3, (r, s, v) => V.DeathGroan(r, s, v), 32000);
            Def("v_sneeze", 1, (r, s, v) => V.Sneeze(r, s, v), 32000);
            for (int i = 0; i < 4; i++)
            {
                int lvl = i;
                Def("breath_in_" + i, 2, (r, s, v) => V.Breath(r, s, true, lvl, false), 32000);
                Def("breath_out_" + i, 2, (r, s, v) => V.Breath(r, s, false, lvl, false), 32000);
            }
            Def("breath_in_wet", 2, (r, s, v) => V.Breath(r, s, true, 2, true), 32000);
            Def("breath_out_wet", 2, (r, s, v) => V.Breath(r, s, false, 2, true), 32000);
            Def("player_hurt", 4, (r, s, v) => V.PlayerHurt(r, s, v), 32000);
            // World
            Def("step_concrete", 5, (r, s, v) => Wo.Footstep(r, s, "concrete"), 32000);
            Def("step_grass", 5, (r, s, v) => Wo.Footstep(r, s, "grass"), 32000);
            Def("step_metal", 4, (r, s, v) => Wo.Footstep(r, s, "metal"), 32000);
            Def("step_wood", 4, (r, s, v) => Wo.Footstep(r, s, "wood"), 32000);
            Def("step_carpet", 4, (r, s, v) => Wo.Footstep(r, s, "carpet"), 32000);
            Def("land", 3, (r, s, v) => W.Thud(r, s, 0.8f, 0.3f));
            Def("body_thud", 5, (r, s, v) => W.Thud(r, s, 0.7f, 0.3f));
            Def("body_thud_soft", 4, (r, s, v) => W.Thud(r, s, 0.3f, 0.2f));
            Def("explosion", 3, (r, s, v) => Wo.Explosion(r, s, false));
            Def("explosion_far", 2, (r, s, v) => Wo.Explosion(r, s, true), 32000);
            Def("wood_impact", 4, (r, s, v) => Wo.WoodImpact(r, s));
            Def("metal_impact", 4, (r, s, v) => Wo.MetalImpact(r, s, false));
            Def("metal_heavy", 3, (r, s, v) => Wo.MetalImpact(r, s, true));
            Def("ricochet", 4, (r, s, v) => Wo.Ricochet(r, s));
            Def("impact_concrete", 4, (r, s, v) => Wo.BulletImpact(r, s, "concrete"));
            Def("impact_metal", 4, (r, s, v) => Wo.BulletImpact(r, s, "metal"));
            Def("impact_wood", 3, (r, s, v) => Wo.BulletImpact(r, s, "wood"));
            Def("impact_dirt", 3, (r, s, v) => Wo.BulletImpact(r, s, "dirt"));
            Def("whiz", 3, (r, s, v) => Wo.BulletWhiz(r, s));
            Def("glass_break", 3, (r, s, v) => Wo.GlassBreak(r, s, false));
            Def("glass_break_big", 2, (r, s, v) => Wo.GlassBreak(r, s, true));
            Def("crate_break", 3, (r, s, v) => Wo.CrateBreak(r, s));
            Def("bell", 2, (r, s, v) => Wo.Bell(r, s));
            Def("boing", 2, (r, s, v) => Wo.Boing(r, s));
            Def("button", 2, (r, s, v) => Wo.Button(r, s));
            Def("hiss", 1, (r, s, v) => Wo.Hiss(r, s, 2f));
            Def("creak", 2, (r, s, v) => Wo.Creak(r, s));
            Def("duck", 2, (r, s, v) => Wo.Squeak(r, s));
            Def("ding", 1, (r, s, v) => Wo.Ding(r, s));
            Def("buzzer", 1, (r, s, v) => Wo.Buzzer(r, s));
            Def("tv_static", 1, (r, s, v) => Wo.Static(r, s, 2f), 22050);
            Def("fan_loop", 1, (r, s, v) => Wo.Fan(r, s), 22050);
            Def("press_slam", 2, (r, s, v) => Wo.PressSlam(r, s));
            Def("amb_wind", 1, (r, s, v) => Wo.Wind(r, s), 22050);
            Def("amb_crickets", 1, (r, s, v) => Wo.Crickets(r, s), 22050);
            Def("amb_hum", 1, (r, s, v) => Wo.FluorescentHum(r, s), 22050);
            Def("amb_city", 1, (r, s, v) => Wo.CityRumble(r, s), 22050);
            Def("amb_drone", 1, (r, s, v) => Wo.Drone(r, s, 55f), 22050);
            Def("nextbot_chase", 1, (r, s, v) => Wo.NextbotChase(r, s), 32000);
            Def("jumpscare", 1, (r, s, v) => Wo.Jumpscare(r, s));
            Def("radio", 1, (r, s, v) => Wo.RadioTune(r, s), 22050);
            // UI + logo + music
            Def("ui_hover", 1, (r, s, v) => U.Blip(r, s, 2300, 2500, 0.03f, 0.25f));
            Def("ui_click", 1, (r, s, v) => U.Notes(s, new[] { 880f, 1320f }, 0.035f, 0.03f, 0.45f));
            Def("ui_back", 1, (r, s, v) => U.Notes(s, new[] { 1320f, 880f }, 0.035f, 0.03f, 0.45f));
            Def("ui_open", 1, (r, s, v) => W.Whoosh(r, s, 0.25f, 800, 3000, 1f));
            Def("ui_close", 1, (r, s, v) => W.Whoosh(r, s, 0.2f, 2500, 700, 1f));
            Def("ui_toggle_on", 1, (r, s, v) => U.Blip(r, s, 700, 1400, 0.08f, 0.4f));
            Def("ui_toggle_off", 1, (r, s, v) => U.Blip(r, s, 1400, 700, 0.08f, 0.4f));
            Def("ui_error", 1, (r, s, v) => Wo.Buzzer(r, s));
            Def("ui_success", 1, (r, s, v) => U.Notes(s, new[] { 523.25f, 659.25f, 783.99f }, 0.07f, 0.12f, 0.5f));
            Def("ui_achievement", 1, (r, s, v) => U.Achievement(r, s));
            Def("ui_type", 3, (r, s, v) => W.Click(r, s, 1.5f, 0.05f, 0.04f));
            Def("ui_notify", 1, (r, s, v) => U.Notes(s, new[] { 1046.5f, 1567.98f }, 0.1f, 0.15f, 0.45f));
            Def("ui_tab", 1, (r, s, v) => U.Blip(r, s, 1500, 1700, 0.05f, 0.35f));
            Def("ui_slider", 1, (r, s, v) => U.Blip(r, s, 3000, 3000, 0.012f, 0.2f));
            Def("logo_riser", 1, (r, s, v) => U.Riser(r, s, 2.6f));
            Def("logo_whoosh", 1, (r, s, v) => W.Whoosh(r, s, 1.1f, 200, 2400, 0.9f));
            Def("logo_impact", 1, (r, s, v) => U.Impact(r, s));
            Def("logo_shimmer", 1, (r, s, v) => U.Shimmer(r, s));
            Def("music_menu", 1, (r, s, v) => U.MenuMusic(r, s), 32000);
            Def("noclip_on", 1, (r, s, v) => W.Whoosh(r, s, 0.4f, 400, 3000, 2f));
            Def("noclip_off", 1, (r, s, v) => W.Whoosh(r, s, 0.4f, 3000, 400, 2f));
            RecipesBoss.Define(Def); // "????" boss realm
            RecipesBackrooms.Define(Def); // Backrooms hum + light flicker
            RecipesWeather.Define(Def); // rain loop, thunder, kill-streak stingers
            RecipesZombie.Define(Def); // zombie groans, bites, shrieks
        }

        /// <summary>Synthesises everything; yields every frame while turning finished buffers into clips.</summary>
        public static IEnumerator GenerateAll()
        {
            DefineAll();
            if (Ready) yield break;
            var done = new ConcurrentQueue<Entry>();
            int total = entries.Count, finished = 0, seed = 1;
            foreach (var e in entries.Values)
            {
                var entry = e;
                int s = seed++;
                Task.Run(() =>
                {
                    try
                    {
                        entry.Data = new float[entry.Variants][];
                        for (int v = 0; v < entry.Variants; v++) entry.Data[v] = entry.Gen(new FastRng(s * 7919 + v * 104729), entry.Rate, v);
                    }
                    catch (Exception ex)
                    {
                        entry.Data = new[] { new float[64] };
                        UnityEngine.Debug.LogError("Sound '" + entry.Name + "' failed: " + ex.Message);
                    }
                    done.Enqueue(entry);
                });
            }
            while (finished < total)
            {
                int budget = 6;
                while (budget-- > 0 && done.TryDequeue(out var e))
                {
                    MakeClips(e);
                    finished++;
                }
                Progress = finished / (float)total;
                yield return null;
            }
            Ready = true;
            Debug.Log($"[SoundBank] {entries.Count} sounds / {ClipCount} clips synthesised.");
        }

        static void MakeClips(Entry e)
        {
            var overrides = new List<AudioClip>();
            var single = Resources.Load<AudioClip>("AudioOverrides/" + e.Name);
            if (single != null) overrides.Add(single);
            for (int i = 1; i <= 8; i++)
            {
                var c = Resources.Load<AudioClip>("AudioOverrides/" + e.Name + "_" + i);
                if (c != null) overrides.Add(c);
            }
            if (overrides.Count > 0) { e.Clips = overrides.ToArray(); e.Data = null; ClipCount += e.Clips.Length; return; }

            e.Clips = new AudioClip[e.Data.Length];
            for (int v = 0; v < e.Data.Length; v++)
            {
                var data = e.Data[v];
                var clip = AudioClip.Create(e.Name + "_" + v, data.Length, 1, e.Rate, false);
                clip.SetData(data, 0);
                e.Clips[v] = clip;
                ClipCount++;
            }
            e.Data = null;
        }

        public static bool Has(string name) => entries.TryGetValue(name, out var e) && e.Clips != null;

        /// <summary>A random variant, never the same one twice in a row.</summary>
        public static AudioClip Get(string name)
        {
            if (!entries.TryGetValue(name, out var e) || e.Clips == null || e.Clips.Length == 0)
            {
                if (Ready && warned.Add(name)) Debug.LogWarning("[SoundBank] Unknown sound: " + name);
                return null;
            }
            if (e.Clips.Length == 1) return e.Clips[0];
            int i = UnityEngine.Random.Range(0, e.Clips.Length);
            if (i == e.Last) i = (i + 1) % e.Clips.Length;
            e.Last = i;
            return e.Clips[i];
        }
    }
}
