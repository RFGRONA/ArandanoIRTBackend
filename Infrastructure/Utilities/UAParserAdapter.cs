using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using UAParser;

namespace ArandanoIRT_Backend.Infrastructure.Utilities
{
    /// <summary>
    /// Implements the <see cref="IUserAgentParser"/> interface by adapting the UAParser library (uap-csharp)
    /// to parse User-Agent strings into the application's <see cref="Domain.ValueObjects.ClientInfo"/> structure.
    /// </summary>
    public class UAParserAdapter : IUserAgentParser
    {
        /// <summary>
        /// Thread-safe static instance of the UAParser library's main parser.
        /// </summary>
        private static readonly Parser _parser = Parser.GetDefault(); // Initialized once
        /// <summary>
        /// Logger for recording adapter activity and potential parsing errors.
        /// </summary>
        private readonly ILogger<UAParserAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="UAParserAdapter"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if logger is null.</exception>
        public UAParserAdapter(ILogger<UAParserAdapter> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Uses the UAParser library to parse the input string. Delegates mapping to a helper method.
        /// Returns default values if input is null/empty or parsing fails.
        /// </remarks>
        public Domain.ValueObjects.ClientInfo Parse(string? userAgentString)
        {
            // Handle null or whitespace input
            if (string.IsNullOrWhiteSpace(userAgentString))
            {
                // Return default domain ClientInfo value
                return new Domain.ValueObjects.ClientInfo();
            }

            try
            {
                // Parse using the UAParser library
                // Note: 'parsedInfo' is the type from the UAParser library.
                UAParser.ClientInfo? parsedInfo = _parser.Parse(userAgentString);

                // Delegate mapping to helper method
                return MapToDomainClientInfo(parsedInfo, userAgentString); // Pass original string for logging on null
            }
            catch (Exception ex) // Catch potential errors during UAParser execution.
            {
                _logger.LogError(ex, "Failed to parse User-Agent string: {UserAgent}", userAgentString);
                // Return default domain ClientInfo value on error
                return new Domain.ValueObjects.ClientInfo();
            }
        }

        /// <summary>
        /// Maps the result from the UAParser library to the application's domain ClientInfo object.
        /// </summary>
        /// <param name="parserResult">The result from UAParser.Parser.Parse().</param>
        /// <param name="originalUserAgent">Original UA string for logging purposes if result is null.</param>
        /// <returns>The application's domain ClientInfo object.</returns>
        private Domain.ValueObjects.ClientInfo MapToDomainClientInfo(UAParser.ClientInfo? parserResult, string originalUserAgent)
        {
            // Handle null result from the parser
            if (parserResult == null)
            {
                _logger.LogWarning("UAParser returned null result for User-Agent: {UserAgent}", originalUserAgent);
                return new Domain.ValueObjects.ClientInfo(); // Return default
            }

            // Extract versions using helpers that handle null UA/OS objects
            string uaVersion = GetVersion(parserResult.UA);
            string osVersion = GetVersion(parserResult.OS);

            // Extract families using null-coalescing operator
            string uaFamily = parserResult.UA?.Family ?? "Unknown";
            string osFamily = parserResult.OS?.Family ?? "Unknown OS";
            string deviceFamily = parserResult.Device?.Family ?? "Unknown Device";

            // Construct the domain ClientInfo object
            return new Domain.ValueObjects.ClientInfo(
                UserAgentFamily: uaFamily,
                UserAgentVersion: uaVersion,
                OSFamily: osFamily,
                OSVersion: osVersion,
                DeviceFamily: deviceFamily
            );
        }

        /// <summary>
        /// Gets the formatted version string from a UAParser UserAgent object.
        /// </summary>
        /// <param name="ua">The UAParser UserAgent object.</param>
        /// <returns>Formatted version string or empty string.</returns>
        private string GetVersion(UserAgent? ua)
        {
            // Delegate to the core version string builder if UA is not null
            return ua == null ? string.Empty : BuildVersionString(ua.Major, ua.Minor, ua.Patch);
        }

        /// <summary>
        /// Gets the formatted version string from a UAParser OS object.
        /// </summary>
        /// <param name="os">The UAParser OS object.</param>
        /// <returns>Formatted version string or empty string.</returns>
        private string GetVersion(OS? os)
        {
            // Delegate to the core version string builder if OS is not null
            return os == null ? string.Empty : BuildVersionString(os.Major, os.Minor, os.Patch);
        }

        /// <summary>
        /// Helper method to construct a combined version string (e.g., "Major.Minor.Patch")
        /// from individual version parts, handling potentially null or empty parts.
        /// </summary>
        /// <param name="major">The major version part (nullable).</param>
        /// <param name="minor">The minor version part (nullable).</param>
        /// <param name="patch">The patch version part (nullable).</param>
        /// <returns>A formatted version string (e.g., "10.2.1", "11.5", "12") or an empty string if major is null/empty.</returns>
        private string BuildVersionString(string? major, string? minor, string? patch) // Renamed from GetVersion
        {
            // Use string.Join for potentially cleaner concatenation (though nested ifs are also fine)
            var parts = new[] { major, minor, patch };
            var validParts = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray(); // Filter out null/empty/whitespace

            return validParts.Length > 0 ? string.Join(".", validParts) : string.Empty;
        }
    }
}