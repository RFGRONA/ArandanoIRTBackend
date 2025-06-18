using ArandanoIRT_Backend.Application.Interfaces.Utilities; 
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects; 
using ArandanoIRT_Backend.Infrastructure.Services; 
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens; 
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims; 
using Xunit;


namespace ArandanoIRT_Backend.Tests.Unit.Infrastructure.Services
{
    /// <summary>
    /// Contains unit tests for the <see cref="JwtTokenService"/> class.
    /// </summary>
    public class JwtTokenServiceTests
    {
        // Mocks for dependencies
        /// <summary> Mock for the application configuration dependency. </summary>
        private readonly Mock<IConfiguration> _mockConfiguration;
        /// <summary> Mock for the refresh token repository dependency. </summary>
        private readonly Mock<IRefreshTokenRepository> _mockRefreshTokenRepository;
        /// <summary> Mock for the date time provider dependency. </summary>
        private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
        /// <summary> Mock for the person repository dependency. </summary>
        private readonly Mock<IPersonRepository> _mockPersonRepository;

        // Instance of the class under test
        /// <summary> The instance of the token service being tested. </summary>
        private readonly JwtTokenService _tokenService;

        // Consistent date/time for testing
        /// <summary> A fixed UTC timestamp used for predictable date/time operations in tests. </summary>
        private readonly DateTime _fixedUtcNow = new DateTime(2024, 05, 20, 10, 0, 0, DateTimeKind.Utc);
        /// <summary> Default IP address used in tests. </summary>
        private const string DEFAULT_IP = "192.168.1.100";
        /// <summary> Default User-Agent string used in tests. </summary>
        private const string DEFAULT_UA = "Test User Agent";
        /// <summary> Default device information string used in tests. </summary>
        private const string DEFAULT_DEVICE = "Test Device Info";
        /// <summary> Default user ID for test entities. </summary>
        private const int DEFAULT_USER_ID = 1;
        /// <summary> Default crop ID for test entities. </summary>
        private const int DEFAULT_CROP_ID = 5;
        /// <summary> A fake JWT signing key for testing purposes. Must meet length requirements for the algorithm (HS256). </summary>
        private const string FAKE_JWT_KEY = "SuperSecretTestKeyMustBeLongEnoughForHmacSha256";


        /// <summary>
        /// Initializes a new instance of the <see cref="JwtTokenServiceTests"/> class.
        /// Sets up mock dependencies (Configuration, Repositories, DateTimeProvider)
        /// and instantiates the <see cref="JwtTokenService"/> for testing.
        /// </summary>
        public JwtTokenServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();
            _mockDateTimeProvider = new Mock<IDateTimeProvider>();
            _mockPersonRepository = new Mock<IPersonRepository>();

            // Sets up the IConfiguration mock to return specific values for JWT settings.
            // Mocks retrieval via specific keys.
            _mockConfiguration.Setup(c => c["Jwt:Key"]).Returns(FAKE_JWT_KEY);
            _mockConfiguration.Setup(c => c["Jwt:ExpiryMinutes"]).Returns("15"); // Example access token expiry
            _mockConfiguration.Setup(c => c["Jwt:RefreshExpiryDays"]).Returns("7"); // Example refresh token expiry

            // Sets up the IDateTimeProvider mock to return a fixed UTC time.
            _mockDateTimeProvider.Setup(dtp => dtp.GetUtcNow()).Returns(_fixedUtcNow);

            // Instantiates the service under test with the mocked dependencies.
            // Uses NullLogger for simplicity in unit tests, avoiding the need to mock ILogger.
            _tokenService = new JwtTokenService(
                _mockConfiguration.Object,
                _mockRefreshTokenRepository.Object,
                _mockDateTimeProvider.Object,
                _mockPersonRepository.Object,
                NullLogger<JwtTokenService>.Instance
            );
        }

        /// <summary>
        /// Helper method to create a <see cref="PersonEntity"/> instance for tests.
        /// </summary>
        /// <param name="id">The person ID.</param>
        /// <param name="cropId">The associated crop ID (optional).</param>
        /// <param name="isAdmin">Whether the person is an admin.</param>
        /// <returns>A new <see cref="PersonEntity"/> instance.</returns>
        private PersonEntity CreateTestPerson(int id = DEFAULT_USER_ID, int? cropId = DEFAULT_CROP_ID, bool isAdmin = false)
        {
            // Note: Password hash is included but not directly used by token service generation logic.
            return new PersonEntity(
                id, "Test", "User", $"test{id}@example.com", "hashedPassword",
                _fixedUtcNow.AddDays(-10), isAdmin, true, cropId
            );
        }

        /// <summary>
        /// Helper method to create a <see cref="RefreshTokenEntity"/> instance for tests.
        /// </summary>
        /// <param name="id">The refresh token ID.</param>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="tokenValue">The refresh token string.</param>
        /// <param name="personId">The associated person ID.</param>
        /// <param name="createdAt">The creation timestamp.</param>
        /// <param name="expiresAt">The expiration timestamp.</param>
        /// <param name="revokedAt">The revocation timestamp (optional).</param>
        /// <param name="revokedByIp">The revoking IP address (optional).</param>
        /// <param name="replacedByToken">The replacing token (optional).</param>
        /// <returns>A new <see cref="RefreshTokenEntity"/> instance.</returns>
        private static RefreshTokenEntity CreateTestRefreshToken(
             int id, long sessionId, string tokenValue, int personId, DateTime createdAt, DateTime expiresAt,
             DateTime? revokedAt = null, string? revokedByIp = null, string? replacedByToken = null)
        {
            return new RefreshTokenEntity(
                 id, sessionId, tokenValue, DEFAULT_DEVICE, DEFAULT_IP, DEFAULT_UA,
                 createdAt, expiresAt, revokedAt, revokedByIp, replacedByToken, personId
             );
        }

        #region Tests for GenerateAndStoreTokensAsync

        /// <summary>
        /// Tests successful generation and storage of tokens for a valid user entity.
        /// Verifies the returned DTO and the interaction with the refresh token repository.
        /// </summary>
        [Fact]
        public async Task GenerateAndStoreTokensAsync_WithValidUser_ShouldReturnSuccessResultWithTokens()
        {
            // Arrange
            // Creates a person entity ensuring necessary details are present.
            var person = CreateTestPerson();
            var expectedAccessTokenExpiry = _fixedUtcNow.AddMinutes(15);
            var expectedRefreshTokenExpiry = _fixedUtcNow.AddDays(7);
            var createdTokenId = 123;
            var createdRefreshTokenValue = "mock-returned-token"; // Value returned by mock

            // Simulates the token entity that would be returned after successful creation.
            var simulatedCreatedToken = new RefreshTokenEntity(
                 createdTokenId,
                 It.IsAny<long>(),
                 createdRefreshTokenValue, // Uses the defined value for the mock return
                 DEFAULT_DEVICE, DEFAULT_IP, DEFAULT_UA,
                 _fixedUtcNow, expectedRefreshTokenExpiry, null, null, null, person.IdPerson
              );

            // Configures the mock repository's Create method.
            _mockRefreshTokenRepository.Setup(repo => repo.Create(It.IsAny<RefreshTokenEntity>()))
                .ReturnsAsync(Result<RefreshTokenEntity>.Success(simulatedCreatedToken));

            // Act
            var result = await _tokenService.GenerateAndStoreTokensAsync(person, DEFAULT_IP, DEFAULT_UA, DEFAULT_DEVICE);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.AccessToken.Should().NotBeNullOrWhiteSpace();
            // Verifies the service returns a non-empty refresh token.
            result.Value.RefreshToken.Should().NotBeNullOrWhiteSpace();
            // Note: We don't compare result.Value.RefreshToken to 'createdRefreshTokenValue'
            // because the *service* generates its own token internally before calling Create.
            // The important part is that *a* token is generated and returned.
            result.Value.AccessTokenExpiration.Should().BeCloseTo(expectedAccessTokenExpiry, TimeSpan.FromSeconds(1));

            // Verifies that the Create method was called once with the correct details.
            _mockRefreshTokenRepository.Verify(repo => repo.Create(It.Is<RefreshTokenEntity>(rt =>
                rt.PersonId == person.IdPerson &&
                rt.IpAddress == DEFAULT_IP &&
                rt.UserAgent == DEFAULT_UA &&
                rt.DeviceInfo == DEFAULT_DEVICE &&
                rt.ExpiresAt == expectedRefreshTokenExpiry &&
                rt.RevokedAt == null &&
                !string.IsNullOrWhiteSpace(rt.Token)
            )), Times.Once);

            // Decodes the generated access token to verify its claims.
            var handler = new JwtSecurityTokenHandler();
            var decodedToken = handler.ReadJwtToken(result.Value.AccessToken);
            decodedToken.Claims.Should().Contain(c => c.Type == "nameid" && c.Value == person.IdPerson.ToString());
            decodedToken.Claims.Should().Contain(c => c.Type == "CropId" && c.Value == person.CropId.ToString());
        }

        /// <summary>
        /// Tests that token generation fails when the provided PersonEntity is null.
        /// </summary>
        [Fact]
        public async Task GenerateAndStoreTokensAsync_WithNullPerson_ShouldReturnFailure()
        {
            // Arrange
            PersonEntity? person = null;

            // Act
            // Uses null-forgiving operator for the test setup.
            var result = await _tokenService.GenerateAndStoreTokensAsync(person!, DEFAULT_IP, DEFAULT_UA, DEFAULT_DEVICE);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("Person data cannot be null");

            // Verifies the repository was not called due to early validation failure.
            _mockRefreshTokenRepository.Verify(repo => repo.Create(It.IsAny<RefreshTokenEntity>()), Times.Never);
        }

        /// <summary>
        /// Tests that token generation fails if the RefreshTokenRepository's Create method fails.
        /// </summary>
        [Fact]
        public async Task GenerateAndStoreTokensAsync_WhenRepoCreateFails_ShouldReturnFailure()
        {
            // Arrange
            var person = CreateTestPerson(); // Uses a default valid person.
            var failureMessage = "DB error";
            // Configures the mock repository's Create method to return failure.
            _mockRefreshTokenRepository.Setup(repo => repo.Create(It.IsAny<RefreshTokenEntity>()))
                .ReturnsAsync(Result<RefreshTokenEntity>.Failure(failureMessage));

            // Act
            var result = await _tokenService.GenerateAndStoreTokensAsync(person, DEFAULT_IP, DEFAULT_UA, DEFAULT_DEVICE);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Verifies the expected error message for this specific failure condition.
            result.ErrorMessage.Should().Contain("Failed to save session information");

            // Verifies that the Create call was attempted.
            _mockRefreshTokenRepository.Verify(repo => repo.Create(It.IsAny<RefreshTokenEntity>()), Times.Once);
        }

        #endregion

        #region Tests for RevokeRefreshTokenAsync

        /// <summary>
        /// Tests successful token refresh using a valid, active (not expired, not revoked) refresh token.
        /// Verifies that new tokens are returned and the old refresh token is updated (revoked/replaced).
        /// </summary>
        [Fact]
        public async Task RefreshTokensAsync_WithValidActiveToken_ShouldReturnNewTokensAndRevokeOld()
        {
            // Arrange
            var person = CreateTestPerson();
            var originalSessionId = _fixedUtcNow.AddSeconds(-10).Ticks;
            var oldRefreshTokenValue = "validOldRefreshToken";
            // Creates an active refresh token entity.
            var oldRefreshToken = CreateTestRefreshToken(10, originalSessionId, oldRefreshTokenValue, person.IdPerson, _fixedUtcNow.AddDays(-1), _fixedUtcNow.AddDays(6));

            var expectedNewAccessTokenExpiry = _fixedUtcNow.AddMinutes(15);
            var expectedNewRefreshTokenExpiry = _fixedUtcNow.AddDays(7);
            var newTokenId = 124;
            // Placeholder for the new refresh token value from the mocked creation.
            var createdNewRefreshTokenValue = "dummy-new-refresh-token-refreshed";

            // Configures GetByTokenAsync to return the active token.
            _mockRefreshTokenRepository.Setup(repo => repo.GetByTokenAsync(oldRefreshTokenValue))
                .ReturnsAsync(Result<RefreshTokenEntity>.Success(oldRefreshToken));
            // Configures GetById to return the associated user.
            _mockPersonRepository.Setup(repo => repo.GetById(person.IdPerson))
                .ReturnsAsync(Result<PersonEntity>.Success(person));

            // Simulates the new token entity that would be returned after successful creation.
            var expectedNewToken = new RefreshTokenEntity(
                 newTokenId, originalSessionId, createdNewRefreshTokenValue, DEFAULT_DEVICE, DEFAULT_IP, DEFAULT_UA,
                 _fixedUtcNow, expectedNewRefreshTokenExpiry, null, null, null, person.IdPerson
              );

            // Configures the mock repository's Create method for the *new* token.
            _mockRefreshTokenRepository.Setup(repo => repo.Create(It.Is<RefreshTokenEntity>(rt => rt.Token != oldRefreshTokenValue)))
                .ReturnsAsync(Result<RefreshTokenEntity>.Success(expectedNewToken));

            // Configures the mock repository's Update method for the *old* token (revocation/replacement).
            _mockRefreshTokenRepository.Setup(repo => repo.Update(It.Is<RefreshTokenEntity>(rt => rt.IdRefreshToken == oldRefreshToken.IdRefreshToken)))
                .ReturnsAsync(Result<bool>.Success(true));

            // Act
            var result = await _tokenService.RefreshTokensAsync(oldRefreshTokenValue, DEFAULT_IP, DEFAULT_UA, DEFAULT_DEVICE);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.AccessToken.Should().NotBeNullOrWhiteSpace();
            result.Value.RefreshToken.Should().NotBeNullOrWhiteSpace().And.NotBe(oldRefreshTokenValue);
            result.Value.AccessTokenExpiration.Should().BeCloseTo(expectedNewAccessTokenExpiry, TimeSpan.FromSeconds(1));

            // Verifies the sequence of repository calls.
            _mockRefreshTokenRepository.Verify(repo => repo.GetByTokenAsync(oldRefreshTokenValue), Times.Once);
            _mockPersonRepository.Verify(repo => repo.GetById(person.IdPerson), Times.Once);
            // Verifies a new token was created for the same session and user.
            _mockRefreshTokenRepository.Verify(repo => repo.Create(It.Is<RefreshTokenEntity>(rt =>
                rt.Session == originalSessionId &&
                rt.Token != oldRefreshTokenValue &&
                rt.PersonId == person.IdPerson
             )), Times.Once);
            // Verifies the old token was updated (implicitly revoked/replaced).
            _mockRefreshTokenRepository.Verify(repo => repo.Update(It.Is<RefreshTokenEntity>(rt =>
                rt.IdRefreshToken == oldRefreshToken.IdRefreshToken &&
                rt.RevokedAt != null && // Checks if RevokedAt is set
                rt.ReplacedByToken != null // Checks if ReplacedByToken is set
            )), Times.Once);
        }

        /// <summary>
        /// Tests the refresh token process when the provided refresh token does not exist in the repository.
        /// </summary>
        [Fact]
        public async Task RefreshTokensAsync_WithNonExistentToken_ShouldReturnFailure()
        {
            // Arrange
            var nonExistentToken = "nonExistentToken123";
            // Configures GetByTokenAsync to return failure (token not found).
            _mockRefreshTokenRepository.Setup(repo => repo.GetByTokenAsync(nonExistentToken))
                .ReturnsAsync(Result<RefreshTokenEntity>.Failure("Not found"));

            // Act
            var result = await _tokenService.RefreshTokensAsync(nonExistentToken, DEFAULT_IP, DEFAULT_UA);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("Invalid session or token");
            // Verifies that subsequent operations (GetById, Create, Update) were not called.
            _mockPersonRepository.Verify(repo => repo.GetById(It.IsAny<int>()), Times.Never);
            _mockRefreshTokenRepository.Verify(repo => repo.Create(It.IsAny<RefreshTokenEntity>()), Times.Never);
            _mockRefreshTokenRepository.Verify(repo => repo.Update(It.IsAny<RefreshTokenEntity>()), Times.Never);
        }

        /// <summary>
        /// Tests the refresh token process when the provided refresh token has expired.
        /// </summary>
        [Fact]
        public async Task RefreshTokensAsync_WithExpiredToken_ShouldReturnFailure()
        {
            // Arrange
            var expiredTokenValue = "expiredTokenValue";
            // Creates a token entity with an expiration date in the past relative to _fixedUtcNow.
            var expiredToken = CreateTestRefreshToken(11, _fixedUtcNow.Ticks, expiredTokenValue, DEFAULT_USER_ID,
                                                  _fixedUtcNow.AddDays(-10), _fixedUtcNow.AddDays(-1));

            // Configures GetByTokenAsync to return the expired token.
            _mockRefreshTokenRepository.Setup(repo => repo.GetByTokenAsync(expiredTokenValue))
                .ReturnsAsync(Result<RefreshTokenEntity>.Success(expiredToken));

            // Act
            var result = await _tokenService.RefreshTokensAsync(expiredTokenValue, DEFAULT_IP, DEFAULT_UA);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("Session has expired");
            // Verifies that subsequent operations were not called.
            _mockPersonRepository.Verify(repo => repo.GetById(It.IsAny<int>()), Times.Never);
            _mockRefreshTokenRepository.Verify(repo => repo.Create(It.IsAny<RefreshTokenEntity>()), Times.Never);
            _mockRefreshTokenRepository.Verify(repo => repo.Update(It.IsAny<RefreshTokenEntity>()), Times.Never);
        }

        /// <summary>
        /// Tests the refresh token process when the provided refresh token has been revoked.
        /// </summary>
        [Fact]
        public async Task RefreshTokensAsync_WithRevokedToken_ShouldReturnFailure()
        {
            // Arrange
            var revokedTokenValue = "revokedTokenValue";
            // Creates a token entity that is not expired but has a RevokedAt timestamp set.
            var revokedToken = CreateTestRefreshToken(12, _fixedUtcNow.Ticks, revokedTokenValue, DEFAULT_USER_ID,
                                                  _fixedUtcNow.AddDays(-5), _fixedUtcNow.AddDays(5),
                                                  revokedAt: _fixedUtcNow.AddDays(-1), revokedByIp: "1.2.3.4");

            // Configures GetByTokenAsync to return the revoked token.
            _mockRefreshTokenRepository.Setup(repo => repo.GetByTokenAsync(revokedTokenValue))
                .ReturnsAsync(Result<RefreshTokenEntity>.Success(revokedToken));

            // Act
            var result = await _tokenService.RefreshTokensAsync(revokedTokenValue, DEFAULT_IP, DEFAULT_UA);

            // Assert
            result.IsFailure.Should().BeTrue();
            // Verifies the appropriate user-facing error message.
            result.ErrorMessage.Should().Contain("Session has been invalidated");
            // Verifies that subsequent operations were not called.
            _mockPersonRepository.Verify(repo => repo.GetById(It.IsAny<int>()), Times.Never);
            _mockRefreshTokenRepository.Verify(repo => repo.Create(It.IsAny<RefreshTokenEntity>()), Times.Never);
            _mockRefreshTokenRepository.Verify(repo => repo.Update(It.IsAny<RefreshTokenEntity>()), Times.Never);
        }

        /// <summary>
        /// Tests the refresh token process when the token is valid but the associated user account cannot be found.
        /// Expects the token to be revoked as a security measure.
        /// </summary>
        [Fact]
        public async Task RefreshTokensAsync_WithValidTokenButUserNotFound_ShouldReturnFailureAndRevokeToken()
        {
            // Arrange
            var validTokenValue = "validTokenUserNotFound";
            // Creates an active refresh token entity.
            var validToken = CreateTestRefreshToken(13, _fixedUtcNow.Ticks, validTokenValue, DEFAULT_USER_ID,
                                                _fixedUtcNow.AddDays(-2), _fixedUtcNow.AddDays(5));

            // Configures GetByTokenAsync to return the active token.
            _mockRefreshTokenRepository.Setup(repo => repo.GetByTokenAsync(validTokenValue))
                .ReturnsAsync(Result<RefreshTokenEntity>.Success(validToken));

            // Configures PersonRepository GetById to return failure (user not found).
            _mockPersonRepository.Setup(repo => repo.GetById(DEFAULT_USER_ID))
                .ReturnsAsync(Result<PersonEntity>.Failure("User not found"));

            // Configures the Update method for the token revocation triggered by the user not being found.
            _mockRefreshTokenRepository.Setup(repo => repo.Update(It.Is<RefreshTokenEntity>(rt => rt.IdRefreshToken == validToken.IdRefreshToken)))
                .ReturnsAsync(Result<bool>.Success(true));

            // Act
            var result = await _tokenService.RefreshTokensAsync(validTokenValue, DEFAULT_IP, DEFAULT_UA);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("Associated user account not found");

            // Verifies that the Update method was called to revoke the orphaned token,
            // capturing the IP address of the failed refresh attempt.
            _mockRefreshTokenRepository.Verify(repo => repo.Update(It.Is<RefreshTokenEntity>(rt =>
               rt.IdRefreshToken == validToken.IdRefreshToken &&
               rt.RevokedAt != null && // Checks that RevokedAt is set
               rt.RevokedByIp == DEFAULT_IP // Checks that the correct IP was recorded
            )), Times.Once);

            // Verifies no new token was created.
            _mockRefreshTokenRepository.Verify(repo => repo.Create(It.IsAny<RefreshTokenEntity>()), Times.Never);
        }

        #endregion

        #region Tests for RevokeRefreshTokenAsync

        /// <summary>
        /// Tests the successful revocation of a valid, active refresh token.
        /// </summary>
        [Fact]
        public async Task RevokeRefreshTokenAsync_WithValidActiveToken_ShouldReturnSuccessAndUpdateToken()
        {
            // Arrange
            var tokenToRevokeValue = "tokenToRevokeValue";
            // Creates an active token entity.
            var tokenToRevoke = CreateTestRefreshToken(14, _fixedUtcNow.Ticks, tokenToRevokeValue, DEFAULT_USER_ID,
                                                 _fixedUtcNow.AddDays(-2), _fixedUtcNow.AddDays(5));

            // Configures GetByTokenAsync to return the active token.
            _mockRefreshTokenRepository.Setup(repo => repo.GetByTokenAsync(tokenToRevokeValue))
                .ReturnsAsync(Result<RefreshTokenEntity>.Success(tokenToRevoke));

            // Configures the Update method to succeed.
            _mockRefreshTokenRepository.Setup(repo => repo.Update(It.Is<RefreshTokenEntity>(rt => rt.IdRefreshToken == tokenToRevoke.IdRefreshToken)))
                .ReturnsAsync(Result<bool>.Success(true));

            // Act
            var result = await _tokenService.RevokeRefreshTokenAsync(tokenToRevokeValue, DEFAULT_IP);

            // Assert
            result.IsSuccess.Should().BeTrue();

            // Verifies GetByTokenAsync and Update were called.
            _mockRefreshTokenRepository.Verify(repo => repo.GetByTokenAsync(tokenToRevokeValue), Times.Once);
            // Verifies the token passed to Update is the correct one and has been marked as revoked.
            _mockRefreshTokenRepository.Verify(repo => repo.Update(It.Is<RefreshTokenEntity>(rt =>
               rt.IdRefreshToken == tokenToRevoke.IdRefreshToken &&
               rt.RevokedAt != null && // Checks RevokedAt is set
               rt.RevokedByIp == DEFAULT_IP && // Checks RevokedByIp is set
               rt.ReplacedByToken == null // Verifies it wasn't marked as replaced
            )), Times.Once);
        }

        /// <summary>
        /// Tests that attempting to revoke a non-existent token returns success gracefully.
        /// </summary>
        [Fact]
        public async Task RevokeRefreshTokenAsync_WithNonExistentToken_ShouldReturnSuccess()
        {
            // Arrange
            var nonExistentToken = "nonExistentToken123";
            // Configures GetByTokenAsync to return failure (token not found).
            _mockRefreshTokenRepository.Setup(repo => repo.GetByTokenAsync(nonExistentToken))
                .ReturnsAsync(Result<RefreshTokenEntity>.Failure("Not found"));

            // Act
            var result = await _tokenService.RevokeRefreshTokenAsync(nonExistentToken, DEFAULT_IP);

            // Assert
            // Revoking a non-existent token is typically treated as success (idempotent).
            result.IsSuccess.Should().BeTrue();

            // Verifies Update was never called because the token wasn't found.
            _mockRefreshTokenRepository.Verify(repo => repo.Update(It.IsAny<RefreshTokenEntity>()), Times.Never);
        }

        /// <summary>
        /// Tests that attempting to revoke an already revoked token returns success gracefully.
        /// </summary>
        [Fact]
        public async Task RevokeRefreshTokenAsync_WithAlreadyRevokedToken_ShouldReturnSuccess()
        {
            // Arrange
            var alreadyRevokedValue = "alreadyRevokedValue";
            // Creates a token that is already marked as revoked.
            var alreadyRevokedToken = CreateTestRefreshToken(15, _fixedUtcNow.Ticks, alreadyRevokedValue, DEFAULT_USER_ID,
                                                         _fixedUtcNow.AddDays(-3), _fixedUtcNow.AddDays(4),
                                                         revokedAt: _fixedUtcNow.AddDays(-1));

            // Configures GetByTokenAsync to return the already revoked token.
            _mockRefreshTokenRepository.Setup(repo => repo.GetByTokenAsync(alreadyRevokedValue))
                .ReturnsAsync(Result<RefreshTokenEntity>.Success(alreadyRevokedToken));

            // Act
            var result = await _tokenService.RevokeRefreshTokenAsync(alreadyRevokedValue, DEFAULT_IP);

            // Assert
            // Revoking an already inactive (revoked) token is treated as success.
            result.IsSuccess.Should().BeTrue();

            // Verifies Update was never called because the token was already inactive.
            _mockRefreshTokenRepository.Verify(repo => repo.Update(It.IsAny<RefreshTokenEntity>()), Times.Never);
        }

        #endregion

        #region Tests for GetPrincipalFromExpiredToken

        /// <summary>
        /// Tests extracting claims principal from a JWT string that is structurally valid
        /// and signed correctly, even if the 'exp' claim indicates it's expired.
        /// </summary>
        [Fact]
        public void GetPrincipalFromExpiredToken_WithValidToken_ShouldReturnPrincipalWithClaims()
        {
            // Arrange
            var person = CreateTestPerson();
            var handler = new JwtSecurityTokenHandler();
            var keyBytes = System.Text.Encoding.ASCII.GetBytes(FAKE_JWT_KEY); // Uses the same key as the service
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, person.IdPerson.ToString()),
                new Claim("CropId", person.CropId.ToString()!) // Example custom claim
            };
            // Creates a token descriptor with claims and an expiration time in the past.
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                // Sets expiration to be before the fixed 'now' time used in tests.
                Expires = _fixedUtcNow.AddMinutes(-1),
                // Sets NotBefore explicitly to avoid clock skew issues during tests.
                NotBefore = _fixedUtcNow.AddMinutes(-5),
                IssuedAt = _fixedUtcNow.AddMinutes(-5),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = handler.CreateToken(tokenDescriptor);
            var tokenString = handler.WriteToken(token); // The JWT string

            // Act
            var principal = _tokenService.GetPrincipalFromExpiredToken(tokenString);

            // Assert
            principal.Should().NotBeNull();
            // Verifies the identity within the principal is marked as authenticated.
            principal!.Identity!.IsAuthenticated.Should().BeTrue();
            // Verifies specific claims can be extracted correctly.
            principal.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(person.IdPerson.ToString());
            principal.FindFirstValue("CropId").Should().Be(person.CropId.ToString());
        }

        /// <summary>
        /// Tests that GetPrincipalFromExpiredToken returns null if the token signature is invalid.
        /// </summary>
        [Fact]
        public void GetPrincipalFromExpiredToken_WithInvalidSignature_ShouldReturnNull()
        {
            // Arrange
            // Starts with a structurally valid JWT example.
            var tokenString = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
            // Tampers with the signature part of the token.
            int lastDotIndex = tokenString.LastIndexOf('.');
            if (lastDotIndex >= 0)
            {
                ReadOnlySpan<char> prefixSpan = tokenString.AsSpan(0, lastDotIndex);
                const string newSuffix = ".InvalidSignatureHere";
                tokenString = string.Concat(prefixSpan, newSuffix);
            }
            else
            {
                throw new ArgumentException("The token does not contain the expected '.' separator.", nameof(tokenString));
            }

            // Act
            var principal = _tokenService.GetPrincipalFromExpiredToken(tokenString);

            // Assert
            // Expects null because signature validation should fail.
            principal.Should().BeNull();
        }

        /// <summary>
        /// Tests that GetPrincipalFromExpiredToken returns null when the input token string is null.
        /// </summary>
        [Fact]
        public void GetPrincipalFromExpiredToken_WithNullToken_ShouldReturnNull()
        {
            // Act
            // Uses null-forgiving operator for the test.
            var principal = _tokenService.GetPrincipalFromExpiredToken(null!);

            // Assert
            principal.Should().BeNull();
        }

        /// <summary>
        /// Tests that GetPrincipalFromExpiredToken returns null when the input token string is empty.
        /// </summary>
        [Fact]
        public void GetPrincipalFromExpiredToken_WithEmptyToken_ShouldReturnNull()
        {
            // Act
            var principal = _tokenService.GetPrincipalFromExpiredToken("");

            // Assert
            principal.Should().BeNull();
        }

        /// <summary>
        /// Tests that GetPrincipalFromExpiredToken returns null when the input token string is malformed.
        /// </summary>
        [Fact]
        public void GetPrincipalFromExpiredToken_WithMalformedToken_ShouldReturnNull()
        {
            // Act
            var principal = _tokenService.GetPrincipalFromExpiredToken("this.is.not.a.jwt");

            // Assert
            // Expects null because the string cannot be parsed as a JWT.
            principal.Should().BeNull();
        }

        // --- Tests for GetSessionIdFromTokenAsync ---

        /// <summary>
        /// Tests retrieving the session ID from a valid, active (not expired, not revoked) refresh token.
        /// </summary>
        [Fact]
        public async Task GetSessionIdFromTokenAsync_WithValidActiveToken_ShouldReturnSessionId()
        {
            // Arrange
            var tokenValue = "validActiveTokenForSessionId";
            long expectedSessionId = 1234567890L;
            // Creates an active token entity.
            var activeToken = CreateTestRefreshToken(20, expectedSessionId, tokenValue, DEFAULT_USER_ID,
                                                 _fixedUtcNow.AddDays(-1), _fixedUtcNow.AddDays(1));

            // Configures GetByTokenAsync to return the active token.
            _mockRefreshTokenRepository.Setup(repo => repo.GetByTokenAsync(tokenValue))
                .ReturnsAsync(Result<RefreshTokenEntity>.Success(activeToken));

            // Act
            var result = await _tokenService.GetSessionIdFromTokenAsync(tokenValue);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(expectedSessionId);
        }

        /// <summary>
        /// Tests retrieving session ID when the provided refresh token does not exist.
        /// </summary>
        [Fact]
        public async Task GetSessionIdFromTokenAsync_WithNonExistentToken_ShouldReturnFailure()
        {
            // Arrange
            var nonExistentToken = "nonExistentTokenForSession";
            // Configures GetByTokenAsync to return failure (token not found).
            _mockRefreshTokenRepository.Setup(repo => repo.GetByTokenAsync(nonExistentToken))
                .ReturnsAsync(Result<RefreshTokenEntity>.Failure("Not Found"));

            // Act
            var result = await _tokenService.GetSessionIdFromTokenAsync(nonExistentToken);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("Refresh token not found");
        }

        /// <summary>
        /// Tests retrieving session ID when the provided refresh token has been revoked.
        /// </summary>
        [Fact]
        public async Task GetSessionIdFromTokenAsync_WithRevokedToken_ShouldReturnFailure()
        {
            // Arrange
            var revokedTokenValue = "revokedTokenForSession";
            long sessionId = 987654321L;
            // Creates a token that is not expired but is revoked.
            var revokedToken = CreateTestRefreshToken(21, sessionId, revokedTokenValue, DEFAULT_USER_ID,
                                                  _fixedUtcNow.AddDays(-2), _fixedUtcNow.AddDays(2),
                                                  revokedAt: _fixedUtcNow.AddHours(-5));

            // Configures GetByTokenAsync to return the revoked token.
            _mockRefreshTokenRepository.Setup(repo => repo.GetByTokenAsync(revokedTokenValue))
                .ReturnsAsync(Result<RefreshTokenEntity>.Success(revokedToken));

            // Act
            var result = await _tokenService.GetSessionIdFromTokenAsync(revokedTokenValue);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("Token has been revoked");
        }

        /// <summary>
        /// Tests retrieving session ID when the provided refresh token has expired.
        /// </summary>
        [Fact]
        public async Task GetSessionIdFromTokenAsync_WithExpiredToken_ShouldReturnFailure()
        {
            // Arrange
            var expiredTokenValue = "expiredTokenForSession";
            long sessionId = 1122334455L;
            // Creates a token that has expired.
            var expiredToken = CreateTestRefreshToken(22, sessionId, expiredTokenValue, DEFAULT_USER_ID,
                                                  _fixedUtcNow.AddDays(-10), _fixedUtcNow.AddDays(-1));

            // Configures GetByTokenAsync to return the expired token.
            _mockRefreshTokenRepository.Setup(repo => repo.GetByTokenAsync(expiredTokenValue))
                .ReturnsAsync(Result<RefreshTokenEntity>.Success(expiredToken));

            // Act
            var result = await _tokenService.GetSessionIdFromTokenAsync(expiredTokenValue);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("Token has expired");
        }

        #endregion
    }
}