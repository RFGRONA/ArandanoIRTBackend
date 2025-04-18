using ArandanoIRT_Backend.Application.Interfaces.Utilities;

namespace ArandanoIRT_Backend.Infrastructure.Utilities 
{
    /// <summary>
    /// Implements the <see cref="IPasswordHasher"/> interface using the BCrypt algorithm
    /// provided by the BCrypt.Net library.
    /// </summary>
    public class BCryptPasswordHasher : IPasswordHasher
    {
        /// <summary>
        /// Creates a hash from a plain text password using BCrypt.
        /// </summary>
        /// <param name="password">The plain text password to hash.</param>
        /// <returns>The generated BCrypt password hash string.</returns>
        /// <exception cref="ArgumentException">Thrown if the input password is null, empty, or whitespace.</exception>
        public string Hash(string password)
        {
            // Validates that the input password is not null or empty.
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty.", nameof(password));
            // Hashes the password using the default work factor provided by BCrypt.Net.
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        /// <summary>
        /// Verifies a plain text password against a stored BCrypt hash.
        /// </summary>
        /// <param name="hash">The stored BCrypt password hash.</param>
        /// <param name="providedPassword">The plain text password provided by the user for verification.</param>
        /// <returns><c>true</c> if the provided password matches the hash; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// Returns <c>false</c> immediately if either the hash or the provided password is null or empty.
        /// Also returns <c>false</c> and logs a warning if the provided hash string is not a valid BCrypt format.
        /// </remarks>
        public bool Verify(string hash, string providedPassword)
        {
            // Performs basic validation on input strings. Returns false if inputs are invalid.
            if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(providedPassword))
                return false;

            try
            {
                // Verifies the provided plain text password against the stored BCrypt hash.
                return BCrypt.Net.BCrypt.Verify(providedPassword, hash);
            }
            // Catches exceptions typically thrown by BCrypt.Verify for invalid hash formats.
            catch (Exception ex) when (ex is FormatException || ex is BCrypt.Net.SaltParseException) // More specific catch for BCrypt
            {
                // Logs a warning indicating an invalid hash format was encountered during verification.
                Serilog.Log.Warning(ex, "Password hash validation failed due to invalid format or salt parse error. Hash prefix: {HashPrefix}", hash.Length > 6 ? hash.Substring(0, 6) : hash); // Log only prefix or length
                // Returns false as the password cannot be verified against an invalid hash.
                return false;
            }
        }
    }
}