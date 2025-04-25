using ArandanoIRT_Backend.Application.Utilities;
using FluentAssertions;
using Xunit;

namespace ArandanoIRT_Backend.Tests.Application.Utilities
{
    /// <summary>
    /// Contains unit tests for the static <see cref="EmailValidatorUtility"/> class.
    /// Focuses primarily on syntax validation which is self-contained.
    /// </summary>
    public class EmailValidatorUtilityTests
    {
        // Note on testing:
        // These tests focus on the syntax validation aspect of ValidateEmailAsync.
        // The DNS lookup part within the static utility cannot be easily unit tested
        // without refactoring EmailValidatorUtility to accept a mocked DNS query interface.

        /// <summary>
        /// Tests that ValidateEmailAsync does not fail due to syntax errors for various valid email formats.
        /// Note: This test implicitly assumes DNS checks would pass for these syntactically valid emails,
        /// as DNS mocking is not feasible for the static method as implemented.
        /// </summary>
        /// <param name="validEmail">A syntactically valid email address string.</param>
        [Theory]
        [InlineData("test@example.com")]
        [InlineData("test.name@example.co.uk")]
        [InlineData("test_123@example-domain.com")]
        [InlineData("firstname.lastname@example.com")]
        [InlineData("email@subdomain.example.com")]
        [InlineData("firstname+lastname@example.com")]
        [InlineData("1234567890@example.com")]
        [InlineData("email@example-one.com")]
        [InlineData("_______@example.com")]
        [InlineData("email@example.name")]
        [InlineData("email@example.museum")]
        [InlineData("email@example.co.jp")]
        [InlineData("firstname-lastname@example.com")]
        public async Task ValidateEmailAsync_ValidSyntaxEmails_ShouldNotFailOnSyntax(string validEmail)
        {
            // Arrange
            // No specific arrangement is needed for the syntax check part.
            // The DNS check result is ignored for this specific unit test focus.

            // Act
            // Calling the public static method.
            var result = await EmailValidatorUtility.ValidateEmailAsync(validEmail);

            // Assert
            // For this unit test, it is assumed DNS checks might pass or fail independently.
            // This is not a perfect unit test due to the static DNS dependency.
            // IsSuccess cannot be definitively asserted without mocking DNS.
            result.Should().NotBeNull();
            // Asserts that the method does not fail specifically due to syntax errors.
            // Checks that common syntax-related failure messages are not present.
            result.ErrorMessage.Should().NotContain("syntax is invalid", StringComparison.OrdinalIgnoreCase.ToString());
            result.ErrorMessage.Should().NotContain("Email is empty", StringComparison.OrdinalIgnoreCase.ToString());
            result.ErrorMessage.Should().NotContain("cannot extract domain", StringComparison.OrdinalIgnoreCase.ToString());
        }

        /// <summary>
        /// Tests that ValidateEmailAsync returns a failure result for various invalid email formats,
        /// primarily due to syntax errors or emptiness.
        /// </summary>
        /// <param name="invalidEmail">An invalid email address string (or null).</param>
        [Theory]
        [InlineData("plainaddress")]                       // Missing '@' and domain
        [InlineData("#@%^%#$@#$@#.com")]                 // Invalid characters
        [InlineData("@example.com")]                     // Missing local part
        [InlineData("Joe Smith <email@example.com>")]   // Contains name part (invalid syntax per strict definition)
        [InlineData("email.example.com")]                 // Missing '@'
        [InlineData("email@example@example.com")]       // Multiple '@' symbols
        [InlineData(".email@example.com")]               // Local part starts with a dot
        [InlineData("email.@example.com")]               // Local part ends with a dot
        [InlineData("email..email@example.com")]         // Consecutive dots in local part
        [InlineData("email@example..com")]               // Consecutive dots in domain part
        [InlineData("email@example.c")]                 // TLD too short (assuming regex requires 2+ chars)
        [InlineData("email@111.222.333.444")]             // IP address as domain (syntax potentially valid, but often disallowed)
        [InlineData("email@example.com (Joe Smith)")]   // Text after address
        [InlineData("email@-example.com")]               // Domain starts with hyphen
        [InlineData("email@example.com-")]               // Domain ends with hyphen
        [InlineData("")]                                  // Empty string
        [InlineData(" ")]                                 // Whitespace string
        [InlineData(null)]                                // Null input
        public async Task ValidateEmailAsync_InvalidSyntaxEmails_ShouldReturnFailure(string? invalidEmail)
        {
            // Arrange
            // Handles the null case explicitly within the test if necessary,
            // though the utility might handle it internally.
            if (invalidEmail == null)
            {
                // Act & Assert specifically for the null input scenario.
                // Tests the null scenario carefully using the null-forgiving operator.
                var nullResult = await EmailValidatorUtility.ValidateEmailAsync(invalidEmail!);
                nullResult.IsFailure.Should().BeTrue();
                nullResult.ErrorMessage.Should().Contain("Email is empty");
                return; // Exit test early for null case
            }

            // Act
            var result = await EmailValidatorUtility.ValidateEmailAsync(invalidEmail);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Checks that the failure message indicates a syntax, emptiness, or domain extraction issue.
            result.ErrorMessage.Should().Match(msg =>
                msg.Contains("syntax is invalid", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("Email is empty", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("cannot extract domain", StringComparison.OrdinalIgnoreCase), // Happens if parsing fails, e.g., no '@'
                "error message should indicate syntax, empty, or domain extraction failure"
            );
        }
    }
}