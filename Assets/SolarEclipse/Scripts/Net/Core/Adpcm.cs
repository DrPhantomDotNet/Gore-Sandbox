using System;

namespace SolarEclipse.Net
{
    /// <summary>
    /// IMA ADPCM voice codec (4 bits per sample, ~64 kbit/s at 16 kHz). Each frame is self-contained
    /// (3-byte header with predictor + step index), so a lost packet never corrupts the next one.
    /// </summary>
    public static class Adpcm
    {
        public const int SampleRate = 16000;
        public const int FrameSamples = 320;                       // 20 ms
        public const int FrameBytes = 3 + FrameSamples / 2;        // 163

        static readonly int[] IndexTable = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };
        static readonly int[] StepTable =
        {
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143,
            157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411,
            1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493,
            10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767,
        };

        /// <summary>Encodes FrameSamples floats (-1..1) into FrameBytes bytes at dst[offset].</summary>
        public static void EncodeFrame(float[] src, int srcOffset, byte[] dst, int offset, ref int predictor, ref int index)
        {
            // Header: starting state, so frames decode independently.
            dst[offset] = (byte)predictor; dst[offset + 1] = (byte)(predictor >> 8); dst[offset + 2] = (byte)index;
            int pred = predictor, idx = index;
            for (int i = 0; i < FrameSamples; i++)
            {
                int sample = (int)Math.Max(-32768, Math.Min(32767, src[srcOffset + i] * 32767f));
                int step = StepTable[idx];
                int diff = sample - pred;
                int code = 0;
                if (diff < 0) { code = 8; diff = -diff; }
                int delta = step >> 3;
                if (diff >= step) { code |= 4; diff -= step; delta += step; }
                step >>= 1;
                if (diff >= step) { code |= 2; diff -= step; delta += step; }
                step >>= 1;
                if (diff >= step) { code |= 1; delta += step; }
                pred += (code & 8) != 0 ? -delta : delta;
                pred = Math.Max(-32768, Math.Min(32767, pred));
                idx = Math.Max(0, Math.Min(88, idx + IndexTable[code]));
                int b = offset + 3 + (i >> 1);
                if ((i & 1) == 0) dst[b] = (byte)code; else dst[b] |= (byte)(code << 4);
            }
            predictor = pred; index = idx;
        }

        /// <summary>Decodes one frame into FrameSamples floats at dst[dstOffset].</summary>
        public static void DecodeFrame(byte[] src, int offset, float[] dst, int dstOffset)
        {
            int pred = (short)(src[offset] | (src[offset + 1] << 8));
            int idx = Math.Max(0, Math.Min(88, (int)src[offset + 2]));
            for (int i = 0; i < FrameSamples; i++)
            {
                int b = src[offset + 3 + (i >> 1)];
                int code = (i & 1) == 0 ? b & 15 : b >> 4;
                int step = StepTable[idx];
                int delta = step >> 3;
                if ((code & 4) != 0) delta += step;
                if ((code & 2) != 0) delta += step >> 1;
                if ((code & 1) != 0) delta += step >> 2;
                pred += (code & 8) != 0 ? -delta : delta;
                pred = Math.Max(-32768, Math.Min(32767, pred));
                idx = Math.Max(0, Math.Min(88, idx + IndexTable[code]));
                dst[dstOffset + i] = pred / 32768f;
            }
        }
    }

    /// <summary>Thread-safe float ring buffer used as a small jitter buffer between network and audio thread.</summary>
    public sealed class AudioRing
    {
        readonly float[] buf;
        int read, write, count;
        readonly object gate = new object();
        public AudioRing(int capacity) { buf = new float[capacity]; }
        public int Count { get { lock (gate) return count; } }

        public void Write(float[] src, int offset, int n)
        {
            lock (gate)
            {
                for (int i = 0; i < n; i++)
                {
                    if (count == buf.Length) { read = (read + 1) % buf.Length; count--; } // overflow: drop oldest
                    buf[write] = src[offset + i];
                    write = (write + 1) % buf.Length;
                    count++;
                }
            }
        }

        /// <summary>Fills dst; missing samples become silence. Returns how many real samples were read.</summary>
        public int Read(float[] dst, int n)
        {
            lock (gate)
            {
                int got = Math.Min(n, count);
                for (int i = 0; i < got; i++) { dst[i] = buf[read]; read = (read + 1) % buf.Length; }
                for (int i = got; i < n; i++) dst[i] = 0f;
                count -= got;
                return got;
            }
        }

        public void Clear() { lock (gate) { read = write = count = 0; } }
    }
}
