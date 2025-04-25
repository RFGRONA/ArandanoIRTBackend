using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Application.Interfaces.IServices
{
    /// <summary>
    /// Defines the contract for user session management operations,
    /// including login, token refresh, and logout.
    /// </summary>
    public interface IAuthSessionService
    {
        /// <summary>
        /// Authenticates a user based on email and password without device information.
        /// </summary>
        /// <param name="request">DTO containing login credentials.</param>
        /// <param name="ipAddress">The IP address of the requesting client.</param>
        /// <param name="userAgent">The user agent string of the requesting client.</param>
        /// <returns>A Task resulting in a Result containing the login payload (user details + tokens) on success, or failure.</returns>
        Task<Result<LoginSuccessPayload>> LoginAsync(LoginRequestDto request, string ipAddress, string userAgent);

        /// <summary>
        /// Authenticates a user based on email and password, including device information.
        /// </summary>
        /// <param name="request">DTO containing login credentials.</param>
        /// <param name="ipAddress">The IP address of the requesting client.</param>
        /// <param name="userAgent">The user agent string of the requesting client.</param>
        /// <param name="deviceInfo">Specific device information (nullable).</param>
        /// <returns>A Task resulting in a Result containing the login payload (user details + tokens) on success, or failure.</returns>
        Task<Result<LoginSuccessPayload>> LoginAsync(LoginRequestDto request, string ipAddress, string userAgent, string? deviceInfo);

        /// <summary>
        /// Refreshes the access token using a valid refresh token without device information.
        /// Implements refresh token rotation via the underlying token service.
        /// </summary>
        /// <param name="refreshTokenValue">The opaque refresh token string.</param>
        /// <param name="ipAddress">The IP address of the requesting client.</param>
        /// <param name="userAgent">The user agent string of the requesting client.</param>
        /// <returns>A Task resulting in a Result containing the new access and refresh tokens on success, or failure.</returns>
        Task<Result<TokenResponseDto>> RefreshTokenAsync(string refreshTokenValue, string ipAddress, string userAgent);

        /// <summary>
        /// Refreshes the access token using a valid refresh token, including device information.
        /// Implements refresh token rotation via the underlying token service.
        /// </summary>
        /// <param name="refreshTokenValue">The opaque refresh token string.</param>
        /// <param name="ipAddress">The IP address of the requesting client.</param>
        /// <param name="userAgent">The user agent string of the requesting client.</param>
        /// <param name="deviceInfo">Specific device information (nullable).</param>
        /// <returns>A Task resulting in a Result containing the new access and refresh tokens on success, or failure.</returns>
        Task<Result<TokenResponseDto>> RefreshTokenAsync(string refreshTokenValue, string ipAddress, string userAgent, string? deviceInfo);

        /// <summary>
        /// Revokes the specific refresh token used for the current session (logout).
        /// </summary>
        /// <param name="refreshTokenValue">The opaque refresh token string to revoke.</param>
        /// <param name="ipAddress">The IP address performing the logout.</param>
        /// <returns>A Task representing the asynchronous operation, with a result indicating success or failure.</returns>
        Task<Result> LogoutAsync(string refreshTokenValue, string ipAddress);

        /// <summary>
        /// Revokes all active refresh tokens associated with a user's session ID (logout everywhere).
        /// </summary>
        /// <param name="sessionId">The session ID (typically from the JWT 'sid' claim) to revoke tokens for.</param>
        /// <param name="ipAddress">The IP address performing the logout.</param>
        /// <returns>A Task representing the asynchronous operation, with a result indicating success or failure.</returns>
        Task<Result> LogoutEverywhereAsync(long sessionId, string ipAddress);
    }
}
