using System.Collections.Generic;
using SolarEclipse.Net;
using UnityEngine;

namespace SolarEclipse
{
    public sealed class OWHeistStep { public string Place, Label; public float Seconds; }

    public sealed class OWHeistDef
    {
        public string Id, Name, Desc, Start, Escape;
        public int Payout, Stars, Guards;
        public string GuardPreset = "Gunman";
        public bool Truck;
        public OWHeistStep[] Steps;
    }

    /// <summary>
    /// Heists (host runs them, everyone sees the state). One heist at a time per server:
    ///   LOBBY  - join at the heist board (or the phone), press READY. Multiplayer needs 2+ ready players; singleplayer can go solo (60% payout).
    ///   TRAVEL - get to the place. HIT - the alarm goes off (wanted stars, police), guards fight back, stand at the loot points
    ///   (more crew = faster). ESCAPE - reach the escape point. Everyone in the crew gets a share (+10% per extra member).
    /// Fails after 10 minutes or when the whole crew is dead at once.
    /// </summary>
    public static class OWHeists
    {
        public const byte Idle = 0, Lobby = 1, Travel = 2, Hit = 3, Escape = 4;

        public static readonly OWHeistDef[] Defs =
        {
            new OWHeistDef
            {
                Id = "gas", Name = "Gas Station Stickup", Payout = 9000, Stars = 1, Guards = 1, Start = "Gas Station", Escape = "Gang Hideout",
                Desc = "Southside. Empty the register at the 24/7 gas station and run before the cops show. A good first job.",
                Steps = new[] { new OWHeistStep { Place = "Gas Register", Label = "Empty the register", Seconds = 10f } },
            },
            new OWHeistDef
            {
                Id = "jewel", Name = "Diamond District Smash", Payout = 38000, Stars = 2, Guards = 2, Start = "Jewelry", Escape = "Scrapyard",
                Desc = "Downtown. Smash all six display cases in the jewelry store. Two security guards.",
                Steps = new[]
                {
                    new OWHeistStep { Place = "Jewel 0", Label = "Smash case 1 / 6", Seconds = 4f }, new OWHeistStep { Place = "Jewel 1", Label = "Smash case 2 / 6", Seconds = 4f },
                    new OWHeistStep { Place = "Jewel 2", Label = "Smash case 3 / 6", Seconds = 4f }, new OWHeistStep { Place = "Jewel 3", Label = "Smash case 4 / 6", Seconds = 4f },
                    new OWHeistStep { Place = "Jewel 4", Label = "Smash case 5 / 6", Seconds = 4f }, new OWHeistStep { Place = "Jewel 5", Label = "Smash case 6 / 6", Seconds = 4f },
                },
            },
            new OWHeistDef
            {
                Id = "truck", Name = "Armored Truck", Payout = 60000, Stars = 3, Guards = 3, GuardPreset = "Soldier", Start = "Factory", Escape = "Docks Boat", Truck = true,
                Desc = "Industrial. An armored cash truck is parked at the factory. Blow it open (it takes a beating), then grab the cash.",
                Steps = new[] { new OWHeistStep { Place = "truck", Label = "Blow open the truck", Seconds = 0f }, new OWHeistStep { Place = "truck", Label = "Grab the cash bags", Seconds = 14f } },
            },
            new OWHeistDef
            {
                Id = "docks", Name = "Docks Shipment", Payout = 85000, Stars = 2, Guards = 4, Start = "Docks", Escape = "Scrapyard",
                Desc = "The Docks. A gang's blue container is full of cargo worth a fortune. Four armed gang members guard it.",
                Steps = new[] { new OWHeistStep { Place = "Docks Shipment", Label = "Cut open the blue container", Seconds = 22f } },
            },
            new OWHeistDef
            {
                Id = "bank", Name = "The Central Bank", Payout = 150000, Stars = 4, Guards = 5, GuardPreset = "Soldier", Start = "Bank", Escape = "Gang Hideout",
                Desc = "Downtown. The big one: drill the vault door, load the gold, and get across town alive with SWAT on your tail.",
                Steps = new[] { new OWHeistStep { Place = "Bank Vault", Label = "Drill the vault door", Seconds = 40f }, new OWHeistStep { Place = "Bank Loot", Label = "Load the gold", Seconds = 15f } },
            },
        };

        // Shared state (host writes, clients read).
        public static int Index = -1;
        public static byte Phase;
        public static int Step;
        public static float Progress, Timer;
        public static readonly List<byte> Crew = new List<byte>();
        public static readonly HashSet<byte> Ready = new HashSet<byte>();
        public static ushort TruckId;

        public static OWHeistDef Current => Index >= 0 && Index < Defs.Length ? Defs[Index] : null;
        public static bool InCrew(byte id) => Crew.Contains(id);
        public static bool InActiveCrew(byte id) => Phase >= Hit && Crew.Contains(id);
        public static bool MeInCrew => InCrew(OWDirector.MyId);
        public static int MinCrew => NetState.Active ? 2 : 1;

        static OWCar truck;
        static float lastPing;

        public static void Reset()
        {
            Index = -1; Phase = Idle; Step = 0; Progress = 0; Timer = 0; Crew.Clear(); Ready.Clear(); TruckId = 0; truck = null;
        }

        // ------------------------------------------------------------------ sync

        public static void Write(NetWriter w)
        {
            w.SByte((sbyte)Index); w.Byte(Phase); w.Byte((byte)Step); w.Float(Progress); w.Float(Timer);
            w.Byte((byte)Crew.Count);
            foreach (var id in Crew) { w.Byte(id); w.Bool(Ready.Contains(id)); }
            w.UShort(TruckId);
        }

        public static void Read(NetReader r)
        {
            Index = r.SByte(); Phase = r.Byte(); Step = r.Byte(); Progress = r.Float(); Timer = r.Float();
            Crew.Clear(); Ready.Clear();
            int n = r.Byte();
            for (int i = 0; i < n; i++) { byte id = r.Byte(); bool rd = r.Bool(); Crew.Add(id); if (rd) Ready.Add(id); }
            TruckId = r.UShort();
        }

        // ------------------------------------------------------------------ what the crew sees

        /// <summary>Where the crew should go right now (null if nowhere).</summary>
        public static Vector3? Target
        {
            get
            {
                var d = Current;
                if (d == null) return null;
                switch (Phase)
                {
                    case Lobby: return OWMap.Place("Heist Board");
                    case Travel: return OWMap.Place(d.Start);
                    case Hit:
                    {
                        var s = d.Steps[Mathf.Clamp(Step, 0, d.Steps.Length - 1)];
                        if (s.Place == "truck") { var t = TruckCar; return t != null ? t.transform.position : OWMap.Place(d.Start); }
                        return OWMap.Place(s.Place);
                    }
                    case Escape: return OWMap.Place(d.Escape);
                }
                return null;
            }
        }

        static OWCar TruckCar
        {
            get
            {
                if (truck != null) return truck;
                if (TruckId == 0) return null;
                var ne = NetEntity.Get(TruckId);
                return ne != null ? ne.GetComponent<OWCar>() : null;
            }
        }

        public static string Objective
        {
            get
            {
                var d = Current;
                if (d == null || !MeInCrew) return null;
                switch (Phase)
                {
                    case Lobby: return d.Name + ": lobby " + Ready.Count + "/" + Crew.Count + " ready" + (Crew.Count < MinCrew ? " - waiting for " + (MinCrew - Crew.Count) + " more player" + (MinCrew - Crew.Count == 1 ? "" : "s") : "");
                    case Travel: return d.Name + ": go to the " + d.Start;
                    case Hit: return d.Steps[Mathf.Clamp(Step, 0, d.Steps.Length - 1)].Label + (Progress > 0f ? "  " + Mathf.RoundToInt(Progress * 100f) + "%" : "");
                    case Escape: return "Escape to the " + d.Escape + "!";
                }
                return null;
            }
        }

        // ------------------------------------------------------------------ host: lobby

        static void Tell(byte id, string text, int tone = 0) => NetManager.I.OWEvent(id, OWEv.Heist, 255, text, tone);
        static void TellCrew(string text, int tone = 0) { foreach (var id in Crew.ToArray()) Tell(id, text, tone); }

        public static void Join(byte pid, int index)
        {
            if (index < 0 || index >= Defs.Length) return;
            if (Crew.Contains(pid)) { if (Index == index) return; Leave(pid); }
            if (Phase == Idle) { Index = index; Phase = Lobby; Crew.Clear(); Ready.Clear(); Timer = 0f; }
            else if (Index != index || Phase != Lobby) { Tell(pid, "A crew is already doing " + Current.Name + ". Wait for them to finish.", 2); return; }
            if (Crew.Count >= 4) { Tell(pid, "That crew is full (4 players).", 2); return; }
            Crew.Add(pid);
            OWDirector.MarkDirty();
            NetManager.I.OWEvent(255, OWEv.Toast, 255, NetManager.I.OWName(pid) + " joined the " + Current.Name + " crew (" + Crew.Count + "/4). Heist board: Southside hangout.");
        }

        public static void Leave(byte pid)
        {
            if (!Crew.Remove(pid)) return;
            Ready.Remove(pid);
            if (Crew.Count == 0) End(false, null);
            OWDirector.MarkDirty();
        }

        public static void SetReady(byte pid, bool ready)
        {
            if (Phase != Lobby || !Crew.Contains(pid)) return;
            if (ready) Ready.Add(pid); else Ready.Remove(pid);
            OWDirector.MarkDirty();
            if (Crew.Count >= MinCrew && Ready.Count == Crew.Count) Begin();
            else if (ready && Crew.Count < MinCrew) TellCrew("Waiting for at least " + MinCrew + " players in the crew (" + Crew.Count + "/" + MinCrew + ").");
        }

        static void Begin()
        {
            var d = Current;
            Phase = Travel; Step = 0; Progress = 0f; Timer = 0f;
            if (d.Truck)
            {
                var rb = PropFactory.Spawn(OWCar.DefFor("c_van", "u_paint_black", "", false, true, false), OWMap.Place(d.Start) + new Vector3(0, 0.6f, -2f), Quaternion.Euler(0, 90f, 0));
                truck = rb != null ? rb.GetComponent<OWCar>() : null;
                if (truck != null) { truck.MaxHealth *= 3f; truck.Health = truck.MaxHealth; var ne = truck.GetComponent<NetEntity>(); TruckId = ne != null ? ne.Id : (ushort)0; }
            }
            TellCrew(d.Name + " is on. Go to the " + d.Start + " (yellow marker)." + (!NetState.Active ? " Solo: 60% payout." : ""), 0);
            OWDirector.MarkDirty();
        }

        // ------------------------------------------------------------------ host: running

        static bool CrewNear(Vector3 p, float r, out int count)
        {
            count = 0;
            foreach (var id in Crew)
                if (!OWDirector.IsDead(id) && NetManager.I.OWPos(id, out var at) && (at - p).sqrMagnitude < r * r) count++;
            return count > 0;
        }

        public static void HostTick(float dt)
        {
            if (Phase == Idle) return;
            var nm = NetManager.I;
            // Players who left the server leave the crew.
            foreach (var id in Crew.ToArray()) if (!nm.OWHasPlayer(id)) Leave(id);
            if (Phase == Idle || Current == null) return;
            var d = Current;
            if (Phase == Lobby) return;
            Timer += dt;
            if (Timer > 600f) { End(false, "Out of time. The heist failed."); return; }
            if (Phase >= Hit)
            {
                bool allDead = true;
                foreach (var id in Crew) if (!OWDirector.IsDead(id)) { allDead = false; break; }
                if (allDead) { End(false, "The whole crew went down. The heist failed."); return; }
            }

            switch (Phase)
            {
                case Travel:
                    if (CrewNear(OWMap.Place(d.Start), d.Truck ? 26f : 20f, out _)) StartHit();
                    break;
                case Hit:
                {
                    var s = d.Steps[Mathf.Clamp(Step, 0, d.Steps.Length - 1)];
                    float before = Progress;
                    if (s.Place == "truck" && s.Seconds <= 0f)
                    {
                        var t = TruckCar;
                        Progress = t == null || t.Wrecked ? 1f : 1f - Mathf.Clamp01(t.Health / Mathf.Max(1f, t.MaxHealth));
                    }
                    else
                    {
                        var target = Target ?? OWMap.Place(s.Place);
                        if (CrewNear(target, 3.6f, out int n)) Progress += dt / Mathf.Max(0.5f, s.Seconds) * Mathf.Min(n, 3);
                        if (Time.time - lastPing > 2.5f && n > 0) { lastPing = Time.time; AudioManager.Play(Step == 0 && d.Id == "bank" ? "sg_tick" : "metal_impact", target, 0.6f); }
                    }
                    if (Mathf.Abs(Progress - before) > 0.04f || Mathf.FloorToInt(Progress * 10) != Mathf.FloorToInt(before * 10)) OWDirector.MarkDirty();
                    if (Progress >= 1f)
                    {
                        Progress = 0f; Step++;
                        if (Step >= d.Steps.Length) { Phase = Escape; TellCrew("Got it! Now escape to the " + d.Escape + ".", 0); }
                        else TellCrew(d.Steps[Step].Label + ".");
                        OWDirector.MarkDirty();
                    }
                    break;
                }
                case Escape:
                    if (CrewNear(OWMap.Place(d.Escape), 14f, out _)) Succeed();
                    break;
            }
        }

        static void StartHit()
        {
            var d = Current;
            Phase = Hit; Step = 0; Progress = 0f;
            var at = OWMap.Place(d.Steps[0].Place == "truck" ? d.Start : d.Steps[0].Place);
            for (int i = 0; i < d.Guards; i++)
            {
                var off = Quaternion.Euler(0, i * 360f / Mathf.Max(1, d.Guards), 0) * Vector3.forward * Random.Range(3f, 6f);
                var pos = at + off;
                if (Physics.CheckCapsule(pos + Vector3.up * 0.5f, pos + Vector3.up * 1.6f, 0.35f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) pos = at + Vector3.up * 0.1f;
                OWDirector.SpawnGuard(pos, d.GuardPreset);
            }
            foreach (var id in Crew) OWDirector.AddWanted(id, d.Stars, "Alarm at the " + d.Start);
            AudioManager.Play("buzzer", at + Vector3.up * 3f, 1f, 0.8f, 1f, 10f, 120f);
            TellCrew("ALARM! " + d.Steps[0].Label + " - the police are on the way.", 2);
            OWDirector.MarkDirty();
        }

        static void Succeed()
        {
            var d = Current;
            int n = Crew.Count;
            float total = d.Payout * (NetState.Active ? 1f : 0.6f) * (1f + 0.1f * (n - 1));
            long each = (long)Mathf.Min(OWSave.MaxPayout, total / Mathf.Max(1, n));
            foreach (var id in Crew.ToArray()) NetManager.I.OWEvent(id, OWEv.Reward, 255, "Heist: " + d.Name, each);
            NetManager.I.OWEvent(255, OWEv.Toast, 255, "The " + d.Name + " crew got away with $" + (each * n).ToString("N0") + "!");
            End(true, null);
        }

        static void End(bool success, string why)
        {
            if (why != null) TellCrew(why, 2);
            if (truck != null && !truck.Wrecked) Object.Destroy(truck.gameObject);
            OWDirector.DespawnGuards();
            Reset();
            OWDirector.MarkDirty();
        }
    }
}
