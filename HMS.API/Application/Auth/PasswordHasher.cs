using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace HMS.API.Application.Auth
{
    public class PasswordHasher : IPasswordHasher
    {
        private const int SaltSize = 128 / 8; // 16
        private const int SubkeySize = 256 / 8; // 32
        private const int IterationCount = 100_000;

        // PBKDF2 with HMACSHA256
        public string Hash(string password)
        {
            var salt = new byte[SaltSize];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);

            var subkey = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, IterationCount, SubkeySize);

            var outputBytes = new byte[1 + salt.Length + subkey.Length];
            outputBytes[0] = 0x01; // format marker
            Buffer.BlockCopy(salt, 0, outputBytes, 1, salt.Length);
            Buffer.BlockCopy(subkey, 0, outputBytes, 1 + salt.Length, subkey.Length);

            return Convert.ToBase64String(outputBytes);
        }

        public bool Verify(string hash, string password)
        {
            if (string.IsNullOrEmpty(hash)) return false;

            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(hash);
            }
            catch
            {
                // not base64
                return false;
            }

            // New PBKDF2 format: [0x01][16-byte salt][32-byte subkey]
            if (decoded.Length == 1 + SaltSize + SubkeySize && decoded[0] == 0x01)
            {
                var salt = new byte[SaltSize];
                Buffer.BlockCopy(decoded, 1, salt, 0, SaltSize);
                var expectedSubkey = new byte[SubkeySize];
                Buffer.BlockCopy(decoded, 1 + SaltSize, expectedSubkey, 0, SubkeySize);

                var actualSubkey = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, IterationCount, SubkeySize);

                return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
            }

            // Legacy SHA-256 raw hash (stored as Base64 of 32 bytes)
            if (decoded.Length == 32)
            {
                using var sha = SHA256.Create();
                var passBytes = System.Text.Encoding.UTF8.GetBytes(password);
                var computed = sha.ComputeHash(passBytes);
                return CryptographicOperations.FixedTimeEquals(computed, decoded);
            }

            // Unknown format
            return false;
        }
    }
}