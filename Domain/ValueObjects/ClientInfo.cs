using System.Text;

namespace ArandanoIRT_Backend.Domain.ValueObjects
{
    /// <summary>
    /// Holds parsed information about the client (e.g., browser, OS, device) making a request,
    /// typically derived from the User-Agent string. This is an immutable record.
    /// </summary>
    /// <param name="UserAgentFamily">The family name of the user agent (e.g., "Chrome", "Firefox", "MyApp"). Defaults to "Unknown".</param>
    /// <param name="UserAgentVersion">The version string of the user agent (e.g., "123.0.0"). Defaults to empty string.</param>
    /// <param name="OSFamily">The family name of the operating system (e.g., "Windows", "Android", "iOS"). Defaults to "Unknown OS".</param>
    /// <param name="OSVersion">The version string of the operating system (e.g., "10", "13"). Defaults to empty string.</param>
    /// <param name="DeviceFamily">The family or model name of the device (e.g., "Generic PC", "iPhone", "Samsung SM-G998U"). Defaults to "Unknown Device".</param>
    public record ClientInfo(
        string UserAgentFamily = "Unknown",
        string UserAgentVersion = "",
        string OSFamily = "Unknown OS",
        string OSVersion = "",
        string DeviceFamily = "Unknown Device" // e.g., Generic PC, iPhone, Samsung SM-G998U
    )
    {
        /// <summary>
        /// Provides a user-friendly formatted string representation of the client information.
        /// Example: "Chrome 123.0.0 / Windows 10" or "MyApp / Android 13" or "iPhone".
        /// </summary>
        /// <returns>
        /// A formatted string summarizing the client information, prioritizing User Agent and OS details,
        /// falling back to Device Family, and ultimately "Unknown Device" if no information is known.
        /// </returns>
        public string ToFormattedString()
        {
            string trimmedAgentVersion = UserAgentVersion?.Trim() ?? "";
            string trimmedOSVersion = OSVersion?.Trim() ?? "";
            bool agentKnown = UserAgentFamily != "Unknown";
            bool osKnown = OSFamily != "Unknown OS";
            // Treat OSFamily "Other" as "unknown" for formatting purposes *unless* it's the only info besides device
            bool osSignificant = osKnown && OSFamily != "Other";
            bool deviceKnown = DeviceFamily != "Unknown Device";
            // Treat DeviceFamily "Other" as "unknown" for formatting *unless* it's the only info overall
            bool deviceSignificant = deviceKnown && DeviceFamily != "Other";

            var builder = new StringBuilder();

            // Append User Agent part if known
            if (agentKnown)
            {
                builder.Append(UserAgentFamily);
                if (!string.IsNullOrWhiteSpace(trimmedAgentVersion))
                {
                    builder.Append(' ').Append(trimmedAgentVersion);
                }
            }

            // Append significant OS part if known
            if (osSignificant)
            {
                if (builder.Length > 0) builder.Append(" / ");
                builder.Append(OSFamily);
                if (!string.IsNullOrWhiteSpace(trimmedOSVersion))
                {
                    builder.Append(' ').Append(trimmedOSVersion);
                }
            }

            // Append significant Device part if known
            if (deviceSignificant)
            {
                // Only add separator if Agent or Significant OS was added
                if (builder.Length > 0 && (agentKnown || osSignificant)) builder.Append(" / ");
                // Add device only if it's significant OR if nothing else was significant
                if (deviceSignificant || (!agentKnown && !osSignificant))
                {
                    builder.Append(DeviceFamily);
                }

            }

            // Fallback logic
            if (builder.Length == 0)
            {
                // If nothing significant was found, return the most specific known piece, or default
                if (agentKnown) return UserAgentFamily + (!string.IsNullOrWhiteSpace(trimmedAgentVersion) ? $" {trimmedAgentVersion}" : ""); // UA is best fallback
                if (osKnown) return OSFamily + (!string.IsNullOrWhiteSpace(trimmedOSVersion) ? $" {trimmedOSVersion}" : ""); // OS is second best
                return DeviceFamily; // Device is last resort
            }

            return builder.ToString();
        }
    }
}