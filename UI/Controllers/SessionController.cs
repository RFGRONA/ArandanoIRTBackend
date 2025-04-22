using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Application.Interfaces.IServices;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Infrastructure.Interfaces.IServices;
using ArandanoIRT_Backend.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;

namespace ArandanoIRT_Backend.UI.Controllers
{
    /// <summary>
    /// Handles endpoints related to user session management, including login,
    /// token refresh, and logout operations.
    /// </summary>
    [Route("api/session")]
    public class SessionController : BaseApiController
    {
        private readonly IAuthSessionService _sessionService;
        private readonly CookiesService _cookiesService;
        private readonly ITokenService _tokenService; 
        private readonly IRequestContextAccessor _requestContextAccessor;
        private readonly ILogger<SessionController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionController"/> class.
        /// </summary>
        /// <param name="sessionService">The application service for session logic.</param>
        /// <param name="cookiesService">Service for handling authentication cookies.</param>
        /// <param name="tokenService">Service for token operations (e.g., reading session ID).</param>
        /// <param name="requestContextAccessor">Accessor for request context information (IP, UserAgent).</param>
        /// <param name="captchaService">The service for CAPTCHA validation (passed to base).</param>
        /// <param name="environment">The web hosting environment (passed to base).</param>
        /// <param name="logger">The logger for this controller.</param>
        public SessionController(
            IAuthSessionService sessionService,
            CookiesService cookiesService,
            ITokenService tokenService,
            IRequestContextAccessor requestContextAccessor,
            ICaptchaService captchaService,
            IWebHostEnvironment environment,
            ILogger<SessionController> logger)
            : base(captchaService, environment) // Pass shared dependencies to base constructor
        {
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
            _cookiesService = cookiesService ?? throw new ArgumentNullException(nameof(cookiesService));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _requestContextAccessor = requestContextAccessor ?? throw new ArgumentNullException(nameof(requestContextAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Logs in an existing user using email and (encrypted) password.
        /// </summary>
        /// <remarks>
        /// Expects password encrypted with the server's public RSA key.
        /// On success, sets HttpOnly cookies (`jwt`, `refreshToken`, `stayLoggedIn`) and returns basic user info + JWT.
        /// </remarks>
        /// <param name="request">Login credentials (email, encrypted password, rememberMe, captcha).</param>
        /// <returns>Basic user details and access token.</returns>
        /// <response code="200">Login successful. Returns LoginResponseDto.</response>
        /// <response code="400">Invalid request (e.g., missing data, invalid CAPTCHA).</response>
        /// <response code="401">Unauthorized (invalid email or password).</response>
        /// <response code="500">Internal error (e.g., failed to set cookies).</response>
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            _logger.LogInformation("Login attempt for email {Email}", request?.Email ?? "N/A");

            if (request == null) { return BadRequest("Request cannot be null."); }

            // Validate CAPTCHA using the base helper
            var captchaValidationResult = await ValidateCaptchaIfNeededAsync(request.CaptchaToken, _logger);
            if (captchaValidationResult != null) return captchaValidationResult;

            // Optional: Explicit ModelState check
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for Login request. Errors: {@ModelState}", ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            // Get request context details
            string ipAddress = _requestContextAccessor.GetIpAddress();
            string userAgent = _requestContextAccessor.GetUserAgent();
            string deviceInfo = _requestContextAccessor.GetFormattedDeviceInfo();

            // Call the session service to perform login
            var result = await _sessionService.LoginAsync(request, ipAddress, userAgent, deviceInfo);

            // Handle failed login (Unauthorized)
            if (result.IsFailure)
            {
                // Log failure reason from service
                _logger.LogWarning("Login failed for email {Email}. Reason: {Error}", request.Email, result.ErrorMessage);
                // Return 401 Unauthorized with service message
                return Unauthorized(result.ErrorMessage);
            }

            // Login successful, payload contains user details, JWT, and Refresh Token string
            var loginPayload = result.Value;

            // Attempt to set authentication cookies
            try
            {
                _cookiesService.SetAuthCookies(
                    HttpContext,
                    loginPayload.LoginDetails.Token, // Access Token (JWT)
                    loginPayload.RefreshToken,       // The opaque Refresh Token string
                    request.RememberMe               // Persistence flag
                );
                _logger.LogInformation("Auth cookies set successfully for User ID {UserId}", loginPayload.LoginDetails.IdUser);
            }
            catch (Exception ex)
            {
                // Log critical error if cookies cannot be set
                _logger.LogError(ex, "Failed to set auth cookies for User ID {UserId} after successful login.", loginPayload.LoginDetails.IdUser);
                // Return 500 as the client state might be inconsistent
                return StatusCode(StatusCodes.Status500InternalServerError, "Login successful, but failed to set session cookies.");
            }

            // Return OK with the user details (client reads JWT from this or cookie)
            return Ok(loginPayload.LoginDetails);
        }


        /// <summary>
        /// Refreshes the JWT access token using a valid refresh token.
        /// </summary>
        /// <remarks>
        /// Reads the refresh token from the 'refreshToken' HttpOnly cookie (preferred) or 'X-Refresh-Token' header.
        /// Implements token rotation (new access/refresh pair generated, old refresh token revoked).
        /// If the original token came from a cookie, cookies are updated. Otherwise, new tokens are returned in the body.
        /// </remarks>
        /// <returns>New access and refresh tokens.</returns>
        /// <response code="200">Tokens refreshed successfully. Returns TokenResponseDto.</response>
        /// <response code="400">Invalid request (e.g., refresh token missing).</response>
        /// <response code="401">Unauthorized (refresh token invalid, expired, or revoked).</response>
        /// <response code="500">Internal error (e.g., failed to renew cookies).</response>
        [HttpPost("refresh-token")]
        [AllowAnonymous] 
        [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RefreshToken()
        {
            string? refreshTokenValue = null;
            string tokenSource = "unknown";

            // 1. Try reading from Cookie
            refreshTokenValue = _cookiesService.GetRefreshToken(HttpContext);
            if (!string.IsNullOrEmpty(refreshTokenValue))
            {
                tokenSource = "cookie";
                _logger.LogDebug("Refresh token found in cookie for refresh request.");
            }
            else
            {
                // 2. Try reading from Header if not in cookie
                if (Request.Headers.TryGetValue("X-Refresh-Token", out StringValues headerValues))
                {
                    refreshTokenValue = headerValues.FirstOrDefault();
                    if (!string.IsNullOrEmpty(refreshTokenValue))
                    {
                        tokenSource = "header";
                        _logger.LogDebug("Refresh token found in X-Refresh-Token header for refresh request.");
                    }
                }
            }

            // 3. Validate token presence
            if (string.IsNullOrEmpty(refreshTokenValue))
            {
                _logger.LogWarning("Refresh token endpoint called but token not found in cookie or header.");
                return BadRequest("Refresh token not found.");
            }

            // --- Proceed with refresh ---
            string ipAddress = _requestContextAccessor.GetIpAddress();
            string userAgent = _requestContextAccessor.GetUserAgent();
            string deviceInfo = _requestContextAccessor.GetFormattedDeviceInfo();

            // Call the session service to refresh tokens
            var result = await _sessionService.RefreshTokenAsync(refreshTokenValue, ipAddress, userAgent, deviceInfo);

            // Handle refresh failure (Unauthorized)
            if (result.IsFailure)
            {
                // Always attempt to remove cookies if refresh fails, regardless of source
                _logger.LogWarning("Refresh token validation failed (Source: {TokenSource}). Attempting to clear auth cookies. Error: {Error}", tokenSource, result.ErrorMessage);
                _cookiesService.RemoveCookies(HttpContext);
                return Unauthorized(result.ErrorMessage); // 401 Unauthorized
            }

            // --- Successful Refresh ---
            var tokenResponse = result.Value; // Contains new AccessToken and RefreshToken strings

            // Conditionally update cookies if the original token came from a cookie
            if (tokenSource == "cookie")
            {
                try
                {
                    _logger.LogInformation("Renewing auth cookies after successful token refresh (source: cookie).");
                    // Use Renew which likely removes old and sets new ones
                    _cookiesService.RenewAuthCookies(HttpContext, tokenResponse.AccessToken, tokenResponse.RefreshToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to set/renew auth cookies after successful token refresh (source: cookie).");
                    // Return 500 if cookie renewal fails for web clients
                    return StatusCode(StatusCodes.Status500InternalServerError, "Token refreshed successfully, but failed to update session cookies.");
                }
            }
            else // tokenSource == "header" or "unknown" (though validation should prevent unknown reaching here)
            {
                _logger.LogInformation("Token refresh successful (Source: {TokenSource}). Returning new tokens in response body.", tokenSource);
                // Do not set cookies if request came from header
            }

            // Always return the new tokens in the response body
            return Ok(tokenResponse);
        }


        /// <summary>
        /// Logs out the current user session.
        /// </summary>
        /// <remarks>
        /// Revokes the current refresh token on the server and removes auth cookies from the client.
        /// Requires the user to be authenticated.
        /// </remarks>
        /// <returns>An IActionResult indicating success (204 No Content).</returns>
        /// <response code="204">Logout successful.</response>
        /// <response code="401">Unauthorized (user not authenticated).</response>
        [HttpPost("logout")] 
        [Authorize] 
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)] 
        public async Task<IActionResult> Logout()
        {
            _logger.LogInformation("Processing logout request for user {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "N/A");

            // Attempt to get the refresh token from the cookie to revoke it
            var refreshTokenValue = _cookiesService.GetRefreshToken(HttpContext);
            if (!string.IsNullOrEmpty(refreshTokenValue))
            {
                string ipAddress = _requestContextAccessor.GetIpAddress();
                // Call the session service to revoke the specific token (best effort)
                var result = await _sessionService.LogoutAsync(refreshTokenValue, ipAddress);
                if (result.IsFailure)
                {
                    // Log if server-side revocation failed, but proceed with client-side cleanup
                    _logger.LogWarning("Failed to revoke refresh token on server during logout. IP: {IPAddress}. Error: {Error}", ipAddress, result.ErrorMessage);
                }
                else
                {
                    _logger.LogInformation("Refresh token revoked successfully on server during logout.");
                }
            }
            else
            {
                // Log if no token was found to revoke (might happen if cookie expired/removed prematurely)
                _logger.LogWarning("Logout called but no refresh token cookie found to revoke on server.");
            }

            // Always remove cookies on the client-side regardless of server revocation status
            _logger.LogDebug("Removing auth cookies from client.");
            _cookiesService.RemoveCookies(HttpContext);

            // Return 204 No Content indicating successful logout process
            return NoContent();
        }

        /// <summary>
        /// Logs out all active sessions for the current user.
        /// </summary>
        /// <remarks>
        /// Revokes all refresh tokens associated with the user's original session ID (obtained from the current token).
        /// Removes auth cookies from the current client. Requires authentication.
        /// </remarks>
        /// <returns>An IActionResult indicating success (204 No Content).</returns>
        /// <response code="204">Logout everywhere successful.</response>
        /// <response code="401">Unauthorized (user not authenticated or token invalid).</response>
        /// <response code="400">Bad Request (e.g., unable to determine session ID from token).</response>
        [HttpPost("logout-everywhere")] 
        [Authorize] 
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> LogoutEverywhere()
        {
            _logger.LogInformation("Processing logout-everywhere request for user {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "N/A");

            // Need the current refresh token to identify the session ID
            var refreshTokenValue = _cookiesService.GetRefreshToken(HttpContext);
            if (string.IsNullOrEmpty(refreshTokenValue))
            {
                // This shouldn't happen if [Authorize] works, but check anyway
                _logger.LogWarning("LogoutEverywhere called but no refresh token cookie found.");
                return Unauthorized("No active session found to perform logout everywhere.");
            }

            // Get IP address for logging/service call
            string ipAddress = _requestContextAccessor.GetIpAddress();

            // Use ITokenService to extract the Session ID from the refresh token
            var sessionIdResult = await _tokenService.GetSessionIdFromTokenAsync(refreshTokenValue);

            if (sessionIdResult.IsFailure)
            {
                // Failed to get session ID - token might be invalid or malformed
                _logger.LogWarning("Could not determine session ID for logout-everywhere using provided token. Error: {Error}", sessionIdResult.ErrorMessage);
                // Remove local cookies anyway
                _cookiesService.RemoveCookies(HttpContext);
                // Return 400 Bad Request as the provided token seems problematic
                return BadRequest($"Unable to identify session from token: {sessionIdResult.ErrorMessage}");
            }
            long sessionId = sessionIdResult.Value;
            _logger.LogInformation("Identified Session ID {SessionId} for logout-everywhere request.", sessionId);


            // Call the session service to revoke all tokens for this session ID (best effort)
            var result = await _sessionService.LogoutEverywhereAsync(sessionId, ipAddress);
            if (result.IsFailure)
            {
                // Log if server-side revocation failed, but proceed with client-side cleanup
                _logger.LogWarning("Server-side revocation failed during logout-everywhere for session {SessionId}. Error: {Error}", sessionId, result.ErrorMessage);
            }
            else
            {
                _logger.LogInformation("Successfully initiated revocation for all tokens for session ID {SessionId}.", sessionId);
            }

            // Always remove cookies on the current client-side
            _logger.LogDebug("Removing auth cookies from client after logout-everywhere request.");
            _cookiesService.RemoveCookies(HttpContext);

            // Return 204 No Content
            return NoContent();
        }

        /// <summary>
        /// Example endpoint to check authentication status and retrieve claims. (For testing/debugging).
        /// </summary>
        /// <returns>Authenticated user's claims information.</returns>
        /// <response code="204">User is authenticated..</response>
        /// <response code="401">User is not authenticated.</response>
        [HttpGet("check-auth")] 
        [Authorize] 
        [ProducesResponseType(typeof(object), StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult CheckAuth()
        {
            // Access claims from the authenticated user principal
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = User.FindFirstValue(ClaimTypes.Role);
            var cropId = User.FindFirstValue("CropId"); // Custom claim example
            var sessionId = User.FindFirstValue("sid"); // Session ID claim example

            _logger.LogInformation("Authorization check successful via check-auth endpoint for User ID: {UserId}, Role: {Role}, CropId: {CropId}, SessionId: {SessionId}",
                userId ?? "N/A",
                role ?? "N/A",
                cropId ?? "N/A",
                sessionId ?? "N/A");

            // Return claims info
            return NoContent();
        }
    }
}
