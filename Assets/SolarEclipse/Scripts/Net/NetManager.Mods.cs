using System.Collections.Generic;
using SolarEclipse.Mods;
using SolarEclipse.Net;
using UnityEngine;

namespace SolarEclipse.Mods
{
    /// <summary>
    /// Modded weapons in multiplayer. The host decides ("Allow modded weapons", off by default). When a player equips a
    /// modded weapon, its package goes to the host, which validates + clamps it with the same anti-cheat rules and
    /// relays ITS copy to everyone, so all players build the same weapon and damage always uses the host's numbers.
    /// Modded weapons get network indices from the host's session list (BuiltinCount + slot), so players with
    /// different mods installed still agree on what an index means.
    /// </summary>
    public static class ModNet
    {
        public const int MaxSessionMods = 40;
        internal static bool clientAllowed;
        internal static readonly List<string> Slots = new List<string>(); // def ids, in the host's order
        static readonly HashSet<string> requested = new HashSet<string>();
        static bool wasActive;

        public static bool Allowed => NetState.IsHost ? NetManager.I != null && NetManager.I.Server.AllowMods : clientAllowed;

        public static void Tick()
        {
            bool active = NetState.Active;
            if (active != wasActive) { wasActive = active; Reset(); }
            if (active && NetState.IsHost && NetManager.I != null) NetManager.I.HostModTick();
        }

        static void Reset()
        {
            Slots.Clear();
            requested.Clear();
            clientAllowed = false;
            NetManager.ResetModSession();
            ModWeapons.EndSession();
        }

        static int Base => ModWeapons.BuiltinCount;

        /// <summary>Catalog def -> network index (-1 = this modded weapon has no session slot yet).</summary>
        public static int ToNet(WeaponDef def)
        {
            int i = WeaponCatalog.All.IndexOf(def);
            if (i < 0) return -1;
            if (i < Base || !NetState.Active) return i;
            int s = Slots.IndexOf(def.Id);
            return s < 0 || Base + s > 254 ? -1 : Base + s;
        }

        /// <summary>Network index -> catalog def (null = unknown).</summary>
        public static WeaponDef FromNet(int net)
        {
            if (net < 0) return null;
            if (net < Base) return net < WeaponCatalog.All.Count ? WeaponCatalog.All[net] : null;
            int s = net - Base;
            if (s >= Slots.Count || string.IsNullOrEmpty(Slots[s])) return null;
            var d = WeaponCatalog.Get(Slots[s]);
            return d != null && ModWeapons.IsMod(d) ? d : null;
        }

        public static int LocalIndex(int net)
        {
            var d = FromNet(net);
            return d == null ? -1 : WeaponCatalog.All.IndexOf(d);
        }

        /// <summary>Called when a modded weapon is equipped or fired: make sure the host (and so everyone) has it.</summary>
        public static void Share(WeaponDef def)
        {
            if (!NetState.Active || !ModWeapons.IsMod(def) || !Allowed || NetManager.I == null) return;
            if (Slots.Contains(def.Id) || requested.Contains(def.Id)) return;
            var p = ModWeapons.Package(def);
            if (p == null) return;
            requested.Add(def.Id);
            NetManager.I.ShareModWeapon(p);
        }

        public static void NoteUsed(WeaponDef def) => Share(def);

        internal static void SetSlot(int slot, string defId)
        {
            while (Slots.Count <= slot) Slots.Add("");
            Slots[slot] = defId;
        }
    }
}

namespace SolarEclipse
{
    public sealed partial class NetManager
    {
        // Host bookkeeping for modded weapons (see Mods/ModNet above).
        static readonly List<byte[]> modSlotData = new List<byte[]>();
        static readonly HashSet<byte> modSynced = new HashSet<byte>();
        static readonly Dictionary<byte, (float last, int count)> modRate = new Dictionary<byte, (float, int)>();
        static bool modAllowedSent;

        public static void ResetModSession()
        {
            modSlotData.Clear();
            modSynced.Clear();
            modRate.Clear();
            modAllowedSent = false;
        }

        /// <summary>Host, every frame: send the mod rule + every session mod to players who just got ready.</summary>
        public void HostModTick()
        {
            bool allowed = Server.AllowMods;
            if (allowed != modAllowedSent) { modAllowedSent = allowed; modSynced.Clear(); }
            foreach (var p in Players.Values)
            {
                if (p.Conn == null || !p.Ready || modSynced.Contains(p.Id)) continue;
                modSynced.Add(p.Id);
                w.Reset(); w.Bool(allowed);
                ToPlayer(p, Msg.ModRules);
                if (!allowed) continue;
                for (int i = 0; i < modSlotData.Count; i++) SendModSlot(p, i);
            }
        }

        void SendModSlot(NetPlayer p, int slot)
        {
            var gz = modSlotData[slot];
            w.Reset(); w.Byte((byte)slot); w.Int(gz.Length); w.Bytes(gz, 0, gz.Length);
            if (p != null) ToPlayer(p, Msg.ModWeaponDef);
            else ToClients(Msg.ModWeaponDef);
        }

        /// <summary>A modded weapon is being used: the host registers it, a client asks the host to.</summary>
        public void ShareModWeapon(ModPackage p)
        {
            if (NetState.IsHost) { HostRegisterMod(p, null); return; }
            if (!NetState.IsClient) return;
            byte[] gz;
            try { gz = ModCodec.ToBytes(p); } catch { return; }
            if (gz.Length > ModCodec.MaxCompressed) { GameEvents.ShowToast("That modded weapon is too big to send."); return; }
            w.Reset(); w.Int(gz.Length); w.Bytes(gz, 0, gz.Length);
            ToHost(Msg.ReqModWeapon);
        }

        void RejectMod(NetPlayer from, string why)
        {
            if (from == null) { GameEvents.ShowToast(why); return; }
            w.Reset(); w.String(why.Length > 150 ? why.Substring(0, 150) : why);
            ToPlayer(from, Msg.ModReject);
        }

        void HostRegisterMod(ModPackage src, NetPlayer from)
        {
            if (!Server.AllowMods) { RejectMod(from, "Modded weapons are turned off on this server."); return; }
            var p = ModStore.Copy(src);
            if (p == null) return;
            var err = ModStore.Validate(p); // the anti-cheat rules again, on the host
            if (err == null && p.Kind != ModKind.Weapon) err = "Only Weapon Editor mods can be used in multiplayer.";
            if (err != null) { RejectMod(from, "The host refused your mod: " + err); return; }
            // The host's own installed copy of the same mod wins over whatever a client sends.
            var mine = ModStore.FindInstalled(p.id);
            if (mine != null && from != null) p = ModStore.Copy(mine);
            var defId = ModWeapons.DefId(p.id);
            if (ModNet.Slots.Contains(defId)) return; // already in this session (first copy stays)
            if (ModNet.Slots.Count >= ModNet.MaxSessionMods || ModWeapons.BuiltinCount + ModNet.Slots.Count > 254) { RejectMod(from, "This server has too many modded weapons already."); return; }
            if (ModWeapons.ApplySession(p) == null) { RejectMod(from, "The host couldn't build your mod."); return; }
            int slot = ModNet.Slots.Count;
            ModNet.SetSlot(slot, defId);
            modSlotData.Add(ModCodec.ToBytes(p));
            SendModSlot(null, slot);
            SystemMessage((from != null ? from.Name : LocalName) + " brought a modded weapon: " + p.name + " [MODDED]", false);
        }

        /// <summary>Client -> host mod messages.</summary>
        void HostModMsg(NetPlayer p, Msg type, NetReader rd)
        {
            if (type != Msg.ReqModWeapon) return;
            modRate.TryGetValue(p.Id, out var rate);
            if (Time.unscaledTime - rate.last < 0.5f || rate.count > 30) return; // flood guard
            modRate[p.Id] = (Time.unscaledTime, rate.count + 1);
            int n = rd.Int();
            if (n <= 0 || n > ModCodec.MaxCompressed) return;
            var gz = rd.Bytes(n);
            if (gz == null || gz.Length != n) return;
            var pkg = ModCodec.FromBytes(gz);
            if (pkg == null) { RejectMod(p, "The host couldn't read your mod."); return; }
            HostRegisterMod(pkg, p);
        }

        /// <summary>Host -> client mod messages.</summary>
        void ClientModMsg(Msg type, NetReader rd)
        {
            switch (type)
            {
                case Msg.ModRules:
                    ModNet.clientAllowed = rd.Bool();
                    if (!ModNet.clientAllowed) GameEvents.ShowToast("Modded weapons are turned off on this server.");
                    break;
                case Msg.ModWeaponDef:
                {
                    int slot = rd.Byte();
                    int n = rd.Int();
                    if (n <= 0 || n > ModCodec.MaxCompressed || slot >= ModNet.MaxSessionMods) return;
                    var gz = rd.Bytes(n);
                    if (gz == null || gz.Length != n) return;
                    var pkg = ModCodec.FromBytes(gz);
                    if (pkg == null || ModStore.Validate(pkg) != null || pkg.Kind != ModKind.Weapon) return; // same rules on every machine
                    var def = ModWeapons.ApplySession(pkg);
                    if (def != null) ModNet.SetSlot(slot, def.Id);
                    break;
                }
                case Msg.ModReject:
                    Toasts.Show("MODDED WEAPON", rd.String(), UI.Warn);
                    break;
            }
        }
    }
}
