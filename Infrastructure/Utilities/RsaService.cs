using System.Security.Cryptography;
using System.Text;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;

namespace ArandanoIRT_Backend.Infrastructure.Utilities 
{
    /// <summary>
    /// Implements the <see cref="IRsaService"/> interface, providing functionalities
    /// for RSA encryption (using public key) and decryption (using private key).
    /// Manages an internally generated RSA key pair.
    /// </summary>
    public class RsaService : IRsaService
    {
        /// <summary>
        /// Internal utility class instance that handles key generation and core crypto operations.
        /// </summary>
        private readonly RSAUtility _rsaUtility;
        /// <summary>
        /// Logger instance for logging RSA operations and errors.
        /// </summary>
        private readonly ILogger<RsaService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RsaService"/> class.
        /// Creates an internal <see cref="RSAUtility"/> which generates a new RSA key pair upon instantiation.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if RSA key pair generation fails during initialization.</exception>
        public RsaService(ILogger<RsaService> logger)
        {

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                // Creates the utility which generates the RSA key pair.
                _rsaUtility = new RSAUtility();
                _logger.LogInformation("RSA keys generated successfully and RsaService initialized.");
            }
            catch (Exception ex) // Catches potential errors during key generation.
            {
                _logger.LogError(ex, "Failed to initialize RSA keys within RSAUtility. RSA functionality will be unavailable.");
                // Wraps the original exception to provide context.
                throw new InvalidOperationException("Fatal error: Failed to initialize RSA keys.", ex);
            }
        }

        /// <inheritdoc/>
        public string Encrypt(string plainText)
        {
            try
            {
                // Delegates encryption to the internal utility using the public key.
                return _rsaUtility.EncryptWithPublicKey(plainText);
            }
            catch (Exception ex) // Catches errors during encryption.
            {
                _logger.LogError(ex, "RSA encryption failed.");
                // Throws a cryptographic exception indicating encryption failure.
                throw new CryptographicException("RSA encryption failed.", ex);
            }
        }

        /// <inheritdoc/>
        public string Decrypt(string encryptedText)
        {
            try
            {
                // Delegates decryption to the internal utility using the private key.
                return _rsaUtility.DecryptWithPrivateKey(encryptedText);
            }
            // Specifically catches crypto errors (e.g., bad padding, invalid base64) which might indicate bad data.
            catch (CryptographicException cryptEx)
            {
                _logger.LogWarning(cryptEx, "RSA decryption failed, likely due to invalid encrypted data format, padding, or key mismatch.");
                throw; // Re-throws the original cryptographic exception.
            }
            // Catches any other unexpected errors during decryption.
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during RSA decryption.");
                // Throws a new cryptographic exception indicating an unexpected decryption failure.
                throw new CryptographicException("RSA decryption failed unexpectedly.", ex);
            }
        }

        /// <inheritdoc/>
        public string GetPublicKeyPem()
        {
            // Retrieves the public key in PEM format from the internal utility.
            return _rsaUtility.GetPublicKey();
        }
    }

    /// <summary>
    /// Internal helper class responsible for generating an RSA key pair and performing
    /// core encryption and decryption operations using that key pair.
    /// </summary>
    internal class RSAUtility
    {
        /// <summary>
        /// Holds the generated RSA public and private keys as strings.
        /// </summary>
        private readonly RsaKeys _rsaKeys;

        /// <summary>
        /// Initializes a new instance of the <see cref="RSAUtility"/> class.
        /// Generates a new 2048-bit RSA key pair upon creation.
        /// Stores the private key as a Base64 string and the public key as a PEM string.
        /// </summary>
        /// <remarks>
        /// Uses <see cref="RSACryptoServiceProvider"/> for key generation. Consider <see cref="RSA.Create()"/> for broader platform compatibility if needed.
        /// </remarks>
        public RSAUtility()
        {
            _rsaKeys = new RsaKeys();
            // Creates an RSA instance with a 2048-bit key size. Dispose is handled by 'using'.
            using var rsa = new RSACryptoServiceProvider(2048);
            // Exports the private key in PKCS#8 format and converts to Base64 string for storage.
            _rsaKeys.PrivateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
            // Exports the public key in SubjectPublicKeyInfo format as a PEM string using an extension method.
            _rsaKeys.PublicKey = rsa.ExportSubjectPublicKeyInfoPem();
        }

        /// <summary>
        /// Encrypts plain text data using the generated RSA public key with OAEP SHA-256 padding.
        /// </summary>
        /// <param name="data">The plain text string to encrypt.</param>
        /// <returns>The Base64 encoded string representation of the encrypted data.</returns>
        public string EncryptWithPublicKey(string data)
        {
            // Converts the plain text string to bytes using UTF-8 encoding.
            byte[] dataToEncrypt = Encoding.UTF8.GetBytes(data);
            // Creates a new RSA instance for the encryption operation.
            using var rsa = RSA.Create();
            // Imports the public key from the stored PEM string.
            rsa.ImportFromPem(_rsaKeys.PublicKey);
            // Encrypts the data using OAEP padding with SHA-256 hash algorithm.
            byte[] encryptedData = rsa.Encrypt(dataToEncrypt, RSAEncryptionPadding.OaepSHA256);
            // Converts the encrypted byte array to a Base64 string for transport/storage.
            return Convert.ToBase64String(encryptedData);
        }

        /// <summary>
        /// Decrypts Base64 encoded data using the generated RSA private key with OAEP SHA-256 padding.
        /// </summary>
        /// <param name="encryptedData">The Base64 encoded string of the encrypted data.</param>
        /// <returns>The original plain text string.</returns>
        /// <exception cref="CryptographicException">Thrown if decryption fails (e.g., invalid data, padding mismatch, key mismatch).</exception>
        public string DecryptWithPrivateKey(string encryptedData)
        {
            // Converts the Base64 encoded input string back to a byte array.
            byte[] dataToDecrypt = Convert.FromBase64String(encryptedData);
            // Creates a new RSA instance for the decryption operation.
            using var rsa = RSA.Create();
            // Imports the private key from the stored Base64 string (PKCS#8 format expected).
            rsa.ImportRSAPrivateKey(Convert.FromBase64String(_rsaKeys.PrivateKey), out _);
            // Decrypts the data using OAEP padding with SHA-256 hash algorithm.
            byte[] decryptedData = rsa.Decrypt(dataToDecrypt, RSAEncryptionPadding.OaepSHA256);
            // Converts the decrypted byte array back to a string using UTF-8 encoding.
            return Encoding.UTF8.GetString(decryptedData);
        }

        /// <summary>
        /// Gets the public key in PEM format.
        /// </summary>
        /// <returns>The public key as a PEM formatted string.</returns>
        public string GetPublicKey()
        {
            // Returns the stored public key PEM string (potentially trimmed).
            return _rsaKeys.PublicKey.Trim();
        }
    }

    /// <summary>
    /// Internal container class to hold the RSA private and public keys as strings.
    /// </summary>
    internal class RsaKeys
    {
        /// <summary>
        /// Gets or sets the private key, typically stored as a Base64 encoded string (e.g., PKCS#8 format).
        /// </summary>
        public string PrivateKey { get; set; } = string.Empty; 

        /// <summary>
        /// Gets or sets the public key, typically stored as a PEM formatted string (e.g., SubjectPublicKeyInfo format).
        /// </summary>
        public string PublicKey { get; set; } = string.Empty; 
    }

    /// <summary>
    /// Represents a simple request structure containing a password string.
    /// </summary>
    /// <remarks>
    /// This class appears somewhat out of place within RSA utilities but is included as provided.
    /// Its purpose might be related to receiving encrypted password data in API requests elsewhere.
    /// </remarks>
    public class PasswordRequest
    {
        /// <summary>
        /// Gets or sets the password string.
        /// </summary>
        public string Password { get; set; } = string.Empty; 
    }

    /// <summary>
    /// Provides internal static extension methods related to RSA key formatting, primarily for PEM encoding.
    /// </summary>
    internal static class RSAExtensions
    {
        /// <summary>
        /// Formats raw byte data (e.g., a key) into a standard PEM string format with line breaks.
        /// </summary>
        /// <param name="data">The raw byte data to format.</param>
        /// <param name="label">The label to use in the BEGIN/END markers (e.g., "PUBLIC KEY", "RSA PRIVATE KEY").</param>
        /// <returns>A PEM formatted string representation of the data.</returns>
        public static string ToStringPem(this byte[] data, string label)
        {
            var pemBuilder = new StringBuilder();
            // Appends the BEGIN marker.
            pemBuilder.AppendLine($"-----BEGIN {label}-----");

            // Converts the byte data to Base64.
            var base64 = Convert.ToBase64String(data);
            // Appends the Base64 data, chunked into 64-character lines.
            for (int i = 0; i < base64.Length; i += 64)
            {
                if (i + 64 < base64.Length)
                {
                    pemBuilder.AppendLine(base64.Substring(i, 64));
                }
                else
                {
                    pemBuilder.AppendLine(base64.Substring(i));
                }
            }

            // Appends the END marker.
            pemBuilder.AppendLine($"-----END {label}-----");
            return pemBuilder.ToString();
        }

        /// <summary>
        /// Exports the public key of an RSA instance into the standard SubjectPublicKeyInfo PEM format.
        /// </summary>
        /// <param name="rsa">The RSA instance whose public key is to be exported.</param>
        /// <returns>A string containing the public key in PEM format.</returns>
        public static string ExportSubjectPublicKeyInfoPem(this RSA rsa)
        {
            // Exports the public key in binary SubjectPublicKeyInfo format.
            var publicKey = rsa.ExportSubjectPublicKeyInfo();
            // Formats the binary data into PEM using the "PUBLIC KEY" label.
            return publicKey.ToStringPem("PUBLIC KEY");
        }
    }
}
