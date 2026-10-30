using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SolarEclipse
{
    public enum BadgeId : byte { Player = 0, Developer = 1, Admin = 2, EarlyAccess = 3, Tester = 4 }

    /// <summary>
    /// Profile badges. "Player" is everyone's default; the others exist only as signed certificates
    /// {subject, badge, issued, grant/revoke} issued from the dev panel (Dev/DevBadges.cs). The subject is
    /// SHA-256 of the player's multiplayer id, so certificates can't be moved to another player. Every certificate
    /// is checked against the public key before it's shown; anything that doesn't verify is ignored.
    /// </summary>
    public static class Badges
    {
        public const int Count = 5;
        public static readonly string[] Names = { "Player", "Developer", "Admin", "Early Access", "Tester" };
        public static readonly string[] Icons = { "person", "dev", "shield", "star", "bug" };
        public static readonly Color[] Colors =
        {
            new Color(0.42f, 0.44f, 0.5f), new Color(0.58f, 0.28f, 0.95f), new Color(0.86f, 0.13f, 0.15f),
            new Color(0.95f, 0.68f, 0.12f), new Color(0.14f, 0.68f, 0.55f),
        };
        /// <summary>Badges the dev panel can grant, in display order.</summary>
        public static readonly BadgeId[] Grantable = { BadgeId.Developer, BadgeId.Admin, BadgeId.Tester, BadgeId.EarlyAccess };

        public const int SignedLength = 43;
        const byte CertVersion = 1;

        public sealed class Cert
        {
            public byte[] Subject;
            public BadgeId Badge;
            public long Issued;
            public bool Grant;
            public byte[] Raw;
        }

        public static byte[] SubjectFor(string guid) => BadgeCrypto.Sha256(Encoding.UTF8.GetBytes("GoreSandbox.badge|" + (guid ?? "")));

        // ------------------------------------------------------------------ certificates

        public static Cert Parse(byte[] raw)
        {
            if (raw == null || raw.Length < SignedLength + 64 || raw.Length > 1024 || raw[0] != CertVersion) return null;
            var subject = new byte[32];
            Buffer.BlockCopy(raw, 1, subject, 0, 32);
            byte b = raw[33];
            if (b == 0 || b >= Count) return null;
            long issued = 0;
            for (int i = 0; i < 8; i++) issued |= (long)raw[34 + i] << (8 * i);
            return new Cert { Subject = subject, Badge = (BadgeId)b, Issued = issued, Grant = raw[42] != 0, Raw = raw };
        }

        static byte[] modulus, exponent;
        static readonly Dictionary<string, bool> verified = new Dictionary<string, bool>();

        static void LoadPublicKey()
        {
            if (modulus != null) return;
            try { modulus = Convert.FromBase64String(BadgeKeys.PublicModulus); exponent = Convert.FromBase64String(BadgeKeys.PublicExponent); }
            catch (Exception e) { Debug.LogWarning("[Badges] bad public key: " + e.Message); modulus = new byte[0]; exponent = new byte[0]; }
        }

        /// <summary>True when the certificate is well formed and signed by the badge key (results cached).</summary>
        public static bool Verify(byte[] raw)
        {
            if (Parse(raw) == null) return false;
            string key = Convert.ToBase64String(raw);
            if (verified.TryGetValue(key, out bool ok)) return ok;
            LoadPublicKey();
            var msg = new byte[SignedLength];
            var sig = new byte[raw.Length - SignedLength];
            Buffer.BlockCopy(raw, 0, msg, 0, SignedLength);
            Buffer.BlockCopy(raw, SignedLength, sig, 0, sig.Length);
            ok = BadgeCrypto.Verify(modulus, exponent, msg, sig);
            if (verified.Count > 512) verified.Clear();
            verified[key] = ok;
            return ok;
        }

        /// <summary>Bitmask (1 &lt;&lt; badge) of the badges these certificates prove for this subject. The newest
        /// certificate per badge wins (a later revoke removes an earlier grant).</summary>
        public static int MaskFrom(byte[] subject, List<byte[]> certs)
        {
            if (subject == null || certs == null) return 0;
            var latest = new long[Count];
            var state = new bool[Count];
            for (int i = 0; i < Count; i++) latest[i] = long.MinValue;
            foreach (var raw in certs)
            {
                var c = Parse(raw);
                if (c == null || !BadgeCrypto.Same(c.Subject, subject) || !Verify(raw)) continue;
                int b = (int)c.Badge;
                if (c.Issued > latest[b] || (c.Issued == latest[b] && !c.Grant)) { latest[b] = c.Issued; state[b] = c.Grant; }
            }
            int mask = 0;
            for (int i = 1; i < Count; i++) if (state[i]) mask |= 1 << i;
            return mask;
        }

        public static bool Has(int mask, BadgeId b) => (mask & (1 << (int)b)) != 0;

        // ------------------------------------------------------------------ signing (dev only)

        static readonly object signerLock = new object();
        static RSAParameters signer;
        static bool hasSigner;
        static volatile bool unlocking;
        static volatile string unlockError;

        public static bool CanSign { get { lock (signerLock) return hasSigner; } }
        public static bool Unlocking => unlocking;
        public static string UnlockError => unlockError;

        /// <summary>Decrypts the private signing key with the dev password on a worker thread (PBKDF2 takes a moment on phones).
        /// The key stays in memory only.</summary>
        public static void Unlock(string password)
        {
            if (CanSign || unlocking || string.IsNullOrEmpty(password)) return;
            unlocking = true;
            unlockError = null;
            byte[] blob, mod;
            try { blob = Convert.FromBase64String(BadgeKeys.EncryptedPrivateKey); LoadPublicKey(); mod = modulus; }
            catch (Exception e) { unlocking = false; unlockError = e.Message; return; }
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (BadgeCrypto.TryDecryptKey(blob, password, mod, out var key)) { lock (signerLock) { signer = key; hasSigner = true; } }
                    else unlockError = "Could not unlock the badge signing key.";
                }
                catch (Exception e) { unlockError = e.Message; }
                finally { unlocking = false; }
            });
        }

        public static void Lock()
        {
            lock (signerLock)
            {
                if (hasSigner)
                {
                    foreach (var a in new[] { signer.D, signer.P, signer.Q, signer.DP, signer.DQ, signer.InverseQ }) if (a != null) Array.Clear(a, 0, a.Length);
                }
                signer = default; hasSigner = false;
            }
        }

        /// <summary>Signs a grant (or revoke) certificate for a subject. Null when the key isn't unlocked.</summary>
        public static byte[] Issue(byte[] subject, BadgeId badge, bool grant)
        {
            if (subject == null || subject.Length != 32 || badge == BadgeId.Player) return null;
            RSAParameters key;
            lock (signerLock) { if (!hasSigner) return null; key = signer; }
            var msg = new byte[SignedLength];
            msg[0] = CertVersion;
            Buffer.BlockCopy(subject, 0, msg, 1, 32);
            msg[33] = (byte)badge;
            long issued = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for (int i = 0; i < 8; i++) msg[34 + i] = (byte)(issued >> (8 * i));
            msg[42] = grant ? (byte)1 : (byte)0;
            var sig = BadgeCrypto.Sign(key, msg);
            var raw = new byte[SignedLength + sig.Length];
            Buffer.BlockCopy(msg, 0, raw, 0, SignedLength);
            Buffer.BlockCopy(sig, 0, raw, SignedLength, sig.Length);
            return raw;
        }
    }
}
