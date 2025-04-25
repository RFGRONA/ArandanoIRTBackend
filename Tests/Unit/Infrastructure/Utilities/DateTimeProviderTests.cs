using ArandanoIRT_Backend.Infrastructure.Utilities;
using FluentAssertions;
using Xunit;

namespace ArandanoIRT_Backend.Tests.Unit.Infrastructure.Utilities
{
    /// <summary>
    /// Contains unit tests for the <see cref="DateTimeProvider"/> class.
    /// </summary>
    public class DateTimeProviderTests
    {
        /// <summary>
        /// The instance of the DateTimeProvider class under test.
        /// </summary>
        private readonly DateTimeProvider _dateTimeProvider = new DateTimeProvider();

        /// <summary>
        /// Tests that GetUtcNow returns a DateTime object whose Kind property is set to Utc.
        /// </summary>
        [Fact]
        public void GetUtcNow_ShouldReturnDateTimeWithUtcKind()
        {
            // Arrange
            // No specific arrangement is needed for this test.

            // Act
            // Calls the GetUtcNow method.
            var result = _dateTimeProvider.GetUtcNow();

            // Assert
            // Uses FluentAssertions to check that the Kind property of the returned DateTime is Utc.
            result.Kind.Should().Be(DateTimeKind.Utc);
        }

        /// <summary>
        /// Tests that GetUtcNow returns a DateTime value that is close to the actual current UTC time,
        /// allowing for minor execution delays.
        /// </summary>
        [Fact]
        public void GetUtcNow_ShouldReturnTimeCloseToActualUtcNow()
        {
            // Arrange
            // Gets the current UTC time just before calling the method under test.
            var expectedTime = DateTime.UtcNow;
            // Defines a small tolerance for the time comparison (e.g., 1 second).
            var tolerance = TimeSpan.FromSeconds(1);

            // Act
            // Calls the GetUtcNow method.
            var actualTime = _dateTimeProvider.GetUtcNow();

            // Assert
            // Uses FluentAssertions' BeCloseTo method to check if the returned time
            // is within the specified tolerance range of the expected time.
            actualTime.Should().BeCloseTo(expectedTime, tolerance);
        }
    }
}