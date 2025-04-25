using ArandanoIRT_Backend.Application.Interfaces.Utilities; 
using ArandanoIRT_Backend.Infrastructure.Services; 
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;


namespace ArandanoIRT_Backend.Tests.Unit.Infrastructure.Services
{
    /// <summary>
    /// Contains unit tests for the <see cref="CloudflareTurnstileService"/> class.
    /// </summary>
    public class CloudflareTurnstileServiceTests
    {
        /// <summary> Mock for the application configuration dependency. </summary>
        private readonly Mock<IConfiguration> _mockConfiguration;
        /// <summary> Mock for the HTTP client factory dependency. </summary>
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        /// <summary> Mock for the HTTP message handler to intercept HTTP requests. </summary>
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        /// <summary> The instance of the CAPTCHA service under test. </summary>
        private readonly CloudflareTurnstileService _captchaService;

        /// <summary> A dummy secret key for testing configuration retrieval. </summary>
        private const string DUMMY_SECRET_KEY = "TestSecretKey12345";
        /// <summary> The expected URL for Cloudflare Turnstile verification endpoint. </summary>
        private const string CLOUDFLARE_VERIFY_URL = "https://challenges.cloudflare.com/api/v3/siteverify";
        /// <summary> A valid CAPTCHA token string for testing success scenarios. </summary>
        private const string VALID_CAPTCHA_TOKEN = "valid-captcha-token";
        /// <summary> An invalid CAPTCHA token string for testing failure scenarios. </summary>
        private const string INVALID_CAPTCHA_TOKEN = "invalid-captcha-token";

        /// <summary>
        /// Initializes a new instance of the <see cref="CloudflareTurnstileServiceTests"/> class,
        /// setting up the necessary mocks for configuration, HTTP client factory, and message handler.
        /// </summary>
        public CloudflareTurnstileServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

            // Sets up the IConfiguration mock to return the dummy secret key.
            _mockConfiguration.Setup(c => c["Captcha:TurnstileSecretKey"]).Returns(DUMMY_SECRET_KEY);

            // Sets up the IHttpClientFactory mock to return an HttpClient instance
            // configured with the mocked HttpMessageHandler.
            var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            // BaseAddress is not strictly necessary if the full URL is always used in PostAsync calls.
            _mockHttpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Instantiates the service under test with the mocked dependencies.
            _captchaService = new CloudflareTurnstileService(
                _mockHttpClientFactory.Object,
                _mockConfiguration.Object,
                NullLogger<CloudflareTurnstileService>.Instance // Uses a null logger for simplicity in unit tests.
            );
        }

        /// <summary>
        /// Sets up the mocked <see cref="HttpMessageHandler"/> to return a specific <see cref="HttpResponseMessage"/>
        /// when its SendAsync method is called with matching request criteria.
        /// </summary>
        /// <param name="statusCode">The HTTP status code to return.</param>
        /// <param name="content">The HTTP content to return.</param>
        private void SetupMockHttpResponse(HttpStatusCode statusCode, HttpContent content)
        {
            _mockHttpMessageHandler.Protected()
                // Sets up the SendAsync method, which is the core method HttpClient uses internally.
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    // Expects a POST request to the specific Cloudflare verification URL.
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.ToString() == CLOUDFLARE_VERIFY_URL
                    ),
                    // Allows any cancellation token to be passed.
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = content,
                });
            // Could optionally mark as Verifiable() if strict verification is needed later.
        }

        /// <summary>
        /// Helper method to set up the mocked <see cref="HttpMessageHandler"/> to return a successful (200 OK)
        /// response containing the specified object serialized as JSON.
        /// </summary>
        /// <param name="jsonObject">The object to serialize and include in the response body.</param>
        private void SetupMockSuccessResponse(object jsonObject)
        {
            var json = JsonSerializer.Serialize(jsonObject);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            SetupMockHttpResponse(HttpStatusCode.OK, content);
        }

        /// <summary>
        /// Sets up the mocked <see cref="HttpMessageHandler"/> to throw an <see cref="HttpRequestException"/>
        /// when SendAsync is called, simulating a network or connection error.
        /// </summary>
        private void SetupMockHttpRequestException()
        {
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                     ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post && req.RequestUri!.ToString() == CLOUDFLARE_VERIFY_URL),
                    ItExpr.IsAny<CancellationToken>()
                )
                 .ThrowsAsync(new HttpRequestException("Simulated network error"));
        }


        // --- Test Cases ---

        /// <summary>
        /// Tests that VerifyCaptchaAsync returns success when a valid token is provided
        /// and the mocked Cloudflare response indicates success.
        /// </summary>
        [Fact]
        public async Task VerifyCaptchaAsync_WithValidTokenAndSuccessfulResponse_ShouldReturnSuccess()
        {
            // Arrange
            // Sets up the mock HTTP response to return a JSON object indicating success.
            var successResponse = new { success = true };
            SetupMockSuccessResponse(successResponse);

            // Act
            var result = await _captchaService.VerifyCaptchaAsync(VALID_CAPTCHA_TOKEN);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.ErrorMessage.Should().BeNullOrEmpty();

            // Verifies that the HTTP POST call was made once to the correct URL.
            _mockHttpMessageHandler.Protected().Verify(
               "SendAsync", Times.Once(),
               ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == CLOUDFLARE_VERIFY_URL),
               ItExpr.IsAny<CancellationToken>());
        }

        /// <summary>
        /// Tests that VerifyCaptchaAsync returns failure when the Cloudflare response indicates
        /// failure (success: false) and includes error codes.
        /// </summary>
        [Fact]
        public async Task VerifyCaptchaAsync_WithValidTokenAndFailureResponse_ShouldReturnFailureWithErrorCodes()
        {
            // Arrange
            // Sets up the mock HTTP response for a failure case with specific error codes.
            var failureResponse = new { success = false, error_codes = new List<string> { "invalid-input-response", "timeout-or-duplicate" } };
            // Manually replaces property name to match the expected JSON ("error-codes" vs "error_codes").
            // Alternatively, configure JsonSerializerOptions or use [JsonPropertyName].
            var json = JsonSerializer.Serialize(failureResponse).Replace("error_codes", "error-codes");
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            // Cloudflare often returns 200 OK even on verification failure.
            SetupMockHttpResponse(HttpStatusCode.OK, content);

            // Act
            var result = await _captchaService.VerifyCaptchaAsync(INVALID_CAPTCHA_TOKEN);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("CAPTCHA verification failed");
            result.ErrorMessage.Should().Contain("invalid-input-response"); // Checks if specific error codes are included
            result.ErrorMessage.Should().Contain("timeout-or-duplicate");

            // Verifies that the HTTP call was made.
            _mockHttpMessageHandler.Protected().Verify(
               "SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        /// <summary>
        /// Tests that VerifyCaptchaAsync returns failure when the HTTP request to Cloudflare
        /// results in an unsuccessful status code (e.g., 500 Internal Server Error).
        /// </summary>
        [Fact]
        public async Task VerifyCaptchaAsync_WithHttpError_ShouldReturnFailure()
        {
            // Arrange
            // Sets up the mock HTTP response to return an error status code.
            var errorContent = new StringContent("Cloudflare server error", System.Text.Encoding.UTF8, "text/plain");
            SetupMockHttpResponse(HttpStatusCode.InternalServerError, errorContent);

            // Act
            var result = await _captchaService.VerifyCaptchaAsync(VALID_CAPTCHA_TOKEN);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Checks for the specific error message indicating an HTTP error.
            result.ErrorMessage.Should().Contain("CAPTCHA verification failed (HTTP Error: InternalServerError)");

            // Verifies that the HTTP call was made.
            _mockHttpMessageHandler.Protected().Verify(
               "SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        /// <summary>
        /// Tests that VerifyCaptchaAsync returns failure when a network error
        /// (simulated by an HttpRequestException) occurs during the HTTP request.
        /// </summary>
        [Fact]
        public async Task VerifyCaptchaAsync_WithNetworkError_ShouldReturnFailure()
        {
            // Arrange
            // Sets up the mock message handler to throw an HttpRequestException.
            SetupMockHttpRequestException();

            // Act
            var result = await _captchaService.VerifyCaptchaAsync(VALID_CAPTCHA_TOKEN);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Checks for the specific error message indicating a network error.
            result.ErrorMessage.Should().Contain("CAPTCHA verification failed (Network Error)");

            // Verifies that the HTTP call was attempted.
            _mockHttpMessageHandler.Protected().Verify(
               "SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        /// <summary>
        /// Tests that VerifyCaptchaAsync returns failure when the response body from Cloudflare
        /// is not valid JSON, preventing successful deserialization.
        /// </summary>
        [Fact]
        public async Task VerifyCaptchaAsync_WithInvalidJsonResponse_ShouldReturnFailure()
        {
            // Arrange
            // Sets up the mock HTTP response with OK status but invalid JSON content.
            var invalidJsonContent = new StringContent("this is not json {", System.Text.Encoding.UTF8, "application/json");
            SetupMockHttpResponse(HttpStatusCode.OK, invalidJsonContent);

            // Act
            var result = await _captchaService.VerifyCaptchaAsync(VALID_CAPTCHA_TOKEN);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Checks for the specific error message indicating a response parsing error.
            result.ErrorMessage.Should().Contain("CAPTCHA verification failed (Response Error)");

            // Verifies that the HTTP call was made.
            _mockHttpMessageHandler.Protected().Verify(
               "SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        /// <summary>
        /// Tests that VerifyCaptchaAsync returns failure immediately if the provided token is null,
        /// without making an HTTP call.
        /// </summary>
        [Fact]
        public async Task VerifyCaptchaAsync_WithNullToken_ShouldReturnFailure()
        {
            // Arrange
            string? token = null;

            // Act
            // Uses null-forgiving operator for the test.
            var result = await _captchaService.VerifyCaptchaAsync(token!);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Be("CAPTCHA token is missing.");

            // Verifies the HTTP call was NOT made due to early validation failure.
            _mockHttpMessageHandler.Protected().Verify(
              "SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        /// <summary>
        /// Tests that VerifyCaptchaAsync returns failure immediately if the provided token is empty,
        /// without making an HTTP call.
        /// </summary>
        [Fact]
        public async Task VerifyCaptchaAsync_WithEmptyToken_ShouldReturnFailure()
        {
            // Arrange
            string token = "";

            // Act
            var result = await _captchaService.VerifyCaptchaAsync(token);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Be("CAPTCHA token is missing.");

            // Verifies the HTTP call was NOT made.
            _mockHttpMessageHandler.Protected().Verify(
               "SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        /// <summary>
        /// Tests that VerifyCaptchaAsync returns failure immediately if the provided token is whitespace,
        /// without making an HTTP call.
        /// </summary>
        [Fact]
        public async Task VerifyCaptchaAsync_WithWhitespaceToken_ShouldReturnFailure()
        {
            // Arrange
            string token = "    ";

            // Act
            var result = await _captchaService.VerifyCaptchaAsync(token);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Be("CAPTCHA token is missing.");

            // Verifies the HTTP call was NOT made.
            _mockHttpMessageHandler.Protected().Verify(
               "SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }
    }
}