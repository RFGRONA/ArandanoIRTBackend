using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Application.Interfaces.Services;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Infrastructure.Interfaces.IServices;
using ArandanoIRT_Backend.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ArandanoIRT_Backend.UI.Controllers
{
    /// <summary>
    /// Controlador para gestionar la autenticación, registro y recuperación de cuentas de usuario.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly CookiesService _cookiesService;
        private readonly ITokenService _tokenService;
        private readonly ICaptchaService _captchaService;
        private readonly IWebHostEnvironment _environment;
        private readonly IRequestContextAccessor _requestContextAccessor;
        private readonly IRsaService _rsaService;
        private readonly ILogger<AuthController> _logger;

        /// <summary>
        /// Inicializa una nueva instancia del controlador <see cref="AuthController"/>.
        /// </summary>
        /// <param name="authService">Servicio para la lógica de autenticación.</param>
        /// <param name="cookiesService">Servicio para manejar cookies de autenticación.</param>
        /// <param name="tokenService">Servicio para generar y validar tokens.</param>
        /// <param name="captchaService">Servicio para verificar CAPTCHA.</param>
        /// <param name="environment">Información sobre el entorno de ejecución.</param>
        /// <param name="requestContextAccessor">Acceso a la información del contexto de la solicitud.</param>
        /// <param name="rsaService">Servicio para operaciones criptográficas RSA.</param>
        /// <param name="logger">Servicio de logging.</param>
        public AuthController(
            IAuthService authService,
            CookiesService cookiesService,
            ITokenService tokenService,
            ICaptchaService captchaService,
            IWebHostEnvironment environment,
            IRequestContextAccessor requestContextAccessor,
            IRsaService rsaService,
            ILogger<AuthController> logger)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _cookiesService = cookiesService ?? throw new ArgumentNullException(nameof(cookiesService));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _captchaService = captchaService ?? throw new ArgumentNullException(nameof(captchaService));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _requestContextAccessor = requestContextAccessor ?? throw new ArgumentNullException(nameof(requestContextAccessor));
            _rsaService = rsaService ?? throw new ArgumentNullException(nameof(rsaService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Registra un nuevo usuario administrador junto con su primer cultivo.
        /// </summary>
        /// <param name="request">Datos del administrador y del cultivo a registrar.</param>
        /// <returns>Respuesta 204 No Content si el registro es exitoso.</returns>
        /// <response code="204">Registro de administrador y cultivo exitoso.</response>
        /// <response code="400">Datos inválidos (ej. email existente, datos incompletos, captcha inválido).</response>
        [HttpPost("register-admin")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RegisterAdmin([FromBody] RegisterAdminRequestDto request)
        {
            if (request == null) { return BadRequest("Request cannot be null."); }

            var captchaValidationResult = await ValidateCaptchaIfNeededAsync(request.CaptchaToken);
            if (captchaValidationResult != null) return captchaValidationResult;

            if (!ModelState.IsValid) { return BadRequest(ModelState); }

            var result = await _authService.RegisterAdminAsync(request);

            return result.IsSuccess
                ? NoContent()
                : BadRequest(result.ErrorMessage);
        }

        /// <summary>
        /// Registra un nuevo usuario regular utilizando un código de invitación.
        /// </summary>
        /// <param name="request">Datos del usuario y código de invitación.</param>
        /// <returns>Respuesta 204 No Content si el registro es exitoso.</returns>
        /// <response code="204">Registro de usuario exitoso.</response>
        /// <response code="400">Datos inválidos (ej. email existente, código inválido/expirado, captcha inválido).</response>
        [HttpPost("register-user")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RegisterUser([FromBody] RegisterUserRequestDto request)
        {
            if (request == null) { return BadRequest("Request cannot be null."); }

            var captchaValidationResult = await ValidateCaptchaIfNeededAsync(request.CaptchaToken);
            if (captchaValidationResult != null) return captchaValidationResult;

            if (!ModelState.IsValid) { return BadRequest(ModelState); }

            var result = await _authService.RegisterUserAsync(request);

            return result.IsSuccess
                ? NoContent()
                : BadRequest(result.ErrorMessage);
        }

        /// <summary>
        /// Inicia sesión para un usuario existente usando email y contraseña (cifrada).
        /// </summary>
        /// <remarks>
        /// La contraseña debe ser enviada cifrada con la clave pública RSA obtenida del endpoint `GET /public-key`.
        /// En caso de éxito, se establecen cookies HttpOnly (`jwt`, `refreshToken`, `stayLoggedIn`) y se devuelve información básica del usuario y el token de acceso JWT.
        /// </remarks>
        /// <param name="request">Credenciales de inicio de sesión (email, contraseña cifrada, recordarme, captcha).</param>
        /// <returns>Datos del usuario y token de acceso.</returns>
        /// <response code="200">Inicio de sesión exitoso. Devuelve LoginResponseDto.</response>
        /// <response code="400">Solicitud inválida (ej. faltan datos, captcha inválido).</response>
        /// <response code="401">No autorizado (email o contraseña incorrectos).</response>
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            if (request == null) { return BadRequest("Request cannot be null."); }

            var captchaValidationResult = await ValidateCaptchaIfNeededAsync(request.CaptchaToken);
            if (captchaValidationResult != null) return captchaValidationResult;

            if (!ModelState.IsValid) { return BadRequest(ModelState); }

            string ipAddress = _requestContextAccessor.GetIpAddress();
            string userAgent = _requestContextAccessor.GetUserAgent();
            string deviceInfo = _requestContextAccessor.GetFormattedDeviceInfo();

            var result = await _authService.LoginAsync(request, ipAddress, userAgent, deviceInfo); // Pasamos deviceInfo

            if (result.IsFailure) { return Unauthorized(result.ErrorMessage); }

            var loginPayload = result.Value;

            // --- Set Cookies ---
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
                _logger.LogError(ex, "Failed to set auth cookies for User ID {UserId} after successful login.", loginPayload.LoginDetails.IdUser);
                // Considerar devolver 500 si las cookies son absolutamente críticas
            }

            return Ok(loginPayload.LoginDetails);
        }

        /// <summary>
        /// Refresca el token de acceso JWT utilizando un token de refresco válido.
        /// </summary>
        /// <remarks>
        /// El token de refresco se busca prioritariamente en la cookie HttpOnly 'refreshToken'.
        /// Si no se encuentra en la cookie, se busca en el encabezado HTTP 'X-Refresh-Token'.
        /// Implementa la rotación de tokens: genera un nuevo par (acceso y refresco) y revoca el antiguo.
        /// Si el token original vino de una cookie, actualiza las cookies 'jwt' y 'refreshToken'.
        /// Si vino de un header, devuelve los nuevos tokens en el cuerpo de la respuesta SIN establecer cookies.
        /// </remarks>
        /// <returns>Nuevos tokens de acceso y refresco con la expiración del de acceso. Vea <see cref="TokenResponseDto"/>.</returns>
        /// <response code="200">Tokens refrescados exitosamente. Devuelve TokenResponseDto.</response>
        /// <response code="400">Solicitud inválida (ej. falta token en cookie y header).</response>
        /// <response code="401">No autorizado (token de refresco inválido, expirado o revocado).</response>
        [HttpPost("refresh-token")]
        [AllowAnonymous] // Sigue siendo anónimo, la validación está en el token mismo
        [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshToken()
        {
            string? refreshTokenValue = null;
            string tokenSource = "unknown"; // To know where it came from

            // 1. Try to get it from the Cookie (preferred for web)
            refreshTokenValue = _cookiesService.GetRefreshToken(HttpContext);
            if (!string.IsNullOrEmpty(refreshTokenValue))
            {
                tokenSource = "cookie";
                _logger.LogDebug("Refresh token found in cookie.");
            }
            else
            {
                // 2. If not in cookie, try to get it from Header (for native clients)
                if (Request.Headers.TryGetValue("X-Refresh-Token", out var headerValues))
                {
                    refreshTokenValue = headerValues.FirstOrDefault();
                    if (!string.IsNullOrEmpty(refreshTokenValue))
                    {
                        tokenSource = "header";
                        _logger.LogDebug("Refresh token found in X-Refresh-Token header.");
                    }
                }
            }

            // 3. Validate if a token was found
            if (string.IsNullOrEmpty(refreshTokenValue))
            {
                _logger.LogWarning("Refresh token not found in cookie or X-Refresh-Token header.");
                return BadRequest("Refresh token not found.");
            }

            // --- Proceed with the refresh logic ---
            string ipAddress = _requestContextAccessor.GetIpAddress();
            string userAgent = _requestContextAccessor.GetUserAgent();
            string deviceInfo = _requestContextAccessor.GetFormattedDeviceInfo();

            var result = await _authService.RefreshTokenAsync(refreshTokenValue, ipAddress, userAgent, deviceInfo);

            if (result.IsFailure)
            {
                // If refresh fails, always attempt to remove cookies (just in case)
                // This is important for web clients that might have invalid cookies.
                _cookiesService.RemoveCookies(HttpContext);
                _logger.LogWarning("Refresh token validation failed. Source: {TokenSource}. Error: {Error}", tokenSource, result.ErrorMessage);
                return Unauthorized(result.ErrorMessage); // 401 Unauthorized
            }

            // --- Successful Refresh ---
            var tokenResponse = result.Value; // Contains the new AccessToken and RefreshToken strings

            // --- Handle Response Depending on the Token's Origin ---
            if (tokenSource == "cookie")
            {
                // If the token came from a cookie (web client), update the cookies.
                try
                {
                    _logger.LogInformation("Renewing auth cookies for request originating from cookie.");
                    _cookiesService.RenewAuthCookies(HttpContext, tokenResponse.AccessToken, tokenResponse.RefreshToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to set/renew auth cookies after successful token refresh (source: cookie).");
                    // What to do here? The token was refreshed, but the web client won't have the new cookies.
                    // We could return a 500 error, or return OK but the web client will have issues.
                    // Returning 500 seems more appropriate if the cookies are essential.
                    return StatusCode(StatusCodes.Status500InternalServerError, "Token refreshed successfully, but failed to set cookies.");
                }
            }
            else // tokenSource == "header" (or "unknown", although it shouldn't reach here if the token was validated)
            {
                // If the token came from a header (native client), DO NOT set cookies.
                // The native client will read the new tokens from the response body.
                _logger.LogInformation("Token refresh successful for request originating from header. Returning tokens in body.");
            }

            // Always return the DTO with the new tokens in the response body.
            // The web client can ignore it if it wants, the native client needs it.
            return Ok(tokenResponse);
        }


        /// <summary>
        /// Inicia el proceso de reseteo de contraseña para el email proporcionado.
        /// </summary>
        /// <remarks>
        /// Envía un código de reseteo al correo electrónico si este existe en el sistema.
        /// Por seguridad, siempre devuelve una respuesta exitosa (200 OK) para evitar la enumeración de correos electrónicos.
        /// </remarks>
        /// <param name="request">Contiene el email del usuario y el token de captcha.</param>
        /// <returns>Respuesta 200 OK con un mensaje genérico.</returns>
        /// <response code="200">Solicitud procesada. Se enviará un correo si el email existe.</response>
        [HttpPost("request-password-reset")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RequestPasswordReset([FromBody] ForgotPasswordRequestDto request)
        {
            if (request == null) { return BadRequest("Request cannot be null."); }

            var captchaValidationResult = await ValidateCaptchaIfNeededAsync(request.CaptchaToken);
            if (captchaValidationResult != null)
            {
                return Ok("If your email address exists in our system, you will receive a password reset code.");
            }

            if (!ModelState.IsValid)
            {
                var validationErrors = ModelState.Where(kvp => kvp.Value?.Errors.Count > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToList());
                _logger.LogWarning("Invalid model state for password reset request (Email: {Email}). Errors: {@ValidationErrors}. Returning OK to prevent enumeration.", request?.Email, validationErrors);
                return Ok("If your email address exists in our system, you will receive a password reset code.");
            }

            var result = await _authService.RequestPasswordResetAsync(request);

            _logger.LogInformation("Processed password reset request for email: {Email}. Returning OK.", request.Email);
            return Ok("If your email address exists in our system, you will receive a password reset code.");
        }

        /// <summary>
        /// Establece una nueva contraseña utilizando un código de reseteo válido.
        /// </summary>
        /// <remarks>
        /// La nueva contraseña (`newPassword`, `confirmPassword`) debe venir cifrada con RSA.
        /// </remarks>
        /// <param name="request">Contiene el código de reseteo (`changeCode`) y la nueva contraseña cifrada (y su confirmación).</param>
        /// <returns>Respuesta 204 No Content si el cambio es exitoso.</returns>
        /// <response code="204">Contraseña actualizada exitosamente.</response>
        /// <response code="400">Solicitud inválida (ej. código inválido/expirado, contraseñas no coinciden, contraseña no cumple políticas).</response>
        [HttpPost("reset-password")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPassword([FromBody] ChangePasswordRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.ResetPasswordAsync(request);

            return result.IsSuccess
                ? NoContent()
                : BadRequest(result.ErrorMessage);
        }

        /// <summary>
        /// Cierra la sesión del usuario actual.
        /// </summary>
        /// <remarks>
        /// Revoca el token de refresco asociado a la sesión actual en el servidor y elimina las cookies de autenticación del cliente.
        /// Requiere que el usuario esté autenticado.
        /// </remarks>
        /// <returns>Respuesta 204 No Content.</returns>
        /// <response code="204">Sesión cerrada exitosamente.</response>
        /// <response code="401">No autorizado (el usuario no está autenticado).</response>
        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout()
        {
            var refreshTokenValue = _cookiesService.GetRefreshToken(HttpContext);
            if (!string.IsNullOrEmpty(refreshTokenValue))
            {
                string ipAddress = _requestContextAccessor.GetIpAddress();
                var result = await _authService.LogoutAsync(refreshTokenValue, ipAddress);
                if (result.IsFailure)
                {
                    _logger.LogWarning("Failed to revoke refresh token during logout. IP: {IPAddress}. Error: {Error}", ipAddress, result.ErrorMessage);
                }
            }
            else
            {
                _logger.LogWarning("Logout called but no refresh token cookie found.");
            }

            _cookiesService.RemoveCookies(HttpContext);

            return NoContent();
        }

        /// <summary>
        /// Cierra todas las sesiones activas del usuario actual.
        /// </summary>
        /// <remarks>
        /// Revoca todos los tokens de refresco asociados a la misma sesión de inicio de sesión original del token actual.
        /// Elimina las cookies de autenticación del cliente actual. Requiere que el usuario esté autenticado.
        /// </remarks>
        /// <returns>Respuesta 204 No Content.</returns>
        /// <response code="204">Todas las sesiones cerradas exitosamente.</response>
        /// <response code="401">No autorizado (el usuario no está autenticado o token inválido).</response>
        /// <response code="400">No se pudo identificar la sesión a cerrar.</response>
        [HttpPost("logout-everywhere")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> LogoutEverywhere()
        {
            var refreshTokenValue = _cookiesService.GetRefreshToken(HttpContext);
            if (string.IsNullOrEmpty(refreshTokenValue))
            {
                _logger.LogWarning("LogoutEverywhere called but no refresh token cookie found.");
                return Unauthorized("No active session found to perform logout everywhere.");
            }

            string ipAddress = _requestContextAccessor.GetIpAddress();
            var sessionIdResult = await _tokenService.GetSessionIdFromTokenAsync(refreshTokenValue);

            if (sessionIdResult.IsFailure)
            {
                _logger.LogWarning("Could not determine session ID for logout-everywhere using provided token. Error: {Error}", sessionIdResult.ErrorMessage);
                _cookiesService.RemoveCookies(HttpContext);
                // Consider returning 400 Bad Request instead of 401 if the token exists but session ID fails
                return BadRequest($"Unable to identify session: {sessionIdResult.ErrorMessage}");
            }

            long sessionId = sessionIdResult.Value;
            var result = await _authService.LogoutEverywhereAsync(sessionId, ipAddress);

            if (result.IsFailure)
            {
                _logger.LogWarning("Server-side revocation failed during logout-everywhere for session {SessionId}. Error: {Error}", sessionId, result.ErrorMessage);
            }
            else
            {
                _logger.LogInformation("Successfully revoked all tokens for session ID {SessionId}.", sessionId);
            }

            _cookiesService.RemoveCookies(HttpContext);
            return NoContent();
        }

        /// <summary>
        /// Obtiene la clave pública RSA en formato PEM para el cifrado del lado del cliente.
        /// </summary>
        /// <returns>La cadena de la clave pública PEM.</returns>
        /// <response code="200">Clave pública obtenida exitosamente.</response>
        /// <response code="500">Error interno al obtener la clave pública.</response>
        [HttpGet("public-key")]
        [AllowAnonymous]
        [Produces("text/plain")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        // [OutputCache(Duration = 3600)] // Considerar cachear si la clave no cambia
        public IActionResult GetPublicKey()
        {
            try
            {
                string publicKeyPem = _rsaService.GetPublicKeyPem();
                return Ok(publicKeyPem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve RSA public key.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Could not retrieve public key.");
            }
        }

        /// <summary>
        /// Endpoint de prueba para verificar si el usuario está autenticado. (Sólo para depuración/ejemplo).
        /// </summary>
        /// <remarks>
        /// Requiere un token de acceso JWT válido (enviado como Bearer token o mediante la cookie 'jwt').
        /// Devuelve información extraída del token.
        /// </remarks>
        /// <returns>Mensaje de éxito con información del usuario autenticado.</returns>
        /// <response code="200">Usuario autenticado.</response>
        /// <response code="401">Usuario no autenticado.</response>
        [HttpGet("check-auth")]
        [Authorize]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult CheckAuth()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = User.FindFirstValue(ClaimTypes.Role);
            var cropId = User.FindFirstValue("CropId"); // Custom claim

            _logger.LogInformation("Authorization check successful for User ID: {UserId}, Role: {Role}, CropId: {CropId}", userId, role, cropId ?? "N/A");

            return Ok(new { Message = $"Authenticated successfully as User ID: {userId}, Role: {role}, CropId: {cropId ?? "N/A"}" });
        }

        // --- Private Helper for CAPTCHA Validation ---
        /// <summary>
        /// Valida el token CAPTCHA si no se está en entorno de Desarrollo.
        /// </summary>
        /// <param name="captchaToken">El token recibido del cliente.</param>
        /// <returns>Un `IActionResult` (BadRequest) si la validación falla, o `null` si la validación es exitosa o se omite.</returns>
        private async Task<IActionResult?> ValidateCaptchaIfNeededAsync(string? captchaToken)
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogInformation("Skipping CAPTCHA validation in Development environment.");
                return null;
            }
            if (string.IsNullOrWhiteSpace(captchaToken))
            {
                _logger.LogWarning("CAPTCHA token is missing in Production/Staging environment.");
                return BadRequest("CAPTCHA validation failed: Token is missing.");
            }
            var captchaResult = await _captchaService.VerifyCaptchaAsync(captchaToken);
            if (captchaResult.IsFailure)
            {
                _logger.LogWarning("CAPTCHA verification failed. Error: {Error}", captchaResult.ErrorMessage);
                return BadRequest("CAPTCHA validation failed.");
            }
            _logger.LogInformation("CAPTCHA verification successful.");
            return null;
        }
    }
}