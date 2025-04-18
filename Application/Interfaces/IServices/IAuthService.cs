// ArandanoIRT_Backend.Application/Interfaces/Services/IAuthService.cs
using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Application.Interfaces.Services
{
    /// <summary>
    /// Defines the contract for authentication related operations.
    /// Handles user registration, login, token management, and password recovery processes.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Registers a new administrator user along with their initial crop.
        /// </summary>
        /// <param name="request">DTO containing admin and crop information.</param>
        /// <returns>A result indicating success or failure.</returns>
        Task<Result> RegisterAdminAsync(RegisterAdminRequestDto request);

        /// <summary>
        /// Registers a new regular user using an invitation code.
        /// </summary>
        /// <param name="request">DTO containing user information and the invitation code.</param>
        /// <returns>A result indicating success or failure.</returns>
        Task<Result> RegisterUserAsync(RegisterUserRequestDto request);

        /// <summary>
        /// Authenticates a user based on email and password.
        /// </summary>
        /// <param name="request">DTO containing login credentials and remember me flag.</param>
        /// <param name="ipAddress">The IP address of the requesting client.</param>
        /// <param name="userAgent">The user agent string of the requesting client.</param>
        /// <param name="deviceInfo">Optional device information.</param>
        /// <returns>A result containing the login payload (user details + tokens) on success, or an error message on failure.</returns>
        Task<Result<LoginSuccessPayload>> LoginAsync(LoginRequestDto request, string ipAddress, string userAgent, string? deviceInfo = null);

        /// <summary>
        /// Refreshes the access token using a valid refresh token.
        /// Implements refresh token rotation.
        /// </summary>
        /// <param name="refreshTokenValue">The opaque refresh token string.</param>
        /// <param name="ipAddress">The IP address of the requesting client.</param>
        /// <param name="userAgent">The user agent string of the requesting client.</param>
        /// <param name="deviceInfo">Optional device information.</param>
        /// <returns>A result containing the new access and refresh tokens on success, or an error message on failure.</returns>
        Task<Result<TokenResponseDto>> RefreshTokenAsync(string refreshTokenValue, string ipAddress, string userAgent, string? deviceInfo = null);

        /// <summary>
        /// Initiates the password reset process for a given email address.
        /// Generates a reset token and typically sends it via email.
        /// </summary>
        /// <param name="request">DTO containing the user's email.</param>
        /// <returns>A result indicating success or failure of initiating the process.</returns>
        Task<Result> RequestPasswordResetAsync(ForgotPasswordRequestDto request); // Using your DTO name, consider renaming to ForgotPasswordRequestDto

        /// <summary>
        /// Resets the user's password using a valid reset token.
        /// </summary>
        /// <param name="request">DTO containing the reset token, new password, and confirmation.</param>
        /// <returns>A result indicating success or failure of the password reset.</returns>
        Task<Result> ResetPasswordAsync(ChangePasswordRequestDto request);

        /// <summary>
        /// Revokes the specific refresh token used for the current session (logout).
        /// </summary>
        /// <param name="refreshTokenValue">The opaque refresh token string to revoke.</param>
        /// <param name="ipAddress">The IP address performing the logout.</param>
        /// <returns>A result indicating success or failure.</returns>
        Task<Result> LogoutAsync(string refreshTokenValue, string ipAddress);

        /// <summary>
        /// (Optional) Revokes all active refresh tokens associated with the user's session ID (logout everywhere).
        /// Requires retrieving the session ID from an active token.
        /// </summary>
        /// <param name="sessionId">The session ID to revoke tokens for.</param>
        /// <param name="ipAddress">The IP address performing the logout.</param>
        /// <returns>A result indicating success or failure.</returns>
        Task<Result> LogoutEverywhereAsync(long sessionId, string ipAddress);

        /// <summary>
        /// Processes a help request submitted by an unauthenticated user.
        /// Validates the associated crop name and sends an email notification to the crop's administrator.
        /// </summary>
        /// <param name="request">The help request data transfer object containing user details, message, and crop name.</param>
        /// <returns>A Task representing the asynchronous operation, with a result indicating success or failure.</returns>
        Task<Result> SendHelpRequestAsync(HelpRequestDto request);
    }
}