using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Application.Interfaces.Utilities
{
    /// <summary>
    /// Defines the contract for shared utility functions used across authentication-related services.
    /// </summary>
    public interface IAuthUtilities
    {
        /// <summary>
        /// Finds a user by email in the repository.
        /// </summary>
        /// <param name="email">The email address to search for.</param>
        /// <returns>A Task resulting in a Result containing the PersonEntity on success, or Failure if not found.</returns>
        Task<Result<PersonEntity>> FindUserByEmailAsync(string email);

        /// <summary>
        /// Decrypts, validates policy, and hashes a password.
        /// </summary>
        /// <param name="encryptedPassword">The encrypted password string.</param>
        /// <param name="operationContextForLog">A string indicating the context (e.g., "AdminRegistration", "PasswordReset") for logging purposes.</param>
        /// <returns>Result containing the hashed password on success, or Failure.</returns>
        Result<string> ProcessPassword(string encryptedPassword, string operationContextForLog);
    }
}