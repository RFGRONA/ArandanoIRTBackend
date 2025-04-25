using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Application.Utilities
{
    /// <summary>
    /// Implements shared utility functions for authentication services.
    /// </summary>
    public class AuthUtilities : IAuthUtilities
    {
        private readonly IPersonRepository _personRepository;
        private readonly IRsaService _rsaService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ILogger<AuthUtilities> _logger;

        // Constructor for dependency injection
        public AuthUtilities(
            IPersonRepository personRepository,
            IRsaService rsaService,
            IPasswordHasher passwordHasher,
            ILogger<AuthUtilities> logger)
        {
            _personRepository = personRepository ?? throw new ArgumentNullException(nameof(personRepository));
            _rsaService = rsaService ?? throw new ArgumentNullException(nameof(rsaService));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<Result<PersonEntity>> FindUserByEmailAsync(string email)
        {
            // Validate input email format
            var emailValidation = await EmailValidatorUtility.ValidateEmailAsync(email);
            if(emailValidation.IsFailure) return Result<PersonEntity>.Failure($"Invalid email format: {email}");

            // Attempt to retrieve user by email from the repository
            var personResult = await _personRepository.GetByEmailAsync(email);

            // Check if user was found
            if (personResult.IsFailure)
            {
                // Log lookup failure (level Debug or Info might be appropriate)
                _logger.LogDebug("User lookup failed for email: {Email}. Reason: {Error}", email, personResult.ErrorMessage);
                // Return specific failure, let caller decide context (login fail vs register proceed)
                return Result<PersonEntity>.Failure("User not found.");
            }

            // User found, return success result
            return personResult;
        }

        /// <inheritdoc/>
        public Result<string> ProcessPassword(string encryptedPassword, string operationContextForLog)
        {
            string decryptedPassword;
            try
            {
                // Decrypt using RSA service
                decryptedPassword = _rsaService.Decrypt(encryptedPassword);
            }
            catch (Exception ex)
            {
                // Log decryption error with context
                _logger.LogError(ex, "Failed to decrypt password during {OperationContext}", operationContextForLog);
                // Simple failure message
                return Result<string>.Failure("Invalid password format or data.");
            }

            // Validate password policy (e.g., length)
            // Move policy details to configuration?
            const int minPasswordLength = 8;
            if (string.IsNullOrWhiteSpace(decryptedPassword) || decryptedPassword.Length < minPasswordLength)
            {
                // Log policy violation with context
                _logger.LogWarning("Password validation failed during {OperationContext}: Does not meet minimum length ({MinLength})", operationContextForLog, minPasswordLength);
                // Simple failure message
                return Result<string>.Failure($"Password must be at least {minPasswordLength} characters long.");
            }

            // Hash the valid, decrypted password
            var hashedPassword = _passwordHasher.Hash(decryptedPassword);

            // Return the hashed password successfully
            return Result<string>.Success(hashedPassword);
        }
    }
}
