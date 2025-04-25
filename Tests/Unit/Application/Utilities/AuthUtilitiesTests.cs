using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Application.Utilities;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;


namespace ArandanoIRT_Backend.Tests.Application.Utilities
{
    /// <summary>
    /// Contains unit tests for the <see cref="AuthUtilities"/> class.
    /// </summary>
    public class AuthUtilitiesTests
    {
        // Mocks for dependencies
        /// <summary> Mock for the person repository dependency. </summary>
        private readonly Mock<IPersonRepository> _mockPersonRepository;
        /// <summary> Mock for the RSA encryption/decryption service dependency. </summary>
        private readonly Mock<IRsaService> _mockRsaService;
        /// <summary> Mock for the password hashing service dependency. </summary>
        private readonly Mock<IPasswordHasher> _mockPasswordHasher;
        /// <summary> Mock for the logger dependency. </summary>
        private readonly Mock<ILogger<AuthUtilities>> _mockLogger;

        // System Under Test (SUT)
        /// <summary> The instance of the utility class being tested. </summary>
        private readonly AuthUtilities _authUtilities;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthUtilitiesTests"/> class,
        /// setting up mocks and the system under test.
        /// </summary>
        public AuthUtilitiesTests()
        {
            _mockPersonRepository = new Mock<IPersonRepository>();
            _mockRsaService = new Mock<IRsaService>();
            _mockPasswordHasher = new Mock<IPasswordHasher>();
            // Uses the concrete type for the logger mock.
            _mockLogger = new Mock<ILogger<AuthUtilities>>();

            // Instantiate the SUT, injecting the mock objects.
            _authUtilities = new AuthUtilities(
                _mockPersonRepository.Object,
                _mockRsaService.Object,
                _mockPasswordHasher.Object,
                _mockLogger.Object
            );
        }

        #region FindUserByEmailAsync Tests

        /// <summary>
        /// Tests that FindUserByEmailAsync returns a successful result with the correct user
        /// when the email is valid and the user exists in the repository.
        /// </summary>
        [Fact]
        public async Task FindUserByEmailAsync_ValidEmailAndUserExists_ReturnsSuccessResultWithUser()
        {
            // Arrange
            var validEmail = "test@example.com";
            // Placeholder password hash.
            var expectedPerson = new PersonEntity(
                idPerson: 1,
                firstName: "Test",
                lastName: "User",
                email: validEmail,
                password: "hashed_password",
                createdAt: DateTime.UtcNow,
                isAdmin: false,
                allNotifications: true,
                cropId: 10
            );

            // Setup mock repository to return the existing user successfully.
            // Assumes usage of a Result<T>.Success static factory or similar mechanism.
            _mockPersonRepository
                .Setup(repo => repo.GetByEmailAsync(validEmail))
                .ReturnsAsync(Result<PersonEntity>.Success(expectedPerson));

            // Act
            var result = await _authUtilities.FindUserByEmailAsync(validEmail);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            // Compares object properties for equivalence.
            result.Value.Should().BeEquivalentTo(expectedPerson);
            result.ErrorMessage.Should().BeNullOrEmpty();

            // Verify repository dependency was called once.
            _mockPersonRepository.Verify(repo => repo.GetByEmailAsync(validEmail), Times.Once);
        }

        /// <summary>
        /// Tests that FindUserByEmailAsync returns a failure result
        /// when the email is valid but the user does not exist in the repository.
        /// </summary>
        [Fact]
        public async Task FindUserByEmailAsync_ValidEmailAndUserNotFound_ReturnsFailureResult()
        {
            // Arrange
            var validEmail = "nonexistent@example.com";
            // Matches the expected error message defined within AuthUtilities.
            var expectedErrorMessage = "User not found.";

            // Setup mock repository to return failure, indicating the user was not found.
            // Assumes usage of Result<T>.Failure.
            _mockPersonRepository
                .Setup(repo => repo.GetByEmailAsync(validEmail))
                .ReturnsAsync(Result<PersonEntity>.Failure(expectedErrorMessage));

            // Act
            var result = await _authUtilities.FindUserByEmailAsync(validEmail);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // No user entity should be returned on failure.
            result.Value.Should().BeNull();
            result.ErrorMessage.Should().Be(expectedErrorMessage);

            // Verify repository dependency was called once.
            _mockPersonRepository.Verify(repo => repo.GetByEmailAsync(validEmail), Times.Once);
        }

        /// <summary>
        /// Tests that FindUserByEmailAsync returns a failure result
        /// when the email format is invalid, bypassing the repository call.
        /// Note: This test relies on the behavior of the static EmailValidatorUtility.
        /// </summary>
        [Theory]
        [InlineData("invalid-email")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("test@")]
        [InlineData("@example.com")]
        public async Task FindUserByEmailAsync_InvalidEmailFormat_ReturnsFailureResult(string? invalidEmail)
        {
            // Arrange
            // No repository mock setup is needed as it should not be called for invalid input.

            // Act
            var result = await _authUtilities.FindUserByEmailAsync(invalidEmail ?? string.Empty);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result.Value.Should().BeNull();
            // Checks for the relevant error message part indicating invalid format.
            result.ErrorMessage.Should().Contain("Invalid email format");

            // Verify repository dependency was *not* called.
            _mockPersonRepository.Verify(repo => repo.GetByEmailAsync(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region ProcessPassword Tests

        /// <summary>
        /// Tests that ProcessPassword returns a successful result with the correctly hashed password
        /// when decryption and policy validation succeed.
        /// </summary>
        [Fact]
        public void ProcessPassword_ValidEncryptedPassword_ReturnsSuccessResultWithHash()
        {
            // Arrange
            var encryptedPassword = "encryptedPasswordData";
            // Represents a decrypted password that meets the length policy (e.g., >= 8 characters).
            var decryptedPassword = "validPassword123";
            var expectedHashedPassword = "hashed_validPassword123";
            var operationContext = "TestContext"; // Example context string

            // Setup mock RSA service for successful decryption.
            _mockRsaService.Setup(rsa => rsa.Decrypt(encryptedPassword)).Returns(decryptedPassword);

            // Setup mock password hasher for successful hashing.
            _mockPasswordHasher.Setup(hasher => hasher.Hash(decryptedPassword)).Returns(expectedHashedPassword);

            // Act
            var result = _authUtilities.ProcessPassword(encryptedPassword, operationContext);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(expectedHashedPassword);
            result.ErrorMessage.Should().BeNullOrEmpty();

            // Verify mock dependencies were called as expected.
            _mockRsaService.Verify(rsa => rsa.Decrypt(encryptedPassword), Times.Once);
            _mockPasswordHasher.Verify(hasher => hasher.Hash(decryptedPassword), Times.Once);
        }

        /// <summary>
        /// Tests that ProcessPassword returns a failure result
        /// when the RSA service fails to decrypt the password.
        /// </summary>
        [Fact]
        public void ProcessPassword_DecryptionFails_ReturnsFailureResult()
        {
            // Arrange
            var encryptedPassword = "corruptedEncryptedData";
            var operationContext = "TestContext";
            // Simulates an exception during decryption.
            var decryptionException = new Exception("Decryption failed!");

            // Setup mock RSA service to throw an exception during decryption.
            _mockRsaService.Setup(rsa => rsa.Decrypt(encryptedPassword)).Throws(decryptionException);

            // Act
            var result = _authUtilities.ProcessPassword(encryptedPassword, operationContext);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // No hash should be returned on failure.
            result.Value.Should().BeNull();
            // Matches the specific error message returned by AuthUtilities in this scenario.
            result.ErrorMessage.Should().Be("Invalid password format or data.");

            // Verify RSA mock was called.
            _mockRsaService.Verify(rsa => rsa.Decrypt(encryptedPassword), Times.Once);
            // Verify Hasher mock was *not* called because decryption failed first.
            _mockPasswordHasher.Verify(hasher => hasher.Hash(It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Tests that ProcessPassword returns a failure result
        /// when decryption succeeds but the password fails the length policy check.
        /// </summary>
        [Fact]
        public void ProcessPassword_PasswordPolicyFails_ReturnsFailureResult()
        {
            // Arrange
            var encryptedPassword = "encryptedShortPassword";
            // Represents a decrypted password that fails the length policy (e.g., < 8 characters).
            var decryptedPassword = "short";
            var operationContext = "TestContext";

            // Setup mock RSA service for successful decryption.
            _mockRsaService.Setup(rsa => rsa.Decrypt(encryptedPassword)).Returns(decryptedPassword);

            // Act
            var result = _authUtilities.ProcessPassword(encryptedPassword, operationContext);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // No hash should be returned on failure.
            result.Value.Should().BeNull();
            // Matches the relevant part of the error message for length policy failure.
            result.ErrorMessage.Should().Contain("must be at least 8 characters long");

            // Verify RSA mock was called.
            _mockRsaService.Verify(rsa => rsa.Decrypt(encryptedPassword), Times.Once);
            // Verify Hasher mock was *not* called because the policy check failed first.
            _mockPasswordHasher.Verify(hasher => hasher.Hash(It.IsAny<string>()), Times.Never);
        }

        #endregion
    }
}