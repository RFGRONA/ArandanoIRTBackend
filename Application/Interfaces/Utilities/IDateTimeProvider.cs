using ArandanoIRT_Backend.Domain.ValueObjects; 

namespace ArandanoIRT_Backend.Application.Interfaces.Utilities
{
    /// <summary>
    /// Provides the current time, abstracting away direct DateTime usage for testability.
    /// </summary>
    public interface IDateTimeProvider
    {
        /// <summary>
        /// Gets the current UTC DateTime.
        /// </summary>
        DateTime GetUtcNow();
    }
}