using ArandanoIRT_Backend.Application.Interfaces.Utilities; 
using ArandanoIRT_Backend.Domain.ValueObjects; 
using ArandanoIRT_Backend.Infrastructure.Services; 
using FluentAssertions;
using Moq;
using System.Net; 
using System.Security.Claims; 
using Xunit;

namespace ArandanoIRT_Backend.Tests.Unit.Infrastructure.Services
{
    /// <summary>
    /// Contains unit tests for the <see cref="RequestContextAccessor"/> class.
    /// </summary>
    public class RequestContextAccessorTests
    {
        /// <summary> Mock for the HttpContext accessor dependency. </summary>
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        /// <summary> Mock for the User-Agent parsing utility dependency. </summary>
        private readonly Mock<IUserAgentParser> _mockUserAgentParser;
        /// <summary> The simulated HTTP context instance used for testing. </summary>
        private readonly DefaultHttpContext _httpContext;
        /// <summary> The instance of the RequestContextAccessor under test. </summary>
        private readonly RequestContextAccessor _requestContextAccessor;

        /// <summary> Default User-Agent string for tests. </summary>
        private const string DEFAULT_UA_STRING = "Mozilla/5.0 TestAgent/1.0";
        /// <summary> Default IP address simulating direct connection. </summary>
        private const string DEFAULT_IP_DIRECT = "192.168.1.1";
        /// <summary> Default IP address simulating value from X-Forwarded-For header. </summary>
        private const string DEFAULT_IP_FORWARDED = "10.0.0.5"; // Renamed constant for clarity
        /// <summary> Default User ID claim value for tests. </summary>
        private const int DEFAULT_USER_ID = 123;
        /// <summary> Default Crop ID claim value for tests. </summary>
        private const int DEFAULT_CROP_ID = 456;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestContextAccessorTests"/> class.
        /// Sets up mocks, creates a <see cref="DefaultHttpContext"/>, and instantiates the service under test.
        /// </summary>
        public RequestContextAccessorTests()
        {
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            _mockUserAgentParser = new Mock<IUserAgentParser>();
            // Creates a new HttpContext instance for each test.
            _httpContext = new DefaultHttpContext();

            // Configures the mock accessor to return the test HttpContext instance.
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(_httpContext);

            // Sets up default features for the HttpContext, which can be overridden in specific tests.
            _httpContext.Connection.RemoteIpAddress = IPAddress.Parse(DEFAULT_IP_DIRECT);
            _httpContext.Request.Headers.UserAgent = DEFAULT_UA_STRING;
            // Defaults to an unauthenticated user principal.
            _httpContext.User = new ClaimsPrincipal();

            // Sets up a default behavior for the UserAgentParser mock.
            // Returns a default ClientInfo instance initially.
            _mockUserAgentParser.Setup(p => p.Parse(It.IsAny<string>()))
                                .Returns(new ClientInfo());

            // Instantiates the service under test with its mocked dependencies.
            _requestContextAccessor = new RequestContextAccessor(
                _mockHttpContextAccessor.Object,
                _mockUserAgentParser.Object
            );
        }
        
        #region Tests for GetIpAddress

        [Fact]
        public void GetIpAddress_WhenNoXForwardedFor_ShouldReturnRemoteIpAddress()
        {
            // Arrange
            // Default setup uses RemoteIpAddress. Ensure X-Forwarded-For is absent.
            _httpContext.Connection.RemoteIpAddress = IPAddress.Parse(DEFAULT_IP_DIRECT);
            _httpContext.Request.Headers.Remove("X-Forwarded-For");

            // Act
            var result = _requestContextAccessor.GetIpAddress();

            // Assert
            result.Should().Be(DEFAULT_IP_DIRECT);
        }

        [Fact]
        public void GetIpAddress_WhenXForwardedForExists_ShouldReturnFirstIpFromHeader()
        {
            // Arrange
            var forwardedIp = "10.0.0.1";
            _httpContext.Request.Headers["X-Forwarded-For"] = forwardedIp;
            // Sets RemoteIpAddress to ensure X-Forwarded-For is preferred when present.
            _httpContext.Connection.RemoteIpAddress = IPAddress.Parse(DEFAULT_IP_DIRECT);

            // Act
            var result = _requestContextAccessor.GetIpAddress();

            // Assert
            result.Should().Be(forwardedIp); // Expects the value from the header.
        }

        [Fact]
        public void GetIpAddress_WhenXForwardedForHasMultipleIPs_ShouldReturnFirstIp()
        {
            // Arrange
            var firstIp = "10.0.0.2";
            var forwardedHeader = $"{firstIp}, 192.168.0.100, 172.16.5.5"; // Comma-separated list
            _httpContext.Request.Headers["X-Forwarded-For"] = forwardedHeader;
            _httpContext.Connection.RemoteIpAddress = IPAddress.Parse(DEFAULT_IP_DIRECT);

            // Act
            var result = _requestContextAccessor.GetIpAddress();

            // Assert
            result.Should().Be(firstIp); // Expects the first IP in the list.
        }

        [Fact]
        public void GetIpAddress_WhenHttpContextIsNull_ShouldReturnUnknown()
        {
            // Arrange
            // Configures the mock accessor to return a null HttpContext.
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);
            // Recreates the service instance with the accessor now returning null.
            var accessorWithNullContext = new RequestContextAccessor(_mockHttpContextAccessor.Object, _mockUserAgentParser.Object);

            // Act
            var result = accessorWithNullContext.GetIpAddress();

            // Assert
            result.Should().Be("Unknown"); // Expects fallback value when context is unavailable.
        }

        #endregion

        #region Tests for GetUserAgent

        [Fact]
        public void GetUserAgent_WhenHeaderExists_ShouldReturnHeaderValue()
        {
            // Arrange
            // Default setup includes the User-Agent header.
            _httpContext.Request.Headers.UserAgent = DEFAULT_UA_STRING;

            // Act
            var result = _requestContextAccessor.GetUserAgent();

            // Assert
            result.Should().Be(DEFAULT_UA_STRING);
        }

        [Fact]
        public void GetUserAgent_WhenHeaderMissing_ShouldReturnUnknown()
        {
            // Arrange
            // Ensures the User-Agent header is missing for this test.
            _httpContext.Request.Headers.Remove("User-Agent");

            // Act
            var result = _requestContextAccessor.GetUserAgent();

            // Assert
            result.Should().Be("Unknown"); // Expects fallback value.
        }

        [Fact]
        public void GetUserAgent_WhenHttpContextIsNull_ShouldReturnUnknown()
        {
            // Arrange
            // Configures the mock accessor to return a null HttpContext.
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);
            var accessorWithNullContext = new RequestContextAccessor(_mockHttpContextAccessor.Object, _mockUserAgentParser.Object);

            // Act
            var result = accessorWithNullContext.GetUserAgent();

            // Assert
            result.Should().Be("Unknown"); // Expects fallback value.
        }

        #endregion

        #region Tests for GetClientInfo and GetFormattedDeviceInfo

        [Fact]
        public void GetClientInfo_ShouldCallUserAgentParserAndReturnResult()
        {
            // Arrange
            var userAgent = "SpecificAgent/2.0";
            _httpContext.Request.Headers.UserAgent = userAgent; // Set specific UA for this test
            var expectedClientInfo = new ClientInfo("SpecificAgent", "2.0", "TestOS", "11", "TestDevice");
            // Configures the parser mock to return a specific ClientInfo object for the given UA.
            _mockUserAgentParser.Setup(p => p.Parse(userAgent)).Returns(expectedClientInfo);

            // Act
            var result = _requestContextAccessor.GetClientInfo();

            // Assert
            result.Should().BeEquivalentTo(expectedClientInfo);
            // Verifies that the IUserAgentParser was called exactly once with the correct UA string.
            _mockUserAgentParser.Verify(p => p.Parse(userAgent), Times.Once);
        }

        [Fact]
        public void GetFormattedDeviceInfo_ShouldCallGetClientInfoAndFormat()
        {
            // Arrange
            var userAgent = "SpecificAgent/2.0";
            _httpContext.Request.Headers.UserAgent = userAgent;
            var clientInfo = new ClientInfo("SpecificAgent", "2.0", "TestOS", "11", "TestDevice");
            // Uses the ClientInfo record's own formatting logic for the expected result.
            var expectedFormattedString = clientInfo.ToFormattedString();
            // Configures the parser mock.
            _mockUserAgentParser.Setup(p => p.Parse(userAgent)).Returns(clientInfo);

            // Act
            var result = _requestContextAccessor.GetFormattedDeviceInfo();

            // Assert
            result.Should().Be(expectedFormattedString);
            // Verifies the parser was called.
            _mockUserAgentParser.Verify(p => p.Parse(userAgent), Times.Once);
        }

        [Fact]
        public void GetClientInfo_WhenCalledMultipleTimes_ShouldReturnCachedResult()
        {
            // Arrange
            var userAgent = "SpecificAgent/3.0";
            _httpContext.Request.Headers.UserAgent = userAgent;
            var expectedClientInfo = new ClientInfo("SpecificAgent", "3.0");
            _mockUserAgentParser.Setup(p => p.Parse(userAgent)).Returns(expectedClientInfo);

            // Act
            var result1 = _requestContextAccessor.GetClientInfo();
            // Calls the method again.
            var result2 = _requestContextAccessor.GetClientInfo();

            // Assert
            result1.Should().BeEquivalentTo(expectedClientInfo);
            // Checks if the SAME object instance is returned, indicating caching.
            result2.Should().BeSameAs(result1);
            // Verifies the underlying parser was called only ONCE due to caching.
            _mockUserAgentParser.Verify(p => p.Parse(userAgent), Times.Once());
        }

        #endregion
        #region Tests for GetCurrentUserId and GetCurrentCropId

        [Fact]
        public void GetCurrentUserId_WhenUserIsAuthenticatedWithNameIdentifier_ShouldReturnUserId()
        {
            // Arrange
            // Creates claims for an authenticated user with a NameIdentifier claim.
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, DEFAULT_USER_ID.ToString()) };
            var identity = new ClaimsIdentity(claims, "TestAuthType"); // Sets authentication type
            _httpContext.User = new ClaimsPrincipal(identity); // Assigns authenticated user

            // Act
            var result = _requestContextAccessor.GetCurrentUserId();

            // Assert
            result.Should().Be(DEFAULT_USER_ID);
        }

        [Fact]
        public void GetCurrentUserId_WhenNameIdentifierClaimIsMissing_ShouldReturnNull()
        {
            // Arrange
            // Simulates an authenticated user lacking the specific NameIdentifier claim.
            var claims = new[] { new Claim(ClaimTypes.Email, "test@example.com") }; // Has other claims
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            _httpContext.User = new ClaimsPrincipal(identity);

            // Act
            var result = _requestContextAccessor.GetCurrentUserId();

            // Assert
            result.Should().BeNull(); // Expects null when the required claim is absent.
        }

        [Fact]
        public void GetCurrentUserId_WhenNameIdentifierClaimIsNotInt_ShouldReturnNull()
        {
            // Arrange
            // Simulates a NameIdentifier claim with a value that cannot be parsed as an integer.
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "not-an-integer") };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            _httpContext.User = new ClaimsPrincipal(identity);

            // Act
            var result = _requestContextAccessor.GetCurrentUserId();

            // Assert
            result.Should().BeNull(); // Expects null due to parsing failure.
        }

        [Fact]
        public void GetCurrentUserId_WhenUserIsNotAuthenticated_ShouldReturnNull()
        {
            // Arrange
            // Ensures the user principal is unauthenticated (default setup).
            _httpContext.User = new ClaimsPrincipal();

            // Act
            var result = _requestContextAccessor.GetCurrentUserId();

            // Assert
            result.Should().BeNull(); // Expects null for unauthenticated users.
        }

        [Fact]
        public void GetCurrentUserId_WhenHttpContextIsNull_ShouldReturnNull()
        {
            // Arrange
            // Configures the mock accessor to return a null HttpContext.
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);
            var accessorWithNullContext = new RequestContextAccessor(_mockHttpContextAccessor.Object, _mockUserAgentParser.Object);

            // Act
            var result = accessorWithNullContext.GetCurrentUserId();

            // Assert
            result.Should().BeNull(); // Expects null when context is unavailable.
        }

        [Fact]
        public void GetCurrentCropId_WhenCropIdClaimExists_ShouldReturnCropId()
        {
            // Arrange
            // Creates claims including the required NameIdentifier and the custom "CropId" claim.
            var claims = new[] {
                new Claim(ClaimTypes.NameIdentifier, DEFAULT_USER_ID.ToString()), // User needs to be identified
                new Claim("CropId", DEFAULT_CROP_ID.ToString()) // The specific custom claim
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            _httpContext.User = new ClaimsPrincipal(identity); // Sets authenticated user

            // Act
            var result = _requestContextAccessor.GetCurrentCropId();

            // Assert
            result.Should().Be(DEFAULT_CROP_ID);
        }

        [Fact]
        public void GetCurrentCropId_WhenCropIdClaimIsMissing_ShouldReturnNull()
        {
            // Arrange
            // Simulates an authenticated user lacking the custom "CropId" claim.
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, DEFAULT_USER_ID.ToString()) };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            _httpContext.User = new ClaimsPrincipal(identity);

            // Act
            var result = _requestContextAccessor.GetCurrentCropId();

            // Assert
            result.Should().BeNull(); // Expects null when the claim is absent.
        }

        [Fact]
        public void GetCurrentCropId_WhenCropIdClaimIsNotInt_ShouldReturnNull()
        {
            // Arrange
            // Simulates a "CropId" claim with a value that cannot be parsed as an integer.
            var claims = new[] {
                new Claim(ClaimTypes.NameIdentifier, DEFAULT_USER_ID.ToString()),
                new Claim("CropId", "not-a-crop-id")
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            _httpContext.User = new ClaimsPrincipal(identity);

            // Act
            var result = _requestContextAccessor.GetCurrentCropId();

            // Assert
            result.Should().BeNull(); // Expects null due to parsing failure.
        }

        [Fact]
        public void GetCurrentCropId_WhenUserIsNotAuthenticated_ShouldReturnNull()
        {
            // Arrange
            // Ensures the user principal is unauthenticated.
            _httpContext.User = new ClaimsPrincipal();

            // Act
            var result = _requestContextAccessor.GetCurrentCropId();

            // Assert
            result.Should().BeNull(); // Expects null for unauthenticated users.
        }

        [Fact]
        public void GetCurrentCropId_WhenHttpContextIsNull_ShouldReturnNull()
        {
            // Arrange
            // Configures the mock accessor to return a null HttpContext.
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);
            var accessorWithNullContext = new RequestContextAccessor(_mockHttpContextAccessor.Object, _mockUserAgentParser.Object);

            // Act
            var result = accessorWithNullContext.GetCurrentCropId();

            // Assert
            result.Should().BeNull(); // Expects null when context is unavailable.
        }

        #endregion
    }
}