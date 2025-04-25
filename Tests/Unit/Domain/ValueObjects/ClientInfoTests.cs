using ArandanoIRT_Backend.Domain.ValueObjects; 
using FluentAssertions;
using Xunit;

namespace ArandanoIRT_Backend.Tests.Unit.Domain.ValueObjects
{
    /// <summary>
    /// Contains unit tests for the <see cref="ClientInfo"/> value object.
    /// Focuses on the logic of the ToFormattedString method.
    /// </summary>
    public class ClientInfoTests
    {
        /// <summary>
        /// Tests the ToFormattedString method returns the expected combined string
        /// when all properties (User Agent, OS with versions, and a significant Device) are provided.
        /// </summary>
        [Fact]
        public void ToFormattedString_ShouldReturnAgentAndOSWithVersionsAndDevice_WhenAllProvided()
        {
            // Arrange
            // Represents a significant device type that should be included.
            var clientInfo = new ClientInfo(
                UserAgentFamily: "Chrome",
                UserAgentVersion: "123.0.0",
                OSFamily: "Windows",
                OSVersion: "11",
                DeviceFamily: "Generic PC"
            );
            string expected = "Chrome 123.0.0 / Windows 11 / Generic PC";

            // Act
            string result = clientInfo.ToFormattedString();

            // Assert
            result.Should().Be(expected);
        }

        /// <summary>
        /// Tests the ToFormattedString method returns User Agent (Family and Version)
        /// and Device Family when OS details are unknown (default).
        /// </summary>
        [Fact]
        public void ToFormattedString_ShouldReturnAgentAndDevice_WhenOSIsUnknown()
        {
            // Arrange
            // Uses the default value indicating an unknown OS Family.
            // Represents a significant device type.
            var clientInfo = new ClientInfo(
                UserAgentFamily: "Firefox",
                UserAgentVersion: "120.1",
                OSFamily: "Unknown OS",
                OSVersion: "",
                DeviceFamily: "Generic PC"
            );
            string expected = "Firefox 120.1 / Generic PC";

            // Act
            string result = clientInfo.ToFormattedString();

            // Assert
            result.Should().Be(expected);
        }

        /// <summary>
        /// Tests the ToFormattedString method returns OS (Family and Version) and Device Family
        /// when User Agent details are unknown (default).
        /// </summary>
        [Fact]
        public void ToFormattedString_ShouldReturnOSAndDevice_WhenAgentIsUnknown()
        {
            // Arrange
            // Uses the default value indicating an unknown User Agent Family.
            // Represents a specific, significant device.
            var clientInfo = new ClientInfo(
                UserAgentFamily: "Unknown",
                UserAgentVersion: "",
                OSFamily: "Android",
                OSVersion: "13",
                DeviceFamily: "Samsung SM-G998U"
            );
            string expected = "Android 13 / Samsung SM-G998U";

            // Act
            string result = clientInfo.ToFormattedString();

            // Assert
            result.Should().Be(expected);
        }

        /// <summary>
        /// Tests the ToFormattedString method returns the User Agent Family and Device Family
        /// when the agent version is missing and OS details are unknown.
        /// </summary>
        [Fact]
        public void ToFormattedString_ShouldReturnAgentFamilyAndDevice_WhenAgentVersionAndOSAreMissing()
        {
            // Arrange
            // Agent version is missing.
            // Represents a significant device type.
            var clientInfo = new ClientInfo(
                UserAgentFamily: "MyApp",
                UserAgentVersion: "",
                OSFamily: "Unknown OS",
                OSVersion: "",
                DeviceFamily: "Generic Mobile"
            );
            string expected = "MyApp / Generic Mobile";

            // Act
            string result = clientInfo.ToFormattedString();

            // Assert
            result.Should().Be(expected);
        }

        /// <summary>
        /// Tests the ToFormattedString method returns the OS Family and Device Family
        /// when the OS version is missing and User Agent details are unknown.
        /// </summary>
        [Fact]
        public void ToFormattedString_ShouldReturnOSFamilyAndDevice_WhenOSVersionAndAgentAreMissing()
        {
            // Arrange
            // Represents a known OS family.
            // OS version is missing.
            // Represents a significant device type.
            var clientInfo = new ClientInfo(
                UserAgentFamily: "Unknown",
                UserAgentVersion: "",
                OSFamily: "iOS",
                OSVersion: "",
                DeviceFamily: "iPhone"
            );
            string expected = "iOS / iPhone";

            // Act
            string result = clientInfo.ToFormattedString();

            // Assert
            result.Should().Be(expected);
        }

        /// <summary>
        /// Tests the ToFormattedString method returns only the Device Family
        /// when both User Agent and OS details are unknown or default.
        /// </summary>
        [Fact]
        public void ToFormattedString_ShouldReturnDeviceFamily_WhenAgentAndOSAreUnknown()
        {
            // Arrange
            // Represents a known, specific device.
            var clientInfo = new ClientInfo(
                UserAgentFamily: "Unknown",
                UserAgentVersion: "",
                OSFamily: "Unknown OS",
                OSVersion: "",
                DeviceFamily: "MyCustomDevice"
            );
            string expected = "MyCustomDevice";

            // Act
            string result = clientInfo.ToFormattedString();

            // Assert
            result.Should().Be(expected);
        }

        /// <summary>
        /// Tests the ToFormattedString method returns the ultimate fallback "Unknown Device"
        /// when all User Agent, OS, and Device Family details are unknown or default.
        /// </summary>
        [Fact]
        public void ToFormattedString_ShouldReturnUnknownDevice_WhenAllInfoIsUnknown()
        {
            // Arrange
            // Uses the default constructor, resulting in default/unknown values.
            var clientInfo = new ClientInfo();
            string expected = "Unknown Device";

            // Act
            string result = clientInfo.ToFormattedString();

            // Assert
            result.Should().Be(expected);
        }

        /// <summary>
        /// Tests that the ToFormattedString method handles whitespace in version strings correctly by trimming them,
        /// and includes the significant Device Family.
        /// </summary>
        [Fact]
        public void ToFormattedString_ShouldTrimWhitespaceAndIncludeDevice_WhenPresent()
        {
            // Arrange
            // Versions contain leading/trailing whitespace.
            // Represents a significant device type.
            var clientInfo = new ClientInfo(
                UserAgentFamily: "Edge",
                UserAgentVersion: " 100.0 ",
                OSFamily: "Windows",
                OSVersion: " 10 ",
                DeviceFamily: "Laptop"
            );
            string expected = "Edge 100.0 / Windows 10 / Laptop"; // Expect whitespace to be trimmed

            // Act
            string result = clientInfo.ToFormattedString();

            // Assert
            result.Should().Be(expected);
        }

        /// <summary>
        /// Tests that the ToFormattedString method omits the Device Family when it is "Other"
        /// and either the User Agent or OS information is known (not default/unknown).
        /// </summary>
        [Fact]
        public void ToFormattedString_ShouldOmitDeviceFamilyOther_WhenAgentOrOSIsKnown()
        {
            // Arrange
            // Represents a non-significant device type ("Other").
            var clientInfo = new ClientInfo(
                UserAgentFamily: "Opera",
                UserAgentVersion: "99",
                OSFamily: "Linux", // OS is known
                OSVersion: "",
                DeviceFamily: "Other"
            );
            // Expect the "Other" device family to be omitted.
            string expected = "Opera 99 / Linux";

            // Act
            string result = clientInfo.ToFormattedString();

            // Assert
            result.Should().Be(expected);
        }
    }
}