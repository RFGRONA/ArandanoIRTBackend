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
    /// Implements services for registering new administrators and regular users.
    /// </summary>
    public class AuthRegistrationService : IAuthRegistrationService
    {
        // Repositories
        private readonly IPersonRepository _personRepository;
        private readonly ICropRepository _cropRepository;
        private readonly ICropInvitationRepository _cropInvitationRepository;

        // Services & Utilities
        private readonly IAuthUtilities _authUtilities;
        private readonly IEmailService _emailService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<AuthRegistrationService> _logger;

        // Constructor for dependency injection
        public AuthRegistrationService(
            IPersonRepository personRepository,
            ICropRepository cropRepository,
            ICropInvitationRepository cropInvitationRepository,
            IAuthUtilities authUtilities,
            IEmailService emailService,
            IDateTimeProvider dateTimeProvider,
            ILogger<AuthRegistrationService> logger)
        {
            _personRepository = personRepository ?? throw new ArgumentNullException(nameof(personRepository));
            _cropRepository = cropRepository ?? throw new ArgumentNullException(nameof(cropRepository));
            _cropInvitationRepository = cropInvitationRepository ?? throw new ArgumentNullException(nameof(cropInvitationRepository));
            _authUtilities = authUtilities ?? throw new ArgumentNullException(nameof(authUtilities));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<Result> RegisterAdminAsync(RegisterAdminRequestDto request)
        {
            // Log the beginning of the operation
            _logger.LogInformation("Attempting admin registration for email: {Email}", request?.AdminInfo?.Email);

            // Check for null request object
            if (request == null)
            {
                _logger.LogWarning("{ServiceMethod} called with null request object.", nameof(RegisterAdminAsync));
                // Simple failure message for the user
                return Result.Failure("Invalid registration data provided.");
            }

            // --- Step 1: Validate Input ---
            var validationResult = await ValidateAdminRegistrationInputAsync(request);
            if (validationResult.IsFailure) return validationResult; // Return Result directly

            // --- Step 2: Process Password ---
            // AdminInfo is non-null due to successful validation
            var passwordResult = _authUtilities.ProcessPassword(request.AdminInfo!.Password, nameof(RegisterAdminAsync));
            if (passwordResult.IsFailure) return passwordResult;
            string hashedPassword = passwordResult.Value;

            // --- Step 3: Check Existing User ---
            var userExistsResult = await _authUtilities.FindUserByEmailAsync(request.AdminInfo!.Email);
            if (userExistsResult.IsFailure) return userExistsResult; // Return Result directly

            var now = _dateTimeProvider.GetUtcNow();

            // --- Transaction Start Recommended Here ---
            // Use Unit of Work or DbContext transaction

            // --- Step 4: Create Crop ---
            var createCropResult = await CreateCropForAdminAsync(request, now);
            if (createCropResult.IsFailure) return createCropResult; // Return Result directly
            var savedCrop = createCropResult.Value;

            // --- Step 5: Create Person (Admin) ---
            var createPersonResult = await CreateAdminPersonAsync(request, hashedPassword, savedCrop.IdCrop, now);
            if (createPersonResult.IsFailure)
            {
                // Log detailed error including crop ID for potential manual cleanup if no transaction
                _logger.LogError("Admin registration failed at Person creation for email {Email}. Crop {CropId} might be orphaned.", request.AdminInfo!.Email, savedCrop.IdCrop);
                // Return simple failure message
                return createPersonResult;
            }
            var savedPerson = createPersonResult.Value;

            // --- Step 6: Update Crop with Admin ID ---
            var updateCropResult = await UpdateCropWithAdminIdAsync(savedCrop, savedPerson.IdPerson);
            if (updateCropResult.IsFailure)
            {
                // Log detailed warning but proceed, registration mostly successful.
                _logger.LogWarning("Admin registration succeeded for {Email}, but failed to update AdminUserId ({AdminId}) on Crop {CropId}. Error: {Error}",
                                 savedPerson.Email, savedPerson.IdPerson, savedCrop.IdCrop, updateCropResult.ErrorMessage);
                // Do not return failure here unless linking admin is absolutely critical
            }

            // --- Transaction Commit Recommended Here ---

            // --- Step 7: Send Welcome Email (Best effort) ---
            await SendWelcomeEmailAsync(savedPerson);

            // Log successful completion
            _logger.LogInformation("Successfully registered new admin {AdminId} for crop {CropId} with email {Email}", savedPerson.IdPerson, savedCrop.IdCrop, savedPerson.Email);
            // Return success
            return Result.Success();
        }

        /// <inheritdoc/>
        public async Task<Result> RegisterUserAsync(RegisterUserRequestDto request)
        {
            // Log the beginning of the operation
            _logger.LogInformation("Attempting user registration for email: {Email}", request?.UserInfo?.Email);

            // Check for null request object
            if (request == null)
            {
                _logger.LogWarning("{ServiceMethod} called with null request object.", nameof(RegisterUserAsync));
                // Simple failure message for the user
                return Result.Failure("Invalid registration data provided.");
            }

            // --- Step 1: Validate Input & Invitation Code ---
            var validationResult = await ValidateUserRegistrationInputAndCodeAsync(request);
            // If validationResult fails, it's Result<CropInvitationEntity>.Failure
            if (validationResult.IsFailure) return Result.Failure(validationResult.ErrorMessage); // Convert to Result.Failure
            var validInvitation = validationResult.Value;

            // --- Step 2: Process Password ---
            // UserInfo is non-null due to successful validation
            var passwordResult = _authUtilities.ProcessPassword(request.UserInfo!.Password, nameof(RegisterUserAsync));
            if (passwordResult.IsFailure) return passwordResult;
            string hashedPassword = passwordResult.Value;

            // --- Step 3: Check Existing User ---
            var userExistsResult = await _authUtilities.FindUserByEmailAsync(request.UserInfo!.Email);
            if (userExistsResult.IsFailure) return userExistsResult; // Return Result directly

            var now = _dateTimeProvider.GetUtcNow();

            // --- Transaction Start Recommended Here ---

            // --- Step 4: Create Person (User) ---
            var createPersonResult = await CreateRegularUserAsync(request, hashedPassword, validInvitation.CropId, now);
            if (createPersonResult.IsFailure) return createPersonResult; // Return Result directly
            var savedPerson = createPersonResult.Value;

            // --- Step 5: Mark Invitation as Used ---
            var markUsedResult = await MarkInvitationAsUsedAsync(validInvitation, savedPerson.IdPerson, request.UserInfo!.AuthCode);
            if (markUsedResult.IsFailure)
            {
                // Log detailed warning but proceed, user creation succeeded.
                _logger.LogWarning("User {Email} registered, but failed to mark invitation code {AuthCode} (ID: {InvId}) as used. Error: {Error}",
                                 savedPerson.Email, request.UserInfo!.AuthCode, validInvitation.IdCropInvitation, markUsedResult.ErrorMessage);
                // Do not return failure here unless marking invitation is absolutely critical
            }

            // --- Transaction Commit Recommended Here ---

            // --- Step 6: Send Welcome Email (Best effort) ---
            await SendWelcomeEmailAsync(savedPerson);

            // Log successful completion
            _logger.LogInformation("Successfully registered new user {UserId} for crop {CropId} with email {Email}", savedPerson.IdPerson, validInvitation.CropId, savedPerson.Email);
            // Return success
            return Result.Success();
        }


        // --- Private Helper Methods ---

        #region Input Validation Helpers

        /// <summary>
        /// Validates the input for admin registration.
        /// </summary>
        /// <param name="request">The registration request DTO.</param>
        /// <returns>A success or failure result.</returns>
        private async Task<Result> ValidateAdminRegistrationInputAsync(RegisterAdminRequestDto request)
        {
            // Check nested DTOs are present
            // Null check for 'request' itself is done in the public method caller.
            if (request.AdminInfo == null || request.CropInfo == null)
            {
                _logger.LogWarning("Admin registration validation failed: AdminInfo or CropInfo is null.");
                return Result.Failure("Required registration information is missing.");
            }

            // Validate email format
            var emailValidation = await EmailValidatorUtility.ValidateEmailAsync(request.AdminInfo.Email);
            if (emailValidation.IsFailure)
            {
                // Log detailed validation error
                _logger.LogWarning("Invalid email format during admin registration: {Email}. Reason: {Reason}", request.AdminInfo.Email, emailValidation.ErrorMessage);
                // Simple message for user
                return Result.Failure($"Invalid email format provided.");
            }
            // Add other basic validations for Name, CropName etc. if needed
            if (string.IsNullOrWhiteSpace(request.AdminInfo.FirstName) || string.IsNullOrWhiteSpace(request.AdminInfo.LastName))
            {
                _logger.LogWarning("Admin registration validation failed: First name or last name is missing.");
                return Result.Failure("First name and last name are required.");
            }
            if (string.IsNullOrWhiteSpace(request.CropInfo.NameCrop))
            {
                _logger.LogWarning("Admin registration validation failed: Crop name is missing.");
                return Result.Failure("Crop name is required.");
            }

            // All checks passed
            return Result.Success();
        }

        /// <summary>
        /// Validates input for user registration and the invitation code.
        /// </summary>
        /// <param name="request">The registration request DTO.</param>
        /// <returns>Result containing the valid CropInvitationEntity on success, or Failure.</returns>
        private async Task<Result<CropInvitationEntity>> ValidateUserRegistrationInputAndCodeAsync(RegisterUserRequestDto request)
        {
            // Check nested DTO and auth code presence
            // Null check for 'request' itself is done in the public method caller.
            if (request.UserInfo == null || string.IsNullOrWhiteSpace(request.UserInfo.AuthCode))
            {
                _logger.LogWarning("User registration validation failed: UserInfo is null or AuthCode is missing.");
                // Simple message for user
                return Result<CropInvitationEntity>.Failure("Required registration information or invitation code is missing.");
            }

            // Validate email format
            var emailValidation = await EmailValidatorUtility.ValidateEmailAsync(request.UserInfo.Email);
            if (emailValidation.IsFailure)
            {
                // Log detailed validation error
                _logger.LogWarning("Invalid email format during user registration: {Email}. Reason: {Reason}", request.UserInfo.Email, emailValidation.ErrorMessage);
                // Simple message for user
                return Result<CropInvitationEntity>.Failure($"Invalid email format provided.");
            }

            // Validate other required fields
            if (string.IsNullOrWhiteSpace(request.UserInfo.FirstName) || string.IsNullOrWhiteSpace(request.UserInfo.LastName))
            {
                _logger.LogWarning("User registration validation failed: First name or last name is missing.");
                return Result<CropInvitationEntity>.Failure("First name and last name are required.");
            }

            // Validate the invitation code's existence, expiry, and status
            var invitationResult = await _cropInvitationRepository.GetActiveByCodeAsync(request.UserInfo.AuthCode);
            if (invitationResult.IsFailure)
            {
                // Log detailed reason for invalid code
                _logger.LogWarning("Invalid or inactive invitation code used: {AuthCode}. Reason: {Error}", request.UserInfo.AuthCode, invitationResult.ErrorMessage);
                // Simple message for user
                return Result<CropInvitationEntity>.Failure($"Invitation code is invalid, expired, or already used.");
            }

            // All checks passed, return the valid invitation entity
            return invitationResult;
        }

        #endregion

        #region Entity Creation & Update Helpers

        /// <summary>
        /// Creates and saves the CropEntity for admin registration.
        /// </summary>
        /// <param name="request">The registration request DTO.</param>
        /// <param name="now">The current UTC timestamp.</param>
        /// <returns>Result containing the saved CropEntity on success, or Failure.</returns>
        private async Task<Result<CropEntity>> CreateCropForAdminAsync(RegisterAdminRequestDto request, DateTime now)
        {
            CropEntity? cropEntity;
            try
            {
                // Map DTO to entity (CropInfo guaranteed non-null by validation)
                cropEntity = request.ToCropEntity(now);
            }
            catch (ArgumentException argEx) // Catch potential mapping errors
            {
                _logger.LogWarning(argEx, "Failed to map Crop DTO to Entity during admin registration.");
                // Simple failure message
                return Result<CropEntity>.Failure("Invalid crop data provided.");
            }

            // Create the crop in the repository
            var createCropResult = await _cropRepository.Create(cropEntity);
            if (createCropResult.IsFailure)
            {
                // Log repository error
                _logger.LogError("Failed to create crop in repository during admin registration for email {Email}. Error: {Error}", request.AdminInfo!.Email, createCropResult.ErrorMessage);
                // Simple failure message
                return Result<CropEntity>.Failure("Failed to save crop information.");
            }
            // Return the successful result with the saved entity (includes ID)
            return createCropResult;
        }

        /// <summary>
        /// Creates and saves the PersonEntity for an administrator.
        /// </summary>
        /// <param name="request">The registration request DTO.</param>
        /// <param name="hashedPassword">The pre-hashed password.</param>
        /// <param name="cropId">The ID of the newly created crop.</param>
        /// <param name="now">The current UTC timestamp.</param>
        /// <returns>Result containing the saved PersonEntity on success, or Failure.</returns>
        private async Task<Result<PersonEntity>> CreateAdminPersonAsync(RegisterAdminRequestDto request, string hashedPassword, int cropId, DateTime now)
        {
            PersonEntity? personEntity;
            try
            {
                // Map DTO to entity (AdminInfo guaranteed non-null by validation)
                // Mapping sets IsAdmin = true
                personEntity = request.ToAdminPersonEntity(now);

                // Set properties not handled by basic mapping
                // Avoid reflection. Add methods to entity or improve mapping.
                typeof(PersonEntity).GetProperty("Password")?.SetValue(personEntity, hashedPassword, null);
                typeof(PersonEntity).GetProperty("CropId")?.SetValue(personEntity, cropId, null);

            }
            catch (Exception ex) // Catch mapping or reflection errors
            {
                _logger.LogWarning(ex, "Failed to map or set properties on Person entity during admin registration.");
                // Simple failure message
                return Result<PersonEntity>.Failure($"Invalid user data provided.");
            }

            // Create the person in the repository
            var createPersonResult = await _personRepository.Create(personEntity);
            if (createPersonResult.IsFailure)
            {
                // Log repository error
                _logger.LogError("Failed to create person in repository during admin registration for email {Email}. Error: {Error}", request.AdminInfo!.Email, createPersonResult.ErrorMessage);
                // Simple failure message
                return Result<PersonEntity>.Failure($"Failed to save user information.");
            }
            // Return the successful result with the saved entity (includes ID)
            return createPersonResult;
        }

        /// <summary>
        /// Updates the previously created crop with the administrator's user ID.
        /// </summary>
        /// <param name="crop">The CropEntity that was just saved.</param>
        /// <param name="adminId">The ID of the newly created administrator person.</param>
        /// <returns>A success or failure result.</returns>
        private async Task<Result> UpdateCropWithAdminIdAsync(CropEntity crop, int adminId)
        {
            try
            {
                // Set properties on the existing entity instance
                // Avoid reflection. Add methods to entity or improve mapping/repository update method.
                typeof(CropEntity).GetProperty("AdminUserId")?.SetValue(crop, adminId, null);
                typeof(CropEntity).GetProperty("UpdatedAt")?.SetValue(crop, _dateTimeProvider.GetUtcNow(), null);

                // Update the crop in the repository
                var updateCropResult = await _cropRepository.Update(crop);
                if (updateCropResult.IsFailure)
                {
                    // Log repository error (Caller will log context)
                    _logger.LogError("Failed repository update for AdminUserId ({AdminId}) on Crop ({CropId}). Error: {Error}",
                                     adminId, crop.IdCrop, updateCropResult.ErrorMessage);
                    // Simple failure message
                    return Result.Failure("Failed to link administrator to crop.");
                }
                // Update successful
                return Result.Success();
            }
            catch (Exception ex) // Catch reflection or other errors
            {
                _logger.LogWarning(ex, "Exception occurred while updating AdminUserId for CropId {CropId}.", crop.IdCrop);
                // Simple failure message
                return Result.Failure($"An unexpected error occurred while updating crop details.");
            }
        }

        /// <summary>
        /// Creates and saves the PersonEntity for a regular user.
        /// </summary>
        /// <param name="request">The registration request DTO.</param>
        /// <param name="hashedPassword">The pre-hashed password.</param>
        /// <param name="cropId">The ID of the crop from the invitation.</param>
        /// <param name="now">The current UTC timestamp.</param>
        /// <returns>Result containing the saved PersonEntity on success, or Failure.</returns>
        private async Task<Result<PersonEntity>> CreateRegularUserAsync(RegisterUserRequestDto request, string hashedPassword, int cropId, DateTime now)
        {
            PersonEntity? personEntity;
            try
            {
                // Map DTO to entity (UserInfo guaranteed non-null by validation)
                // Mapping sets IsAdmin = false and passes CropId
                personEntity = request.ToUserPersonEntity(now, cropId);

                // Set properties not handled by basic mapping
                // Avoid reflection. Add methods to entity or improve mapping.
                typeof(PersonEntity).GetProperty("Password")?.SetValue(personEntity, hashedPassword, null);

            }
            catch (Exception ex) // Catch mapping or reflection errors
            {
                _logger.LogWarning(ex, "Failed to map or set properties on Person entity during user registration.");
                // Simple failure message
                return Result<PersonEntity>.Failure($"Invalid user data provided.");
            }

            // Create the person in the repository
            var createPersonResult = await _personRepository.Create(personEntity);
            if (createPersonResult.IsFailure)
            {
                // Log repository error
                _logger.LogError("Failed to create person in repository during user registration for email {Email}. Error: {Error}", request.UserInfo!.Email, createPersonResult.ErrorMessage);
                // Simple failure message
                return Result<PersonEntity>.Failure($"Failed to save user information.");
            }
            // Return the successful result with the saved entity (includes ID)
            return createPersonResult;
        }

        /// <summary>
        /// Marks a crop invitation as used in the repository.
        /// </summary>
        /// <param name="invitation">The valid CropInvitationEntity.</param>
        /// <param name="userId">The ID of the user who used the invitation.</param>
        /// <param name="authCodeForLog">The invitation code string for logging.</param>
        /// <returns>A success or failure result.</returns>
        private async Task<Result> MarkInvitationAsUsedAsync(CropInvitationEntity invitation, int userId, string authCodeForLog)
        {
            try
            {
                // Call repository method to mark the invitation as used
                var markUsedResult = await _cropInvitationRepository.MarkAsUsedAsync(invitation.IdCropInvitation, userId);
                if (markUsedResult.IsFailure)
                {
                    // Log repository error (Caller will log context)
                    _logger.LogError("Failed repository call to mark invitation {InvId} as used by User {UserId}. Error: {Error}",
                                     invitation.IdCropInvitation, userId, markUsedResult.ErrorMessage);
                    // Simple failure message
                    return Result.Failure("Failed to update invitation status.");
                }
                // Log success
                _logger.LogInformation("Successfully marked invitation code {AuthCode} (ID: {InvitationId}) as used by User ID {UserId}.", authCodeForLog, invitation.IdCropInvitation, userId);
                // Return success
                return Result.Success();
            }
            catch (Exception ex) // Catch unexpected repository errors
            {
                _logger.LogError(ex, "Exception occurred while marking invitation {InvitationId} as used by User ID {UserId}.", invitation.IdCropInvitation, userId);
                // Simple failure message
                return Result.Failure($"An unexpected error occurred while updating the invitation.");
            }
        }

        #endregion

        #region Email Helper

        /// <summary>
        /// Sends a welcome email to the newly registered person. Best effort.
        /// </summary>
        /// <param name="person">The PersonEntity of the new user/admin.</param>
        private async Task SendWelcomeEmailAsync(PersonEntity person)
        {
            try
            {
                // Generate email body
                string emailBody = _emailService.GenerateWelcomeBody(person.FirstName);
                // Send email
                await _emailService.SendEmailAsync(person.Email, "Welcome to Arandano IRT!", emailBody);
            }
            catch (Exception ex)
            {
                // Log failure to send email but do not fail the registration process
                _logger.LogWarning(ex, "Failed to send welcome email to {Email} (User ID: {UserId}) after registration.", person.Email, person.IdPerson);
            }
        }

        #endregion
    }
}
