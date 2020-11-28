using System;
using System.Security.Cryptography;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.DataProtection;

namespace Aporta.Utilities
{
    /// <inheritdoc />
    public class DataEncryptor : IDataEncryption
    {
        private readonly IDataProtector _protector;

        public DataEncryptor(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("Aporta");
        }
        public string Encrypt(string value)
        {
            return !string.IsNullOrEmpty(value) ? _protector.Protect(value) : value;
        }

        public string Decrypt(string value)
        {
            return !string.IsNullOrEmpty(value) ? _protector.Unprotect(value) : value;
        }

        public string Hash(string value)
        {
            var salt = GenerateSalt(16);

            var bytes = KeyDerivation.Pbkdf2(value, salt, KeyDerivationPrf.HMACSHA512, 10000, 16);

            return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(bytes)}";
        }

        public bool CheckMatch(string hash, string value)
        {
            try
            {
                var parts = hash.Split(':');

                var salt = Convert.FromBase64String(parts[0]);

                var bytes = KeyDerivation.Pbkdf2(value, salt, KeyDerivationPrf.HMACSHA512, 10000, 16);

                return parts[1].Equals(Convert.ToBase64String(bytes));
            }
            catch
            {
                return false;
            }
        }

        private static byte[] GenerateSalt(int length)
        {
            var salt = new byte[length];

            using var random = RandomNumberGenerator.Create();
            
            random.GetBytes(salt);

            return salt;
        }
    }
}