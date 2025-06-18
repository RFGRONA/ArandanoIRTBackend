using AngleSharp.Io;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

/// <summary>
/// Authentication handler for devices using opaque tokens.
/// Validates tokens provided in the Authorization header against the DeviceTokenService.
/// </summary>
public class DeviceAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IDeviceTokenService _deviceTokenService;
    private readonly ILogger<DeviceAuthenticationHandler> _logger;

    /// <summary>
    /// The name of the authentication scheme for devices.
    /// </summary>
    public const string SchemeName = "DeviceAuthScheme";

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="options">The authentication scheme options.</param>
    /// <param name="logger">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    /// <param name="deviceTokenService">The service for device token management.</param>
    /// <exception cref="ArgumentNullException">Thrown if deviceTokenService is null.</exception>
    public DeviceAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IDeviceTokenService deviceTokenService)
        : base(options, logger, encoder)
    {
        _deviceTokenService = deviceTokenService ?? throw new ArgumentNullException(nameof(deviceTokenService));
        _logger = logger.CreateLogger<DeviceAuthenticationHandler>(); // Create logger using factory
    }

    /// <summary>
    /// Handles the authentication process for incoming requests.
    /// Extracts and validates the device token.
    /// </summary>
    /// <returns>The result of the authentication attempt.</returns>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        _logger.LogDebug("Executing device authentication handler.");

        // 1. Extract the token from the Authorization header
        string? token = null;
        var authorizationHeader = Request.Headers[HeaderNames.Authorization].FirstOrDefault();

        // Expecting format: "Device <token_value>" or "Bearer <token_value>" if we use Bearer for devices too (less ideal)
        const string schemePrefix = "Device "; // Define a specific prefix for device tokens
        if (!string.IsNullOrEmpty(authorizationHeader) && authorizationHeader.StartsWith(schemePrefix, StringComparison.OrdinalIgnoreCase))
        {
            token = authorizationHeader.Substring(schemePrefix.Length).Trim();
            _logger.LogTrace("Device token extracted from Authorization header with '{SchemePrefix}' prefix.", schemePrefix);
        }
        else if (!string.IsNullOrEmpty(authorizationHeader) && authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            // Optional: Allow Bearer prefix for devices if needed, but 'Device' is more explicit.
            token = authorizationHeader.Substring("Bearer ".Length).Trim();
            _logger.LogTrace("Device token extracted from Authorization header with 'Bearer ' prefix (consider using '{SchemePrefix}').", schemePrefix);
        }


        if (string.IsNullOrEmpty(token))
        {
            _logger.LogDebug("No device token found in Authorization header.");
            // No token found, authentication is not successful.
            // Return NoResult() to indicate that this handler did not find credentials,
            // allowing other handlers in the pipeline to try if configured.
            return AuthenticateResult.NoResult();
        }

        // 2. Validate the token using the DeviceTokenService
        try
        {
            // ValidateTokenAsync returns Result<DeviceTokenResponseDto>
            var validationResult = await _deviceTokenService.ValidateTokenAsync(token);

            if (validationResult.IsFailure)
            {
                _logger.LogWarning("Device token validation failed: {Reason}", validationResult.ErrorMessage);
                // Token invalid (not found, expired, revoked). Authentication fails.
                // Fail() provides a reason.
                return AuthenticateResult.Fail(validationResult.ErrorMessage ?? "Invalid device token.");
            }

            // Token is valid and active. Get the Device ID from the result DTO.
            var tokenDetails = validationResult.Value;

            if (!tokenDetails.DeviceId.HasValue || tokenDetails.DeviceId.Value <= 0)
            {
                _logger.LogError("Device token {TokenId} is valid but lacks a valid Device ID.", tokenDetails.DeviceTokenId);
                // This indicates a critical data inconsistency.
                return AuthenticateResult.Fail("Invalid token state: Missing device ID.");
            }

            int deviceId = tokenDetails.DeviceId.Value;

            // 3. Create Claims Principal for the authenticated device
            var claims = new[]
            {
                    // Use ClaimTypes.NameIdentifier for the device ID
                    new Claim(ClaimTypes.NameIdentifier, deviceId.ToString()),
                    // Add a role claim if devices have a distinct role
                    new Claim(ClaimTypes.Role, "Device"), // Assign a 'Device' role
                    // Optional: Add other claims from tokenDetails if useful (e.g., token ID, expiry)
                    new Claim("DeviceTokenId", tokenDetails.DeviceTokenId.ToString()), // Store the ID of the token record
                     new Claim("RefreshTokenExpiration", tokenDetails.RefreshTokenExpiration.ToString("o")), // ISO 8601 format
                     new Claim("AccessTokenExpiration", tokenDetails.AccessTokenExpiration.ToString("o"))
                };

            // Create an identity and principal
            var identity = new ClaimsIdentity(claims, Scheme.Name); // Use the scheme name
            var principal = new ClaimsPrincipal(identity);

            // 4. Authentication successful
            var ticket = new AuthenticationTicket(principal, Scheme.Name); // Use the scheme name
            _logger.LogInformation("Device authentication successful for Device ID: {DeviceId}.", deviceId);

            return AuthenticateResult.Success(ticket);

        }
        catch (Exception ex)
        {
            // Catch any unexpected errors during the authentication process
            _logger.LogError(ex, "An unexpected error occurred during device authentication.");
            return AuthenticateResult.Fail("An unexpected error occurred during authentication.");
        }
    }
}