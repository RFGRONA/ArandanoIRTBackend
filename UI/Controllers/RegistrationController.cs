using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Application.Interfaces.IServices;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArandanoIRT_Backend.UI.Controllers
{
    /// <summary>
    /// Handles endpoints related to user and administrator registration.
    /// </summary>
    [Route("api/registration")] 
    public class RegistrationController : BaseApiController
    {
        private readonly IAuthRegistrationService _registrationService;
        private readonly ILogger<RegistrationController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistrationController"/> class.
        /// </summary>
        /// <param name="registrationService">The application service for registration logic.</param>
        /// <param name="captchaService">The service for CAPTCHA validation (passed to base).</param>
        /// <param name="environment">The web hosting environment (passed to base).</param>
        /// <param name="logger">The logger for this controller.</param>
        public RegistrationController(
            IAuthRegistrationService registrationService,
            ICaptchaService captchaService,
            IWebHostEnvironment environment,
            ILogger<RegistrationController> logger)
            : base(captchaService, environment) // Pass shared dependencies to base constructor
        {
            _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Registers a new administrator user along with their initial crop.
        /// </summary>
        /// <param name="request">Data for the administrator and crop to be registered.</param>
        /// <returns>An IActionResult indicating success (204 No Content) or failure (400 Bad Request).</returns>
        /// <response code="204">Admin and crop registration successful.</response>
        /// <response code="400">Invalid data (e.g., existing email, incomplete data, invalid CAPTCHA).</response>
        [HttpPost("admin")] 
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)] // Return error message string on bad request
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)] // For ModelState errors
        public async Task<IActionResult> RegisterAdmin([FromBody] RegisterAdminRequestDto request)
        {
            // Log entry point
            _logger.LogInformation("Attempting to register admin with email {Email}", request?.AdminInfo?.Email ?? "N/A");

            // Check for null request body
            if (request == null)
            {
                _logger.LogWarning("RegisterAdmin endpoint called with null request body.");
                return BadRequest("Request body cannot be null.");
            }

            // Validate CAPTCHA using the base controller's helper
            var captchaValidationResult = await ValidateCaptchaIfNeededAsync(request.CaptchaToken, _logger);
            if (captchaValidationResult != null) return captchaValidationResult; // Returns BadRequest if failed

            // Optional: Explicit ModelState check (ApiController does this, but explicit check allows custom logging)
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for RegisterAdmin request. Errors: {@ModelState}", ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            // Call the application service
            var result = await _registrationService.RegisterAdminAsync(request);

            // Handle the service result
            if (result.IsSuccess)
            {
                _logger.LogInformation("Admin registration successful for email {Email}", request.AdminInfo?.Email ?? "N/A");
                // Return 204 No Content for successful creation/update without a body
                return NoContent();
            }
            else
            {
                _logger.LogWarning("Admin registration failed for email {Email}. Reason: {Error}", request.AdminInfo?.Email ?? "N/A", result.ErrorMessage);
                // Return 400 Bad Request with the specific error message from the service
                return BadRequest(result.ErrorMessage);
            }
        }

        /// <summary>
        /// Registers a new regular user using an invitation code.
        /// </summary>
        /// <param name="request">Data for the user and the invitation code.</param>
        /// <returns>An IActionResult indicating success (204 No Content) or failure (400 Bad Request).</returns>
        /// <response code="204">User registration successful.</response>
        /// <response code="400">Invalid data (e.g., existing email, invalid/expired code, invalid CAPTCHA).</response>
        [HttpPost("user")] 
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RegisterUser([FromBody] RegisterUserRequestDto request)
        {
            // Log entry point
            _logger.LogInformation("Attempting to register user with email {Email}", request?.UserInfo?.Email ?? "N/A");

            // Check for null request body
            if (request == null)
            {
                _logger.LogWarning("RegisterUser endpoint called with null request body.");
                return BadRequest("Request body cannot be null.");
            }

            // Validate CAPTCHA using the base controller's helper
            var captchaValidationResult = await ValidateCaptchaIfNeededAsync(request.CaptchaToken, _logger);
            if (captchaValidationResult != null) return captchaValidationResult; // Returns BadRequest if failed

            // Optional: Explicit ModelState check
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for RegisterUser request. Errors: {@ModelState}", ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            // Call the application service
            var result = await _registrationService.RegisterUserAsync(request);

            // Handle the service result
            if (result.IsSuccess)
            {
                _logger.LogInformation("User registration successful for email {Email}", request.UserInfo?.Email ?? "N/A");
                // Return 204 No Content for successful creation
                return NoContent();
            }
            else
            {
                _logger.LogWarning("User registration failed for email {Email}. Reason: {Error}", request.UserInfo?.Email ?? "N/A", result.ErrorMessage);
                // Return 400 Bad Request with the specific error message
                return BadRequest(result.ErrorMessage);
            }
        }
    }
}