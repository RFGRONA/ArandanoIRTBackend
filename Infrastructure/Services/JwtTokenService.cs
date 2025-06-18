using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ArandanoIRT_Backend.Infrastructure.Services
{
    /// <summary>
    /// Implements the <see cref="ITokenService"/> interface, providing services for generating,
    /// validating, refreshing, and revoking JWT access tokens and database-backed opaque refresh tokens.
    /// </summary>
    public class JwtTokenService : ITokenService
    {

        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IPersonRepository _personRepository;
        private readonly ILogger<JwtTokenService> _logger;

        private readonly string _jwtKey;
        private readonly double _jwtExpiryMinutes;
        private readonly double _refreshExpiryDays;
        // private readonly string? _issuer;
        // private readonly string? _audience;

        /// <summary>
        /// Initializes a new instance of the <see cref="JwtTokenService"/> class.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if dependencies or required JWT config keys are null/missing.</exception>
        /// <exception cref="FormatException">Thrown if JWT expiry/refresh config values cannot be parsed.</exception>
        public JwtTokenService(
            IConfiguration configuration, // Keep parameter
            IRefreshTokenRepository refreshTokenRepository,
            IDateTimeProvider dateTimeProvider,
            IPersonRepository personRepository,
            ILogger<JwtTokenService> logger)
        {
            // Validate and inject dependencies
            _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _personRepository = personRepository ?? throw new ArgumentNullException(nameof(personRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ArgumentNullException.ThrowIfNull(configuration); // Null check for configuration parameter

            _jwtKey = configuration["Jwt:Key"] ?? throw new ArgumentNullException(nameof(configuration), "Configuration missing required value for Jwt:Key");

            // Use TryParse with fallback and logging for robust configuration reading
            if (!double.TryParse(configuration["Jwt:ExpiryMinutes"], out _jwtExpiryMinutes))
            {
                _logger.LogWarning("Jwt:ExpiryMinutes configuration is missing or invalid. Using default 30 minutes.");
                _jwtExpiryMinutes = 30;
            }
            if (!double.TryParse(configuration["Jwt:RefreshExpiryDays"], out _refreshExpiryDays))
            {
                _logger.LogWarning("Jwt:RefreshExpiryDays configuration is missing or invalid. Using default 7 days.");
                _refreshExpiryDays = 7;
            }
            // _issuer = configuration["Jwt:Issuer"];
            // _audience = configuration["Jwt:Audience"];

            _logger.LogInformation("JwtTokenService initialized with JWT expiry {JwtExpiry} mins, Refresh expiry {RefreshExpiry} days.", _jwtExpiryMinutes, _refreshExpiryDays);
        }

        /// <inheritdoc/>
        public async Task<Result<TokenResponseDto>> GenerateAndStoreTokensAsync(PersonEntity person, string ipAddress, string userAgent)
        {
            // Calls the main implementation, passing null for deviceInfo.
            return await GenerateAndStoreTokensAsync(person, ipAddress, userAgent, null);
        }

        /// <inheritdoc/>
        public async Task<Result<TokenResponseDto>> GenerateAndStoreTokensAsync(PersonEntity person, string ipAddress, string userAgent, string? deviceInfo)
        {
            // Calls the internal method for core logic, passing null for existingSessionId (new session).
            // Pass null explicitly, removing reliance on internal default for this path.
            return await GenerateAndStoreTokensInternalAsync(person, ipAddress, userAgent, deviceInfo, null);
        }

        /// <summary>
        /// Internal core logic for generating tokens and storing refresh token. Handles session ID reuse/creation.
        /// </summary>
        private async Task<Result<TokenResponseDto>> GenerateAndStoreTokensInternalAsync(
            PersonEntity person, string ipAddress, string userAgent, string? deviceInfo, long? existingSessionId = null)
        {
            // Basic input validation
            if (person == null)
            {
                _logger.LogWarning("Attempted to generate tokens for a null person entity.");
                return Result<TokenResponseDto>.Failure("Person data cannot be null for token generation.");
            }
            if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(userAgent))
            {
                _logger.LogWarning("IP address or User agent is missing for token generation. User ID: {UserId}", person.IdPerson);
                return Result<TokenResponseDto>.Failure("Client context information (IP, UserAgent) is required.");
            }

            try
            {
                var now = _dateTimeProvider.GetUtcNow();
                var accessTokenExpiry = now.AddMinutes(_jwtExpiryMinutes);
                var refreshTokenExpiry = now.AddDays(_refreshExpiryDays);

                long sessionId = existingSessionId ?? now.Ticks; // Reuse or create new Session ID
                _logger.LogDebug("Using Session ID {SessionId} for token generation. Was existing provided: {ExistingProvided}", sessionId, existingSessionId.HasValue);

                var accessToken = GenerateJwt(person, accessTokenExpiry);
                var refreshTokenValue = GenerateSecureRandomString();

                // Create RefreshTokenEntity using helper method
                var refreshTokenEntity = CreateRefreshTokenEntity(person.IdPerson, sessionId, refreshTokenValue, deviceInfo, ipAddress, userAgent, now, refreshTokenExpiry);

                // Store the new refresh token
                var createResult = await _refreshTokenRepository.Create(refreshTokenEntity);
                if (createResult.IsFailure)
                {
                    _logger.LogError("Failed to store refresh token for User ID {UserId}, Session ID {SessionId}. Error: {Error}", person.IdPerson, sessionId, createResult.ErrorMessage);
                    return Result<TokenResponseDto>.Failure("Failed to save session information. Please try again.");
                }

                _logger.LogInformation("Generated and stored tokens for User ID {UserId}. Session ID: {SessionId}, Refresh Token ID: {RefreshTokenId}",
                                        person.IdPerson, sessionId, createResult.Value.IdRefreshToken);

                // Return the DTO
                return Result<TokenResponseDto>.Success(new TokenResponseDto
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshTokenValue,
                    AccessTokenExpiration = accessTokenExpiry
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating tokens for User ID {UserId}, Session ID {SessionId}", person?.IdPerson, existingSessionId ?? -1);
                return Result<TokenResponseDto>.Failure("An unexpected error occurred while generating tokens.");
            }
        }

        /// <summary>
        /// Creates a new RefreshTokenEntity instance with provided details.
        /// </summary>
        private static RefreshTokenEntity CreateRefreshTokenEntity(
            int personId, long sessionId, string tokenValue, string? deviceInfo,
            string ipAddress, string userAgent, DateTime createdAt, DateTime expiresAt)
        {
            // Encapsulates entity creation logic
            return new RefreshTokenEntity(
                idRefreshToken: 0, 
                session: sessionId,
                token: tokenValue,
                deviceInfo: deviceInfo ?? "Unknown",
                ipAddress: ipAddress, 
                userAgent: userAgent, 
                createdAt: createdAt,
                expiresAt: expiresAt,
                revokedAt: null,
                revokedByIp: null,
                replacedByToken: null,
                personId: personId
            );
        }

        /// <inheritdoc/>
        public async Task<Result<long>> GetSessionIdFromTokenAsync(string refreshTokenValue)
        {
            if (string.IsNullOrWhiteSpace(refreshTokenValue))
            {
                return Result<long>.Failure("Refresh token cannot be empty.");
            }

            try
            {
                var tokenResult = await _refreshTokenRepository.GetByTokenAsync(refreshTokenValue);
                if (tokenResult.IsFailure)
                {
                    _logger.LogWarning("GetSessionIdFromTokenAsync: Refresh token not found. Error: {Error}", tokenResult.ErrorMessage);
                    return Result<long>.Failure("Refresh token not found.");
                }

                var tokenEntity = tokenResult.Value;

                // Use helper to check if token is active
                var activityValidation = ValidateRefreshTokenActivity(tokenEntity);
                if (activityValidation.IsFailure)
                {
                    _logger.LogWarning("GetSessionIdFromTokenAsync: Token {TokenId} is inactive. Reason: {Reason}", tokenEntity.IdRefreshToken, activityValidation.ErrorMessage);
                    return Result<long>.Failure(activityValidation.ErrorMessage ?? "Token is inactive.");
                }

                _logger.LogDebug("GetSessionIdFromTokenAsync: Found Session ID {SessionId} for token ID {TokenId}", tokenEntity.Session, tokenEntity.IdRefreshToken);
                return Result<long>.Success(tokenEntity.Session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving session ID from refresh token.");
                return Result<long>.Failure("An unexpected error occurred while retrieving session info.");
            }
        }

        /// <summary>
        /// Checks if a refresh token entity is active (not revoked and not expired).
        /// </summary>
        /// <param name="tokenEntity">The token entity to check.</param>
        /// <returns>Success Result if active, Failure Result with reason if inactive.</returns>
        private Result ValidateRefreshTokenActivity(RefreshTokenEntity tokenEntity)
        {
            if (tokenEntity.RevokedAt != null)
            {
                return Result.Failure("Token has been revoked.");
            }
            // Use consistent time provider
            if (tokenEntity.ExpiresAt <= _dateTimeProvider.GetUtcNow())
            {
                return Result.Failure("Token has expired.");
            }
            return Result.Success();
        }

        /// <inheritdoc/>
        public async Task<Result<TokenResponseDto>> RefreshTokensAsync(string refreshTokenValue, string ipAddress, string userAgent)
        {
            // Calls the main implementation, passing null for deviceInfo.
            return await RefreshTokensAsync(refreshTokenValue, ipAddress, userAgent, null);
        }

        /// <inheritdoc/>
        public async Task<Result<TokenResponseDto>> RefreshTokensAsync(string refreshTokenValue, string ipAddress, string userAgent, string? deviceInfo)
        {
            // Basic input validation
            if (string.IsNullOrWhiteSpace(refreshTokenValue))
                return Result<TokenResponseDto>.Failure("Refresh token is required.");
            if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(userAgent))
                return Result<TokenResponseDto>.Failure("Client context information (IP, UserAgent) is required.");

            // 1. Get and Validate Old Token (using helper)
            var validationResult = await ValidateRefreshTokenForRefresh(refreshTokenValue);
            if (validationResult.IsFailure)
            {
                // Error already logged in helper
                return Result<TokenResponseDto>.Failure(validationResult.ErrorMessage ?? "Invalid session or token.");
            }
            var dbToken = validationResult.Value; // Validated, active token

            // 2. Retrieve Associated User (PersonId is validated in helper)
            var personResult = await _personRepository.GetById(dbToken.PersonId!.Value); // Use null-forgiving !
            if (personResult.IsFailure)
            {
                _logger.LogError("User account (ID: {PersonId}) linked to refresh token {TokenId} not found.", dbToken.PersonId.Value, dbToken.IdRefreshToken);
                await RevokeRefreshTokenInternalAsync(dbToken, ipAddress, "User account not found"); // Revoke orphaned token
                return Result<TokenResponseDto>.Failure("Associated user account not found.");
            }
            var person = personResult.Value;

            // 3. Generate NEW Tokens (reusing session ID)
            var newTokenResult = await GenerateAndStoreTokensInternalAsync(person, ipAddress, userAgent, deviceInfo, dbToken.Session);
            if (newTokenResult.IsFailure)
            {
                _logger.LogError("Failed to generate new tokens during refresh for User ID {UserId}, Session ID {SessionId}. Error: {Error}", person.IdPerson, dbToken.Session, newTokenResult.ErrorMessage);
                return Result<TokenResponseDto>.Failure("Failed to generate new tokens. Please try again or log in.");
            }

            // 4. Revoke OLD Token (Token Rotation) (using helper)
            await RevokeOldTokenAfterRefresh(dbToken, newTokenResult.Value.RefreshToken, ipAddress);

            // 5. Return NEW Tokens
            return Result<TokenResponseDto>.Success(newTokenResult.Value);
        }

        /// <summary>
        /// Retrieves and validates a refresh token entity for the refresh process. Ensures token exists, is active, and has a PersonId.
        /// </summary>
        private async Task<Result<RefreshTokenEntity>> ValidateRefreshTokenForRefresh(string refreshTokenValue)
        {
            var dbTokenResult = await _refreshTokenRepository.GetByTokenAsync(refreshTokenValue);
            if (dbTokenResult.IsFailure)
            {
                _logger.LogWarning("Refresh token not found during refresh. Snippet: {TokenSnippet}", refreshTokenValue.Length > 10 ? refreshTokenValue.Substring(0, 10) : refreshTokenValue);
                return Result<RefreshTokenEntity>.Failure("Invalid session or token.");
            }
            var dbToken = dbTokenResult.Value;

            var activityValidation = ValidateRefreshTokenActivity(dbToken);
            if (activityValidation.IsFailure)
            {
                _logger.LogWarning("Attempted refresh using inactive token {TokenId} (Session: {SessionId}). Reason: {Reason}", dbToken.IdRefreshToken, dbToken.Session, activityValidation.ErrorMessage);
                // Map internal reasons to user-facing messages
                string failureReason = activityValidation.ErrorMessage?.Contains("revoked") == true
                    ? "Session has been invalidated. Please log in again."
                    : "Session has expired. Please log in again.";
                // Consider revoking other session tokens if one is revoked (requires separate logic)
                return Result<RefreshTokenEntity>.Failure(failureReason);
            }

            if (!dbToken.PersonId.HasValue)
            {
                _logger.LogError("Critical: Refresh token {TokenId} (Session: {SessionId}) lacks PersonId.", dbToken.IdRefreshToken, dbToken.Session);
                // Attempt to revoke this invalid token
                await RevokeRefreshTokenInternalAsync(dbToken, "Unknown IP - System", "Token lacks PersonId");
                return Result<RefreshTokenEntity>.Failure("Invalid token state. Please contact support.");
            }

            return Result<RefreshTokenEntity>.Success(dbToken);
        }

        /// <summary>
        /// Revokes the old refresh token after a successful refresh, marking it as replaced. Logs errors but doesn't fail the operation.
        /// </summary>
        private async Task RevokeOldTokenAfterRefresh(RefreshTokenEntity oldToken, string newRefreshTokenValue, string ipAddress)
        {
            // Use internal helper for the update logic
            await RevokeRefreshTokenInternalAsync(oldToken, ipAddress, $"Replaced by new token in session {oldToken.Session}", newRefreshTokenValue);
        }

        /// <inheritdoc/>
        public async Task<Result> RevokeRefreshTokenAsync(string refreshTokenValue, string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(refreshTokenValue))
                return Result.Failure("Refresh token is required.");
            if (string.IsNullOrWhiteSpace(ipAddress))
                return Result.Failure("IP address is required for revocation context.");

            // 1. Get and Validate Token for Revocation (using helper)
            var validationResult = await ValidateRefreshTokenForRevocation(refreshTokenValue);
            if (validationResult.IsFailure)
            {
                // If token not found or already inactive, consider it success. Logged in helper.
                return Result.Success();
            }
            var dbToken = validationResult.Value; // Token exists and is active.

            // 2. Perform Revocation (using internal helper)
            return await RevokeRefreshTokenInternalAsync(dbToken, ipAddress, "Direct revocation request");
        }

        /// <summary>
        /// Retrieves and validates a refresh token for direct revocation. Returns success only if token exists and is active.
        /// </summary>
        private async Task<Result<RefreshTokenEntity>> ValidateRefreshTokenForRevocation(string refreshTokenValue)
        {
            var dbTokenResult = await _refreshTokenRepository.GetByTokenAsync(refreshTokenValue);
            if (dbTokenResult.IsFailure)
            {
                _logger.LogInformation("Revocation requested for non-existent token. Snippet: {TokenSnippet}", refreshTokenValue.Length > 10 ? refreshTokenValue.Substring(0, 10) : refreshTokenValue);
                return Result<RefreshTokenEntity>.Failure("Token not found."); 
            }
            var dbToken = dbTokenResult.Value;

            var activityValidation = ValidateRefreshTokenActivity(dbToken);
            if (activityValidation.IsFailure)
            {
                _logger.LogInformation("Revocation requested for already inactive token {TokenId} (Session: {SessionId}). Reason: {Reason}", dbToken.IdRefreshToken, dbToken.Session, activityValidation.ErrorMessage);
                return Result<RefreshTokenEntity>.Failure(activityValidation.ErrorMessage ?? "Token already inactive."); 
            }

            return Result<RefreshTokenEntity>.Success(dbToken); 
        }

        /// <summary>
        /// Marks a token entity as revoked and attempts to update it in the database. Optionally sets ReplacedByToken.
        /// </summary>
        private async Task<Result> RevokeRefreshTokenInternalAsync(RefreshTokenEntity dbToken, string ipAddress, string reason, string? replacedByToken = null)
        {
            var now = _dateTimeProvider.GetUtcNow();
            dbToken.Revoke(now, ipAddress);
            if (replacedByToken != null)
            {
                dbToken.Replace(replacedByToken);
            }

            var updateResult = await _refreshTokenRepository.Update(dbToken);
            if (updateResult.IsSuccess)
            {
                _logger.LogInformation("Successfully updated refresh token {TokenId} (Session: {SessionId}) for User ID {UserId}. Action: Revoked. Reason: {Reason}. Replaced: {IsReplaced}",
                                    dbToken.IdRefreshToken, dbToken.Session, dbToken.PersonId ?? -1, reason, replacedByToken != null);
                return Result.Success();
            }
            
            _logger.LogError("Failed to update (revoke/replace) refresh token {TokenId} (Session: {SessionId}) for User ID {UserId}. Reason: {Reason}. Error: {Error}",
                                dbToken.IdRefreshToken, dbToken.Session, dbToken.PersonId ?? -1, reason, updateResult.ErrorMessage);
            return Result.Failure("Failed to update refresh token state due to a database error.");
        }


        /// <inheritdoc/>
        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            var keyBytes = Encoding.ASCII.GetBytes(_jwtKey);
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                ValidateIssuer = false, // Configure as needed
                ValidateAudience = false, // Configure as needed

                // <<< SECURITY NOTE: ValidateLifetime = false >>>
                // Intentionally false ONLY for this method to extract claims from a potentially expired token
                // during the refresh process. Access is NOT granted based on this. Signature IS validated.
                ValidateLifetime = false,
                ClockSkew = TimeSpan.Zero
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

                if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    _logger.LogWarning("Invalid token structure or algorithm in GetPrincipalFromExpiredToken. Algorithm: {Alg}",
                                     (securityToken as JwtSecurityToken)?.Header?.Alg ?? "N/A");
                    return null;
                }
                return principal;
            }
            catch (SecurityTokenValidationException stve)
            {
                _logger.LogWarning("Token validation failed (e.g., invalid signature) in GetPrincipalFromExpiredToken: {Message}", stve.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting principal from token.");
                return null;
            }
        }

        // --- Private Helper Methods (GenerateJwt, GenerateSecureRandomString) ---

        /// <summary>
        /// Generates a JWT access token string for the specified user.
        /// </summary>
        private string GenerateJwt(PersonEntity person, DateTime expires)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var keyBytes = Encoding.ASCII.GetBytes(_jwtKey);
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, person.IdPerson.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, person.IdPerson.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, person.Email ?? string.Empty), 
                new Claim(JwtRegisteredClaimNames.GivenName, person.FirstName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.FamilyName, person.LastName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), 
                new Claim(ClaimTypes.Role, person.IsAdmin ? "Admin" : "User")
            };

            if (person.CropId.HasValue && person.CropId.Value > 0)
            {
                claims.Add(new Claim("CropId", person.CropId.Value.ToString()));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expires.ToUniversalTime(),
                NotBefore = _dateTimeProvider.GetUtcNow().AddSeconds(-5), 
                IssuedAt = _dateTimeProvider.GetUtcNow(), 
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
                // Issuer = _issuer, 
                // Audience = _audience 
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// Generates a cryptographically secure random string (Base64Url encoded).
        /// </summary>
        private static string GenerateSecureRandomString(int byteLength = 32)
        {
            using var rng = RandomNumberGenerator.Create();
            var randomBytes = new byte[byteLength];
            rng.GetBytes(randomBytes);
            return Base64UrlEncoder.Encode(randomBytes);
        }
    }
}