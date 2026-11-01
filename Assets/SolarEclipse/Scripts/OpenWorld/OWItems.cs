using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    public enum OWCat { Gun, Car, CarUpgrade, Top, Pants, Shoes, Jewelry, Glasses, Mask, Hat, WeaponSkin, Apartment, Emote }

    /// <summary>One thing you can buy in Open World.</summary>
    public sealed class OWItem
    {
        public string Id, Name, Desc;
        public OWCat Cat;
        public int Price;
        public Color Color = Color.white, Color2 = Color.white;
        public int Variant;          // mask / hat style, emote id, car body, upgrade kind...
        public string Weapon;        // guns: the weapon id
        public float Speed = 1f, Accel = 1f, Grip = 1f, Mass = 1f, Health = 1f; // cars
        public bool LongSleeves;
        public bool Starter;         // owned from the start
    }

    /// <summary>
    /// The Open World catalog: 100 items (12 cars, 10 car upgrades, 30 clothes, 15 masks and hats, 15 weapon skins,
    /// 10 apartment items, 8 emotes) plus the gun store. Ids are stable (saves use them).
    /// </summary>
    public static class OWItems
    {
        public static readonly List<OWItem> All = new List<OWItem>();
        static readonly Dictionary<string, OWItem> byId = new Dictionary<string, OWItem>();
        public static readonly string[] CatNames = { "Guns", "Cars", "Car upgrades", "Tops", "Pants", "Shoes", "Jewelry", "Glasses", "Masks", "Hats", "Weapon skins", "Apartment", "Emotes" };

        public static OWItem Get(string id) => id != null && byId.TryGetValue(id, out var it) ? it : null;
        public static List<OWItem> In(OWCat c) => All.FindAll(i => i.Cat == c);

        static OWItem Add(OWItem it) { All.Add(it); byId[it.Id] = it; return it; }
        static Color C(string hex) => MathX.Hex(hex);

        static void Gun(string id, string weapon, string name, int price, bool starter = false) =>
            Add(new OWItem { Id = id, Weapon = weapon, Name = name, Cat = OWCat.Gun, Price = price, Starter = starter, Desc = starter ? "Your free starter pistol. Unlimited ammo." : "Sold at Bullet Bros." });

        static void Car(string id, string name, int price, int body, string paint, float speed, float accel, float grip, float mass, float hp, string desc) =>
            Add(new OWItem { Id = id, Name = name, Cat = OWCat.Car, Price = price, Variant = body, Color = C(paint), Speed = speed, Accel = accel, Grip = grip, Mass = mass, Health = hp, Desc = desc });

        static void Wear(OWCat cat, string id, string name, int price, string col, string col2 = null, int variant = 0, bool sleeves = false, string desc = "") =>
            Add(new OWItem { Id = id, Name = name, Cat = cat, Price = price, Color = C(col), Color2 = col2 != null ? C(col2) : C(col), Variant = variant, LongSleeves = sleeves, Desc = desc });

        static OWItems()
        {
            // ---------------- Gun store (Bullet Bros.)
            Gun("g_glock", "glock", "Glock 17", 0, true);
            Gun("g_m1911", "m1911", "M1911", 1200);
            Gun("g_revolver", "revolver", "Revolver", 1800);
            Gun("g_deagle", "deagle", "Desert Eagle", 3500);
            Gun("g_uzi", "uzi", "UZI", 2500);
            Gun("g_mac11", "mac11", "MAC-11", 2800);
            Gun("g_mp5", "mp5", "MP5", 4200);
            Gun("g_vector", "vector", "KRISS Vector", 6000);
            Gun("g_ak47", "ak47", "AK-47", 7500);
            Gun("g_m4a1", "m4a1", "M4A1", 8000);
            Gun("g_scarh", "scarh", "SCAR-H", 9500);
            Gun("g_shotgun", "shotgun", "Pump Shotgun", 4000);
            Gun("g_dbarrel", "dbarrel", "Double Barrel", 3000);
            Gun("g_aa12", "aa12", "AA-12", 14000);
            Gun("g_sniper", "sniper", "Sniper Rifle", 12000);
            Gun("g_barrett", "barrett", "Barrett .50", 25000);
            Gun("g_m249", "m249", "M249 SAW", 22000);
            Gun("g_knife", "knife", "Knife", 300);
            Gun("g_katana", "katana", "Katana", 2000);
            Gun("g_bat", "bat", "Baseball Bat", 250);
            Gun("g_machete", "machete", "Machete", 900);
            Gun("g_chainsaw", "chainsaw", "Chainsaw", 8000);

            // ---------------- 12 cars (Eclipse Motors). body: 0 hatch 1 sedan 2 muscle 3 sports 4 suv 5 pickup 6 police 7 limo 8 super 9 van 10 monster 11 bike
            Car("c_hatch", "Puddle Hatchback", 4000, 0, "#7f8c8d", 0.8f, 0.8f, 1f, 0.8f, 0.8f, "Small, slow, cheap. Gets you there. Eventually.");
            Car("c_sedan", "Civic Sedan", 9000, 1, "#2c3e50", 0.9f, 0.9f, 1f, 1f, 1f, "A normal car for normal people.");
            Car("c_muscle", "Hellcat '69", 28000, 2, "#c0392b", 1.15f, 1.3f, 0.85f, 1.1f, 1f, "Loud V8. Burnouts included.");
            Car("c_sports", "Vortex GT", 45000, 3, "#f1c40f", 1.3f, 1.25f, 1.2f, 0.9f, 0.9f, "Low, fast and hungry for corners.");
            Car("c_suv", "Bulwark SUV", 22000, 4, "#111111", 0.95f, 0.95f, 1f, 1.5f, 1.4f, "Heavy and tough. Runs things over without noticing.");
            Car("c_pickup", "Ranchero Pickup", 15000, 5, "#8e5a2b", 0.95f, 1f, 0.95f, 1.3f, 1.2f, "Big bed, bigger bumper.");
            Car("c_police", "Interceptor", 38000, 6, "#ecf0f1", 1.2f, 1.2f, 1.1f, 1.2f, 1.3f, "A retired police cruiser. With the siren.");
            Car("c_limo", "Stretch Limo", 60000, 7, "#1a1a1a", 0.9f, 0.85f, 0.9f, 1.8f, 1.3f, "Arrive like a boss.");
            Car("c_super", "Eclipse X9", 150000, 8, "#8e44ad", 1.55f, 1.6f, 1.35f, 0.9f, 0.9f, "The fastest car in Port Eclipse.");
            Car("c_van", "Getaway Van", 12000, 9, "#dfe6e9", 0.85f, 0.85f, 0.9f, 1.4f, 1.3f, "Room for the whole crew and the loot.");
            Car("c_monster", "Mauler Monster Truck", 90000, 10, "#27ae60", 1f, 1.1f, 1.1f, 2.2f, 2f, "Huge wheels. Crushes cars and Goredolls.");
            Car("c_bike", "Viper Motorbike", 18000, 11, "#e67e22", 1.35f, 1.5f, 1.1f, 0.35f, 0.6f, "Two wheels, no doors, no fear.");
            // Content Update vehicles (also in the sandbox spawn menu)
            Car("c_kart", "Go-Kart", 6000, 12, "#e74c3c", 1.0f, 1.4f, 1.4f, 0.3f, 0.5f, "Tiny, twitchy, hilarious.");
            Car("c_dozer", "Bulldozer", 70000, 13, "#f39c12", 0.45f, 0.9f, 1.3f, 4f, 3f, "Slow. Unstoppable. Pushes cars and bodies out of the way with its blade.");
            Car("c_tank", "Tank", 400000, 14, "#4b5320", 0.6f, 1.0f, 1.4f, 6f, 6f, "Armoured tank with a cannon: LMB fires a high-explosive shell.");
            Car("c_cart", "Shopping Cart", 500, 15, "#bdc3c7", 0.55f, 0.7f, 0.8f, 0.15f, 0.3f, "Someone stuck a motor on a shopping cart. Why not.");

            // ---------------- 10 car upgrades (Eclipse Customs). variant: 0 paint, 1 neon, 2 nitro, 3 armour, 4 horn
            Wear(OWCat.CarUpgrade, "u_paint_red", "Paint: Candy Red", 1500, "#e3001b", null, 0, false, "Glossy red paint for your car.");
            Wear(OWCat.CarUpgrade, "u_paint_black", "Paint: Matte Black", 1500, "#151515", null, 0, false, "Murdered-out black.");
            Wear(OWCat.CarUpgrade, "u_paint_chrome", "Paint: Chrome", 6000, "#d0d6dc", null, 0, false, "Mirror chrome.");
            Wear(OWCat.CarUpgrade, "u_paint_gold", "Paint: Gold", 12000, "#e1b12c", null, 0, false, "Solid gold. Probably.");
            Wear(OWCat.CarUpgrade, "u_paint_green", "Paint: Toxic Green", 2500, "#39ff14", null, 0, false, "Glows in your enemies' nightmares.");
            Wear(OWCat.CarUpgrade, "u_neon_blue", "Neon Underglow: Blue", 4000, "#00a8ff", null, 1, false, "Blue lights under your car.");
            Wear(OWCat.CarUpgrade, "u_neon_pink", "Neon Underglow: Pink", 4000, "#ff2fa8", null, 1, false, "Pink lights under your car.");
            Wear(OWCat.CarUpgrade, "u_nitro", "Nitro Boost", 9000, "#00d2ff", null, 2, false, "Hold Shift (or SPRINT) for a big speed boost.");
            Wear(OWCat.CarUpgrade, "u_armor", "Armour Plating", 11000, "#555555", null, 3, false, "Your cars take half damage.");
            Wear(OWCat.CarUpgrade, "u_horn", "Air Horn", 800, "#ffffff", null, 4, false, "A horn they hear across the city.");

            // ---------------- 30 clothes (Threads + Eclipse Boutique)
            Wear(OWCat.Top, "t_hoodie_black", "Black Hoodie", 300, "#1b1b1b", null, 0, true);
            Wear(OWCat.Top, "t_hoodie_red", "Red Hoodie", 300, "#b71c1c", null, 0, true);
            Wear(OWCat.Top, "t_hoodie_grey", "Grey Hoodie", 300, "#7d7d7d", null, 0, true);
            Wear(OWCat.Top, "t_leather", "Leather Jacket", 1800, "#2b1d14", null, 0, true);
            Wear(OWCat.Top, "t_suit_black", "Black Suit", 4500, "#111216", "#f2f2f2", 0, true);
            Wear(OWCat.Top, "t_suit_white", "White Suit", 4500, "#f0f0f0", "#111111", 0, true);
            Wear(OWCat.Top, "t_track_blue", "Blue Tracksuit Top", 600, "#1e3799", null, 0, true);
            Wear(OWCat.Top, "t_track_red", "Red Tracksuit Top", 600, "#c23616", null, 0, true);
            Wear(OWCat.Top, "t_puffer", "Puffer Jacket", 1400, "#e58e26", null, 0, true);
            Wear(OWCat.Top, "t_tank", "White Tank Top", 150, "#f5f6fa", null, 0, false);
            Wear(OWCat.Top, "t_flannel", "Flannel Shirt", 450, "#8c1c13", null, 0, true);
            Wear(OWCat.Top, "t_gold_jacket", "Gold Jacket", 15000, "#d4af37", null, 0, true, "Pure flex.");
            Wear(OWCat.Pants, "p_jeans", "Blue Jeans", 250, "#273c75");
            Wear(OWCat.Pants, "p_slacks", "Black Slacks", 800, "#151515");
            Wear(OWCat.Pants, "p_cargo", "Cargo Pants", 400, "#6b6b47");
            Wear(OWCat.Pants, "p_track", "Track Pants", 350, "#192a56");
            Wear(OWCat.Pants, "p_camo", "Camo Pants", 600, "#4b5320");
            Wear(OWCat.Pants, "p_white", "White Trousers", 1200, "#ecf0f1");
            Wear(OWCat.Shoes, "s_sneakers", "White Sneakers", 300, "#f5f5f5");
            Wear(OWCat.Shoes, "s_boots", "Combat Boots", 700, "#2d2a26");
            Wear(OWCat.Shoes, "s_gold", "Gold Sneakers", 9000, "#d4af37");
            Wear(OWCat.Shoes, "s_loafers", "Leather Loafers", 2200, "#5a3a22");
            Wear(OWCat.Jewelry, "j_chain_gold", "Gold Chain", 6000, "#f1c40f", null, 0, false, "Heavy gold chain.");
            Wear(OWCat.Jewelry, "j_chain_diamond", "Diamond Chain", 40000, "#dff9fb", "#a5f3ff", 1, false, "Iced out.");
            Wear(OWCat.Jewelry, "j_watch_gold", "Gold Watch", 8000, "#f1c40f", null, 2, false, "Tells the time. In gold.");
            Wear(OWCat.Jewelry, "j_watch_diamond", "Diamond Watch", 30000, "#dff9fb", null, 3, false, "Tells the time. In diamonds.");
            Wear(OWCat.Jewelry, "j_grill", "Gold Grill", 12000, "#f1c40f", null, 4, false, "Gold teeth.");
            Wear(OWCat.Glasses, "e_aviators", "Aviators", 900, "#c8a24a", "#2d3436");
            Wear(OWCat.Glasses, "e_shades", "Black Shades", 500, "#0b0b0b", "#0b0b0b");
            Wear(OWCat.Glasses, "e_gold", "Gold Frames", 7000, "#f1c40f", "#6c5ce7");

            // ---------------- 15 masks and hats (Mask Mart + Boutique). Masks: variant = style. Hats: variant = Hats index.
            Wear(OWCat.Mask, "m_skull", "Skull Mask", 1500, "#f5f6fa", "#111111", 0);
            Wear(OWCat.Mask, "m_clown", "Clown Mask", 1500, "#ffffff", "#e74c3c", 1);
            Wear(OWCat.Mask, "m_hockey", "Hockey Mask", 1200, "#f0f0e0", "#8a0303", 2);
            Wear(OWCat.Mask, "m_pig", "Pig Mask", 1000, "#ffb6c1", "#d1789a", 3);
            Wear(OWCat.Mask, "m_ski", "Ski Mask", 500, "#151515", "#d63031", 4);
            Wear(OWCat.Mask, "m_gas", "Gas Mask", 2500, "#3d3d3d", "#16a085", 5);
            Wear(OWCat.Mask, "m_bandana", "Bandana", 300, "#c0392b", "#ffffff", 6);
            Wear(OWCat.Mask, "m_robot", "Robot Mask", 5000, "#b2bec3", "#00d2ff", 7);
            Wear(OWCat.Hat, "h_gold_cap", "Designer Cap", 3000, "#d4af37", null, 1);
            Wear(OWCat.Hat, "h_crown", "Crown", 50000, "#f1c40f", null, 8);
            Wear(OWCat.Hat, "h_tophat", "Top Hat", 4000, "#111111", null, 5);
            Wear(OWCat.Hat, "h_cowboy", "Cowboy Hat", 1800, "#8b5a2b", null, 4);
            Wear(OWCat.Hat, "h_beanie", "Beanie", 200, "#2c3e50", null, 3);
            Wear(OWCat.Hat, "h_bucket", "Bucket Hat", 600, "#f39c12", null, 11);
            Wear(OWCat.Hat, "h_viking", "Viking Helmet", 7000, "#95a5a6", null, 15);

            // ---------------- 15 weapon skins (Bullet Bros. back room)
            Wear(OWCat.WeaponSkin, "w_gold", "Gold", 20000, "#d4af37", null, 0);
            Wear(OWCat.WeaponSkin, "w_chrome", "Chrome", 9000, "#cfd8dc", null, 0);
            Wear(OWCat.WeaponSkin, "w_blackops", "Black Ops", 2500, "#121212", null, 0);
            Wear(OWCat.WeaponSkin, "w_arctic", "Arctic Camo", 3000, "#e8eef2", "#9fb4c7", 1);
            Wear(OWCat.WeaponSkin, "w_woodland", "Woodland Camo", 3000, "#4b5320", "#2e2a1e", 1);
            Wear(OWCat.WeaponSkin, "w_desert", "Desert Camo", 3000, "#c2a878", "#8b6b3e", 1);
            Wear(OWCat.WeaponSkin, "w_neon_pink", "Neon Pink", 6000, "#ff2fa8", null, 2);
            Wear(OWCat.WeaponSkin, "w_neon_blue", "Neon Blue", 6000, "#00a8ff", null, 2);
            Wear(OWCat.WeaponSkin, "w_diamond", "Diamond", 60000, "#e0ffff", "#9ef0ff", 1);
            Wear(OWCat.WeaponSkin, "w_blood", "Blood Red", 7000, "#7a0000", "#b30000", 1);
            Wear(OWCat.WeaponSkin, "w_rosegold", "Rose Gold", 15000, "#b76e79", null, 0);
            Wear(OWCat.WeaponSkin, "w_carbon", "Carbon Fibre", 5000, "#2b2b2b", "#3c3c3c", 1);
            Wear(OWCat.WeaponSkin, "w_toxic", "Toxic", 6000, "#39ff14", null, 2);
            Wear(OWCat.WeaponSkin, "w_galaxy", "Galaxy", 25000, "#2e1a47", "#6c5ce7", 1);
            Wear(OWCat.WeaponSkin, "w_bone", "Bone", 8000, "#e6dcc3", "#bfae8a", 1);

            // ---------------- 10 apartment items (The Meridian)
            Wear(OWCat.Apartment, "a_apartment", "Meridian Penthouse", 25000, "#ffffff", null, 0, false, "Your own penthouse: locker, garage and room for trophies.");
            Wear(OWCat.Apartment, "a_gunwall", "Gun Wall", 8000, "#5a5a5a", null, 1, false, "Every gun you own on display.");
            Wear(OWCat.Apartment, "a_trophy", "Trophy Case", 5000, "#d4af37", null, 2, false, "Shows off your heist record.");
            Wear(OWCat.Apartment, "a_neon", "Neon Sign", 2500, "#ff2fa8", null, 3, false, "Pink neon on the wall.");
            Wear(OWCat.Apartment, "a_pool", "Pool Table", 6000, "#0b6623", null, 4, false, "For celebrating heists.");
            Wear(OWCat.Apartment, "a_tv", "Giant TV", 4500, "#111111", null, 5, false, "Ninety inches of TV.");
            Wear(OWCat.Apartment, "a_statue", "Gold Statue", 30000, "#f1c40f", null, 6, false, "A golden statue of you. Sort of.");
            Wear(OWCat.Apartment, "a_aquarium", "Shark Aquarium", 20000, "#0984e3", null, 7, false, "Glowing blue tank.");
            Wear(OWCat.Apartment, "a_sofa", "Leather Sofa", 3500, "#3b2416", null, 8, false, "Comfy.");
            Wear(OWCat.Apartment, "a_cash", "Pile of Cash", 50000, "#2ecc71", null, 9, false, "A big pile of money. Literally.");


            // ---------------- Content Update: 51 more items
            Wear(OWCat.Top, "t_hawaiian", "Hawaiian Shirt", 400, "#ff7f50", null, 0, false, "Vacation mode.");
            Wear(OWCat.Top, "t_varsity", "Varsity Jacket", 1600, "#8e1b1b", "#f5f5f5", 0, true);
            Wear(OWCat.Top, "t_camo", "Camo Jacket", 900, "#4b5320", null, 0, true);
            Wear(OWCat.Top, "t_trench", "Trench Coat", 3200, "#5c4a32", null, 0, true, "Detective vibes.");
            Wear(OWCat.Top, "t_neon_hoodie", "Neon Hoodie", 2500, "#39ff14", null, 0, true);
            Wear(OWCat.Top, "t_tactical", "Tactical Vest", 3500, "#2d3436", null, 0, true, "Looks bulletproof. Isn't.");
            Wear(OWCat.Top, "t_tux_red", "Red Tuxedo", 8000, "#8b0000", "#111111", 0, true);
            Wear(OWCat.Top, "t_jersey", "Eclipse FC Jersey", 700, "#6c5ce7", "#fdcb6e", 0, false);
            Wear(OWCat.Top, "t_labcoat", "Lab Coat", 1200, "#f5f6fa", null, 0, true);
            Wear(OWCat.Top, "t_diamond_jacket", "Diamond Jacket", 75000, "#dff9fb", "#a5f3ff", 0, true, "Covered in crystals. Absurd.");
            Wear(OWCat.Pants, "p_leather", "Leather Pants", 1800, "#1e1e1e");
            Wear(OWCat.Pants, "p_gold", "Gold Pants", 12000, "#d4af37");
            Wear(OWCat.Pants, "p_tactical", "Tactical Pants", 900, "#3d3d29");
            Wear(OWCat.Pants, "p_red", "Red Trousers", 700, "#a4161a");
            Wear(OWCat.Shoes, "s_red", "Red Sneakers", 600, "#d63031");
            Wear(OWCat.Shoes, "s_neon", "Neon Runners", 1500, "#00ff9d");
            Wear(OWCat.Shoes, "s_diamond", "Diamond Kicks", 45000, "#dff9fb");
            Wear(OWCat.Jewelry, "j_chain_platinum", "Platinum Chain", 22000, "#e5e4e2", null, 0, false, "Cold and heavy.");
            Wear(OWCat.Jewelry, "j_watch_platinum", "Platinum Watch", 18000, "#e5e4e2", null, 2, false);
            Wear(OWCat.Jewelry, "j_grill_diamond", "Diamond Grill", 35000, "#dff9fb", null, 4, false, "Blinding smile.");
            Wear(OWCat.Glasses, "e_neon", "Neon Shades", 2200, "#ff2fa8", "#00a8ff");
            Wear(OWCat.Glasses, "e_round", "Round Specs", 400, "#2d3436", "#dfe6e9");
            Wear(OWCat.Glasses, "e_visor", "Cyber Visor", 9000, "#00d2ff", "#00d2ff");
            Wear(OWCat.Mask, "m_skull_gold", "Gold Skull", 18000, "#d4af37", "#111111", 0);
            Wear(OWCat.Mask, "m_clown_evil", "Evil Clown", 3000, "#dfe6e9", "#2d3436", 1);
            Wear(OWCat.Mask, "m_hockey_blood", "Bloody Hockey Mask", 2500, "#f0f0e0", "#b30000", 2);
            Wear(OWCat.Mask, "m_ski_white", "White Ski Mask", 600, "#f5f6fa", "#2d3436", 4);
            Wear(OWCat.Mask, "m_hazmat", "Hazmat Mask", 3500, "#f1c40f", "#2d3436", 5);
            Wear(OWCat.Mask, "m_robot_gold", "Gold Robot", 15000, "#d4af37", "#ff2fa8", 7);
            Wear(OWCat.Hat, "h_party", "Party Hat", 300, "#e84393", null, 9);
            Wear(OWCat.Hat, "h_halo", "Halo", 20000, "#ffeaa7", null, 12);
            Wear(OWCat.Hat, "h_horns", "Devil Horns", 6000, "#c0392b", null, 13);
            Wear(OWCat.Hat, "h_army", "Army Helmet", 1500, "#4b5320", null, 7);
            Wear(OWCat.Hat, "h_headband", "Headband", 250, "#d63031", null, 10);
            Wear(OWCat.WeaponSkin, "w_lava", "Lava", 12000, "#ff4500", null, 2);
            Wear(OWCat.WeaponSkin, "w_ice", "Ice", 9000, "#a5f3ff", "#ffffff", 1);
            Wear(OWCat.WeaponSkin, "w_tiger", "Tiger", 7000, "#f39c12", "#111111", 1);
            Wear(OWCat.WeaponSkin, "w_zebra", "Zebra", 5000, "#f5f6fa", "#111111", 1);
            Wear(OWCat.WeaponSkin, "w_money", "Money", 30000, "#27ae60", "#a3e4a1", 1);
            Wear(OWCat.WeaponSkin, "w_plasma", "Plasma", 14000, "#9b59ff", null, 2);
            Wear(OWCat.WeaponSkin, "w_obsidian", "Obsidian", 11000, "#0b0b0f", "#2e1a47", 1);
            Wear(OWCat.WeaponSkin, "w_candy", "Candy", 6000, "#ff9ff3", "#48dbfb", 1);
            Wear(OWCat.Apartment, "a_cinema", "Home Cinema", 15000, "#111111", null, 10, false, "A projector wall and a row of seats.");
            Wear(OWCat.Apartment, "a_bar", "Home Bar", 12000, "#5a3a22", null, 11, false, "Bottles, stools and neon.");
            Wear(OWCat.Apartment, "a_goldbath", "Gold Bathtub", 40000, "#f1c40f", null, 12, false, "A solid gold bathtub. Obviously.");
            Wear(OWCat.Apartment, "a_dj", "DJ Booth", 9000, "#6c5ce7", null, 13, false, "Decks, speakers and lights.");
            Wear(OWCat.Apartment, "a_safe", "Gold Safe", 25000, "#d4af37", null, 14, false, "A walk-in safe full of gold bars.");
            // ---------------- 12 emotes (phone > EMOTES). variant = emote id
            string[] emotes = { "Wave", "Dance", "Salute", "Flex", "Clap", "Point", "Taunt", "Money Rain", "Bow", "Cheer", "Facepalm", "Robot" };
            int[] emotePrices = { 0, 1500, 500, 800, 500, 300, 1200, 25000, 400, 600, 300, 2000 };
            for (int i = 0; i < emotes.Length; i++)
                Add(new OWItem { Id = "x_" + emotes[i].ToLowerInvariant().Replace(" ", ""), Name = emotes[i], Cat = OWCat.Emote, Price = emotePrices[i], Variant = i, Starter = i == 0, Desc = i == 7 ? "Throw money in the air. Everyone sees it." : "Everyone around you sees it." });
        }

        /// <summary>Number of items in the 100 (everything but the gun store).</summary>
        public static int CollectionCount => All.FindAll(i => i.Cat != OWCat.Gun).Count;
    }
}
