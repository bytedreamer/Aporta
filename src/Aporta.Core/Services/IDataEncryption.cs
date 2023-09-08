namespace Aporta.Core.Services;

public interface IDataEncryption
{
    /// <summary>
    /// Encrypt a value
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    string Encrypt(string value);
        
    /// <summary>
    /// Decrypt a value
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    string Decrypt(string value);
        
    /// <summary>
    /// Create a one way hash of a value
    /// </summary>
    /// <param name="value"></param>
    /// <param name="salt"></param>
    /// <returns></returns>
    string Hash(string value, byte[] salt);

    /// <summary>
    /// Generate salt used for hashing
    /// </summary>
    /// <returns></returns>
    byte[] GenerateSalt();

    /// <summary>
    /// Generate a random password
    /// </summary>
    /// <returns></returns>
    string GeneratePassword();
}