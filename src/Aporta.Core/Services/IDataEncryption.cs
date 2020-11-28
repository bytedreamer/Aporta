namespace Aporta.Core.Services
{
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
        /// <returns></returns>
        string Hash(string value);

        /// <summary>
        /// Check that the value matches the hash
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        bool CheckMatch(string hash, string value);
    }
}