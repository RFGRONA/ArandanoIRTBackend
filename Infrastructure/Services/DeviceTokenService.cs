using ArandanoIRT_Backend.Application.DTOs.Device;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects; 
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace ArandanoIRT_Backend.Infrastructure.Services
{
    /// <summary>
    /// Implements the <see cref="IDeviceTokenService"/> interface, providing services
    /// for managing opaque authentication tokens for devices.
    /// </summary>
    public class DeviceTokenService : IDeviceTokenService
    {
        private readonly IDeviceTokenRepository _deviceTokenRepository;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<DeviceTokenService> _logger;
        private readonly int _accessTokenExpiryMinutes; // Expiry configured for device access token
        private readonly int _refreshTokenExpiryDays; // Expiry configured for device refresh token
        private const int ACCESS_TOKEN_BYTE_LENGTH = 32; // Length for opaque access token string (Base64Url ~43 chars)
        private const int REFRESH_TOKEN_BYTE_LENGTH = 64; // Length for opaque refresh token string (Base64Url ~86 chars)


        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceTokenService"/> class.
        /// </summary>
        /// <param name="deviceTokenRepository">The repository for device tokens.</param>
        /// <param name="dateTimeProvider">The provider for current date and time.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if dependencies are null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if required configuration values are missing or invalid.</exception>
        public DeviceTokenService(
            IDeviceTokenRepository deviceTokenRepository,
            IDateTimeProvider dateTimeProvider,
            IConfiguration configuration,
            ILogger<DeviceTokenService> logger)
        {
            _deviceTokenRepository = deviceTokenRepository ?? throw new ArgumentNullException(nameof(deviceTokenRepository));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Read configuration for token expiry times.
            if (!int.TryParse(configuration["DeviceToken:AccessTokenExpiryMinutes"], out _accessTokenExpiryMinutes) || _accessTokenExpiryMinutes <= 0)
            {
                _logger.LogWarning("DeviceToken:AccessTokenExpiryMinutes configuration is missing or invalid. Using default 60 minutes.");
                _accessTokenExpiryMinutes = 60; // Default value
            }

            if (!int.TryParse(configuration["DeviceToken:RefreshTokenExpiryDays"], out _refreshTokenExpiryDays) || _refreshTokenExpiryDays <= 0)
            {
                _logger.LogWarning("DeviceToken:RefreshTokenExpiryDays configuration is missing or invalid. Using default 30 days.");
                _refreshTokenExpiryDays = 30; // Default value
            }

            _logger.LogInformation("DeviceTokenService initialized with Access Token expiry {AccessTokenExpiry} mins, Refresh Token expiry {RefreshTokenExpiry} days.", _accessTokenExpiryMinutes, _refreshTokenExpiryDays);
        }

        /// <inheritdoc/>
        // Changed return type to Result<DeviceTokenResponseDto>
        public async Task<Result<DeviceTokenResponseDto>> GenerateAndStoreTokensAsync(int deviceId, string ipAddress, string userAgent, string? deviceInfo)
        {
            _logger.LogInformation("Generating tokens for Device ID: {DeviceId}", deviceId);

            if (deviceId <= 0)
            {
                _logger.LogWarning("Attempted to generate tokens for invalid device ID: {DeviceId}", deviceId);
                return Result<DeviceTokenResponseDto>.Failure("Invalid device ID for token generation.");
            }
            if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(userAgent))
            {
                _logger.LogWarning("IP address or User agent is missing for device token generation. Device ID: {DeviceId}", deviceId);
                return Result<DeviceTokenResponseDto>.Failure("Client context information (IP, UserAgent) is required.");
            }


            try
            {
                var now = _dateTimeProvider.GetUtcNow();
                // Access token expiry is relative to creation time
                var accessTokenExpiry = now.AddMinutes(_accessTokenExpiryMinutes);
                // Refresh token expiry is also relative to creation time for this new pair
                var refreshTokenExpiry = now.AddDays(_refreshTokenExpiryDays);


                // Generate opaque tokens
                var accessTokenValue = GenerateSecureRandomString(ACCESS_TOKEN_BYTE_LENGTH);
                var refreshTokenValue = GenerateSecureRandomString(REFRESH_TOKEN_BYTE_LENGTH);

                // Create DeviceTokenEntity.
                var newTokenEntity = new DeviceTokenEntity(
                    idDeviceToken: 0, // DB will generate ID
                    deviceId: deviceId,
                    token: accessTokenValue, // Store Access Token in 'Token' field
                    refreshToken: refreshTokenValue, // Store Refresh Token in 'RefreshToken' field
                    createdAt: now,
                    expiresAt: refreshTokenExpiry,
                    revokedAt: null,
                    revokedByIp: ipAddress, // Using creation IP as placeholder, will be set on revoke.
                    deviceInfo: deviceInfo ?? string.Empty, // Handle null deviceInfo, Entity constructor expects string
                    userAgent: userAgent
                );

                // Store the new device token record
                var createResult = await _deviceTokenRepository.Create(newTokenEntity);

                if (createResult.IsFailure)
                {
                    _logger.LogError("Failed to store new DeviceToken for Device ID {DeviceId}. Error: {Error}", deviceId, createResult.ErrorMessage);
                    return Result<DeviceTokenResponseDto>.Failure("Failed to save device token. Please try again.");
                }

                var createdTokenEntity = createResult.Value; // The saved entity with DB ID


                _logger.LogInformation("Generated and stored new DeviceToken (ID: {DeviceTokenId}) for Device ID {DeviceId}. Access expires: {AccessExpiry}, Refresh expires: {RefreshExpiry}",
                                        createdTokenEntity.IdDeviceToken, deviceId, accessTokenExpiry, refreshTokenExpiry);

                // Construct and return the Response DTO
                var responseDto = new DeviceTokenResponseDto
                {
                    AccessToken = createdTokenEntity.Token,
                    RefreshToken = createdTokenEntity.RefreshToken,
                    AccessTokenExpiration = accessTokenExpiry, // Include calculated Access Token expiry
                    RefreshTokenExpiration = createdTokenEntity.ExpiresAt, // Use entity's ExpiresAt (Refresh expiry)
                    DeviceTokenId = createdTokenEntity.IdDeviceToken,
                    DeviceId = createdTokenEntity.DeviceId // DeviceId is nullable in entity, required in DTO? Check DTO. Nullable in DTO too.
                };

                return Result<DeviceTokenResponseDto>.Success(responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating tokens for Device ID {DeviceId}.", deviceId);
                return Result<DeviceTokenResponseDto>.Failure("An unexpected error occurred during token generation.");
            }
        }

        /// <summary>
        /// Generates a cryptographically secure random string (Base64Url encoded).
        /// </summary>
        private static string GenerateSecureRandomString(int byteLength)
        {
            using var rng = RandomNumberGenerator.Create();
            var randomBytes = new byte[byteLength];
            rng.GetBytes(randomBytes);
            // Base64Url encoding is safe for use in URLs and headers.
            return Base64UrlEncoder.Encode(randomBytes);
        }


        /// <inheritdoc/>
        // Changed return type to Result<DeviceTokenResponseDto>
        public async Task<Result<DeviceTokenResponseDto>> ValidateTokenAsync(string tokenValue)
        {
            _logger.LogInformation("Validating device token.");

            if (string.IsNullOrWhiteSpace(tokenValue))
            {
                _logger.LogWarning("Attempted to validate empty or null device token string.");
                return Result<DeviceTokenResponseDto>.Failure("Token string cannot be empty.");
            }

            try
            {
                // 1. Attempt to retrieve the token from the database by its value.
                var tokenResult = await _deviceTokenRepository.GetByTokenAsync(tokenValue);

                if (tokenResult.IsFailure)
                {
                    _logger.LogWarning("Device token not found during validation.");
                    return Result<DeviceTokenResponseDto>.Failure("Invalid token."); // Generic message
                }

                var tokenEntity = tokenResult.Value;

                // 2. Check if the token is active (not revoked and not expired).
                var validationResult = ValidateDeviceTokenActivity(tokenEntity);

                if (validationResult.IsFailure)
                {
                    _logger.LogWarning("Device token {TokenId} is inactive. Reason: {Reason}. Device ID: {DeviceId}",
                                       tokenEntity.IdDeviceToken, validationResult.ErrorMessage, tokenEntity.DeviceId);
                    // Return a more specific error message based on the internal reason
                    return Result<DeviceTokenResponseDto>.Failure(validationResult.ErrorMessage ?? "Invalid token.");
                }

                // The token is valid and active.
                _logger.LogDebug("Device token validated successfully for Device ID.");


                // 3. Construct and return the Response DTO with the current token details.
                var accessTokenExpiration = tokenEntity.CreatedAt.AddMinutes(_accessTokenExpiryMinutes);


                var responseDto = new DeviceTokenResponseDto
                {
                    AccessToken = tokenEntity.Token,
                    RefreshToken = tokenEntity.RefreshToken, // Return Refresh token too
                    AccessTokenExpiration = accessTokenExpiration, // Calculated Access Token expiry
                    RefreshTokenExpiration = tokenEntity.ExpiresAt, // Entity's ExpiresAt (Refresh token expiry)
                    DeviceTokenId = tokenEntity.IdDeviceToken,
                    DeviceId = tokenEntity.DeviceId
                };


                _logger.LogInformation("Device token validated successfully for Device ID: {DeviceId}. Token ID: {TokenId}.", tokenEntity.DeviceId, tokenEntity.IdDeviceToken);
                return Result<DeviceTokenResponseDto>.Success(responseDto);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during device token validation.");
                return Result<DeviceTokenResponseDto>.Failure("An unexpected error occurred during token validation.");
            }
        }

        /// <summary>
        /// Checks if a device token entity is active (not revoked and not expired).
        /// </summary>
        /// <param name="tokenEntity">The token entity to check.</param>
        /// <returns>Success Result if active, Failure Result with reason if inactive.</returns>
        private Result ValidateDeviceTokenActivity(DeviceTokenEntity tokenEntity)
        {
            if (tokenEntity.RevokedAt != null)
            {
                return Result.Failure("Token has been revoked.");
            }

            // Entity's ExpiresAt is used for validity check.
            if (tokenEntity.ExpiresAt <= _dateTimeProvider.GetUtcNow())
            {
                return Result.Failure("Token has expired.");
            }

            return Result.Success();
        }


        /// <inheritdoc/>
        public async Task<Result<bool>> RevokeTokenAsync(string tokenValue, string ipAddress)
        {
            _logger.LogInformation("Revoking device token.");

            if (string.IsNullOrWhiteSpace(tokenValue))
            {
                _logger.LogWarning("Attempted to revoke empty or null device token string.");
                return Result<bool>.Success(false); // Nothing to revoke is not a failure.
            }

            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                _logger.LogWarning("IP address is missing for device token revocation.");
                ipAddress = "Unknown IP"; // Use a default if IP is missing but required by entity/repo
            }


            try
            {
                // Delegate the revocation to the repository.
                var revokeResult = await _deviceTokenRepository.RevokeByTokenAsync(tokenValue, _dateTimeProvider.GetUtcNow(), ipAddress);

                if (revokeResult.IsFailure)
                {
                    _logger.LogError("Repository failed to revoke device token. Error: {Error}", revokeResult.ErrorMessage);
                    return Result<bool>.Failure("Failed to revoke token due to a database error.");
                }

                if (revokeResult.Value)
                {
                    _logger.LogInformation("Successfully revoked device token.");
                }
                else
                {
                    _logger.LogInformation("Device token revocation requested, but token not found or already revoked.");
                }

                return revokeResult; // Returns true if successfully revoked, false if not found/already.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during device token revocation.");
                return Result<bool>.Failure("An unexpected error occurred during token revocation.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> RevokeTokensByDeviceIdAsync(int deviceId, string ipAddress)
        {
            _logger.LogInformation("Revoking all device tokens for Device ID: {DeviceId}", deviceId);

            if (deviceId <= 0)
            {
                _logger.LogWarning("Attempted to revoke device tokens for invalid device ID: {DeviceId}", deviceId);
                return Result<bool>.Failure("Invalid device ID provided for revocation.");
            }
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                _logger.LogWarning("IP address is missing for device tokens revocation by device ID: {DeviceId}", deviceId);
                ipAddress = "Unknown IP"; // Use a default if IP is missing
            }


            try
            {
                var revokeResult = await _deviceTokenRepository.RevokeByDeviceIdAsync(deviceId, _dateTimeProvider.GetUtcNow(), ipAddress);

                if (revokeResult.IsFailure)
                {
                    _logger.LogError("Repository failed to revoke device tokens for device ID {DeviceId}. Error: {Error}", deviceId, revokeResult.ErrorMessage);
                    return Result<bool>.Failure("Failed to revoke device tokens due to a database error.");
                }

                // Repository RevokeTokensByDeviceIdAsync returns Success(true) if revoked, Success(false) if no tokens found.
                if (revokeResult.Value)
                {
                    _logger.LogInformation("Successfully initiated revocation for device tokens for Device ID {DeviceId}.", deviceId);
                }
                else
                {
                    _logger.LogInformation("Revocation of device tokens requested for Device ID {DeviceId}, but no active tokens found.", deviceId);
                }

                return revokeResult; // Returns true if successfully revoked, false if no tokens found.

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during device tokens revocation by device ID {DeviceId}.", deviceId);
                return Result<bool>.Failure("An unexpected error occurred during device tokens revocation.");
            }
        }


        /// <inheritdoc/>
        // Changed return type to Result<DeviceTokenResponseDto>
        public async Task<Result<DeviceTokenResponseDto>> RefreshDeviceTokensAsync(string refreshTokenValue, string ipAddress, string userAgent, string? deviceInfo)
        {
            _logger.LogInformation("Refreshing device token from IP: {IPAddress}", ipAddress);

            if (string.IsNullOrWhiteSpace(refreshTokenValue))
                return Result<DeviceTokenResponseDto>.Failure("Refresh token is required.");
            if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(userAgent))
                return Result<DeviceTokenResponseDto>.Failure("Client context information (IP, UserAgent) is required.");


            try
            {
                // 1. Validate the provided refresh token.
                var oldTokenResult = await _deviceTokenRepository.GetByTokenAsync(refreshTokenValue);

                if (oldTokenResult.IsFailure || oldTokenResult.Value == null)
                {
                    _logger.LogWarning("Refresh failed: Provided refresh token not found. Snippet: {TokenSnippet}. Error: {Error}", refreshTokenValue.Length > 10 ? refreshTokenValue.Substring(0, 10) : refreshTokenValue, oldTokenResult.ErrorMessage);
                    return Result<DeviceTokenResponseDto>.Failure("Invalid refresh token."); // Generic message
                }
                var oldTokenEntity = oldTokenResult.Value;

                // 2. Validate the old token's activity (not revoked, not expired).
                var activityValidation = ValidateDeviceTokenActivity(oldTokenEntity);

                if (activityValidation.IsFailure)
                {
                    _logger.LogWarning("Attempted device token refresh using inactive token {TokenId}. Reason: {Reason}. Device ID: {DeviceId}",
                                       oldTokenEntity.IdDeviceToken, activityValidation.ErrorMessage, oldTokenEntity.DeviceId);
                    // Return specific inactivity reason from validation
                    return Result<DeviceTokenResponseDto>.Failure(activityValidation.ErrorMessage ?? "Refresh token is inactive or expired.");
                }

                // 3. Ensure the old token has an associated DeviceId
                if (!oldTokenEntity.DeviceId.HasValue || oldTokenEntity.DeviceId.Value <= 0)
                {
                    _logger.LogError("Critical: Valid refresh token {TokenId} lacks DeviceId.", oldTokenEntity.IdDeviceToken);
                    // Attempt to revoke this invalid token state using its value
                    await RevokeTokenAsync(oldTokenEntity.Token, "Unknown IP - System"); // Revoke the Access Token part
                    await RevokeTokenAsync(oldTokenEntity.RefreshToken, "Unknown IP - System"); // Revoke the Refresh Token part
                    return Result<DeviceTokenResponseDto>.Failure("Invalid token state. Please contact support.");
                }
                var deviceId = oldTokenEntity.DeviceId.Value;


                // 4. Generate a NEW pair of tokens for the same device.
                // Reusing the deviceId from the old token.
                var newTokenResult = await GenerateAndStoreTokensAsync(deviceId, ipAddress, userAgent, deviceInfo);

                if (newTokenResult.IsFailure)
                {
                    _logger.LogError("Failed to generate new tokens during refresh for Device ID {DeviceId}. Error: {Error}", deviceId, newTokenResult.ErrorMessage);
                    return Result<DeviceTokenResponseDto>.Failure("Failed to generate new tokens. Please try again.");
                }
                var newDeviceTokenResponseDto = newTokenResult.Value; // This is already the DTO we want to return.


                // 5. Revoke the OLD refresh token (Token Rotation).
                var revokeOldTokenResult = await _deviceTokenRepository.RevokeByTokenAsync(oldTokenEntity.RefreshToken, _dateTimeProvider.GetUtcNow(), ipAddress);

                if (revokeOldTokenResult.IsFailure)
                {
                    _logger.LogWarning("Failed to revoke old device token {OldTokenId} during refresh for Device ID {DeviceId}. Error: {Error}", oldTokenEntity.IdDeviceToken, deviceId, revokeOldTokenResult.ErrorMessage);
                    // Decide if refresh should fail if old token revocation fails.
                    // Log the warning but still return the new tokens for robustness.
                }


                // 6. Return the NEW tokens (already in the response DTO format).
                _logger.LogInformation("Successfully refreshed tokens for Device ID {DeviceId}. New Token ID: {NewTokenId}. Old Token ID: {OldTokenId}",
                                        deviceId, newDeviceTokenResponseDto.DeviceTokenId, oldTokenEntity.IdDeviceToken);
                return Result<DeviceTokenResponseDto>.Success(newDeviceTokenResponseDto);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during device token refresh.");
                return Result<DeviceTokenResponseDto>.Failure("An unexpected error occurred during token refresh.");
            }
        }
    }
}