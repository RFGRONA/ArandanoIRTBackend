using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Application.Interfaces.IServices;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArandanoIRT_Backend.UI.Controllers
{
    /// <summary>
    /// Handles endpoints related to user help requests.
    /// </summary>
    [Route("api/help-requests")]
    public class HelpRequestsController : BaseApiController
    {
        private readonly IHelpRequestService _helpRequestService;
        private readonly ILogger<HelpRequestsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="HelpRequestsController"/> class.
        /// </summary>
        /// <param name="helpRequestService">The application service for help request logic.</param>
        /// <param name="captchaService">The service for CAPTCHA validation (passed to base).</param>
        /// <param name="environment">The web hosting environment (passed to base).</param>
        /// <param name="logger">The logger for this controller.</param>
        public HelpRequestsController(
            IHelpRequestService helpRequestService,
            ICaptchaService captchaService,
            IWebHostEnvironment environment,
            ILogger<HelpRequestsController> logger)
            : base(captchaService, environment) // Pass shared dependencies to base constructor
        {
            _helpRequestService = helpRequestService ?? throw new ArgumentNullException(nameof(helpRequestService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Submits a help request from an unauthenticated user.
        /// </summary>
        /// <remarks>
        /// Validates the specified crop name exists and sends an email to the crop's administrator.
        /// Requires CAPTCHA validation outside of Development environment.
        /// </remarks>
        /// <param name="request">Data for the help request (name, email, subject, crop name, message, optional captcha).</param>
        /// <returns>An IActionResult indicating success (204 No Content) or failure (400 Bad Request).</returns>
        /// <response code="204">Help request sent successfully.</response>
        /// <response code="400">Invalid request (e.g., missing data, crop not found, invalid CAPTCHA, email send failed).</response>
        [HttpPost("unauth")]
        [AllowAnonymous] 
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SendUnauthenticatedHelpRequest([FromBody] HelpRequestDto request)
        {
            // Log entry point
            _logger.LogInformation("Received unauthenticated help request from {Email} regarding crop {CropName}", request?.Email ?? "N/A", request?.CropName ?? "N/A");

            // Check for null request body
            if (request == null)
            {
                _logger.LogWarning("Unauthenticated help request endpoint called with null request body.");
                return BadRequest("Request body cannot be null.");
            }

            // Validate CAPTCHA using the base controller's helper
            var captchaValidationResult = await ValidateCaptchaIfNeededAsync(request.CaptchaToken, _logger);
            if (captchaValidationResult != null) return captchaValidationResult; // Returns BadRequest if failed

            // Optional: Explicit ModelState check
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for unauthenticated help request. Errors: {@ModelState}", ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            // Call the application service
            var result = await _helpRequestService.SendUnauthenticatedHelpAsync(request);

            // Handle the service result
            if (result.IsSuccess)
            {
                _logger.LogInformation("Unauthenticated help request from {Email} for crop {CropName} processed successfully.", request.Email, request.CropName);
                // Return 204 No Content for success
                return NoContent();
            }
            else
            {
                // Log the specific failure reason from the service
                _logger.LogWarning("Failed to process unauthenticated help request from {Email} for crop {CropName}. Reason: {Error}", request.Email, request.CropName, result.ErrorMessage);
                // Return 400 Bad Request with the error message
                return BadRequest(result.ErrorMessage);
            }
        }
    }
}