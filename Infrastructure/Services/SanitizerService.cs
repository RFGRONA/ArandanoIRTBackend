using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using Ganss.Xss;
using System.Net;

namespace ArandanoIRT_Backend.Infrastructure.Services
{
    /// <summary>
    /// Implements the <see cref="ISanitizerService"/>.
    /// This implementation uses HtmlSanitizer for XSS protection (default config)
    /// and then decodes HTML entities to preserve original characters.
    /// Note: Default HtmlSanitizer configuration allows some safe HTML tags.
    /// </summary>
    public class SanitizerService : ISanitizerService
    {
        // Use default configuration for safety
        private readonly HtmlSanitizer _sanitizer = new HtmlSanitizer();
        private readonly ILogger<SanitizerService> _logger; // Added logger

        /// <summary>
        /// Initializes a new instance of the <see cref="SanitizerService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if logger is null.</exception>
        public SanitizerService(ILogger<SanitizerService> logger) // Added logger dependency
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // No specific configuration needed here, using default _sanitizer
        }

        /// <inheritdoc/>
        public string Sanitize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input ?? string.Empty;
            }

            try
            {
                // 1. Sanitize using HtmlSanitizer (removes dangerous tags/attributes)
                //    Allows safe tags by default, may encode entities.
                string sanitizedOutput = _sanitizer.Sanitize(input);

                // 2. Decode HTML entities to get original characters back
                //    (e.g., &amp; -> &, &lt; -> <)
                return WebUtility.HtmlDecode(sanitizedOutput);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sanitization or decoding for input: {InputSnippet}", input.Length > 100 ? input.Substring(0, 100) : input);
                // Fallback: return original input or empty string in case of error?
                // Returning empty might be safer than returning potentially tainted original input.
                return string.Empty; // Or consider re-throwing or returning original based on desired policy
            }
        }
    }
}