using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SolarEclipse.Net
{
    [StructLayout(LayoutKind.Explicit)]
    struct FloatBits { [FieldOffset(0)] public float F; [FieldOffset(0)] public int I; }

    /// <summary>Little-endian binary writer that grows as needed. Pure C# (no Unity).</summary>
    public sealed class NetWriter
    {
        public byte[] Buffer;
        public int Length;

        public NetWriter(int capacity = 256) { Buffer = new byte[capacity]; }

        public NetWriter Reset() { Length = 0; return this; }

        void Ensure(int extra)
        {
            if (Length + extra <= Buffer.Length) return;
            int n = Math.Max(Buffer.Length * 2, Length + extra);
            Array.Resize(ref Buffer, n);
        }

        public void Byte(byte v) { Ensure(1); Buffer[Length++] = v; }
        public void SByte(sbyte v) => Byte((byte)v);
        public void Bool(bool v) => Byte(v ? (byte)1 : (byte)0);
        public void UShort(ushort v) { Ensure(2); Buffer[Length++] = (byte)v; Buffer[Length++] = (byte)(v >> 8); }
        public void Short(short v) => UShort((ushort)v);
        public void Int(int v) { Ensure(4); for (int i = 0; i < 4; i++) Buffer[Length++] = (byte)(v >> (8 * i)); }
        public void UInt(uint v) => Int((int)v);
        public void Long(long v) { Ensure(8); for (int i = 0; i < 8; i++) Buffer[Length++] = (byte)(v >> (8 * i)); }

        public void Float(float v) => Int(new FloatBits { F = v }.I);

        public void String(string s)
        {
            s = s ?? "";
            int max = Encoding.UTF8.GetMaxByteCount(s.Length);
            Ensure(2 + max);
            int n = Encoding.UTF8.GetBytes(s, 0, s.Length, Buffer, Length + 2);
            if (n > ushort.MaxValue) throw new ArgumentException("string too long");
            Buffer[Length] = (byte)n; Buffer[Length + 1] = (byte)(n >> 8);
            Length += 2 + n;
        }

        public void Bytes(byte[] src, int offset, int count)
        {
            Ensure(count);
            System.Buffer.BlockCopy(src, offset, Buffer, Length, count);
            Length += count;
        }

        public void BlockWithLength(byte[] src, int offset, int count) { UShort((ushort)count); Bytes(src, offset, count); }

        public byte[] ToArray()
        {
            var a = new byte[Length];
            System.Buffer.BlockCopy(Buffer, 0, a, 0, Length);
            return a;
        }
    }

    /// <summary>Reader matching <see cref="NetWriter"/>. Reading past the end returns zeros and sets <see cref="Overflow"/>.</summary>
    public sealed class NetReader
    {
        byte[] data;
        int pos, end;
        public bool Overflow { get; private set; }
        public int Remaining => end - pos;
        public int Position => pos;

        public NetReader() { }
        public NetReader(byte[] d, int offset, int length) { Set(d, offset, length); }

        public NetReader Set(byte[] d, int offset, int length)
        {
            data = d; pos = offset; end = offset + length; Overflow = false;
            return this;
        }

        bool Need(int n)
        {
            if (pos + n <= end) return true;
            Overflow = true;
            pos = end;
            return false;
        }

        public byte Byte() => Need(1) ? data[pos++] : (byte)0;
        public sbyte SByte() => (sbyte)Byte();
        public bool Bool() => Byte() != 0;
        public ushort UShort() { if (!Need(2)) return 0; ushort v = (ushort)(data[pos] | (data[pos + 1] << 8)); pos += 2; return v; }
        public short Short() => (short)UShort();
        public int Int() { if (!Need(4)) return 0; int v = data[pos] | (data[pos + 1] << 8) | (data[pos + 2] << 16) | (data[pos + 3] << 24); pos += 4; return v; }
        public uint UInt() => (uint)Int();
        public long Long() { if (!Need(8)) return 0; long v = 0; for (int i = 0; i < 8; i++) v |= (long)data[pos + i] << (8 * i); pos += 8; return v; }
        public float Float() => new FloatBits { I = Int() }.F;

        public string String()
        {
            int n = UShort();
            if (!Need(n)) return "";
            var s = Encoding.UTF8.GetString(data, pos, n);
            pos += n;
            return s;
        }

        public byte[] Bytes(int n)
        {
            if (!Need(n)) return new byte[0];
            var a = new byte[n];
            System.Buffer.BlockCopy(data, pos, a, 0, n);
            pos += n;
            return a;
        }

        public byte[] BlockWithLength() => Bytes(UShort());
    }
}
