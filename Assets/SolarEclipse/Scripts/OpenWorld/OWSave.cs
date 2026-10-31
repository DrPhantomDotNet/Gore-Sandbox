using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    [Serializable]
    public sealed class OWSkin { public string weapon = "", skin = ""; }

    [Serializable]
    public sealed class OWSaveData
    {
        public int version = 1;
        public long cash = 1500;
        public List<string> owned = new List<string>();
        public string top = "", pants = "", shoes = "", jewelry = "", glasses = "", mask = "", hat = "";
        public string car = "";                       // car the phone calls
        public string paint = "", neon = "";          // applied car upgrades (ids)
        public List<OWSkin> skins = new List<OWSkin>();
        public string gun = "g_glock";                // gun in hand
        public string emote = "x_wave";
        public int heists, jobs, races;
        public long totalEarned;
        public long hourStart; public long earnedThisHour;
    }

    /// <summary>
    /// Your Open World money and stuff, saved on this device. Cash only comes from finished heists and jobs (with a
    /// cap per payout and per hour, so a modified client can't hand itself millions in one go).
    /// </summary>
    public static class OWSave
    {
        const string Key = "openworld_save_v1";
        public const long MaxPayout = 150000, MaxPerHour = 600000;
        static OWSaveData data;
        public static event Action Changed;

        public static OWSaveData D
        {
            get
            {
                if (data != null) return data;
                try { var json = PlayerPrefs.GetString(Key, ""); if (!string.IsNullOrEmpty(json)) data = JsonUtility.FromJson<OWSaveData>(json); } catch (Exception) { }
                if (data == null) data = new OWSaveData();
                foreach (var it in OWItems.All) if (it.Starter && !data.owned.Contains(it.Id)) data.owned.Add(it.Id);
                if (data.cash < 0) data.cash = 0;
                return data;
            }
        }

        public static long Cash => D.cash;
        public static bool Owns(string id) => D.owned.Contains(id);

        public static void Save()
        {
            try { PlayerPrefs.SetString(Key, JsonUtility.ToJson(D)); PlayerPrefs.Save(); } catch (Exception e) { Debug.LogWarning("[OpenWorld] save: " + e.Message); }
            Changed?.Invoke();
        }

        /// <summary>Adds earned money (heist / job / race). Returns what was actually paid after the limits.</summary>
        public static long Earn(long amount, string reason)
        {
            if (amount <= 0) return 0;
            var d = D;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now - d.hourStart > 3600) { d.hourStart = now; d.earnedThisHour = 0; }
            amount = Math.Min(amount, MaxPayout);
            amount = Math.Min(amount, Math.Max(0, MaxPerHour - d.earnedThisHour));
            if (amount <= 0) { GameEvents.ShowToast("Payout limit reached for this hour."); return 0; }
            d.cash += amount; d.totalEarned += amount; d.earnedThisHour += amount;
            Save();
            OWHud.Money(amount, reason);
            AudioManager.Ui("ui_success");
            return amount;
        }

        public static bool Spend(long amount)
        {
            if (amount < 0 || D.cash < amount) return false;
            D.cash -= amount;
            Save();
            return true;
        }

        /// <summary>Hospital bill / lost cash on death (small).</summary>
        public static void Lose(long amount)
        {
            amount = Math.Min(amount, D.cash);
            if (amount <= 0) return;
            D.cash -= amount;
            Save();
        }

        public static bool Buy(OWItem it)
        {
            if (it == null || Owns(it.Id)) return false;
            if (!Spend(it.Price)) { AudioManager.Ui("ui_error"); GameEvents.ShowToast("Not enough cash."); return false; }
            D.owned.Add(it.Id);
            Save();
            AudioManager.Ui("ui_success");
            GameEvents.ShowToast("Bought: " + it.Name);
            return true;
        }

        // ------------------------------------------------------------------ equipping

        public static string Equipped(OWCat c)
        {
            var d = D;
            switch (c)
            {
                case OWCat.Top: return d.top;
                case OWCat.Pants: return d.pants;
                case OWCat.Shoes: return d.shoes;
                case OWCat.Jewelry: return d.jewelry;
                case OWCat.Glasses: return d.glasses;
                case OWCat.Mask: return d.mask;
                case OWCat.Hat: return d.hat;
                case OWCat.Car: return d.car;
                case OWCat.Emote: return d.emote;
                case OWCat.Gun: return d.gun;
                default: return "";
            }
        }

        /// <summary>Wear / select an owned item (or "" to take it off).</summary>
        public static void Equip(OWCat c, string id)
        {
            if (!string.IsNullOrEmpty(id) && !Owns(id)) return;
            var d = D;
            switch (c)
            {
                case OWCat.Top: d.top = id; break;
                case OWCat.Pants: d.pants = id; break;
                case OWCat.Shoes: d.shoes = id; break;
                case OWCat.Jewelry: d.jewelry = id; break;
                case OWCat.Glasses: d.glasses = id; break;
                case OWCat.Mask: d.mask = id; break;
                case OWCat.Hat: d.hat = id; break;
                case OWCat.Car: d.car = id; break;
                case OWCat.Emote: d.emote = id; break;
                case OWCat.Gun: d.gun = id; break;
                case OWCat.CarUpgrade:
                {
                    var it = OWItems.Get(id);
                    if (it != null && it.Variant == 0) d.paint = id;
                    else if (it != null && it.Variant == 1) d.neon = id;
                    break;
                }
            }
            Save();
            if (c != OWCat.Gun && c != OWCat.Car && c != OWCat.Emote && c != OWCat.CarUpgrade) PushOutfit();
        }

        public static string SkinFor(string weaponId)
        {
            var s = D.skins.Find(x => x.weapon == weaponId);
            return s != null ? s.skin : "";
        }

        public static void SetSkin(string weaponId, string skinId)
        {
            if (!string.IsNullOrEmpty(skinId) && !Owns(skinId)) return;
            D.skins.RemoveAll(x => x.weapon == weaponId);
            if (!string.IsNullOrEmpty(skinId)) D.skins.Add(new OWSkin { weapon = weaponId, skin = skinId });
            Save();
        }

        /// <summary>Outfit code shared with other players (ProfileData.Outfit).</summary>
        public static string OutfitCode
        {
            get
            {
                var d = D;
                return string.Join(";", new[] { d.top, d.pants, d.shoes, d.jewelry, d.glasses, d.mask, d.hat });
            }
        }

        static void PushOutfit()
        {
            var p = PlayerProfile.Local;
            if (p == null) return;
            p.Outfit = OutfitCode;
            p.Changed();
            PlayerProfile.Save();
        }

        /// <summary>Car upgrades owned (nitro / armour / horn).</summary>
        public static bool HasUpgrade(int variant) => OWItems.In(OWCat.CarUpgrade).Exists(u => u.Variant == variant && Owns(u.Id));
    }
}
