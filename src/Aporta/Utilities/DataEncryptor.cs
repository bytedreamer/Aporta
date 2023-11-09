using System;
using System.Security.Cryptography;
using Aporta.Extensions;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.DataProtection;

namespace Aporta.Utilities;

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

    public string Hash(string value, byte[] salt)
    {
        var bytes = KeyDerivation.Pbkdf2(value, salt, KeyDerivationPrf.HMACSHA512, 10000, 16);

        return Convert.ToBase64String(bytes);
    }

    public byte[] GenerateSalt()
    {
        var salt = new byte[16];

        using var random = RandomNumberGenerator.Create();
            
        random.GetBytes(salt);

        return salt;
    }

    public string GeneratePassword()
    {
        const string lowerCase = "abcdefghijklmnopqursuvwxyz";
        const string upperCaes = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string numbers = "123456789";
        const string specials = @"!@£$%^&*()#€";
        char[] password = new char[16];
        string charSet = lowerCase + upperCaes + numbers + specials;
        var random = new Random();
        int counter;
        for (counter = 0; counter < 16; counter++)
        {
            password[counter] = charSet[random.Next(charSet.Length - 1)];
        }

        return string.Join(null, password);
    }
}