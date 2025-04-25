using ArandanoIRT_Backend.Infrastructure.Utilities; 
using ArandanoIRT_Backend.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ArandanoIRT_Backend.Tests.Unit.Infrastructure.Utilities 
{
    /// <summary>
    /// Contains unit tests for the <see cref="UAParserAdapter"/> class.
    /// </summary>
    public class UAParserAdapterTests
    {
        /// <summary>
        /// The instance of the UAParserAdapter under test.
        /// Instantiated with a NullLogger for unit testing isolation.
        /// </summary>
        private readonly UAParserAdapter _parserAdapter = new UAParserAdapter(NullLogger<UAParserAdapter>.Instance);

        /// <summary>
        /// Tests parsing a typical User-Agent string from Chrome on Windows desktop.
        /// Verifies the extracted browser, OS, and device information, and the formatted string output.
        /// </summary>
        [Fact]
        public void Parse_WithChromeDesktopUserAgent_ShouldReturnCorrectClientInfo()
        {
            // Arrange
            var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36";
            var expected = new ClientInfo("Chrome", "105.0.0", "Windows", "10", "Other"); // UAParser might identify desktop as "Other" device
            var expectedFormatted = "Chrome 105.0.0 / Windows 10"; // Formatted string omits device "Other"

            // Act
            var result = _parserAdapter.Parse(userAgent);

            // Assert
            result.Should().BeEquivalentTo(expected);
            result.ToFormattedString().Should().Be(expectedFormatted);
        }

        /// <summary>
        /// Tests parsing a typical User-Agent string from Safari on an iPhone.
        /// Verifies the extracted browser, OS, and device information, and the formatted string output.
        /// </summary>
        [Fact]
        public void Parse_WithSafariMobileUserAgent_ShouldReturnCorrectClientInfo()
        {
            // Arrange
            var userAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 15_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.5 Mobile/15E148 Safari/604.1";
            var expected = new ClientInfo("Mobile Safari", "15.5", "iOS", "15.5", "iPhone");
            var expectedFormatted = "Mobile Safari 15.5 / iOS 15.5 / iPhone";

            // Act
            var result = _parserAdapter.Parse(userAgent);

            // Assert
            result.Should().BeEquivalentTo(expected);
            result.ToFormattedString().Should().Be(expectedFormatted);
        }

        /// <summary>
        /// Tests parsing a typical User-Agent string from Chrome on Android (Samsung device).
        /// Verifies the extracted browser, OS, and device information, and the formatted string output.
        /// </summary>
        [Fact]
        public void Parse_WithAndroidChromeUserAgent_ShouldReturnCorrectClientInfo()
        {
            // Arrange
            var userAgent = "Mozilla/5.0 (Linux; Android 11; SM-G991U) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/95.0.4638.50 Mobile Safari/537.36";
            var expected = new ClientInfo("Chrome Mobile", "95.0.4638", "Android", "11", "Samsung SM-G991U");
            var expectedFormatted = "Chrome Mobile 95.0.4638 / Android 11 / Samsung SM-G991U";

            // Act
            var result = _parserAdapter.Parse(userAgent);

            // Assert
            result.Should().BeEquivalentTo(expected);
            result.ToFormattedString().Should().Be(expectedFormatted);
        }

        /// <summary>
        /// Tests parsing a User-Agent string from a known bot (Googlebot).
        /// Verifies the extracted bot name and device type ("Spider"), and the formatted string output.
        /// </summary>
        [Fact]
        public void Parse_WithBotUserAgent_ShouldReturnBotClientInfo()
        {
            // Arrange
            var userAgent = "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";
            // UAParser identifies bots and sets specific fields.
            var expected = new ClientInfo("Googlebot", "2.1", "Other", "", "Spider");
            var expectedFormatted = "Googlebot 2.1 / Spider"; // Formatted string includes device "Spider"

            // Act
            var result = _parserAdapter.Parse(userAgent);

            // Assert
            result.Should().BeEquivalentTo(expected);
            result.ToFormattedString().Should().Be(expectedFormatted);
        }

        /// <summary>
        /// Tests parsing when the input User-Agent string is null.
        /// Expects a default ClientInfo object representing unknown information.
        /// </summary>
        [Fact]
        public void Parse_WithNullUserAgent_ShouldReturnDefaultClientInfo()
        {
            // Arrange
            string? userAgent = null;
            // Expects the default ClientInfo returned by the adapter for null/empty input.
            var expected = new ClientInfo();
            var expectedFormatted = "Unknown Device"; // Default formatted string

            // Act
            var result = _parserAdapter.Parse(userAgent);

            // Assert
            result.Should().BeEquivalentTo(expected);
            result.ToFormattedString().Should().Be(expectedFormatted);
        }

        /// <summary>
        /// Tests parsing when the input User-Agent string is empty.
        /// Expects a default ClientInfo object representing unknown information.
        /// </summary>
        [Fact]
        public void Parse_WithEmptyUserAgent_ShouldReturnDefaultClientInfo()
        {
            // Arrange
            var userAgent = "";
            var expected = new ClientInfo();
            var expectedFormatted = "Unknown Device";

            // Act
            var result = _parserAdapter.Parse(userAgent);

            // Assert
            result.Should().BeEquivalentTo(expected);
            result.ToFormattedString().Should().Be(expectedFormatted);
        }

        /// <summary>
        /// Tests parsing when the input User-Agent string consists only of whitespace.
        /// Expects a default ClientInfo object representing unknown information.
        /// </summary>
        [Fact]
        public void Parse_WithWhitespaceUserAgent_ShouldReturnDefaultClientInfo()
        {
            // Arrange
            var userAgent = "    ";
            var expected = new ClientInfo();
            var expectedFormatted = "Unknown Device";

            // Act
            var result = _parserAdapter.Parse(userAgent);

            // Assert
            result.Should().BeEquivalentTo(expected);
            result.ToFormattedString().Should().Be(expectedFormatted);
        }

        /// <summary>
        /// Tests parsing an uncommon or potentially unrecognized User-Agent string.
        /// Expects the parser to return default/fallback values ("Other") for unrecognized components.
        /// </summary>
        [Fact]
        public void Parse_WithUncommonUserAgent_ShouldReturnPartialOrDefaultInfo()
        {
            // Arrange
            var userAgent = "SomeCustomHttpClient/1.0"; // An agent string the parser might not fully recognize.
                                                        // Expects default "Other" values for families and empty strings for versions.
            var expected = new ClientInfo("Other", "", "Other", "", "Other");
            var expectedFormatted = "Other"; // Formatted string shows only "Other" when all components are default/unknown.

            // Act
            var result = _parserAdapter.Parse(userAgent);

            // Assert
            result.Should().BeEquivalentTo(expected);
            result.ToFormattedString().Should().Be(expectedFormatted);
        }
    }
}