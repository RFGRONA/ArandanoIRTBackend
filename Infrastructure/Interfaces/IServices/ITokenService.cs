using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.ValueObjects;
using System.Security.Claims;


namespace ArandanoIRT_Backend.Infrastructure.Interfaces.IServices
{
    /// <summary>
    /// Defines a contract for services responsible for handling the lifecycle of authentication tokens,
    /// including the generation, storage, validation, refresh, and revocation of JWT access tokens
    /// and persistent refresh tokens.
    /// </summary>
    public interface ITokenService
    {
        /// <summary>
        /// Asynchronously generates a new set of access and refresh tokens for a specified user.
        /// Creates and stores the refresh token information (including context like IP, UserAgent) in the database.
        /// </summary>
        /// <param name="person">The <see cref="PersonEntity"/> for whom to generate tokens.</param>
        /// <param name="ipAddress">The IP address of the client requesting the tokens.</param>
        /// <param name="userAgent">The user agent string of the client requesting the tokens.</param>
        /// <param name="deviceInfo">Optional descriptive information about the client device.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> which holds a <see cref="TokenResponseDto"/> (containing the access token,
        /// refresh token, and access token expiry) if successful, or a failure result otherwise.
        /// </returns>
        Task<Result<TokenResponseDto>> GenerateAndStoreTokensAsync(PersonEntity person, string ipAddress, string userAgent, string? deviceInfo = null);

        /// <summary>
        /// Asynchronously validates a provided refresh token against the database, generates a new set of access
        /// and refresh tokens if the provided token is valid and active, revokes the old refresh token (marking it as replaced),
        /// and stores the new refresh token information.
        /// </summary>
        /// <param name="refreshTokenValue">The opaque refresh token string provided by the client.</param>
        /// <param name="ipAddress">The IP address of the client making the refresh request.</param>
        /// <param name="userAgent">The user agent string of the client making the refresh request.</param>
        /// <param name="deviceInfo">Optional descriptive information about the client device making the refresh request.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> which holds a new <see cref="TokenResponseDto"/> if successful,
        /// or a failure result with an error message otherwise (e.g., token not found, expired, revoked).
        /// </returns>
        Task<Result<TokenResponseDto>> RefreshTokensAsync(string refreshTokenValue, string ipAddress, string userAgent, string? deviceInfo = null);

        /// <summary>
        /// Asynchronously revokes a specific refresh token stored in the database, marking it as invalid.
        /// </summary>
        /// <param name="refreshTokenValue">The opaque refresh token string to revoke.</param>
        /// <param name="ipAddress">The IP address initiating the revocation action (for auditing purposes).</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result"/> indicating success or failure of the revocation operation.
        /// </returns>
        Task<Result> RevokeRefreshTokenAsync(string refreshTokenValue, string ipAddress);

        /// <summary>
        /// Attempts to extract the claims principal from a JWT access token string, potentially ignoring lifetime validation
        /// (allowing expired tokens to be read) but still validating the signature.
        /// Useful for retrieving user information from an expired token during the refresh process.
        /// </summary>
        /// <param name="token">The JWT access token string (can be expired but must be well-formed and have a valid signature).</param>
        /// <returns>
        /// The <see cref="ClaimsPrincipal"/> extracted from the token if parsing and signature validation succeed;
        /// otherwise, <c>null</c>.
        /// </returns>
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);

        /// <summary>
        /// Asynchronously retrieves the session identifier associated with a given refresh token string from the database.
        /// </summary>
        /// <param name="refreshTokenValue">The opaque refresh token string.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> which holds the session ID (<c>long</c>) if the token is found,
        /// or a failure result otherwise.
        /// </returns>
        Task<Result<long>> GetSessionIdFromTokenAsync(string refreshTokenValue);
    }
}