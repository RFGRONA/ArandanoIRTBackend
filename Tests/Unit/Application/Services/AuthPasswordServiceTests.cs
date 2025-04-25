using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Application.Services;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects; 
using FluentAssertions;
using Moq;
using Xunit;

namespace ArandanoIRT_Backend.Tests.Application.Services
{
    /// <summary>
    /// Contains unit tests for the <see cref="AuthPasswordService"/> class.
    /// </summary>
    public class AuthPasswordServiceTests
    {
        // Mocks
        /// <summary> Mock for the person repository dependency. </summary>
        private readonly Mock<IPersonRepository> _mockPersonRepository;
        /// <summary> Mock for the change password token repository dependency. </summary>
        private readonly Mock<IChangePasswordRepository> _mockChangePasswordRepository;
        /// <summary> Mock for the authentication utilities dependency. </summary>
        private readonly Mock<IAuthUtilities> _mockAuthUtilities;
        /// <summary> Mock for the email service dependency. </summary>
        private readonly Mock<IEmailService> _mockEmailService;
        /// <summary> Mock for the date time provider dependency. </summary>
        private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
        /// <summary> Mock for the logger dependency. </summary>
        private readonly Mock<ILogger<AuthPasswordService>> _mockLogger;

        // SUT (System Under Test)
        /// <summary> The instance of the service being tested. </summary>
        private readonly AuthPasswordService _authPasswordService;

        // Common data for tests
        /// <summary> A fixed point in time for deterministic tests. </summary>
        private readonly DateTime _fixedUtcNow = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        /// <summary> A valid email address used in tests. </summary>
        private readonly string _validEmail = "test@example.com";
        /// <summary> A sample hashed password string. </summary>
        private readonly string _hashedPassword = "newHashedPassword";
        /// <summary> A valid password reset token string. </summary>
        private readonly string _resetToken = "validResetToken123";
        /// <summary> The ID of the default test person. </summary>
        private readonly int _personId = 1;
        /// <summary> A common PersonEntity instance used across tests. </summary>
        private readonly PersonEntity _personEntity;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthPasswordServiceTests"/> class,
        /// setting up all mock dependencies and the service under test.
        /// </summary>
        public AuthPasswordServiceTests()
        {
            _mockPersonRepository = new Mock<IPersonRepository>();
            _mockChangePasswordRepository = new Mock<IChangePasswordRepository>();
            _mockAuthUtilities = new Mock<IAuthUtilities>();
            _mockEmailService = new Mock<IEmailService>();
            _mockDateTimeProvider = new Mock<IDateTimeProvider>();
            _mockLogger = new Mock<ILogger<AuthPasswordService>>();

            // Setup fixed time provider behavior.
            _mockDateTimeProvider.Setup(dtp => dtp.GetUtcNow()).Returns(_fixedUtcNow);

            // Setup a common person entity instance used in multiple tests.
            _personEntity = new PersonEntity(
                idPerson: _personId, firstName: "Test", lastName: "User", email: _validEmail, password: "oldHash",
                createdAt: _fixedUtcNow.AddDays(-10), isAdmin: false, allNotifications: true, cropId: 1);

            // Instantiate the System Under Test (SUT) with mock objects.
            _authPasswordService = new AuthPasswordService(
                _mockPersonRepository.Object,
                _mockChangePasswordRepository.Object,
                _mockAuthUtilities.Object,
                _mockEmailService.Object,
                _mockDateTimeProvider.Object,
                _mockLogger.Object
            );
        }

        #region RequestPasswordResetAsync Tests

        [Fact]
        public async Task RequestPasswordResetAsync_ValidEmailUserExists_ReturnsSuccessAndSendsEmail()
        {
            // Arrange
            var request = new ForgotPasswordRequestDto { Email = _validEmail };
            var generatedToken = "generatedToken";
            var changePasswordEntity = new ChangePasswordEntity(0, generatedToken, _fixedUtcNow.AddMinutes(30), _fixedUtcNow, _personId);

            // Configures mock to simulate finding an existing user.
            _mockAuthUtilities.Setup(auth => auth.FindUserByEmailAsync(_validEmail))
                             .ReturnsAsync(Result<PersonEntity>.Success(_personEntity));
            // Configures mock for successful token creation.
            // Returns a Result<ChangePasswordEntity>.
            _mockChangePasswordRepository.Setup(repo => repo.Create(It.IsAny<ChangePasswordEntity>()))
                                         .ReturnsAsync(Result<ChangePasswordEntity>.Success(changePasswordEntity));
            // Configures mocks for successful email generation and sending.
            _mockEmailService.Setup(svc => svc.GeneratePasswordResetTokenBody(It.IsAny<string>(), It.IsAny<string>()))
                             .Returns("Email Body");
            // Configures SendEmailAsync mock to return Task<Result>.
            _mockEmailService.Setup(svc => svc.SendEmailAsync(_validEmail, It.IsAny<string>(), It.IsAny<string>()))
                             .Returns(Task.FromResult(Result.Success()));

            // Act
            var result = await _authPasswordService.RequestPasswordResetAsync(request);

            // Assert
            result.Should().NotBeNull();
            // Always returns Success for enumeration protection, even if internally successful.
            result.IsSuccess.Should().BeTrue();

            // Verifies that the expected internal calls occurred.
            _mockAuthUtilities.Verify(auth => auth.FindUserByEmailAsync(_validEmail), Times.Once);
            _mockChangePasswordRepository.Verify(repo => repo.Create(It.Is<ChangePasswordEntity>(
                cp => cp.PersonId == _personId && cp.ResetTokenExpiresAt > _fixedUtcNow)), Times.Once);
            // Verifies email generation uses the correct person name.
            _mockEmailService.Verify(svc => svc.GeneratePasswordResetTokenBody(_personEntity.FirstName, It.IsAny<string>()), Times.Once);
            _mockEmailService.Verify(svc => svc.SendEmailAsync(_validEmail, "Your Password Reset Code", "Email Body"), Times.Once);
        }

        [Fact]
        public async Task RequestPasswordResetAsync_UserNotFound_ReturnsSuccessAndDoesNotSendEmail()
        {
            // Arrange
            var request = new ForgotPasswordRequestDto { Email = "notfound@example.com" };

            // Configures mock to simulate that the user does not exist.
            _mockAuthUtilities.Setup(auth => auth.FindUserByEmailAsync(request.Email))
                             .ReturnsAsync(Result<PersonEntity>.Failure("User not found."));

            // Act
            var result = await _authPasswordService.RequestPasswordResetAsync(request);

            // Assert
            result.Should().NotBeNull();
            // Always returns Success for enumeration protection.
            result.IsSuccess.Should().BeTrue();

            // Verifies that internal calls related to token creation and email sending did NOT happen.
            _mockChangePasswordRepository.Verify(repo => repo.Create(It.IsAny<ChangePasswordEntity>()), Times.Never);
            _mockEmailService.Verify(svc => svc.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RequestPasswordResetAsync_InvalidEmailFormat_ReturnsSuccessAndDoesNothing()
        {
            // Arrange
            var request = new ForgotPasswordRequestDto { Email = "invalid-email" };

            // No mock setup needed for FindUserByEmailAsync for this test,
            // as the service should validate the email format *before* calling the dependency.

            // Act
            var result = await _authPasswordService.RequestPasswordResetAsync(request);

            // Assert
            result.Should().NotBeNull();
            // Always returns Success for enumeration protection.
            result.IsSuccess.Should().BeTrue();

            // Verify FindUserByEmailAsync was *never* called due to invalid format.
            _mockAuthUtilities.Verify(auth => auth.FindUserByEmailAsync(request.Email), Times.Never);
            _mockChangePasswordRepository.Verify(repo => repo.Create(It.IsAny<ChangePasswordEntity>()), Times.Never);
            _mockEmailService.Verify(svc => svc.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RequestPasswordResetAsync_TokenCreationFails_ReturnsFailure()
        {
            // Arrange
            var request = new ForgotPasswordRequestDto { Email = _validEmail };
            var dbError = "DB error saving token";

            // Configures mock to simulate finding an existing user.
            _mockAuthUtilities.Setup(auth => auth.FindUserByEmailAsync(_validEmail))
                             .ReturnsAsync(Result<PersonEntity>.Success(_personEntity));
            // Configures mock for failed token creation.
            // Returns a Result<ChangePasswordEntity>.
            _mockChangePasswordRepository.Setup(repo => repo.Create(It.IsAny<ChangePasswordEntity>()))
                                         .ReturnsAsync(Result<ChangePasswordEntity>.Failure(dbError));

            // Act
            var result = await _authPasswordService.RequestPasswordResetAsync(request);

            // Assert
            // This represents an internal server error, so it should return Failure.
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Matches the expected service error message.
            result.ErrorMessage.Should().Be("Failed to save password reset request.");

            // Verify mocks were called up to the point of failure.
            _mockAuthUtilities.Verify(auth => auth.FindUserByEmailAsync(_validEmail), Times.Once);
            _mockChangePasswordRepository.Verify(repo => repo.Create(It.IsAny<ChangePasswordEntity>()), Times.Once);
            // Verifies that the email was not sent due to the prior failure.
            _mockEmailService.Verify(svc => svc.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region ResetPasswordAsync Tests

        [Fact]
        public async Task ResetPasswordAsync_ValidRequestAndToken_ReturnsSuccess()
        {
            // Arrange
            var newPassword = "newValidPassword123";
            // Assumes password argument is already encrypted if expected by the service.
            var request = new ChangePasswordRequestDto
            {
                ChangeCode = _resetToken,
                NewPassword = newPassword,
                ConfirmPassword = newPassword
            };
            // Represents a token that is not expired relative to _fixedUtcNow.
            var validTokenEntity = new ChangePasswordEntity(
                idChangePassword: 5,
                passwordResetToken: _resetToken,
                resetTokenExpiresAt: _fixedUtcNow.AddMinutes(10),
                tokenCreatedAt: _fixedUtcNow.AddMinutes(-10),
                personId: _personId);

            // Setup: Password processing (hashing/validation) succeeds.
            _mockAuthUtilities.Setup(auth => auth.ProcessPassword(newPassword, nameof(AuthPasswordService.ResetPasswordAsync)))
                             .Returns(Result<string>.Success(_hashedPassword));
            // Setup: Token validation finds an active token.
            _mockChangePasswordRepository.Setup(repo => repo.GetActiveByTokenAsync(_resetToken))
                                         .ReturnsAsync(Result<ChangePasswordEntity>.Success(validTokenEntity));
            // Setup: Password update in the person repository succeeds.
            // Returns Task<Result<bool>>.
            _mockPersonRepository.Setup(repo => repo.UpdatePasswordAsync(_personId, _hashedPassword, _fixedUtcNow))
                                 .Returns(Task.FromResult(Result<bool>.Success(true)));
            // Setup: Token deletion (cleanup) succeeds.
            // Returns Task<Result<bool>>.
            _mockChangePasswordRepository.Setup(repo => repo.Delete(validTokenEntity.IdChangePassword))
                                         .Returns(Task.FromResult(Result<bool>.Success(true)));
            // Setup: Confirmation email dependencies succeed.
            // Mock setup needed for sending confirmation email.
            _mockPersonRepository.Setup(repo => repo.GetById(_personId))
                                 .ReturnsAsync(Result<PersonEntity>.Success(_personEntity));
            _mockEmailService.Setup(svc => svc.GeneratePasswordChangeConfirmationBody(It.IsAny<string>()))
                           .Returns("Confirmation Body");
            // Configures SendEmailAsync mock to return Task<Result>.
            _mockEmailService.Setup(svc => svc.SendEmailAsync(_validEmail, "Password Changed Successfully", "Confirmation Body"))
                           .Returns(Task.FromResult(Result.Success()));

            // Act
            var result = await _authPasswordService.ResetPasswordAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();

            // Verify critical calls occurred in the expected sequence.
            _mockAuthUtilities.Verify(auth => auth.ProcessPassword(newPassword, It.IsAny<string>()), Times.Once);
            _mockChangePasswordRepository.Verify(repo => repo.GetActiveByTokenAsync(_resetToken), Times.Once);
            _mockPersonRepository.Verify(repo => repo.UpdatePasswordAsync(_personId, _hashedPassword, _fixedUtcNow), Times.Once);
            _mockChangePasswordRepository.Verify(repo => repo.Delete(validTokenEntity.IdChangePassword), Times.Once);
            _mockEmailService.Verify(svc => svc.SendEmailAsync(_validEmail, "Password Changed Successfully", "Confirmation Body"), Times.Once);
        }

        [Theory]
        [InlineData("token", "pass1", "pass2")] // Passwords don't match scenario
        [InlineData("token", null, "pass1")]    // Missing password scenario
        [InlineData(null, "pass1", "pass1")]    // Missing token scenario
        public async Task ResetPasswordAsync_InvalidInput_ReturnsFailure(string? token, string? newPass, string? confirmPass)
        {
            // Arrange
            // Test assumes non-null assertion for properties, but handles overall null input logic.
            var request = new ChangePasswordRequestDto
            {
                ChangeCode = token!,
                NewPassword = newPass!,
                ConfirmPassword = confirmPass!
            };

            // Act
            var result = await _authPasswordService.ResetPasswordAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Checks for a non-empty error message (can be made more specific).
            result.ErrorMessage.Should().NotBeNullOrEmpty();

            // Verify no dependencies were called because input validation should fail first.
            _mockAuthUtilities.Verify(auth => auth.ProcessPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockChangePasswordRepository.Verify(repo => repo.GetActiveByTokenAsync(It.IsAny<string>()), Times.Never);
            _mockPersonRepository.Verify(repo => repo.UpdatePasswordAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
            _mockChangePasswordRepository.Verify(repo => repo.Delete(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ResetPasswordAsync_PasswordProcessingFails_ReturnsFailure()
        {
            // Arrange
            // Password that fails policy check in the mocked ProcessPassword.
            var newPassword = "newInvalidPassword";
            var request = new ChangePasswordRequestDto { ChangeCode = _resetToken, NewPassword = newPassword, ConfirmPassword = newPassword };
            var passwordError = "Password policy failed";

            // Setup: Configures ProcessPassword mock to return failure.
            _mockAuthUtilities.Setup(auth => auth.ProcessPassword(newPassword, It.IsAny<string>()))
                             .Returns(Result<string>.Failure(passwordError));

            // Act
            var result = await _authPasswordService.ResetPasswordAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Be(passwordError);

            // Verify dependencies were called appropriately.
            _mockAuthUtilities.Verify(auth => auth.ProcessPassword(newPassword, It.IsAny<string>()), Times.Once);
            // Token validation should not happen if password processing fails first.
            _mockChangePasswordRepository.Verify(repo => repo.GetActiveByTokenAsync(It.IsAny<string>()), Times.Never);
            _mockPersonRepository.Verify(repo => repo.UpdatePasswordAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task ResetPasswordAsync_InvalidToken_ReturnsFailure()
        {
            // Arrange
            var newPassword = "newValidPassword123";
            var request = new ChangePasswordRequestDto { ChangeCode = "invalidToken", NewPassword = newPassword, ConfirmPassword = newPassword };
            var tokenError = "Token not found or expired";

            // Setup: Password processing succeeds.
            _mockAuthUtilities.Setup(auth => auth.ProcessPassword(newPassword, It.IsAny<string>()))
                             .Returns(Result<string>.Success(_hashedPassword));
            // Setup: Token validation fails (token not found or expired).
            _mockChangePasswordRepository.Setup(repo => repo.GetActiveByTokenAsync(request.ChangeCode))
                                         .ReturnsAsync(Result<ChangePasswordEntity>.Failure(tokenError));

            // Act
            var result = await _authPasswordService.ResetPasswordAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Matches the expected user-facing service error message.
            result.ErrorMessage.Should().Be("Password reset code is invalid or has expired. Please request a new one.");

            // Verify dependencies were called up to the point of failure.
            _mockAuthUtilities.Verify(auth => auth.ProcessPassword(newPassword, It.IsAny<string>()), Times.Once);
            _mockChangePasswordRepository.Verify(repo => repo.GetActiveByTokenAsync(request.ChangeCode), Times.Once);
            _mockPersonRepository.Verify(repo => repo.UpdatePasswordAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }


        [Fact]
        public async Task ResetPasswordAsync_PasswordUpdateFails_ReturnsFailure()
        {
            // Arrange
            var newPassword = "newValidPassword123";
            var request = new ChangePasswordRequestDto { ChangeCode = _resetToken, NewPassword = newPassword, ConfirmPassword = newPassword };
            var validTokenEntity = new ChangePasswordEntity(5, _resetToken, _fixedUtcNow.AddMinutes(10), _fixedUtcNow.AddMinutes(-10), _personId);
            var updateError = "DB error updating password";

            // Setup: Dependencies succeed up to the point of password update.
            _mockAuthUtilities.Setup(auth => auth.ProcessPassword(newPassword, It.IsAny<string>())).Returns(Result<string>.Success(_hashedPassword));
            _mockChangePasswordRepository.Setup(repo => repo.GetActiveByTokenAsync(_resetToken)).ReturnsAsync(Result<ChangePasswordEntity>.Success(validTokenEntity));

            // Setup: Password update fails.
            // Returns Task<Result<bool>>.
            _mockPersonRepository.Setup(repo => repo.UpdatePasswordAsync(_personId, _hashedPassword, _fixedUtcNow))
                                 .Returns(Task.FromResult(Result<bool>.Failure(updateError)));

            // Act
            var result = await _authPasswordService.ResetPasswordAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Matches the expected service error message.
            result.ErrorMessage.Should().Be("Failed to update password.");

            // Verify dependencies were called up to the point of failure.
            _mockAuthUtilities.Verify(auth => auth.ProcessPassword(newPassword, It.IsAny<string>()), Times.Once);
            _mockChangePasswordRepository.Verify(repo => repo.GetActiveByTokenAsync(_resetToken), Times.Once);
            _mockPersonRepository.Verify(repo => repo.UpdatePasswordAsync(_personId, _hashedPassword, _fixedUtcNow), Times.Once);
            // Verifies that the token was not deleted because the process failed earlier.
            _mockChangePasswordRepository.Verify(repo => repo.Delete(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        // Test focuses on behavior when token cleanup fails after a successful password update.
        public async Task ResetPasswordAsync_TokenDeletionFails_StillReturnsSuccess()
        {
            // Arrange
            var newPassword = "newValidPassword123";
            var request = new ChangePasswordRequestDto { ChangeCode = _resetToken, NewPassword = newPassword, ConfirmPassword = newPassword };
            var validTokenEntity = new ChangePasswordEntity(5, _resetToken, _fixedUtcNow.AddMinutes(10), _fixedUtcNow.AddMinutes(-10), _personId);
            var deleteError = "DB error deleting token";

            // Setup: Dependencies succeed up to the point of token deletion.
            _mockAuthUtilities.Setup(auth => auth.ProcessPassword(newPassword, It.IsAny<string>())).Returns(Result<string>.Success(_hashedPassword));
            _mockChangePasswordRepository.Setup(repo => repo.GetActiveByTokenAsync(_resetToken)).ReturnsAsync(Result<ChangePasswordEntity>.Success(validTokenEntity));
            _mockPersonRepository.Setup(repo => repo.UpdatePasswordAsync(_personId, _hashedPassword, _fixedUtcNow)).Returns(Task.FromResult(Result<bool>.Success(true)));
            _mockPersonRepository.Setup(repo => repo.GetById(_personId)).ReturnsAsync(Result<PersonEntity>.Success(_personEntity)); // For email
            _mockEmailService.Setup(svc => svc.GeneratePasswordChangeConfirmationBody(It.IsAny<string>())).Returns("Confirmation Body");
            _mockEmailService.Setup(svc => svc.SendEmailAsync(_validEmail, "Password Changed Successfully", "Confirmation Body")).Returns(Task.FromResult(Result.Success()));

            // Setup: Token deletion fails.
            // Returns Task<Result<bool>>.
            _mockChangePasswordRepository.Setup(repo => repo.Delete(validTokenEntity.IdChangePassword))
                                         .Returns(Task.FromResult(Result<bool>.Failure(deleteError)));

            // Act
            var result = await _authPasswordService.ResetPasswordAsync(request);

            // Assert
            // Should still return success to the user even if token cleanup fails (best effort).
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();

            // Verify all steps were attempted, including the failed deletion.
            _mockAuthUtilities.Verify(auth => auth.ProcessPassword(newPassword, It.IsAny<string>()), Times.Once);
            _mockChangePasswordRepository.Verify(repo => repo.GetActiveByTokenAsync(_resetToken), Times.Once);
            _mockPersonRepository.Verify(repo => repo.UpdatePasswordAsync(_personId, _hashedPassword, _fixedUtcNow), Times.Once);
            // Verifies deletion was attempted.
            _mockChangePasswordRepository.Verify(repo => repo.Delete(validTokenEntity.IdChangePassword), Times.Once);
            // Verifies that the confirmation email was still sent.
            _mockEmailService.Verify(svc => svc.SendEmailAsync(_validEmail, "Password Changed Successfully", "Confirmation Body"), Times.Once);
        }

        #endregion
    }
}