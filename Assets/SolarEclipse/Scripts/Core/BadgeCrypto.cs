using System;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace SolarEclipse
{
    /// <summary>
    /// Plain .NET crypto for profile badge certificates (no UnityEngine here, so it can be tested offline).
    /// Grants are signed with RSA-2048 / SHA-256 / PKCS#1 v1.5. The game ships only the public key plus the private key
    /// encrypted with AES-256-CBC under a PBKDF2-HMAC-SHA256 key derived from the dev login password: only someone who
    /// knows that password can sign. Every primitive has a hand-written fallback (BigInteger RSA, HMAC PBKDF2) in case
    /// a platform's class library lacks an API, and both paths give identical results.
    /// </summary>
    public static class BadgeCrypto
    {
        public const int Iterations = 200000;
        static readonly byte[] Sha256Prefix = { 0x30, 0x31, 0x30, 0x0d, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01, 0x05, 0x00, 0x04, 0x20 };

        public static byte[] Sha256(byte[] data)
        {
            SHA256 h = null;
            try { h = SHA256.Create(); } catch (Exception) { }
            if (h == null) h = new SHA256Managed(); // in case code stripping removed the CryptoConfig mapping
            using (h) return h.ComputeHash(data);
        }

        static Aes CreateAes()
        {
            Aes a = null;
            try { a = Aes.Create(); } catch (Exception) { }
            return a ?? new AesManaged();
        }

        // ------------------------------------------------------------------ PBKDF2-HMAC-SHA256

        public static byte[] Pbkdf2(string password, byte[] salt, int iterations, int length)
        {
            try
            {
                using (var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                    return kdf.GetBytes(length);
            }
            catch (Exception) { return Pbkdf2Manual(password, salt, iterations, length); }
        }

        public static byte[] Pbkdf2Manual(string password, byte[] salt, int iterations, int length)
        {
            var result = new byte[length];
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(password)))
            {
                int blocks = (length + 31) / 32;
                for (int b = 1; b <= blocks; b++)
                {
                    var s = new byte[salt.Length + 4];
                    Buffer.BlockCopy(salt, 0, s, 0, salt.Length);
                    s[salt.Length] = (byte)(b >> 24); s[salt.Length + 1] = (byte)(b >> 16); s[salt.Length + 2] = (byte)(b >> 8); s[salt.Length + 3] = (byte)b;
                    var u = hmac.ComputeHash(s);
                    var t = (byte[])u.Clone();
                    for (int i = 1; i < iterations; i++)
                    {
                        u = hmac.ComputeHash(u);
                        for (int k = 0; k < t.Length; k++) t[k] ^= u[k];
                    }
                    int off = (b - 1) * 32;
                    Buffer.BlockCopy(t, 0, result, off, Math.Min(32, length - off));
                }
            }
            return result;
        }

        // ------------------------------------------------------------------ private key storage

        public static byte[] SerializeKey(RSAParameters p)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(Encoding.ASCII.GetBytes("GSK1"));
                foreach (var f in new[] { p.Modulus, p.Exponent, p.D, p.P, p.Q, p.DP, p.DQ, p.InverseQ })
                {
                    bw.Write((ushort)f.Length);
                    bw.Write(f);
                }
                bw.Flush();
                return ms.ToArray();
            }
        }

        public static bool DeserializeKey(byte[] data, out RSAParameters p)
        {
            p = default;
            try
            {
                using (var br = new BinaryReader(new MemoryStream(data)))
                {
                    if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "GSK1") return false;
                    var f = new byte[8][];
                    for (int i = 0; i < 8; i++) f[i] = br.ReadBytes(br.ReadUInt16());
                    p = new RSAParameters { Modulus = f[0], Exponent = f[1], D = f[2], P = f[3], Q = f[4], DP = f[5], DQ = f[6], InverseQ = f[7] };
                    return true;
                }
            }
            catch (Exception) { return false; }
        }

        /// <summary>salt(16) | iv(16) | AES-256-CBC(PKCS7) ciphertext of the serialized key.</summary>
        public static byte[] EncryptKey(RSAParameters key, string password)
        {
            var salt = new byte[16]; var iv = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(salt); rng.GetBytes(iv); }
            var aesKey = Pbkdf2(password, salt, Iterations, 32);
            byte[] ct;
            using (var aes = CreateAes())
            {
                aes.KeySize = 256; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                using (var enc = aes.CreateEncryptor(aesKey, iv)) { var plain = SerializeKey(key); ct = enc.TransformFinalBlock(plain, 0, plain.Length); Array.Clear(plain, 0, plain.Length); }
            }
            Array.Clear(aesKey, 0, aesKey.Length);
            var blob = new byte[32 + ct.Length];
            Buffer.BlockCopy(salt, 0, blob, 0, 16); Buffer.BlockCopy(iv, 0, blob, 16, 16); Buffer.BlockCopy(ct, 0, blob, 32, ct.Length);
            return blob;
        }

        /// <summary>Decrypts the embedded private key (in memory only). Fails on a wrong password or a key that doesn't match the public key.</summary>
        public static bool TryDecryptKey(byte[] blob, string password, byte[] expectedModulus, out RSAParameters key)
        {
            key = default;
            if (blob == null || blob.Length < 48 || string.IsNullOrEmpty(password)) return false;
            byte[] aesKey = null, plain = null;
            try
            {
                var salt = new byte[16]; var iv = new byte[16];
                Buffer.BlockCopy(blob, 0, salt, 0, 16); Buffer.BlockCopy(blob, 16, iv, 0, 16);
                aesKey = Pbkdf2(password, salt, Iterations, 32);
                using (var aes = CreateAes())
                {
                    aes.KeySize = 256; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                    using (var dec = aes.CreateDecryptor(aesKey, iv)) plain = dec.TransformFinalBlock(blob, 32, blob.Length - 32);
                }
                if (!DeserializeKey(plain, out key)) return false;
                if (expectedModulus != null && !Same(key.Modulus, expectedModulus)) { key = default; return false; }
                return true;
            }
            catch (Exception) { key = default; return false; }
            finally
            {
                if (aesKey != null) Array.Clear(aesKey, 0, aesKey.Length);
                if (plain != null) Array.Clear(plain, 0, plain.Length);
            }
        }

        // ------------------------------------------------------------------ RSA sign / verify (SHA-256, PKCS#1 v1.5)

        public static byte[] Sign(RSAParameters key, byte[] message)
        {
            try
            {
                using (var rsa = RSA.Create())
                {
                    rsa.ImportParameters(key);
                    var sig = rsa.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    if (sig != null && sig.Length == key.Modulus.Length) return sig;
                }
            }
            catch (Exception) { }
            return SignManual(key, message);
        }

        public static bool Verify(byte[] modulus, byte[] exponent, byte[] message, byte[] signature)
        {
            if (modulus == null || exponent == null || message == null || signature == null || signature.Length != modulus.Length) return false;
            try
            {
                using (var rsa = RSA.Create())
                {
                    rsa.ImportParameters(new RSAParameters { Modulus = modulus, Exponent = exponent });
                    if (rsa.VerifyData(message, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) return true;
                }
            }
            catch (Exception) { }
            // Independent, exact PKCS#1 v1.5 check (also covers class libraries without RSA.VerifyData).
            return VerifyManual(modulus, exponent, message, signature);
        }

        static byte[] Encode(byte[] message, int k)
        {
            var h = Sha256(message);
            int tLen = Sha256Prefix.Length + h.Length;
            if (k < tLen + 11) return null;
            var em = new byte[k];
            em[0] = 0; em[1] = 1;
            for (int i = 2; i < k - tLen - 1; i++) em[i] = 0xff;
            em[k - tLen - 1] = 0;
            Buffer.BlockCopy(Sha256Prefix, 0, em, k - tLen, Sha256Prefix.Length);
            Buffer.BlockCopy(h, 0, em, k - h.Length, h.Length);
            return em;
        }

        static BigInteger FromBE(byte[] be)
        {
            var le = new byte[be.Length + 1];
            for (int i = 0; i < be.Length; i++) le[i] = be[be.Length - 1 - i];
            return new BigInteger(le);
        }

        static byte[] ToBE(BigInteger v, int k)
        {
            var le = v.ToByteArray();
            var be = new byte[k];
            for (int i = 0; i < k && i < le.Length; i++) be[k - 1 - i] = le[i];
            return be;
        }

        public static byte[] SignManual(RSAParameters key, byte[] message)
        {
            int k = key.Modulus.Length;
            var em = Encode(message, k);
            var s = BigInteger.ModPow(FromBE(em), FromBE(key.D), FromBE(key.Modulus));
            return ToBE(s, k);
        }

        public static bool VerifyManual(byte[] modulus, byte[] exponent, byte[] message, byte[] signature)
        {
            try
            {
                int k = modulus.Length;
                var n = FromBE(modulus);
                var s = FromBE(signature);
                if (s >= n) return false;
                var m = BigInteger.ModPow(s, FromBE(exponent), n);
                var em = Encode(message, k);
                return em != null && Same(ToBE(m, k), em);
            }
            catch (Exception) { return false; }
        }

        public static bool Same(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int d = 0;
            for (int i = 0; i < a.Length; i++) d |= a[i] ^ b[i];
            return d == 0;
        }
    }
}
