using System;
using System.Net;

namespace SolarEclipse.Net
{
    /// <summary>
    /// Minimal STUN (RFC 5389) binding request/response. Sent from the game socket to a free public STUN
    /// server, it tells us the public IP:port our router maps that socket to, which peers use for hole punching.
    /// </summary>
    public static class Stun
    {
        public static readonly (string host, int port)[] Servers =
        {
            ("stun.l.google.com", 19302), ("stun1.l.google.com", 19302), ("stun.cloudflare.com", 3478), ("stun2.l.google.com", 19302),
        };

        const uint Cookie = 0x2112A442;

        public static byte[] BuildRequest(Random rng)
        {
            var p = new byte[20];
            p[0] = 0x00; p[1] = 0x01;           // Binding Request
            p[2] = 0x00; p[3] = 0x00;           // length
            p[4] = 0x21; p[5] = 0x12; p[6] = 0xA4; p[7] = 0x42;
            for (int i = 8; i < 20; i++) p[i] = (byte)rng.Next(256);
            return p;
        }

        public static bool IsResponse(byte[] d, int len) =>
            len >= 20 && d[0] == 0x01 && d[1] == 0x01 && d[4] == 0x21 && d[5] == 0x12 && d[6] == 0xA4 && d[7] == 0x42;

        /// <summary>Returns the mapped public endpoint from a Binding Success Response, or null.</summary>
        public static IPEndPoint Parse(byte[] d, int len)
        {
            if (!IsResponse(d, len)) return null;
            int msgLen = (d[2] << 8) | d[3];
            int end = Math.Min(len, 20 + msgLen);
            int pos = 20;
            IPEndPoint mapped = null;
            while (pos + 4 <= end)
            {
                int type = (d[pos] << 8) | d[pos + 1];
                int alen = (d[pos + 2] << 8) | d[pos + 3];
                int v = pos + 4;
                if (v + alen > end) break;
                if ((type == 0x0020 || type == 0x8020) && alen >= 8 && d[v + 1] == 0x01)
                {
                    int port = ((d[v + 2] << 8) | d[v + 3]) ^ (int)(Cookie >> 16);
                    var ip = new byte[4];
                    for (int i = 0; i < 4; i++) ip[i] = (byte)(d[v + 4 + i] ^ (byte)(Cookie >> (24 - 8 * i)));
                    return new IPEndPoint(new IPAddress(ip), port);
                }
                if (type == 0x0001 && alen >= 8 && d[v + 1] == 0x01)
                {
                    int port = (d[v + 2] << 8) | d[v + 3];
                    mapped = new IPEndPoint(new IPAddress(new[] { d[v + 4], d[v + 5], d[v + 6], d[v + 7] }), port);
                }
                pos = v + ((alen + 3) & ~3);
            }
            return mapped;
        }
    }
}
