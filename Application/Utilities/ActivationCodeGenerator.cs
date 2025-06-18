using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace ArandanoIRT_Backend.Application.Utilities
{
    /// <summary>
    /// Provides utility methods for generating secure activation codes for devices.
    /// </summary>
    public static class ActivationCodeGenerator
    {
        // Define the desired byte length for the activation code.
        private const int ACTIVATION_CODE_BYTE_LENGTH = 8;

        /// <summary>
        /// Generates a new cryptographically secure random string to be used as a device activation code.
        /// The code is Base64Url encoded to be safe for use in URLs or configurations.
        /// </summary>
        /// <returns>A unique, secure activation code string.</returns>
        public static string GenerateActivationCode()
        {
            using var rng = RandomNumberGenerator.Create();
            var randomBytes = new byte[ACTIVATION_CODE_BYTE_LENGTH];
            rng.GetBytes(randomBytes);
            // Base64Url encoding provides URL-safe characters ('+', '/', '=') replaced.
            return Base64UrlEncoder.Encode(randomBytes);
        }
    }
}