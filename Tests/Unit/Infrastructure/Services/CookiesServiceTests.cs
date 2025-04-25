using ArandanoIRT_Backend.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Primitives; 
using Moq;
using Xunit;

namespace ArandanoIRT_Backend.Tests.Unit.Infrastructure.Services
{
    /// <summary>
    /// Contains unit tests for the <see cref="CookiesService"/> class.
    /// Uses <see cref="DefaultHttpContext"/> and mocks <see cref="IConfiguration"/>
    /// and <see cref="IRequestCookieCollection"/> for controlled testing.
    /// Implements <see cref="IDisposable"/> although no unmanaged resources are directly used here.
    /// </summary>
    public class CookiesServiceTests : IDisposable
    {
        /// <summary> Mock for the application configuration dependency. </summary>
        private readonly Mock<IConfiguration> _mockConfiguration;
        /// <summary> The instance of the CookiesService under test. Recreated if configuration changes. </summary>
        private CookiesService _cookiesService;
        /// <summary> The simulated HTTP context for the tests. </summary>
        private DefaultHttpContext _httpContext;
        /// <summary> Mock for the collection of request cookies. </summary>
        private Mock<IRequestCookieCollection> _mockRequestCookies;

        /// <summary> A sample JWT value for testing. </summary>
        private const string TEST_JWT = "test-jwt-token";
        /// <summary> A sample refresh token value for testing. </summary>
        private const string TEST_REFRESH = "test-refresh-token";
        /// <summary> A sample domain value for testing cookie options. </summary>
        private const string TEST_DOMAIN = ".test.local";

        /// <summary>
        /// Initializes a new instance of the <see cref="CookiesServiceTests"/> class.
        /// Sets up mocks for dependencies and creates a fresh <see cref="HttpContext"/> for each test.
        /// </summary>
        public CookiesServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _httpContext = new DefaultHttpContext();
            // Initializes the mock for request cookies.
            _mockRequestCookies = new Mock<IRequestCookieCollection>();

            // Assigns the mock collection to the HttpContext's Request.Cookies property.
            _httpContext.Request.Cookies = _mockRequestCookies.Object;

            // Performs default setup for configuration (no domain initially).
            SetupConfigurationDomain(null);
            // Instantiates the service with default mock configuration.
            _cookiesService = new CookiesService(_mockConfiguration.Object);
        }

        /// <summary>
        /// Disposes resources if necessary. Currently no explicit unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // No specific resources to dispose in this test class setup.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Helper method to configure the mocked <see cref="IConfiguration"/>
        /// for the "Cookies:Domain" setting.
        /// </summary>
        /// <param name="domainValue">The domain value to return, or null.</param>
        private void SetupConfigurationDomain(string? domainValue)
        {
            _mockConfiguration.Setup(c => c["Cookies:Domain"]).Returns(domainValue);
            // Recreates the service instance because its behavior depends on the configuration value
            // which might be changed by this helper during test setup.
            _cookiesService = new CookiesService(_mockConfiguration.Object);
        }

        /// <summary>
        /// Helper method to parse the 'Set-Cookie' headers from the <see cref="HttpResponse"/>.
        /// </summary>
        /// <param name="setCookieValues">The StringValues collection from the response headers.</param>
        /// <returns>A dictionary mapping cookie names to their full Set-Cookie header string.</returns>
        private static Dictionary<string, string> ParseSetCookieHeader(StringValues setCookieValues)
        {
            var cookies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var headerValue in setCookieValues)
            {
                if (string.IsNullOrWhiteSpace(headerValue)) continue;

                var parts = headerValue.Split(';').Select(p => p.Trim()).ToList();
                if (parts.Count > 0)
                {
                    var cookiePair = parts[0].Split('=', 2);
                    if (cookiePair.Length == 2)
                    {
                        // Stores the full header string to allow checking attributes later.
                        cookies[cookiePair[0]] = headerValue;
                    }
                }
            }
            return cookies;
        }

        /// <summary>
        /// Helper method to configure the mocked <see cref="IRequestCookieCollection"/>
        /// to simulate the presence of a specific cookie.
        /// </summary>
        /// <param name="key">The name of the cookie.</param>
        /// <param name="value">The value of the cookie.</param>
        private void SetupRequestCookie(string key, string value)
        {
            // Configures the TryGetValue method, which is often used for safe cookie retrieval.
            // Assigns the value to a local variable needed for the 'out' parameter setup in Moq.
            var outValue = value;
            _mockRequestCookies.Setup(c => c.TryGetValue(key, out outValue)).Returns(true);
            // Also configures the indexer access, although the service might primarily use TryGetValue.
            _mockRequestCookies.Setup(c => c[key]).Returns(value);
        }

        /// <summary>
        /// Helper method to configure the mocked <see cref="IRequestCookieCollection"/>
        /// to simulate the absence of a specific cookie.
        /// </summary>
        /// <param name="key">The name of the cookie that should be missing.</param>
        private void SetupRequestCookieMissing(string key)
        {
            // Configures TryGetValue to return false, indicating the cookie was not found.
            // Important: The out parameter type must match the expected signature (string?).
            string? outValue = null;
            _mockRequestCookies.Setup(c => c.TryGetValue(key, out outValue)).Returns(false);
            // Configures the indexer to return null (TryGetValue is generally preferred over indexer access).
            _mockRequestCookies.Setup(c => c[key]).Returns((string?)null);
        }


        // --- Tests for SetAuthCookies ---
        // These tests verify cookies written to the Response object.

        [Fact]
        public void SetAuthCookies_WhenStayLoggedInTrue_ShouldSetPersistentCookiesWithOptions()
        {
            // Arrange
            // Sets up configuration with a specific domain for this test.
            SetupConfigurationDomain(TEST_DOMAIN);
            bool stayLoggedIn = true;

            // Act
            _cookiesService.SetAuthCookies(_httpContext, TEST_JWT, TEST_REFRESH, stayLoggedIn);

            // Assert
            // Verifies that Set-Cookie headers were added to the response.
            _httpContext.Response.Headers.TryGetValue("Set-Cookie", out var setCookieValues).Should().BeTrue();
            var cookies = ParseSetCookieHeader(setCookieValues);

            // Verifies JWT cookie attributes.
            cookies.Should().ContainKey("jwt");
            cookies["jwt"].Should().Contain($"{TEST_JWT};");
            cookies["jwt"].Should().Contain("path=/");
            cookies["jwt"].Should().Contain($"domain={TEST_DOMAIN}");
            // Checks for the 'expires' attribute, indicating a persistent cookie.
            cookies["jwt"].Should().Contain("expires=");
            cookies["jwt"].Should().Contain("secure");
            cookies["jwt"].Should().Contain("httponly");
            cookies["jwt"].Should().Contain("samesite=none");

            // Verifies refreshToken cookie attributes.
            cookies.Should().ContainKey("refreshToken");
            cookies["refreshToken"].Should().Contain($"{TEST_REFRESH};");
            cookies["refreshToken"].Should().Contain($"domain={TEST_DOMAIN}");
            // Checks for the 'expires' attribute.
            cookies["refreshToken"].Should().Contain("expires=");
            cookies["refreshToken"].Should().Contain("secure");
            cookies["refreshToken"].Should().Contain("httponly");
            cookies["refreshToken"].Should().Contain("samesite=none");

            // Verifies stayLoggedIn cookie attributes.
            cookies.Should().ContainKey("stayLoggedIn");
            // Checks that the value is 'True'.
            cookies["stayLoggedIn"].Should().Contain($"{bool.TrueString};");
            cookies["stayLoggedIn"].Should().Contain($"domain={TEST_DOMAIN}");
            // Checks for the 'expires' attribute.
            cookies["stayLoggedIn"].Should().Contain("expires=");
            cookies["stayLoggedIn"].Should().Contain("secure");
            cookies["stayLoggedIn"].Should().Contain("httponly");
            cookies["stayLoggedIn"].Should().Contain("samesite=none");
        }

        [Fact]
        public void SetAuthCookies_WhenStayLoggedInFalse_ShouldSetSessionCookiesWithOptions()
        {
            // Arrange
            // Sets up configuration without a domain for this test.
            SetupConfigurationDomain(null);
            bool stayLoggedIn = false;

            // Act
            _cookiesService.SetAuthCookies(_httpContext, TEST_JWT, TEST_REFRESH, stayLoggedIn);

            // Assert
            _httpContext.Response.Headers.TryGetValue("Set-Cookie", out var setCookieValues).Should().BeTrue();
            var cookies = ParseSetCookieHeader(setCookieValues);

            // Verifies JWT cookie attributes.
            cookies.Should().ContainKey("jwt");
            cookies["jwt"].Should().Contain($"{TEST_JWT};");
            cookies["jwt"].Should().Contain("path=/");
            // Verifies 'domain' attribute is absent.
            cookies["jwt"].Should().NotContain("domain=");
            // Verifies 'expires' attribute is absent (session cookie).
            cookies["jwt"].Should().NotContain("expires=");
            cookies["jwt"].Should().Contain("secure");
            cookies["jwt"].Should().Contain("httponly");
            cookies["jwt"].Should().Contain("samesite=none");

            // Verifies refreshToken cookie attributes.
            cookies.Should().ContainKey("refreshToken");
            cookies["refreshToken"].Should().Contain($"{TEST_REFRESH};");
            cookies["refreshToken"].Should().NotContain("domain=");
            cookies["refreshToken"].Should().NotContain("expires=");
            cookies["refreshToken"].Should().Contain("secure");
            cookies["refreshToken"].Should().Contain("httponly");
            cookies["refreshToken"].Should().Contain("samesite=none");

            // Verifies stayLoggedIn cookie attributes.
            cookies.Should().ContainKey("stayLoggedIn");
            // Checks that the value is 'False'.
            cookies["stayLoggedIn"].Should().Contain($"{bool.FalseString};");
            cookies["stayLoggedIn"].Should().NotContain("domain=");
            cookies["stayLoggedIn"].Should().NotContain("expires=");
            cookies["stayLoggedIn"].Should().Contain("secure");
            cookies["stayLoggedIn"].Should().Contain("httponly");
            cookies["stayLoggedIn"].Should().Contain("samesite=none");
        }


        // --- Tests for GetStayLoggedIn ---
        // These tests rely on the mocked Request.Cookies.

        [Fact]
        public void GetStayLoggedIn_WhenCookieIsTrue_ShouldReturnTrue()
        {
            // Arrange
            // Uses helper to setup the mock Request.Cookies.
            SetupRequestCookie("stayLoggedIn", "True");

            // Act
            var result = _cookiesService.GetStayLoggedIn(_httpContext);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void GetStayLoggedIn_WhenCookieIsFalse_ShouldReturnFalse()
        {
            // Arrange
            SetupRequestCookie("stayLoggedIn", "False");

            // Act
            var result = _cookiesService.GetStayLoggedIn(_httpContext);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData("yes")] // Scenario: Other non-boolean value
        [InlineData("")]    // Scenario: Empty value
        [InlineData("INVALID")] // Scenario: Other invalid string
        public void GetStayLoggedIn_WhenCookieIsNotValidBoolean_ShouldReturnFalse(string cookieValue)
        {
            // Arrange
            SetupRequestCookie("stayLoggedIn", cookieValue);

            // Act
            var result = _cookiesService.GetStayLoggedIn(_httpContext);

            // Assert
            // Expects false because bool.TryParse will fail for these values.
            result.Should().BeFalse();
        }

        [Fact]
        public void GetStayLoggedIn_WhenCookieDoesNotExist_ShouldReturnFalse()
        {
            // Arrange
            // Uses helper to indicate the cookie is missing from the request.
            SetupRequestCookieMissing("stayLoggedIn");

            // Act
            var result = _cookiesService.GetStayLoggedIn(_httpContext);

            // Assert
            result.Should().BeFalse(); // Expects false if the cookie doesn't exist.
        }

        // --- Tests for GetRefreshToken ---
        // These tests rely on the mocked Request.Cookies.

        [Fact]
        public void GetRefreshToken_WhenCookieExists_ShouldReturnTokenValue()
        {
            // Arrange
            SetupRequestCookie("refreshToken", TEST_REFRESH);

            // Act
            var result = _cookiesService.GetRefreshToken(_httpContext);

            // Assert
            result.Should().Be(TEST_REFRESH);
        }

        [Fact]
        public void GetRefreshToken_WhenCookieDoesNotExist_ShouldReturnNull()
        {
            // Arrange
            SetupRequestCookieMissing("refreshToken");

            // Act
            var result = _cookiesService.GetRefreshToken(_httpContext);

            // Assert
            result.Should().BeNull(); // Expects null if the cookie doesn't exist.
        }

        // --- Tests for RenewAuthCookies ---
        // These tests read the 'stayLoggedIn' cookie from the mocked request
        // and verify cookies written to the response.

        [Fact]
        public void RenewAuthCookies_WhenStayLoggedInWasTrue_ShouldRenewWithExpiry()
        {
            // Arrange
            // Simulates that the original request had stayLoggedIn = true via the mock setup.
            SetupRequestCookie("stayLoggedIn", "True");
            var newJwt = "new-jwt";
            var newRefresh = "new-refresh";

            // Act
            _cookiesService.RenewAuthCookies(_httpContext, newJwt, newRefresh);

            // Assert
            _httpContext.Response.Headers.TryGetValue("Set-Cookie", out var setCookieValues).Should().BeTrue();
            var cookies = ParseSetCookieHeader(setCookieValues);

            // Verifies the renewed cookies are persistent (have 'expires').
            cookies["jwt"].Should().Contain(newJwt);
            cookies["jwt"].Should().Contain("expires=");
            cookies["refreshToken"].Should().Contain(newRefresh);
            cookies["refreshToken"].Should().Contain("expires=");
            cookies["stayLoggedIn"].Should().Contain(bool.TrueString);
            cookies["stayLoggedIn"].Should().Contain("expires=");
        }

        [Fact]
        public void RenewAuthCookies_WhenStayLoggedInWasFalse_ShouldRenewAsSession()
        {
            // Arrange
            // Simulates that the original request had stayLoggedIn = false via the mock setup.
            SetupRequestCookie("stayLoggedIn", "False");
            var newJwt = "new-jwt";
            var newRefresh = "new-refresh";

            // Act
            _cookiesService.RenewAuthCookies(_httpContext, newJwt, newRefresh);

            // Assert
            _httpContext.Response.Headers.TryGetValue("Set-Cookie", out var setCookieValues).Should().BeTrue();
            var cookies = ParseSetCookieHeader(setCookieValues);

            // Verifies the renewed cookies are session cookies (no 'expires').
            cookies["jwt"].Should().Contain(newJwt);
            cookies["jwt"].Should().NotContain("expires=");
            cookies["refreshToken"].Should().Contain(newRefresh);
            cookies["refreshToken"].Should().NotContain("expires=");
            cookies["stayLoggedIn"].Should().Contain(bool.FalseString);
            cookies["stayLoggedIn"].Should().NotContain("expires=");
        }

        // --- Tests for RemoveCookies ---
        // This test verifies cookies written to the Response object.

        [Fact]
        public void RemoveCookies_ShouldAddCookiesWithPastExpiry()
        {
            // Arrange
            // Sets up configuration with a domain.
            SetupConfigurationDomain(TEST_DOMAIN);

            // Act
            _cookiesService.RemoveCookies(_httpContext);

            // Assert
            _httpContext.Response.Headers.TryGetValue("Set-Cookie", out var setCookieValues).Should().BeTrue();
            var cookies = ParseSetCookieHeader(setCookieValues);

            // Verifies JWT cookie removal attributes.
            cookies.Should().ContainKey("jwt");
            // Checks for empty value indicating removal.
            cookies["jwt"].Should().Contain("=;");
            cookies["jwt"].Should().Contain("path=/");
            cookies["jwt"].Should().Contain($"domain={TEST_DOMAIN}");
            // Verifies 'expires' attribute is present, indicating expiration.
            cookies["jwt"].Should().Contain("expires=");
            // Attempts to verify the expires date is in the past using a regex pattern
            // matching common date formats with past years (e.g., ending in 19xx or 2000-2024).
            cookies["jwt"].Should().MatchRegex(@"expires=.*(19\d{2}|20[0-4]\d)[^0-9]");

            // Verifies refreshToken cookie removal attributes.
            cookies.Should().ContainKey("refreshToken");
            cookies["refreshToken"].Should().Contain("=;");
            cookies["refreshToken"].Should().Contain($"domain={TEST_DOMAIN}");
            cookies["refreshToken"].Should().Contain("expires=");
            cookies["refreshToken"].Should().MatchRegex(@"expires=.*(19\d{2}|20[0-4]\d)[^0-9]");

            // Verifies stayLoggedIn cookie removal attributes.
            cookies.Should().ContainKey("stayLoggedIn");
            cookies["stayLoggedIn"].Should().Contain("=;");
            cookies["stayLoggedIn"].Should().Contain($"domain={TEST_DOMAIN}");
            cookies["stayLoggedIn"].Should().Contain("expires=");
            cookies["stayLoggedIn"].Should().MatchRegex(@"expires=.*(19\d{2}|20[0-4]\d)[^0-9]");

            // Also verifies standard security attributes are set on removal cookies.
            cookies["jwt"].Should().Contain("secure");
            cookies["jwt"].Should().Contain("httponly");
            cookies["jwt"].Should().Contain("samesite=none");
        }
    }
}