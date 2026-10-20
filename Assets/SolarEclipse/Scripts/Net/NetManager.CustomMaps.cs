using SolarEclipse.Net;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Custom map transfer: when the host plays a map made in the map editor, joining clients get the map's JSON
    /// (gzipped) in a reliable message sent right before Welcome. Reliable messages arrive in order, so by the time
    /// OnWelcome looks the map id up in MapCatalog the downloaded map is already registered.
    /// </summary>
    public sealed partial class NetManager
    {
        const int MaxCustomMapBytes = 240 * 1024; // fragmentation tops out at 255 x ~1 KB

        void SendCustomMap(NetPlayer p)
        {
            var map = GameManager.I != null ? GameManager.I.CurrentMap : null;
            var json = CustomMaps.JsonFor(map);
            if (json == null) return;
            byte[] gz;
            try { gz = CustomMaps.Compress(json); }
            catch (System.Exception e) { Debug.LogWarning("[Net] couldn't compress custom map: " + e.Message); return; }
            if (gz.Length > MaxCustomMapBytes) { Log("Custom map too large to send (" + gz.Length / 1024 + " KB)"); return; }
            w.Reset();
            w.Int(gz.Length);
            w.Bytes(gz, 0, gz.Length);
            ToPlayer(p, Msg.CustomMap);
            Log("Sent custom map '" + map.Name + "' to " + p.Name + " (" + gz.Length / 1024f + " KB)");
        }

        void OnCustomMap(NetReader rd)
        {
            try
            {
                int n = rd.Int();
                if (n <= 0 || n > MaxCustomMapBytes) return;
                var gz = rd.Bytes(n);
                if (gz.Length != n) return;
                CustomMaps.RegisterDownloaded(CustomMaps.Decompress(gz));
            }
            catch (System.Exception e) { Debug.LogWarning("[Net] bad custom map from host: " + e.Message); }
        }
    }
}
