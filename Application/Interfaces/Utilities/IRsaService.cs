namespace ArandanoIRT_Backend.Application.Interfaces.Utilities
{
    /// <summary>
    /// Service interface for RSA encryption and decryption operations.
    /// </summary>
    public interface IRsaService
    {
        /// <summary>
        /// Encrypts data using the configured public key.
        /// </summary>
        /// <param name="plainText">The data to encrypt.</param>
        /// <returns>The Base64 encoded encrypted string.</returns>
        string Encrypt(string plainText);

        /// <summary>
        /// Decrypts data using the configured private key.
        /// </summary>
        /// <param name="encryptedText">The Base64 encoded encrypted string.</param>
        /// <returns>The decrypted plain text.</returns>
        string Decrypt(string encryptedText);

        /// <summary>
        /// Gets the public key in PEM format.
        /// </summary>
        /// <returns>The public key string.</returns>
        string GetPublicKeyPem();
    }
}
