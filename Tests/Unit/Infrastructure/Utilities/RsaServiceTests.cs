using FluentAssertions;
using System.Security.Cryptography; 
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ArandanoIRT_Backend.Infrastructure.Utilities;

namespace ArandanoIRT_Backend.Tests.Unit.Infrastructure.Utilities 
{
    /// <summary>
    /// Contains unit tests for the <see cref="RsaService"/> class.
    /// </summary>
    public class RsaServiceTests
    {
        /// <summary>
        /// The instance of the RsaService under test.
        /// Instantiated with a NullLogger for unit testing isolation.
        /// </summary>
        private readonly RsaService _rsaService = new RsaService(NullLogger<RsaService>.Instance);

        /// <summary>
        /// Tests that GetPublicKeyPem returns a non-empty string formatted as a standard PEM public key.
        /// </summary>
        [Fact]
        public void GetPublicKeyPem_ShouldReturnNonEmptyPemString()
        {
            // Arrange
            // The _rsaService instance is initialized in the field declaration.

            // Act
            // Calls the GetPublicKeyPem method.
            var publicKeyPem = _rsaService.GetPublicKeyPem();

            // Assert
            // Uses FluentAssertions to check that the result is not null or empty.
            publicKeyPem.Should().NotBeNullOrWhiteSpace();
            // Checks that the result starts and ends with standard PEM markers.
            publicKeyPem.Should().StartWith("-----BEGIN PUBLIC KEY-----");
            publicKeyPem.Should().EndWith("-----END PUBLIC KEY-----");
            // Performs a basic check for typical Base64 content within the PEM block.
            // "MIIB" is a common start sequence for RSA public key data in DER format (then Base64 encoded).
            publicKeyPem.Should().Contain("MIIB");
        }

        /// <summary>
        /// Tests that text encrypted using the Encrypt method can be successfully decrypted
        /// back to the original text using the Decrypt method (round trip test).
        /// </summary>
        /// <param name="plainText">The plain text to encrypt and decrypt.</param>
        [Theory]
        [InlineData("This is a test string.")]
        [InlineData("Another test with symbols !@#$%^&*()_+=")]
        [InlineData("Short")]
        [InlineData("")] // Scenario: Test empty string
        public void Encrypt_Decrypt_RoundTrip_ShouldReturnOriginalText(string plainText)
        {
            // Arrange
            // The _rsaService instance is initialized.

            // Act
            // Encrypts the plain text.
            var encryptedText = _rsaService.Encrypt(plainText);
            // Decrypts the resulting encrypted text.
            var decryptedText = _rsaService.Decrypt(encryptedText);

            // Assert
            // Uses FluentAssertions to check that the decrypted text matches the original plain text.
            decryptedText.Should().Be(plainText);
            // Ensures the encrypted text is different from the plain text, unless the input was empty.
            if (!string.IsNullOrEmpty(plainText))
            {
                encryptedText.Should().NotBe(plainText);
                // Verifies the encrypted text is a valid Base64 string.
                Action act = () => Convert.FromBase64String(encryptedText);
                act.Should().NotThrow();
            }
            else
            {
                // Encrypting an empty string might still produce non-empty Base64 output depending on padding.
                encryptedText.Should().NotBeNull();
            }
        }

        /// <summary>
        /// Tests that Decrypt throws a <see cref="CryptographicException"/> wrapping a
        /// <see cref="FormatException"/> when given input that is not valid Base64.
        /// </summary>
        [Fact]
        public void Decrypt_WithInvalidBase64_ShouldThrowCryptoExceptionWithFormatException()
        {
            // Arrange
            // Defines an invalid Base64 string.
            var invalidEncryptedText = "This is definitely not Base64!";

            // Act
            // Defines the action that calls Decrypt with invalid data.
            Action act = () => _rsaService.Decrypt(invalidEncryptedText);

            // Assert
            // Verifies that the RsaService catches the FormatException from Convert.FromBase64String
            // and wraps it in a CryptographicException.
            act.Should().Throw<CryptographicException>()
               .WithInnerExceptionExactly<FormatException>();
        }


        /// <summary>
        /// Tests that Decrypt throws a <see cref="CryptographicException"/> when given data
        /// that is a valid Base64 string but does not represent valid RSA encrypted data
        /// (e.g., wrong format, padding issues, wrong key).
        /// </summary>
        [Fact]
        public void Decrypt_WithInvalidRsaData_ShouldThrowCryptographicException()
        {
            // Arrange
            // Defines a valid Base64 string that is unlikely to be valid RSA encrypted data.
            var invalidRsaData = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Just some random bytes that are not RSA encrypted"));

            // Act
            // Defines the action that calls Decrypt with the invalid (but Base64 valid) RSA data.
            Action act = () => _rsaService.Decrypt(invalidRsaData);

            // Assert
            // Uses FluentAssertions to check that the action throws a CryptographicException.
            // This typically occurs due to padding errors or data length/format issues during the RSA decryption process.
            act.Should().Throw<CryptographicException>();
        }

        /// <summary>
        /// Tests that Encrypt throws a <see cref="CryptographicException"/> wrapping an
        /// <see cref="ArgumentNullException"/> when the input string is null.
        /// </summary>
        [Fact]
        public void Encrypt_WithNullInput_ShouldThrowCryptoExceptionWithArgumentNull()
        {
            // Arrange
            string? plainText = null;

            // Act
            // Defines the action that calls Encrypt with null input.
            // Uses null-forgiving operator as an exception is expected.
            Action act = () => _rsaService.Encrypt(plainText!);

            // Assert
            // Expects CryptographicException because RsaService should wrap the underlying ArgumentNullException.
            act.Should().Throw<CryptographicException>()
               .WithInnerExceptionExactly<ArgumentNullException>();
        }
    }
}