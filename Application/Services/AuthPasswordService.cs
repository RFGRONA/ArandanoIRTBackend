using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Application.Interfaces.IServices;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Application.Utilities;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace ArandanoIRT_Backend.Application.Services
{
    /// <summary>
    /// Implements services for handling password reset requests and password changes using tokens.
    /// </summary>
    public class AuthPasswordService : IAuthPasswordService
    {
        // Repositories
        private readonly IPersonRepository _personRepository;
        private readonly IChangePasswordRepository _changePasswordRepository;

        // Services & Utilities
        private readonly IAuthUtilities _authUtilities;
        private readonly IEmailService _emailService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<AuthPasswordService> _logger;

        // Constructor for dependency injection
        public AuthPasswordService(
            IPersonRepository personRepository,
            IChangePasswordRepository changePasswordRepository,
            IAuthUtilities authUtilities,
            IEmailService emailService,
            IDateTimeProvider dateTimeProvider,
            ILogger<AuthPasswordService> logger)
        {
            _personRepository = personRepository ?? throw new ArgumentNullException(nameof(personRepository));
            _changePasswordRepository = changePasswordRepository ?? throw new ArgumentNullException(nameof(changePasswordRepository));
            _authUtilities = authUtilities ?? throw new ArgumentNullException(nameof(authUtilities));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<Result> RequestPasswordResetAsync(ForgotPasswordRequestDto request)
        {
            // Log entry point
            _logger.LogInformation("Processing password reset request for email: {Email}", request?.Email);

            // Null check for request DTO
            if (request == null)
            {
                _logger.LogWarning("{ServiceMethod} called with null request object.", nameof(RequestPasswordResetAsync));
                // Always return success to prevent enumeration
                return Result.Success();
            }

            // --- Step 1: Validate Input ---
            // This helper internally handles logging and returns Success even for invalid email format
            var validationResult = await ValidatePasswordResetRequestInputAsync(request);
            // We only proceed if the *format* was technically valid, even if we return Success on format failure.
            // If format is invalid, no need to check user existence or generate token.
            if (validationResult.IsFailure)
            {
                // Logged inside helper, just return success as per enumeration protection
                return Result.Success();
            }


            // --- Step 2: Find User ---
            // Email guaranteed non-null by initial check and format validation success
            var personResult = await _authUtilities.FindUserByEmailAsync(request.Email!);
            if (personResult.IsFailure)
            {
                // User not found, log warning but return success to prevent enumeration.
                _logger.LogWarning("Password reset requested for non-existent email: {Email}. Responding with success.", request.Email);
                return Result.Success();
            }
            var person = personResult.Value;

            // --- Step 3: Generate and Store Reset Token ---
            var tokenGenerationResult = await GenerateAndStorePasswordResetTokenAsync(person);
            // tokenGenerationResult is Result<string>
            if (tokenGenerationResult.IsFailure)
            {
                // Failure creating token is an internal error, safe to return Failure here.
                // Error logged inside helper.
                return Result.Failure(tokenGenerationResult.ErrorMessage); // Use simple message
            }
            string resetToken = tokenGenerationResult.Value;

            // --- Step 4: Send Reset Email (Best effort) ---
            await SendPasswordResetEmailAsync(person, resetToken);

            // Log completion of initiation phase
            _logger.LogInformation("Password reset process initiated successfully for email {Email}.", request.Email);
            // Always return success to the caller endpoint
            return Result.Success();
        }

        /// <inheritdoc/>
        public async Task<Result> ResetPasswordAsync(ChangePasswordRequestDto request)
        {
            // Log entry point
            _logger.LogInformation("Attempting password reset using token.");

            // Null check for request DTO
            if (request == null)
            {
                _logger.LogWarning("{ServiceMethod} called with null request object.", nameof(ResetPasswordAsync));
                return Result.Failure("Password reset data cannot be null.");
            }

            // --- Step 1: Validate Input and New Password Match ---
            var inputValidationResult = ValidatePasswordResetInput(request);
            if (inputValidationResult.IsFailure) return inputValidationResult; // Returns Result

            // --- Step 2: Process New Password ---
            // NewPassword guaranteed non-null by validation
            var passwordProcessingResult = _authUtilities.ProcessPassword(request.NewPassword!, nameof(ResetPasswordAsync)); 
            if (passwordProcessingResult.IsFailure) return Result.Failure(passwordProcessingResult.ErrorMessage);
            string newHashedPassword = passwordProcessingResult.Value;

            // --- Step 3: Validate Reset Token ---
            // ChangeCode guaranteed non-null by validation
            var tokenValidationResult = await ValidatePasswordResetTokenAsync(request.ChangeCode!);
            // tokenValidationResult is Result<ChangePasswordEntity>
            if (tokenValidationResult.IsFailure) return Result.Failure(tokenValidationResult.ErrorMessage); // Convert to Result.Failure
            var validToken = tokenValidationResult.Value;
            // PersonId guaranteed non-null and > 0 by validation helper
            int personId = validToken.PersonId!.Value;

            // --- Transaction Start Recommended Here ---
            // Ensures password update and token deletion are atomic

            // --- Step 4: Update User's Password ---
            var updateResult = await UpdateUserPasswordAsync(personId, newHashedPassword);
            if (updateResult.IsFailure) return updateResult; // Returns Result

            // --- Step 5: Invalidate (Delete) Used Reset Token ---
            // Best effort - log errors but don't fail the overall process if deletion fails
            await DeletePasswordResetTokenAsync(validToken.IdChangePassword, personId);

            // --- Transaction Commit Recommended Here ---

            // --- Step 6: Send Confirmation Email (Best effort) ---
            await SendPasswordChangeConfirmationEmailAsync(personId);

            // Log successful completion
            _logger.LogInformation("Password successfully reset for User ID {UserId} using token ID {TokenId}.", personId, validToken.IdChangePassword);
            // Return success
            return Result.Success();
        }

        // --- Private Helper Methods ---

        #region Input Validation Helpers

        /// <summary>
        /// Validates input for a password reset request, protecting against enumeration.
        /// </summary>
        /// <param name="request">The password reset request DTO.</param>
        /// <returns>Success result if email format is valid, Failure otherwise (but caller should return Success).</returns>
        private async Task<Result> ValidatePasswordResetRequestInputAsync(ForgotPasswordRequestDto request)
        {
            // Email presence is checked by caller (or should be). Here we check format.
            if (string.IsNullOrWhiteSpace(request.Email)) // Double check, though caller does too
            {
                _logger.LogWarning("Password reset requested with empty email field.");
                // Treat as invalid format for internal flow control, but caller returns Success.
                return Result.Failure("Email is required.");
            }

            // Validate email format
            var emailValidation = await EmailValidatorUtility.ValidateEmailAsync(request.Email);
            if (emailValidation.IsFailure)
            {
                // Log detailed error
                _logger.LogWarning("Password reset requested for invalid email format: {Email}. Reason: {Reason}", request.Email, emailValidation.ErrorMessage);
                // Indicate failure for internal flow, but caller returns Success.
                return Result.Failure("Invalid email format.");
            }
            // Format is valid
            return Result.Success();
        }

        /// <summary>
        /// Validates input for the password reset confirmation step (token, new password, confirmation).
        /// </summary>
        /// <param name="request">The change password request DTO.</param>
        /// <returns>A success or failure result.</returns>
        private Result ValidatePasswordResetInput(ChangePasswordRequestDto request)
        {
            // Check required fields are present
            // Null check for 'request' itself is done by the caller.
            if (string.IsNullOrWhiteSpace(request.ChangeCode) ||
                string.IsNullOrWhiteSpace(request.NewPassword) ||
                string.IsNullOrWhiteSpace(request.ConfirmPassword))
            {
                _logger.LogWarning("Password reset failed: Missing code, new password, or confirmation.");
                return Result.Failure("Reset code, new password, and confirmation password are required.");
            }

            // Check if new password and confirmation match
            if (request.NewPassword != request.ConfirmPassword)
            {
                _logger.LogWarning("Password reset failed: New password and confirmation do not match.");
                return Result.Failure("New password and confirmation password do not match.");
            }

            // All checks passed
            return Result.Success();
        }

        #endregion

        #region Token Processing Helper

        /// <summary>
        /// Generates a secure random string (e.g., for tokens). (Static helper).
        /// </summary>
        /// <param name="byteLength">The desired length of the underlying byte array.</param>
        /// <returns>A Base64Url encoded string representation of the random bytes.</returns>
        private static string GenerateSecureRandomString(int byteLength = 8) // Using 16 bytes for slightly shorter tokens (~11 chars)
        {
            using var rng = RandomNumberGenerator.Create();
            var randomBytes = new byte[byteLength];
            rng.GetBytes(randomBytes);
            // Base64Url encoding provides URL-safe characters.
            return Base64UrlEncoder.Encode(randomBytes);
        }

        /// <summary>
        /// Generates and stores a new password reset token for the user.
        /// Assumes prior tokens for the same user are handled (e.g., invalidated or deleted) by the repository's Create method.
        /// </summary>
        /// <param name="person">The PersonEntity for whom to generate the token.</param>
        /// <returns>Result containing the generated token string on success, or Failure.</returns>
        private async Task<Result<string>> GenerateAndStorePasswordResetTokenAsync(PersonEntity person)
        {
            string resetToken;
            try
            {
                // Generate a cryptographically secure token
                resetToken = GenerateSecureRandomString(16); // e.g., 16 bytes = ~22 URL-safe chars
            }
            catch (Exception ex)
            {
                // Log failure during token generation
                _logger.LogError(ex, "Failed to generate secure random string for password reset token for User ID {UserId}.", person.IdPerson);
                // Simple failure message
                return Result<string>.Failure("Failed to generate reset token.");
            }

            var now = _dateTimeProvider.GetUtcNow();
            // Define token expiration (e.g., 30 minutes) - move to config?
            var expiresAt = now.AddMinutes(30);

            // Create the entity to be saved
            var changePasswordEntity = new ChangePasswordEntity(
                idChangePassword: 0, // DB generates ID
                passwordResetToken: resetToken,
                resetTokenExpiresAt: expiresAt,
                tokenCreatedAt: now,
                personId: person.IdPerson
            );

            // Create the token record in the repository
            // Repository Create should handle logic like removing/invalidating old tokens for this personId
            var createTokenResult = await _changePasswordRepository.Create(changePasswordEntity);
            if (createTokenResult.IsFailure)
            {
                // Log repository error
                _logger.LogError("Failed to store password reset token for User ID {UserId}. Error: {Error}", person.IdPerson, createTokenResult.ErrorMessage);
                // Simple failure message
                return Result<string>.Failure($"Failed to save password reset request.");
            }

            // Return the generated token string
            return Result<string>.Success(resetToken);
        }


        /// <summary>
        /// Validates the password reset token existence, expiry, and data integrity.
        /// </summary>
        /// <param name="token">The password reset token string.</param>
        /// <returns>Result containing the valid ChangePasswordEntity on success, or Failure.</returns>
        private async Task<Result<ChangePasswordEntity>> ValidatePasswordResetTokenAsync(string token)
        {
            // Attempt to retrieve the active token from the repository
            var tokenResult = await _changePasswordRepository.GetActiveByTokenAsync(token);

            // Check if the token was not found or is inactive/expired
            if (tokenResult.IsFailure)
            {
                // Log detailed reason
                _logger.LogWarning("Password reset failed: Invalid or expired token provided. Reason: {Error}", tokenResult.ErrorMessage);
                // Simple message for user
                return Result<ChangePasswordEntity>.Failure("Password reset code is invalid or has expired. Please request a new one.");
            }
            var validToken = tokenResult.Value; // Token entity exists and is active

            // Double-check PersonId integrity (should be guaranteed by GetActiveByTokenAsync, but good practice)
            if (!validToken.PersonId.HasValue || validToken.PersonId.Value <= 0)
            {
                _logger.LogError("Password reset token {TokenId} has invalid PersonId: {PersonId}", validToken.IdChangePassword, validToken.PersonId);
                return Result<ChangePasswordEntity>.Failure("Invalid reset token data encountered.");
            }

            // Token is valid and associated with a valid user ID
            return tokenResult; // Return the valid token entity
        }

        /// <summary>
        /// Updates the user's password hash in the person repository.
        /// </summary>
        /// <param name="personId">The ID of the user whose password to update.</param>
        /// <param name="newHashedPassword">The new hashed password.</param>
        /// <returns>A success or failure result.</returns>
        private async Task<Result> UpdateUserPasswordAsync(int personId, string newHashedPassword)
        {
            var now = _dateTimeProvider.GetUtcNow();
            // Call the repository method to update the password and timestamp
            var updatePasswordResult = await _personRepository.UpdatePasswordAsync(personId, newHashedPassword, now);

            // Check for repository errors
            if (updatePasswordResult.IsFailure)
            {
                // Log detailed repository error
                _logger.LogError("Failed to update password for User ID {UserId} in repository. Error: {Error}", personId, updatePasswordResult.ErrorMessage);
                // Simple failure message
                return Result.Failure($"Failed to update password.");
            }
            // Password updated successfully
            return Result.Success();
        }

        /// <summary>
        /// Deletes the used password reset token from the repository. Best effort.
        /// </summary>
        /// <param name="tokenId">The ID of the token record to delete.</param>
        /// <param name="personId">The associated Person ID (for logging).</param>
        private async Task DeletePasswordResetTokenAsync(int tokenId, int personId)
        {
            // Attempt to delete the token
            var deleteTokenResult = await _changePasswordRepository.Delete(tokenId);

            // Log only if deletion fails, as it's a cleanup step
            if (deleteTokenResult.IsFailure)
            {
                // Log a warning: password changed but token cleanup failed. Manual cleanup might be needed.
                _logger.LogWarning("Failed to delete used password reset token {TokenId} for User ID {UserId} after successful password reset. Error: {Error}",
                                   tokenId, personId, deleteTokenResult.ErrorMessage);
            }
        }

        #endregion

        #region Email Helpers

        /// <summary>
        /// Sends the password reset email containing the token. Best effort.
        /// </summary>
        /// <param name="person">The PersonEntity of the user requesting the reset.</param>
        /// <param name="resetToken">The generated password reset token.</param>
        private async Task SendPasswordResetEmailAsync(PersonEntity person, string resetToken)
        {
            try
            {
                // Generate the email body
                string emailBody = _emailService.GeneratePasswordResetTokenBody(person.FirstName, resetToken);
                // Send the email
                await _emailService.SendEmailAsync(person.Email, "Your Password Reset Code", emailBody);
                _logger.LogInformation("Password reset email successfully sent to {Email} for User ID {UserId}.", person.Email, person.IdPerson);
            }
            catch (Exception ex)
            {
                // Log failure to send email but do not fail the RequestPasswordReset process
                _logger.LogError(ex, "Failed to send password reset email to {Email} for User ID {UserId}, although token was generated.", person.Email, person.IdPerson);
            }
        }

        /// <summary>
        /// Sends a confirmation email after a successful password change. Best effort.
        /// </summary>
        /// <param name="personId">The ID of the user whose password was changed.</param>
        private async Task SendPasswordChangeConfirmationEmailAsync(int personId)
        {
            // Retrieve minimal user details needed for the email (e.g., email, first name)
            var personDetailsResult = await _personRepository.GetById(personId);

            if (personDetailsResult.IsSuccess)
            {
                var person = personDetailsResult.Value;
                try
                {
                    // Generate the confirmation body
                    string emailBody = _emailService.GeneratePasswordChangeConfirmationBody(person.FirstName);
                    // Send the confirmation email
                    await _emailService.SendEmailAsync(person.Email, "Password Changed Successfully", emailBody);
                    _logger.LogInformation("Password change confirmation email sent to {Email} for User ID {UserId}", person.Email, personId);
                }
                catch (Exception ex)
                {
                    // Log failure to send confirmation, but the password reset itself was successful
                    _logger.LogWarning(ex, "Failed to send password change confirmation email to {Email} for User ID {UserId}.", person.Email, personId);
                }
            }
            else
            {
                // Log if user details couldn't be retrieved for sending the email
                _logger.LogWarning("Could not retrieve user details for User ID {UserId} to send password change confirmation email. Error: {Error}", personId, personDetailsResult.ErrorMessage);
            }
        }

        #endregion
    }
}
