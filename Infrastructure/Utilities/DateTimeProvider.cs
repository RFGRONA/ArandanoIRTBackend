using ArandanoIRT_Backend.Application.Interfaces.Utilities;

namespace ArandanoIRT_Backend.Infrastructure.Utilities
{
    /// <summary>
    /// Implements the <see cref="IDateTimeProvider"/> interface.
    /// Provides a mechanism to retrieve the current Coordinated Universal Time (UTC),
    /// primarily facilitating testability for components dependent on the current time.
    /// </summary>
    public class DateTimeProvider : IDateTimeProvider
    {
        /// <inheritdoc/>
        public DateTime GetUtcNow()
        {
            // Returns the current UTC date and time from the system clock.
            return DateTime.UtcNow;
        }
    }
}