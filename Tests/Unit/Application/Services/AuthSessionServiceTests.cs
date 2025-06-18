using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Application.Services;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;
using System.Security.Cryptography;


namespace ArandanoIRT_Backend.Tests.Application.Services
{
    /// <summary>
    /// Contains unit tests for the <see cref="AuthSessionService"/> class.
    /// </summary>
    public class AuthSessionServiceTests : IDisposable
    {
        // Mocks
        /// <summary> Mock for authentication utilities dependency. </summary>
        private readonly Mock<IAuthUtilities> _mockAuthUtilities;
        /// <summary> Mock for the person repository dependency. </summary>
        private readonly Mock<IPersonRepository> _mockPersonRepository;
        /// <summary> Mock for the refresh token repository dependency. </summary>
        private readonly Mock<IRefreshTokenRepository> _mockRefreshTokenRepository;
        /// <summary> Mock for the failed login attempt repository dependency. </summary>
        private readonly Mock<IFailedLoginAttemptRepository> _mockFailedLoginAttemptRepository;
        /// <summary> Mock for the token service dependency. </summary>
        private readonly Mock<ITokenService> _mockTokenService;
        /// <summary> Mock for the RSA encryption/decryption service dependency. </summary>
        private readonly Mock<IRsaService> _mockRsaService;
        /// <summary> Mock for the password hashing service dependency. </summary>
        private readonly Mock<IPasswordHasher> _mockPasswordHasher;
        /// <summary> Mock for the email service dependency. </summary>
        private readonly Mock<IEmailService> _mockEmailService;
        /// <summary> Mock for the date time provider dependency. </summary>
        private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
        /// <summary> Mock for the logger dependency. </summary>
        private readonly Mock<ILogger<AuthSessionService>> _mockLogger;

        // SUT (System Under Test)
        /// <summary> The instance of the service being tested. </summary>
        private readonly AuthSessionService _authSessionService;

        // Common data for tests
        /// <summary> A fixed point in time for deterministic tests. </summary>
        private readonly DateTime _fixedUtcNow = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        /// <summary> A valid email address used in tests. </summary>
        private readonly string _validEmail = "test@example.com";
        /// <summary> A sample encrypted password string. </summary>
        private readonly string _validPasswordEncrypted = "encryptedPassword";
        /// <summary> The expected decrypted plain text password for valid scenarios. </summary>
        private readonly string _validPasswordDecrypted = "plainPassword123";
        /// <summary> A decrypted plain text password used for invalid password scenarios. </summary>
        private readonly string _invalidPasswordDecrypted = "wrongPassword";
        /// <summary> A sample hashed password stored for the test user. </summary>
        private readonly string _hashedPassword = "hashedPassword";
        /// <summary> A sample IP address used in tests. </summary>
        private readonly string _ipAddress = "127.0.0.1";
        /// <summary> A sample User-Agent string used in tests. </summary>
        private readonly string _userAgent = "TestAgent/1.0";
        /// <summary> Sample device information used in tests. </summary>
        private readonly string _deviceInfo = "Test Device";
        /// <summary> The ID of the default test person. </summary>
        private readonly int _personId = 1;
        /// <summary> A common PersonEntity instance used across tests. </summary>
        private readonly PersonEntity _personEntity;
        /// <summary> A sample successful token response DTO. </summary>
        private readonly TokenResponseDto _generatedTokens;
        /// <summary> A valid refresh token string. </summary>
        private readonly string _validRefreshToken = "validRefreshTokenValue";
        /// <summary> A sample session ID used in tests. </summary>
        private readonly long _sessionId = 123456789;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthSessionServiceTests"/> class,
        /// setting up all mock dependencies and the service under test.
        /// </summary>
        public AuthSessionServiceTests()
        {
            _mockAuthUtilities = new Mock<IAuthUtilities>();
            _mockPersonRepository = new Mock<IPersonRepository>();
            _mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();
            _mockFailedLoginAttemptRepository = new Mock<IFailedLoginAttemptRepository>();
            _mockTokenService = new Mock<ITokenService>();
            _mockRsaService = new Mock<IRsaService>();
            _mockPasswordHasher = new Mock<IPasswordHasher>();
            _mockEmailService = new Mock<IEmailService>();
            _mockDateTimeProvider = new Mock<IDateTimeProvider>();
            _mockLogger = new Mock<ILogger<AuthSessionService>>();

            // Configures the mock time provider to return the fixed date.
            _mockDateTimeProvider.Setup(dtp => dtp.GetUtcNow()).Returns(_fixedUtcNow);

            // Initializes a common person entity for use in tests.
            _personEntity = new PersonEntity(
               idPerson: _personId, firstName: "Test", lastName: "User", email: _validEmail, password: _hashedPassword,
               createdAt: _fixedUtcNow.AddDays(-10), isAdmin: false, allNotifications: true, cropId: 1);

            // Initializes a common token response DTO used in successful scenarios.
            _generatedTokens = new TokenResponseDto { AccessToken = "newAccessToken", RefreshToken = "newRefreshToken", AccessTokenExpiration = _fixedUtcNow.AddHours(1) };

            // Instantiates the System Under Test (SUT) with mock objects.
            _authSessionService = new AuthSessionService(
               _mockAuthUtilities.Object, _mockPersonRepository.Object, _mockRefreshTokenRepository.Object,
               _mockFailedLoginAttemptRepository.Object, _mockRsaService.Object, _mockPasswordHasher.Object,
               _mockTokenService.Object, _mockEmailService.Object, _mockDateTimeProvider.Object, _mockLogger.Object);
        }

        /// <summary>
        /// Disposes resources used by the test context, if any.
        /// Currently relies on GC but could implement IDisposable pattern if needed.
        /// </summary>
        public void Dispose()
        {
            // No explicit disposable resources in this setup, but good practice to include.
            GC.SuppressFinalize(this);
        }


        // Helper method to configure mocks for successful login prerequisites.
        /// <summary>
        /// Sets up mock dependencies for a successful login scenario,
        /// excluding token generation which is tested separately.
        /// </summary>
        private void SetupSuccessfulLoginPrerequisites()
        {
            // Configures FindUserByEmailAsync to return the test user.
            _mockAuthUtilities.Setup(auth => auth.FindUserByEmailAsync(_validEmail))
                             .ReturnsAsync(Result<PersonEntity>.Success(_personEntity));
            // Configures RSA decryption to return the valid plain text password.
            _mockRsaService.Setup(rsa => rsa.Decrypt(_validPasswordEncrypted))
                           .Returns(_validPasswordDecrypted);
            // Configures password verification to return true (valid password).
            _mockPasswordHasher.Setup(h => h.Verify(_hashedPassword, _validPasswordDecrypted))
                               .Returns(true);
            // GetRecentAttemptsAsync is not configured here as it is not called on the success path.
            // Configures failed attempt creation mock (though not called on success path).
            _mockFailedLoginAttemptRepository.Setup(repo => repo.Create(It.IsAny<FailedLoginAttemptEntity>()))
                                             .ReturnsAsync((FailedLoginAttemptEntity fa) => Result<FailedLoginAttemptEntity>.Success(fa));
            // Configures person update (e.g., LastLoginAt) to succeed.
            _mockPersonRepository.Setup(repo => repo.Update(It.Is<PersonEntity>(p => p.IdPerson == _personId)))
                                 .Returns(Task.FromResult(Result<bool>.Success(true)));
        }


        #region LoginAsync Tests

        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsSuccessPayload()
        {
            // Arrange
            var request = new LoginRequestDto { Email = _validEmail, Password = _validPasswordEncrypted };
            // Includes setting password verification to true via the helper.
            SetupSuccessfulLoginPrerequisites();
            // Configures token generation to succeed.
            _mockTokenService.Setup(ts => ts.GenerateAndStoreTokensAsync(_personEntity, _ipAddress, _userAgent, null))
                             .ReturnsAsync(Result<TokenResponseDto>.Success(_generatedTokens));

            // Act
            var result = await _authSessionService.LoginAsync(request, _ipAddress, _userAgent);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();

            // Verify calls occurred as expected for a successful login.
            _mockAuthUtilities.Verify(auth => auth.FindUserByEmailAsync(_validEmail), Times.Once);
            _mockRsaService.Verify(rsa => rsa.Decrypt(_validPasswordEncrypted), Times.Once);
            _mockPasswordHasher.Verify(h => h.Verify(_hashedPassword, _validPasswordDecrypted), Times.Once);
            _mockFailedLoginAttemptRepository.Verify(repo => repo.GetRecentAttemptsAsync(_personId, It.IsAny<DateTime>()), Times.Never); // Not called on success
            _mockFailedLoginAttemptRepository.Verify(repo => repo.Create(It.IsAny<FailedLoginAttemptEntity>()), Times.Never); // Not called on success
            _mockPersonRepository.Verify(repo => repo.Update(It.Is<PersonEntity>(p => p.IdPerson == _personId && p.LastLoginAt == _fixedUtcNow)), Times.Once);
            _mockTokenService.Verify(ts => ts.GenerateAndStoreTokensAsync(_personEntity, _ipAddress, _userAgent, null), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new LoginRequestDto { Email = "notfound@example.com", Password = _validPasswordEncrypted };
            var expectedError = "Invalid email or password."; // Standard error message
            // Configures FindUserByEmailAsync to return failure (user not found).
            _mockAuthUtilities.Setup(auth => auth.FindUserByEmailAsync(request.Email)).ReturnsAsync(Result<PersonEntity>.Failure("User not found."));

            // Act
            var result = await _authSessionService.LoginAsync(request, _ipAddress, _userAgent);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Be(expectedError);

            // Verify only FindUserByEmailAsync was called.
            _mockAuthUtilities.Verify(auth => auth.FindUserByEmailAsync(request.Email), Times.Once);
            _mockRsaService.Verify(rsa => rsa.Decrypt(It.IsAny<string>()), Times.Never);
            _mockPasswordHasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockFailedLoginAttemptRepository.Verify(repo => repo.Create(It.IsAny<FailedLoginAttemptEntity>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_DecryptionFails_ReturnsFailureAndRecordsAttempt()
        {
            // Arrange
            var request = new LoginRequestDto { Email = _validEmail, Password = "badEncryptedPassword" };
            var expectedError = "Invalid email or password.";
            // Configures FindUserByEmailAsync to succeed.
            _mockAuthUtilities.Setup(auth => auth.FindUserByEmailAsync(_validEmail)).ReturnsAsync(Result<PersonEntity>.Success(_personEntity));
            // Configures RSA decryption to throw an exception.
            _mockRsaService.Setup(rsa => rsa.Decrypt(request.Password)).Throws(new CryptographicException("Decryption failed"));
            // Configures GetRecentAttempts for the warning check after failure.
            _mockFailedLoginAttemptRepository.Setup(repo => repo.GetRecentAttemptsAsync(It.IsAny<int>(), It.IsAny<DateTime>())).ReturnsAsync(Result<IEnumerable<FailedLoginAttemptEntity>>.Success(new List<FailedLoginAttemptEntity>()));
            // Configures Create attempt mock to return success.
            _mockFailedLoginAttemptRepository.Setup(repo => repo.Create(It.IsAny<FailedLoginAttemptEntity>())).ReturnsAsync((FailedLoginAttemptEntity fa) => Result<FailedLoginAttemptEntity>.Success(fa));

            // Act
            var result = await _authSessionService.LoginAsync(request, _ipAddress, _userAgent);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Be(expectedError);

            // Verify failed attempt was recorded and password hashing was not reached.
            _mockFailedLoginAttemptRepository.Verify(repo => repo.Create(It.Is<FailedLoginAttemptEntity>(fa => fa.PersonId == _personId && fa.IpAddress == _ipAddress)), Times.Once);
            _mockPasswordHasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockTokenService.Verify(ts => ts.GenerateAndStoreTokensAsync(It.IsAny<PersonEntity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_IncorrectPassword_ReturnsFailureAndRecordsAttempt()
        {
            // Arrange
            var request = new LoginRequestDto { Email = _validEmail, Password = _validPasswordEncrypted };
            var expectedError = "Invalid email or password.";
            // Configures FindUserByEmailAsync to succeed.
            _mockAuthUtilities.Setup(auth => auth.FindUserByEmailAsync(_validEmail)).ReturnsAsync(Result<PersonEntity>.Success(_personEntity));
            // Configures RSA decryption to return the incorrect plain text password.
            _mockRsaService.Setup(rsa => rsa.Decrypt(_validPasswordEncrypted)).Returns(_invalidPasswordDecrypted);
            // Configures password verification to return false.
            _mockPasswordHasher.Setup(h => h.Verify(_hashedPassword, _invalidPasswordDecrypted)).Returns(false);
            // Configures mock needed for the subsequent warning check logic.
            _mockFailedLoginAttemptRepository.Setup(repo => repo.GetRecentAttemptsAsync(It.IsAny<int>(), It.IsAny<DateTime>())).ReturnsAsync(Result<IEnumerable<FailedLoginAttemptEntity>>.Success(new List<FailedLoginAttemptEntity>()));
            // Configures Create attempt mock to return success.
            _mockFailedLoginAttemptRepository.Setup(repo => repo.Create(It.IsAny<FailedLoginAttemptEntity>())).ReturnsAsync((FailedLoginAttemptEntity fa) => Result<FailedLoginAttemptEntity>.Success(fa));
            // Configures email service mock (though not expected to be called here unless threshold met).
            _mockEmailService.Setup(svc => svc.SendEmailAsync(_validEmail, It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(Result.Success()));

            // Act
            var result = await _authSessionService.LoginAsync(request, _ipAddress, _userAgent);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Be(expectedError);

            // Verify password hashing was called and failed attempt was recorded.
            _mockPasswordHasher.Verify(h => h.Verify(_hashedPassword, _invalidPasswordDecrypted), Times.Once);
            _mockFailedLoginAttemptRepository.Verify(repo => repo.Create(It.Is<FailedLoginAttemptEntity>(fa => fa.PersonId == _personId)), Times.Once);
            // Verify token generation was not reached.
            _mockTokenService.Verify(ts => ts.GenerateAndStoreTokensAsync(It.IsAny<PersonEntity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            // Verify suspicious activity email was NOT sent (assuming threshold not met).
            _mockEmailService.Verify(svc => svc.SendEmailAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains("Suspicious")), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_FailedAttemptThresholdMet_SendsWarningEmail()
        {
            // Arrange
            var request = new LoginRequestDto { Email = _validEmail, Password = _validPasswordEncrypted };
            int threshold = 5; // Assumed threshold for warning email
            // Creates a list representing threshold - 1 existing recent attempts.
            var existingAttempts = Enumerable.Range(0, threshold - 1)
                .Select(i => new FailedLoginAttemptEntity(i, _fixedUtcNow.AddMinutes(-(i + 1)), _ipAddress, _deviceInfo ?? "Unknown", _userAgent ?? "Unknown", _personId))
                .ToList();

            // Setup mocks for failed password verification path.
            _mockAuthUtilities.Setup(auth => auth.FindUserByEmailAsync(_validEmail)).ReturnsAsync(Result<PersonEntity>.Success(_personEntity));
            _mockRsaService.Setup(rsa => rsa.Decrypt(_validPasswordEncrypted)).Returns(_invalidPasswordDecrypted);
            // Configures password verification to return false (incorrect password).
            _mockPasswordHasher.Setup(h => h.Verify(_hashedPassword, _invalidPasswordDecrypted)).Returns(false);

            // Setup GetRecentAttemptsAsync to return the existing attempts (threshold - 1).
            _mockFailedLoginAttemptRepository.Setup(repo => repo.GetRecentAttemptsAsync(_personId, It.IsAny<DateTime>()))
                                             .ReturnsAsync(Result<IEnumerable<FailedLoginAttemptEntity>>.Success(existingAttempts));
            // Setup Create attempt to succeed (this will be the threshold-th attempt).
            _mockFailedLoginAttemptRepository.Setup(repo => repo.Create(It.IsAny<FailedLoginAttemptEntity>()))
                                             .ReturnsAsync((FailedLoginAttemptEntity fa) => Result<FailedLoginAttemptEntity>.Success(fa));

            // Setup mocks for email generation and sending.
            _mockEmailService.Setup(svc => svc.GenerateSuspiciousActivityBody(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()))
                           .Returns("Warning Body");
            // Configures SendEmailAsync mock to return Task<Result>.
            _mockEmailService.Setup(svc => svc.SendEmailAsync(_validEmail, It.IsAny<string>(), It.IsAny<string>()))
                           .Returns(Task.FromResult(Result.Success()));

            // Act
            var result = await _authSessionService.LoginAsync(request, _ipAddress, _userAgent, _deviceInfo);

            // Assert
            // Login still fails because the password was incorrect.
            result.IsFailure.Should().BeTrue();

            // Verify failed attempt was recorded.
            _mockFailedLoginAttemptRepository.Verify(repo => repo.Create(It.Is<FailedLoginAttemptEntity>(fa => fa.PersonId == _personId)), Times.Once);
            // Verify GetRecentAttempts was called to check the threshold.
            _mockFailedLoginAttemptRepository.Verify(repo => repo.GetRecentAttemptsAsync(_personId, It.IsAny<DateTime>()), Times.Once);
            // Verify warning email was generated and sent because the threshold was met.
            _mockEmailService.Verify(svc => svc.GenerateSuspiciousActivityBody(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()), Times.Never);
            _mockEmailService.Verify(svc => svc.SendEmailAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains("Suspicious")), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_TokenGenerationFails_ReturnsFailure()
        {
            // Arrange
            var request = new LoginRequestDto { Email = _validEmail, Password = _validPasswordEncrypted };
            var tokenError = "Token service unavailable";
            // Setup prerequisites for a successful login up to token generation.
            SetupSuccessfulLoginPrerequisites();
            // Configures token generation to fail.
            _mockTokenService.Setup(ts => ts.GenerateAndStoreTokensAsync(_personEntity, _ipAddress, _userAgent, null))
                             .ReturnsAsync(Result<TokenResponseDto>.Failure(tokenError));

            // Act
            var result = await _authSessionService.LoginAsync(request, _ipAddress, _userAgent);

            // Assert
            result.IsFailure.Should().BeTrue();
            // Matches the expected user-facing error message for this scenario.
            result.ErrorMessage.Should().Be("Login succeeded but failed to create session tokens. Please try again.");

            // Verify token generation was attempted.
            _mockTokenService.Verify(ts => ts.GenerateAndStoreTokensAsync(_personEntity, _ipAddress, _userAgent, null), Times.Once);
            // Verify Person Update (LastLogin) still happened before token failure.
            _mockPersonRepository.Verify(repo => repo.Update(It.Is<PersonEntity>(p => p.IdPerson == _personId && p.LastLoginAt == _fixedUtcNow)), Times.Once);
        }

        #endregion

        #region RefreshTokenAsync Tests

        [Fact]
        public async Task RefreshTokenAsync_ValidToken_ReturnsSuccessFromTokenService()
        {
            // Arrange
            // Configures the token service's RefreshTokensAsync to succeed.
            _mockTokenService.Setup(ts => ts.RefreshTokensAsync(_validRefreshToken, _ipAddress, _userAgent, _deviceInfo))
                             .ReturnsAsync(Result<TokenResponseDto>.Success(_generatedTokens));

            // Act
            var result = await _authSessionService.RefreshTokenAsync(_validRefreshToken, _ipAddress, _userAgent, _deviceInfo);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEquivalentTo(_generatedTokens);

            // Verify the token service was called correctly.
            _mockTokenService.Verify(ts => ts.RefreshTokensAsync(_validRefreshToken, _ipAddress, _userAgent, _deviceInfo), Times.Once);
        }

        [Fact]
        public async Task RefreshTokenAsync_TokenServiceFails_ReturnsFailure()
        {
            // Arrange
            var tokenError = "Invalid refresh token";
            // Configures the token service's RefreshTokensAsync to fail.
            _mockTokenService.Setup(ts => ts.RefreshTokensAsync(_validRefreshToken, _ipAddress, _userAgent, null))
                             .ReturnsAsync(Result<TokenResponseDto>.Failure(tokenError));

            // Act
            var result = await _authSessionService.RefreshTokenAsync(_validRefreshToken, _ipAddress, _userAgent);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Be(tokenError); // Passes through the error message

            // Verify the token service was called correctly.
            _mockTokenService.Verify(ts => ts.RefreshTokensAsync(_validRefreshToken, _ipAddress, _userAgent, null), Times.Once);
        }

        #endregion

        #region LogoutAsync Tests

        [Fact]
        public async Task LogoutAsync_ValidToken_ReturnsSuccessFromTokenService()
        {
            // Arrange
            // Configures the token service's RevokeRefreshTokenAsync to succeed.
            _mockTokenService.Setup(ts => ts.RevokeRefreshTokenAsync(_validRefreshToken, _ipAddress))
                           .Returns(Task.FromResult(Result.Success()));

            // Act
            var result = await _authSessionService.LogoutAsync(_validRefreshToken, _ipAddress);

            // Assert
            result.IsSuccess.Should().BeTrue();

            // Verify the token service was called correctly.
            _mockTokenService.Verify(ts => ts.RevokeRefreshTokenAsync(_validRefreshToken, _ipAddress), Times.Once);
        }

        [Fact]
        public async Task LogoutAsync_TokenServiceFails_ReturnsFailure()
        {
            // Arrange
            var revokeError = "Token not found";
            // Configures the token service's RevokeRefreshTokenAsync to fail.
            _mockTokenService.Setup(ts => ts.RevokeRefreshTokenAsync(_validRefreshToken, _ipAddress))
                           .Returns(Task.FromResult(Result.Failure(revokeError)));

            // Act
            var result = await _authSessionService.LogoutAsync(_validRefreshToken, _ipAddress);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Be(revokeError); // Passes through the error message

            // Verify the token service was called correctly.
            _mockTokenService.Verify(ts => ts.RevokeRefreshTokenAsync(_validRefreshToken, _ipAddress), Times.Once);
        }

        #endregion

        #region LogoutEverywhereAsync Tests

        [Fact]
        public async Task LogoutEverywhereAsync_ValidSessionId_ReturnsSuccessFromRepo()
        {
            // Arrange
            // Configures the refresh token repository's RevokeBySessionIdAsync to succeed.
            _mockRefreshTokenRepository.Setup(repo => repo.RevokeBySessionIdAsync(_sessionId, _ipAddress))
                                       .Returns(Task.FromResult(Result<bool>.Success(true)));

            // Act
            var result = await _authSessionService.LogoutEverywhereAsync(_sessionId, _ipAddress);

            // Assert
            result.IsSuccess.Should().BeTrue();

            // Verify the repository method was called correctly.
            _mockRefreshTokenRepository.Verify(repo => repo.RevokeBySessionIdAsync(_sessionId, _ipAddress), Times.Once);
        }

        [Fact]
        public async Task LogoutEverywhereAsync_RepoFails_ReturnsFailure()
        {
            // Arrange
            var repoError = "DB error during revoke all";
            // Configures the refresh token repository's RevokeBySessionIdAsync to fail.
            _mockRefreshTokenRepository.Setup(repo => repo.RevokeBySessionIdAsync(_sessionId, _ipAddress))
                                       .Returns(Task.FromResult(Result<bool>.Failure(repoError)));

            // Act
            var result = await _authSessionService.LogoutEverywhereAsync(_sessionId, _ipAddress);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Be(repoError); // Passes through the repository error message

            // Verify the repository method was called correctly.
            _mockRefreshTokenRepository.Verify(repo => repo.RevokeBySessionIdAsync(_sessionId, _ipAddress), Times.Once);
        }

        #endregion
    }
}