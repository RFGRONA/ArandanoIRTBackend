using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Application.Interfaces.Services;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Application.Mappings;
using ArandanoIRT_Backend.Application.Utilities;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using ArandanoIRT_Backend.Infrastructure.Interfaces.IServices;

namespace ArandanoIRT_Backend.Application.Services
{
    /// <summary>
    /// Implements authentication and authorization related services.
    /// Handles user registration, login, password management, and token operations.
    /// </summary>
    /// <remarks>
    /// Implements the <see cref="IAuthService"/> interface.
    /// </remarks>
    public class AuthService(
        IPersonRepository personRepository,
        ICropRepository cropRepository,
        ICropInvitationRepository cropInvitationRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IChangePasswordRepository changePasswordRepository,
        IFailedLoginAttemptRepository failedLoginAttemptRepository,
        IStatusRepository statusRepository, 
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        IDateTimeProvider dateTimeProvider,
        IRsaService rsaService,
        ILogger<AuthService> logger) : IAuthService
    {
        // Repositories
        /// <summary>The repository for managing person data.</summary>
        private readonly IPersonRepository _personRepository = personRepository ?? throw new ArgumentNullException(nameof(personRepository));
        /// <summary>The repository for managing crop data.</summary>
        private readonly ICropRepository _cropRepository = cropRepository ?? throw new ArgumentNullException(nameof(cropRepository));
        /// <summary>The repository for managing crop invitation data.</summary>
        private readonly ICropInvitationRepository _cropInvitationRepository = cropInvitationRepository ?? throw new ArgumentNullException(nameof(cropInvitationRepository));
        /// <summary>The repository for managing refresh token data.</summary>
        private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
        /// <summary>The repository for managing password change request data.</summary>
        private readonly IChangePasswordRepository _changePasswordRepository = changePasswordRepository ?? throw new ArgumentNullException(nameof(changePasswordRepository));
        /// <summary>The repository for managing failed login attempt data.</summary>
        private readonly IFailedLoginAttemptRepository _failedLoginAttemptRepository = failedLoginAttemptRepository ?? throw new ArgumentNullException(nameof(failedLoginAttemptRepository));
        /// <summary>The repository for managing status related data (usage assumed).</summary>
        private readonly IStatusRepository _statusRepository = statusRepository ?? throw new ArgumentNullException(nameof(statusRepository));

        // Services & Utilities
        /// <summary>The service for generating and managing authentication tokens.</summary>
        private readonly ITokenService _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        /// <summary>The utility for hashing and verifying passwords.</summary>
        private readonly IPasswordHasher _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        /// <summary>The service for sending emails.</summary>
        private readonly IEmailService _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        /// <summary>The provider for accessing the current date and time.</summary>
        private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        /// <summary>The service for RSA encryption/decryption.</summary>
        private readonly IRsaService _rsaService = rsaService; // Note: Original code did not null-check this specific dependency assignment.
        /// <summary>The logger for recording service events.</summary>
        private readonly ILogger<AuthService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <inheritdoc/>
        public async Task<Result> RegisterAdminAsync(RegisterAdminRequestDto request)
        {
            // 1. Validates input data.
            if (request?.AdminInfo == null || request.CropInfo == null)
            {
                _logger.LogWarning("RegisterAdminAsync called with null request data.");
                return Result.Failure("Invalid registration data provided.");
            }

            var emailValidation = await EmailValidatorUtility.ValidateEmailAsync(request.AdminInfo.Email);
            if (emailValidation.IsFailure)
            {
                _logger.LogWarning("Invalid email format during admin registration: {Email}", request.AdminInfo.Email);
                return Result.Failure($"Invalid email format: {request.AdminInfo.Email}. {emailValidation.ErrorMessage}");
            }

            string decryptedPassword;
            try
            {
                // 2. Decrypts the provided password.
                decryptedPassword = _rsaService.Decrypt(request.AdminInfo.Password);
            }
            catch (Exception ex) // Catches potential decryption errors.
            {
                _logger.LogError(ex, "Failed to decrypt password for email {Email}", request.AdminInfo.Email);
                // Does not reveal specifics about decryption failure to the client.
                return Result.Failure("Invalid password format or data.");
            }

            // 3. Validates password policy (example: length).
            if (string.IsNullOrWhiteSpace(decryptedPassword) || decryptedPassword.Length < 8) // Example: Minimum 8 characters.
            {
                _logger.LogWarning("Password validation failed for email {Email}: Too short", request.AdminInfo.Email);
                return Result.Failure("Password must be at least 8 characters long.");
            }

            // 4. Hashes the decrypted password.
            var hashedPassword = _passwordHasher.Hash(decryptedPassword);
            // Clears decrypted password from memory.
            decryptedPassword = string.Empty;

            // 5. Checks if the email already exists.
            var existingPersonResult = await _personRepository.GetByEmailAsync(request.AdminInfo.Email);
            if (existingPersonResult.IsSuccess) // If GetByEmailAsync returns success, the email exists.
            {
                _logger.LogWarning("Attempted to register admin with existing email: {Email}", request.AdminInfo.Email);
                return Result.Failure($"The email address '{request.AdminInfo.Email}' is already registered.");
            }
            // Expects GetByEmailAsync to fail if the email is not found, which is the desired outcome here.

            var now = _dateTimeProvider.GetUtcNow();

            // 6. Creates the CropEntity using mapping.
            CropEntity cropEntity;
            try
            {
                cropEntity = request.ToCropEntity(now); // Uses the mapping extension method.
            }
            catch (ArgumentException argEx)
            {
                _logger.LogWarning("Invalid crop data provided for admin registration. Error: {Error}", argEx.Message);
                return Result.Failure($"Invalid crop data: {argEx.Message}");
            }

            // 7. Saves the new CropEntity to the database.
            var createCropResult = await _cropRepository.Create(cropEntity);
            if (createCropResult.IsFailure)
            {
                _logger.LogError("Failed to create crop during admin registration for email {Email}. Error: {Error}", request.AdminInfo.Email, createCropResult.ErrorMessage);
                return Result.Failure($"Failed to create crop: {createCropResult.ErrorMessage}");
            }
            var savedCrop = createCropResult.Value; // Gets the entity with the assigned ID.

            // 8. Creates the PersonEntity using mapping and sets remaining properties.
            PersonEntity personEntity;
            try
            {
                // Uses mapping, creating an entity with IsAdmin=true.
                personEntity = request.ToAdminPersonEntity(now);

                // Manually sets properties not handled by basic mapping (hashed password, crop ID).
                typeof(PersonEntity).GetProperty("Password")?.SetValue(personEntity, hashedPassword, null);
                typeof(PersonEntity).GetProperty("CropId")?.SetValue(personEntity, savedCrop.IdCrop, null);

            }
            catch (ArgumentException argEx)
            {
                _logger.LogWarning("Invalid admin data provided for registration. Error: {Error}", argEx.Message);
                // A transaction is recommended to handle cleanup if person creation fails after crop creation.
                return Result.Failure($"Invalid admin data: {argEx.Message}");
            }

            // 9. Saves the new PersonEntity to the database.
            var createPersonResult = await _personRepository.Create(personEntity);
            if (createPersonResult.IsFailure)
            {
                _logger.LogError("Failed to create person during admin registration for email {Email}. Error: {Error}", request.AdminInfo.Email, createPersonResult.ErrorMessage);
                // Critical state: Crop created but person failed. Requires cleanup or transaction.
                // For now, returns failure.
                // await _cropRepository.Delete(savedCrop.IdCrop); // Example: Attempt cleanup (might fail).
                return Result.Failure($"Failed to create user: {createPersonResult.ErrorMessage}");
            }
            var savedPerson = createPersonResult.Value; // Gets the entity with the assigned ID.

            // 10. Updates the created Crop entity with the AdminUserId.
            try
            {
                // Sets the AdminUserId on the previously saved crop entity.
                typeof(CropEntity).GetProperty("AdminUserId")?.SetValue(savedCrop, savedPerson.IdPerson, null);
                // Sets the UpdatedAt timestamp (optional, depends on application logic).
                typeof(CropEntity).GetProperty("UpdatedAt")?.SetValue(savedCrop, _dateTimeProvider.GetUtcNow(), null);

                var updateCropResult = await _cropRepository.Update(savedCrop);
                if (updateCropResult.IsFailure)
                {
                    // Logs a warning, but registration is technically successful. Might require manual correction.
                    _logger.LogWarning("Failed to update AdminUserId ({AdminId}) for CropId {CropId} after admin registration. Error: {Error}",
                                        savedPerson.IdPerson, savedCrop.IdCrop, updateCropResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception occurred while updating AdminUserId for CropId {CropId} after admin registration.", savedCrop.IdCrop);
            }


            // 11. Sends a welcome email to the new administrator.
            try
            {
                string emailBody = _emailService.GenerateWelcomeBody(savedPerson.FirstName);
                await _emailService.SendEmailAsync(savedPerson.Email, "Welcome to Arandano IRT!", emailBody);
            }
            catch (Exception ex)
            {
                // Logs failure to send email, but does not fail the registration process.
                _logger.LogWarning(ex, "Failed to send welcome email to {Email} after successful registration.", savedPerson.Email);
            }

            _logger.LogInformation("Successfully registered new admin {AdminId} for crop {CropId} with email {Email}", savedPerson.IdPerson, savedCrop.IdCrop, savedPerson.Email);
            return Result.Success();
        }

        /// <inheritdoc/>
        public async Task<Result> RegisterUserAsync(RegisterUserRequestDto request)
        {
            _logger.LogInformation("Attempting to register new user for email: {Email} with code {AuthCode}", request?.UserInfo?.Email, request?.UserInfo?.AuthCode);

            // 1. Validates input data.
            if (request?.UserInfo == null || string.IsNullOrWhiteSpace(request.UserInfo.AuthCode))
            {
                _logger.LogWarning("RegisterUserAsync called with null request data or missing auth code.");
                return Result.Failure("Invalid registration data provided. Invitation code is required.");
            }

            var emailValidation = await EmailValidatorUtility.ValidateEmailAsync(request.UserInfo.Email);
            if (emailValidation.IsFailure)
            {
                _logger.LogWarning("Invalid email format during user registration: {Email}", request.UserInfo.Email);
                return Result.Failure($"Invalid email format: {request.UserInfo.Email}. {emailValidation.ErrorMessage}");
            }

            // 2. Validates the invitation code.
            // GetActiveByCodeAsync checks existence, expiry, and 'pending' status.
            var invitationResult = await _cropInvitationRepository.GetActiveByCodeAsync(request.UserInfo.AuthCode);
            if (invitationResult.IsFailure)
            {
                _logger.LogWarning("Invalid or inactive invitation code used: {AuthCode}. Error: {Error}", request.UserInfo.AuthCode, invitationResult.ErrorMessage);
                return Result.Failure($"Invitation code is invalid, expired, or already used. ({invitationResult.ErrorMessage})");
            }
            var validInvitation = invitationResult.Value; // Stores the valid invitation entity.

            string decryptedPassword;
            try
            {
                // 3. Decrypts the provided password.
                decryptedPassword = _rsaService.Decrypt(request.UserInfo.Password);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt password for user registration email {Email}", request.UserInfo.Email);
                return Result.Failure("Invalid password format or data.");
            }

            // 4. Validates password policy (example: length).
            if (string.IsNullOrWhiteSpace(decryptedPassword) || decryptedPassword.Length < 8)
            {
                _logger.LogWarning("User password validation failed for email {Email}: Too short", request.UserInfo.Email);
                return Result.Failure("Password must be at least 8 characters long.");
            }

            // 5. Hashes the decrypted password.
            var hashedPassword = _passwordHasher.Hash(decryptedPassword);
            decryptedPassword = string.Empty; // Clears decrypted password.

            // 6. Checks if the email already exists.
            var existingPersonResult = await _personRepository.GetByEmailAsync(request.UserInfo.Email);
            if (existingPersonResult.IsSuccess)
            {
                _logger.LogWarning("Attempted to register user with existing email: {Email}", request.UserInfo.Email);
                return Result.Failure($"The email address '{request.UserInfo.Email}' is already registered.");
            }

            var now = _dateTimeProvider.GetUtcNow();

            // 7. Creates the PersonEntity using mapping.
            PersonEntity personEntity;
            try
            {
                // Uses mapping, passing the CropId from the validated invitation (creates entity with IsAdmin=false).
                personEntity = request.ToUserPersonEntity(now, validInvitation.CropId);

                // Manually sets the hashed password.
                typeof(PersonEntity).GetProperty("Password")?.SetValue(personEntity, hashedPassword, null);
            }
            catch (ArgumentException argEx)
            {
                _logger.LogWarning("Invalid user data provided for registration. Error: {Error}", argEx.Message);
                return Result.Failure($"Invalid user data: {argEx.Message}");
            }

            // 8. Saves the new PersonEntity to the database.
            var createPersonResult = await _personRepository.Create(personEntity);
            if (createPersonResult.IsFailure)
            {
                _logger.LogError("Failed to create person during user registration for email {Email}. Error: {Error}", request.UserInfo.Email, createPersonResult.ErrorMessage);
                return Result.Failure($"Failed to create user: {createPersonResult.ErrorMessage}");
            }
            var savedPerson = createPersonResult.Value; // Gets the entity with the assigned ID.

            // 9. Marks the invitation code as used.
            try
            {
                // Calls the specific repository method to handle marking as used.
                var markUsedResult = await _cropInvitationRepository.MarkAsUsedAsync(validInvitation.IdCropInvitation, savedPerson.IdPerson);

                if (markUsedResult.IsFailure)
                {
                    // Logs warning: user created but invitation not marked. Indicates potential inconsistency.
                    _logger.LogWarning("Failed to mark invitation code {AuthCode} (ID: {InvitationId}) as used for User ID {UserId} after user creation. Error: {Error}",
                                         request.UserInfo.AuthCode, validInvitation.IdCropInvitation, savedPerson.IdPerson, markUsedResult.ErrorMessage);
                    // Current behavior: logs and continues. Transaction recommended for atomicity.
                }
                else
                {
                    _logger.LogInformation("Successfully marked invitation code {AuthCode} (ID: {InvitationId}) as used by User ID {UserId}.", request.UserInfo.AuthCode, validInvitation.IdCropInvitation, savedPerson.IdPerson);
                }
            }
            catch (Exception ex) // Catches unexpected errors from the repository call.
            {
                _logger.LogError(ex, "Exception occurred while calling MarkAsUsedAsync for invitation {InvitationId}, User ID {UserId}.", validInvitation.IdCropInvitation, savedPerson.IdPerson);
                // Logs and continues, user creation succeeded.
            }


            // 10. Sends a welcome email to the new user.
            try
            {
                string emailBody = _emailService.GenerateWelcomeBody(savedPerson.FirstName);
                await _emailService.SendEmailAsync(savedPerson.Email, "Welcome to Arandano IRT!", emailBody);
            }
            catch (Exception ex)
            {
                // Logs failure to send email, but does not fail the registration process.
                _logger.LogWarning(ex, "Failed to send welcome email to {Email} after successful user registration.", savedPerson.Email);
            }

            _logger.LogInformation("Successfully registered new user {UserId} for crop {CropId} with email {Email}", savedPerson.IdPerson, validInvitation.CropId, savedPerson.Email);
            return Result.Success();
        }

        /// <inheritdoc/>
        public async Task<Result<LoginSuccessPayload>> LoginAsync(LoginRequestDto request, string ipAddress, string userAgent, string? deviceInfo = null)
        {
            _logger.LogInformation("Attempting login for email: {Email} from IP: {IPAddress}", request?.Email, ipAddress);

            // 1. Validates input data.
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Result<LoginSuccessPayload>.Failure("Email and password are required.");
            }

            var emailValidation = await EmailValidatorUtility.ValidateEmailAsync(request.Email);
            // Does not reveal specific email validity issues to the user.
            if (emailValidation.IsFailure)
            {
                _logger.LogWarning("Invalid email format during login: {Email}", request.Email);
                return Result<LoginSuccessPayload>.Failure("Invalid email or password.");
            }

            // 2. Retrieves the user by email.
            var personResult = await _personRepository.GetByEmailAsync(request.Email);
            if (personResult.IsFailure)
            {
                // User not found. Logs the attempt but returns a generic error message.
                _logger.LogWarning("Login failed: User not found for email {Email} from IP {IPAddress}", request.Email, ipAddress);
                // Only records failed attempts for existing users to avoid database pollution for non-existent emails.
                return Result<LoginSuccessPayload>.Failure("Invalid email or password.");
            }
            var person = personResult.Value; // User exists.

            // 3. Defines constants for failed login attempt logic.
            const int failedAttemptThreshold = 5;
            TimeSpan failedAttemptWindow = TimeSpan.FromMinutes(30); // Sets the window for checking attempts.
            var timeThreshold = _dateTimeProvider.GetUtcNow().Subtract(failedAttemptWindow);
            var now = _dateTimeProvider.GetUtcNow();

            string decryptedPassword;
            try
            {
                // 4. Decrypts the provided password.
                decryptedPassword = _rsaService.Decrypt(request.Password);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt password during login for email {Email}", request.Email);
                // Records the failed attempt before returning.
                await RecordFailedLoginAttemptAsync(person.IdPerson, now, ipAddress, deviceInfo, userAgent);
                // Checks if a warning email is needed after recording the failure.
                await CheckAndSendSuspiciousActivityWarningAsync(person, timeThreshold, failedAttemptThreshold, ipAddress);
                return Result<LoginSuccessPayload>.Failure("Invalid email or password."); // Returns generic error.
            }

            // 5. Verifies the decrypted password against the stored hash.
            bool isPasswordValid = _passwordHasher.Verify(person.Password, decryptedPassword);
            decryptedPassword = string.Empty; // Clears decrypted password.

            if (!isPasswordValid)
            {
                // 6. Handles the failed login attempt (invalid password).
                _logger.LogWarning("Login failed: Invalid password for email {Email} from IP {IPAddress}", request.Email, ipAddress);
                // Records the failed attempt.
                await RecordFailedLoginAttemptAsync(person.IdPerson, now, ipAddress, deviceInfo, userAgent);
                // Checks if a warning email is needed after recording the failure.
                await CheckAndSendSuspiciousActivityWarningAsync(person, timeThreshold, failedAttemptThreshold, ipAddress);
                return Result<LoginSuccessPayload>.Failure("Invalid email or password.");
            }

            // 7. Handles the successful login.
            _logger.LogInformation("Login successful for email {Email} (User ID: {UserId}) from IP {IPAddress}", request.Email, person.IdPerson, ipAddress);

            // Updates the LastLoginAt timestamp for the user.
            try
            {
                typeof(PersonEntity).GetProperty("LastLoginAt")?.SetValue(person, now, null);
                var updateResult = await _personRepository.Update(person);
                if (updateResult.IsFailure)
                {
                    // Logs warning but continues; login succeeded even if timestamp update failed.
                    _logger.LogWarning("Failed to update LastLoginAt for User ID {UserId}. Error: {Error}", person.IdPerson, updateResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception occurred while updating LastLoginAt for User ID {UserId}.", person.IdPerson);
            }


            // Generates new access and refresh tokens.
            var tokenResult = await _tokenService.GenerateAndStoreTokensAsync(person, ipAddress, userAgent, deviceInfo);
            if (tokenResult.IsFailure)
            {
                _logger.LogError("Failed to generate tokens for User ID {UserId} after successful login. Error: {Error}", person.IdPerson, tokenResult.ErrorMessage);
                return Result<LoginSuccessPayload>.Failure("Login succeeded but failed to create session tokens. Please try again.");
            }
            var generatedTokens = tokenResult.Value;

            // Maps the PersonEntity to the LoginResponseDto.
            var loginResponseDto = person.ToLoginResponseDto(); // Uses mapping extension.
            loginResponseDto.Token = generatedTokens.AccessToken; // Sets the generated JWT access token.

            // Creates the final payload containing login details and the refresh token.
            var payload = new LoginSuccessPayload
            {
                LoginDetails = loginResponseDto,
                RefreshToken = generatedTokens.RefreshToken
            };

            return Result<LoginSuccessPayload>.Success(payload);
        }


        /// <inheritdoc/>
        public async Task<Result> RequestPasswordResetAsync(ForgotPasswordRequestDto request)
        {
            _logger.LogInformation("Password reset requested for email: {Email}", request?.Email);

            // 1. Validates input data.
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return Result.Failure("Email is required.");
            }

            var emailValidation = await EmailValidatorUtility.ValidateEmailAsync(request.Email);
            if (emailValidation.IsFailure)
            {
                _logger.LogWarning("Invalid email format for password reset request: {Email}", request.Email);
                // Returns success even if email format is invalid to prevent email enumeration attacks.
                _logger.LogInformation("Password reset requested for potentially invalid email {Email}. Responding with success to prevent enumeration.", request.Email);
                return Result.Success();
            }

            // 2. Finds the user by email.
            var personResult = await _personRepository.GetByEmailAsync(request.Email);
            if (personResult.IsFailure)
            {
                // User not found, but returns success to the client to prevent enumeration.
                _logger.LogWarning("Password reset requested for non-existent email: {Email}. Responding with success to prevent enumeration.", request.Email);
                return Result.Success(); // Important: Prevents email enumeration.
            }
            var person = personResult.Value;

            // 3. Generates a secure password reset token.
            string resetToken;
            try
            {
                // Uses 8 bytes -> ~11 Base64Url chars (secure, reasonably short for copy-paste).
                resetToken = GenerateSecureRandomString(8);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate secure random string for password reset token for User ID {UserId}.", person.IdPerson);
                return Result.Failure("Failed to generate reset token.");
            }


            // 4. Stores the generated reset token in the database.
            var now = _dateTimeProvider.GetUtcNow();
            var expiresAt = now.AddMinutes(30); // Sets token validity to 30 minutes.

            var changePasswordEntity = new ChangePasswordEntity(
                idChangePassword: 0, // DB generates ID.
                passwordResetToken: resetToken,
                resetTokenExpiresAt: expiresAt,
                tokenCreatedAt: now,
                personId: person.IdPerson
            );

            // Repository's Create method typically handles deleting previous tokens for the user.
            var createTokenResult = await _changePasswordRepository.Create(changePasswordEntity);
            if (createTokenResult.IsFailure)
            {
                _logger.LogError("Failed to store password reset token for User ID {UserId}. Error: {Error}", person.IdPerson, createTokenResult.ErrorMessage);
                return Result.Failure($"Failed to save password reset request: {createTokenResult.ErrorMessage}");
            }

            // 5. Sends the password reset email containing the token.
            try
            {
                string emailBody = _emailService.GeneratePasswordResetTokenBody(person.FirstName, resetToken);
                await _emailService.SendEmailAsync(person.Email, "Your Password Reset Code", emailBody);
                _logger.LogInformation("Password reset email successfully sent to {Email} for User ID {UserId}.", person.Email, person.IdPerson);
            }
            catch (Exception ex)
            {
                // Logs failure to send email, but considers the operation successful as token was generated/stored.
                _logger.LogError(ex, "Failed to send password reset email to {Email} for User ID {UserId}, although token was generated.", person.Email, person.IdPerson);
            }

            // Returns success regardless of email sending outcome (unless policy dictates otherwise)
            // and always returns success if the user wasn't found (prevents enumeration).
            return Result.Success();
        }

        /// <inheritdoc/>
        public async Task<Result> ResetPasswordAsync(ChangePasswordRequestDto request)
        {
            _logger.LogInformation("Attempting to reset password using code: {ChangeCode}", request?.ChangeCode);

            // 1. Validates input data.
            if (request == null || string.IsNullOrWhiteSpace(request.ChangeCode) || string.IsNullOrWhiteSpace(request.NewPassword) || string.IsNullOrWhiteSpace(request.ConfirmPassword))
            {
                return Result.Failure("Reset code, new password, and confirmation password are required.");
            }

            if (request.NewPassword != request.ConfirmPassword)
            {
                return Result.Failure("New password and confirmation password do not match.");
            }

            string decryptedPassword;
            try
            {
                // Decrypts the new password.
                decryptedPassword = _rsaService.Decrypt(request.NewPassword);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt new password during reset.");
                return Result.Failure("Invalid new password format or data.");
            }

            // Validates password policy.
            if (string.IsNullOrWhiteSpace(decryptedPassword) || decryptedPassword.Length < 8)
            {
                return Result.Failure("New password must be at least 8 characters long.");
            }

            // 2. Validates the provided reset token.
            var tokenResult = await _changePasswordRepository.GetActiveByTokenAsync(request.ChangeCode);
            if (tokenResult.IsFailure)
            {
                _logger.LogWarning("Invalid or expired password reset code used: {ChangeCode}. Error: {Error}", request.ChangeCode, tokenResult.ErrorMessage);
                // Uses specific message for invalid/expired token.
                return Result.Failure($"Password reset code is invalid or has expired. Please request a new one.");
            }
            var validToken = tokenResult.Value; // Stores the valid token entity.

            // Updated code to handle nullable value type
            int personId = validToken.PersonId ?? throw new InvalidOperationException("PersonId cannot be null.");
            // No need to retrieve the full user entity if UpdatePasswordAsync only needs the ID.

            // Ensures PersonId from token is valid (safety check).
            if (personId <= 0)
            {
                _logger.LogError("Password reset token {TokenId} has an invalid PersonId {PersonId}", validToken.IdChangePassword, personId);
                return Result.Failure("Invalid reset token data.");
            }

            // 4. Hashes the new (decrypted) password.
            var hashedPassword = _passwordHasher.Hash(decryptedPassword);
            decryptedPassword = string.Empty; // Clears decrypted password.

            // 5. Updates the user's password in the database.
            var now = _dateTimeProvider.GetUtcNow();
            var updatePasswordResult = await _personRepository.UpdatePasswordAsync(personId, hashedPassword, now);

            if (updatePasswordResult.IsFailure)
            {
                _logger.LogError("Failed to update password for User ID {UserId} during reset using token {TokenId}. Error: {Error}",
                                 personId, validToken.IdChangePassword, updatePasswordResult.ErrorMessage);
                // Fails definitively if password update fails.
                return Result.Failure($"Failed to update password: {updatePasswordResult.ErrorMessage}");
            }

            _logger.LogInformation("Password successfully updated for User ID {UserId} using reset token {TokenId}.", personId, validToken.IdChangePassword);


            // 6. Invalidates (deletes) the used reset token.
            var deleteTokenResult = await _changePasswordRepository.Delete(validToken.IdChangePassword);
            if (deleteTokenResult.IsFailure)
            {
                // Logs warning: password changed but token cleanup failed. Manual cleanup might be needed.
                _logger.LogWarning("Failed to delete used password reset token {TokenId} for User ID {UserId}. Error: {Error}",
                                  validToken.IdChangePassword, personId, deleteTokenResult.ErrorMessage);
            }

            // 7. Sends a confirmation email about the password change.
            var personDetailsResult = await _personRepository.GetById(personId); // Retrieves minimal details for email.
            if (personDetailsResult.IsSuccess)
            {
                try
                {
                    string emailBody = _emailService.GeneratePasswordChangeConfirmationBody(personDetailsResult.Value.FirstName);
                    await _emailService.SendEmailAsync(personDetailsResult.Value.Email, "Password Changed Successfully", emailBody);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send password change confirmation email to {Email} for User ID {UserId}.", personDetailsResult.Value.Email, personId);
                    // Does not fail the overall operation.
                }
            }
            else
            {
                _logger.LogWarning("Could not retrieve user details for User ID {UserId} to send password change confirmation email.", personId);
            }


            // 8. Returns success.
            return Result.Success();
        }

        /// <inheritdoc/>
        public async Task<Result> LogoutAsync(string refreshTokenValue, string ipAddress)
        {
            _logger.LogInformation("Attempting to logout/revoke specific refresh token from IP: {IPAddress}", ipAddress);
            // Delegates logic to ITokenService.RevokeRefreshTokenAsync.
            return await _tokenService.RevokeRefreshTokenAsync(refreshTokenValue, ipAddress);
        }

        /// <inheritdoc/>
        public async Task<Result> LogoutEverywhereAsync(long sessionId, string ipAddress)
        {
            _logger.LogInformation("Attempting to logout/revoke all tokens for session ID: {SessionId} from IP: {IPAddress}", sessionId, ipAddress);
            // Delegates logic directly to IRefreshTokenRepository.RevokeBySessionIdAsync.
            return await _refreshTokenRepository.RevokeBySessionIdAsync(sessionId, ipAddress);
        }

        /// <inheritdoc/>
        public async Task<Result<TokenResponseDto>> RefreshTokenAsync(string refreshTokenValue, string ipAddress, string userAgent, string? deviceInfo = null)
        {
            _logger.LogInformation("Attempting to refresh token from IP: {IPAddress}", ipAddress);
            // Delegates most logic to ITokenService.RefreshTokenAsync.
            return await _tokenService.RefreshTokensAsync(refreshTokenValue, ipAddress, userAgent, deviceInfo);
        }

        /// <inheritdoc/>
        public async Task<Result> SendHelpRequestAsync(HelpRequestDto request)
        {
            // 1. Validate input DTO
            if (request == null)
            {
                _logger.LogWarning("SendHelpRequestAsync called with a null request object.");
                return Result.Failure("Help request data cannot be null.");
            }
            // Basic validation for required fields (although DTO attributes should handle this)
            if (string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Subject) ||
                string.IsNullOrWhiteSpace(request.CropName) ||
                string.IsNullOrWhiteSpace(request.Message))
            {
                _logger.LogWarning("SendHelpRequestAsync called with missing required fields.");
                return Result.Failure("All fields (Name, Email, Subject, Crop Name, Message) are required.");
            }

            _logger.LogInformation("Processing help request from {RequesterEmail} regarding crop '{CropName}'.", request.Email, request.CropName);

            // 2. Validate Crop Name existence
            var cropResult = await _cropRepository.GetByNameAsync(request.CropName);
            if (cropResult.IsFailure)
            {
                _logger.LogWarning("Help request failed: Crop '{CropName}' not found. Error: {Error}", request.CropName, cropResult.ErrorMessage);
                // Return a user-friendly error, don't expose internal details like "not found".
                return Result.Failure($"The specified crop '{request.CropName}' could not be found. Please check the name and try again.");
            }
            var crop = cropResult.Value;

            // 3. Get the Administrator's details
            if (!crop.AdminUserId.HasValue)
            {
                _logger.LogError("Help request failed: Crop '{CropName}' (ID: {CropId}) does not have an assigned administrator.", crop.NameCrop, crop.IdCrop);
                // This indicates a data integrity issue.
                return Result.Failure("Could not process the request because the crop does not have an assigned administrator.");
            }

            var adminResult = await _personRepository.GetById(crop.AdminUserId.Value);
            if (adminResult.IsFailure)
            {
                _logger.LogError("Help request failed: Could not find administrator with ID {AdminId} for crop '{CropName}'. Error: {Error}", crop.AdminUserId.Value, crop.NameCrop, adminResult.ErrorMessage);
                // This is also likely a data integrity issue.
                return Result.Failure("Could not process the request due to an internal error finding the administrator.");
            }
            var administrator = adminResult.Value;

            // 4. Generate the email body
            string emailBody;
            try
            {
                emailBody = _emailService.GenerateHelpRequestBody(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate help request email body for request from {RequesterEmail}.", request.Email);
                return Result.Failure("Failed to prepare the help request email.");
            }

            // 5. Send the email to the administrator
            string emailSubject = $"[Ayuda ArandanoIRT] Nueva Solicitud: {request.Subject}"; 
            var emailResult = await _emailService.SendEmailAsync(administrator.Email, emailSubject, emailBody);

            if (emailResult.IsFailure)
            {
                _logger.LogError("Failed to send help request email to administrator {AdminEmail} for crop '{CropName}'. Error: {Error}", administrator.Email, crop.NameCrop, emailResult.ErrorMessage);
                return Result.Failure($"Failed to send the help request email. Please try again later or contact support directly. Error: {emailResult.ErrorMessage}");
            }

            // 6. Return success
            return Result.Success(); 
        }

        // Private Helper Methods

        /// <summary>
        /// Records a failed login attempt in the database.
        /// </summary>
        /// <param name="personId">The ID of the user for whom the login failed.</param>
        /// <param name="attemptTime">The UTC timestamp of the failed attempt.</param>
        /// <param name="ipAddress">The IP address from which the attempt originated.</param>
        /// <param name="deviceInfo">Optional formatted device information string.</param>
        /// <param name="userAgent">The user agent string of the client.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task RecordFailedLoginAttemptAsync(int personId, DateTime attemptTime, string ipAddress, string? deviceInfo, string userAgent)
        {
            try
            {
                var failedAttempt = new FailedLoginAttemptEntity(
                    idFailedLoginAttempt: 0,
                    attemptDate: attemptTime,
                    ipAddress: ipAddress ?? "Unknown",
                    deviceInfo: deviceInfo ?? "Unknown",
                    userAgent: userAgent ?? "Unknown",
                    personId: personId
                );
                var createAttemptResult = await _failedLoginAttemptRepository.Create(failedAttempt);
                if (createAttemptResult.IsFailure)
                {
                    _logger.LogError("Failed to record failed login attempt for User ID {UserId}. Error: {Error}", personId, createAttemptResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while recording failed login attempt for User ID {UserId}.", personId);
            }
        }

        /// <summary>
        /// Checks the count of recent failed login attempts and sends a warning email if a threshold is met.
        /// </summary>
        /// <param name="person">The PersonEntity for whom the check is being performed.</param>
        /// <param name="timeThreshold">The UTC timestamp defining the start of the check window.</param>
        /// <param name="attemptThreshold">The number of failed attempts that triggers the warning.</param>
        /// <param name="currentIpAddress">The IP address of the current failed attempt (for inclusion in the email).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task CheckAndSendSuspiciousActivityWarningAsync(PersonEntity person, DateTime timeThreshold, int attemptThreshold, string currentIpAddress)
        {
            try
            {
                var recentAttemptsResult = await _failedLoginAttemptRepository.GetRecentAttemptsAsync(person.IdPerson, timeThreshold);

                if (recentAttemptsResult.IsFailure)
                {
                    _logger.LogWarning("Could not retrieve recent failed attempts for User ID {UserId} to check for suspicious activity. Error: {Error}", person.IdPerson, recentAttemptsResult.ErrorMessage);
                    return; // Cannot proceed with check.
                }

                int failedCount = recentAttemptsResult.Value?.Count() ?? 0;

                // Sends warning ONLY if the count JUST reached the threshold (e.g., exactly 5).
                // Prevents sending multiple emails for subsequent failures (6th, 7th, etc.).
                if (failedCount == attemptThreshold)
                {
                    _logger.LogWarning("Suspicious activity detected: {Count} failed login attempts for User ID {UserId} within the time window. Sending warning email.", failedCount, person.IdPerson);
                    try
                    {
                        string emailBody = _emailService.GenerateSuspiciousActivityBody(person.FirstName, _dateTimeProvider.GetUtcNow(), currentIpAddress);
                        await _emailService.SendEmailAsync(person.Email, "Suspicious Login Activity Detected", emailBody);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send suspicious activity warning email to {Email} for User ID {UserId}.", person.Email, person.IdPerson);
                        // Does not fail the login flow because of email failure.
                    }
                }
                else if (failedCount > attemptThreshold)
                {
                    // Logs that threshold has been exceeded, indicating warning was likely already sent.
                    _logger.LogDebug("User ID {UserId} has exceeded the failed login threshold ({Count}/{Threshold}), warning email likely already sent.", person.IdPerson, failedCount, attemptThreshold);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while checking/sending suspicious activity warning for User ID {UserId}.", person.IdPerson);
            }
        }

        /// <summary>
        /// Generates a cryptographically secure random string using RNG.
        /// </summary>
        /// <param name="byteLength">The desired length of the underlying byte array (default 32).</param>
        /// <returns>A Base64Url encoded string representation of the random bytes.</returns>
        /// <remarks>
        /// Base64Url encoding is used for URL safety, suitable for tokens passed in URLs or headers.
        /// </remarks>
        private static string GenerateSecureRandomString(int byteLength = 32)
        {
            using var rng = RandomNumberGenerator.Create();
            var randomBytes = new byte[byteLength];
            rng.GetBytes(randomBytes);
            // Base64Url encoding provides URL-safe characters.
            return Base64UrlEncoder.Encode(randomBytes);
        }
    }
}