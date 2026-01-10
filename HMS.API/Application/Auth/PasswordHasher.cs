using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace HMS.API.Application.Auth
{
    public class PasswordHasher : IPasswordHasher
    {
        // PBKDF2 with HMACSHA256
        public string Hash(string password)
        {
            var salt = new byte[128 / 8];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);

            var subkey = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, 100_000, 256 / 8);

            var outputBytes = new byte[1 + salt.Length + subkey.Length];
            outputBytes[0] = 0x01; // format marker
            Buffer.BlockCopy(salt, 0, outputBytes, 1, salt.Length);
            Buffer.BlockCopy(subkey, 0, outputBytes, 1 + salt.Length, subkey.Length);

            return Convert.ToBase64String(outputBytes);
        }

        public bool Verify(string hash, string password)
        {
            var decoded = Convert.FromBase64String(hash);
            if (decoded[0] != 0x01) return false;

            var salt = new byte[128 / 8];
            Buffer.BlockCopy(decoded, 1, salt, 0, salt.Length);

            var expectedSubkey = new byte[256 / 8];
            Buffer.BlockCopy(decoded, 1 + salt.Length, expectedSubkey, 0, expectedSubkey.Length);

            var actualSubkey = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, 100_000, 256 / 8);

            return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
        }
    }
}