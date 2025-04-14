using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;
using ArandanoIRT_Backend.Infrastructure.Interfaces.IServices;
using Microsoft.IdentityModel.Tokens;
using Serilog;
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
        /// <summary>
        /// Provides access to application configuration settings.
        /// </summary>
        private readonly IConfiguration _configuration;
        /// <summary>
        /// Repository for managing refresh token persistence.
        /// </summary>
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        /// <summary>
        /// Provider for obtaining consistent UTC timestamps.
        /// </summary>
        private readonly IDateTimeProvider _dateTimeProvider;
        /// <summary>
        /// Repository for accessing person (user) data.
        /// </summary>
        private readonly IPersonRepository _personRepository; // Added based on usage in RefreshTokensAsync
        /// <summary>
        /// Static Serilog logger instance specific to this service.
        /// </summary>
        private readonly Serilog.ILogger _logger = Log.ForContext<JwtTokenService>();

        /// <summary>
        /// The secret key used for signing and validating JWTs, read from configuration.
        /// </summary>
        private readonly string _jwtKey;
        /// <summary>
        /// The configured lifetime duration for JWT access tokens, in minutes.
        /// </summary>
        private readonly double _jwtExpiryMinutes;
        /// <summary>
        /// The configured lifetime duration for refresh tokens, in days.
        /// </summary>
        private readonly double _refreshExpiryDays;
        // private readonly string? _issuer; // Uncomment if using issuer validation
        // private readonly string? _audience; // Uncomment if using audience validation

        /// <summary>
        /// Initializes a new instance of the <see cref="JwtTokenService"/> class.
        /// </summary>
        /// <param name="configuration">The application configuration provider.</param>
        /// <param name="refreshTokenRepository">The repository for refresh token data access.</param>
        /// <param name="dateTimeProvider">The provider for current UTC time.</param>
        /// <param name="personRepository">The repository for person data access.</param>
        /// <exception cref="ArgumentNullException">Thrown if configuration, refreshTokenRepository, dateTimeProvider, personRepository, or required JWT configuration keys are null.</exception>
        public JwtTokenService(
            IConfiguration configuration,
            IRefreshTokenRepository refreshTokenRepository,
            IDateTimeProvider dateTimeProvider,
            IPersonRepository personRepository) // Added personRepository dependency
        {
            // Injects dependencies.
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _personRepository = personRepository ?? throw new ArgumentNullException(nameof(personRepository)); // Added null check

            // Reads and validates required configuration values for JWT generation/validation.
            _jwtKey = _configuration["Jwt:Key"] ?? throw new ArgumentNullException(nameof(configuration), "Configuration missing value for Jwt:Key");
            _jwtExpiryMinutes = double.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "30"); // Default 30 mins
            _refreshExpiryDays = double.Parse(_configuration["Jwt:RefreshExpiryDays"] ?? "7");   // Default 7 days
            // _issuer = _configuration["Jwt:Issuer"]; // Uncomment if using issuer validation
            // _audience = _configuration["Jwt:Audience"]; // Uncomment if using audience validation

            _logger.Information("JwtTokenService initialized with JWT expiry {JwtExpiry} mins, Refresh expiry {RefreshExpiry} days.", _jwtExpiryMinutes, _refreshExpiryDays);
        }

        /// <inheritdoc/>
        public async Task<Result<TokenResponseDto>> GenerateAndStoreTokensAsync(PersonEntity person, string ipAddress, string userAgent, string? deviceInfo = null)
        {
            // For initial token generation (e.g., login), passes null for existingSessionId to generate a new one.
            return await GenerateAndStoreTokensInternalAsync(person, ipAddress, userAgent, deviceInfo, null);
        }

        /// <summary>
        /// Internal core logic for generating both access and refresh tokens and storing the refresh token.
        /// Handles session ID creation or reuse.
        /// </summary>
        /// <param name="person">The person entity for whom to generate tokens.</param>
        /// <param name="ipAddress">The IP address of the client.</param>
        /// <param name="userAgent">The user agent of the client.</param>
        /// <param name="deviceInfo">Optional device information.</param>
        /// <param name="existingSessionId">Optional. If provided (e.g., during refresh), this session ID is reused; otherwise, a new one is generated.</param>
        /// <returns>A Result containing the new TokenResponseDto or an error message.</returns>
        private async Task<Result<TokenResponseDto>> GenerateAndStoreTokensInternalAsync(
            PersonEntity person, string ipAddress, string userAgent, string? deviceInfo, long? existingSessionId = null)
        {
            // Validates input person entity.
            if (person == null)
            {
                _logger.Warning("Attempted to generate tokens for a null person entity.");
                return Result<TokenResponseDto>.Failure("Person data cannot be null for token generation."); // More specific error
            }

            try
            {
                // Gets consistent timestamps.
                var now = _dateTimeProvider.GetUtcNow();
                var accessTokenExpiry = now.AddMinutes(_jwtExpiryMinutes);
                var refreshTokenExpiry = now.AddDays(_refreshExpiryDays);

                // --- Determine Session ID ---
                // Uses the existing session ID if provided (for refresh), otherwise generates a new one based on current ticks (for login).
                long sessionId = existingSessionId ?? now.Ticks;
                _logger.Debug("Using Session ID {SessionId} for token generation. Was existing provided: {ExistingProvided}",
                              sessionId, existingSessionId.HasValue);

                // 1. Generate the JWT Access Token using a private helper.
                var accessToken = GenerateJwt(person, accessTokenExpiry);

                // 2. Generate a secure, opaque Refresh Token string using a private helper.
                var refreshTokenValue = GenerateSecureRandomString();

                // 3. Creates the RefreshTokenEntity domain object to be stored.
                var refreshTokenEntity = new RefreshTokenEntity(
                    idRefreshToken: 0, // Database generates ID on insert.
                    session: sessionId, // Uses the determined session ID.
                    token: refreshTokenValue, // The generated opaque token.
                    deviceInfo: deviceInfo ?? "Unknown", // Uses provided device info or default.
                    ipAddress: ipAddress ?? "Unknown", // Uses provided IP or default.
                    userAgent: userAgent ?? "Unknown", // Uses provided agent or default.
                    createdAt: now, // Creation timestamp.
                    expiresAt: refreshTokenExpiry, // Expiration timestamp.
                    revokedAt: null, // Initially not revoked.
                    revokedByIp: null, // Initially not revoked.
                    replacedByToken: null, // Initially not replaced.
                    personId: person.IdPerson // Links to the person.
                );

                // 4. Stores the new refresh token entity in the database via the repository.
                var createResult = await _refreshTokenRepository.Create(refreshTokenEntity);
                if (createResult.IsFailure)
                {
                    _logger.Error("Failed to store refresh token for User ID {UserId}. Error: {Error}", person.IdPerson, createResult.ErrorMessage);
                    // Returns a generic error to the user.
                    return Result<TokenResponseDto>.Failure("Failed to save session information. Please try again.");
                }

                // Logs successful generation and storage, including the generated refresh token ID.
                _logger.Information("Generated and stored tokens for User ID {UserId}. Session ID: {SessionId}, Refresh Token ID: {RefreshTokenId}",
                                    person.IdPerson, sessionId, createResult.Value.IdRefreshToken); // Uses ID from the result after creation.

                // 5. Creates and returns the DTO containing the new tokens and access token expiry.
                return Result<TokenResponseDto>.Success(new TokenResponseDto
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshTokenValue, // Returns the opaque refresh token string to the client.
                    AccessTokenExpiration = accessTokenExpiry
                });
            }
            catch (Exception ex) // Catches any unexpected errors during the process.
            {
                _logger.Error(ex, "Error generating tokens for User ID {UserId}", person?.IdPerson);
                return Result<TokenResponseDto>.Failure($"An unexpected error occurred while generating tokens.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<long>> GetSessionIdFromTokenAsync(string refreshTokenValue)
        {
            // Basic input validation.
            if (string.IsNullOrWhiteSpace(refreshTokenValue))
            {
                return Result<long>.Failure("Refresh token cannot be empty.");
            }

            try
            {
                // Retrieves the token entity from the database using the provided opaque token value.
                var tokenResult = await _refreshTokenRepository.GetByTokenAsync(refreshTokenValue);
                if (tokenResult.IsFailure)
                {
                    _logger.Warning("GetSessionIdFromTokenAsync: Refresh token not found in repository. Error: {Error}", tokenResult.ErrorMessage);
                    // Returns a specific internal error message.
                    return Result<long>.Failure("Refresh token not found.");
                }

                var tokenEntity = tokenResult.Value;

                // Checks if the retrieved token is inactive (revoked or expired).
                if (tokenEntity.RevokedAt != null || tokenEntity.ExpiresAt <= _dateTimeProvider.GetUtcNow())
                {
                    _logger.Warning("GetSessionIdFromTokenAsync: Token {TokenId} is inactive (revoked or expired).", tokenEntity.IdRefreshToken);
                    return Result<long>.Failure("Token is inactive.");
                }

                // Returns the session ID associated with the valid, active token.
                _logger.Debug("GetSessionIdFromTokenAsync: Found Session ID {SessionId} for token ID {TokenId}", tokenEntity.Session, tokenEntity.IdRefreshToken);
                return Result<long>.Success(tokenEntity.Session);
            }
            catch (Exception ex) // Handles unexpected errors during retrieval or validation.
            {
                _logger.Error(ex, "Error retrieving session ID from refresh token.");
                return Result<long>.Failure($"An unexpected error occurred while retrieving session info.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<TokenResponseDto>> RefreshTokensAsync(string refreshTokenValue, string ipAddress, string userAgent, string? deviceInfo = null)
        {
            // Basic input validation.
            if (string.IsNullOrWhiteSpace(refreshTokenValue))
                return Result<TokenResponseDto>.Failure("Refresh token is required.");

            // 1. Retrieves the existing refresh token entity from the database.
            var dbTokenResult = await _refreshTokenRepository.GetByTokenAsync(refreshTokenValue);
            if (dbTokenResult.IsFailure)
            {
                _logger.Warning("Refresh token not found in database during refresh attempt. Provided token snippet: {TokenSnippet}", refreshTokenValue.Length > 10 ? refreshTokenValue.Substring(0, 10) : refreshTokenValue);
                // Returns a generic error to prevent token probing.
                return Result<TokenResponseDto>.Failure("Invalid session or token.");
            }

            var dbToken = dbTokenResult.Value; // The existing token entity from the DB.
            var now = _dateTimeProvider.GetUtcNow();

            // 2. Validates the status of the existing token.
            if (dbToken.RevokedAt != null)
            {
                _logger.Warning("Attempted refresh using a revoked token. User ID: {UserId}, Token ID: {TokenId}, Session: {SessionId}", dbToken.PersonId ?? -1, dbToken.IdRefreshToken, dbToken.Session);
                // Security: Consider implementing logic here to revoke all other tokens in the same session (dbToken.Session).
                return Result<TokenResponseDto>.Failure("Session has been invalidated. Please log in again.");
            }
            if (dbToken.ExpiresAt <= now)
            {
                _logger.Information("Attempted refresh using an expired token. User ID: {UserId}, Token ID: {TokenId}, Session: {SessionId}", dbToken.PersonId ?? -1, dbToken.IdRefreshToken, dbToken.Session);
                // Optionally delete the expired token here? For now, just fail.
                return Result<TokenResponseDto>.Failure("Session has expired. Please log in again.");
            }
            // Ensures the token is linked to a person (critical for generating new tokens).
            if (!dbToken.PersonId.HasValue)
            {
                _logger.Error("Critical state: Refresh token {TokenId} (Session: {SessionId}) found without a PersonId.", dbToken.IdRefreshToken, dbToken.Session);
                // This indicates a data integrity issue.
                return Result<TokenResponseDto>.Failure("Invalid token state. Please contact support.");
            }

            // 3. Retrieves the associated user data.
            var personResult = await _personRepository.GetById(dbToken.PersonId.Value);
            if (personResult.IsFailure)
            {
                _logger.Error("User account (ID: {PersonId}) linked to refresh token {TokenId} (Session: {SessionId}) not found.", dbToken.PersonId.Value, dbToken.IdRefreshToken, dbToken.Session);
                // If user doesn't exist, the refresh token is invalid. Revoke it.
                await RevokeRefreshTokenAsync(refreshTokenValue, ipAddress);
                return Result<TokenResponseDto>.Failure("Associated user account not found.");
            }
            var person = personResult.Value;

            // 4. Generates a NEW set of tokens, REUSING the session ID from the old token (important for session management).
            // Calls the internal helper, passing the existing session ID.
            var newTokenResult = await GenerateAndStoreTokensInternalAsync(person, ipAddress, userAgent, deviceInfo, dbToken.Session);

            if (newTokenResult.IsFailure)
            {
                _logger.Error("Failed to generate new tokens during refresh for User ID {UserId}, Session ID {SessionId}. Error: {Error}", person.IdPerson, dbToken.Session, newTokenResult.ErrorMessage);
                // Fails definitively if new token generation fails. Does not revoke old token yet.
                return Result<TokenResponseDto>.Failure("Failed to generate new tokens. Please try again or log in.");
            }

            // 5. Revokes the OLD refresh token, marking it as replaced by the new one (Token Rotation).
            dbToken.Revoke(now, ipAddress); // Marks old token revoked.
            dbToken.Replace(newTokenResult.Value.RefreshToken); // Links old token to the new refresh token value.

            // Updates the old token record in the database.
            var updateResult = await _refreshTokenRepository.Update(dbToken);
            if (updateResult.IsFailure)
            {
                // This is problematic: User received new tokens, but the old token wasn't properly revoked/updated.
                // This could potentially allow replay if not handled carefully. Logs a critical error.
                _logger.Error("CRITICAL: Failed to update (revoke/replace) old refresh token {TokenId} (Session: {SessionId}) after issuing new tokens for User ID {UserId}. Error: {Error}",
                              dbToken.IdRefreshToken, dbToken.Session, person.IdPerson, updateResult.ErrorMessage);
                // Proceeds with success as the user has valid new tokens, but logs the inconsistency. Manual cleanup might be needed.
            }
            else
            {
                // Logs successful rotation.
                _logger.Information("Successfully refreshed tokens for User ID {UserId}. Old token {OldTokenId} (Session: {SessionId}) revoked, replaced by new token.",
                                    person.IdPerson, dbToken.IdRefreshToken, dbToken.Session);
            }

            // 6. Returns the NEW tokens to the client.
            return Result<TokenResponseDto>.Success(newTokenResult.Value);
        }


        /// <inheritdoc/>
        public async Task<Result> RevokeRefreshTokenAsync(string refreshTokenValue, string ipAddress)
        {
            // Basic input validation.
            if (string.IsNullOrWhiteSpace(refreshTokenValue))
                return Result.Failure("Refresh token is required.");

            // 1. Retrieves the token entity from the database.
            var dbTokenResult = await _refreshTokenRepository.GetByTokenAsync(refreshTokenValue);
            if (dbTokenResult.IsFailure)
            {
                // If token not found, it's effectively invalid/revoked. Log and return success.
                _logger.Information("Attempted to revoke a refresh token that was not found. Token snippet: {TokenSnippet}", refreshTokenValue.Length > 10 ? refreshTokenValue.Substring(0, 10) : refreshTokenValue);
                return Result.Success();
            }

            var dbToken = dbTokenResult.Value;
            var now = _dateTimeProvider.GetUtcNow();

            // 2. Checks if token is already inactive.
            if (dbToken.RevokedAt != null)
            {
                _logger.Information("Refresh token {TokenId} (Session: {SessionId}) was already revoked at {RevokedAt}.", dbToken.IdRefreshToken, dbToken.Session, dbToken.RevokedAt);
                return Result.Success(); // Already revoked.
            }
            if (dbToken.ExpiresAt <= now)
            {
                _logger.Information("Refresh token {TokenId} (Session: {SessionId}) is already expired ({ExpiresAt}).", dbToken.IdRefreshToken, dbToken.Session, dbToken.ExpiresAt);
                return Result.Success(); // Already expired.
            }

            // 3. Marks the token entity as revoked.
            dbToken.Revoke(now, ipAddress);
            // Note: Does not set ReplacedByToken as this is a direct revocation.

            // 4. Updates the token record in the database.
            var updateResult = await _refreshTokenRepository.Update(dbToken);
            if (updateResult.IsSuccess)
            {
                _logger.Information("Successfully revoked refresh token {TokenId} (Session: {SessionId}) for User ID {UserId}", dbToken.IdRefreshToken, dbToken.Session, dbToken.PersonId ?? -1);
                return Result.Success();
            }
            else
            {
                // Logs failure to update the revoked status.
                _logger.Error("Failed to update (revoke) refresh token {TokenId} (Session: {SessionId}) for User ID {UserId}. Error: {Error}", dbToken.IdRefreshToken, dbToken.Session, dbToken.PersonId ?? -1, updateResult.ErrorMessage);
                return Result.Failure("Failed to revoke refresh token due to a database error.");
            }
        }

        /// <inheritdoc/>
        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            // Basic null check.
            if (string.IsNullOrWhiteSpace(token)) return null;

            // Configures validation parameters, explicitly disabling lifetime validation.
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true, // Must validate signature.
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_jwtKey)),
                ValidateIssuer = false, // Set true if using Issuer validation in config.
                ValidateAudience = false, // Set true if using Audience validation in config.
                ValidateLifetime = false, // IMPORTANT: Allows expired tokens.
                ClockSkew = TimeSpan.Zero // No clock skew needed if lifetime isn't checked.
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                // Validates the token structure, signature, and issuer/audience (if configured).
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

                // Performs additional check on the validated token's algorithm.
                if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    _logger.Warning("Invalid token structure or algorithm encountered when getting principal from expired token. Algorithm: {Alg}", (securityToken as JwtSecurityToken)?.Header?.Alg);
                    return null; // Returns null if algorithm is unexpected.
                }
                // Returns the principal containing claims if signature and structure are valid.
                return principal;
            }
            catch (SecurityTokenValidationException stve) // Catches validation errors OTHER than lifetime.
            {
                // Logs validation errors like invalid signature.
                _logger.Warning("SecurityTokenValidationException while getting principal from token (expected if signature invalid): {Message}", stve.Message);
                // Since ValidateLifetime is false, this shouldn't be a lifetime error. Return null for other validation failures.
                return null;
            }
            catch (Exception ex) // Catches unexpected errors.
            {
                _logger.Error(ex, "Unexpected exception while trying to get principal from (potentially expired) token.");
                return null;
            }
        }

        // --- Private Helper Methods ---

        /// <summary>
        /// Generates a JWT access token string for the specified user with a defined expiry.
        /// </summary>
        /// <param name="person">The user entity containing claims information.</param>
        /// <param name="expires">The expiration timestamp for the token.</param>
        /// <returns>A signed JWT access token string.</returns>
        private string GenerateJwt(PersonEntity person, DateTime expires)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var keyBytes = Encoding.ASCII.GetBytes(_jwtKey);

            // Creates the list of claims for the token payload.
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, person.IdPerson.ToString()), // Standard User ID claim
                new Claim(JwtRegisteredClaimNames.Sub, person.IdPerson.ToString()), // Subject claim
                new Claim(JwtRegisteredClaimNames.Email, person.Email), // Email claim
                new Claim(JwtRegisteredClaimNames.GivenName, person.FirstName), // First Name claim
                new Claim(JwtRegisteredClaimNames.FamilyName, person.LastName), // Last Name claim
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // JWT ID - unique per token instance
                new Claim(ClaimTypes.Role, person.IsAdmin ? "Admin" : "User") // Role claim
            };

            // Adds CropId claim only if it's valid.
            if (person.CropId.HasValue && person.CropId.Value > 0)
            {
                claims.Add(new Claim("CropId", person.CropId.Value.ToString())); // Custom CropId claim
            }

            // Creates the token descriptor.
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expires.ToUniversalTime(), // Ensures expiry is UTC.
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
                // Issuer = _issuer, // Add if issuer validation is used
                // Audience = _audience, // Add if audience validation is used
            };

            // Creates and serializes the token.
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// Generates a cryptographically secure random string suitable for use as an opaque refresh token.
        /// </summary>
        /// <param name="byteLength">The desired length of the underlying random byte array (default is 32 bytes = 256 bits).</param>
        /// <returns>A Base64Url encoded string representation of the random bytes.</returns>
        private string GenerateSecureRandomString(int byteLength = 32)
        {
            // Generates cryptographically secure random bytes.
            using var rng = RandomNumberGenerator.Create();
            var randomBytes = new byte[byteLength];
            rng.GetBytes(randomBytes);
            // Encodes bytes using Base64Url encoding (URL-safe, no padding).
            return Base64UrlEncoder.Encode(randomBytes);
        }
    }
}