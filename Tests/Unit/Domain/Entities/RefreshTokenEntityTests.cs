using ArandanoIRT_Backend.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace ArandanoIRT_Backend.Tests.Unit.Domain.Entities
{
    /// <summary>
    /// Contains unit tests for the <see cref="RefreshTokenEntity"/> class.
    /// Focuses on constructor validation and method logic (Revoke, Replace).
    /// </summary>
    public class RefreshTokenEntityTests
    {
        // --- Default valid values for constructor parameters ---
        /// <summary> Default ID for test entities. </summary>
        private const int DefaultId = 1;
        /// <summary> Default session identifier for test entities. </summary>
        private const long DefaultSession = 1234567890L;
        /// <summary> Default token value for test entities. </summary>
        private const string DefaultToken = "valid-refresh-token-string";
        /// <summary> Default device information for test entities. </summary>
        private const string DefaultDeviceInfo = "Chrome on Windows 11";
        /// <summary> Default IP address for test entities. </summary>
        private const string DefaultIpAddress = "192.168.1.100";
        /// <summary> Default user agent string for test entities. </summary>
        private const string DefaultUserAgent = "Mozilla/5.0...";
        /// <summary> Default creation timestamp for test entities. </summary>
        private static readonly DateTime DefaultCreatedAt = DateTime.UtcNow.AddDays(-1);
        /// <summary> Default expiration timestamp for test entities. </summary>
        private static readonly DateTime DefaultExpiresAt = DateTime.UtcNow.AddDays(29);
        /// <summary> Default person ID for test entities. </summary>
        private const int DefaultPersonId = 101;

        /// <summary>
        /// Creates a valid instance of <see cref="RefreshTokenEntity"/> with specified or default values.
        /// </summary>
        /// <param name="id">The refresh token ID.</param>
        /// <param name="session">The session identifier.</param>
        /// <param name="token">The refresh token string.</param>
        /// <param name="deviceInfo">Information about the device associated with the token.</param>
        /// <param name="ipAddress">The IP address associated with the token creation.</param>
        /// <param name="userAgent">The user agent string associated with the token creation.</param>
        /// <param name="createdAt">The creation timestamp. Uses default if null.</param>
        /// <param name="expiresAt">The expiration timestamp. Uses default if null.</param>
        /// <param name="revokedAt">The revocation timestamp (optional).</param>
        /// <param name="revokedByIp">The IP address that revoked the token (optional).</param>
        /// <param name="replacedByToken">The token that replaced this one (optional).</param>
        /// <param name="personId">The associated Person ID (optional).</param>
        /// <returns>A new instance of <see cref="RefreshTokenEntity"/>.</returns>
        private static RefreshTokenEntity CreateValidRefreshTokenEntity(
            int id = DefaultId,
            long session = DefaultSession,
            string token = DefaultToken,
            string deviceInfo = DefaultDeviceInfo,
            string ipAddress = DefaultIpAddress,
            string userAgent = DefaultUserAgent,
            DateTime? createdAt = null,
            DateTime? expiresAt = null,
            DateTime? revokedAt = null,
            string? revokedByIp = null,
            string? replacedByToken = null,
            int? personId = DefaultPersonId)
        {
            return new RefreshTokenEntity(
                id,
                session,
                token,
                deviceInfo,
                ipAddress,
                userAgent,
                createdAt ?? DefaultCreatedAt,
                expiresAt ?? DefaultExpiresAt,
                revokedAt,
                revokedByIp,
                replacedByToken,
                personId
            );
        }

        // --- Constructor Validation Tests ---

        /// <summary>
        /// Verifies constructor throws ArgumentException if Token is null, empty, or whitespace.
        /// </summary>
        /// <param name="invalidToken">The invalid token value (null, empty, or whitespace).</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public void Constructor_ShouldThrowArgumentException_WhenTokenIsInvalid(string? invalidToken)
        {
            // Act
            // Uses null-forgiving operator (!) for the null test case.
            Action act = () => CreateValidRefreshTokenEntity(token: invalidToken!);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("The token is required. (Parameter 'token')");
        }

        /// <summary>
        /// Verifies constructor throws ArgumentException if DeviceInfo is null, empty, or whitespace.
        /// </summary>
        /// <param name="invalidDeviceInfo">The invalid device info value.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public void Constructor_ShouldThrowArgumentException_WhenDeviceInfoIsInvalid(string? invalidDeviceInfo)
        {
            // Act
            // Uses null-forgiving operator (!) for the null test case.
            Action act = () => CreateValidRefreshTokenEntity(deviceInfo: invalidDeviceInfo!);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("Device information is required. (Parameter 'deviceInfo')");
        }

        /// <summary>
        /// Verifies constructor throws ArgumentException if IpAddress is null, empty, or whitespace.
        /// </summary>
        /// <param name="invalidIpAddress">The invalid IP address value.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public void Constructor_ShouldThrowArgumentException_WhenIpAddressIsInvalid(string? invalidIpAddress)
        {
            // Act
            // Uses null-forgiving operator (!) for the null test case.
            Action act = () => CreateValidRefreshTokenEntity(ipAddress: invalidIpAddress!);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("The IP address is required. (Parameter 'ipAddress')");
        }

        /// <summary>
        /// Verifies constructor throws ArgumentException if UserAgent is null, empty, or whitespace.
        /// </summary>
        /// <param name="invalidUserAgent">The invalid user agent value.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public void Constructor_ShouldThrowArgumentException_WhenUserAgentIsInvalid(string? invalidUserAgent)
        {
            // Act
            // Uses null-forgiving operator (!) for the null test case.
            Action act = () => CreateValidRefreshTokenEntity(userAgent: invalidUserAgent!);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("The user agent is required. (Parameter 'userAgent')");
        }

        /// <summary>
        /// Verifies constructor successfully creates an instance and assigns properties correctly
        /// when all arguments are valid, including optional nulls for nullable properties.
        /// </summary>
        [Fact]
        public void Constructor_ShouldCreateInstance_WhenValidArgumentsProvidedIncludingNulls()
        {
            // Arrange
            int expectedId = 2;
            long expectedSession = 9876543210L;
            string expectedToken = "another-valid-token";
            string expectedDeviceInfo = "iPhone iOS 17";
            string expectedIp = "10.0.0.1";
            string expectedUserAgent = "App/1.0";
            DateTime expectedCreated = DateTime.UtcNow.AddHours(-2);
            DateTime expectedExpires = DateTime.UtcNow.AddDays(14);
            DateTime? nullRevokedAt = null; // Explicitly testing null optional value
            string? nullRevokedByIp = null; // Explicitly testing null optional value
            string? nullReplacedBy = null;  // Explicitly testing null optional value
            int? expectedPersonId = 202;

            // Act
            var refreshToken = new RefreshTokenEntity(
                expectedId,
                expectedSession,
                expectedToken,
                expectedDeviceInfo,
                expectedIp,
                expectedUserAgent,
                expectedCreated,
                expectedExpires,
                nullRevokedAt,
                nullRevokedByIp,
                nullReplacedBy,
                expectedPersonId
            );

            // Assert
            refreshToken.IdRefreshToken.Should().Be(expectedId);
            refreshToken.Session.Should().Be(expectedSession);
            refreshToken.Token.Should().Be(expectedToken);
            refreshToken.DeviceInfo.Should().Be(expectedDeviceInfo);
            refreshToken.IpAddress.Should().Be(expectedIp);
            refreshToken.UserAgent.Should().Be(expectedUserAgent);
            refreshToken.CreatedAt.Should().Be(expectedCreated);
            refreshToken.ExpiresAt.Should().Be(expectedExpires);
            refreshToken.RevokedAt.Should().BeNull();          // Verifies null was assigned
            refreshToken.RevokedByIp.Should().BeNull();        // Verifies null was assigned
            refreshToken.ReplacedByToken.Should().BeNull();    // Verifies null was assigned
            refreshToken.PersonId.Should().Be(expectedPersonId);
        }

        /// <summary>
        /// Verifies constructor successfully creates an instance when the optional PersonId is null.
        /// </summary>
        [Fact]
        public void Constructor_ShouldCreateInstance_WhenPersonIdIsNull()
        {
            // Arrange
            int? nullPersonId = null; // Explicitly testing null for PersonId

            // Act
            // Uses the helper method with the null PersonId.
            var refreshToken = CreateValidRefreshTokenEntity(personId: nullPersonId);

            // Assert
            refreshToken.PersonId.Should().BeNull(); // Verifies PersonId is correctly assigned as null
        }

        // --- Method Tests ---

        /// <summary>
        /// Verifies that the Revoke method correctly sets the RevokedAt and RevokedByIp properties.
        /// </summary>
        [Fact]
        public void Revoke_ShouldSetRevokedAtAndRevokedByIp()
        {
            // Arrange
            var refreshToken = CreateValidRefreshTokenEntity(); // Creates a default valid token
            var revocationTime = DateTime.UtcNow;
            string revocationIp = "172.16.0.5";

            // Act
            refreshToken.Revoke(revocationTime, revocationIp);

            // Assert
            refreshToken.RevokedAt.Should().Be(revocationTime);
            refreshToken.RevokedByIp.Should().Be(revocationIp);
            refreshToken.ReplacedByToken.Should().BeNull(); // Should not be affected by Revoke
        }

        /// <summary>
        /// Verifies that the Revoke method correctly sets RevokedAt and handles a null RevokedByIp argument.
        /// </summary>
        [Fact]
        public void Revoke_ShouldSetRevokedAtAndHandleNullRevokedByIp()
        {
            // Arrange
            var refreshToken = CreateValidRefreshTokenEntity();
            var revocationTime = DateTime.UtcNow.AddSeconds(-10);
            string? nullRevocationIp = null; // Test with null IP

            // Act
            refreshToken.Revoke(revocationTime, nullRevocationIp);

            // Assert
            refreshToken.RevokedAt.Should().Be(revocationTime);
            refreshToken.RevokedByIp.Should().BeNull(); // Verifies null IP is handled correctly
        }

        /// <summary>
        /// Verifies that the Replace method correctly sets the ReplacedByToken property.
        /// </summary>
        [Fact]
        public void Replace_ShouldSetReplacedByToken()
        {
            // Arrange
            var refreshToken = CreateValidRefreshTokenEntity();
            string replacementToken = "new-token-that-replaced-this-one";

            // Act
            refreshToken.Replace(replacementToken);

            // Assert
            refreshToken.ReplacedByToken.Should().Be(replacementToken);
            refreshToken.RevokedAt.Should().BeNull();     // Should not be affected by Replace
            refreshToken.RevokedByIp.Should().BeNull();   // Should not be affected by Replace
        }
    }
}