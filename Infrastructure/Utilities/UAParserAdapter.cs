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
        /// Initialized once using the default regex definitions.
        /// </summary>
        private static readonly Parser _parser = Parser.GetDefault();
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
        /// Uses the UAParser library to parse the input string.
        /// Returns a <see cref="Domain.ValueObjects.ClientInfo"/> record with default values ("Unknown", etc.)
        /// if the input string is null/empty or if a parsing error occurs.
        /// </remarks>
        public Domain.ValueObjects.ClientInfo Parse(string? userAgentString)
        {
            // Handles null or whitespace input by returning a default ClientInfo object.
            if (string.IsNullOrWhiteSpace(userAgentString))
            {
                return new Domain.ValueObjects.ClientInfo(); // Return default values.
            }

            try
            {
                // Calls the UAParser library's Parse method to get structured UA info.
                // Note: This 'ClientInfo' is the type from the UAParser library.
                UAParser.ClientInfo? clientInfoResult = _parser.Parse(userAgentString);

                // Formats the version strings using the private helper method.
                string uaVersion = GetVersion(clientInfoResult?.UA?.Major, clientInfoResult?.UA?.Minor, clientInfoResult?.UA?.Patch);
                string osVersion = GetVersion(clientInfoResult?.OS?.Major, clientInfoResult?.OS?.Minor, clientInfoResult?.OS?.Patch);

                // Maps the parsed results (or defaults if null) to the application's domain ClientInfo record.
                return new Domain.ValueObjects.ClientInfo(
                    UserAgentFamily: clientInfoResult?.UA?.Family ?? "Unknown",
                    UserAgentVersion: uaVersion,
                    OSFamily: clientInfoResult?.OS?.Family ?? "Unknown OS",
                    OSVersion: osVersion,
                    DeviceFamily: clientInfoResult?.Device?.Family ?? "Unknown Device"
                );
            }
            catch (Exception ex) // Catches potential errors during UAParser execution.
            {
                _logger.LogError(ex, "Failed to parse User-Agent string: {UserAgent}", userAgentString);
                // Returns a default ClientInfo object if parsing fails.
                return new Domain.ValueObjects.ClientInfo();
            }
        }

        /// <summary>
        /// Helper method to construct a combined version string (e.g., "Major.Minor.Patch")
        /// from individual version parts, handling potentially null or empty parts.
        /// </summary>
        /// <param name="major">The major version part (nullable).</param>
        /// <param name="minor">The minor version part (nullable).</param>
        /// <param name="patch">The patch version part (nullable).</param>
        /// <returns>A formatted version string (e.g., "10.2.1", "11.5", "12", "") or an empty string if major is null/empty.</returns>
        private string GetVersion(string? major, string? minor, string? patch)
        {
            // Concatenates version parts if major version exists.
            if (!string.IsNullOrWhiteSpace(major))
            {
                // Adds minor version if available.
                if (!string.IsNullOrWhiteSpace(minor))
                {
                    // Adds patch version if available.
                    if (!string.IsNullOrWhiteSpace(patch))
                        return $"{major}.{minor}.{patch}";
                    // Returns major.minor if patch is missing.
                    return $"{major}.{minor}";
                }
                // Returns only major if minor/patch are missing.
                return major;
            }
            // Returns empty string if major version is missing.
            return string.Empty;
        }
    }
}