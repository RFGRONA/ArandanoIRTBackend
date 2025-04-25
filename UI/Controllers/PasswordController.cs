using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Application.Interfaces.IServices;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArandanoIRT_Backend.UI.Controllers
{
    /// <summary>
    /// Handles endpoints related to user password management, like reset requests.
    /// </summary>
    [Route("api/password")]
    public class PasswordController : BaseApiController
    {
        private readonly IAuthPasswordService _passwordService;
        private readonly ILogger<PasswordController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PasswordController"/> class.
        /// </summary>
        /// <param name="passwordService">The application service for password logic.</param>
        /// <param name="captchaService">The service for CAPTCHA validation (passed to base).</param>
        /// <param name="environment">The web hosting environment (passed to base).</param>
        /// <param name="logger">The logger for this controller.</param>
        public PasswordController(
            IAuthPasswordService passwordService,
            ICaptchaService captchaService,
            IWebHostEnvironment environment,
            ILogger<PasswordController> logger)
            : base(captchaService, environment) 
        {
            _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Initiates the password reset process for the provided email address.
        /// </summary>
        /// <remarks>
        /// Sends a reset code to the email if it exists in the system.
        /// For security, always returns a successful response (200 OK) to prevent email enumeration.
        /// </remarks>
        /// <param name="request">Contains the user's email and optional CAPTCHA token.</param>
        /// <returns>A generic success message.</returns>
        /// <response code="200">Request processed. An email will be sent if the address exists.</response>
        /// <response code="400">Bad request (body is null, ideally should still return 200 for enumeration protection if possible).</response> // Added 400 for null body case
        [HttpPost("request-reset")] 
        [AllowAnonymous]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)] 
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)] 
        public async Task<IActionResult> RequestPasswordReset([FromBody] ForgotPasswordRequestDto request)
        {
            _logger.LogInformation("Processing password reset request for email: {Email}", request?.Email ?? "N/A");

            // Handle null request body explicitly, this is a case where 400 is okay before enumeration protection logic.
            if (request == null)
            {
                _logger.LogWarning("RequestPasswordReset endpoint called with null request body.");
                return BadRequest("Request body cannot be null.");
            }

            // Validate CAPTCHA using the base helper
            var captchaValidationResult = await ValidateCaptchaIfNeededAsync(request.CaptchaToken, _logger);
            if (captchaValidationResult != null)
            {
                // Even if CAPTCHA fails, return OK for enumeration protection
                _logger.LogWarning("CAPTCHA validation failed for password reset request (Email: {Email}), but returning OK.", request.Email);
                return Ok("If your email address exists in our system, you will receive a password reset code.");
            }

            // Optional: Explicit ModelState check
            if (!ModelState.IsValid)
            {
                // Log validation errors but return OK for enumeration protection
                _logger.LogWarning("Invalid model state for password reset request (Email: {Email}). Errors: {@ValidationErrors}. Returning OK to prevent enumeration.", request.Email, ModelState.Values.SelectMany(v => v.Errors));
                return Ok("If your email address exists in our system, you will receive a password reset code.");
            }

            // Call the application service
            // The service itself also implements enumeration protection by always returning Success internally
            // unless there's a critical failure generating the token.
            var result = await _passwordService.RequestPasswordResetAsync(request);

            // Log potential internal failures from the service (like token generation failure)
            if (result.IsFailure)
            {
                _logger.LogError("Internal error during RequestPasswordResetAsync for email {Email}. Error: {Error}. Still returning OK to client.", request.Email, result.ErrorMessage);
            }
            else
            {
                _logger.LogInformation("Successfully processed password reset request for email: {Email}. Returning OK.", request.Email);
            }


            // Always return OK to the client regardless of whether the email existed or an email was sent.
            return Ok("If your email address exists in our system, you will receive a password reset code.");
        }

        /// <summary>
        /// Sets a new password using a valid reset code.
        /// </summary>
        /// <remarks>
        /// Expects the new password (`newPassword`, `confirmPassword`) to be RSA encrypted.
        /// </remarks>
        /// <param name="request">Contains the reset code (`changeCode`) and the new encrypted password (and its confirmation).</param>
        /// <returns>An IActionResult indicating success (204 No Content) or failure (400 Bad Request).</returns>
        /// <response code="204">Password updated successfully.</response>
        /// <response code="400">Invalid request (e.g., invalid/expired code, passwords don't match, password policy violation).</response>
        [HttpPost("reset")] 
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPassword([FromBody] ChangePasswordRequestDto request)
        {
            _logger.LogInformation("Attempting to reset password using provided code.");

            // Check for null request body
            if (request == null)
            {
                _logger.LogWarning("ResetPassword endpoint called with null request body.");
                return BadRequest("Request body cannot be null.");
            }

            // Optional: Explicit ModelState check
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for ResetPassword request. Errors: {@ModelState}", ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            // Call the application service
            var result = await _passwordService.ResetPasswordAsync(request);

            // Handle the service result
            if (result.IsSuccess)
            {
                _logger.LogInformation("Password reset successful for the provided code.");
                // Return 204 No Content for success
                return NoContent();
            }
            else
            {
                _logger.LogWarning("Password reset failed for the provided code. Reason: {Error}", result.ErrorMessage);
                // Return 400 Bad Request with the specific error message
                return BadRequest(result.ErrorMessage);
            }
        }
    }
}
