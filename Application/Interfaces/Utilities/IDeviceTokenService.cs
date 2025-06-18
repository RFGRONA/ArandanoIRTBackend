using ArandanoIRT_Backend.Application.DTOs.Device;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Application.Interfaces.Utilities
{
    /// <summary>
    /// Defines the interface for the service responsible for managing authentication tokens for devices.
    /// Handles generation, validation, refresh, and revocation of opaque tokens.
    /// </summary>
    public interface IDeviceTokenService
    {
        /// <summary>
        /// Generates a new pair of opaque access and refresh tokens for a device
        /// and stores the refresh token record in the database.
        /// </summary>
        /// <param name="deviceId">The ID of the device the tokens are for.</param>
        /// <param name="ipAddress">The IP address of the device.</param>
        /// <param name="userAgent">The user agent string from the device's request.</param>
        /// <param name="deviceInfo">Formatted device information string.</param>
        /// <returns>
        /// A <see cref="Result{T}"/> indicating success and the created <see cref="DeviceTokenResponseDto"/>
        /// containing the generated token values and expiries, or failure with an error message.
        /// </returns>
        // Updated return type to use the new response DTO
        Task<Result<DeviceTokenResponseDto>> GenerateAndStoreTokensAsync(int deviceId, string ipAddress, string userAgent, string? deviceInfo);

        /// <summary>
        /// Validates an opaque access or refresh token provided by a device.
        /// Checks against the database for existence, validity period, and revoked status.
        /// </summary>
        /// <param name="tokenValue">The opaque token string to validate.</param>
        /// <returns>
        /// A <see cref="Result{T}"/> indicating success and the validated <see cref="DeviceTokenResponseDto"/>
        /// if the token is active and found, or failure with a specific reason (e.g., not found, expired, revoked).
        /// </returns>
        // Updated return type to use the new response DTO
        Task<Result<DeviceTokenResponseDto>> ValidateTokenAsync(string tokenValue);

        /// <summary>
        /// Revokes a specific opaque token (access or refresh) by its string value.
        /// Marks the token as revoked in the database.
        /// </summary>
        /// <param name="tokenValue">The opaque token string to revoke.</param>
        /// <param name="ipAddress">The IP address from which the revocation request originated.</param>
        /// <returns>
        /// A <see cref="Result"/> indicating success (even if the token wasn't found or was already revoked)
        /// or failure if a database error occurs.
        /// </returns>
        Task<Result<bool>> RevokeTokenAsync(string tokenValue, string ipAddress);


        /// <summary>
        /// Revokes all active opaque tokens associated with a specific device ID.
        /// Useful when a device is deactivated or deleted.
        /// </summary>
        /// <param name="deviceId">The ID of the device whose tokens should be revoked.</param>
        /// <param name="ipAddress">The IP address associated with the revocation action (e.g., administrator's IP).</param>
        /// <returns>
        /// A <see cref="Result"/> indicating success (even if no tokens were found to revoke)
        /// or failure if a database error occurs.
        /// </returns>
        Task<Result<bool>> RevokeTokensByDeviceIdAsync(int deviceId, string ipAddress);


        /// <summary>
        /// Refreshes a device's token pair using a valid opaque refresh token.
        /// Generates a new pair, stores the new refresh token, and revokes the old one.
        /// </summary>
        /// <param name="refreshTokenValue">The opaque refresh token provided by the device.</param>
        /// <param name="ipAddress">The IP address of the device.</param>
        /// <param name="userAgent">The user agent string from the device's request.</param>
        /// <param name="deviceInfo">Formatted device information string.</param>
        /// <returns>
        /// A <see cref="Result{T}"/> indicating success and the new <see cref="DeviceTokenResponseDto"/>
        /// containing the new token values, or failure if the provided refresh token is invalid/expired/revoked.
        /// </returns>
        // Updated return type to use the new response DTO
        Task<Result<DeviceTokenResponseDto>> RefreshDeviceTokensAsync(string refreshTokenValue, string ipAddress, string userAgent, string? deviceInfo);
    }
}