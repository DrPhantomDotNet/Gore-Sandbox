using System.Collections.Generic;
using SolarEclipse.Net;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Runs Open World on Port Eclipse (added to the map root by PortEclipseMap). On every machine it sets up your
    /// loadout (fists + the gun you picked, the starter Glock never runs dry), the keys (hold G: guns, P: phone,
    /// J: emote, M: map), driving, the penthouse, the hospital bill and the HUD. On the host (or in singleplayer) it also
    /// runs the world: parked cars, people on the sidewalks, police and wanted stars, heists (OWHeists) and bounties.
    /// Clients ask the host for everything that changes the world (OWNet).
    /// </summary>
    /// <summary>Interactables that act on the player's own machine (shops, doors, cars ask the host themselves).</summary>
    public interface ILocalInteractable { }

    public sealed class OWDirector : MonoBehaviour
    {
        public static OWDirector I { get; private set; }
        public static bool Active => I != null;
        public static byte MyId => NetState.Active && NetManager.I != null ? NetManager.I.LocalId : (byte)0;
        static bool Host => !NetState.IsClient;
        static bool Mobile => Application.isMobilePlatform || Settings.TouchControlsActive;

        /// <summary>Creative players (host or granted) keep noclip and the Spawn Gun.</summary>
        public static bool Creative => NetState.Active && NetManager.I != null && (NetState.IsHost || NetManager.I.LocalCreative);
        public static bool AllowNoclip => !Active || (Creative && VoiceChat.PttKey != KeyCode.V); // V is push-to-talk here

        // ------------------------------------------------------------------ shared state (host decides, clients mirror)

        public static readonly Dictionary<byte, int> Wanted = new Dictionary<byte, int>();
        public static bool BountyActive;
        public static Vector3 BountyPos;
        public static int MyWanted => Wanted.TryGetValue(MyId, out var w) ? w : 0;

        /// <summary>Your map pin (phone map / minimap). Null when none.</summary>
        public static Vector3? Waypoint;

        enum Role { Civ, Cop, Guard, Gang, Boss }
        static readonly Dictionary<Goredoll, Role> roles = new Dictionary<Goredoll, Role>();
        static readonly Dictionary<Goredoll, float> diedAt = new Dictionary<Goredoll, float>();
        static readonly Dictionary<byte, float> lastCrime = new Dictionary<byte, float>();
        static readonly Dictionary<byte, OWCar> called = new Dictionary<byte, OWCar>();
        static readonly Dictionary<byte, float> lastText = new Dictionary<byte, float>();
        static readonly List<(Vector3 pos, OWCar car, float freeAt)> parked = new List<(Vector3, OWCar, float)>();
        static Goredoll bountyBoss;
        static byte bountyBy = 255;
        static float bountyUntil;

        PlayerController setupFor;
        float gHeld = -1f, nextState, nextSpawn, nextPolice, nextCleanup, nextInput;
        bool wasDead;
        Weapon skinned;
        readonly HashSet<byte> hiddenDrivers = new HashSet<byte>();

        static readonly string[] ParkedModels = { "c_hatch", "c_sedan", "c_sedan", "c_suv", "c_pickup", "c_van", "c_muscle", "c_hatch", "c_bike", "c_sports" };
        static readonly string[] ParkedPaints = { "", "", "u_paint_red", "u_paint_black", "", "u_paint_green", "" };

        // ------------------------------------------------------------------ lifecycle

        void Awake()
        {
            I = this;
            Wanted.Clear(); roles.Clear(); diedAt.Clear(); lastCrime.Clear(); called.Clear(); parked.Clear();
            BountyActive = false; bountyBoss = null; Waypoint = null;
            OWHeists.Reset();
            OWJobs.Reset();
            OWPhone.Reset();
            UIRoot.HudLayer += OWHud.Draw;
            Goredoll.Died += OnDollDied;
            ArmedBrain.PlayerFilter = MayTarget;
            EnsureAchievements();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            UIRoot.HudLayer -= OWHud.Draw;
            Goredoll.Died -= OnDollDied;
            if (ArmedBrain.PlayerFilter == (System.Func<Goredoll, IPlayerTarget, bool>)MayTarget) ArmedBrain.PlayerFilter = null;
            OWCar.LocalDriving = null;
            OWHeists.Reset();
            OWJobs.Reset();
            OWPhone.Reset();
            Wanted.Clear();
            OWApartment.Clear();
        }

        void Start()
        {
            if (!Host) return;
            // Big Marcus hangs out by the heist board.
            var marcus = GoredollFactory.Spawn(OWMap.Place("Marcus"), 180f, "Passive");
            if (marcus != null) marcus.DisplayName = "Big Marcus";
            // Parked cars: real, drivable and networked.
            int max = Mobile ? 10 : 16;
            var rnd = new System.Random(4242);
            for (int i = 0; i < OWMap.CarSpawns.Count && parked.Count < max; i++)
            {
                var pos = OWMap.CarSpawns[i];
                parked.Add((pos, SpawnParked(pos, rnd), 0f));
            }
        }

        static OWCar SpawnParked(Vector3 pos, System.Random rnd)
        {
            string model = ParkedModels[rnd.Next(ParkedModels.Length)];
            string paint = ParkedPaints[rnd.Next(ParkedPaints.Length)];
            var rb = PropFactory.Spawn(OWCar.DefFor(model, paint, "", false, false, false), pos, Quaternion.Euler(0, rnd.Next(2) == 0 ? 0f : 180f, 0));
            return rb != null ? rb.GetComponent<OWCar>() : null;
        }

        // ------------------------------------------------------------------ achievements

        static bool achReady;
        public const string StatHeists = "ow_heists", StatSpent = "ow_spent", StatRaces = "ow_races", StatStars = "ow_stars", StatBank = "ow_bank", StatItems = "ow_items";

        public static void EnsureAchievements()
        {
            if (achReady) return;
            achReady = true;
            void Add(string id, string name, string desc, string stat, float target)
            {
                if (!Stats.Achievements.Exists(a => a.Id == id)) Stats.Achievements.Add(new Achievement(id, name, desc, stat, target));
            }
            Add("ow_first_heist", "First Score", "Finish a heist in Port Eclipse.", StatHeists, 1);
            Add("ow_crew", "Career Criminal", "Finish 10 heists.", StatHeists, 10);
            Add("ow_bank", "The Big One", "Rob the Central Bank.", StatBank, 1);
            Add("ow_spender", "Big Spender", "Spend $250,000 in Port Eclipse.", StatSpent, 250000);
            Add("ow_collector", "Collector", "Own 50 Open World items.", StatItems, 50);
            Add("ow_racer", "Getaway Driver", "Win a street race.", StatRaces, 1);
            Add("ow_wanted", "Most Wanted", "Reach 5 wanted stars.", StatStars, 5);
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing) return;
            var p = PlayerController.Instance;
            if (p != null)
            {
                if (setupFor != p)
                {
                    bool first = setupFor == null;
                    setupFor = p; SetupLoadout();
                    if (first) Toasts.Show("PORT ECLIPSE", Settings.TouchControlsActive
                        ? "GUNS: your guns · PHONE: map, jobs, calls, texts. Heist board: Southside. Shops: Downtown."
                        : "Hold G: guns · P: phone · M: map · J: emote · " + VoiceChat.PttKey + ": talk. Heist board: Southside. Shops: Downtown.", new Color(1f, 0.78f, 0.16f));
                }
                LocalTick(p);
            }
            if (Host) HostTick();
        }

        void LocalTick(PlayerController p)
        {
            if (p.Noclip && !Creative) p.SetNoclip(false);

            // Death: leave the car, pay the hospital.
            if (p.IsDead && !wasDead)
            {
                if (OWCar.LocalDriving != null) LeaveCar();
                long bill = System.Math.Min(1000, OWSave.Cash / 20 + 100);
                if (OWSave.Cash > 0) { OWSave.Lose(bill); GameEvents.ShowToast("Port Eclipse General: hospital bill $" + bill.ToString("N0")); }
                OWJobs.Fail("You died.");
            }
            wasDead = p.IsDead;

            // Gun skins on whatever you hold.
            var cur = p.Inventory != null ? p.Inventory.Current : null;
            if (cur != skinned) { skinned = cur; ApplySkin(cur); }

            bool free = !UIRoot.AnyBlocking && !UIRoot.DevPanelCapturing && !DevPanel.Open && !p.IsDead;
            if (free && !Settings.TouchControlsActive)
            {
                if (Input.GetKeyDown(KeyCode.G)) gHeld = Time.unscaledTime;
                if (gHeld >= 0f && Input.GetKey(KeyCode.G) && Time.unscaledTime - gHeld > 0.2f && OWCar.LocalDriving == null) { gHeld = -1f; UIRoot.Push(new GunMenuScreen(true)); }
                if (Input.GetKeyDown(KeyCode.J)) PlayEmote(OWSave.D.emote);
                if (Input.GetKeyDown(KeyCode.M)) PhoneScreen.Open(PhoneScreen.App.Map);
            }
            if (!Input.GetKey(KeyCode.G)) gHeld = -1f;

            if (MyWanted >= 5) Stats.UnlockById("ow_wanted");
            OWJobs.Tick(p);
            OWPhone.Tick();
            OWApartment.Tick();

            // Hide other players' bodies while they drive (they sit inside the car).
            if (NetState.Active && NetManager.I != null)
            {
                foreach (var np in NetManager.I.Players.Values)
                {
                    if (np.Avatar == null) continue;
                    bool driving = false;
                    foreach (var c in OWCar.All) if (c != null && c.DriverId == np.Id) { driving = true; break; }
                    np.Avatar.HiddenInCar = driving;
                }
            }
        }

        // ------------------------------------------------------------------ loadout, guns, skins, emotes

        /// <summary>Fists in slot 1, your chosen gun in slot 2 (Creative players also keep the Spawn Gun).</summary>
        public static void SetupLoadout()
        {
            var p = PlayerController.Instance;
            if (p == null || p.Inventory == null) return;
            var inv = p.Inventory;
            inv.Strip();
            inv.Give("fists", 0, false);
            GiveGun(OWSave.D.gun, false);
            if (Creative) inv.Give("spawngun", 2, false);
            inv.Select(inv.Slots[1] != null ? 1 : 0, true);
        }

        /// <summary>Puts a gun you own in slot 2 and (optionally) takes it out.</summary>
        public static void GiveGun(string itemId, bool equip)
        {
            var p = PlayerController.Instance;
            if (p == null) return;
            var it = OWItems.Get(itemId);
            if (it == null || it.Cat != OWCat.Gun || !OWSave.Owns(it.Id)) it = OWItems.Get("g_glock");
            OWSave.D.gun = it.Id;
            var inv = p.Inventory;
            if (inv.Slots[1] != null) inv.Remove(1);
            int idx = inv.Give(it.Weapon, 1, false);
            if (idx >= 0 && inv.Slots[idx] is Firearm fa) fa.FreeAmmo = it.Starter;
            if (equip && idx >= 0) inv.Select(idx, true);
            if (I != null) I.skinned = null;
        }

        static void ApplySkin(Weapon w)
        {
            if (w == null || w.Model == null || w.Def == null) return;
            var skin = OWItems.Get(OWSave.SkinFor(w.Def.Id));
            if (skin == null) return;
            int i = 0;
            foreach (var r in w.Model.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (w.Model.GlowRenderers.Contains(r)) continue;
                var c = skin.Variant == 1 && (i++ % 2 == 1) ? skin.Color2 : skin.Color;
                if (skin.Variant == 2) r.sharedMaterial = Mats.Glow(c, 1.3f);
                else r.material.color = c;
            }
        }

        public static void PlayEmote(string id)
        {
            var it = OWItems.Get(id);
            if (it == null || it.Cat != OWCat.Emote || !OWSave.Owns(it.Id)) return;
            OWSave.D.emote = it.Id;
            NetManager.I.OWRequest(OWReq.Emote, "", it.Variant);
        }

        // ------------------------------------------------------------------ interact points

        public static void Use(OWPoint pt)
        {
            if (pt == null) return;
            var p = PlayerController.Instance;
            switch (pt.Kind)
            {
                case "guns": UIRoot.Push(new ShopScreen("BULLET BROS.", OWCat.Gun, OWCat.WeaponSkin)); break;
                case "clothes": UIRoot.Push(new ShopScreen("THREADS", OWCat.Top, OWCat.Pants, OWCat.Shoes)); break;
                case "masks": UIRoot.Push(new ShopScreen("MASK MART", OWCat.Mask, OWCat.Hat)); break;
                case "boutique": UIRoot.Push(new ShopScreen("ECLIPSE BOUTIQUE", OWCat.Jewelry, OWCat.Glasses, OWCat.Emote)); break;
                case "cars": UIRoot.Push(new ShopScreen("ECLIPSE MOTORS", OWCat.Car)); break;
                case "customs": UIRoot.Push(new ShopScreen("ECLIPSE CUSTOMS", OWCat.CarUpgrade)); break;
                case "heists": UIRoot.Push(new HeistBoardScreen()); break;
                case "apartment":
                    if (OWSave.Owns("a_apartment")) EnterApartment();
                    else UIRoot.Push(new ShopScreen("THE MERIDIAN", OWCat.Apartment));
                    break;
                case "exit": if (p != null) p.Teleport(OWMap.ApartmentOutside, 180f); AudioManager.Ui("ui_open"); break;
                case "locker": UIRoot.Push(new LockerScreen()); break;
                case "garage": UIRoot.Push(new GarageScreen()); break;
                case "race": GameEvents.ShowToast(OWCar.LocalDriving != null ? "Stop on the checkered line to start." : "Bring a car and stop on the checkered line to start a street race."); break;
            }
        }

        public static void EnterApartment()
        {
            var p = PlayerController.Instance;
            if (p == null) return;
            if (OWCar.LocalDriving != null) LeaveCar();
            p.Teleport(OWMap.ApartmentInside, 0f);
            OWApartment.Refresh();
            AudioManager.Ui("ui_open");
            GameEvents.ShowToast("Your penthouse at the Meridian. Locker: outfits. Garage: your cars.");
        }

        // ------------------------------------------------------------------ cars (local side)

        public static void RequestDrive(OWCar car)
        {
            if (car == null || car.Wrecked) return;
            if (NetState.IsClient)
            {
                var ne = car.GetComponent<NetEntity>();
                if (ne != null) NetManager.I.OWRequest(OWReq.Drive, "", 0, default, ne.Id);
            }
            else
            {
                var req = NetManager.I != null ? NetManager.I.CurrentRequester : null;
                HostDrive(req != null ? req.Id : MyId, car);
            }
        }

        public static void LeaveCar()
        {
            var car = OWCar.LocalDriving;
            if ((object)car == null) return;
            ExitLocal(car);
            if (car == null) return;
            if (NetState.IsClient)
            {
                var ne = car.GetComponent<NetEntity>();
                if (ne != null) NetManager.I.OWRequest(OWReq.ExitCar, "", 0, default, ne.Id);
            }
            else HostExit(MyId, car);
        }

        static void EnterLocal(OWCar car)
        {
            var p = PlayerController.Instance;
            if (p == null || car == null) return;
            OWCar.LocalDriving = car;
            if (p.Inventory != null) p.Inventory.Select(0, true);
            AudioManager.Play("creak", car.transform.position, 0.5f);
            GameEvents.ShowToast(Settings.TouchControlsActive ? "Drive with the stick. JUMP: brake · SPRINT: nitro · KICK: horn · INTERACT: get out" : "W/S drive · A/D steer · Space brake · Shift nitro · H horn · E get out");
        }

        static void ExitLocal(OWCar car)
        {
            var p = PlayerController.Instance;
            OWCar.LocalDriving = null;
            if (p == null) return;
            Vector3 pos = p.transform.position + Vector3.up * 0.5f;
            if (car != null)
            {
                pos = car.transform.position - car.transform.right * 2.4f + Vector3.up * 0.6f;
                if (Physics.CheckCapsule(pos + Vector3.up * 0.4f, pos + Vector3.up * 1.6f, 0.35f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                    pos = car.transform.position + car.transform.right * 2.4f + Vector3.up * 0.6f;
            }
            p.Teleport(pos);
            if (p.Inventory != null && p.Inventory.Slots[1] != null) p.Inventory.Select(1, true);
        }

        public static void SendCarInput(OWCar car, float throttle, float steer, bool brake, bool boost)
        {
            if (car == null) return;
            if (!NetState.IsClient) { car.SetInput(throttle, steer, brake, boost); return; }
            if (I == null || Time.unscaledTime < I.nextInput) return;
            I.nextInput = Time.unscaledTime + 0.05f;
            var ne = car.GetComponent<NetEntity>();
            if (ne != null) NetManager.I.OWCarInput(ne.Id, throttle, steer, brake, boost);
        }

        /// <summary>Called by OWCar.Explode (host / singleplayer).</summary>
        public static void OnCarWrecked(OWCar car)
        {
            if (car == null || NetState.IsClient) return;
            if (car.DriverId != 255)
            {
                byte d = car.DriverId;
                car.DriverId = 255;
                if (d == MyId) ExitLocal(car);
                Broadcast(OWEv.CarDriver, d, 0, car);
            }
            Broadcast(OWEv.CarWreck, 255, 0, car);
            for (int i = 0; i < parked.Count; i++) if (parked[i].car == car) parked[i] = (parked[i].pos, null, Time.time + 60f);
        }

        static void Broadcast(OWEv kind, byte from, long n, OWCar car)
        {
            var ne = car != null ? car.GetComponent<NetEntity>() : null;
            if (NetState.IsHost && ne != null) NetManager.I.OWEvent(255, kind, from, "", n, default, ne.Id);
        }

        /// <summary>Calls your selected car (with its upgrades) to the road next to you, or to the Meridian garage.</summary>
        public static void CallMyCar()
        {
            var p = PlayerController.Instance;
            var d = OWSave.D;
            if (p == null) return;
            if (string.IsNullOrEmpty(d.car) || !OWSave.Owns(d.car)) { GameEvents.ShowToast("You don't own a car yet. Eclipse Motors sells them (Midtown)."); return; }
            Vector3 spot; float yaw;
            if (p.transform.position.y < -20f) { spot = OWMap.GarageSpot; yaw = 0f; }
            else if (!RoadSpotNear(p.transform.position, out spot, out yaw)) { GameEvents.ShowToast("No road nearby. Walk to a street first."); return; }
            var def = OWCar.DefFor(d.car, d.paint, d.neon, OWSave.HasUpgrade(2), OWSave.HasUpgrade(3), OWSave.HasUpgrade(4));
            NetManager.I.OWRequest(OWReq.CallCar, def, (long)yaw, spot);
            GameEvents.ShowToast("Your " + OWItems.Get(d.car).Name + " is waiting on the street" + (p.transform.position.y < -20f ? " outside the Meridian." : "."));
            AudioManager.Ui("phone_ping");
        }

        static readonly float[] RoadLines = { -180, -120, -60, 0, 60, 120, 180 };

        /// <summary>The closest point in the right-hand lane of a street (roads run every 60 m).</summary>
        public static bool RoadSpotNear(Vector3 from, out Vector3 spot, out float yaw)
        {
            spot = Vector3.zero; yaw = 0f;
            float best = 60f;
            foreach (var l in RoadLines)
            {
                float dz = Mathf.Abs(from.z - l);
                if (dz < best) { best = dz; spot = new Vector3(Mathf.Clamp(from.x, -175f, 175f), 0.6f, l - 3f); yaw = 90f; }
                float dx = Mathf.Abs(from.x - l);
                if (dx < best) { best = dx; spot = new Vector3(l + 3f, 0.6f, Mathf.Clamp(from.z, -175f, 175f)); yaw = 0f; }
            }
            if (best >= 60f) return false;
            // Nudge forward if something is in the way.
            for (int i = 0; i < 6 && Physics.CheckBox(spot + Vector3.up * 0.9f, new Vector3(1.2f, 0.7f, 2.6f), Quaternion.Euler(0, yaw, 0), Layers.WorldMask | (1 << Layers.Props), QueryTriggerInteraction.Ignore); i++)
                spot += Quaternion.Euler(0, yaw, 0) * Vector3.forward * 6f;
            return true;
        }

        // ------------------------------------------------------------------ events from the host

        public static void OnEvent(OWEv kind, byte from, string s, long n, Vector3 v, ushort e)
        {
            var nm = NetManager.I;
            switch (kind)
            {
                case OWEv.Reward:
                {
                    long paid = OWSave.Earn(n, s);
                    if (paid > 0 && s.StartsWith("Heist"))
                    {
                        OWSave.D.heists++; OWSave.Save();
                        Stats.Add(StatHeists);
                        if (s.Contains("Bank")) Stats.Add(StatBank);
                    }
                    else if (paid > 0) { OWSave.D.jobs++; OWSave.Save(); }
                    break;
                }
                case OWEv.Text: OWPhone.ReceiveText(from, s); break;
                case OWEv.Money: OWPhone.ReceiveMoney(from, n); break;
                case OWEv.Call: OWPhone.OnCall(from, (int)n); break;
                case OWEv.Emote:
                {
                    var it = OWItems.In(OWCat.Emote).Find(x => x.Variant == n);
                    if (from == MyId) { if (it != null) GameEvents.ShowToast("Emote: " + it.Name); break; }
                    if (nm != null && nm.Players.TryGetValue(from, out var np) && np.Avatar != null)
                    {
                        np.Avatar.EmoteId = (int)n;
                        np.Avatar.EmoteUntil = Time.time + (n == 1 ? 6f : 3.5f);
                        if (n == 7) AudioManager.Play("cash", np.Avatar.transform.position + Vector3.up * 1.5f, 0.8f);
                    }
                    break;
                }
                case OWEv.CarDriver:
                {
                    if (!NetState.IsClient) break; // the host applied it already
                    var ne = NetEntity.Get(e);
                    var car = ne != null ? ne.GetComponent<OWCar>() : null;
                    if (car == null) break;
                    car.DriverId = n == 1 ? from : (byte)255;
                    if (n == 1 && from == MyId) EnterLocal(car);
                    else if (OWCar.LocalDriving == car && (n == 0 || from != MyId)) ExitLocal(car);
                    break;
                }
                case OWEv.CarWreck:
                {
                    if (!NetState.IsClient) break;
                    var ne = NetEntity.Get(e);
                    var car = ne != null ? ne.GetComponent<OWCar>() : null;
                    if (car != null) car.WreckVisual();
                    if (OWCar.LocalDriving == car && car != null) ExitLocal(car);
                    break;
                }
                case OWEv.Toast: Toasts.Show("PORT ECLIPSE", s, new Color(1f, 0.78f, 0.16f)); break;
                case OWEv.Heist:
                    Toasts.Show("HEIST", s, new Color(1f, 0.4f, 0.25f));
                    AudioManager.Ui(n == 1 ? "ui_success" : n == 2 ? "ui_error" : "phone_ping");
                    break;
            }
        }

        // ------------------------------------------------------------------ state sync

        public static void WriteState(NetWriter w)
        {
            OWHeists.Write(w);
            w.Byte((byte)Mathf.Min(Wanted.Count, 32));
            int k = 0;
            foreach (var kv in Wanted) { if (k++ >= 32) break; w.Byte(kv.Key); w.Byte((byte)kv.Value); }
            w.Bool(BountyActive); w.Vec3(BountyPos);
            // Who drives what (for players who joined later).
            var driven = new List<(ushort, byte)>();
            foreach (var c in OWCar.All)
                if (c != null && c.DriverId != 255) { var ne = c.GetComponent<NetEntity>(); if (ne != null) driven.Add((ne.Id, c.DriverId)); }
            w.Byte((byte)Mathf.Min(driven.Count, 32));
            for (int i = 0; i < driven.Count && i < 32; i++) { w.UShort(driven[i].Item1); w.Byte(driven[i].Item2); }
        }

        public static void ReadState(NetReader r)
        {
            OWHeists.Read(r);
            Wanted.Clear();
            int n = r.Byte();
            for (int i = 0; i < n; i++) { byte id = r.Byte(); int s = r.Byte(); if (s > 0) Wanted[id] = s; }
            BountyActive = r.Bool(); BountyPos = r.Vec3();
            var drivers = new Dictionary<ushort, byte>();
            int dn = r.Byte();
            for (int i = 0; i < dn; i++) { ushort id = r.UShort(); drivers[id] = r.Byte(); }
            foreach (var c in OWCar.All)
            {
                if (c == null || c == OWCar.LocalDriving) continue;
                var ne = c.GetComponent<NetEntity>();
                if (ne != null) c.DriverId = drivers.TryGetValue(ne.Id, out var d) ? d : (byte)255;
            }
        }

        static bool stateDirty;
        public static void MarkDirty() => stateDirty = true;

        // ------------------------------------------------------------------ host: requests

        public static void HostRequest(byte pid, OWReq kind, byte target, string s, long n, Vector3 v, ushort e)
        {
            if (!Active || NetState.IsClient) return;
            var nm = NetManager.I;
            switch (kind)
            {
                case OWReq.HeistJoin: OWHeists.Join(pid, (int)n); break;
                case OWReq.HeistLeave: OWHeists.Leave(pid); break;
                case OWReq.HeistReady: OWHeists.SetReady(pid, n != 0); break;
                case OWReq.CallCar: HostCallCar(pid, s, v, n); break;
                case OWReq.Drive: { var ne = NetEntity.Get(e); var car = ne != null ? ne.GetComponent<OWCar>() : null; if (car != null) HostDrive(pid, car); break; }
                case OWReq.ExitCar: { var ne = NetEntity.Get(e); var car = ne != null ? ne.GetComponent<OWCar>() : null; if (car != null) HostExit(pid, car); break; }
                case OWReq.Emote: if (n >= 0 && n < 8) nm.OWEvent(255, OWEv.Emote, pid, "", n); break;
                case OWReq.Text:
                    if (!nm.OWHasPlayer(target) || target == pid || string.IsNullOrWhiteSpace(s)) break;
                    if (lastText.TryGetValue(pid, out var lt) && Time.time - lt < 0.4f) break;
                    lastText[pid] = Time.time;
                    nm.OWEvent(target, OWEv.Text, pid, ChatFilter.Apply(s.Length > 160 ? s.Substring(0, 160) : s));
                    break;
                case OWReq.Money:
                    if (!nm.OWHasPlayer(target) || target == pid || n <= 0 || n > OWPhone.MaxTransfer) break;
                    nm.OWEvent(target, OWEv.Money, pid, "", n);
                    break;
                case OWReq.Call:
                    if (!nm.OWHasPlayer(target) || target == pid) break;
                    nm.OWEvent(target, OWEv.Call, pid, "", n);
                    break;
                case OWReq.Bounty: StartBounty(pid); break;
            }
        }

        static void HostCallCar(byte pid, string def, Vector3 spot, long yaw)
        {
            if (string.IsNullOrEmpty(def) || !def.StartsWith("car|") || def.Length > 80) return;
            var parts = def.Split('|');
            var model = parts.Length > 1 ? OWItems.Get(parts[1]) : null;
            if (model == null || model.Cat != OWCat.Car) return;
            var nm = NetManager.I;
            bool garage = (spot - OWMap.GarageSpot).sqrMagnitude < 4f;
            if (!garage && nm.OWPos(pid, out var at) && MathX.Flat(at - spot).magnitude > 90f) return;
            if (called.TryGetValue(pid, out var old) && old != null && (old.DriverId == 255 || old.DriverId == pid))
            {
                if (old.DriverId == pid) HostExit(pid, old);
                DeleteCar(old);
            }
            var rb = PropFactory.Spawn(def, spot, Quaternion.Euler(0, yaw, 0));
            var car = rb != null ? rb.GetComponent<OWCar>() : null;
            if (car == null) return;
            car.OwnerId = pid;
            called[pid] = car;
        }

        static void DeleteCar(OWCar car)
        {
            if (car != null) Destroy(car.gameObject); // NetEntity tells the clients
        }

        static void HostDrive(byte pid, OWCar car)
        {
            if (car == null || car.Wrecked || car.DriverId != 255) return;
            var nm = NetManager.I;
            if (nm.OWPos(pid, out var at) && Vector3.Distance(at, car.transform.position) > 9f) return;
            foreach (var c in OWCar.All) if (c != null && c != car && c.DriverId == pid) HostExit(pid, c);
            car.DriverId = pid;
            if (pid == MyId) EnterLocal(car);
            Broadcast(OWEv.CarDriver, pid, 1, car);
        }

        static void HostExit(byte pid, OWCar car)
        {
            if (car == null || car.DriverId != pid) return;
            car.DriverId = 255;
            car.SetInput(0f, 0f, true, false);
            Broadcast(OWEv.CarDriver, pid, 0, car);
        }

        // ------------------------------------------------------------------ host: world

        void HostTick()
        {
            var nm = NetManager.I;
            float now = Time.time;

            // Drivers who left or died get out.
            foreach (var c in OWCar.All)
            {
                if (c == null || c.DriverId == 255) continue;
                if (!nm.OWHasPlayer(c.DriverId) || IsDead(c.DriverId)) { byte d = c.DriverId; if (d == MyId && OWCar.LocalDriving == c) ExitLocal(c); HostExit(d, c); }
            }

            // Parked cars come back a minute after they're wrecked (when nobody is looking).
            for (int i = 0; i < parked.Count; i++)
            {
                var pk = parked[i];
                if (pk.car == null && pk.freeAt > 0f && now > pk.freeAt && !AnyPlayerWithin(pk.pos, 35f))
                    parked[i] = (pk.pos, SpawnParked(pk.pos, new System.Random(i * 31 + (int)now)), 0f);
            }

            // Wanted stars cool down; dying clears them.
            foreach (var id in new List<byte>(Wanted.Keys))
            {
                if (!nm.OWHasPlayer(id) || IsDead(id)) { Wanted.Remove(id); stateDirty = true; continue; }
                if (OWHeists.InActiveCrew(id)) { lastCrime[id] = now; continue; }
                lastCrime.TryGetValue(id, out var lc);
                if (now - lc > 25f + Wanted[id] * 8f)
                {
                    Wanted[id]--;
                    lastCrime[id] = now - 12f;
                    stateDirty = true;
                    if (Wanted[id] <= 0) { Wanted.Remove(id); nm.OWEvent(id, OWEv.Toast, 255, "You lost the cops."); }
                }
            }

            OWHeists.HostTick(Time.deltaTime);

            if (BountyActive && (now > bountyUntil || bountyBoss == null)) { BountyActive = false; stateDirty = true; }
            if (BountyActive && bountyBoss != null && !bountyBoss.IsDead) { var bp = bountyBoss.Position; if ((bp - BountyPos).sqrMagnitude > 4f) { BountyPos = bp; } }

            if (now > nextSpawn) { nextSpawn = now + 2.5f; ManagePeople(); }
            if (now > nextPolice) { nextPolice = now + 3.5f; ManagePolice(); }
            if (now > nextCleanup) { nextCleanup = now + 5f; Cleanup(); }

            if (now > nextState || stateDirty) { nextState = now + 1f; stateDirty = false; nm.OWBroadcastState(); }
        }

        public static bool IsDead(byte id)
        {
            if (id == MyId) return PlayerController.Instance == null || PlayerController.Instance.IsDead;
            var nm = NetManager.I;
            return nm == null || !nm.Players.TryGetValue(id, out var p) || (p.Flags & NetPlayer.FDead) != 0;
        }

        /// <summary>Host: every player id in the session (you first).</summary>
        public static List<byte> PlayerIds()
        {
            var list = new List<byte> { MyId };
            var nm = NetManager.I;
            if (NetState.Active && nm != null) foreach (var id in nm.Players.Keys) if (id != MyId && nm.Players[id].Ready) list.Add(id);
            return list;
        }

        static bool AnyPlayerWithin(Vector3 pos, float r)
        {
            var nm = NetManager.I;
            foreach (var id in PlayerIds()) if (nm.OWPos(id, out var at) && (at - pos).sqrMagnitude < r * r) return true;
            return false;
        }

        static int Count(Role r) { int n = 0; foreach (var kv in roles) if (kv.Key != null && kv.Value == r && !kv.Key.IsDead) n++; return n; }

        static bool SidewalkSpot(Vector3 near, float min, float max, out Vector3 spot)
        {
            spot = Vector3.zero;
            for (int t = 0; t < 12; t++)
            {
                var s = OWMap.Sidewalks[Random.Range(0, OWMap.Sidewalks.Count)] + new Vector3(Random.Range(-14f, 14f), 0f, 0f);
                float d = MathX.Flat(s - near).magnitude;
                if (d < min || d > max) continue;
                if (Physics.CheckCapsule(s + Vector3.up * 0.5f, s + Vector3.up * 1.6f, 0.4f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                spot = s;
                return true;
            }
            return false;
        }

        static Goredoll SpawnRole(Vector3 pos, Role role, string preset, string name)
        {
            var d = GoredollFactory.Spawn(pos, Random.Range(0f, 360f), preset);
            if (d == null) return null;
            d.DisplayName = name;
            roles[d] = role;
            return d;
        }

        static readonly string[] CivNames = { "Pedestrian", "Tourist", "Office Worker", "Jogger", "Shopper", "Commuter", "Local", "Street Vendor" };

        void ManagePeople()
        {
            if (OWMap.Sidewalks.Count == 0) return;
            var ids = PlayerIds();
            int want = Mathf.Clamp(ids.Count * 4, 4, Mobile ? 6 : 10);
            if (Count(Role.Civ) >= want || Goredoll.All.Count > (Mobile ? 12 : 18)) return;
            var nm = NetManager.I;
            var pid = ids[Random.Range(0, ids.Count)];
            if (!nm.OWPos(pid, out var at) || at.y < -20f) return;
            if (SidewalkSpot(at, 22f, 70f, out var spot)) SpawnRole(spot, Role.Civ, "Passive", CivNames[Random.Range(0, CivNames.Length)]);
        }

        void ManagePolice()
        {
            if (OWMap.Sidewalks.Count == 0) return;
            var nm = NetManager.I;
            int stars = 0; byte worst = 255;
            foreach (var kv in Wanted) if (kv.Value > stars) { stars = kv.Value; worst = kv.Key; }
            int cops = Count(Role.Cop);
            int want = Mathf.Min(Mobile ? 6 : 9, stars * 2);
            if (stars == 0 || cops >= want || worst == 255 || Goredoll.All.Count > (Mobile ? 14 : 22)) return;
            if (!nm.OWPos(worst, out var at) || at.y < -20f) return;
            if (SidewalkSpot(at, 20f, 55f, out var spot))
            {
                var cop = SpawnRole(spot, Role.Cop, stars >= 3 ? "Soldier" : "Gunman", stars >= 3 ? "SWAT" : "Police Officer");
                if (cop != null && Random.value < 0.5f) AudioManager.Play("siren", spot + Vector3.up * 2f, 0.6f, 1f, 1f, 10f, 140f);
            }
        }

        void Cleanup()
        {
            float now = Time.time;
            foreach (var d in new List<Goredoll>(roles.Keys))
            {
                if (d == null) { roles.Remove(d); diedAt.Remove(d); continue; }
                var role = roles[d];
                bool far = !AnyPlayerWithin(d.Position, role == Role.Cop ? 130f : 110f);
                if (d.IsDead)
                {
                    if (!diedAt.ContainsKey(d)) diedAt[d] = now;
                    if (now - diedAt[d] > 75f && !AnyPlayerWithin(d.Position, 30f)) { roles.Remove(d); diedAt.Remove(d); d.Despawn(); }
                    continue;
                }
                bool unneeded = role == Role.Cop && Wanted.Count == 0 && !AnyPlayerWithin(d.Position, 25f);
                if ((role == Role.Civ || role == Role.Cop) && (far || unneeded)) { roles.Remove(d); d.Despawn(); }
            }
        }

        /// <summary>Host: removes heist guards (when the heist ends).</summary>
        public static void DespawnGuards()
        {
            foreach (var d in new List<Goredoll>(roles.Keys))
                if (d != null && roles[d] == Role.Guard && !d.IsDead) { roles.Remove(d); d.Despawn(); }
        }

        public static Goredoll SpawnGuard(Vector3 pos, string preset) => SpawnRole(pos, Role.Guard, preset, preset == "Soldier" ? "Security" : "Guard");

        // ------------------------------------------------------------------ host: crime

        /// <summary>Which player did that (null source / AI = 255).</summary>
        public static byte PlayerOf(object src)
        {
            if (src is RemotePlayer rp && rp.Player != null) return rp.Player.Id;
            if (src is PlayerController || src is Weapon) return MyId;
            return 255;
        }

        public static void AddWanted(byte pid, int stars, string why)
        {
            if (pid == 255 || !Active || NetState.IsClient) return;
            Wanted.TryGetValue(pid, out var cur);
            int nw = Mathf.Clamp(cur + stars, 0, 5);
            lastCrime[pid] = Time.time;
            if (nw == cur) return;
            Wanted[pid] = nw;
            stateDirty = true;
            NetManager.I.OWEvent(pid, OWEv.Toast, 255, "WANTED " + new string('*', nw) + "  " + why);
        }

        static void OnDollDied(Goredoll d, object attacker)
        {
            if (!Active || NetState.IsClient || d == null) return;
            byte pid = PlayerOf(attacker);
            roles.TryGetValue(d, out var role);
            if (!roles.ContainsKey(d)) role = Role.Civ;
            diedAt[d] = Time.time;
            if (d == bountyBoss)
            {
                byte to = pid != 255 ? pid : bountyBy;
                if (to != 255) NetManager.I.OWEvent(to, OWEv.Reward, 255, "Bounty: gang boss", 12000);
                NetManager.I.OWEvent(255, OWEv.Toast, 255, "The gang boss is dead" + (to != 255 ? " (" + NetManager.I.OWName(to) + " gets the bounty)." : "."));
                BountyActive = false; bountyBoss = null; stateDirty = true;
                return;
            }
            if (pid == 255) return;
            switch (role)
            {
                case Role.Civ: AddWanted(pid, 1, "Murder"); break;
                case Role.Cop: AddWanted(pid, 2, "Cop killer"); break;
                case Role.Guard: AddWanted(pid, 0, ""); break;
            }
        }

        /// <summary>Armed Goredolls: police only go for wanted players; guards for the heist crew and wanted players.</summary>
        static bool MayTarget(Goredoll d, IPlayerTarget p)
        {
            if (d == null || !roles.TryGetValue(d, out var role)) return true;
            byte pid = p is RemotePlayer rp && rp.Player != null ? rp.Player.Id : p is PlayerController ? MyId : (byte)255;
            bool wanted = pid != 255 && Wanted.ContainsKey(pid);
            switch (role)
            {
                case Role.Cop: return wanted;
                case Role.Guard: return wanted || OWHeists.InActiveCrew(pid);
                default: return true;
            }
        }

        // ------------------------------------------------------------------ host: bounties

        static readonly string[] BountyPlaces = { "Gang Hideout", "Scrapyard", "Factory", "Docks" };

        static void StartBounty(byte pid)
        {
            var nm = NetManager.I;
            if (BountyActive) { nm.OWEvent(pid, OWEv.Toast, 255, "There's already a bounty out. Check your map (skull)."); return; }
            var place = OWMap.Place(BountyPlaces[Random.Range(0, BountyPlaces.Length)]);
            bountyBoss = SpawnRole(place, Role.Boss, "Soldier", "Gang Boss 'Knuckles'");
            if (bountyBoss == null) return;
            for (int i = 0; i < 2; i++) SpawnRole(place + new Vector3(Random.Range(-4f, 4f), 0, Random.Range(-4f, 4f)), Role.Gang, "Gunman", "Gang Member");
            BountyActive = true; BountyPos = place; bountyBy = pid; bountyUntil = Time.time + 480f;
            stateDirty = true;
            nm.OWEvent(255, OWEv.Toast, 255, nm.OWName(pid) + " took a bounty: $12,000 for the gang boss. Skull on the map.");
        }
    }
}
