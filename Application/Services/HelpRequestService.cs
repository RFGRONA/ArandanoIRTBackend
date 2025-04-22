using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Application.Interfaces.IServices;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Application.Services
{
    /// <summary>
    /// Implements services for handling user help requests.
    /// </summary>
    public class HelpRequestService : IHelpRequestService
    {
        private readonly ICropRepository _cropRepository;
        private readonly IPersonRepository _personRepository;
        private readonly IEmailService _emailService;
        private readonly ILogger<HelpRequestService> _logger;

        // Constructor for dependency injection
        public HelpRequestService(
            ICropRepository cropRepository,
            IPersonRepository personRepository,
            IEmailService emailService,
            ILogger<HelpRequestService> logger)
        {
            _cropRepository = cropRepository ?? throw new ArgumentNullException(nameof(cropRepository));
            _personRepository = personRepository ?? throw new ArgumentNullException(nameof(personRepository));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<Result> SendUnauthenticatedHelpAsync(HelpRequestDto request)
        {
            // Check for null request before validation helper
            if (request == null)
            {
                _logger.LogWarning("{ServiceMethod} called with a null request object.", nameof(SendUnauthenticatedHelpAsync));
                return Result.Failure("Help request data cannot be null.");
            }

            // 1. Validate Input DTO (using helper)
            var inputValidation = ValidateHelpRequestInput(request);
            if (inputValidation.IsFailure) return inputValidation; // Returns Result

            _logger.LogInformation("Processing help request from {RequesterEmail} regarding crop '{CropName}'.", request.Email, request.CropName);

            // 2. Find Crop and its Administrator (using helper)
            // CropName is guaranteed non-null here due to validation success
            var adminDetailsResult = await FindCropAdministratorAsync(request.CropName!);
            // adminDetailsResult is Result<PersonEntity>
            if (adminDetailsResult.IsFailure) return Result.Failure(adminDetailsResult.ErrorMessage);
            var administrator = adminDetailsResult.Value;

            // 3. Generate and Send Email (using helper)
            var emailResult = await GenerateAndSendHelpRequestEmailAsync(request, administrator);
            if (emailResult.IsFailure) return emailResult; // Returns Result

            _logger.LogInformation("Help request email successfully sent to administrator {AdminEmail} for crop '{CropName}'.", administrator.Email, request.CropName!);
            return Result.Success();
        }

        // --- Private Helper Methods ---

        /// <summary>
        /// Validates the input HelpRequestDto.
        /// </summary>
        /// <param name="request">The request DTO to validate.</param>
        /// <returns>A success or failure result.</returns>
        private Result ValidateHelpRequestInput(HelpRequestDto request)
        {
            // Basic validation (consider using FluentValidation or DataAnnotations on DTO)
            // Null check for request itself is done in the public method caller.
            if (string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Subject) ||
                string.IsNullOrWhiteSpace(request.CropName) ||
                string.IsNullOrWhiteSpace(request.Message))
            {
                _logger.LogWarning("Help request validation failed due to missing required fields.");
                return Result.Failure("All fields (Name, Email, Subject, Crop Name, Message) are required.");
            }
            // Could add email format validation here too using EmailValidatorUtility if needed.
            // var emailValidation = EmailValidatorUtility.ValidateEmail(request.Email);
            // if (emailValidation.IsFailure) return Result.Failure(emailValidation.ErrorMessage);

            return Result.Success();
        }

        /// <summary>
        /// Finds the specified crop and retrieves its administrator details.
        /// </summary>
        /// <param name="cropName">The name of the crop.</param>
        /// <returns>Result containing the administrator's PersonEntity on success.</returns>
        private async Task<Result<PersonEntity>> FindCropAdministratorAsync(string cropName)
        {
            // Find the crop by name
            var cropResult = await _cropRepository.GetByNameAsync(cropName);
            if (cropResult.IsFailure)
            {
                _logger.LogWarning("Help request failed: Crop '{CropName}' not found. Error: {Error}", cropName, cropResult.ErrorMessage);
                // Provide a user-friendly message
                return Result<PersonEntity>.Failure($"The specified crop '{cropName}' could not be found.");
            }
            var crop = cropResult.Value;

            // Check if the crop has an assigned administrator
            if (!crop.AdminUserId.HasValue)
            {
                _logger.LogError("Help request cannot proceed: Crop '{CropName}' (ID: {CropId}) does not have an assigned administrator.", crop.NameCrop, crop.IdCrop);
                // Indicate data integrity issue
                return Result<PersonEntity>.Failure("Could not process the request because the crop does not have an assigned administrator.");
            }

            // Find the administrator person entity by ID
            var adminResult = await _personRepository.GetById(crop.AdminUserId.Value);
            if (adminResult.IsFailure)
            {
                _logger.LogError("Help request failed: Could not find administrator with ID {AdminId} for crop '{CropName}'. Error: {Error}", crop.AdminUserId.Value, crop.NameCrop, adminResult.ErrorMessage);
                // Indicate internal error or data issue
                return Result<PersonEntity>.Failure("Could not process the request due to an internal error finding the administrator.");
            }

            // Return the administrator entity successfully
            return adminResult; 
        }

        /// <summary>
        /// Generates the help request email body and sends it to the administrator.
        /// </summary>
        /// <param name="request">The original help request DTO.</param>
        /// <param name="administrator">The PersonEntity of the administrator to email.</param>
        /// <returns>A success or failure result.</returns>
        private async Task<Result> GenerateAndSendHelpRequestEmailAsync(HelpRequestDto request, PersonEntity administrator)
        {
            string emailBody;
            try
            {
                // Generate the email body using the email service
                emailBody = _emailService.GenerateHelpRequestBody(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate help request email body for request from {RequesterEmail}.", request.Email);
                // Return failure if body generation fails
                return Result.Failure("Failed to prepare the help request email.");
            }

            // Define the email subject
            // Subject guaranteed non-null by validation
            string emailSubject = $"[Ayuda ArandanoIRT] Nueva Solicitud: {request.Subject!}";

            // Send the email using the email service
            var emailResult = await _emailService.SendEmailAsync(administrator.Email, emailSubject, emailBody);

            // Check if email sending failed
            if (emailResult.IsFailure)
            {
                _logger.LogError("Failed to send help request email to administrator {AdminEmail}. Error: {Error}", administrator.Email, emailResult.ErrorMessage);
                // Return failure, including the error from the email service
                return Result.Failure($"Failed to send the help request email. Error: {emailResult.ErrorMessage}");
            }

            // Email sent successfully
            return Result.Success();
        }
    }
}