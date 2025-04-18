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
            // Constructs the User Agent part (Family + optional Version).
            string agentPart = string.IsNullOrWhiteSpace(UserAgentVersion)
                ? UserAgentFamily
                : $"{UserAgentFamily} {UserAgentVersion}".Trim();

            // Constructs the OS part (Family + optional Version).
            string osPart = string.IsNullOrWhiteSpace(OSVersion)
                ? OSFamily
                : $"{OSFamily} {OSVersion}".Trim();

            // Prioritizes displaying Agent and/or OS if known.
            if (agentPart != "Unknown" || osPart != "Unknown OS")
            {
                // Combines Agent and OS if both are known and different from default values.
                if (agentPart != "Unknown" && osPart != "Unknown OS")
                    return $"{agentPart} / {osPart}";
                // Shows only Agent if OS is unknown.
                if (agentPart != "Unknown")
                    return agentPart;
                // Shows only OS if Agent is unknown.
                // osPart != "Unknown OS"
                return osPart;
            }
            // Falls back to Device Family if Agent and OS are unknown.
            else if (DeviceFamily != "Unknown Device")
            {
                return DeviceFamily;
            }
            // Ultimate fallback if no information is available.
            else
            {
                return "Unknown Device";
            }
        }
    }
}