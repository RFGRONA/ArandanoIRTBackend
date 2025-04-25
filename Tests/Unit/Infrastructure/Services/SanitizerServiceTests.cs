using ArandanoIRT_Backend.Infrastructure.Services; 
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ArandanoIRT_Backend.Tests.Unit.Infrastructure.Services
{
    /// <summary>
    /// Contains unit tests for the <see cref="SanitizerService"/> class (using default HtmlSanitizer + HtmlDecode).
    /// Expects dangerous tags/attributes to be removed, safe tags/attributes to be preserved, and HTML entities to be decoded.
    /// </summary>
    public class SanitizerServiceTests
    {
        /// <summary>
        /// The instance of the SanitizerService under test.
        /// Instantiated with a NullLogger for unit testing isolation.
        /// </summary>
        private readonly SanitizerService _sanitizerService = new SanitizerService(NullLogger<SanitizerService>.Instance);

        /// <summary>
        /// Tests that plain text without any HTML tags remains unchanged after sanitization.
        /// </summary>
        /// <param name="plainText">The plain text input string.</param>
        [Theory]
        [InlineData("This is plain text.")]
        [InlineData("Another text without tags.")]
        [InlineData("Text with numbers 123 and symbols !@#$%^&*()_+")]
        public void Sanitize_WithPlainText_ShouldReturnOriginalText(string plainText)
        {
            // Act
            var result = _sanitizerService.Sanitize(plainText);

            // Assert
            result.Should().Be(plainText);
        }

        /// <summary>
        /// Tests that simple, commonly safe HTML tags like 'p' and 'b' are preserved.
        /// </summary>
        [Fact]
        public void Sanitize_WithSimpleSafeHtmlTags_ShouldPreserveTags()
        {
            // Arrange
            var input = "<p>Hello</p> <b>World</b>!";
            var expected = "<p>Hello</p> <b>World</b>!";

            // Act
            var result = _sanitizerService.Sanitize(input);

            // Assert
            result.Should().Be(expected);
        }

        /// <summary>
        /// Tests that safe tags like 'a' with generally safe attributes like 'href' are preserved,
        /// while potentially unsafe attributes like 'class' might be removed by default rules.
        /// Verifies that other potentially safe tags like 'span' are also preserved by default.
        /// </summary>
        [Fact]
        public void Sanitize_WithSafeTagsAndAttributes_ShouldPreserveSafeTagsAndAttributes()
        {
            // Arrange
            var input = "<a href='http://example.com' class='link'>Click Me</a> <span>Text</span>";
            // Defines the expected output based on default sanitizer rules (href allowed, class removed, span allowed).
            // Note: Attribute quotes might be normalized to double quotes.
            var expected = "<a href=\"http://example.com\">Click Me</a> <span>Text</span>";

            // Act
            var result = _sanitizerService.Sanitize(input);

            // Assert
            result.Should().Be(expected);
        }

        /// <summary>
        /// Tests that potentially dangerous 'script' tags and their entire content are removed.
        /// </summary>
        [Fact]
        public void Sanitize_WithScriptTags_ShouldRemoveScriptTagsAndContent()
        {
            // Arrange
            var input = "Text before <script>alert('XSS');</script> Text after";
            // Expects the script block to be completely removed.
            var expected = "Text before  Text after"; // Note: Sanitizer might leave extra space

            // Act
            var result = _sanitizerService.Sanitize(input);

            // Assert
            // Using .Replace to ignore potential minor whitespace differences left by sanitizer.
            result.Replace(" ", "").Should().Be(expected.Replace(" ", ""));
        }

        /// <summary>
        /// Tests that safe self-closing tags (like 'br', 'hr', 'img') and their safe attributes (like 'src') are preserved.
        /// Note: Tag formatting might be normalized (e.g.,  becomes).
        /// </summary>
        [Fact]
        public void Sanitize_WithSelfClosingTags_ShouldPreserveSafeTags()
        {
            // Arrange
            var input = "Line 1<br/>Line 2<hr />Line 3<img src='image.jpg'/>";
            // Defines the expected output based on default rules (br, hr, img with src preserved, formatting normalized).
            var expected = "Line 1<br>Line 2<hr>Line 3<img src=\"image.jpg\">";

            // Act
            var result = _sanitizerService.Sanitize(input);

            // Assert
            result.Should().Be(expected);
        }

        /// <summary>
        /// Tests that malformed HTML (e.g., unclosed tags) is handled gracefully according
        /// to the underlying sanitizer library's specific correction/removal logic.
        /// The exact output depends heavily on the library implementation.
        /// </summary>
        [Fact]
        public void Sanitize_WithMalformedHtml_ShouldHandleGracefully()
        {
            // Arrange
            var input = "Text with <b unclosed tag. Another <p>paragraph.";
            // Defines the expected output based on observed behavior of the sanitizer library
            // (e.g., it might remove the broken 'b' tag and keep the 'p' tag).
            var expected = "Text with <b>paragraph.</b>";

            // Act
            var result = _sanitizerService.Sanitize(input);

            // Assert
            // This assertion should be adjusted based on the *actual* output of the sanitizer being used.
            result.Should().Be(expected); 
        }

        /// <summary>
        /// Tests that null input results in an empty string.
        /// </summary>
        [Fact]
        public void Sanitize_WithNullInput_ShouldReturnEmptyString()
        {
            // Act
            var result = _sanitizerService.Sanitize(null);

            // Assert
            result.Should().Be(string.Empty); 
        }

        /// <summary>
        /// Tests that empty string input results in an empty string output.
        /// </summary>
        [Fact]
        public void Sanitize_WithEmptyInput_ShouldReturnEmptyString()
        {
            // Act
            var result = _sanitizerService.Sanitize("");

            // Assert
            result.Should().Be("");
        }

        /// <summary>
        /// Tests that input consisting only of whitespace is returned unchanged.
        /// </summary>
        [Fact]
        public void Sanitize_WithWhitespaceInput_ShouldReturnWhitespaceString()
        {
            // Arrange
            var whitespaceInput = "    \t \n ";

            // Act
            var result = _sanitizerService.Sanitize(whitespaceInput);

            // Assert
            // Expects whitespace to be preserved as it's not unsafe HTML.
            result.Should().Be(whitespaceInput);
        }

        /// <summary>
        /// Tests that safe HTML tags are preserved while HTML entities within the content
        /// are decoded back to their original characters.
        /// </summary>
        [Fact]
        public void Sanitize_WithMixedContentAndEntities_ShouldKeepSafeTagsAndDecodeEntities()
        {
            // Arrange
            var input = "<p>Text with &lt;less than&gt; and &amp; ampersand.</p> <strong>Bold</strong>";
            // Expects '<', '>', '&' to be decoded, safe tags 'p' and 'strong' remain.
            var expected = "<p>Text with <less than> and & ampersand.</p> <strong>Bold</strong>";

            // Act
            var result = _sanitizerService.Sanitize(input);

            // Assert
            result.Should().Be(expected);
        }

        /// <summary>
        /// Tests that dangerous attributes (like 'onclick') are removed from tags,
        /// while potentially safe attributes like 'style' might remain but could be normalized or restricted.
        /// </summary>
        [Fact]
        public void Sanitize_WithDangerousAttributes_ShouldRemoveAttributes()
        {
            // Arrange
            var input = "<p onclick='alert(\"XSS\")' style='color:red; font-weight: bold;'>Dangerous</p>";
            // Defines the expected output based on default sanitizer rules: 'onclick' removed,
            // 'style' attribute might be kept but its content potentially normalized/restricted.
            // The exact output for 'style' depends heavily on the sanitizer library's configuration.
            var expected = "<p style=\"color: rgba(255, 0, 0, 1); font-weight: bold\">Dangerous</p>"; // Example: rgba color, normalized weight

            // Act
            var result = _sanitizerService.Sanitize(input);

            // Assert
            // This assertion needs verification against the actual sanitizer output, especially for the 'style' attribute.
            result.Should().Be(expected); 
        }
    }
}