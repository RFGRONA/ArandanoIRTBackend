using AngleSharp.Io;
using ArandanoIRT_Backend.Application.DTOs.Device;
using ArandanoIRT_Backend.Application.Interfaces.IServices;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ArandanoIRT_Backend.UI.Controllers
{
    /// <summary>
    /// Handles API endpoints for direct communication with device hardware.
    /// Includes device activation, authentication, token refresh, and data reception.
    /// These endpoints primarily use the device-specific authentication scheme.
    /// </summary>
    [Route("api/device-api")] // A different route to distinguish from management endpoints
    [ApiController] // Indicates that this controller responds to web API requests
    // No [Authorize] at controller level, applied per endpoint as some are [AllowAnonymous]
    public class DeviceCommunicationController : ControllerBase
    {
        private readonly IDeviceCommunicationService _deviceCommunicationService;
        private readonly ILogger<DeviceCommunicationController> _logger;
        private readonly IDateTimeProvider _dateTimeProvider;

        // Assuming you have a service or way to get client context info (IP, UserAgent, DeviceInfo)
        // Example: private readonly IRequestContextAccessor _requestContextAccessor;


        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceCommunicationController"/> class.
        /// </summary>
        /// <param name="deviceCommunicationService">The application service for device communication logic.</param>
        /// <param name="logger">The logger for this controller.</param>
        /// <param name="dateTimeProvider">The service for getting the current UTC time.</param>
        /// <exception cref="ArgumentNullException">Thrown if dependencies are null.</exception>
        public DeviceCommunicationController(
            IDeviceCommunicationService deviceCommunicationService,
            ILogger<DeviceCommunicationController> logger, 
            IDateTimeProvider dateTimeProvider // Injected for UTC time handling
            /* , IRequestContextAccessor requestContextAccessor // Inject if using */
            )
        {
            _deviceCommunicationService = deviceCommunicationService ?? throw new ArgumentNullException(nameof(deviceCommunicationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            // _requestContextAccessor = requestContextAccessor ?? throw new ArgumentNullException(nameof(requestContextAccessor)); // Assign if using
        }

        /// <summary>
        /// Endpoint for devices to activate themselves using a registration code.
        /// Does NOT require authentication initially.
        /// </summary>
        /// <param name="requestDto">The DTO containing the device ID and activation code.</param>
        /// <returns>An IActionResult indicating success (200 OK with token details) or failure (400 Bad Request).</returns>
        /// <response code="200">Device activated successfully, tokens returned.</response>
        /// <response code="400">Invalid activation data (e.g., code invalid, expired, device ID mismatch).</response>
        [HttpPost("activate")] // POST to api/device-api/activate
        [AllowAnonymous] // Activation does not require prior authentication
        [ProducesResponseType(typeof(DeviceActivationResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ActivateDevice([FromBody] DeviceActivationRequestDto requestDto)
        {
            _logger.LogInformation("Received device activation request for Device ID: {DeviceId}", requestDto?.DeviceId);

            if (requestDto == null)
            {
                _logger.LogWarning("ActivateDevice called with null DTO.");
                return BadRequest("Activation data cannot be null.");
            }
            // DTO validation attributes handle basic checks on requestDto.
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for ActivateDevice request (Device ID: {DeviceId}). Errors: {@ModelState}", requestDto.DeviceId, ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            // Get client context info (IP, UserAgent, DeviceInfo)
            // Using HttpContext directly or via a service like IHttpContextAccessor or IRequestContextAccessor
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
            string userAgent = Request.Headers[HeaderNames.UserAgent].ToString() ?? "Unknown UserAgent";
            string deviceInfo = userAgent; // Simple device info from UserAgent for now, could use a parser utility


            // Call the application service
            // Service returns Result<DeviceActivationResponseDto>
            var result = await _deviceCommunicationService.ActivateDeviceAsync(requestDto, ipAddress, userAgent, deviceInfo);

            // Handle the service result
            if (result.IsSuccess)
            {
                _logger.LogInformation("Device activation successful for Device ID: {DeviceId}.", requestDto.DeviceId);
                // Return 200 OK with the response DTO containing tokens
                return Ok(result.Value);
            }
            else
            {
                // Service returns failure for invalid code, device ID mismatch, repo errors, etc.
                _logger.LogWarning("Device activation failed for Device ID {DeviceId}. Reason: {Error}", requestDto.DeviceId, result.ErrorMessage);
                return BadRequest(result.ErrorMessage); // Generic error message from service
            }
        }

        /// <summary>
        /// Endpoint for devices to authenticate and check backend status, and potentially receive new tokens.
        /// Requires authentication using the device scheme.
        /// The device sends its current Access Token in the Authorization header.
        /// </summary>
        /// <param name="requestDto">Contains the token (Access Token) for validation.</param>
        /// <returns>An IActionResult indicating success (200 OK with token details) or failure (401 Unauthorized).</returns>
        /// <response code="200">Authentication successful, token details returned.</response>
        /// <response code="400">Invalid data format.</response>
        /// <response code="401">Authentication failed (token invalid, expired, revoked, or device not Active).</response>
        [HttpPost("auth")] // POST to api/device-api/auth
        // Requires authentication using the custom device scheme
        [Authorize(AuthenticationSchemes = DeviceAuthenticationHandler.SchemeName)]
        [ProducesResponseType(typeof(DeviceAuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> AuthenticateDevice([FromBody] DeviceAuthRequestDto requestDto)
        {
            _logger.LogInformation("Received device authentication request from IP: {IPAddress}", HttpContext.Connection.RemoteIpAddress?.ToString());

            // Authentication has already happened by the DeviceAuthenticationHandler due to [Authorize].
            // The handler validated the token and set the User principal.

            // Get device ID from authenticated principal (set by DeviceAuthenticationHandler)
            var deviceIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(deviceIdClaim) || !int.TryParse(deviceIdClaim, out int deviceId))
            {
                _logger.LogError("Authenticated device ID claim missing or invalid during AuthenticateDevice endpoint execution.");
                // This indicates a serious issue with the authentication handler setup or claims.
                return Unauthorized("Could not identify authenticated device."); // Should not happen
            }

            if (requestDto == null)
            {
                _logger.LogWarning("AuthenticateDevice called with null DTO for Device ID: {DeviceId}.", deviceId);
                return BadRequest("Authentication data cannot be null.");
            }
            // DTO validation attributes handle basic checks on requestDto.
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for AuthenticateDevice request (Device ID: {DeviceId}). Errors: {@ModelState}", deviceId, ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            // Get client context info
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
            string userAgent = Request.Headers[HeaderNames.UserAgent].ToString() ?? "Unknown UserAgent";
            string deviceInfo = userAgent; // Simple device info


            // Call service to handle authentication/status check and potentially return new tokens.
            // The service will re-validate the token implicitly or explicitly depending on its logic.
            // Passing the token value from the DTO.
            // The service also checks device status (if Active).
            // Service returns Result<DeviceAuthResponseDto>
            var result = await _deviceCommunicationService.AuthenticateDeviceAsync(requestDto.Token, ipAddress, userAgent, deviceInfo);

            // Handle the service result
            if (result.IsSuccess)
            {
                _logger.LogInformation("Device authentication process successful for Device ID: {DeviceId}.", deviceId);
                // Return 200 OK with the response DTO containing token details
                return Ok(result.Value);
            }
            else
            {
                // Service returns failure if token invalid/inactive OR if device status is not Active.
                // Map service error messages to appropriate HTTP responses.
                if (result.ErrorMessage?.Contains("Invalid token", StringComparison.OrdinalIgnoreCase) == true ||
                    result.ErrorMessage?.Contains("expired", StringComparison.OrdinalIgnoreCase) == true ||
                     result.ErrorMessage?.Contains("revoked", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger.LogWarning("Device token validation failed for Device ID {DeviceId}. Reason: {Error}", deviceId, result.ErrorMessage);
                    return Unauthorized(result.ErrorMessage); // 401 for token validation failures
                }
                if (result.ErrorMessage?.Contains("not in a state", StringComparison.OrdinalIgnoreCase) == true ||
                     result.ErrorMessage?.Contains("Status is not", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger.LogWarning("Device {DeviceId} failed authentication due to inactive status. Reason: {Error}", deviceId, result.ErrorMessage);
                    return Unauthorized(result.ErrorMessage); // 401 if device status is not Active
                }

                _logger.LogWarning("Device authentication failed for Device ID {DeviceId}. Reason: {Error}", deviceId, result.ErrorMessage);
                return BadRequest(result.ErrorMessage); // 400 for other service failures (e.g., config errors)
            }
        }

        /// <summary>
        /// Endpoint for devices to refresh their tokens using a valid refresh token.
        /// Requires authentication using the device scheme (sending the Refresh Token in the Authorization header).
        /// </summary>
        /// <param name="requestDto">Contains the refresh token for refresh.</param>
        /// <returns>An IActionResult indicating success (200 OK with new token details) or failure (401 Unauthorized).</returns>
        /// <response code="200">Token refresh successful, new token details returned.</response>
        /// <response code="400">Invalid data format.</response>
        /// <response code="401">Authentication failed (refresh token invalid, expired, revoked, or device not Active).</response>
        [HttpPost("refresh-token")] // POST to api/device-api/refresh-token
        // Requires authentication using the device scheme (the handler will validate the refresh token)
        [Authorize(AuthenticationSchemes = DeviceAuthenticationHandler.SchemeName)]
        [ProducesResponseType(typeof(DeviceAuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshDeviceToken([FromBody] DeviceAuthRequestDto requestDto) // Assuming refresh token is in the same DTO structure
        {
            _logger.LogInformation("Received device token refresh request from IP: {IPAddress}", HttpContext.Connection.RemoteIpAddress?.ToString());

            // Authentication has already happened by the DeviceAuthenticationHandler.
            // The handler validated the refresh token and set the User principal.

            // Get device ID from authenticated principal
            var deviceIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(deviceIdClaim) || !int.TryParse(deviceIdClaim, out int deviceId))
            {
                _logger.LogError("Authenticated device ID claim missing or invalid during RefreshDeviceToken endpoint execution.");
                return Unauthorized("Could not identify authenticated device."); // Should not happen
            }

            if (requestDto == null)
            {
                _logger.LogWarning("RefreshDeviceToken called with null DTO for Device ID: {DeviceId}.", deviceId);
                return BadRequest("Authentication data cannot be null.");
            }
            // DTO validation attributes handle basic checks on requestDto.
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for RefreshDeviceToken request (Device ID: {DeviceId}). Errors: {@ModelState}", deviceId, ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            // Get client context info
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
            string userAgent = Request.Headers[HeaderNames.UserAgent].ToString() ?? "Unknown UserAgent";
            string deviceInfo = userAgent; // Simple device info


            // Call service to handle token refresh.
            // Service returns Result<DeviceAuthResponseDto>
            var result = await _deviceCommunicationService.RefreshDeviceTokenAsync(requestDto.Token, ipAddress, userAgent, deviceInfo);

            // Handle the service result
            if (result.IsSuccess)
            {
                _logger.LogInformation("Device token refresh successful for Device ID: {DeviceId}.", deviceId);
                // Return 200 OK with the response DTO containing the new tokens
                return Ok(result.Value);
            }
            else
            {
                // Service returns failure if refresh token invalid/inactive OR if device status is not Active.
                // Map service error messages to 401.
                if (result.ErrorMessage?.Contains("Invalid token", StringComparison.OrdinalIgnoreCase) == true ||
                   result.ErrorMessage?.Contains("expired", StringComparison.OrdinalIgnoreCase) == true ||
                    result.ErrorMessage?.Contains("revoked", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger.LogWarning("Device token refresh validation failed for Device ID {DeviceId}. Reason: {Error}", deviceId, result.ErrorMessage);
                    return Unauthorized(result.ErrorMessage); // 401 for token validation failures
                }
                if (result.ErrorMessage?.Contains("not in a state", StringComparison.OrdinalIgnoreCase) == true ||
                     result.ErrorMessage?.Contains("Status is not", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger.LogWarning("Device {DeviceId} failed refresh due to inactive status. Reason: {Error}", deviceId, result.ErrorMessage);
                    return Unauthorized(result.ErrorMessage); // 401 if device status is not Active
                }

                _logger.LogWarning("Device token refresh failed for Device ID {DeviceId}. Reason: {Error}", deviceId, result.ErrorMessage);
                return BadRequest(result.ErrorMessage); // 400 for other service failures
            }
        }


        /// <summary>
        /// Endpoint for devices to send ambient sensor data.
        /// Requires authentication using the device scheme.
        /// The device sends its Access Token in the Authorization header.
        /// </summary>
        /// <param name="dataDto">The DTO containing the environmental sensor readings.</param>
        /// <returns>An IActionResult indicating success (204 No Content) or failure (400 Bad Request).</returns>
        /// <response code="204">Ambient data received and processed successfully.</response>
        /// <response code="400">Invalid data format or service error.</response>
        /// <response code="401">Authentication failed.</response>
        [HttpPost("ambient-data")] // POST to api/device-api/ambient-data
        [Authorize(AuthenticationSchemes = DeviceAuthenticationHandler.SchemeName)] // Authorize using the device scheme
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ReceiveAmbientData([FromBody] AmbientDataDto dataDto)
        {
            _logger.LogInformation("Received ambient data from IP: {IPAddress}", HttpContext.Connection.RemoteIpAddress?.ToString());

            // Get device ID from authenticated principal
            var deviceIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(deviceIdClaim) || !int.TryParse(deviceIdClaim, out int deviceId))
            {
                _logger.LogError("Authenticated device ID claim missing or invalid during ReceiveAmbientData endpoint execution.");
                return Unauthorized("Could not identify authenticated device."); // Should not happen
            }

            if (dataDto == null)
            {
                _logger.LogWarning("ReceiveAmbientData called with null DTO for Device ID: {DeviceId}.", deviceId);
                return BadRequest("Ambient data cannot be null.");
            }
            // DTO validation attributes handle basic format/required checks on dataDto.
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for ReceiveAmbientData request (Device ID: {DeviceId}). Errors: {@ModelState}", deviceId, ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }


            // Call service to process ambient data. Service handles device association check,
            // weather data lookup, and saving.
            var result = await _deviceCommunicationService.ReceiveAmbientDataAsync(deviceId, dataDto);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Ambient data processed successfully for Device ID: {DeviceId}.", deviceId);
                return NoContent(); // 204 No Content for successful reception and processing
            }
            else
            {
                // Service returns failure for invalid device association, weather service errors, save errors.
                _logger.LogWarning("Processing ambient data failed for Device ID {DeviceId}. Reason: {Error}", deviceId, result.ErrorMessage);
                return BadRequest(result.ErrorMessage); // Generic error message from service
            }
        }

        /// <summary>
        /// Endpoint for devices to send thermal and image data.
        /// Requires authentication using the device scheme.
        /// Uses multipart/form-data.
        /// </summary>
        /// <param name="thermalData">The DTO containing the thermal data and statistics (JSON part, bound from form).</param>
        /// <param name="imageFile">The image file (RGB/Thermal, bound from form).</param>
        /// <param name="thermalImageDataJson">The raw JSON string of the thermal data (bound from form).</param>
        /// <returns>An IActionResult indicating success (204 No Content) or failure (400 Bad Request).</returns>
        /// <response code="204">Capture data received and processed successfully.</response>
        /// <response code="400">Invalid data format or service error.</response>
        /// <response code="401">Authentication failed.</response>
        [HttpPost("capture-data")] // POST to api/device-api/capture-data
        [Authorize(AuthenticationSchemes = DeviceAuthenticationHandler.SchemeName)] // Authorize using the device scheme
        [Consumes("multipart/form-data")] // Specify content type
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ReceiveCaptureData(
            [FromForm] ThermalDataDto thermalData,
            IFormFile imageFile,
            [FromForm] string thermalImageDataJson // Receive the raw JSON string from the form part
            )
        {
            _logger.LogInformation("Received capture data from IP: {IPAddress}", HttpContext.Connection.RemoteIpAddress?.ToString());

            // Get device ID from authenticated principal
            var deviceIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(deviceIdClaim) || !int.TryParse(deviceIdClaim, out int deviceId))
            {
                _logger.LogError("Authenticated device ID claim missing or invalid during ReceiveCaptureData endpoint execution.");
                return Unauthorized("Could not identify authenticated device."); // Should not happen
            }

            if (thermalData == null)
            {
                _logger.LogWarning("ReceiveCaptureData called with null thermal data DTO for Device ID: {DeviceId}.", deviceId);
                return BadRequest("Thermal data cannot be null.");
            }
            if (imageFile == null || imageFile.Length == 0)
            {
                _logger.LogWarning("ReceiveCaptureData called with null or empty image file for Device ID: {DeviceId}.", deviceId);
                return BadRequest("Image file cannot be null or empty.");
            }
            if (string.IsNullOrWhiteSpace(thermalImageDataJson))
            {
                _logger.LogWarning("ReceiveCaptureData called with null or empty thermal image JSON string for Device ID: {DeviceId}", deviceId);
                return BadRequest("Thermal image JSON data cannot be null or empty.");
            }

            // DTO validation attributes on ThermalDataDto handle basic checks.
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for ReceiveCaptureData request (Device ID: {DeviceId}). Errors: {@ModelState}", deviceId, ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            // The device firmware sends RecordedAt with ambient data, but not explicitly with capture data
            // in the provided code snippets. Use backend time as a fallback or if the device doesn't provide it.
            // If the device *should* provide a timestamp with capture data, it needs to be added to the form fields.
            DateTime recordedAt = _dateTimeProvider.GetUtcNow(); // Use backend time for RecordedAt


            // Call service to process capture data. Service handles device association check,
            // weather data lookup (for night/day), image processing (skip RGB at night), and saving.
            // Service returns Result<bool>
            var result = await _deviceCommunicationService.ReceiveCaptureDataAsync(deviceId, thermalData, imageFile, recordedAt, thermalImageDataJson); // Pass the raw JSON string

            if (result.IsSuccess)
            {
                _logger.LogInformation("Capture data processed successfully for Device ID: {DeviceId}.", deviceId);
                return NoContent(); // 204 No Content for successful reception and processing
            }
            else
            {
                // Service returns failure for invalid device association, weather service errors, save errors, etc.
                _logger.LogWarning("Processing capture data failed for Device ID {DeviceId}. Reason: {Error}", deviceId, result.ErrorMessage);
                return BadRequest(result.ErrorMessage); // Generic error message from service
            }
        }

        /// <summary>
        /// Endpoint for devices to send log entries.
        /// Requires authentication using the device scheme.
        /// The device sends its Access Token in the Authorization header.
        /// </summary>
        /// <param name="logEntryDto">The DTO containing the log information.</param>
        /// <returns>An IActionResult indicating success (204 No Content) or failure (400 Bad Request).</returns>
        /// <response code="204">Log entry received and processed successfully.</response>
        /// <response code="400">Invalid data format or service error.</response>
        /// <response code="401">Authentication failed.</response>
        [HttpPost("log")] // POST to api/device-api/log
        [Authorize(AuthenticationSchemes = DeviceAuthenticationHandler.SchemeName)] // Authorize using the device scheme
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ReceiveDeviceLog([FromBody] DeviceLogEntryDto logEntryDto)
        {
            _logger.LogInformation("Received device log from IP: {IPAddress}", HttpContext.Connection.RemoteIpAddress?.ToString());

            // Get device ID from authenticated principal
            var deviceIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(deviceIdClaim) || !int.TryParse(deviceIdClaim, out int deviceId))
            {
                _logger.LogError("Authenticated device ID claim missing or invalid during ReceiveDeviceLog endpoint execution.");
                return Unauthorized("Could not identify authenticated device."); // Should not happen
            }

            if (logEntryDto == null)
            {
                _logger.LogWarning("ReceiveDeviceLog called with null DTO for Device ID: {DeviceId}.", deviceId);
                return BadRequest("Log entry data cannot be null.");
            }
            // DTO validation attributes handle basic format/required checks on logEntryDto.
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for ReceiveDeviceLog request (Device ID: {DeviceId}). Errors: {@ModelState}", deviceId, ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }


            // Call service to process device log. Service handles saving.
            var result = await _deviceCommunicationService.ReceiveDeviceLogAsync(deviceId, logEntryDto);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Device log processed successfully for Device ID: {DeviceId}.", deviceId);
                return NoContent(); // 204 No Content for successful reception and processing
            }
            else
            {
                // Service returns failure for save errors.
                _logger.LogWarning("Processing device log failed for Device ID {DeviceId}. Reason: {Error}", deviceId, result.ErrorMessage);
                return BadRequest(result.ErrorMessage); // Generic error message from service
            }
        }
    }
}
