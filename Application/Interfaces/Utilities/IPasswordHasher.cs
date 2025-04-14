namespace ArandanoIRT_Backend.Application.Interfaces.Utilities
{
    /// <summary>
    /// Service interface for hashing and verifying passwords.
    /// </summary>
    public interface IPasswordHasher
    {
        /// <summary>
        /// Creates a hash from a plain text password.
        /// </summary>
        /// <param name="password">The plain text password.</param>
        /// <returns>The hashed password.</returns>
        string Hash(string password);

        /// <summary>
        /// Verifies a plain text password against a stored hash.
        /// </summary>
        /// <param name="hash">The stored password hash.</param>
        /// <param name="providedPassword">The plain text password provided by the user.</param>
        /// <returns>True if the password matches the hash, false otherwise.</returns>
        bool Verify(string hash, string providedPassword);
    }
}