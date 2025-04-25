using ArandanoIRT_Backend.Application.DTOs.Auth; 
using ArandanoIRT_Backend.Application.Interfaces.Utilities; 
using ArandanoIRT_Backend.Infrastructure.Utilities; 
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ArandanoIRT_Backend.Tests.Unit.Infrastructure.Utilities 
{
    /// <summary>
    /// Contains unit tests for the <see cref="SmtpEmailService"/> class.
    /// </summary>
    public class SmtpEmailServiceTests
    {
        /// <summary> Mock for the application configuration dependency. </summary>
        private readonly Mock<IConfiguration> _mockConfiguration;
        /// <summary> The instance of the email service under test. </summary>
        private readonly SmtpEmailService _emailService;

        // Constants for test data
        /// <summary> A valid email address for testing. </summary>
        private const string VALID_EMAIL = "test@valid.com";
        /// <summary> An invalid email address format for testing validation. </summary>
        private const string INVALID_EMAIL = "invalid-email";
        /// <summary> A sample user name for email body generation. </summary>
        private const string USER_NAME = "Test User";
        /// <summary> A sample crop name for email body generation. </summary>
        private const string CROP_NAME = "Test Crop";
        /// <summary> A sample access code for invitation emails. </summary>
        private const string ACCESS_CODE = "ABC-123";
        /// <summary> A sample password reset token for emails. </summary>
        private const string RESET_TOKEN = "reset-token-xyz";
        /// <summary> A sample confirmation link for emails. </summary>
        private const string CONFIRM_LINK = "http://example.com/confirm?token=abc";
        /// <summary> A sample expiration date for invitation emails. </summary>
        private readonly DateTime EXPIRY_DATE = new DateTime(2025, 12, 31, 23, 59, 00, DateTimeKind.Utc);
        /// <summary> A sample timestamp for suspicious activity emails. </summary>
        private readonly DateTime ATTEMPT_TIME = new DateTime(2025, 04, 23, 10, 30, 00, DateTimeKind.Utc);


        /// <summary>
        /// Initializes a new instance of the <see cref="SmtpEmailServiceTests"/> class.
        /// Sets up mock configuration required by the <see cref="SmtpEmailService"/>.
        /// </summary>
        public SmtpEmailServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();

            // Mocks necessary EmailSettings used internally by SmtpEmailService
            // (likely for creating an EmailSenderUtility or similar).
            SetupEmailConfigMock("test.smtp.com", "587", "testuser", "testpass", "sender@example.com");

            // Instantiates the service under test with mocked configuration and null logger.
            _emailService = new SmtpEmailService(
                _mockConfiguration.Object,
                NullLogger<SmtpEmailService>.Instance
            );
        }

        /// <summary>
        /// Helper method to configure the <see cref="IConfiguration"/> mock
        /// to return specific values for SMTP settings.
        /// </summary>
        /// <param name="host">The SMTP host value.</param>
        /// <param name="port">The SMTP port value.</param>
        /// <param name="user">The SMTP username value.</param>
        /// <param name="pass">The SMTP password value.</param>
        /// <param name="fromAddress">The default 'From' email address value.</param>
        private void SetupEmailConfigMock(string? host, string? port, string? user, string? pass, string? fromAddress)
        {
            _mockConfiguration.Setup(c => c["Smtp:Host"]).Returns(host);
            _mockConfiguration.Setup(c => c["Smtp:Port"]).Returns(port);
            _mockConfiguration.Setup(c => c["Smtp:User"]).Returns(user);
            _mockConfiguration.Setup(c => c["Smtp:Password"]).Returns(pass);
            _mockConfiguration.Setup(c => c["Smtp:From"]).Returns(fromAddress);
        }

        // --- Tests for Generate...Body Methods ---
        // These tests verify the content generation logic.

        [Fact]
        public void GeneratePasswordResetBody_ShouldContainUserNameAndToken()
        {
            // Act
            var body = _emailService.GeneratePasswordResetBody(USER_NAME, RESET_TOKEN);
            // Assert
            body.Should().Contain(USER_NAME);
            body.Should().Contain(RESET_TOKEN);
            // Checks for expected Spanish text in the template.
            body.Should().Contain("Restablecer Contraseña");
        }

        [Fact]
        public void GenerateWelcomeBody_ShouldContainUserName()
        {
            // Act
            var body = _emailService.GenerateWelcomeBody(USER_NAME);
            // Assert
            body.Should().Contain(USER_NAME);
            // Checks for expected Spanish text in the template.
            body.Should().Contain("Bienvenido");
        }

        [Fact]
        public void GenerateAccountConfirmationBody_ShouldContainUserNameAndLink()
        {
            // Act
            var body = _emailService.GenerateAccountConfirmationBody(USER_NAME, CONFIRM_LINK);
            // Assert
            body.Should().Contain(USER_NAME);
            body.Should().Contain(CONFIRM_LINK);
            // Checks for expected Spanish text in the template.
            body.Should().Contain("Confirmar Correo");
        }

        [Fact]
        public void GenerateInvitationBody_ShouldContainInviterCropCodeAndExpiry()
        {
            // Arrange
            // Formats the expiry date as expected by the template (example format).
            var expiryString = EXPIRY_DATE.ToString("D");
            // Act
            var body = _emailService.GenerateInvitationBody(USER_NAME, CROP_NAME, ACCESS_CODE, expiryString);
            // Assert
            body.Should().Contain(USER_NAME);
            body.Should().Contain(CROP_NAME);
            body.Should().Contain(ACCESS_CODE);
            body.Should().Contain(expiryString);
            // Checks for expected Spanish text in the template.
            body.Should().Contain("invitado a colaborar");
        }

        [Fact]
        public void GenerateSuspiciousActivityBody_ShouldContainUserNameTimeAndIp()
        {
            // Arrange
            var ip = "123.123.123.123";
            // Act
            var body = _emailService.GenerateSuspiciousActivityBody(USER_NAME, ATTEMPT_TIME, ip);
            // Assert
            body.Should().Contain(USER_NAME);
            body.Should().Contain(ip);
            // Performs a basic check for the date part of the timestamp.
            body.Should().Contain(ATTEMPT_TIME.ToString("yyyy-MM-dd"));
            // Checks for expected Spanish text in the template.
            body.Should().Contain("intentos fallidos de inicio de sesión");
        }

        [Fact]
        public void GeneratePasswordResetTokenBody_ShouldContainUserNameAndToken()
        {
            // Act
            var body = _emailService.GeneratePasswordResetTokenBody(USER_NAME, RESET_TOKEN);
            // Assert
            body.Should().Contain(USER_NAME);
            body.Should().Contain(RESET_TOKEN);
            // Checks for expected Spanish text in the plain text template.
            body.Should().Contain("código de restablecimiento");
        }

        [Fact]
        public void GeneratePasswordChangeConfirmationBody_ShouldContainUserName()
        {
            // Act
            var body = _emailService.GeneratePasswordChangeConfirmationBody(USER_NAME);
            // Assert
            body.Should().Contain(USER_NAME);
            // Checks that the body confirms the password change in Spanish.
            body.Should().ContainEquivalentOf("ha sido cambiada");
        }

        [Fact]
        public void GenerateHelpRequestBody_ShouldContainAllRequestDetails()
        {
            // Arrange
            var request = new HelpRequestDto
            {
                Name = "Help Seeker",
                Email = "seeker@help.com",
                CropName = "Problem Crop",
                Subject = "Urgent Issue",
                Message = "My plants are sad."
            };

            // Act
            var body = _emailService.GenerateHelpRequestBody(request);

            // Assert
            body.Should().Contain(request.Name);
            body.Should().Contain(request.Email);
            body.Should().Contain(request.CropName);
            body.Should().Contain(request.Subject);
            body.Should().Contain(request.Message);
            // Checks for expected Spanish text in the template.
            body.Should().Contain("Solicitud de Ayuda");
        }

        // --- Tests for SendEmailAsync ---
        // These tests primarily focus on input validation and configuration checks,
        // as the actual SMTP sending is not performed in unit tests.

        /// <summary>
        /// Tests that SendEmailAsync returns a failure result when provided with an invalid recipient email address.
        /// Relies on the validation performed by EmailValidatorUtility internally.
        /// </summary>
        [Fact]
        public async Task SendEmailAsync_WithInvalidEmail_ShouldReturnFailure()
        {
            // Arrange
            // Constructor sets up valid SMTP config, but email validation should fail first.

            // Act
            var result = await _emailService.SendEmailAsync(INVALID_EMAIL, "Test Subject", "Test Body");

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Verifies the expected error message for invalid email format.
            result.ErrorMessage.Should().Contain("Invalid recipient email address");
        }

        /// <summary>
        /// Tests that SendEmailAsync returns success for a valid email address when SMTP configuration is present.
        /// This test simulates the scenario where configuration is valid, and email sending *would* be attempted
        /// (though it doesn't actually send in the unit test environment).
        /// </summary>
        [Fact]
        public async Task SendEmailAsync_WithValidEmailAndConfig_ShouldReturnSuccess()
        {
            // Arrange
            // Ensures SMTP configuration is valid via the helper (called in constructor).
            SetupEmailConfigMock("test.smtp.com", "587", "testuser", "testpass", "sender@example.com");

            // Act
            // This test expects SendEmailAsync not to throw an exception due to invalid config.
            // The actual email sending logic (using SmtpClient) is not executed here.
            var result = await _emailService.SendEmailAsync(VALID_EMAIL, "Test Subject", "Test Body");

            // Assert
            result.Should().NotBeNull();
            // Assumes the service returns success if configuration and email are valid,
            // even if the underlying send operation is not performed or mocked.
            result.IsSuccess.Should().BeTrue();
            result.ErrorMessage.Should().BeNullOrEmpty();
        }

        /// <summary>
        /// Tests that SendEmailAsync (or its internal utility) throws an exception when required SMTP configuration is missing.
        /// This behavior depends on the implementation of the underlying email sending utility.
        /// </summary>
        [Fact]
        public async Task SendEmailAsync_WithValidEmailAndMissingConfig_ShouldThrowException()
        {
            // Arrange
            // Sets up configuration with a missing required value (e.g., Host).
            SetupEmailConfigMock(null, "587", "testuser", "testpass", "sender@example.com");

            // Act
            // Expects an exception (likely InvalidOperationException or ArgumentNullException)
            // to be thrown when the service is instantiated or used with missing configuration.
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                // Creates a new service instance with the faulty configuration.
                var emailServiceWithBadConfig = new SmtpEmailService(
                    _mockConfiguration.Object,
                    NullLogger<SmtpEmailService>.Instance
                );

                // Attempts to send an email, which should trigger the configuration validation error.
                await emailServiceWithBadConfig.SendEmailAsync(VALID_EMAIL, "Test Subject", "Test Body");
            });

            // Assert
            // Verifies that an exception was indeed thrown.
            exception.Should().NotBeNull();
        }
    }
}