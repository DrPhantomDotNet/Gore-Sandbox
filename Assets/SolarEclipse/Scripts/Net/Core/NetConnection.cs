using System;
using System.Collections.Generic;
using System.Net;

namespace SolarEclipse.Net
{
    /// <summary>Wire format constants shared by every packet.</summary>
    public static class Wire
    {
        public const byte Magic0 = 0x47, Magic1 = 0x53;      // "GS"
        public const byte KUnreliable = 1, KReliable = 2, KAck = 3, KConnectionless = 4;
        public const byte FragmentType = 255;
        public const int MaxMessage = 1100;                  // keep datagrams under typical MTU

        public static bool IsGame(byte[] d, int len) => len >= 4 && d[0] == Magic0 && d[1] == Magic1;

        public static byte[] Connectionless(byte type, byte[] payload, int len)
        {
            var p = new byte[4 + len];
            p[0] = Magic0; p[1] = Magic1; p[2] = KConnectionless; p[3] = type;
            if (len > 0) Buffer.BlockCopy(payload, 0, p, 4, len);
            return p;
        }

        /// <summary>True if a is newer than b with 16-bit wrap-around.</summary>
        public static bool SeqGreater(ushort a, ushort b) => (a > b && a - b <= 32768) || (a < b && b - a > 32768);
    }

    public struct NetMessage
    {
        public byte Type;
        public byte[] Data;    // payload only (type byte stripped)
        public bool Reliable;
    }

    /// <summary>
    /// One peer-to-peer link. Unreliable messages are fire-and-forget; reliable ones are numbered, acknowledged,
    /// re-sent until acked, delivered in order exactly once, and split into fragments when large.
    /// </summary>
    public sealed class NetConnection
    {
        sealed class Pending { public byte[] Packet; public double FirstSend, LastSend; public int Sends; }

        public IPEndPoint EndPoint;
        public int Id;
        public object Tag;
        public float Rtt = 0.15f;
        public double LastHeard, Created;
        public bool Failed { get; private set; }
        public int PendingCount => pending.Count + backlog.Count;
        public long ReliableSent, ReliableResent;

        readonly Action<byte[], int, IPEndPoint> send;
        readonly Dictionary<ushort, Pending> pending = new Dictionary<ushort, Pending>();
        readonly Queue<byte[]> backlog = new Queue<byte[]>();          // full messages (type + payload) waiting for window space
        readonly Dictionary<ushort, byte[]> early = new Dictionary<ushort, byte[]>();
        readonly List<byte> fragBuffer = new List<byte>();
        ushort sendSeq, recvSeq;
        ushort fragGroup;
        const int Window = 512;
        const int MaxSends = 80;

        public NetConnection(IPEndPoint ep, Action<byte[], int, IPEndPoint> sender, double now)
        {
            EndPoint = ep;
            send = sender;
            LastHeard = Created = now;
        }

        // ------------------------------------------------------------------ sending

        public void SendUnreliable(byte type, byte[] payload, int len)
        {
            var p = new byte[4 + len];
            p[0] = Wire.Magic0; p[1] = Wire.Magic1; p[2] = Wire.KUnreliable; p[3] = type;
            if (len > 0) Buffer.BlockCopy(payload, 0, p, 4, len);
            send(p, p.Length, EndPoint);
        }

        public void SendReliable(byte type, byte[] payload, int len, double now)
        {
            if (Failed) return;
            if (len + 1 <= Wire.MaxMessage)
            {
                var msg = new byte[1 + len];
                msg[0] = type;
                if (len > 0) Buffer.BlockCopy(payload, 0, msg, 1, len);
                Enqueue(msg, now);
                return;
            }
            // Fragment: [group u16][index u8][count u8][chunk...]; the chunks together form (type + payload).
            int total = len + 1;
            const int chunk = Wire.MaxMessage - 8;
            int count = (total + chunk - 1) / chunk;
            if (count > 255) throw new ArgumentException("message too large: " + len);
            var whole = new byte[total];
            whole[0] = type;
            Buffer.BlockCopy(payload, 0, whole, 1, len);
            ushort group = fragGroup++;
            for (int i = 0; i < count; i++)
            {
                int off = i * chunk, n = Math.Min(chunk, total - off);
                var msg = new byte[1 + 4 + n];
                msg[0] = Wire.FragmentType;
                msg[1] = (byte)group; msg[2] = (byte)(group >> 8); msg[3] = (byte)i; msg[4] = (byte)count;
                Buffer.BlockCopy(whole, off, msg, 5, n);
                Enqueue(msg, now);
            }
        }

        void Enqueue(byte[] msg, double now)
        {
            if (pending.Count >= Window || backlog.Count > 0) { backlog.Enqueue(msg); return; }
            Transmit(msg, now);
        }

        void Transmit(byte[] msg, double now)
        {
            ushort seq = sendSeq++;
            var p = new byte[5 + msg.Length];
            p[0] = Wire.Magic0; p[1] = Wire.Magic1; p[2] = Wire.KReliable; p[3] = (byte)seq; p[4] = (byte)(seq >> 8);
            Buffer.BlockCopy(msg, 0, p, 5, msg.Length);
            pending[seq] = new Pending { Packet = p, FirstSend = now, LastSend = now, Sends = 1 };
            ReliableSent++;
            send(p, p.Length, EndPoint);
        }

        /// <summary>Re-sends unacknowledged packets; marks the link failed if one never gets through.</summary>
        public void Update(double now)
        {
            if (Failed) return;
            double interval = Math.Max(0.08, Rtt * 1.6);
            int budget = 64; // cap resends per tick so a bad link doesn't flood
            foreach (var kv in pending)
            {
                var p = kv.Value;
                if (now - p.LastSend < interval) continue;
                if (budget-- <= 0) break;
                p.LastSend = now;
                p.Sends++;
                ReliableResent++;
                if (p.Sends > MaxSends) { Failed = true; return; }
                send(p.Packet, p.Packet.Length, EndPoint);
            }
            while (backlog.Count > 0 && pending.Count < Window) Transmit(backlog.Dequeue(), now);
        }

        // ------------------------------------------------------------------ receiving

        /// <summary>Handles one datagram from this peer; complete messages are appended to <paramref name="output"/>.</summary>
        public void Receive(byte[] d, int len, double now, List<NetMessage> output)
        {
            if (len < 4) return;
            LastHeard = now;
            switch (d[2])
            {
                case Wire.KUnreliable:
                    output.Add(new NetMessage { Type = d[3], Data = Slice(d, 4, len - 4), Reliable = false });
                    break;
                case Wire.KAck:
                {
                    if (len < 5) return;
                    ushort seq = (ushort)(d[3] | (d[4] << 8));
                    if (pending.TryGetValue(seq, out var p))
                    {
                        if (p.Sends == 1) Rtt = Rtt * 0.85f + (float)(now - p.FirstSend) * 0.15f;
                        pending.Remove(seq);
                    }
                    break;
                }
                case Wire.KReliable:
                {
                    if (len < 6) return;
                    ushort seq = (ushort)(d[3] | (d[4] << 8));
                    var ack = new byte[] { Wire.Magic0, Wire.Magic1, Wire.KAck, d[3], d[4] };
                    send(ack, ack.Length, EndPoint);
                    if (seq == recvSeq)
                    {
                        Deliver(Slice(d, 5, len - 5), output);
                        recvSeq++;
                        while (early.TryGetValue(recvSeq, out var next))
                        {
                            early.Remove(recvSeq);
                            Deliver(next, output);
                            recvSeq++;
                        }
                    }
                    else if (Wire.SeqGreater(seq, recvSeq) && !early.ContainsKey(seq) && early.Count < 4096)
                        early[seq] = Slice(d, 5, len - 5);
                    break;
                }
            }
        }

        void Deliver(byte[] msg, List<NetMessage> output)
        {
            if (msg.Length == 0) return;
            if (msg[0] != Wire.FragmentType)
            {
                output.Add(new NetMessage { Type = msg[0], Data = Slice(msg, 1, msg.Length - 1), Reliable = true });
                return;
            }
            if (msg.Length < 5) return;
            int index = msg[3], count = msg[4];
            if (index == 0) fragBuffer.Clear();
            for (int i = 5; i < msg.Length; i++) fragBuffer.Add(msg[i]);
            if (index == count - 1 && fragBuffer.Count > 0)
            {
                var whole = fragBuffer.ToArray();
                fragBuffer.Clear();
                output.Add(new NetMessage { Type = whole[0], Data = Slice(whole, 1, whole.Length - 1), Reliable = true });
            }
        }

        static byte[] Slice(byte[] src, int offset, int count)
        {
            if (count <= 0) return new byte[0];
            var a = new byte[count];
            Buffer.BlockCopy(src, offset, a, 0, count);
            return a;
        }
    }
}
