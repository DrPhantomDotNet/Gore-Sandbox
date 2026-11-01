using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Open World additions (Content Update): the casino (slots, roulette, blackjack + a heist), the museum (diamond
    /// heist), the nightclub (dance floor, dancers, bar), the heliport (rent a helicopter), the marina (rent a boat),
    /// the rail yard (gold train heist) and gang territories you can take over for passive income.
    /// </summary>
    public static class OWContent
    {
        public const int BoatRent = 400, HeliRent = 2500;

        public static void Use(OWPoint pt)
        {
            switch (pt.Kind)
            {
                case "casino": UIRoot.Push(new CasinoScreen()); break;
                case "bar":
                    if (OWSave.Spend(50)) { AudioManager.Ui("cash"); GameEvents.ShowToast("You knock back a drink. -$50"); var p = PlayerController.Instance; if (p != null) CameraFX.Shake(0.3f); }
                    else GameEvents.ShowToast("Drinks are $50.");
                    break;
                case "club": GameEvents.ShowToast("Welcome to NOVA. Hit the dance floor: J plays your emote."); break;
                case "marina": Rent("veh_boat", OWMap.Place("Marina Dock"), BoatRent, "boat"); break;
                case "heliport": Rent("veh_heli", OWMap.Place("Helipad"), HeliRent, "helicopter"); break;
            }
        }

        static void Rent(string def, Vector3 at, int price, string what)
        {
            if (NetState.IsClient) { GameEvents.ShowToast("Only the host can drive boats and helicopters for now."); return; }
            if (!OWSave.Spend(price)) { GameEvents.ShowToast("Renting a " + what + " costs $" + price.ToString("N0") + "."); AudioManager.Ui("ui_error"); return; }
            foreach (var old in Object.FindObjectsOfType<Heli>()) if (old != null && Heli.Local != old && Vector3.Distance(old.transform.position, at) < 6f) Object.Destroy(old.gameObject);
            foreach (var old in Boat.All.ToArray()) if (old != null && Boat.Local != old && Vector3.Distance(old.transform.position, at) < 8f) Object.Destroy(old.gameObject);
            PropFactory.Spawn(def, at, Quaternion.identity);
            AudioManager.Ui("cash");
            GameEvents.ShowToast("Your " + what + " is ready. Press E on it. -$" + price.ToString("N0"));
        }
    }

    // ====================================================================== map pieces

    public static class OWContentMap
    {
        static Material T(Texture2D t) => MapBuilder.Tex(t);

        public static void Casino(MapBuilder m, Vector3 c)
        {
            var b = PortEclipseMap.Room(m, c + new Vector3(-20, 0.15f, -18), c + new Vector3(20, 7f, 18), TextureFactory.Wallpaper, new Color(0.45f, 0.08f, 0.12f), c.x, 4f);
            PortEclipseMap.Tower(m, c + new Vector3(-20, 7f, -18), c + new Vector3(20, 48f, 18), TextureFactory.Concrete, new Color(0.25f, 0.2f, 0.3f));
            m.Box("Casino Sign", c + new Vector3(0, 6f, -18.4f), Quaternion.identity, new Vector3(20f, 1.8f, 0.15f), Mats.Glow(new Color(1f, 0.8f, 0.2f), 3f), SurfaceType.Glass, Color.white, 1f, false);
            m.Block("Casino Carpet", c + new Vector3(-19.6f, 0.15f, -17.6f), c + new Vector3(19.6f, 0.17f, 17.6f), T(TextureFactory.Carpet), SurfaceType.Carpet, new Color(0.35f, 0.05f, 0.1f), 0.5f, false);
            for (int i = 0; i < 8; i++)
            {
                var p = c + new Vector3(-15 + i * 4, 0.15f, -10);
                m.Box("Slot Machine", p + Vector3.up * 0.9f, Quaternion.identity, new Vector3(1f, 1.8f, 0.8f), Mats.LitMetal, SurfaceType.Metal, new Color(0.7f, 0.15f, 0.2f));
                m.Box("Slot Screen", p + new Vector3(0, 1.3f, -0.41f), Quaternion.identity, new Vector3(0.7f, 0.5f, 0.02f), Mats.Glow(new Color(0.9f, 0.8f, 0.3f), 1.5f), SurfaceType.Glass, Color.white, 1f, false);
                OWPoint.Make(m.Root, "casino", "Casino games (slots, roulette, blackjack)", p + new Vector3(0, 1.2f, -1.1f), new Vector3(1.2f, 2.2f, 0.8f));
            }
            for (int i = 0; i < 3; i++)
            {
                var p = c + new Vector3(-10 + i * 10, 0.15f, 2);
                m.Cylinder("Game Table", p, 1.8f, 0.9f, T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.35f, 0.2f, 0.1f), 18);
                m.Cylinder("Felt", p + Vector3.up * 0.9f, 1.65f, 0.03f, Mats.Lit, SurfaceType.Carpet, new Color(0.05f, 0.4f, 0.15f), 18, false);
                OWPoint.Make(m.Root, "casino", "Casino games (slots, roulette, blackjack)", p + Vector3.up * 1.2f, new Vector3(3.6f, 1.4f, 3.6f));
                m.CeilingLight(p + Vector3.up * 6.4f, new Color(1f, 0.85f, 0.6f), 10f, 1.6f);
            }
            m.Block("Cashier Cage", c + new Vector3(-6, 0.15f, 12), c + new Vector3(6, 3f, 12.3f), Mats.LitMetal, SurfaceType.Metal, new Color(0.7f, 0.65f, 0.3f));
            OWMap.Places["Casino"] = c + new Vector3(0, 0.2f, -22);
            OWMap.Places["Casino Cage"] = c + new Vector3(0, 0.2f, 13.5f);
            OWMap.Places["Casino Vault"] = c + new Vector3(14, 0.2f, 15f);
            m.Box("Casino Vault Door", c + new Vector3(14, 1.6f, 17.6f), Quaternion.identity, new Vector3(2.6f, 3f, 0.3f), Mats.LitMetal, SurfaceType.Metal, new Color(0.6f, 0.6f, 0.62f));
            for (int k = 0; k < 6; k++) m.Box("Chips", c + new Vector3(-3 + k, 1.1f, 13.5f), Quaternion.identity, new Vector3(0.4f, 0.3f, 0.4f), Mats.Glow(new Color(0.9f, 0.3f, 0.3f), 0.3f), SurfaceType.Wood, Color.white, 1f, false);
        }

        public static void Museum(MapBuilder m, Vector3 c)
        {
            PortEclipseMap.Room(m, c + new Vector3(-18, 0.15f, -16), c + new Vector3(18, 9f, 16), TextureFactory.Plaster, new Color(0.92f, 0.9f, 0.85f), c.x, 5f);
            m.Block("Museum Steps", c + new Vector3(-12, 0.15f, -20), c + new Vector3(12, 0.6f, -16), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.9f, 0.88f, 0.84f));
            for (int k = -3; k <= 3; k++) m.Cylinder("Column", c + new Vector3(k * 3.4f, 0.6f, -17.5f), 0.55f, 8.4f, T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.95f, 0.93f, 0.88f), 14);
            m.Box("Museum Pediment", c + new Vector3(0, 9.3f, -17.5f), Quaternion.identity, new Vector3(25f, 1f, 3f), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.95f, 0.93f, 0.88f));
            // Exhibits: a dinosaur skeleton (cylinders), statues, the diamond in a glass case under a spotlight
            for (int i = 0; i < 6; i++) m.Cylinder("Vertebra", c + new Vector3(-10 + i * 0.9f, 2.4f + Mathf.Sin(i * 0.6f) * 0.4f, 6), 0.25f, 0.3f, T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.85f, 0.8f, 0.68f), 8);
            m.Box("Statue", c + new Vector3(10, 1.4f, 8), Quaternion.identity, new Vector3(1f, 2.8f, 1f), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.8f, 0.8f, 0.82f));
            m.Box("Diamond Plinth", c + new Vector3(0, 0.7f, 0), Quaternion.identity, new Vector3(1.2f, 1.1f, 1.2f), T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.15f, 0.15f, 0.18f));
            m.Box("Diamond Case", c + new Vector3(0, 1.6f, 0), Quaternion.identity, new Vector3(1.1f, 0.8f, 1.1f), Mats.Glass, SurfaceType.Glass, Color.white, 1f, false);
            m.Box("The Eclipse Diamond", c + new Vector3(0, 1.55f, 0), Quaternion.Euler(45, 45, 0), new Vector3(0.3f, 0.3f, 0.3f), Mats.Glow(new Color(0.3f, 0.2f, 0.5f), 2.5f), SurfaceType.Glass, Color.white, 1f, false);
            m.CeilingLight(c + new Vector3(0, 8.5f, 0), new Color(0.9f, 0.9f, 1f), 12f, 2.4f);
            OWMap.Places["Museum"] = c + new Vector3(0, 0.7f, -22);
            OWMap.Places["Museum Display"] = c + new Vector3(0, 0.2f, -1.4f);
        }

        public static void Nightclub(MapBuilder m, Vector3 c)
        {
            PortEclipseMap.Room(m, c + new Vector3(-18, 0.15f, -16), c + new Vector3(18, 7f, 16), TextureFactory.Concrete, new Color(0.12f, 0.1f, 0.18f), c.x, 4f);
            PortEclipseMap.Tower(m, c + new Vector3(-18, 7f, -16), c + new Vector3(18, 30f, 16), TextureFactory.Brick, new Color(0.3f, 0.25f, 0.35f));
            m.Box("NOVA Sign", c + new Vector3(0, 5.6f, -16.4f), Quaternion.identity, new Vector3(12f, 1.6f, 0.15f), Mats.Glow(new Color(1f, 0.2f, 0.8f), 3.5f), SurfaceType.Glass, Color.white, 1f, false);
            // Dance floor tiles that flash to the beat
            var tiles = new List<Renderer>();
            for (int x = 0; x < 6; x++) for (int z = 0; z < 6; z++)
            {
                var t = m.Box("Dance Tile", c + new Vector3(-6 + x * 2.4f, 0.17f, -3 + z * 2.4f), Quaternion.identity, new Vector3(2.3f, 0.04f, 2.3f), Mats.Glow(Color.white, 1.2f), SurfaceType.Glass, Color.white, 1f, false);
                tiles.Add(t.GetComponent<Renderer>());
            }
            var lights = new List<Light>();
            for (int i = 0; i < 4; i++)
            {
                var l = new GameObject("Club Light").AddComponent<Light>();
                l.transform.SetParent(m.Root, false); l.transform.position = c + new Vector3(-5 + i * 3.3f, 6f, 3);
                l.type = LightType.Point; l.range = 14f; l.intensity = 2.5f;
                lights.Add(l);
            }
            var cl = m.Root.gameObject.AddComponent<ClubLights>(); cl.Tiles.AddRange(tiles); cl.Lights.AddRange(lights); cl.Center = c;
            m.Box("DJ Booth", c + new Vector3(0, 1f, 13), Quaternion.identity, new Vector3(5f, 1.7f, 1.2f), Mats.LitMetal, SurfaceType.Metal, new Color(0.15f, 0.12f, 0.2f));
            for (int i = -1; i <= 1; i += 2) m.Box("Speaker", c + new Vector3(i * 4.5f, 1.6f, 13.5f), Quaternion.identity, new Vector3(1.4f, 3f, 1.2f), Mats.Lit, SurfaceType.Wood, new Color(0.06f, 0.06f, 0.07f));
            m.Box("Bar Counter", c + new Vector3(-14, 0.7f, 0), Quaternion.identity, new Vector3(1.2f, 1.1f, 14f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.25f, 0.15f, 0.1f));
            m.Box("Bar Neon", c + new Vector3(-17.7f, 3f, 0), Quaternion.identity, new Vector3(0.1f, 0.3f, 12f), Mats.Glow(new Color(0.2f, 0.8f, 1f), 3f), SurfaceType.Glass, Color.white, 1f, false);
            OWPoint.Make(m.Root, "bar", "Buy a drink ($50)", c + new Vector3(-12.8f, 1.2f, 0), new Vector3(1.2f, 2f, 10f));
            OWPoint.Make(m.Root, "club", "NOVA nightclub", c + new Vector3(0, 1.2f, -18f), new Vector3(4f, 2.6f, 2f));
            OWMap.Places["Nightclub"] = c + new Vector3(0, 0.2f, -19);
            OWMap.Places["Club Floor"] = c + new Vector3(0, 0.2f, 3);
        }

        public static void Heliport(MapBuilder m, Vector3 c)
        {
            PortEclipseMap.Tower(m, c + new Vector3(-20, 0.15f, 4), c + new Vector3(20, 8f, 20), TextureFactory.Concrete, new Color(0.5f, 0.52f, 0.56f));
            m.Block("Helipad", c + new Vector3(-12, 0.15f, -18), c + new Vector3(12, 0.2f, 2), T(TextureFactory.Asphalt), SurfaceType.Concrete, new Color(0.3f, 0.32f, 0.35f), 0.3f, false);
            m.Cylinder("H Circle", c + new Vector3(0, 0.21f, -8), 5f, 0.01f, Mats.Lit, SurfaceType.Concrete, new Color(0.95f, 0.8f, 0.1f), 24, false);
            m.Cylinder("H Inner", c + new Vector3(0, 0.22f, -8), 4.6f, 0.01f, Mats.Lit, SurfaceType.Concrete, new Color(0.3f, 0.32f, 0.35f), 24, false);
            m.Box("H", c + new Vector3(-1.2f, 0.23f, -8), Quaternion.identity, new Vector3(0.5f, 0.01f, 4f), Mats.Lit, SurfaceType.Concrete, Color.white, 1f, false);
            m.Box("H", c + new Vector3(1.2f, 0.23f, -8), Quaternion.identity, new Vector3(0.5f, 0.01f, 4f), Mats.Lit, SurfaceType.Concrete, Color.white, 1f, false);
            m.Box("H", c + new Vector3(0, 0.23f, -8), Quaternion.identity, new Vector3(2.4f, 0.01f, 0.5f), Mats.Lit, SurfaceType.Concrete, Color.white, 1f, false);
            OWPoint.Make(m.Root, "heliport", "Rent a helicopter ($" + OWContent.HeliRent.ToString("N0") + ")", c + new Vector3(0, 1.2f, 3.2f), new Vector3(4f, 2.4f, 1.6f));
            m.Box("Heliport Sign", c + new Vector3(0, 4f, 3.9f), Quaternion.identity, new Vector3(10f, 1.4f, 0.15f), Mats.Glow(new Color(0.3f, 0.7f, 1f), 2.5f), SurfaceType.Glass, Color.white, 1f, false);
            OWMap.Places["Heliport"] = c + new Vector3(0, 0.25f, 0);
            OWMap.Places["Helipad"] = c + new Vector3(0, 0.3f, -8);
        }

        public static void Marina(MapBuilder m, Vector3 c)
        {
            m.Block("Boardwalk", c + new Vector3(-23, 0.15f, -23), c + new Vector3(23, 0.3f, 23), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.55f, 0.4f, 0.25f), 0.3f);
            m.Block("Pier", c + new Vector3(23, -0.2f, -3), c + new Vector3(70, 0.3f, 3), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.5f, 0.36f, 0.22f), 0.3f);
            for (int i = 0; i < 6; i++) m.Cylinder("Pile", c + new Vector3(28 + i * 8, -3f, 3.4f), 0.3f, 3.4f, T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.35f, 0.25f, 0.15f), 8);
            m.Box("Marina Hut", c + new Vector3(10, 1.8f, 10), Quaternion.identity, new Vector3(8f, 3.3f, 6f), T(TextureFactory.Plaster), SurfaceType.Concrete, new Color(0.9f, 0.9f, 0.95f));
            OWPoint.Make(m.Root, "marina", "Rent a speedboat ($" + OWContent.BoatRent + ")", c + new Vector3(10, 1.2f, 6.5f), new Vector3(4f, 2.4f, 1.4f));
            OWMap.Places["Marina"] = c + new Vector3(0, 0.35f, 0);
            OWMap.Places["Marina Dock"] = c + new Vector3(55, -0.55f, 8);
        }

        public static void RailYard(MapBuilder m, Vector3 c)
        {
            m.Block("Gravel", c + new Vector3(-23, 0.15f, -23), c + new Vector3(23, 0.2f, 23), T(TextureFactory.Dirt), SurfaceType.Dirt, new Color(0.45f, 0.42f, 0.38f), 0.3f);
            for (int t = -1; t <= 1; t += 2)
            {
                float z = t * 6f;
                for (float x = -22; x < 23; x += 1.2f) m.Box("Sleeper", c + new Vector3(x, 0.25f, z), Quaternion.identity, new Vector3(0.25f, 0.1f, 2.6f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.35f, 0.25f, 0.18f), 1f, false);
                m.Box("Rail", c + new Vector3(0, 0.35f, z - 0.72f), Quaternion.identity, new Vector3(46f, 0.12f, 0.08f), Mats.LitMetal, SurfaceType.Metal, new Color(0.55f, 0.55f, 0.58f), 1f, false);
                m.Box("Rail", c + new Vector3(0, 0.35f, z + 0.72f), Quaternion.identity, new Vector3(46f, 0.12f, 0.08f), Mats.LitMetal, SurfaceType.Metal, new Color(0.55f, 0.55f, 0.58f), 1f, false);
            }
            // The gold train: locomotive + armoured car on the north track
            m.Box("Locomotive", c + new Vector3(-12, 2.2f, 6), Quaternion.identity, new Vector3(12f, 3.6f, 3f), Mats.LitMetal, SurfaceType.Metal, new Color(0.15f, 0.2f, 0.35f));
            m.Box("Armoured Car", c + new Vector3(4, 2.2f, 6), Quaternion.identity, new Vector3(14f, 3.6f, 3f), Mats.LitMetal, SurfaceType.Metal, new Color(0.35f, 0.36f, 0.32f));
            m.Box("Car Door", c + new Vector3(4, 2f, 4.45f), Quaternion.identity, new Vector3(3f, 2.6f, 0.1f), Mats.LitMetal, SurfaceType.Metal, new Color(0.6f, 0.55f, 0.2f), 1f, false);
            for (int k = 0; k < 6; k++) m.Box("Gold Bars", c + new Vector3(1 + k, 0.5f, 3.4f), Quaternion.identity, new Vector3(0.7f, 0.25f, 0.4f), Mats.Glow(new Color(1f, 0.8f, 0.2f), 0.5f), SurfaceType.Metal, Color.white, 1f, false);
            m.StreetLamp(c + new Vector3(-15, 0.2f, -12), 0f, new Color(1f, 0.8f, 0.5f), 8f, 24f, 2f);
            OWMap.Places["Rail Yard"] = c + new Vector3(-20, 0.25f, -16);
            OWMap.Places["Train Doors"] = c + new Vector3(4, 0.25f, 3.2f);
            OWMap.Places["Train Gold"] = c + new Vector3(3.5f, 0.25f, 2.6f);
        }
    }

    /// <summary>Nightclub dance floor + lights flashing to a 128 BPM beat; dancers on the host.</summary>
    public sealed class ClubLights : MonoBehaviour
    {
        public readonly List<Renderer> Tiles = new List<Renderer>();
        public readonly List<Light> Lights = new List<Light>();
        public Vector3 Center;
        MaterialPropertyBlock mpb;
        static readonly Color[] Cols = { new Color(1f, 0.1f, 0.6f), new Color(0.1f, 0.9f, 1f), new Color(0.6f, 0.2f, 1f), new Color(1f, 0.8f, 0.1f), new Color(0.2f, 1f, 0.4f) };
        float nextBeat; int beat; bool spawned;

        void Update()
        {
            if (!spawned && !NetState.IsClient && GameManager.I != null && GameManager.I.State == GameState.Playing)
            {
                spawned = true;
                for (int i = 0; i < 5; i++)
                {
                    var d = GoredollFactory.Spawn(Center + new Vector3(-4 + i * 2f, 0.2f, 1 + (i % 2) * 2), Random.Range(0f, 360f), "Passive");
                    if (d != null) { d.DisplayName = "Clubber"; if (d.Brain != null) d.Brain.ForceDance = true; }
                }
            }
            if (Time.time < nextBeat) return;
            nextBeat = Time.time + 60f / 128f;
            beat++;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            for (int i = 0; i < Tiles.Count; i++)
            {
                if (Tiles[i] == null) continue;
                var col = Cols[(i * 7 + beat * 3) % Cols.Length] * ((i + beat) % 3 == 0 ? 0.2f : 1f);
                mpb.SetColor("_Color", col);
                mpb.SetColor("_EmissionColor", col * 2f);
                Tiles[i].SetPropertyBlock(mpb);
            }
            for (int i = 0; i < Lights.Count; i++) if (Lights[i] != null) Lights[i].color = Cols[(i + beat) % Cols.Length];
        }
    }

    // ====================================================================== casino minigames

    public sealed class CasinoScreen : UIScreen
    {
        int tab, bet = 100;
        string result = "";
        Color resultCol = Color.white;
        // slots
        readonly int[] reels = { 0, 1, 2 }; float spinUntil = -1f;
        static readonly string[] Symbols = { "7", "BAR", "$", "*", "@" };
        // roulette
        int pick = 0; float wheelUntil = -1f; int landed = -1;
        // blackjack
        readonly List<int> player = new List<int>(), dealer = new List<int>(); bool bjActive;
        readonly System.Random rng = new System.Random();

        public override bool IsModal => true;
        public override void OnOpen() => AudioManager.Ui("ui_open");
        public override bool OnBack() { if (spinUntil > 0f || wheelUntil > 0f || bjActive) return true; UIRoot.Pop(); return true; }

        public override void Draw()
        {
            UI.Dim(0.7f);
            var r = UI.Centered(Mathf.Min(1100, UI.VW - 60), Mathf.Min(820, UI.VH - 40));
            UI.Panel(r, "Lucky Eclipse Casino");
            UI.Label(new Rect(r.xMax - 360, r.y + 18, 330, 44), "$" + OWSave.Cash.ToString("N0"), 30, new Color(0.45f, 1f, 0.5f), TextAnchor.MiddleRight, FontStyle.Normal, true, false, UI.HeavyFont);
            int nt = UI.Tabs(new Rect(r.x + 24, r.y + 80, r.width - 48, 50), tab, new[] { "SLOTS", "ROULETTE", "BLACKJACK" }, 20);
            if (nt != tab && spinUntil < 0f && wheelUntil < 0f && !bjActive) { tab = nt; result = ""; }
            var body = new Rect(r.x + 24, r.y + 150, r.width - 48, r.height - 260);
            // Bet selector
            int[] bets = { 100, 500, 1000, 5000 };
            for (int i = 0; i < bets.Length; i++)
                if (UI.Button(new Rect(body.x + i * 150, r.yMax - 90, 140, 56), "$" + bets[i].ToString("N0"), bet == bets[i] ? ButtonKind.Primary : ButtonKind.Normal, null, 18)) bet = bets[i];
            UI.Label(new Rect(body.x, body.yMax - 60, body.width, 50), result, 26, resultCol, TextAnchor.MiddleCenter, FontStyle.Bold);
            switch (tab)
            {
                case 0: Slots(body); break;
                case 1: Roulette(body); break;
                case 2: Blackjack(body); break;
            }
            if (UI.Button(new Rect(r.xMax - 244, r.yMax - 90, 220, 56), "LEAVE", ButtonKind.Normal, IconFactory.Get("arrow_left"))) OnBack();
        }

        bool Pay(int amount) { if (!OWSave.Spend(amount)) { result = "Not enough cash."; resultCol = UI.Bad; AudioManager.Ui("ui_error"); return false; } return true; }
        void Win(long amount, string text) { if (amount > 0) OWSave.Earn(amount, "Casino"); result = text; resultCol = amount > 0 ? UI.Good : UI.Bad; AudioManager.Ui(amount > 0 ? "cash" : "ui_error"); }

        void Slots(Rect b)
        {
            float w = 180, x0 = b.center.x - w * 1.5f - 20;
            bool spinning = spinUntil > 0f && Time.unscaledTime < spinUntil;
            for (int i = 0; i < 3; i++)
            {
                var rr = new Rect(x0 + i * (w + 20), b.y + 40, w, 220);
                UI.Rounded(rr, new Color(0.95f, 0.93f, 0.85f));
                int sym = spinning && Time.unscaledTime < spinUntil - (2 - i) * 0.35f ? (int)(Time.unscaledTime * 20f + i * 7) % Symbols.Length : reels[i];
                UI.Label(rr, Symbols[sym], 90, sym == 0 ? UI.Bad : new Color(0.15f, 0.15f, 0.2f), TextAnchor.MiddleCenter, FontStyle.Normal, false, false, UI.HeavyFont);
            }
            if (spinUntil > 0f && !spinning)
            {
                spinUntil = -1f;
                if (reels[0] == reels[1] && reels[1] == reels[2]) Win(bet * (reels[0] == 0 ? 25 : 10), reels[0] == 0 ? "JACKPOT! 777" : "THREE OF A KIND!");
                else if (reels[0] == reels[1] || reels[1] == reels[2]) Win(bet * 2, "A pair. Double your bet.");
                else Win(0, "No luck.");
            }
            if (UI.Button(new Rect(b.center.x - 150, b.y + 290, 300, 70), "SPIN  $" + bet.ToString("N0"), ButtonKind.Primary, IconFactory.Get("refresh"), 24, !spinning) && Pay(bet))
            {
                for (int i = 0; i < 3; i++) reels[i] = rng.Next(Symbols.Length);
                if (rng.NextDouble() < 0.12) reels[2] = reels[1] = reels[0];
                spinUntil = Time.unscaledTime + 1.6f; result = ""; AudioManager.Ui("ui_click");
            }
        }

        void Roulette(Rect b)
        {
            string[] picks = { "RED (x2)", "BLACK (x2)", "GREEN 0 (x14)" };
            for (int i = 0; i < 3; i++) if (UI.Button(new Rect(b.center.x - 340 + i * 230, b.y + 20, 220, 60), picks[i], pick == i ? ButtonKind.Primary : ButtonKind.Normal, null, 18, wheelUntil < 0f)) pick = i;
            bool spinning = wheelUntil > 0f && Time.unscaledTime < wheelUntil;
            var wheel = new Rect(b.center.x - 110, b.y + 100, 220, 220);
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(spinning ? Time.unscaledTime * 720f * Mathf.Clamp01(wheelUntil - Time.unscaledTime) : 0f, wheel.center * UI.Scale);
            UI.Tex(wheel, TextureFactory.Circle, new Color(0.3f, 0.05f, 0.05f));
            for (int i = 0; i < 18; i++)
            {
                var d = new Vector2(Mathf.Cos(i * Mathf.PI / 9f), Mathf.Sin(i * Mathf.PI / 9f)) * 85f;
                UI.Tex(new Rect(wheel.center.x + d.x - 14, wheel.center.y + d.y - 14, 28, 28), TextureFactory.Circle, i == 0 ? UI.Good : i % 2 == 0 ? UI.Bad : Color.black);
            }
            GUI.matrix = m;
            if (landed >= 0 && !spinning) UI.Label(new Rect(wheel.x, wheel.yMax + 4, wheel.width, 36), landed == 0 ? "0 GREEN" : landed + (landed % 2 == 0 ? " RED" : " BLACK"), 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (wheelUntil > 0f && !spinning)
            {
                wheelUntil = -1f;
                bool green = landed == 0, red = !green && landed % 2 == 0;
                if (pick == 2 && green) Win(bet * 14, "ZERO! x14!");
                else if ((pick == 0 && red) || (pick == 1 && !red && !green)) Win(bet * 2, "Winner!");
                else Win(0, "The house wins.");
            }
            if (UI.Button(new Rect(b.center.x - 150, b.y + 360, 300, 66), "SPIN  $" + bet.ToString("N0"), ButtonKind.Primary, null, 22, !spinning) && Pay(bet))
            {
                landed = rng.Next(37); wheelUntil = Time.unscaledTime + 2.4f; result = ""; AudioManager.Ui("ui_click");
            }
        }

        static int Score(List<int> h) { int s = 0, aces = 0; foreach (var c in h) { s += c == 1 ? 11 : Mathf.Min(10, c); if (c == 1) aces++; } while (s > 21 && aces-- > 0) s -= 10; return s; }
        static string Card(int c) => c == 1 ? "A" : c == 11 ? "J" : c == 12 ? "Q" : c == 13 ? "K" : c.ToString();
        int DrawCard() => rng.Next(1, 14);

        void Blackjack(Rect b)
        {
            void Hand(float y, string who, List<int> h, bool hide)
            {
                UI.Label(new Rect(b.x, y, 200, 40), who + (hide ? "" : "  (" + Score(h) + ")"), 22, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                for (int i = 0; i < h.Count; i++)
                {
                    var cr = new Rect(b.x + 220 + i * 100, y - 30, 86, 120);
                    UI.Rounded(cr, hide && i == 1 ? new Color(0.5f, 0.1f, 0.15f) : Color.white);
                    if (!(hide && i == 1)) UI.Label(cr, Card(h[i]), 40, i % 2 == 0 ? Color.black : UI.Bad, TextAnchor.MiddleCenter, FontStyle.Bold, false);
                }
            }
            Hand(b.y + 50, "DEALER", dealer, bjActive);
            Hand(b.y + 210, "YOU", player, false);
            float by = b.y + 330;
            if (!bjActive)
            {
                if (UI.Button(new Rect(b.center.x - 150, by, 300, 64), "DEAL  $" + bet.ToString("N0"), ButtonKind.Primary, null, 22) && Pay(bet))
                {
                    player.Clear(); dealer.Clear(); player.Add(DrawCard()); player.Add(DrawCard()); dealer.Add(DrawCard()); dealer.Add(DrawCard());
                    bjActive = true; result = "";
                    if (Score(player) == 21) { bjActive = false; Win(bet * 5 / 2, "BLACKJACK!"); }
                }
                return;
            }
            if (UI.Button(new Rect(b.center.x - 320, by, 300, 64), "HIT", ButtonKind.Normal, null, 22)) { player.Add(DrawCard()); if (Score(player) > 21) { bjActive = false; Win(0, "Bust! " + Score(player)); } }
            if (UI.Button(new Rect(b.center.x + 20, by, 300, 64), "STAND", ButtonKind.Primary, null, 22))
            {
                while (Score(dealer) < 17) dealer.Add(DrawCard());
                int p = Score(player), d = Score(dealer);
                bjActive = false;
                if (d > 21 || p > d) Win(bet * 2, "You win! " + p + " vs " + d);
                else if (p == d) Win(bet, "Push. Bet returned.");
                else Win(0, "Dealer wins. " + d + " vs " + p);
            }
        }
    }

    // ====================================================================== gang territories

    /// <summary>
    /// Gang territories: Southside, the Docks and Industrial belong to gangs. Walk in and the gang sends a squad (host);
    /// kill 6 gang members there and the district is yours: $1,500 every 10 minutes while you play. Ownership is saved.
    /// </summary>
    public sealed class OWTerritories : MonoBehaviour
    {
        public static readonly string[] Gangs = { "Southside", "The Docks", "Industrial" };
        static readonly Dictionary<string, float> lastSquad = new Dictionary<string, float>();
        static readonly Dictionary<Goredoll, string> members = new Dictionary<Goredoll, string>();
        float nextIncome, nextCheck;
        string shown;

        public static bool Owned(string d) => PlayerPrefs.GetInt("ow_turf_" + d, 0) == 1;
        static int Kills(string d) => PlayerPrefs.GetInt("ow_turf_kills_" + d, 0);

        void OnEnable() { Goredoll.Died += Died; nextIncome = Time.time + 600f; members.Clear(); lastSquad.Clear(); UIRoot.HudLayer += Hud; }
        void OnDisable() { Goredoll.Died -= Died; UIRoot.HudLayer -= Hud; }

        void Update()
        {
            if (!OWDirector.Active) return;
            if (Time.time > nextIncome)
            {
                nextIncome = Time.time + 600f;
                int n = 0; foreach (var g in Gangs) if (Owned(g)) n++;
                if (n > 0) OWSave.Earn(1500 * n, "Territory income");
            }
            if (Time.time < nextCheck || NetState.IsClient) return;
            nextCheck = Time.time + 3f;
            foreach (var id in OWDirector.PlayerIds())
            {
                if (!NetManager.I.OWPos(id, out var at) || at.y < -20f) continue;
                var d = OWMap.District(at);
                if (System.Array.IndexOf(Gangs, d) < 0) continue;
                if (lastSquad.TryGetValue(d, out var t) && Time.time - t < 75f) continue;
                int alive = 0; foreach (var kv in members) if (kv.Key != null && !kv.Key.IsDead && kv.Value == d) alive++;
                if (alive >= 3) continue;
                lastSquad[d] = Time.time;
                for (int i = 0; i < 3; i++)
                {
                    var pos = at + Quaternion.Euler(0, i * 120 + Random.Range(0, 60), 0) * Vector3.forward * Random.Range(16f, 24f);
                    if (Physics.Raycast(pos + Vector3.up * 30f, Vector3.down, out var hit, 40f, Layers.WorldMask, QueryTriggerInteraction.Ignore) && hit.point.y < 1f) pos = hit.point + Vector3.up * 0.1f; else continue;
                    var g = GoredollFactory.Spawn(pos, 0f, i == 0 ? "Gunman" : NpcTypes.Preset("prisoner"));
                    if (g != null) { g.DisplayName = d + " Gang"; members[g] = d; }
                }
                NetManager.I.OWEvent(id, OWEv.Toast, 255, "You're on " + d + " gang turf. Kill 6 of them to take it over.");
            }
        }

        static void Died(Goredoll d, object attacker)
        {
            if (NetState.IsClient || d == null || !members.TryGetValue(d, out var district)) return;
            members.Remove(d);
            byte pid = OWDirector.PlayerOf(attacker);
            if (pid != 255) GameModes.Send(pid, "turf:" + district);
        }

        /// <summary>A gang member you killed (sent by the host through the mode channel).</summary>
        public static void OnKill(string district)
        {
            if (Owned(district)) return;
            int k = Kills(district) + 1;
            PlayerPrefs.SetInt("ow_turf_kills_" + district, k);
            if (k >= 6) { PlayerPrefs.SetInt("ow_turf_" + district, 1); Toasts.Show("TERRITORY TAKEN", district + " is yours: +$1,500 every 10 minutes.", UI.Good); AudioManager.Ui("ui_success"); }
            else GameEvents.ShowToast(district + " gang: " + k + "/6");
            PlayerPrefs.Save();
        }

        void Hud()
        {
            if (!OWDirector.Active || GameManager.I == null || GameManager.I.HudHidden) return;
            var p = PlayerController.Instance;
            if (p == null) return;
            var d = OWMap.District(p.transform.position);
            if (System.Array.IndexOf(Gangs, d) < 0 || p.transform.position.y < -20f) { shown = null; return; }
            shown = d;
            string t = Owned(d) ? d + ": YOUR TURF" : d + ": gang turf (" + Kills(d) + "/6)";
            UI.Label(new Rect(UI.VW - 520, UI.VH - 60, 490, 30), t, 17, Owned(d) ? UI.Good : UI.Bad, TextAnchor.MiddleRight, FontStyle.Bold);
        }
    }

    // ====================================================================== boats

    /// <summary>A speedboat for the sea east of Port Eclipse (host / singleplayer drives it).</summary>
    public sealed class Boat : MonoBehaviour, IInteractable
    {
        public static readonly List<Boat> All = new List<Boat>();
        public static Boat Local;
        Rigidbody rb; Transform seat; float orbit; Vector2 input; bool driven; AudioSource engine;
        const float WaterY = -0.6f;
        public string Prompt => driven ? "" : "Drive the boat";

        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = Machines.Root("Speedboat", pos, rot, false, 800f);
            var r = go.GetComponent<Rigidbody>(); r.useGravity = false; r.drag = 0.8f; r.angularDrag = 2f; r.interpolation = RigidbodyInterpolation.Interpolate;
            Machines.Part(go.transform, "Hull", mb =>
            {
                mb.Color = Color.white; mb.Gloss = 0.7f;
                mb.Prism(new[] { new Vector2(-1.1f, 0.5f), new Vector2(1.1f, 0.5f), new Vector2(0.9f, -0.3f), new Vector2(-0.9f, -0.3f) }, 5f, new Vector3(0, 0.3f, 0));
                mb.SetColor(new Color(0.85f, 0.15f, 0.12f), 0.6f); mb.Box(new Vector3(0, 0.35f, 2.6f), new Vector3(1.4f, 0.5f, 0.6f));
                mb.SetColor(new Color(0.1f, 0.12f, 0.15f), 0.95f); mb.Box(new Vector3(0, 1.0f, 0.7f), new Vector3(1.6f, 0.6f, 0.08f));
                mb.SetColor(new Color(0.15f, 0.15f, 0.17f), 0.4f); mb.Box(new Vector3(0, 0.6f, -2.6f), new Vector3(0.5f, 0.7f, 0.4f));
            }, null, false);
            var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.3f, 0); bc.size = new Vector3(2.2f, 0.9f, 5.6f);
            var b = go.AddComponent<Boat>();
            b.rb = r;
            b.seat = new GameObject("Seat").transform; b.seat.SetParent(go.transform, false); b.seat.localPosition = new Vector3(-0.4f, 0.9f, -0.3f);
            b.engine = go.AddComponent<AudioSource>(); b.engine.clip = SoundBank.Get("car_engine"); b.engine.loop = true; b.engine.spatialBlend = 1f; b.engine.maxDistance = 80f; b.engine.volume = 0f;
            if (b.engine.clip != null) b.engine.Play();
            return go;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() { All.Remove(this); if (Local == this) Local = null; }

        public void Interact(GameObject user)
        {
            if (driven) return;
            if (NetState.IsClient) { GameEvents.ShowToast("Only the host can drive boats for now."); return; }
            driven = true; Local = this;
            GameEvents.ShowToast("W/S throttle · A/D steer · E get out");
        }

        void Leave()
        {
            driven = false; Local = null;
            var p = PlayerController.Instance;
            if (p != null) p.Teleport(transform.position + transform.right * -2.5f + Vector3.up * 1.2f);
        }

        public void DriveTick(PlayerController p, InputFrame f, float dt)
        {
            if (this == null) { Local = null; return; }
            if (f.Use) { Leave(); return; }
            input = f.Move;
            if (p.CC.enabled) p.CC.enabled = false;
            p.transform.SetPositionAndRotation(seat.position - Vector3.up * 0.9f, Quaternion.Euler(0, transform.eulerAngles.y, 0));
            if (VRManager.Active) return;
            orbit = Mathf.Clamp(Mathf.Lerp(orbit, 0f, dt * 1.2f) + f.Look.x * 0.4f, -150f, 150f);
            var target = transform.position + Vector3.up * 1.2f;
            var cp = target - Quaternion.Euler(12f, transform.eulerAngles.y + orbit, 0) * Vector3.forward * 9f + Vector3.up * 1.5f;
            p.CamPivot.position = Vector3.Lerp(p.CamPivot.position, cp, 1f - Mathf.Exp(-8f * dt));
            p.CamPivot.rotation = Quaternion.LookRotation(target - p.CamPivot.position);
            p.Cam.transform.localPosition = Vector3.zero; p.Cam.transform.localRotation = Quaternion.identity;
        }

        void FixedUpdate()
        {
            if (rb == null || NetState.IsClient) return;
            // Buoyancy: float at the waterline (gravity only above it), bob a little.
            float depth = WaterY - transform.position.y;
            if (depth > -0.2f) rb.AddForce(Vector3.up * (Mathf.Clamp(depth + 0.2f, 0f, 1.5f) * 22f - 2f), ForceMode.Acceleration);
            else rb.AddForce(Physics.gravity, ForceMode.Acceleration);
            var up = Vector3.Cross(transform.up, Vector3.up);
            rb.AddTorque(up * 30f - rb.angularVelocity * 2f, ForceMode.Acceleration);
            if (!driven) { input = Vector2.zero; }
            var fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            rb.AddForce(fwd * input.y * 16f, ForceMode.Acceleration);
            rb.AddTorque(Vector3.up * input.x * 1.8f * Mathf.Clamp(rb.velocity.magnitude / 4f, 0.3f, 1f), ForceMode.Acceleration);
            if (engine != null) { engine.volume = (driven ? 0.6f : 0f) * Settings.Data.sfxVolume * Settings.Data.masterVolume; engine.pitch = 0.8f + Mathf.Clamp01(rb.velocity.magnitude / 20f); }
            if (driven && rb.velocity.magnitude > 4f && Random.value < 0.4f) FX.Smoke(transform.position - transform.forward * 2.8f + Vector3.up * 0.2f, Vector3.up + Random.insideUnitSphere, 0.8f, new Color(0.85f, 0.9f, 0.95f, 0.5f), 1.2f);
        }
    }
}
