using ArandanoIRT_Backend.Domain.ValueObjects;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using System.Text.Json;

namespace ArandanoIRT_Backend.Infrastructure.Services
{
    /// <summary>
    /// Implements the <see cref="ICaptchaService"/> interface using the Cloudflare Turnstile API
    /// for verifying CAPTCHA challenges.
    /// </summary>
    public class CloudflareTurnstileService : ICaptchaService
    {
        /// <summary>
        /// Factory for creating HttpClient instances.
        /// </summary>
        private readonly IHttpClientFactory _httpClientFactory;
        /// <summary>
        /// Provides access to application configuration settings.
        /// </summary>
        private readonly IConfiguration _configuration;
        /// <summary>
        /// Logger for recording service events and errors.
        /// </summary>
        private readonly ILogger<CloudflareTurnstileService> _logger;
        /// <summary>
        /// The secret key obtained from Cloudflare Turnstile configuration.
        /// </summary>
        private readonly string _secretKey;
        /// <summary>
        /// The Cloudflare Turnstile site verification API endpoint URL.
        /// </summary>
        private const string VerifyEndpoint = "https://challenges.cloudflare.com/api/v3/siteverify";

        /// <summary>
        /// Represents the deserialized JSON response from the Cloudflare Turnstile siteverify endpoint.
        /// </summary>
        private class TurnstileResponse
        {
            /// <summary>
            /// Gets or sets a value indicating whether the CAPTCHA verification was successful.
            /// </summary>
            public bool Success { get; set; }
            // Add other fields like 'hostname', 'action', 'cdata' if needed for more detailed validation.

            /// <summary>
            /// Gets or sets a list of error codes returned by Cloudflare if verification failed. Nullable.
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("error-codes")]
            public List<string>? ErrorCodes { get; set; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CloudflareTurnstileService"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The factory for creating HttpClient instances.</param>
        /// <param name="configuration">The application configuration provider.</param>
        /// <param name="logger">The logger instance for this service.</param>
        /// <exception cref="ArgumentNullException">Thrown if httpClientFactory, configuration, logger, or the 'Captcha:TurnstileSecretKey' configuration value is null.</exception>
        public CloudflareTurnstileService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<CloudflareTurnstileService> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // Retrieves the secret key from configuration, throwing if not found.
            _secretKey = _configuration["Captcha:TurnstileSecretKey"] ?? throw new ArgumentNullException("Captcha:TurnstileSecretKey", "Turnstile Secret Key ('Captcha:TurnstileSecretKey') not found in configuration.");
        }

        /// <inheritdoc/>
        /// <remarks>
        /// This method communicates with the Cloudflare Turnstile API endpoint. Its success depends on
        /// network connectivity, the availability of the Cloudflare service, and the correct configuration
        /// of the <c>Captcha:TurnstileSecretKey</c> setting.
        /// </remarks>
        public async Task<Result> VerifyCaptchaAsync(string token)
        {
            // Validates that the token provided by the client is not empty.
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("VerifyCaptchaAsync called with empty token.");
                return Result.Failure("CAPTCHA token is missing.");
            }

            try
            {
                // Creates an HttpClient instance using the factory.
                var httpClient = _httpClientFactory.CreateClient();
                // Prepares the form data payload for the Cloudflare API request.
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "secret", _secretKey },     // Includes the secret key.
                    { "response", token }        // Includes the token received from the frontend widget.
                    // Optionally add 'remoteip' if needed and available: { "remoteip", userIpAddress }
                });

                // Sends the POST request to the Cloudflare verification endpoint.
                _logger.LogDebug("Sending verification request to Cloudflare Turnstile endpoint: {Endpoint}", VerifyEndpoint);
                HttpResponseMessage response = await httpClient.PostAsync(VerifyEndpoint, content);

                // Reads the response body as a string.
                string responseBody = await response.Content.ReadAsStringAsync();

                // Checks if the HTTP request itself was successful.
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Cloudflare Turnstile verification request failed with status code {StatusCode}. Response: {ResponseBody}", response.StatusCode, responseBody);
                    return Result.Failure($"CAPTCHA verification failed (HTTP Error: {response.StatusCode}).");
                }

                // Logs the successful response body for debugging.
                _logger.LogDebug("Cloudflare Turnstile verification response received: {ResponseBody}", responseBody);
                // Deserializes the JSON response into the TurnstileResponse object (case-insensitive property matching).
                var turnstileResponse = JsonSerializer.Deserialize<TurnstileResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Checks if deserialization was successful and if Cloudflare reported success.
                if (turnstileResponse == null || !turnstileResponse.Success)
                {
                    // Logs the failure details provided by Cloudflare.
                    _logger.LogWarning("Cloudflare Turnstile verification failed. Success: {Success}, Errors: {Errors}",
                                       turnstileResponse?.Success ?? false,
                                       string.Join(", ", turnstileResponse?.ErrorCodes ?? [])); // Use empty list if ErrorCodes is null
                    // Returns a failure result including the error codes.
                    return Result.Failure($"CAPTCHA verification failed. Errors: {string.Join(", ", turnstileResponse?.ErrorCodes ?? ["unknown"])}"); // Provide default error if null
                }

                // Verification was fully successful.
                _logger.LogInformation("Cloudflare Turnstile verification successful for provided token.");
                return Result.Success();
            }
            catch (JsonException jsonEx) // Handles errors during JSON deserialization.
            {
                _logger.LogError(jsonEx, "Failed to deserialize Cloudflare Turnstile response.");
                return Result.Failure("CAPTCHA verification failed (Response Error).");
            }
            catch (HttpRequestException httpEx) // Handles network errors during the HTTP request.
            {
                _logger.LogError(httpEx, "HTTP request failed during Cloudflare Turnstile verification.");
                return Result.Failure("CAPTCHA verification failed (Network Error).");
            }
            catch (Exception ex) // Handles any other unexpected errors.
            {
                _logger.LogError(ex, "Unexpected error during Cloudflare Turnstile verification.");
                return Result.Failure("An unexpected error occurred during CAPTCHA verification.");
            }
        }
    }
}