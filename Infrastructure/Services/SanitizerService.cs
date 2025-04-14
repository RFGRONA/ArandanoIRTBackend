using Ganss.Xss;

namespace ArandanoIRT_Backend.Infrastructure.Services
{
    /// <summary>
    /// Provides a service to sanitize input strings by removing all HTML tags,
    /// helping to prevent Cross-Site Scripting (XSS) attacks.
    /// Uses the Ganss.Xss.HtmlSanitizer library configured to disallow all tags.
    /// </summary>
    public class SanitizerService
    {
        /// <summary>
        /// The configured HtmlSanitizer instance used internally for removing HTML tags.
        /// </summary>
        private readonly HtmlSanitizer _sanitizer;

        /// <summary>
        /// Initializes a new instance of the <see cref="SanitizerService"/> class.
        /// Creates and configures an <see cref="HtmlSanitizer"/> instance to remove all HTML tags from input strings.
        /// </summary>
        public SanitizerService()
        {
            // Creates a new HtmlSanitizer instance.
            _sanitizer = new HtmlSanitizer();
            // Configures the sanitizer to remove all HTML tags by clearing the default allowed list.
            _sanitizer.AllowedTags.Clear();
            // Note: Additional configuration (e.g., allowed attributes on non-existent tags, CSS) could be applied here if needed.
        }

        /// <summary>
        /// Sanitizes the provided input string by removing all HTML tags according to the configured policy.
        /// </summary>
        /// <param name="input">The input string that may contain HTML tags.</param>
        /// <returns>A sanitized string with all HTML tags removed. Returns the original string if input is null or already sanitized.</returns>
        public string Sanitize(string input)
        {
            // Calls the Sanitize method of the configured HtmlSanitizer instance.
            return _sanitizer.Sanitize(input);
        }
    }
}