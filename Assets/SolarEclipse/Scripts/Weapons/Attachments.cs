using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    public enum AttachSlot { Sight, Magazine, Muzzle, Grip, Laser }

    /// <summary>One attachment choice (index 0 of every slot is the stock/none option).</summary>
    public sealed class AttachmentDef
    {
        public string Id, Name, Description;
        public AttachSlot Slot;
    }

    /// <summary>Stat modifiers applied on the live <see cref="Firearm"/> (multipliers default to 1).</summary>
    public struct AttachStats
    {
        public float MagMul, ReloadMul, RecoilMul, HipSpreadMul, AimSpreadMul, AimSpeedMul, DamageMul, RangeMul;
        /// <summary>0 = keep the weapon's own value.</summary>
        public float AimFov, AimDistance;
        public bool Suppressed, Laser;
        /// <summary>0 iron sights, 1 red dot, 2 holographic, 3 4x scope.</summary>
        public int Optic;

        public static AttachStats None => new AttachStats
        {
            MagMul = 1f, ReloadMul = 1f, RecoilMul = 1f, HipSpreadMul = 1f, AimSpreadMul = 1f, AimSpeedMul = 1f, DamageMul = 1f, RangeMul = 1f,
        };
    }

    /// <summary>
    /// Weapon customization: per-weapon attachment loadouts (saved in PlayerPrefs), which attachments fit which gun
    /// (data below), the stat modifiers, and mounting the attachment meshes (Weapons/AttachmentModels.cs) on a model.
    /// Applied by Firearm.OnAttach, so every way of getting a gun (spawn menu, Full Arsenal, Horde refill, respawn) gets it.
    /// Multiplayer: purely local (stats + visuals on the owner's machine; remote avatars show stock guns and the
    /// host still uses catalog damage for a client's shots).
    /// </summary>
    public static class Attachments
    {
        public static readonly string[] SlotNames = { "SIGHT", "MAGAZINE", "MUZZLE", "GRIP", "LASER" };

        public static readonly List<AttachmentDef>[] Options =
        {
            new List<AttachmentDef>
            {
                new AttachmentDef { Id = "iron", Name = "Iron sights", Slot = AttachSlot.Sight, Description = "The gun's own sights." },
                new AttachmentDef { Id = "reddot", Name = "Red dot", Slot = AttachSlot.Sight, Description = "Small reflex sight. Clear view, slightly tighter aim." },
                new AttachmentDef { Id = "holo", Name = "Holographic", Slot = AttachSlot.Sight, Description = "Wide window with a ring reticle. Slight zoom." },
                new AttachmentDef { Id = "scope", Name = "4x scope", Slot = AttachSlot.Sight, Description = "Magnified scope. Very accurate aimed fire, slower to aim." },
            },
            new List<AttachmentDef>
            {
                new AttachmentDef { Id = "std", Name = "Standard", Slot = AttachSlot.Magazine, Description = "Stock magazine." },
                new AttachmentDef { Id = "ext", Name = "Extended", Slot = AttachSlot.Magazine, Description = "+50% ammo, slower reload." },
                new AttachmentDef { Id = "drum", Name = "Drum", Slot = AttachSlot.Magazine, Description = "Double ammo. Much slower reload, heavier to aim." },
            },
            new List<AttachmentDef>
            {
                new AttachmentDef { Id = "none", Name = "None", Slot = AttachSlot.Muzzle, Description = "Bare muzzle." },
                new AttachmentDef { Id = "supp", Name = "Suppressor", Slot = AttachSlot.Muzzle, Description = "Quiet shots, no muzzle flash, AI hears less. Slightly less damage and range." },
                new AttachmentDef { Id = "comp", Name = "Compensator", Slot = AttachSlot.Muzzle, Description = "Vents gas upward: much less recoil." },
            },
            new List<AttachmentDef>
            {
                new AttachmentDef { Id = "none", Name = "None", Slot = AttachSlot.Grip, Description = "No foregrip." },
                new AttachmentDef { Id = "vgrip", Name = "Vertical grip", Slot = AttachSlot.Grip, Description = "Less recoil and spread." },
                new AttachmentDef { Id = "agrip", Name = "Angled grip", Slot = AttachSlot.Grip, Description = "Much faster aiming down sights." },
            },
            new List<AttachmentDef>
            {
                new AttachmentDef { Id = "off", Name = "None", Slot = AttachSlot.Laser, Description = "No laser." },
                new AttachmentDef { Id = "laser", Name = "Laser sight", Slot = AttachSlot.Laser, Description = "Red laser dot where you aim. Less hip-fire spread." },
            },
        };

        /// <summary>Where attachments go on each model (model-local). Rail = top surface under the optic (z = optic centre);
        /// Under = underside of the handguard where a foregrip clamps; LaserPos = laser module centre.</summary>
        sealed class Mount
        {
            public string Allowed;           // space separated attachment ids that fit (stock options always fit)
            public Vector3 Rail, Under, LaserPos;
            public bool OpticOnSlide;
        }

        static readonly Dictionary<string, Mount> Mounts = new Dictionary<string, Mount>
        {
            ["glock"] = new Mount { Allowed = "reddot holo ext drum supp comp laser", Rail = new Vector3(0, 0.072f, 0.03f), LaserPos = new Vector3(0, 0.006f, 0.125f), OpticOnSlide = true },
            ["uzi"] = new Mount { Allowed = "reddot holo ext drum supp comp vgrip agrip laser", Rail = new Vector3(0, 0.0775f, 0.06f), Under = new Vector3(0, -0.003f, 0.15f), LaserPos = new Vector3(0.029f, 0.045f, 0.15f) },
            ["ar15"] = new Mount { Allowed = "reddot holo scope ext drum supp comp vgrip agrip laser", Rail = new Vector3(0, 0.095f, 0.08f), Under = new Vector3(0, 0.03f, 0.33f), LaserPos = new Vector3(0.033f, 0.055f, 0.41f) },
            ["ar17"] = new Mount { Allowed = "reddot holo ext comp vgrip agrip laser", Rail = new Vector3(0, 0.08f, 0.07f), Under = new Vector3(0, 0.0175f, 0.42f), LaserPos = new Vector3(0.026f, 0.035f, 0.46f) },
            ["shotgun"] = new Mount { Allowed = "reddot holo ext comp laser", Rail = new Vector3(0, 0.08f, 0.09f), LaserPos = new Vector3(0.025f, 0.05f, 0.12f) },
            ["sniper"] = new Mount { Allowed = "ext supp comp vgrip agrip laser", Under = new Vector3(0, 0.0075f, 0.4f), LaserPos = new Vector3(0.03f, 0.03f, 0.43f) },
            ["revolver"] = new Mount { Allowed = "reddot comp laser", Rail = new Vector3(0, 0.083f, 0.07f), LaserPos = new Vector3(0, 0.043f, 0.16f) },
            ["mac11"] = new Mount { Allowed = "reddot holo ext drum supp comp vgrip agrip laser", Rail = new Vector3(0, 0.078f, 0.06f), Under = new Vector3(0, 0.016f, 0.125f), LaserPos = new Vector3(0.026f, 0.04f, 0.12f) },
        };

        // Arsenal pack guns (Weapons/ArsenalPack.cs) register their mounts from their GunKit specs.
        static Attachments()
        {
            ArsenalPack.RegisterMounts((id, allowed, rail, under, laser, onSlide) =>
                Mounts[id] = new Mount { Allowed = allowed, Rail = rail, Under = under, LaserPos = laser, OpticOnSlide = onSlide });
        }

        /// <summary>Whether this weapon can be customized at all.</summary>
        public static bool Supports(WeaponDef def) => def != null && def.Kind == WeaponKind.Firearm && Mounts.ContainsKey(def.Id);
        public static bool Supports(string id) => Supports(WeaponCatalog.Get(id));

        public static bool Fits(string weaponId, int slot, int option)
        {
            if (option == 0) return true;
            if (!Mounts.TryGetValue(weaponId, out var m)) return false;
            var id = Options[slot][option].Id;
            foreach (var a in m.Allowed.Split(' ')) if (a == id) return true;
            return false;
        }

        // ------------------------------------------------------------------ saved loadouts

        static readonly Dictionary<string, int[]> cache = new Dictionary<string, int[]>();
        const string PrefPrefix = "attach_";

        /// <summary>Selected option index per slot (validated against what fits). Returns a copy.</summary>
        public static int[] Get(string weaponId)
        {
            if (!cache.TryGetValue(weaponId, out var sel))
            {
                sel = new int[Options.Length];
                string saved = "";
                try { saved = PlayerPrefs.GetString(PrefPrefix + weaponId, ""); } catch { }
                var parts = saved.Split('|');
                for (int s = 0; s < Options.Length && s < parts.Length; s++)
                {
                    int idx = Options[s].FindIndex(o => o.Id == parts[s]);
                    sel[s] = idx > 0 && Fits(weaponId, s, idx) ? idx : 0;
                }
                cache[weaponId] = sel;
            }
            return (int[])sel.Clone();
        }

        public static void Set(string weaponId, int slot, int option)
        {
            var sel = Get(weaponId);
            sel[slot] = Fits(weaponId, slot, option) ? option : 0;
            cache[weaponId] = sel;
            var ids = new string[Options.Length];
            for (int s = 0; s < ids.Length; s++) ids[s] = Options[s][sel[s]].Id;
            try { PlayerPrefs.SetString(PrefPrefix + weaponId, string.Join("|", ids)); PlayerPrefs.Save(); } catch { }
        }

        public static int CountFitted(string weaponId)
        {
            int n = 0;
            foreach (var i in Get(weaponId)) if (i > 0) n++;
            return n;
        }

        // ------------------------------------------------------------------ stats

        public static AttachStats Compute(WeaponDef def, int[] sel)
        {
            var s = AttachStats.None;
            switch (Options[0][sel[0]].Id)
            {
                case "reddot": s.Optic = 1; s.AimDistance = 0.15f; s.AimSpreadMul *= 0.85f; break;
                case "holo": s.Optic = 2; s.AimDistance = 0.15f; s.AimSpreadMul *= 0.85f; s.AimFov = def.AimFov * 0.92f; break;
                case "scope": s.Optic = 3; s.AimDistance = 0.085f; s.AimFov = 0.27f; s.AimSpreadMul *= 0.35f; s.AimSpeedMul *= 0.8f; break;
            }
            switch (Options[1][sel[1]].Id)
            {
                case "ext": s.MagMul = 1.5f; s.ReloadMul *= 1.2f; break;
                case "drum": s.MagMul = 2f; s.ReloadMul *= 1.6f; s.AimSpeedMul *= 0.75f; break;
            }
            switch (Options[2][sel[2]].Id)
            {
                case "supp": s.Suppressed = true; s.DamageMul *= 0.9f; s.RangeMul *= 0.85f; s.RecoilMul *= 0.92f; break;
                case "comp": s.RecoilMul *= 0.72f; break;
            }
            switch (Options[3][sel[3]].Id)
            {
                case "vgrip": s.RecoilMul *= 0.8f; s.HipSpreadMul *= 0.9f; s.AimSpreadMul *= 0.9f; break;
                case "agrip": s.AimSpeedMul *= 1.35f; break;
            }
            if (Options[4][sel[4]].Id == "laser") { s.Laser = true; s.HipSpreadMul *= 0.7f; }
            return s;
        }

        /// <summary>Applies the saved loadout to a freshly attached firearm (stats + meshes). Catalog guns only.</summary>
        public static void Apply(Firearm fa)
        {
            var def = fa.Def;
            if (def == null || fa.Model == null || !Supports(def) || !ReferenceEquals(WeaponCatalog.Get(def.Id), def)) return;
            var sel = Get(def.Id);
            fa.Mods = Compute(def, sel);
            if (fa.Mods.DamageMul != 1f || fa.Mods.RangeMul != 1f)
                fa.ShotDef = new WeaponDef
                {
                    Id = def.Id, Name = def.Name, Description = def.Description, Glyph = def.Glyph, Kind = def.Kind,
                    Damage = def.Damage * fa.Mods.DamageMul, Rpm = def.Rpm, HipSpread = def.HipSpread, AimSpread = def.AimSpread, Recoil = def.Recoil,
                    Range = def.Range * fa.Mods.RangeMul, Penetration = def.Penetration, Force = def.Force, Caliber = def.Caliber, Mag = def.Mag,
                    Pellets = def.Pellets, CasingType = def.CasingType, Auto = def.Auto, ShellReload = def.ShellReload, ReloadTime = def.ReloadTime,
                    EmptyExtra = def.EmptyExtra, AimFov = def.AimFov, AimDistance = def.AimDistance, NoiseRadius = def.NoiseRadius,
                    FireSound = def.FireSound, MagOut = def.MagOut, MagIn = def.MagIn, Charge = def.Charge, HipPos = def.HipPos, WeightKg = def.WeightKg,
                };
            BuildVisuals(fa.Model, def, sel, true);
        }

        /// <summary>Mounts the attachment meshes on a model and re-points its anchors (optic sight line, muzzle, left hand).</summary>
        public static void BuildVisuals(WeaponModel m, WeaponDef def, int[] sel, bool live)
        {
            if (m == null || def == null || !Mounts.TryGetValue(def.Id, out var mount)) return;
            try
            {
                int optic = sel[0];
                if (optic > 0)
                {
                    // Flip-up irons fold away under an optic.
                    foreach (var r in m.GetComponentsInChildren<Renderer>(true))
                        if (r.name == "IronSights" || r.name == "SightDot") r.enabled = false;
                    var parent = mount.OpticOnSlide && m.Slide != null ? m.Slide : m.transform;
                    var basePos = mount.OpticOnSlide && m.Slide != null ? mount.Rail - m.SlideRest : mount.Rail;
                    AttachmentModels.Optic(m, optic, parent, basePos, mount.Rail, out var rear, out var front);
                    m.SightRear = m.Anchor("OpticRear", rear, Quaternion.identity);
                    m.SightFront = m.Anchor("OpticFront", front, Quaternion.identity);
                    m.Sight = m.SightRear;
                }
                if (sel[1] > 0 && !def.ShellReload) AttachmentModels.Magazine(m, sel[1] == 2);
                if (sel[2] > 0 && m.Muzzle != null)
                {
                    float len = AttachmentModels.MuzzleDevice(m, sel[2] == 1, def.Caliber > 7f || def.Pellets > 1);
                    m.Muzzle.localPosition += m.Muzzle.localRotation * Vector3.forward * len;
                }
                if (sel[3] > 0 && mount.Under != Vector3.zero)
                {
                    AttachmentModels.Foregrip(m, mount.Under, sel[3] == 1);
                    if (m.SupportL != null)
                    {
                        if (sel[3] == 1)
                        {
                            // Left hand wraps the vertical grip (mirror of a right hand on a pistol grip).
                            var gripCentre = mount.Under + new Vector3(0, -0.04f, 0);
                            m.SupportL.localPosition = gripCentre + new Vector3(-0.024f, -0.02f, -0.055f);
                            m.SupportL.localRotation = Quaternion.Euler(-2f, 0f, 90f);
                        }
                        else m.SupportL.localPosition += new Vector3(0, -0.01f, Mathf.Clamp(mount.Under.z - m.SupportL.localPosition.z, -0.03f, 0.03f));
                    }
                }
                if (sel[4] > 0) AttachmentModels.LaserModule(m, mount.LaserPos, live);
            }
            catch (System.Exception e) { Debug.LogWarning("Attachments for " + def.Id + " failed: " + e.Message); }
        }

        /// <summary>Re-creates the local player's copy of this gun so a changed loadout takes effect (keeps the slot).</summary>
        public static void RefreshLive(string weaponId)
        {
            var p = PlayerController.Instance;
            if (p == null || p.Inventory == null) return;
            var inv = p.Inventory;
            var def = WeaponCatalog.Get(weaponId);
            if (def == null) return;
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                var w = inv.Slots[i];
                if (w == null || w.Def != def || !(w is Firearm)) continue;
                bool current = inv.CurrentIndex == i;
                inv.GiveCustom(Weapon.Create(def), i, current);
            }
        }
    }

    /// <summary>4x scope attachment: full-screen scope picture (same idea as the sniper's) and hiding the gun behind it.</summary>
    public static class AttachmentScope
    {
        static Texture2D mask;

        sealed class Hider : MonoBehaviour
        {
            public bool Hidden;
            public Renderer[] Renderers;
        }

        public static void SetHidden(WeaponModel model, bool hidden)
        {
            if (model == null) return;
            var h = model.GetComponent<Hider>();
            if (h == null) { if (!hidden) return; h = model.gameObject.AddComponent<Hider>(); }
            if (h.Hidden == hidden) return;
            h.Hidden = hidden;
            if (hidden)
            {
                // Remember only what is visible now (folded iron sights stay hidden afterwards).
                var list = new List<Renderer>();
                foreach (var r in model.GetComponentsInChildren<Renderer>(true)) if (r.enabled) { list.Add(r); r.enabled = false; }
                h.Renderers = list.ToArray();
            }
            else if (h.Renderers != null) foreach (var r in h.Renderers) if (r != null) r.enabled = true;
        }

        static Texture2D Mask()
        {
            if (mask != null) return mask;
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Scope4xMask" };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = r >= 0.97f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 0.97f, r)) * 0.85f;
                    px[y * N + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            mask = tex;
            return tex;
        }

        public static void Draw(float W, float H, float amount)
        {
            if (amount <= 0.01f) return;
            float a = Mathf.Clamp01(amount * 1.4f);
            var black = new Color(0f, 0f, 0f, a);
            float D = H * 0.92f, cx = W / 2f, cy = H / 2f;
            var circle = new Rect(cx - D / 2f, cy - D / 2f, D, D);
            UI.Tex(circle, Mask(), new Color(1f, 1f, 1f, a));
            UI.Fill(new Rect(-20f, -20f, circle.x + 21f, H + 40f), black);
            UI.Fill(new Rect(circle.xMax - 1f, -20f, W - circle.xMax + 21f, H + 40f), black);
            UI.Fill(new Rect(circle.x, -20f, D, circle.y + 21f), black);
            UI.Fill(new Rect(circle.x, circle.yMax - 1f, D, H - circle.yMax + 21f), black);
            // ACOG-style reticle: thin cross with a gap, chevron-ish centre and a red dot.
            float R = D / 2f;
            var ink = new Color(0f, 0f, 0f, 0.9f * a);
            UI.Fill(new Rect(cx - R, cy - 1f, R * 0.85f, 2f), ink);
            UI.Fill(new Rect(cx + R * 0.15f, cy - 1f, R * 0.85f, 2f), ink);
            UI.Fill(new Rect(cx - 1f, cy + R * 0.15f, 2f, R * 0.85f), ink);
            UI.Fill(new Rect(cx - 1f, cy - R, 2f, R * 0.7f), ink);
            for (int i = 1; i <= 4; i++) UI.Fill(new Rect(cx - 8f + i, cy + R * 0.15f + i * R * 0.12f, 16f - 2f * i, 2f), ink);
            UI.Fill(new Rect(cx - 3f, cy - 3f, 6f, 6f), new Color(1f, 0.15f, 0.1f, a));
        }
    }
}
