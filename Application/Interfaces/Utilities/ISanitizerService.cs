namespace ArandanoIRT_Backend.Application.Interfaces.Utilities
{
    /// <summary>
    /// Defines the contract for a service that sanitizes input strings
    /// to remove potentially harmful content, such as HTML tags.
    /// </summary>
    public interface ISanitizerService
    {
        /// <summary>
        /// Sanitizes the provided input string according to the implementation's policy.
        /// </summary>
        /// <param name="input">The potentially unsafe input string.</param>
        /// <returns>A sanitized string, safe for storage or specific uses.</returns>
        string Sanitize(string? input); // Made input nullable for flexibility
    }
}