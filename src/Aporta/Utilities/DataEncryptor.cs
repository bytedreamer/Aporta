using System;
using Aporta.Core.Services;
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
            throw new NotImplementedException();
        }
    }
}