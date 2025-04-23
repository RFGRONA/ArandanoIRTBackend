using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using Ganss.Xss;

namespace ArandanoIRT_Backend.Infrastructure.Services
{
    /// <summary>
    /// Implements the <see cref="ISanitizerService"/> using the Ganss.Xss.HtmlSanitizer library.
    /// This implementation is configured to remove ALL HTML tags from the input string.
    /// </summary>
    public class SanitizerService : ISanitizerService
    {
        /// <summary>
        /// The configured HtmlSanitizer instance used internally for removing HTML tags.
        /// It's thread-safe and configured once upon instantiation.
        /// </summary>
        private readonly HtmlSanitizer _sanitizer;

        /// <summary>
        /// Initializes a new instance of the <see cref="SanitizerService"/> class.
        /// Creates and configures an <see cref="HtmlSanitizer"/> instance to remove all HTML tags.
        /// </summary>
        public SanitizerService()
        {
            // Creates a new HtmlSanitizer instance with default options.
            _sanitizer = new HtmlSanitizer();

            // --- Configuration: Remove ALL HTML Tags ---
            // Clears the list of allowed tags, effectively disallowing all HTML elements.
            _sanitizer.AllowedTags.Clear();
            // --- End Configuration ---
        }

        /// <inheritdoc/>
        public string Sanitize(string? input)
        {
            // Returns empty string if input is null or whitespace,
            // as sanitizing these would yield empty string anyway.
            if (string.IsNullOrWhiteSpace(input))
            {
                return input ?? string.Empty; // Return original null/whitespace or empty
            }

            // Calls the Sanitize method of the configured HtmlSanitizer instance.
            // This removes all HTML tags based on the configuration in the constructor.
            return _sanitizer.Sanitize(input);
        }
    }
}