using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Application.Interfaces.IServices;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Application.Mappings;
using ArandanoIRT_Backend.Application.Utilities;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Application.Services
{
    /// <summary>
    /// Implements services for user session management including login, token refresh, and logout.
    /// </summary>
    public class AuthSessionService : IAuthSessionService
    {
        // Dependencies
        private readonly IAuthUtilities _authUtilities;
        private readonly IPersonRepository _personRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IFailedLoginAttemptRepository _failedLoginAttemptRepository;
        private readonly ITokenService _tokenService;
        private readonly IRsaService _rsaService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IEmailService _emailService; 
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<AuthSessionService> _logger;

        public const string EmailError = "Invalid email or password."; 

        // Constructor
        public AuthSessionService(
            IAuthUtilities authUtilities,
            IPersonRepository personRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IFailedLoginAttemptRepository failedLoginAttemptRepository,
            IRsaService rsaService,
            IPasswordHasher passwordHasher,
            ITokenService tokenService,
            IEmailService emailService, 
            IDateTimeProvider dateTimeProvider,
            ILogger<AuthSessionService> logger
            )
        {
            _authUtilities = authUtilities ?? throw new ArgumentNullException(nameof(authUtilities));
            _personRepository = personRepository ?? throw new ArgumentNullException(nameof(personRepository));
            _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
            _failedLoginAttemptRepository = failedLoginAttemptRepository ?? throw new ArgumentNullException(nameof(failedLoginAttemptRepository));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService)); 
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _rsaService = rsaService ?? throw new ArgumentNullException(nameof(rsaService));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        }

        // --- Login Methods ---

        /// <inheritdoc/>
        public async Task<Result<LoginSuccessPayload>> LoginAsync(LoginRequestDto request, string ipAddress, string userAgent)
        {
            // Call the main overload with null deviceInfo
            return await LoginAsync(request, ipAddress, userAgent, null);
        }

        /// <inheritdoc/>
        public async Task<Result<LoginSuccessPayload>> LoginAsync(LoginRequestDto request, string ipAddress, string userAgent, string? deviceInfo)
        {
            // Log entry point
            _logger.LogInformation("Processing login attempt for email: {Email} from IP: {IPAddress}", request?.Email, ipAddress);

            // Null check for request DTO
            if (request == null)
            {
                _logger.LogWarning("{ServiceMethod} called with null request object.", nameof(LoginAsync));
                return Result<LoginSuccessPayload>.Failure("Invalid login data provided.");
            }

            // --- Step 1: Validate Input ---
            var validationResult = await ValidateLoginInputAsync(request);
            // validationResult is Result. Convert failure if needed.
            if (validationResult.IsFailure) return Result<LoginSuccessPayload>.Failure(validationResult.ErrorMessage);

            // --- Step 2: Find User ---
            // Email guaranteed non-null by validation
            var personResult = await _authUtilities.FindUserByEmailAsync(request.Email!);
            if (personResult.IsFailure) // Failure means not found
            {
                // Log the specific event of user not found during login
                _logger.LogWarning("Login failed: User not found for email {Email} from IP {IPAddress}", request.Email, ipAddress);
                // Simple message for API response
                return Result<LoginSuccessPayload>.Failure(EmailError);
            }
            var person = personResult.Value;

            // --- Step 3: Handle Failed Attempts & Verify Password ---
            // Password guaranteed non-null by validation
            var verificationResult = await HandleFailedAttemptsAndVerifyPasswordAsync(person, request.Password!, ipAddress, userAgent, deviceInfo);
            // verificationResult is Result. Convert failure if needed.
            if (verificationResult.IsFailure) return Result<LoginSuccessPayload>.Failure(verificationResult.ErrorMessage);

            // --- Step 4: Login Successful - Update Last Login (Best effort) ---
            await UpdateLastLoginAsync(person);

            // --- Step 5: Generate Tokens ---
            // Delegate token generation and storage to ITokenService
            var tokenResult = await _tokenService.GenerateAndStoreTokensAsync(person, ipAddress, userAgent, deviceInfo);
            if (tokenResult.IsFailure)
            {
                // Log critical error: login OK but token generation failed
                _logger.LogError("Failed to generate tokens for User ID {UserId} after successful login. Error: {Error}", person.IdPerson, tokenResult.ErrorMessage);
                // Simple message for API response
                return Result<LoginSuccessPayload>.Failure("Login succeeded but failed to create session tokens. Please try again.");
            }
            var generatedTokensDto = tokenResult.Value; // Expects TokenResponseDto

            // --- Step 6: Prepare Payload ---
            var payload = PrepareLoginPayload(person, generatedTokensDto.AccessToken, generatedTokensDto.RefreshToken);

            // Log successful login
            _logger.LogInformation("Login successful for User ID {UserId} ({Email}) from IP {IPAddress}", person.IdPerson, request.Email, ipAddress);
            // Return success payload
            return Result<LoginSuccessPayload>.Success(payload);
        }

        // --- Refresh Token Methods ---

        /// <inheritdoc/>
        public async Task<Result<TokenResponseDto>> RefreshTokenAsync(string refreshTokenValue, string ipAddress, string userAgent)
        {
            // Call the main overload with null deviceInfo
            return await RefreshTokenAsync(refreshTokenValue, ipAddress, userAgent, null);
        }

        /// <inheritdoc/>
        public async Task<Result<TokenResponseDto>> RefreshTokenAsync(string refreshTokenValue, string ipAddress, string userAgent, string? deviceInfo)
        {
            // Log entry point
            _logger.LogInformation("Processing token refresh request from IP: {IPAddress}", ipAddress);
            // Delegate directly to the token service, which handles validation, rotation, etc.
            return await _tokenService.RefreshTokensAsync(refreshTokenValue, ipAddress, userAgent, deviceInfo);
        }

        // --- Logout Methods ---

        /// <inheritdoc/>
        public async Task<Result> LogoutAsync(string refreshTokenValue, string ipAddress)
        {
            // Log entry point
            _logger.LogInformation("Processing specific token revocation (logout) from IP: {IPAddress}", ipAddress);
            // Delegate directly to the token service
            return await _tokenService.RevokeRefreshTokenAsync(refreshTokenValue, ipAddress);
        }

        /// <inheritdoc/>
        public async Task<Result> LogoutEverywhereAsync(long sessionId, string ipAddress)
        {
            // Log entry point
            _logger.LogInformation("Processing revocation of all tokens (logout everywhere) for session ID: {SessionId} from IP: {IPAddress}", sessionId, ipAddress);
            // Delegate directly to the refresh token repository
            // Consider if ITokenService should wrap this for consistency? For now, direct call is fine.
            return await _refreshTokenRepository.RevokeBySessionIdAsync(sessionId, ipAddress);
        }


        // --- Private Helper Methods ---

        #region Login Helpers

        /// <summary>
        /// Validates the basic input for the login request.
        /// </summary>
        /// <param name="request">The login request DTO.</param>
        /// <returns>A success or failure result.</returns>
        private async Task<Result> ValidateLoginInputAsync(LoginRequestDto request)
        {
            // Null checks for request object and essential fields
            // Null check for 'request' itself done by caller
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                _logger.LogWarning("Login validation failed: Email or Password missing.");
                return Result.Failure("Email and password are required.");
            }
            // Validate email format
            var emailValidation = await EmailValidatorUtility.ValidateEmailAsync(request.Email);
            if (emailValidation.IsFailure)
            {
                // Log detailed error
                _logger.LogWarning("Invalid email format during login: {Email}. Reason: {Reason}", request.Email, emailValidation.ErrorMessage);
                // Simple message for API response (security)
                return Result.Failure(EmailError);
            }
            // Input is valid
            return Result.Success();
        }

        /// <summary>
        /// Handles failed login attempt recording, checks, and password verification using IAuthUtilities.
        /// </summary>
        /// <param name="person">The PersonEntity attempting to log in.</param>
        /// <param name="encryptedPassword">The encrypted password from the request.</param>
        /// <param name="ipAddress">Client IP address.</param>
        /// <param name="userAgent">Client user agent.</param>
        /// <param name="deviceInfo">Optional device info.</param>
        /// <returns>Success result if password is valid, Failure result otherwise.</returns>
        private async Task<Result> HandleFailedAttemptsAndVerifyPasswordAsync(PersonEntity person, string encryptedPassword, string ipAddress, string userAgent, string? deviceInfo)
        {
            // Configuration for failed attempts - move to settings?
            const int FailedAttemptThreshold = 5;
            TimeSpan FailedAttemptWindow = TimeSpan.FromMinutes(30);
            var now = _dateTimeProvider.GetUtcNow();
            var timeThreshold = now.Subtract(FailedAttemptWindow);

            string decryptedPassword;
            try
            {
                decryptedPassword = _rsaService.Decrypt(encryptedPassword); 
            }
            catch (Exception ex) // Catch decryption errors
            {
                _logger.LogError(ex, "Failed to decrypt password during login for email {Email}", person.Email);
                // Record failed attempt because decryption failed (implies bad data sent)
                await RecordFailedLoginAttemptAsync(person.IdPerson, now, ipAddress, deviceInfo, userAgent);
                await CheckAndSendSuspiciousActivityWarningAsync(person, timeThreshold, FailedAttemptThreshold, ipAddress); // Check after recording
                // Simple failure message
                return Result.Failure(EmailError);
            }

            // Verify the decrypted password against the stored hash
            bool isPasswordValid = _passwordHasher.Verify(person.Password, decryptedPassword); // Use injected PasswordHasher

            if (!isPasswordValid)
            {
                // Log invalid password attempt
                _logger.LogWarning("Login failed: Invalid password for email {Email} from IP {IPAddress}", person.Email, ipAddress);
                // Record the failed attempt
                await RecordFailedLoginAttemptAsync(person.IdPerson, now, ipAddress, deviceInfo, userAgent);
                // Check if threshold met and send warning
                await CheckAndSendSuspiciousActivityWarningAsync(person, timeThreshold, FailedAttemptThreshold, ipAddress);
                // Simple failure message
                return Result.Failure(EmailError);
            }

            // Password is valid
            return Result.Success();
        }


        /// <summary>
        /// Updates the user's LastLoginAt timestamp in the repository. Best effort.
        /// </summary>
        /// <param name="person">The person entity who logged in.</param>
        private async Task UpdateLastLoginAsync(PersonEntity person)
        {
            try
            {
                var now = _dateTimeProvider.GetUtcNow();
                // Avoid reflection - assuming an internal method on the entity or repo update method
                typeof(PersonEntity).GetProperty("LastLoginAt")?.SetValue(person, now, null);

                // Update the person record
                var updateResult = await _personRepository.Update(person); // Assumes Update handles which fields changed
                if (updateResult.IsFailure)
                {
                    // Log failure to update timestamp but don't fail the login
                    _logger.LogWarning("Failed to update LastLoginAt for User ID {UserId} after login. Error: {Error}", person.IdPerson, updateResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                // Log any unexpected exception during update
                _logger.LogWarning(ex, "Exception occurred while updating LastLoginAt for User ID {UserId}.", person.IdPerson);
            }
        }

        /// <summary>
        /// Prepares the successful login payload including DTO and refresh token.
        /// Marked as static as it doesn't access instance members.
        /// </summary>
        /// <param name="person">The authenticated person entity.</param>
        /// <param name="accessToken">The generated JWT access token.</param>
        /// <param name="refreshToken">The generated refresh token.</param>
        /// <returns>A LoginSuccessPayload object.</returns>
        private static LoginSuccessPayload PrepareLoginPayload(PersonEntity person, string accessToken, string refreshToken)
        {
            // Map PersonEntity to the LoginResponseDto part of the payload
            var loginResponseDto = AuthMapping.ToLoginResponseDto(person);
            // Set the generated access token within the response DTO
            loginResponseDto.Token = accessToken;

            // Construct the final payload
            return new LoginSuccessPayload
            {
                LoginDetails = loginResponseDto,
                RefreshToken = refreshToken // Include the refresh token separately
            };
        }

        /// <summary>
        /// Records a failed login attempt in the database. Best effort.
        /// </summary>
        /// <param name="personId">The ID of the user for whom the login failed.</param>
        /// <param name="attemptTime">The UTC timestamp of the failed attempt.</param>
        /// <param name="ipAddress">The IP address from which the attempt originated.</param>
        /// <param name="deviceInfo">Optional formatted device information string.</param>
        /// <param name="userAgent">The user agent string of the client.</param>
        private async Task RecordFailedLoginAttemptAsync(int personId, DateTime attemptTime, string ipAddress, string? deviceInfo, string userAgent)
        {
            try
            {
                // Create the entity for the failed attempt
                var failedAttempt = new FailedLoginAttemptEntity(
                    idFailedLoginAttempt: 0, // DB generates ID
                    attemptDate: attemptTime,
                    ipAddress: ipAddress ?? "Unknown", // Handle potential nulls
                    deviceInfo: deviceInfo ?? "Unknown",
                    userAgent: userAgent ?? "Unknown",
                    personId: personId
                );
                // Save to the repository
                var createAttemptResult = await _failedLoginAttemptRepository.Create(failedAttempt);
                // Log only if saving the attempt fails
                if (createAttemptResult.IsFailure)
                {
                    _logger.LogError("Failed to record failed login attempt for User ID {UserId}. Error: {Error}", personId, createAttemptResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                // Log any unexpected exception during recording
                _logger.LogError(ex, "Exception while recording failed login attempt for User ID {UserId}.", personId);
            }
        }

        /// <summary>
        /// Checks the count of recent failed login attempts and sends a warning email if a threshold is met. Best effort.
        /// </summary>
        /// <param name="person">The PersonEntity for whom the check is being performed.</param>
        /// <param name="timeThreshold">The UTC timestamp defining the start of the check window.</param>
        /// <param name="attemptThreshold">The number of failed attempts that triggers the warning.</param>
        /// <param name="currentIpAddress">The IP address of the current failed attempt (for inclusion in the email).</param>
        private async Task CheckAndSendSuspiciousActivityWarningAsync(PersonEntity person, DateTime timeThreshold, int attemptThreshold, string currentIpAddress)
        {
            try
            {
                // Get recent failed attempts from the repository
                var recentAttemptsResult = await _failedLoginAttemptRepository.GetRecentAttemptsAsync(person.IdPerson, timeThreshold);

                // Log if attempts couldn't be retrieved
                if (recentAttemptsResult.IsFailure)
                {
                    _logger.LogWarning("Could not retrieve recent failed attempts for User ID {UserId} to check for suspicious activity. Error: {Error}", person.IdPerson, recentAttemptsResult.ErrorMessage);
                    return; // Cannot proceed with check
                }

                // Count the number of recent failures
                int failedCount = recentAttemptsResult.Value?.Count() ?? 0;

                // Send warning ONLY if the count JUST reached the threshold (e.g., exactly 5).
                // Prevents sending multiple emails for subsequent failures (6th, 7th, etc.).
                if (failedCount == attemptThreshold)
                {
                    // Log the suspicious activity detection
                    _logger.LogWarning("Suspicious activity detected: {Count} failed login attempts for User ID {UserId} within the time window. Sending warning email.", failedCount, person.IdPerson);
                    try
                    {
                        // Generate the warning email body using the email service
                        string emailBody = _emailService.GenerateSuspiciousActivityBody(person.FirstName, _dateTimeProvider.GetUtcNow(), currentIpAddress);
                        // Send the warning email
                        await _emailService.SendEmailAsync(person.Email, "Suspicious Login Activity Detected", emailBody);
                    }
                    catch (Exception ex)
                    {
                        // Log failure to send the email, but don't fail the login flow
                        _logger.LogError(ex, "Failed to send suspicious activity warning email to {Email} for User ID {UserId}.", person.Email, person.IdPerson);
                    }
                }
                else if (failedCount > attemptThreshold)
                {
                    // Log that threshold has been exceeded (debug level is fine)
                    _logger.LogDebug("User ID {UserId} has exceeded the failed login threshold ({Count}/{Threshold}), warning email likely already sent.", person.IdPerson, failedCount, attemptThreshold);
                }
            }
            catch (Exception ex)
            {
                // Log any unexpected exception during the check/send process
                _logger.LogError(ex, "Exception while checking/sending suspicious activity warning for User ID {UserId}.", person.IdPerson);
            }
        }

        #endregion
    }
}