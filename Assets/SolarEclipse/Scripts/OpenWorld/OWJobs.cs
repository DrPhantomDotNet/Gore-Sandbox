using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Street jobs you do on your own (your phone's JOBS app): deliveries (pick up a package, drop it off before the
    /// timer runs out) and street races (stop a car on the checkered line in Industrial, then hit every checkpoint).
    /// Bounties go through the host (OWDirector.StartBounty) because the target is a real armed Goredoll.
    /// </summary>
    public static class OWJobs
    {
        public enum Kind { None, Delivery, Race }
        public static Kind Active { get; private set; }
        public static Vector3 Target { get; private set; }
        public static float TimeLeft { get; private set; }
        public static string Title { get; private set; }
        static int stage, checkpoint;
        static Vector3 dropOff;
        static long reward;
        static float raceStart, startCooldown;

        static readonly string[] Stops = { "BULLET BROS.", "THREADS", "MASK MART", "ECLIPSE MOTORS", "ECLIPSE CUSTOMS", "ECLIPSE BOUTIQUE", "Hospital", "Police Station", "Gas Station", "Scrapyard", "Factory", "Docks", "The Meridian", "Bank", "Jewelry" };
        static readonly Vector3[] RaceRoute =
        {
            new Vector3(120, 0, 60), new Vector3(120, 0, -120), new Vector3(0, 0, -120), new Vector3(-60, 0, -60),
            new Vector3(-60, 0, 120), new Vector3(60, 0, 120), new Vector3(60, 0, 60),
        };
        const float RacePar = 100f;

        public static void Reset() { Active = Kind.None; Title = null; }

        public static string Objective
        {
            get
            {
                switch (Active)
                {
                    case Kind.Delivery: return (stage == 0 ? "Pick up the package at " : "Deliver the package to ") + Title + "  " + Mathf.CeilToInt(TimeLeft) + "s";
                    case Kind.Race: return "Street race: checkpoint " + (checkpoint + 1) + "/" + (RaceRoute.Length + 1) + "  " + (Time.time - raceStart).ToString("0.0") + "s (par " + RacePar + "s)";
                }
                return null;
            }
        }

        static Vector3 Me()
        {
            var car = OWCar.LocalDriving;
            if (car != null) return car.transform.position;
            var p = PlayerController.Instance;
            return p != null ? p.transform.position : Vector3.zero;
        }

        static string FarStop(Vector3 from, float min, out Vector3 pos)
        {
            for (int t = 0; t < 30; t++)
            {
                var n = Stops[Random.Range(0, Stops.Length)];
                if (!OWMap.Places.TryGetValue(n, out pos)) continue;
                if (MathX.Flat(pos - from).magnitude >= min) return n;
            }
            pos = OWMap.Place("Hospital");
            return "Hospital";
        }

        public static void StartDelivery()
        {
            if (Active != Kind.None) { GameEvents.ShowToast("Finish (or cancel) your current job first."); return; }
            var me = Me();
            string a = FarStop(me, 60f, out var pa);
            string b = FarStop(pa, 140f, out var pb);
            Active = Kind.Delivery; stage = 0; Title = a; Target = pa; dropOff = pb;
            TimeLeft = 90f + MathX.Flat(pa - me).magnitude * 0.6f;
            reward = 1500 + (long)(MathX.Flat(pb - pa).magnitude * 14f);
            OWDirector.Waypoint = pa;
            Toasts.Show("DELIVERY", "Pick up a package at " + a + ". Pays $" + reward.ToString("N0") + ".", UI.Accent2);
            AudioManager.Ui("phone_ping");
        }

        public static void StartRace()
        {
            if (Active != Kind.None) return;
            if (OWCar.LocalDriving == null) { GameEvents.ShowToast("You need a car for a street race."); return; }
            Active = Kind.Race; checkpoint = 0; raceStart = Time.time; Title = "Street race";
            Target = RaceRoute[0];
            OWDirector.Waypoint = Target;
            Toasts.Show("STREET RACE", "GO! Hit every checkpoint. Beat " + RacePar + "s for the big prize.", UI.Accent2);
            AudioManager.Ui("ui_success");
        }

        public static void Cancel() { if (Active != Kind.None) { Fail("Job cancelled."); } }

        public static void Fail(string why)
        {
            if (Active == Kind.None) return;
            Active = Kind.None;
            OWDirector.Waypoint = null;
            startCooldown = Time.time + 8f;
            Toasts.Show("JOB FAILED", why, UI.Bad);
            AudioManager.Ui("ui_error");
        }

        static void Pay(long amount, string reason)
        {
            Active = Kind.None;
            OWDirector.Waypoint = null;
            startCooldown = Time.time + 8f;
            OWDirector.OnEvent(OWEv.Reward, 255, reason, amount, default, 0);
        }

        public static void Tick(PlayerController p)
        {
            var me = Me();
            // Street race start line (Industrial lot): stop a car on it.
            if (Active == Kind.None && OWCar.LocalDriving != null && Time.time > startCooldown && OWMap.Places.TryGetValue("Race Start", out var rs)
                && MathX.Flat(me - rs).magnitude < 8f && OWCar.LocalDriving.Speed < 3f)
                StartRace();

            switch (Active)
            {
                case Kind.Delivery:
                    TimeLeft -= Time.deltaTime;
                    if (TimeLeft <= 0f) { Fail("Too slow. The client found another courier."); return; }
                    if (MathX.Flat(me - Target).magnitude < 5f && Mathf.Abs(me.y - Target.y) < 4f)
                    {
                        if (stage == 0)
                        {
                            stage = 1; Target = dropOff;
                            Title = OWMap.Places.ContainsValue(dropOff) ? NameOf(dropOff) : "the drop-off";
                            TimeLeft = 60f + MathX.Flat(dropOff - me).magnitude * 0.55f;
                            OWDirector.Waypoint = dropOff;
                            AudioManager.Ui("phone_ping");
                            GameEvents.ShowToast("Got the package. Deliver it to " + Title + ".");
                        }
                        else Pay(reward, "Delivery job");
                    }
                    break;
                case Kind.Race:
                    if (OWCar.LocalDriving == null) { Fail("You left your car."); return; }
                    if (Time.time - raceStart > 300f) { Fail("Race timed out."); return; }
                    if (MathX.Flat(me - Target).magnitude < 9f)
                    {
                        AudioManager.Ui("ui_tab");
                        checkpoint++;
                        if (checkpoint > RaceRoute.Length)
                        {
                            float t = Time.time - raceStart;
                            bool win = t <= RacePar;
                            if (win) { Stats.Add(OWDirector.StatRaces); OWSave.D.races++; OWSave.Save(); }
                            Toasts.Show(win ? "YOU WIN!" : "FINISHED", t.ToString("0.0") + "s" + (win ? " - under par!" : " - par is " + RacePar + "s"), win ? UI.Good : UI.Warn);
                            Pay(win ? 9000 : 2500, "Street race");
                            return;
                        }
                        Target = checkpoint < RaceRoute.Length ? RaceRoute[checkpoint] : OWMap.Place("Race Start");
                        OWDirector.Waypoint = Target;
                    }
                    break;
            }
        }

        static string NameOf(Vector3 v)
        {
            foreach (var kv in OWMap.Places) if (kv.Value == v) return kv.Key;
            return "the drop-off";
        }
    }
}
