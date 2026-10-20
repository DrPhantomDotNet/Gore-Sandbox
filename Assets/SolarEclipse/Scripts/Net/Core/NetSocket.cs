using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SolarEclipse.Net
{
    public struct Datagram
    {
        public byte[] Data;
        public int Length;
        public IPEndPoint From;
    }

    /// <summary>
    /// One UDP socket with a background receive thread. Everything (game traffic, STUN, hole punching) goes
    /// through the same socket so the NAT mapping discovered by STUN is the one peers actually reach.
    /// </summary>
    public sealed class NetSocket : IDisposable
    {
        readonly Socket socket;
        readonly Thread thread;
        volatile bool running = true;
        public readonly ConcurrentQueue<Datagram> Inbox = new ConcurrentQueue<Datagram>();
        public int Port { get; }
        public long BytesIn, BytesOut, PacketsIn, PacketsOut;
        public Action<string> Log;

        public NetSocket(int port)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try { socket.IOControl(unchecked((int)0x9800000C), new byte[] { 0 }, null); } catch { } // Windows: ignore ICMP "port unreachable" resets
            socket.EnableBroadcast = true;
            socket.ReceiveBufferSize = 1 << 20;
            socket.SendBufferSize = 1 << 20;
            try { socket.Bind(new IPEndPoint(IPAddress.Any, port)); }
            catch (SocketException) when (port != 0) { socket.Bind(new IPEndPoint(IPAddress.Any, 0)); }
            Port = ((IPEndPoint)socket.LocalEndPoint).Port;
            thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "GSB-UDP" };
            thread.Start();
        }

        void ReceiveLoop()
        {
            var buf = new byte[65536];
            while (running)
            {
                try
                {
                    EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                    int n = socket.ReceiveFrom(buf, ref ep);
                    if (n <= 0) continue;
                    var copy = new byte[n];
                    Buffer.BlockCopy(buf, 0, copy, 0, n);
                    Interlocked.Add(ref BytesIn, n);
                    Interlocked.Increment(ref PacketsIn);
                    Inbox.Enqueue(new Datagram { Data = copy, Length = n, From = (IPEndPoint)ep });
                }
                catch (SocketException) { if (!running) break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception e) { Log?.Invoke("UDP receive error: " + e.Message); }
            }
        }

        public void Send(byte[] data, int length, IPEndPoint to)
        {
            if (to == null || !running) return;
            try
            {
                socket.SendTo(data, 0, length, SocketFlags.None, to);
                Interlocked.Add(ref BytesOut, length);
                Interlocked.Increment(ref PacketsOut);
            }
            catch (Exception e) { Log?.Invoke("UDP send to " + to + " failed: " + e.Message); }
        }

        public void Dispose()
        {
            running = false;
            try { socket.Close(); } catch { }
        }
    }
}
