using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace ArandanoIRT_Backend.UI.Controllers
{
    /// <summary>
    /// Base controller for API endpoints providing shared functionality.
    /// </summary>
    [ApiController] // Applies common API conventions
    public abstract class BaseApiController : ControllerBase
    {
        // Shared services injected into the base controller
        private readonly ICaptchaService _captchaService;
        private readonly IWebHostEnvironment _environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseApiController"/> class.
        /// </summary>
        /// <param name="captchaService">Service for CAPTCHA verification.</param>
        /// <param name="environment">Provides information about the web hosting environment.</param>
        /// <exception cref="ArgumentNullException">Thrown if captchaService or environment is null.</exception>
        protected BaseApiController( // Constructor is protected
            ICaptchaService captchaService,
            IWebHostEnvironment environment)
        {
            _captchaService = captchaService ?? throw new ArgumentNullException(nameof(captchaService));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        /// <summary>
        /// Validates the CAPTCHA token if the application is not running in the Development environment.
        /// </summary>
        /// <param name="captchaToken">The CAPTCHA token received from the client.</param>
        /// <param name="logger">The specific logger instance from the derived controller for contextual logging.</param>
        /// <returns>
        /// A Task resulting in an <see cref="IActionResult"/> (specifically <see cref="BadRequestObjectResult"/>)
        /// if CAPTCHA validation fails or is required but missing.
        /// Returns null if validation is successful or skipped (in Development environment).
        /// </returns>
        protected async Task<IActionResult?> ValidateCaptchaIfNeededAsync(string? captchaToken, ILogger logger)
        {
            // Skip validation if in Development environment
            if (_environment.IsDevelopment())
            {
                // Log that validation is skipped
                logger.LogInformation("Skipping CAPTCHA validation in Development environment.");
                // Return null indicating validation passed/skipped
                return null;
            }

            // Check if token is provided in non-Development environments
            if (string.IsNullOrWhiteSpace(captchaToken))
            {
                // Log the missing token warning
                logger.LogWarning("CAPTCHA token is missing in Production/Staging environment.");
                // Return BadRequest with a user-friendly message
                return BadRequest("CAPTCHA validation failed: Token is missing.");
            }

            // Verify the provided token using the captcha service
            var captchaResult = await _captchaService.VerifyCaptchaAsync(captchaToken);

            // Check if verification failed
            if (captchaResult.IsFailure)
            {
                // Log the specific failure reason from the service
                logger.LogWarning("CAPTCHA verification failed. Error: {Error}", captchaResult.ErrorMessage);
                // Return BadRequest with a generic user-friendly message
                return BadRequest("CAPTCHA validation failed.");
            }

            // Log successful verification
            logger.LogInformation("CAPTCHA verification successful.");
            // Return null indicating validation passed
            return null;
        }
    }
}